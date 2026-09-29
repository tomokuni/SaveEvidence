# Feature.Marquee.Core 外部機能仕様書

`MarqueeControl`（ドラッグで矩形を選び、移動・リサイズできるコントロール）の **UI 非依存の計算層**が提供する外部機能と公開 API の仕様です。利用者（UI 実装・ホスト）から見える情報のみを記載し、内部実装の詳細は記載しません。設計の意図は [README](README.md)、構造と依存は [SPEC_System](SPEC_System.md) を参照してください。

## 1. 提供機能

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| 選択操作の状態機械 | `Marquee` | 押下・移動・解放・キー入力を通知すると、状態遷移の判断（nudge の開始/終了・ダブルクリックの確定/キャンセル・ハンドルのスナップ・クリック検出・最小サイズによるキャンセル・エコー移動の抑制）を行い、効果と通知を返す |
| 幾何の解決 | `Marquee.ResolveGeometry` | 描画領域のサイズを渡すと、1 フレーム分の幾何（穴あけ矩形・ハンドル・始点マーカー・マウス位置）を返す。**穴あけ矩形の優先順（ハイライト → 選択）と閉区間の扱いはコアが単一所有する** |
| 描画計画の解決（本コアの対象外） | 提示層 `Feature.Marquee.Presenter` の `MarqueePresenter` / `MarqueeAppearance` | 幾何に外観（色・フォント・枠線の太さ・表示可否）を重ねた描画計画（`MarqueeDrawPlan`）は提示層が単一所有する（本コアは描画 API も描画ポートも色も持たない） |
| 効果の適用 | `IMarqueeEffects` | フォーカス・再描画・カーソル操作・カーソル形状・ポインターキャプチャ・画面座標変換の要求を UI 実装が受ける（不要なメンバーの空実装は `MarqueeEffectsBase` を継承すれば省略できる） |
| 通知 | `Marquee.Notified` | 選択の開始/変更/確定/キャンセル・追従位置の移動・確定/キャンセル要求を `MarqueeNotice` で通知する |
| 外部からの状態操作 | `Marquee` | 選択の復元・対話状態の初期化・確定/キャンセルの要求・ハイライト矩形の設定を API から実行できる |
| 座標変換 | `Marquee` / `ICoordTransform` | ドメイン座標（利用者が定める座標系）とローカル DIP の相互変換と、ドメイン境界の指定 |
| ラベルの文言と基準点 | 提示層 `Feature.Marquee.Presenter` の `MarqueeLabelTexts` / `MarqueeLabelPlan` | サイズラベルの書式と表示スケール、カーソル座標ラベルの文言と基準点、実寸から配置矩形への写像（本コアはマウス位置という素材だけを返す） |

### 前提条件

- 本コアは UI フレームワークに依存しません。UI 固有の型は利用者（UI 実装）が境界で変換します。
- 座標は `EsPoint` / `EsRect` / `EsSize` / `EsKey`（`EsUtil.Platform.Core`）で扱います。
- カーソル操作は `ICursorService` 抽象にのみ依存します（`null` の場合は何もしません）。
- **本コアが返すのは幾何だけです**。文言・表示可否・描画順・色は提示層（`Feature.Marquee.Presenter`）が単一所有します。**描画と実寸測定**は利用者（UI 実装）が行います（実寸は描画 API でしか測れないため）。

## 2. 公開 API 一覧

可視性は public のみを記載します（`internal` は [SPEC_System](SPEC_System.md) の可視性ポリシーを参照）。

### `EsUtil.Feature.Marquee.Core`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `Marquee` | class | 操作の唯一の窓口となるファサード |

### `EsUtil.Feature.Marquee.Core.Contract`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `IMarqueeEffects` | interface | コアが要求する効果の適用先（UI 実装が実装する） |
| `MarqueeEffectsBase` | abstract class | `IMarqueeEffects` の全メンバーを virtual の空実装で提供する基底クラス（必要な効果だけ override する） |

