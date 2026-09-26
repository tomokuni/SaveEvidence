# MarqueeControl.MewUI

`MarqueeControl`（リサイズ可能なドラッグ矩形）の **MewUI 版**です。Avalonia 版と同一の UI 非依存コア（`Feature.MarqueeCore`）と描画層（`MarqueeControl.UIBase`）を使い、
入力の受付と効果の適用・カーソル制御・描画ポートの適合のみを MewUI で実装しています。層構成（Core / UIBase / UI）の全体像は [`src/README.md`](../README.md) を参照してください。

## 特徴

- **コア共有**: 選択矩形の幾何計算・状態機械・**入力の解釈（nudge セッション・確定/キャンセル・スナップ・クリック検出・最小サイズ判定）**は `Feature.MarqueeCore` が単一所有します（UI 層は公開ファサード `Marquee` だけを参照し、内部の状態機械・フレーム解決には触れません）。
  Avalonia 版と MewUI 版の挙動は同じ計算に基づきます。
- **直接描画**: 中間ビットマップを経由せず、`SkiaCanvasView` のキャンバスへ直接描画します。ハンドル・十字マーカー・外側/内側の装飾（暗転マスク・穴あけ枠・塗り・内枠）の描画手順は UI 非依存の描画層（`MarqueeControl.UIBase`）が単一所有し、キャンバスの適合は `SkiaEsCanvas`（Platform.Core）が担います。
- **色の設定（`Paint`）**: ハンドル・十字マーカー・ラベル・装飾の色は**描画にだけ必要な設定**のためコアには含めず、描画層（`MarqueeControl.UIBase` の `MarqueePaint`）が単一所有します。`Paint` プロパティで指定し（既定は `MarqueePaint.Default`）、部分変更は `MarqueePaint.Default with { ... }` で行います。**描くかどうかは色のアルファではなく `DecoOptions` の明示フラグ（`ShowOverlay` / `ShowOuterBorder` / `ShowInnerFill` / `ShowInnerBorder`）が決めます**。
- **文字は UI 層が要素として配置する**: サイズラベル（`MarqueeTextPlacement`）とカーソル座標ラベル（`MarqueeTextPlacement`）の**文言・寸法は Core が、色は描画層の `MarqueePaint` が単一所有**し、実寸から配置矩形を求める写像は描画層（`MarqueeControl.UIBase` の `MarqueeLabelPlan`）が単一所有し、UI 層が `Border` + `TextBlock` を子要素として置く（子要素は親の描画より後に描かれるため常に最上位）。実寸測定は自 UI の API（`SkiaText.Measure`）で行う。操作・情報ラベルはアプリ固有の付加情報のため、ホスト（`FadeMaskDragRectControl`）が自前で描く（外観は `FadeMaskDragRectOptions.Labels` が単一所有する）
- **DPI 追従**: `OnDpiChanged` でスケーリング係数を更新し、DIP 座標で描画します（枠線・ハンドルの画面上の大きさを保ちます）。
- **コア型のイベント引数**: イベントの座標は UI 非依存のコア型（`EsPoint` / `EsRect`）で公開します（境界変換が不要）。

## インストール

```xml
<PackageReference Include="Aprillz.MewUI.Skia" Version="0.21.1" />
<ProjectReference Include="..\MarqueeControl.MewUI\src\MarqueeControl.MewUI.csproj" />
```

MewUI は API が安定化中（0.x）のため、パッケージのバージョンは固定してください（`MewUIVersion` プロパティで一元管理）。

## 使い方

