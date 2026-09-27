# Feature.LoupeCore システム仕様書

`LoupeControl`（選択範囲を拡大表示するコントロール）の **UI 非依存のコア**の構造・依存・例外処理・設計判断・コンポーネント構成を記載します。外部機能と公開 API は [SPEC_ExtFunc](SPEC_ExtFunc.md)、利用手順は [README](README.md) を参照してください。

## 1. システム概要

| 項目 | 内容 |
| --- | --- |
| 目的 | ルーペの表示レイアウト・描画入力・描画内容の解決を 1 箇所に集約し、UI ごとの実装差から切り離す |
| 提供形態 | クラスライブラリ（`Feature.LoupeCore.dll`） |
| 想定利用者 | UI 実装（`LoupeControl.AvaloniaUI` / `LoupeControl.MewUI`）・それらを組み込むホストアプリケーション |
| 汎用性 | 特定のアプリ・UI に依存しない。SaveEvidence のマルチ UI 対応における「コントロールの Core / UI 分割」のパイロット実装であり、そのまま別プロジェクトへ独立できる |

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| ターゲット | `net10.0` |
| 言語 | C# 14（`Nullable` 有効・`ImplicitUsings` 有効） |
| ルート名前空間 | `EsUtil.Feature.LoupeCore`。フォルダへ追従し、`.Checkerboard` / `.Decoration` / `.Drawing` / `.Logging` / `.Placement` / `.Setting` を持ちます |
| ドキュメント生成 | `GenerateDocumentationFile` 有効（XML は配布物へ出さず `cref` の検証のみに使う） |
| テスト | xUnit v3（UI 初期化不要。`InternalsVisibleTo` で `internal` を検証する） |
| 依存 | UI フレームワークを一切参照しない。直接依存は `Platform.Core` と `Microsoft.Extensions.Logging.Abstractions`（ログ定義のソース生成。SkiaSharp は `Platform.Core` 経由） |

## 3. アーキテクチャ

### 3.1 依存方向

```mermaid
graph LR
  A[LoupeControl.AvaloniaUI<br/>Avalonia 実装] --> R[LoupeControl.UIBase<br/>描画の手順とラベル計画]
  M[LoupeControl.MewUI<br/>MewUI 実装] --> R
  R --> B[Feature.LoupeCore]
  A --> C[LoupeControl.UIBase<br/>コントロール化のスケジューリング]
  C --> B
  B --> D[Platform.Core]
```

層の全体像（Core / UIBase / UI）は [`src/README.md`](../README.md) を参照してください。

- `Feature.LoupeCore` は UI フレームワークへ依存しません。依存は `Platform.Core` のみです。
- **描画の手順と色は UI 側の共有層（`LoupeControl.UIBase` の `ILoupeRenderer` / `LoupeRendererBase` / `LoupeRenderer` / `LoupePaint`）が単一所有します**（描画先は `Common` の `IEsCanvas`。本プロジェクトは描画 API も色も持ちません）。
- **コントロール化にだけ必要な処理（再描画要求のバッチ化・世代番号管理）は `LoupeControl.UIBase` が所有します**（同層も UI フレームワークに依存せず、実行キューは UI がコールバックで注入します）。
- UI 実装は、コア型と自身の型（`Avalonia.Point` / `Size` / `IBrush`、`SKColor`）を相互変換します。変換は境界（サービス呼び出し・イベント引数の生成・描画）の 1 行に限定し、UI 側の状態・描画は UI の型のまま維持します。
- 設定（表示サイズ・拡大率・寸法・中心座標ラベルの表示可否）は `LoupeOptions` が単一所有し、UI 層は本型を 1 プロパティとして公開するだけです（設定の既定値・補正も本型が持つ）。UI 層は寸法値を保持しません（描画時に読むだけ）。**色はコアの型に含めず、描画層 `LoupeControl.UIBase` の `LoupePaint` が単一所有します**（コアは色の値を決めず、描画可否の判定にも使いません）。
- `LoupeOptions` を含む**大部分の型を `public`** とします（外部が独自の UI アダプタを実装できるようにするため）。内部手順（配置計算・描画引数の計算）は `internal` とし、単体テストにのみ `InternalsVisibleTo`（`Feature.LoupeCore.Tests`）で公開します。
- コントロール化に必要な型（`LoupeRenderCoordinator`）は `LoupeControl.UIBase` が `public` として提供します。

