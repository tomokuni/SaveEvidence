using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests;

/// <summary><see cref="ZoomViewSession"/> のテスト（状態・判断・キャッシュ・契約）。</summary>
/// <remarks>
/// UI 層が持っていた判断（Fit の表現・実効ズーム率・可否・キャッシュ無効化・遷移適用・パン・スクロールバー）を<br/>
/// コアが単一所有するため、本テストがそれらを網羅する（UI 初期化不要の <c>[Fact]</c> で並列実行）。<br/>
/// UI 側のテストは「入力の反映・通知の転送・描画」のみを検証する。
/// </remarks>
public class ZoomViewSessionTests
{
    /// <summary>ビューポートと画像を設定したセッションを生成する（Fit 表示で開始する）。</summary>
    /// <param name="imageSize">元画像のサイズ。</param>
    /// <param name="viewportSize">ビューポートのサイズ。</param>
    /// <returns>セッション。</returns>
    private static ZoomViewSession CreateSession(EsSize imageSize, EsSize viewportSize)
    {
        var session = new ZoomViewSession();
        session.SetViewport(viewportSize);
        session.SetSource(hasSource: true, imageSize);
        return session;
    }

    /// <summary>100x100 の画像・200x200 のビューポート（Fit = 200%）のセッションを生成する。</summary>
    /// <returns>セッション。</returns>
    private static ZoomViewSession CreateStandard() => CreateSession(new EsSize(100, 100), new EsSize(200, 200));

    // ====================================================================
    // コンテンツ（画像）と Fit の表現
    // ====================================================================

    /// <summary>画像を設定すると Fit 表示（内部ズーム率 0・オフセット 0）になることを検証する。</summary>
    [Fact]
    internal void SetSource_Fit表示へリセットする()
    {
        var session = CreateStandard();

        Assert.True(session.IsFit);
        Assert.Equal(0.0, session.InternalZoomPercent);
        Assert.Equal(new EsVector(0, 0), session.Offset);
        Assert.Equal(200.0, session.ZoomPercent, 6); // Fit の実効倍率
    }

    /// <summary>画像を設定するとズーム状態とビューポートの両方を通知することを検証する。</summary>
    [Fact]
    internal void SetSource_両方を通知する()
    {
        var session = new ZoomViewSession();
        session.SetViewport(new EsSize(200, 200));
        var kinds = new List<ZoomNoticeKind>();
        session.Subscribe(notice => kinds.Add(notice.Kind));

        var feedback = session.SetSource(hasSource: true, new EsSize(100, 100));

        Assert.Equal([ZoomNoticeKind.ZoomChanged, ZoomNoticeKind.ViewportChanged], kinds);
        Assert.True(feedback.Redraw);
    }

    /// <summary>画像未設定・ビューポート未確定時は Fit 実効倍率がフォールバック（100%）であることを検証する。</summary>
    [Fact]
    internal void FitZoomPercent_未確定はフォールバックを使う()
    {
        var session = new ZoomViewSession();

        Assert.Equal(100.0, session.FitZoomPercent, 6);
        Assert.Equal(100.0, session.ZoomPercent, 6);
    }

    // ====================================================================
    // ビューポート変更と Fit キャッシュ（旧 UI にあった陳腐化の回帰）
    // ====================================================================

    /// <summary>ビューポート変更で Fit 実効倍率のキャッシュが破棄されることを検証する。</summary>
    /// <remarks>
    /// 固定倍率表示中でもキャッシュを破棄する（旧 Avalonia 実装は Fit 中のみ破棄していたため、<br/>
    /// 固定倍率中にリサイズすると以降の Fit 値が古いままになる不具合があった）。
    /// </remarks>
    [Fact]
    internal void SetViewport_固定倍率中でもFit実効倍率を更新する()
    {
        var session = CreateStandard();
        Assert.Equal(200.0, session.FitZoomPercent, 6);

        session.SetZoom(100); // 固定倍率中にする
        session.SetViewport(new EsSize(400, 400));

        // 400x400 のビューポートへ 100x100 の画像 → Fit = 400%
        Assert.Equal(400.0, session.FitZoomPercent, 6);
    }

    /// <summary>同じビューポートの再設定では何もしないことを検証する。</summary>
    [Fact]
    internal void SetViewport_同値なら何もしない()
    {
        var session = CreateStandard();
        var count = 0;
        session.Subscribe(_ => count++);

        var feedback = session.SetViewport(new EsSize(200, 200));

        Assert.Equal(0, count);
        Assert.Equal(default, feedback);
    }

