using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>フローティング パネル（書式バー・右クリックの外観フライアウト）を置く窓口（UI が実装する）。</summary>
/// <remarks>
/// <b>UI が実装するのはここだけ</b>: アプリの画面へパネルを載せ、動かし、外すだけを行う。
/// いつ出すか・どこへ出すか・ドラッグでどう動くかは <see cref="AnnotationPanelController"/> が
/// 単一所有する（4 つの UI 版で動作が食い違わないようにするため）。<br/>
/// <b>独立したウィンドウを作らない</b>: OS のウィンドウはタスク バーへ出る・アプリの後ろへ回る・
/// フォーカスを奪うといった問題が版ごとに出るため、アプリの画面の上へ重ねる。<br/>
/// <b>座標はアプリの画面の左上を原点とする DIP</b>: 画像（プレビュー）の内側で切れないよう、
/// 重ねる先はプレビューではなくアプリの画面の最上位にする。
/// </remarks>
public interface IAnnotationPanelHost
{
    /// <summary>パネルを置ける領域（DIP）を取得する。</summary>
    /// <value>アプリの画面のうちパネルを置ける範囲。 パネルはこの外へは出さない（見えなくなるため）。</value>
    EsRect AvailableArea { get; }

    /// <summary>表示中のパネルの左上（DIP）を取得する。</summary>
    /// <value>表示していない場合の値は不定（呼び出し側は表示中だけ読む）。</value>
    EsPoint PanelPosition { get; }

    /// <summary>表示中のパネルの大きさ（DIP）を取得する。</summary>
    /// <value>表示していない場合の値は不定（呼び出し側は表示中だけ読む）。</value>
    EsSize PanelSize { get; }

    /// <summary>パネルを作って表示する（既に出ている場合は作り直す）。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅。 そのまま適用する）。</param>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    void CreatePanel(AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPoint position);

    /// <summary>パネルを移動する。</summary>
    /// <param name="position">左上の位置（アプリの画面の左上を原点とする DIP）。</param>
    void MovePanel(EsPoint position);

    /// <summary>パネルを画面から外す。</summary>
    void DestroyPanel();
}
