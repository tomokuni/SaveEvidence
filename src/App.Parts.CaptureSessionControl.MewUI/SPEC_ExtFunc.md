# App.Parts.CaptureSessionControl.MewUI 外部機能仕様

MewUI 版の撮影セッションが提供する**公開 API** を記載します。 内部実装（描画手順・キャッシュ等）は記載しません。

## 1. 公開型の構成

| 型 | 種別 | 役割 |
| --- | --- | --- |
| `CaptureSessionControl` | sealed クラス（Facade） | 撮影と切り出しの窓口。 排他（同時に 1 つだけ）とセッションの寿命を単一所有する |
| `CaptureOverlayServices` | sealed record | 撮影が必要とするサービスの束（設定は含まない） |
| `CropViewControl` | sealed クラス（複合コントロール） | 画像ビューポート（拡大・パン・Fit）+ 外部提供矩形オーバーレイ |
| `FadeMaskDragRectControl` | sealed クラス（複合コントロール） | 暗転マスク + 選択矩形 + ルーペ。 モードで可視性を切替 |
| `FadeMaskDragRectSelectionEventArgs` | sealed record | 確定要求のイベント引数（`SelectionDomainRect: EsRect`） |
| `FadeMaskDragRectPointEventArgs` | sealed record | ルーペ位置のイベント引数（`Position: EsPoint`） |

`OverlayRect` / `OverlayLineStyle`（矩形オーバーレイの描画情報）は `App.Parts.CaptureSessionControl.Core`（名前空間 `EsUtil.SaveEvidence.Parts.CaptureSession`）が提供します。

名前空間は `EsUtil.SaveEvidence.Parts.CaptureSession.MewUI` です。

`CaptureOverlay`（撮影セッション）と `CropSession`（切り出しセッション）は `internal` です
（1 セッション = 1 インスタンスであり、生成・実行・破棄・排他の受付は Facade が担います）。

## 2. CaptureSessionControl

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| コンストラクタ | `CaptureSessionControl(CropViewControl cropView, ICaptureTarget cropTarget, Grid cropHost, CaptureSettings? initialSettings = null, ILogger? logger = null)` | Facade を生成する。 切り出しの対象（`cropView`）とオーバーレイを重ねる領域（`cropHost`）はホストが用意する。 `initialSettings` の null メンバーは既定値で埋める |
| `Capture` | `EsBitmap? Capture(Window owner, CaptureType type, CaptureOverlayServices services)` | オーバーレイを表示し、確定した領域を切り出して返す。 キャンセル・取得失敗・退化した選択の場合は null。 **実行中のセッションがある場合は何もせず null を返す** |
| `ToggleCrop` | `void ToggleCrop()` | 切り出しの開始・確定・キャンセルを切り替える。 どの操作を行うかの判断はコアの `CropSessionActions.Resolve` が単一所有する（未開始なら開始 / 選択済みなら確定 / 未選択ならキャンセル）。 **撮影中は開始しない** |
| `AppearanceSettings` | `CaptureSettings` | 現在の設定（getter は全メンバー non-null）。 設定時は null メンバーのみ現在値を維持する部分更新。 **同一値の再設定はスキップ**。 検証・適用ログはコア（`CaptureSettingsMerge.Apply`）が単一所有する。 実行中の切り出しセッションへは即時反映する |

### 2.0 排他の規則

撮影（`Capture`）と切り出し（`ToggleCrop`）は**同時に 1 つだけ**実行できます。  
この規則は `App.Parts.CaptureSessionControl.Core` の `CaptureSessionSlot` が単一所有し、本 Facade がその唯一の適用者です。  
実行中の要求は無視し、**実行中のセッションを中断しません**（撮影中に切り出しを開始できない・切り出し中に撮影を開始できない）。

### 2.1 動作

1. 設定（Facade の `AppearanceSettings`）に従ってオーナーウィンドウをキャプチャ除外に設定し、画面外へ退避します（`CaptureOwnerScope` が担う）。
2. 仮想スクリーン（全モニターを包含する領域）を 1 枚の画像として取得します（`ICaptureService.CaptureVirtualScreen`）。
3. 画像を敷いたボーダーレス・最前面のウィンドウを仮想スクリーン全面へ表示し、確定またはキャンセルまで待ちます（`ShowDialog` の入れ子ループ）。
4. 確定した領域を切り出して返し、オーナーの配置を復帰して例外を解除します（例外時も `finally` で復帰）。

