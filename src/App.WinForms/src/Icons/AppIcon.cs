namespace EsUtil.SaveEvidence.App.WinForms.Icons;

/// <summary>アプリが表示するアイコンの種類。</summary>
/// <remarks>
/// アイコンは<b>他の UI 版と同じ SVG ファイル</b>（<c>SvgIconLoader</c>（App.Core）が単一所有する）から描く。<br/>
/// 本列挙は表示名であり、実際のファイルとの対応と描画は <see cref="IconLoader"/> が単一所有する。<br/>
/// <b>共有するアイコンを持たない種類</b>（<see cref="Menu"/>）だけは、他 UI 版と同じ見た目になる図形を
/// <see cref="IconLoader"/> が描く。
/// </remarks>
internal enum AppIcon
{
    /// <summary>メニュー（他の UI 版は ☰ の文字で表す）。</summary>
    Menu,

    /// <summary>ナビゲーション パネルを開く（閉じているときに使う）。</summary>
    PanelOpen,

    /// <summary>ナビゲーション パネルを閉じる（開いているときに使う）。</summary>
    PanelClose,

    /// <summary>キャプチャ（アプリのアイコン。 ウィンドウ アイコンにも使う）。</summary>
    Capture,

    /// <summary>フォルダビュー。</summary>
    Folder,

    /// <summary>Undo 一覧。</summary>
    Undo,

    /// <summary>設定。</summary>
    Settings,

    /// <summary>保存。</summary>
    Save,

    /// <summary>スクリーン選択。</summary>
    ScreenSelect,

    /// <summary>ウィンドウ選択。</summary>
    WindowSelect,

    /// <summary>領域選択。</summary>
    AreaSelect,

    /// <summary>切り出し。</summary>
    Crop,

    /// <summary>拡大。</summary>
    ZoomIn,

    /// <summary>縮小。</summary>
    ZoomOut,

    /// <summary>ウィンドウに合わせる（Fit）。</summary>
    ZoomFit,
}
