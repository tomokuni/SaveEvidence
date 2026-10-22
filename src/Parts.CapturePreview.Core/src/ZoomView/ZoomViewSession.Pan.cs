
namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

/// <summary>ズームビューの状態管理クラス（パンと座標変換）。</summary>
/// <remarks>
/// パン位置の設定・grab 方式のドラッグ・ホイール回転量からのパン換算・座標変換を担う。<br/>
/// 換算則（<see cref="ScrollMath.WheelScrollUnit"/>）とクランプ（<see cref="ZoomMath.ClampOffset"/>）はコアの計算クラスが単一所有する。<br/>
/// 状態・読み取りは <c>ZoomViewSession.cs</c>、ズーム遷移は <c>ZoomViewSession.Zoom.cs</c>、<br/>
/// スクロールバーは <c>ZoomViewSession.ScrollBar.cs</c> が担う。
/// </remarks>
public sealed partial class ZoomViewSession
{
    /// <summary>パン位置を設定する（コンテンツ座標）。</summary>
    /// <param name="offset">パン位置（コンテンツ座標）。</param>
    /// <returns>UI が適用する効果（変化した場合のみ再描画）。</returns>
    /// <remarks>スクロール範囲へクランプされる。 クランプ結果が現在値と同じ場合は何もしない（不要な再描画・通知を避ける）。</remarks>
    public ZoomFeedback SetOffset(EsVector offset)
    {
        var clamped = ClampOffsetForCurrentState(offset);
        if (clamped == _offset)
        {
            return default;
        }

        _offset = clamped;
        RaiseViewportChanged();
        return new ZoomFeedback { ShouldRedraw = true };
    }

    /// <summary>パンする（コンテンツ座標の移動量）。</summary>
    /// <param name="delta">コンテンツ座標の移動量。</param>
    /// <returns>UI が適用する効果（変化した場合のみ再描画）。</returns>
    public ZoomFeedback PanBy(EsVector delta)
        => SetOffset(new EsVector(_offset.X + delta.X, _offset.Y + delta.Y));

    /// <summary>ホイールの回転量でパンする（回転量から移動量への換算を単一所有する）。</summary>
    /// <param name="delta">ホイールの回転量（X = 横 / Y = 縦。 1 ノッチ = 1.0）。</param>
    /// <param name="shift">Shift 併用かどうか（縦回転を横パンとして扱う）。</param>
    /// <returns>UI が適用する効果（変化した場合のみ再描画）。</returns>
    /// <remarks>
    /// 移動量は DIP で計算し、実効倍率で除してコンテンツ座標へ変換する（ズーム中も画面上の移動量が一定になる）。<br/>
    /// 本コントロール上でホイールが発生した場合と、ホストが他のコントロール（選択オーバーレイ等）で受けた<br/>
    /// ホイールを転送する場合の双方が本メソッドを使用する（換算則の二重所有を作らない）。
    /// </remarks>
    public ZoomFeedback PanByWheel(EsVector delta, bool shift)
    {
        var dx = shift ? delta.Y * ScrollMath.WheelScrollUnit : delta.X * ScrollMath.WheelScrollUnit;
        var dy = shift ? 0 : delta.Y * ScrollMath.WheelScrollUnit;
        var scale = EffectiveScale;
        return PanBy(new EsVector(dx / scale, dy / scale));
    }

    /// <summary>ドラッグによるパンを開始する（grab 方式）。</summary>
    /// <param name="position">ポインタ位置（ビューポート座標）。</param>
    /// <returns>UI が適用する効果（ポインタのキャプチャ）。</returns>
    /// <remarks>掴んだ位置のコンテンツ座標を保存し、以降の <see cref="Move"/> でその位置をポインタへ追従させる。</remarks>
    public ZoomFeedback Press(EsPoint position)
    {
        _isPanning = true;
        _grabSourcePos = ClientToSource(position);
        return new ZoomFeedback { ShouldCapturePointer = true };
    }

    /// <summary>ドラッグ中は掴んだ位置がポインタに追従するようパンする。</summary>
    /// <param name="position">ポインタ位置（ビューポート座標）。</param>
    /// <returns>UI が適用する効果（変化した場合のみ再描画）。</returns>
    /// <remarks>
    /// 描画位置 = スケール × (コンテンツ − オフセット) がポインタ位置と一致するため、<br/>
    /// <c>オフセット = 掴んだコンテンツ座標 − ポインタ位置 / 実効スケール</c> となる（クランプは <see cref="SetOffset"/> が行う）。
    /// </remarks>
    public ZoomFeedback Move(EsPoint position)
    {
        if (!_isPanning)
        {
            return default;
        }

        var scale = EffectiveScale;
        return SetOffset(new EsVector(
            _grabSourcePos.X - position.X / scale,
            _grabSourcePos.Y - position.Y / scale));
    }

    /// <summary>ドラッグによるパンを終了する。</summary>
    /// <returns>UI が適用する効果（ポインタの解放）。</returns>
    public ZoomFeedback Release()
    {
        if (!_isPanning)
        {
            return default;
        }

        _isPanning = false;
        return new ZoomFeedback { ShouldReleasePointer = true };
    }

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（オフセット基準）。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>コンテンツ座標（元画像ピクセル）。</returns>
    /// <remarks>中央寄せの原点補正は含まない（含む場合は <see cref="ClientToDisplayedSource"/> を使用する）。</remarks>
    public EsPoint ClientToSource(EsPoint viewportPos)
        => ZoomMath.ClientToSource(viewportPos, _offset, EffectiveScale);

    /// <summary>ビューポート座標をコンテンツ座標（元画像ピクセル）へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="viewportPos">ビューポート座標（DIP）。</param>
    /// <returns>コンテンツ座標（元画像ピクセル）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。</remarks>
    public EsPoint ClientToDisplayedSource(EsPoint viewportPos)
        => ZoomMath.ClientToDisplayedSource(viewportPos, ImageOrigin, EffectiveScale);

    /// <summary>コンテンツ座標（元画像ピクセル）をビューポート座標へ変換する（画像表示矩形の原点基準）。</summary>
    /// <param name="contentPos">コンテンツ座標（元画像ピクセル）。</param>
    /// <returns>ビューポート座標（DIP）。</returns>
    /// <remarks>Fit 中央寄せの原点補正を含む。</remarks>
    public EsPoint DisplayedSourceToClient(EsPoint contentPos)
        => ZoomMath.DisplayedSourceToClient(contentPos, ImageOrigin, EffectiveScale);

    /// <summary>現在の状態に応じてオフセットをスクロール範囲内へクランプする。</summary>
    /// <param name="offset">クランプするオフセット（コンテンツ座標）。</param>
    /// <returns>クランプ後のオフセット。 Fit 中または画像未設定時は原点。</returns>
    /// <remarks>Fit 中（内部スケールが 0）と画像未設定時はパンできないため原点を返す（ゼロ除算も避ける）。</remarks>
    private EsVector ClampOffsetForCurrentState(EsVector offset)
        => !_hasSource || _zoomPercent <= 0
            ? default
            : ZoomMath.ClampOffset(offset, Scale, _sourceSize, _viewportSize);
}