### 2.2 操作

| モード | 操作 |
| --- | --- |
| スクリーン選択 | カーソル下のモニターをハイライトし、左クリックまたは Enter で確定 |
| ウィンドウ選択 | カーソル下の最前面ウィンドウをハイライトし、左クリックまたは Enter で確定 |
| 範囲選択 | ドラッグで矩形を指定し、Enter で確定（矢印キーで微調整できる） |

- 右クリックでは確定しません。 要素を検出できない位置のクリックでも確定しません（誤操作で撮影を取りこぼさないため）。
- **左クリックの確定は押下で要素を固定し、解放時に行います**（押下を伴わない解放・退化した検出では確定しない。 固定と取り出しの規則はコアの `CaptureClickSelection` が単一所有する）。
- **範囲選択では十字カーソルを表示します**（選択スタイルの判定はコアの `CaptureTypeTraits.IsHighlighted` が単一所有し、ハイライト型では表示しない。 Avalonia 版と同じ見え方にする）。
- **Enter（Return を含む）= 確定 / Escape = キャンセル**です（確定対象が無い状態の Enter は何もしません。 キーの解釈はコアの `CaptureKeyActions` が単一所有する）。
- Escape または他ウィンドウへの切り替え（非アクティブ化）でキャンセルします。
- オーバーレイはタスクバー・Alt+Tab の一覧に表示しません（一時的な表示のため）。

### 2.3 座標系

| 座標 | 意味 |
| --- | --- |
| 画像ピクセル（ドメイン） | 取得画像のピクセル。 **画像の左上が原点**（画像の左上は撮影範囲の原点 = 仮想スクリーンの原点に対応するため、画面物理座標とは原点の分だけ異なる。 原点は負値になりうる） |
| 画面物理ピクセル | 検出結果・入力（マウス）の座標系 |

ドメイン座標とローカル DIP の変換は `ICoordTransform` を選択コントロールへ注入して行います（コントロールは座標系を意識しません）。  
確定時の切り出し矩形は**画面物理座標の入口**（`CaptureClipMath.ResolveCaptureRect`）で解決します。 選択矩形は画像ローカル座標（画像の左上が原点）のため、**境界で `CaptureClipMath.ToScreenRect` により画面物理座標へ戻してから**渡します（１つの座標空間に揃えることで、原点が負値のマルチモニタでも切り出し位置・幅が正しくなる）。

確定要求（キー・クリック・アダプタ要求）は、すべて同じ入口（画面物理座標）で `CaptureLogMessage.LogConfirmRequested` に記録します（記録する矩形の座標空間を UI 間で揃える）。 完了の領域（`CaptureResult.SourceBounds`）も契約どおり仮想画面座標（画面物理）で返します。

### 2.4 切り出し規則

- 範囲選択は**閉区間**（始点・終点のピクセルを含む）で切り出します。
- スクリーン選択・ウィンドウ選択は検出結果が全ピクセルを含むため、終点の 1 ピクセルを加算しません。
- 切り出し矩形は画像の範囲へクランプします（画像の右端・下端で透明な余白が付かないようにするため）。
- 切り出せる矩形が無い（退化した選択）場合はキャンセル扱い（null）になります。
- 計算は `App.Parts.CaptureSessionControl.Core` の `CaptureClipMath.ResolveCaptureRect` が単一所有します（種別の性質とクランプ・丸めを組み合わせた唯一の入口）。
- 最小サイズは選択操作（Marquee）が判定します（検出矩形は要素の大きさであり、小さい要素も切り出し対象とする）。

## 3. CaptureOverlayServices

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `Capture` | `ICaptureService` | 画面キャプチャ（仮想スクリーンの取得） |
| `Finder` | `IElementFinder` | 要素検出（カーソル下のモニター・ウィンドウ） |
| `Window` | `IWindowService` | ネイティブウィンドウ操作（キャプチャ除外・退避・物理ピクセルでの配置） |
| `Logger` | `ILogger?` | ロガー（null の場合は出力しない） |

**設定は含めません**: 設定は Facade（`AppearanceSettings`）が単一所有し、撮影と切り出しが同じ値を使います（ホストが設定の変換を 2 回呼ぶ・2 つの設定値が食い違う余地をなくす）。

