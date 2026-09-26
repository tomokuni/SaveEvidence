# LoupeControl.UIBase 外部機能仕様書

`LoupeControl.UIBase`（`LoupeControl` をコントロールとして成り立たせる UI 非依存層）が提供する外部機能と公開 API の仕様です。利用者（UI 実装・ホスト・独自コントロールの作者）から見える情報のみを記載し、内部実装の詳細は記載しません。設計の意図は [README](README.md)、構造と依存は [SPEC_System](SPEC_System.md) を参照してください。

本層の機能は 3 つに分かれます。

| 区分 | 機能 |
| --- | --- |
| **描画手順** | ルーペの図形の描画・ラベル要素の配置指示 |
| **組み込み規則** | 再描画要求のバッチ化と世代番号管理 |
| **UI が出力するログ** | 拡大元画像の変更・描画の例外のログ定義と、ログへ出す値の書式 |

## 1. 提供機能

### 1.1 描画手順

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| ルーペの描画（図形） | `ILoupeRenderer.DrawShapes`（本層の契約） | 下地・市松模様・拡大画像・十字線・外枠を描画順に描く（**テキストは描かない**。テキストを canvas へ描く実装は基底の `DrawLabel`（既定は何もしない）を上書きする）。層の順序はコア（`LoupeDrawContent.Layers`）がデータとして単一所有し、本層の `LoupeRendererBase` がその順に写す |
| 拡大画像の切り出しと描画（色を使わない層） | `LoupeRendererBase`（本層の基底が実装する） | コアが解決した切り出し領域（`LoupeDrawContent.MagnifiedImage`）で `EsBitmap.Crop` を呼び、描画先へ `DrawImage` する。破棄済みソースの `ObjectDisposedException` は本層で吸収する（市松模様のみが残る） |
| テキスト要素の配置 | `LoupeLabelPlan.Resolve`（UI 層が呼ぶ） | 実寸（描画 API の測定値）からラベル要素の配置指示（`LoupeLabelPlacement`）を解決する。**要素として置くか画像として描くかは UI 層が決める** |
| 描画に使う色 | `LoupePaint` | 下地・市松模様・外枠・十字線・中心座標ラベルの色を単一所有する（**描画の可否は持たない**） |

#### 1.1.1 前提条件

- 描画先は `IEsCanvas`（`EsUtil.SaveEvidence.Common.Imaging`）です。Skia は `SkiaEsCanvas`、Avalonia の `DrawingContext` は `AvaloniaCanvas`（`Platform.AvaloniaUI`）を使います。
- 描画の単位は**物理ピクセル**です（`LoupeRenderRequest.PixelSize` が作る正方形が描画面）。DIP → 物理ピクセルの換算はコアが済ませています。
- 描画面の消去は下地の層（`LoupeDrawLayer.Background`）が行います（前フレームの残像を残しません）。
- 拡大元画像は UI ごとに型が異なるため引数で渡します（本層は `EsBitmap` のみを受け取ります）。
- 描画の色は `LoupePaint` として描画実装が保持します（コアの契約（`LoupeRenderRequest`）には含めません）。

### 1.2 組み込み規則

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| 再描画要求のバッチ化 | `LoupeRenderCoordinator.RequestRender` | 複数の要求を 1 フレーム 1 回の実行へ結合する（表示中のみ。非表示ならビットマップを解放する） |
| 世代番号による古い結果の破棄 | `LoupeRenderCoordinator.ShouldApply` | 非同期に生成した描画結果が最新世代かどうかを判定する |
| 要求の破棄と解放 | `LoupeRenderCoordinator.CancelAndRelease` | 保留中の要求を破棄し、保持しているビットマップを解放する（非表示遷移・明示クリア） |

#### 1.2.1 前提条件

- 実行キューと優先度は UI 層が `post` コールバックとして注入します（本層はフレームワーク固有の API を知りません）。
- 表示状態・ビットマップの解放・描画の開始もコールバックで受け取ります（本層はコントロールの状態とリソースを保持しません）。
- 中間ビットマップを持たず同期描画する UI（MewUI 版）は本機能を使いません。

### 1.3 UI が出力するログ

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| 拡大元画像の変更のログ | `LoupeUiLogMessage.LogSourceChanged` | 設定値の変更ログと同じ書式・レベル（Debug）で出力する。**同一インスタンスの再設定では出力しない**（参照の同一性の判定を本層が単一所有する）。画像の型は UI ごとに異なるため、寸法の取り出しは各 UI が担う（書式は `LoupeLogFormat.DescribeImage` が単一所有する） |
| 描画の例外のログ | `LoupeUiLogMessage.LogUpdateError` | Error レベルで出力する（例外スタックトレース付き）。描画の例外を検知するのは UI の描画パイプラインであるため、本層が定義を所有する |
| ログへ出す値の書式 | `LoupeLogFormat` | 未設定の表記（`Null`）と画像の寸法（`DescribeImage`）。**書式を UI 間で揃える**（UI 実装が自前で整形すると同じ値でも書式がずれる） |

