# Platform.Core 外部機能仕様書

UI フレームワーク非依存の共有ライブラリとして、以下の公開 API を提供します。

## 公開型

### `EsKey`（enum）

UI フレームワーク非依存のキー識別子。`Avalonia.Input.Key` と同一のメンバー名を持ち、ホットキー設定の文字列表現・JSON シリアライズ（`JsonStringEnumConverter` 使用時）の互換性を保ちます。

### `EsWindowState`（enum）

UI フレームワーク非依存のウィンドウ表示状態。`Avalonia.Controls.WindowState` と同一のメンバー名（`Normal` / `Minimized` / `Maximized` / `FullScreen`）を持ち、設定ファイルの JSON シリアライズ（`JsonStringEnumConverter` 使用時）の互換性を保ちます。

### `CaptureType`（enum）

キャプチャの開始方法を表します。

| メンバー | 説明 |
| --- | --- |
| `ScreenSelect` | 全画面（カーソル位置のスクリーンを対象） |
| `WindowSelect` | ウィンドウ選択（カーソル位置の最前面ウィンドウを対象） |
| `AreaSelect` | 範囲選択（ドラッグで任意矩形） |

UI 実装のキャプチャ UI ライブラリ（`CaptureSessionControl.AvaloniaUI` 等）とホストが共有します。ホストではグローバルホットキーのアクション識別子（`IGlobalHotKeyService<CaptureType>`）としても使用します。

### `HotKeyModifiers`（`[Flags]` enum）

| メンバー | 値 | 説明 |
| --- | --- | --- |
| `None` | `0x0` | 修飾キーなし |
| `Alt` | `0x1` | Alt キー |
| `Control` | `0x2` | Ctrl キー |
| `Shift` | `0x4` | Shift キー |
| `Windows` | `0x8` | Windows キー |

値は Win32 `RegisterHotKey` の `MOD_*` 定数と一致します。

### `HotKeySetting`（record）

```csharp
public sealed record HotKeySetting(HotKeyModifiers Modifiers, EsKey Key)
{
    public override string ToString();
}
```

- `ToString()`: "Ctrl + Shift + Q" のようなユーザー可読な文字列を返す。数字キー（`D0`〜`D9`）・記号キー（`OemMinus` 等）は対応する記号で表示する。
- レコード型のため、`Modifiers` と `Key` が一致すれば等価。

### `Coordinate.ScrollMath`（static class）

- `MaxOffset(double contentSize, double viewportSize, double scale)`: スクロール範囲の最大オフセットを返す。
- `ViewportSize(double viewportSize, double scale)`: コンテンツ座標での表示範囲を返す。
- `ZoomViewControl` のスクロールバー計算で共用する。

### `Coordinate.DpiScaler`（static class）

- `StandardDpi`: DIP（デバイス非依存ピクセル）の基準 DPI（`96.0` = 1 ピクセル = 1 DIP）。スケーリング係数の算出と、DIP を 1:1 で描く描画先の DPI が共用する。
- `StandardScaling`: スケーリング係数の基準値（`1.0` = DIP と物理ピクセルが 1:1）。係数が未確定のときに使う値。
- `ToScaling(double dpi)`: DPI をスケーリング係数（表示 DIP → 物理ピクセル）へ換算する（`StandardDpi` を 1.0 とする倍率）。
- `IsValidScaling(double scaling)`: スケーリング係数が有効（描画・変換に使える）かどうかを判定する。**有限の正の値のみ有効**とし、0 以下と非数（NaN / 無限大）は無効とする（乗算で座標が整数の上限へ溢れてウィンドウが壊れるのを防ぐ）。
- `NormalizeScaling(double scaling)`: スケーリング係数を有効値へ正規化する（無効な値は `StandardScaling`）。
- `ScaleToPixel(int/double dipValue, double scaling)`: DIP 値を物理ピクセルへ変換（切り上げ・最低 1。浮動小数誤差を吸収）。
- `ToDip(int/double physical, double scaling)`: 物理ピクセルを DIP へ変換（`ScaleToPixel` の逆変換。**丸め・最低値の規則は適用しない**）。ウィンドウの位置・サイズを物理ピクセルから DIP へ戻す場面（全モニタの包含矩形をウィンドウへ適用する等）で共用する。
- `ToDip(EsSize physicalSize, double scaling)`: 物理ピクセルのサイズを DIP のサイズへ変換する（成分ごとの換算を上記 `ToDip` へ委譲する）。コア型同士の換算を UI 側で組み立てないための入口。
- `LoupeControl` の表示サイズ計算と、両 UI のウィンドウ配置・サイズ換算で共用する。