`ICaptureService` / `IElementFinder` / `IWindowService` は `Platform.Core`（名前空間 `EsUtil.Platform.Core.Native`）が提供し、Windows 実装は同 `Native.Win`（`WinCaptureService` 等）です。 ホストが生成して抽象型として注入します。

## 4. 使用する設定項目

`CaptureSettings`（`App.Parts.CaptureSessionControl.Core`）のうち、本ライブラリが参照する項目です。

| 項目 | 用途 |
| --- | --- |
| `HideOwnerWindow` | 撮影中にオーナーウィンドウを画面外へ退避するか |
| `MinSelectionSize` | 選択矩形のドラッグ中の最小サイズ（`MarqueeOptions.MinSelectionSize` へ反映。 切り出しの可否は選択操作そのものが判定する） |
| `BorderColor` / `BorderThickness` / `OverlayColor` | 選択枠と暗転の外観（太さと色は提示層の `MarqueeAppearance`（`OuterBorderThickness` / `OuterBorderColor` / `OverlayColor`）へ反映。 枠線は選択矩形と共用） |
| `HandleColor` / `HandleSize` / `HandleHitRadius` | ドラッグハンドル（寸法は選択矩形の設定、色は提示層の `MarqueeAppearance.HandleColor` へ反映） |
| `MarkerColor` / `MarkerLength` | 微調整時の十字マーカー（寸法は選択矩形の設定、色は提示層の `MarqueeAppearance.MarkerColor` へ反映） |
| `ClampToDomainBounds` | 画像外の選択を禁止するか |
| （ホイール操作） | 選択オーバーレイ上のホイールをビューポート（ズーム・パン）へ転送する。 ズーム率・パン量の決定は `CropViewControl` が単一所有する |
| `ShowCoordLabel` / `CoordLabelColor` / `CoordLabelBackgroundColor` / `CoordLabelFontSize` | カーソル位置の座標ラベル（表示可否・フォントサイズ・色は提示層の `MarqueeAppearance` へ反映） |
| `SizeLabelVisible` / `SizeLabelColor` / `TextBackgroundColor` / `SizeLabelFontSize` | サイズラベル（表示可否・フォントサイズ・色は提示層の `MarqueeAppearance` へ反映。 **文言はホストが単一所有する**） |
| `InfoLabelVisible` / `InfoLabelColor` / `InfoLabelBackgroundColor` / `InfoLabelFontSize` | 情報ラベル（ウィンドウタイトル。 `FadeMaskDragRectLabelOptions`（ホストが描く付加情報ラベルの外観）へ反映） |
| `ShowSubLines` / `SubLineFontSize` / `SubLineColor` / `SubLineBackgroundColor` | 操作ラベル（サブライン。 `FadeMaskDragRectLabelOptions` へ反映） |
| `LoupeSize` / `LoupeZoomLevel` / `LoupeOffset` / `LoupeCrossColor` / `LoupeFrameColor` / `LoupeFrameThickness` | ルーペ（サイズ・倍率・オフセットは `LoupeOptions`、枠線の太さは `LoupeAppearance.BorderThickness`、色は外観 `LoupeAppearance` へ反映） |
| `LoupePasteboardColor` | ルーペの画像外（ペーストボード）の色（外観 `LoupeAppearance.PasteboardColor` へ反映） |
| `LoupeSelectionRectColorA` / `LoupeSelectionRectColorB` | ルーペ画像上の選択矩形破線（選択矩形破線の色（`FadeMaskDragRectOptions`）へ反映） |
| `LoupeShowCoordLabel` / `LoupeCoordLabelColor` / `LoupeCoordLabelBackgroundColor` / `LoupeCoordLabelFontSize` | ルーペの中心座標ラベル（表示可否・フォントサイズ・色は外観 `LoupeAppearance` へ反映） |

### 4.1 適用先

上表の項目は撮影（`CaptureOverlay`）と切り出し（`CropSession`）が同じマッピングで反映します。 **計算に必要な寸法・対話パラメータ**はコアの `FadeMaskDragRectOptionsMapper` が単一所有し、**描画にだけ必要な外観（色・枠線の太さ・表示可否）**は UI 間で共有する `App.Parts.CaptureSessionControl.UIBase` の `CapturePaintMapper`（`MarqueeAppearance` / `LoupeAppearance` を組み立ててコントロールのプロパティへ設定する）が担います。
ただし、切り出しはプレビューの表示に合わせる必要があるため、次の項目は撮影専用です。

