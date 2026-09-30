# FadeMaskDragRectControl.UIBase 外部機能仕様書

`FadeMaskDragRectControl.UIBase` は、領域選択の複合コントロール `FadeMaskDragRectControl`（Avalonia 版 / MewUI 版）が
共有する**UI 非依存の処理**を提供します。 名前空間は `EsUtil.SaveEvidence.FadeMaskDragRect.UIBase`、アセンブリ名は
`EsUtil.SaveEvidence.FadeMaskDragRect.UIBase` です。

## 提供する外部機能

| 機能 | 提供元 | 内容 |
| --- | --- | --- |
| 子コントロールの値の解決 | `FadeMaskDragRectChildValues.Resolve` | 設定とモードから、内包コントロールへ適用する値（選択矩形の設定・装飾の設定・ルーペの設定・オフセット・入力の可否）を解決する |
| 付加情報ラベルの配置指示 | `FadeMaskHostLabelRenderer.Resolve` | 操作ラベル・情報ラベルを注目矩形の内側（**矩形の線幅 + 1 DIP**）から縦に積む配置指示（描画順）を返す |
| ラベル 1 行の体裁の適用 | `MarqueeHostLabelRenderer.Resolve` | 基準点・文言・色・内側余白から、背景と文字の配置指示（背景 = 文字の実寸 + 内側余白 × 2）を返す |

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `FadeMaskDragRectChildValues` | `public` | 任意（両 UI とホスト） |
| `FadeMaskHostLabelRenderer` | `public` | 任意（両 UI とホスト） |
| `MarqueeHostLabelRenderer` | `public` | 任意（本層の `FadeMaskHostLabelRenderer`。 他の複合コントロールも再利用できる） |

本層は UI フレームワークを参照しないため、両 UI から同じ値で参照できます。

## `FadeMaskDragRectChildValues`（値の解決）

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `Marquee` | `MarqueeOptions` | 選択矩形の設定（モードから導出するハンドルの表示可否を反映済み） |
| `MarqueeAppearance` | `MarqueeAppearance` | 選択矩形の外観（ホストが与えた値を保ち、モードから導出する項目（ハンドルの表示可否・全面暗転の可否）だけを上書きする） |
| `Loupe` | `LoupeOptions` | ルーペの設定 |
| `LoupeOffset` | `double` | ルーペのカーソル中心からの右下オフセット（DIP） |
| `IsHitTestVisible` | `bool` | 選択矩形コントロールが入力を受け取るか（選択矩形を使うモードのみ true） |

### `Resolve(FadeMaskDragRectOptions options, FadeMaskDragRectMode mode, EsRect? highlightDomainRect)`

| 入力 | 内容 |
| --- | --- |
| `options` | 設定の単一値（`FadeMaskDragRectOptions`） |
| `mode` | 動作モード。 未定義の値は「選択矩形を使わない」扱いになる |
| `highlightDomainRect` | ハイライト矩形（ドメイン座標）。 **実行時状態のため設定に含まれず、呼び出し側が現在値を渡す**。 null はハイライトなし |

| モード | `MarqueeAppearance.ShowHandles` | `MarqueeAppearance.ShowFullOverlay` | `IsHitTestVisible` |
| --- | --- | --- | --- |
| `Crop` / `AreaSelect` | true | true | true |
| `ScreenSelect` / `WindowSelect` / 未定義 | false | false | false |

**設定の値は写され、モードから導出する項目だけが上書きされます**（設定に含まれないハイライト矩形は引数の値）。

**UI 層は結果をそのまま子コントロールへ代入します**（代入には UI 固有型が必要なため、本層は代入を行いません）。
本層を挟むことで、片方の UI だけ分配規則が変わる余地がなくなります。

## `FadeMaskHostLabelRenderer`（付加情報ラベルの配置指示）

### `Resolve(in MarqueeDrawPlan plan, FadeMaskDragRectLabelOptions labels, MarqueeAppearance appearance, string? operationText, string? infoText, Func<string, double, EsSize> measureText)`

