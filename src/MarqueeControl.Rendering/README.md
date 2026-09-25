# MarqueeControl.Rendering

`MarqueeControl`（マーキー選択＝ドラッグで矩形を選び、移動・リサイズできるコントロール）の**UI 非依存の描画層**です。Avalonia 版と MewUI 版が同じ描画手順を共有します。

## 目的

- **「どう描くか」を 1 箇所に集約する**。コア（`MarqueeControl.Core`）は「何を描くか（判断とデータ）」だけを決め、描画 API の呼び出し手順は本層が単一所有する。
- **描画を差し替えられる**。描画先は `IEsCanvas`（`Platform.Core`、UI 非依存の描画ポート）であり、描画手順は `IMarqueeRenderer`（`MarqueeControl.Core` の契約）を実装した型で置き換えられる（ファサードの `Marquee.Renderer` へ設定する）。UI の追加・描画方式の変更はポートまたは実装の差し替えだけで済む。
- **UI 実装を薄くする**。UI 層は描画内容の解決と描画先（描画ポート）の用意だけを行い、描画 API の呼び出し手順は本層が持つ。
- **UI 初期化なしで描画を検証できる**。描画の実行は Skia（`SkiaEsCanvas`）で行うため、ウィンドウなしの画素テストが書ける（`tests/`）。
- UI フレームワークへ依存しない（参照は `MarqueeControl.Core` と `Platform.Core` のみ）。

## 層の分担

層の全体像（Core / Rendering / CoreForControl / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（MarqueeControl.AvaloniaUI / MarqueeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    ├──> MarqueeControl.Rendering ← 本プロジェクト   どう描くか（描画 API の呼び出し手順。IMarqueeRenderer を実装する）
    ├──> MarqueeControl.CoreForControl   組み込み先がある場合にだけ必要な規則
    └──> MarqueeControl.Core   何を描くか + 判断（状態・値・純粋計算・コア⇔UI の契約）
           └──> Platform.Core（Es* 値型・ICoordTransform・ICursorService・IEsCanvas・EsLabelLayout）
※ Rendering と CoreForControl の間に依存はない（兄弟）。Core は Rendering / CoreForControl を参照しない。
```

**帰属の判定**: 「何を描くか（判断・データ）」はコア、「どう描くか（API 呼び出し）」は本層。`MarqueeFrame`（描くかどうかの判断）はコアに残る。

## 収録する型

| 型 | 責務 |
| --- | --- |
| `MarqueeRenderer` | 描画内容（`MarqueeDrawContent`）を描画ポートへ写す（**描画手順の単一所有者**。`IMarqueeRenderer` の既定実装で、`Default` のインスタンスを共有する）。`DrawShapes`（図形の層のみ。ラベルの層は描かない）/ `ResolveLabels`（要素の配置指示） |
| `MarqueeOuterRenderer` | 選択矩形の外側の装飾（暗転マスク・穴あけ枠）を描く |
| `MarqueeInnerRenderer` | 穴あけ矩形の内側の装飾（塗り・内枠）を描く |
| `MarqueeLabelPlan` | コアの描画内容からラベル要素（`EsLabelElement`）の配置指示を解決する（実寸 → 配置の写像） |
| `MarqueeHostLabelRenderer` | ホストが描く付加情報ラベル（操作・情報）の体裁を適用して描く（文言はホストが与える） |

## ラベルは UI の要素が描く

| 対象 | 誰が描くか | 呼ぶ API |
| --- | --- | --- |
| 図形（装飾・ハンドル・始点マーカー） | 本層 | `DrawShapes`（**ラベルの層は描かない**） |
| ラベル（サイズ・カーソル座標） | UI の要素（`Border` + `TextBlock` 等） | `ResolveLabels`（配置指示。既定実装は `MarqueeLabelPlan.Resolve` へ委譲）の結果を要素へ写す（子要素は親より後に描かれるため**ラベルは常に最上位**） |

本層はラベルをキャンバスへ描く経路を持ちません（`Draw` / `DrawLabels` はありません）。体裁（背景 = 文字の実寸 + 内側余白 × 2、文字原点、行送り）はコア（`EsLabelLayout` / `EsLabelElement`）が単一所有します。

## 描画の差し替え

| 差し替えたいもの | 方法 |
| --- | --- |
| **描画先（UI フレームワーク）** | `IEsCanvas` を実装する（例: `SkiaEsCanvas`（Skia）/ `AvaloniaCanvas`（Avalonia の `DrawingContext`））。**描画手順はそのまま使える** |
| **描画手順そのもの** | `IMarqueeRenderer`（`MarqueeControl.Core`）を実装した型を作り、ファサードの `Marquee.Renderer` へ設定する（本層の `MarqueeRenderer` を使わなくてもよい） |
| **色・寸法** | `MarqueeOptions`（設定）と `MarqueeDecoOptions`（装飾）を変える（描画層は値を保持しない） |

## 独自のコントロールへ組み込む（最小実装）

マーキー選択だけを持つ独自コントロールは、**入力の転送**・**効果の受領**・**描画**の 3 点を書けば作れる。

```csharp
// 1. 効果（本 UI が必要とするものだけ。MarqueeEffectsBase が他の効果の空実装を提供する）
private sealed class Effects(MyControl owner) : MarqueeEffectsBase
{
    public override void RequestRedraw() => owner.InvalidateVisual();
}

