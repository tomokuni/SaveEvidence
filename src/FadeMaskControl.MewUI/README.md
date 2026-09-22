# FadeMaskControl.MewUI

`FadeMaskControl.AvaloniaUI`（Avalonia UI 実装）と同じ **UI 非依存コア（`FadeMaskControl.Core`）** を使い、描画層のみを **MewUI** で実装した、領域外を暗転させるオーバーレイコントロールです。

## 目的

画像やプレビューの上に重ね、**選択領域以外を暗転**させて視認性を高めます。
暗転の幾何計算（穴あけ矩形の決定・四辺の帯への分割・枠線矩形）は `FadeMaskControl.Core` が単一所有するため、**Avalonia 版と同じ見た目**になります。

## 特徴

| 項目 | 内容 |
| --- | --- |
| 基底クラス | `Aprillz.MewUI.Skia.Controls.SkiaCanvasView`（SkiaSharp で直接描画する） |
| 描画 | 「クライアント矩形 − 穴あけ矩形」の帯のみを塗る（穴あけ部分は描画しない） |
| 枠線 | 穴あけ矩形の四辺を内側に描く（コアの `FadeMaskOverlayGeometry.GetBorderRects` が単一所有） |
| 入力 | **受け取らない**。装飾専用のため入力の判断を持たない（ホストが `IsHitTestVisible = false` にして下のコントロールへ入力を通す） |
| 描画判断 | どの矩形を暗転するか・枠線を描くか・ラベルをどう組むかは**コア（`FadeMaskFrame`）が単一所有**する。UI は返されたフレームを描くだけ |
| 外観 | **1 つの値（`Appearance`）で設定する**。既定値はコアの `FadeMaskAppearance` が単一所有し、Avalonia 版と一致する |
| DPI 追従 | `OnDpiChanged` で表示スケーリング係数を `newDpi / 96` へ自動更新する |
| 連続再描画 | **無効**。プロパティ変更時にのみ再描画する |
| ラベル | サイズラベル（1 行目）とサブライン（2 行目以降）を穴あけ矩形の左上へ縦に積む。縦位置の計算は `FadeMaskLabelLayout`（コア）が単一所有する |
| 日本語のラベル | 非 ASCII を含む場合は字形を持つフォントへ自動フォールバックする（既定のフォントに日本語のグリフが無い環境があるため） |

## インストール（プロジェクト参照）

```xml
<ProjectReference Include="..\..\FadeMaskControl.MewUI\src\FadeMaskControl.MewUI.csproj" />
```

パッケージは `Aprillz.MewUI.Skia` を参照します（バージョンは `MewUIVersion` プロパティで 0.21.1 に固定）。

## 使い方

```csharp
using Estable.FadeMask;
using Estable.MewUI.FadeMask;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

var overlay = new FadeMaskControl
{
    IsHitTestVisible = false,                        // 入力を下へ通す
    LogicalSelectionRect = new EsRect(100, 80, 320, 240),   // 論理座標（元画像ピクセル）
    HasSelection = true,
    // 外観は 1 つの値（コアの FadeMaskAppearance）で設定する（部分変更は with 式）
    Appearance = new FadeMaskAppearance
    {
        OverlayColor = DrawingColor.FromArgb(128, 0, 0, 0),
        BorderColor = DrawingColor.FromArgb(255, 169, 169, 169),
        BorderThickness = 2,
    },
};
```

ラベルを表示する場合は、サイズラベルの文言の生成をホスト（コアの `FadeMaskLabels.FormatSizeLabel` を使う）へ委ねます。
サブライン（操作ラベル・情報ラベル）の文言・順序・表示可否は `Appearance.BuildSubLines` が組み立てます（描画モードとウィンドウタイトルを渡す）。

```csharp
overlay.SizeLabelOverrideProvider = () =>
    overlay.LogicalSelectionRect is { } rect ? FadeMaskLabels.FormatSizeLabel(rect, scaling: 1.0) : null;
overlay.DisplayScaleScaling = 1.25;   // 自動生成時の DisplayScale（プロバイダを設定する場合は不要）
overlay.Mode = FadeMaskRenderMode.WindowSelect;
overlay.WindowTitle = "メモ帳";
```

## プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `LogicalSelectionRect` | `EsRect?` | `null` | 論理座標（ドメイン）の選択矩形 |
| `LogicalHighlightRect` | `EsRect?` | `null` | 論理座標のハイライト矩形。選択矩形より優先される |
| `HasSelection` | `bool` | `false` | true の場合、`LogicalSelectionRect` を穴あけ矩形として使う |
| `HasHighlight` | `bool` | `false` | true の場合、`LogicalHighlightRect` を穴あけ矩形として使う（選択より優先） |
| `Appearance` | `FadeMaskAppearance` | コアの既定値 | 外観（暗転色・枠線・ラベル）の単一所有。変更すると自動で再描画される |
| `WindowTitle` | `string` | `""` | 情報ラベル（3 行目）の文言 |
| `Mode` | `FadeMaskRenderMode` | `AreaSelect` | 描画モード（操作ラベルの文言選定・情報ラベルの表示可否） |
| `ShowFullOverlay` | `bool` | `false` | true の場合は選択が無くても全面を暗転する |
| `RenderScaling` | `double` | 1.0 | DIP → 物理ピクセルの係数。DPI 変更で自動更新される |
| `Scaling` | `double` | 1.0 | 座標変換ポートへ適用する倍率（`Port` が未設定の場合のみ使用。使い分けの規則はコアが単一所有） |
| `DisplayScaleScaling` | `double` | `Scaling` | サイズラベルの自動生成に使う表示スケール（0 以下は `Scaling` と同値） |
| `Port` | `ICoordTransform?` | `null` | 論理座標 → DIP の座標変換ポート（設定時は `Scaling` より優先） |
| `SizeLabelOverrideProvider` | `Func<string?>?` | `null` | サイズラベルの文言を返すデリゲート。null または空文字を返すと矩形から自動生成する |
| `Log` | `ILogger` | `NullLogger` | 状態変更ログの出力先（null は `NullLogger`。Avalonia 版と同一の書式） |

外観の個別項目（暗転色・枠線・ラベル）は `Appearance`（コアの `FadeMaskAppearance`。色は `System.Drawing.Color`）が所有します。

## 描画仕様

| 状況 | 描画 |
| --- | --- |
| 穴あけ矩形あり | 暗転（帯 4 領域）+ 枠線（4 辺）+ ラベル |
| 穴あけ矩形なし + `ShowFullOverlay` | 暗転（全面のみ）。枠線・ラベルなし |
| 穴あけ矩形なし + 暗転なし | 何も描画しない |
| 穴あけ矩形がクライアント外 | 全面を暗転（描画漏れを防ぐ） |
| 穴あけ矩形あり + ラベル表示 | 穴あけ矩形の左上に「サイズラベル（1 行目）→操作ラベル（2 行目）→情報ラベル（3 行目）」を縦に積む（行間は内側余白） |
| サイズラベルの文言なし | サブラインを 1 行目から描画する（繰り上げ） |

## 状態変更ログ

`Log` プロパティへ `ILogger` を設定すると、描画状態の変更を出力します（既定は出力なし）。書式は Avalonia 版と同一です。

| レベル | 出力内容 |
| --- | --- |
| Debug | `[FadeMask] SelectionRect: null → (10, 20, 30, 40)` / `[FadeMask] HighlightRect: ...` |
| Trace | `[FadeMask] CoordinateTransform: True` |

同じ値の再設定では出力しません（外観設定の再適用でログが溢れないようにする）。

## 構成

```text
src/FadeMaskControl.MewUI/
├── README.md                # 本ファイル
├── SPEC_ExtFunc.md          # 外部機能仕様書
├── SPEC_System.md           # システム仕様書
├── src/
│   ├── FadeMaskControl.MewUI.csproj  # net10.0 / MewUI 0.21.1 固定
│   ├── FadeMaskControl.cs            # SkiaCanvasView 派生のオーバーレイ（状態・外観・フレーム解決の呼び出し）
│   └── FadeMaskSkiaRenderer.cs       # フレーム（暗転・枠線・ラベル）の描画
└── tests/
    ├── FadeMaskControl.MewUI.Tests.csproj
    ├── TestHelper.cs                 # テスト共通（描画先・画素数え上げ）
    └── FadeMaskSkiaRenderer.Tests.cs # 描画結果の画素検証（26 件）
```

## ビルドとテスト

```powershell
dotnet build src/FadeMaskControl.MewUI/src/FadeMaskControl.MewUI.csproj
dotnet test src/FadeMaskControl.MewUI/tests/FadeMaskControl.MewUI.Tests.csproj
```

## 関連ドキュメント

- `../FadeMaskControl.Core/README.md` — 共有する UI 非依存ロジック
- `../FadeMaskControl/README.md` — Avalonia 版フェードマスク
