using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.SaveEvidence.Parts.LineDetection.Sample.ViewModels;

namespace EsUtil.SaveEvidence.Parts.LineDetection.Sample.Views;

/// <summary>メインウィンドウ。 ナビゲーションで選択されたページ（線分検出・データ検証・データ作成）を表示する。</summary>
/// <remarks>
/// ナビゲーション（左ペイン）とページ切替（<see cref="DetectPage"/> / <see cref="VerifyPage"/> / <see cref="CreatePage"/>）を統括する。<br/>
/// ページごとの状態は各ページの ViewModel が独立して保持する（ページ間で共有しない）。<br/>
/// 起動時に「線分検出」ページへ初期画像（二重矩形）を設定する。
/// </remarks>
public partial class MainWindow : Window
{
    /// <summary>ナビゲーションと各ページの ViewModel を統括する。</summary>
    private readonly MainWindowViewModel _vm = new();

    /// <summary>MainWindow の新しいインスタンスを初期化する。</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // 初期画像（検出の基準となる二重矩形）を表示する。
        // Crop のための辺確定のロジック研究用に、起動直後から検出線分が枠の境界と一致するかを確認できるようにする
        Opened += async (_, _) =>
        {
            await Task.Delay(100);
            _vm.DetectPage.SetPastedImage(CreateInitialWindowImage());
        };
    }

    /// <summary>初期表示用の検証画像（外枠・内枠の二重矩形）を生成する。</summary>
    /// <returns>生成した Bitmap（900×640・白背景に黒い二重矩形）。</returns>
    /// <remarks>
    /// Crop のための辺確定のロジック研究用に、起動時に検出の基準となる画像を表示する。<br/>
    /// 外枠 (50,40)-(850,600)・内枠 (100,90)-(800,550) の線幅 4px の矩形を描画する。<br/>
    /// 検出線分が対象座標で矩形の境界（枠線の外側ピクセル: X=48 / X=852 / Y=38 / Y=602 等）と
    /// 一致するかを確認できる。
    /// </remarks>
    private static Bitmap CreateInitialWindowImage()
    {
        const int width = 900;
        const int height = 640;
        var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using var ctx = rtb.CreateDrawingContext();
        ctx.FillRectangle(Brushes.White, new Rect(0, 0, width, height));
        var pen = new Pen(Brushes.Black, 4);

        // 外枠 (50,40)-(850,600)
        ctx.DrawRectangle(pen, new Rect(50, 40, 800, 560));

        // 内枠 (100,90)-(800,550)
        ctx.DrawRectangle(pen, new Rect(100, 90, 700, 460));

        return rtb;
    }
}
