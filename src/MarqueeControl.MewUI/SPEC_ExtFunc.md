# MarqueeControl.MewUI 外部機能仕様書

`MarqueeControl.MewUI` の外部仕様（公開 API・イベント・動作）を記載します。内部実装の詳細は記載しません。

## 1. 概要

リサイズ可能なドラッグ矩形のコントロール（MewUI 版）です。画像やスクリーンの上へ重ねて使用し、
マウスドラッグ・矢印キーで選択矩形を作成・変形します。

選択矩形・ドラッグ・nudge のロジックは UI 非依存コア（`Feature.MarqueeCore`）が単一所有し、
Avalonia 版と同一の挙動になります。

## 2. 依存関係

| 参照 | 用途 |
| --- | --- |
| `Feature.MarqueeCore` | 選択幾何（`SelectionGeometry`）・状態機械（`MarqueeState`）・**公開ファサード（`Marquee`）/ 効果の適用先（`IMarqueeEffects`）**・公開型（`HitTarget` / `MarqueeInteractionState` / `MarqueeSnapshot`）。状態管理クラス（`MarqueeSession`）とフレーム解決（`MarqueeFrame`）は internal（ファサードの内部実装） |
| `MarqueeControl.Rendering` | 描画の手順（`MarqueeRenderer` / `MarqueeOuterRenderer` / `MarqueeInnerRenderer`）とラベル計画（`MarqueeLabelPlan`）。描画先は描画ポート（`IEsCanvas`）であり、本 UI は `SkiaEsCanvas`（Platform.Core）を適合させる |
| `Platform.Core` | コア幾何型（`EsPoint` / `EsRect` / `EsSize`）、`ICoordTransform` / `CoordTransform`、`ICursorService` |
| `Platform.Native` | カーソルサービスの既定実装（Win32 / NoOp） |
| `Aprillz.MewUI.Skia`（0.21.1 固定） | `SkiaCanvasView`・`MewProperty`・SkiaSharp 描画 |

## 3. 座標系

| 座標系 | 意味 |
| --- | --- |
| ドメイン座標 | 選択対象の座標（画像ピクセル・仮想スクリーンピクセル等）。コアの状態機械はこの座標で動作する |
| ローカル DIP | 本コントロールのローカル座標。入力と描画はこの座標で行う |

両者の変換は座標変換ポート（`ICoordTransform`）が担います。入力（Forward = DIP → ドメイン）と
描画（Inverse = ドメイン → DIP）の境界で適用されます。

## 4. プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Options` | `MarqueeOptions` | `MarqueeOptions.Default` | 選択矩形の外観・対話パラメータをまとめた不変の設定値（コアの `Estable.Marquee.MarqueeOptions`） |
| `DecoOptions` | `MarqueeDecoOptions` | `MarqueeDecoOptions.Default` | **装飾（暗転マスク・穴あけ枠・ラベル・ハイライト）の設定**（装飾層の `Estable.Marquee.Decoration.MarqueeDecoOptions`） |
| `SizeLabelOverrideProvider` | `Func<string?>?` | `null` | サイズラベル（1 行目）の文言を描画時に評価するプロバイダ。**文言はホストが与える**（コアは特定の文言を持たない。文言の生成はホストの `CaptureLabelTexts` 等が担う）。操作・情報ラベルの文言も同様にホストが与える |
| `RenderScaling` | `double` | `1.0` | DIP → 物理ピクセルのスケーリング係数（DPI 変更時に自動更新） |
| `Renderer` | `IMarqueeRenderer` | `MarqueeRenderer.Default` | 描画の実装（**描画の差し替え**。null 不可）。設定すると再描画とラベル要素の再配置を要求する（描画先は `SkiaEsCanvas` が担う） |

