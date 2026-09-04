# FadeMaskDragRectControl

## 目次

- [FadeMaskDragRectControl](#fademaskdragrectcontrol)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [AXAML](#axaml)
    - [Code-behind（モード・外観・座標ポート設定）](#code-behindモード外観座標ポート設定)
  - [6. 座標系](#6-座標系)
  - [7. サンプル](#7-サンプル)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. プロジェクト構成](#9-プロジェクト構成)
  - [10. ライセンス](#10-ライセンス)

---

## 1. 概要

Avalonia UI 用の**領域選択複合コントロール**です。ドラッグ選択（`DragRectControl`）・暗転マスク（`FadeMaskControl`）・拡大鏡（`LoupeControl`）の**組み立て・配線・状態同期を 1 箇所に集約**し、ホスト（上位 AP）へ統一インターフェースを提供します。ホストはライフサイクルとモード固有処理に専念できます。

## 2. 機能一覧

- **複合構造（Facade / Composite）**: DragRect + FadeMask + Loupe の生成・配置・配線を 1 コントロールに集約
- **配線の内包**: `SelectionChanged` → 暗転マスク同期、`LoupePositionMoved` → ルーペ中心更新、確定・キャンセル・ズーム要求のホスト通知を内部で完結
- **モード切替**: `Crop` / `AreaSelect` / `ScreenSelect` / `WindowSelect` で描画モード・DragRect / Loupe の有効・無効・全面暗転を一括切替
- **外観の一括設定**: `AppearanceSettings` プロパティ（nullable record・部分更新）で DragRect / FadeMask / Loupe の外観を 1 回で設定
- **座標変換ポートの一括分配**: `SetCoordinateTransform(ICoordinatePort?)` で 3 コントロールへ同一ポートを分配
- **ビューポート変化の追従**: `NotifyViewportChanged()` でパン・ズーム等のビューポート変化を通知（DragRect / FadeMask の再描画 + ルーペ位置再計算。ScrollViewer 連携のスクロールも同じ経路で処理）
- **選択状態の外部設定**: `SetSelection` / `ClearSelection` / `SetDragRectSelection` で選択矩形を暗転マスクへ同期（ルーペの選択矩形破線は `Rendered` イベント経由で描画）
- **ルーペ制御**: `SetLoupePosition` / `SetLoupeSource` / `ClearLoupe` / `LoupeCenterPoint` で中心・ソース・後片付けを制御
- **ハイライト状態**: `LogicalHighlightRect` / `HasHighlight` / `ScreenIndex` / `DisplayScaleScaling` / `WindowTitle` で ScreenSelect / WindowSelect のハイライトを設定
- **公開型のカプセル化**: イベント引数は独自型（`FadeMaskDragRectSelectionEventArgs` / `FadeMaskDragRectPointEventArgs`）、内包コントロールへの直接アクセスは internal（公開 API は本コントロールのみ）
- **Avalonia Binding 対応の StyledProperty**（内包コントロールが公開）

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\FadeMaskDragRectControl\src\FadeMaskDragRect.csproj" />
```

必要な名前空間:

```csharp
using Estable.AvaloniaUI.FadeMaskDragRect;
using Estable.AvaloniaUI.Platform; // ICoordinatePort / CoordinatePort
```

## 5. クイックスタート

### AXAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:fdr="clr-namespace:Estable.AvaloniaUI.FadeMaskDragRect;assembly=FadeMaskDragRect">
    <Grid>
        <Image x:Name="VirtualScreen" Stretch="Fill" />
        <fdr:FadeMaskDragRectControl x:Name="SelectionOverlay" />
    </Grid>
</Window>
```

### Code-behind（モード・外観・座標ポート設定）

```csharp
// モード（描画モード・DragRect / Loupe の有効・無効・全面暗転を一括切替）
SelectionOverlay.Mode = FadeMaskDragRectMode.AreaSelect;

// 外観設定（DragRect / FadeMask / Loupe を一括設定。null メンバーは現在値維持の部分更新）
SelectionOverlay.AppearanceSettings = new FadeMaskDragRectSettings
{
    MinSelectionSize = 6,
    BorderColor = Colors.White,
    BorderThickness = 2.0,
    OverlayColor = Color.FromAColor(77, Colors.Black), // 黒 30%（不透明度は色のアルファに統合）
    RequestedSize = 200,
    ZoomLevel = 2,
});

// 座標変換ポート（ドメイン = 仮想スクリーンピクセル。DragRect / FadeMask / Loupe へ一括分配）
SelectionOverlay.SetCoordinateTransform(CoordinatePort.DomainScale(() => scaling));

// イベント（配線は内部で完結。確定・キャンセル・ズーム要求のみ購読）
SelectionOverlay.ConfirmRequested += (_, e) => OnConfirm(e.Rect);
SelectionOverlay.CancelRequested += (_, _) => OnCancel();
SelectionOverlay.ZoomRequested += (_, zoomIn) => OnZoom(zoomIn);

// ルーペソース（元画像 / 仮想スクリーン Bitmap）
SelectionOverlay.Loupe.Source = bitmap;
```

## 6. 座標系

- **ドメイン座標**: 元画像ピクセル（Crop）または仮想スクリーンピクセル（AreaSelect）など、ホストが定義する論理座標。
- **DIP 座標**: コントロールローカル DIP。`SetCoordinateTransform` で注入した `ICoordinatePort` がドメイン ↔ DIP を双方向変換します（描画時に毎回評価され、ズーム・スクロール・ウィンドウサイズ変更に追従）。
- **閉区間描画**: 選択領域は始点・終点を含む閉区間として扱い、暗転マスク（非暗転領域）とルーペの選択枠（`Rendered` イベント経由でホスト側が描画）が一致します。

## 7. サンプル

- 統合サンプル: `SnipCaptureUI/sample/`（画面キャプチャ・切出しの全体フロー）
- 個別サンプル: `DragRectControl/sample/` / `FadeMaskControl/sample/` / `LoupeControl/sample/`

## 8. ドキュメント

| 文書 | 内容 |
| --- | --- |
| [SPEC_ExtFunc.md](SPEC_ExtFunc.md) | 外部機能仕様書（公開 API・座標系・使用手順） |
| [SPEC_System.md](SPEC_System.md) | システム仕様書（システム概要・アーキテクチャ・コンポーネント図） |

## 9. プロジェクト構成

```text
FadeMaskDragRectControl/
├── src/
│   ├── FadeMaskDragRect.csproj
│   ├── FadeMaskDragRectControl.cs   （複合コントロール本体）
│   ├── FadeMaskDragRectMode.cs      （動作モード enum）
│   └── FadeMaskDragRectSettings.cs  （外観設定 record）
└── tests/
    ├── FadeMaskDragRect.Tests.csproj
    ├── TestAppBuilder.cs
    └── FadeMaskDragRectControl.Tests.cs
```

## 10. ライセンス

本プロジェクトのライセンスに従います。
