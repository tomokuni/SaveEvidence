# CaptureSessionControl.AvaloniaUI サンプルアプリケーション

## 目次

- [CaptureSessionControl.AvaloniaUI サンプルアプリケーション](#capturesessioncontrolavaloniaui-サンプルアプリケーション)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 実行方法](#2-実行方法)
  - [3. 操作方法](#3-操作方法)
  - [4. キャプチャ操作の流れ](#4-キャプチャ操作の流れ)
  - [5. プロジェクト構成](#5-プロジェクト構成)
  - [6. 検証項目（動作確認チェックリスト）](#6-検証項目動作確認チェックリスト)

---

## 1. 概要

`CaptureSessionControl.AvaloniaUI/sample/` にある **CaptureSessionControl.AvaloniaUI 動作確認用の独立したサンプルアプリケーション**です。

- `CaptureSessionControl`（`Estable.AvaloniaUI.CaptureSession.CaptureSessionControl`）を実際に利用して、画面キャプチャと切出し（Crop）の動作を確認できます。
- **全画面キャプチャ**（`ScreenSelect`）: カーソル位置のスクリーンをハイライトしてクリックで確定
- **ウィンドウ選択キャプチャ**（`WindowSelect`）: カーソル位置の最前面ウィンドウをハイライトしてクリックで確定
- **範囲選択キャプチャ**（`AreaSelect`）: ドラッグで範囲選択（ルーペ付き）してクリックで確定
- **切出し（Crop）**: プレビュー画像上でドラッグ選択し、Enter / ダブルクリックで確定して切り出し範囲を取得
- キャプチャ結果の画像と領域（仮想画面座標）を表示
- ルーペの表示状態・拡大倍率を設定パネルから変更して `AreaSelect` に反映
- `Cancel()` / `Confirm()` の API 呼び出しをボタンで確認

## 2. 実行方法

```powershell
dotnet run --project sample/CaptureSessionControl.AvaloniaUI.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project sample/CaptureSessionControl.AvaloniaUI.Sample.csproj
```

### バッチファイル（リリースビルド＆起動）

`sample/RunSample.bat` をダブルクリックすると、リリースビルドを行った後、サンプルアプリケーションを起動します。

```text
RunSample.bat  →  リリースビルド（dotnet build -c Release）→ CaptureSessionControl.AvaloniaUI.Sample.exe を起動
```

> 本サンプルは独立したプロジェクトです。
> `CaptureSessionControl.AvaloniaUI.csproj` は `sample/**` をコンパイル対象から除外しています。

## 3. 操作方法

| 操作 | 内容 |
| --- | --- |
| 「全画面」ボタン | `StartCaptureAsync(CaptureType.ScreenSelect, ...)` で全画面キャプチャを開始 |
| 「ウィンドウ選択」ボタン | `StartCaptureAsync(CaptureType.WindowSelect, ...)` でウィンドウ選択を開始 |
| 「範囲選択（AreaSelect）」ボタン | `StartCaptureAsync(CaptureType.AreaSelect, ...)` で範囲選択を開始（ルーペ付き） |
| 「プレビューで切出し」ボタン | `StartCropAsync(...)` でプレビュー画像上の切出しを開始 |
| 「キャンセル」ボタン | `Cancel()` で進行中セッションをキャンセル |
| 「確定（Crop のみ有効）」ボタン | `Confirm()` で Crop を現在の選択矩形で確定（Enter キー相当） |
| 「ルーペを表示」チェックボックス | `LoupeVisible` を ON/OFF（AreaSelect に反映） |
| 「拡大倍率」スライダー | `LoupeZoomLevel` を 1〜16 で変更（AreaSelect に反映） |
| キャプチャ結果領域 | キャプチャ画像と領域（仮想画面座標）を表示。画像は `CaptureCompleted`（同期イベント）で更新 |
| Crop プレビュー領域 | グリッド付きダミー画像（640×400）を表示。ドラッグで選択 → Enter / ダブルクリックで確定 |
| 操作ログ領域 | ボタン操作・セッション結果・`CaptureCompleted` 受信を時分秒ミリ秒付きで表示 |

## 4. キャプチャ操作の流れ

1. **全画面 / ウィンドウ選択**: ボタンを押すと全画面透明オーバーレイが表示されます。カーソルを移動すると対象（スクリーン / ウィンドウ）がハイライトされ、クリックでキャプチャが確定します。Escape でキャンセルできます。
2. **範囲選択（AreaSelect）**: ボタンを押すと仮想スクリーンのスナップショットが背景に表示され、ドラッグで範囲を選択します（ルーペが追従）。クリックでキャプチャが確定します。Escape でキャンセルできます。
3. **切出し（Crop）**: 「プレビューで切出し」ボタンを押すと、右側のプレビュー画像上に選択矩形と暗転マスクが表示されます。ドラッグで範囲を選択し、Enter / ダブルクリック（選択内）で確定します。Escape でキャンセルできます。
   - Crop 中のホイール操作: **通常ホイール**でプレビュー画像をスクロール、**Ctrl + ホイール**でズームイン/アウト（選択矩形・暗転マスク・ルーペは追従）。

> **CaptureCompleted（同期イベント）**: キャプチャ確定時、`CaptureCompleted` イベント（同期実行）が発行され、サンプルはここでキャプチャ画像を更新します。
> 同期イベントのため、ハンドラ（画像変更）の完了後にキャプチャセッションがオーナーを再表示する「画像変更 → オーナー再表示」の順序保証を確認できます。

## 5. プロジェクト構成

```text
CaptureSessionControl.AvaloniaUI/sample/
├── CaptureSessionControl.AvaloniaUI.Sample.csproj # サンプルアプリのプロジェクト（net10.0-windows / Avalonia 12.1）
├── app.manifest                # アプリケーションマニフェスト（DPI 認識 PerMonitorV2）
├── Program.cs                  # エントリポイント（Avalonia 起動）
├── App.axaml(.cs)              # アプリケーション本体（FluentTheme）
├── ViewModels/
│   └── MainWindowViewModel.cs  # キャプチャ結果・設定・操作ログの保持（CommunityToolkit.Mvvm の ObservableObject）
└── Views/
    ├── MainWindow.axaml        # メインウィンドウのレイアウト
    └── MainWindow.axaml.cs     # CaptureSessionControl の呼び出し・結果反映・ダミー画像生成
```

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
