# CaptureSessionControl.MewUI

## 目次

- [CaptureSessionControl.MewUI](#capturesessioncontrolmewui)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. 依存プロジェクト](#3-依存プロジェクト)
  - [4. クイックスタート](#4-クイックスタート)
  - [5. ドキュメント](#5-ドキュメント)
  - [6. プロジェクト構成](#6-プロジェクト構成)
  - [7. ライセンス](#7-ライセンス)

---

## 1. 概要

画面キャプチャ（スクリーン選択 / ウィンドウ選択 / 範囲選択）の **MewUI 版セッション**です。仮想スクリーン（全モニター）を 1 枚の画像として敷いたオーバーレイを表示し、選択された領域を切り出して返します。

撮影・切り出しの共通規則（切り出し矩形・設定・セッション状態・オーナー退避）は `CaptureSessionControl.Core` が単一所有し、本プロジェクトは MewUI の描画・入力・ウィンドウ操作だけを担います。

## 2. 機能一覧

- **3 種のキャプチャモード**: スクリーン選択（カーソル下のモニター）/ ウィンドウ選択（カーソル下の最前面ウィンドウ）/ 範囲選択（ドラッグ）
- **仮想スクリーン全体を対象**: 全モニターを 1 枚の画像として取得し、オーバーレイで全面を覆う（1 モニターに限定するとカーソルのあるモニターでしか操作できないため）
- **自ウィンドウの写り込み防止**: 撮影前にキャプチャ除外（`WDA_EXCLUDEFROMCAPTURE`）を設定し、画面外へ退避する
- **ちらつきの防止**: オーナーの退避は非表示ではなく位置の変更。オーバーレイは透明で表示し、画像が合成されたフレームで透明を解除する
- **選択矩形のクロップ**: 確定時は閉区間（始点・終点のピクセルを含む）で切り出す（計算は Core の `CropBoundsMath`）- **プレビューの切り出し**（`CropOverlay`）: プレビューの上へ領域選択のオーバーレイを重ね、ドラッグで指定した領域で現在の画像を切り出す（適用先は Core の `ICaptureTarget`）
- **ラベル描画**: サイズラベル・操作ラベル・情報ラベルを選択矩形の左上へ表示する
- **UI 非依存のサービス注入**: キャプチャ・要素検出・ウィンドウ操作は `PlatformFactory.Core` の抽象を受け取る（本ライブラリはアプリの型を参照しない）

## 3. 依存プロジェクト

```text
CaptureSessionControl.MewUI
    ├── CaptureSessionControl.Core（撮影・切り出しの共通規則）
    ├── PlatformFactory.MewUI（コア型 ⇔ MewUI 型の Interop / DIP 変換）
    ├── PlatformFactory.Core（ICaptureService / IElementFinder / IWindowService）
    ├── FadeMaskDragRectControl.MewUI（暗転マスク + 選択矩形 + ルーペ）
    └── CropViewControl.MewUI（切り出しビュー。将来の切り出しセッション用）
```

依存は一方向です（アプリから本ライブラリを参照し、逆は参照しない）。

## 4. クイックスタート

```csharp
using Aprillz.MewUI;
using Estable.MewUI.CaptureSession;
using Estable.Platform;

// 撮影セッションに渡すサービスと設定を組み立てる（ホストが生成する）
var services = new CaptureOverlayServices(
    PlatformFactory.CreateCaptureService(),
    PlatformFactory.CreateElementFinder(),
    PlatformFactory.CreateWindowService(),
    captureSettings);          // CaptureSessionControl.Core の CaptureSettings

// オーバーレイを表示し、確定した領域を取得する（null はキャンセル）
EsBitmap? image = CaptureOverlay.Capture(ownerWindow, CaptureType.AreaSelect, services);
```

`CaptureSettings` は `CaptureSessionControl.Core`（名前空間 `Estable.CaptureSession`）が提供します。ホストは設定画面の値からこれを作り、`CaptureOverlayServices.Settings` へ渡します（アプリの `Settings` → `CaptureSettings` の変換は `app.Core` の `CaptureSettingsMapper` が担います）。

## 5. ドキュメント

- `SPEC_ExtFunc.md` — 公開 API の外部仕様
- `SPEC_System.md` — 設計・依存・実測で判明した制約

## 6. プロジェクト構成

```text
src/
│   ├── CaptureSessionControl.MewUI.csproj  # ライブラリ（net10.0-windows）
│   ├── CaptureOverlay.cs                   # オーバーレイ（撮影セッション。公開 API は静的 Capture）
│   ├── CropOverlay.cs                      # プレビューの切り出しセッション（公開クラス）
│   ├── CaptureOverlayServices.cs           # サービスと設定の束（公開 record）
│   ├── CaptureLog.cs                       # ログ定義（internal / LoggerMessage）
│   └── ImageView.cs                        # 画像を領域いっぱいへ描画するビュー（internal）
└── tests/                                  # 単体テスト（未作成）
```

## 7. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
