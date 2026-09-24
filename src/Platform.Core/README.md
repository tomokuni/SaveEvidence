# Platform.Core

`Platform.AvaloniaUI`（Avalonia UI 実装）および将来の MewUI 実装が共有する、**UI フレームワーク非依存の型**を提供するプロジェクトです。

## 目的

UI フレームワークに依存していた型（ホットキー設定・座標変換・画像コンテナ・サービス抽象）を本プロジェクトへ切り出すことで、`app.Core`（設定モデル・ViewModel）や各コントロールのコアロジック、将来の MewUI 実装からも UI フレームワーク非依存のまま利用できます。

## 収録する型

| 型 | 責務 |
| --- | --- |
| `EsKey` | UI フレームワーク非依存のキー識別子（列挙型）。メンバー名は `Avalonia.Input.Key` と同一にし、文字列表現・シリアライズの互換性を保つ |
| `CaptureType` | キャプチャ種別（全画面 / ウィンドウ選択 / 範囲選択）。UI 実装のキャプチャ UI ライブラリとホストが共有する。ホストではグローバルホットキーのアクション識別子（`IGlobalHotKeyService<CaptureType>`）としても使用する |
| `EsWindowState` | UI フレームワーク非依存のウィンドウ表示状態（列挙型）。メンバー名は `Avalonia.Controls.WindowState` と同一にし、設定ファイルのシリアライズ互換性を保つ |
| `HotKeyModifiers` | ホットキーの修飾キー（Alt / Control / Shift / Windows）を表すフラグ列挙型 |
| `HotKeySetting` | 修飾キーとキーの組合せを表す値オブジェクト（record）。`ToString()` で "Ctrl + Shift + Q" 形式を返す |
| `ICaptureService` | 画面領域のキャプチャ（画像は `EsBitmap`、座標は `EsPixelRect`） |
| `ICursorService` | OS カーソルの位置取得・移動・表示切替（座標は `EsPixelPoint`） |
| `IWindowService` | ネイティブウィンドウ操作（タスクスイッチャー非表示・キャプチャ除外・退避。識別子は `EsPlatformHandle`） |
| `IElementFinder` | 指定位置のウィンドウ / スクリーン検出と一覧取得 |
| `ICursorOverrideGuard` | 選択操作中のカーソル上書き抑止 |
| `IGlobalHotKeyService<T>` | グローバルホットキーの登録・解除・アクション配信 |
| `ElementFinderResults` | 要素検出の結果（矩形・表示名・識別子・クラス名） |
| `Coordinate.ICoordTransform` | 2 つの座標系間の変換ポート（順変換・逆変換・境界）。座標は `EsPoint` / `EsSize` |
| `Coordinate.CoordTransform` | 変換ポートの具象実装（`Func` 委譲）。`Identity` / `Compose` / `ScrollOffset` / `Scale` / `DomainScale`（境界付きオーバーロードあり） / `ImageLayout` / `ImageLayoutScaleX` |
| `Coordinate.ScrollMath` | スクロール範囲・ビューポートサイズの純粋計算（`MaxOffset` / `ViewportSize`）とホイール 1 段のスクロール量（`WheelScrollUnit`） |
| `Coordinate.DpiScaler` | DIP 値をスケーリング係数で物理ピクセルへ変換（`ScaleToPixel`。切り上げ・最低 1）と、その逆変換（`ToDip`（数値 / `EsSize`）。丸めなし）。DPI の基準値（`StandardDpi`）・DPI → スケーリングの換算（`ToScaling`）・係数の有効判定（`IsValidScaling`。**非数も無効**）と正規化（`NormalizeScaling`）も単一所有する |
| `Coordinate.CoordTransformExtensions` | 座標変換ポート未設定（null）を等価変換として解決する（`OrIdentity`）。各コントロール・コアのジオメトリは本規則を自前で書かない |
| `Geometry.EsPoint` / `EsSize` / `EsVector` / `EsRect` | UI フレームワーク非依存の DIP 相当の幾何値型（`readonly record struct`。`EsRect` は `Right` / `Bottom` を提供） |
| `Geometry.EsPixelPoint` / `EsPixelRect` | UI フレームワーク非依存の画面物理座標（整数ピクセル） |
| `Geometry.EsPlatformHandle` | UI フレームワーク非依存のネイティブウィンドウ識別子 |
| `Geometry.CoordinateFormatter` | 座標を `(x, y)` 形式の表示文字列へ整形する純粋関数 |
| `Imaging.EsBitmap` | 複数のビットマップ種別を横断する軽量なユニバーサルビットマップラッパー（BGRA8888・Premul）。内部に `SKBitmap` を唯一所有し、SkiaSharp のみに依存する |
| `Imaging.ImageDrawMode` | 画像を描画先の領域へ配置する方法（縦横比を保つ `Uniform` / 領域全体へ引き伸ばす `Fill`） |
| `Imaging.ImageDrawMath` | 画像を描画先の領域へ配置する矩形の計算（純粋関数）。UI ごとに異なる描画側は本クラスの結果を使う |
| `Imaging.SkiaColorInterop` | コアの色型（`System.Drawing.Color`）を Skia の色型へ変換する共通ヘルパー（`color.ToSKColor()`）。MewUI 版の描画層（6 箇所）が共用する |
| `Imaging.SkiaGeometryInterop` | コアの幾何型（`EsRect` / `EsPoint`）を Skia の幾何型へ変換する共通ヘルパー（`rect.ToSKRect()`）。Skia の整数サイズからコアのサイズへの逆変換（`ToEsSize`）も持つ |
| `Imaging.SkiaFill` | Skia の矩形塗り（`Rect` / `Rects` / `RoundRect`、単色・不透明前提でアンチエイリアス無効）の単一所有。描画層とホストの文字描画が共用する |
| `Imaging.SkiaLabel` | Skia のラベル 1 件（背景 → 文字の順序。背景の角丸に対応）の単一所有。両 UI のラベル描画が共用する |
| `Imaging.SkiaText` | Skia のテキスト描画（フォント解決（非 ASCII は字形を持つフォントへフォールバック）・実寸測定・描画）の単一所有。描画層とホストの文字描画が共用する |
| `CoordMath`（internal） | 画像レイアウト計算の一元化（`CalculateUniformLayout` / `ComputeImageRenderInfo`）。`CoordTransform` から利用する |
| `LoggerFallback` | ロガーの null を `NullLogger.Instance` へ吸収する共通ヘルパー（`logger.OrNullLogger()`）。「ロガー未設定（null）は出力なし」の規則を単一所有し、各コントロール・サービスの `?? NullLogger.Instance` を排除する |

