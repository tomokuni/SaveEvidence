# CaptureSessionControl.MewUI 外部機能仕様

MewUI 版の撮影セッションが提供する**公開 API** を記載します。内部実装（描画手順・キャッシュ等）は記載しません。

## 1. 公開型の構成

| 型 | 種別 | 役割 |
| --- | --- | --- |
| `CaptureSessionControl` | sealed クラス（Facade） | 撮影と切り出しの窓口。排他（同時に 1 つだけ）とセッションの寿命を単一所有する |
| `CaptureOverlayServices` | sealed record | 撮影が必要とするサービスの束（設定は含まない） |

名前空間は `EsUtil.SaveEvidence.CaptureSession.MewUI` です。

`CaptureOverlay`（撮影セッション）と `CropSession`（切り出しセッション）は `internal` です
（1 セッション = 1 インスタンスであり、生成・実行・破棄・排他の受付は Facade が担います）。

## 2. CaptureSessionControl

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| コンストラクタ | `CaptureSessionControl(CropViewControl cropView, ICaptureTarget cropTarget, Grid cropHost, CaptureSettings? initialSettings = null, ILogger? logger = null)` | Facade を生成する。切り出しの対象（`cropView`）とオーバーレイを重ねる領域（`cropHost`）はホストが用意する。`initialSettings` の null メンバーは既定値で埋める |
| `Capture` | `EsBitmap? Capture(Window owner, CaptureType type, CaptureOverlayServices services)` | オーバーレイを表示し、確定した領域を切り出して返す。キャンセル・取得失敗・退化した選択の場合は null。**実行中のセッションがある場合は何もせず null を返す** |
| `ToggleCrop` | `void ToggleCrop()` | 切り出しの開始・確定・キャンセルを切り替える。どの操作を行うかの判断はコアの `CropSessionActions.Resolve` が単一所有する（未開始なら開始 / 選択済みなら確定 / 未選択ならキャンセル）。**撮影中は開始しない** |
| `AppearanceSettings` | `CaptureSettings` | 現在の設定（getter は全メンバー non-null）。設定時は null メンバーのみ現在値を維持する部分更新。**同一値の再設定はスキップ**。検証・適用ログはコア（`CaptureSettingsMerge.Apply`）が単一所有する。実行中の切り出しセッションへは即時反映する |

### 2.0 排他の規則

撮影（`Capture`）と切り出し（`ToggleCrop`）は**同時に 1 つだけ**実行できます。  
この規則は `CaptureSessionControl.Core` の `CaptureSessionSlot` が単一所有し、本 Facade がその唯一の適用者です。  
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

- 右クリックでは確定しません。要素を検出できない位置のクリックでも確定しません（誤操作で撮影を取りこぼさないため）。
- **左クリックの確定は押下で要素を固定し、解放時に行います**（押下を伴わない解放・退化した検出では確定しない。固定と取り出しの規則はコアの `CaptureClickSelection` が単一所有する）。
- **範囲選択では十字カーソルを表示します**（選択スタイルの判定はコアの `CaptureTypeTraits.IsHighlighted` が単一所有し、ハイライト型では表示しない。Avalonia 版と同じ見え方にする）。
- **Enter（Return を含む）= 確定 / Escape = キャンセル**です（確定対象が無い状態の Enter は何もしません。キーの解釈はコアの `CaptureKeyActions` が単一所有する）。
- Escape または他ウィンドウへの切り替え（非アクティブ化）でキャンセルします。
- オーバーレイはタスクバー・Alt+Tab の一覧に表示しません（一時的な表示のため）。

### 2.3 座標系

| 座標 | 意味 |
| --- | --- |
| 画像ピクセル（ドメイン） | 取得画像のピクセル。**画像の左上が原点**（画像の左上は撮影範囲の原点 = 仮想スクリーンの原点に対応するため、画面物理座標とは原点の分だけ異なる。原点は負値になりうる） |
| 画面物理ピクセル | 検出結果・入力（マウス）の座標系 |

ドメイン座標とローカル DIP の変換は `ICoordTransform` を選択コントロールへ注入して行います（コントロールは座標系を意識しません）。  
確定時の切り出し矩形は**画面物理座標の入口**（`CaptureClipMath.ResolveCaptureRect`）で解決します。選択矩形は画像ローカル座標（画像の左上が原点）のため、**境界で `CaptureClipMath.ToScreenRect` により画面物理座標へ戻してから**渡します（１つの座標空間に揃えることで、原点が負値のマルチモニタでも切り出し位置・幅が正しくなる）。

確定要求（キー・クリック・アダプタ要求）は、すべて同じ入口（画面物理座標）で `CaptureLogMessage.LogConfirmRequested` に記録します（記録する矩形の座標空間を UI 間で揃える）。完了の領域（`CaptureResult.SourceBounds`）も契約どおり仮想画面座標（画面物理）で返します。

### 2.4 切り出し規則

- 範囲選択は**閉区間**（始点・終点のピクセルを含む）で切り出します。
- スクリーン選択・ウィンドウ選択は検出結果が全ピクセルを含むため、終点の 1 ピクセルを加算しません。
- 切り出し矩形は画像の範囲へクランプします（画像の右端・下端で透明な余白が付かないようにするため）。
- 切り出せる矩形が無い（退化した選択）場合はキャンセル扱い（null）になります。
- 計算は `CaptureSessionControl.Core` の `CaptureClipMath.ResolveCaptureRect` が単一所有します（種別の性質とクランプ・丸めを組み合わせた唯一の入口）。
- 最小サイズは選択操作（Marquee）が判定します（検出矩形は要素の大きさであり、小さい要素も切り出し対象とする）。