### `EsUtil.Feature.Marquee.Core.Drawing`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeGeometry` | record struct | 1 フレーム分の幾何（`ClientSize` / `HoleRect` / `HoleDomainRect` / `Handles` / `Marker` / `MousePosition` ＋ `SurfaceBounds`）。**コアが責任を持つ値の全量** |
| `MarqueeRenderRequest` | record struct | 1 フレーム分の幾何を解決する入力（設定・状態と、描画時に決まる値）。`Marquee.CreateRequest` が生成し、`Marquee.ResolveGeometry` へ渡す。値の組は `MarqueeRenderRequest.Create` が 1 箇所に閉じる。**`Create` を public にする理由**: ファサード（`Marquee.CreateRequest`）と独自 UI が入力を組み立てる唯一の入口である（描画先の領域を知るのは呼び出し側だけである） |
| `MarqueeHoleRect` | record struct | 穴あけ矩形と**閉区間かどうか**の組（選択矩形は閉区間（終点 +1）、ハイライト矩形はホストが全ピクセルを含む閉区間として与えるため +1 しない） |
| `MarqueeHandles` / `MarqueeMarker` | record struct | ハンドル・始点マーカーの幾何（位置はコアが解決済み） |

### `EsUtil.Feature.Marquee.Core.Interaction`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeSnapshot` | readonly record struct | 通知時点の状態（状態・選択矩形・アクティブターゲット・マウス位置・操作フラグ）。値型のため通知のたびにヒープを確保しない |
| `MarqueeNotice` / `MarqueeNoticeKind` | record / enum | 通知（種別・通知座標・スナップショット） |
| `MarqueeHitTarget` | enum | ヒット対象（8 方向のハンドル + `Move` + `None`） |
| `MarqueeCursorKind` | enum | カーソル形状の種別（`Default` / `DiagonalNWSE` / `DiagonalNESW` / `Vertical` / `Horizontal` / `Move`） |
| `MarqueeInteractionState` | enum | 対話状態（`NoSelection` / `StartPointNudging` / `Dragging` / `DraggingNudging` / `Selected` / `SelectedNudging`） |

### `EsUtil.Feature.Marquee.Core.Setting`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `MarqueeOptions` | record | 設定（**計算に必要な値のみ**＝寸法・対話パラメータ）と既定値・補正。**色・フォントサイズ・枠線の太さ・表示可否は持たない**（提示層の `MarqueeAppearance` が単一所有する） |

