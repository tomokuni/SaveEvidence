# LoupeControl.MewUI

`LoupeControl.AvaloniaUI`（Avalonia UI 実装）と同じ **UI 非依存コア（`Feature.LoupeCore`）と描画層（`LoupeControl.UIBase`）** を使い、描画ポートの適合（`SkiaEsCanvas`）と入力の受付のみを **MewUI** で実装したルーペ（拡大鏡）コントロールです。

## 目的

ルーペのロジック（数値の安全域補正・切り出し領域の計算・装飾矩形の計算）は `Feature.LoupeCore` が単一所有し、描画の手順と色は UI 非依存の描画層（`LoupeControl.UIBase` の `LoupeRenderer` / `LoupePaint`）が単一所有します。
本プロジェクトはその計算結果を描画ポート `SkiaEsCanvas`（Platform.Core）経由で **Skia のキャンバスへ描画させるだけ** の層です。
そのため Avalonia 版と**同じ設定・同じ見た目**になり、設定ファイル（`%LOCALAPPDATA%\SaveEvidence\settings.json`）も両 UI で共用できます。層構成（Core / UIBase / UI）の全体像は [`src/README.md`](../README.md) を参照してください。

## 特徴

| 項目 | 内容 |
| --- | --- |
| 基底クラス | `Aprillz.MewUI.Controls.Panel`（子の `SkiaCanvasView` が Skia で直接描画する） |
| 中間バッファ | **生成しない**。画面のキャンバスへ直接描画する（Avalonia 版は `RenderTargetBitmap` を経由する） |
| 描画サイズ | `Options.RequestedSize` × `DipToDeviceScaling`（物理ピクセル）。キャンバスより大きい場合はキャンバスに合わせる（クランプ規則はコアが単一所有） |
| 拡大方式 | 最近傍（画素をそのまま拡大表示する） |
| DPI 追従 | `OnDpiChanged` で表示スケーリング係数を DPI から換算した値へ自動更新する（換算規則は `DpiScaler.ToScaling` が単一所有。ホストの設定は不要） |
| 連続再描画 | **無効**。プロパティ変更時にのみ再描画する（静止画のため） |
| 位置決め | **本コントロールは位置を持たない**。ホストが `ResolveLayout`（コアのファサード）の結果で配置する |

## インストール（プロジェクト参照）

```xml
<ProjectReference Include="..\..\LoupeControl.MewUI\src\LoupeControl.MewUI.csproj" />
```

パッケージは `Aprillz.MewUI.Skia` を参照します（バージョンは `MewUIVersion` プロパティで 0.21.1 に固定）。

## 使い方

```csharp
using EsUtil.Feature.LoupeCore;
using EsUtil.Feature.LoupeCore.Setting;
using EsUtil.SaveEvidence.Loupe.MewUI;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

var loupe = new LoupeControl
{
    Source = esBitmap,                       // 拡大元の 100% 画像（コアの EsBitmap）
    CenterPoint = new EsPoint(320, 240),     // 拡大中心（元画像ピクセル座標）
    // 寸法はコアの Options（with 式で部分変更する）
    Options = LoupeOptions.Default with
    {
        RequestedSize = 160,                 // 一辺のサイズ（DIP）
        ZoomLevel = 4,                       // 拡大率
        BorderThickness = 2,                 // 枠線の太さ（DIP）
    },
    // 色は描画の設定（LoupePaint）で指定する
    Paint = LoupePaint.Default with
    {
        CrossColor = DrawingColor.FromArgb(102, 255, 0, 0),
        CheckerColorA = DrawingColor.FromArgb(255, 255, 255, 255),
        CheckerColorB = DrawingColor.FromArgb(255, 192, 192, 192),
        BorderColor = DrawingColor.FromArgb(255, 0, 0, 0),
    },
};
```

`Source` と `CenterPoint` を更新すると自動的に再描画されます（`InvalidateVisual`）。表示位置はホストが決めます。

