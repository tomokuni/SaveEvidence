# Parts.CapturePreviewControl.AvaloniaUI サンプルアプリケーション

## 目次

- [Parts.CapturePreviewControl.AvaloniaUI サンプルアプリケーション](#partscapturepreviewcontrolavaloniaui-サンプルアプリケーション)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 実行方法](#2-実行方法)
  - [3. 操作方法](#3-操作方法)
  - [4. キャプチャ操作の流れ](#4-キャプチャ操作の流れ)
  - [5. プロジェクト構成](#5-プロジェクト構成)
  - [6. 検証項目（動作確認チェックリスト）](#6-検証項目動作確認チェックリスト)

---

## 1. 概要

`Parts.CapturePreviewControl.AvaloniaUI/sample/` にある **Parts.CapturePreviewControl.AvaloniaUI 動作確認用の独立したサンプルアプリケーション**です。

- `CaptureSessionControl`（`EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.CaptureSessionControl`）を実際に利用して、画面キャプチャと切出し（Crop）の動作を確認できます。
- **全画面キャプチャ**（`ScreenSelect`）: カーソル位置のスクリーンをハイライトしてクリックで確定
- **ウィンドウ選択キャプチャ**（`WindowSelect`）: カーソル位置の最前面ウィンドウをハイライトしてクリックで確定
- **範囲選択キャプチャ**（`AreaSelect`）: ドラッグで範囲選択（ルーペ付き）してクリックで確定
- **切出し（Crop）**: プレビュー画像上でドラッグ選択し、Enter / ダブルクリックで確定して切り出し範囲を取得
- **プレビューのズーム・パン**: ⊖ / ⊕ / Fit ボタン・拡大率スライダ（10〜500%）・中央寄せ切替・Ctrl+ホイールでズーム、ドラッグ / ホイールでパン（切り出しビューが内包する画像ビューポート）
- **画像を開く**: 任意の画像ファイル（PNG / JPG / BMP / GIF / WEBP）をプレビューへ読み込み（読み込むと Fit 表示にリセット）
- **ポインタ座標表示**: プレビュー上のマウス位置の元画像ピクセル（対象座標）を表示
- キャプチャ結果の画像と領域（仮想画面座標）を表示
- ルーペの表示状態・拡大倍率を設定パネルから変更して `AreaSelect` に反映
- `Cancel()` / `Confirm()` の API 呼び出しをボタンで確認

## 2. 実行方法

```powershell
dotnet run --project sample/Parts.CapturePreviewControl.AvaloniaUI.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project sample/Parts.CapturePreviewControl.AvaloniaUI.Sample.csproj
```

### バッチファイル（リリースビルド＆起動）

`sample/RunSample.bat` をダブルクリックすると、リリースビルドを行った後、サンプルアプリケーションを起動します。

```text
RunSample.bat  →  リリースビルド（dotnet build -c Release）→ Parts.CapturePreviewControl.AvaloniaUI.Sample.exe を起動
```

> 本サンプルは独立したプロジェクトです。
> `Parts.CapturePreviewControl.AvaloniaUI.csproj` は `sample/**` をコンパイル対象から除外しています。

## 3. 操作方法

| 操作 | 内容 |
| --- | --- |
| 「全画面」ボタン | `StartCaptureAsync(CaptureType.ScreenSelect, ...)` で全画面キャプチャを開始 |
| 「ウィンドウ選択」ボタン | `StartCaptureAsync(CaptureType.WindowSelect, ...)` でウィンドウ選択を開始 |
| 「範囲選択（AreaSelect）」ボタン | `StartCaptureAsync(CaptureType.AreaSelect, ...)` で範囲選択を開始（ルーペ付き） |
| 「プレビューで切出し」ボタン | `StartCropAsync(...)` でプレビュー画像上の切出しを開始（対象画像は `PreviewCropView.Source`） |
| 「キャンセル」ボタン | `Cancel()` で進行中セッションをキャンセル |
| 「確定（Crop のみ有効）」ボタン | `Confirm()` で Crop を現在の選択矩形で確定（Enter キー相当） |
| 「拡大倍率」スライダー | `LoupeZoomLevel` を 1〜16 で変更（AreaSelect に反映） |
| 「⊖ / ⊕」ボタン | プレビューのズームアウト / ズームイン（コアが定める 23 段階を遷移） |
| 「Fit」ボタン | プレビューを Fit 表示（ビューポートに収まる Uniform 倍率）へ戻す |
| 「拡大率」スライダ | プレビューの拡大率（10〜500%）を直接指定する |
| 「中央寄せ」チェック | `CenterAlign`（Fit 中に画像がビューポートより小さい場合の中央寄せ / 左上寄せ）を切替（即時反映） |
| 「画像を開く...」ボタン | 画像ファイル（PNG / JPG / BMP / GIF / WEBP）をプレビューへ読み込み、Fit 表示にリセットする |
| ポインタ座標表示 | プレビュー上のマウス位置の対象座標（元画像ピクセル。 画像外は `-`）を表示する |
| Ctrl + ホイール（プレビュー上） | マウスポインタ位置を中心にズーム（×1.1 / ÷1.1 連続遷移） |
| キャプチャ結果領域 | キャプチャ画像と領域（仮想画面座標）を表示。 画像は `CaptureCompleted`（同期イベント）で更新 |
| Crop プレビュー領域 | グリッド付きダミー画像（640×400）を表示。 ドラッグで選択 → Enter / ダブルクリックで確定 |
| 操作ログ領域 | ボタン操作・セッション結果・`CaptureCompleted` 受信を時分秒ミリ秒付きで表示 |

## 4. キャプチャ操作の流れ

1. **全画面 / ウィンドウ選択**: ボタンを押すと全画面透明オーバーレイが表示されます。 カーソルを移動すると対象（スクリーン / ウィンドウ）がハイライトされ、クリックでキャプチャが確定します。 Escape でキャンセルできます。
2. **範囲選択（AreaSelect）**: ボタンを押すと仮想スクリーンのスナップショットが背景に表示され、ドラッグで範囲を選択します（ルーペが追従）。 クリックでキャプチャが確定します。 Escape でキャンセルできます。
3. **切出し（Crop）**: 「プレビューで切出し」ボタンを押すと、右側のプレビュー画像上に選択矩形と暗転マスクが表示されます。 ドラッグで範囲を選択し、Enter / ダブルクリック（選択内）で確定します。 Escape でキャンセルできます。
   - Crop 中のホイール操作: **通常ホイール**でプレビュー画像をスクロール、**Ctrl + ホイール**でズームイン/アウト（選択矩形・暗転マスク・ルーペは追従）。

> **CaptureCompleted（同期イベント）**: キャプチャ確定時、`CaptureCompleted` イベント（同期実行）が発行され、サンプルはここでキャプチャ画像を更新します。
> 同期イベントのため、ハンドラ（画像変更）の完了後にキャプチャセッションがオーナーを再表示する「画像変更 → オーナー再表示」の順序保証を確認できます。

## 5. プロジェクト構成

```text
Parts.CapturePreviewControl.AvaloniaUI/sample/
├── Parts.CapturePreviewControl.AvaloniaUI.Sample.csproj # サンプルアプリのプロジェクト（net10.0-windows / Avalonia 12.1）
├── app.manifest                # アプリケーションマニフェスト（DPI 認識 PerMonitorV2）
├── Program.cs                  # エントリポイント（Avalonia 起動）
├── App.axaml(.cs)              # アプリケーション本体（FluentTheme）
├── ViewModels/
│   └── MainWindowViewModel.cs  # キャプチャ結果・設定・操作ログの保持（CommunityToolkit.Mvvm の ObservableObject）
└── Views/
    ├── MainWindow.axaml        # メインウィンドウのレイアウト
    └── MainWindow.axaml.cs     # CaptureSessionControl の呼び出し・結果反映・ダミー画像生成
```

プレビューのズーム・パンは `Views/MainWindow.axaml(.cs)`（⊖ / ⊕ / Fit・拡大率スライダ・中央寄せ・ポインタ座標）と `ViewModels/MainWindowViewModel.cs`（`ZoomPercent` / `ZoomDisplayText` / `CenterAlign`）が担い、実体は `CropViewControl` が内包する画像ビューポート（`Parts.CapturePreviewControl.Core` の設定）に委譲します。

## 6. 検証項目（動作確認チェックリスト）

- [ ] 「全画面」でスクリーンがハイライトされ、クリックでキャプチャ結果が表示される
- [ ] 「ウィンドウ選択」でウィンドウがハイライトされ、クリックでキャプチャ結果が表示される
- [ ] 「範囲選択（AreaSelect）」でドラッグ選択（ルーペ付き）→ クリックでキャプチャ結果が表示される
- [ ] ルーペ設定（表示・倍率）が AreaSelect に反映される
- [ ] 「プレビューで切出し」でプレビュー上に選択矩形 + 暗転マスクが表示され、Enter / ダブルクリックで切り出し範囲が表示される
- [ ] Escape でキャンセルすると 0x0 の空結果が表示される
- [ ] 「キャンセル」ボタンで進行中セッションがキャンセルされる
- [ ] キャプチャ確定時に `CaptureCompleted`（同期イベント）が発行され、操作ログに「画像を更新しました」が表示される（画像変更 → オーナー再表示の順序）
- [ ] 操作ログ領域に各操作のログが時分秒ミリ秒付きで表示される
- [ ] ⊖ / ⊕ ボタンで拡大率が段階遷移し、拡大率表示とスライダが同期する
- [ ] Fit 中は実効倍率の % が表示され、⊖ / ⊕ で実効倍率を基準に段階遷移する
- [ ] 拡大率スライダの変更が即時反映される
- [ ] Ctrl + ホイールでマウスポインタ位置を中心にズームする（ポインタ位置が維持される）
- [ ] プレビュー画像をドラッグでパンできる（掴んだ位置がポインタに追従する）
- [ ] ホイール / Shift + ホイールで縦・横にパンできる
- [ ] 100px グリッドが拡大率どおりに拡大される（倍率の検証）
- [ ] ウィンドウをリサイズすると Fit 表示が再計算される
- [ ] 「中央寄せ」チェックを OFF にすると、Fit 中（画像がビューポートより小さい場合）に画像が左上寄せになる
- [ ] 「画像を開く...」で画像を差し替えると Fit 表示にリセットされる
- [ ] ポインタ座標表示が画像ピクセル（対象座標）と一致する（画像外は `-`）
