# Feature.MarqueeCore 外部機能仕様書

`MarqueeControl`（ドラッグで矩形を選び、移動・リサイズできるコントロール）の **UI 非依存の状態層**が提供する外部機能と公開 API の仕様です。利用者（UI 実装・ホスト）から見える情報のみを記載し、内部実装の詳細は記載しません。設計の意図は [README](README.md)、構造と依存は [SPEC_System](SPEC_System.md) を参照してください。

## 1. 提供機能

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| 選択操作の状態機械 | `Marquee` | 押下・移動・解放・キー入力を通知すると、状態遷移の判断（nudge の開始/終了・ダブルクリックの確定/キャンセル・ハンドルのスナップ・クリック検出・最小サイズによるキャンセル・エコー移動の抑制）を行い、効果と通知を返す |
| 描画内容の解決 | `Marquee.ResolveContent` | 描画領域のサイズを渡すと、1 フレーム分の描画内容（装飾・ハンドル・始点マーカー・サイズラベル・カーソル座標ラベルと描画順）を返す。「描くかどうか」の判断はすべてコアが下す |
| 描画（本コアの対象外） | 描画層 `MarqueeControl.UIBase` の `IMarqueeRenderer` / `MarqueeRendererBase` | 描画内容を描画面へ写す手順は UI 側の共有層が単一所有する（本コアは描画 API も描画ポートも持たない）。色は `MarqueePaint`、描画先は `IEsCanvas` が担う |
| 効果の適用 | `IMarqueeEffects` | フォーカス・再描画・カーソル操作・カーソル形状・ポインターキャプチャ・画面座標変換の要求を UI 実装が受ける（不要なメンバーの空実装は `MarqueeEffectsBase` を継承すれば省略できる） |
| 通知 | `Marquee.Notified` | 選択の開始/変更/確定/キャンセル・追従位置の移動・確定/キャンセル要求を `MarqueeNotice` で通知する |
| 外部からの状態操作 | `Marquee` | 選択の復元・対話状態の初期化・確定/キャンセルの要求を API から実行できる |
| 座標変換 | `Marquee` / `ICoordTransform` | ドメイン座標（利用者が定める座標系）とローカル DIP の相互変換と、ドメイン境界の指定 |
| ラベルの体裁計算 | 描画層 `MarqueeControl.UIBase` の `MarqueePaint` / `MarqueeLabelPlan` | 内側余白の適用・アラインの適用・カーソル基準の配置と領域内へのクランプ・行送り（描画にだけ必要な規則のためコアは持たない） |
| 装飾（暗転マスク等） | `MarqueeDecoOptions` / `MarqueeOuterDecoFrame` / `MarqueeInnerDecoFrame` | 選択矩形の外側/内側へ描く装飾の設定と描画内容 |

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
| `IMarqueeEffects` | interface | コアが要求する効果の適用先（UI 実装が実装する） |
| `MarqueeEffectsBase` | abstract class | `IMarqueeEffects` の全メンバーを virtual の空実装で提供する基底クラス（必要な効果だけ override する） |

### `EsUtil.Feature.MarqueeCore.Drawing`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeDrawContent` | record struct | 1 フレーム分の描画内容（装飾・穴あけ矩形・ハンドル・マーカー・テキストの配置・層の順序） |
| `MarqueeRenderRequest` | record struct | 1 フレーム分の描画入力（設定・状態と、描画時に決まる値）。`Marquee.CreateRequest` が生成し、`Marquee.ResolveContent` へ渡す。値の組は `MarqueeRenderRequest.Create` が 1 箇所に閉じる。**`Create` を public にする理由**: ファサード（`Marquee.CreateRequest`）と独自 UI が描画入力を組み立てる唯一の入口である（描画先の領域を知るのは呼び出し側だけである） |
| `MarqueeHandles` / `MarqueeMarker` | record struct | ハンドル・始点マーカーの描画契約 |
| `MarqueeTextPlacement` | record struct | コアが UI 層へ渡すテキスト（文言・基準点・アライン・描画領域）。**要素として置くか canvas へ描くかは UI 層が決める**（フォントサイズ・色は描画層の `MarqueePaint` が持つ） |
| `MarqueeDrawLayer` | enum | 描画層（`Decoration` / `Handles` / `Marker` / `Label`）。**`Label` はテキストがある場合だけ末尾に置く**（UI 要素として置く UI は無視してよい） |