### `Geometry.EsAxis`（enum）

レイアウト計算で扱う軸（`X` = 幅 / `Y` = 高さ）です。軸を `bool` で渡すと `true` が横か縦か読めなくなるため、列挙型で指定します。

### `Geometry.EsHorizontalAlign` / `EsVerticalAlign`（enum）

対象（矩形・画像・テキスト等）を**基準点の水平/垂直方向のどこへ合わせるか**を表します。幅・高さが対象の実寸に依存する場面で、基準点がどこを指すかを実寸を知らずに指定するための値です（`bool` で指定すると `true` が左端か中央か読めないため、`EsAxis` と同じ方針で列挙型にします）。

| 型 | 値 |
| --- | --- |
| `EsHorizontalAlign` | `Left` / `Center` / `Right` |
| `EsVerticalAlign` | `Top` / `Middle` / `Bottom` |

**本型は配置の規則を持ちません**（基準点と実寸から位置を求める手順は利用側が単一所有します。「背景 = 実寸 + 内側余白 × 2」の体裁とアラインの適用は描画層（`MarqueeControl.UIBase` の `MarqueeLabelPlan` / `LoupeControl.UIBase` の `LoupeLabelPlan`）が担います）。

### `LoggerFallback`（static class）

ロガーの null を `NullLogger.Instance` へ吸収する共通ヘルパー。拡張メソッドとして提供します。

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 拡張メソッド | `OrNullLogger` | `static ILogger OrNullLogger(this ILogger? logger)` | 指定されたロガーを返す。null の場合は `NullLogger.Instance`（出力なし）を返す |

```csharp
private readonly ILogger _log = logger.OrNullLogger();   // logger が null でも安全
```

「ロガー未設定（null）は出力なしとして扱う」規則を単一所有します。各コントロール・サービスは `logger ?? NullLogger.Instance` を自前で書かず本メソッドへ委譲してください。

### `Imaging.DrawingColorExtensions`（static class）

描画色の判定を提供する拡張メソッドです。

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 拡張メソッド | `IsVisible` | `static bool IsVisible(this DrawingColor color)` | アルファが 0 より大きい場合に true（**アルファ 0 は「描かない」**を意味する） |

```csharp
public bool HasFrame => FrameColor.IsVisible();   // 透明なら描かない
```

描画の設定（ルーペの `LoupePaint`。下地・市松模様・外枠・十字線）が `color.A > 0` を書くと、同じ意味の判定が散らばり、片方だけが不可視の描画要求を UI へ届けます。マーキーの装飾（暗転マスク・穴あけ枠・内側の塗りと枠線）の描画可否は、色のアルファではなくコア（`MarqueeDecoOptions`）の明示フラグが単一所有します。

### `Imaging.ImageDrawMode`（enum）

画像を描画先の領域へ配置する方法。ブール値の引数で受けると `false` の意味が読めないため、列挙型で表現します。

| メンバー | 意味 |
| --- | --- |
| `Fill` | 縦横比を無視して領域全体へ引き伸ばす |
| `Uniform` | 縦横比を保ち、はみ出さない倍率で中央へ配置する（余白は描画しない） |

### `Imaging.ImageDrawMath`（static class）

