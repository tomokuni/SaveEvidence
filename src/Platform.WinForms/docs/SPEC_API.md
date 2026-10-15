# Platform.WinForms API リファレンス

`Platform.WinForms` は `EsUtil.Platform.Core` のコア型と WinForms / GDI+ の型を相互変換し、GDI+ で画像を描くための表示ビューと描画プリミティブを提供します。 名前空間は `EsUtil.SaveEvidence.Platform.WinForms`（画像関連は `EsUtil.SaveEvidence.Platform.WinForms.Imaging`）です。

## 目次 <!-- omit from toc -->

- [1. 名前空間と型](#1-名前空間と型)
- [2. Interop（コア型 ⇔ GDI+ 型）](#2-interopコア型--gdi-型)
  - [2.1 `EsKeyWinFormsInterop`](#21-eskeywinformsinterop)
  - [2.2 `EsGeometryWinFormsInterop`](#22-esgeometrywinformsinterop)
  - [2.3 `EsWindowStateWinFormsInterop`](#23-eswindowstatewinformsinterop)
  - [2.4 `EsPlatformHandleWinFormsInterop`](#24-esplatformhandlewinformsinterop)
- [3. ウィンドウ配置](#3-ウィンドウ配置)
  - [3.1 `WindowPlacementMath`](#31-windowplacementmath)
- [4. モニター DPI](#4-モニター-dpi)
  - [4.1 `MonitorDpi`](#41-monitordpi)
- [5. 画像](#5-画像)
  - [5.1 `ClientPixelScale`](#51-clientpixelscale)
  - [5.2 `ImageDrawMode`](#52-imagedrawmode)
  - [5.3 `ImageDrawMath`](#53-imagedrawmath)
  - [5.4 `EsBitmapDrawingInterop`](#54-esbitmapdrawinginterop)
  - [5.5 `GdiText`](#55-gditext)
  - [5.6 `GdiFill`](#56-gdifill)
  - [5.7 `GdiLabel`](#57-gdilabel)
  - [5.8 `GdiImageRenderer`](#58-gdiimagerenderer)
  - [5.9 `GdiImageView`](#59-gdiimageview)
- [6. 使用例](#6-使用例)
- [7. 制限事項](#7-制限事項)
- [8. 前提条件](#8-前提条件)

## 1. 名前空間と型

| 名前空間 | 型 |
| --- | --- |
| `EsUtil.SaveEvidence.Platform.WinForms` | `EsKeyWinFormsInterop` / `EsGeometryWinFormsInterop` / `EsWindowStateWinFormsInterop` / `EsPlatformHandleWinFormsInterop` / `WindowPlacementMath` / `MonitorDpi` |
| `EsUtil.SaveEvidence.Platform.WinForms.Imaging` | `ClientPixelScale` / `ImageDrawMode` / `ImageDrawMath` / `EsBitmapDrawingInterop` / `GdiText` / `GdiFill` / `GdiLabel` / `GdiImageRenderer` / `GdiImageView` |

すべて public です（`internal` 型は持ちません）。

## 2. Interop（コア型 ⇔ GDI+ 型）

### 2.1 `EsKeyWinFormsInterop`

`EsKey` と `System.Windows.Forms.Keys` を相互変換する静的クラスです。

| メソッド | 説明 |
| --- | --- |
| `ToWinFormsKeys(this EsKey key)` → `Keys` | コアのキーを WinForms のキーへ変換する。 対応先がない場合（WinForms が表現できないキー・未定義値）は `Keys.None` |
| `ToEsKey(this Keys key)` → `EsKey` | WinForms のキーをコアのキーへ変換する。 対応先がない場合（キー以外の入力・未定義値）は `EsKey.None` |
| `IsRepresentable(this EsKey key)` → `bool` | WinForms に対応するキーがあるかを判定する（`None` は true） |

変換規則:

1. **名前が異なるキー**（`EsKey.LeftShift` ⇔ `Keys.LShiftKey` 等の 15 組）を別名表で解決する
2. **未定義値**（`(EsKey)999` 等）を `EnumCanonical.TryParse`（`EsUtil.Platform.Core`）で検出し `None` へフォールバックする
3. **名前が一致するメンバー**へ変換する

左右を区別しない `Keys.ShiftKey` / `Keys.ControlKey` / `Keys.Menu` は、`EsKey` に同じ意味の名前が無いため対応先なしとします（既定の矢印キーへ化けさせないため）。

### 2.2 `EsGeometryWinFormsInterop`

コアの幾何型と GDI+ の座標型を相互変換する静的クラスです。

| メソッド | 説明 |
| --- | --- |
| `ToPointF(this EsPoint)` / `ToEsPoint(this PointF)` / `ToEsPoint(this Point)` | 座標 |
| `ToPoint(this EsPoint)` | コアの座標を整数座標へ変換する（四捨五入は 0.5 を 0 から遠い側へ丸める規則を本メソッドが単一所有する） |
| `ToPoint(this EsPointInt)` / `ToEsPointInt(this Point)` / `ToEsPointInt(this PointF)` | 画面物理ピクセルの座標（整数）との変換 |
| `ToSizeF(this EsSize)` / `ToEsSize(this SizeF)` / `ToEsSize(this Size)` | サイズ |
| `ToSize(this EsSize)` | コアのサイズを整数サイズへ丸める |
| `ToPointF(this EsVector)` / `ToEsVector(this PointF)` | ベクター |
| `ToRectangleF(this EsRect)` / `ToEsRect(this RectangleF)` | 矩形 |
| `ToRectangle(this EsRectInt)` | 画面物理ピクセルの矩形をそのまま GDI+ の矩形へ写す（値域が同じ整数のため損失しない） |

**精度**: GDI+ の幾何型は単精度であるため、コア型（倍精度）を往復させると単精度へ丸められます（整数値と 0.5 刻みの値は損失しません）。

**色の変換は持ちません**: コアの色型は `System.Drawing.Color` そのものです。

### 2.3 `EsWindowStateWinFormsInterop`

`EsWindowState` と `System.Windows.Forms.FormWindowState` を相互変換する静的クラスです。

| メソッド | 説明 |
| --- | --- |
| `ToFormWindowState(this EsWindowState state)` → `FormWindowState` | 対応先がない場合（全画面・未定義値）は `Normal` |
| `ToEsWindowState(this FormWindowState state)` → `EsWindowState` | 未定義値は `Normal` |
| `IsRepresentable(this EsWindowState state)` → `bool` | 対応するウィンドウ状態がある場合（全画面・未定義値以外）は true |

`EsWindowState.FullScreen` に対応する `FormWindowState` はありません（WinForms の全画面は枠なし + 最大化で表すため）。 全画面を通常表示として復元しないよう、呼び出し側は `IsRepresentable` で先に判定できます。

### 2.4 `EsPlatformHandleWinFormsInterop`

| メソッド | 説明 |
| --- | --- |
| `ToEsPlatformHandle(this Control? control)` → `EsPlatformHandle` | WinForms のコントロールのネイティブ ウィンドウ識別子（HWND 相当）を返す。 null または**ハンドル未生成**の場合は 0 |

`Control.Handle` は取得した時点でウィンドウを生成するため、本メソッドは `IsHandleCreated` を確認してから読みます（ウィンドウを必要としない文脈で副作用を起こしません）。

## 3. ウィンドウ配置

### 3.1 `WindowPlacementMath`

ウィンドウの位置・サイズの相互変換（DIP ⇔ 画面物理ピクセル）を提供する純粋クラスです。 純粋関数のみであるため、UI 初期化なしの `[Fact]` で並列検証できます。

| メソッド | 説明 |
| --- | --- |
| `ToPhysicalPixelPoint(this EsPoint dip, double scale)` → `EsPointInt` | DIP の座標を画面物理ピクセルへ換算する（四捨五入）。 スケールが 0 以下・非数の場合は基準値（1.0）として扱う |
| `ToPhysicalPixelSize(this EsSize dip, double scale)` → `Size` | DIP のサイズを画面物理ピクセルへ換算する（四捨五入・**最小 1**）。 幅・高さが 0 になると復元できないため下限を設ける |
| `ToDipPoint(this EsPointInt physical, double scale)` → `EsPoint` | 画面物理ピクセルの座標を DIP へ換算する |
| `ToDipLength(int physical, double scale)` → `double` | 画面物理ピクセルの長さを DIP へ換算する |
| `IsUsable(this EsRectInt? bounds)` → `bool` | 幅と高さがともに 1 以上かを判定する（壊れた設定ファイルで 0 サイズのウィンドウを作らない） |

換算と正規化は `EsUtil.Platform.Core` の `DpiScaler`（UI 共通の単一所有者）へ委譲します。

## 4. モニター DPI

### 4.1 `MonitorDpi`

指定位置のモニターの DPI を照会する静的クラスです。 WinForms の公開 API はモニター単位の DPI を公開しないため（`Screen` は矩形とデバイス名のみ、`Control.DeviceDpi` はコントロールが属する 1 値のみ）、本層が Win32 の `GetDpiForMonitor` を呼びます。

| メソッド | 説明 |
| --- | --- |
| `GetScaleForPoint(this EsPointInt point)` → `double` | 指定位置にあるモニターのスケール係数（1.0 = 96 DPI）を返す。 **照会できない場合は 0**（`DpiScaler.IsValidScaling` が false） |

**0 を返す理由**: 1.0 を返すと「照会できなかった」と「96 DPI のモニター」を区別できないためです。 呼び出し側は 0 を無効値として扱い、代替値で解決します。

**DPI 非対応プロセスでの値**: OS がシステム DPI を返します（モニターごとの実効 DPI にはなりません。 DPI 対応の有効化はアプリの実行ファイルが行います）。

## 5. 画像

### 5.1 `ClientPixelScale`

| メソッド | 説明 |
| --- | --- |
| `Resolve(double clientPixels, double clientDip, double fallback)` → `double` | 描画面の実サイズ（物理ピクセル）と表示領域（DIP）の比から実効倍率を解決する。 表示領域が 0 以下の場合は `fallback`（DPI の通知から保持した値）を使う |

補正（非数・範囲外は基準値）は `EsUtil.Platform.Core` の `DpiScaler.NormalizeScaling` が単一所有します。

DPI の通知はハンドル生成より前に届かないことがあり、また描画先がオフスクリーン（`Bitmap` 上の `Graphics`）の場合は基準 DPI を報告します。 その状態で保持値だけを使うと図形が 100% で描かれ、DPI を反映済みの画像と重ねたときにずれます。

### 5.2 `ImageDrawMode`

| 値 | 説明 |
| --- | --- |
| `Fill` | 縦横比を無視して領域全体へ引き伸ばす |
| `Uniform` | 縦横比を保ち、はみ出さない倍率で中央へ配置する（余白は描画しない） |

ブール値の引数（`uniform`）で受けると `false` が「引き伸ばし」であることを読み取れないため、意図が名前で分かる列挙型にしています。

### 5.3 `ImageDrawMath`

| メソッド | 説明 |
| --- | --- |
| `ComputeDestination(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, ImageDrawMode mode)` → `EsRect` | 元画像を描画先の領域へ配置する矩形を計算する。 元画像または描画先が 0 以下の場合は空の矩形（`IsEmpty` が真）を返す |

戻り値は UI 非依存のコア型（`EsRect`）です。 純粋関数であるため、倍率・中央寄せ・空の扱いをヘッドレスで検証できます。

### 5.4 `EsBitmapDrawingInterop`

| メソッド | 説明 |
| --- | --- |
| `ToDrawingBitmap(this EsReadOnlyBitmap source)` → `Bitmap` | コアの画像を GDI+ のビットマップ（`Format32bppPArgb`）へ複製する。 **所有権は呼び出し側へ移る** |

- **ピクセル形式**: コアの画像は BGRA8888・アルファ乗算済みであるため、同じ並び・同じ乗算の `Format32bppPArgb` を使います（形式が一致しないと成分の順序が入れ替わります）。
- **複製は高価**: 1 回の呼び出しが全面の画素を複製します。 描画のたびに呼ばず、**画像が差し替わった時点で 1 度だけ作って保持**してください。
- **例外**: `source` が null の場合は `ArgumentNullException`、破棄済みの場合は `ObjectDisposedException` になります。

### 5.5 `GdiText`

GDI+ のテキスト描画（フォント解決・実寸測定・描画）を単一所有する静的クラスです。

| メソッド | 説明 |
| --- | --- |
| `Measure(string text, double fontSize)` → `EsSize` | 文字列の実寸（幅・行高）を **DIP** で返す。 測定は基準 DPI（96）の専用描画面で行う |
| `Draw(this Graphics graphics, string text, double left, double top, double fontSize, Color color)` | 文字列を左上原点で描画する。 フォント サイズは DIP で渡し、描画面の変換（DPI の拡大）に従う |

測定と描画は同じ書式（`StringFormat.GenericTypographic`）と同じフォント サイズを使います（測定した寸法で作った背景から文字がはみ出さないようにするため）。 測定用の描画面・フォント・ブラシはスレッドごとに 1 つを保持し、呼び出しのたびに作らないようにしています。

### 5.6 `GdiFill`

GDI+ の矩形塗り（単色）を単一所有する静的クラスです。

| メソッド | 説明 |
| --- | --- |
| `Rect(this Graphics graphics, EsRect rect, Color color)` | 矩形を塗る。 **幅・高さが 0 以下の場合は塗らない**（GDI+ は負のサイズの矩形を描画しない） |
| `RoundRect(this Graphics graphics, EsRect rect, double cornerRadius, Color color)` | 角丸の矩形を塗る（角丸半径 0 以下は四角形）。 半径は矩形の短辺の半分へクランプする |

塗りにはアンチエイリアスを適用しません（矩形は画素境界に沿って塗る）。 ブラシはスレッドごとに 1 つを持ち、色だけを書き換えて使い回します（**塗りの途中で別の色の塗りを求めない**前提です）。

### 5.7 `GdiLabel`

ラベル 1 件（背景 + 文字）の描画順を単一所有する静的クラスです。

| メソッド | 説明 |
| --- | --- |
| `Draw(this Graphics graphics, EsRect background, Color backgroundColor, string text, EsPoint textOrigin, double fontSize, Color textColor, double cornerRadius = 0)` | 背景を塗ってから文字を描く（順序を本メソッドが保証する） |

背景の塗りは `GdiFill` へ、文字の描画は `GdiText` へ委譲します。 テキストの実寸測定と配置の計算は**呼び出し側**が行います（余白・行送り・クランプの規則は各コントロールのコアが単一所有します）。

### 5.8 `GdiImageRenderer`

GDI+ の描画面へ画像を 1 回の描画で敷く描画層です。

| メソッド | 説明 |
| --- | --- |
| `Render(this Graphics graphics, Size targetSize, Bitmap? source, ImageDrawMode mode)` | 画像を描画先の領域へ配置して描画する。 画像が null、または配置が空の場合は何も描画しない |
| `DrawScaled(this Graphics graphics, Bitmap source, EsRect destination)` | 画像を指定の矩形へ描画する（拡大・縮小の設定を単一所有する入口）。 幅・高さが 0 以下の場合は何も描画しない |

**背景を塗りません**: GDI+ の描画面はコントロールの下地と共有されるため（別のキャンバスを持たない）、余白の下地は呼び出し側が塗ります。

**拡大・縮小は最近傍**で、`PixelOffsetMode.Half` を指定します（指定しないと元画像の外側を透明として拾い、縁の 1 画素が薄くなります）。 描画先の設定は呼び出しの前後で保存・復帰するため、他の描画と混在させても設定が漏れません。

### 5.9 `GdiImageView`

コアの画像を親が割り当てた領域へ描画する表示ビュー（`Control` 派生）です。

| メンバー | 説明 |
| --- | --- |
| `Source`（`EsReadOnlyBitmap?`） | 表示する画像。 設定すると GDI+ のビットマップへ複製し直し、直前の複製を破棄する（同じインスタンスの再設定では複製しない）。 null または破棄済みの場合は何も描画しない |
| `UniformDraw`（`protected virtual bool`） | 縦横比を保って領域へ収めるかを返す。 既定は false（領域全体へ引き伸ばす）。 派生クラスが固定値を与える |

- 表示サイズは親のレイアウトに従います（自身の要求サイズを持ちません）。
- 下地を塗らないため、**子コントロールを重ねられます**（画像の無い部分は親が見えます）。
- 複製したビットマップは本クラスが所有し、`Dispose` で破棄します（引数の画像は破棄しません）。

## 6. 使用例

GDI+ で画像を描き、その上へ重ねる層を作る場合の組み立てです。

```csharp
using EsUtil.SaveEvidence.Platform.WinForms;
using EsUtil.SaveEvidence.Platform.WinForms.Imaging;

// 1. コアの画像を GDI+ で描ける形へ移す（画像の差し替え時に 1 度だけ）
using var bitmap = EsBitmapDrawingInterop.ToDrawingBitmap(readOnlyBitmap);

// 2. 画像を敷くビューを親いっぱいへ置く
var view = new GdiImageView { Dock = DockStyle.Fill, Source = readOnlyBitmap };
parent.Controls.Add(view);

// 3. 画像を任意の矩形へ描く（下地 → 画像 → 上描きの順）
var state = graphics.Save();
graphics.ScaleTransform((float)scaling, (float)scaling);
GdiFill.Rect(graphics, backgroundRect, System.Drawing.Color.FromArgb(255, 32, 32, 32));
GdiImageRenderer.DrawScaled(graphics, bitmap, destinationRect);
graphics.Restore(state);

// 4. ラベル（背景 + 文字）は実寸から組み立てる
var textSize = GdiText.Measure("100%", 12);
GdiLabel.Draw(
    graphics,
    new EsRect(8, 8, textSize.Width + 8, textSize.Height + 8),
    System.Drawing.Color.FromArgb(153, 0, 0, 0),
    "100%",
    new EsPoint(12, 12),
    12,
    System.Drawing.Color.White);
```

## 7. 制限事項

| 制限 | 内容 |
| --- | --- |
| 色の変換 | 提供しない（コアの色型が `System.Drawing.Color` そのもの） |
| 全画面のウィンドウ状態 | `FormWindowState` に無いため写像できない（`IsRepresentable` で判定する） |
| モニター単位の DPI | プロセスが DPI 非対応の場合は OS がシステム DPI を返す（モニターごとの実効 DPI にはならない） |
| 画像の描画品質 | 拡大・縮小は最近傍である（スムーズな補間が要る場合は呼び出し側が描画設定を変える） |
| 描画のスレッド | GDI+ の描画面はスレッドごとに扱う（本プロジェクトが保持する測定用資源もスレッド単位） |

## 8. 前提条件

| 項目 | 内容 |
| --- | --- |
| 対象フレームワーク | `net10.0-windows`（`UseWindowsForms` を有効にする） |
| プラットフォーム | Windows のみ（`System.Drawing` と Win32 の DPI 照会を使う） |
| 座標系 | コア型は DIP、`EsPointInt` / `EsRectInt` は画面物理ピクセルを表す。 変換は `WindowPlacementMath` と `DpiScaler` を通す |
| 描画先 | `Graphics`（コントロールの描画面・`Graphics.FromImage` のどちらでもよい）。 描画面の座標系は呼び出し側が定める |
| 単位 | `EsSize` / `EsRect` / フォント サイズは DIP。 `Bitmap` と `EsReadOnlyBitmap` は物理ピクセル |
