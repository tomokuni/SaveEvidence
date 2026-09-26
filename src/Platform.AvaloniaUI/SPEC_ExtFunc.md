# Platform.AvaloniaUI 外部機能仕様書

## 目次

- [Platform.AvaloniaUI 外部機能仕様書](#platformavaloniaui-外部機能仕様書)
  - [目次](#目次)
  - [1. 文書概要](#1-文書概要)
  - [2. 目的](#2-目的)
  - [3. 外部利用条件](#3-外部利用条件)
    - [3.1 プロジェクト参照](#31-プロジェクト参照)
    - [3.2 必要な名前空間](#32-必要な名前空間)
  - [4. 基本的な使用方法](#4-基本的な使用方法)
    - [4.1 コア型との相互変換](#41-コア型との相互変換)
    - [4.2 カーソル上書き抑止のアタッチ](#42-カーソル上書き抑止のアタッチ)
    - [4.3 画面座標変換](#43-画面座標変換)
  - [5. 公開API](#5-公開api)
    - [5.1 ScreenConverter](#51-screenconverter)
    - [5.2 相互変換（Interop）](#52-相互変換interop)
    - [5.3 ColorExtensions（C# 14 静的拡張メンバー）](#53-colorextensionsc-14-静的拡張メンバー)
    - [5.4 EsBitmapAvaloniaExtensions / EsBitmapAvaloniaImage / CapacityLimitedCache](#54-esbitmapavaloniaextensions--esbitmapavaloniaimage--capacitylimitedcache)
    - [5.5 AvaloniaCanvas](#55-avaloniacanvas)
    - [5.6 型一覧](#56-型一覧)
  - [6. 座標系](#6-座標系)
  - [7. 制約事項](#7-制約事項)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `Platform.AvaloniaUI` |
| プロジェクト | `Platform.AvaloniaUI/src/Platform.AvaloniaUI.csproj` |
| 名前空間 | `EsUtil.Platform.AvaloniaUI`（画像アダプタは `.Imaging`、相互変換は `.Interop`、ラベル要素は `.Labels`、座標変換は `.Coordinate`） |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要設計パターン | Adapter（コア型 ⇔ Avalonia 型の適合）/ 拡張メソッドによる Interop |
| 主な用途 | Avalonia 固有のアダプタ（画面座標変換・コア型との相互変換・画像／幾何アダプタ）の提供 |

## 2. 目的

本プロジェクトは **Avalonia UI 固有のアダプタのみ**を提供します。
UI フレームワーク非依存の抽象・値型・純粋ロジックは `Platform.Core`、
ネイティブ（OS）実装と実行時プラットフォームの既定実装ファクトリは `Platform.Native` が担います。

| 機能 | 提供元 |
| --- | --- |
| サービス抽象（`ICaptureService` / `ICursorService` / `IWindowService` / `IElementFinder` / `ICursorOverrideGuard` / `IGlobalHotKeyService<T>`） | `Platform.Core` |
| コア値型（`EsKey` / `EsPoint` / `EsSize` / `EsVector` / `EsRect` / `EsPointInt` / `EsRectInt` / `EsPlatformHandle` 等） | `Platform.Core` |
| 座標変換ポート（`ICoordTransform` / `CoordTransform`）・`ScrollMath` / `DpiScaler` | `Platform.Core` |
| 画像コンテナ（`EsBitmap`） | `Platform.Core` |
| 既定実装のファクトリ（`PlatformFactory.CreateXxx()`）・Win32 / NoOp 実装・System.Drawing 変換 | `Platform.Native`（本プロジェクトから参照される） |
| **画面座標変換（`ScreenConverter`）・相互変換（Interop）・画像／幾何アダプタ** | **本プロジェクト** |

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\Platform.AvaloniaUI\src\Platform.AvaloniaUI.csproj" />
```

本プロジェクトは `Platform.Core` と `Platform.Native` を参照しているため、
本プロジェクトを参照すればそれらの公開型も利用できます。

### 3.2 必要な名前空間

```csharp
using EsUtil.Platform.AvaloniaUI;          // ScreenConverter / 相互変換 / 幾何アダプタ / ColorExtensions
using EsUtil.Platform.AvaloniaUI.Imaging;  // EsBitmapAvaloniaExtensions / EsBitmapAvaloniaImage / CapacityLimitedCache
using EsUtil.Platform.Core;                     // サービス抽象 / コア値型
using EsUtil.Platform.Native;                   // PlatformFactory（既定実装のファクトリ）
using EsUtil.Platform.Core.Imaging;             // EsBitmap / EsBitmapDrawingExtensions
using EsUtil.Platform.Core.Coordinate;          // ICoordTransform / CoordTransform
using EsUtil.Platform.Core.Geometry;            // EsPoint / EsSize / EsVector / EsRect / EsPointInt / EsRectInt / EsPlatformHandle
```

## 4. 基本的な使用方法

### 4.1 コア型との相互変換

UI フレームワーク非依存のコア型と Avalonia の型を相互変換します。
サービス抽象へ渡すときは Avalonia → コア、描画・表示境界ではコア → Avalonia へ変換します。

```csharp
// Avalonia → コア
EsRectInt rect = captureRect.ToEsRectInt();
EsPointInt origin = pixelPoint.ToEsPointInt();
EsPlatformHandle handle = window.TryGetPlatformHandle().ToEsPlatformHandle();
EsPoint point = avaloniaPoint.ToEsPoint();
EsSize size = bounds.Size.ToEsSize();
EsVector offset = scrollViewer.Offset.ToEsVector();
EsRect esRect = avaloniaRect.ToEsRect();

// コア → Avalonia
PixelRect back = rect.ToAvaloniaPixelRect();
Point p = point.ToAvaloniaPoint();
Size s = size.ToAvaloniaSize();
```

キー（ホットキー設定）も同様に相互変換します。

```csharp
Avalonia.Input.Key key = esKey.ToAvaloniaKey();
EsKey esKey2 = key.ToEsKey();
```

### 4.2 カーソル上書き抑止のアタッチ

コアの `ICursorOverrideGuard.Attach` は UI 非依存のハンドルを受け取るため、
Avalonia の `TopLevel` からハンドルを取得する拡張メソッドを提供します。

```csharp
var guard = PlatformFactory.CreateCursorOverrideGuard();

// TopLevel を渡せる（内部で EsPlatformHandle へ変換して Attach する）
guard.Attach(window);
```

### 4.3 画面座標変換

```csharp
var screenPt = ScreenConverter.LocalDipToScreen(control, localDip); // ローカル DIP → 画面物理
var localDip = ScreenConverter.ScreenToLocalDip(control, screenPt); // 画面物理 → ローカル DIP

// コントロールローカル DIP ↔ 画面物理の動的変換ポート（TopLevel を毎回解決）
ICoordTransform dipPort = ScreenConverter.CreateVisualDipPort(control);
```

## 5. 公開API

### 5.1 ScreenConverter

コントロールローカル DIP ↔ 画面物理 Pixel の変換を提供する静的クラスです。
`TopLevel` のスケーリングと画面上位置を解決する唯一の場所であり、Avalonia に依存するため本プロジェクトに残します。

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `LocalDipToScreen` | `PixelPoint LocalDipToScreen(Visual source, Point local)` | コントロールローカル DIP → 画面物理 Pixel（`TopLevel.PointToScreen` 委譲） |
| `ScreenToLocalDip` | `Point ScreenToLocalDip(Visual source, PixelPoint screen)` | 画面物理 Pixel → コントロールローカル DIP |
| `CreateDipPort` | `ICoordTransform CreateDipPort(PixelPoint origin, double renderScaling)`（internal） | 原点＋スケーリングによる変換ポート（`EsPoint` を入出力） |
| `CreateVisualDipPort` | `ICoordTransform CreateVisualDipPort(Visual source)` | コントロールローカル DIP ↔ 画面物理 Pixel の動的ポート（`TopLevel` を毎回解決） |

### 5.2 相互変換（Interop）

| 型 | 説明 |
| --- | --- |
| `EsKeyAvaloniaInterop` | `EsKey` ⇔ `Avalonia.Input.Key`（`ToAvaloniaKey()` / `ToEsKey()`。数値ではなく名前で変換） |
| `EsWindowStateAvaloniaInterop` | `EsWindowState` ⇔ `Avalonia.Controls.WindowState`（名前で変換。未定義値は `Normal` へフォールバック） |
| `EsGeometryAvaloniaInterop` | `EsPoint` / `EsSize` / `EsVector` / `EsRect` / `EsPointInt` / `EsRectInt` ⇔ Avalonia の対応型 |
| `EsPlatformHandleAvaloniaInterop` | `Avalonia.Platform.IPlatformHandle` ⇔ `EsPlatformHandle`（null はハンドル値 0 に変換） |
| `ColorAvaloniaInterop` | `Avalonia.Media.Color` ⇔ `System.Drawing.Color`（コアの色型。ARGB をそのまま保持） |
| `CursorOverrideGuardAvaloniaExtensions` | `ICursorOverrideGuard.Attach(this ICursorOverrideGuard, TopLevel)` |

### 5.3 ColorExtensions（C# 14 静的拡張メンバー）

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `FromAColor` | `static Color FromAColor(byte alpha, Color baseColor)` | 基準色（名前付き色等）と不透明度から色を生成する（例: `Color.FromAColor(128, Colors.Black)` = 黒 50%）。RGB 直指定より可読性が高い |

### 5.4 EsBitmapAvaloniaExtensions / EsBitmapAvaloniaImage / CapacityLimitedCache

`EsBitmap`（`Platform.Core` 提供）を Avalonia へ適合させます。

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 生成 | `ToEsBitmap` | `EsBitmap ToEsBitmap(this Bitmap bitmap)` | Avalonia `Bitmap` から生成（複製） |
| 生成 | `ToAvaloniaWriteableBitmap` | `WriteableBitmap ToAvaloniaWriteableBitmap(this EsBitmap bitmap)` | Avalonia `WriteableBitmap` へ変換（複製を所有） |
| 生成 | `ToAvaloniaBitmap` | `Bitmap ToAvaloniaBitmap(this EsBitmap bitmap)` | Avalonia `Bitmap` へ変換（複製を所有） |
| 生成 | `ToAvaloniaImage` | `IImage ToAvaloniaImage(this EsBitmap bitmap)` | `IImage` アダプタ（`EsBitmapAvaloniaImage`）を生成（所有権が移る） |
| 生成 | `CreateAvaloniaWriteableBitmap` | `static WriteableBitmap CreateAvaloniaWriteableBitmap(int width, int height, AvaloniaColor color)` | 単色の `WriteableBitmap` を生成 |
| 生成 | `CreateAvaloniaImage` | `static EsBitmapAvaloniaImage CreateAvaloniaImage(int width, int height, AvaloniaColor color)` | 単色の `EsBitmapAvaloniaImage` を生成 |
| 変換 | `Size` | `PixelSize Size(this EsBitmap bitmap)` | 画像サイズ（Avalonia `PixelSize`） |
| 変換 | `Crop` | `EsBitmap Crop(this EsBitmap bitmap, PixelRect rect)` | Avalonia `PixelRect` で切り出し |
| ピクセル | `GetPixel` / `SetPixel` / `TrySetPixel` / `Clear` | Avalonia Color / PixelPoint 版 | Avalonia 型でのピクセル操作 |

| 型 | 説明 |
| --- | --- |
| `EsBitmapAvaloniaImage` | `EsBitmap` を内包して `IImage` として公開するアダプタ。`Draw` は Skia 直描画（`WriteableBitmap`` を生成しない）。`Dispose` で内包を解放 |
| `CapacityLimitedCache<TKey, TValue>` | 生成にコストがかかる値をキー単位で共有する容量制限付きキャッシュ（`GetOrCreate` / `Count` / `DefaultCapacity`。スレッド安全。値はファクトリ関数で生成し、容量超過時は全体をクリアして無制限のメモリ増加を防ぐ） |

```csharp
// 色 → ブラシの共有（描画のたびに生成しない）
private static readonly CapacityLimitedCache<Color, SolidColorBrush> s_cache =
    new(static color => new SolidColorBrush(color));
var brush = s_cache.GetOrCreate(color);
```

容量管理（しきい値・超過時の全クリア）を本型が単一所有するため、利用側（ブラシ・ペンのキャッシュ）は容量の判定を持ちません。

### 5.5 AvaloniaCanvas

`EsUtil.Platform.Core.Imaging.IEsCanvas`（UI 非依存の描画ポート）を Avalonia の `DrawingContext` へ適合させるアダプタです。
コア側の描画層（`MarqueeControl.UIBase` / `LoupeControl.UIBase`）が本ポートだけを呼ぶため、Avalonia 側は本クラスを渡すだけで描画できます。

| メンバー | 説明 |
| --- | --- |
| `AvaloniaCanvas(DrawingContext context)` | 描画先を指定して生成する（`context` が null なら `ArgumentNullException`） |
| `Clear(EsRect surface, DrawingColor color)` | `DrawingContext` に全面消去の API が無いため、渡された矩形を塗って代用する |
| `FillRect` / `FillRects` / `FillRoundRect` | 矩形・角丸矩形を塗る（ブラシは容量制限付きで共有） |
| `StrokeRect` / `DrawLine` | 枠線・線分を描く（ペンは容量制限付きで共有） |
| `DrawLabel(...)` | 背景を塗ってから文字を描く（**順序は本メソッドが保証する**） |
| `DrawImage(EsBitmap, EsRect, EsRect)` | `ToAvaloniaBitmap` で変換して描く（変換した複製は本メソッドが破棄する） |
| `MeasureText(string, double)` | `FormattedText` で実寸を測る |

```csharp
using var ctx = renderTargetBitmap.CreateDrawingContext();
new MarqueeRenderer(MarqueePaint.Default).DrawShapes(new AvaloniaCanvas(ctx), content);
```

### 5.6 型一覧

公開型は `ScreenConverter` / `EsKeyAvaloniaInterop` / `EsWindowStateAvaloniaInterop` /
`EsGeometryAvaloniaInterop` / `EsPlatformHandleAvaloniaInterop` / `ColorAvaloniaInterop` / `CursorOverrideGuardAvaloniaExtensions` /
`ColorExtensions` / `EsBitmapAvaloniaImage` / `EsBitmapAvaloniaExtensions` / `AvaloniaCanvas` /
`CapacityLimitedCache<TKey, TValue>` / `EsLabelElementHost` です。

インターフェース・コア値型・`CoordTransform`・`EsBitmap` は `Platform.Core`、
`PlatformFactory.CreateXxx()` と Win32 / NoOp 実装・`EsBitmapDrawingExtensions` は `Platform.Native` が提供します。

## 6. 座標系

| 座標系 | 型 | 用途 |
| --- | --- | --- |
| ドメイン座標 | `EsPoint` / `EsRect` | 選択状態・nudge・イベント通知（上位 AP が定義） |
| コントロールローカル DIP | `EsPoint` / `EsSize`（描画時は Avalonia 型） | マウス入力・描画（Avalonia 論理座標） |
| 画面物理 Pixel | `EsPointInt` / `EsRectInt` | カーソル操作・キャプチャ・画面変換 |

- ドメイン ↔ DIP の変換は `ICoordTransform`（`Platform.Core`）が担います。
- ローカル DIP ↔ 画面物理の変換は `ScreenConverter`（本プロジェクト）が担います。
- 各層の境界では本プロジェクトの相互変換拡張で型を変換します。

## 7. 制約事項

- `ScreenConverter` は Avalonia の `TopLevel` を解決するため、Avalonia 版専用です（他 UI では各 UI の実装が必要）。
- ビジュアルツリー未接続時、`ScreenConverter` は等価変換（画面座標 = ローカル DIP）として動作します。
- `EsBitmapAvaloniaImage` / `ToAvaloniaImage` は内包 `EsBitmap` の所有権を移すため、呼び出し側は元の `EsBitmap` を破棄しないでください。
- `ToAvaloniaWriteableBitmap` / `ToAvaloniaBitmap` は複製を返すため、呼び出し側が所有・破棄します。