```csharp
using EsUtil.Feature.MarqueeCore.Setting;
using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.SaveEvidence.Marquee.MewUI;
using EsUtil.SaveEvidence.Marquee.UIBase;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;

// 1. 生成（カーソルサービスは省略すると Windows 既定実装になる）
var marquee = new MarqueeControl
{
    // 寸法・表示可否・対話パラメータは 1 つの設定値で渡す（同値の再設定では再描画されない）
    Options = MarqueeOptions.Default with { ShowCoordLabel = true },

    // 色は描画の設定値が単一所有する（部分変更は with 式で行う）
    Paint = MarqueePaint.Default with { OuterBorderColor = System.Drawing.Color.White },
};

// 2. 座標変換ポートを設定する（Forward = ローカル DIP → ドメイン / Bounds = ドメイン境界）
marquee.SetCoordinateTransform(new CoordTransform(dip => dip, domain => domain, () => new EsSize(1920, 1080)));

// 3. イベントを購読する
marquee.SelectionChanged += (_, e) => Console.WriteLine($"選択: {e.Rect}");
marquee.LoupePositionMoved += (_, e) => UpdateLoupe(e.Point);
marquee.SelectionCompleted += (_, e) => Capture(e.Rect);

// 3.5 ラベル（サイズ・カーソル座標）はコントロールがテキスト要素として配置する。
// ホストは描画完了イベントで付加情報（操作・情報ラベル）を描ける（図形はコントロールが描き終えている）。
marquee.Rendered += (_, e) =>
{
    // 図形（装飾・ハンドル・マーカー）はコントロールが描く。
    // 文字は Content.Layers の順に従い、UI 層が配置したテキスト要素が表示する。
};

// 4. 外部状態から選択を復元する（境界外は自動でクリップされる）
marquee.SetSelection(new EsRect(100, 100, 400, 300));
```

画像の上へ重ねる場合は、親の `Grid` に追加したうえで入力の優先順を管理します（本コントロールが入力を受け取ります）。

```csharp
grid.Add(cropView);     // 画像（下）
grid.Add(marquee);     // 選択オーバーレイ（上。入力を受け取る）
```

## 操作

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ（矩形外） | 新規選択の開始 |
| 左ドラッグ（ハンドル上） | リサイズ（角・辺の 8 方向） |
| 左ドラッグ（矩形内） | 移動 |
| ダブルクリック（矩形内） | 確定要求（`ConfirmRequested`） |
| ダブルクリック（矩形外） | キャンセル要求（`CancelRequested`） |
| 矢印キー | nudge（1 ドメインピクセル単位の微調整。カーソルを非表示にして十字マーカーを表示） |
| Enter | 確定要求（`ConfirmRequested`） |
| Escape | キャンセル要求（`CancelRequested`） |
| Ctrl + ホイール | ズーム要求（`ZoomRequested`。方向と発生位置）。解釈は組み込みの層（`MarqueeControl.UIBase`）が単一所有する |
| ホイール（Ctrl なし） | スクロール要求（`ScrollRequested`。回転量・Shift・発生位置。スクロール先はホストが決める） |

矢印キーの nudge は本コントロールにフォーカスがある場合のみ動作します（マウス押下時に自動でフォーカスを取得します）。

## Avalonia 版との違い

| 項目 | Avalonia 版 | MewUI 版 |
| --- | --- | --- |
| イベント引数の座標 | Avalonia の `Point` / `Rect` | **コア型の `EsPoint` / `EsRect`** |
| マウスダウン・アップのイベント | `MouseDown` / `MouseUp` | **持たない**（生のポインター入力はホストが自 UI のイベントで観測するため） |
| ビューポート変化 | `AttachScrollViewer` で `ScrollViewer` を自動購読 | **`NotifyViewportChanged()` をホストが呼ぶ** |
| nudge のキー入力 | フォーカス非依存（`TopLevel` を購読） | フォーカス依存（本コントロールにフォーカスがある場合のみ） |
| ログ | `ILogger` を注入して出力 | `Log` プロパティで同様に出力する（書式・レベルは同一） |
| 描画 | 中間ビットマップ経由（`AvaloniaCanvas`） | キャンバスへ直接描画（`SkiaEsCanvas`） |
| 文字（ラベル・座標ラベル）の表示 | UI 層が配置したテキスト要素（`Border` + `TextBlock`） | 同左（図形は `Rendered` の `Content.Layers` の順にコントロールが描く） |