`MarqueeOptions` の項目（既定値はコアが単一所有。Avalonia 版と一致）:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `ShowHandles` | `bool` | `true` | ドラッグハンドルを表示するかどうか |
| `HandleColor` | `System.Drawing.Color` | 白 | ドラッグハンドルの塗り色 |
| `HandleSize` | `double` | `10.0` | ドラッグハンドルの一辺サイズ（DIP） |
| `HandleHitRadius` | `double` | `12.0` | ドラッグハンドルのヒットテスト半径（DIP。ドメインへ変換して判定する） |
| `MinSelectionSize` | `double` | `6.0` | 選択矩形の最小サイズ（ドメイン座標。これ未満のドラッグはキャンセル扱い） |
| `ClampToDomainBounds` | `bool` | `true` | ドメイン境界外の選択を禁止する。ON へ切り替えた瞬間に既存の選択矩形をクリップする |
| `MarkerColor` | `System.Drawing.Color` | 白 | nudge 中に表示する十字マーカーの色 |
| `MarkerLength` | `double` | `16.0` | 十字マーカーの全長（DIP。中心から両端までの半幅は 1/2） |
| `ShowCoordLabel` | `bool` | `false` | カーソル座標ラベル（`(x, y)`）の表示 |
| `CoordLabelColor` | `System.Drawing.Color` | 白 | 座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `System.Drawing.Color` | 黒 60%（`#99000000`） | 座標ラベルの背景色 |
| `CoordLabelFontSize` | `double` | `12.0` | 座標ラベルのフォントサイズ（6〜48 に補正。非数は 12 へ補正） |

設定の一部だけを変える場合は `MarqueeOptions.Default with { ... }` で指定します（指定しない項目は既定値を保つ）。
同値の設定を再適用しても再描画・ログは発生しません。
外観（色・サイズ）と対話パラメータ（`HandleHitRadius` / `MinSelectionSize` / `ClampToDomainBounds`）は同じ設定値に含まれますが、
後者だけがファサードへ伝搬します（判断と変更ログはコアが単一所有する）。

## 5. メソッド

| メソッド | 戻り値 | 説明 |
| --- | --- | --- |
| `SetCoordinateTransform(ICoordTransform?)` | - | ドメイン ↔ ローカル DIP の変換ポートを設定する。null の場合は等価変換（1 ドメインピクセル = 1 DIP・境界なし） |
| `SetSelection(EsRect)` | - | 選択矩形を外部状態から復元する。`Options.ClampToDomainBounds` が有効な場合は境界内へクリップしてから設定する |
| `ResetSelection()` | - | 選択状態と nudge 状態を初期化する（nudge 中だった場合はカーソルを再表示する） |
| `RequestConfirm()` | - | 確定要求を処理する（Enter と同じ条件）。ドラッグ中・未選択では何もしない |
| `RequestCancel()` | - | キャンセル要求を処理する（Escape と同じ条件）。選択を破棄した後にイベントを発火する |
| `NotifyViewportChanged()` | - | ビューポートの変化を通知する（現在の状態の再通知と再描画。通知の組み立てはコントロール化の層の `MarqueeViewportNotifier` が担う） |

## 6. プロパティ（取得のみ）

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `CurrentSnapshot` | `MarqueeSnapshot` | 現在の状態（State / SelectionRect / ActiveTarget / MousePosition / IsMouseDown / NudgeActive / HasSelection。`readonly record struct`） |

## 7. イベント

