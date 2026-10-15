# Platform.WinUI3 API リファレンス

`EsUtil.Platform.Core` の型と WinUI 3（Windows App SDK）の型を相互変換する公開 API の仕様です。
すべて拡張メソッドまたは静的メソッドであり、インスタンスを生成しません。

## 目次 <!-- omit from toc -->

- [1. 名前空間と型](#1-名前空間と型)
- [2. EsKeyWinUI3Interop](#2-eskeywinui3interop)
- [3. EsGeometryWinUI3Interop](#3-esgeometrywinui3interop)
- [4. EsWindowStateWinUI3Interop](#4-eswindowstatewinui3interop)
- [5. EsPlatformHandleWinUI3Interop](#5-esplatformhandlewinui3interop)
- [6. WindowPlacementMath](#6-windowplacementmath)
- [7. Imaging.ClientPixelScale](#7-imagingclientpixelscale)
- [8. Imaging.ImageDrawMath / Imaging.ImageDrawMode](#8-imagingimagedrawmath--imagingimagedrawmode)
- [9. Imaging.SkiaImageView](#9-imagingskiaimageview)
- [10. Imaging.SkiaImageRenderer（内部・描画）](#10-imagingskiaimagerenderer内部描画)
- [11. Imaging.WinUI3TextMeasure](#11-imagingwinui3textmeasure)
- [12. 使用例](#12-使用例)
- [13. 制限事項](#13-制限事項)

## 1. 名前空間と型

| 名前空間 | 型 | 公開範囲 |
| --- | --- | --- |
| `EsUtil.SaveEvidence.Platform.WinUI3` | `EsKeyWinUI3Interop` | 公開 |
| `EsUtil.SaveEvidence.Platform.WinUI3` | `EsGeometryWinUI3Interop` | 公開 |
| `EsUtil.SaveEvidence.Platform.WinUI3` | `EsWindowStateWinUI3Interop` | 公開 |
| `EsUtil.SaveEvidence.Platform.WinUI3` | `EsPlatformHandleWinUI3Interop` | 公開 |
| `EsUtil.SaveEvidence.Platform.WinUI3` | `WindowPlacementMath` | 公開 |
| `EsUtil.SaveEvidence.Platform.WinUI3.Imaging` | `ClientPixelScale` | 公開 |
| `EsUtil.SaveEvidence.Platform.WinUI3.Imaging` | `ImageDrawMath` | 公開 |
| `EsUtil.SaveEvidence.Platform.WinUI3.Imaging` | `ImageDrawMode`（列挙型） | 公開 |
| `EsUtil.SaveEvidence.Platform.WinUI3.Imaging` | `WinUI3TextMeasure` | 公開 |
| `EsUtil.SaveEvidence.Platform.WinUI3.Imaging` | `SkiaImageView` | 公開 |
| `EsUtil.SaveEvidence.Platform.WinUI3.Imaging` | `SkiaImageRenderer`（内部・描画） | 単体テストのみ |

前提条件（単位・座標系）:

- **DIP（論理ピクセル）と画面物理ピクセルを混ぜない**。 `WindowPlacementMath` が両者の変換を担う。
- **画面物理座標は仮想スクリーン座標**（プライマリ モニターの左上が原点。 原点は負値になりうる）を指す。
- **キーは名前で対応付ける**（`EsKey` と `VirtualKey` は採番体系が異なる）。

## 2. EsKeyWinUI3Interop

`EsKey`（223 メンバー）と `VirtualKey`（172 メンバー）を相互変換します。

| メンバー | 説明 |
| --- | --- |
| `VirtualKey ToVirtualKey(this EsKey key)` | 対応するキーを返す。 未定義値・対応先がないキーは `VirtualKey.None` |
| `EsKey ToEsKey(this VirtualKey key)` | 対応するキーを返す。 未定義値・キー以外の入力（マウス ボタン・ゲームパッド）は `EsKey.None` |
| `bool IsRepresentable(this EsKey key)` | 対応するキーがある場合は `true`。 `EsKey.None` は `true` |

対応の規則:

- **名前が異なる組は別名表で解決する**（例: `Return` ⇔ `Enter`、`CapsLock` ⇔ `CapitalLock`、`D0` ⇔ `Number0`、`Prior` ⇔ `PageUp`、`NumLock` ⇔ `NumberKeyLock`、`LeftCtrl` ⇔ `LeftControl`、`BrowserBack` ⇔ `GoBack`）。
- **同じキーを表す複数のコアのキーは、同じ WinUI 3 のキーへ写り、逆変換は主名を代表とする**（`Return` / `Enter` はともに `Enter` へ写り、逆変換は `Return` になる）。
- **対応先がないキーは `None` へ落とす**（記号キー・メディア キー・入力方式の一部。 序数を返して別のキーへ化けさせない）。
- 未定義値（`(EsKey)999` 等）は `EnumCanonical` が検出する（`Enum.TryParse` は数値文字列を受け付けるため、名前解決の成否だけでは検出できない）。

## 3. EsGeometryWinUI3Interop

コアの幾何型・色型を WinUI 3（WinRT の投影型）の型へ相互変換します。

| メンバー | 説明 |
| --- | --- |
| `Point ToWinUIPoint(this EsPoint point)` | コアの座標 → 座標型 |
| `Point ToWinUIPoint(this EsPointInt point)` | 画面物理ピクセルの座標 → 座標型（DIP と物理ピクセルを同じ型で表すため、値はそのまま写す） |
| `EsPoint ToEsPoint(this Point point)` | 座標型 → コアの座標 |
| `EsPointInt ToEsPointInt(this Point point)` | 座標型 → 画面物理ピクセルの座標（四捨五入） |
| `Size ToWinUISize(this EsSize size)` | コアのサイズ → サイズ型（負の幅・高さは 0 へクランプ） |
| `EsSize ToEsSize(this Size size)` | サイズ型 → コアのサイズ |
| `Vector2 ToVector2(this EsVector vector)` | コアのベクター → ベクター型（要素の変換は `float` へ丸められる） |
| `EsVector ToEsVector(this Vector2 vector)` | ベクター型 → コアのベクター |
| `Rect ToWinUIRect(this EsRect rect)` | コアの矩形 → 矩形型（負の幅・高さは 0 へクランプ） |
| `EsRect ToEsRect(this Rect rect)` | 矩形型 → コアの矩形 |
| `Color ToWinUIColor(this System.Drawing.Color color)` | コアの色 → 色型（ARGB は保持される） |
| `System.Drawing.Color ToDrawingColor(this Color color)` | 色型 → コアの色（ARGB は保持される） |

制限事項:

- **座標型・サイズ型・矩形型は内部の記憶域が `float`** である。 `EsPoint` が保持する `double` は往復で `float` の有効桁まで落ちる。
- **サイズ型・矩形型のコンストラクターは負の幅・高さを拒否する**（例外になる）。 本層が 0 へクランプするため、空の矩形（負のサイズ）を渡しても例外にはならない（X / Y は保持される）。

## 4. EsWindowStateWinUI3Interop

WinUI 3 は 1 つの列挙型でウィンドウ状態を表さないため、2 系統の写像を提供します。

| メンバー | 説明 |
| --- | --- |
| `AppWindowPresenterKind ToAppWindowPresenterKind(this EsWindowState state)` | 全画面は `FullScreen`、それ以外は `Default` |
| `EsWindowState ToEsWindowState(this AppWindowPresenterKind kind)` | `FullScreen` は全画面、それ以外（`Default` / `CompactOverlay` / `Overlapped`）は通常 |
| `OverlappedPresenterState ToOverlappedPresenterState(this EsWindowState state)` | 最小化・最大化は同名の状態、通常と全画面は `Restored` |
| `EsWindowState ToEsWindowState(this OverlappedPresenterState state)` | 同名の状態、`Restored` は通常 |

制限事項:

- **全画面は重ね表示の状態にメンバーがない**ため、`ToOverlappedPresenterState` は `Restored` を返す（全画面の解除として扱う）。
- **`CompactOverlay` はコアの窓状態にない**ため、通常へ写す。

## 5. EsPlatformHandleWinUI3Interop

| メンバー | 説明 |
| --- | --- |
| `EsPlatformHandle ToEsPlatformHandle(this Window? window)` | ウィンドウ → コアのネイティブ識別子（Win32 の `HWND`）。 null の場合はハンドル値 0 |

前提条件: ウィンドウが生成済みであること（生成前はハンドルが 0 になる）。

## 6. WindowPlacementMath

ウィンドウ位置・サイズの DIP ⇔ 画面物理ピクセルの変換を提供します（純粋関数のみ）。

| メンバー | 説明 |
| --- | --- |
| `EsPointInt ToPhysicalPixelPoint(EsPoint dip, double scale)` | DIP の座標 → 画面物理ピクセルの座標（四捨五入） |
| `System.Drawing.Size ToPhysicalPixelSize(EsSize dip, double scale)` | DIP のサイズ → 画面物理ピクセルのサイズ（四捨五入。 **最小 1**） |
| `EsPoint ToDipPoint(EsPointInt physical, double scale)` | 画面物理ピクセルの座標 → DIP の座標 |
| `double ToDipLength(int physical, double scale)` | 画面物理ピクセルの長さ → DIP の長さ |
| `bool IsUsable(EsRectInt? bounds)` | 幅と高さがともに 1 以上なら `true`（0 サイズのウィンドウで操作不能になるのを防ぐ） |

前提条件:

- `scale` は DPI スケール係数（1.0 = 96 DPI）。 **0 以下・非数は 1.0** として扱う（判定は `DpiScaler` が所有する）。
- サイズは復元できない 0 を避けるため最小 1 にクランプする。

## 7. Imaging.ClientPixelScale

| メンバー | 説明 |
| --- | --- |
| `double Resolve(double clientPixels, double clientDip, double fallback)` | 描画面（物理ピクセル）と表示領域（DIP）の比を実効倍率として返す。 表示領域が 0 以下なら `fallback` を使う。 範囲外・非数は基準値へ補正 |

## 8. Imaging.ImageDrawMath / Imaging.ImageDrawMode

| メンバー | 説明 |
| --- | --- |
| `EsRect ComputeDestination(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, ImageDrawMode mode)` | 元画像を描画先の領域へ配置する矩形を返す。 元画像・描画先のいずれかが 0 以下なら空の矩形 |

`ImageDrawMode` の値:

| 値 | 意味 |
| --- | --- |
| `Fill` | 縦横比を無視して領域全体へ引き伸ばす |
| `Uniform` | 縦横比を保ち、はみ出さない倍率で中央へ配置する（余白は描画しない） |

## 9. Imaging.SkiaImageView

`EsReadOnlyBitmap` を親が割り当てた領域へ描画する表示ビューです（`SKXamlCanvas` 派生）。

| メンバー | 説明 |
| --- | --- |
| `EsReadOnlyBitmap? Source { get; set; }` | 表示する画像。 null の場合は何も描画しない。 設定すると描画とレイアウトを無効化する |
| `bool UniformDraw { get; }`（`protected virtual`） | `true` の場合は縦横比を保って中央へ収める。 既定は `false`（領域全体へ引き伸ばす）。 派生クラスが固定値を与える |

前提条件:

- **依存関係プロパティではない**。 WinUI 3 の依存関係プロパティは WinRT へ投影できる型だけを受け付けるため、コアの画像型は登録できない。 ホストはコードビハインドから設定する（XAML のバインディング対象にしない）。
- 要求サイズを持たない（表示サイズは親のレイアウトに従う）。

## 10. Imaging.SkiaImageRenderer（内部・描画）

| メンバー | 説明 |
| --- | --- |
| `void Render(SKCanvas canvas, SKImageInfo info, EsReadOnlyBitmap? source, ImageDrawMode mode)` | キャンバスを透明でクリアし、配置の矩形へ画像を 1 回の描画で敷く。 未設定・破棄済み・配置が空の場合は描画しない |

## 11. Imaging.WinUI3TextMeasure

| メンバー | 説明 |
| --- | --- |
| `EsSize Measure(string text, double fontSize)` | テキストの実寸（DIP）を返す。 テキストエンジンで測れない環境では Skia の実測で代用する |

前提条件: ラベルを**要素として置く表示**（XAML のレイアウトへ寸法を渡す場合）で使う。 Skia のキャンバスへ文字を描く表示は、測るエンジンと描くエンジンを揃える必要があるため `SkiaText` を使う。

## 12. 使用例

```csharp
using EsUtil.SaveEvidence.Platform.WinUI3;
using EsUtil.SaveEvidence.Platform.WinUI3.Imaging;

// 1. ウィンドウの位置とサイズを設定（画面物理ピクセル）から復元する
if (WindowPlacementMath.IsUsable(settings.MainWindowBounds))
{
    var scale = window.XamlRoot?.RasterizationScale ?? 1.0;
    var dipPoint = WindowPlacementMath.ToDipPoint(new EsPointInt(bounds.X, bounds.Y), scale);
    window.AppWindow.Move(new PointInt32(
        (int)dipPoint.X, (int)dipPoint.Y));
}

// 2. スクリーン座標の位置へ画像を敷く
var view = new SkiaImageView { Source = image };

// 3. コアのキーをホットキー登録へ渡す
var key = settings.HotKey.Key.ToVirtualKey();
```

## 13. 制限事項

- **XAML の型はヘッドレスで生成できない**。 XAML のランタイムを初期化していないプロセスで `Microsoft.UI.Xaml` のコントロールを生成すると `COMException`（0x80040154 `REGDB_E_CLASSNOTREG`）になる。 `Imaging.SkiaImageView` のインスタンス生成はアプリでの動作確認に委ねる。
- **ウィンドウを要する変換（`ToEsPlatformHandle`）は単体テストで検証できない**（ウィンドウの生成が必要）。
- 幾何型の変換は `float` の記憶域へ丸められる。
