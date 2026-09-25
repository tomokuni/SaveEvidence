# LoupeControl サンプルアプリケーション

## 目次

- [LoupeControl サンプルアプリケーション](#loupecontrol-サンプルアプリケーション)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 実行方法](#2-実行方法)
  - [3. 操作方法](#3-操作方法)
  - [4. 検証項目（動作確認チェックリスト）](#4-検証項目動作確認チェックリスト)
  - [5. 構成](#5-構成)
  - [6. 使い方（初めての方向け）](#6-使い方初めての方向け)

---

## 1. 概要

`LoupeControl.AvaloniaUI/sample/` にある **LoupeControl 動作確認用の独立したサンプルアプリケーション**です。

- `LoupeControl`（`EsUtil.SaveEvidence.Loupe.AvaloniaUI.LoupeControl`）を実際に配置して動作を確認できます。
- テスト画像（グラデーション＋グリッド＋マーカー）を生成し、マウス追従で拡大表示します。
- 外観設定（拡大率・サイズ・十字線・市松模様・外枠・描画スケール）をスライダー/コンボボックスで変更できます。
- `LoupeControl` をVisual Treeへ配置し、内部の `Image` 要素で表示します。
- `Clear()` / `RequestRender()` の API 呼び出しをボタンで確認できます。
- `LoupeControl.Log` に `ILogger`（デバッグ出力）を設定し、描画エラー通知を確認できます。

## 2. 実行方法

```powershell
dotnet run --project sample/LoupeControl.AvaloniaUI.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project sample/LoupeControl.AvaloniaUI.Sample.csproj
```

### バッチファイル（リリースビルド＆起動）

`sample/RunSample.bat` をダブルクリックすると、リリースビルドを行った後、サンプルアプリケーションを起動します。

```text
RunSample.bat  →  リリースビルド（dotnet build -c Release）→ LoupeControl.AvaloniaUI.Sample.exe を起動
```

> 本サンプルは独立したプロジェクトです。
> `LoupeControl.AvaloniaUI.csproj` は `sample/**` をコンパイル対象から除外しています。

## 3. 操作方法

| 操作 | 内容 |
| --- | --- |
| テスト画像上をマウス移動 | ルーペがカーソルの右下に追従して拡大表示 |
| 画像の外へマウス移動 | 画像外の領域のみ市松模様（距離に応じて増加。視野全体が外に出た場合のみ全面市松） |
| 「ルーペを表示」チェック | `IsVisible` の切り替え（非表示時は Bitmap 解放） |
| 拡大率 / ルーペサイズのスライダー | `Options.ZoomLevel` / `LoupeSize` の変更 |
| 基本サイズのスライダー | `Options.RequestedSize`（クランプ前の基本サイズ。設定時に `LoupeSize` へ反映） |
| 色コンボボックス | `Options.Appearance`（`CrossColor` / `CheckerColorA/B` / `FrameColor`。「なし」で描画なし） |
| タイルサイズ・枠の太さ | `Options.Appearance`（`CheckerSize` / `FrameWidth`。不透明度は色のアルファに統合） |
| 描画スケール | `RenderScaling`（DIP→物理ピクセル変換の確認） |
| 選択矩形オーバーレイ | `SelectionRectVisible` チェック・`SelectionRect`（X / Y / W / H）の編集（ドメイン座標 = 元画像ピクセル）。テスト画像上に破線の枠＋座標ラベルが表示され、ルーペ内にも破線が描画される |
| Clear() ボタン | `Clear()` を呼び出し、非表示＋Bitmap 解放 |
| RequestRender() ボタン | `RequestRender()` を呼び出し、プロパティ変更を伴わない再生成を要求 |

## 4. 検証項目（動作確認チェックリスト）

- [ ] マウス追従で拡大画像が表示される（グラデーション・円マーカー・グリッド）
- [ ] 10px グリッドがズーム倍率どおりに拡大される（倍率の検証）
- [ ] ズーム・サイズ変更が即時反映される
- [ ] 十字線・市松模様・外枠の色が反映される（不透明度は色のアルファに統合）
- [ ] 「なし」を選ぶと該当要素が描画されなくなる
- [ ] `RenderScaling` を 0.5〜2.0 に変更すると Bitmap の物理ピクセルサイズが変化する
- [ ] `Options.RequestedSize` を変更すると `LoupeSize` にも反映される（基本サイズ → 表示サイズ）
- [ ] `SelectionRectVisible` を ON にすると、テスト画像上に選択矩形の破線枠＋座標ラベルが表示され、場所が一目でわかる
- [ ] `SelectionRectVisible` を ON にすると、`SelectionRect`（ドメイン座標）の枠線がルーペ画像上にも破線で描画される（ズーム 2 倍以上）
- [ ] `SelectionRect` の X / Y / W / H を変更すると、テスト画像上の枠とルーペ内の破線の位置・サイズが変化する
- [ ] `SelectionRectColorA` / `SelectionRectColorB`（「なし」で非表示・不透明度は色のアルファ）が破線に反映される
- [ ] チェック OFF / `Clear()` で Bitmap が解放され、チェック ON で再表示される
- [ ] `RequestRender()` で再生成が要求され、`Rendered` が発火する
- [ ] ルーペ上端の中央に中心座標ラベル（`(x, y)`）が表示され、マウス移動で文言が追従する（コントロールがテキスト要素として表示する）
- [ ] 画像の外では、距離に応じて市松模様が部分的に表示される（全面市松になるのは視野全体が外に出た時のみ）

## 5. 構成

```text
LoupeControl.AvaloniaUI/sample/
├── LoupeControl.AvaloniaUI.Sample.csproj   # サンプルアプリのプロジェクト（net10.0 / Avalonia 12.1）
├── app.manifest                 # Windows DPI 設定（PerMonitorV2）
├── Program.cs                   # エントリポイント
├── App.axaml / App.axaml.cs     # アプリケーション定義（FluentTheme）
├── Logging/
│   └── DebugLogger.cs           # LoupeControl.Log の設定例（デバッグ出力へ転送）
├── Imaging/
│   └── TestImageFactory.cs      # テスト画像生成（グラデーション＋グリッド＋マーカー）
├── ViewModels/
│   ├── BrushOption.cs           # 色選択肢
│   └── MainWindowViewModel.cs   # ルーペ設定・中心座標
└── Views/
    ├── MainWindow.axaml(.cs)    # メイン画面（設定パネル＋テスト画像＋ルーペ）
```

## 6. 使い方（初めての方向け）

1. **起動**: テスト画像（グラデーション＋グリッド＋マーカー）が表示されます。
2. **マウス追従**: テスト画像上をマウスで動かすと、カーソルの右下にルーペが追従して拡大表示されます。画像の外では、距離に応じて市松模様が部分的に表示されます。
3. **ルーペの表示/非表示**: 「ルーペを表示」チェックで切り替えます（非表示時は Bitmap 解放）。
4. **外観の変更**: ズーム・サイズ・色・タイルサイズ・枠の太さ・描画スケールをスライダー/コンボボックスで変更します（即時反映）。
5. **基本サイズ（Options.RequestedSize）**: スライダーでクランプ前の基本サイズを変更すると、表示サイズ（`LoupeSize`）にも反映されます。
6. **選択矩形オーバーレイ**: 「選択矩形を表示」を ON にすると、`SelectionRect`（ドメイン座標 = 元画像ピクセル）の枠線がルーペ画像上に 2 色破線（色A/色B）で表示されます（ズーム 2 倍以上）。X / Y / W / H・色A / 色B を変更できます（不透明度は色のアルファに統合）。
7. **API 呼び出し**: `Clear()` / `RequestRender()` ボタンで各公開 API の動作を確認できます。