### `EsUtil.Feature.MarqueeCore.Interaction`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeSnapshot` | readonly record struct | 通知時点の状態（状態・選択矩形・アクティブハンドル・マウス位置・操作フラグ）。値型のため通知のたびにヒープを確保しない |
| `MarqueeNotice` / `MarqueeNoticeKind` | record / enum | 通知（種別・通知座標・スナップショット） |
| `MarqueeHitTarget` | enum | ヒット対象（8 方向のハンドル + `Move` + `None`） |
| `MarqueeCursorKind` | enum | カーソル形状の種別（`Default` / `DiagonalNWSE` / `DiagonalNESW` / `Vertical` / `Horizontal` / `Move`） |
| `MarqueeInteractionState` | enum | 対話状態（`NoSelection` / `StartPointNudging` / `Dragging` / `DraggingNudging` / `Selected` / `SelectedNudging`） |

### `EsUtil.Feature.MarqueeCore.Setting`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeOptions` | record | 設定（寸法・表示可否・対話パラメータ）と既定値・補正。**色は持たない**（色は描画層の `MarqueePaint` が単一所有する） |

### `EsUtil.Feature.MarqueeCore.Decoration`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeDecoOptions` | record | 装飾の設定（寸法・描画可否フラグ・全面暗転・ハイライト・内側の寸法） |
| `MarqueeOuterDecoAppearance` / `MarqueeInnerDecoAppearance` | record | 外側/内側の装飾の寸法（枠線の太さのみ。**色は持たない**） |
| `MarqueeOuterDecoFrame` / `MarqueeInnerDecoFrame` | record struct | 外側/内側の装飾の描画内容（暗転領域・枠線・塗り。**色は持たない**） |

装飾の色（暗転・穴あけ枠・内側の塗りと枠線）は本コアに含みません（描画にだけ必要な設定であるため、描画層 `MarqueeControl.UIBase` の `MarqueePaint` が単一所有します）。描くかどうかも色のアルファ（透明度）ではなく、本コアの明示フラグが決めます。

### `MarqueeDecoOptions` のメンバー

| 分類 | メンバー | 既定値 |
| --- | --- | --- |
| 寸法 | `OuterAppearance.BorderThickness`（`MarqueeOuterDecoAppearance`） | 2.0（穴あけ枠の太さ。0.5 未満は 0.5 へ補正される） |
| 描画可否 | `ShowOverlay` | `true`（暗転マスクを描く。**アルファ 0 を「描かない」の代用にしない**） |
| 描画可否 | `ShowOuterBorder` | `true`（穴あけ枠を描く） |
| 表示ポリシー | `OuterShowFullOverlay` | `false`（穴あけ矩形が無いときは暗転しない。true で全面を暗転する） |
| ハイライト | `OuterHighlightRect` | null（null が「ハイライトしない」。有効フラグは持たない） |
| 寸法 | `InnerAppearance.BorderThickness`（`MarqueeInnerDecoAppearance`） | 2.0（内側の枠線の太さ。0.5 未満は 0.5 へ補正される） |
| 描画可否 | `ShowInnerFill` | `false`（穴あけ矩形の内側を塗らない） |
| 描画可否 | `ShowInnerBorder` | `false`（穴あけ矩形の内側の枠線を描かない） |

装飾の描画可否の判断は**述語**として提供します（コアのフレーム解決は述語の結果だけを使います。アルファ 0 を「描かない」の代用にしません）。

| 述語 | 条件 |
| --- | --- |
| `ShouldDrawOverlay(bool hasHoleRect)` | 穴あけ矩形がある、または全面暗転（`OuterShowFullOverlay`）が有効（どちらも無い場合は暗転しない） |

