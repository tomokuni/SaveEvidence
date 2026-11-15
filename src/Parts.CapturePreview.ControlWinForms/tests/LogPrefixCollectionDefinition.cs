using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Tests;

/// <summary>ログ接頭辞（<c>CapturePreviewLogMessage.Prefix</c>。 静的）を書き換えるテストのコレクション定義。</summary>
/// <remarks>
/// 接頭辞はプロセス全体で 1 つのため、別のテスト クラスと並列に動くと互いの値を壊す。<br/>
/// 同じコレクションへ入れて並列実行を無効にする（接頭辞を扱うテストだけが対象）。
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LogPrefixCollectionDefinition
{
    /// <summary>コレクション名。</summary>
    public const string Name = "LogPrefix";
}