| イベント | 引数 | 発火条件 |
| --- | --- | --- |
| `SelectionStarted` | `MarqueePointEventArgs` | 新規選択のドラッグ開始（始点の決定） |
| `SelectionChanged` | `SelectionRectEventArgs` | 選択矩形の変更（ドラッグ中・nudge 中・外部復元・境界クリップ） |
| `SelectionCompleted` | `SelectionRectEventArgs` | ドラッグ終了による選択の確定 |
| `SelectionCancelled` | `EventArgs` | 小さすぎる矩形での終了 |
| `LoupePositionMoved` | `MarqueePointEventArgs` | ルーペが表示する中心座標 |
| `LoupePositionMoved` | `MarqueePointEventArgs` | ルーペが表示する中心座標（通常追従はポインタ位置、ドラッグ・nudge 中は動いている側のハンドル位置） |
| `ViewportChanged` | `SelectionRectEventArgs` | `NotifyViewportChanged()` の呼び出し |
| `ZoomRequested` | `WheelZoomRequest` | Ctrl+ホイール（方向と発生位置。解釈は組み込みの層が単一所有する） |
| `ScrollRequested` | `WheelScrollRequest` | 通常ホイール（回転量・Shift・発生位置。解釈は組み込みの層が単一所有する） |
| `ConfirmRequested` | `SelectionRectEventArgs` | 矩形内ダブルクリック・Enter |
| `CancelRequested` | `EventArgs` | 矩形外ダブルクリック・Escape・`RequestCancel()` |
| `Rendered` | `MarqueeRenderedEventArgs` | 描画完了の通知（描画のたびに発火）。`Canvas`（DPI スケーリング済み・描画中のみ有効）と描画内容（`Content` = `MarqueeDrawContent`）を渡す。**サイズ・座標ラベルはコントロールがテキスト要素として表示する**ため、ホストは付加情報（操作・情報ラベル）の描画に使う |

座標系はすべてドメイン座標です。

### 7.1 ラベル（文字）の表示

サイズラベルとカーソル座標ラベルの**文言・外観はコアが単一所有**し、実寸から配置矩形を求める写像は描画層（`MarqueeControl.Rendering` の `MarqueeLabelPlan`）が単一所有します。コントロールは `Border` + `TextBlock` を子要素として置き（`SkiaText.Measure` で実寸を測って渡す）、**ラベルは常に最上位**に表示されます。操作・情報ラベルはアプリ固有の付加情報のため、ホストが `Rendered` のハンドラで `Options.Labels` の外観に従って描きます。
描画のたびに `Rendered` が発火するため、ホストはそのハンドラで追加描画できます。

```csharp
using Estable.MewUI.Marquee;

// 付加情報（操作・情報ラベル）を自前で描く。
// サイズ・座標ラベルはコントロールがテキスト要素として表示する（図形もコントロールが描く）。
marquee.Rendered += (_, e) =>
{
    // e.Content.HoleRect（基準）と Options.Labels（外観）を使って描く。
};
```

- `Rendered` はコントロールの描画中に発火します。ハンドラ内で描画を完了させてください（キャンバスを保持しない）。
- `e.Canvas` は DPI スケーリング適用済みのため、ホストは DIP 座標で描けます。

## 8. 操作仕様

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ（矩形外） | 新規選択を開始する。ハンドルヒット時は元の選択を保持したまま開始位置がハンドル点へスナップする |
| 左ドラッグ（ハンドル上） | 8 方向のリサイズ。ハンドル位置は 1 ドメインピクセル精度でカーソルに追従する |
| 左ドラッグ（矩形内） | 移動（サイズを維持する） |
| ダブルクリック（矩形内） | `ConfirmRequested` を発火して選択をクリアする |
| ダブルクリック（矩形外） | `CancelRequested` を発火する（選択は保持する） |
| 矢印キー | nudge（1 ドメインピクセル単位）。未選択時は始点、選択確定後・ドラッグ中は終点、ハンドル選択中はそのハンドルを動かす |
| Enter | `ConfirmRequested` を発火して選択をクリアする（ドラッグ中・未選択では何もしない） |
| Escape | `CancelRequested` を発火する（選択があれば破棄する） |
| Ctrl + ホイール | `ZoomRequested` を発火する（方向と発生位置。ズームの実処理はホスト） |
| ホイール（Ctrl なし） | `ScrollRequested` を発火する（回転量・Shift・発生位置。スクロール先とスクロール量の解釈はホスト） |
| （解釈の所有者） | Ctrl の判定とログは組み込みの層（`MarqueeControl.CoreForControl` の `MarqueeWheelInterpreter`）が単一所有し、本コントロールは要求を公開イベントへ転送するだけ |

