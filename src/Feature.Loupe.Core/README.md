# Feature.Loupe.Core

`LoupeControl`（選択範囲を拡大表示するコントロール）の **UI 非依存のコア**です。Avalonia 版と MewUI 版が同じ計算結果を共有します。

## 概要

ルーペの表示レイアウト（表示サイズ・表示位置）と描画の幾何（切り出し領域・描画面）の解決を 1 箇所に集約し、UI ごとの実装差から切り離します。

**操作の唯一の窓口は公開ファサード `Loupe`** です。UI 層とホストは本型だけを参照し、表示レイアウトと描画の幾何の解決を委ねます（個々の計算クラスは内部実装です）。

**描画は本コアの関心事ではありません**。描画 API も描画の抽象も持たず、返すのは描画の幾何（`LoupeGeometry`）だけです。装飾（十字線・枠線）の矩形・文言・表示可否・描画順は描画の都合として UI 側の共有層（`Feature.Loupe.Presenter`）が単一所有します。

UI フレームワークに依存しないため、UI 初期化なしの `[Fact]` で検証できます。

## 使い方

```csharp
using EsUtil.Feature.Loupe.Core;
using EsUtil.Feature.Loupe.Core.Setting;

// 設定（計算に必要な値のみ。色・フォント・表示可否は外観（Feature.Loupe.Presenter）が持つ）
var options = new LoupeOptions { RequestedSize = 160, ZoomLevel = 4 };
using var loupe = new Loupe(options);

// 拡大中心（ドメイン座標 = 元画像ピクセル座標）と表示位置のオフセット
loupe.CenterPoint = new EsPoint(1920, 1080);
loupe.PositionOffset = new EsPoint(16, 16);

// 表示レイアウト（親領域のサイズを渡す。拡大中心が未設定の場合は位置が null）
var layout = loupe.ResolveLayout(parentSize: new EsSize(800, 600));

// 描画の幾何（1 フレーム分。UI 層は本値を組み立てず、提示層へ渡す）
var geometry = loupe.ResolveGeometry(displaySize: 160, availableSize: new EsSize(800, 600));
var capture = geometry.MagnifiedImage;   // 切り出し領域・描画元・描画先
```

## 含まれるもの

| グループ | 型 |
| --- | --- |
| ファサード | `Loupe`（操作の唯一の窓口） / `Loupe.Limits`（既定値と安全域・補正） |
| 描画の幾何 | `Drawing.LoupeGeometry` / `Drawing.LoupeMagnifiedImage` |
| 設定 | `Setting.LoupeOptions`（表示サイズ・拡大率。色・フォント・表示可否は持たない） |
| 表示レイアウト | `Placement.LoupeLayout`（表示サイズと表示位置） |
| ログ | `Logging.LoupeLogMessage`（internal） |

## 依存

- 依存は `Platform.Core`（`Es*` 幾何型・`ICoordTransform`）と `Microsoft.Extensions.Logging.Abstractions`（ログ定義のソース生成）のみです（SkiaSharp は参照しません）。
- UI フレームワークを参照しません。UI 固有の型は利用者（UI 実装）が境界で変換します。
- 依存方向は UI 層 → `Feature.Loupe.Presenter` → 本プロジェクト → `Platform.Core` の一方向です（コアは提示層を参照しません）。

層の全体像は [`../README.md`](../README.md) を参照してください。

## カスタマイズ

| 差し替えたいもの | 方法 |
| --- | --- |
| 表示サイズ・拡大率 | `LoupeOptions`（`RequestedSize` / `ZoomLevel`）を変える |
| 数値の安全域 | `Loupe.Limits` の既定値と補正（`CoerceBorderThickness` 等）を使う |
| 見た目（色・フォント・枠線の太さ・表示可否） | `Feature.Loupe.Presenter` の `LoupeAppearance` を変える（本コアは色を持たない） |
| 描画計画（層・配置） | `Feature.Loupe.Presenter` の `LoupePresenter.ResolveDrawPlan` を上書きする |
| 描画そのもの | 各 UI の描画実装（`LoupeContextPainter` / `LoupeSkiaPainter`）を差し替える |

## 座標型

UI フレームワークの幾何型（`Avalonia.Point` / `Size` 等）ではなく、`Platform.Core` のコア幾何型 `EsPoint` / `EsSize` / `EsRect` を使用します。

UI 実装側は、境界（サービス呼び出し・イベント引数の生成箇所）で 1 行の相互変換（`.ToEsPoint()` / `.ToEsSize()` / `.ToAvaloniaPoint()`）を行います。UI 内部の状態・描画は UI 側の型のままで構いません。

## ビルド

```powershell
dotnet build src/Feature.Loupe.Core/src/Feature.Loupe.Core.csproj
```

ルーペ系の 4 プロジェクト（Core / Presenter / AvaloniaUI / MewUI）は `GenerateDocumentationFile` を有効にし、**存在しないメンバーへの `<see cref>` をビルド警告（CS1574）で検出**します（メンバー改名・削除でドキュメントが腐るのを防ぐ）。

## テスト

```powershell
dotnet test src/Feature.Loupe.Core/tests/Feature.Loupe.Core.Tests.csproj
```

`tests/` が単体テストを持ちます。UI 初期化が不要なため `[Fact]` / `[Theory]` で並列実行され、**ソースのフォルダを鏡映**します（`src/Drawing/` の型は `tests/Drawing/` で検証する）。

## ドキュメント

- [`docs/SPEC_API.md`](docs/SPEC_API.md) — 公開 API のリファレンスと使用例
- [`docs/SPEC_System.md`](docs/SPEC_System.md) — アーキテクチャ・単一所有・設計判断・ログ
- [`../README.md`](../README.md) — リポジトリ全体の層構成

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
