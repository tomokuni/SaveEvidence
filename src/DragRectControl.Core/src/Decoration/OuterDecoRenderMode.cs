namespace Estable.DragRect.Decoration;

/// <summary>オーバーレイ描画のモード（描画要素の切り替えに使用する）。</summary>
/// <remarks>
/// AreaSelect / ScreenSelect / WindowSelect / Crop の 4 モードで描画内容が異なる。<br/>
/// 描画要素の切り替えはモードの列挙で表現し、bool フラグによる分岐は行わない。
/// </remarks>
public enum OuterDecoRenderMode
{
    /// <summary>範囲選択（全画面オーバーレイ。ルーペ・ドラッグハンドル・始点マーカーを表示する）。</summary>
    AreaSelect,

    /// <summary>スクリーン選択（ハイライト + スクリーン番号ラベルを表示する）。</summary>
    ScreenSelect,

    /// <summary>ウィンドウ選択（ハイライト + ウィンドウタイトルラベルを表示する）。</summary>
    WindowSelect,

    /// <summary>切出し（プレビューコントロール上のオーバーレイ。全面暗転 + 選択矩形を表示する）。</summary>
    Crop,
}
