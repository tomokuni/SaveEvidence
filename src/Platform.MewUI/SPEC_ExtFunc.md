# Platform.MewUI 外部機能仕様書

`Platform.MewUI` は `Platform.Core` のコア型と MewUI（`Aprillz.MewUI`）の型を相互変換します。名前空間は `EsUtil.Platform.MewUI` です。

## 公開 API

すべて静的クラスの拡張メソッドです。`using EsUtil.Platform.MewUI;` のみで利用できます。

### `EsKeyMewUIInterop`

| メソッド | 説明 |
| --- | --- |
| `ToMewUIKey(this EsKey key)` → `Aprillz.MewUI.Key` | コアのキーを MewUI のキーへ変換する。対応先がない場合（MewUI が表現できないキー・未定義値）は `Key.None` |
| `ToEsKey(this Aprillz.MewUI.Key key)` → `EsKey` | MewUI のキーをコアのキーへ変換する。未定義値は `EsKey.None` |
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
| MewUI が表現できない `EsKey` | 133（`BrowserBack` / `DbeRoman` 等。`ToMewUIKey()` は `Key.None` を返す） |

`EsKey` が Avalonia / Win32 のキー体系（223）に合わせているのに対し、MewUI `Key` は抽象化された小さな集合（91・独自採番）であるため、値による対応付けはできません。

### `EsGeometryMewUIInterop`

| メソッド | 説明 |
| --- | --- |
| `ToMewUIPoint(this EsPoint)` / `ToEsPoint(this Aprillz.MewUI.Point)` | 2 次元座標 |
| `ToEsPixelPoint(this Aprillz.MewUI.Point)` | 画面物理ピクセルの座標（整数）へ変換する（小数を四捨五入する規則を本メソッドが単一所有する） |
| `ToMewUIPoint(this EsPixelPoint)` | 画面物理ピクセルの座標から MewUI の座標へ（DPI 照会等へ渡す用） |
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

コアで「空の矩形」（負のサイズ）を表現している場合、MewUI へ渡すと 0 サイズになります。判定（`Width <= 0`）には影響しませんが、値そのものを保持したい場合は MewUI へ変換しないでください。

### `EsWindowStateMewUIInterop`

| メソッド | 説明 |
| --- | --- |
| `ToMewUIWindowState(this EsWindowState)` → `Aprillz.MewUI.Controls.WindowState` | 未定義値は `Normal` |
| `ToEsWindowState(this Aprillz.MewUI.Controls.WindowState)` → `EsWindowState` | 未定義値は `Normal` |

メンバー名は完全に一致します（`Normal` / `Minimized` / `Maximized` / `FullScreen`）。設定ファイルの互換性のため名前ベースで変換します。

### `EsPlatformHandleMewUIInterop`

| メソッド | 説明 |
| --- | --- |
| `ToEsPlatformHandle(this Aprillz.MewUI.Window? window)` → `EsPlatformHandle` | `Window.Handle`（`nint`）をコアの識別子へ変換する。null はハンドル値 0 |

Win32 の HWND に相当するため、`IWindowService` / `ICursorOverrideGuard` などハンドルを要求するサービスへ MewUI のウィンドウを渡せます。

### `Imaging.SkiaImageView`（class : `SkiaCanvasView`）

`EsBitmap` を親が割り当てた領域へ描画する表示ビュー。撮影画像のオーバーレイ（領域全体へ引き伸ばす）や
サムネイル（縦横比を保って収める）のように、「1 枚の画像を領域へ敷く」表示を共通化します。

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| プロパティ | `Source` | `EsBitmap? Source { get; set; }` | 表示する画像。null・破棄済みの場合は何も描画しない（余白のみ） |
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
`SkiaImageRenderer`（internal）が単一所有し、配置の矩形は `Platform.Core` の `ImageDrawMath` が単一所有します。  
静止画のため連続再描画は無効化し、`Source` の変更時のみ描画します。

### `Labels.EsLabelElementHost`（class）

コア（描画実装）が解決したラベル要素（`EsLabelElement`）を、親パネルの子要素（`Border` + `TextBlock`）として同期するホスト。
ラベルを「テキスト要素として配置する」コントロール（マーキー選択・ルーペ）が共通で必要とする処理（要素の追加・再利用・削除、内側余白・角丸・色・配置矩形の写像）を単一所有します。

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| ctor | `EsLabelElementHost` | `EsLabelElementHost(Panel owner)` | 要素を子として持つ親パネルを指定する（`owner` が null なら `ArgumentNullException`） |
| メソッド | `Measure` | `void Measure(Size availableSize)` | 子要素へ利用可能サイズを伝搬して測定する（本 UI は子を自動で測定しないため、コントロールの測定フェーズから呼ぶ） |
| メソッド | `Sync` | `void Sync(IReadOnlyList<EsLabelElement> elements, Rect bounds)` | 配置指示へ子要素を同期し、親の絶対座標（`bounds` の左上）を基準に配置する（`elements` が null なら `ArgumentNullException`） |

寸法・位置・文言・色はコアが単一所有するため、本型は位置の計算を持ちません（要素は子の末尾へ置かれ、**常に最上位**になります）。実寸測定は呼び出し側が行います（測定 API と実寸の使い方はコントロールごとに異なるため）。

## 依存

| 依存 | 用途 |
| --- | --- |
| `Platform.Core`（`EsUtil.Platform.Core.*`） | コア型（`EsKey` / `EsPoint` / `EsSize` / `EsVector` / `EsRect` / `EsWindowState` / `EsPlatformHandle` / `EsBitmap` / `ImageDrawMath`） |
| `Aprillz.MewUI.Core`（バージョン固定） | MewUI の型（`Key` / `Point` / `Size` / `Vector` / `Rect` / `Color` / `Window` / `WindowState` / `MewProperty`） |
| `Aprillz.MewUI.Skia`（バージョン固定） | `SkiaCanvasView`（`SkiaImageView` の基底）と `SkiaSharp` の型 |

プラットフォームホスト（`Aprillz.MewUI.Windows` 等）やバックエンドには依存しません（`Skia` パッケージは描画型のみを提供し、ホストを含まないため）。

## バージョン依存

`Aprillz.MewUI.Core` は **0.21.1 に固定**しています。MewUI は 0.x で API が安定化中のため、
アップグレード時はテスト（キー数 91 の回帰検知・別名表の一致・負サイズのクランプ）で破壊的変更を検出してください。
