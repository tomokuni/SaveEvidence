# FadeMaskDragRectControl.UIBase 外部機能仕様書

`FadeMaskDragRectControl.UIBase` は、領域選択の複合コントロール `FadeMaskDragRectControl`（Avalonia 版 / MewUI 版）が
共有する**UI 非依存の処理**を提供します。名前空間は `EsUtil.SaveEvidence.FadeMaskDragRect.UIBase`、アセンブリ名は
`FadeMaskDragRectControl.UIBase` です。

## 提供する外部機能

| 機能 | 提供元 | 内容 |
| --- | --- | --- |
| 子コントロールの値の解決 | `FadeMaskDragRectChildValues.Resolve` | 設定とモードから、内包コントロールへ適用する値（選択矩形の設定・装飾の設定・ルーペの設定・オフセット・入力の可否）を解決する |
| 付加情報ラベルの描画 | `FadeMaskHostLabelRenderer.Draw` | 操作ラベル・情報ラベルを穴あけ矩形の左上から縦に積んで描画する |

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `FadeMaskDragRectChildValues` | `public` | 任意（両 UI とホスト） |
| `FadeMaskHostLabelRenderer` | `public` | 任意（両 UI とホスト） |

本層は UI フレームワークを参照しないため、両 UI から同じ値で参照できます。

## `FadeMaskDragRectChildValues`（値の解決）

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `Marquee` | `MarqueeOptions` | 選択矩形の設定（モードから導出するハンドルの表示可否を反映済み） |
| `Deco` | `MarqueeDecoOptions` | 装飾の設定（ハイライト矩形と全面暗転の可否を反映済み） |
| `Loupe` | `LoupeOptions` | ルーペの設定 |
| `LoupeOffset` | `double` | ルーペのカーソル中心からの右下オフセット（DIP） |
| `IsHitTestVisible` | `bool` | 選択矩形コントロールが入力を受け取るか（選択矩形を使うモードのみ true） |

### `Resolve(FadeMaskDragRectOptions options, FadeMaskDragRectMode mode, EsRect? highlightRect)`

| 入力 | 内容 |
| --- | --- |
| `options` | 設定の単一値（`FadeMaskDragRectOptions`） |
| `mode` | 動作モード。未定義の値は「選択矩形を使わない」扱いになる |
| `highlightRect` | ハイライト矩形（ドメイン座標）。**実行時状態のため設定に含まれず、呼び出し側が現在値を渡す**。null はハイライトなし |

| モード | `ShowHandles` | `OuterShowFullOverlay` | `IsHitTestVisible` |
| --- | --- | --- | --- |
| `Crop` / `AreaSelect` | true | true | true |
| `ScreenSelect` / `WindowSelect` / 未定義 | false | false | false |

**設定の値は写され、モードから導出する項目だけが上書きされます**（設定に含まれないハイライト矩形は引数の値）。

**UI 層は結果をそのまま子コントロールへ代入します**（代入には UI 固有型が必要なため、本層は代入を行いません）。
本層を挟むことで、片方の UI だけ分配規則が変わる余地がなくなります。

## `FadeMaskHostLabelRenderer`（付加情報ラベルの描画）

### `Draw(IEsCanvas canvas, in MarqueeDrawContent content, FadeMaskDragRectLabelOptions labels, string? operationText, string? infoText)`

| 引数 | 制約 |
| --- | --- |
| `canvas` | 描画先の描画ポート。null は `ArgumentNullException` |
| `content` | 選択矩形コントロールが解決した描画内容（穴あけ矩形とサイズラベルの有無を参照する） |
| `labels` | 付加情報ラベルの外観（表示可否・寸法・色） |
| `operationText` | 操作ラベルの文言（**ホストが生成する**。null または空文字は描かない） |
| `infoText` | 情報ラベルの文言（**ホストが生成する**。null または空文字は描かない） |

### 描画の規則

1. **穴あけ矩形（選択またはハイライトの領域）が無い場合は何も描きません**（積み上げの基準が無いため）。
2. 積み上げの開始位置は穴あけ矩形の左上です。**サイズラベルが描かれている場合はその下から始めます**（重なりを避ける）。
3. **操作ラベル → 情報ラベルの順**に、表示可否を満たす行だけを描きます（左端は穴あけ矩形の左端に揃う）。
4. **表示可否は設定スイッチと文言の有無の両方**が成立したときだけ真です（対象外のモードでは文言が空になるため、
   スイッチだけでは判定できません）。
5. 体裁（背景 = 文字の実寸 + 内側余白）は `MarqueeHostLabelRenderer` が、体裁の値（内側余白）と行送りは `MarqueePaint`（`MarqueeControl.UIBase`）が単一所有します。

操作ラベル・情報ラベルは選択矩形コントロールの概念ではない（撮影オーバーレイ等へ組み込むホストが必要とする付加情報）
ため、**文言の生成はホストが担います**。本層が単一所有するのは「どの位置へどの順に積むか」と「描くかどうか」です。

## 依存関係

| 依存 | 用途 |
| --- | --- |
| `FadeMaskDragRectControl.Core`（`EsUtil.SaveEvidence.FadeMaskDragRect`） | 設定・動作モード・モードから導出する状態の解決 |
| `Feature.MarqueeCore` / `Feature.LoupeCore` | 内包コントロールの設定型（`MarqueeOptions` / `MarqueeDecoOptions` / `LoupeOptions` / `MarqueeDrawContent`） |
| `MarqueeControl.UIBase` | 付加情報ラベルの体裁（`MarqueeHostLabelRenderer`） |
| Common | 描画ポート（IEsCanvas） |
| Platform.Core | コア幾何型・値型 |

UI フレームワーク（Avalonia / MewUI）への依存はありません。外部パッケージへの直接依存もありません。

`FadeMaskDragRectControl.Core` の `internal` 型（`FadeMaskDragRectModes` / `FadeMaskDragRectState`）を参照するため、
同プロジェクトの `InternalsVisibleTo` に本アセンブリが含まれます。
