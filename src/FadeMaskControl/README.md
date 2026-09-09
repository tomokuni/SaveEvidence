# FadeMaskControl

## 目次

- [FadeMaskControl](#fademaskcontrol)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [AXAML（依存関係プロパティ描画）](#axaml依存関係プロパティ描画)
    - [外部レンダラー（モード別描画）](#外部レンダラーモード別描画)
  - [6. 入力動作](#6-入力動作)
  - [7. サンプル](#7-サンプル)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. アーキテクチャ](#9-アーキテクチャ)
  - [10. プロジェクト構成](#10-プロジェクト構成)
  - [11. ライセンス](#11-ライセンス)

---

## 1. 概要

Avalonia UI 用の選択オーバーレイ描画コントロールです。領域選択時の暗転オーバーレイ（穴あけマスク）・選択枠線・サイズラベルを描画し、モード別の描画（ハイライト・ウィンドウタイトル・スクリーン番号）は `FadeMaskRenderer` が担当します。

## 2. 機能一覧

- 暗転オーバーレイ（穴あけマスク）の描画（選択矩形の外側を暗転）
- 選択矩形・選択枠線・サイズラベルの描画
- 外部レンダラー（`FadeMaskRenderer`）への描画委譲（モード別描画の統一）
- モード別描画（AreaSelect / ScreenSelect / WindowSelect / Crop）に対応
- Avalonia Binding 対応の StyledProperty（双方向バインド）
- サイズラベル・情報ラベルの表示制御（DPI スケーリング対応）
- **座標変換ポート（`SetCoordinateTransform(ICoordTransform?)` + `LogicalSelectionRect`）**: DragRectControl と同方式。論理座標（ドメイン）の選択矩形を描画時に動的評価で DIP 変換（ウィンドウサイズ変更・ズーム・スクロールに追従）
- **非暗転領域の閉区間描画**: 非暗転領域（穴）は始点・終点の両ピクセルを含む閉区間（暗転領域は含まない）

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\FadeMaskControl\src\FadeMaskControl.csproj" />
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
        xmlns:fm="clr-namespace:Estable.AvaloniaUI.FadeMask;assembly=FadeMaskControl">
    <Grid Background="LightGray">
        <fm:FadeMaskControl LogicalSelectionRect="{Binding LogicalSelectionRect}"
                            OverlayColor="#96000000"
                            BorderColor="Red"
                            BorderThickness="2"
                            ShowSizeLabel="True"/>
    </Grid>
</Window>
```

### 外部レンダラー（モード別描画）

```csharp
using Estable.AvaloniaUI.FadeMask;

var control = new FadeMaskControl();
var renderer = new FadeMaskRenderer
{
    Mode = FadeMaskRenderMode.WindowSelect,
    HasHighlight = true,
    LogicalHighlightRect = new Rect(10, 10, 200, 100),
    WindowTitle = "Sample Window",
};
control.ExternalRenderer = renderer;
control.InvalidateVisual(); // 再描画を要求
```

## 6. 入力動作

- **暗転オーバーレイ**: 選択矩形の外側（上・下・左・右）を暗転色で塗りつぶす（穴あけマスク）
- **サイズラベル**: 選択矩形の左上に座標・サイズ・表示スケールを表示（`DisplayScaleScaling` 対応）
- **外部レンダラー委譲**: `ExternalRenderer` 設定時は依存関係プロパティの描画を行わず、レンダラーへ完全委譲

## 7. サンプル

`sample/` に動作確認用のサンプルアプリが含まれています。暗転マスク・サイズラベルの描画と公開プロパティの動的変更を確認できます。

```bash
dotnet run --project sample/FadeMaskControl.Sample.csproj
```

## 8. ドキュメント

- [外部機能仕様書](SPEC_ExtFunc.md)
- [システム仕様書](SPEC_System.md)

## 9. アーキテクチャ

公開 API（StyledProperty・公開プロパティ・ExternalRenderer）と描画実装（静的コンストラクタ・Render オーバーライド）は
`src/FadeMaskControl.cs` に集約します。ファイル分割による無理な分離は避け、暗転マスク・サイズラベルの
共通描画ロジックのみ `FadeMaskDrawer`（internal）に委譲します。

- `FadeMaskControl.cs`: 公開 API（StyledProperty・公開プロパティ・ExternalRenderer・公開 API 一覧）＋ 描画実装（Render・AffectsRender 登録）
- `FadeMaskRenderer.cs`: 公開レンダラー（描画状態の真のソース・モード別描画）
- `FadeMaskRenderMode.cs`: 公開 enum（描画モード）
- `FadeMaskDrawer.cs`: 暗転マスク・サイズラベルの共通描画ロジック（internal）

## 10. プロジェクト構成

```text
FadeMaskControl/
├── src/
│   ├── FadeMaskControl.csproj
│   ├── FadeMaskControl.cs         # 公開 API（FadeMaskControl: StyledProperty・公開プロパティ・ExternalRenderer・公開 API 一覧）＋ 描画実装
│   ├── FadeMaskRenderer.cs        # 公開レンダラー（描画状態の真のソース・モード別描画）
│   ├── FadeMaskRenderMode.cs      # 公開 enum（描画モード）
│   └── FadeMaskDrawer.cs          # internal 描画ユーティリティ（暗転マスク・サイズラベル）
├── sample/                        # サンプルアプリ（sample/README.md 参照）
└── tests/                         # 単体テスト（行・ブランチ 100%）
```

## 11. ライセンス

このリポジトリのライセンス方針に従います。ライセンスが明示されていない環境では、再配布・商用利用の条件を確認してから利用してください。
