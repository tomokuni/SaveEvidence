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
| `FadeMaskDragRectControl.Core` | 動作モード（`FadeMaskDragRectMode`）と、モードから子コントロールの有効・無効（選択矩形を使うか）への写像 |
| `MarqueeControl.MewUI` / `Feature.MarqueeCore` | 選択矩形の入力と状態機械 |
| `MarqueeControl.CoreForControl` | ホイール要求（`WheelZoomRequest` / `WheelScrollRequest`）の中継で型を参照する（解釈は内包コントロールが組み込みの層へ委ねる） |
| `Feature.MarqueeCore`（`Decoration` 名前空間） | 選択矩形の**外側に描く装飾**（暗転マスク・穴あけ枠・ハイライト）の設定・外観・幾何・描画内容の解決（名前空間 `EsUtil.Feature.MarqueeCore.Decoration`。描画は `MarqueeControl` が行う） |
| `LoupeControl.MewUI` / `Feature.LoupeCore` | 拡大鏡の描画と表示位置計算 |
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
| `LogicalHighlightRect` | `EsRect?` | `null` | ハイライト矩形（ドメイン座標。**null はハイライトなし**。選択矩形より優先） |
| `ShowFullOverlay` | `bool`（取得のみ） | `true` | 全面暗転の有無（モードから導出） |
| `LoupeCenterPoint` | `EsPoint?`（取得のみ） | `null` | ルーペ中心（ドメイン座標） |
| `Options` | `FadeMaskDragRectOptions`（コア） | `FadeMaskDragRectOptions.Default` | 設定の単一値（下記）。同じ値の再設定はスキップされる |
| `Scaling` | `double` | `1.0` | ドメイン → DIP 変換の基準倍率（設定には含めない環境値） |
| `DisplayScaleScaling` | `double` | `0`（`Scaling` と同値） | サイズラベルの DisplayScale 補正（環境値） |
| `LoupeRenderScaling` | `double` | `1.0` | ルーペの DIP → 物理ピクセル倍率（環境値） |

`Options`（コアの `FadeMaskDragRectOptions`）の項目:

| 項目 | 型 | 説明 |
| --- | --- | --- |
| `Marquee` | `MarqueeOptions`（コア） | 選択矩形の設定（枠線を除く） |
| `Loupe` | `LoupeOptions`（コア） | ルーペの設定（拡大率・表示サイズ・外観。市松模様の下地色も含む） |
| `SelectionRectColorA` / `SelectionRectColorB` | `System.Drawing.Color` | ルーペ画像上の選択矩形破線の色（2 色交互。既定は白 / 黒） |
| `LoupeOffset` | `double` | ルーペの中心からの右下オフセット（DIP。既定は 20） |

文言と環境値（設定値（`Options`）には含めない）:

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` | `Func<string?>?` | `null` | ラベル文言を描画時に評価するデリゲート（null または空文字を返す場合は描画しない。文言はホストが与える） |
| `Log` | `ILogger` | `NullLogger` | 操作ログの出力先（内包するドラッグ選択コントロールとルーペへ伝搬する。null は `NullLogger` へフォールバック。暗転マスクは選択のたびにログを出すため対象外） |

内包コントロールへの直接アクセス（`Marquee` / `Loupe`）は internal（自身のテストのみ）。ホストは `Options` / `Mode` / 文言プロパティの公開 API を使います。

## 6. メソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordinateTransform(ICoordTransform?)` | 座標変換ポートを一括設定する（ドラッグ選択とルーペへ分配）。null の場合は倍率からコアが解決する |
| `SetSelection(EsRect)` | 選択状態を外部から設定する（幅・高さのいずれかが 0 以下の場合は選択なし） |
| `ClearSelection()` | 選択状態をクリアして全面暗転へ戻す |
| `SetLoupeSource(EsBitmap?)` | ルーペの拡大元画像を設定する（設定だけでは表示しない） |
| `SetLoupePosition(EsPoint)` | ルーペ中心（ドメイン座標）を設定して表示を開始する（画像が未設定の間は表示しない） |
| `ClearLoupe()` | ルーペを非表示にして画像と中心を破棄する |
| `RequestConfirm()` | 確定要求を処理する（Enter キー相当） |
| `RequestCancel()` | キャンセル要求を処理する（Escape キー相当） |
| `FocusMarquee()` | Marquee 選択へフォーカスを設定する（矢印キー nudge の受付開始） |
| `NotifyViewportChanged()` | ビューポートの変化を通知する（再描画とルーペ位置の再計算を要求） |
| `InvalidateOverlay()` | 暗転マスクの再描画を要求する |