画像を描画先の領域へ配置する矩形（`SKRect`）の計算。UI ホストを必要としない純粋関数のため、倍率・中央寄せ・空の扱いをヘッドレスで検証できます。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `ComputeDestination` | `static SKRect ComputeDestination(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, ImageDrawMode mode)` | 元画像を描画先の領域へ配置する矩形。元画像・描画先のいずれかが 0 以下なら `SKRect.Empty`（描画しない） |

```csharp
// 親が割り当てた領域（800x600）へ、撮影画像を引き伸ばして敷く
var dest = ImageDrawMath.ComputeDestination(image.Width, image.Height, 800, 600, ImageDrawMode.Fill);
```

画像を描く側は UI ごとに異なるため本クラスは計算だけを担い、実際の描画は各 UI の描画層（MewUI 版は `SkiaImageRenderer`）が行います。

### `Geometry.EsPoint`（readonly record struct）

```csharp
public readonly record struct EsPoint(double X, double Y);
```

UI フレームワーク非依存の 2 次元座標。Avalonia 版の相互変換は `Platform.AvaloniaUI` の `EsGeometryAvaloniaInterop` が提供します。

座標ラベルの文言は `Format(int decimals = 0)` が単一所有します（`(x, y)` 形式。`decimals` で小数点以下の桁数を 0〜15 から指定し、既定は 0 = 整数へ丸め）。`ToString()` はレコードの診断表現のままです（デバッガー・ログ・テストの失敗表示から小数を消さないため）。

```csharp
// カーソル座標ラベル（マーキー）・中心座標ラベル（ルーペ）が同じ書式を共用する
var text = mousePosition.Format();      // "(123, 456)"
var precise = mousePosition.Format(2);  // "(123.40, 456.70)"
```

### `Geometry.EsSize` / `Geometry.EsVector` / `Geometry.EsRect`（readonly record struct）

```csharp
public readonly record struct EsSize(double Width, double Height);
public readonly record struct EsVector(double X, double Y);
public readonly record struct EsRect(double X, double Y, double Width, double Height);
```

UI フレームワーク非依存のサイズ・ベクター・矩形。相互変換は `EsGeometryAvaloniaInterop` が提供します。

`EsRect` は `Right`（`X + Width`）・`Bottom`（`Y + Height`）・`IsEmpty`（幅または高さが 0 以下）に加え、コントロールに依存しない 2 つの規則を単一所有します（各コアが個別に書くと同じ意味の式が散らばり、片方だけ端の扱いを変えると同じ矩形から違う結果になる）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `ComputeInnerEdges` | `IReadOnlyList<EsRect> ComputeInnerEdges(double thickness)` | 上・下・左・右の順の辺の矩形（4 個）。左右の高さは負にならず 0 にクランプする。太さの下限正規化は行わない（用途で異なるため呼び出し側が行う） |
| `ClampToArea` | `EsRect ClampToArea(EsSize area)` | 四隅を領域（左上を原点・指定サイズ）内へ個別にクランプし、その包絡矩形を返す（負の辺は 0） |

```csharp
// 選択矩形の枠線（マーキー）とルーペの外枠（Loupe）が同じ式を共用する
var edges = hole.ComputeInnerEdges(Math.Max(0.5, borderThickness));
```

`EsSize` は「レイアウト前の領域（幅・高さが 0）ではクランプしない」という規則を単一所有します（各コア・各 UI が書くと、同じ意味の述語が散らばり、片方だけが要素を勝手に動かす）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `HasArea` | `bool HasArea { get; }` | 幅・高さとも 0 より大きい場合に true（レイアウト前・親未接続の領域は false） |
| `ClampToArea` | `double ClampToArea(double size)` | サイズを領域の最小辺へクランプする。領域が未確定の場合はクランプしない |
| `ClampOffset` | `double ClampOffset(double offset, double size, EsAxis axis)` | 1 軸分の位置を `0〜領域の長さ − サイズ` へクランプする（対象の軸の長さが 0 以下ならクランプしない。対象が収まらない場合は 0） |

### `Geometry.EsPointInt` / `Geometry.EsSizeInt` / `Geometry.EsRectInt`（readonly record struct）

