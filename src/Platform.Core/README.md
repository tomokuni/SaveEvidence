# Platform.Core

`Platform.AvaloniaUI` / `Platform.MewUI` が共有する **UI フレームワーク非依存の型**（キーと入力・幾何・座標変換・画像・OS サービス抽象）を提供するライブラリです。

## 概要

UI フレームワークに依存していた型を本プロジェクトへ切り出すことで、アプリ（`app.Core`）と各コントロールのコア・提示層から、UI フレームワーク非依存のまま利用できます。

**実行時のプラットフォーム分岐を持ちません**。OS サービスの Windows 実装（`Native/Win`）はホストが生成して抽象型として注入します。テストはモックを注入できるため、Win32 実行環境を必要としません。

## 使い方

```csharp
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;

// 幾何（DIP 相当の値型。UI の座標型は境界で 1 行変換する）
var rect = EsRect.FromLTRB(10, 20, 110, 120);
var center = rect.Center;                             // EsPoint
var inside = rect.ClampToArea(new EsSize(200, 100));  // 領域内へクランプ

// 座標系の変換ポート（画像レイアウト・スクロール・スケーリング）
var port = CoordTransform.ImageLayout(() => new EsSize(800, 600), new EsSize(1920, 1080));
var dip = port.Forward(new EsPoint(100, 100));
```

```csharp
// ホスト（Windows 前提）が OS サービスの既定実装を生成して注入する
services.AddSingleton<IGlobalHotKeyService<CaptureType>>(_ => new WinGlobalHotKeyService<CaptureType>());
```

## 含まれるもの

| グループ | 型 |
| --- | --- |
| キーと入力 | `EsKey` / `EsWindowState` / `HotKeyModifiers` / `HotKeySetting` / `CaptureType` |
| 幾何 | `EsPoint` / `EsSize` / `EsVector` / `EsRect` とその整数版、`EsThickness` / `EsRounding` / `EsAxis` |
| 座標変換 | `ICoordTransform` / `CoordTransform` / `ScrollMath` / `DpiScaler` |
| 画像 | `EsBitmap`（自前のピクセルバッファ） / `ImageDrawMath` / `ImageDrawMode` |
| OS サービス抽象 | `ICaptureService` / `ICursorService` / `IWindowService` / `IElementFinder` / `ICursorOverrideGuard` / `IGlobalHotKeyService<T>` |
| Windows 実装 | `Native.Win.*`（Win32 P/Invoke。ホストが生成して注入する） |
| 補助 | `EsPlatformHandle` / `LoggerFallback` / `DrawingColorExtensions` |

各 UI 実装は、自身の型とコア型を相互変換する Interop 層（例: `Platform.AvaloniaUI` の `EsGeometryAvaloniaInterop` / `EsKeyAvaloniaInterop`）を個別に提供します。

## 依存

- 外部パッケージは Microsoft 公式のみです（`Microsoft.Extensions.Logging.Abstractions`、`Native/Imaging` の `System.Drawing.Common`）。
- UI フレームワーク（Avalonia / MewUI / WinUI / WinForms）と SkiaSharp を参照しません（Skia への依存は [`Platform.Skia`](../Platform.Skia/README.md) が単一所有する）。
- 依存方向は `Platform.AvaloniaUI` / `Platform.MewUI` → 本プロジェクトの一方向です。

判定基準と設計判断は [`docs/SPEC_System.md`](docs/SPEC_System.md) を参照してください。

## カスタマイズ

| 差し替えたいもの | 方法 |
| --- | --- |
| OS サービスの実装 | `Native.*` の抽象を実装し、ホストで注入する（Windows 以外・テスト用のモック） |
| 各 UI の型との相互変換 | Interop 層を自作する（`Platform.AvaloniaUI` の `Es*Interop` が実装例） |
| 画像の描画 | 描画は各 UI が行う（本プロジェクトは `EsBitmap` と配置計算だけを提供する） |

## ビルド

```powershell
dotnet build src/Platform.Core/src/Platform.Core.csproj
```

## テスト

```powershell
dotnet test src/Platform.Core/tests/Platform.Core.Tests.csproj
```

## ドキュメント

- [`docs/SPEC_API.md`](docs/SPEC_API.md) — 公開 API のリファレンスと使用例
- [`docs/SPEC_System.md`](docs/SPEC_System.md) — アーキテクチャ・依存の判定基準・設計判断
- [`../README.md`](../README.md) — リポジトリ全体の層構成

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）
