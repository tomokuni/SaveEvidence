# LoupeControl.UIBase

`LoupeControl`（拡大鏡＝カーソル位置の周囲を拡大表示するコントロール）を**コントロールとして成り立たせる UI 非依存の層**です。Avalonia 版と MewUI 版が同じ処理を共有します。

本層は 2 つの関心事を持ちます。どちらも「単体で使う `Loupe` だけでは決まらない」ため、コア（`Feature.LoupeCore`）には置きません。

| 関心事 | 内容 | 代表型 |
| --- | --- | --- |
| **描画手順** | 「どう描くか」（描画 API の呼び出し手順＝層の順序と写し方） | `LoupeRenderer` / `LoupeLabelPlan` |
| **組み込み規則** | 「他のコントロールへ組み込むときにだけ必要な規則」（再描画要求のバッチ化と世代番号管理） | `LoupeRenderCoordinator` |

## 目的

- **「どう描くか」を 1 箇所に集約する**。コア（`Feature.LoupeCore`）は「何を描くか（判断とデータ）」だけを決め、描画 API の呼び出し手順は本層が単一所有する。
- **描画を差し替えられる**。描画先は `IEsCanvas`（`Platform.Core`、UI 非依存の描画ポート）であり、描画手順は `ILoupeRenderer`（`Feature.LoupeCore` の契約）を実装した型で置き換えられる（ファサードの `Loupe.Renderer` へ設定する）。UI の追加・描画方式の変更はポートまたは実装の差し替えだけで済む。
- **非同期の再描画を 1 フレーム 1 回へ結合する**。中間ビットマップを持つ UI（Avalonia 版）は「要求 → 実行キュー → 非同期生成 → 表示反映」の順に動くため、要求の合流と古い結果の破棄が必要になる。この手順を UI ごとに書かせない。
- **UI 実装を薄くする**。UI 層は描画内容の解決・描画先（描画ポート）の用意・実行キューの提供だけを行い、描画 API の呼び出し手順と再描画のスケジューリングは本層が持つ。
- **UI 初期化なしで検証できる**。描画の実行は Skia（`SkiaEsCanvas`）で行うため、ウィンドウなしの画素テストが書ける（`tests/`）。再描画のスケジューリングも UI 型を持たないため `[Fact]` で検証できる。
- UI フレームワークへ依存しない（参照は `Feature.LoupeCore` と `Platform.Core` のみ。実行キューは `post` コールバックとして UI が注入する）。

## 層の分担

層の全体像（Core / UIBase / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（LoupeControl.AvaloniaUI / LoupeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    ├──> LoupeControl.UIBase ← 本プロジェクト   どう描くか（描画手順）＋ 組み込み先があるときだけ必要な規則
    └──> Feature.LoupeCore   何を描くか + 判断（状態・値・純粋計算・コア⇔UI の契約）
           └──> Platform.Core（Es* 値型・ICoordTransform・IEsCanvas・EsLabelLayout）
