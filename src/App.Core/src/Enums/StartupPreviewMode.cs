using System.ComponentModel;

namespace EsUtil.SaveEvidence.App.Core.Enums;

/// <summary>起動直後のキャプチャ ページに置く画像（プレースホルダ）の種類。</summary>
/// <remarks>
/// 起動直後は画像が無く、保存・コピー・切り出しがすべて無効になる。 どちらを置くかは利用者が選ぶ<br/>
/// （「グラデーション画像」は機能の動作確認に、「空白」は撮影前の状態をそのまま見たい場合に使う）。
/// </remarks>
public enum StartupPreviewMode
{
    /// <summary>グラデーション画像を置く（撮影の前後を画素の実測で判定できる）。</summary>
    [Description("グラデーション画像")]
    Gradient = 0,

    /// <summary>画像を置かない（プレビューは画像領域の背景色だけになる）。</summary>
    [Description("空白")]
    Blank = 1,
}
