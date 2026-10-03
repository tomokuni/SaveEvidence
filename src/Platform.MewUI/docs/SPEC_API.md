# Platform.MewUI API リファレンス

`Platform.MewUI` は `EsUtil.Platform.Core` のコア型と MewUI（`Aprillz.MewUI`）の型を相互変換します。 名前空間は `EsUtil.SaveEvidence.Platform.MewUI` です。

## 公開 API

すべて静的クラスの拡張メソッドです。 `using EsUtil.SaveEvidence.Platform.MewUI;` のみで利用できます。

### `EsKeyMewUIInterop`

| メソッド | 説明 |
| --- | --- |
| `ToMewUIKey(this EsKey key)` → `Aprillz.MewUI.Key` | コアのキーを MewUI のキーへ変換する。 対応先がない場合（MewUI が表現できないキー・未定義値）は `Key.None` |
| `ToEsKey(this Aprillz.MewUI.Key key)` → `EsKey` | MewUI のキーをコアのキーへ変換する。 未定義値は `EsKey.None` |
| `IsRepresentable(this EsKey key)` → `bool` | MewUI に対応するキーがあるかを判定する（`None` は true） |

#### 変換規則

1. **名前が異なるキー**（`EsKey.Back` ⇔ `Key.Backspace`）を別名表で解決する
2. **未定義値**（`(EsKey)999` 等）を `Enum.IsDefined` で検出し `None` へフォールバックする
   （`Enum.TryParse` は数値文字列を受け付けてしまうため、名前解決の成否だけでは検出できない）
3. **名前が一致するメンバー**へ変換する

#### 対応関係

| 項目 | 値 |
| --- | --- |
| `EsKey` のメンバー数 | 223 |
| MewUI `Key` のメンバー数 | 91 |
| 名前が一致するキー | 90 |
| 名前が異なるキー | 1（`Back` ⇔ `Backspace`） |
| MewUI が表現できない `EsKey` | 133（`BrowserBack` / `DbeRoman` 等。 `ToMewUIKey()` は `Key.None` を返す） |

`EsKey` が Avalonia / Win32 のキー体系（223）に合わせているのに対し、MewUI `Key` は抽象化された小さな集合（91・独自採番）であるため、値による対応付けはできません。

### `EsGeometryMewUIInterop`

| メソッド | 説明 |
| --- | --- |
| `ToMewUIPoint(this EsPoint)` / `ToEsPoint(this Aprillz.MewUI.Point)` | 2 次元座標 |
| `ToEsPointInt(this Aprillz.MewUI.Point)` | 画面物理ピクセルの座標（整数）へ変換する（小数を四捨五入する規則を本メソッドが単一所有する） |
| `ToMewUIPoint(this EsPointInt)` | 画面物理ピクセルの座標から MewUI の座標へ（DPI 照会等へ渡す用） |
| `ToMewUISize(this EsSize)` / `ToEsSize(this Aprillz.MewUI.Size)` | サイズ |
| `ToMewUIVector(this EsVector)` / `ToEsVector(this Aprillz.MewUI.Vector)` | ベクター |
| `ToMewUIRect(this EsRect)` / `ToEsRect(this Aprillz.MewUI.Rect)` | 矩形 |
| `ToMewUIColor(this System.Drawing.Color)` / `ToDrawingColor(this Aprillz.MewUI.Color)` | 色（ARGB をそのまま保持） |

#### 負のサイズの扱い（挙動の差）

**MewUI の `Rect` / `Size` は負のサイズを 0 にクランプします**（Avalonia は負の値を保持します）。

| 入力 | MewUI での値 | 備考 |
| --- | --- | --- |
| `new EsRect(100, 100, -20, -30)` | `X=100, Y=100, W=0, H=0` | 座標は保持され、サイズのみ 0 になる |
| `new EsSize(-20, -30)` | `W=0, H=0` | — |

コアで「空の矩形」（負のサイズ）を表現している場合、MewUI へ渡すと 0 サイズになります。 判定（`Width <= 0`）には影響しませんが、値そのものを保持したい場合は MewUI へ変換しないでください。

### `EsWindowStateMewUIInterop`

| メソッド | 説明 |
| --- | --- |
| `ToMewUIWindowState(this EsWindowState)` → `Aprillz.MewUI.Controls.WindowState` | 未定義値は `Normal` |
| `ToEsWindowState(this Aprillz.MewUI.Controls.WindowState)` → `EsWindowState` | 未定義値は `Normal` |

メンバー名は完全に一致します（`Normal` / `Minimized` / `Maximized` / `FullScreen`）。 設定ファイルの互換性のため名前ベースで変換します。

### `EsPlatformHandleMewUIInterop`

| メソッド | 説明 |
| --- | --- |
| `ToEsPlatformHandle(this Aprillz.MewUI.Window? window)` → `EsPlatformHandle` | `Window.Handle`（`nint`）をコアの識別子へ変換する。 null はハンドル値 0 |