※ コアは本層を参照しない（本層の描画実装はコアの契約 ILoupeRenderer を実装する）。
※ 本層を使うのは LoupeControl.AvaloniaUI のみ（MewUI は中間ビットマップを持たず同期描画するため再描画のスケジューリングが不要）。
```

**帰属の判定**: 「何を描くか（判断・データ）」はコア、「どう描くか（API 呼び出し）」は本層。

## 収録する型

### 描画手順

| 型 | 責務 |
| --- | --- |
| `LoupeRenderer` | 描画入力（`LoupeRenderRequest`）を描画ポートへ写す（**描画手順の単一所有者**。`ILoupeRenderer` の既定実装で、`Default` のインスタンスを共有する）。`DrawShapes`（下地・市松・拡大画像・十字線・外枠。**ラベルの層は描かない**）/ `ResolveLabels`（要素の配置指示） |
| `LoupeLabelPlan` | コアの通知（`LoupeRenderNotification`）からラベル要素（`EsLabelElement`）の配置指示を解決する |

### 組み込み規則

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `LoupeRenderCoordinator` | public | 再描画要求のバッチ化（1 フレームに 1 回）と世代番号管理。表示状態・ビットマップ解放・描画開始・実行キューへのポストをコールバックで受け取る |

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

## Core との境界（なぜスケジューリングを Core に置かないか）

| 関心事 | 所有者 | 理由 |
| --- | --- | --- |
| 表示レイアウト・描画入力・通知内容の解決（ルーペの見た目と計算） | `Feature.LoupeCore` | 単体で使っても同じ結果になる（独立した部品の本体） |
| 再描画要求のバッチ化・世代番号管理 | 本層 | **非同期の再描画をいつ何回行うか**は、UI へ組み込んで初めて問題になる（単体のルーペには「再描画のスケジューリング」という概念が無い） |
| 実行キューと優先度の選択（`DispatcherPriority.Render` 等） | UI 層 | フレームワーク固有の API である |
| 描画の実行（中間ビットマップ / キャンバス直描画） | UI 層 | 描画方式（中間バッファの有無）は UI ごとに異なる |

## UI ごとの利用状況

| 型 | AvaloniaUI | MewUI | 補足 |
| --- | --- | --- | --- |
| `LoupeRenderer` / `LoupeLabelPlan` | 使う | 使う | 描画手順は両 UI で共通 |
| `LoupeRenderCoordinator` | 使う（中間ビットマップを非同期生成し `Image.Source` へ反映） | **使わない** | MewUI は `OnPaintSurface` の中で同期描画するため、要求の合流と世代番号管理が不要 |

**UI フレームワークによって未使用になる型が存在します**（`LoupeRenderCoordinator` は中間ビットマップを持つ UI だけが使う）。これは「抽象として複数の UI で共用できる処理を本層へ置き、必要とする UI だけが実装・利用する」設計の帰結であり、コア（`Feature.LoupeCore`）の公開面には影響しません。同じ必要が生じた時点で MewUI にも注入できます。

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

- ラベルを要素として配置する UI は 3 で `DrawShapes` を呼び、`ResolveLabels`（既定実装は `LoupeLabelPlan.Resolve`）の結果を要素へ写す。
- 表示位置（左上）が必要なら `Loupe.ResolveLayout(parentSize)` を使う（親領域へ収めるクランプはコアが単一所有する）。
- 拡大元画像は UI ごとに型が異なるため契約（`LoupeRenderRequest`）に含めません。UI が `EsBitmap` を解決して `DrawShapes` へ渡します。

再描画のバッチ化が必要な場合（中間ビットマップを非同期生成する UI）は次のように使います。

```csharp
// UI 層（コントロール）: 実行キューと優先度を注入して生成する
_coordinator = new LoupeRenderCoordinator(
    isVisible: () => IsVisible,
    releaseBitmap: ReleaseBitmap,
    startRender: StartRender,
    logRenderCore: generation => Log.LogRenderCore(generation),
    post: action => Dispatcher.UIThread.Post(action, DispatcherPriority.Render));

_coordinator.RequestRender();      // 複数の要求は 1 回のポストに結合される
_coordinator.CancelAndRelease();   // 非表示遷移・明示クリア（要求の破棄 + 解放）
```

## 依存関係

```text
LoupeControl.UIBase
    ├── Feature.LoupeCore（描画入力・通知・外観・切り出しの計算）
    └── Platform.Core（IEsCanvas・Es* 値型・SkiaEsCanvas・EsLabelLayout）
```

**UI フレームワークへ依存しません**。UI 固有の描画 API は `IEsCanvas` の実装が担い、実行キューは `post` コールバックとして UI が注入します。

名前空間は `EsUtil.SaveEvidence.Loupe.UIBase`。アセンブリ名は `LoupeControl.UIBase`。

## テスト

`tests/` が本プロジェクトのテストを持ちます。描画は UI 非依存の描画ポート（Skia）で行い、再描画のスケジューリングは UI 型を持たないため、**UI 初期化なしで検証**できます（合計 32 件）。

```text
tests/
├── LoupeRenderer.Tests.cs             # 層ごとの描画（下地・市松・拡大画像・十字線・外枠・層の選択・引数の検証）（15 件）
├── LoupeLabelPlan.Tests.cs            # 実寸 → ラベル要素の写像（5 件）
├── LoupeRenderCoordinator.Tests.cs    # バッチ化・世代番号・適用判定・破棄と解放・防御分岐（12 件）
└── TestHelper.cs                      # テスト用の描画先（Skia）
```

```powershell
dotnet test src/LoupeControl.UIBase/tests/LoupeControl.UIBase.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
