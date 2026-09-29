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
| `Native.ICaptureService` | 画面領域のキャプチャ（画像は `EsBitmap`、座標は `EsRectInt`） |
| `Native.ICursorService` | OS カーソルの位置取得・移動・表示切替（座標は `EsPointInt`） |
| `Native.IWindowService` | ネイティブウィンドウ操作（タスクスイッチャー非表示・キャプチャ除外・退避。識別子は `EsPlatformHandle`） |
| `Native.IElementFinder` | 指定位置のウィンドウ / スクリーン検出と一覧取得 |
| `Native.ICursorOverrideGuard` | 選択操作中のカーソル上書き抑止 |
| `Native.IGlobalHotKeyService<T>` | グローバルホットキーの登録・解除・アクション配信 |
| `Native.ElementFinderResults` | 要素検出の結果（矩形・表示名・識別子・クラス名） |
| `Native.PlatformLog` | Win 実装が出力するログ定義（`[LoggerMessage]` ソース生成。internal） |
| `Native.Win.WinCaptureService` / `WinWindowService` / `WinElementFinder` / `WinCursorService` / `WinCursorOverrideGuard` / `WinGlobalHotKeyService<T>` | 上記抽象の Windows 実装（Win32 P/Invoke）。**既定実装の選択はホストが行い、抽象型として注入する**（具象を直接参照するのはホストとテストのみ） |
| `Native.Imaging.EsBitmapDrawingExtensions` | `System.Drawing.Bitmap` ⇔ `EsBitmap` の相互変換（`System.Drawing.Common` を使う Windows 専用の拡張） |
| `Coord.ICoordTransform` | 2 つの座標系間の変換ポート（順変換・逆変換・境界）。座標は `EsPoint` / `EsSize` |
| `Coord.CoordTransform` | 変換ポートの具象実装（`Func` 委譲）。`Identity` / `Compose` / `ScrollOffset` / `Scale` / `DomainScale`（境界付きオーバーロードあり） / `ImageLayout` / `ImageLayoutScaleX`。**画像レイアウト（レターボックス・スケール）と平行移動・スケーリングの純粋計算も本型が単一所有する**（各ファクトリが使う単一実装であり、独立したヘルパー型を持たない） |
| `Coord.ScrollMath` | スクロール範囲・ビューポートサイズの純粋計算（`MaxOffset` / `ViewportSize`）とホイール 1 段のスクロール量（`WheelScrollUnit`） |
| `Coord.DpiScaler` | DIP 値をスケーリング係数で物理ピクセルへ変換（`ScaleToPixel`。切り上げ・最低 1）と、その逆変換（`ToDip`（数値 / `EsSize`）。丸めなし）。DPI の基準値（`StandardDpi`）・DPI → スケーリングの換算（`ToScaling`）・係数の有効判定（`IsValidScaling`。**非数も無効**）と正規化（`NormalizeScaling`）も単一所有する |
| `Coord.CoordTransformExtensions` | 座標変換ポート未設定（null）を等価変換として解決する（`OrIdentity`）。各コントロール・コアのジオメトリは本規則を自前で書かない |
| `Geometry.EsPoint` / `EsSize` / `EsVector` / `EsRect` | UI フレームワーク非依存の DIP 相当の幾何値型（`readonly record struct`）。**座標ラベルの文言は `EsPoint.Format`**（`(x, y)` 形式。小数点以下の桁数を指定できる）、**領域の判定とクランプは `EsSize`**（`HasArea` / `ClampToArea` / `ClampOffset`）、**枠線の内側 4 辺・領域内クランプ・重なり部分は `EsRect`**（`ComputeInnerEdges` / `ClampToArea` / `Intersect`）が単一所有する（コントロールを問わず同じ式を使う。選択矩形の枠線とルーペの外枠、選択のドメイン境界とルーペの位置クランプ、画像の表示矩形とビューポートの交差） |
| `Geometry.EsPointInt` / `EsSizeInt` / `EsRectInt` | UI フレームワーク非依存の**整数版**の幾何値型（`EsPoint` / `EsSize` / `EsRect` の整数版。画面物理ピクセル座標・画像ピクセル座標・矩形に使う） |
| `Geometry.EsAxis` | レイアウト計算で扱う軸（`X` / `Y`）。軸を `bool` で指定しないための列挙型 |
| `EsPlatformHandle` | UI フレームワーク非依存のネイティブウィンドウ識別子（座標・寸法ではないため `Geometry` ではなくルート直下） |
| `Imaging.EsBitmap` | 複数のビットマップ種別を横断する軽量なユニバーサルビットマップラッパー（BGRA8888・Premul）。**自前のピクセルバッファ（`NativeMemory.AlignedAlloc` の 64 バイト整列）を所有**し、外部パッケージに依存しない（Skia との相互変換は [`Platform.Skia`](../Platform.Skia/README.md) が担う） |
| `Imaging.ImageDrawMode` | 画像を描画先の領域へ配置する方法（縦横比を保つ `Uniform` / 領域全体へ引き伸ばす `Fill`） |
| `Imaging.ImageDrawMath` | 画像を描画先の領域へ配置する矩形の計算（純粋関数）。UI ごとに異なる描画側は本クラスの結果を使う |
| `Imaging.DrawingColorExtensions` | 描画色の判定（`IsVisible`＝アルファが 0 より大きいか）。「アルファ 0 は描かない」の判定を単一所有する |
| `LoggerFallback` | ロガーの null を `NullLogger.Instance` へ吸収する共通ヘルパー（`logger.OrNullLogger()`）。「ロガー未設定（null）は出力なし」の規則を単一所有し、各コントロール・サービスの `?? NullLogger.Instance` を排除する |

