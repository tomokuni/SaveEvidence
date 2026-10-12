using Avalonia.Headless.XUnit;
using EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;
using Xunit;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Tests;

/// <summary>一覧のサムネイル生成が終わるまでの表示を支える仕組み（代替画像と一覧アイテムの変更通知）の単体テスト。</summary>
/// <remarks>
/// 「生成中でも生成済みのサムネイルを表示し、未生成には代替画像を表示する」ためには、<br/>
/// 1. 代替画像が寸法ごとに共有され破棄されないこと（他の行の表示を壊さない）、<br/>
/// 2. 背景の生成が終わったときに一覧の表示が差し替わること（変更通知）<br/>
/// の 2 点が要る。 ここではその 2 点だけを検証する（生成そのものは <c>ThumbnailScheduler</c> のテストが担う）。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ThumbnailPlaceholderTests
{
    [AvaloniaFact]
    internal void 代替画像は同じ寸法では同じ実体を返す()
    {
        var first = ThumbnailPlaceholder.Get(96);
        var second = ThumbnailPlaceholder.Get(96);

        Assert.Same(first, second);
    }

    [AvaloniaFact]
    internal void 代替画像は寸法ごとに別の実体を返す()
    {
        var small = ThumbnailPlaceholder.Get(48);
        var large = ThumbnailPlaceholder.Get(192);

        // 寸法そのものはヘッドレスでは検証しない（変換後の Bitmap の寸法は描画側が決める）
        Assert.NotSame(small, large);
    }

    [AvaloniaFact]
    internal void 代替画像は破棄しても取得し直せる()
    {
        // 呼び出し側が借用して表示するだけ（破棄しない）契約であることを、破棄後に使えることで示す
        var placeholder = ThumbnailPlaceholder.Get(64);
        placeholder.Dispose();

        var again = ThumbnailPlaceholder.Get(64);

        Assert.NotNull(again);
    }

    [AvaloniaFact]
    internal void ファイル一覧のサムネイル差し替えは通知される()
    {
        var item = new FileListItem { FilePath = "a.png", FileName = "a.png", ThumbnailWidth = 96 };
        var changed = new List<string>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        item.Thumbnail = ThumbnailPlaceholder.Get(96);

        Assert.Equal([nameof(FileListItem.Thumbnail)], changed);
    }

    [AvaloniaFact]
    internal void 履歴一覧のサムネイル差し替えは通知される()
    {
        var item = new UndoListItem { Label = "現在", IsCurrent = true, ThumbnailWidth = 96 };
        var changed = new List<string>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        item.Thumbnail = ThumbnailPlaceholder.Get(96);

        Assert.Equal([nameof(UndoListItem.Thumbnail)], changed);
    }
}
