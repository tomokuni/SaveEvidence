# FadeMaskDragRectControl

> プロジェクト名: `FadeMaskDragRectControl.AvaloniaUI`（Avalonia UI 版）。UI 非依存のロジックは `FadeMaskDragRectControl.Core`、MewUI 版は `FadeMaskDragRectControl.MewUI` にあります。

## 目次

- [FadeMaskDragRectControl](#fademaskdragrectcontrol)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [AXAML](#axaml)
    - [Code-behind（モード・設定・座標ポート設定）](#code-behindモード設定座標ポート設定)
  - [6. 座標系](#6-座標系)
  - [7. サンプル](#7-サンプル)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. プロジェクト構成](#9-プロジェクト構成)
  - [10. ライセンス](#10-ライセンス)

---

## 1. 概要

Avalonia UI 用の**領域選択複合コントロール**です。ドラッグ選択（`MarqueeControl`。暗転マスクはその**外側の装飾**として描画される）・拡大鏡（`LoupeControl`）の**組み立て・配線・状態同期を 1 箇所に集約**し、ホスト（上位 AP）へ統一インターフェースを提供します。ホストはライフサイクルとモード固有処理に専念できます。

## 2. 機能一覧

- **複合構造（Facade / Composite）**: Marquee（外側の装飾を含む）+ Loupe の生成・配置・配線を 1 コントロールに集約
- **配線の内包**: `SelectionChanged` → 暗転マスク同期、`LoupePositionMoved` → ルーペ中心更新、確定・キャンセル・ズーム要求のホスト通知を内部で完結
- **モード切替**: `Crop` / `AreaSelect` / `ScreenSelect` / `WindowSelect` で描画モード・Marquee / Loupe の有効・無効・全面暗転を一括切替
- **設定の一括適用**: `Options` プロパティ（1 つの設定値）で選択矩形 / 暗転マスク / ルーペ / 選択矩形破線を 1 回で適用（既定値・項目の対応・枠線の太さの共用規則はコアが単一所有）
- **外観の一括適用**: `MarqueeAppearance` / `LoupeAppearance` プロパティ（描画にだけ必要な外観の単一値）で内包する選択矩形・ルーペの外観を適用（コアの設定値は色・表示可否を持たない）
- **座標変換ポートの一括分配**: `SetCoordinateTransform(ICoordTransform?)` で 2 コントロールへ同一ポートを分配
- **ビューポート変化の追従**: `NotifyViewportChanged()` でパン・ズーム等のビューポート変化を通知（Marquee の再描画 + ルーペ位置再計算。ScrollViewer 連携のスクロールも同じ経路で処理）
- **選択状態の外部設定**: `SetSelection` / `ClearSelection` で選択矩形を設定（選択状態の単一のソースは `MarqueeControl` のセッションであり、本コントロールはその読み取りと委譲のみを行う。ルーペの選択矩形破線は `LoupeRendered` イベント経由で描画）
- **ルーペ描画完了の通知**: `LoupeRendered` イベントで描画済み Bitmap と切り出し左上・スケールを通知（ホストが外部提供矩形を Bitmap へ直接描画できる）
- **ログの伝搬**: `Log` プロパティ（`ILogger`）で内包するドラッグ選択コントロールとルーペの操作ログをホストのログ基盤へ転送
- **ルーペ制御**: `SetLoupePosition` / `SetLoupeSource` / `ClearLoupe` / `LoupeCenterPoint` で中心・ソース・後片付けを制御
- **ハイライト状態**: `LogicalHighlightRect` / `DisplayScaleScaling` で ScreenSelect / WindowSelect のハイライトを設定（**null が「ハイライトなし」を意味する**）
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
using EsUtil.SaveEvidence.FadeMaskDragRect.AvaloniaUI;
using EsUtil.Platform.Core.Coordinate; // ICoordTransform / CoordTransform（コア提供）
```

## 5. クイックスタート

### AXAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:fdr="clr-namespace:EsUtil.SaveEvidence.FadeMaskDragRect.AvaloniaUI;assembly=FadeMaskDragRectControl.AvaloniaUI">
    <Grid>
        <Image x:Name="VirtualScreen" Stretch="Fill" />
        <fdr:FadeMaskDragRectControl x:Name="SelectionOverlay" />
    </Grid>
</Window>
```

### Code-behind（モード・設定・座標ポート設定）

```csharp
// モード（描画モード・Marquee / Loupe の有効・無効・全面暗転を一括切替）
SelectionOverlay.Mode = FadeMaskDragRectMode.AreaSelect;

// 設定（ホストの設定型からコアの設定値へ変換して 1 回で適用する。変換はコアのマッパーが単一所有）
SelectionOverlay.Options = captureSettings.ToFadeMaskDragRectOptions();

// 一部の項目だけを変える場合はコアの既定値から with 式で組み立てる
SelectionOverlay.Options = FadeMaskDragRectOptions.Default with
{
    Marquee = MarqueeOptions.Default with { HandleSize = 12.0 },
    Loupe = LoupeOptions.Default with { RequestedSize = 200, ZoomLevel = 2 },
};

// 外観（色・枠線の太さ・表示可否）は外観の単一値をプロパティへ設定する
SelectionOverlay.MarqueeAppearance = MarqueeAppearance.Default with
{
    OverlayColor = System.Drawing.Color.FromArgb(77, 0, 0, 0), // 黒 30%
    OuterBorderThickness = 3.0,
};
SelectionOverlay.LoupeAppearance = LoupeAppearance.Default with { BorderColor = System.Drawing.Color.Red };

// DPI スケーリングは設定（Options）の対象外（環境値。個別プロパティで設定）
SelectionOverlay.DomainToDipScaling = 1.0;
SelectionOverlay.LoupeDipToDeviceScaling = 1.0;

// 座標変換ポート（ドメイン = 仮想スクリーンピクセル。Marquee / Loupe へ一括分配）
SelectionOverlay.SetCoordinateTransform(CoordTransform.DomainScale(() => scaling));

// イベント（配線は内部で完結。確定・キャンセル・ズーム要求のみ購読）
SelectionOverlay.ConfirmRequested += (_, e) => OnConfirm(e.Rect);
SelectionOverlay.CancelRequested += (_, _) => OnCancel();
SelectionOverlay.ZoomRequested += (_, request) => OnZoom(request.ZoomIn, request.Position);
SelectionOverlay.ScrollRequested += (_, request) => OnScroll(request.DeltaX, request.DeltaY, request.Shift);

// ルーペソース（元画像 / 仮想スクリーン Bitmap）
SelectionOverlay.Loupe.Source = bitmap;
```

## 6. 座標系

- **ドメイン座標**: 元画像ピクセル（Crop）または仮想スクリーンピクセル（AreaSelect）など、ホストが定義する論理座標。
- **DIP 座標**: コントロールローカル DIP。`SetCoordinateTransform` で注入した `ICoordTransform` がドメイン ↔ DIP を双方向変換します（描画時に毎回評価され、ズーム・スクロール・ウィンドウサイズ変更に追従）。
- **閉区間描画**: 選択領域は始点・終点を含む閉区間として扱い、暗転マスク（非暗転領域）とルーペの選択枠（`LoupeRendered` イベント経由でホスト側が描画）が一致します。

## 7. サンプル

- 統合サンプル: `CaptureSessionControl.AvaloniaUI/sample/`（画面キャプチャ・切出しの全体フロー）
- 個別サンプル: `MarqueeControl.AvaloniaUI/sample/` / `LoupeControl.AvaloniaUI/sample/`

## 8. ドキュメント

| 文書 | 内容 |
| --- | --- |
| [SPEC_ExtFunc.md](SPEC_ExtFunc.md) | 外部機能仕様書（公開 API・座標系・使用手順） |
| [SPEC_System.md](SPEC_System.md) | システム仕様書（システム概要・アーキテクチャ・コンポーネント図） |

## 9. プロジェクト構成

```text
FadeMaskDragRectControl.AvaloniaUI/
├── src/
│   ├── FadeMaskDragRectControl.AvaloniaUI.csproj
│   ├── FadeMaskDragRectControl.cs              （複合コントロール本体）
│   ├── FadeMaskDragRectSelectionEventArgs.cs   （確定要求のイベント引数）
│   └── FadeMaskDragRectPointEventArgs.cs       （ルーペ位置のイベント引数）
└── tests/
    ├── FadeMaskDragRectControl.AvaloniaUI.Tests.csproj
    ├── TestAppBuilder.cs
    ├── FadeMaskDragRectControl.Tests.cs
    └── FadeMaskDragRectControlState.Tests.cs
```

### 9.1 共有コア（`FadeMaskDragRectControl.Core`）

UI 非依存の設定の単一値（`FadeMaskDragRectOptions`）・動作モード・モードから導出する状態・選択矩形の可否判定・
ルーペの選択矩形オーバーレイ描画は `src/FadeMaskDragRectControl.Core`（名前空間 `EsUtil.SaveEvidence.FadeMaskDragRect`）が
単一所有します（Avalonia / MewUI で共有）。

```text
FadeMaskDragRectControl.Core/
├── src/
│   ├── FadeMaskDragRectMode.cs       （動作モード enum。両 UI が同一の型を使用する）
│   ├── FadeMaskDragRectModes.cs      （モード → 状態の解決）
│   ├── FadeMaskDragRectState.cs      （モードから導出される表示可否）
│   ├── FadeMaskDragRectOptions.cs    （設定の単一値と枠線の共用規則）
│   ├── FadeMaskDragRectSelection.cs  （選択矩形の可否判定）
│   └── FadeMaskDragRectOverlay.cs    （選択矩形の破線オーバーレイ描画）
└── tests/
```

### 9.2 UI 間の共有層（`FadeMaskDragRectControl.UIBase`）

「設定とモードから子コントロールへ適用する値の解決」と「付加情報ラベル（操作・情報）の描画」は
`src/FadeMaskDragRectControl.UIBase`（名前空間 `EsUtil.SaveEvidence.FadeMaskDragRect.UIBase`）が単一所有します
（Avalonia / MewUI で共有。片方の UI だけ分配規則が変わる余地をなくすため）。

```text
FadeMaskDragRectControl.UIBase/
├── src/
│   ├── FadeMaskDragRectChildValues.cs    （設定とモードから子コントロールへ適用する値の解決）
│   └── FadeMaskHostLabelRenderer.cs      （付加情報ラベルの積み上げと描画）
└── tests/
```

本コントロールは、解決された値を子コントロールへ代入し、描画ポートを用意して文言を渡すだけです
（ラベルの体裁と行送りは `Feature.Marquee.Presenter` の `MarqueeHostLabelRenderer` / `MarqueeAppearance` が
単一所有します）。

## 10. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