```csharp
// 位置決めはコアのファサードが計算する（中心 + オフセット、親領域内へクランプ）
var layout = loupe.ResolveLayout(parentSize);
if (layout.Position is { } position)
{
    loupe.Arrange(new Rect(x + position.X, y + position.Y, layout.DisplaySize, layout.DisplaySize));
}
```

> **注意**: `ResolveLayout` はコアのファサード（`Feature.LoupeCore` の `Loupe`、`public` 型）の API です（汎用コアとして公開されており、`InternalsVisibleTo` は不要です）。

## プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Source` | `EsBitmap?` | `null` | 拡大元の 100% 画像。null の場合は市松模様のみ |
| `CenterPoint` | `EsPoint?` | `null` | 拡大中心（元画像ピクセル座標）。null の場合は市松模様のみ |
| `Options` | `LoupeOptions` | `LoupeOptions.Default` | 設定（表示サイズ・拡大率・寸法）。`with` 式で部分変更する。数値項目はコアの型が生成時に安全域へ補正する |
| `Paint` | `LoupePaint` | `LoupePaint.Default` | 描画に使う色（下地・市松模様・枠線・十字線・中心座標ラベルの 7 色）。`with` 式で部分変更する。色はコアの計算に入らない |
| `DipToDeviceScaling` | `double` | 1.0 | DIP → 物理ピクセルの係数。0.1〜8.0 に補正（非有限は既定値）。DPI 変更で自動更新 |
| `Log` | `ILogger` | `NullLogger` | 設定変更ログと描画例外の出力先（null は `NullLogger`。Avalonia 版と同一の書式） |

`Options`（コアの `LoupeOptions`）の項目:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `RequestedSize` | `int` | 160 | 一辺のサイズ（DIP）。1〜4096 に補正 |
| `ZoomLevel` | `int` | 4 | 拡大率。1〜64 に補正 |
| `CheckerSize` | `int` | 6 | 市松模様の 1 タイルサイズ（DIP）。1〜128 に補正 |
| `BorderThickness` | `int` | 2 | 枠線の太さ（DIP）。1〜64 に補正 |
| `ShowCoordLabel` | `bool` | `true` | 上端中央の座標ラベルの表示可否（中心未設定時は描画しない） |
| `ShowCheckerboard` | `bool` | `true` | 市松模様の表示可否 |
| `ShowCrosshair` | `bool` | `true` | 十字線の表示可否 |
| `ShowBorder` | `bool` | `true` | 枠線の表示可否 |

`Paint`（`LoupePaint`。`EsUtil.SaveEvidence.Loupe.UIBase`）の項目:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `BackgroundColor` | `System.Drawing.Color` | 透明 | 下地（ベース）色。市松模様・拡大画像より先に表面全体へ塗る。背後の要素を透かさない場合は不透明色を指定する |
| `CrossColor` | `System.Drawing.Color` | 赤 40%（`#66FF0000`） | 十字線の色 |
| `CheckerColorA` / `CheckerColorB` | `System.Drawing.Color` | Gray 30% / DarkGray 30% | 市松模様の明色 / 暗色 |
| `BorderColor` | `System.Drawing.Color` | LightGray（`#FFD3D3D3`） | 枠線の色 |
| `CoordLabelColor` | `System.Drawing.Color` | 白 | 座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `System.Drawing.Color` | 黒 60%（`#99000000`） | 座標ラベルの背景色 |
| `CoordLabelFontSize` | `double` | 12.0 | 座標ラベルのフォントサイズ（DIP）。6〜48 に補正 |
| `CoordLabelCornerRadius` | `int` | 2 | 座標ラベルの背景の角丸半径（DIP）。0〜64 に補正（0 は角丸なし） |

`Options.RequestedSize` はレイアウトにも影響します（`MeasureContent` が一辺 `RequestedSize` の正方形を返します）。

