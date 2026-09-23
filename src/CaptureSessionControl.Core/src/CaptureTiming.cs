namespace Estable.CaptureSession;

/// <summary>撮影セッションが使う時間・座標の方針値を単一所有する。</summary>
/// <remarks>
/// 「画面キャプチャのタイムアウト」と「オーナーウィンドウの画面外退避座標」は UI の見た目ではなく<b>運用方針</b>であり、<br/>
/// 両 UI で同じ値を使う。UI ごとに数値を書くと、片方だけ変更されて挙動が食い違う<br/>
/// （実際に画面外座標が両 UI で別々に定義されていた）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしで参照できる。
/// </remarks>
public static class CaptureTiming
{
    /// <summary>画面キャプチャ（仮想スクリーン全体の取得）のタイムアウトを取得する。</summary>
    /// <value>タイムアウト時間（既定 5 秒）。</value>
    /// <remarks>
    /// 画面キャプチャは DWM（デスクトップ合成）の応答不能時に長時間ブロックし得る。<br/>
    /// 取得を待ち続けるとオーバーレイが表示されないまま操作不能になるため、上限を定めて打ち切る。
    /// </remarks>
    public static readonly TimeSpan ScreenCaptureTimeout = TimeSpan.FromMilliseconds(5000);

    /// <summary>オーナーウィンドウを退避させる画面外の座標（画面物理ピクセル）を取得する。</summary>
    /// <value>画面外の座標（<c>-32000</c>）。</value>
    /// <remarks>
    /// Windows で標準的に使われる画面外配置であり、ヒットテスト・要素検出の対象外になる。<br/>
    /// 非表示（<c>Hide</c>）ではなく位置の変更にするのは、復帰時にウィンドウが再作成されてちらつくのを避けるためである。
    /// </remarks>
    public const int OwnerOffScreenCoordinate = -32000;
}
