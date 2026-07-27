namespace Estable.AvaloniaUI.FadeMaskDragRect;

/// <summary>FadeMaskDragRectControl の動作モード（FadeMaskRenderMode への変換と子コントロールの可視性を決定）。</summary>
/// <remarks>
/// ホスト（CropSession / OverlayWindow）はキャプチャ種別に応じて本モードを設定するだけで、<br/>
/// 内部コントロール（DragRect / Loupe）の有効・無効と全面暗転の表示が自動的に切り替わる。<br/>
/// <br/>
/// - <see cref="Crop"/>: DragRect + FadeMask + Loupe を有効化（プレビュー内の切出し）。<br/>
/// - <see cref="AreaSelect"/>: DragRect + FadeMask + Loupe を有効化（全画面オーバーレイの領域選択）。<br/>
/// - <see cref="ScreenSelect"/> / <see cref="WindowSelect"/>: FadeMask のみ有効化<br/>
///   （DragRect / Loupe は無効。ハイライト表示のみで、ホストがクリック確定フローを担う）。
/// </remarks>
public enum FadeMaskDragRectMode
{
    /// <summary>切出し（プレビューコントロール上のオーバーレイ。全面暗転 + 選択矩形 + ルーペ）。</summary>
    Crop,

    /// <summary>範囲選択（全画面オーバーレイ。全面暗転 + 選択矩形 + ルーペ + ドラッグハンドル）。</summary>
    AreaSelect,

    /// <summary>スクリーン選択（ハイライト + スクリーン番号ラベルのみ。クリック確定フローはホストが担う）。</summary>
    ScreenSelect,

    /// <summary>ウィンドウ選択（ハイライト + ウィンドウタイトルラベルのみ。クリック確定フローはホストが担う）。</summary>
    WindowSelect,
}
