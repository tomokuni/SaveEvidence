# FadeMaskDragRectControl.MewUI 外部機能仕様書

`FadeMaskDragRectControl.MewUI` の外部仕様（公開 API・動作モード・操作）を記載します。内部実装の詳細は記載しません。

## 1. 概要

領域選択の複合コントロール（MewUI 版）です。選択矩形（ドラッグ）・暗転マスク・ルーペを 1 つのコントロールにまとめ、
配線と状態同期を内側で完結させます。ホストは `Mode` の設定と状態の受け取りだけで利用できます。

動作モードとモードから描画設定への写像は UI 非依存コア（`FadeMaskDragRectControl.Core`）が単一所有し、
Avalonia 版と同一の解釈になります。

## 2. 依存関係

| 参照 | 用途 |
| --- | --- |
| `FadeMaskDragRectControl.Core` | 動作モード（`FadeMaskDragRectMode`）とモードから描画設定への写像 |
| `DragRectControl.MewUI` / `DragRectControl.Core` | 選択矩形の入力と状態機械 |
| `FadeMaskControl.MewUI` / `FadeMaskControl.Core` | 暗転マスクの描画 |
| `LoupeControl.MewUI` / `LoupeControl.Core` | 拡大鏡の描画と表示位置計算 |
| `Platform.Core` | コア幾何型・座標変換ポート・画像コンテナ |
| `Aprillz.MewUI.Skia`（0.21.1 固定） | `Panel`・`MewProperty`・SkiaSharp |

## 3. 座標系

| 座標系 | 意味 |
| --- | --- |
| ドメイン座標 | 選択対象の座標（画像ピクセル・仮想スクリーンピクセル等）。選択矩形とルーペ中心はこの座標で扱う |
| ローカル DIP | コントロールのローカル座標。入力と描画はこの座標で行う |

両者の変換は座標変換ポート（`ICoordTransform`）が担い、選択矩形・暗転マスク・ルーペの配置で共用します。

## 4. 動作モード

| モード | 選択矩形 | ルーペ | 全面暗転 | 用途 |
| --- | --- | --- | --- | --- |
| `Crop` | 有効 | 有効 | 有効 | プレビュー内の切出し |
| `AreaSelect` | 有効 | 有効 | 有効 | 全画面オーバーレイの領域選択 |
| `ScreenSelect` | 無効 | 無効 | 無効 | スクリーン選択（ハイライトのみ。確定フローはホスト） |
| `WindowSelect` | 無効 | 無効 | 無効 | ウィンドウ選択（ハイライトのみ。確定フローはホスト） |

既定値は `AreaSelect` です。未定義の値を設定した場合は `WindowSelect` と同じ扱いになります。

## 5. プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Mode` | `FadeMaskDragRectMode` | `AreaSelect` | 動作モード（可視性と全面暗転を一括切替） |
| `HasSelection` | `bool`（取得のみ） | `false` | 選択矩形が有効か |
| `SelectionRect` | `EsRect`（取得のみ） | 空矩形 | 選択矩形（ドメイン座標。未選択時は 0x0） |
| `HasHighlight` | `bool` | `false` | ハイライトを穴あけ矩形として使うか |
| `LogicalHighlightRect` | `EsRect?` | `null` | ハイライト矩形（ドメイン座標。選択矩形より優先） |
| `ShowFullOverlay` | `bool`（取得のみ） | `true` | 全面暗転の有無（モードから導出） |
| `LoupeCenterPoint` | `EsPoint?`（取得のみ） | `null` | ルーペ中心（ドメイン座標） |
| `LoupeOffset` | `double` | `20.0` | ルーペの中心からの右下オフセット（DIP） |
| `OverlayColor` | `System.Drawing.Color` | 暗転マスクの既定（黒 50%） | 暗転の色（不透明度はアルファに統合） |
| `BorderColor` | `System.Drawing.Color` | 暗転マスクの既定 | 選択枠線の色（選択矩形と暗転マスクで共用） |
| `BorderThickness` | `double` | 暗転マスクの既定 | 選択枠線の太さ（DIP。選択矩形と暗転マスクで共用） |
| `ShowSizeLabel` | `bool` | `true` | サイズラベル（1 行目）を表示するか |
| `SizeLabelTextProvider` | `Func<string?>?` | `null` | サイズラベルの文言を描画時に評価するデリゲート（null または空文字を返す場合は描画しない） |
| `SizeLabelFontSize` | `double` | `14.0` | サイズラベルのフォントサイズ（DIP） |
| `SizeLabelColor` | `System.Drawing.Color` | 白 | サイズラベルの文字色 |
| `SizeLabelBackgroundColor` | `System.Drawing.Color` | 黒 60% | サイズラベルの背景色（アルファ 0 の場合は黒 50% へフォールバック） |
| `SubLines` | `IReadOnlyList<FadeMaskSubLine>?` | `null` | サイズラベルの下に積む行（2 行目以降） |
| `SelectionOverlayColorA` / `SelectionOverlayColorB` | `System.Drawing.Color` | 白 / 黒 | ルーペ画像上の選択矩形破線の色（2 色交互） |
| `Log` | `ILogger` | `NullLogger` | 操作ログの出力先（内包するドラッグ選択コントロールとルーペへ伝搬する。null は `NullLogger` へフォールバック。暗転マスクは選択のたびにログを出すため対象外） |
| `Port` | `ICoordTransform?`（取得のみ） | 等価変換 | 暗転マスクへ適用中の座標変換ポート |
| `Overlay` | `FadeMaskControl`（取得のみ） | - | 暗転マスク（外観の詳細設定用） |
| `DragRect` | `DragRectControl`（取得のみ） | - | ドラッグ選択（外観の詳細設定用） |
| `Loupe` | `LoupeControl`（取得のみ） | - | 拡大鏡（外観の詳細設定用） |

