using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>フローティング パネルのウィンドウを操作する窓口（UI が実装する）。</summary>
/// <remarks>
/// <b>UI が実装するのはここだけ</b>: ウィンドウの生成・移動・破棄だけを行う。 いつ出すか・どこへ出すか・
/// ドラッグでどう動くかは <see cref="AnnotationPanelController"/> が単一所有する（4 つの UI 版で
/// 動作が食い違わないようにするため）。<br/>
/// <b>位置の単位は物理ピクセル</b>（アンカーも移動先も同じ）。 DIP で扱う UI は、実装の中で
/// 倍率を掛けて変換する。
/// </remarks>
public interface IAnnotationPanelHost
{
    /// <summary>表示中のパネルの左上（物理ピクセル）を取得する。</summary>
    /// <value>表示していない場合の値は不定（呼び出し側は表示中だけ読む）。</value>
    EsPointInt PanelPosition { get; }

    /// <summary>表示中のパネルの大きさ（物理ピクセル）を取得する。</summary>
    /// <value>表示していない場合の値は不定（呼び出し側は表示中だけ読む）。</value>
    EsSizeInt PanelSize { get; }

    /// <summary>画面の作業領域（物理ピクセル）を取得する。</summary>
    /// <value>パネルを置く画面の、タスク バーを除いた領域。</value>
    EsRect WorkArea { get; }

    /// <summary>パネルを作って表示する（既に出ている場合は作り直す）。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅。 そのまま適用する）。</param>
    /// <param name="screenPosition">左上の位置（物理ピクセル）。</param>
    void CreatePanel(AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPointInt screenPosition);

    /// <summary>パネルを移動する。</summary>
    /// <param name="screenPosition">左上の位置（物理ピクセル）。</param>
    void MovePanel(EsPointInt screenPosition);

    /// <summary>パネルを閉じて破棄する。</summary>
    void DestroyPanel();
}
