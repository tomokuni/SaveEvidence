# TODO（後で対応する課題）

## 課題: `CropSession`（SnipCaptureUI）が `ZoomViewControl` の内部 `Image.Stretch` を参照して Fit 判定している

### 状況

- `CropSession`（SnipCaptureUI/src/Sessions/CropSession.cs）が、`ZoomViewControl.ImageControl`（内部 `Image`）の `Stretch == Stretch.Uniform` を参照して「Fit 表示中か」を判定している。
- `ZoomViewControl.ImageControl` は、Crop セッション（`SnipCapture.StartCropAsync(Control previewControl, ...)`）が対象画像コントロールを必要とするために公開している。

### あるべき姿

- `CropSession` が内部 `Image.Stretch`（実装の詳細）に依存せず、`ZoomViewControl` の意味的な状態（`ZoomPercent <= 0` = Fit）で判定できるようにする。
- これにより `ZoomViewControl` のカプセル化が強化され、内部 `Image` の露出（`ImageControl`）に依存しない設計になる。

### 対応方針（検討事項）

- `ZoomViewControl` に「Fit 中かどうか」を意味的に公開する（例: `bool IsFit => ZoomPercent <= 0;` または既存の `ZoomPercent` で判定）。
- `CropSession` 側で `previewControl` を `ZoomViewControl` 型として受け取り、`Image.Stretch` の代わりに該当する状態で判定する。
- `SnipCaptureUI` プロジェクト側の変更が必要（`CropSession` の `CoordinatePort.ImageLayout(...)` の判定ロジックを修正）。

### 対象外とした理由（当初）

- `SnipCaptureUI` プロジェクトの責務であり、`ZoomViewControl` 単体の改善範囲を超えるため。

### 備考

- `ZoomViewControl` 側では `ZoomPercent <= 0` で同判定が可能。
- 対応時は、`ZoomViewControl` のカプセル化（`ImageControl` 公開の要否）も合わせて再評価する。
