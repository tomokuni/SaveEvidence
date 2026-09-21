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
| 枠線 | 穴あけ矩形の四辺を内側に描く（`GetBorderRects` が単一所有） |
| 入力 | **受け取らない**。ホストが `IsHitTestVisible = false` にして下のコントロールへ入力を通す |
| DPI 追従 | `OnDpiChanged` で表示スケーリング係数を `newDpi / 96` へ自動更新する |
| 連続再描画 | **無効**。プロパティ変更時にのみ再描画する |
| ラベル | サイズラベル（1 行目）とサブライン（2 行目以降）を選択矩形の左上へ縦に積む。縦位置の計算は `FadeMaskLabelLayout`（コア）が単一所有する |
| 日本語のラベル | 非 ASCII を含む場合は字形を持つフォントへ自動フォールバックする（既定のフォントに日本語のグリフが無い環境があるため） |

## インストール（プロジェクト参照）

```xml
<ProjectReference Include="..\..\FadeMaskControl.MewUI\src\FadeMaskControl.MewUI.csproj" />
```

パッケージは `Aprillz.MewUI.Skia` を参照します（バージョンは `MewUIVersion` プロパティで 0.21.1 に固定）。

## 使い方

```csharp
using Estable.MewUI.FadeMask;
using EsUtil.Platform.Core.Geometry;

var overlay = new FadeMaskControl
{
    IsHitTestVisible = false,                        // 入力を下へ通す
    SelectionRect = new EsRect(100, 80, 320, 240),   // 論理座標（元画像ピクセル）
    HasSelection = true,
    OverlayColor = System.Drawing.Color.FromArgb(128, 0, 0, 0),
    BorderColor = System.Drawing.Color.FromArgb(255, 169, 169, 169),
    BorderThickness = 2,
};
```

ラベルを表示する場合は、文言の生成をホスト（またはコアの `FadeMaskLabels`）へ委ねます。

```csharp
overlay.SizeLabelTextProvider = () =>
    overlay.SelectionRect is { } rect ? FadeMaskLabels.FormatSizeLabel(rect, scaling: 1.0) : null;
overlay.SubLines =
[
    new FadeMaskSubLine("ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル", 12, System.Drawing.Color.White),
];
```

## プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectionRect` | `EsRect?` | `null` | 論理座標（ドメイン）の選択矩形 |
| `HighlightRect` | `EsRect?` | `null` | 論理座標のハイライト矩形。選択矩形より優先される |
| `HasSelection` | `bool` | `false` | true の場合、`SelectionRect` を穴あけ矩形として使う |
| `HasHighlight` | `bool` | `false` | true の場合、`HighlightRect` を穴あけ矩形として使う（選択より優先） |
| `OverlayColor` | `Color` | 黒 50% | 暗転の色。アルファで不透明度を表す（0 の場合は暗転しない） |
| `BorderColor` | `Color` | ダークグレー | 枠線の色。アルファで不透明度を表す（0 の場合は枠線を描画しない） |
| `BorderThickness` | `double` | 2.0 | 枠線の太さ（DIP）。0.5 未満は 0.5 に正規化される |
| `ShowFullOverlay` | `bool` | `false` | true の場合は選択が無くても全面を暗転する |
| `RenderScaling` | `double` | 1.0 | DIP → 物理ピクセルの係数。DPI 変更で自動更新される |
| `Scaling` | `double` | 1.0 | 座標変換ポートへ適用する倍率（マルチスクリーン補正等。`Port` が未設定の場合のみ使用） |
| `Port` | `ICoordTransform?` | `null` | 論理座標 → DIP の座標変換ポート（設定時は `Scaling` より優先） |
| `ShowSizeLabel` | `bool` | `true` | サイズラベルを表示するか |
| `SizeLabelTextProvider` | `Func<string?>?` | `null` | サイズラベルの文言を返すデリゲート。null または空文字を返すとラベルを描画しない |
| `SizeLabelFontSize` | `double` | 14.0 | サイズラベルのフォントサイズ（DIP） |
| `SizeLabelColor` | `Color` | 白 | サイズラベルの文字色 |
| `SizeLabelBackgroundColor` | `Color` | 黒 60% | サイズラベルの背景色（アルファ 0 の場合は黒 50% へフォールバック） |
| `SubLines` | `IReadOnlyList<FadeMaskSubLine>?` | `null` | 2 行目以降の行。`FadeMaskSubLine`（文言・文字サイズ・文字色・背景色）を上から順に積む |
| `Log` | `ILogger` | `NullLogger` | 状態変更ログの出力先（null は `NullLogger`。Avalonia 版と同一の書式） |

`Color` は `System.Drawing.Color` です。

## 描画仕様

| 状況 | 描画 |
| --- | --- |
| 穴あけ矩形あり | 暗転（帯 4 領域）+ 枠線（4 辺） |
| 穴あけ矩形なし + `ShowFullOverlay` | 暗転（全面のみ）。枠線なし |
| 穴あけ矩形なし + 暗転なし | 何も描画しない |
| 穴あけ矩形がクライアント外 | 全面を暗転（描画漏れを防ぐ） |
| 穴あけ矩形あり + ラベル表示 | 穴あけ矩形の左上に「サイズラベル（1 行目）→サブライン（2 行目以降）」を縦に積む（行間は内側余白） |
| サイズラベルを非表示 / 文言が空 | サブラインを 1 行目から描画する（繰り上げ） |

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
│   ├── FadeMaskControl.cs            # SkiaCanvasView 派生のオーバーレイ
│   ├── FadeMaskSubLine.cs            # サブライン（2 行目以降）の型
│   └── FadeMaskSkiaRenderer.cs       # 暗転・枠線・ラベルの描画
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