## 6. メソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordinateTransform(ICoordTransform?)` | 座標変換ポートを一括設定する。null の場合は等価変換（1 ドメインピクセル = 1 DIP・境界なし） |
| `SetSelection(EsRect)` | 選択状態を外部から設定する（幅・高さのいずれかが 0 以下の場合は選択なし） |
| `ClearSelection()` | 選択状態をクリアして全面暗転へ戻す |
| `SetDragRectSelection(EsRect)` | 選択矩形をドラッグ選択コントロールへ復元する（選択変更が暗転マスクへ伝播する） |
| `SetLoupeSource(EsBitmap?)` | ルーペの拡大元画像を設定する（設定だけでは表示しない） |
| `SetLoupePosition(EsPoint)` | ルーペ中心（ドメイン座標）を設定して表示を開始する（画像が未設定の間は表示しない） |
| `ClearLoupe()` | ルーペを非表示にして画像と中心を破棄する |
| `RequestConfirm()` | 確定要求を処理する（Enter キー相当） |
| `RequestCancel()` | キャンセル要求を処理する（Escape キー相当） |
| `FocusDragRect()` | ドラッグ選択へフォーカスを設定する（矢印キー nudge の受付開始） |
| `NotifyViewportChanged()` | ビューポートの変化を通知する（再描画とルーペ位置の再計算を要求） |
| `InvalidateOverlay()` | 暗転マスクの再描画を要求する |

## 7. イベント

| イベント | 引数 | 発火条件 |
| --- | --- | --- |
| `ConfirmRequested` | `FadeMaskDragRectSelectionEventArgs` | 矩形内ダブルクリック・Enter・`RequestConfirm`（選択確定後のみ） |
| `CancelRequested` | `EventArgs` | 矩形外ダブルクリック・Escape・`RequestCancel` |
| `ZoomRequested` | `DragRectZoomRequest` | Ctrl+ホイール（方向と発生位置） |
| `LoupePositionMoved` | `FadeMaskDragRectPointEventArgs` | ドラッグ選択からのルーペ中心の更新（マウス移動・ドラッグ・nudge） |

座標はすべてドメイン座標です。

## 8. 操作仕様

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ | 選択矩形の作成・リサイズ・移動（内包するドラッグ選択コントロールが処理する） |
| 矢印キー | 選択矩形の nudge（内包コントロールへフォーカスがある場合のみ） |
| Enter / Escape | 確定・キャンセル要求を発火する |
| Ctrl + ホイール | ズーム要求を発火する（ズームの実処理はホスト） |
| マウス移動 | ルーペ中心を更新し、表示位置を再計算する |

### 制限

- 選択矩形を使わないモード（`ScreenSelect` / `WindowSelect`）では、ドラッグ選択とルーペを非表示にします。
- ルーペは拡大元画像（`SetLoupeSource`）と中心（`SetLoupePosition`）の両方が設定された場合のみ表示します。
  親領域の外へはみ出す場合は内側へクランプします。
- ルーペ画像上の選択矩形破線は、選択矩形を使うモード（`Crop` / `AreaSelect`）でのみ描画します（破線の幾何はコアの `FadeMaskDragRectOverlay` が単一所有します）。
- ラベルの文言は本コントロールが自動生成しません。ホストが `SizeLabelTextProvider` と `SubLines` で与えます
  （文言の単一所有は `FadeMaskControl.Core` の `FadeMaskLabels`。表示位置の計算は同コアの `FadeMaskLabelLayout`）。

## 9. Avalonia 版との違い

| 項目 | 差異 |
| --- | --- |
| イベント引数の座標 | コア型（`EsPoint` / `EsRect`）で公開する |
| 外観設定 | `AppearanceSettings` の一括設定は持たない（内包コントロールの個別プロパティで設定する） |
| ルーペの位置決め | 本コントロールがコアの計算で配置する（Avalonia 版はルーペ自身が算出する） |
| ルーペの描画スケール | 自動追従（Avalonia 版は `LoupeRenderScaling` の手動設定が必要） |
| サイズラベル・情報ラベル・サブライン | 描画する（Avalonia 版は設定 record から自動生成するが、MewUI 版はホストがデリゲートで与える） |
| ルーペ画像上の選択矩形破線 | 描画する（同じコアの破線幾何を使う。描画先はキャンバスと `EsBitmap` の違い） |
| `ScrollViewer` の自動連携 | ホストが `NotifyViewportChanged()` を呼ぶ |
| ログ | `Log` プロパティで同様に伝搬する（書式は Avalonia 版と同一） |

## 10. エラー・例外

| 条件 | 動作 |
| --- | --- |
| `SetCoordinateTransform(null)` | 等価変換（境界なし）を適用する |
| `SetSelection` にサイズ 0 以下の矩形 | 選択なしとして扱う（例外は発生しない） |
| ルーペ画像が未設定の状態で `SetLoupePosition` | 中心のみ記録し、表示しない（例外は発生しない） |
| 未定義の `Mode` を設定 | `WindowSelect` と同じ扱いになる（例外は発生しない） |
