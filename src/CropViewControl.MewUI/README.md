# CropViewControl.MewUI

`CropViewControl.AvaloniaUI`（Avalonia UI 実装）と同じ **UI 非依存コア（`CropViewControl.Core`）** を使い、描画層を **MewUI** で実装した高レベル複合コントロールです。

## 目的

画像表示（拡大・パン・Fit）と外部提供矩形のオーバーレイ描画を、ホストが単一のコントロールとして利用できるようにします。
ズームの計算は `ZoomViewControl.Core`、矩形のダッシュ長は `CropViewControl.Core` が単一所有するため、**Avalonia 版と同じ見た目・挙動**になります。

## 特徴

| 項目 | 内容 |
| --- | --- |
| 適用パターン | Facade（ズームビューと矩形オーバーレイを内包し、ホストへ統一インターフェースを提供する） |
| 層の責務 | UI 層は入力・描画・ホスト結線のみを担い、判断を持たない（矩形のクランプ・太さの下限・市松の描画可否はコアが単一所有する） |
| 内部構造 | `ZoomViewControl`（画像ビューポート）+ `RectOverlayCanvas`（透明な矩形オーバーレイ）を重ねる |
| 矩形の描画 | 実線は前景色の枠のみ。破線は「背景色の実線 → 前景色の破線」の 2 回描画で市松風の視認性を出す |
| 座標変換 | 画像表示矩形の原点基準（Fit 中央寄せの補正を含む）で画像と正確に一致する |
| 入力 | 下のズームビューが受け取る（オーバーレイは `IsHitTestVisible = false`） |
| DPI 追従 | 内包するズームビューが `OnDpiChanged` で自動追従する |
| 選択オーバーレイ | 対象外。ホストが `FadeMaskDragRectControl.MewUI` を重ねて構成する（`app.MewUI` の切り出しはこの構成） |
| ログ | `Log` プロパティ（内包するズームビューへ伝搬する。ズーム操作の前後のズーム率を Information で出力） |

## インストール（プロジェクト参照）

```xml
<ProjectReference Include="..\..\CropViewControl.MewUI\src\CropViewControl.MewUI.csproj" />
```

パッケージは `Aprillz.MewUI.Skia` を参照します（バージョンは `MewUIVersion` プロパティで 0.21.1 に固定）。

## 使い方

```csharp
using EsUtil.SaveEvidence.CropView;
using EsUtil.SaveEvidence.CropView.MewUI;
using EsUtil.Platform.Core.Geometry;

var cropView = new CropViewControl
{
    Source = esBitmap,          // コアの EsBitmap
    CenterAlign = true,
};

// 検出矩形などのオーバーレイ（ドメイン座標 = 元画像ピクセル座標）
cropView.Rects =
[
    new OverlayRect(
        new EsRect(10, 20, 100, 80), OverlayLineStyle.Dashed,
        System.Drawing.Color.FromArgb(255, 255, 64, 64),
        System.Drawing.Color.FromArgb(255, 255, 255, 255),
        Thickness: 2),
];

// ズーム操作とイベントはズームビューと同じ
cropView.ZoomIn();
cropView.ZoomFit();
cropView.ZoomChanged += (_, e) => zoomText.Text = $"{e.ZoomPercent:0}%";
```

## プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Source` | `EsBitmap?` | `null` | 表示する画像。設定すると Fit 表示へリセットする |
| `CenterAlign` | `bool` | `true` | 画像がビューポートより小さい軸を中央寄せする |
| `Appearance` | `ZoomViewAppearance` | 暗色の下地・市松模様なし | 下地（市松模様の大きさ・2 色・背景色）の外観の**単一値**。コアが単一所有する |
| `Rects` | `IReadOnlyList<OverlayRect>` | 空 | オーバーレイとして描画する矩形（ドメイン座標）。設定すると再描画する |
| `ZoomView` | `ZoomViewControl` | — | 内包するズームビューポート（座標変換・表示サイズの利用に使う） |
| `ZoomPercent` | `double` | Fit の実効倍率 | 実効ズーム率（%）。0 より大きい値のみ設定できる |
| `CanZoomIn` / `CanZoomOut` | `bool` | `false` | ズーム可否（画像未設定時は false） |
| `Log` | `ILogger` | `NullLogger` | ズーム操作ログの出力先（内包するズームビューへ伝搬する） |

## メソッド

| メソッド | 説明 |
| --- | --- |
| `ZoomIn()` / `ZoomOut()` / `ZoomFit()` | 段階遷移・Fit 表示（内包するズームビューへ委譲） |
| `PanByWheel(delta, shift)` | ホイールの回転量でパンする（ホストがオーバーレイ等で受けたホイールの転送用） |
| `ZoomByWheel(zoomIn, position)` | Ctrl+ホイールのズームを適用する（同上。`position` はズーム中心） |
| `TranslatePositionFrom(element, position)` | 重ねた要素のローカル座標を本コントロールの座標へ変換する（ズーム中心の座標合わせ用） |
| `ImageRectViewport()` | 画像の表示矩形（ビューポート座標・DIP） |
| `ViewportToContent(pos)` / `ContentToViewport(pos)` | 画像表示矩形の原点基準の座標変換 |

## イベント

| イベント | 説明 |
| --- | --- |
| `ZoomChanged` | ズーム率・Fit 状態・ズーム可否（内包するズームビューから中継） |
| `ViewportChanged` | ビューポートの変化（パン・ズーム・リサイズ。内包するズームビューから中継） |

## 構成

```text
src/CropViewControl.MewUI/
├── README.md                # 本ファイル
├── SPEC_ExtFunc.md          # 外部機能仕様書
├── SPEC_System.md           # システム仕様書
├── src/
│   ├── CropViewControl.MewUI.csproj  # net10.0 / MewUI 0.21.1 固定
│   ├── CropViewControl.cs            # Facade（ズームビュー + 矩形オーバーレイ）
│   ├── RectOverlayCanvas.cs          # 透明な矩形オーバーレイ（ドメイン→ビューポート変換）
│   └── CropSkiaRenderer.cs           # コアが確定した描画入力を SKCanvas へ写す
└── tests/
    ├── CropViewControl.MewUI.Tests.csproj
    ├── TestHelper.cs                 # テスト共通（描画先・画像生成・画素数え上げ）
    ├── CropSkiaRenderer.Tests.cs     # 枠・破線・境界値の画素検証（12 件）
    └── CropViewControl.Tests.cs      # Facade の委譲・外観・座標変換（15 件）
```

## ビルドとテスト

```powershell
dotnet build src/CropViewControl.MewUI/src/CropViewControl.MewUI.csproj
dotnet test src/CropViewControl.MewUI/tests/CropViewControl.MewUI.Tests.csproj
```

## 関連ドキュメント

- `../CropViewControl.Core/README.md` — 共有する UI 非依存ロジック
- `../CropViewControl.AvaloniaUI/README.md` — Avalonia 版クロップビュー
- `../ZoomViewControl.MewUI/README.md` — 内包するズームビュー
- `../app.MewUI/README.md` — 組み込み先のアプリ
