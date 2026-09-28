# FadeMaskDragRectControl.UIBase

`FadeMaskDragRectControl`（領域選択の複合コントロール）の **UI 間で共有する処理**です。Avalonia 版と MewUI 版が
同じ分配規則とラベル配置を使うことを保証します。

## 目的

- **設定とモードから内包コントロールの値への解決を 1 箇所へ集約**する。以前は両 UI が
  「設定の写し → モードから導出する項目の上書き」という 2 段の分配をそれぞれ持っており、片方だけ規則が
  変わる余地があった。
- **付加情報ラベル（操作・情報）の積み上げを 1 箇所へ集約**する。体裁は既に
  `MarqueeHostLabelRenderer` と `MarqueePaint`（`Feature.Marquee.Presenter`）が単一所有しているが、
  「どの文言をどの位置へ積むか」は両 UI が別々に書いていた。

## 層の位置付け

```text
FadeMaskDragRectControl.Core（設定・モード・モードから導出する状態）
        ↓
FadeMaskDragRectControl.UIBase（値の解決 ＋ ラベルの描画）
        ↓
FadeMaskDragRectControl.AvaloniaUI / .MewUI（子コントロールへの代入・配線）
```

- **値の決定は本層、代入は UI 層**。結果を代入するには子コントロール（UI 固有型）が必要なため、本層は
  UI 非依存の値（コアの設定型）までを返す。
- モードの解釈はコア（`FadeMaskDragRectModes`）が単一所有する。本層はその結果を値へ反映するだけである。

## 構成

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `FadeMaskDragRectChildValues` | `public readonly record struct` | 設定とモードから内包コントロールへ適用する値を解決する |
| `FadeMaskHostLabelRenderer` | `public static class` | 付加情報ラベル（操作・情報）の積み上げを描画ポートへ写す |

## 依存関係

```text
FadeMaskDragRectControl.UIBase
    ├── FadeMaskDragRectControl.Core（設定・モード・状態の解決）
    ├── Feature.Marquee.Core / Feature.Loupe.Core（内包コントロールの設定型）
    ├── Feature.Marquee.Presenter（付加情報ラベルの体裁）
    └── Platform.Core（コア幾何型・描画ポート・ラベル配置）
```

UI フレームワークへ依存しません。外部パッケージへの直接依存もありません。

`FadeMaskDragRectControl.Core` の `internal` 型（`FadeMaskDragRectModes` / `FadeMaskDragRectState`）を参照するため、
同プロジェクトの `InternalsVisibleTo` に本アセンブリが含まれます。

## 主な API

```csharp
// 設定とモードから子コントロールへ適用する値を解決する（代入は UI 層が行う）
var values = FadeMaskDragRectChildValues.Resolve(options, mode, marqueeAppearance);
marquee.Options = values.Marquee;
marquee.Appearance = values.MarqueeAppearance;
marquee.IsHitTestVisible = values.IsHitTestVisible;
loupe.Options = values.Loupe;
loupe.PositionOffset = values.LoupeOffset;

// 付加情報ラベルの配置指示を解決する（文言の生成はホスト、描画は各 UI）
var placements = FadeMaskHostLabelRenderer.Resolve(
    plan, options.Labels, marqueeAppearance, operationText, infoText, measureText);
```

## 設計上の約束

1. **モードの解釈はコアが単一所有する**（本層は `FadeMaskDragRectModes.Resolve` を呼ぶだけ）。
2. **ハイライト矩形は実行時状態**のため、設定から写さず引数で受ける（設定の差し替えで失わない）。
3. **選択矩形を使わないモードでも描画は止めない**（暗転マスクのハイライトを描くため、止めるのは入力だけ）。
4. **付加情報ラベルの文言はホストが生成する**（本層は位置と表示可否だけを決める）。
5. **空文字の文言は描かない**（対象外のモードでは文言が空になるため）。

## テスト

```text
tests/
├── FadeMaskDragRectChildValues.Tests.cs   # 値の解決（モード・ハイライト・設定の写し）
└── FadeMaskHostLabelRenderer.Tests.cs     # ラベルの積み上げ（描画ポートの画素検証）
```

合計 17 件（`[Theory]` は展開後のケース数）。UI 初期化が不要なため、すべて `[Fact]` / `[Theory]` で並列実行されます。

```powershell
dotnet test src/FadeMaskDragRectControl.UIBase/tests/FadeMaskDragRectControl.UIBase.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
