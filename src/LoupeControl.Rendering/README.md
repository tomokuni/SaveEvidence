# LoupeControl.Rendering

`LoupeControl`（拡大鏡＝カーソル位置の周囲を拡大表示するコントロール）の**UI 非依存の描画層**です。Avalonia 版と MewUI 版が同じ描画手順を共有します。

## 目的

- **「どう描くか」を 1 箇所に集約する**。コア（`Feature.LoupeCore`）は「何を描くか（判断とデータ）」だけを決め、描画 API の呼び出し手順は本層が単一所有する。
- **描画を差し替えられる**。描画先は `IEsCanvas`（`Platform.Core`、UI 非依存の描画ポート）であり、描画手順は `ILoupeRenderer`（`Feature.LoupeCore` の契約）を実装した型で置き換えられる（ファサードの `Loupe.Renderer` へ設定する）。UI の追加・描画方式の変更はポートまたは実装の差し替えだけで済む。
- **UI 実装を薄くする**。UI 層は描画内容の解決と描画先（描画ポート）の用意だけを行い、描画 API の呼び出し手順は本層が持つ。
- **UI 初期化なしで描画を検証できる**。描画の実行は Skia（`SkiaEsCanvas`）で行うため、ウィンドウなしの画素テストが書ける（`tests/`）。
- UI フレームワークへ依存しない（参照は `Feature.LoupeCore` と `Platform.Core` のみ）。

## 層の分担

層の全体像（Core / Rendering / CoreForControl / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（LoupeControl.AvaloniaUI / LoupeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    ├──> LoupeControl.Rendering ← 本プロジェクト   どう描くか（描画 API の呼び出し手順。ILoupeRenderer を実装する）
    ├──> LoupeControl.CoreForControl   組み込み先がある場合にだけ必要な規則
    └──> Feature.LoupeCore   何を描くか + 判断（状態・値・純粋計算・コア⇔UI の契約）
           └──> Platform.Core（Es* 値型・ICoordTransform・IEsCanvas・EsLabelLayout）
※ Rendering と CoreForControl の間に依存はない（兄弟）。Core は Rendering / CoreForControl を参照しない。
```

**帰属の判定**: 「何を描くか（判断・データ）」はコア、「どう描くか（API 呼び出し）」は本層。

## 収録する型

| 型 | 責務 |
| --- | --- |
| `LoupeRenderer` | 描画入力（`LoupeRenderRequest`）を描画ポートへ写す（**描画手順の単一所有者**。`ILoupeRenderer` の既定実装で、`Default` のインスタンスを共有する）。`DrawShapes`（下地・市松・拡大画像・十字線・外枠。**ラベルの層は描かない**）/ `ResolveLabels`（要素の配置指示） |
| `LoupeLabelPlan` | コアの通知（`LoupeRenderNotification`）からラベル要素（`EsLabelElement`）の配置指示を解決する |

## 描画の単位

**物理ピクセル**です。`LoupeRenderRequest.PixelSize` が作る正方形が描画面になります。DIP → 物理ピクセルの換算（`LoupeAppearanceScaler` / `LoupePositionCalculator`）はコアが済ませているため、本層は換算しません。中心座標ラベルだけは DIP で配置します（コアが `EsLabelLayout` で配置を決める）。

## ラベルは UI の要素が描く

| 対象 | 誰が描くか | 呼ぶ API |
| --- | --- | --- |
| 図形（下地・市松模様・拡大画像・十字線・外枠） | 本層 | `DrawShapes`（**ラベルの層は描かない**） |
| ラベル（中心座標） | UI の要素（`Border` + `TextBlock` 等） | `ResolveLabels`（`Loupe.ResolveLabels` が解決したラベル契約を渡す。既定実装は `LoupeLabelPlan.Resolve` へ委譲）の結果を要素へ写す（子要素は親より後に描かれるため**ラベルは常に最上位**） |

本層はラベルをキャンバスへ描く経路を持ちません（`Draw` / `DrawLabels` はありません）。

## 描画の差し替え

| 差し替えたいもの | 方法 |
| --- | --- |
| **描画先（UI フレームワーク）** | `IEsCanvas` を実装する（例: `SkiaEsCanvas`（Skia）/ `AvaloniaCanvas`（Avalonia の `DrawingContext`））。**描画手順はそのまま使える** |
| **描画手順そのもの** | `ILoupeRenderer`（`Feature.LoupeCore`）を実装した型を作り、ファサードの `Loupe.Renderer` へ設定する（本層の `LoupeRenderer` を使わなくてもよい） |
| **色・寸法・拡大率** | `LoupeOptions`（設定）と `LoupeAppearance`（外観）を変える（描画層は値を保持しない） |

## 独自のコントロールへ組み込む（最小実装）

ルーペ（拡大鏡）を独自コントロールとして実装する場合、**状態の更新**と**描画**から作れます。

```csharp
// 1. 生成（設定は 1 値。表示サイズ・拡大率・外観をここへまとめる。第 1 引数は描画の実装）
private readonly Loupe _loupe = new(LoupeRenderer.Default);
private EsBitmap? _source;   // 拡大元の画像（本 UI が解決して渡す）

