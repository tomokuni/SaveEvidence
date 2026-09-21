# LoupeControl.MewUI

`LoupeControl.AvaloniaUI`（Avalonia UI 実装）と同じ **UI 非依存コア（`LoupeControl.Core`）** を使い、描画層のみを **MewUI** で実装したルーペ（拡大鏡）コントロールです。

## 目的

ルーペのロジック（数値の安全域補正・切り出し領域の計算・装飾矩形の計算）は `LoupeControl.Core` が単一所有します。
本プロジェクトはその計算結果を **Skia のキャンバスへ描画するだけ** の層です。
そのため Avalonia 版と**同じ設定・同じ見た目**になり、設定ファイル（`%LOCALAPPDATA%\SaveEvidence\settings.json`）も両 UI で共用できます。

## 特徴

| 項目 | 内容 |
| --- | --- |
| 基底クラス | `Aprillz.MewUI.Skia.Controls.SkiaCanvasView`（SkiaSharp で直接描画する） |
| 中間バッファ | **生成しない**。画面のキャンバスへ直接描画する（Avalonia 版は `RenderTargetBitmap` を経由する） |
| 拡大方式 | 最近傍（画素をそのまま拡大表示する） |
| DPI 追従 | `OnDpiChanged` で表示スケーリング係数を `newDpi / 96` へ自動更新する（ホストの設定は不要） |
| 連続再描画 | **無効**。プロパティ変更時にのみ再描画する（静止画のため） |
| 位置決め | **本コントロールは位置を持たない**。ホストが `LoupePositionCalculator`（コア）で配置する |

## インストール（プロジェクト参照）

```xml
<ProjectReference Include="..\..\LoupeControl.MewUI\src\LoupeControl.MewUI.csproj" />
```

パッケージは `Aprillz.MewUI.Skia` を参照します（バージョンは `MewUIVersion` プロパティで 0.21.1 に固定）。

## 使い方

```csharp
using Estable.MewUI.Loupe;
using EsUtil.Platform.Core.Geometry;
using MewUIColor = Aprillz.MewUI.Color;

var loupe = new LoupeControl
{
    Source = esBitmap,                       // 拡大元の 100% 画像（コアの EsBitmap）
    CenterPoint = new EsPoint(320, 240),     // 拡大中心（元画像ピクセル座標）
    RequestedSize = 160,                     // 一辺のサイズ（DIP）
    ZoomLevel = 4,                           // 拡大率
    CrossColor = MewUIColor.FromArgb(102, 255, 0, 0),
    CheckerColorA = MewUIColor.FromArgb(255, 255, 255, 255),
    CheckerColorB = MewUIColor.FromArgb(255, 192, 192, 192),
    FrameColor = MewUIColor.FromArgb(255, 0, 0, 0),
    FrameWidth = 2,
};
```

`Source` と `CenterPoint` を更新すると自動的に再描画されます（`InvalidateVisual`）。表示位置はホストが決めます。

```csharp
// 位置決めはコアの計算を利用する（中心 + オフセット、親領域内へクランプ）
var (size, position) = LoupePositionCalculator.Compute(
    requestedSize, parentSize, hasParentSize, centerDip, offset);
```

> **注意**: `LoupePositionCalculator` は `LoupeControl.Core` の `internal` 型です。外部から使用するには
> `InternalsVisibleTo` の追加が必要です（本リポジトリの `app.MewUI` はアセンブリ内から使用します）。

## プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Source` | `EsBitmap?` | `null` | 拡大元の 100% 画像。null の場合は市松模様のみ |
| `CenterPoint` | `EsPoint?` | `null` | 拡大中心（元画像ピクセル座標）。null の場合は市松模様のみ |
| `RequestedSize` | `int` | 160 | 一辺のサイズ（DIP）。1〜4096 に補正 |
| `RenderScaling` | `double` | 1.0 | DIP → 物理ピクセルの係数。0.1〜8.0 に補正（非有限は既定値）。DPI 変更で自動更新 |
| `ZoomLevel` | `int` | 4 | 拡大率。1〜64 に補正 |
| `CrossColor` | `Color?` | `null` | 十字線の色（不透明度はアルファ）。null は描画しない |
| `CheckerSize` | `int` | 6 | 市松模様の 1 タイルサイズ（DIP）。1〜128 に補正 |
| `CheckerColorA` / `CheckerColorB` | `Color?` | `null` | 市松模様の明色 / 暗色。null は透明 |
| `BackgroundColor` | `Color` | 白（不透明） | 市松模様の下地色。市松模様の色は半透明のため、下地がないと後ろの要素が透ける |
| `FrameColor` | `Color?` | `null` | 外枠の色。null は描画しない |
| `FrameWidth` | `int` | 2 | 外枠の太さ（DIP）。1〜64 に補正 |
| `ShowCoordLabel` | `bool` | `true` | 上端中央の座標ラベルの表示可否 |
| `CoordLabelColor` | `Color?` | `null` | 座標ラベルの文字色。null は白 |
| `CoordLabelBackgroundColor` | `Color?` | `null` | 座標ラベルの背景色。null は黒 60% |
| `CoordLabelFontSize` | `double` | 12.0 | 座標ラベルのフォントサイズ（DIP）。6〜48 に補正 |
| `Log` | `ILogger` | `NullLogger` | 設定変更ログと描画例外の出力先（null は `NullLogger`。Avalonia 版と同一の書式） |

`RequestedSize` はレイアウトにも影響します（`MeasureContent` が一辺 `RequestedSize` の正方形を返します）。

## イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `Rendered` | `LoupeRenderedEventArgs` | 描画完了の通知（描画先キャンバス・一辺の物理ピクセル・拡大率・中心・切り出し領域の左上・スケール）。ホストはハンドラ内でキャンバスへオーバーレイを追加描画できる |

オーバーレイの描画位置は `(ドメイン座標 − CaptureTopLeft) × Scale` で求めます（破線の例は `FadeMaskDragRectControl.MewUI`）。

## ログ

`Log` プロパティへ `ILogger` を設定すると、設定変更と描画の例外を出力します（既定は出力なし）。書式は Avalonia 版と同一です。

| レベル | 出力例 |
| --- | --- |
| Debug | `[LoupeCtrl] Setting: ZoomLevel 4 → 8`（Source / CenterPoint / RequestedSize / RenderScaling / ZoomLevel。同一値なら出力しない） |
| Error | `[LoupeCtrl] Update exception`（描画の例外。アプリは継続する） |

Avalonia 版の Trace ログ（再描画パイプライン）は持ちません（MewUI 版は中間ビットマップを作らず同期描画するため）。

## 構成

```text
src/LoupeControl.MewUI/
├── README.md                # 本ファイル
├── SPEC_ExtFunc.md          # 外部機能仕様書
├── SPEC_System.md           # システム仕様書
├── src/
│   ├── LoupeControl.MewUI.csproj   # net10.0 / MewUI 0.21.1 固定
│   ├── LoupeControl.cs             # SkiaCanvasView 派生のコントロール
│   ├── LoupeRenderedEventArgs.cs   # 描画完了の通知（キャンバスと表示情報）
│   └── LoupeSkiaRenderer.cs        # コアの計算結果を SKCanvas へ描画する層
└── tests/
    ├── LoupeControl.MewUI.Tests.csproj
    ├── TestHelper.cs                   # テスト共通（描画先・画像生成）
    ├── LoupeSkiaRenderer.Tests.cs      # 描画結果の画素検証
    ├── LoupeControl.Tests.cs           # プロパティ補正・レイアウト
    └── LoupeRenderedEventArgs.Tests.cs # 描画完了通知の引数
```

## ビルドとテスト

```powershell
dotnet build src/LoupeControl.MewUI/src/LoupeControl.MewUI.csproj
dotnet test src/LoupeControl.MewUI/tests/LoupeControl.MewUI.Tests.csproj
```

## 関連ドキュメント

- `../LoupeControl.Core/README.md` — 共有する UI 非依存ロジック
- `../LoupeControl/README.md` — Avalonia 版ルーペ
- `../app.MewUI/README.md` — 組み込み先のアプリ
