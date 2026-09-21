# PlatformFactory.Core 外部機能仕様書

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

UI 実装のキャプチャ UI ライブラリ（`SnipCaptureUI` 等）とホストが共有します。ホストではグローバルホットキーのアクション識別子（`IGlobalHotKeyService<CaptureType>`）としても使用します。

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

- `ScaleToPixel(int/double dipValue, double scaling)`: DIP 値を物理ピクセルへ変換（切り上げ・最低 1。浮動小数誤差を吸収）。
- `LoupeControl` の表示サイズ計算で共用する。

### `Geometry.EsPoint`（readonly record struct）

```csharp
public readonly record struct EsPoint(double X, double Y);
```

UI フレームワーク非依存の 2 次元座標。Avalonia 版の相互変換は `PlatformFactory` の `EsGeometryAvaloniaInterop` が提供します。

### `Geometry.EsSize` / `Geometry.EsVector` / `Geometry.EsRect`（readonly record struct）

```csharp
public readonly record struct EsSize(double Width, double Height);
public readonly record struct EsVector(double X, double Y);
public readonly record struct EsRect(double X, double Y, double Width, double Height);
```

UI フレームワーク非依存のサイズ・ベクター・矩形。相互変換は `EsGeometryAvaloniaInterop` が提供します。`EsRect` は `Right`（`X + Width`）と `Bottom`（`Y + Height`）を提供します。

### `Geometry.EsPixelPoint` / `Geometry.EsPixelRect`（readonly record struct）

```csharp
public readonly record struct EsPixelPoint(int X, int Y);
public readonly record struct EsPixelRect(int X, int Y, int Width, int Height);
```

画面の物理ピクセル座標・矩形（DPI スケーリング適用後）。`EsPixelRect` は `Right` / `Bottom` / `BottomRight` / `Center` を提供し、`Contains(EsPixelPoint)`（半開区間）と `Union(EsPixelRect)` を持ちます。

### `Geometry.EsPlatformHandle`（readonly record struct）

```csharp
public readonly record struct EsPlatformHandle(nint Handle, string? Descriptor = null);
```

ネイティブウィンドウ識別子（Win32: HWND 等）。`IsValid`（ハンドル値が 0 以外か）を提供します。

### `Geometry.CoordinateFormatter`（static class）

- `Format(EsPoint point)`: 座標を `(x, y)` 形式の文字列へ整形する（`F0` で整数に丸める）。

### `ICaptureService`（interface）

画面領域のキャプチャを抽象化します。公開 API は UI フレームワーク非依存の型のみを使用します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `CaptureArea` | `EsBitmap? CaptureArea(EsPixelRect bounds)` | 指定画面領域（画面物理座標）をキャプチャ。失敗時は null |
| `CaptureVirtualScreen` | `EsBitmap? CaptureVirtualScreen(out EsPixelPoint origin)` | 仮想スクリーン全体を 1 回でキャプチャ |

### `ICursorService`（interface）

OS 依存のカーソル操作を抽象化します（UI フレームワーク標準で代替可能な機能は含めません）。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `GetCursorScreenPoint` | `EsPixelPoint GetCursorScreenPoint()` | 現在のカーソル位置を画面物理座標で取得 |
| `SetCursorScreenPoint` | `void SetCursorScreenPoint(EsPixelPoint point)` | カーソルを画面物理座標へ移動 |
| `ShowCursor` | `void ShowCursor(bool show)` | マウスポインターの表示/非表示を切替 |

### `IWindowService`（interface）

ネイティブウィンドウ操作を抽象化します。ウィンドウは `EsPlatformHandle` で識別します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `HideFromTaskSwitcher` | `void HideFromTaskSwitcher(EsPlatformHandle handle)` | タスクバー / Alt+Tab 非表示 |
| `SetCaptureExclusion` | `bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude)` | キャプチャ除外 / 解除（未対応環境は false） |
| `SetBounds` | `bool SetBounds(EsPlatformHandle handle, EsPixelRect bounds)` | 位置・サイズを画面物理ピクセルで指定（未対応環境は false。全モニターを跨ぐ配置に使う） |
| `EvacuateWindow` | `void EvacuateWindow(EsPlatformHandle handle)` | 最前面解除・非表示・クローズ（UI ハング時の退避） |

### `IElementFinder`（interface）

