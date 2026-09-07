# PlatformFactory 外部機能仕様書

## 目次

- [PlatformFactory 外部機能仕様書](#platformfactory-外部機能仕様書)
  - [目次](#目次)
  - [1. 文書概要](#1-文書概要)
  - [2. 目的](#2-目的)
  - [3. 外部利用条件](#3-外部利用条件)
    - [3.1 プロジェクト参照](#31-プロジェクト参照)
    - [3.2 必要な名前空間](#32-必要な名前空間)
  - [4. 基本的な使用方法](#4-基本的な使用方法)
    - [4.1 プラットフォームサービスの生成](#41-プラットフォームサービスの生成)
    - [4.2 座標変換ポート](#42-座標変換ポート)
    - [4.3 画面座標変換](#43-画面座標変換)
  - [5. 公開API](#5-公開api)
    - [5.1 プロパティ](#51-プロパティ)
      - [5.1.1 CoordinatePort.Identity（静的プロパティ）](#511-coordinateportidentity静的プロパティ)
    - [5.2 メソッド](#52-メソッド)
      - [5.2.1 PlatformFactory（静的ファクトリ）](#521-platformfactory静的ファクトリ)
      - [5.2.2 CoordinatePort（静的ファクトリ）](#522-coordinateport静的ファクトリ)
      - [5.2.3 ScreenConverter](#523-screenconverter)
      - [5.2.4 ColorExtensions（C# 14 静的拡張メンバー）](#524-colorextensionsc-14-静的拡張メンバー)
      - [5.2.5 PbBitmap（ピクセル操作）と PbBitmapAvaloniaExtensions / PbBitmapDrawingExtensions（生成・変換）](#525-pbbitmapピクセル操作と-pbbitmapavaloniaextensions--pbbitmapdrawingextensions生成変換)
    - [5.3 イベント](#53-イベント)
    - [5.4 型](#54-型)
      - [5.4.1 PlatformFactory](#541-platformfactory)
      - [5.4.2 インターフェース（7 種）](#542-インターフェース7-種)
      - [5.4.3 CoordinatePort](#543-coordinateport)
      - [5.4.4 ScreenConverter](#544-screenconverter)
      - [5.4.5 その他の公開型](#545-その他の公開型)
  - [6. 座標系](#6-座標系)
    - [6.1 3 層の座標](#61-3-層の座標)
    - [6.2 CoordinatePort の変換規則](#62-coordinateport-の変換規則)
  - [7. ログ](#7-ログ)
    - [7.1 LogLevel 設定ポリシー](#71-loglevel-設定ポリシー)
    - [7.2 ログ定義一覧](#72-ログ定義一覧)
  - [8. 制約事項](#8-制約事項)
  - [9. 最適化施策](#9-最適化施策)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `PlatformFactory` |
| プロジェクト | `PlatformFactory/PlatformFactory.csproj` |
| 名前空間 | `Estable.AvaloniaUI.Platform`（画像ユーティリティは `Estable.AvaloniaUI.Platform.Imaging`） |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要設計パターン | Factory Method（PlatformFactory）/ Func 委譲による DIP ポート（CoordinatePort）/ P/Invoke 集約（WinNative） |
| 主な用途 | 実行時プラットフォームに応じた既定実装の生成と、座標変換・画面キャプチャ・要素検出の抽象化 API の提供 |

## 2. 目的

`PlatformFactory` は、実行時プラットフォーム（Windows / 他環境）に応じたプラットフォーム分岐を一元化し、プラットフォーム固有実装（Win32 API ベース）とフォールバック実装（NoOp）を抽象（インターフェース）経由で提供します。

次の機能を提供します。

- プラットフォームに応じた既定実装の生成（`PlatformFactory.CreateXxx()`）
- カーソル操作（`ICursorService`）
- 要素検出（`IElementFinder`）
- カーソル上書き抑止（`ICursorOverrideGuard`）
- 画面キャプチャ（`ICaptureService`）
- ネイティブウィンドウ操作（`IWindowService`）
- グローバルホットキー（`IGlobalHotKeyService<T>`）
- 座標変換ポート（`ICoordinatePort` / `CoordinatePort` / `ScreenConverter`）
- 画像ユーティリティ（`BitmapExtensions` / `PbBitmap` / `PbBitmapAvaloniaExtensions` / `PbBitmapDrawingExtensions` / `ImageProcessor`）

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\PlatformFactory\src\PlatformFactory.csproj" />
```

### 3.2 必要な名前空間

```csharp
using Estable.AvaloniaUI.Platform;          // PlatformFactory / ICoordinatePort / CoordinatePort / ScreenConverter 等
using Estable.AvaloniaUI.Platform.Imaging;  // BitmapExtensions / PbBitmap / PbBitmapAvaloniaExtensions / PbBitmapDrawingExtensions / ImageProcessor 等
```

## 4. 基本的な使用方法

### 4.1 プラットフォームサービスの生成

```csharp
var cursorService = PlatformFactory.CreateCursorService();
var captureService = PlatformFactory.CreateCaptureService(logger);
var elementFinder = PlatformFactory.CreateElementFinder();
var windowService = PlatformFactory.CreateWindowService();
var cursorOverrideGuard = PlatformFactory.CreateCursorOverrideGuard();
var hotKeyService = PlatformFactory.CreateGlobalHotKeyService<MyActionType>();
```

ホスト（上位 AP）が独自実装を持つ場合は、本ファクトリを使わず各インターフェースに直接注入します（DIP）。

### 4.2 座標変換ポート

```csharp
// 等価変換（1 ドメイン = 1 DIP・無制限）
ICoordinatePort identity = CoordinatePort.Identity;

// スクロールオフセット平行移動（コンテンツ ↔ ビューポート）
ICoordinatePort scroll = CoordinatePort.ScrollOffset(() => scrollViewer.Offset);

// ドメイン = ローカル DIP × 倍率（呼び出し毎評価）
ICoordinatePort scale = CoordinatePort.DomainScale(() => renderScaling);

// 画像レイアウト（レターボックス・Uniform スケール）。Forward = DIP → 論理（元画像ピクセル）
ICoordinatePort layout = CoordinatePort.ImageLayout(() => viewport.Bounds.Size, sourceSize);

// 2 ポート合成（inner 先行 → outer 後続）
ICoordinatePort composed = CoordinatePort.Compose(layout, scroll);
```

変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できます。

### 4.3 画面座標変換

```csharp
var screenPt = ScreenConverter.LocalDipToScreen(control, localDip); // ローカル DIP → 画面物理
var localDip = ScreenConverter.ScreenToLocalDip(control, screenPt); // 画面物理 → ローカル DIP

// コントロールローカル DIP ↔ 画面物理の動的変換ポート（TopLevel を毎回解決）
ICoordinatePort dipPort = ScreenConverter.CreateVisualDipPort(control);
```

## 5. 公開API

公開 API は「プロパティ / メソッド / イベント / 型」の 4 分類で構成されます。
座標を扱う API は、ドメイン座標系 / DIP 座標系 / 画面物理 Pixel のどれであるかを明記します。

### 5.1 プロパティ

#### 5.1.1 CoordinatePort.Identity（静的プロパティ）

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Identity` | `static ICoordinatePort Identity { get; }` | 等価変換（A = B）の既定ポート。変換対象の座標系はポート利用時に決定される（例: ドメイン座標系 ↔ DIP 座標系） |

### 5.2 メソッド

#### 5.2.1 PlatformFactory（静的ファクトリ）

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `CreateCursorService` | `ICursorService CreateCursorService(ILogger? logger = null)` | 既定のカーソル操作サービスを生成 |
| `CreateElementFinder` | `IElementFinder CreateElementFinder()` | 既定の要素検出サービスを生成 |
| `CreateCursorOverrideGuard` | `ICursorOverrideGuard CreateCursorOverrideGuard()` | 既定のカーソル上書き抑止ガードを生成 |
| `CreateCaptureService` | `ICaptureService CreateCaptureService(ILogger? logger = null)` | 既定のキャプチャサービスを生成 |
| `CreateWindowService` | `IWindowService CreateWindowService(ILogger? logger = null)` | 既定のネイティブウィンドウ操作サービスを生成 |
| `CreateGlobalHotKeyService` | `IGlobalHotKeyService<T> CreateGlobalHotKeyService<T>() where T : notnull` | 既定のグローバルホットキーサービスを生成 |

#### 5.2.2 CoordinatePort（静的ファクトリ）

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Compose` | `static ICoordinatePort Compose(ICoordinatePort outer, ICoordinatePort inner)` | 2 ポート合成（inner 先行 → outer 後続。Bounds は outer 優先） |
| `ScrollOffset` | `static ICoordinatePort ScrollOffset(Func<Vector> offsetProvider)` | スクロールオフセット平行移動（コンテンツ ↔ ビューポート。両者とも DIP 座標系） |
| `Scale` | `static ICoordinatePort Scale(double factor)` | 原点基準の等方スケーリング（factor ≤ 0 は 1.0 扱い・固定値） |
| `DomainScale` | `static ICoordinatePort DomainScale(Func<double> factorProvider)` | ドメイン座標系 = ローカル DIP 座標系 × 倍率（呼び出し毎評価） |
| `ImageLayout` | `static ICoordinatePort ImageLayout(Func<Size> viewportSizeProvider, Size sourceSize, Func<bool>? uniformProvider = null)` | 画像レイアウト（レターボックス・スケール）の動的ポート（Forward = DIP 座標系 → 論理 = ドメイン座標系（元画像ピクセル）、Bounds = 元画像サイズ） |
| `ImageLayoutScaleX` | `static double ImageLayoutScaleX(Func<Size> viewportSizeProvider, Size sourceSize, Func<bool>? uniformProvider = null)` | 画像レイアウトの X スケール（ズーム率導出用） |
| コンストラクタ | `CoordinatePort(Func<Point, Point> forward, Func<Point, Point> inverse, Func<Size?> bounds)` | 変換関数を直接注入（座標系は利用側が定義） |

#### 5.2.3 ScreenConverter

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `LocalDipToScreen` | `PixelPoint LocalDipToScreen(Visual source, Point local)` | コントロールローカル DIP 座標系 → 画面物理 Pixel（`TopLevel.PointToScreen` 委譲） |
| `ScreenToLocalDip` | `Point ScreenToLocalDip(Visual source, PixelPoint screen)` | 画面物理 Pixel → コントロールローカル DIP 座標系 |
| `CreateVisualDipPort` | `ICoordinatePort CreateVisualDipPort(Visual source)` | コントロールローカル DIP 座標系 ↔ 画面物理 Pixel の動的ポート（TopLevel を毎回解決） |

#### 5.2.4 ColorExtensions（C# 14 静的拡張メンバー）

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `FromAColor` | `static Color FromAColor(byte alpha, Color baseColor)` | 基準色（名前付き色等）と不透明度から色を生成する。既存の `Color.FromArgb(alpha, r, g, b)` は RGB 直指定で何色か判別しにくいため、基準色を指定して可読性を向上する（例: `Color.FromAColor(128, Colors.Black)` = 黒 50%） |

#### 5.2.5 PbBitmap（ピクセル操作）と PbBitmapAvaloniaExtensions / PbBitmapDrawingExtensions（生成・変換）

`PbBitmap` は内部表現（`SKBitmap` BGRA8888・Premul）と、`SkiaSharp` のみに依存するピクセル操作・生成/変換（`Create` / `FromSKBitmap` / `ToSKBitmap`）を提供するコア型です。  
Avalonia 依存の生成・変換は `PbBitmapAvaloniaExtensions`（`this Bitmap` / `this PbBitmap` の拡張メソッド）、System.Drawing（Windows）依存の生成・変換は `PbBitmapDrawingExtensions`（`this DrawingBitmap` / `this PbBitmap` の拡張メソッド）に分離され、コアは画像種別（Avalonia / System.Drawing）に依存しません。  

**PbBitmap（コア）**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 生成 | `Create` | `static PbBitmap Create(int width, int height)` | 指定サイズの空ビットマップ（BGRA8888・Premul）を生成。`SkiaSharp` のみに依存 |
| 生成 | `Create` | `static PbBitmap Create(int width, int height, Color color)` | 指定サイズのビットマップを生成し、全ピクセルを指定色で塗りつぶす（`Create` + `Clear` を1回で行う）。テスト・サンプルで多用する単色画像生成を簡便化 |
| 生成 | `FromSKBitmap` | `static PbBitmap FromSKBitmap(SKBitmap bitmap)` | 内部 `SKBitmap` から生成（**所有権を譲り受け**、破棄に連動）。`SkiaSharp` のみに依存 |
| プロパティ | `Width` / `Height` | `int` | 幅 / 高さ（ピクセル） |
| プロパティ | `Size` | `Size` | 幅・高さ（ピクセル。System.Drawing.Size） |
| プロパティ | `Stride` | `int` | 1 ラインあたりバイト数（パディング含む） |
| プロパティ | `Address` | `nint` | ピクセルデータ先頭アドレス（BGRA8888） |
| プロパティ | `IsDisposed` | `bool` | 破棄済みか |
| ピクセル | `GetPixel` | `(byte B, byte G, byte R, byte A) GetPixel(int x, int y)` | 指定座標の BGRA（アルファ乗算済み）を取得。範囲外は `IndexOutOfRangeException` |
| ピクセル | `GetPixel` | `Color GetPixel(Point point)` | 指定座標の BGRA を `System.Drawing.Color` で取得 |
| ピクセル | `SetPixel` | `void SetPixel(int x, int y, byte b, byte g, byte r, byte a)` | 指定座標の BGRA（アルファ乗算済み）を設定 |
| ピクセル | `SetPixel` | `void SetPixel(Point point, Color color)` | 指定座標へ `System.Drawing.Color` を設定 |
| ピクセル | `TrySetPixel` | `bool TrySetPixel(int x, int y, Color color)` | 指定座標へ色を設定（範囲外は `false`。描画での範囲外無視に使用） |
| ピクセル | `Clear` | `void Clear(byte b, byte g, byte r, byte a)` | 全ピクセルを指定色（BGRA）で塗りつぶし |
| ピクセル | `Clear` | `void Clear(Color color)` | 全ピクセルを指定色（System.Drawing.Color）で塗りつぶし |
| 変換 | `ToSKBitmap` | `SKBitmap ToSKBitmap()` | 内部インスタンスを返す（共有参照。呼び出し側で Dispose しない）。`SkiaSharp` のみに依存 |
| その他 | `Clone` | `PbBitmap Clone()` | 独立した複製を生成（所有権は呼び出し側） |
| その他 | `Crop` | `PbBitmap Crop(Rectangle rect)` / `PbBitmap Crop(int x, int y, int width, int height)` | 指定矩形領域を切り出す（要求サイズ。ソース範囲外は透明パディング） |

**PbBitmapAvaloniaExtensions（生成・変換・ピクセル操作）**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 生成 | `ToPbBitmap` | `PbBitmap ToPbBitmap(this Bitmap bitmap)` | Avalonia `Bitmap` から生成（複製。元は変更されない。`CopyPixels` ベースで高速・可逆） |
| 生成 | `ToAvaloniaWriteableBitmap` | `WriteableBitmap ToAvaloniaWriteableBitmap(this PbBitmap bitmap)` | Avalonia `WriteableBitmap` へ変換（複製を所有。`WriteableBitmap.Lock` ベース） |
| 生成 | `ToAvaloniaImage` | `IImage ToAvaloniaImage(this PbBitmap bitmap)` | Avalonia の画像入力（`IImage`）として公開するアダプタ（`PbBitmapAvaloniaImage`）を生成（**ゼロコピー・複製なし**） |
| 生成 | `CreateAvaloniaWriteableBitmap` | `static WriteableBitmap CreateAvaloniaWriteableBitmap(int width, int height, AvaloniaColor color)` | 単色の Avalonia `WriteableBitmap` を生成（テスト・サンプルの重複色塗りを一元化） |
| 生成 | `CreateAvaloniaImage` | `static PbBitmapAvaloniaImage CreateAvaloniaImage(int width, int height, AvaloniaColor color)` | 単色の `PbBitmapAvaloniaImage` を生成（`WriteableBitmap` を生成しないゼロコピー版。`Source` 設定等で高速） |
| 変換 | `Size` | `PixelSize Size(this PbBitmap bitmap)` | 画像サイズ（Avalonia `PixelSize`）。`this PbBitmap` のインスタンス拡張 |
| 変換 | `Crop` | `PbBitmap Crop(this PbBitmap bitmap, PixelRect rect)` | Avalonia `PixelRect` で切り出し（コアの `Crop` へ委譲） |
| ピクセル | `GetPixel` | `AvaloniaColor GetPixel(this PbBitmap bitmap, PixelPoint point)` | 指定座標のピクセルを Avalonia Color で取得 |
| ピクセル | `SetPixel` | `void SetPixel(this PbBitmap bitmap, PixelPoint point, AvaloniaColor color)` | 指定座標へ Avalonia Color を設定 |
| ピクセル | `TrySetPixel` | `bool TrySetPixel(this PbBitmap bitmap, int x, int y, AvaloniaColor color)` | 指定座標へ Avalonia Color を設定（範囲外は `false`） |
| ピクセル | `Clear` | `void Clear(this PbBitmap bitmap, AvaloniaColor color)` | 全ピクセルを指定色（Avalonia Color）で塗りつぶし（アルファ乗算） |

**PbBitmapDrawingExtensions（System.Drawing・Windows 専用）**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 生成 | `ToPbBitmap` | `PbBitmap ToPbBitmap(this DrawingBitmap bitmap)` | `System.Drawing.Bitmap` から生成（複製。**Windows 専用**・`[SupportedOSPlatform("windows")]`） |
| 変換 | `ToSystemDrawingBitmap` | `DrawingBitmap ToSystemDrawingBitmap(this PbBitmap bitmap)` | `System.Drawing.Bitmap`（Format32bppPArgb）へ変換（複製。**Windows 専用**・`[SupportedOSPlatform("windows")]`） |

> `FromSKBitmap` は引数の所有権を譲り受け、本型の破棄時に `Dispose` します。`ToSKBitmap` は内部インスタンスをそのまま返すため呼び出し側で `Dispose` しないでください。`ToAvaloniaWriteableBitmap` / `ToSystemDrawingBitmap` は複製を返すため呼び出し側が所有します。`ToAvaloniaImage` / `CreateAvaloniaImage` は内包する `PbBitmap` の所有権を生成した `PbBitmapAvaloniaImage` へ移すため、呼び出し側は元の `PbBitmap` を破棄しないでください。

`PbBitmapAvaloniaImage` は `PbBitmap` を内包して `IImage` として公開するアダプタです。`Draw` は `ISkiaSharpApiLeaseFeature` を取得して `SKCanvas` へ直接描画します（`WriteableBitmap` / `Bitmap` を生成しないゼロコピー描画）。破棄は `Dispose` で行い、内包 `PbBitmap` の所有権を保持します。

### 5.3 イベント

公開イベントとして、`IGlobalHotKeyService<T>.HotKeyPressed`（WM_HOTKEY 受信時のアクション配信）があります。その他はサービスの生成・座標変換・画面キャプチャの抽象提供のみで、結果は戻り値で通知します。

### 5.4 型

公開型は `PlatformFactory` / 各インターフェース（`ICursorService` / `IElementFinder` / `ICursorOverrideGuard` / `ICaptureService` / `IWindowService` / `IGlobalHotKeyService<T>` / `ICoordinatePort`）/ `CoordinatePort` / `ScreenConverter` / `ElementFinderResults` / `BitmapExtensions` / `PbBitmap` / `PbBitmapAvaloniaImage` / `PbBitmapAvaloniaExtensions` / `PbBitmapDrawingExtensions` / `ImageProcessor` です。
プラットフォーム固有実装（Win / NoOp 系）と内部ユーティリティ（`CoordinateMath` / `PlatformLog`）はすべて internal（非公開）です。公開 API はプラットフォーム非依存型のみを使用します。

#### 5.4.1 PlatformFactory

公開静的ファクトリ（`public static class`・Factory Method パターン）。生成メソッドは「5.2.1 メソッド」に記載の通りです。

#### 5.4.2 インターフェース（7 種）

| インターフェース | メンバー | 説明 |
| --- | --- | --- |
| `ICursorService` | `PixelPoint GetCursorScreenPoint()` | 現在のカーソル位置を画面物理 Pixel で取得 |
| | `void SetCursorScreenPoint(PixelPoint point)` | カーソルを画面物理 Pixel へ移動 |
| | `void ShowCursor(bool show)` | マウスポインターの表示 / 非表示切替 |
| `IElementFinder` | `ElementFinderResults FindWindowUnderCursor(PixelPoint screenPoint)` | 指定位置（画面物理 Pixel）の最前面ウィンドウ検出。ウィンドウ矩形は最大化時 = ワークエリア（`GetWindowRect`）/ 通常時 = DWM 影除外（`DWMWA_EXTENDED_FRAME_BOUNDS`）。タスクバー / デスクトップウィンドウ（クラス名: `Shell_TrayWnd` / `Shell_SecondaryTrayWnd` / `Shell_ImmersiveTrayWnd` / `Progman` / `WorkerW`）は選択対象外。**タイトル空の `ApplicationFrameWindow`**（スタートメニュー等の UWP オーバーレイ）も除外（タイトル付きの実際の UWP アプリは選択対象） |
| | `ElementFinderResults FindScreenUnderCursor(PixelPoint screenPoint)` | 指定位置（画面物理 Pixel）が含まれるスクリーン検出 |
| | `IReadOnlyList<ElementFinderResults> ListScreens()` | 全スクリーン一覧（仮想スクリーン 1 枚画像の管理用） |
| | `IReadOnlyList<ElementFinderResults> ListWindows()` | 全ウィンドウ一覧 |
| `ICursorOverrideGuard` | `bool IsAttached { get; }` | 抑止がアタッチ済みか |
| | `void Attach(TopLevel window)` | 指定ウィンドウにカーソル上書き抑止をアタッチ（アタッチ中は常に抑止） |
| `ICaptureService` | `Bitmap? CaptureArea(PixelRect bounds)` | 指定画面領域（画面物理 Pixel 座標）をキャプチャ |
| | `Bitmap? CaptureVirtualScreen(out PixelPoint origin)` | 仮想スクリーン全体を 1 回のキャプチャで取得（origin は画面物理 Pixel） |
| `IWindowService` | `void HideFromTaskSwitcher(IPlatformHandle handle)` | タスクバー / Alt+Tab 非表示（選択オーバーレイ用） |
| | `bool SetCaptureExclusion(IPlatformHandle handle, bool exclude)` | キャプチャ除外 / 解除（Windows 10 2004+ のみ true） |
| | `void EvacuateWindow(IPlatformHandle handle)` | 最前面解除・非表示・クローズ（UI ハング時の退避） |
| `IGlobalHotKeyService<T>` | `void Initialize(IntPtr hWnd)` | 初期化（WndProc サブクラス化で WM_HOTKEY 受信） |
| | `void RegisterAll(IReadOnlyList<(T Action, HotKeySetting Setting)> registrations)` | ホットキーとアクションの対応を登録 |
| | `void UnregisterAll()` | 全ホットキーを解除 |
| | `event Action<T>? HotKeyPressed` | WM_HOTKEY 受信時にアクションを配信 |
| `ICoordinatePort` | `Point Forward(Point p)` | 順変換（座標系 A → B。座標系はポート定義による） |
| | `Point Inverse(Point p)` | 逆変換（座標系 B → A。座標系はポート定義による） |
| | `Size? Bounds { get; }` | 座標系 B の境界（null = 無制限） |

#### 5.4.3 CoordinatePort

公開座標変換ポート（`public sealed class : ICoordinatePort`）。プロパティは「5.1.1」、生成メソッドは「5.2.2」に記載の通りです。

#### 5.4.4 ScreenConverter

公開静的クラス（`public static class`）。メソッドは「5.2.3」に記載の通りです。

#### 5.4.5 その他の公開型

| 型 | 説明 |
| --- | --- |
| `ElementFinderResults` record struct | 検出要素の結果（`Bounds`（画面物理 Pixel）/ `IsDetected` / `Title` / `Handle` / `ClassName`） |
| `BitmapExtensions.CopyRegionWithPadding` | 範囲外許容で領域コピー（クリップ + オフセット、範囲外は透明のまま。座標はピクセル単位） |
| `BitmapExtensions.ExtractRegion` | 矩形切り出し（範囲外は透明パディング。座標はピクセル単位。w/h ≤ 0 は `ArgumentOutOfRangeException`） |
| `ColorExtensions.FromAColor` | C# 14 静的拡張メンバー。基準色（名前付き色等）とアルファ値から `Color` を生成（例: `Color.FromAColor(128, Colors.Black)` = 黒 50%） |
| `PbBitmap` | 複数ビットマップ種別を横断する軽量ユニバーサルラッパー（コア）。内部に `SKBitmap`（BGRA8888・Premul）を唯一所有し、`SkiaSharp` のみに依存したピクセル操作と `Create` / `FromSKBitmap` / `ToSKBitmap` を提供 |
| `PbBitmapExtensions` | `PbBitmap` の生成・変換拡張（`this Bitmap` / `this DrawingBitmap` / `this PbBitmap`）。Avalonia `Bitmap` / `System.Drawing.Bitmap` との相互変換を提供 |
| `ImageProcessor` | 画像の切り出し・ウィンドウ領域検出を提供する静的ユーティリティ。`PbBitmap` に対してピクセルレベルの操作を行う（`Crop` / `DetectAndCropWindow` / `DetectWindowBounds`） |

## 6. 座標系

### 6.1 3 層の座標

| 座標系 | 用途 |
| --- | --- |
| ドメイン座標 | 選択状態・nudge・イベント通知（上位 AP が定義） |
| コントロールローカル DIP | マウス入力・描画（Avalonia 論理座標） |
| 画面物理 Pixel | カーソル操作・キャプチャ・画面変換 |

ドメイン ↔ DIP の変換は `ICoordinatePort`、ローカル DIP ↔ 画面物理は `ScreenConverter` が担います。

### 6.2 CoordinatePort の変換規則

| ファクトリ | Forward（A → B） | Inverse（B → A） | Bounds |
| --- | --- | --- | --- |
| `Identity` | 等価 | 等価 | null |
| `ScrollOffset` | オフセット減算（コンテンツ ↔ ビューポート） | オフセット加算 | null |
| `Scale` | × factor | ÷ factor | null |
| `DomainScale` | × 倍率（毎回評価） | ÷ 倍率（毎回評価） | null |
| `ImageLayout` | DIP → 論理（レターボックス考慮） | 論理 → DIP | 元画像サイズ |
| `Compose(outer, inner)` | inner 先行 → outer 後続 | outer 先行 → inner 後続 | outer 優先 |

## 7. ログ

本プロジェクトのログ定義は `PlatformLog.cs`（internal・LoggerMessage ソース生成）と `WinCaptureService.Log.cs`（private）に集約し、`ILogger` の拡張メソッドとして定義する。
各メソッドは `this ILogger` 第一引数で定義し、呼び出し側では logger 引数を省略する（例: `_log.LogCursorVisibilityChanged(show)`）。
各サービスのコンストラクタ / ファクトリメソッド（`PlatformFactory.CreateXxx(ILogger? logger = null)`）でロガーを注入する。null の場合は出力しない。
出力先・レベル制御はホストアプリケーションが所有する（本クラスは定義のみ）。

### 7.1 LogLevel 設定ポリシー

トレースログは LogLevel.Trace に限定せず、「重要度 × 発生頻度」でレベルを使い分ける。

| LogLevel | 対象 | 本番での意図 |
| --- | --- | --- |
| `Error` | 例外・失敗（処理継続不可） | 常時出力し、障害を確実に残す |
| `Warning` | 異常だがフォールバックで継続（ハング退避・キャプチャ失敗等） | 常時出力し、異常発生の痕跡を残す |
| `Information` | 重要な状態遷移（低頻度：カーソル表示切替・ウィンドウ操作） | 常時出力し、操作手順の再現を可能にする |
| `Debug` | 操作イベントの詳細（キャプチャ内部フロー） | 障害時のみ有効化して解析 |
| `Trace` | 高頻度・極詳細 | 特別な性能・座標解析時のみ有効化 |

判断基準:

1. 例外・警告は常時残す（`Error` / `Warning`）
2. 重要な状態遷移（低頻度）は `Information`
3. 操作イベントの詳細（中頻度）は `Debug`
4. 高頻度・極詳細は `Trace`
5. カーソル位置取得（`GetCursorScreenPoint`）等の高頻度呼び出しはログ対象外（ノイズ防止）

### 7.2 ログ定義一覧

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Warning` | `PlatformLog.LogWindowEvacuated` | ウィンドウ退避（最前面解除・非表示・WM_CLOSE） |
| `Information` | `PlatformLog.LogCursorVisibilityChanged` | カーソル表示/非表示の状態変更 |
| `Information` | `PlatformLog.LogWindowHiddenFromTaskSwitcher` | タスクバー / Alt+Tab からの除外 |
| `Information` | `PlatformLog.LogCaptureExclusionChanged` | キャプチャ除外設定の変更（成否込み） |
| `Debug` | `WinCaptureService.LogVirtualScreenBounds` | 仮想スクリーン全体キャプチャの矩形 |
| `Debug` | `WinCaptureService.LogCaptureSdBitmap` / `LogCaptureSdBitmapDone` | 仮想スクリーン Bitmap キャプチャの開始 / 完了 |

## 8. 制約事項

- Windows 実装（`WinCaptureService` 等）は `[SupportedOSPlatform("windows")]` 属性付きです。他環境では NoOp 実装（安全なフォールバック）が使用されます。
- `ICaptureService.CaptureVirtualScreen` の返却座標は仮想スクリーン座標です（プライマリが (0,0) でない場合 X / Y は負値になり得ます）。
- `IWindowService.SetCaptureExclusion` は Windows 10 2004+ のみ `true` を返します（それ以前はキャプチャ除外できません）。
- `CoordinatePort.Scale` は factor ≤ 0 を 1.0 として扱います（縮小・反転はしません）。
- `BitmapExtensions.ExtractRegion` は幅・高さ ≤ 0 の場合 `ArgumentOutOfRangeException` をスローします。

## 9. 最適化施策

| 施策 | 内容 |
| --- | --- |
| NoOp 実装のシングルトン化 | `NoOpCursorService` / `NoOpElementFinder` / `NoOpCaptureService` / `NoOpWindowService` は `Instance` シングルトンを返し、インスタンス生成コストを回避 |
| プラットフォーム判定の一元化 | `OperatingSystem.IsWindows()` は生成時にのみ評価（キャッシュ不可能な OS 問い合わせのため、頻繁な呼び出しを避ける） |
| Func 委譲による動的ポート | `CoordinatePort` の変換関数は呼び出し毎に評価され、ズーム・スクロールの動的変化を状態コピーなしで反映（オブジェクト生成を抑制） |
| 画像レイアウト計算の一元化 | `CoordinateMath.ComputeImageRenderInfo` に描画情報計算を集約し、`ImageLayout` / `ImageLayoutScaleX` で共用（DRY） |
| ログのソース生成 | `PlatformLog` は LoggerMessage ソース生成（`[LoggerMessage]`）により、ボックス化・文字列補間のオーバーヘッドを回避 |
| キャプチャのバックグラウンド実行 | 仮想スクリーン全体のキャプチャはバックグラウンドスレッド + タイムアウト（上位 AP が制御）で実行し、UI スレッドをブロックしない |
