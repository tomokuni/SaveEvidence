# Platform.Native 外部機能仕様書

UI フレームワーク非依存の実装ライブラリとして、以下の公開 API を提供します。

## 1. 公開型

| 型 | 説明 |
| --- | --- |
| `PlatformFactory` | 実行時プラットフォームに応じた既定実装を生成する静的ファクトリ（Factory Method パターン） |
| `EsBitmapDrawingExtensions` | `System.Drawing.Bitmap` ⇔ `EsBitmap` の変換拡張（Windows 専用） |

実装クラス（`Win*` / `NoOp*`）と `PlatformLog` は internal です。

## 2. PlatformFactory（静的ファクトリ）

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `CreateCursorService` | `ICursorService CreateCursorService(ILogger? logger = null)` | カーソル操作サービスを生成 |
| `CreateElementFinder` | `IElementFinder CreateElementFinder()` | 要素検出サービスを生成 |
| `CreateCursorOverrideGuard` | `ICursorOverrideGuard CreateCursorOverrideGuard()` | カーソル上書き抑止ガードを生成 |
| `CreateCaptureService` | `ICaptureService CreateCaptureService(ILogger? logger = null)` | キャプチャサービスを生成 |
| `CreateWindowService` | `IWindowService CreateWindowService(ILogger? logger = null)` | ウィンドウ操作サービスを生成 |
| `CreateGlobalHotKeyService` | `IGlobalHotKeyService<T> CreateGlobalHotKeyService<T>() where T : notnull` | グローバルホットキーサービスを生成 |

生成される型は実行時プラットフォームで切り替わります（Windows = Win32 実装 / 他環境 = NoOp 実装）。

## 3. EsBitmapDrawingExtensions（System.Drawing・Windows 専用）

`EsBitmap`（`Platform.Core` 提供）と `System.Drawing.Bitmap` を相互変換します。
System.Drawing は Windows 専用のため、各メソッドに `[SupportedOSPlatform("windows")]` を付与しています。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `ToEsBitmap` | `EsBitmap ToEsBitmap(this DrawingBitmap bitmap)` | `System.Drawing.Bitmap` から生成（複製。元は変更されない） |
| `ToDrawingBitmap` | `DrawingBitmap ToDrawingBitmap(this EsBitmap bitmap)` | `System.Drawing.Bitmap`（Format32bppPArgb）へ変換（複製を所有） |

> `ToDrawingBitmap` は複製を返すため、呼び出し側が所有・破棄します。

## 4. 利用例

```csharp
using EsUtil.Platform.Core;     // サービス抽象 / コア値型
using EsUtil.Platform.Native;   // PlatformFactory（既定実装のファクトリ）

// 実行時プラットフォームに応じた既定実装を生成する
var windowService = PlatformFactory.CreateWindowService(logger);
var captureService = PlatformFactory.CreateCaptureService(logger);
var hotKeyService = PlatformFactory.CreateGlobalHotKeyService<CaptureType>();

// ウィンドウは UI フレームワーク非依存のハンドルで指定する
windowService.HideFromTaskSwitcher(new EsPlatformHandle(nativeHandle, "HWND"));
```

## 5. 利用側の注意

- 本プロジェクトは UI フレームワークを参照しません。UI 側は各フレームワーク固有の型から
  ハンドルを取得して `EsPlatformHandle` へ変換してから渡します（Avalonia 版は `EsPlatformHandleAvaloniaInterop`）。
- ホストが独自実装を注入する場合は、本ファクトリを使わず各インターフェースへ直接注入します（DIP）。