// 2. 状態の更新（マウス移動などで拡大中心を設定する）
public void SetCenter(EsPoint domainPoint)
{
    _loupe.CenterPoint = domainPoint;
    InvalidateVisual();
}

// 3. 描画（描画入力の生成はコア、描画の手順は LoupeRenderer）
protected override void Render(DrawingContext context)
{
    var canvas = new AvaloniaCanvas(context);
    var request = _loupe.CreateRequest(layout.DisplaySize);  // DIP → 物理 px と描画先のクランプ
    var notification = _loupe.ResolveNotification(request); // 切り出し領域・スケール・層の順序
    _loupe.Renderer.DrawShapes(canvas, _source, request, notification);
}
```

- ラベルを要素として配置する UI は 3 で `DrawShapes` を呼び、`ResolveLabels`（既定実装は `LoupeLabelPlan.Resolve`）の結果を要素へ写す。- 表示位置（左上）が必要なら `Loupe.ResolveLayout(parentSize)` を使う（親領域へ収めるクランプはコアが単一所有する）。
- 拡大元画像は UI ごとに型が異なるため契約（`LoupeRenderRequest`）に含めません。UI が `EsBitmap` を解決して `DrawShapes` へ渡します。
- 再描画のバッチ化（描画要求の合流・世代番号）が必要なら `LoupeControl.CoreForControl` の `LoupeRenderCoordinator` を使う。

## 依存関係

```text
LoupeControl.Rendering
    ├── Feature.LoupeCore（描画入力・通知・外観・切り出しの計算）
    └── Platform.Core（IEsCanvas・Es* 値型・SkiaEsCanvas・EsLabelLayout）
```

**UI フレームワークへ依存しません**。UI 固有の描画 API は `IEsCanvas` の実装が担います。

名前空間は `Estable.Loupe.Rendering`。アセンブリ名は `LoupeControl.Rendering`。

## テスト

`tests/` が本プロジェクトのテストを持ちます。描画は UI 非依存の描画ポート（Skia）で行うため、**UI 初期化なしで画素を検証**できます（合計 21 件）。

```text
tests/
├── LoupeRenderer.Tests.cs             # 層ごとの描画（下地・市松・拡大画像・十字線・外枠・層の選択・引数の検証）（16 件）
├── LoupeLabelPlan.Tests.cs            # 実寸 → ラベル要素の写像（5 件）
└── TestHelper.cs                      # テスト用の描画先（Skia）
```

```powershell
dotnet test src/LoupeControl.Rendering/tests/LoupeControl.Rendering.Tests.csproj
```
