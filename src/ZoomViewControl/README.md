# ZoomViewControl

画像プレビューのズーム・パン・Fit 表示を提供する独立ライブラリです。

## 概要

- **ZoomableView**: ScrollViewer を使用しない自己完結型ビューポート（RenderTransform ベース）
- **ZoomManager**: ズーム率の遷移・適用・スライダ同期を統括（`IZoomHost` を注入）
- **純粋関数**: `ZoomMath`（ズーム中心維持）/ `ZoomTransition`（段階遷移）/ `ScrollMetrics`（スクロールバー幾何）
- 上位 AP は `IZoomHost` を実装して注入するだけで、ズーム計算・表示の詳細は本ライブラリが担います

## 使用例

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

// ZoomManager に注入
var zoomManager = new ZoomManager
{
    ViewModel = viewModel,
    ZoomableView = zoomableView,
    ZoomSlider = slider,
};
```

## ドキュメント

- `SPEC_ExtFunc.md`（外部機能仕様書）
- `SPEC_System.md`（システム仕様書）
