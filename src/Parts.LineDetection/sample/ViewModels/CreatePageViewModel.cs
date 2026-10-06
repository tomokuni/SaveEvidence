using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Native.Win;
using EsUtil.SaveEvidence.Parts.LineDetection.Sample.Models;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

namespace EsUtil.SaveEvidence.Parts.LineDetection.Sample.ViewModels;

/// <summary>
/// 「データ作成」ページの ViewModel。 評価用データ作成（Marquee キャプチャ → 正解ウィンドウ選択 → 保存）を統括する。
/// </summary>
/// <remarks>
/// キャプチャの開始（Marquee）は UI スレッドから行う必要があるため、<see cref="Views.MainWindow"/> の
/// コードビハインドが CaptureSessionControl を呼び、結果（画像 + 領域）を <see cref="ApplyCaptureResult"/> で受け取る。<br/>
/// 領域内ウィンドウ情報の取得（Win32 の <see cref="IElementFinder.ListWindows"/>）と正解選択・保存を本クラスが担う。<br/>
/// 保存が完了すると <see cref="DatasetSaved"/> を発火する（親 ViewModel が一覧を再読み込みするために使用）。
/// </remarks>
public sealed partial class CreatePageViewModel : ObservableObject
{
    /// <summary>保存先ディレクトリ名（実行ファイル基準）。</summary>
    private const string DataDirectoryName = "dataset";

    /// <summary>正解データの JSON 出力オプション（毎回生成しない）。</summary>
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    /// <summary>データセット保存が完了したときに発火するイベント（ファイルパスを通知）。</summary>
    public event EventHandler<DatasetSavedEventArgs>? DatasetSaved;

    /// <summary>表示中のキャプチャ画像。</summary>
    [ObservableProperty]
    private Bitmap? _capturedImage;

    /// <summary>キャプチャ画像があるかどうか（保存ボタンの活性化）。</summary>
    public bool CanSave => CapturedImage is not null;

    /// <summary>キャプチャ画像の変更を保存可否へ反映する。</summary>
    partial void OnCapturedImageChanged(Bitmap? value) => OnPropertyChanged(nameof(CanSave));

    /// <summary>キャプチャ領域（仮想画面座標）。</summary>
    public PixelRect? CaptureBounds { get; private set; }

    /// <summary>ステータスバーの表示テキスト。</summary>
    [ObservableProperty]
    private string _statusText = "「キャプチャ開始」でスクリーンから Marquee します。";

    /// <summary>最後に保存したファイルのパス（表示用）。</summary>
    [ObservableProperty]
    private string? _lastSavedPath;

    /// <summary>領域内ウィンドウの一覧（正解選択用）。</summary>
    public ObservableCollection<WindowInfoItem> WindowInfos { get; } = [];

    /// <summary>選択中（正解）のウィンドウ件数。</summary>
    public int SelectedCount => WindowInfos.Count(w => w.IsSelected);