```csharp
public readonly record struct EsPointInt(int X, int Y);
public readonly record struct EsSizeInt(int Width, int Height);
public readonly record struct EsRectInt(int X, int Y, int Width, int Height);
```

`EsPoint` / `EsSize` / `EsRect` の**整数版**（画面物理ピクセル座標・画像ピクセル座標とその矩形・寸法に使用する）。`EsRectInt` は `Right` / `Bottom` / `TopLeft` / `BottomRight` / `Center` を提供し、`Contains(EsPointInt)`（半開区間）と `Union(EsRectInt)` を持ちます。`TopLeft` は矩形の原点（画像の原点等）を座標の手組みなしで取り出すために使います。`EsPointInt.ToEsPoint()` は小数を扱う `EsPoint` への変換で、UI が成分を書き写さないよう本型が単一所有します。

### `EsPlatformHandle`（readonly record struct）

```csharp
public readonly record struct EsPlatformHandle(nint Handle, string? Descriptor = null);
```

ネイティブウィンドウ識別子（Win32: HWND 等）。`IsValid`（ハンドル値が 0 以外か）を提供します。座標・寸法ではないためルート名前空間（`EsUtil.Platform.Core`）にあります。

### `ICaptureService`（interface）

画面領域のキャプチャを抽象化します。公開 API は UI フレームワーク非依存の型のみを使用します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `CaptureArea` | `EsBitmap? CaptureArea(EsRectInt bounds)` | 指定画面領域（画面物理座標）をキャプチャ。失敗時は null |
| `CaptureVirtualScreen` | `EsBitmap? CaptureVirtualScreen(out EsPointInt origin)` | 仮想スクリーン全体を 1 回でキャプチャ |

### `ICursorService`（interface）

OS 依存のカーソル操作を抽象化します（UI フレームワーク標準で代替可能な機能は含めません）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `GetCursorScreenPoint` | `EsPointInt GetCursorScreenPoint()` | 現在のカーソル位置を画面物理座標で取得 |
| `SetCursorScreenPoint` | `void SetCursorScreenPoint(EsPointInt point)` | カーソルを画面物理座標へ移動 |
| `ShowCursor` | `void ShowCursor(bool show)` | マウスポインターの表示/非表示を切替 |

### `IWindowService`（interface）

ネイティブウィンドウ操作を抽象化します。ウィンドウは `EsPlatformHandle` で識別します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `HideFromTaskSwitcher` | `void HideFromTaskSwitcher(EsPlatformHandle handle)` | タスクバー / Alt+Tab 非表示 |
| `SetCaptureExclusion` | `bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude)` | キャプチャ除外 / 解除（未対応環境は false） |
| `SetBounds` | `bool SetBounds(EsPlatformHandle handle, EsRectInt bounds)` | 位置・サイズを画面物理ピクセルで指定（未対応環境は false。全モニターを跨ぐ配置に使う） |
| `EvacuateWindow` | `void EvacuateWindow(EsPlatformHandle handle)` | 最前面解除・非表示・クローズ（UI ハング時の退避） |

### `IElementFinder`（interface）

指定位置の要素（ウィンドウ・スクリーン）検出を抽象化します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `FindWindowUnderCursor` | `ElementFinderResults FindWindowUnderCursor(EsPointInt screenPoint)` | 指定位置の最前面ウィンドウを検出 |
| `FindScreenUnderCursor` | `ElementFinderResults FindScreenUnderCursor(EsPointInt screenPoint)` | 指定位置が含まれるスクリーンを検出 |
| `ListScreens` | `IReadOnlyList<ElementFinderResults> ListScreens()` | 全スクリーン一覧 |
| `ListWindows` | `IReadOnlyList<ElementFinderResults> ListWindows()` | 全ウィンドウ一覧 |

### `ICursorOverrideGuard`（interface : IDisposable）