## 主なプロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Options` | `MarqueeOptions` | `MarqueeOptions.Default` | 選択矩形の寸法・表示可否・対話パラメータをまとめた設定（コアの `EsUtil.Feature.MarqueeCore.Setting.MarqueeOptions`。**色は含まない**） |
| `Paint` | `MarqueePaint` | `MarqueePaint.Default` | **色（ハンドル・十字マーカー・ラベル・装飾）の設定**（描画層の `EsUtil.SaveEvidence.Marquee.UIBase.MarqueePaint`）。変更すると描画実装が新しい色で差し替わる |
| `DecoOptions` | `MarqueeDecoOptions` | `MarqueeDecoOptions.Default` | 装飾（暗転マスク・穴あけ枠・ハイライト）の表示可否と寸法の設定（`EsUtil.Feature.MarqueeCore.Decoration.MarqueeDecoOptions`。**色は含まない**） |
| `DipToDeviceScaling` | `double` | `1.0` | DIP → 物理ピクセルの倍率（DPI 変更で自動更新） |

`MarqueeOptions` の項目（既定値はコアが単一所有。Avalonia 版と同一）:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `ShowHandles` | `bool` | `true` | ドラッグハンドルの表示可否 |
| `HandleSize` | `double` | `10.0` | ハンドルの一辺サイズ（DIP） |
| `HandleHitRadius` | `double` | `12.0` | ハンドルのヒットテスト半径（DIP） |
| `MinSelectionSize` | `double` | `6.0` | 選択矩形の最小サイズ（ドメイン座標） |
| `ClampToDomainBounds` | `bool` | `true` | ドメイン境界外の選択を禁止する |
| `MarkerLength` | `double` | `16.0` | 十字マーカーの全長（DIP） |
| `ShowCoordLabel` | `bool` | `false` | カーソル座標ラベルの表示 |

色とラベルのフォントサイズは描画の設定値 `MarqueePaint`（描画層）が単一所有します。

`MarqueePaint` の項目（既定値は本型が単一所有する）:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `HandleColor` / `HandleBorderColor` | `System.Drawing.Color` | 白 / `#FFA9A9A9` | ハンドルの塗り色と枠線色（全成分 0 の塗り色は白へ解決される） |
| `MarkerColor` | `System.Drawing.Color` | 白 | 十字マーカーの色 |
| `SizeLabelColor` / `SizeLabelBackgroundColor` | `System.Drawing.Color` | 白 / 黒 60% | サイズラベルの文字色と背景色 |
| `CoordLabelColor` / `CoordLabelBackgroundColor` | `System.Drawing.Color` | 白 / 黒 60% | ラベルの文字色と背景色 |
| `OverlayColor` / `OuterBorderColor` | `System.Drawing.Color` | 黒 50% / `#FFA9A9A9` | 暗転マスクと穴あけ枠の色 |
| `InnerFillColor` / `InnerBorderColor` | `System.Drawing.Color` | 透明 / 透明 | 穴あけ矩形の内側の塗り色と枠線色 |

色は UI 非依存の `System.Drawing.Color` で指定します。設定の一部だけを変える場合は `MarqueeOptions.Default with { ... }` / `MarqueeDecoOptions.Default with { ... }` / `MarqueePaint.Default with { ... }` で指定します（指定しない項目は既定値を保つ）。

## 主なメソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordinateTransform(ICoordTransform?)` | ドメイン ↔ ローカル DIP の変換ポートを設定する（null は等価変換） |
| `SetSelection(EsRect)` | 選択矩形を復元する（`Options.ClampToDomainBounds` 有効時は境界内へクリップ） |
| `ResetSelection()` | 選択状態と nudge 状態を初期化する |
| `RequestConfirm()` / `RequestCancel()` | Enter / Escape と同じ条件で確定・キャンセルを要求する |
| `NotifyViewportChanged()` | ビューポートの変化を通知する（現在の状態の再通知と再描画を要求。通知の組み立てはコントロール化の層の `MarqueeViewportNotifier` が担う） |
| `CurrentSnapshot` | 現在の状態のスナップショット（`MarqueeSnapshot`） |