### 3.2 データフロー（レイアウト → 描画）

```mermaid
sequenceDiagram
  participant UI as LoupeControl（UI 実装）
  participant Fac as Loupe（コアのファサード）
  participant Core as Feature.LoupeCore
  participant PC as Platform.Core

  UI->>Fac: ResolveLayout(parentSize)
  Fac->>Core: parentSize.ClampToArea(requestedSize)
  Core-->>Fac: 表示サイズ（DIP。要素サイズ・描画サイズ・位置クランプで同じ値を使う）
  Fac->>PC: ICoordTransform.Inverse(center) → EsPoint（親領域 DIP）
  Fac->>Core: LoupePlacementGeometry.ComputePosition(表示サイズ, parentSize, centerDip, offset)
  Core-->>Fac: EsPoint（表示位置）
  Fac-->>UI: LoupeLayout（表示サイズ + 表示位置）
  UI->>UI: 要素サイズ（Width / Height）と RenderTransform / Arrange へ反映する

  UI->>Fac: CreateRequest(表示サイズ, canvasSize)
  Fac->>Core: LoupeRenderRequest.Create(center, 表示サイズ, dipToDeviceScaling, Options, 描画先)
  Core-->>Fac: 描画入力（物理ピクセルサイズを解決済み。拡大率・寸法は Options から取る）
  Fac-->>UI: LoupeRenderRequest（描画と通知の両方で使用）

  UI->>Fac: ResolveContent(request)
  Fac->>Core: Loupe.ResolveContent(request)
  Fac-->>UI: LoupeDrawContent（描画面・タイル・矩形・層の順序）

  Note over UI: 描画完了通知（切り出し左上・拡大率）は描画入力（CaptureTopLeft / ZoomLevel）から読む
```

## 4. コンポーネント構成

