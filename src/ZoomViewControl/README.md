# ZoomViewControl

## 目次

- [ZoomViewControl](#zoomviewcontrol)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [AXAML（ZoomableView の配置）](#axamlzoomableview-の配置)
    - [IZoomHost の実装](#izoomhost-の実装)
    - [ZoomManager の注入](#zoommanager-の注入)
  - [6. ズーム操作](#6-ズーム操作)
  - [7. サンプル](#7-サンプル)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. プロジェクト構成](#9-プロジェクト構成)
  - [10. ライセンス](#10-ライセンス)

---

## 1. 概要

画像プレビューのズーム・パン・Fit 表示を提供する独立ライブラリです。

- **ZoomableView**: ScrollViewer を使用しない自己完結型ビューポート（RenderTransform ベース）
- **ZoomManager**: ズーム率の遷移・適用・スライダ同期を統括（`IZoomHost` を注入）
- **純粋関数**: `ZoomMath`（ズーム中心維持）/ `ZoomTransition`（段階遷移）/ `ScrollMetrics`（スクロールバー幾何）
- 上位 AP は `IZoomHost` を実装して注入するだけで、ズーム計算・表示の詳細は本ライブラリが担います

## 2. 機能一覧

- **ズーム遷移**: ⊖⊕ ボタンの 23 段階遷移 / Ctrl+ホイールの ×1.1 連続遷移
- **ズーム中心維持**: マウスポインタ位置 / ビューポート中心を基準に拡大縮小
- **パン**: マウスドラッグ（grab 方式）/ ホイール / スクロールバー
- **Fit 表示**: ビューポートに収まる Uniform スケール（自動）と実効倍率の表示
- **座標変換**: ビューポート ↔ コンテンツ（元画像ピクセル）の双方向変換
- **スクロールバー**: オーバーレイ表示（コンテンツがビューポートより大きい場合のみ）
- **イベント**: `ZoomRequested`（Ctrl+ホイール）/ `ViewportChanged`（表示状態の変化通知）
- **ログ**: `ZoomManager.Log` への `ILogger` 注入（未設定時は `NullLogger`）

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\ZoomViewControl\src\ZoomViewControl.csproj" />
```

必要な名前空間:

```csharp
using Estable.AvaloniaUI.ZoomView;
```

## 5. クイックスタート

### AXAML（ZoomableView の配置）

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:zoom="clr-namespace:Estable.AvaloniaUI.ZoomView;assembly=ZoomViewControl">
    <zoom:ZoomableView x:Name="ZoomableView" Background="#FF2B2B2B"/>
</Window>
```

### IZoomHost の実装

```csharp
// ホスト（ViewModel 等）が IZoomHost を実装
public sealed partial class MainViewModel : ObservableObject, IZoomHost
{
    public int ZoomPercent { get; set; }
    public Bitmap? PreviewImage { get; set; }
    public bool CanZoomIn { get; set; }
    public bool CanZoomOut { get; set; }
    public string ZoomDisplayText { get; set; } = "100%";
    public bool CenterAlign => /* 設定値 */;
}
```

### ZoomManager の注入

```csharp
// ZoomManager に注入
var zoomManager = new ZoomManager
{
    ViewModel = viewModel,
    ZoomableView = zoomableView,
    ZoomSlider = slider,
};

// スライダの変更を ZoomManager へ反映（XAML ロード中の Slider 初期化でも
// ValueChanged が発火するため、依存注入後のコンストラクタで購読する）
zoomSlider.ValueChanged += (_, e) => zoomManager.ApplyZoomFromSlider(zoomSlider.Value);

// 画像変更時にソースを設定（Fit 表示にリセット）
zoomableView.SetSource(image);

// Ctrl+ホイールのズーム要求（マウスポインタ位置をズーム中心に設定）
zoomableView.ZoomRequested += (_, e) =>
{
    zoomManager.PointerPosition = e.PointerPosition;
    if (e.ZoomIn) zoomManager.ZoomInByWheel();
    else zoomManager.ZoomOutByWheel();
    zoomManager.PointerPosition = null;
};
```

## 6. ズーム操作

| 操作 | 内容 |
| --- | --- |
| ⊕ / ⊖ ボタン | ズームイン / ズームアウト（`ZoomTransition.ButtonZoomLevels` の 23 段階） |
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
│   ├── ZoomManager.cs          # ズーム遷移・適用・スライダ同期の統括
│   ├── ZoomableView.cs         # ScrollViewer 不使用のパン・ズームビューポート
│   ├── ZoomMath.cs             # ズーム中心維持・Fit 実効倍率・座標変換（純粋関数）
│   ├── ZoomTransition.cs       # 段階遷移・×1.1 遷移（純粋関数）
│   ├── ScrollMetrics.cs        # スクロールバー幾何（純粋関数）
│   ├── IZoomHost.cs            # ホスト抽象インターフェース
│   ├── ZoomRequestedEventArgs.cs
│   └── Logging/                # ログ定義（LogMessage）
├── tests/        # 単体テスト（ZoomViewControl.Tests）
└── sample/       # サンプルアプリケーション
```

## 10. ライセンス

このリポジトリのライセンス方針に従います。ライセンスが明示されていない環境では、再配布・商用利用の条件を確認してから利用してください。
