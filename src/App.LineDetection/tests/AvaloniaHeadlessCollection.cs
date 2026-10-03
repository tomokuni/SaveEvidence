using Xunit;

namespace EsUtil.SaveEvidence.LineDetection.Tests;

/// <summary>Avalonia ヘッドレステストの直列化用コレクション定義。</summary>
/// <remarks>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）は並列実行非対応（Dispatcher.UIThread が共有）のため、<br/>
/// このコレクションに属するテストクラスは直列実行される。<br/>
/// <see cref="Xunit.CollectionDefinitionAttribute.DisableParallelization"/> により、他のコレクション（純粋ロジック <c>[Fact]</c>）と並列実行しないことを保証する。<br/>
/// 純粋ロジックテスト（<c>[Fact]</c>）はコレクションに属さず並列実行される。
/// </remarks>
[CollectionDefinition("AvaloniaHeadless", DisableParallelization = true)]
public sealed class AvaloniaHeadlessCollection;