## イベント

`SelectionStarted` / `SelectionChanged` / `SelectionCompleted` / `SelectionCancelled` /
`LoupePositionMoved` / `ViewportChanged` / `ZoomRequested` / `ScrollRequested` /
`ConfirmRequested` / `CancelRequested` / `Rendered`

**生のポインター入力（Mouse 系）の公開イベントは持ちません**（ホストは自 UI の入力イベントを使います。押下状態は `CurrentSnapshot.IsMouseDown`）。

`Rendered` は描画完了の通知です（描画のたびに発火し、引数の `Content` に装飾・ハンドル・マーカー・ラベルと層の順序が含まれます）。コントロールの描画中に発火するため、ハンドラ内で描画を完了させます。

## テスト

```powershell
dotnet test src/MarqueeControl.MewUI/tests/MarqueeControl.MewUI.Tests.csproj
```

- 描画（ハンドル・十字マーカー・外側/内側の装飾）の画素検証は UI 非依存の描画層（`MarqueeControl.UIBase`）のテストが担うため、本プロジェクトは描画のテストを持たない。
- ラベル要素の配置（実寸 → 配置矩形）の検証は描画層の `MarqueeLabelPlan.Tests.cs` が担う。UI 層の要素配置は `MarqueeControl.Tests.cs` が検証する。
- `MarqueeControl.Tests.cs`: 依存プロパティの既定値・補正、`CurrentSnapshot`、選択の復元・境界クリップ、確定・キャンセル、ビューポート通知、`Log` のフォールバックと設定変更ログ。
- ログの定義（操作ログ・設定変更）はコアの `MarqueeLogMessage` にあり、その検証はコアのテストが行います（ズーム要求のログは組み込みの層の `MarqueeWheelLogMessage` が所有し、`MarqueeControl.UIBase.Tests` が検証します）。

入力の解釈（nudge セッション・確定/キャンセル・スナップ・クリック検出・最小サイズ判定）はコアが担うため、その検証は `Feature.MarqueeCore.Tests` が行います。

## 操作ログ

`Log` プロパティへ `ILogger` を設定すると、操作ログを出力します（既定は出力なし）。書式・レベルは Avalonia 版と同一です。  
ログはコア（`EsUtil.Feature.MarqueeCore.Logging.MarqueeLogMessage`）と組み込みの層（`EsUtil.SaveEvidence.Marquee.UIBase.MarqueeWheelLogMessage`）が定義し、ロガーはファサード（`Marquee.Log`）が保持します。本プロジェクトはログ定義を持ちません。

| レベル | 出力内容 |
| --- | --- |
| Debug | MouseDown / MouseUp / DoubleClick / DragStart / KeyDown / ConfirmRequested / CancelRequested / ZoomRequested / 設定変更 |
| Trace | MouseDown 直前の位置 / MouseUp 直後の位置 |

定義はコア（`EsUtil.Feature.MarqueeCore.Logging.MarqueeLogMessage`）と組み込みの層（`EsUtil.SaveEvidence.Marquee.UIBase.MarqueeWheelLogMessage`）が持ち、本プロジェクトは定義を持ちません。

## 未対応

- `ScrollViewer` の自動連携（ホストが `NotifyViewportChanged()` を呼ぶ）。
- フォーカス非依存の矢印キー nudge。
  - 文字（ラベル・座標ラベル）の要素配置（UI 層の責務。配置矩形は描画層 `MarqueeControl.UIBase` の `MarqueeLabelPlan` が求める）。

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