    /// <summary>キャプチャ結果（画像 + 領域）を反映し、領域内ウィンドウを列挙する。</summary>
    /// <param name="result">キャプチャ結果。 null・画像なしの場合は何もしない。</param>
    /// <remarks>
    /// 1. キャプチャ画像と領域（<see cref="CaptureBounds"/>）を保存する<br/>
    /// 2. <see cref="IElementFinder.ListWindows"/>（Win32 の EnumWindows・Z オーダー順）で現在のウィンドウ一覧を取得する<br/>
    /// 3. キャプチャ領域と交差するウィンドウを画像ローカル座標へ変換して一覧表示する<br/>
    /// 4. 領域外のウィンドウは表示しない（評価対象が領域内のウィンドウのため）
    /// </remarks>
    public void ApplyCaptureResult(CaptureResult? result)
    {
        if (result?.Image is not { } image || result.SourceBounds is not { } sourceBounds)
        {
            StatusText = "キャプチャはキャンセルされました。";
            return;
        }

        // キャプチャ結果（コア型）を表示用の Avalonia 型へ変換する（表示境界で 1 回だけ行う）
        var bounds = sourceBounds.ToAvaloniaPixelRect();
        CapturedImage = image.ToAvaloniaBitmap();
        CaptureBounds = bounds;
        WindowInfos.Clear();

        var finder = new WinElementFinder();
        foreach (var window in finder.ListWindows())
        {
            // 検出結果の矩形は UI 非依存のコア型のため、表示境界で Avalonia 型へ変換する
            var windowBounds = window.Bounds.ToAvaloniaPixelRect();
            // キャプチャ領域と交差するウィンドウのみ対象（画像ローカル座標へ変換）
            if (!Intersects(bounds, windowBounds))
            {
                continue;
            }

            var local = ToLocal(bounds, windowBounds);
            WindowInfos.Add(new WindowInfoItem(local, window.Title ?? "", window.ClassName ?? "", OnSelectionChanged));
        }

        StatusText = $"キャプチャ完了: {bounds.Width}×{bounds.Height} 領域内に {WindowInfos.Count} 件のウィンドウを検出。正解を選択してください。";
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>選択状態の変更を反映する（チェックボックス変更時に呼ばれる）。</summary>
    public void OnSelectionChanged()
        => OnPropertyChanged(nameof(SelectedCount));

    /// <summary>選択されたウィンドウを正解データとして、画像（PNG）とウィンドウ情報（JSON）を保存する。</summary>
    /// <remarks>
    /// 1. 保存先ディレクトリ（実行ファイル基準の <c>dataset/</c>）を作成する<br/>
    /// 2. 画像を <c>{タイムスタンプ}.png</c> で保存する<br/>
    /// 3. 選択されたウィンドウを <see cref="GroundTruthData"/> に変換し、<c>{タイムスタンプ}.json</c> で保存する<br/>
    /// 4. JSON は検証側が正解データとして読み込める形式
    /// </remarks>
    [RelayCommand]
    private void Save()
    {
        if (CapturedImage is null || CaptureBounds is null)
        {
            StatusText = "キャプチャ画像がありません。";
            return;
        }

        var selected = WindowInfos.Where(w => w.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusText = "正解ウィンドウが選択されていません。";
            return;
        }

        var dir = Path.Combine(AppContext.BaseDirectory, DataDirectoryName);
        Directory.CreateDirectory(dir);
        var baseName = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        var pngPath = Path.Combine(dir, $"{baseName}.png");
        var jsonPath = Path.Combine(dir, $"{baseName}.json");

        CapturedImage.Save(pngPath, PngBitmapEncoderOptions.Default);

        var data = new GroundTruthData(
            ClosedIntervalRect.FromPixelRect(CaptureBounds.Value),
            [.. selected.Select(w => new GroundTruthWindow(
                ClosedIntervalRect.FromPixelRect(w.Bounds), w.Title, w.ClassName))]);
        var json = JsonSerializer.Serialize(data, s_jsonOptions);
        File.WriteAllText(jsonPath, json, new System.Text.UTF8Encoding(false));

        LastSavedPath = jsonPath;
        StatusText = $"保存しました: {jsonPath}（正解 {selected.Count} 件）";

        // 保存完了を親へ通知し、検証ページのドロップダウン一覧を再読み込みさせる
        DatasetSaved?.Invoke(this, new DatasetSavedEventArgs(jsonPath));
    }

    /// <summary>2 つの矩形が交差するかどうかを判定する。</summary>
    private static bool Intersects(PixelRect a, PixelRect b)
        => a.X < b.X + b.Width && b.X < a.X + a.Width
            && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    /// <summary>画面物理座標の矩形をキャプチャ領域基準（画像ローカル座標）へ変換する。</summary>
    private static PixelRect ToLocal(PixelRect captureBounds, PixelRect screenRect)
    {
        var x = screenRect.X - captureBounds.X;
        var y = screenRect.Y - captureBounds.Y;
        return new PixelRect(x, y, screenRect.Width, screenRect.Height);
    }
}
