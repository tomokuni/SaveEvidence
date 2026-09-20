# FadeMaskControl サンプルアプリケーション

## 目次

- [FadeMaskControl サンプルアプリケーション](#fademaskcontrol-サンプルアプリケーション)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 実行方法](#2-実行方法)
  - [3. 画面構成と操作](#3-画面構成と操作)
  - [4. 使い方（初めての方向け）](#4-使い方初めての方向け)
  - [5. AXAML での利用パターン](#5-axaml-での利用パターン)
  - [6. 外部レンダラー（FadeMaskRenderer）について](#6-外部レンダラーfademaskrendererについて)
  - [7. 検証項目（動作確認チェックリスト）](#7-検証項目動作確認チェックリスト)

---

## 1. 概要

`FadeMaskControl/sample/` にある **FadeMaskControl 動作確認用の独立したサンプルアプリケーション**です。

- `FadeMaskControl`（`Estable.AvaloniaUI.FadeMask.FadeMaskControl`）を実際に Visual Tree へ配置して動作を確認できます。
- 暗転オーバーレイ（穴あけマスク）とサイズラベルの描画を確認できます。
- 公開プロパティ（`SelectionRect` / `OverlayColor` / `TextColor` / `TextBackgroundColor` / `FontSize` / `ShowSizeLabel`）のバインドと動的変更を確認できます。色の不透明度は色のアルファに統合されています。

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
| 暗転オーバーレイ（Opacity） | スライダーで `OverlayColor` のアルファ値（0〜255）を変更 |
| 選択枠線（Thickness） | スライダーで外部レンダラー（`ExternalRenderer`）の `BorderThickness` を変更（不透明度は色のアルファで表現） |
| サイズラベル | `ShowSizeLabel` チェックボックス・`FontSize` スライダー・`SizeLabelOverride` テキストボックス（空は自動生成）で変更 |
| スケーリング | `Scaling` / `DisplayScaleScaling` のスライダー（0 は自動 = Scaling と同値）で変更 |
| 情報ラベル | `InfoLabelVisible` チェックボックス・`InfoLabelColor`（色コード）・`InfoLabelBackgroundColor`（アルファ込み） / `InfoLabelFontSize` スライダーで変更（ScreenSelect / WindowSelect のハイライト時のみ表示） |

## 4. 使い方（初めての方向け）

1. **起動直後**: 初期モード `AreaSelect`・初期選択矩形（50, 40, 220, 160）に応じて、グレー領域の右上部分が暗転（穴あけマスク）されます。
2. **描画モード（ComboBox）**: 選択すると `FadeMaskRenderer`（`ExternalRenderer`）の描画状態が切り替わります。
   - `AreaSelect`: 選択矩形の穴あけマスク＋サイズラベル（既定）
   - `ScreenSelect`: ハイライト矩形＋スクリーン番号ラベル
   - `WindowSelect`: ハイライト矩形＋ウィンドウタイトルラベル
   - `Crop`: 全面暗転＋選択矩形の穴あけ
3. **X / Y / W / H のテキストボックス**: 数値を入力すると**入力のたびに**選択矩形が更新され、暗転領域（穴あけ位置）とサイズラベルの座標が即座に変化します。
   - 例: X を `100` にすると、穴が右へ移動します。W を `300` にすると、穴が横に広がります。
   - 空文字や数値以外を入力した場合は現在値が維持されます。
4. **「矩形なし（全面暗転）」ボタン**: クリックすると選択矩形が 0x0 になり、グレー領域全体が暗転します。
5. **Opacity スライダー**: 右へ動かすと暗転が濃くなります（アルファ値 0〜255）。
6. **Thickness スライダー**: 選択枠線の太さを変更します（不透明度は色のアルファで表現）。
7. **サイズラベル**: チェックボックスで表示/非表示、スライダーでフォントサイズを変更します。
8. **サイズラベル明示指定**: 「明示指定（`SizeLabelOverride`）」テキストボックスに文字列を入力すると、サイズラベルのテキストが明示指定に置き換わります（空文字に戻すと自動生成）。
9. **スケーリング**: `Scaling` で DPI スケーリング係数（サイズラベルの物理ピクセル換算）、`DisplayScaleScaling` でマルチスクリーン補正（0 は自動 = Scaling と同値）を変更します。
10. **情報ラベル**: `InfoLabelVisible` / `InfoLabelColor` / `InfoLabelBackgroundColor`（アルファ込み） / `InfoLabelFontSize` を変更できます（ScreenSelect / WindowSelect のハイライト時のみ表示）。

> **補足**: 選択枠線は FadeMaskControl の依存関係プロパティ描画では描画されませんが、
> 本サンプルは `ExternalRenderer`（FadeMaskRenderer）経由で描画するため、枠線・ハイライト・
> ウィンドウタイトル・スクリーン番号なども確認できます。

## 5. AXAML での利用パターン

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:fm="clr-namespace:Estable.AvaloniaUI.FadeMask;assembly=FadeMaskControl.AvaloniaUI">
    <Grid Background="LightGray">
        <fm:FadeMaskControl LogicalSelectionRect="{Binding SelectionRect}"
                            OverlayColor="#96000000"
                            ShowSizeLabel="True"/>
    </Grid>
</Window>
```

## 6. 外部レンダラー（FadeMaskRenderer）について

本サンプルは `FadeMaskControl` に `ExternalRenderer`（`FadeMaskRenderer`）を設定して描画します。
モード別の描画（AreaSelect / ScreenSelect / WindowSelect / Crop のハイライト・選択矩形・全面暗転・ウィンドウタイトル・スクリーン番号）は
`FadeMaskRenderer` が担当し、描画モードの ComboBox で切り替えられます。

`ExternalRenderer` は CLR プロパティのため XAML バインドできません。コードビハインドで設定します。

```csharp
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

## 7. 検証項目（動作確認チェックリスト）

- [ ] 選択矩形が指定位置に表示され、矩形外が暗転（穴あけマスク）される
- [ ] 描画モード切替（ComboBox）で AreaSelect / ScreenSelect / WindowSelect / Crop の描画が切り替わる
- [ ] 「矩形なし（全面暗転）」で全面が暗転される
- [ ] 暗転オーバーレイの Opacity スライダーで暗転の濃さが変わる
- [ ] `BorderThickness` の変更が枠線に反映される（不透明度は色のアルファで表現）
- [ ] サイズラベルの表示/非表示・フォントサイズが反映される
- [ ] `SizeLabelOverride` の明示指定でサイズラベルが置き換わり、空文字で自動生成に戻る
- [ ] `Scaling` / `DisplayScaleScaling` の変更がサイズラベルの物理ピクセル換算に反映される（0 は自動 = Scaling と同値）
- [ ] `InfoLabelVisible` / `InfoLabelColor` / `InfoLabelBackgroundColor`（アルファ込み） / `InfoLabelFontSize` の変更が情報ラベルに反映される（ScreenSelect / WindowSelect）
- [ ] X / Y / W / H の編集で選択矩形とラベルの座標表示が更新される
