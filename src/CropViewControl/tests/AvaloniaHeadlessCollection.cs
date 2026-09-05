using Xunit;

namespace Estable.AvaloniaUI.CropView.Tests;

/// <summary>Avalonia ヘッドレステストの直列化用コレクション定義。</summary>
/// <remarks>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）は並列実行非対応（Dispatcher.UIThread が共有）のため、
/// このコレクションに属するテストクラスは直列実行される。<br/>
/// 純粋ロジックテスト（<c>[Fact]</c>）はコレクションに属さず並列実行される。
/// </remarks>
[CollectionDefinition("AvaloniaHeadless")]
public sealed class AvaloniaHeadlessCollection;
