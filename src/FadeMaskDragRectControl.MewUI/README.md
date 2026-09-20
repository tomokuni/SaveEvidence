# FadeMaskDragRectControl.MewUI

`FadeMaskDragRectControl`（領域選択の複合コントロール）の **MewUI 版**です。選択矩形（ドラッグ）・暗転マスク・
ルーペを 1 つのコントロールにまとめ、配線と状態同期を内側で完結させます。

## 特徴

- **Facade / Composite**: 3 つの MewUI コントロール（`DragRectControl` / `FadeMaskControl` / `LoupeControl`）を
  重ねて配置し、ホストへ統一インターフェースを提供します。ホストはライフサイクルとモード固有処理に専念できます。
- **モード切替**: `Mode` を設定するだけで、選択矩形とルーペの可視性・全面暗転が切り替わります
  （モードの解釈は `FadeMaskDragRectControl.Core` が単一所有）。
- **状態同期の内蔵**: 選択矩形の変更・開始・キャンセルを暗転マスクへ同期し、前の矩形の残留を防ぎます。
- **ルーペの自動配置**: 中心（ドメイン座標）+ オフセットから表示位置を計算し、親領域内へクランプします
  （計算はコアの `LoupePositionCalculator` が単一所有）。
- **コア型のイベント引数**: イベントの座標は UI 非依存のコア型（`EsPoint` / `EsRect`）で公開します。

## 使い方

```csharp
using Estable.FadeMaskDragRect;
using Estable.MewUI.FadeMaskDragRect;
using Estable.Platform.Coordinate;
using Estable.Platform.Geometry;

var selection = new FadeMaskDragRectControl
{
    Mode = FadeMaskDragRectMode.Crop,   // プレビュー内の切出し
    LoupeOffset = 20.0,
};

// ドメイン ↔ ローカル DIP の変換ポートを設定する（暗転マスク・選択矩形・ルーペで共用）
selection.SetCoordinateTransform(new CoordTransform(dip => dip, domain => domain, () => new EsSize(width, height)));

// ルーペの拡大元画像を設定してから、中心を指定して表示する
selection.SetLoupeSource(bitmap);
selection.SetLoupePosition(new EsPoint(120, 340));

// 確定・キャンセル・ズーム要求を購読する
selection.ConfirmRequested += (_, e) => Capture(e.SelectionRect);
selection.CancelRequested += (_, _) => Cancel();
selection.ZoomRequested += (_, zoomIn) => Zoom(zoomIn);
```

画像の上へ重ねる場合は、親の `Grid` へ追加します（本コントロールが選択の入力を受け取ります）。

```csharp
grid.Add(cropView);     // 画像（下）
grid.Add(selection);    // 暗転マスク + 選択矩形 + ルーペ（上）
```

## 動作モード

| モード | 選択矩形 | ルーペ | 全面暗転 | 用途 |
| --- | --- | --- | --- | --- |
| `Crop` | 有効 | 有効 | 有効 | プレビュー内の切出し |
| `AreaSelect` | 有効 | 有効 | 有効 | 全画面オーバーレイの領域選択 |
| `ScreenSelect` | 無効 | 無効 | 無効 | スクリーン選択（ハイライトのみ。確定はホスト） |
| `WindowSelect` | 無効 | 無効 | 無効 | ウィンドウ選択（ハイライトのみ。確定はホスト） |

## 主な API

| メンバー | 説明 |
| --- | --- |
| `Mode` | 動作モード（既定は `AreaSelect`） |
| `HasSelection` / `SelectionRect` | 現在の選択状態（ドメイン座標） |
| `HasHighlight` / `LogicalHighlightRect` | ハイライト（`ScreenSelect` / `WindowSelect` の穴あけ矩形） |
| `ShowFullOverlay` | 全面暗転の有無（モードから導出） |
| `LoupeCenterPoint` / `LoupeOffset` | ルーペ中心（ドメイン座標）と右下オフセット（DIP） |
| `OverlayColor` / `BorderColor` / `BorderThickness` | 暗転色・枠線（枠線は選択矩形と暗転マスクで共用） |
| `ShowSizeLabel` / `SizeLabelTextProvider` / `SizeLabelFontSize` / `SizeLabelColor` / `SizeLabelBackgroundColor` | サイズラベル（1 行目）の表示・文言・外観（文言はホストが与える） |
| `SubLines` | サイズラベルの下に積む行（2 行目以降。`FadeMaskSubLine` の一覧） |
| `SelectionOverlayColorA` / `SelectionOverlayColorB` | ルーペ画像上の選択矩形破線の色（2 色交互。既定は白 / 黒） |
| `Log` | 操作ログの出力先（null は `NullLogger`。内包するドラッグ選択コントロールとルーペへ伝搬する） |
| `Port` | 暗転マスクへ適用中の座標変換ポート |
| `Overlay` / `DragRect` / `Loupe` | 内包コントロール（外観の詳細設定用） |
| `SetCoordinateTransform(ICoordTransform?)` | 座標変換ポートを一括設定（null は等価変換） |
| `SetSelection` / `ClearSelection` | 選択状態の外部設定・クリア |
| `SetLoupeSource(EsBitmap?)` / `SetLoupePosition(EsPoint)` / `ClearLoupe` | ルーペの制御 |
| `RequestConfirm` / `RequestCancel` | Enter / Escape 相当の確定・キャンセル要求 |
| `SetDragRectSelection` / `FocusDragRect` | 選択矩形の復元・矢印キー nudge の受付開始 |
| `NotifyViewportChanged` / `InvalidateOverlay` | ビューポート変化の通知・暗転マスクの再描画要求 |

## イベント

`ConfirmRequested` / `CancelRequested` / `ZoomRequested` / `LoupePositionMoved`

## Avalonia 版との違い

| 項目 | Avalonia 版 | MewUI 版 |
| --- | --- | --- |
| イベント引数の座標 | Avalonia の `Point` / `Rect` | **コア型の `EsPoint` / `EsRect`** |
| 外観設定 | `AppearanceSettings`（40 超のメンバーを持つ設定 record）で一括設定 | **内包コントロール（`Overlay` / `DragRect` / `Loupe`）の個別プロパティで設定**（YAGNI） |
| ルーペの位置決め | ルーペコントロール自身が座標変換ポートから算出 | **本コントロールが `LoupePositionCalculator` で配置**（MewUI のルーペは位置を持たない） |
| サイズラベル・情報ラベル・サブライン | 描画する | 描画する（文言はコアの `FadeMaskLabels` とホストが与える） |
| ルーペ画像上の選択矩形破線 | 描画完了イベント経由で描画 | 同じコアの破線幾何を使い、ルーペの `Rendered` で同一のキャンバスへ描画する（見た目は一致） |
| `ScrollViewer` の自動連携 | `AttachScrollViewer` で購読 | ホストが `NotifyViewportChanged()` を呼ぶ |
| ログ | `ILogger` を注入して内包コントロールへ伝搬 | `Log` プロパティで同様に伝搬する（書式は同一） |

## テスト

```powershell
dotnet test src/FadeMaskDragRectControl.MewUI/tests/FadeMaskDragRectControl.MewUI.Tests.csproj
```

モード切替・選択状態の同期・外観の委譲・座標変換ポート・ルーペの配置（オフセットとクランプ）・ホストへの通知を検証します。

## ライセンス

このリポジトリのライセンス方針に従います。ライセンスが明示されていない環境では、再配布・商用利用の条件を確認してから利用してください。
