# FadeMaskControl

> プロジェクト名: `FadeMaskControl.AvaloniaUI`（Avalonia UI 版）。UI 非依存のロジックは `FadeMaskControl.Core`、MewUI 版は `FadeMaskControl.MewUI` にあります。

## 目次

- [FadeMaskControl](#fademaskcontrol)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [AXAML（依存関係プロパティ描画）](#axaml依存関係プロパティ描画)
    - [コード（モード別描画）](#コードモード別描画)
  - [6. 描画動作](#6-描画動作)
  - [7. サンプル](#7-サンプル)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. アーキテクチャ](#9-アーキテクチャ)
  - [10. プロジェクト構成](#10-プロジェクト構成)
  - [11. ライセンス](#11-ライセンス)

---

## 1. 概要

Avalonia UI 用の選択オーバーレイ描画コントロールです。領域選択時の暗転オーバーレイ（穴あけマスク）・選択枠線・ラベル（サイズ・操作・情報）を描画します。外観はコアの `FadeMaskAppearance` が 1 つの値として所有し、選択矩形・ハイライト矩形・全面暗転などの状態を個別プロパティで受け取ります。

## 2. 機能一覧

- 暗転オーバーレイ（穴あけマスク）の描画（穴あけ矩形の外側を暗転）
- 選択枠線・ラベル（サイズラベル・操作ラベル・情報ラベル）の描画
- 設定と描画状態の単一所有（`Options` = コアの `FadeMaskOptions`。外観・モード・情報ラベルの文言・全面暗転・選択/ハイライト矩形を 1 つの値で設定）
- **描画判断の単一所有**: どの矩形を暗転するか・枠線を描くか・どのラベルを描くか（サイズラベルの自動生成を含む）はコアの `FadeMaskFrame` が決定し、UI 層は返されたフレームを描くだけ（MewUI 版と同一の結果）
- **入力の判断を持たない**: 装飾専用のため入力を扱わない（ホストが `IsHitTestVisible = false` で下へ通す）
- モード別描画（AreaSelect / ScreenSelect / WindowSelect / Crop）に対応
- Avalonia Binding 対応の StyledProperty（双方向バインド）
- **座標変換ポート（`SetCoordinateTransform(ICoordTransform?)` + `Options.LogicalSelectionRect`）**: DragRectControl と同方式。論理座標（ドメイン）の選択矩形を描画時に動的評価で DIP 変換（ウィンドウサイズ変更・ズーム・スクロールに追従）
- **非暗転領域の閉区間描画**: 非暗転領域（穴）は始点・終点の両ピクセルを含む閉区間（暗転領域は含まない）

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\FadeMaskControl.AvaloniaUI\src\FadeMaskControl.AvaloniaUI.csproj" />
```

必要な名前空間:

```csharp
using Estable.AvaloniaUI.FadeMask;
```

## 5. クイックスタート

### AXAML（依存関係プロパティ描画）

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:fm="clr-namespace:Estable.AvaloniaUI.FadeMask;assembly=FadeMaskControl.AvaloniaUI">
    <Grid Background="LightGray">
        <fm:FadeMaskControl LogicalSelectionRect="{Binding LogicalSelectionRect}"
                            HasSelection="True"
                            Appearance="{Binding Appearance}"/>
    </Grid>
</Window>
```

### コード（モード別描画）

```csharp
using EsUtil.Platform.AvaloniaUI;   // ToDrawingColor
using Estable.FadeMask;             // FadeMaskAppearance / FadeMaskRenderMode

var control = new FadeMaskControl
{
    // 外観は 1 つの値（コアの FadeMaskAppearance）で設定する
    Appearance = new FadeMaskAppearance
    {
        OverlayColor = Color.Parse("#96000000").ToDrawingColor(),
        BorderColor = Colors.Red.ToDrawingColor(),
        BorderThickness = 2,
    },
    Mode = FadeMaskRenderMode.WindowSelect,
    HasHighlight = true,
    LogicalHighlightRect = new Rect(10, 10, 200, 100),
    WindowTitle = "Sample Window",
};
control.InvalidateVisual(); // 再描画を要求（プロパティ変更でも自動で再描画される）
```

## 6. 描画動作

- **暗転オーバーレイ**: 穴あけ矩形の外側（上・下・左・右）を暗転色で塗りつぶす（穴あけマスク）。穴あけ矩形が無い場合は `Options.ShowFullOverlay` が true のときだけ全面を暗転する
- **選択枠線**: 穴あけ矩形の四辺を内側に描画する
- **ラベル**: 穴あけ矩形の左上に「サイズラベル（1 行目）→ 操作ラベル（2 行目）→ 情報ラベル（3 行目）」を縦に積む。サイズラベルが無い場合は繰り上げる
- **設定の反映**: `Options`（コアの `FadeMaskOptions`）を変更すると自動で再描画される（`AffectsRender`。同じ値の再設定では通知しない）

## 7. サンプル

`sample/` に動作確認用のサンプルアプリが含まれています。暗転マスク・ラベルの描画と公開プロパティの動的変更を確認できます。

```bash
dotnet run --project sample/FadeMaskControl.AvaloniaUI.Sample.csproj
```

## 8. ドキュメント

- [外部機能仕様書](SPEC_ExtFunc.md)
- [システム仕様書](SPEC_System.md)

## 9. アーキテクチャ

公開 API（StyledProperty・公開プロパティ）と描画実装（静的コンストラクタ・Render オーバーライド）は
`src/FadeMaskControl.cs` に集約します。ファイル分割による無理な分離は避け、描画のみを
`FadeMaskDrawer`（internal）に委譲します。

**判断はコアが持つ**: 「どの矩形を暗転するか」「枠線を描くか」「ラベルをどう組むか」はコアの `FadeMaskFrame` が単一所有します。
UI 層は外観と状態を `FadeMaskRequest` へ詰めて `FadeMaskFrame.Resolve` を呼び、返されたフレームを描くだけです（判断・既定値の解決を持ちません）。
同じ判断を MewUI 版も使うため、両 UI の見た目は一致します（テキストの実寸測定と描画 API の写像のみが UI 固有）。

- `FadeMaskControl.cs`: 公開 API（StyledProperty・公開プロパティ）＋ 描画実装（Render・AffectsRender 登録・ログ）
- `FadeMaskDrawer.cs`: フレーム（暗転領域・枠線・ラベル）の描画（internal。引数の形は MewUI 版の `FadeMaskSkiaRenderer.Draw` と同一）

## 10. プロジェクト構成

```text
FadeMaskControl/                     # プロジェクト名: FadeMaskControl.AvaloniaUI
├── src/
│   ├── FadeMaskControl.AvaloniaUI.csproj
│   ├── FadeMaskControl.cs            # 公開 API（StyledProperty・公開プロパティ）+ 描画実装
│   └── FadeMaskDrawer.cs             # internal 描画ユーティリティ（フレームの描画）
├── sample/                           # サンプルアプリ（sample/README.md 参照）
└── tests/                            # 単体テスト（行・ブランチ 100%）
```

## 11. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
