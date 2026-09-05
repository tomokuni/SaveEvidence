# PlatformFactory

## 目次

- [PlatformFactory](#platformfactory)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [プラットフォームサービスの生成](#プラットフォームサービスの生成)
    - [座標変換ポート](#座標変換ポート)
    - [画面座標変換](#画面座標変換)
  - [6. 設計方針](#6-設計方針)
    - [公開 API の最小化](#公開-api-の最小化)
    - [一方向依存](#一方向依存)
  - [7. サンプル](#7-サンプル)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. プロジェクト構成](#9-プロジェクト構成)
  - [10. ライセンス](#10-ライセンス)

---

## 1. 概要

実行時プラットフォーム（Windows / 他環境）に応じたプラットフォーム固有実装を生成する静的ファクトリと、座標変換・画面キャプチャ・要素検出などの抽象化 API を提供するライブラリです。

## 2. 機能一覧

- **Factory Method パターン**: `PlatformFactory.CreateXxx()` で実行時プラットフォームに応じた既定実装を生成（Windows = Win32 API 実装 / 他環境 = NoOp 実装）
- **プラットフォーム判定の一元化**: `OperatingSystem.IsWindows()` による分岐を本ライブラリに集約（各プロジェクトの重複分岐を排除・DRY）
- **抽象化 API**: `ICursorService` / `IElementFinder` / `ICursorOverrideGuard` / `ICaptureService` / `IWindowService` / `IGlobalHotKeyService<T>` / `ICoordinatePort` のインターフェースで具象型への依存を排除（DIP）
- **座標変換ポート**: `CoordinatePort`（`Identity` / `Compose` / `ScrollOffset` / `Scale` / `DomainScale` / `ImageLayout` / `ImageLayoutScaleX`）でドメイン ↔ DIP ↔ 画面物理の変換を Func 委譲で提供
- **座標変換ユーティリティ**: `ScreenConverter`（ローカル DIP ↔ 画面物理）
- **画面キャプチャ**: `ICaptureService.CaptureArea` / `CaptureVirtualScreen`（Windows: System.Drawing + Win32 API）
- **要素検出**: `IElementFinder`（カーソル位置のウィンドウ / スクリーン検出・一覧取得）
- **グローバルホットキー**: `IGlobalHotKeyService<T>`（登録・WM_HOTKEY 受信・アクション配信。WndProc サブクラス化を内包）
- **UI ハング監視**: `Watchdog`（UI スレッド完全ハング時の最終保険。オーバーレイ退避）
- **画像ユーティリティ**: `BitmapExtensions`（領域切り出し・パディング付きコピー）/ `UnifiedBitmap`（複数ビットマップ種別を横断する軽量ラッパーのコア）。内部に `SKBitmap`(BGRA8888・Premul) を所有し、`SkiaSharp` のみに依存したピクセル操作（`GetPixel` / `SetPixel` / `Clear` / `Clone`）と `Create` / `FromSKBitmap` / `ToSKBitmap`（SKBitmap 生成・変換）を提供。/ `UnifiedBitmapExtensions`（生成・変換拡張。`ToUnifiedBitmap` / `ToAvaloniaBitmap` / `ToSystemDrawingBitmap` で Avalonia / System.Drawing / SkiaSharp と高速変換）
- **色生成拡張**: `ColorExtensions.FromAColor`（C# 14 静的拡張メンバー。名前付き色とアルファ値から `Color` を生成し、RGB 直指定の可読性問題を解消）
- **プラットフォーム非依存の公開 API**: 公開型は Avalonia / .NET 標準型（`Bitmap` / `PixelPoint` / `PixelRect` / `IPlatformHandle`）のみを使用し、Win32 ハンドル・System.Drawing 型を公開しない

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\PlatformFactory\src\PlatformFactory.csproj" />
```

必要な名前空間:

```csharp
using Estable.AvaloniaUI.Platform;          // PlatformFactory / ICoordinatePort / CoordinatePort / ScreenConverter / Watchdog 等
using Estable.AvaloniaUI.Platform.Imaging;  // BitmapExtensions / UnifiedBitmap / UnifiedBitmapExtensions / ImageProcessor 等
```

## 5. クイックスタート

### プラットフォームサービスの生成

```csharp
// 実行時プラットフォーム（Windows / 他環境）に応じた既定実装を生成する
var cursorService = PlatformFactory.CreateCursorService();
var captureService = PlatformFactory.CreateCaptureService(logger);
var elementFinder = PlatformFactory.CreateElementFinder();
var windowService = PlatformFactory.CreateWindowService();
var cursorOverrideGuard = PlatformFactory.CreateCursorOverrideGuard();
var hotKeyService = PlatformFactory.CreateGlobalHotKeyService<MyActionType>();

// ホストが独自実装を持つ場合は、本ファクトリを使わず各インターフェースに直接注入する（DIP）
```

### 座標変換ポート

```csharp
// 等価変換（1 ドメイン = 1 DIP・無制限）
ICoordinatePort identity = CoordinatePort.Identity;

// スクロールオフセット平行移動（コンテンツ ↔ ビューポート）
ICoordinatePort scroll = CoordinatePort.ScrollOffset(() => scrollViewer.Offset);

// ドメイン = ローカル DIP × 倍率（呼び出し毎評価。DPI 200% 等）
ICoordinatePort scale = CoordinatePort.DomainScale(() => 2.0);

// 画像レイアウト（レターボックス・Uniform スケール）。Forward = DIP → 論理（元画像ピクセル）
ICoordinatePort layout = CoordinatePort.ImageLayout(() => viewport.Bounds.Size, sourceSize);

// 2 ポート合成（inner 先行 → outer 後続）
ICoordinatePort composed = CoordinatePort.Compose(layout, scroll);
```

### 画面座標変換

```csharp
// コントロールローカル DIP → 画面物理
var screenPt = ScreenConverter.LocalDipToScreen(control, localDip);

// 画面物理 → コントロールローカル DIP
var localDip = ScreenConverter.ScreenToLocalDip(control, screenPt);
```

## 6. 設計方針

### 公開 API の最小化

プラットフォーム固有実装（`Win\*` / `NoOp\*`）と内部ユーティリティ（`CoordinateMath` / `PlatformLog`）はすべて internal です。公開 API は抽象（各インターフェース）とプラットフォーム非依存型のみで構成され、具象型への依存を排除します（DIP・カプセル化）。

### 一方向依存

`PlatformFactory` → 各インターフェース（抽象）→ Win / NoOp 実装（internal）の一方向の階層構造です。上位 AP（DragRectControl / SnipCaptureUI 等）はインターフェース経由で利用します。

## 7. サンプル

サンプルアプリケーションはありません（ライブラリプロジェクトのため）。各コントロールプロジェクト（DragRectControl / LoupeControl / FadeMaskControl 等）の sample が本ライブラリを利用しています。

## 8. ドキュメント

- [外部機能仕様書](SPEC_ExtFunc.md)
- [システム仕様書](SPEC_System.md)

## 9. プロジェクト構成

```text
PlatformFactory/
├── src/
│   ├── PlatformFactory.csproj      # ライブラリプロジェクト（Avalonia / Logging.Abstractions / System.Drawing.Common）
│   └── Platform/
│       ├── PlatformFactory.cs      # 公開ファクトリ（Factory Method パターン）
│       ├── ICursorService.cs       # カーソル操作抽象
│       ├── IElementFinder.cs       # 要素検出抽象
│       ├── ICursorOverrideGuard.cs # カーソル上書き抑止抽象
│       ├── ICaptureService.cs      # キャプチャ抽象
│       ├── IWindowService.cs       # ウィンドウ操作抽象
│       ├── IGlobalHotKeyService.cs # グローバルホットキー抽象
│       ├── Watchdog.cs             # UI ハング監視（public）
│       ├── ElementFinderResults.cs # 要素検出結果 record
│       ├── ColorExtensions.cs      # 色生成拡張（C# 14 静的拡張メンバー）
│       ├── PlatformLog.cs          # ログ定義（internal）
│       ├── Coordinate/             # 座標変換（ICoordinatePort / CoordinatePort / ScreenConverter / CoordinateMath）
│       ├── Imaging/                # 画像ユーティリティ（BitmapExtensions / UnifiedBitmap / UnifiedBitmapExtensions / ImageProcessor）
│       ├── Win/                    # Windows 実装（internal: Win32 API / P-Invoke 集約）
│       └── NoOp/                   # 他環境フォールバック（internal: シングルトン）
└── tests/                          # 単体テスト
```

## 10. ライセンス

MIT