各 UI 実装は、自身の型とコア型を相互変換する Interop 層（例: Avalonia 版の `EsKeyAvaloniaInterop` / `EsWindowStateAvaloniaInterop` / `EsGeometryAvaloniaInterop` / `EsPlatformHandleAvaloniaInterop` / `ColorAvaloniaInterop`）を個別に提供します。

**色の表現**: コアの色型は `System.Drawing.Color`（`System.Drawing.Primitives`。共有フレームワーク同梱でクロスプラットフォーム）です。`EsBitmap` のピクセル API もこの型を使用します。Skia の描画 API へ渡す場合の変換（`ToSKColor`）は [`Platform.Skia`](../Platform.Skia/README.md) が単一所有します。

サービス抽象（`Native/`）とその **Windows 実装（`Native/Win/`）** を同一プロジェクトに置きます。**実行時のプラットフォーム分岐（ファクトリ）を持たず**、ホスト（アプリ・コントロール）が `WinXxx` を生成して抽象型として注入します。

```csharp
// ホスト（Windows 前提）が既定実装を生成して注入する
services.AddSingleton<IGlobalHotKeyService<CaptureType>>(_ => new WinGlobalHotKeyService<CaptureType>());
```

テストは同じ抽象を実装したモックを注入できるため、Win32 環境を必要としません。

## 依存方向

```text
Platform.AvaloniaUI / Platform.MewUI（UI アダプタ）
  └──> Platform.Core（本プロジェクト。Native/ 配下が Win32 実装を担う）
```

本プロジェクトが UI フレームワークへ依存することはありません。

### 依存の判定基準（本プロジェクトの存在理由）

本プロジェクトは**「このソリューション以外でもそのまま使える型」**だけを置きます（`Native/` 配下だけが例外です）。判定は依存で機械的に行います。

| 基準 | 内容 |
| --- | --- |
| **外部 PackageReference なし** | `.NET 標準ライブラリ`（`System.*`）と **Microsoft 公式パッケージ**（`Microsoft.*`）は許容する。それ以外の外部パッケージは**参照しない**（違反は `.work/check-packages.ps1` が検出する） |
| **UI フレームワーク非依存** | Avalonia / MewUI / WinUI / WinForms を参照しない |
| **ソリューション固有の概念を持たない** | 特定のアプリ・コントロールに固有の型は置かない（そのような型は各コントロールの層へ置く） |
| **OS 依存なし** | Win32 P/Invoke を含まない（`Native/` 配下の責務） |

**`Native/` 配下は上記の「可搬性」の基準から除外します**（Windows 限定の実装を、抽象と同じ場所で保守するための例外です）。Win32 P/Invoke と `System.Drawing.Common`（`System.Drawing.Bitmap` を扱う `Native.Imaging`）は `Native/` 配下だけが持ちます。

**Skia は「外部パッケージ」に該当するため、本プロジェクトは SkiaSharp に依存しません**（Skia への依存は [`Platform.Skia`](../Platform.Skia/README.md) が単一所有します）。

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
