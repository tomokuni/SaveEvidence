# CaptureSessionControl.Core

## 目次

- [CaptureSessionControl.Core](#capturesessioncontrolcore)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. 依存プロジェクト](#3-依存プロジェクト)
  - [4. クイックスタート](#4-クイックスタート)
  - [5. ドキュメント](#5-ドキュメント)
  - [6. プロジェクト構成](#6-プロジェクト構成)
  - [7. ライセンス](#7-ライセンス)

---

## 1. 概要

撮影（スクリーン / ウィンドウ / 範囲選択）と切り出し（Crop）の**共通規則**を提供する UI 非依存ライブラリです。Avalonia 版（`CaptureSessionControl.AvaloniaUI`）と MewUI 版（`app.MewUI`）が同じ規則を使うため、規則の二重実装を防ぐ目的で独立しています。

## 2. 機能一覧

- **切り出し矩形の計算**（`CropBoundsMath`）: 選択矩形（半開区間）→ 画像内のピクセル矩形。画像内へのクランプ・端数の丸め（0.5 は切り上げ）・閉区間（始点・終点のピクセルを含む）・最小サイズ判定を単一所有する
- **外観・動作設定**（`CaptureSettings`）: 全メンバー nullable の record。null は「デフォルト値」または「現在値維持（部分更新）」を表し、`Validate` / `ToNonNull` を提供する
- **キャプチャ結果**（`CaptureResult`）: `EsBitmap?`（画像）と `EsPixelRect?`（仮想スクリーン座標の領域）の組
- **切り出し結果の適用先**（`ICaptureTarget`）: 撮影・切り出しの結果をアプリ側へ渡すポート（現在の画像・画像差し替えの通知・切り出しの適用）。アプリのドメイン操作（Undo 退避・未保存化・ステータス通知）は実装側に残す- **画像の配置計算**（`ImageDrawMath`）: 画像を描画先の領域へ配置する矩形（Uniform = 縦横比を保って中央 / Fill = 引き伸ばし）- **UI 非依存**: Avalonia / MewUI のどちらの型も参照しない（色は `System.Drawing.Color`、座標は `Es*` 型）

## 3. 依存プロジェクト

```text
CaptureSessionControl.Core
    └── Platform.Core（Es* 幾何型 / EsBitmap）
```

依存は一方向です（UI プロジェクトから本プロジェクトを参照し、逆は参照しない）。

## 4. クイックスタート

```csharp
using Estable.CaptureSession;
using EsUtil.Platform.Core.Geometry;

// 選択矩形（画像ピクセル・半開区間）から切り出し範囲を求める
var bounds = CropBoundsMath.ComputeCropBounds(
    new EsRect(10, 10, 40, 40),      // 選択矩形
    new EsSize(1920, 1080),          // 画像サイズ
    minSelectionSize: 6,             // これ未満は空（切り出し不可）
    closedInterval: true);           // 終点の 1 ピクセルを含める

if (!bounds.IsEmpty)
{
    // bounds で画像を切り出す
}
```

## 5. ドキュメント

- `SPEC_ExtFunc.md` — 公開 API の外部仕様
- `SPEC_System.md` — 設計・依存・検証方針

## 6. プロジェクト構成

```text
src/
│   ├── CaptureSessionControl.Core.csproj  # ライブラリ（net10.0）
│   ├── CropBoundsMath.cs                  # 切り出し矩形の計算
│   ├── ImageDrawMath.cs                   # 画像の配置矩形（Uniform / Fill）
│   ├── ICaptureTarget.cs                  # 切り出し結果の適用先（ポート）
│   ├── CaptureSettings.cs                 # 外観・動作設定（record）
│   └── CaptureResult.cs                   # キャプチャ結果（record）
└── tests/
    ├── CaptureSessionControl.Core.Tests.csproj
    ├── CropBoundsMath.Tests.cs            # クランプ・丸め・最小サイズ・閉区間（33 件）
    ├── ImageDrawMath.Tests.cs             # Uniform / Fill の配置と空の扱い（11 件）
    └── CaptureTargetContractTests.cs      # ICaptureTarget の契約（3 件）
```

## 7. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