    /// <summary>Fit 中のビューポート変更が実効ズーム率の通知を伴うことを検証する。</summary>
    [Fact]
    internal void SetViewport_Fit中は実効ズーム率を通知する()
    {
        var session = CreateStandard();
        ZoomState? captured = null;
        session.Subscribe(notice =>
        {
            if (notice.Kind == ZoomNoticeKind.ZoomChanged)
            {
                captured = notice.State;
            }
        });

        session.SetViewport(new EsSize(400, 400));

        Assert.NotNull(captured);
        Assert.Equal(400.0, captured.Value.ZoomPercent, 6);
    }

    // ====================================================================
    // ズーム率の設定と実効値
    // ====================================================================

    /// <summary>Fit 中の実効ズーム率がクランプされた実効倍率であることを検証する。</summary>
    [Fact]
    internal void ZoomPercent_Fit中はクランプされた実効倍率を返す()
    {
        // 100x100 の画像を 1000x1000 のビューポートへ Fit → 1000%（上限 500% でクランプ）
        var session = CreateSession(new EsSize(100, 100), new EsSize(1000, 1000));

        Assert.Equal(500.0, session.ZoomPercent, 6);
    }

    /// <summary>固定倍率の設定がオフセットをクランプすることを検証する。</summary>
    [Fact]
    internal void SetZoom_固定倍率では実効ズーム率をそのまま返す()
    {
        var session = CreateStandard();

        session.SetZoom(150);

        Assert.False(session.IsFit);
        Assert.Equal(150.0, session.ZoomPercent, 6);
        Assert.Equal(1.5, session.Scale, 6);
    }

    /// <summary>ズーム中心（コンテンツ座標）がスケール変更後も不変であることを検証する。</summary>
    [Fact]
    internal void SetZoom_ズーム中心のコンテンツ座標を維持する()
    {
        var session = CreateSession(new EsSize(1000, 1000), new EsSize(200, 200));
        session.SetZoom(100);
        session.SetOffset(new EsVector(300, 200));

        // ビューポート座標 (100, 100) のコンテンツ座標を求める
        var before = session.ClientToSource(new EsPoint(100, 100));
        session.SetZoom(200, new EsPoint(100, 100));
        var after = session.ClientToSource(new EsPoint(100, 100));

        Assert.Equal(before.X, after.X, 6);
        Assert.Equal(before.Y, after.Y, 6);
    }

    /// <summary>Fit へ移行するとオフセットがリセットされることを検証する。</summary>
    [Fact]
    internal void SetZoom_Fitへ移行するとオフセットをリセットする()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(200);
        session.SetOffset(new EsVector(100, 100));

        session.SetZoom(0);