| 引数 | 制約 |
| --- | --- |
| `plan` | 選択矩形コントロールが解決した描画計画（注目矩形とサイズラベルの表示可否を参照する） |
| `labels` | 付加情報ラベルの外観（表示可否・寸法・色） |
| `appearance` | 選択矩形コントロールの外観（null 不可。 サイズラベルのフォントサイズを行送りに使う） |
| `operationText` | 操作ラベルの文言（**ホストが生成する**。 null または空文字は返さない） |
| `infoText` | 情報ラベルの文言（**ホストが生成する**。 null または空文字は返さない） |
| `measureText` | テキストの実寸を測る関数（null 不可） |

戻り値は `IReadOnlyList<MarqueeLabelPlacement>`（`MarqueeLabelPlacement` は `Feature.Marquee.Presenter`）。

### 配置の規則

1. **注目矩形（選択またはハイライトの領域）が無い場合は空のリストを返します**（積み上げの基準が無いため）。
2. 積み上げの開始位置は注目矩形の左上から「矩形の線幅 + 1 DIP」だけ内側へ寄せた点（`MarqueeDrawPlan.LabelAnchor`）です。 **サイズラベルが描かれている場合はその下から始めます**（重なりを避ける）。
3. **操作ラベル → 情報ラベルの順**に、表示可否を満たす行だけを返します（左端は注目矩形の左端に揃う）。
4. **表示可否は設定スイッチと文言の有無の両方**が成立したときだけ真です（対象外のモードでは文言が空になるため、
   スイッチだけでは判定できません）。
5. 体裁（背景 = 文字の実寸 + 内側余白）は `MarqueeHostLabelRenderer` が、体裁の値（内側余白）は `MarqueeAppearance`（`Feature.Marquee.Presenter`）が単一所有します。

操作ラベル・情報ラベルは選択矩形コントロールの概念ではない（撮影オーバーレイ等へ組み込むホストが必要とする付加情報）
ため、**文言の生成はホストが担います**。 本層が単一所有するのは「どの位置へどの順に積むか」と「返すかどうか」です。

**本クラスは描画しません**（UI フレームワークごとの描画モデルの差は抽象では吸収しきれないため、UI が自 UI の API で描きます）。

## `MarqueeHostLabelRenderer`（ラベル 1 行の体裁）

### `Resolve(EsPoint anchor, string text, double fontSize, DrawingColor textColor, DrawingColor backgroundColor, double padding, Func<string, double, EsSize> measureText)`

| 引数 | 制約 |
| --- | --- |
| `anchor` | ラベルの基準点（背景の左上に置く座標） |
| `text` | 文言（null 不可） |
| `fontSize` | フォントサイズ（DIP） |
| `textColor` / `backgroundColor` | 文字色・背景色（背景の不透明度はアルファ） |
| `padding` | 内側余白（DIP。 サイズラベルと同じ値（`MarqueeAppearance.LabelPadding`）を渡す） |
| `measureText` | テキストの実寸を測る関数（null 不可） |

戻り値は `MarqueeLabelPlacement`。 背景は文字の実寸へ内側余白を四方へ加えた大きさになり、文字は同じ余白だけ内側へ入ります。
角丸は持たず（要素へ写す UI は `CornerRadius = 0` にする）、縦積みの行送りは「前の行の高さ + 内側余白 × 2」です。

## 依存関係

| 依存 | 用途 |
| --- | --- |
| `FadeMaskDragRectControl.Core`（`EsUtil.SaveEvidence.FadeMaskDragRect`） | 設定・動作モード・モードから導出する状態の解決 |
| `Feature.Marquee.Core` / `Feature.Loupe.Core` / `Feature.Marquee.Presenter` | 内包コントロールの設定型（`MarqueeOptions` / `LoupeOptions`）と外観（`MarqueeAppearance`）・描画計画（`MarqueeDrawPlan`） |
| `Platform.Core` | コア幾何型・値型 |

UI フレームワーク（Avalonia / MewUI）への依存はありません。 外部パッケージへの直接依存もありません。

`FadeMaskDragRectControl.Core` の `internal` 型（`FadeMaskDragRectModes` / `FadeMaskDragRectState`）を参照するため、
同プロジェクトの `InternalsVisibleTo` に本アセンブリが含まれます。
