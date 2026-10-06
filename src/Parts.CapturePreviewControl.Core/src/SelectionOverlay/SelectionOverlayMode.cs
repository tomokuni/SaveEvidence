namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;

/// <summary>領域選択の複合コントロールの動作モード（描画モードと子コントロールの可視性を決定）。</summary>
/// <remarks>
/// ホスト（上位 AP）はキャプチャ種別に応じて本モードを設定するだけで、<br/>
/// 内部コントロールの有効・無効と全面暗転の表示が自動的に切り替わる。<br/>
/// <br/>
/// - <see cref="Crop"/>: 選択矩形 + 暗転マスク + ルーペを有効化する（プレビュー内の切出し）。<br/>
/// - <see cref="AreaSelect"/>: 選択矩形 + 暗転マスク + ルーペを有効化する（全画面オーバーレイの領域選択）。<br/>
/// - <see cref="ScreenSelect"/> / <see cref="WindowSelect"/>: 暗転マスクのみ有効化する<br/>
///   （選択矩形・ルーペは無効。 ハイライト表示のみで、クリック確定フローはホストが担う）。
/// </remarks>
public enum SelectionOverlayMode
{
    /// <summary>切出し（プレビューコントロール上のオーバーレイ。 全面暗転 + 選択矩形 + ルーペ）。</summary>
    Crop,

    /// <summary>範囲選択（全画面オーバーレイ。 全面暗転 + 選択矩形 + ルーペ + ドラッグハンドル）。</summary>
    AreaSelect,

    /// <summary>スクリーン選択（ハイライト + スクリーン番号ラベルのみ。 クリック確定フローはホストが担う）。</summary>
    ScreenSelect,

    /// <summary>ウィンドウ選択（ハイライト + ウィンドウタイトルラベルのみ。 クリック確定フローはホストが担う）。</summary>
    WindowSelect,
}