### `Marquee`（ファサード）のメンバー

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Marquee(IMarqueeEffects, ICursorService?, MarqueeOptions?)` | ctor | `effects` は null 不可。`cursor` は null 可（カーソル操作を行わない）。描画実装は受け取らない（描画は UI の関心事であり、本コアは描画 API を持たない） |
| `Notified` | event | 通知（`MarqueeNotice`） |
| `Log` | property | ロガー。null は `NullLogger` へ吸収される |
| `Options` | property | 設定。設定すると対話パラメータを反映し、**変化した項目をログ出力**し、境界外選択の禁止の変化時のみ即時クリップを行う |
| `DomainToDipScaling` | property | 座標変換ポート未指定時に適用する倍率。既定は `Limits.DomainToDipScalingDefault`（1.0）。設定時は 0.1〜8.0 へ補正される |
| `DecoOptions` | property | 装飾の設定 |
| `SizeLabelOverrideProvider` | property | サイズラベルの文言プロバイダ（null または空文字はコアが自動生成） |
| `DisplayScaleScaling` | property | サイズラベルの DisplayScale 表示に使う係数（0 以下は `DomainToDipScaling` と同値） |
| `HasSelection` / `SelectionRect` | property | 選択の有無と選択矩形（ドメイン座標） |
| `CreateSnapshot(EsPoint?)` | method | 状態スナップショットの生成 |
| `SetCoordinateTransform(ICoordTransform?)` | method | 座標変換ポートの設定（null は等価変換） |
| `ToDomain(EsPoint)` / `ToDip(EsPoint)` | method | 座標変換 |
| `Press(EsPoint, int)` / `Move(EsPoint)` / `Release(EsPoint)` | method | ポインター入力の通知（ドメイン座標） |
| `KeyDown(EsKey, EsPoint?)` | method | キー入力の通知。処理済みの真偽を返す（`Escape` / `Enter`（`Return` 含む）/ 矢印キー） |
| `EndNudge()` | method | nudge の終了（フォーカス喪失で使用） |
| `SetSelection(EsRect)` / `ResetSelection()` | method | 選択の復元・対話状態の初期化 |
| `RequestConfirm()` / `RequestCancel()` | method | 確定/キャンセルの要求（キー・ダブルクリックと同一経路） |
| `CreateRequest(EsSize)` | method | 描画 1 フレーム分の入力（`MarqueeRenderRequest`）の生成（設定・状態と、描画時に決まる値） |
| `ResolveContent(MarqueeRenderRequest)` | method | 1 フレーム分の描画内容（`MarqueeDrawContent`）の解決 |

**設定では変更しない固定値（ハンドル枠の太さ 1.5・カーソル座標ラベルのオフセット 8.0・始点マーカーの太さ 2.0）は公開しません**。参照するのはコアの描画内容の解決だけであり、UI 層は解決済みの値（`MarqueeHandles.BorderThickness`・`MarqueeTextPlacement.Point`）を受け取って使うためです（公開面へ出す必然性がない）。

### `MarqueeOptions` のメンバー

| 分類 | メンバー | 既定値 | 安全域（生成時に補正） |
| --- | --- | --- | --- |
| 寸法 | `HandleSize` | 10.0 | 1.0〜256.0 |
| 寸法 | `MarkerLength` | 16.0（描画時の半幅は 1/2） | 2.0〜4096.0 |
| 表示可否 | `ShowHandles` | `true` | — |
| 表示可否 | `ShowCoordLabel` | `true` | — |
| 表示可否 | `ShowSizeLabel` | `true` | — |
| 対話 | `HandleHitRadius` | 12.0（DIP） | 0.0〜256.0 |
| 対話 | `MinSelectionSize` | 6.0（ドメイン座標。これ未満はキャンセル扱い） | 1.0〜4096.0 |
| 対話 | `ClampToDomainBounds` | `true` | — |

数値項目の補正は**本型の生成時に適用**します（`init` アクセサ。安全域は `Marquee.Limits` が単一所有する）。設定の経路（オブジェクト初期化子・`with` 式・逆シリアル化）によらず範囲内の値だけが保持されます。

表示可否の判断は**述語**として提供します（コアのフレーム解決は述語の結果だけを使います）。

| 述語 | 条件 |
| --- | --- |
| `ShouldShowSizeLabel(bool hasHoleRect)` | 表示の設定が有効かつ穴あけ矩形がある（基準点が矩形の左上であり、文言も矩形の寸法から作るため） |
| `ShouldShowCoordLabel()` | 表示の設定が有効（基準点はカーソル位置であり、位置は描画入力が常に持つ） |

ハンドル枠の太さ（1.5）・カーソル座標ラベルのオフセット（8.0）・始点マーカーの太さ（2.0）は**設定では変更しない固定値**であり、**描画内容の解決（`Marquee.DrawContent.cs`）の private 定数**が単一所有します（いずれもコア内だけで使う値であり、UI 層は解決済みの値を受け取るため公開しません）。

`MarqueeOptions` は**色もラベルのフォントサイズも持ちません**。これらは描画にだけ必要な設定であるため、描画層 `MarqueeControl.UIBase` の `MarqueePaint` が単一所有します（部分変更の基点は `MarqueePaint.Default`。ハンドル枠と穴あけ枠の既定は共通の `MarqueePaint.DefaultHandleBorderColor` = `#FFA9A9A9`、フォントサイズは `SizeLabelFontSizeDefault` = 14.0 / `CoordLabelFontSizeDefault` = 12.0、内側余白は `LabelPadding`（既定 4.0））。ラベルの文字色・背景色の既定値（白 / 黒 60%）も `MarqueePaint` が単一所有するため、サイズラベル・カーソル座標ラベル・ルーペが同じ見た目になります。