指定位置の要素（ウィンドウ・スクリーン）検出を抽象化します。

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `FindWindowUnderCursor` | `ElementFinderResults FindWindowUnderCursor(EsPixelPoint screenPoint)` | 指定位置の最前面ウィンドウを検出 |
| `FindScreenUnderCursor` | `ElementFinderResults FindScreenUnderCursor(EsPixelPoint screenPoint)` | 指定位置が含まれるスクリーンを検出 |
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
    EsPixelRect Bounds,
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
| `ImageLayout` | `static ICoordTransform ImageLayout(Func<EsSize> viewportSizeProvider, EsSize sourceSize, Func<bool>? uniformProvider = null)` | 画像レイアウト（レターボックス・スケール）の動的ポート（Bounds = 元画像サイズ） |
| `ImageLayoutScaleX` | `static double ImageLayoutScaleX(Func<EsSize> viewportSizeProvider, EsSize sourceSize, Func<bool>? uniformProvider = null)` | 画像レイアウトの X スケール（ズーム率導出用） |
| コンストラクタ | `CoordTransform(Func<EsPoint, EsPoint> forward, Func<EsPoint, EsPoint> inverse, Func<EsSize?> bounds)` | 変換関数を直接注入（座標系は利用側が定義） |

### `Imaging.EsBitmap`（sealed class : IDisposable）

SkiaSharp のみに依存するピクセルコンテナ（BGRA8888・アルファ乗算済み）。

**生成・変換**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 生成 | `Create` | `static EsBitmap Create(int width, int height)` | 指定サイズの空ビットマップ（BGRA8888・Premul）を生成 |
| 生成 | `Create` | `static EsBitmap Create(Size size)` | 指定サイズの空ビットマップを生成 |
| 生成 | `Create` | `static EsBitmap Create(int width, int height, Color color)` / `static EsBitmap Create(Size size, Color color)` | 指定サイズのビットマップを全ピクセル指定色で生成 |
| 生成 | `FromSKBitmap` | `static EsBitmap FromSKBitmap(SKBitmap bitmap)` | 内部 `SKBitmap` から生成（所有権を譲り受け、破棄に連動） |
| 変換 | `ToSKBitmap` | `SKBitmap ToSKBitmap()` | 内部インスタンスを返す（共有参照。呼び出し側で Dispose しない） |

**プロパティ・ピクセル操作・その他**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| プロパティ | `Width` / `Height` | `int` | 幅 / 高さ（ピクセル） |
| プロパティ | `Size` | `Size` | 幅・高さ（ピクセル。System.Drawing.Size） |
| プロパティ | `Stride` | `int` | 1 ラインあたりバイト数（パディング含む） |
| プロパティ | `Address` | `nint` | ピクセルデータ先頭アドレス（BGRA8888） |
| プロパティ | `IsDisposed` | `bool` | 破棄済みか |
| ピクセル | `GetPixel` | `(byte B, byte G, byte R, byte A) GetPixel(int x, int y)` | 指定座標の BGRA（アルファ乗算済み）を取得。範囲外は `IndexOutOfRangeException` |
| ピクセル | `GetPixel` | `Color GetPixel(Point point)` | 指定座標の BGRA を `System.Drawing.Color` で取得 |
| ピクセル | `SetPixel` | `void SetPixel(int x, int y, byte b, byte g, byte r, byte a)` | 指定座標の BGRA（アルファ乗算済み）を設定 |
| ピクセル | `SetPixel` | `void SetPixel(Point point, Color color)` | 指定座標へ `System.Drawing.Color` を設定 |
| ピクセル | `TrySetPixel` | `bool TrySetPixel(int x, int y, Color color)` | 指定座標へ色を設定（範囲外は `false`） |
| ピクセル | `Clear` | `void Clear(byte b, byte g, byte r, byte a)` / `void Clear(Color color)` | 全ピクセルを指定色で塗りつぶし |
| その他 | `Clone` | `EsBitmap Clone()` | 独立した複製を生成（所有権は呼び出し側） |
| その他 | `Crop` | `EsBitmap Crop(Rectangle rect)` / `EsBitmap Crop(int x, int y, int width, int height)` | 指定矩形領域を切り出す（ソース範囲外は透明パディング。位置関係は保たれる） |
| その他 | `Dispose` | `void Dispose()` | 内部 `SKBitmap` を解放（二重呼び出しは無害） |
| その他 | `ThrowIfDisposed` | `void ThrowIfDisposed()` | 破棄済みなら `ObjectDisposedException` を送出 |

> `FromSKBitmap` は引数の所有権を譲り受け、本型の破棄時に `Dispose` します。`ToSKBitmap` は内部インスタンスをそのまま返すため呼び出し側で `Dispose` しないでください。

System.Drawing.Color（`System.Drawing.Primitives`）と SkiaSharp のみに依存するため、Windows 以外でも動作します。System.Drawing.Bitmap との変換を含む Windows 専用の拡張は `PlatformFactory` の `EsBitmapDrawingExtensions` が提供します。

## 利用側の注意

本プロジェクトは UI フレームワークへの依存を一切持ちません。Avalonia 等の UI 実装側で `EsKey` と自身のキー型を相互変換する Interop 層（例: `PlatformFactory` プロジェクトの `EsKeyAvaloniaInterop`）を用意して利用してください。