選択操作中のカーソル上書き抑止を抽象化します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `IsAttached` | `bool IsAttached { get; }` | 抑止がアタッチ済みか |
| `Attach` | `void Attach(EsPlatformHandle handle)` | 指定ウィンドウに抑止をアタッチ |
| `Dispose` | `void Dispose()` | 抑止を解除 |

### `IGlobalHotKeyService<T>`（interface : IDisposable）

グローバルホットキーの登録・解除・アクション配信を抽象化します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Initialize` | `void Initialize(IntPtr hWnd)` | ウィンドウハンドルを設定しフックを設定 |
| `RegisterAll` | `void RegisterAll(IReadOnlyList<(T Action, HotKeySetting Setting)> registrations)` | ホットキーとアクションの対応を登録 |
| `UnregisterAll` | `void UnregisterAll()` | 全ホットキーを解除 |
| `HotKeyPressed` | `event Action<T>? HotKeyPressed` | ホットキー押下時にアクションを配信 |

### `ElementFinderResults`（readonly record struct）

```csharp
public readonly record struct ElementFinderResults(
    EsRectInt Bounds,
    bool IsDetected,
    string? Title = null,
    EsPlatformHandle? Handle = null,
    string? ClassName = null);
```

要素検出の結果。座標は入力と同じ画面物理座標で統一されます。

### `Coordinate.ICoordTransform`（interface）

2 つの座標系間の変換ポート。座標はコア幾何型で扱います。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Forward` | `EsPoint Forward(EsPoint p)` | 順変換（座標系 A → 座標系 B） |
| `Inverse` | `EsPoint Inverse(EsPoint p)` | 逆変換（座標系 B → 座標系 A） |
| `Bounds` | `EsSize? Bounds { get; }` | 座標系 B の境界（null = 無制限） |

### `Coordinate.CoordTransform`（sealed class : ICoordTransform）

