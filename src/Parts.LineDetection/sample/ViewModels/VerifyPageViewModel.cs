using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using EsUtil.Platform.Core;

namespace EsUtil.SaveEvidence.Parts.LineDetection.Sample.ViewModels;

/// <summary>「データ検証」ページの ViewModel。 選択した dataset 画像を検出し、正解データ（GroundTruth）と照合する。</summary>
/// <remarks>
/// 線分検出ページとは独立した <see cref="DetectionContext"/> を保持し、検出結果・ルーペ・ポインタ状態を専有する。<br/>
/// ドロップダウンで選択した画像 1 件を検出して正解と照合し、<see cref="VerifyResults"/> に結果を反映する。<br/>
/// 全画像の一括検証は「データ全検証」ページ（<see cref="VerifyAllPageViewModel"/>）が担当する。
/// </remarks>
public sealed partial class VerifyPageViewModel : ObservableObject
{
    /// <summary>ビューモデルが保持する独立した検出コンテキスト。</summary>
    public DetectionContext Context { get; }

    /// <summary>検証レポート（一致/不一致・差分）の表示状態。</summary>
    public static bool IsDatasetReportVisible => true;

    /// <summary>検出結果（検出結果・選択中・垂直線分・水平線分）の表示状態。</summary>
    /// <remarks>単一検証では常に表示する。</remarks>
    public static bool IsDetectionResultVisible => true;

    /// <summary>dataset の画像一覧（ドロップダウン用）。</summary>
    public IReadOnlyList<DatasetEntry> DatasetItems { get; private set; } = [];

    /// <summary>選択中の dataset エントリ。</summary>
    [ObservableProperty]
    private DatasetEntry? _selectedDatasetItem;

    /// <summary>検証結果の一覧（画像名 + 一致/不一致 + 4 辺の詳細）。</summary>
    public ObservableCollection<VerifyResultItem> VerifyResults { get; } = [];

    /// <summary>VerifyPageViewModel を初期化する。</summary>
    public VerifyPageViewModel()
    {
        Context = new DetectionContext
        {
            SelectedDetector = new LineDetectorOption(new LogicLineDetector())
        };
        DatasetItems = DatasetVerifier.LoadDatasetItems();

        // 初期選択を Initial_window とする（大文字小文字を無視して照合。 見つからない場合は未選択のまま）
        SelectedDatasetItem = DatasetItems.FirstOrDefault(
            e => e.Name.Equals(InitialDatasetName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>初期選択するデータセット名。</summary>
    private const string InitialDatasetName = "Initial_window";

    /// <summary>選択中の dataset エントリが変わったとき、その画像を表示して検出・一致確認を行う。</summary>
    /// <param name="value">選択されたエントリ。</param>
    /// <remarks>
    /// 選択した画像を <see cref="DetectionContext.Image"/> に設定して検出し、JSON との一致・不一致を
    /// <see cref="VerifyResults"/> に反映する。 画像が無い場合は何もしない。
    /// </remarks>
    partial void OnSelectedDatasetItemChanged(DatasetEntry? value)
    {
        if (value is null)
        {
            return;
        }

        try
        {
            // 選択画像を表示（前の画像を破棄）
            Context.Image?.Dispose();
            Context.Image = new Bitmap(value.PngPath);
            Context.Detect();

            // 選択画像 1 件分のみを検証結果として表示する
            VerifyResults.Clear();
            if (Context.SelectedDetector is { } detector)
            {
                VerifyResults.Add(DatasetVerifier.VerifyImage(value, detector.Detector));
            }

            Context.StatusText = $"dataset: {value.Name} を検証";
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // ステータスへ理由を出し、診断用に記録する（アプリを止めない）
            System.Diagnostics.Debug.WriteLine($"dataset 読み込み失敗: {value.Name}（{ex.Message}）");
            Context.StatusText = $"dataset 読み込みエラー: {value.Name}（{ex.Message}）";
        }
    }

    /// <summary>検証ページの dataset 一覧を再読み込みする。</summary>
    /// <remarks>データ作成ページで保存された後に呼び出し、ドロップダウンへ反映する。</remarks>
    public void ReloadDatasetItems()
    {
        DatasetItems = DatasetVerifier.LoadDatasetItems();

        // 選択中のエントリが一覧から消えた場合は解除する
        if (SelectedDatasetItem is { } sel && !DatasetItems.Contains(sel))
        {
            SelectedDatasetItem = null;
        }

        OnPropertyChanged(nameof(DatasetItems));
    }
}