### 制限

- `Options.ClampToDomainBounds` が有効な場合、選択は `ICoordTransform.Bounds` で示される境界内に制限されます。
  新規選択・ハンドル変形・移動・nudge のすべてに適用されます。
- 選択矩形の幅・高さが `Options.MinSelectionSize` 未満の状態でドラッグを終了するとキャンセル扱いになります
  （`SelectionCancelled` を発火して選択を破棄します）。
- 矢印キーの nudge は本コントロールにフォーカスがある場合のみ動作します（マウス押下時に自動でフォーカスを取得します）。
  nudge 中は OS カーソルを非表示にし、終了時に nudge 位置へ戻して再表示します。フォーカスを失った場合も nudge を終了します。

## 9. Avalonia 版との違い

| 項目 | 差異 |
| --- | --- |
| イベント引数の座標 | コア型（`EsPoint` / `EsRect`）で公開する（Avalonia 版は Avalonia の `Point` / `Rect`） |
| マウスダウン・アップのイベント | 持たない（基底 `UIElement` の同名メンバーが同じ情報を提供するため、本コントロールでは公開しない） |
| ビューポート変化 | ホストが `NotifyViewportChanged()` を呼ぶ（Avalonia 版は `AttachScrollViewer` で `ScrollViewer` を購読） |
| nudge のキー入力 | フォーカス依存（Avalonia 版は `TopLevel` を購読してフォーカス非依存） |
| ログ | `Log` プロパティで `ILogger` を注入して出力する（書式・レベルは Avalonia 版と同一） |
| 文字（サイズラベル・座標ラベル）の表示 | **コントロールがテキスト要素（`Border` + `TextBlock`）として配置する**（文言・外観はコア、実寸測定は `SkiaText.Measure`、配置は描画層 `MarqueeControl.Rendering` の `MarqueeLabelPlan`） |

## 9.5 ログ

`Log` プロパティへ `ILogger` を設定すると操作ログを出力します（未設定・null の場合は `NullLogger` になり出力しません）。

| レベル | 出力内容 |
| --- | --- |
| Debug | `MouseDown`（位置・クリック数・選択有無）/ `MouseUp`（位置・ドラッグ有無・選択有無）/ `DoubleClick`（矩形内か）/ `DragStart`（ハンドル・位置）/ `KeyDown`（キー・処理有無）/ `ConfirmRequested`（矩形）/ `CancelRequested` / `ZoomRequested` / `Setting`（`Options.ClampToDomainBounds` の変更前後） |
| Trace | `MouseDown 直前の位置` / `MouseUp 直後の位置` |

マウス移動の高頻度ログは出力しません（押下直前・解放直後の位置のみ記録します）。

## 10. 使用例

```csharp
var marquee = new MarqueeControl
{
    Options = MarqueeOptions.Default with
    {
        // 選択枠線は外側の装飾（暗転マスクの穴あけ枠）が描く
        OuterAppearance = MarqueeOptions.Default.OuterAppearance with
        {
            BorderColor = System.Drawing.Color.FromArgb(255, 0, 120, 215),
        },
        HandleHitRadius = 12.0,
        ShowCoordLabel = true,
    },
};

// ドメイン境界（画像座標）を設定する
marquee.SetCoordinateTransform(new CoordTransform(dip => dip, domain => domain, () => new EsSize(width, height)));

// 確定した矩形を取得する
marquee.SelectionCompleted += (_, e) => OnCaptured(e.Rect);
```

## 11. エラー・例外

| 条件 | 動作 |
| --- | --- |
| カーソルサービスが null | Windows 既定実装（Win32）を使用する |
| `SetCoordinateTransform(null)` | 等価変換（境界なし）を適用する |
| `Options.CoordLabelFontSize` に NaN / ∞ | 既定値（12）へ補正する（例外は発生しない） |
| `SetSelection` に境界外の矩形 | `Options.ClampToDomainBounds` が有効なら境界内へクリップする（例外は発生しない） |
