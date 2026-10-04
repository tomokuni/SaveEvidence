# Platform.AvaloniaUI

## 目次

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
  - [5.1 コア型との相互変換](#51-コア型との相互変換)
  - [5.2 カーソル上書き抑止のアタッチ](#52-カーソル上書き抑止のアタッチ)
  - [5.3 座標変換ポート](#53-座標変換ポート)
  - [5.4 画面座標変換](#54-画面座標変換)
- [6. 設計方針](#6-設計方針)
  - [6.1 責務の分割](#61-責務の分割)
  - [6.2 公開 API の最小化](#62-公開-api-の最小化)
  - [6.3 依存方向](#63-依存方向)
- [7. サンプル](#7-サンプル)
- [8. ドキュメント](#8-ドキュメント)
- [9. プロジェクト構成](#9-プロジェクト構成)
- [10. テスト](#10-テスト)
- [11. ライセンス](#11-ライセンス)

---

## 1. 概要

Avalonia UI 固有のアダプタ（画面座標変換・コア型との相互変換・画像／幾何アダプタ）を提供するライブラリです。
UI フレームワーク非依存の抽象・値型・純粋ロジックと、ネイティブ（OS）の Win32 実装は `EsUtil.Platform.Core`（後者は `Native/` 配下） が担います。

## 2. 機能一覧

- **画面座標変換**: `ScreenConverter`（コントロールローカル DIP ↔ 画面物理。 Avalonia の `TopLevel` に依存するため本プロジェクトに残す）
- **座標変換ポート**: `ICoordTransform` / `CoordTransform` は UI フレームワーク非依存のコア型として `EsUtil.Platform.Core` が提供
- **ネイティブ実装**: Win32 実装（`EsUtil.Platform.Core.Native.Win`）は `EsUtil.Platform.Core` が提供（**実行時のプラットフォーム分岐は持たず、ホストが生成して抽象型として注入する**）
- **コア型との相互変換**: `EsKeyAvaloniaInterop`（キー）/ `EsWindowStateAvaloniaInterop`（ウィンドウ状態）/ `EsGeometryAvaloniaInterop`（`EsPoint`・`EsSize`・`EsVector`・`EsRect`・`EsPointInt`・`EsRectInt`）/ `EsPlatformHandleAvaloniaInterop`（`IPlatformHandle`）/ `ColorAvaloniaInterop`（`System.Drawing.Color`）
- **カーソル抑止の仲介**: `CursorOverrideGuardAvaloniaExtensions.Attach(this ICursorOverrideGuard, TopLevel)`（`TopLevel` からハンドルを取得して Core の `Attach` へ委譲）
- **画像ユーティリティ**: `EsBitmap`（`EsUtil.Platform.Core` 提供）の Avalonia 向け拡張（`EsBitmapAvaloniaExtensions`）と `IImage`
- **画像アダプタ**（`EsBitmapAvaloniaImage`）、容量制限付きキャッシュ（`CapacityLimitedCache<TKey, TValue>`）
- **ブラシキャッシュ**: `SolidColorBrushCache`（`System.Drawing.Color` の色 → `SolidColorBrush` の変換と容量管理を単一所有する。 描画アダプタとラベル要素の配置が共用する）
- **テキスト実寸の測定**: `AvaloniaTextMeasure`（ラベル要素の配置矩形を実寸から決めるための唯一の入口。 UI ごとに測定を書くと片方だけ実寸 0 になり、文字が切り落とされる）
- **色生成拡張**: `ColorExtensions.FromAColor`（C# 14 静的拡張メンバー。 名前付き色とアルファ値から `Color` を生成し、RGB 直指定の可読性問題を解消）

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\Platform.AvaloniaUI\src\Platform.AvaloniaUI.csproj" />
```

本プロジェクトは `EsUtil.Platform.Core` を参照しているため、
本プロジェクトを参照すればそれらの型も利用できます。

必要な名前空間:

```csharp
using EsUtil.SaveEvidence.Platform.AvaloniaUI;               // ScreenConverter / 相互変換 / 幾何アダプタ / ColorExtensions
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;       // EsBitmapAvaloniaExtensions / EsBitmapAvaloniaImage / CapacityLimitedCache / AvaloniaTextMeasure
using EsUtil.Platform.Core;                     // EsKey / EsWindowState / EsPlatformHandle 等
using EsUtil.Platform.Core.Native;              // サービス抽象（ICaptureService 等）
using EsUtil.Platform.Core.Native.Win;          // 既定実装を直接生成する場合のみ（ホスト）
using EsUtil.Platform.Core.Native.Imaging;      // EsBitmapDrawingExtensions（Windows 専用）
using EsUtil.Platform.Core.Imaging;             // EsBitmap
using EsUtil.Platform.Core.Coord;               // ICoordTransform / CoordTransform
using EsUtil.Platform.Core.Geometry;            // EsPoint / EsSize / EsVector / EsRect / EsPointInt / EsRectInt
```

## 5. クイックスタート

### 5.1 コア型との相互変換

UI フレームワーク非依存のコア型と Avalonia の型を相互変換します。

```csharp
using EsUtil.SaveEvidence.Platform.AvaloniaUI;   // 相互変換拡張
using EsUtil.Platform.Core.Geometry;    // EsPoint / EsRectInt 等

// Avalonia → コア（サービス抽象へ渡すとき）
EsRectInt rect = captureRect.ToEsRectInt();
EsPlatformHandle handle = window.TryGetPlatformHandle().ToEsPlatformHandle();
EsPoint point = avaloniaPoint.ToEsPoint();

// コア → Avalonia（描画・表示境界）
PixelRect back = rect.ToAvaloniaPixelRect();
```

### 5.2 カーソル上書き抑止のアタッチ

`ICursorOverrideGuard.Attach` は UI 非依存のハンドルを受け取るため、Avalonia の `TopLevel` からハンドルを取得する拡張メソッドを提供します。

```csharp
var guard = new WinCursorOverrideGuard();
guard.Attach(window);   // TopLevel → EsPlatformHandle の変換を仲介する
```

### 5.3 座標変換ポート

座標変換ポートは UI フレームワーク非依存のコア型として `EsUtil.Platform.Core`（`EsUtil.Platform.Core.Coord` 名前空間）が提供します。 軸は「ローカル DIP ↔ 対象座標」の 1 つだけで、DIP とデバイスピクセルの換算は `DpiScaler` が単一所有します。 座標はコア幾何型（`EsPoint` / `EsSize` / `EsVector`）で扱います。

```csharp
// 等価変換（ローカル DIP = 対象座標・無制限）
ICoordTransform identity = CoordTransform.Identity;

// 対象 = ローカル DIP × 倍率（呼び出し毎評価。 倍率は 対象座標 / ローカル DIP）
ICoordTransform scale = CoordTransform.ClientToSource(() => 2.0);

// 画像サイズで境界を付ける（選択を画像外へ出さないためのクランプ）
ICoordTransform bounded = CoordTransform.ClientToSource(() => 2.0, () => imageSize.ToEsSize());
```

Avalonia の `Point` / `Size` / `Vector` を受け渡す場合は、境界で `.ToEsPoint()` / `.ToEsSize()` / `.ToEsVector()`（逆は `.ToAvaloniaPoint()` 等）で変換します。

本プロジェクトは座標変換ポートを提供しません（画面物理は対象座標ではない別の軸のため）。 画面座標は下記の名前付きメソッドを使います。

### 5.4 画面座標変換

```csharp
// コントロールローカル DIP → 画面物理
var screenPt = ScreenConverter.LocalDipToScreen(control, localDip);

// 画面物理 → コントロールローカル DIP
var localDip = ScreenConverter.ScreenToLocalDip(control, screenPt);
```

## 6. 設計方針

### 6.1 責務の分割

| プロジェクト | 責務 |
| --- | --- |
| `EsUtil.Platform.Core` | UI 非依存の抽象・値型・純粋ロジック（`Native/` 配下は Win32 実装） |
| 本プロジェクト | Avalonia 固有のアダプタ（`TopLevel` / Avalonia 型に依存するもの）のみ |

### 6.2 公開 API の最小化

Win32 実装（`Native.Win`）はホストが生成して抽象型として注入する（実行時の分岐を持たない）。
本プロジェクトの公開 API は Avalonia 固有のアダプタ（`ScreenConverter` / 相互変換 / 画像・幾何アダプタ）のみで構成されます。

### 6.3 依存方向

`Platform.AvaloniaUI`（本プロジェクト）→ `EsUtil.Platform.Core`（`Native/` の Win32 実装を含む） の一方向の階層構造です。 上位 AP（MarqueeControl / Parts.CapturePreviewControl.AvaloniaUI 等）は本プロジェクトを参照すれば、必要な抽象と実装をすべて利用できます。

## 7. サンプル

サンプルアプリケーションはありません（ライブラリプロジェクトのため）。 各コントロールプロジェクト（MarqueeControl / LoupeControl 等）の sample が本ライブラリを利用しています。

## 8. ドキュメント

- [外部機能仕様書](docs/SPEC_API.md)
- [システム仕様書](docs/SPEC_System.md)

## 9. プロジェクト構成

```text
Platform.AvaloniaUI/
├── src/
│   ├── Platform.AvaloniaUI.csproj      # ライブラリプロジェクト（Avalonia / Avalonia.Skia）
│   └── Platform/
│       ├── ColorExtensions.cs      # 色生成拡張（C# 14 静的拡張メンバー）
│       ├── Coord/             # ScreenConverter（Avalonia の TopLevel に依存するため本プロジェクトに残す）
│       ├── Imaging/                # 画像アダプタ（EsBitmapAvaloniaExtensions / EsBitmapAvaloniaImage / CapacityLimitedCache / AvaloniaTextMeasure）
│       └── Interop/                # コア型との相互変換（EsKeyAvaloniaInterop / EsWindowStateAvaloniaInterop / EsGeometryAvaloniaInterop / EsPlatformHandleAvaloniaInterop / ColorAvaloniaInterop / CursorOverrideGuardAvaloniaExtensions）
└── tests/                          # 単体テスト
```

抽象・値型・純粋ロジックと Win32 実装の全体は [`EsUtil.Platform.Core`](../EsUtil.Platform.Core/README.md) を参照してください（Win32 実装は `Native/` 配下）。

## 10. テスト

`src/Platform.AvaloniaUI/tests` が本プロジェクトのテストを持ちます（xUnit v3）。 実行手順と合否基準は [検証手順.md](../../.github/検証手順.md) が定めます。

`powershell
dotnet test src/Platform.AvaloniaUI/tests/Platform.AvaloniaUI.Tests.csproj
`

## 11. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
