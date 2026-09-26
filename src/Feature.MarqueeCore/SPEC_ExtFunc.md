# Feature.MarqueeCore 外部機能仕様書

`MarqueeControl`（ドラッグで矩形を選び、移動・リサイズできるコントロール）の **UI 非依存の状態層**が提供する外部機能と公開 API の仕様です。利用者（UI 実装・ホスト）から見える情報のみを記載し、内部実装の詳細は記載しません。設計の意図は [README](README.md)、構造と依存は [SPEC_System](SPEC_System.md) を参照してください。

## 1. 提供機能

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| 選択操作の状態機械 | `Marquee` | 押下・移動・解放・キー入力を通知すると、状態遷移の判断（nudge の開始/終了・ダブルクリックの確定/キャンセル・ハンドルのスナップ・クリック検出・最小サイズによるキャンセル・エコー移動の抑制）を行い、効果と通知を返す |
| 描画内容の解決 | `Marquee.ResolveContent` | 描画領域のサイズを渡すと、1 フレーム分の描画内容（装飾・ハンドル・始点マーカー・サイズラベル・カーソル座標ラベルと描画順）を返す。「描くかどうか」の判断はすべてコアが下す |
| 描画の実行（差し替え可能） | `IMarqueeRenderer` / `Marquee.Renderer` | 描画内容を描画面へ写す実装を注入する（既定実装は描画層 `MarqueeControl.UIBase` の `new MarqueeRenderer(MarqueePaint.Default)`。描画手順は `MarqueeRendererBase` が、色は `MarqueePaint` が単一所有する）。実装を差し替えれば描画手順を、描画ポート（`IEsCanvas`）の実装を差し替えれば描画先を変えられる |
| 効果の適用 | `IMarqueeEffects` | フォーカス・再描画・カーソル操作・カーソル形状・ポインターキャプチャ・画面座標変換の要求を UI 実装が受ける（不要なメンバーの空実装は `MarqueeEffectsBase` を継承すれば省略できる） |
| 通知 | `Marquee.Notified` | 選択の開始/変更/確定/キャンセル・追従位置の移動・確定/キャンセル要求を `MarqueeNotice` で通知する |
| 外部からの状態操作 | `Marquee` | 選択の復元・対話状態の初期化・確定/キャンセルの要求を API から実行できる |
| 座標変換 | `Marquee` / `ICoordTransform` | ドメイン座標（利用者が定める座標系）とローカル DIP の相互変換と、ドメイン境界の指定 |
| ラベルの体裁計算 | 描画層 `MarqueeControl.UIBase` の `MarqueePaint` / `MarqueeLabelPlan` | 内側余白の適用・アラインの適用・カーソル基準の配置と領域内へのクランプ・行送り（描画にだけ必要な規則のためコアは持たない） |
| 装飾（暗転マスク等） | `MarqueeDecoOptions` / `OuterDecoFrame` / `InnerDecoFrame` | 選択矩形の外側/内側へ描く装飾の設定と描画内容 |
| 幾何計算 | `SelectionGeometry` | 正規化・ハンドル位置・近傍判定・対角・境界クリップ・ドラッグ変形・描画対象ハンドル一覧・カーソル種別 |

### 前提条件

- 本コアは UI フレームワークに依存しません。UI 固有の型は利用者（UI 実装）が境界で変換します。
- 座標は `EsPoint` / `EsRect` / `EsSize` / `EsKey`（`EsUtil.Platform.Core`）で扱います。
- カーソル操作は `ICursorService` 抽象にのみ依存します（`null` の場合は何もしません）。
- ラベルの**文言・基準点（アラインを含む）**はコアが単一所有しますが、**描画と実寸測定**は利用者が行います（実寸は描画 API でしか測れないため）。体裁（背景矩形・内側余白・フォントサイズ・色）は描画層（`MarqueeControl.UIBase` の `MarqueePaint` / `MarqueeLabelPlan`）が単一所有します。

## 2. 公開 API 一覧