### `Marquee.Limits` のメンバー

選択矩形の数値パラメータの既定値と安全域を単一所有し、範囲外の値を補正します（非有限は既定値）。**数値スキーマに加えて「未設定（0 以下）の解決規則」も持ちます**（`ResolveDisplayScale`）。

| 定数 | 値 | 説明 |
| --- | --- | --- |
| `HandleSizeMin` / `HandleSizeMax` / `HandleSizeDefault` | `1.0` / `256.0` / `10.0` | ドラッグハンドルの一辺サイズ（DIP） |
| `MarkerLengthMin` / `MarkerLengthMax` / `MarkerLengthDefault` | `2.0` / `4096.0` / `16.0` | 十字マーカーの全長（DIP） |
| `HandleHitRadiusMin` / `HandleHitRadiusMax` / `HandleHitRadiusDefault` | `0.0` / `256.0` / `12.0` | ハンドルのヒットテスト半径（DIP） |
| `MinSelectionSizeMin` / `MinSelectionSizeMax` / `MinSelectionSizeDefault` | `1.0` / `4096.0` / `6.0` | 選択矩形の最小サイズ（ドメイン座標） |
| `BorderThicknessMin` / `BorderThicknessMax` / `BorderThicknessDefault` | `0.5` / `64.0` / `2.0` | 枠線の太さ（DIP。装飾の外観の既定値も本値を参照する） |
| `DomainToDipScalingMin` / `DomainToDipScalingMax` / `DomainToDipScalingDefault` | `0.1` / `8.0` / `1.0` | ポート未指定時に適用する倍率（環境値。他の数値より後に置く） |

| メソッド | 説明 |
| --- | --- |
| `CoerceHandleSize(double)` / `CoerceMarkerLength(double)` / `CoerceHandleHitRadius(double)` / `CoerceMinSelectionSize(double)` | 各寸法を安全域へ補正（非有限は既定値） |
| `CoerceBorderThickness(double)` | 枠線の太さを 0.5〜64.0 へ補正（0 以下の指定を無効化する） |
| `CoerceDomainToDipScaling(double)` | 倍率を 0.1〜8.0 へ補正 |
| `ResolveDisplayScale(double, double)` | 表示スケールの解決（internal。未設定（0 以下）は倍率を使う） |

### `IMarqueeEffects` のメンバー

| メンバー | 説明 |
| --- | --- |
| `RequestFocus()` | コントロールへフォーカスを要求する |
| `RequestRedraw()` | 再描画を要求する |
| `SetCursorShape(MarqueeCursorKind)` | カーソル形状を設定する（UI が自 UI のカーソル型へ写像する） |
| `SetPointerCapture(bool)` | ポインターキャプチャを取得/解放する |
| `ToScreenPoint(EsPoint)` | ローカル DIP を画面座標へ変換する（nudge のカーソル移動にのみ使う） |

`MarqueeEffectsBase` はこれらの全メンバーを virtual の空実装として提供する `abstract class` です。独自の UI アダプタは本クラスを継承し、必要な効果（例: `RequestRedraw`）だけを override します（不要なメンバーの空実装を書かなくて済む）。

### 描画の契約（`IMarqueeRenderer` / `MarqueeRendererBase`）

描画の契約は `MarqueeControl.UIBase`（UI 側の共有層）が所有します（本コアは描画 API も描画ポートも持ちません）。引数は描画ポート（`IEsCanvas`）と描画内容（`MarqueeDrawContent`）であり、描く値はすべて描画内容に解決済みです。層の順序はコアが `MarqueeDrawContent.Layers` として単一所有し、実装はその順に写すだけです。