| 項目 | 理由 |
| --- | --- |
| `LoupeDipToDeviceScaling` / `DisplayScaleScaling` | 切り出しのルーペ・ラベルはプレビューの表示倍率（`CropViewControl` が所有）に従うため、設定値を与えない |

情報ラベルはキャプチャコアの `CaptureLabelTexts.GetInfoText` がウィンドウ選択に限定するため、切り出しでは表示されません。

### 4.2 切り出し（CropSession）

切り出しは Facade の `ToggleCrop` からのみ操作します（`CropSession` は internal のため、ホストが直接操作することはありません）。  
切り出しセッションは開始時にオーバーレイを表示ツリーへ加え、確定・キャンセルで取り外します（ホストが事前に表示ツリーへ入れる必要はありません）。

| 操作 | 内容 |
| --- | --- |
| 開始 | オーバーレイを表示し、入力を選択コントロールへ移す（画像が未設定の場合は何もしない） |
| 確定（Enter 相当） | 現在の選択矩形を切り出す（退化した選択はキャンセルとして記録し、画像を差し替えない） |
| キャンセル（Escape 相当） | 選択を破棄してオーバーレイを取り外す（画像は差し替えない） |

切り出しの適用先は `ICaptureTarget`（アプリ側が実装するポート）です。 切り出し結果の適用（Undo 退避・画像差し替え）はアプリ側の責務です。

## 5. CropViewControl（切り出しビュー）

画像の表示（拡大・パン・Fit）と、ホストから渡された矩形のオーバーレイ描画を 1 つのコントロールにまとめた複合コントロールです（Facade パターン）。 画像ビューポートは `App.Parts.ZoomViewControl.MewUI` を内包し、その上に透明な矩形オーバーレイを重ねます。

名前空間は `EsUtil.SaveEvidence.Parts.CaptureSession.MewUI`、クラスは `CropViewControl`（`public sealed`、`Grid` 派生）です。

### 5.1 プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Source` | `EsBitmap?` | `null` | 表示する画像。 設定すると Fit 表示（ズーム率 0・オフセット 0）へリセットする |
| `CenterAlign` | `bool` | `true` | 画像がビューポートより小さい軸を中央寄せする（false で左上寄せ） |
| `Appearance` | `ZoomViewAppearance` | 暗色の下地・市松模様なし | 下地（市松模様の大きさ・2 色・背景色）の外観の**単一値**。 コアが単一所有する |
| `Rects` | `IReadOnlyList<OverlayRect>` | 空 | オーバーレイとして描画する矩形（ドメイン座標 = 元画像ピクセル座標）。 null は設定できない |
| `ZoomView` | `ZoomViewControl` | — | 内包する画像ビューポート |
| `ZoomPercent` | `double` | Fit の実効倍率 | 実効ズーム率（%）。 **0 より大きい値のみ設定可能**（0 以下は無視） |
| `CanZoomIn` / `CanZoomOut` | `bool` | `false` | ズーム可否（画像未設定時は false） |
| `Log` | `ILogger` | `NullLogger` | ズーム操作ログの出力先（内包する画像ビューポートへ伝搬する） |

### 5.2 メソッド

| メソッド | 説明 |
| --- | --- |
| `ZoomIn()` / `ZoomOut()` | 段階レベルを 1 つ移動する（移動先がなければ何もしない） |
| `ZoomFit()` | Fit 表示へ戻す |
| `PanByWheel(EsVector, bool)` | ホイールの回転量（X/Y）と Shift 併用でパンする |
| `ZoomByWheel(bool, EsPoint?)` | Ctrl+ホイールのズームを適用する（位置はズーム中心。 null はビューポート中心） |
| `TranslatePositionFrom(Element, EsPoint)` | 重ねた要素のローカル座標を本コントロールの座標へ変換する |
| `ImageRectViewport()` | 画像の表示矩形（ビューポート座標・DIP） |
| `ViewportToContent(EsPoint)` | ビューポート座標 → ドメイン座標（画像表示矩形の原点基準） |
| `ContentToViewport(EsPoint)` | ドメイン座標 → ビューポート座標（画像表示矩形の原点基準） |