#### 1.3.1 前提条件

- ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有します（本層は定義のみ）。
- コアが出力する定義（設定値・拡大中心の変更）は `Feature.LoupeCore` の `LoupeLogMessage`（internal）が単一所有します。UI は本層の定義だけを呼びます。
- UI 固有の描画方式に対応するログ（中間ビットマップを生成するパイプライン固有のもの等）は各 UI プロジェクトが所有します（本層へは置きません）。

## 2. 公開 API 一覧

### `EsUtil.SaveEvidence.Loupe.UIBase`（描画手順）

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupeRenderer` | sealed class | 既定の描画実装（`ILoupeRenderer`（本層の契約））。色（`LoupePaint`）を必須引数で受け取り、インスタンスごとに保持する |
| `LoupePaint` | sealed record | 描画に使う色（下地・市松模様・外枠・十字線・中心座標ラベルの 7 色）の単一所有者（**描画可否の述語は持たない**。可否はコアの明示フラグが単一所有し、非表示の項目は空の描画内容として渡る）。`Default` を基点に `with` 式で部分変更する |
| `LoupeLabelPlan` | static class | ラベル要素の配置指示の解決（色は `LoupePaint` から受け取る） |
| `LoupeUiLogMessage` | static partial class | UI が出力するログ定義（拡大元画像の変更・描画の例外） |
| `LoupeLogFormat` | static class | UI がログへ出す値の書式（未設定の表記・画像の寸法） |

### `LoupeRenderer` のメンバー（`ILoupeRenderer` の実装）

| メンバー | 説明 |
| --- | --- |
| `LoupeRenderer(LoupePaint paint)` | 描画に使う色を指定して生成する（`LoupePaint.Default` を基点に組み立てる）。`paint` が null なら `ArgumentNullException` |
| `DrawShapes(IEsCanvas canvas, EsBitmap? source, in LoupeDrawContent content)` | 層の順に図形を描く（層の順序・矩形・拡大画像の幾何はコアが解決済み。本層は写すだけ）。`source` が null または切り出し領域が無い場合は拡大画像を描かない。`canvas` または内容の層が null なら `ArgumentNullException` |
| `CoordLabelFontSize` / `CoordLabelCornerRadius`（描画実装＝`LoupeRenderer` のプロパティ） | ラベルのフォントサイズと角丸（描画の設定 `LoupePaint` の値への入口）。**描画の契約（`ILoupeRenderer`）には含めない** |
| `DrawLabel(IEsCanvas canvas, in LoupeDrawContent content)`（`protected virtual`） | テキストを描画ポートへ描くフック。**既定は何もしない**（UI 要素として置く UI は上書きしない） |

### `LoupePaint` のメンバー（`EsUtil.SaveEvidence.Loupe.UIBase`・`public sealed record`）

| メンバー | 説明 |
| --- | --- |
| `Default` | すべての色を既定値にした値（部分変更の基点: `LoupePaint.Default with { ... }`） |
| `BackgroundColor` / `CheckerColorA` / `CheckerColorB` / `FrameColor` / `CrossColor` / `CoordLabelColor` / `CoordLabelBackgroundColor` | 描画に使う 7 色（UI 非依存の `System.Drawing.Color`） |

### `LoupeLabelPlan` のメンバー

| メンバー | 説明 |
| --- | --- |
| `Resolve(LoupeTextPlacement? coordLabel, LoupePaint paint, Func<string, double, EsSize> measureText)` | 配置するラベル要素（`LoupeLabelPlacement`）を 1 件返す（テキストが無い場合は空）。`paint` または `measureText` が null なら `ArgumentNullException` |

### `LoupeRenderCoordinator`（`EsUtil.SaveEvidence.Loupe.UIBase`・`public sealed class`）

| メンバー | 説明 |
| --- | --- |
| `LoupeRenderCoordinator(Func<bool> isVisible, Action releaseBitmap, Action<long> startRender, Action<long> logRenderCore, Action<Action> post)` | 表示状態の取得・ビットマップの解放・描画の開始・ログ・実行キューへのポストを注入する。いずれかが null なら `ArgumentNullException` |
| `Generation` | 現在の世代番号（`long`）。描画結果へ対応付けて古い結果を判別する |
| `RequestRender()` | 再描画を要求する（表示中は 1 フレーム 1 回へ結合、非表示はビットマップを解放する） |
| `ShouldApply(long generation)` | その世代の描画結果を表示へ適用してよいか（表示中かつ最新世代なら true） |
| `CancelAndRelease()` | 保留中の要求を破棄し、ビットマップを解放する |
| `RenderCore()` | 実行キューから呼ばれる描画の実行（世代番号を採番してから描画を開始する） |
| `CancelPending()` | 保留中の要求を破棄する（解放はしない） |

```csharp
var coordinator = new LoupeRenderCoordinator(
    isVisible: () => IsVisible,
    releaseBitmap: ReleaseBitmap,
    startRender: StartRender,
    logRenderCore: generation => Log.LogRenderCore(generation),
    post: action => Dispatcher.UIThread.Post(action, DispatcherPriority.Render));