### `Marquee`（ファサード）のメンバー

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Marquee(IMarqueeEffects, ICursorService?, MarqueeOptions?)` | ctor | `effects` は null 不可。`cursor` は null 可（カーソル操作を行わない）。描画実装も外観も受け取らない（描画は UI の関心事であり、本コアは描画 API を持たない） |
| `Notified` | event | 通知（`MarqueeNotice`） |
| `Log` | property | ロガー。null は `NullLogger` へ吸収される |
| `Options` | property | 設定。設定すると対話パラメータを反映し、**変化した項目をログ出力**し、境界外選択の禁止の変化時のみ即時クリップを行う |
| `DomainToDipScaling` | property | 座標変換ポート未指定時に適用する倍率。既定は `Limits.DomainToDipScalingDefault`（1.0）。設定時は 0.1〜8.0 へ補正される |
| `HighlightRect` | property | ハイライト矩形（ドメイン座標）。null が「ハイライトしない」。**変更は有無の変化だけをログ出力する**（操作中に高頻度で変化しうるため） |
| `HasSelection` / `SelectionRect` | property | 選択の有無と選択矩形（ドメイン座標） |
| `CreateSnapshot(EsPoint?)` | method | 状態スナップショットの生成 |
| `SetCoordTransform(ICoordTransform?)` | method | 座標変換ポートの設定（null は等価変換） |
| `ToDomain(EsPoint)` / `ToDip(EsPoint)` | method | 座標変換 |
| `Press(EsPoint, int)` / `Move(EsPoint)` / `Release(EsPoint)` | method | ポインター入力の通知（ドメイン座標） |
| `KeyDown(EsKey, EsPoint?)` | method | キー入力の通知。処理済みの真偽を返す（`Escape` / `Enter`（`Return` 含む）/ 矢印キー） |
| `EndNudge()` | method | nudge の終了（フォーカス喪失で使用） |
| `SetSelection(EsRect)` / `ResetSelection()` | method | 選択の復元・対話状態の初期化 |
| `RequestConfirm()` / `RequestCancel()` | method | 確定/キャンセルの要求（キー・ダブルクリックと同一経路） |
| `CreateRequest(EsSize)` | method | 幾何 1 フレーム分の入力（`MarqueeRenderRequest`）の生成（設定・状態と、描画時に決まる値） |
| `ResolveGeometry(MarqueeRenderRequest)` | method | 1 フレーム分の幾何（`MarqueeGeometry`）の解決 |

**設定では変更しない固定値（ハンドル枠の太さ 1.5・始点マーカーの太さ 2.0・カーソル座標ラベルのオフセット 8.0）は公開しません**。参照するのはコアの幾何の解決と提示層の文言生成だけであり、UI 層は解決済みの値（`MarqueeHandles.BorderThickness` 等）や提示層が返す配置指示を受け取って使うためです（公開面へ出す必然性がない）。

### `MarqueeOptions` のメンバー

| 分類 | メンバー | 既定値 | 安全域（生成時に補正） |
| --- | --- | --- | --- |
| 寸法 | `HandleSize` | 10.0 | 1.0〜256.0 |
| 寸法 | `MarkerLength` | 16.0（幾何の半幅は 1/2） | 2.0〜4096.0 |
| 対話 | `HandleHitRadius` | 12.0（DIP） | 0.0〜256.0 |
| 対話 | `MinSelectionSize` | 6.0（ドメイン座標。これ未満はキャンセル扱い） | 1.0〜4096.0 |
| 対話 | `ClampToDomainBounds` | `true` | — |

数値項目の補正は**本型の生成時に適用**します（`init` アクセサ。安全域は `Marquee.Limits` が単一所有する）。設定の経路（オブジェクト初期化子・`with` 式・逆シリアル化）によらず範囲内の値だけが保持されます。

**本型は表示可否を持ちません**。ハンドル・サイズラベル・カーソル座標ラベル・装飾を描くかどうかは、**描画の外観（提示層の `MarqueeAppearance`）の明示フラグ**が単一所有します（述語は `ShouldShowSizeLabel(bool)` / `ShouldShowCoordLabel()` / `ShouldDrawOverlay(bool)`）。

**本型は色もラベルのフォントサイズも持ちません**。これらは描画にだけ必要な設定であるため、提示層 `Feature.Marquee.Presenter` の `MarqueeAppearance` が単一所有します（部分変更の基点は `MarqueeAppearance.Default`。ハンドル枠と穴あけ枠の既定は共通の `MarqueeAppearance.DefaultBorderColor` = `#FFA9A9A9`、フォントサイズは `SizeLabelFontSizeDefault` = 14.0 / `CoordLabelFontSizeDefault` = 12.0、枠線の太さは `OuterBorderThickness` / `InnerBorderThickness`（既定 2.0）、内側余白は `LabelPadding`（既定 4.0））。ラベルの文字色・背景色の既定値（白 / 黒 60%）も `MarqueeAppearance` が単一所有するため、サイズラベル・カーソル座標ラベル・ルーペが同じ見た目になります。

### `Marquee.Limits` のメンバー

選択矩形の数値パラメータの既定値と安全域を単一所有し、範囲外の値を補正します（非有限は既定値）。**数値スキーマに加えて「未設定（0 以下）の解決規則」も持ちます**（`ResolveDisplayScale`）。

| 定数 | 値 | 説明 |
| --- | --- | --- |
| `HandleSizeMin` / `HandleSizeMax` / `HandleSizeDefault` | `1.0` / `256.0` / `10.0` | ドラッグハンドルの一辺サイズ（DIP） |
| `MarkerLengthMin` / `MarkerLengthMax` / `MarkerLengthDefault` | `2.0` / `4096.0` / `16.0` | 十字マーカーの全長（DIP） |
| `HandleHitRadiusMin` / `HandleHitRadiusMax` / `HandleHitRadiusDefault` | `0.0` / `256.0` / `12.0` | ハンドルのヒットテスト半径（DIP） |
| `MinSelectionSizeMin` / `MinSelectionSizeMax` / `MinSelectionSizeDefault` | `1.0` / `4096.0` / `6.0` | 選択矩形の最小サイズ（ドメイン座標） |
| `BorderThicknessMin` / `BorderThicknessMax` / `BorderThicknessDefault` | `0.5` / `64.0` / `2.0` | 枠線の太さ（DIP。提示層の外観の既定値も本値を参照する） |
| `DomainToDipScalingMin` / `DomainToDipScalingMax` / `DomainToDipScalingDefault` | `0.1` / `8.0` / `1.0` | ポート未指定時に適用する倍率（環境値。他の数値より後に置く） |