`Func` 委譲による変換ポートと、用途別ファクトリを提供します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Identity` | `static ICoordTransform Identity { get; }` | 等価変換（A = B）の既定ポート |
| `Compose` | `static ICoordTransform Compose(ICoordTransform outer, ICoordTransform inner)` | 2 ポート合成（inner 先行 → outer 後続。Bounds は outer 優先） |
| `ScrollOffset` | `static ICoordTransform ScrollOffset(Func<EsVector> offsetProvider)` | スクロールオフセット平行移動（Forward = 減算 / Inverse = 加算） |
| `Scale` | `static ICoordTransform Scale(double factor)` | 原点基準の等方スケーリング（factor ≤ 0 は 1.0 扱い・固定値） |
| `DomainScale` | `static ICoordTransform DomainScale(Func<double> factorProvider)` | ドメイン = ローカル DIP × 倍率（呼び出し毎評価） |
| `DomainScale` | `static ICoordTransform DomainScale(Func<double> factorProvider, Func<EsSize?> boundsProvider)` | 同じ変換に境界（画像サイズ等。呼び出し毎評価）を付けたもの |
| `ImageLayout` | `static ICoordTransform ImageLayout(Func<EsSize> viewportSizeProvider, EsSize sourceSize, Func<bool>? uniformProvider = null)` | 画像レイアウト（レターボックス・スケール）の動的ポート（Bounds = 元画像サイズ） |
| `ImageLayoutScaleX` | `static double ImageLayoutScaleX(Func<EsSize> viewportSizeProvider, EsSize sourceSize, Func<bool>? uniformProvider = null)` | 画像レイアウトの X スケール（ズーム率導出用） |
| コンストラクタ | `CoordTransform(Func<EsPoint, EsPoint> forward, Func<EsPoint, EsPoint> inverse, Func<EsSize?> bounds)` | 変換関数を直接注入（座標系は利用側が定義） |

### `Imaging.EsBitmap`（sealed class : IDisposable）

**自前のピクセルバッファ**（BGRA8888・アルファ乗算済み）を所有するピクセルコンテナ。`NativeMemory.AlignedAlloc` で 64 バイト整列して確保し、確保直後に全領域を 0（透明）で初期化する。**外部パッケージへ依存しない**。

**生成・変換**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 生成 | `Create` | `static EsBitmap Create(int width, int height)` | 指定サイズの空ビットマップ（BGRA8888・Premul）を生成 |
| 生成 | `Create` | `static EsBitmap Create(EsSizeInt size)` | 指定サイズの空ビットマップを生成 |
| 生成 | `Create` | `static EsBitmap Create(int width, int height, Color color)` / `static EsBitmap Create(EsSizeInt size, Color color)` | 指定サイズのビットマップを全ピクセル指定色で生成 |

**Skia との相互変換**（`ToSKBitmap` / `ToEsBitmap`）は SkiaSharp へ依存するため [`Platform.Skia`](../Platform.Skia/README.md) の `EsBitmapSkiaExtensions` が提供します（`ToSKBitmap` は非所有ビュー、`ToEsBitmap` は複製。所有権はコア側に残ります）。

**プロパティ・ピクセル操作・その他**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| プロパティ | `Width` / `Height` | `int` | 幅 / 高さ（ピクセル） |
| プロパティ | `Size` | `EsSizeInt` | 幅・高さ（ピクセル。コアの整数サイズ型） |
| プロパティ | `Stride` | `int` | 1 ラインあたりバイト数（**常に 幅 × 4**。パディングなし） |
| プロパティ | `Address` | `nint` | ピクセルデータ先頭アドレス（BGRA8888） |
| プロパティ | `IsDisposed` | `bool` | 破棄済みか |
| ピクセル | `GetPixel` | `(byte B, byte G, byte R, byte A) GetPixel(int x, int y)` | 指定座標の BGRA（アルファ乗算済み）を取得。範囲外は `IndexOutOfRangeException` |
| ピクセル | `GetPixel` | `Color GetPixel(EsPointInt point)` | 指定座標の BGRA を `System.Drawing.Color` で取得 |
| ピクセル | `SetPixel` | `void SetPixel(int x, int y, byte b, byte g, byte r, byte a)` | 指定座標の BGRA（アルファ乗算済み）を設定 |
| ピクセル | `SetPixel` | `void SetPixel(EsPointInt point, Color color)` | 指定座標へ `System.Drawing.Color` を設定 |
| ピクセル | `TrySetPixel` | `bool TrySetPixel(int x, int y, Color color)` | 指定座標へ色を設定（範囲外は `false`） |
| ピクセル | `Clear` | `void Clear(byte b, byte g, byte r, byte a)` / `void Clear(Color color)` | 全ピクセルを指定色で塗りつぶし |
| その他 | `Clone` | `EsBitmap Clone()` | 独立した複製を生成（所有権は呼び出し側） |
| その他 | `Crop` | `EsBitmap Crop(EsRectInt rect)` / `EsBitmap Crop(int x, int y, int width, int height)` | 指定矩形領域を切り出す（ソース範囲外は透明パディング。位置関係は保たれる） |
| その他 | `Dispose` | `void Dispose()` | ピクセルバッファを解放（二重呼び出しは無害） |
| その他 | `ThrowIfDisposed` | `void ThrowIfDisposed()` | 破棄済みなら `ObjectDisposedException` を送出 |

> `Stride` は常に `幅 × 4`（パディングなし）です。`Clear(byte,byte,byte,byte)` は与えられた BGRA 成分をそのまま書き込みます（乗算済みへの変換はしません）。

`System.Drawing.Color`（`System.Drawing.Primitives`）のみに依存するため、**外部パッケージなしで Windows 以外でも動作します**。Skia との変換は `Platform.Skia`、System.Drawing.Bitmap との変換を含む Windows 専用の拡張は `Platform.Native` が提供します。

## 利用側の注意

本プロジェクトは UI フレームワークへの依存を一切持ちません。Avalonia 等の UI 実装側で `EsKey` と自身のキー型を相互変換する Interop 層（例: `Platform.AvaloniaUI` プロジェクトの `EsKeyAvaloniaInterop`）を用意して利用してください。
