# CropView

## 目次

- [CropView](#cropview)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [AXAML](#axaml)
    - [Code-behind](#code-behind)
  - [6. サンプルアプリケーション](#6-サンプルアプリケーション)
  - [7. ドキュメント](#7-ドキュメント)
  - [8. プロジェクト構成](#8-プロジェクト構成)
  - [9. ライセンス](#9-ライセンス)

---

## 1. 概要

Avalonia UI 用の**画像表示 + 領域選択を統合した高レベル複合コントロール**です。

- **画像ビューポート**（`ZoomViewControl`）: 拡大・原寸・Fit 表示・パン
- **領域選択オーバーレイ**（`FadeMaskDragRectControl`）: ドラッグ選択・暗転マスク・ルーペ
- **外部提供矩形オーバーレイ**: ホストから渡された矩形（破線・色・幅）を View とルーペの両方へ描画

これらを 1 つのコントロールに統合し、ホスト（上位 AP）へ単一インターフェースを提供します。
ホストは画像ソースの設定・選択の受け渡し・ズーム操作に専念できます。

## 2. 機能一覧

- **複合構造（Facade / Composite）**: `ZoomViewControl` + `FadeMaskDragRectControl` + 矩形オーバーレイを内包
- **画像表示**: 拡大（`ZoomIn` / `ZoomOut`）/ 原寸 / Fit 表示（`ZoomFit`）/ パン（マウスドラッグ・ホイール）
- **領域選択**: ドラッグ選択・暗転マスク・ルーペ（`FadeMaskDragRectControl` へ委譲）
  - オーバーレイの可視性・ヒットテストは `SetSelectionOverlayVisible` で制御（Crop セッションの開始・終了時にホストが切替）。初期状態（画像なし）は非表示・ヒットテスト不可。
- **外部提供矩形**: `Rects`（`IReadOnlyList<OverlayRect>`）を View（ビューポート）とルーペの両方へ描画
  - 矩形情報（`OverlayRect`）: 矩形・破線種類・前景色・背景色・線幅
  - 破線種類（`OverlayLineStyle`）: `Solid` / `Dashed` / `Dotted`
- **座標変換**: `ContentToViewport` / `ViewportToContent`（ドメイン ↔ ビューポート座標）
- **選択モード**: `SelectionMode`（`FadeMaskDragRectMode`）で切替（既定 `Crop`）
- **イベント**: `ConfirmRequested` / `CancelRequested` / `ZoomRequested` / `LoupePositionMoved`（FadeMaskDragRectControl へ委譲）/ `RectsChanged`（外部矩形の変化通知）

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\src\CropViewControl.csproj" />
```

必要な名前空間:

```csharp
using Estable.AvaloniaUI.CropView;
using Estable.AvaloniaUI.FadeMaskDragRect; // FadeMaskDragRectMode
using Avalonia; // Point / Rect
```

## 5. クイックスタート

### AXAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:crop="clr-namespace:Estable.AvaloniaUI.CropView;assembly=CropViewControl">
    <crop:CropViewControl x:Name="CropPreview" />
</Window>
```

### Code-behind

```csharp
// 画像を設定（Fit 表示にリセット）
CropPreview.Source = bitmap;

// 外部提供矩形（破線・色・幅）を表示
CropPreview.Rects = new[]
{
    new OverlayRect(new Rect(10, 10, 200, 150), OverlayLineStyle.Dashed, Colors.Orange, Colors.White, 2.0),
};

// ズーム操作（拡大・縮小・Fit）
CropPreview.ZoomIn();
CropPreview.ZoomOut();
CropPreview.ZoomFit();

// 選択モード（既定 Crop）
CropPreview.SelectionMode = FadeMaskDragRectMode.Crop;

// 座標変換（ドメイン座標 ↔ ビューポート座標）
var viewport = CropPreview.ContentToViewport(new Point(100, 100));
var content = CropPreview.ViewportToContent(new Point(200, 200));

// 選択イベント
CropPreview.ConfirmRequested += (_, e) => OnConfirm(e.Rect);
CropPreview.CancelRequested += (_, _) => OnCancel();
```

## 6. サンプルアプリケーション

`SnipCaptureUI/sample/` が CropView を利用した動作確認サンプルです。
Crop プレビューを拡大・パン・Fit しながら、ドラッグ選択 → 確定で切り出しを確認できます。

## 7. ドキュメント

| 文書 | 内容 |
| --- | --- |
| [SPEC_ExtFunc.md](SPEC_ExtFunc.md) | 外部機能仕様書（公開 API・座標系・使用手順） |
| [SPEC_System.md](SPEC_System.md) | システム仕様書（システム概要・アーキテクチャ・コンポーネント図） |

## 8. プロジェクト構成

```text
CropViewControl/
├── src/
│   ├── CropViewControl.csproj
│   ├── CropViewControl.cs    （高レベル複合コントロール本体）
│   ├── OverlayRect.cs       （外部提供矩形の描画情報 record）
│   └── OverlayLineStyle.cs  （枠線スタイル enum）
└── tests/
    ├── CropViewControl.Tests.csproj
    ├── TestAppBuilder.cs
    └── CropViewControl.Tests.cs
```

## 9. ライセンス

本プロジェクトのライセンスに従います。
