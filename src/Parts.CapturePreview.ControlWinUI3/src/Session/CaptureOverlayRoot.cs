using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session;

/// <summary>撮影オーバーレイの内容を載せるルート パネル（マウス カーソルを設定できる）。</summary>
/// <remarks>
/// <b>なぜ派生型が必要か</b>: WinUI 3 の <c>UIElement.ProtectedCursor</c> は <c>protected</c> であり、<br/>
/// 派生クラスからしか設定できない（コントロール自身が知っているカーソル形状を公開する手段が他にない）。<br/>
/// 本クラスは撮影オーバーレイのルート（全面を覆う領域）として、ドラッグ選択中に十字カーソルを表示する入口だけを提供する。<br/>
/// <b>背景</b>は撮影画像の平均色を設定する（画像が合成されるまでの 1 フレームの見え方を抑える）。
/// </remarks>
internal sealed class CaptureOverlayRoot : Grid
{
    /// <summary>十字カーソル（ドラッグで矩形を作る種別で使う）。</summary>
    private static readonly InputCursor s_crossCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);

    /// <summary>ドラッグ選択用の十字カーソルを適用する。</summary>
    /// <remarks>
    /// ハイライト型（スクリーン選択 / ウィンドウ選択）では呼ばない（既定のカーソルのままにする）。<br/>
    /// 種別による使い分けの判断はホスト（撮影オーバーレイ）がコアへ問い合わせて行う。
    /// </remarks>
    public void ApplyCrossCursor() => ProtectedCursor = s_crossCursor;
}
