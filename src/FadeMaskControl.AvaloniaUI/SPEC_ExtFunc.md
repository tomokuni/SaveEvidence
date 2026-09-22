# FadeMaskControl 外部機能仕様書

## 目次

- [FadeMaskControl 外部機能仕様書](#fademaskcontrol-外部機能仕様書)
  - [目次](#目次)
  - [1. 文書概要](#1-文書概要)
  - [2. 目的](#2-目的)
  - [3. 外部利用条件](#3-外部利用条件)
    - [3.1 プロジェクト参照](#31-プロジェクト参照)
    - [3.2 必要な名前空間](#32-必要な名前空間)
    - [3.3 Visual Tree への配置](#33-visual-tree-への配置)
  - [4. 基本的な使用方法](#4-基本的な使用方法)
    - [4.1 依存関係プロパティによる描画](#41-依存関係プロパティによる描画)
    - [4.2 モード別描画（ハイライト・ウィンドウタイトル）](#42-モード別描画ハイライトウィンドウタイトル)
  - [5. 公開API](#5-公開api)
    - [5.1 プロパティ](#51-プロパティ)
      - [5.1.1 FadeMaskControl](#511-fademaskcontrol)
    - [5.2 メソッド](#52-メソッド)
    - [5.3 イベント](#53-イベント)
    - [5.4 型](#54-型)
      - [5.4.1 FadeMaskControl](#541-fademaskcontrol)
      - [5.4.2 FadeMaskAppearance](#542-fademaskappearance)
      - [5.4.3 FadeMaskRenderMode](#543-fademaskrendermode)
    - [5.5 非暗転領域の閉区間仕様（始点・終点を含む）](#55-非暗転領域の閉区間仕様始点終点を含む)
    - [5.6 左上ラベル（3 行構成・繰り上げ）](#56-左上ラベル3-行構成繰り上げ)

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `FadeMaskControl` |
| プロジェクト | `FadeMaskControl.AvaloniaUI/src/FadeMaskControl.AvaloniaUI.csproj` |
| 名前空間 | `Estable.AvaloniaUI.FadeMask`（外観・モードは `Estable.FadeMask`） |
| 対象フレームワーク | `net10.0` |
| UI フレームワーク | Avalonia 12.1 |
| 主要基底クラス | `Avalonia.Controls.Control` |
| 主な用途 | 選択オーバーレイ描画（暗転マスク・選択枠線・ラベル・モード別描画） |

## 2. 目的

`FadeMaskControl` は、選択操作中のオーバーレイ描画を提供する独立した Avalonia コントロールです。

次の機能を提供します。

- 穴あけ矩形の外側を暗転する穴あけマスクの描画
- 選択枠線・ラベル（サイズ・操作・情報）の描画
- 外観の単一所有（`Appearance` = コアの `FadeMaskAppearance`）
- モード別描画（AreaSelect / ScreenSelect / WindowSelect / Crop）

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\FadeMaskControl.AvaloniaUI\src\FadeMaskControl.AvaloniaUI.csproj" />
```

### 3.2 必要な名前空間

```csharp
using Estable.AvaloniaUI.FadeMask;
```

### 3.3 Visual Tree への配置

`FadeMaskControl` は `Control` です。

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:fm="clr-namespace:Estable.AvaloniaUI.FadeMask;assembly=FadeMaskControl.AvaloniaUI">
    <Grid Background="LightGray">
        <fm:FadeMaskControl x:Name="Overlay"
                            LogicalSelectionRect="{Binding SelectionRect}"
                            HasSelection="True"
                            Appearance="{Binding Appearance}"/>
    </Grid>
</Window>
```

## 4. 基本的な使用方法

### 4.1 依存関係プロパティによる描画

```csharp
// 外観は 1 つの値（コアの FadeMaskAppearance）で設定する（部分変更は with 式）
var overlay = new FadeMaskControl
{
    LogicalSelectionRect = new Rect(50, 50, 200, 150),
    HasSelection = true,
    Appearance = new FadeMaskAppearance
    {
        OverlayColor = Color.FromAColor(150, Colors.Black).ToDrawingColor(),
        BorderColor = Colors.Red.ToDrawingColor(),
        BorderThickness = 2,
    },
};
panel.Children.Add(overlay);
```

### 4.2 モード別描画（ハイライト・ウィンドウタイトル）

```csharp
var overlay = new FadeMaskControl { IsHitTestVisible = false, ZIndex = 50 };
overlay.Appearance = overlay.Appearance with
{
    OverlayColor = Color.FromAColor(150, Colors.Black).ToDrawingColor(),
    BorderColor = Colors.Red.ToDrawingColor(),
};
overlay.Mode = FadeMaskRenderMode.WindowSelect;
overlay.HasHighlight = true;
overlay.LogicalHighlightRect = new Rect(10, 10, 200, 100);
overlay.WindowTitle = "Sample Window";
overlay.InvalidateVisual(); // 再描画を要求（プロパティ変更でも自動で再描画される）
```

## 5. 公開API

公開 API は「プロパティ / メソッド / イベント / 型」の 4 分類で構成されます。
座標を扱う API は、ドメイン座標系 / DIP 座標系のどちらであるかを明記します。

### 5.1 プロパティ

#### 5.1.1 FadeMaskControl

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Appearance` | `FadeMaskAppearance` | コアの既定値 | 外観（暗転色・枠線・ラベル）の単一所有。変更すると自動で再描画される |
| `LogicalSelectionRect` | `Rect?` | `null` | 選択矩形（**ドメイン座標系**）。`SetCoordinateTransform` で設定した座標変換ポートで描画時に DIP へ変換される（ウィンドウサイズ変更・ズーム・スクロールに追従） |
| `LogicalHighlightRect` | `Rect?` | `null` | ハイライト矩形（WindowSelect / ScreenSelect・**ドメイン座標系**）。選択矩形より優先される |
| `HasSelection` | `bool` | `false` | 選択が有効か（true のとき `LogicalSelectionRect` を穴あけ矩形にする） |
| `HasHighlight` | `bool` | `false` | ハイライトが有効か（true のとき `LogicalHighlightRect` を穴あけ矩形にする。選択より優先） |
| `WindowTitle` | `string` | `""` | 情報ラベル（3 行目）の文言（ハイライト中のウィンドウタイトル） |
| `Mode` | `FadeMaskRenderMode` | `AreaSelect` | 描画モード（操作ラベルの文言選定・情報ラベルの表示可否） |
| `ShowFullOverlay` | `bool` | `false` | 穴あけ矩形が無くても全面を暗転する（範囲選択のドラッグ前・切出しの初期状態用） |
| `Scaling` | `double` | `1.0` | DPI スケーリング係数（ドメイン → DIP の基準倍率） |
| `DisplayScaleScaling` | `double` | `Scaling` | サイズラベル用スケーリング（マルチスクリーン補正。0 以下は `Scaling` と同値） |
| `SizeLabelOverrideProvider` | `Func<string?>?` | `null` | サイズラベルのテキストを描画時に評価するプロバイダ（null・空文字を返した場合は矩形と `DisplayScaleScaling` から自動生成。デリゲートのため AXAML バインド不可） |
| `Log` | `ILogger` | `NullLogger` | 内部ロガー（null 設定時は `NullLogger` へフォールバック。出力先・レベル制御はホストが所有） |

外観の個別項目（暗転色・枠線・ラベル）は `Appearance` が所有します。

### 5.2 メソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordinateTransform(ICoordTransform?)` | ドメイン↔DIP 座標変換ポートを設定する（null = 等価変換。DragRectControl と同方式） |

### 5.3 イベント

| イベント | 説明 |
| --- | --- |
| StyledProperty の変更通知 | `AvaloniaPropertyChangedEventArgs` で変更を通知（`AffectsRender` により自動再描画） |

コントロールは公開イベントを独自に定義せず、StyledProperty の変更通知でホストと連携します。

### 5.4 型

#### 5.4.1 FadeMaskControl

公開コントロール（`public class : Control`）。
公開 API は「5.1.1 プロパティ」「5.2 メソッド」「5.3 イベント」に記載の通りです。

#### 5.4.2 FadeMaskAppearance

外観の値型（コアの `FadeMaskControl.Core`・`Estable.FadeMask` 名前空間。`public sealed record`）。
色は UI 非依存の `System.Drawing.Color` です。変更は `with` 式による部分変更です。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `OverlayColor` | `System.Drawing.Color` | `#80000000`（黒 50%） | 暗転オーバーレイの色（アルファ 0 の場合は暗転しない） |
| `BorderColor` | `System.Drawing.Color` | `#FFA9A9A9`（ダークグレー） | 選択枠線の色（アルファ 0 の場合は枠線を描画しない） |
| `BorderThickness` | `double` | `2.0` | 選択枠線の太さ（DIP 座標系。0.5 未満は 0.5 に正規化） |
| `ShowSizeLabel` | `bool` | `true` | サイズラベル（1 行目）の表示有無 |
| `SizeLabelColor` | `System.Drawing.Color` | `White` | サイズラベル（1 行目）の文字色 |
| `SizeLabelBackgroundColor` | `System.Drawing.Color` | `#99000000`（黒 60%） | サイズラベル（1 行目）の背景色（不透明度はアルファに統合。アルファ 0 は黒 50% として描画） |
| `SizeLabelFontSize` | `double` | `14.0` | サイズラベル（1 行目）のフォントサイズ（DIP 座標系） |
| `ShowSubLines` | `bool` | `true` | 操作ラベル（2 行目）の表示有無 |
| `SubLineColor` | `System.Drawing.Color` | `#FFDCDCDC`（薄いグレー） | 操作ラベル（2 行目）の文字色 |
| `SubLineBackgroundColor` | `System.Drawing.Color` | `#99000000`（黒 60%） | 操作ラベル（2 行目）の背景色（不透明度はアルファに統合。アルファ 0 は黒 50% として描画） |
| `SubLineFontSize` | `double` | `14.0` | 操作ラベル（2 行目）のフォントサイズ（DIP 座標系） |
| `InfoLabelVisible` | `bool` | `true` | 情報ラベル（3 行目）の表示有無（WindowSelect かつタイトル非空のときのみ表示） |
| `InfoLabelColor` | `System.Drawing.Color` | `White` | 情報ラベル（3 行目）の文字色 |
| `InfoLabelBackgroundColor` | `System.Drawing.Color` | `#99000000`（黒 60%） | 情報ラベル（3 行目）の背景色（不透明度はアルファに統合。アルファ 0 は黒 50% として描画） |
| `InfoLabelFontSize` | `double` | `14.0` | 情報ラベル（3 行目）のフォントサイズ（DIP 座標系） |

`BuildSubLines(FadeMaskRenderMode mode, string windowTitle)` が、上記の設定からラベルの行（2 行目以降）を組み立てます（文言の選定はコアが単一所有）。

#### 5.4.3 FadeMaskRenderMode

| 値 | 説明 |
| --- | --- |
| `AreaSelect` | 範囲選択（全画面オーバーレイ） |
| `ScreenSelect` | スクリーン選択（ハイライト） |
| `WindowSelect` | ウィンドウ選択（ハイライト + ウィンドウタイトルラベル） |
| `Crop` | 切出し（全面暗転 + 選択矩形） |

### 5.5 非暗転領域の閉区間仕様（始点・終点を含む）

**非暗転領域（穴）は始点・終点の両ピクセルを含む閉区間**、**暗転領域は始点・終点のいずれも含まない**ことを意図する仕様です。

- `LogicalSelectionRect`（ドメイン座標）を DIP へ変換する際は、終点（`Right` / `Bottom`）に +1 してから変換します（`Rect(Inverse(始点), Inverse(終点 + (1, 1)))`）。
- `LogicalHighlightRect`（ScreenSelect / WindowSelect のスクリーン・ウィンドウ境界）は、検出結果（PixelRect）が最初から**全ピクセルを含む閉区間表現**のため、+1 しません（`closedInterval: false`。+1 すると実際より 1px 大きい枠・サイズラベルになる）。
- 暗転マスクは穴あけ矩形の外側（上・下・左・右）を塗るため、閉区間の矩形を受け取れば自動的に終点ピクセルも非暗転になります（計算はコアの `FadeMaskOverlayGeometry` が単一所有）。
- これにより、DragRect の選択枠（閉区間描画）・キャプチャ（閉区間 +1）と表示が一致します。

### 5.6 左上ラベル（3 行構成・繰り上げ）

穴あけ矩形（選択矩形・ハイライト矩形）の左上に、最大 3 行のラベルを表示します。各行は**表示/非表示を個別に設定可能**で、非表示の行は詰めて描画されます（繰り上げ）。

| 行 | ラベル名 | 表示フラグ | 内容 | フォント/色 |
| --- | --- | --- | --- | --- |
| 1 行目 | サイズラベル | `Appearance.ShowSizeLabel` | `SizeLabelOverrideProvider` の戻り値、または矩形と `DisplayScaleScaling` から自動生成した `(L, T) - (R, B),  (W×H), DisplayScale: (n%)` | `SizeLabelFontSize` / `SizeLabelColor` / `SizeLabelBackgroundColor` |
| 2 行目 | 操作ラベル | `Appearance.ShowSubLines` | モード別の確定・キャンセル方法（Enter / Esc / クリック / ダブルクリック） | `SubLineFontSize` / `SubLineColor` / `SubLineBackgroundColor` |
| 3 行目 | 情報ラベル | `Appearance.InfoLabelVisible` | WindowSelect のウィンドウタイトル（`WindowTitle`） | `InfoLabelFontSize` / `InfoLabelColor` / `InfoLabelBackgroundColor` |

**背景の合成**: 各行の背景色（`SizeLabelBackgroundColor` / `SubLineBackgroundColor` / `InfoLabelBackgroundColor`）のアルファで不透明度を表す（例: 黒 60% = `#99000000`）。背景色のアルファが 0 の場合は黒 50%（`#80000000`）として描画します（実効色の解決はコアが行います）。

**繰り上げ規則**: サイズラベルが非表示なら操作ラベルが 1 行目、情報ラベルが 2 行目に描画されます（以降も同様）。操作ラベルが非表示なら情報ラベルが 2 行目になります。

**操作ラベルの内容（モード別）**:

- ScreenSelect / WindowSelect: `Enter / クリック: 確定  Esc: キャンセル`
- AreaSelect / Crop: `ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル`

**注記**: 情報ラベル（ウィンドウタイトル）は左上のラベル領域（3 行目）に集約して表示します。