| ファイル | 型 | 可視性 | 責務 |
| --- | --- | --- | --- |
| `src/Loupe.cs`（ルート） | `Loupe` | public | **公開ファサード（操作の唯一の窓口）**。設定・環境値・拡大中心・位置オフセットの保持、表示レイアウトの解決（`ResolveLayout`）、描画入力の生成（`CreateRequest`）、**描画内容の解決（`ResolveContent`）**、設定変更の検出とログ出力（`Log`） |
| `src/Loupe.DrawContent.cs`（ルート） | `Loupe`（**partial**） | internal | 描画内容の**内部手順**（市松模様のタイル・十字線 / 枠線の矩形・中心座標ラベルのテキスト配置）と描く層の一覧の組み立て。描くかどうかの判断を単一所有し、**層の有無の全組合せ（32 通り）を事前生成した表**から選ぶ（描画のたびに一覧を組み立てない）。公開の入口 `ResolveContent` は `Loupe.cs` が持つ |
| `src/Loupe.Limits.cs`（ルート） | `Loupe.Limits`（`Loupe` の入れ子） | public | 数値パラメータの既定値・安全域（最小 / 最大）の単一所有と、範囲外の値の補正。**UI がプロパティ登録に使う** |
| `src/Setting/LoupeOptions.cs` | `LoupeOptions` | public | 設定の値オブジェクト（表示サイズ・拡大率・寸法・表示可否）。既定値・生成時の補正（`init` アクセサ。設定経路によらず範囲内の値だけを保持する）・`Default`・`ShouldShowCoordLabel`。**色は持たない**（色は描画層 `LoupeControl.UIBase` の `LoupePaint` が単一所有する） |
| `src/Drawing/LoupeRenderRequest.cs` | `LoupeRenderRequest` | public | 1 フレーム分の描画入力（コア⇔UI の契約）、DIP 値からの組み立て（`Create`。**換算済みの寸法** `CheckerSizePixels` / `BorderThicknessPixels` を含む。UI 層は自分で組み立てず、ファサードの `Loupe.CreateRequest` が返した値を受け取る）、切り出し領域の幾何の導出（`CaptureSize` / `MagnifiedImage` / `SurfaceBounds` は internal、`CaptureTopLeft` / `ZoomLevel` は描画完了通知のために public） |
| `src/Drawing/LoupeDrawContent.cs` | `LoupeDrawContent` | public | **1 フレーム分の描画内容**（描画面の矩形・タイル列挙子・十字線 / 枠線の矩形・拡大画像の幾何・層の順序）。描画層へ渡す唯一の描画契約であり、幾何計算を含まない |
| `src/Drawing/LoupeMagnifiedImage.cs` | `LoupeMagnifiedImage` | public | 拡大画像の切り出しと描画の矩形（切り出し領域・描画元・描画先） |
| `src/Drawing/LoupeCheckerTiles.cs` | `LoupeCheckerTiles` | public | 市松模様の暗色タイルの矩形を**リストを確保せずに列挙する構造体**（`foreach` で列挙でき、描画ポートの列挙子への適合は描画層が行う） |
| `src/Drawing/LoupeTextPlacement.cs` | `LoupeTextPlacement` | public | コアが UI 層へ渡すテキスト（文言・基準点・アライン・描画領域）。**要素として置くか canvas へ描くかは UI 層が決める**（体裁・フォントサイズ・角丸・色は描画層 `LoupeControl.UIBase` の `LoupePaint` / `LoupeLabelPlan` が単一所有する） |
| `src/Drawing/LoupeDrawLayer.cs` | `LoupeDrawLayer` | public | 描画層の順序（`Background` / `Checkerboard` / `Image` / `Crosshair` / `Border` / `Label`。`Label` はテキストがある場合のみ） |
| `src/Placement/LoupeLayout.cs` | `LoupeLayout` | public | 表示レイアウトの値（表示サイズ + 表示位置。配置しない場合は位置が null） |
| （本プロジェクトには置かない） | `ILoupeRenderer` / `LoupeRendererBase` | — | 描画の契約と描画手順の基底は **UI 側の共有層（`LoupeControl.UIBase`）が所有します**（本コアは描画 API も描画ポートも持たない） |
| `src/Logging/LoupeLogMessage.cs` | `LoupeLogMessage` | internal | コアが出力するログ定義（`LoggerMessage` ソース生成）の単一所有（**UI が出力する定義と書式は `LoupeControl.UIBase` が単一所有する**） |
| `src/Checkerboard/LoupeCheckerboard.cs` | `LoupeCheckerboard` | internal | 市松模様の明暗の判定規則（タイル矩形の列挙は `LoupeCheckerTiles`） |
| `src/Checkerboard/LoupeDivMath.cs` | `LoupeDivMath` | internal | 区画数の切り上げ除算（`CeilDiv`。「余りを 1 区画として数える」規則の単一所有。タイル数と切り出し領域の一辺が共用する） |
| `src/Decoration/LoupeDecoGeometry.cs` | `LoupeDecoGeometry` | internal | 十字線（縦横 2 本）と枠線（4 辺）の描画矩形の計算 |
| `src/Drawing/LoupeMetricsScaler.cs` | `LoupeMetricsScaler` | internal | 寸法値（タイルサイズ・枠線の太さ）の物理ピクセル換算（結果は描画入力へ解決済み。適用先が描画入力のため `Drawing/`） |
| `src/Placement/LoupePlacementGeometry.cs` | `LoupePlacementGeometry` | internal | 表示位置（左上）の計算（中心 + オフセットと親領域内へのクランプ。親サイズ未確定の判定も本クラスが扱う） |

## 5. 単一所有（同じ真実を 2 箇所に持たない）

