# Platform.AvaloniaUI

## 目次

- [Platform.AvaloniaUI](#platformavaloniaui)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [コア型との相互変換](#コア型との相互変換)
    - [カーソル上書き抑止のアタッチ](#カーソル上書き抑止のアタッチ)
    - [座標変換ポート](#座標変換ポート)
    - [画面座標変換](#画面座標変換)
  - [6. 設計方針](#6-設計方針)
    - [公開 API の最小化](#公開-api-の最小化)
    - [依存方向](#依存方向)
  - [7. サンプル](#7-サンプル)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. プロジェクト構成](#9-プロジェクト構成)
  - [10. ライセンス](#10-ライセンス)

---

## 1. 概要

Avalonia UI 固有のアダプタ（画面座標変換・コア型との相互変換・画像／幾何アダプタ）を提供するライブラリです。
UI フレームワーク非依存の抽象・値型・純粋ロジックは `Platform.Core`、ネイティブ（OS）実装とファクトリは `Platform.Native` が担います。

## 2. 機能一覧

- **画面座標変換**: `ScreenConverter`（コントロールローカル DIP ↔ 画面物理。Avalonia の `TopLevel` に依存するため本プロジェクトに残す）
- **座標変換ポート**: `ICoordTransform` / `CoordTransform` は UI フレームワーク非依存のコア型として `Platform.Core` が提供
- **ネイティブ実装とファクトリ**: `PlatformFactory.CreateXxx()` と Win32 / NoOp 実装は `Platform.Native` が提供
- **コア型との相互変換**: `EsKeyAvaloniaInterop`（キー）/ `EsWindowStateAvaloniaInterop`（ウィンドウ状態）/ `EsGeometryAvaloniaInterop`（`EsPoint`・`EsSize`・`EsVector`・`EsRect`・`EsPointInt`・`EsRectInt`）/ `EsPlatformHandleAvaloniaInterop`（`IPlatformHandle`）/ `ColorAvaloniaInterop`（`System.Drawing.Color`）
- **カーソル抑止の仲介**: `CursorOverrideGuardAvaloniaExtensions.Attach(this ICursorOverrideGuard, TopLevel)`（`TopLevel` からハンドルを取得して Core の `Attach` へ委譲）
- **画像ユーティリティ**: `EsBitmap`（`Platform.Core` 提供）の Avalonia 向け拡張（`EsBitmapAvaloniaExtensions`）と `IImage`
- **描画アダプタ**: `AvaloniaCanvas`（`Common` の描画ポート `IEsCanvas` を `DrawingContext` へ適合させる。塗り・枠線・ラベル・画像の描画を写す） アダプタ（`EsBitmapAvaloniaImage`）、容量制限付きキャッシュ（`CapacityLimitedCache<TKey, TValue>`）
- **ブラシキャッシュ**: `SolidColorBrushCache`（`System.Drawing.Color` の色 → `SolidColorBrush` の変換と容量管理を単一所有する。描画アダプタとラベル要素の配置が共用する）
- **色生成拡張**: `ColorExtensions.FromAColor`（C# 14 静的拡張メンバー。名前付き色とアルファ値から `Color` を生成し、RGB 直指定の可読性問題を解消）

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\Platform.AvaloniaUI\src\Platform.AvaloniaUI.csproj" />
```

本プロジェクトは `Platform.Core` と `Platform.Native` を参照しているため、
本プロジェクトを参照すればそれらの型も利用できます。

必要な名前空間:

```csharp
using EsUtil.Platform.AvaloniaUI;          // ScreenConverter / 相互変換 / 幾何アダプタ / ColorExtensions
using EsUtil.Platform.AvaloniaUI.Imaging;  // EsBitmapAvaloniaExtensions / EsBitmapAvaloniaImage / CapacityLimitedCache
using EsUtil.Platform.Core;                     // サービス抽象 / コア値型
using EsUtil.Platform.Native;                   // PlatformFactory（既定実装のファクトリ）
using EsUtil.Platform.Core.Imaging;             // EsBitmap / EsBitmapDrawingExtensions
using EsUtil.Platform.Core.Coordinate;          // ICoordTransform / CoordTransform
using EsUtil.Platform.Core.Geometry;            // EsPoint / EsSize / EsVector / EsRect / EsPointInt / EsRectInt / EsPlatformHandle
```

## 5. クイックスタート

### コア型との相互変換

UI フレームワーク非依存のコア型と Avalonia の型を相互変換します。

```csharp
using EsUtil.Platform.AvaloniaUI;   // 相互変換拡張
using EsUtil.Platform.Core.Geometry;    // EsPoint / EsRectInt 等

// Avalonia → コア（サービス抽象へ渡すとき）
EsRectInt rect = captureRect.ToEsRectInt();
EsPlatformHandle handle = window.TryGetPlatformHandle().ToEsPlatformHandle();
EsPoint point = avaloniaPoint.ToEsPoint();

// コア → Avalonia（描画・表示境界）
PixelRect back = rect.ToAvaloniaPixelRect();
```

### カーソル上書き抑止のアタッチ

`ICursorOverrideGuard.Attach` は UI 非依存のハンドルを受け取るため、Avalonia の `TopLevel` からハンドルを取得する拡張メソッドを提供します。

```csharp
var guard = PlatformFactory.CreateCursorOverrideGuard();
guard.Attach(window);   // TopLevel → EsPlatformHandle の変換を仲介する
```

### 座標変換ポート

座標変換ポートは UI フレームワーク非依存のコア型として `Platform.Core`（`EsUtil.Platform.Core.Coordinate` 名前空間）が提供します。座標はコア幾何型（`EsPoint` / `EsSize` / `EsVector`）で扱います。

```csharp
// 等価変換（1 ドメイン = 1 DIP・無制限）
ICoordTransform identity = CoordTransform.Identity;

// スクロールオフセット平行移動（コンテンツ ↔ ビューポート）
ICoordTransform scroll = CoordTransform.ScrollOffset(() => scrollViewer.Offset.ToEsVector());

// ドメイン = ローカル DIP × 倍率（呼び出し毎評価。DPI 200% 等）
ICoordTransform scale = CoordTransform.DomainScale(() => 2.0);

// 画像レイアウト（レターボックス・Uniform スケール）。Forward = DIP → 論理（元画像ピクセル）
ICoordTransform layout = CoordTransform.ImageLayout(() => viewport.Bounds.Size.ToEsSize(), sourceSize.ToEsSize());

// 2 ポート合成（inner 先行 → outer 後続）
ICoordTransform composed = CoordTransform.Compose(layout, scroll);
```

Avalonia の `Point` / `Size` / `Vector` を受け渡す場合は、境界で `.ToEsPoint()` / `.ToEsSize()` / `.ToEsVector()`（逆は `.ToAvaloniaPoint()` 等）で変換します。

### 画面座標変換

```csharp
// コントロールローカル DIP → 画面物理
var screenPt = ScreenConverter.LocalDipToScreen(control, localDip);

// 画面物理 → コントロールローカル DIP
var localDip = ScreenConverter.ScreenToLocalDip(control, screenPt);
```

## 6. 設計方針

### 責務の分割

| プロジェクト | 責務 |
| --- | --- |
| `Platform.Core` | UI 非依存の抽象・値型・純粋ロジック |
| `Platform.Native` | Win32 / NoOp 実装とファクトリ（UI 非依存） |
| 本プロジェクト | Avalonia 固有のアダプタ（`TopLevel` / Avalonia 型に依存するもの）のみ |

### 公開 API の最小化

Win32 / NoOp 実装は `Platform.Native` が唯一の生成経路となるため internal です。
本プロジェクトの公開 API は Avalonia 固有のアダプタ（`ScreenConverter` / 相互変換 / 画像・幾何アダプタ）のみで構成されます。

### 依存方向

`Platform.AvaloniaUI`（本プロジェクト）→ `Platform.Native` → `Platform.Core` の一方向の階層構造です。上位 AP（MarqueeControl / CaptureSessionControl.AvaloniaUI 等）は本プロジェクトを参照すれば、必要な抽象と実装をすべて利用できます。

## 7. サンプル

サンプルアプリケーションはありません（ライブラリプロジェクトのため）。各コントロールプロジェクト（MarqueeControl / LoupeControl 等）の sample が本ライブラリを利用しています。

## 8. ドキュメント

- [外部機能仕様書](SPEC_ExtFunc.md)
- [システム仕様書](SPEC_System.md)

## 9. プロジェクト構成

```text
Platform.AvaloniaUI/
├── src/
│   ├── Platform.AvaloniaUI.csproj      # ライブラリプロジェクト（Avalonia / Avalonia.Skia）
│   └── Platform/
│       ├── ColorExtensions.cs      # 色生成拡張（C# 14 静的拡張メンバー）
│       ├── Coordinate/             # ScreenConverter（Avalonia の TopLevel に依存するため本プロジェクトに残す）
│       ├── Imaging/                # 画像・描画アダプタ（EsBitmapAvaloniaExtensions / EsBitmapAvaloniaImage / AvaloniaCanvas / CapacityLimitedCache）pacityLimitedCache）
│       └── Interop/                # コア型との相互変換（EsKeyAvaloniaInterop / EsWindowStateAvaloniaInterop / EsGeometryAvaloniaInterop / EsPlatformHandleAvaloniaInterop / ColorAvaloniaInterop / CursorOverrideGuardAvaloniaExtensions）
└── tests/                          # 単体テスト
```

ファクトリと Win32 / NoOp 実装は [`Platform.Native`](../Platform.Native/README.md)、
抽象・値型・純粋ロジックは [`Platform.Core`](../Platform.Core/README.md) を参照してください。

## 10. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