### 5.3 イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `ZoomChanged` | `ZoomChangedEventArgs` | ズーム率・Fit 状態・ズーム可否（内包する画像ビューポートから中継） |
| `ViewportChanged` | `EventArgs` | パン・ズーム・リサイズ（内包する画像ビューポートから中継） |

### 5.4 矩形オーバーレイの仕様

| 項目 | 仕様 |
| --- | --- |
| 座標系 | ドメイン座標（元画像ピクセル）。 表示時にビューポート座標へ変換する |
| 実線（`Solid`） | 前景色（`Foreground`）の枠のみを描画する |
| 破線（`Dashed`） / 点線（`Dotted`） | 背景色（`Background`）の実線の上へ前景色の破線を重ねる（市松風の視認性） |
| ダッシュ長 | 破線 = 8 / 間隔 4、点線 = 2 / 間隔 2（`App.Parts.CaptureSessionControl.Core` の `OverlayLineDash` が単一所有） |
| 線の太さ | `Thickness`（DIP）。 0 以下は 1 として扱う。 枠は矩形の内側へ描画する |
| サイズ 0 の矩形 | 何も描画しない |
| 色 | `System.Drawing.Color`（アルファを含む） |

### 5.5 エラー時の挙動

| 状況 | 挙動 |
| --- | --- |
| `Source` が `null` | 何も描画しない（例外にしない） |
| `Rects` が空 | 何も描画しない（例外にしない） |
| `ZoomPercent` へ 0 以下を設定 | 無視する（Fit へは `ZoomFit()` を使う） |

領域選択は本コントロールの対象外です。 領域選択は `FadeMaskDragRectControl` を重ねて構成します。

## 6. FadeMaskDragRectControl（領域選択）

選択矩形（ドラッグ）・暗転マスク・ルーペを 1 つのコントロールにまとめ、配線と状態同期を内側で完結させる複合コントロールです（Facade / Composite パターン）。 ホストは `Mode` の設定と状態の受け取りだけで利用できます。 動作モードとモードから描画設定への写像は UI 非依存コア（`App.Parts.CaptureSessionControl.Core`）が単一所有します。

名前空間は `EsUtil.SaveEvidence.Parts.CaptureSession.MewUI`、クラスは `FadeMaskDragRectControl`（`public sealed`、`Panel` 派生）です。

### 6.1 動作モード

| モード | 選択矩形 | ルーペ | 全面暗転 | 用途 |
| --- | --- | --- | --- | --- |
| `Crop` | 有効 | 有効 | 有効 | プレビュー内の切出し |
| `AreaSelect` | 有効 | 有効 | 有効 | 全画面オーバーレイの領域選択 |
| `ScreenSelect` | 無効 | 無効 | 無効 | スクリーン選択（ハイライトのみ。 確定フローはホスト） |
| `WindowSelect` | 無効 | 無効 | 無効 | ウィンドウ選択（ハイライトのみ。 確定フローはホスト） |

既定値は `AreaSelect` です。 未定義の値を設定した場合は `WindowSelect` と同じ扱いになります。

### 6.2 プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Mode` | `FadeMaskDragRectMode` | `AreaSelect` | 動作モード（可視性と全面暗転を一括切替） |
| `Options` | `FadeMaskDragRectOptions`（コア） | `Default` | 設定の単一値（選択矩形 / 暗転マスク / ルーペ / 選択矩形破線。 色は含まない）。 同じ値の再設定はスキップされる |
| `MarqueeAppearance` / `LoupeAppearance` | 提示層の外観 | 各 `Default` | 描画にだけ必要な外観の単一値（コアの設定値には含まれない）。 `MarqueeAppearance` の設定時はモードから導出する表示可否を上書きし直す |
| `HasSelection` / `SelectionDomainRect` | `bool` / `EsRect` | `false` / 空矩形 | 選択矩形の有無と現在値（ドメイン座標） |
| `HighlightDomainRect` | `EsRect?` | `null` | ハイライト矩形（ドメイン座標。 **null はハイライトなし**。 選択矩形より優先） |
| `ShowFullOverlay` | `bool`（取得のみ） | `true` | 全面暗転の有無（モードから導出） |
| `LoupeCenterPoint` | `EsPoint?`（取得のみ） | `null` | ルーペ中心（ドメイン座標） |
| `DipToDomainScaling` / `DisplayScaleScaling` / `LoupeDipToDeviceScaling` | `double` | 各 `1.0` 等 | DPI スケーリング（設定には含めない環境値） |
| `SizeLabelOverrideProvider` / `OperationTextProvider` / `InfoTextProvider` | `Func<string?>?` | `null` | ラベル文言を描画時に評価するデリゲート（null または空文字は描画しない。 文言はホストが与える） |
| `Log` | `ILogger` | `NullLogger` | 操作ログの出力先（内包する選択コントロールとルーペへ伝搬する） |

