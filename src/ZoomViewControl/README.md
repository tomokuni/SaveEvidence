# ZoomViewControl

## 目次

- [ZoomViewControl](#zoomviewcontrol)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [AXAML（ZoomViewControl の配置）](#axamlzoomviewcontrol-の配置)
    - [画像の設定とイベント購読](#画像の設定とイベント購読)
  - [6. ズーム操作](#6-ズーム操作)
  - [7. サンプル](#7-サンプル)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. プロジェクト構成](#9-プロジェクト構成)
  - [10. ライセンス](#10-ライセンス)

---

## 1. 概要

画像プレビューのズーム・パン・Fit 表示を提供する独立ライブラリです。

- **ZoomViewControl**: 自己完結型複合コントロール。内部 ZoomableView を内包し、ズーム率（実効ズーム率）の遷移・適用・Fit 表示・可否を統括。特定コントロールに依存せず、`ZoomChanged` イベントで実効ズーム率・Fit 状態・可否をホストへ通知（表示文字列の生成はホストの責務）
- **ZoomableView（内部実装）**: ScrollViewer を使用しない自己完結型ビューポート（RenderTransform ベース）。`ZoomViewControl` が Facade として必要な低レベル機能（座標変換・画像表示矩形・ビューポート変化通知）を再公開
- **設定（`ZoomConfig`）**: ズーム遷移のパラメータ（`MinZoom` / `MaxZoom` / `WheelFactor` / `ButtonZoomLevels`）をまとめた検証付き不変値。コンストラクタ注入で指定し、`ZoomViewControl` のプロパティで参照・設定できる。クランプ・書式化に使用でき、不正な値はガードされる
- **純粋関数（内部実装）**: `ZoomMath` / `ZoomTransition` / `ScrollMetrics` はコントロール内部の計算のため非公開（遷移パラメータは `ZoomConfig` で受け取る純粋関数であり状態を持たない）
- 上位 AP は `ZoomViewControl` の `Source` を設定し、`ZoomChanged` を購読するだけで、ズーム計算・表示の詳細は本ライブラリが担います

## 2. 機能一覧

- **ズーム遷移**: ⊖⊕ ボタンの 23 段階遷移 / Ctrl+ホイールの ×1.1 連続遷移
- **ズーム中心維持**: マウスポインタ位置 / ビューポート中心を基準に拡大縮小
- **パン**: マウスドラッグ（grab 方式）/ ホイール / スクロールバー
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
<ProjectReference Include="..\ZoomViewControl\src\ZoomViewControl.csproj" />
```

必要な名前空間:

```csharp
using Estable.AvaloniaUI.ZoomView;
```

## 5. クイックスタート

### AXAML（ZoomViewControl の配置）

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:zoom="clr-namespace:Estable.AvaloniaUI.ZoomView;assembly=ZoomViewControl">
    <zoom:ZoomViewControl x:Name="ZoomView" Background="#FF2B2B2B"
                          MinZoom="20" MaxZoom="800" WheelFactor="1.2"/>
</Window>
```

#### 設定（ズーム範囲・倍率）

`ZoomViewControl` の `MinZoom` / `MaxZoom` / `WheelFactor` / `ButtonZoomLevels` は**読み書き可能プロパティ**として公開されており、XAML 属性またはコードビハインドで設定できます。設定値は `ZoomConfig`（検証付き不変値）で管理され、設定時に `ZoomConfig.With`（検証付き部分更新）を経由するため、不正な値はガードされます。

- **XAML 属性**: `MinZoom="20"` のように直接設定（数値のみ。レベル集合はコンマ区切りで設定不可のためコードで設定する）
- **コードビハインド**: `zoomView.MinZoom = 20;` / `zoomView.ButtonZoomLevels = [10, 20, 50, 100];` のように設定する
- **コンストラクタ注入**: `var zoomView = new ZoomViewControl(new ZoomConfig(20, 800, 1.2, [10, 20, 50, 100, 200]));` のように生成時に設定する（コードのみ・XAML では使用不可）

> `ButtonZoomLevels`（`IReadOnlyList<double>`）は XAML 属性では設定できないため、カスタムする場合はコードビハインドまたはコンストラクタで設定する。
> `ZoomConfig` はプロパティの直接変更ができません（読み取り専用）。値の部分更新は `ZoomConfig.With` 経由で行う。

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

> Ctrl+ホイールのズーム要求（`ZoomRequested`）・ビューポートリサイズ（`SizeChanged`）の配線は
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
dotnet run --project sample/ZoomViewControl.Sample.csproj
```

詳細は `sample/README.md` を参照してください。

## 8. ドキュメント

- `SPEC_ExtFunc.md`（外部機能仕様書）
- `SPEC_System.md`（システム仕様書）

## 9. プロジェクト構成

```text
ZoomViewControl/
├── src/          # ライブラリ本体
│   ├── ZoomViewControl.cs      # 自己完結型複合コントロール（ズーム遷移・適用・表示を統括）
│   ├── ZoomableView.cs         # ScrollViewer 不使用のパン・ズームビューポート
│   ├── ZoomConfig.cs           # ズーム遷移の設定（コンストラクタ注入・不変値）
│   ├── ZoomMath.cs             # ズーム中心維持・Fit 実効倍率・座標変換（純粋関数）
│   ├── ZoomTransition.cs       # 段階遷移・×1.1 遷移（純粋関数）
│   ├── ScrollMetrics.cs        # スクロールバー幾何（純粋関数）
│   ├── ZoomChangedEventArgs.cs # ズーム変更イベント引数
│   ├── ZoomAvailability.cs     # ズーム可否の値オブジェクト
│   ├── ZoomRequestedEventArgs.cs
│   └── Logging/                # ログ定義（LogMessage）
├── tests/        # 単体テスト（ZoomViewControl.Tests）
└── sample/       # サンプルアプリケーション
```

## 10. ライセンス

このリポジトリのライセンス方針に従います。ライセンスが明示されていない環境では、再配布・商用利用の条件を確認してから利用してください。