数値の安全域への補正と変更ログはプロパティの登録（`MewProperty.Register` の `coerce` / `changed`）へ渡します。したがってバインディングなど setter を経由しない設定でも安全域が保たれ、変更が記録されます（補正の規則はコアの `Loupe.Limits`、設定値の変更ログはコアの `LoupeLogMessage` が単一所有し、拡大元画像の変更と描画の例外のログは `LoupeControl.UIBase` の `LoupeUiLogMessage` が単一所有します）。設定（`Options`）と寸法（`Options`）の数値項目はコアの型が生成時に補正するため、本コントロールは補正を登録しません（環境値の `DipToDeviceScaling` のみ `coerce` を登録します）。

## イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `Rendered` | `LoupeRenderedEventArgs` | 描画完了の通知（描画先キャンバス・一辺の物理ピクセル・切り出し領域の左上・スケール）。ホストはハンドラ内でキャンバスへオーバーレイを追加描画できる |

オーバーレイの描画位置は `(ドメイン座標 − CaptureTopLeft) × Scale` で求めます（破線の例は `FadeMaskDragRectControl.MewUI`）。

## ログ

`Log` プロパティへ `ILogger` を設定すると、設定変更と描画の例外を出力します（既定は出力なし）。書式は Avalonia 版と同一です。

| レベル | 出力例 |
| --- | --- |
| Debug | `[Feature.LoupeCore] Setting: ZoomLevel 4 → 8`（コアの設定。Source は `[LoupeControl.UIBase] Setting: Source ...`）/ `CenterPoint` / `RequestedSize` / `DipToDeviceScaling` / `ZoomLevel`。変化判定（同一値・同一インスタンスなら出力しない）は出力する主体のログ定義（Source は `LoupeControl.UIBase` の `LoupeUiLogMessage`、他はコアの `LoupeLogMessage`）が単一所有し、UI 層は同値判定を持たない。設定の変更は変化した項目だけを出力する） |
| Error | `[LoupeControl.UIBase] Update exception`（描画の例外。アプリは継続する） |

Avalonia 版の Trace ログ（再描画パイプライン）は持ちません（MewUI 版は中間ビットマップを作らず同期描画するため）。

## 構成

```text
src/LoupeControl.MewUI/
├── README.md                # 本ファイル
├── SPEC_ExtFunc.md          # 外部機能仕様書
├── SPEC_System.md           # システム仕様書
├── src/
│   ├── LoupeControl.MewUI.csproj   # net10.0 / MewUI 0.21.1 固定
│   ├── LoupeControl.cs             # Panel 派生のコントロール（子の図形キャンバスとラベル要素を配置する。描画は描画層へ委譲）
│   ├── LoupeRenderedEventArgs.cs   # 描画完了の通知（キャンバスと表示情報）
└── tests/
    ├── LoupeControl.MewUI.Tests.csproj
    ├── TestHelper.cs                   # テスト共通（描画先・画像生成）
    ├── RecordingLogger.cs              # ログ収集（テスト用）
    ├── LoupeControl.Tests.cs           # プロパティ補正・レイアウト
    └── LoupeRenderedEventArgs.Tests.cs # 描画完了通知の引数
```

## ビルドとテスト

```powershell
dotnet build src/LoupeControl.MewUI/src/LoupeControl.MewUI.csproj
dotnet test src/LoupeControl.MewUI/tests/LoupeControl.MewUI.Tests.csproj
```

本プロジェクトは `GenerateDocumentationFile` を有効にし、**存在しないメンバーへの `<see cref>` をビルド警告（CS1574）で検出**します（XML は検証にのみ使い、配布物へは出しません）。

## 関連ドキュメント

- `../Feature.LoupeCore/README.md` — 共有する UI 非依存ロジック
- `../LoupeControl.UIBase/README.md` — 共有する UI 非依存の描画層
- `../LoupeControl.AvaloniaUI/README.md` — Avalonia 版ルーペ
- `../app.MewUI/README.md` — 組み込み先のアプリ