`Options`（コアの `FadeMaskDragRectOptions`）の主な項目は、`Marquee`（`MarqueeOptions`。 寸法・表示可否。 色は含まない）/ `Loupe`（`LoupeOptions`）/ `SelectionRectColorA` / `SelectionRectColorB`（ルーペ画像上の選択矩形破線の 2 色）/ `LoupeOffset`（中心からの右下オフセット。 DIP）です。

### 6.3 メソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordTransform(ICoordTransform?)` | 座標変換ポートを一括設定する（選択矩形・暗転マスク・ルーペの配置で共用。 null は倍率からコアが解決する） |
| `SetSelection(EsRect)` / `ClearSelection()` | 選択状態の外部設定・クリア |
| `SetLoupeSource(EsBitmap?)` / `SetLoupePosition(EsPoint)` / `ClearLoupe()` | ルーペの制御 |
| `RequestConfirm()` / `RequestCancel()` | 確定要求・キャンセル要求（Enter / Escape 相当） |
| `FocusMarquee()` | 選択矩形へフォーカスを設定する（矢印キー nudge の受付開始） |
| `RefreshOverlay()` | 表示（パン・ズーム・表示サイズ）の変化へ追従する |
| `InvalidateOverlay()` | 暗転マスクの再描画を要求する |

### 6.4 イベント

| イベント | 引数 | 発火条件 |
| --- | --- | --- |
| `ConfirmRequested` | `FadeMaskDragRectSelectionEventArgs` | 矩形内ダブルクリック・Enter・`RequestConfirm`（選択確定後のみ） |
| `CancelRequested` | `EventArgs` | 矩形外ダブルクリック・Escape・`RequestCancel` |
| `LoupePositionMoved` | `FadeMaskDragRectPointEventArgs` | ルーペ中心の更新（ドメイン座標） |

座標はすべてドメイン座標です。

### 6.5 座標系と操作

| 座標系 | 意味 |
| --- | --- |
| ドメイン座標 | 選択対象の座標（画像ピクセル・仮想スクリーンピクセル等）。 選択矩形とルーペ中心はこの座標で扱う |
| ローカル DIP | コントロールのローカル座標。 入力と描画はこの座標で行う |

両者の変換は座標変換ポート（`ICoordTransform`）が担い、選択矩形・暗転マスク・ルーペの配置で共用します。

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ | 選択矩形の作成・リサイズ・移動（内包する選択コントロールが処理する） |
| 矢印キー | 選択矩形の nudge（内包コントロールへフォーカスがある場合のみ） |
| Enter / Escape | 確定・キャンセル要求を発火する |
| マウス移動 | ルーペ中心を更新し、表示位置を再計算する |

ホイール操作（ズーム・パン）と表示の変化の追従は本コントロールの配線ではありません（コントロール化を前提とした処理）。 ホイールはホストが `MouseWheel` を購読してビューポートへ転送し、表示の変化はホストが検知して `RefreshOverlay()` を呼びます。

### 6.6 エラー時の挙動

| 条件 | 動作 |
| --- | --- |
| `SetCoordTransform(null)` | 倍率からコアが座標変換を解決する（倍率 1.0 なら等価変換・境界なし） |
| `SetSelection` にサイズ 0 以下の矩形 | 選択なしとして扱う（例外は発生しない） |
| ルーペ画像が未設定の状態で `SetLoupePosition` | 中心のみ記録し、表示しない（例外は発生しない） |
| 未定義の `Mode` を設定 | `WindowSelect` と同じ扱いになる（例外は発生しない） |