各 UI 実装は、自身の型とコア型を相互変換する Interop 層（例: Avalonia 版の `EsKeyAvaloniaInterop` / `EsWindowStateAvaloniaInterop` / `EsGeometryAvaloniaInterop` / `EsPlatformHandleAvaloniaInterop` / `ColorAvaloniaInterop`）を個別に提供します。

**色の表現**: コアの色型は `System.Drawing.Color`（`System.Drawing.Primitives`。共有フレームワーク同梱でクロスプラットフォーム）です。`EsBitmap` のピクセル API もこの型を使用します。Skia の描画 API へ渡す場合は `SkiaColorInterop.ToSKColor` を使用します（SkiaSharp は UI フレームワークではないため、本プロジェクトが変換の単一所有を担います）。

サービス抽象の具象実装（Win32 / NoOp）と `PlatformFactory.CreateXxx()` のファクトリは、現在 `Platform.AvaloniaUI`（Avalonia 実装プロジェクト）側にあります。

## 依存方向

```text
Platform.AvaloniaUI（Avalonia アダプタ）/ Platform.Native（OS 実装）
  └──> Platform.Core（本プロジェクト）
```

本プロジェクトが UI フレームワークへ依存することはありません。外部依存は `SkiaSharp`（`EsBitmap`）と `Microsoft.Extensions.Logging.Abstractions`（`LoggerFallback`）のみです。

## ビルド

```powershell
dotnet build src/Platform.Core/src/Platform.Core.csproj
```

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。

```powershell
dotnet test src/Platform.Core/tests/Platform.Core.Tests.csproj
```

詳細な設計は `SPEC_System.md`、公開 API は `SPEC_ExtFunc.md` を参照してください。
