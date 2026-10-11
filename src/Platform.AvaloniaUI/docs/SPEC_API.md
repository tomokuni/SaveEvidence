# Platform.AvaloniaUI API リファレンス

## 目次 <!-- omit from toc -->

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
  - [5.4 EsBitmapAvaloniaExtensions / EsBitmapAvaloniaAdapter / CapacityLimitedCache](#54-esbitmapavaloniaextensions--esbitmapavaloniaadapter--capacitylimitedcache)
  - [5.5 型一覧](#55-型一覧)
- [6. 座標系](#6-座標系)
- [7. 制約事項](#7-制約事項)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `Platform.AvaloniaUI` |
| プロジェクト | `src/Platform.AvaloniaUI/src/Platform.AvaloniaUI.csproj` |
| 名前空間 | `EsUtil.SaveEvidence.Platform.AvaloniaUI`（画像アダプタは `.Imaging`、相互変換は `.Interop`、座標変換は `.Coord`） |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要設計パターン | Adapter（コア型 ⇔ Avalonia 型の適合）/ 拡張メソッドによる Interop |
| 主な用途 | Avalonia 固有のアダプタ（画面座標変換・コア型との相互変換・画像／幾何アダプタ）の提供 |

## 2. 目的

本プロジェクトは **Avalonia UI 固有のアダプタのみ**を提供します。
UI フレームワーク非依存の抽象・値型・純粋ロジックと、ネイティブ（OS）の Win32 実装は `EsUtil.Platform.Core`（後者は `Native/` 配下） が担います。

| 機能 | 提供元 |
| --- | --- |
| サービス抽象（`ICaptureService` / `ICursorService` / `IWindowService` / `IElementFinder` / `ICursorOverrideGuard` / `IGlobalHotKeyService<T>`） | `EsUtil.Platform.Core` |
| コア値型（`EsKey` / `EsPoint` / `EsSize` / `EsVector` / `EsRect` / `EsPointInt` / `EsRectInt` / `EsPlatformHandle` 等） | `EsUtil.Platform.Core` |
| 座標変換ポート（`ICoordTransform` / `CoordTransform`）・`ScrollMath` / `DpiScaler` | `EsUtil.Platform.Core` |
| 画像コンテナ（`EsReadOnlyBitmap` / `EsBitmap` / `EsBitmap` / `EsBitmap`） | `EsUtil.Platform.Core` |
| 既定実装（Win32）・サービス抽象 | `EsUtil.Platform.Core`（`Native/` ・`Native/Win/`） |
| **画面座標変換（`ScreenConverter`）・相互変換（Interop）・画像／幾何アダプタ** | **本プロジェクト** |

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\Platform.AvaloniaUI\src\Platform.AvaloniaUI.csproj" />
```

本プロジェクトは `EsUtil.Platform.Core` を参照しているため、
本プロジェクトを参照すればその公開型も利用できます。

### 3.2 必要な名前空間

```csharp
using EsUtil.SaveEvidence.Platform.AvaloniaUI;               // ScreenConverter / 相互変換 / 幾何アダプタ / ColorExtensions
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;       // EsBitmapAvaloniaExtensions / EsBitmapAvaloniaAdapter / CapacityLimitedCache / AvaloniaTextMeasure
using EsUtil.Platform.Core;                     // EsKey / EsWindowState / EsPlatformHandle 等
using EsUtil.Platform.Core.Native;              // サービス抽象（ICaptureService 等）
using EsUtil.Platform.Core.Native.Win;          // 既定実装を直接生成する場合のみ（ホスト）
using EsUtil.Platform.Core.Imaging;             // EsBitmap
using EsUtil.Platform.Core.Coord;               // ICoordTransform / CoordTransform
using EsUtil.Platform.Core.Geometry;            // EsPoint / EsSize / EsVector / EsRect / EsPointInt / EsRectInt
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
var guard = new WinCursorOverrideGuard();

// TopLevel を渡せる（内部で EsPlatformHandle へ変換して Attach する）
guard.Attach(window);
```

### 4.3 画面座標変換

```csharp
var screenPt = ScreenConverter.LocalDipToScreen(control, localDip); // ローカル DIP → 画面物理
var localDip = ScreenConverter.ScreenToLocalDip(control, screenPt); // 画面物理 → ローカル DIP
```

## 5. 公開API

### 5.1 ScreenConverter

コントロールローカル DIP ↔ 画面物理 Pixel の変換を提供する静的クラスです。
`TopLevel` のスケーリングと画面上位置を解決する唯一の場所であり、Avalonia に依存するため本プロジェクトに残します。

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `LocalDipToScreen` | `PixelPoint LocalDipToScreen(Visual source, Point local)` | コントロールローカル DIP → 画面物理 Pixel（`TopLevel.PointToScreen` 委譲） |
| `ScreenToLocalDip` | `Point ScreenToLocalDip(Visual source, PixelPoint screen)` | 画面物理 Pixel → コントロールローカル DIP |

### 5.2 相互変換（Interop）

| 型 | 説明 |
| --- | --- |
| `EsKeyAvaloniaInterop` | `EsKey` ⇔ `Avalonia.Input.Key`（`ToAvaloniaKey()` / `ToEsKey()`。 数値ではなく名前で変換。 未定義値は `None`） |
| `EsWindowStateAvaloniaInterop` | `EsWindowState` ⇔ `Avalonia.Controls.WindowState`（名前で変換。 未定義値は `Normal` へフォールバック） |
| `EsGeometryAvaloniaInterop` | `EsPoint` / `EsSize` / `EsVector` / `EsRect` / `EsPointInt` / `EsRectInt` ⇔ Avalonia の対応型 |
| `EsPlatformHandleAvaloniaInterop` | `Avalonia.Platform.IPlatformHandle` ⇔ `EsPlatformHandle`（null はハンドル値 0 に変換） |
| `ColorAvaloniaInterop` | `Avalonia.Media.Color` ⇔ `System.Drawing.Color`（コアの色型。 ARGB をそのまま保持） |
| `CursorOverrideGuardAvaloniaExtensions` | `ICursorOverrideGuard.Attach(this ICursorOverrideGuard, TopLevel)` |

### 5.3 ColorExtensions（C# 14 静的拡張メンバー）

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `FromAColor` | `static Color FromAColor(byte alpha, Color baseColor)` | 基準色（名前付き色等）と不透明度から色を生成する（例: `Color.FromAColor(128, Colors.Black)` = 黒 50%）。 RGB 直指定より可読性が高い |

### 5.4 EsBitmapAvaloniaExtensions / EsBitmapAvaloniaAdapter / CapacityLimitedCache

コアの画像（`EsUtil.Platform.Core` 提供）を Avalonia へ適合させます。

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 生成 | `ToEsBitmap` | `EsBitmap ToEsBitmap(this Bitmap bitmap)` | Avalonia `Bitmap` から生成（複製。 **書き換え可能な型で返す**） |
| 生成 | `ToAvaloniaWriteableBitmap` | `WriteableBitmap ToAvaloniaWriteableBitmap(this EsReadOnlyBitmap bitmap)` | Avalonia `WriteableBitmap` へ変換（複製を所有） |
| 生成 | `ToAvaloniaBitmap` | `Bitmap ToAvaloniaBitmap(this EsReadOnlyBitmap bitmap)` | Avalonia `Bitmap` へ変換（複製を所有） |
| 生成 | `CreateAvaloniaWriteableBitmap` | `static WriteableBitmap CreateAvaloniaWriteableBitmap(int width, int height, AvaloniaColor color)` | 単色の `WriteableBitmap` を生成 |
| 生成 | `CreateAvaloniaImage` | `static EsBitmapAvaloniaAdapter CreateAvaloniaImage(int width, int height, AvaloniaColor color)` | 単色の画像を所有するアダプタを生成（コアの `EsBitmap` を作り `Adopt` で包む。 **破棄は呼び出し側**） |
| ピクセル | `SetPixel` | `void SetPixel(this EsBitmap bitmap, PixelPoint point, AvaloniaColor color)` | Avalonia 型でのピクセル書き込み（範囲外の無視が必要な場合はコアの `TrySetPixel` を `System.Drawing.Color` で使う） |

| 型 | 説明 |
| --- | --- |
| `EsBitmapAvaloniaAdapter` | コアの画像（`EsReadOnlyBitmap`）を包み、**`IImage`（表示）として公開する**アダプタ（コアの画像は `GetEsBitmap()` で取り出す）。 **コアの型は実装しない**（コアの読み取り面は画像自身が持つため）。 **射影元はコアの画像に限る**（Avalonia の `Bitmap` を真実とする入口は持たない。 Avalonia の画像をコアで扱う場合は `ToEsBitmap()` で変換してから包む）。 コアへの射影は恒等で、Avalonia 側の射影は**初回に一度だけ**作る。 **真実は所有しない**（破棄はホスト。 射影だけを `Dispose` で解放）。 **新しく生成した画像を解放する人がいない場合は `Adopt` で所有させる**。 ピクセル操作・切り出しは提供しない（コアの `EsBitmapExtensions` / `EsBitmap` を使 う）。 **一覧のサムネイルのように小さく表示する場合は `displayMaxSize` を渡す** （等倍の射影は画像 1 枚につき元寸法の複製を保持する。 実測: 4K で 32 MB / 7.8 ms に対し、96x54 の双線形の縮小版は 68 µs / 20 KB） |
| `CapacityLimitedCache<TKey, TValue>` | 生成にコストがかかる値をキー単位で共有する容量制限付きキャッシュ（`GetOrCreate` / `Count` / `DefaultCapacity`。 スレッド安全。 値はファクトリ関数で生成し、容量超過時は全体をクリアして無制限のメモリ増加を防ぐ） |
| `AvaloniaTextMeasure` | テキストの実寸を測る（`Measure(text, fontSize)`。 ラベルの配置矩形の計算に使う。 測定に色は影響しないためブラシは固定で共有する） |

**`EsBitmapAvaloniaAdapter` のメンバー**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 生成 | コンストラクタ | `EsBitmapAvaloniaAdapter(EsReadOnlyBitmap source, int displayMaxSize = 0)` | コアの画像を真実と する（表示は初回に複製。 **借用であり破棄しない**）。 `displayMaxSize` が正で**画像が上限より大きいときだけ**表示用の射影を縮小する |
| 生成 | `Adopt` | `static EsBitmapAvaloniaAdapter Adopt(EsBitmap source)` | コアの画像を**引き取って所有**するアダプタを生成（`Dispose` で画像も解放。 生成した画像を解放する人がいない場合に限る。 **解放できる型だけを引き取る**） |
| プロパティ | `Size` | `Size` | 表示サイズ（`IImage`。 `DisplayMaxSize` が正の場合は**縮小後の寸法**。 真実が破棄済みの場合は `ObjectDisposedException`） |
| プロパティ | `DisplayMaxSize` | `int` | 表示に使う長辺の上限（0 は上限なし = 等倍）。 **表示用の射影だけ**を縮小し、保存用の `GetAvaloniaBitmap` は等倍のまま（表示と保存で射影を分ける）。 **`GetEsBitmap()`（コアの画像）は真実の寸法のまま**（ピクセルと矛盾しない） |
| メソッド | `GetEsBitmap` | `EsReadOnlyBitmap GetEsBitmap()` | コアの画像を**借用**で取得する |
| メソッド | `GetAvaloniaBitmap` | `Bitmap GetAvaloniaBitmap()` | Avalonia の画像を**借用**で取得する（保存・クリップボード用。 **等倍**） |
| メソッド | `Draw` / `Dispose` | `IImage` / 冪等 | 描画（`DisplayMaxSize` が正なら縮小した射影を描く。 真実が破棄済みの場合は no-op）・射影（等倍・縮小。 と `Adopt` した画像）の解放 |

**破棄済みの扱い**: 真実のコア画像が破棄済みの場合、`Size` と `GetEsBitmap` / `GetAvaloniaBitmap` は `ObjectDisposedException` を投げます（破棄状態の判定はコアの画像自身の `IsDisposed` を使います）。 `Draw` だけは何も描画しません（表示は例外で止めない）。 無効な画像の寸法を正常値として渡さないための規則です。

**命名の規約（所有権で分ける）**: **`Get*` は借用**（破棄・書き換え禁止。 所有権は相手）、**`To*` は新規生成**（所有権が呼び出し側へ移る。 拡張メソッド）です。 同じ画像を扱っても、どちらが新しいかが名前で判別できます。

```csharp
// 色 → ブラシの共有（描画のたびに生成しない）
private static readonly CapacityLimitedCache<Color, SolidColorBrush> s_cache =
    new(static color => new SolidColorBrush(color));
var brush = s_cache.GetOrCreate(color);
```

容量管理（しきい値・超過時の全クリア）を本型が単一所有するため、利用側（ブラシ・ペンのキャッシュ）は容量の判定を持ちません。

### 5.5 型一覧

公開型は `ScreenConverter` / `EsKeyAvaloniaInterop` / `EsWindowStateAvaloniaInterop` /
`EsGeometryAvaloniaInterop` / `EsPlatformHandleAvaloniaInterop` / `ColorAvaloniaInterop` / `CursorOverrideGuardAvaloniaExtensions` /
`ColorExtensions` / `EsBitmapAvaloniaAdapter` / `EsBitmapAvaloniaExtensions` /
`CapacityLimitedCache<TKey, TValue>` / `SolidColorBrushCache` です。

コア値型・`CoordTransform`・画像型（`EsReadOnlyBitmap` / `EsBitmap`）と、その Win32 実装（`Native.Win`）・画像のファイル入出力（`Native.Win`）は `EsUtil.Platform.Core` が提供します。

## 6. 座標系

| 座標系 | 型 | 用途 |
| --- | --- | --- |
| 対象座標 | `EsPoint` / `EsRect` | 選択状態・nudge・イベント通知（上位 AP が定義） |
| コントロールローカル DIP | `EsPoint` / `EsSize`（描画時は Avalonia 型） | マウス入力・描画（Avalonia 論理座標） |
| 画面物理 Pixel | `EsPointInt` / `EsRectInt` | カーソル操作・キャプチャ・画面変換 |

- 対象 ↔ DIP の変換は `ICoordTransform`（`EsUtil.Platform.Core`）が担います。
- ローカル DIP ↔ 画面物理の変換は `ScreenConverter`（本プロジェクト）が担います。
- 各層の境界では本プロジェクトの相互変換拡張で型を変換します。

## 7. 制約事項

- `ScreenConverter` は Avalonia の `TopLevel` を解決するため、Avalonia 版専用です（他 UI では各 UI の実装が必要）。
- ビジュアルツリー未接続時、`ScreenConverter` は等価変換（画面座標 = ローカル DIP）として動作します。
- `EsBitmapAvaloniaExtensions` の `To*` は新しい画像を生成するため、呼び出し側が所有・破棄します（`CreateAvaloniaImage` は `Adopt` でアダプタに所有させます）。
- **表示用の縮小はコアの `EsBitmapExtensions.Resize`（`DownscaleAlgorithm.Box` = 面積平均）が単一所有します**（本プロジェクトに縮小の実装は置きません）。 アダプタは `DisplayMaxSize` を渡され、かつ**画像が上限より大きいときに限り**表示用の射影を縮小します（上限に収まっている画像は等倍の射影を共用し、同じ寸法の複製を 2 つ作りません）。
- **表示に面積平均を選ぶ理由は画質の一貫性です**: 同じ寸法のサムネイルを出すフォルダビュー（ファイル → `Bitmap.DecodeToWidth`）は面積平均相当の画質です。 双線形だと**同じサイズなのに一覧ごとに見え方が変わる**ためです（実測: 4K → 320x180 で幅 1 px の細線の輝度は面積平均 21 / 双線形 127 / 最近傍 255）。 面積平均は並列化されるため 4K 1 枚で 1.5〜2.0 ms です。
- **ファイルの保存・読み込みはコア（`EsBitmapExtensions.Save` / `EsBitmap.Load` / `EsBitmap.Load`）が単一所有します**（本プロジェクトに保存・読み込みの実装は置きません）。