## 3. CaptureOverlayServices

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `Capture` | `ICaptureService` | 画面キャプチャ（仮想スクリーンの取得） |
| `Finder` | `IElementFinder` | 要素検出（カーソル下のモニター・ウィンドウ） |
| `Window` | `IWindowService` | ネイティブウィンドウ操作（キャプチャ除外・退避・物理ピクセルでの配置） |
| `Logger` | `ILogger?` | ロガー（null の場合は出力しない） |

**設定は含めません**: 設定は Facade（`AppearanceSettings`）が単一所有し、撮影と切り出しが同じ値を使います（ホストが設定の変換を 2 回呼ぶ・2 つの設定値が食い違う余地をなくす）。

`ICaptureService` / `IElementFinder` / `IWindowService` は `Platform.Core`（名前空間 `EsUtil.Platform.Core`）が提供します。生成は `PlatformFactory.CreateXxx()` が唯一の経路です。

## 4. 使用する設定項目

`CaptureSettings`（`CaptureSessionControl.Core`）のうち、本ライブラリが参照する項目です。

| 項目 | 用途 |
| --- | --- |
| `HideOwnerWindow` | 撮影中にオーナーウィンドウを画面外へ退避するか |
| `MinSelectionSize` | 選択矩形のドラッグ中の最小サイズ（`MarqueeOptions.MinSelectionSize` へ反映。切り出しの可否は選択操作そのものが判定する） |
| `BorderColor` / `BorderThickness` / `OverlayColor` | 選択枠と暗転の外観（太さと色は提示層の `MarqueeAppearance`（`OuterBorderThickness` / `OuterBorderColor` / `OverlayColor`）へ反映。枠線は選択矩形と共用） |
| `HandleColor` / `HandleSize` / `HandleHitRadius` | ドラッグハンドル（寸法は選択矩形の設定、色は提示層の `MarqueeAppearance.HandleColor` へ反映） |
| `MarkerColor` / `MarkerLength` | 微調整時の十字マーカー（寸法は選択矩形の設定、色は提示層の `MarqueeAppearance.MarkerColor` へ反映） |
| `ClampToDomainBounds` | 画像外の選択を禁止するか |
| （ホイール操作） | 選択オーバーレイ上のホイールをビューポート（ズーム・パン）へ転送する。ズーム率・パン量の決定は `CropViewControl` が単一所有する |
| `ShowCoordLabel` / `CoordLabelColor` / `CoordLabelBackgroundColor` / `CoordLabelFontSize` | カーソル位置の座標ラベル（表示可否・フォントサイズ・色は提示層の `MarqueeAppearance` へ反映） |
| `SizeLabelVisible` / `SizeLabelColor` / `TextBackgroundColor` / `SizeLabelFontSize` | サイズラベル（表示可否・フォントサイズ・色は提示層の `MarqueeAppearance` へ反映。**文言はホストが単一所有する**） |
| `InfoLabelVisible` / `InfoLabelColor` / `InfoLabelBackgroundColor` / `InfoLabelFontSize` | 情報ラベル（ウィンドウタイトル。`FadeMaskDragRectLabelOptions`（ホストが描く付加情報ラベルの外観）へ反映） |
| `ShowSubLines` / `SubLineFontSize` / `SubLineColor` / `SubLineBackgroundColor` | 操作ラベル（サブライン。`FadeMaskDragRectLabelOptions` へ反映） |
| `LoupeSize` / `LoupeZoomLevel` / `LoupeOffset` / `LoupeCrossColor` / `LoupeFrameColor` / `LoupeFrameThickness` | ルーペ（サイズ・倍率・オフセットは `LoupeOptions`、枠線の太さは `LoupeAppearance.BorderThickness`、色は外観 `LoupeAppearance` へ反映） |
| `LoupePasteboardColor` | ルーペの画像外（ペーストボード）の色（外観 `LoupeAppearance.PasteboardColor` へ反映） |
| `LoupeSelectionRectColorA` / `LoupeSelectionRectColorB` | ルーペ画像上の選択矩形破線（選択矩形破線の色（`FadeMaskDragRectOptions`）へ反映） |
| `LoupeShowCoordLabel` / `LoupeCoordLabelColor` / `LoupeCoordLabelBackgroundColor` / `LoupeCoordLabelFontSize` | ルーペの中心座標ラベル（表示可否・フォントサイズ・色は外観 `LoupeAppearance` へ反映） |

### 4.1 適用先

上表の項目は撮影（`CaptureOverlay`）と切り出し（`CropSession`）が同じマッピングで反映します。**計算に必要な寸法・対話パラメータ**はコアの `FadeMaskDragRectOptionsMapper` が単一所有し、**描画にだけ必要な外観（色・枠線の太さ・表示可否）**は UI 間で共有する `CaptureSessionControl.UIBase` の `CapturePaintMapper`（`MarqueeAppearance` / `LoupeAppearance` を組み立ててコントロールのプロパティへ設定する）が担います。
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

切り出しの適用先は `ICaptureTarget`（アプリ側が実装するポート）です。切り出し結果の適用（Undo 退避・画像差し替え）はアプリ側の責務です。