可視性は public のみを記載します（`internal` は [SPEC_System](SPEC_System.md) の可視性ポリシーを参照）。

### `EsUtil.Feature.MarqueeCore`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `Marquee` | class | 操作の唯一の窓口となるファサード |

### `EsUtil.Feature.MarqueeCore.Contract`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `IMarqueeRenderer` | interface | 描画内容を描画面へ写す実装の契約（**描画の差し替え点**。既定実装は描画層の `MarqueeRenderer`） |
| `IMarqueeEffects` | interface | コアが要求する効果の適用先（UI 実装が実装する） |
| `MarqueeEffectsBase` | abstract class | `IMarqueeEffects` の全メンバーを virtual の空実装で提供する基底クラス（必要な効果だけ override する） |

### `EsUtil.Feature.MarqueeCore.Drawing`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeDrawContent` | record struct | 1 フレーム分の描画内容（装飾・穴あけ矩形・ハンドル・マーカー・テキストの配置・層の順序） |
| `MarqueeHandles` / `MarqueeMarker` | record struct | ハンドル・始点マーカーの描画契約 |
| `MarqueeTextPlacement` | record struct | コアが UI 層へ渡すテキスト（文言・基準点・アライン・描画領域）。**要素として置くか canvas へ描くかは UI 層が決める**（フォントサイズ・色は描画層の `MarqueePaint` が持つ） |
| `MarqueeDrawLayer` | enum | 描画層（`Decoration` / `Handles` / `Marker` / `Label`）。**`Label` はテキストがある場合だけ末尾に置く**（UI 要素として置く UI は無視してよい） |

### `EsUtil.Feature.MarqueeCore.Interaction`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeSnapshot` | readonly record struct | 通知時点の状態（状態・選択矩形・アクティブハンドル・マウス位置・操作フラグ）。値型のため通知のたびにヒープを確保しない |
| `MarqueeNotice` / `MarqueeNoticeKind` | record / enum | 通知（種別・通知座標・スナップショット） |
| `HitTarget` | enum | ヒット対象（8 方向のハンドル + `Move` + `None`） |
| `MarqueeCursorKind` | enum | カーソル形状の種別（`Default` / `DiagonalNWSE` / `DiagonalNESW` / `Vertical` / `Horizontal` / `Move`） |
| `MarqueeInteractionState` | enum | 対話状態（`NoSelection` / `StartPointNudging` / `Dragging` / `DraggingNudging` / `Selected` / `SelectedNudging`） |

### `EsUtil.Feature.MarqueeCore.Setting`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeOptions` | record | 設定（寸法・表示可否・対話パラメータ）と既定値・補正。**色は持たない**（色は描画層の `MarqueePaint` が単一所有する） |

### `EsUtil.Feature.MarqueeCore.Selection`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `SelectionGeometry` | static class | 純粋な幾何計算とハンドル関連の変換 |

### `EsUtil.Feature.MarqueeCore.Decoration`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeDecoOptions` | record | 装飾の設定（寸法・描画可否フラグ・全面暗転・ハイライト・内側の寸法） |
| `OuterDecoAppearance` / `InnerDecoAppearance` | record | 外側/内側の装飾の寸法（枠線の太さのみ。**色は持たない**） |
| `OuterDecoFrame` / `InnerDecoFrame` | record struct | 外側/内側の装飾の描画内容（暗転領域・枠線・塗り。**色は持たない**） |

装飾の色（暗転・穴あけ枠・内側の塗りと枠線）は本コアに含みません（描画にだけ必要な設定であるため、描画層 `MarqueeControl.UIBase` の `MarqueePaint` が単一所有します）。描くかどうかも色のアルファ（透明度）ではなく、本コアの明示フラグが決めます。

### `MarqueeDecoOptions` のメンバー