Win32 の HWND に相当するため、`IWindowService` / `ICursorOverrideGuard` などハンドルを要求するサービスへ MewUI のウィンドウを渡せます。

### `Imaging.ClientPixelScale`（static class）

描画面（物理ピクセル）と表示領域（DIP）の比から、描画に使うスケーリング係数を解決します。

| メンバー | 内容 |
| --- | --- |
| `Resolve(double clientPixels, double clientDip, double fallback)` | 表示領域が確定していればその比（`clientPixels / clientDip`）を、未確定（0 以下）なら `fallback` を返す（0.1〜8.0 へ補正） |

**DPI の通知は追加直後のコントロールへ届きません**（実測: ウィンドウ表示後に追加したコントロールは `OnDpiChanged` が呼ばれず、登録時の既定値 1.0 のままになる）。
その状態で保持値だけで描画すると図形が 100% で描かれ、DPI を反映済みの画像と重ねたときにずれます。
描画面から求めた倍率を使えば、通知の有無によらず画像と同じ倍率で描けます。

### `Imaging.MewUITextMeasure`（static class）

MewUI のテキスト エンジンで文字列の実寸を測ります（ラベルの配置の唯一の入口）。

| メンバー | 内容 |
| --- | --- |
| `Measure(string text, double fontSize)` → `EsSize` | 文字列の実寸（DIP。 送り幅と行高）を返す |
| `Measure`（内部） | ツリー外の `TextBlock` へ文字列とフォントサイズを写して `Measure` し、`DesiredSize` を返す。 DPI はアプリケーションから解決されるため、要素と同じ丸めで測れる |

**描画に使うエンジンで測る必要があります**: MewUI のテキスト エンジン（DirectWrite の GDI 互換レイアウト）は文字の送りを物理ピクセルの格子へ丸めるため、Skia の実測より数 % 広い値を返します（実測: 14 DIP のラベルで 363.8 DIP に対し 378.4 DIP）。 ラベルを `TextBlock` 要素として置く UI が背景を Skia の実測で作ると、**文字が背景からはみ出して切れます**。

グラフィックス バックエンドが登録されていない環境（ウィンドウを生成しない単体テスト）では測定できないため、Skia の実測で代用します（実アプリでは常にエンジンで測ります）。

### `Imaging.SkiaImageView`（class : `SkiaCanvasView`）

`EsBitmap` を親が割り当てた領域へ描画する表示ビュー。 撮影画像のオーバーレイ（領域全体へ引き伸ばす）や
サムネイル（縦横比を保って収める）のように、「1 枚の画像を領域へ敷く」表示を共通化します。

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| プロパティ | `Source` | `EsBitmap? Source { get; set; }` | 表示する画像。 null・破棄済みの場合は何も描画しない（余白のみ） |
| プロパティ | `UniformDraw` | `protected virtual bool UniformDraw { get; }` | 縦横比を保って収めるか（既定は false = 領域全体へ引き伸ばす） |

```csharp
// 親の領域いっぱいへ撮影画像を敷く（引き伸ばし）
content.Add(new SkiaImageView { Source = screen });

// 縦横比から大きさを決める表示は、計測を上書きして UniformDraw を true にする
internal sealed class ThumbnailView : SkiaImageView
{
    protected override bool UniformDraw => true;
    protected override Size MeasureContent(Size availableSize) => ...;
}
```

描画手順（透明でクリア → 未設定・破棄済みの判定 → 配置の計算 → 1 回の `DrawBitmap`）は
`SkiaImageRenderer`（internal）が単一所有し、配置の矩形は本プロジェクトの `ImageDrawMath` が単一所有します。  
静止画のため連続再描画は無効化し、`Source` の変更時のみ描画します。

## 依存関係

| 依存 | 用途 |
| --- | --- |
| `EsUtil.Platform.Core`（`EsUtil.Platform.Core.*`） | コア型（`EsKey` / `EsPoint` / `EsSize` / `EsVector` / `EsRect` / `EsWindowState` / `EsPlatformHandle` / `EsBitmap`） |
| `Aprillz.MewUI.Core`（バージョン固定） | MewUI の型（`Key` / `Point` / `Size` / `Vector` / `Rect` / `Color` / `Window` / `WindowState` / `MewProperty`） |
| `Aprillz.MewUI.Skia`（バージョン固定） | `SkiaCanvasView`（`SkiaImageView` の基底）と `SkiaSharp` の型 |

プラットフォームホスト（`Aprillz.MewUI.Windows` 等）やバックエンドには依存しません（`Skia` パッケージは描画型のみを提供し、ホストを含まないため）。

## バージョン依存

`Aprillz.MewUI.Core` は **0.21.1 に固定**しています。 MewUI は 0.x で API が安定化中のため、
アップグレード時はテスト（キー数 91 の回帰検知・別名表の一致・負サイズのクランプ）で破壊的変更を検出してください。