// 2. 生成（カーソル操作が不要なら cursor は null でよい。第 1 引数は描画の実装）
private readonly Marquee _marquee = new(MarqueeRenderer.Default, new Effects(this));

// 3. 入力の転送（座標はドメイン座標へ変換して渡す）
protected override void OnPointerPressed(PointerPressedEventArgs e)
    => _marquee.Press(e.GetPosition(this).ToDomain(), e.ClickCount);

// 4. 描画（描画内容の解決はコア、描画の手順は描画実装）
protected override void Render(DrawingContext context)
{
    var size = Bounds.Size.ToEsSize();
    _marquee.Renderer.DrawShapes(new AvaloniaCanvas(context), _marquee.ResolveContent(size));
}
```

- ラベルを要素として配置する UI は 4 で `DrawShapes` を呼び、`MarqueeLabelPlan.Resolve` の結果を要素へ写す。
- ホスト固有の付加情報ラベルは `MarqueeHostLabelRenderer.Draw` で体裁を揃えて描く。
- 座標変換（ドメイン ⇔ DIP）は `Marquee.SetCoordinateTransform` へ注入する（未指定は等倍）。
- ホイールの解釈（Ctrl = ズーム / 通常 = スクロール）が必要なら `MarqueeControl.CoreForControl` の `MarqueeWheelInterpreter` を使う。

## 依存関係

```text
MarqueeControl.Rendering
    ├── MarqueeControl.Core（描画内容の契約・選択の判断）
    └── Platform.Core（IEsCanvas・Es* 値型・SkiaEsCanvas・EsLabelLayout）
```

**UI フレームワークへ依存しません**（Avalonia / MewUI の型を参照しない）。UI 固有の描画 API は `IEsCanvas` の実装（各 UI プロジェクト・プラットフォーム層）が担う。

名前空間は `Estable.Marquee.Rendering`。アセンブリ名は `MarqueeControl.Rendering`。

## テスト

`tests/` が本プロジェクトのテストを持ちます。描画は UI 非依存の描画ポート（Skia）で行うため、**UI 初期化なしで画素を検証**できます（合計 26 件）。

```text
tests/
├── MarqueeRenderer.Tests.cs           # 図形の描画（ハンドル 8 方向・枠線・十字線・装飾・層の選択・引数の検証）（12 件）
├── MarqueeLabelPlan.Tests.cs          # 実寸 → ラベル要素の写像（順序・配置・色）（9 件）
├── MarqueeHostLabelRenderer.Tests.cs  # 付加情報ラベルの体裁（背景・文字の順序・余白）（5 件）
└── TestHelper.cs                      # テスト用の描画先（Skia）
```

```powershell
dotnet test src/MarqueeControl.Rendering/tests/MarqueeControl.Rendering.Tests.csproj
```