| 分類 | メンバー | 既定値 |
| --- | --- | --- |
| 寸法 | `OuterAppearance.BorderThickness`（`OuterDecoAppearance`） | 2.0（穴あけ枠の太さ） |
| 描画可否 | `ShowOverlay` | `true`（暗転マスクを描く。**アルファ 0 を「描かない」の代用にしない**） |
| 描画可否 | `ShowOuterBorder` | `true`（穴あけ枠を描く） |
| 表示ポリシー | `OuterShowFullOverlay` | `false`（穴あけ矩形が無いときは暗転しない。true で全面を暗転する） |
| ハイライト | `OuterHighlightRect` | null（null が「ハイライトしない」。有効フラグは持たない） |
| 寸法 | `InnerAppearance.BorderThickness`（`InnerDecoAppearance`） | 2.0（内側の枠線の太さ） |
| 描画可否 | `ShowInnerFill` | `false`（穴あけ矩形の内側を塗らない） |
| 描画可否 | `ShowInnerBorder` | `false`（穴あけ矩形の内側の枠線を描かない） |

### `Marquee`（ファサード）のメンバー

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Marquee(IMarqueeRenderer, IMarqueeEffects, ICursorService?, MarqueeOptions?)` | ctor | `renderer` と `effects` は null 不可。`cursor` は null 可（カーソル操作を行わない）。描画実装は必須（本コアは描画層を参照できないため、コンストラクタで注入する） |
| `Notified` | event | 通知（`MarqueeNotice`） |
| `Log` | property | ロガー。null は `NullLogger` へ吸収される |
| `Options` | property | 設定。設定すると対話パラメータを反映し、境界外選択の禁止の変化時のみログと即時クリップを行う |
| `Scaling` | property | 座標変換ポート未指定時に適用する倍率。既定は `DefaultScaling`（1.0） |
| `DecoOptions` | property | 装飾の設定 |
| `SizeLabelOverrideProvider` | property | サイズラベルの文言プロバイダ（null または空文字はコアが自動生成） |
| `DisplayScaleScaling` | property | サイズラベルの DisplayScale 表示に使う係数（0 以下は `Scaling` と同値） |
| `HasSelection` / `SelectionRect` | property | 選択の有無と選択矩形（ドメイン座標） |
| `CreateSnapshot(EsPoint?)` | method | 状態スナップショットの生成 |
| `SetCoordinateTransform(ICoordTransform?)` | method | 座標変換ポートの設定（null は等価変換） |
| `ToDomain(EsPoint)` / `ToDip(EsPoint)` | method | 座標変換 |
| `Press(EsPoint, int)` / `Move(EsPoint)` / `Release(EsPoint)` | method | ポインター入力の通知（ドメイン座標） |
| `KeyDown(EsKey, EsPoint?)` | method | キー入力の通知。処理済みの真偽を返す（`Escape` / `Enter`（`Return` 含む）/ 矢印キー） |
| `EndNudge()` | method | nudge の終了（フォーカス喪失で使用） |
| `SetSelection(EsRect)` / `ResetSelection()` | method | 選択の復元・対話状態の初期化 |
| `RequestConfirm()` / `RequestCancel()` | method | 確定/キャンセルの要求（キー・ダブルクリックと同一経路） |
| `ResolveContent(EsSize)` | method | 1 フレーム分の描画内容の解決 |
| `Renderer` | property | 描画の実装（`IMarqueeRenderer`）の取得・設定（**描画の差し替え**。null 不可。設定すると再描画を要求する） |
| `SizeLabelFontSize` / `CoordLabelFontSize`（描画実装＝`MarqueeRenderer` のプロパティ） | property | テキストのフォントサイズ（描画の設定 `MarqueePaint` の値への入口）。**描画の契約と本コアの設定には含めない** |
| `DefaultScaling` | const | 倍率の既定値（1.0） |

### `MarqueeOptions` のメンバー

| 分類 | メンバー | 既定値 |
| --- | --- | --- |
| 表示可否 | `ShowHandles` | `true` |
| 寸法 | `HandleSize` | 10.0 |
| 寸法 | `MarkerLength` | 16.0（描画時の半幅は 1/2） |
| 表示可否 | `ShowCoordLabel` | `false` |
| 表示可否 | `ShowSizeLabel` | `true` |
| 対話 | `HandleHitRadius` | 12.0（DIP） |
| 対話 | `MinSelectionSize` | 6.0（ドメイン座標。これ未満はキャンセル扱い） |
| 対話 | `ClampToDomainBounds` | `true` |
| 体裁 | `HandleBorderThickness` | 1.5（`MarqueeHandles.BorderThickness` として描画層へ渡る） |
| 体裁 | `CoordLabelOffset` | 8.0（カーソルからラベルを離す距離） |

`MarqueeOptions` は**色もラベルのフォントサイズも持ちません**。これらは描画にだけ必要な設定であるため、描画層 `MarqueeControl.UIBase` の `MarqueePaint` が単一所有します（部分変更の基点は `MarqueePaint.Default`。ハンドル枠と穴あけ枠の既定は共通の `MarqueePaint.DefaultHandleBorderColor` = `#FFA9A9A9`、フォントサイズは `SizeLabelFontSizeDefault` = 14.0 / `CoordLabelFontSizeDefault` = 12.0、内側余白は `LabelPadding`（既定 4.0））。ラベルの文字色・背景色の既定値（白 / 黒 60%）も `MarqueePaint` が単一所有するため、サイズラベル・カーソル座標ラベル・ルーペが同じ見た目になります。

