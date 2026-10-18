using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>ナビゲーションの 1 ページ分の内容と、表示される／表示から外れる／先読みするときに実行する処理。</summary>
/// <param name="Content">ページの内容（<c>NavigationView</c> の内容として表示する要素）。</param>
/// <param name="OnActivated">ページが表示されるたびに呼ぶ処理。 不要なページは null。</param>
/// <param name="OnKeyDown">ページ固有のキー処理。 不要なページは null。<c>Handled</c> を設定した場合、以降へ伝わらない。</param>
/// <param name="OnDeactivated">ページが表示から外れるたびに呼ぶ処理。 不要なページ は null。</param>
/// <param name="OnWarmUp">起動後に一度だけ呼ぶ処理（ページを表示せずに一覧を構築し、低負荷で準備する）。 不要なページは null。</param>
/// <param name="OnWindowDeactivated">ウィンドウが非アクティブになったときに呼ぶ処理（開いているポップアップを閉じる等）。 不要なページは null。</param>
/// <remarks>
/// <b>OnActivated が必要な理由</b>: MewUI の <c>NavigationView</c> は選択のたびに内容を渡し直す用途を想定しているが、<br/>
/// 本アプリはページの要素を 1 度だけ生成して再利用する（状態と購読を保持するため）。<br/>
/// そのため「表示されるたびに最新化する」処理（設定の再読込・一覧の再構築）を本デリゲートで明示的に呼び出す。<br/>
/// Avalonia 版が <c>IsVisible</c> の変更で行っている処理に相当する。<br/>
/// <b>OnDeactivated が必要な理由</b>: 表示中だけ全力にしたい処理（サムネイルの背景生成）を下げるためである。
/// ページの要素は <c>IsVisible</c> を切り替えるだけで再利用されるため、要素側では「隠れた」ことを検知できない。<br/>
/// <b>OnWarmUp が必要な理由</b>: 起動後に、ページを表示せずに一覧の構築とサムネイルの低負荷生成を始めるためである。
/// 最初の切り替えで生成済みのサムネイルが並ぶ（キャプチャページの操作は妨げない）。<br/>
/// <b>呼び出し順</b>: <see cref="Content"/> の生成直後と、以降の選択のたびに <see cref="OnActivated"/> が呼ばれる。
/// 表示から外れるページには、次のページの <see cref="OnActivated"/> より前に <see cref="OnDeactivated"/> が呼ばれる。<br/>
/// <b>OnKeyDown が必要な理由</b>: ページ固有のショートカット（切り出しの <c>Ctrl+R</c> 等）は、フォーカスに依存せず<br/>
/// ページが表示されている間だけ有効にする必要がある。 ウィンドウの <c>PreviewKeyDown</c>（トンネル段階）から<br/>
/// 表示中のページへ転送し、Avalonia 版の <c>MenuItem.InputGesture</c> と同じ振る舞いにする。
/// </remarks>
internal sealed record PageContent(
    FrameworkElement Content, Action? OnActivated = null, Action<KeyEventArgs>? OnKeyDown = null, Action? OnDeactivated = null,
    Action? OnWarmUp = null, Action? OnWindowDeactivated = null);
