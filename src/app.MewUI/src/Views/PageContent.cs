using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace SaveEvidence.MewUI.Views;

/// <summary>ナビゲーションの 1 ページ分の内容と、表示されるたびに実行する処理。</summary>
/// <param name="Content">ページの内容（<c>NavigationView</c> の内容として表示する要素）。</param>
/// <param name="OnActivated">ページが表示されるたびに呼ぶ処理。不要なページは null。</param>
/// <remarks>
/// <b>OnActivated が必要な理由</b>: MewUI の <c>NavigationView</c> は選択のたびに内容を渡し直す用途を想定しているが、<br/>
/// 本アプリはページの要素を 1 度だけ生成して再利用する（状態と購読を保持するため）。<br/>
/// そのため「表示されるたびに最新化する」処理（設定の再読込・一覧の再構築）を本デリゲートで明示的に呼び出す。<br/>
/// Avalonia 版が <c>IsVisible</c> の変更で行っている処理に相当する。<br/>
/// <b>呼び出し順</b>: <see cref="Content"/> の生成直後と、以降の選択のたびに <see cref="OnActivated"/> が呼ばれる。
/// </remarks>
internal sealed record PageContent(FrameworkElement Content, Action? OnActivated = null);
