# ColorPickerControl システム仕様書（SPEC_System）

## 目次

- [ColorPickerControl システム仕様書（SPEC_System）](#colorpickercontrol-システム仕様書spec_system)
  - [目次](#目次)
  - [1. システム概要](#1-システム概要)
  - [2. 技術スタック](#2-技術スタック)
  - [3. アーキテクチャ](#3-アーキテクチャ)
  - [4. 例外処理ポリシー](#4-例外処理ポリシー)
  - [5. コンポーネント図](#5-コンポーネント図)
  - [6. コンポーネント一覧](#6-コンポーネント一覧)
    - [6.1 ColorPickerControl](#61-colorpickercontrol)
    - [6.2 ColorPicker.axaml](#62-colorpickeraxaml)
    - [6.3 ColorEntry](#63-colorentry)
    - [6.4 ColorSortMode](#64-colorsortmode)
    - [6.5 StringToBrushConverter](#65-stringtobrushconverter)
    - [6.6 ColorString](#66-colorstring)
    - [6.7 ColorDisplayConverter](#67-colordisplayconverter)
  - [7. 依存ルール](#7-依存ルール)
  - [8. バリデーション状態遷移](#8-バリデーション状態遷移)
  - [9. ライフサイクル](#9-ライフサイクル)

---

## 1. システム概要

SaveEvidence は、Windows デスクトップ上の画面をキャプチャし、プレビュー・編集・保存するための Avalonia UI アプリケーションです（.NET 10.0 / C# 14 / MVVM）。

本プロジェクト（ColorPickerControl）は、このシステムのうち**色選択 UI**（色名 / 16進入力 + ドロップダウン + カラーピッカー）を担う再利用可能なコントロールです。app の設定画面などから双方向バインディングで利用されます。

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| **プロジェクト** | `ColorPickerControl` |
| **ルート名前空間** | `Estable.AvaloniaUI.ColorPicker` |
| **言語** | C# 14（.NET 10.0） |
| **UI フレームワーク** | Avalonia UI 12.1（UserControl / ComboBox / ColorView / Flyout） |
| **ターゲットフレームワーク** | net10.0 |
| **ビルドツール** | MSBuild / .NET SDK 10.0 |
| **ログ** | Microsoft.Extensions.Logging.Abstractions（LoggerMessage ソース生成） |
| **テスト** | xUnit v3 / Avalonia.Headless.XUnit |
| **依存プロジェクト** | なし（Avalonia.Controls.ColorPicker の ColorView を利用） |

## 3. アーキテクチャ

### 3.1 レイヤー構造

```text
公開コントロール（ColorPickerControl: ColorPicker.axaml.cs）
  → ドメイン（ColorEntry / ColorSortMode）/ コンバーター（StringToBrushConverter）
```

### 3.2 依存の方向

- 依存は**一方向の階層構造**（XAML → コントロール → ドメイン / コンバーター）。
- 公開 API（StyledProperty・公開プロパティ）は ColorPicker.axaml.cs に集約し、非公開実装も同ファイル内に隠蔽する（無理なファイル分割・内部公開を解消）。
- 状態管理はすべて StyledProperty（Avalonia プロパティシステム）に集約する。
- ドメインロジック（ColorEntry の色計算）は UI 非依存（Avalonia.Media のみ）。

## 4. 例外処理ポリシー

### 4.1 基本方針

- ライブラリは例外を原則伝播し、上位 AP（app）で処理する。
- UI レイヤー（app）ではユーザー操作起因の例外を各ハンドラで catch し、UI の応答性を維持する。
- ログは LoggerMessage ソース生成（`Logging/ColorPickerLog.cs`）で出力する。高頻度イベントは Debug / Trace、障害は Error で記録する。

### 4.2 本プロジェクト固有の例外処理

- 無効な色入力は例外にせず、バリデーション状態（`HasValidationError` / `ValidationErrorMessage`）で通知し、フォーカス喪失時に最終有効値へ復帰する。
- `StringToBrushConverter.ConvertBack` は未サポートのため `NotSupportedException` を投げる（双方向変換は行わない）。
- 解釈不能な色文字列は `Brushes.Transparent`（スウォッチ背景）として安全に扱う。

## 5. コンポーネント図

```mermaid
flowchart TB
    subgraph CPC["ColorPickerControl プロジェクト (ColorPickerControl)"]
        Ctl["ColorPickerControl<br/>public sealed partial<br/>: UserControl<br/>ColorPicker.axaml.cs（公開 API ＋ 非公開実装を単一ファイルに集約）"]
        Xaml["ColorPicker.axaml<br/>ComboBox + スウォッチ + Button"]
        Ent["ColorEntry<br/>internal sealed record<br/>ColorEntry.cs"]
        Srt["ColorSortMode<br/>public enum<br/>ColorSortMode.cs"]
        Conv1["StringToBrushConverter<br/>public sealed<br/>StringToBrushConverter.cs"]
        Str["ColorString<br/>public static<br/>ColorString.cs"]
        Dsp["ColorDisplayConverter<br/>public sealed<br/>ColorDisplayConverter.cs"]
        Fld["_comboBox / _lastValidColor<br/>private フィールド"]
    end
    subgraph Dep["外部依存"]
        Av["Avalonia 12.1<br/>UserControl / ComboBox / ColorView / Flyout"]
        ColorPicker["Avalonia.Controls.ColorPicker<br/>ColorView コントロール"]
    end
    subgraph Host["上位AP（消費側）"]
        VM["ViewModel<br/>SelectedColor 双方向バインド"]
    end

    Xaml --> Ctl
    Ctl --> Fld
    Ctl --> Ent
    Ctl --> Srt
    Ctl --> Conv1
    Ctl --> Str
    Ctl --> Dsp
    Conv1 --> Str
    Dsp --> Str
    Ctl --> ColorPicker
    VM -->|"SelectedColor (TwoWay)"| Ctl
    VM -->|"ColorSortMode / HighlightBorderOnError"| Ctl
    Ctl -->|"HasValidationError / ValidationErrorMessage"| VM
```

## 6. コンポーネント一覧

### 6.1 ColorPickerControl

- **分類**: 公開コントロール（`public sealed partial class : UserControl`）
- **責務**:
  - `SelectedColor`（色名/16進コード）の双方向バインド対応の保持（公開 API は ColorPicker.axaml.cs に集約）
  - 入力バリデーション（`IsValidColor` / `UpdateValidationStatus`）
  - ドロップダウン（ComboBox）の構築と並び順切替（`ColorSortMode`）
  - テキスト選択の自動解除（`OnSelectionChanged` → `ResetTextBoxSelection`）
  - 無効入力の最終有効値復帰（`OnLostFocus`）
  - カラーピッカー Flyout の表示（`OnColorPickerButtonClick` → `CreateColorPickerFlyout`）
  - ColorView テーマの内包適用（`EnsureColorViewTheme` / `EnsureColorViewThemeCore`。ホスト明示指定を優先）
  - スウォッチ用の Padding 調整（`OnComboBoxTemplateApplied` → `ApplyPaddingWithSwatchOffset`）
  - バリデーション失敗時の枠線強調（`UpdateBorderValidationVisual`）
- **公開API**: `SelectedColor` / `ColorSortMode` / `HighlightBorderOnError` / `HasValidationError` / `ValidationErrorMessage` / `ComboBoxMinWidth` / `ComboBoxMaxWidth` / `ComboBoxWidth` / `ComboBoxPadding` / `ComboBoxMargin`（StyledProperty も同ファイルに定義）
- **構成**: 公開 API（StyledProperty・公開プロパティ）と非公開実装（定数・静的フィールド・コンストラクタ・入力イベント・Flyout・テーマ管理・バリデーション）を単一ファイルに集約（無理な分割はしない）
- **内部メソッド（internal）**: `IsValidColor` / `UpdateValidationStatus` / `ResetTextBoxSelection` / `FindTextBoxInVisualTree` / `EnsureColorViewTheme` / `EnsureColorViewThemeCore` / `IsColorViewTheme`（テストからの検証用に公開）
- **依存**: ColorEntry / ColorSortMode / StringToBrushConverter / ColorString / ColorDisplayConverter / Avalonia.Controls.ColorPicker（ColorView）

### 6.2 ColorPicker.axaml

- **分類**: UI レイアウト（XAML）
- **責務**:
  - ComboBox（テキスト入力 + ドロップダウン）の配置
  - 色スウォッチ（Border）のオーバーレイ配置
  - カラーピッカー起動ボタン（…）の配置
  - コンバーターのリソース登録
- **依存**: ColorPickerControl / StringToBrushConverter

### 6.3 ColorEntry

- **分類**: 内部レコード（色名 + 16進コードの組。ドロップダウンの選択肢データ）
- **可視性**: `internal sealed record`（公開 API ではなく内部型。外部から直接扱わない）
- **責務**:
  - 色の英語名・色名（`Color.TryParse` で解釈可能な文字列）の保持
  - 全色一覧の静的提供（`All` / `AllByHue`）
  - 色相順ソートキーの計算（`GetSortKey`）
- **特徴**: `Avalonia.Media.Colors` の静的プロパティをリフレクションで取得。`DynamicDependency` でトリマー対策済み。`Value` パラメータには現在の実装では色名（= `Colors` のプロパティ名）が保持される
- **依存**: Avalonia.Media

### 6.4 ColorSortMode

- **分類**: 公開 enum
- **責務**: ドロップダウンの並び順の指定（`ByName` / `ByHue`）
- **依存**: なし

### 6.5 StringToBrushConverter

- **分類**: 公開 IValueConverter（`public sealed class`）
- **責務**: 色名/16進コード → `IBrush`（スウォッチ背景）
- **特徴**: 解釈不能な値は `Brushes.Transparent`。`ConvertBack` は `NotSupportedException`
- **依存**: Avalonia.Media / Avalonia.Data.Converters

### 6.6 ColorString

- **分類**: 公開静的ユーティリティ（`public static class`）
- **責務**: 色文字列（色名 / #RRGGBB / #AARRGGBB / +% 形式）の解析・整形・正規化
- **特徴**:
  - 保存値は `ToArgbHex` で `#AARRGGBB`（アルファ先頭 8 桁）へ正規化
  - 画面表示は `ToDisplayText` / `Format` で `{色}+{不透明度%}` 形式へ整形（不透明度 100% は省略）
  - `TryParse` で両形式を受け付ける（色と不透明度の合成）
- **依存**: Avalonia.Media

### 6.7 ColorDisplayConverter

- **分類**: 公開 IValueConverter（`public sealed class`）
- **責務**: 保存値（`#AARRGGBB`）と画面表示テキスト（`{色}+{不透明度%}`）の相互変換
- **特徴**: `Convert` / `ConvertBack` とも `ColorString`（`ToDisplayText` / `ToArgbHex`）に委譲。解釈不能な値は入力をそのまま返す
- **依存**: Avalonia.Media / Avalonia.Data.Converters

## 7. 依存ルール

- 依存方向は**一方向の階層構造**（XAML → コントロール → ドメイン/コンバーター）。
- 公開 API（StyledProperty・公開プロパティ）は `ColorPicker.axaml.cs` に集約し、非公開実装も同ファイル内の private / internal メンバーとして隠蔽する（無理なファイル分割・内部公開・具象依存を解消）。
- 状態管理はすべて StyledProperty（Avalonia プロパティシステム）に集約し、private フィールドは補助（`_lastValidColor`）のみ。
- ドメインロジック（`ColorEntry` の色計算）は UI 非依存（Avalonia.Media のみ）。
- 上位APへは双方向バインド（StyledProperty）と PropertyChanged のみで連携。

## 8. バリデーション状態遷移

```mermaid
stateDiagram-v2
    [*] --> Valid: SelectedColor 有効
    Valid --> Empty: 空文字
    Valid --> Invalid: 無効な色コード
    Invalid --> Valid: 有効な色コード
    Invalid --> Valid: フォーカス喪失（最終有効値へ復帰）
    Empty --> Valid: 有効な色コード
    Empty --> Invalid: 無効な色コード
    Valid --> [*]
```

- `HasValidationError` / `ValidationErrorMessage` は `SelectedColor` の変更に応じて更新されます。

## 9. ライフサイクル

```mermaid
sequenceDiagram
    participant VM as ViewModel
    participant Ctl as ColorPickerControl
    participant Cmb as ComboBox
    participant Fly as Flyout(ColorView)

    VM->>Ctl: SelectedColor 設定（TwoWay）
    Ctl->>Ctl: バリデーション判定・最終有効値更新
    Ctl-->>VM: HasValidationError / ValidationErrorMessage

    VM->>Ctl: ColorSortMode 変更
    Ctl->>Cmb: ItemsSource 切替（AllByHue / All）

    Ctl->>Cmb: テキスト入力・ドロップダウン選択
    Cmb-->>Ctl: SelectionChanged / LostFocus
    Ctl->>Ctl: バリデーション・復帰処理

    VM->>Ctl: カラーピッカー起動
    Ctl->>Fly: ShowAt（OK / キャンセル）
    Fly-->>Ctl: OK → SelectedColor 反映
    Ctl-->>VM: SelectedColor 更新（TwoWay）
```