        Assert.Equal(new EsVector(0, 0), session.Offset);
    }

    // ====================================================================
    // パン
    // ====================================================================

    /// <summary>画像未設定時はパンできない（原点のまま）ことを検証する。</summary>
    [Fact]
    internal void SetOffset_画像なしは原点のまま()
    {
        var session = new ZoomViewSession();

        var feedback = session.SetOffset(new EsVector(50, 50));

        Assert.Equal(new EsVector(0, 0), session.Offset);
        Assert.Equal(default, feedback);
    }

    /// <summary>Fit 中はパンできない（原点のまま）ことを検証する。</summary>
    [Fact]
    internal void SetOffset_Fit中は原点のまま()
    {
        var session = CreateStandard();

        session.SetOffset(new EsVector(50, 50));

        Assert.Equal(new EsVector(0, 0), session.Offset);
    }

    /// <summary>パンがスクロール範囲へクランプされることを検証する。</summary>
    [Fact]
    internal void SetOffset_スクロール範囲へクランプする()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(100);

        session.SetOffset(new EsVector(1000, 1000));

        // 最大オフセット = コンテンツ − ビューポート / スケール = 400 − 200 = 200
        Assert.Equal(new EsVector(200, 200), session.Offset);
    }

    /// <summary>同じオフセットの再設定では何もしないことを検証する。</summary>
    [Fact]
    internal void SetOffset_同値なら何もしない()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(100);
        session.SetOffset(new EsVector(50, 50));
        var count = 0;
        session.Subscribe(_ => count++);

        var feedback = session.SetOffset(new EsVector(50, 50));

        Assert.Equal(0, count);
        Assert.Equal(default, feedback);
    }

    /// <summary>通常ホイールが 1 ノッチ = 48 DIP を実効倍率で除した量をパンすることを検証する。</summary>
    [Fact]
    internal void PanByWheel_48DIPを実効倍率で除す()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(200); // 実効倍率 2.0

        session.PanByWheel(new EsVector(0, 1), shift: false);

        // 48 / 2.0 = 24 コンテンツ座標
        Assert.Equal(24.0, session.Offset.Y, 6);
    }

    /// <summary>Shift 併用のホイールが縦回転を横パンとして扱うことを検証する。</summary>
    [Fact]
    internal void PanByWheel_Shift併用は横パンになる()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(100);

        session.PanByWheel(new EsVector(0, 1), shift: true);

        Assert.Equal(48.0, session.Offset.X, 6);
        Assert.Equal(0.0, session.Offset.Y, 6);
    }

    // ====================================================================
    // grab 方式のパン（入力セッション）
    // ====================================================================

    /// <summary>ドラッグ中は掴んだ位置がポインターに追従することを検証する。</summary>
    [Fact]
    internal void Move_掴んだ位置がポインターに追従する()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(100);

        // (50, 50) を掴んで (30, 40) へ移動する → ポインターの移動量ぶんコンテンツが移動する
        session.Press(new EsPoint(50, 50));
        session.Move(new EsPoint(30, 40));

        // オフセット = 掴んだコンテンツ座標 − ポインター / スケール = 50 − 30 = 20
        Assert.Equal(20.0, session.Offset.X, 6);
        Assert.Equal(10.0, session.Offset.Y, 6);
    }

    /// <summary>パン開始がポインターのキャプチャを要求することを検証する。</summary>
    [Fact]
    internal void Press_ポインタキャプチャを要求する()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(100);

        var feedback = session.Press(new EsPoint(10, 10));

        Assert.True(feedback.CapturePointer);
        Assert.True(session.IsPanning);
    }

    /// <summary>解放がポインターの解放を要求することを検証する。</summary>
    [Fact]
    internal void Release_ポインタ解放を要求する()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(100);
        session.Press(new EsPoint(10, 10));

        var feedback = session.Release();

        Assert.True(feedback.ReleasePointer);
        Assert.False(session.IsPanning);
    }

    /// <summary>パン中でない移動は何もしないことを検証する。</summary>
    [Fact]
    internal void Move_パン中でなければ何もしない()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(100);

        var feedback = session.Move(new EsPoint(30, 40));

        Assert.Equal(default, feedback);
        Assert.Equal(new EsVector(0, 0), session.Offset);
    }

    /// <summary>解放を二重に呼んでも何もしないことを検証する。</summary>
    [Fact]
    internal void Release_パン中でなければ何もしない()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));

        Assert.Equal(default, session.Release());
    }

    // ====================================================================
    // ズーム遷移とログ
    // ====================================================================

    /// <summary>ズームイン・アウトが段階レベルへ移動することを検証する。</summary>
    [Fact]
    internal void ZoomIn_ZoomOut_段階レベルへ移動する()
    {
        var session = CreateSession(new EsSize(200, 200), new EsSize(100, 100)); // Fit = 50%
        Assert.Equal(50.0, session.ZoomPercent, 6);

        session.ZoomIn();
        Assert.Equal(62.0, session.ZoomPercent, 6);

        session.ZoomOut();
        Assert.Equal(50.0, session.ZoomPercent, 6);
    }

    /// <summary>移動できない場合は何もしない（ログも出力しない）ことを検証する。</summary>
    [Fact]
    internal void ZoomIn_移動できない場合は何もしない()
    {
        var session = CreateSession(new EsSize(100, 100), new EsSize(100, 100));
        var logger = new RecordingLogger();
        session.Log = logger;
        session.SetZoom(ZoomOptions.Default.MaxZoom);

        var feedback = session.ZoomIn();

        Assert.Equal(default, feedback);
        Assert.Empty(logger.Messages);
    }

    /// <summary>ズーム操作のログが実効ズーム率（Fit 中は実効倍率）を記録することを検証する。</summary>
    /// <remarks>内部ズーム率の 0 ではなく、実際に表示されている実効倍率を記録する（両 UI で一致させる）。</remarks>
    [Fact]
    internal void ZoomIn_Fit中は実効ズーム率をログに記録する()
    {
        var session = CreateSession(new EsSize(200, 200), new EsSize(100, 100)); // Fit = 50%
        var logger = new RecordingLogger();
        session.Log = logger;

        session.ZoomIn();

        Assert.Equal("[CapturePreviewControl] ZoomIn: 50% -> 62%", Assert.Single(logger.Messages));
    }

    /// <summary>Ctrl+ホイールのズームが WheelFactor 倍へ移動し実効ズーム率を記録することを検証する。</summary>
    [Fact]
    internal void ZoomByWheel_WheelFactor倍へ移動する()
    {
        var session = CreateSession(new EsSize(1000, 1000), new EsSize(200, 200));
        var logger = new RecordingLogger();
        session.Log = logger;
        session.SetZoom(100);

        session.ZoomByWheel(zoomIn: true, new EsPoint(100, 100));

        // 100 × 1.1 = 110
        Assert.Equal(110.0, session.ZoomPercent, 6);
        Assert.Equal("[CapturePreviewControl] ZoomInWheel: 100% -> 110%", Assert.Single(logger.Messages));
    }

    /// <summary>指輪のズーム中心が呼び出しの間だけ保持されることを検証する。</summary>
    [Fact]
    internal void ZoomByWheel_指定した中心をズーム中心にする()
    {
        var session = CreateSession(new EsSize(1000, 1000), new EsSize(200, 200));
        session.SetZoom(100);
        session.SetOffset(new EsVector(300, 300));

        var before = session.ClientToSource(new EsPoint(50, 50));
        session.ZoomByWheel(zoomIn: true, new EsPoint(50, 50));
        var after = session.ClientToSource(new EsPoint(50, 50));

        Assert.Equal(before.X, after.X, 6);
        Assert.Equal(before.Y, after.Y, 6);
    }

    /// <summary>Fit へ戻すと Fit 表示になることを検証する。</summary>
    [Fact]
    internal void ZoomFit_Fit表示へ戻す()
    {
        var session = CreateStandard();
        session.SetZoom(300);

        session.ZoomFit();

        Assert.True(session.IsFit);
        Assert.Equal(200.0, session.ZoomPercent, 6);
    }

    // ====================================================================
    // 可否
    // ====================================================================

    /// <summary>画像未設定時は可否が何れも不可であることを検証する。</summary>
    [Fact]
    internal void CanZoom_画像なしは不可()
    {
        var session = new ZoomViewSession();

        Assert.False(session.CanZoomIn);
        Assert.False(session.CanZoomOut);
    }

    /// <summary>上限ではズームイン不可・最小ではズームアウト不可であることを検証する。</summary>
    [Fact]
    internal void CanZoom_上限と下限で不可になる()
    {
        var session = CreateStandard();
        // Fit（50%）でも下位レベル（40%）があるためズームアウト可能
        Assert.True(session.CanZoomOut);
        Assert.True(session.CanZoomIn);

        session.SetZoom(ZoomOptions.Default.MaxZoom);
        Assert.False(session.CanZoomIn);
        Assert.True(session.CanZoomOut);

        // Fit が最小レベル（10%）になる条件ではズームアウト不可
        var atMinimum = CreateSession(new EsSize(100, 100), new EsSize(10, 10));
        Assert.Equal(ZoomOptions.Default.MinZoom, atMinimum.ZoomPercent, 6);
        Assert.False(atMinimum.CanZoomOut);
    }

    /// <summary>設定変更で可否のキャッシュが破棄されることを検証する。</summary>
    [Fact]
    internal void ApplyConfig_可否を再計算する()
    {
        var session = CreateStandard();
        Assert.True(session.CanZoomIn);

        // 上限を Fit の実効倍率（200%）へ下げる → ズームイン不可になる
        session.ApplyConfig(ZoomOptions.Default.With(maxZoom: 200, buttonZoomLevels: [10, 50, 100, 200]));

        Assert.False(session.CanZoomIn);
    }

    /// <summary>現在状態（通知値）が 4 値を同時に確定することを検証する。</summary>
    [Fact]
    internal void CurrentState_4値を返す()
    {
        var session = CreateSession(new EsSize(200, 200), new EsSize(100, 100));

        var state = session.CurrentState;

        Assert.Equal(50.0, state.ZoomPercent, 6);
        Assert.True(state.IsFit);
        Assert.True(state.CanZoomIn);
        Assert.True(state.CanZoomOut);
    }

    // ====================================================================
    // 表示値（サイズ・原点・矩形）
    // ====================================================================

    /// <summary>Fit の表示サイズがビューポートへ収まることを検証する。</summary>
    [Fact]
    internal void DisplaySize_Fitはビューポートへ収まる()
    {
        var session = CreateSession(new EsSize(400, 200), new EsSize(200, 200));

        var size = session.DisplaySize;

        Assert.Equal(200.0, size.Width, 6);
        Assert.Equal(100.0, size.Height, 6);
    }

    /// <summary>Fit の原点が中央寄せ補正されることを検証する。</summary>
    [Fact]
    internal void ImageOrigin_Fitは中央寄せする()
    {
        var session = CreateSession(new EsSize(400, 200), new EsSize(200, 200));

        var origin = session.ImageOrigin;

        Assert.Equal(0.0, origin.X, 6);
        Assert.Equal(50.0, origin.Y, 6); // (200 − 100) / 2
    }

    /// <summary>中央寄せを無効にすると原点が 0 になることを検証する。</summary>
    [Fact]
    internal void ImageOrigin_左上寄せは原点が0になる()
    {
        var session = CreateSession(new EsSize(400, 200), new EsSize(200, 200));
        session.SetCenterAlign(centerAlign: false);

        var origin = session.ImageOrigin;

        Assert.Equal(0.0, origin.X, 6);
        Assert.Equal(0.0, origin.Y, 6);
    }

    /// <summary>画像未設定時の表示矩形が空であることを検証する。</summary>
    [Fact]
    internal void ImageRect_画像なしは空になる()
    {
        var session = new ZoomViewSession();

        Assert.Equal(default, session.ImageRect);
        Assert.Equal(0.0, session.DisplaySize.Width, 6);
    }

    /// <summary>固定倍率のパンで原点がオフセットに追従することを検証する。</summary>
    [Fact]
    internal void ImageOrigin_固定倍率のパンに追従する()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(100);

        session.SetOffset(new EsVector(30, 40));

        var origin = session.ImageOrigin;
        Assert.Equal(-30.0, origin.X, 6);
        Assert.Equal(-40.0, origin.Y, 6);
    }

    // ====================================================================
    // 座標変換
    // ====================================================================

    /// <summary>画像表示矩形の原点基準の座標変換が往復することを検証する。</summary>
    [Fact]
    internal void 座標変換が往復する()
    {
        var session = CreateSession(new EsSize(100, 100), new EsSize(200, 200));

        var viewport = session.DisplayedSourceToClient(new EsPoint(25, 30));
        var content = session.ClientToDisplayedSource(viewport);

        Assert.Equal(25.0, content.X, 6);
        Assert.Equal(30.0, content.Y, 6);
    }

    /// <summary>オフセット基準の座標変換が往復することを検証する。</summary>
    [Fact]
    internal void オフセット基準の座標変換が往復する()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(200);
        session.SetOffset(new EsVector(50, 20));

        var viewport = session.DisplayedSourceToClient(new EsPoint(100, 100));
        var content = session.ClientToDisplayedSource(viewport);

        Assert.Equal(100.0, content.X, 6);
        Assert.Equal(100.0, content.Y, 6);
    }

    // ====================================================================
    // スクロールバー
    // ====================================================================

    /// <summary>拡大時にスクロール可能になり、同期値が確定することを検証する。</summary>
    [Fact]
    internal void ScrollMetrics_拡大時はスクロール可能になる()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(200);

        var vertical = session.ScrollMetrics(vertical: true);

        Assert.True(vertical.IsScrollable);
        Assert.Equal(300.0, vertical.MaxOffset, 6); // 400 − 200 / 2
        Assert.Equal(100.0, vertical.ViewportSize, 6); // 200 / 2
    }

    /// <summary>Fit 表示ではスクロール不要であることを検証する。</summary>
    [Fact]
    internal void ScrollMetrics_Fitはスクロール不可になる()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));

        Assert.False(session.ScrollMetrics(vertical: true).IsScrollable);
    }

    /// <summary>画像未設定時はスクロール不可であることを検証する。</summary>
    [Fact]
    internal void ScrollMetrics_画像なしはスクロール不可になる()
    {
        var session = new ZoomViewSession();

        Assert.False(session.ScrollMetrics(vertical: false).IsScrollable);
    }

    /// <summary>スクロールバーがスクロール可能な軸にのみ生成されることを検証する。</summary>
    [Fact]
    internal void CurrentScrollBars_スクロール可能な軸にのみ生成する()
    {
        // 横長の画像を 100% で表示 → 横のみスクロール可能
        var session = CreateSession(new EsSize(400, 100), new EsSize(200, 200));
        session.SetZoom(100);

        var (horizontal, vertical) = session.CurrentScrollBars();

        Assert.NotNull(horizontal);
        Assert.Null(vertical);
    }

    /// <summary>スクロールバーを無効にすると生成されないことを検証する。</summary>
    [Fact]
    internal void CurrentScrollBars_無効時は生成しない()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(100);
        session.SetAppearance(session.Appearance with { ShowScrollBars = false });

        var (horizontal, vertical) = session.CurrentScrollBars();

        Assert.Null(horizontal);
        Assert.Null(vertical);
    }

    /// <summary>トラック押下でサムがその位置へ移動し、オフセットが更新されることを検証する。</summary>
    [Fact]
    internal void TryBeginScrollBarDrag_トラック押下でサムを移動する()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(200);

        // 縦スクロールバーのトラック内（サムの外）を押す
        var vertical = session.CurrentScrollBars().Vertical!.Value;
        var press = new EsPoint(vertical.Track.X + 1, vertical.Track.Bottom - 1);

        Assert.True(session.TryBeginScrollBarDrag(press));
        Assert.True(session.IsDraggingScrollBar);
        Assert.True(session.Offset.Y > 0);
    }

    /// <summary>スクロールバーを無効にするとトラック押下を受理しないことを検証する。</summary>
    [Fact]
    internal void TryBeginScrollBarDrag_無効時は受理しない()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(200);
        session.SetAppearance(session.Appearance with { ShowScrollBars = false });

        Assert.False(session.TryBeginScrollBarDrag(new EsPoint(190, 100)));
    }

    /// <summary>ドラッグの更新と終了がオフセットを更新し、ポインタ解放を要求することを検証する。</summary>
    [Fact]
    internal void UpdateScrollBarDrag_オフセットを更新して解放を要求する()
    {
        var session = CreateSession(new EsSize(400, 400), new EsSize(200, 200));
        session.SetZoom(200);
        var vertical = session.CurrentScrollBars().Vertical!.Value;
        var press = new EsPoint(vertical.Track.X + 1, vertical.Track.Y + 1);
        session.TryBeginScrollBarDrag(press);
        var before = session.Offset.Y;

        session.UpdateScrollBarDrag(new EsPoint(press.X, press.Y + 20));
        var feedback = session.ReleaseScrollBarDrag();

        Assert.True(session.Offset.Y > before);
        Assert.True(feedback.ReleasePointer);
        Assert.False(session.IsDraggingScrollBar);
    }

    // ====================================================================
    // 外観・環境値
    // ====================================================================

    /// <summary>外観の設定が再描画を要求し、同値の再設定では何もしないことを検証する。</summary>
    [Fact]
    internal void SetAppearance_変化時のみ再描画する()
    {
        var session = new ZoomViewSession();

        var changed = session.SetAppearance(ZoomViewAppearance.Default with { CheckerSize = 8 });
        var same = session.SetAppearance(ZoomViewAppearance.Default with { CheckerSize = 8 });

        Assert.True(changed.Redraw);
        Assert.Equal(default, same);
        Assert.Equal(8, session.Appearance.CheckerSize);
    }

    /// <summary>中央寄せの設定がビューポートの通知を伴い、同値では何もしないことを検証する。</summary>
    [Fact]
    internal void SetCenterAlign_変化時のみ通知する()
    {
        var session = new ZoomViewSession();
        var count = 0;
        session.Subscribe(_ => count++);

        var changed = session.SetCenterAlign(centerAlign: false);
        var same = session.SetCenterAlign(centerAlign: false);

        Assert.Equal(1, count);
        Assert.True(changed.Redraw);
        Assert.Equal(default, same);
    }

    /// <summary>ロガー未設定でもログ出力が安全であることを検証する。</summary>
    [Fact]
    internal void Log_未設定でも例外にならない()
    {
        var session = CreateStandard();

        session.ZoomIn();
        session.ZoomOut();

        Assert.NotNull(session.Log);
    }
}
