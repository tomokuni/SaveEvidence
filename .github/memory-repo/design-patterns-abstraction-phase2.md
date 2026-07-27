# Phase 2: デザインパターンによるWin依存抽象化

## 概要
Windows環境依存処理をBridge/Strategy/Abstract Factoryパターンで抽象化。
CaptureManagerから分離したSelectionOverlay配下のWin依存処理を論理的な纏まりでグルーピング。

## 成果物一覧

### 新規作成ファイル
| ファイル | パターン | 役割 |
|---|---|---|
| `Controls/SelectionOverlay/IPlatformFactory.cs` | Abstract Factory | 5種のWin依存サービス生成インターフェース |
| `Controls/SelectionOverlay/Win/PlatformFactory_Win.cs` | Abstract Factory | Windows実装の具象ファクトリ |
| `Controls/SelectionOverlay/ICursorService.cs` | Bridge | カーソル位置+形状を統合 |
| `Controls/SelectionOverlay/Win/CursorService_Win.cs` | Bridge | Win32カーソル操作実装 |

### 既存改修ファイル
| ファイル | 変更内容 |
|---|---|
| `Program.cs` | DI登録: IPlatformFactory→PlatformFactory_Win, 各サービスはFactory経由で生成 |
| `MainWindow.axaml.cs` | ICursorService+IWindowFinder+IScreenCaptureCompositor+IMagnifierManagerをDI解決 |
| `SelectionWindow.axaml.cs` | コンストラクタで5つの依存をDI受取 (`new XXX_Win()` 排除) |

### 削除ファイル
- `ICursorHelper.cs`, `CropCursorService_Win.cs`, `CursorHelper_Win.cs`, `ICropCursorService.cs`

## 採用パターン
- **Abstract Factory** (`IPlatformFactory`): OS固有オブジェクト群を一括生成
- **Bridge** (`ICursorService`, `IScreenCaptureCompositor`, `IMagnifierManager`): 実装と抽象を分離
- **Strategy** (`IWindowFinder`): アルゴリズムの差し替え
- **Adapter**（PixelRect ↔ Rectangle拡張メソッド）: OS非依存型変換

## DI登録パターン (Program.cs)
```csharp
services.AddSingleton<IPlatformFactory, PlatformFactory_Win>();
services.AddSingleton<ICursorService>(sp => sp.GetRequiredService<IPlatformFactory>().CreateCursorService());
services.AddSingleton<IWindowFinder>(sp => sp.GetRequiredService<IPlatformFactory>().CreateWindowFinder());
services.AddSingleton<ISourceRegionExtractor>(sp => sp.GetRequiredService<IPlatformFactory>().CreateSourceRegionExtractor());
services.AddSingleton<IScreenCaptureCompositor>(sp => sp.GetRequiredService<IPlatformFactory>().CreateScreenCaptureCompositor());
services.AddSingleton<IMagnifierManager>(sp => sp.GetRequiredService<IPlatformFactory>().CreateMagnifierManager());
```

## SelectionWindowコンストラクタシグネチャ
```csharp
public SelectionWindow(
    CaptureType captureType,
    Action<Bitmap?, Rectangle?> onComplete,
    ICursorService cursorService,
    IWindowFinder windowFinder,
    IScreenCaptureCompositor compositor,
    IMagnifierManager magnifier)
```

## FallbackCapture対応
`MagnifierManager_Win` 固有の `FallbackCapture` は `IMagnifierManager` 未定義のため、
`SelectionWindow` 内で `is MagnifierManager_Win magWin` キャスト後に設定。