### `IMarqueeEffects` のメンバー

| メンバー | 説明 |
| --- | --- |
| `RequestFocus()` | コントロールへフォーカスを要求する |
| `RequestRedraw()` | 再描画を要求する |
| `SetCursorShape(MarqueeCursorKind)` | カーソル形状を設定する（UI が自 UI のカーソル型へ写像する） |
| `SetPointerCapture(bool)` | ポインターキャプチャを取得/解放する |
| `ToScreenPoint(EsPoint)` | ローカル DIP を画面座標へ変換する（nudge のカーソル移動にのみ使う） |

`MarqueeEffectsBase` はこれらの全メンバーを virtual の空実装として提供する `abstract class` です。独自の UI アダプタは本クラスを継承し、必要な効果（例: `RequestRedraw`）だけを override します（不要なメンバーの空実装を書かなくて済む）。

### `IMarqueeRenderer` のメンバー

| メンバー | 説明 |
| --- | --- |
| `DrawShapes(IEsCanvas, in MarqueeDrawContent)` | 図形の層（装飾・ハンドル・始点マーカー）を描画順に描く（**テキストは描かない**）。テキストを canvas へ描く実装は基底の `DrawLabel`（既定は何もしない）を上書きする |

描画順はコアが単一所有し（`MarqueeDrawContent.Layers`）、実装は順序を判断しません。テキストを描く層（`MarqueeDrawLayer.Label`）は基底がフックとして用意し、**既定は何もしません**（UI 要素として置く UI は上書きしません）。本コアは描画層（`MarqueeControl.UIBase`）を参照できないため、既定実装（`new MarqueeRenderer(MarqueePaint.Default)`。描画手順は `MarqueeRendererBase` が、色は `MarqueePaint` が単一所有する）は利用者が渡します。

### `MarqueeNoticeKind` の値

| 値 | 発生条件 |
| --- | --- |
| `SelectionStarted` | 新規選択のドラッグを開始した |
| `SelectionChanged` | 選択矩形が変化した |
| `SelectionCompleted` | 選択が確定した（確定要求と同じ契機） |
| `SelectionCancelled` | 選択が破棄された |
| `MarkerPositionMoved` | 十字マーカーが追従する位置が変化した（汎用名。ルーペ中心への写像は UI 層が行う） |
| `ConfirmRequested` | 確定要求を受理した |
| `CancelRequested` | キャンセル要求を受理した |

## 3. 契約（利用者が守る・受け取る値）

### 描画内容（`MarqueeDrawContent`）

