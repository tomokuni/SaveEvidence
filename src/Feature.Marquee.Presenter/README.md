# Feature.Marquee.Presenter

マーキー選択（ドラッグで矩形を選び、移動・リサイズできる操作）の見た目を定める **コントロール化を前提としない UI 非依存の層**です。UI 実装が変わっても同じ処理を共有します。

## 概要

本層は 2 つの関心事を持ちます。どちらも「単体で使う `Marquee` だけでは決まらない」ため、コア（`Feature.Marquee.Core`）には置きません。

| 関心事 | 内容 | 代表型 |
| --- | --- | --- |
| **提示（Presenter）** | 描画の幾何（`MarqueeGeometry`）と外観から**描画計画**（層の順序・装飾矩形・文言・ラベル配置）を解決する | `MarqueePresenter` / `MarqueeDrawPlan` / `MarqueeDrawLayer` |
| **外観** | 色・フォント・枠線の太さ・表示可否（描画にだけ必要な設定の単一値） | `MarqueeAppearance` |

**描画は行いません**。UI フレームワークごとの描画モデルの差は描画 API の抽象では吸収しきれないため、本層は**描画計画というデータ**を返し、描画は各 UI の描画実装が自 UI の API で行います。

**本層はコントロール化を前提とした処理を持ちません**（ホイール操作・ビューポート連携・入力の解釈・組み込み先の概念は UI 層が持ちます）。

## 使い方

マーキー選択だけを持つ独自コントロールは、**入力の転送**・**効果の受領**・**描画**の 3 点を書けば作れます。

```csharp
// 1. 効果（本 UI が必要とするものだけ。MarqueeEffectsBase が他の効果の空実装を提供する）
private sealed class Effects(MyControl owner) : MarqueeEffectsBase
{
    public override void RequestRedraw() => owner.InvalidateVisual();
}

// 2. 生成（カーソル操作が不要なら cursor は null でよい。外観は MarqueeAppearance が持つ）
private readonly MarqueePresenter _presenter = new(new Effects(this));

// 3. 入力の転送（座標はドメイン座標へ変換して渡す）
protected override void OnPointerPressed(PointerPressedEventArgs e)
    => _presenter.Press(e.GetPosition(this).ToDomain(), e.ClickCount);

// 4. 描画（描画計画の解決は提示層、描画は自 UI の API）
protected override void Render(DrawingContext context)
{
    var plan = _presenter.ResolveDrawPlan(Bounds.Size.ToEsSize(), MeasureText);  // 幾何 + 外観 + 層 + 配置を解決済み
    PaintByOwnApi(context, in plan);                                            // 自 UI の型へ写して層の順に描く
    // ラベルを要素として置く場合は plan.Labels（配置指示）を要素へ写す
}
```

- 外観を変える場合は `_presenter.Appearance = MarqueeAppearance.Default with { ... }` を設定します。
- 座標変換（ドメイン ⇔ DIP）は `MarqueePresenter.SetCoordTransform` へ注入します（未指定は等倍）。
- **ホイール操作（ズーム・パン）と表示の変化（パン・ズーム）への追従はホスト（UI 層）が扱います**（ビューポートが存在して初めて意味を持つ処理のため）。

## 含まれるもの

| グループ | 型 |
| --- | --- |
| 提示 | `MarqueePresenter`（描画計画の解決。`sealed` ではない） / `Drawing.MarqueeDrawPlan` / `Drawing.MarqueeDrawLayer` / `Drawing.MarqueeTextPlacement` |
| 外観 | `Setting.MarqueeAppearance`（色・フォントサイズ・枠線の太さ・表示可否） |
| ラベル | `Label.MarqueeLabelPlacement`（配置指示） |
| ログ | `Logging.MarqueePresenterLogMessage`（internal） |

## 依存

- 依存は `Feature.Marquee.Core`（幾何・状態・判断・ファサード）と `Platform.Core`（`Es*` 値型・`ICoordTransform`・`ICursorService`）、`Microsoft.Extensions.Logging.Abstractions`（外観の変更ログの定義）のみです。
- **UI フレームワークへ依存しません**（描画 API の抽象も持ちません）。
- 依存方向は UI 層 → 本層 → `Feature.Marquee.Core` → `Platform.Core` の一方向です（コアは本層を参照しません）。

層の全体像は [`src/README.md`](../README.md) を参照してください。

## カスタマイズ

| 差し替えたいもの | 方法 |
| --- | --- |
| 見た目（色・フォント・枠線の太さ・表示可否） | `MarqueeAppearance` を変える（UI の `Appearance` プロパティへ設定する） |
| 描画計画の構成（層・装飾・文言の独自化） | `MarqueePresenter` を継承し `ResolveDrawPlan` を上書きする、または `MarqueePresenterXxx` を作る |
| 描画そのもの | UI 層の描画実装を差し替える |
| 寸法・対話パラメータ | `MarqueeOptions`（`HandleSize` / `MarkerLength` / `HitRadius` / `MinSelectionSize` / `ClampToDomainBounds`）を変える（コアが単一所有する） |
| 付加情報ラベル（操作・情報）の体裁 | コントロール固有の付加情報であるため本層に置かない（組み込み先のホストが文言と描画を持つ） |

## ビルド

```powershell
dotnet build src/Feature.Marquee.Presenter/src/Feature.Marquee.Presenter.csproj
```

## テスト

```powershell
dotnet test src/Feature.Marquee.Presenter/tests/Feature.Marquee.Presenter.Tests.csproj
```

`tests/` が単体テストを持ちます。描画計画の値（矩形・層・配置・文言）は UI 型を持たないため **UI 初期化なしで検証**でき、画素の検証は描画 API を持つ UI 側のテストが担います。

## ドキュメント

- [`docs/SPEC_API.md`](docs/SPEC_API.md) — 公開 API のリファレンスと使用例
- [`docs/SPEC_System.md`](docs/SPEC_System.md) — アーキテクチャ・単一所有・設計判断・ログ
- [`src/README.md`](../README.md) — リポジトリ全体の層構成

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
