using Avalonia.Controls;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Crop;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Controls;

/// <summary>キャプチャページの画像の 4 辺・4 隅へ影を描く重ね合わせ（Avalonia 実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（画像の外側へ影を置くだけの重ね合わせ）。<br/>
/// <b>描画は <c>BoxShadow</c> に任せる</b>: 4 辺と 4 隅をぼかし付きで正しく描けるため、本クラスは<br/>
/// 画像の表示矩形へ<b>背景を持たない枠</b>を合わせるだけにする（影の値は <see cref="ShadowFactory"/> が<br/>
/// <see cref="EsUtil.SaveEvidence.App.Core.Services.ShadowPlan"/> から作る）。<br/>
/// <b>画像の上へ重ねてよい理由</b>: 枠は背景を持たないため、影は枠の外側にだけ掛かる。<br/>
/// <b>追従</b>: ズーム・パン・ウィンドウの大きさの変更で画像の表示矩形が変わるため、<br/>
/// <see cref="CropViewControl.ViewportChanged"/> を購読して位置と大きさを更新する。
/// </remarks>
internal sealed class ImageShadowOverlay : Canvas
{
    /// <summary>影を描く枠（画像の表示矩形へ合わせる）。</summary>
    private readonly Border _shadow = new()
    {
        BoxShadow = ShadowFactory.Create(),
        IsHitTestVisible = false,
    };

    /// <summary>影を落とす画像の表示矩形を提供するコントロール。</summary>
    private CropViewControl? _crop;

    /// <summary>重ね合わせを初期化する（マウス操作は下のコントロールへ通す）。</summary>
    public ImageShadowOverlay()
    {
        IsHitTestVisible = false;
        Children.Add(_shadow);
    }

    /// <summary>影を落とす対象のコントロールを設定する。</summary>
    /// <param name="crop">画像を表示するコントロール。</param>
    public void Attach(CropViewControl crop)
    {
        ArgumentNullException.ThrowIfNull(crop);

        _crop = crop;
        crop.ViewportChanged += (_, _) => Update();
        Update();
    }

    /// <summary>画像の表示矩形へ枠を合わせる。</summary>
    private void Update()
    {
        if (_crop is null)
        {
            return;
        }

        var image = _crop.ImageRectViewport();
        if (image.Width <= 0 || image.Height <= 0)
        {
            _shadow.IsVisible = false;
            return;
        }

        _shadow.IsVisible = true;
        SetLeft(_shadow, image.X);
        SetTop(_shadow, image.Y);
        _shadow.Width = image.Width;
        _shadow.Height = image.Height;
    }
}