| 項目 | 内容 |
| --- | --- |
| `Decoration` / `InnerDecoration` | 外側/内側の装飾の描画内容（描くものが無い場合は各項目が空） |
| `HoleRect` | 穴あけ矩形（DIP。選択もハイライトも無い場合は null） |
| `Handles` / `Marker` | ハンドル・始点マーカー（null は描かない） |
| `SizeLabelText` / `CoordLabelText` | テキスト（`MarqueeTextPlacement`。null は表示しない）。文言・基準点・アライン・描画領域を持つ |
| `Layers` | 描く層とその順序。先頭から順に処理する。テキストの層（`Label`）はテキストがある場合のみ末尾（常に最上位）。**UI 要素として置く UI は本層を無視してよい**（基底のフックの既定は何もしない） |

### テキストの位置（`MarqueeTextPlacement`）

- コアが決めるのは**文言・基準点・アライン（水平/垂直）・描画領域**です（`EsHorizontalAlign` / `EsVerticalAlign` で基準点がテキストのどこを指すかを指定するため、コアは幅・高さを知りません）。
- サイズラベルは基準点を穴あけ矩形の左上にし、アラインは左端・上端です。行送り（行の高さ + 内側余白 × 2）で次の行の上端を求めるため、**背景の下端と次の行の上端が一致します**。
- カーソル座標ラベルは基準点を「カーソル + `CoordLabelOffset`」にし、描画領域を渡します（描画領域の右端・下端をはみ出す場合は領域内へクランプされ、軸が 0 以下の場合はクランプされません）。
- **フォントサイズ・色・内側余白は描画の設定（`MarqueePaint`）**、実寸から背景矩形を求める計算とアラインの適用は描画層の `MarqueeLabelPlan` が単一所有します。**要素として置くか canvas へ描くかは UI 層が決めます**。

### 操作の受理条件

| 操作 | 受理条件 |
| --- | --- |
| `Press` のダブルクリック | 選択矩形の内側なら確定、外側ならキャンセル |
| `RequestConfirm` | ドラッグ中は無視。選択があるときのみ確定し、確定後は選択を破棄する |
| `RequestCancel` | ドラッグ中または選択がある場合は選択を破棄してから通知する |
| `KeyDown` の矢印キー | 選択済みかつマウス非押下の場合は nudge しない |
| `Release` | 最小サイズ未満の矩形はキャンセル扱い |

## 4. 座標系

| 座標系 | 用途 |
| --- | --- |
| ドメイン座標 | 選択矩形・入力座標・通知座標。利用者が座標変換ポートで定義する |
| ローカル DIP | 描画内容の座標。`ICoordTransform.Inverse` で得る |

- 選択矩形は**閉区間**（始点・終点とも含む）として扱い、DIP への変換時に終点へ +1 します。
- ドメイン境界（`ICoordTransform.Bounds`）は `ClampToDomainBounds` が有効な場合のクリップ範囲です。

## 5. 制限事項

- ホイール操作（Ctrl+ズーム / スクロール）は本コアに含みません（`MarqueeControl.UIBase` が単一所有します）。
- ビューポートの変化の通知は本コアに含みません（`MarqueeControl.UIBase` が単一所有します）。
- 描画 API は提供しません（描画の手順は UI 非依存の描画層 `MarqueeControl.UIBase` が単一所有し、描画先は `Platform.Core` の `IEsCanvas` です。描画層は本コアの描画内容（`MarqueeDrawContent`）と幾何（`SelectionGeometry`）を参照します）。
- 色（ハンドル・始点マーカー・ラベル・装飾）は本コアに含みません（描画にだけ必要な設定であるため、描画層 `MarqueeControl.UIBase` の `MarqueePaint` が単一所有します）。
- 操作ラベル・情報ラベル（アプリ固有の付加情報）は本コアが関知しません。ホストが文言の生成と描画を行います（体裁の適用は `MarqueeControl.UIBase` の `MarqueeHostLabelRenderer` / `FadeMaskHostLabelRenderer` を共用できます）。

## 6. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