coordinator.RequestRender();      // 複数の要求は 1 回のポストへ結合される
coordinator.CancelAndRelease();   // 非表示遷移・明示クリア

// 非同期の描画結果を反映する側で世代を照合する
if (coordinator.ShouldApply(generation)) { ApplyBitmap(bitmap); }
```

### `LoupeUiLogMessage`（`EsUtil.SaveEvidence.Loupe.UIBase`・`public static partial class`）

| メンバー | 説明 |
| --- | --- |
| `LogSourceChanged(ILogger, EsBitmap? oldValue, EsBitmap? newValue)` | 拡大元画像の変更（Debug。`[Feature.LoupeCore] Setting: Source {OldValue} → {NewValue}`）。**同一インスタンスなら出力しない** |
| `LogSourceChanged(ILogger, string oldValue, string newValue)` | 寸法を UI が整形して渡す経路（上記と同じ書式。書式は `LoupeLogFormat.DescribeImage` を使う） |
| `LogUpdateError(ILogger, Exception)` | 描画の例外（Error。`[Feature.LoupeCore] Update exception`） |

```csharp
logger.LogSourceChanged(oldBitmap, newBitmap);   // 別インスタンスなら出力する
logger.LogSourceChanged(LoupeLogFormat.DescribeImage(width, height), LoupeLogFormat.Null);
logger.LogUpdateError(exception);
```

### `LoupeLogFormat`（`EsUtil.SaveEvidence.Loupe.UIBase`・`public static class`）

| メンバー | 説明 |
| --- | --- |
| `Null` | `"null"`（未設定の表記） |
| `DescribeImage(int? width, int? height)` | `Width×Height` 形式。片方でも未設定なら `null` |

## 3. 契約（利用者が守る・受け取る値）

### 描画の呼び分け

| 描く対象 | 呼ぶ API |
| --- | --- |
| 図形（下地・市松模様・拡大画像・十字線・外枠） | `DrawShapes`（**テキストは描かない**） |
| テキスト（中心座標） | `LoupeLabelPlan.Resolve`（コアの `LoupeDrawContent.CoordLabelText` を渡す。UI が要素として置くか canvas へ描くかを決める） |

### 描画面（`LoupeRenderRequest`）

| 項目 | 内容 |
| --- | --- |
| `PixelSize` | 一辺の物理ピクセル。描画面は左上原点の `PixelSize` 正方形 |
| `SurfaceBounds` | その正方形の矩形（下地・市松模様が塗る範囲） |
| `ZoomLevel` | 拡大率（十字線の交点と切り出し領域に使う） |
| `Options` | ルーペの設定（拡大率・寸法・ラベル表示の可否）。色は `LoupePaint` が単一所有する |
| `DipToDeviceScaling` | 寸法の DIP 値 → 物理ピクセルの換算係数（換算はコアが済ませている） |

### 層（`LoupeDrawLayer`）

| 層 | 内容 |
| --- | --- |
| `Background` | 下地（`Clear` で描画面全体を置き換える） |
| `Checkerboard` | 市松模様（明色で全面を塗り、暗色タイルを列挙して塗る） |
| `Image` | 拡大画像（中心の周囲を切り出して拡大描画） |
| `Crosshair` | 十字線（切り出し領域の中央） |
| `Frame` | 外枠（4 辺） |
| `CoordLabel` | 中心座標ラベル（**本層の `DrawShapes` は描かない**。UI の要素が描く） |

### 再描画のスケジューリング

- `RequestRender()` は表示中に何度呼んでも、実行キューへのポストは 1 回に結合されます（**1 フレーム 1 回**）。
- 描画結果を表示へ反映する側は、必ず `ShouldApply(generation)` で世代を照合してください（古い結果を適用すると表示が巻き戻ります）。
- 非表示状態で `RequestRender()` を呼ぶと、ビットマップの解放のみが行われます。

## 4. 制限事項

- 拡大の補間方式（最近傍 / 線形）は描画ポートの実装に依存します。
- 拡大描画の描画先は描画面より最大 `ZoomLevel - 1` ピクセル大きくなることがあります（切り上げた切り出し領域の拡大分。はみ出しは描画先のクリップに任せます）。
- 文字の折り返し・複数行は扱いません（1 行のみ）。
- 破線・クリップ・不透明度の指定は扱いません（描画ポートの契約に含めない）。
- 描画面（オフスクリーンビットマップ等）の生成は UI の責務です（本層は与えられた描画面へ描くだけ）。
- 再描画のスケジューリングは「実行キューへのポスト」を前提とします（同期描画の UI は本機能を使わず、そのまま描画します）。

## 5. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
