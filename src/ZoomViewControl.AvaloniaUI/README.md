# ZoomViewControl

> プロジェクト名: `ZoomViewControl.AvaloniaUI`（Avalonia UI 版）。UI 非依存のロジックは `ZoomViewControl.Core`、MewUI 版は `ZoomViewControl.MewUI` にあります。

## 目次

- [ZoomViewControl](#zoomviewcontrol)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [AXAML（ZoomViewControl の配置）](#axamlzoomviewcontrol-の配置)
      - [設定（ズーム範囲・倍率）](#設定ズーム範囲倍率)
    - [画像の設定とイベント購読](#画像の設定とイベント購読)
  - [6. ズーム操作](#6-ズーム操作)
  - [7. サンプル](#7-サンプル)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. プロジェクト構成](#9-プロジェクト構成)
  - [10. ライセンス](#10-ライセンス)

---

## 1. 概要

画像プレビューのズーム・パン・Fit 表示を提供する独立ライブラリです。

- **ZoomViewControl**: 自己完結型複合コントロール。内部ビューポートを内包し、公開 API をコアの `ZoomViewSession`（状態と判断を単一所有）へ委譲する。特定コントロールに依存せず、`ZoomChanged` イベントで実効ズーム率・Fit 状態・可否をホストへ通知（表示文字列の生成はホストの責務）
- **設定（`ZoomConfig`）**: ズーム遷移のパラメータ（`MinZoom` / `MaxZoom` / `WheelFactor` / `ButtonZoomLevels`）をまとめた検証付き不変値（`public`）。**コンストラクタ注入でカスタマイズする**（旧設定プロパティは参照がないため削除した）。既定値（`Default`）も用意され、不正な値はガードされる
- 上位 AP は `ZoomViewControl` の `Source` を設定し、`ZoomChanged` を購読するだけで、ズーム計算・表示の詳細は本ライブラリが担います

## 2. 機能一覧

- **ズーム遷移**: ⊖⊕ ボタンの 23 段階遷移 / Ctrl+ホイールの ×1.1 連続遷移
- **ズーム中心維持**: マウスポインタ位置 / ビューポート中心を基準に拡大縮小
- **パン**: マウスドラッグ（grab 方式）/ ホイール / スクロールバー
- **ホイール操作の公開 API**: `PanByWheel` / `ZoomByWheel`（ホストが他のコントロールで受けたホイールを転送する。換算とズーム率の決定は本コントロールが単一所有）
- **Fit 表示**: ビューポートに収まる Uniform スケール（自動）と実効倍率の表示
- **座標変換**: ビューポート ↔ コンテンツ（元画像ピクセル）の双方向変換。オフセット基準に加え、画像表示矩形の原点基準（Fit 中央寄せ補正込み）も提供
- **スクロールバー**: オーバーレイ表示（コンテンツがビューポートより大きい場合のみ）
- **イベント**: `ZoomChanged`（実効ズーム率・Fit 状態・可否の通知）/ `ViewportChanged`（表示状態の変化通知）。Ctrl+ホイールのズーム要求は内部で自己完結するため、ホストは購読不要
- **ログ**: `ZoomViewControl.Log` への `ILogger` 注入（未設定時は `NullLogger`）

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\ZoomViewControl.AvaloniaUI\src\ZoomViewControl.AvaloniaUI.csproj" />
```

必要な名前空間:

```csharp
using EsUtil.SaveEvidence.ZoomView.AvaloniaUI;
```

## 5. クイックスタート

### AXAML（ZoomViewControl の配置）

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:zoom="clr-namespace:EsUtil.SaveEvidence.ZoomView.AvaloniaUI;assembly=ZoomViewControl.AvaloniaUI">
    <zoom:ZoomViewControl x:Name="ZoomView" Background="#FF2B2B2B"/>
</Window>
```

#### 設定（ズーム範囲・倍率）

ズーム遷移のパラメータ（`MinZoom` / `MaxZoom` / `WheelFactor` / `ButtonZoomLevels`）は `ZoomConfig`（検証付き不変値）に集約されます。既定値（`ZoomConfig.Default`）で動作させ、必要に応じて **コンストラクタ注入**（`new ZoomViewControl(config)`）でカスタマイズできます。不正な値は `ZoomConfig` がガードします。

### 画像の設定とイベント購読

```csharp
// ZoomViewControl に画像を設定（Fit 表示にリセット）とロガー注入
zoomView.Log = logger;                  // 任意（未設定時は NullLogger）
zoomView.Source = image;

// ズーム率の変化をスライダと ⊖⊕ ボタンの状態へ同期する（ZoomViewControl は特定コントロールに依存しない）
zoomView.ZoomChanged += (_, e) =>
{
    zoomSlider.Value = e.ZoomPercent;
    zoomInButton.IsEnabled = e.CanZoomIn;
    zoomOutButton.IsEnabled = e.CanZoomOut;
};

// スライダの変更を ZoomViewControl へ反映（XAML ロード中の Slider 初期化でも
// ValueChanged が発火するため、依存注入後のコンストラクタで購読する）
zoomSlider.ValueChanged += (_, e) => zoomView.ZoomPercent = zoomSlider.Value;

// 初期表示時（ZoomChanged 発火前）は、ズーム可否プロパティでボタン状態を初期化する
zoomInButton.IsEnabled = zoomView.CanZoomIn;
zoomOutButton.IsEnabled = zoomView.CanZoomOut;
```

> Ctrl+ホイールのズーム・ビューポートリサイズ・パンの配線は内部ビューポートが自己完結します（セッションへ直接働きかけるため、ホスト側で購読する必要はありません）。
> `ZoomViewControl` が内部で自己完結するため、ホスト側で購読する必要はありません。
> ホストは `ZoomChanged`（スライダ・⊖⊕ ボタン同期用）と `ViewportChanged`（オーバーレイ追従用）のみ購読します。
> 座標変換（`ViewportToContentImage` / `ContentToViewportImage`）・画像表示矩形（`ImageRectViewport`）・
> Fit 状態（`IsFit`）・ズーム操作（`ZoomIn` / `ZoomOut` / `ZoomFit`）も `ZoomViewControl` が Facade として提供します
> （内部 `ZoomableView` へは到達しません。内部 `Image` コントロールも公開しません）。

## 6. ズーム操作

| 操作 | 内容 |
| --- | --- |
| ⊕ / ⊖ ボタン | ズームイン / ズームアウト（23 段階。段階リストはコントロール内部で管理） |
| Ctrl + ホイール | マウスポインタ位置を中心にズーム（×1.1 / ÷1.1 連続遷移） |
| マウスドラッグ | パン（grab 方式。掴んだ位置がポインタに追従） |
| ホイール / Shift+ホイール | 縦パン / 横パン |
| Fit（ズーム率 0） | ビューポートに収まる Uniform スケール（自動） |

- Fit 中は実効倍率の % を表示し、⊕ / ⊖ ・Ctrl+ホイールは実効倍率を基準に遷移します。
- ズーム率の遷移ルール・表示方式の詳細は `SPEC_ExtFunc.md` を参照してください。

## 7. サンプル

`sample/` に動作確認用のサンプルアプリケーションがあります（テスト画像のズーム・パン・Fit・スライダ・Ctrl+ホイール・ポインタ座標表示）。

```powershell
dotnet run --project sample/ZoomViewControl.AvaloniaUI.Sample.csproj
```

詳細は `sample/README.md` を参照してください。

## 8. ドキュメント

- `SPEC_ExtFunc.md`（外部機能仕様書）
- `SPEC_System.md`（システム仕様書）

## 9. プロジェクト構成

本コントロールは、UI フレームワーク非依存のロジックを `ZoomViewControl.Core` へ分離しています（マルチ UI 対応）。

```text
ZoomViewControl.AvaloniaUI/
├── src/          # ライブラリ本体（Avalonia 実装）
│   ├── ZoomViewControl.cs      # 自己完結型複合コントロール（公開 API の委譲・通知の転送）
│   ├── ZoomableView.cs         # ScrollViewer 不使用のパン・ズームビューポート（入力ブリッジ・描画の適用）
│   └── ZoomChangedEventArgs.cs # ズーム変更イベント引数（コアの ZoomState を保持する薄い型）
├── tests/        # 単体テスト（ZoomViewControl.AvaloniaUI.Tests）
│   ├── ZoomViewControl.Tests.cs  # 公開 API・Fit 移行・実効ズーム率
│   └── ZoomableView.Tests.cs     # 画像配置・Transform・スクロールバー同期・入力ブリッジ
└── sample/       # サンプルアプリケーション

../ZoomViewControl.Core/        # UI 非依存の状態と判断（ZoomViewSession）、設定（ZoomConfig）、
                                # 契約（ZoomState / ZoomFeedback / ZoomNotice）、外観（ZoomViewAppearance）、ログ
```

## 10. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