### `MarqueeDrawLayer` の値

| 値 | 内容 |
| --- | --- |
| `Decoration` | 選択矩形の外側・内側に描く装飾（暗転マスク・穴あけ枠・枠線・内側の塗り。描く値がある場合のみ） |
| `Handles` | ドラッグハンドル（選択がある場合のみ） |
| `Marker` | 始点マーカー（nudge 中のみ） |
| `Label` | テキスト（サイズラベル・カーソル座標ラベル。文言がある場合のみ。常に最上位） |

**層にある ⇔ 描く値がある**（層の有無 = 描く値の有無）。描く値の無い層は含めないため、UI 層は層ごとの可否を判定しません。

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
- カーソル座標ラベルは基準点を「カーソル + 固定オフセット（8.0 DIP）」にし、描画領域を渡します（描画領域の右端・下端をはみ出す場合は領域内へクランプされ、軸が 0 以下の場合はクランプされません）。
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
- 描画 API は提供しません（描画の手順は UI 非依存の描画層 `MarqueeControl.UIBase` が単一所有し、描画先は `Platform.Core` の `IEsCanvas` です。描画層は本コアの描画内容（`MarqueeDrawContent`）だけを参照し、層の順序に従って写すだけです）。
- 色（ハンドル・始点マーカー・ラベル・装飾）は本コアに含みません（描画にだけ必要な設定であるため、描画層 `MarqueeControl.UIBase` の `MarqueePaint` が単一所有します）。
- 操作ラベル・情報ラベル（アプリ固有の付加情報）は本コアが関知しません。ホストが文言の生成と描画を行います（体裁の適用は `MarqueeControl.UIBase` の `MarqueeHostLabelRenderer` / `FadeMaskHostLabelRenderer` を共用できます）。

## 6. ログ

ロガーは `Marquee.Log` で受け取り、**入力の判断**（`[Feature.MarqueeCore] MouseDown` / `MouseUp` / `DoubleClick` / `DragStart` / `KeyDown` / `ConfirmRequested` / `CancelRequested` 等。Debug。位置の記録は Trace）と**設定の変更**（`[Feature.MarqueeCore] Setting: {名前} {前} → {後}`。Debug）を出力します。ロガー未設定（null）は出力なしです。

| 契機 | 出力 |
| --- | --- |
| `Options` の change | 変化した項目だけ（`ShowHandles` / `HandleSize` / `MarkerLength` / `ShowCoordLabel` / `ShowSizeLabel` / `HandleHitRadius` / `MinSelectionSize` / `ClampToDomainBounds`） |
| `DecoOptions` の change | 変化した項目だけ（`ShowOverlay` / `ShowOuterBorder` / `ShowOuterFullOverlay` / `ShowInnerFill` / `ShowInnerBorder` / `OuterAppearance.BorderThickness` / `InnerAppearance.BorderThickness` / `OuterHighlightRect`（有無）） |
| `DomainToDipScaling` / `DisplayScaleScaling` の set | 変化したときだけ |
| 文言プロバイダ 2 種の set | 設定の有無が変化したときだけ（デリゲートは値の比較ができないため） |

**差分検出（変化した項目だけを出力する判断）と「同値なら出力しない」規則は本コアが単一所有します**（UI 層は前後の値を渡さず、変更を通知するだけです）。ログ基盤（ファイル出力・レベル制御）はホストが所有し、本コアは定義だけを持ちます。**UI 固有のログは各 UI プロジェクトが所有します**（ズーム要求のログは `MarqueeControl.UIBase` の `MarqueeWheelLogMessage`）。

**メッセージの接頭辞は、そのログを定義しているプロジェクト名（コアは `[Feature.MarqueeCore]`、UI 側の共有層は `[MarqueeControl.UIBase]`）とします**（接頭辞で出力元の層を読み分けられるようにする。全層が同じ接頭辞だと、ログを見てもどのアセンブリの定義が出力したかを特定できない）。

## 7. ライセンス

もう一方のコア（`Feature.LoupeCore`）との機能差に由来する非対称は、README の「機能差に由来する非対称（もう一方のコアとの違い）」を参照してください。

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
