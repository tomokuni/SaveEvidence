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
- **選択矩形のクロップ**: 確定時は種別に応じて切り出す（範囲選択は閉区間 = 始点・終点のピクセルを含む。切り出し矩形の解決は Core の `CaptureClipMath.ResolveCaptureRect`（**画面物理座標の入口**。選択矩形は画像ローカル座標のため `ToScreenRect` で戻してから渡す）、切り出しは Core の `CaptureClipMath`）
- **キーボード操作**: Enter（Return を含む）= 確定 / Escape = キャンセル（確定対象が無い状態の Enter は何もしない。解釈は Core の `CaptureKeyActions` が単一所有する）
- **クリック確定**: 左押下でカーソル下の要素を固定し、解放時に確定する（押下を伴わない解放・退化した検出では確定しない。規則は Core の `CaptureClickSelection` が単一所有する）
- **プレビューの切り出し**（`CaptureSessionControl.ToggleCrop`）: プレビューの上へ領域選択のオーバーレイを重ね、ドラッグで指定した領域で現在の画像を切り出す（適用先は Core の `ICaptureTarget`）
- **撮影と切り出しの排他**: 同時に実行できるのはどちらか 1 つ（規則は Core の `CaptureSessionSlot` が単一所有し、本ライブラリの Facade がその唯一の適用者になる）
- **設定の単一所有と即時反映**: 設定（`AppearanceSettings`）は Facade が単一所有し、撮影と切り出しで同じ値を使う。設定時は部分更新（null メンバーのみ現在値を維持）・検証・同値スキップ・適用ログを Core（`CaptureSettingsMerge.Apply`）が担い、実行中の切り出しセッションへは即時反映する
- **ラベル描画**: サイズラベル・操作ラベル・情報ラベルを選択矩形の左上へ表示する
- **UI 非依存のサービス注入**: キャプチャ・要素検出・ウィンドウ操作は `Platform.Core` の抽象を受け取る（本ライブラリはアプリの型を参照しない）

## 3. 依存プロジェクト

```text
CaptureSessionControl.MewUI
    ├── CaptureSessionControl.Core（撮影・切り出しの共通規則）
    ├── Platform.MewUI（コア型 ⇔ MewUI 型の Interop / DIP 変換）
    ├── Platform.Core（ICaptureService / IElementFinder / IWindowService / ICaptureTarget）
    ├── FadeMaskDragRectControl.MewUI（暗転マスク + 選択矩形 + ルーペ）
    └── CropViewControl.MewUI（切り出しビュー。切り出しセッションの対象）
```

依存は一方向です（アプリから本ライブラリを参照し、逆は参照しない）。

## 4. クイックスタート

```csharp
using Aprillz.MewUI;
using EsUtil.SaveEvidence.CaptureSession.MewUI;
using EsUtil.Platform.Core;

// 撮影・切り出しの Facade を生成する（切り出しの対象とオーバーレイを重ねる領域はホストが用意する）
var session = new CaptureSessionControl(
    cropView,                  // 切り出しの対象（CropViewControl）
    captureTarget,             // 切り出しの適用先（Core の ICaptureTarget）
    cropHost,                  // 切り出しのオーバーレイを重ねる Grid
    captureSettings,           // CaptureSessionControl.Core の CaptureSettings
    themeBackground,           // 切り出しのルーペの市松模様の下地色
    logger);

// 撮影: オーバーレイを表示し、確定した領域を取得する（null はキャンセル。切り出し中は受付しない）
EsBitmap? image = session.Capture(ownerWindow, CaptureType.AreaSelect, services);

// 切り出し: 開始 → 確定 → キャンセルを 1 つの操作で切り替える
session.ToggleCrop();

// 設定の変更（設定画面など）は同じ Facade へ反映する（実行中の切り出しセッションへ即時反映される）
session.AppearanceSettings = changed;
```

`CaptureSettings` は `CaptureSessionControl.Core`（名前空間 `EsUtil.SaveEvidence.CaptureSession`）が提供します。ホストは設定画面の値からこれを作り、**Facade（コンストラクタまたは `AppearanceSettings`）へ渡します**（撮影と切り出しが同じ値を使う。アプリの `Settings` → `CaptureSettings` の変換は `app.Core` の `CaptureSettingsMapper` が担います）。

## 5. ドキュメント

- `SPEC_ExtFunc.md` — 公開 API の外部仕様
- `SPEC_System.md` — 設計・依存・実測で判明した制約

## 6. プロジェクト構成

```text
src/
│   ├── CaptureSessionControl.MewUI.csproj  # ライブラリ（net10.0-windows）
│   ├── CaptureSessionControl.cs            # Facade（公開 API。撮影と切り出しの窓口）
│   ├── CapturePaintMapper.cs               # 設定から描画にだけ必要な色（MarqueePaint / LoupePaint）を組み立てる
│   ├── CaptureOverlay.cs                   # 撮影セッション（internal。1 セッション = 1 インスタンス）
│   ├── Sessions/CropSession.cs             # 切り出しセッション（internal。1 セッション = 1 インスタンス）
│   ├── CaptureOverlayServices.cs           # サービス（キャプチャ・検出・ウィンドウ・ロガー）の束（公開 record）
│   └── （撮影画像の描画は Platform.MewUI の SkiaImageView を使う）
```

本プロジェクトは UI 操作（描画・入力・ウィンドウ）のみを持ち、共通規則は `CaptureSessionControl.Core` のテストで検証します（本プロジェクトにテストプロジェクトは置きません）。

## 7. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
