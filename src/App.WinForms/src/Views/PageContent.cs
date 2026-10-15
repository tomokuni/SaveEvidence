namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>ページの内容と、表示の切り替えに伴って実行する処理の束。</summary>
/// <param name="Content">表示するコントロール。</param>
/// <param name="OnActivated">表示されるたびに実行する処理（一覧の再構築・設定の再読込）。</param>
/// <param name="OnDeactivated">表示から外れるたびに実行する処理（背景の生成を低負荷へ戻す）。</param>
/// <param name="OnWarmUp">起動後に 1 度だけ実行する処理（先読み）。</param>
/// <param name="OnShortcut">ページ固有のショートカット キーを処理する関数（処理したら true）。</param>
/// <remarks>
/// ページの内容は<b>選択時に 1 度だけ生成</b>し、以降は同じコントロールを使い回す<br/>
/// （ページの状態と変更購読を保つため）。 「表示されるたびに最新化する処理」は<br/>
/// <see cref="OnActivated"/> として渡し、ナビゲーションの選択時に呼ぶ。
/// </remarks>
internal sealed record PageContent(
    Control Content,
    Action? OnActivated = null,
    Action? OnDeactivated = null,
    Action? OnWarmUp = null,
    Func<Keys, bool>? OnShortcut = null);