| 計算 / 値 | 所有者 |
| --- | --- |
| ルーペの設定（表示サイズ・拡大率・寸法）とその既定値 | `LoupeOptions` |
| 設定の数値項目の安全域への補正（生成時） | `LoupeOptions`（`init` アクセサ。規則は `Loupe.Limits` が単一所有する） |
| 設定の変更ログの差分検出（変化した項目だけを出力） | `LoupeLogMessage.LogOptionsChanged`（出力の契機は `Loupe.Options` の setter） |
| ルーペの寸法（タイルサイズ・枠線の太さ）とラベル表示の可否 | `LoupeOptions` |
| 描画の色（下地・市松模様・枠線・十字線・中心座標ラベルの 7 色）とその既定値 | `LoupePaint`（描画層 `LoupeControl.UIBase`。**描画可否の述語は持たない**） |
| 下地（ベース）色とその既定値 | `LoupePaint.BackgroundColor`（描画層 `LoupeControl.UIBase`。最初に塗る） |
| 数値パラメータ（スカラー）の安全域への補正 | `Loupe.Limits` |
| 描画の可否（市松模様・十字線・枠線・中心座標ラベルの表示可否） | `LoupeOptions.ShowCheckerboard` / `ShowCrosshair` / `ShowBorder` / `ShowCoordLabel`（明示フラグ。透明色を代用にしない） |
| ラベル表示の可否（設定と中心の有無） | `LoupeOptions.ShouldShowCoordLabel` |
| 切り出し領域の一辺（ドメインピクセル） | `LoupeRenderRequest.CaptureSize`（internal。切り上げは `LoupeDivMath.CeilDiv`） |
| 切り出し領域の左上（中心 → 左上の変換） | `LoupeRenderRequest.CaptureTopLeft`（中心未設定は null。描画完了通知のために public） |
| 拡大画像の切り出しと描画の矩形（切り出し領域・描画元・描画先） | `LoupeRenderRequest.MagnifiedImage`（internal）。`Loupe.ResolveContent` が `LoupeDrawContent.MagnifiedImage` として描画層へ渡す（切り出しの実行は描画層） |
| 表示サイズの親領域へのクランプと表示サイズの決定 | `Loupe.ResolveLayout`（規則は `Platform.Core` の `EsSize.ClampToArea`。状態を保持しないため、親領域のサイズを引数で受ける） |
| 表示位置（中心 + オフセットと親領域内へのクランプ） | `Loupe.ResolveLayout`（規則は internal の `Placement.LoupePlacementGeometry.ComputePosition`。親サイズ未確定の判定は `EsSize.HasArea`） |
| 描画入力（物理ピクセルサイズ）の組み立てと描画先へのクランプ | `Loupe.CreateRequest`（規則は internal の `LoupeRenderRequest.ResolvePixelSize`） |
| 描画完了通知の内容（切り出し左上・拡大率） | `LoupeRenderRequest.CaptureTopLeft` / `ZoomLevel`（描画入力が単一所有する。専用の通知型を持たない） |
| 中心座標ラベルの文言・基準点と表示可否 | `Loupe.ResolveContent`（文言は `EsPoint.Format`、表示可否は `LoupeOptions.ShouldShowCoordLabel`） |
| ラベルの既定の見た目（文字色 = 白、背景色 = 黒 60%、フォントサイズ = 12.0） | 描画層 `LoupeControl.UIBase` の `LoupePaint` |
| 十字線・枠線の矩形 | `Loupe.ResolveContent`（規則は internal の `Decoration.LoupeDecoGeometry`） |
| ラベルの余白・背景矩形・文字原点 | 描画層 `LoupeControl.UIBase` の `LoupeLabelPlan`（左右の余白は `LoupeLabelPlan` が、上下はルーペ固有の値を単一所有する） |
| DIP → 物理ピクセルの変換（切り上げ・最低 1） | `Platform.Core` の `DpiScaler.ScaleToPixel` |
| 寸法値の物理ピクセル換算（市松タイル・枠線の太さ） | `LoupeRenderRequest.Create`（規則は internal の `Drawing.LoupeMetricsScaler`。結果は描画入力に含まれる） |
| 市松模様のタイル矩形の列挙 | `Loupe.ResolveContent`（`LoupeCheckerTiles`。リストを確保しない） |
| 設定・環境値・拡大中心・位置オフセットの変更の検出（変化したときだけ出力） | `Loupe`（`Options` / `DipToDeviceScaling` / `CenterPoint` / `PositionOffset` の setter） |
| ログの書式とレベル、変化判定（同値なら出力しない） | `LoupeLogMessage`（コアが出力する定義。UI が出力する定義と書式は `LoupeControl.UIBase` が単一所有する） |

`LoupeRenderRequest.MagnifiedImage`（internal）/ 描画層（`LoupeControl.UIBase`）の十字線・枠線描画はいずれも上記へ委譲し、同じ計算を二重に所有しません。

## 6. 例外処理ポリシー

本プロジェクトは純粋計算で構成されます。例外を送出するのは `EsBitmap` の引数検証（`ArgumentOutOfRangeException`）と破棄後アクセス（`ObjectDisposedException`）のみです。破棄済みソースの例外の捕捉は描画層（`LoupeRendererBase`）の責務とします。

## 7. 可視性ポリシー

