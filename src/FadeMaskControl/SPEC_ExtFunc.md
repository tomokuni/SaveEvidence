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
    - [4.2 外部レンダラーによるモード別描画](#42-外部レンダラーによるモード別描画)
  - [5. 公開API](#5-公開api)
    - [5.1 プロパティ](#51-プロパティ)
      - [5.1.1 FadeMaskControl](#511-fademaskcontrol)
      - [5.1.2 FadeMaskRenderer](#512-fademaskrenderer)
    - [5.2 メソッド](#52-メソッド)
    - [5.3 イベント](#53-イベント)
    - [5.4 型](#54-型)
      - [5.4.1 FadeMaskControl](#541-fademaskcontrol)
      - [5.4.2 FadeMaskRenderer](#542-fademaskrenderer)
      - [5.4.3 FadeMaskRenderMode](#543-fademaskrendermode)
    - [5.5 非暗転領域の閉区間仕様（始点・終点を含む）](#55-非暗転領域の閉区間仕様始点終点を含む)
    - [5.6 左上ラベル（3 行構成・繰り上げ）](#56-左上ラベル3-行構成繰り上げ)
  - [6. ログ](#6-ログ)
    - [6.1 LogLevel 設定ポリシー](#61-loglevel-設定ポリシー)
    - [6.2 ログ定義一覧](#62-ログ定義一覧)
  - [7. 最適化施策](#7-最適化施策)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `FadeMaskControl` |
| プロジェクト | `FadeMaskControl/src/FadeMaskControl.csproj` |
| 名前空間 | `Estable.AvaloniaUI.FadeMask` |
| 対象フレームワーク | `net10.0` |
| UI フレームワーク | Avalonia 12.1 |
| 主要基底クラス | `Avalonia.Controls.Control` |
| 主な用途 | 選択オーバーレイ描画（暗転マスク・選択枠線・サイズラベル・モード別描画） |

## 2. 目的

`FadeMaskControl` は、選択操作中のオーバーレイ描画を提供する独立した Avalonia コントロールです。

次の機能を提供します。

- 選択矩形の外側を暗転する穴あけマスクの描画
- 選択枠線・サイズラベルの描画
- 外部レンダラー（`FadeMaskRenderer`）への描画委譲（モード別描画の統一）
- モード別描画（AreaSelect / ScreenSelect / WindowSelect / Crop）

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\FadeMaskControl\src\FadeMaskControl.csproj" />
```

### 3.2 必要な名前空間

```csharp
using Estable.AvaloniaUI.FadeMask;
```

### 3.3 Visual Tree への配置

`FadeMaskControl` は `Control` です。

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:fm="clr-namespace:Estable.AvaloniaUI.FadeMask;assembly=FadeMaskControl">
    <Grid Background="LightGray">
        <fm:FadeMaskControl x:Name="Overlay"
                            LogicalSelectionRect="{Binding SelectionRect}"
                            OverlayColor="#96000000"
                            BorderColor="Red"
                            BorderThickness="2"
                            ShowSizeLabel="True"/>
    </Grid>
</Window>
```

## 4. 基本的な使用方法

### 4.1 依存関係プロパティによる描画

```csharp
var overlay = new FadeMaskControl
{
    LogicalSelectionRect = new Rect(50, 50, 200, 150),
    OverlayColor = Color.FromArgb(150, 0, 0, 0),
    BorderColor = Colors.Red,
    BorderThickness = 2.0,
    ShowSizeLabel = true,
};
panel.Children.Add(overlay);
```

### 4.2 外部レンダラーによるモード別描画

```csharp
var overlay = new FadeMaskControl { IsHitTestVisible = false, ZIndex = 50 };
var renderer = new FadeMaskRenderer
{
    Mode = FadeMaskRenderMode.WindowSelect,
    HasHighlight = true,
    LogicalHighlightRect = new Rect(10, 10, 200, 100),
    WindowTitle = "Sample Window",
};
overlay.ExternalRenderer = renderer;
overlay.InvalidateVisual(); // 再描画を要求
```

## 5. 公開API

公開 API は「プロパティ / メソッド / イベント / 型」の 4 分類で構成されます。
座標を扱う API は、ドメイン座標系 / DIP 座標系のどちらであるかを明記します。

### 5.1 プロパティ

#### 5.1.1 FadeMaskControl

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `LogicalSelectionRect` | `Rect?` | `null` | 選択矩形（**ドメイン座標系**）。`SetCoordinateTransform` で設定した座標変換ポートで描画時に DIP へ変換される（ウィンドウサイズ変更・ズーム・スクロールに追従） |
| `OverlayColor` | `Color` | `Color.FromArgb(0,0,0,0)` | 暗転オーバーレイの色 |
| `BorderColor` | `Color` | `DarkGray（#FFA9A9A9）` | 選択枠線の色（不透明度は色のアルファに統合） |
| `BorderThickness` | `double` | `2.0` | 選択枠線の太さ（DIP 座標系） |
| `TextColor` | `Color` | `White` | ラベルテキストの色 |
| `TextBackgroundColor` | `Color` | `Color.FromArgb(153,0,0,0)` | ラベル背景の色（不透明度は色のアルファに統合。既定は黒 60%） |
| `FontSize` | `double` | `14.0` | ラベルのフォントサイズ（DIP 座標系） |
| `ShowSizeLabel` | `bool` | `true` | サイズラベルの表示有無 |
| `ExternalRenderer` | `FadeMaskRenderer?` | `null` | 外部レンダラー（設定時は描画を完全委譲。設定時に既存の座標変換ポート・論理選択矩形をレンダラーへ同期） |

#### 5.1.2 FadeMaskRenderer

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Scaling` | `double` | `1.0` | DPI スケーリング係数 |
| `DisplayScaleScaling` | `double` | `Scaling` | サイズラベル用スケーリング（マルチスクリーン補正） |
| `TextColor` | `Color` | `White` | ラベルテキストの色 |
| `TextBackgroundColor` | `Color` | `Color.FromArgb(153,0,0,0)` | サイズラベル（1 行目）の背景色（不透明度は色のアルファに統合。既定は黒 60%） |
| `FontSize` | `double` | `14.0` | ラベルのフォントサイズ（DIP 座標系） |
| `OverlayArgbColor` | `Color` | `Color.FromArgb(128,0,0,0)` | 暗転オーバーレイの色（アルファ含む。既定は黒 50%） |
| `BorderColor` | `Color` | `DarkGray（#FFA9A9A9）` | 選択枠線の色（不透明度は色のアルファに統合） |
| `BorderThickness` | `double` | `2.0` | 選択枠線の太さ（DIP 座標系） |
| `InfoLabelColor` | `Color` | `White` | 情報ラベル（3 行目）の文字色 |
| `InfoLabelBackgroundColor` | `Color` | `Color.FromArgb(153,0,0,0)` | 情報ラベル（3 行目）の背景色（不透明度は色のアルファに統合。既定は黒 60%） |
| `InfoLabelFontSize` | `double` | `14.0` | 情報ラベル（3 行目）のフォントサイズ（DIP 座標系） |
| `ShowSubLines` | `bool` | `true` | 操作ラベル（2 行目）の表示有無 |
| `SubLineFontSize` | `double` | `14.0` | 操作ラベル（2 行目）のフォントサイズ（DIP 座標系） |
| `SubLineColor` | `Color` | `#DCDCDC` | 操作ラベル（2 行目）の文字色 |
| `SubLineBackgroundColor` | `Color` | `#99000000`（黒 60%） | 操作ラベル（2 行目）の背景色（不透明度はアルファに統合） |
| `LogicalHighlightRect` | `Rect?` | `null` | ハイライト矩形（WindowSelect / ScreenSelect・**ドメイン座標系**）。`CoordinateTransform` と組み合わせて描画時に DIP 変換 |
| `LogicalSelectionRect` | `Rect?` | `null` | 選択矩形（AreaSelect / Crop・**ドメイン座標系**）。`CoordinateTransform` と組み合わせて描画時に DIP 変換 |
| `CoordinateTransform` | `ICoordinatePort?` | `null` | 論理座標（ドメイン）↔ DIP の座標変換ポート。`LogicalSelectionRect` / `LogicalHighlightRect` と組み合わせて描画時に DIP 矩形を算出（全モード共通） |
| `HasHighlight` | `bool` | `false` | ハイライト有効 |
| `HasSelection` | `bool` | `false` | 選択有効 |
| `WindowTitle` | `string` | `""` | ハイライトされたウィンドウのタイトル |
| `ScreenIndex` | `int` | `-1` | 選択されたスクリーンのインデックス |
| `Mode` | `FadeMaskRenderMode` | `WindowSelect` | 描画モード |
| `InfoLabelVisible` | `bool` | `true` | 情報ラベル（3 行目）の表示有無 |
| `ShowSizeLabel` | `bool` | `true` | サイズラベル（1 行目）の表示有無 |
| `ShowSubLines` | `bool` | `true` | 操作ラベル（2 行目）の表示有無 |
| `SubLineFontSize` | `double` | `14.0` | 操作ラベル（2 行目）のフォントサイズ |
| `SubLineColor` | `Color` | `#DCDCDC` | 操作ラベル（2 行目）の文字色 |
| `SubLineBackgroundColor` | `Color` | `#99000000`（黒 60%） | 操作ラベル（2 行目）の背景色（不透明度はアルファに統合） |
| `SizeLabelOverrideProvider` | `Func<string?>?` | `null` | サイズラベルのテキストを描画時に評価するプロバイダ（null 時は矩形から自動生成） |
| `ShowFullOverlay` | `bool` | `false` | 選択なしでも全面暗転（Crop 用） |

### 5.2 メソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordinateTransform(ICoordinatePort?)` | ドメイン↔DIP 座標変換ポートを設定する（null = 等価変換。DragRectControl と同方式）。設定時は ExternalRenderer へも同期する |
| `FadeMaskRenderer.Draw(DrawingContext, Size)` | 全描画要素を描画する（`Size` はクライアントサイズ・**DIP 座標系**） |

### 5.3 イベント

| イベント | 説明 |
| --- | --- |
| StyledProperty の変更通知 | `AvaloniaPropertyChangedEventArgs` で変更を通知（`AffectsRender` により自動再描画） |

コントロールは公開イベントを独自に定義せず、StyledProperty の変更通知と外部レンダラー（`ExternalRenderer`）でホストと連携します。

### 5.4 型

#### 5.4.1 FadeMaskControl

公開コントロール（`public class : Control`）。
公開 API は「5.1.1 プロパティ」「5.2 メソッド」「5.3 イベント」に記載の通りです。

#### 5.4.2 FadeMaskRenderer

公開レンダラー（`public sealed class`・描画状態の真のソース）。
公開 API は「5.1.2 プロパティ」「5.2 メソッド」に記載の通りです。

#### 5.4.3 FadeMaskRenderMode

| 値 | 説明 |
| --- | --- |
| `AreaSelect` | 範囲選択（全画面オーバーレイ） |
| `ScreenSelect` | スクリーン選択（ハイライト + スクリーン番号ラベル） |
| `WindowSelect` | ウィンドウ選択（ハイライト + ウィンドウタイトルラベル） |
| `Crop` | 切出し（全面暗転 + 選択矩形） |

### 5.5 非暗転領域の閉区間仕様（始点・終点を含む）

**非暗転領域（穴）は始点・終点の両ピクセルを含む閉区間**、**暗転領域は始点・終点のいずれも含まない**ことを意図する仕様です。

- `LogicalSelectionRect`（ドメイン座標）を DIP へ変換する際は、終点（`Right` / `Bottom`）に +1 してから変換します（`Rect(Inverse(始点), Inverse(終点 + (1, 1)))`）。
- `FadeMaskDrawer.DrawOverlay` は受け取った矩形の外側（上・下・左・右）を暗転で塗りつぶすため、閉区間の矩形を受け取れば自動的に終点ピクセルも非暗転になります。
- これにより、DragRect の選択枠（閉区間描画）・キャプチャ（閉区間 +1）と表示が一致します。

### 5.6 左上ラベル（3 行構成・繰り上げ）

選択矩形・ハイライト矩形の左上に、最大 3 行のラベルを表示します。各行は**表示/非表示を個別に設定可能**で、非表示の行は詰めて描画されます（繰り上げ）。

| 行 | ラベル名 | 表示フラグ | 内容 | フォント/色 |
| --- | --- | --- | --- | --- |
| 1 行目 | サイズラベル | `ShowSizeLabel` | `LT: (x, y), RB: (x, y), WH: (w, h), DisplayScale: (n%)` または `SizeLabelOverride` | `FontSize` / `TextColor` / `TextBackgroundColor` |
| 2 行目 | 操作ラベル | `ShowSubLines` | モード別の確定・キャンセル方法（Enter / Esc / クリック / ダブルクリック） | `SubLineFontSize` / `SubLineColor` / `SubLineBackgroundColor` |
| 3 行目 | 情報ラベル | `InfoLabelVisible` | WindowSelect のウィンドウタイトル | `InfoLabelFontSize` / `InfoLabelColor` / `InfoLabelBackgroundColor` |

**背景の合成**: 各行の背景色（`TextBackgroundColor` / `SubLineBackgroundColor` / `InfoLabelBackgroundColor`）のアルファで不透明度を表す（例: 黒 60% = `#99000000`）。背景色の既定値は黒 60%（#99000000）。

**繰り上げ規則**: サイズラベルが非表示なら操作ラベルが 1 行目、情報ラベルが 2 行目に描画されます（以降も同様）。操作ラベルが非表示なら情報ラベルが 2 行目になります。

**操作ラベルの内容（モード別）**:

- ScreenSelect / WindowSelect: `Enter / クリック: 確定  Esc: キャンセル`
- AreaSelect / Crop: `ダブルクリック: 確定  Esc / 矩形外クリック: キャンセル`

**注記**: 情報ラベル（ウィンドウタイトル / スクリーン番号）は左上のラベル領域（3 行目）に集約して表示します。

## 6. ログ

本コントロールのログ定義は `Logging/FadeMaskLog.cs`（LoggerMessage ソース生成）に集約し、`ILogger` 経由で出力する。
描画専用コントロールのため、マウス・キーイベントのログはない（状態の変更のみ記録）。
出力先・レベル制御はホストアプリケーションが所有する（本クラスは定義のみ）。

### 6.1 LogLevel 設定ポリシー

トレースログは LogLevel.Trace に限定せず、「重要度 × 発生頻度」でレベルを使い分ける。

| LogLevel | 対象 | 本番での意図 |
| --- | --- | --- |
| `Error` | 例外・失敗（処理継続不可） | 常時出力し、障害を確実に残す |
| `Warning` | 異常だがフォールバックで継続 | 常時出力し、異常発生の痕跡を残す |
| `Information` | ユーザー操作の主要イベント（低頻度） | 常時出力し、操作手順の再現を可能にする |
| `Debug` | 状態変更の詳細（選択矩形 / レンダラー） | 障害時のみ有効化して解析 |
| `Trace` | 高頻度・極詳細（座標変換設定） | 特別な解析時のみ有効化 |

判断基準:

1. 例外・警告は常時残す（`Error` / `Warning`）
2. 操作の起点イベント（低頻度）は `Information`
3. マウス・キー等の操作イベント（中頻度）は `Debug`
4. 高頻度・極詳細は `Trace`
5. 設定変更（変更前後）は `Debug`

### 6.2 ログ定義一覧

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Debug` | `FadeMaskLog.LogSelectionRectChanged` | 論理選択矩形（ドメイン座標）の変更（変更前後） |
| `Debug` | `FadeMaskLog.LogExternalRenderer` | 外部レンダラーの設定 / 解除 |
| `Trace` | `FadeMaskLog.LogCoordinateTransform` | 座標変換ポートの設定 |

## 7. 最適化施策

| 施策 | 内容 |
| --- | --- |
| AffectsRender 登録 | 描画に影響する StyledProperty のみ再描画をトリガー（不要な再描画を抑制） |
| 共通描画ロジックの一元化 | 暗転マスク・サイズラベルは `FadeMaskDrawer` に集約（`FadeMaskControl` と `FadeMaskRenderer` で共用・DRY） |
| ブラシの即時生成 | 描画毎に `SolidColorBrush` を生成（状態がプロパティで完結するためキャッシュ不要・シンプル優先） |
| モード判定の単一化 | 描画要素の切り替えは `FadeMaskRenderMode` の列挙で判定（bool フラグ分岐なし・可読性向上） |
