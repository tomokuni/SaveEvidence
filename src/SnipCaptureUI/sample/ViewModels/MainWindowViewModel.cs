using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.AvaloniaUI.SnipCaptureUI;

namespace SnipCaptureUI.Sample.ViewModels;

/// <summary>メインウィンドウのビューモデル。</summary>
/// <remarks>
/// SnipCapture のキャプチャ結果（画像・切り出し範囲）と表示用の説明テキスト、操作ログを保持する。<br/>
/// SnipCapture の呼び出し（キャプチャ開始・Crop 開始）は View（コードビハインド）が行い、<br/>
/// 本クラスは結果の受け取りと表示状態の管理を担う。<br/>
/// </remarks>
public sealed class MainWindowViewModel : ObservableObject
{
    private string _statusText = "キャプチャモードを選択して開始してください。";
    private Bitmap? _capturedImage;
    private string _resultText = "";

    /// <summary>操作ガイドと結果の表示テキスト。</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>キャプチャ結果の画像（null = 未キャプチャ）。</summary>
    public Bitmap? CapturedImage
    {
        get => _capturedImage;
        set => SetProperty(ref _capturedImage, value);
    }

    /// <summary>キャプチャ結果の説明テキスト（領域・サイズ等）。</summary>
    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    /// <summary>ルーペを表示するかどうか（AreaSelect に反映）。</summary>
    public bool LoupeVisible { get; set; } = true;

    /// <summary>ルーペの拡大倍率（AreaSelect に反映）。</summary>
    public int LoupeZoomLevel { get; set; } = 4;

    /// <summary>キャプチャ結果を更新する（画像とテキストの両方）。</summary>
    /// <param name="result">キャプチャ結果。</param>
    public void UpdateResult(CaptureResult? result)
    {
        if (result?.Image is { } image && result.SourceBounds is { } bounds)
        {
            // キャプチャ結果（UnifiedBitmap）を表示用 Bitmap へ変換する（表示境界）。
            CapturedImage = image.ToAvaloniaBitmap();
            ResultText = $"領域: X={bounds.X}, Y={bounds.Y}, 幅={bounds.Width}, 高さ={bounds.Height}（仮想画面座標）";
            StatusText = "キャプチャが完了しました。";
        }
        else
        {
            ResultText = "キャンセルされました（0x0 の空結果）。";
            StatusText = "キャプチャはキャンセルされました。";
        }
    }

    /// <summary>キャプチャ結果のテキストのみを更新する（画像は <c>CaptureCompleted</c> イベントで更新済み）。</summary>
    /// <param name="result">キャプチャ結果。</param>
    /// <remarks>
    /// <c>CaptureCompleted</c>（同期イベント）で画像変更を行い、await 完了後はテキストのみ更新することで、
    /// 「画像変更 → オーナー再表示」の順序保証を確認できるようにする。<br/>
    /// </remarks>
    public void UpdateResultText(CaptureResult? result)
    {
        if (result?.Image is { } image && result.SourceBounds is { } bounds)
        {
            ResultText = $"領域: X={bounds.X}, Y={bounds.Y}, 幅={bounds.Width}, 高さ={bounds.Height}（仮想画面座標）";
            StatusText = "キャプチャが完了しました。";
        }
        else
        {
            ResultText = "キャンセルされました（0x0 の空結果）。";
            StatusText = "キャプチャはキャンセルされました。";
        }
    }

    /// <summary>切出し（Crop）の結果を更新する。</summary>
    /// <param name="rect">切り出し範囲（元画像上の論理座標）。null はキャンセル。</param>
    public void UpdateCropResult(Avalonia.PixelRect? rect)
    {
        if (rect is { } r && r.Width > 0 && r.Height > 0)
        {
            ResultText = $"切り出し範囲: X={r.X}, Y={r.Y}, 幅={r.Width}, 高さ={r.Height}（元画像ピクセル）";
            StatusText = "切出しが完了しました。";
        }
        else
        {
            ResultText = "切出しはキャンセルされました（0x0 の空結果）。";
            StatusText = "切出しはキャンセルされました。";
        }
    }

    /// <summary>操作ログの行一覧。</summary>
    public ObservableCollection<string> LogLines { get; } = [];

    /// <summary>操作ログを追加する（時分秒ミリ秒付き）。</summary>
    /// <param name="message">ログ本文。</param>
    public void AddLog(string message)
        => LogLines.Add($"{DateTime.Now:HH:mm:ss.fff} {message}");
}