| メソッド | 説明 |
| --- | --- |
| `CoerceHandleSize(double)` / `CoerceMarkerLength(double)` / `CoerceHandleHitRadius(double)` / `CoerceMinSelectionSize(double)` | 各寸法を安全域へ補正（非有限は既定値） |
| `CoerceBorderThickness(double)` | 枠線の太さを 0.5〜64.0 へ補正（0 以下の指定を無効化する。提示層の外観が使う） |
| `CoerceDomainToDipScaling(double)` | 倍率を 0.1〜8.0 へ補正 |
| `ResolveDisplayScale(double, double)` | 表示スケールの解決（**public**。未設定（0 以下）は倍率を使う。提示層のサイズラベル書式が使う） |

### `IMarqueeEffects` のメンバー

| メンバー | 説明 |
| --- | --- |
| `RequestFocus()` | コントロールへフォーカスを要求する |
| `RequestRedraw()` | 再描画を要求する |
| `SetCursorShape(MarqueeCursorKind)` | カーソル形状を設定する（UI が自 UI のカーソル型へ写像する） |
| `SetPointerCapture(bool)` | ポインターキャプチャを取得/解放する |
| `ToScreenPoint(EsPoint)` | ローカル DIP を画面座標へ変換する（nudge のカーソル移動にのみ使う） |

`MarqueeEffectsBase` はこれらの全メンバーを virtual の空実装として提供する `abstract class` です。独自の UI アダプタは本クラスを継承し、必要な効果（例: `RequestRedraw`）だけを override します（不要なメンバーの空実装を書かなくて済む）。

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

### 幾何（`MarqueeGeometry`）

| 項目 | 内容 |
| --- | --- |
| `ClientSize` / `SurfaceBounds` | 描画領域のサイズ（DIP）と、原点（0, 0）からの描画面全体 |
| `HoleRect` | 穴あけ矩形（DIP。選択もハイライトも無い場合は null）。選択は閉区間（終点 +1）、ハイライトは +0 |
| `HoleDomainRect` | 穴あけ矩形（ドメイン座標）。ホストが矩形ごとの表示スケールを解決するために使う |
| `Handles` | ドラッグハンドルの幾何（`Rects` 8 方向・`BorderThickness`）。選択が無い場合は null |
| `Marker` | 始点マーカーの幾何（`Center` / `Thickness` / `Size`＝半幅）。nudge 中でない場合は null |
| `MousePosition` | 直近のマウス位置（ドメイン座標）。カーソル座標ラベルの基準 |

**幾何は「描く値の無い項目は null / 空」で返します**（穴あけ矩形なし・選択なし・nudge 中でない）。**表示可否（ハンドル・ラベルを描くかどうか）は本コアの責務ではありません**（提示層の外観が単一所有します）。

### 操作の受理条件

| 操作 | 受理条件 |
| --- | --- |
| `Press` のダブルクリック | 選択矩形の内側なら確定、外側ならキャンセル |
| `RequestConfirm` | ドラッグ中は無視。選択があるときのみ確定し、確定後は選択を破棄する |
| `RequestCancel` | ドラッグ中または選択がある場合は選択を破棄してから通知する |
| `KeyDown` の矢印キー | 選択済みかつマウス非押下の場合は nudge しない（領域作成後の nudge はドラッグ中のみ） |
| `Release` | 最小サイズ未満の矩形はキャンセル扱い |

## 4. 座標系

| 座標系 | 用途 |
| --- | --- |
| ドメイン座標 | 選択矩形・入力座標・通知座標・ハイライト矩形。利用者が座標変換ポートで定義する |
| ローカル DIP | 幾何の座標（`HoleRect` / `Handles` / `Marker`）。`ICoordTransform.Inverse` で得る |

