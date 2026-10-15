namespace EsUtil.SaveEvidence.App.WinForms.Icons;

/// <summary>アプリが表示するアイコンの種類。</summary>
/// <remarks>
/// アイコンは GDI+ の図形で描く（SVG のラスタライズやアイコン フォントを必要としない）。<br/>
/// 論理名で参照し、実際の描画は <see cref="IconLoader"/> が単一所有する。
/// </remarks>
internal enum AppIcon
{
    /// <summary>メニュー（横 3 本線）。</summary>
    Menu,

    /// <summary>保存。</summary>
    Save,

    /// <summary>スクリーン（モニター）選択。</summary>
    ScreenSelect,

    /// <summary>ウィンドウ選択。</summary>
    WindowSelect,

    /// <summary>範囲選択（破線の矩形）。</summary>
    AreaSelect,

    /// <summary>切り出し（切り出しマーク）。</summary>
    Crop,

    /// <summary>自動切り出し（矩形 + 拡大鏡）。</summary>
    AutoCrop,

    /// <summary>コピー。</summary>
    Copy,

    /// <summary>貼り付け。</summary>
    Paste,

    /// <summary>元に戻す。</summary>
    Undo,

    /// <summary>元に戻すを取り消す。</summary>
    Redo,

    /// <summary>最新の情報に更新。</summary>
    Refresh,

    /// <summary>昇順に並べ替える。</summary>
    SortAscending,

    /// <summary>降順に並べ替える。</summary>
    SortDescending,

    /// <summary>設定（歯車）。</summary>
    Settings,

    /// <summary>フォルダ。</summary>
    Folder,

    /// <summary>拡大。</summary>
    ZoomIn,

    /// <summary>縮小。</summary>
    ZoomOut,

    /// <summary>ウィンドウに合わせる（Fit）。</summary>
    ZoomFit,

    /// <summary>ナビゲーション パネルの開閉。</summary>
    NavToggle,
}
