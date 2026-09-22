# FadeMaskControl サンプルアプリケーション

## 目次

- [FadeMaskControl サンプルアプリケーション](#fademaskcontrol-サンプルアプリケーション)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 実行方法](#2-実行方法)
  - [3. 画面構成と操作](#3-画面構成と操作)
  - [4. 使い方（初めての方向け）](#4-使い方初めての方向け)
  - [5. AXAML での利用パターン](#5-axaml-での利用パターン)
  - [6. 外観（FadeMaskAppearance）について](#6-外観fademaskappearanceについて)
  - [7. 検証項目（動作確認チェックリスト）](#7-検証項目動作確認チェックリスト)

---

## 1. 概要

`FadeMaskControl/sample/` にある **FadeMaskControl 動作確認用の独立したサンプルアプリケーション**です。

- `FadeMaskControl`（`Estable.AvaloniaUI.FadeMask.FadeMaskControl`）を実際に Visual Tree へ配置して動作を確認できます。
- 暗転オーバーレイ（穴あけマスク）とサイズラベルの描画を確認できます。
- 公開プロパティ（外観は `Appearance`（コアの `FadeMaskAppearance`）、状態は `LogicalSelectionRect` / `HasSelection` / `HasHighlight` / `WindowTitle` / `Mode` / `ShowFullOverlay` など）のバインドと動的変更を確認できます。色の不透明度は色のアルファに統合されています。

## 2. 実行方法

```powershell
dotnet run --project sample/FadeMaskControl.AvaloniaUI.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project sample/FadeMaskControl.AvaloniaUI.Sample.csproj
```

### バッチファイル（リリースビルド＆起動）

`sample/RunSample.bat` をダブルクリックすると、リリースビルドを行った後、サンプルアプリケーションを起動します。

```text
RunSample.bat  →  リリースビルド（dotnet build -c Release）→ FadeMaskControl.Sample.exe を起動
```

> 本サンプルは独立したプロジェクトです。
> `FadeMaskControl.csproj` は `sample/**` をコンパイル対象から除外しています。

## 3. 画面構成と操作

| 要素 | 内容 |
| --- | --- |
| オーバーレイ表示領域 | グレー背景に `FadeMaskControl` を配置。選択矩形の穴あけマスク＋サイズラベルを描画 |
| 描画モード（ComboBox） | `AreaSelect` / `ScreenSelect` / `WindowSelect` / `Crop` を選択してモード別描画を切替 |
| 選択矩形（X / Y / W / H） | テキストボックスで選択矩形の座標・サイズを編集（**数値を入力すると即座に反映**） |
| 「矩形なし（全面暗転）」ボタン | `SelectionRect` を 0x0 に設定し全面暗転を確認 |
| 暗転オーバーレイ（Opacity） | スライダーで `Appearance.OverlayColor` のアルファ値（0〜255）を変更 |
| 選択枠線（Thickness） | スライダーで `Appearance.BorderThickness` を変更（不透明度は色のアルファで表現） |
| サイズラベル | `Appearance.ShowSizeLabel` チェックボックス・`Appearance.SizeLabelFontSize` スライダー・`SizeLabelOverride` テキストボックス（空は自動生成）で変更 |
| スケーリング | `Scaling` / `DisplayScaleScaling` のスライダー（0 は自動 = Scaling と同値）で変更 |
| 情報ラベル | `Appearance.InfoLabelVisible` チェックボックス・`Appearance.InfoLabelColor`（色コード）・`Appearance.InfoLabelBackgroundColor`（アルファ込み） / `Appearance.InfoLabelFontSize` スライダーで変更（ScreenSelect / WindowSelect のハイライト時のみ表示） |

## 4. 使い方（初めての方向け）

1. **起動直後**: 初期モード `AreaSelect`・初期選択矩形（50, 40, 220, 160）に応じて、グレー領域の右上部分が暗転（穴あけマスク）されます。
2. **描画モード（ComboBox）**: 選択するとコントロールの描画状態（`Mode` / `HasHighlight` / `LogicalHighlightRect` / `WindowTitle` / `ShowFullOverlay`）が切り替わります。
   - `AreaSelect`: 選択矩形の穴あけマスク＋サイズラベル＋操作ラベル（既定）
   - `ScreenSelect`: ハイライト矩形＋操作ラベル
   - `WindowSelect`: ハイライト矩形＋操作ラベル＋ウィンドウタイトル（情報ラベル）
   - `Crop`: 全面暗転＋選択矩形の穴あけ
3. **X / Y / W / H のテキストボックス**: 数値を入力すると**入力のたびに**選択矩形が更新され、暗転領域（穴あけ位置）とサイズラベルの座標が即座に変化します。
   - 例: X を `100` にすると、穴が右へ移動します。W を `300` にすると、穴が横に広がります。
   - 空文字や数値以外を入力した場合は現在値が維持されます。
4. **「矩形なし（全面暗転）」ボタン**: クリックすると選択矩形が 0x0 になり、グレー領域全体が暗転します。
5. **Opacity スライダー**: 右へ動かすと暗転が濃くなります（アルファ値 0〜255）。
6. **Thickness スライダー**: 選択枠線の太さを変更します（不透明度は色のアルファで表現）。
7. **サイズラベル**: チェックボックスで表示/非表示、スライダーでフォントサイズを変更します。
8. **サイズラベル明示指定**: 「明示指定（`SizeLabelOverride`）」テキストボックスに文字列を入力すると、サイズラベルのテキストが明示指定に置き換わります（空文字に戻すと自動生成）。
9. **スケーリング**: `Scaling` で ドメイン → DIP 変換の基準倍率（座標変換ポートを設定していない場合は矩形の位置・サイズとサイズラベルの換算の両方に反映）、`DisplayScaleScaling` でマルチスクリーン補正（0 は自動 = Scaling と同値）を変更します。
10. **情報ラベル**: `InfoLabelVisible` / `InfoLabelColor` / `InfoLabelBackgroundColor`（アルファ込み） / `InfoLabelFontSize` を変更できます（ScreenSelect / WindowSelect のハイライト時のみ表示）。

> **補足**: 本サンプルは `Appearance`（外観）と状態プロパティ（`LogicalSelectionRect` / `HasHighlight` 等）を
> AXAML で直接バインドするため、枠線・ハイライト・ウィンドウタイトルなども確認できます。
> サイズラベルの文言プロバイダ（デリゲート）のみコードビハインドで設定します。

## 5. AXAML での利用パターン

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

## 6. 外観（FadeMaskAppearance）について

本サンプルは外観を 1 つの値（`FadeMaskControl.Appearance` = コアの `FadeMaskAppearance`）で設定します。
ViewModel は `Appearance` を保持し、各編集項目（色・サイズ・太さ）の setter が `with` 式で部分更新します。
モード別の描画（ハイライト・選択矩形・全面暗転・ウィンドウタイトル）は状態プロパティ（`Mode` / `HasHighlight` / `LogicalHighlightRect` / `WindowTitle` / `ShowFullOverlay`）で切り替えます。

外観は StyledProperty のため AXAML でバインドできます。`Func` であるサイズラベルの文言プロバイダのみコードビハインドで設定します。

```csharp
var control = new FadeMaskControl
{
    Appearance = new FadeMaskAppearance
    {
        OverlayColor = Color.FromAColor(150, Colors.Black).ToDrawingColor(),
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

## 7. 検証項目（動作確認チェックリスト）

- [ ] 選択矩形が指定位置に表示され、矩形外が暗転（穴あけマスク）される
- [ ] 描画モード切替（ComboBox）で AreaSelect / ScreenSelect / WindowSelect / Crop の描画が切り替わる
- [ ] 「矩形なし（全面暗転）」で全面が暗転される
- [ ] 暗転オーバーレイの Opacity スライダーで暗転の濃さが変わる
- [ ] `BorderThickness` の変更が枠線に反映される（不透明度は色のアルファで表現）
- [ ] サイズラベルの表示/非表示・フォントサイズが反映される
- [ ] `SizeLabelOverride` の明示指定でサイズラベルが置き換わり、空文字で自動生成に戻る
- [ ] `Scaling` / `DisplayScaleScaling` の変更が矩形の位置・サイズ（ポート未設定時）とサイズラベルの物理ピクセル換算に反映される（0 は自動 = Scaling と同値）
- [ ] `InfoLabelVisible` / `InfoLabelColor` / `InfoLabelBackgroundColor`（アルファ込み） / `InfoLabelFontSize` の変更が情報ラベルに反映される（ScreenSelect / WindowSelect）
- [ ] X / Y / W / H の編集で選択矩形とラベルの座標表示が更新される