- 選択矩形は**閉区間**（始点・終点とも含む）として扱い、DIP への変換時に終点へ +1 します。**ハイライト矩形はホストが全ピクセルを含む閉区間として与えるため +1 しません**（優先順と閉区間の規則は `MarqueeRenderRequest` が単一所有します）。
- ドメイン境界（`ICoordTransform.Bounds`）は `ClampToDomainBounds` が有効な場合のクリップ範囲です。
- **コアは描画面の原点が常に (0, 0) である前提**で幾何を返します（親の座標を混ぜるとハンドルがずれるため、描画領域は原点を持たない `EsSize` で受けます）。

## 5. 制限事項

- ホイール操作（Ctrl+ズーム / スクロール）は本コアに含みません（組み込み先が存在して初めて意味を持つ処理であるため、UI 層（ホスト）がビューポートへ転送します）。
- ビューポートの変化の通知は本コアに含みません（同じ理由で UI 層（ホスト）が持ち、変化を検知したら `RefreshOverlay()` を呼びます）。
- 描画 API は提供しません。描画計画（外観・文言・層の順序）は提示層（`Feature.Marquee.Presenter`）が単一所有し、描画は各 UI が行います。
- **装飾（暗転マスク・穴あけ枠・内側の塗り）の概念を持ちません**。装飾は選択矩形コントロール単体の概念ではなく、組み込み先の表示であるため、提示層が単一所有します（本コアは穴あけ矩形という幾何だけを返します）。
- 色（ハンドル・始点マーカー・ラベル・装飾）は本コアに含みません（描画にだけ必要な設定であるため、提示層の `MarqueeAppearance` が単一所有します）。
- 文言（サイズラベルの書式・カーソル座標ラベルの文字列）は本コアに含みません（提示層の `MarqueeLabelTexts` が単一所有します。本コアはマウス位置という素材だけを返します）。
- 操作ラベル・情報ラベル（アプリ固有の付加情報）は本コアが関知しません。ホストが文言の生成と描画を行います。

## 6. ログ

ロガーは `Marquee.Log` で受け取り、**入力の判断**（`[Feature.Marquee.Core] MouseDown` / `MouseUp` / `DoubleClick` / `DragStart` / `KeyDown` / `ConfirmRequested` / `CancelRequested` 等。Debug。位置の記録は Trace）と**設定の変更**（`[Feature.Marquee.Core] Setting: {名前} {前} → {後}`。Debug）を出力します。ロガー未設定（null）は出力なしです。

| 契機 | 出力 |
| --- | --- |
| `Options` の change | 変化した項目だけ（`HandleSize` / `MarkerLength` / `HandleHitRadius` / `MinSelectionSize` / `ClampToDomainBounds`） |
| `HighlightRect` の set | 有無が変化したときだけ（`False → True` / `True → False`） |
| `DomainToDipScaling` の set | 変化したときだけ |

**差分検出（変化した項目だけを出力する判断）と「同値なら出力しない」規則は本コアが単一所有します**（UI 層は前後の値を渡さず、変更を通知するだけです）。ログ基盤（ファイル出力・レベル制御）はホストが所有し、本コアは定義だけを持ちます。**描画にだけ必要な設定（外観・表示スケーリング係数）の変更ログは提示層が所有します**（`Feature.Marquee.Presenter` の `MarqueePresenterLogMessage`）。**本コアは外観を持たないため、外観の変更ログも持ちません**（外観はコアの計算・判断に入らない）。

**メッセージの接頭辞は、そのログを定義しているプロジェクト名（コアは `[Feature.Marquee.Core]`、提示層は `[Feature.Marquee.Presenter]`）とします**（接頭辞で出力元の層を読み分けられるようにする。全層が同じ接頭辞だと、ログを見てもどのアセンブリの定義が出力したかを特定できない）。

## 7. ライセンス

もう一方のコア（`Feature.Loupe.Core`）との機能差に由来する非対称は、README の「機能差に由来する非対称（もう一方のコアとの違い）」を参照してください。

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