## 7. イベント

| イベント | 引数 | 発火条件 |
| --- | --- | --- |
| `ConfirmRequested` | `FadeMaskDragRectSelectionEventArgs` | 矩形内ダブルクリック・Enter・`RequestConfirm`（選択確定後のみ） |
| `CancelRequested` | `EventArgs` | 矩形外ダブルクリック・Escape・`RequestCancel` |
| `ZoomRequested` | `WheelZoomRequest` | Ctrl+ホイール（方向と発生位置） |
| `ScrollRequested` | `WheelScrollRequest` | 通常ホイール（生の回転量・Shift・発生位置。スクロール先はホストが決める） |
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
- ルーペ画像上の選択矩形破線は、選択矩形を使うモード（`Crop` / `AreaSelect`）でのみ描画します（破線の幾何はコアの `FadeMaskDragRectOverlay` が、描画の可否はコアの `FadeMaskDragRectState.ShowSelectionOverlay` が単一所有する）。選択矩形を変更した場合はルーペの再描画を要求します（破線はルーペのプロパティに現れないため）。
- サイズラベルの文言は本コントロールが自動生成しません。ホストが `SizeLabelOverrideProvider` で与えます（指定しない場合の既定の文言はコアの `MarqueeSizeLabel.Format` が単一所有する）。
- サイズラベルとカーソル座標ラベルは選択矩形コントロールが**テキスト要素（`Border` + `TextBlock`）として配置**します（実寸から配置矩形を求める写像は描画層 `MarqueeControl.Rendering` の `MarqueeLabelPlan.Resolve` が単一所有する）。操作・情報ラベルはアプリ固有の付加情報のため、本コントロールが `Options.Labels` の外観に従って描きます（文言は `CaptureLabelTexts` が与える）。

## 9. Avalonia 版との違い

| 項目 | 差異 |
| --- | --- |
| イベント引数の座標 | コア型（`EsPoint` / `EsRect`）で公開する |
| 外観設定 | コアの `FadeMaskDragRectOptions` を 1 プロパティ（`Options`）として公開する（Avalonia 版と同じ設定値を使う） |
| ルーペの位置決め | 本コントロールがコアの計算で配置する（Avalonia 版はルーペ自身が算出する） |
| ルーペの描画スケール | 自動追従（Avalonia 版は `LoupeRenderScaling` の手動設定が必要） |
| サイズラベル・操作ラベル・情報ラベル | サイズラベルはコントロールが表示し、操作・情報ラベルはホストが `Options.Labels` の外観で描く |
| ルーペ画像上の選択矩形破線 | 描画する（同じコアの破線幾何を使う。描画先はキャンバスと `EsBitmap` の違い） |
| ルーペ描画完了の通知 | 本コントロールが内部で受けて破線を描くため、ホストへは中継しない（Avalonia 版は `LoupeRendered` で中継し、ホストがルーペ画像へ直接描く） |
| `ScrollViewer` の自動連携 | ホストが `NotifyViewportChanged()` を呼ぶ |
| ログ | `Log` プロパティで同様に伝搬する（書式は Avalonia 版と同一） |

## 10. エラー・例外

| 条件 | 動作 |
| --- | --- |
| `SetCoordinateTransform(null)` | 倍率からコアが座標変換を解決する（倍率 1.0 なら等価変換・境界なし） |
| `SetSelection` にサイズ 0 以下の矩形 | 選択なしとして扱う（例外は発生しない） |
| ルーペ画像が未設定の状態で `SetLoupePosition` | 中心のみ記録し、表示しない（例外は発生しない） |
| 未定義の `Mode` を設定 | `WindowSelect` と同じ扱いになる（例外は発生しない） |