- 大部分の型・メンバーを `public` とします（UI 非依存の汎用ライブラリとして、外部の UI アダプタ実装を可能にするため）。内部手順は `internal` とし、単体テストにのみ `InternalsVisibleTo` で公開します。
- **クラスが `internal` の場合、そのメンバーも `internal` とします**（`public` と書いても外部から見えないため、可視性の宣言を実態と一致させる）。
- 可視性の変更（`public` → `internal`）は破壊的な設計変更として扱います。
- **UI 固有のログは各 UI プロジェクトが所有します**（中間ビットマップを生成する描画パイプライン固有のログは `LoupeControl.AvaloniaUI` の `LoupeRenderLog`）。

## 8. テスト方針

| 方針 | 内容 |
| --- | --- |
| UI 初期化不要 | ヘッドレス実行を必要としないため `[Fact]` / `[Theory]`（`AvaloniaFact` ではない）で並列実行する |
| 単位 | 純粋計算（`LoupeCheckerboard` / `LoupeDivMath` / `LoupeDecoGeometry` / `LoupeMetricsScaler` / `LoupePlacementGeometry`）・ファサード・描画内容の解決 |
| 配置 | **ソースのフォルダを鏡映**する（`tests/Checkerboard` / `tests/Decoration` / `tests/Drawing` / `tests/Logging` / `tests/Placement` / `tests/Setting`。ファサードの partial のテストはルート） |
| UI 依存の検証 | `LoupeControl.AvaloniaUI.Tests`（Avalonia ヘッドレス・直列）が担う（コントロール生成・描画・`StyledProperty` の coerce 経由の反映・イベント発火） |
| 合計 | 159 件（`[Theory]` は展開後のケース数） |

```powershell
dotnet test src/Feature.LoupeCore/tests/Feature.LoupeCore.Tests.csproj
```

## 9. 設計判断（UI 非依存の境界）

本プロジェクトの型は、UI フレームワークの型をシグネチャに持たないことを境界の条件としています。

| 判断 | 内容 |
| --- | --- |
| 座標・寸法の型 | コア（`EsUtil.Platform.Core.Geometry`）の `EsPoint` / `EsSize` / `EsRect` を使い、UI 型は境界で 1 行変換する |
| 画像の型 | 拡大元画像は UI ごとに型が異なるため契約（`LoupeRenderRequest`）に含めず、描画層が解決した `EsBitmap` を別引数で受け取る |
| 文字の描画 | コアは文言・寸法・配置の基準だけを持ち、実寸 → 配置矩形の写像は描画層（`LoupeControl.UIBase` の `LoupeLabelPlan`）が、実寸測定と要素の配置は UI 層が行う（実寸は描画 API 依存のため）。色は描画層の `LoupePaint` が与える |
| 再描画のバッチ化 | UI のディスパッチャーに依存するため `LoupeControl.UIBase` が単一所有する（コアは同期計算のみ） |
| 色 | コアは色を持たない（色は描画にだけ必要な設定であり、描画層 `LoupeControl.UIBase` の `LoupePaint` が単一所有する）。色の型は UI 非依存の `System.Drawing.Color` とし、UI の色型への変換は描画ポートの境界で行う |
| 切り出し領域の左上 | 切り出し（描画層）と描画完了通知の 2 経路で重複する計算を `LoupeRenderRequest.CaptureTopLeft`（描画完了通知のために public）が単一所有する（切り出しと通知が同じ値を使う） |
| ラベルの体裁 | 「背景 = 文字の実寸 + 内側余白 × 2」「アラインの適用」「行送り」は描画にだけ必要な規則のため、描画層（`MarqueeControl.UIBase` / `LoupeControl.UIBase`）が単一所有する（コアは文言・基準点・アライン・クランプ領域だけを渡す） |
| 設定では変更しない固定値 | **公開しません**。コア内だけで使う値（中心座標ラベルの上端余白）は描画内容の解決（`Loupe.DrawContent.cs`）の private 定数とし、UI 層は解決済みの値（`LoupeTextPlacement.Point`）を受け取る |
| フォントサイズの安全域 | 6〜48 と「非数は既定値」は描画層（`LoupePaint` / `MarqueePaint`）が単一所有する |

## 10. ライセンス

もう一方のコア（`Feature.MarqueeCore`）との機能差に由来する非対称は、README の「機能差に由来する非対称（もう一方のコアとの違い）」を参照してください。

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
