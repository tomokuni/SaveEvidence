using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>ナビゲーションの 1 ページ分の内容と、表示される／表示から外れる／先読みするときに実行する処理。</summary>
/// <param name="Content">ページの内容（ナビゲーションの内容として表示する要素）。</param>
/// <param name="OnActivated">ページが表示されるたびに呼ぶ処理。 不要なページは null。</param>
/// <param name="OnKeyDown">ページ固有のキー処理。 不要なページは null。 <c>Handled</c> を設定した場合、以降へ伝わらない。</param>
/// <param name="OnDeactivated">ページが表示から外れるたびに呼ぶ処理。 不要なページは null。</param>
/// <param name="OnWarmUp">起動後に一度だけ呼ぶ処理（ページを表示せずに一覧を構築し、低負荷で準備する）。 不要なページは null。</param>
/// <remarks>
/// <b>OnActivated が必要な理由</b>: 本アプリはページの要素を 1 度だけ生成して再利用する（状態と購読を保持するため）。<br/>
/// そのため「表示されるたびに最新化する」処理（設定の再読込・一覧の再構築）を本デリゲートで明示的に呼び出す。<br/>
/// <b>OnDeactivated が必要な理由</b>: 表示中だけ全力にしたい処理（サムネイルの背景生成）を下げるためである。<br/>
/// ページの要素は表示の切り替えでも生存し続けるため、要素側では「隠れた」ことを検知できない。<br/>
/// <b>OnWarmUp が必要な理由</b>: 起動後に、ページを表示せずに一覧の構築とサムネイルの低負荷生成を<br/>
/// 始めるためである（最初の切り替えで生成済みのサムネイルが並ぶ）。<br/>
/// <b>呼び出し順</b>: 内容の生成直後と、以降の選択のたびに <see cref="OnActivated"/> が呼ばれる。<br/>
/// 表示から外れるページには、次のページの <see cref="OnActivated"/> より前に <see cref="OnDeactivated"/> が呼ばれる。<br/>
/// <b>OnKeyDown が必要な理由</b>: ページ固有のショートカット（切り出しの <c>Ctrl+R</c> 等）は、フォーカスに<br/>
/// 依存せずページが表示されている間だけ有効にする必要がある。 ウィンドウの <c>KeyDown</c> から<br/>
/// 表示中のページへ転送する。
/// </remarks>
internal sealed record PageContent(
    FrameworkElement Content,
    Action? OnActivated = null,
    Action<KeyRoutedEventArgs>? OnKeyDown = null,
    Action? OnDeactivated = null,
    Action? OnWarmUp = null);
