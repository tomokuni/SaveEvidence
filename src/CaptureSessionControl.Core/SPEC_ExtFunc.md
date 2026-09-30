# CaptureSessionControl.Core 外部機能仕様

撮影・切り出しの共通規則が提供する**公開 API** を記載します。 内部実装（呼び出し手順・キャッシュ等）は記載しません。

## 1. 公開型の構成

| 型 | 種別 | 役割 |
| --- | --- | --- |
| `CropBoundsMath` | 静的クラス | 切り出し矩形の計算（純粋関数） |
| `CaptureClipMath` | 静的クラス | 選択矩形→切り出し矩形の解決（座標空間は画面物理）と、画像ローカル座標との相互変換 |
| `CaptureImageHolder` | クラス | 撮影した画像の保持と所有権（差し替え・破棄後の破棄・未取得時の範囲） |
| `CaptureVirtualScreen` | 静的クラス | 仮想スクリーンの撮影対象の範囲（全モニタの包含矩形）と、取得の待ち方（タイムアウトで打ち切り、未使用結果を破棄する） |
| `CaptureHighlight` | record struct | 検出結果を選択オーバーレイへ反映するための状態（ハイライト・矩形・表示名・変化） |
| `CaptureHighlightSession` | クラス | ハイライトの状態・判断・ログの単一所有（ドメイン変換・未検出の正規化・表示名の変化） |
| `CaptureKeyAction` | 列挙型 | オーバーレイのキー操作で実行する内容（何もしない / 確定 / キャンセル） |
| `CaptureKeyActions` | 静的クラス | 押下されたキーから実行すべき操作の決定（Enter・Escape） |
| `CaptureClickSelection` | クラス | クリック確定の固定（押下で固定・解放で取り出し） |
| `CropLabelTexts` | 静的クラス | 切り出しのサイズラベル書式の単一所有 |
| `CaptureLabelTexts` | 静的クラス | キャプチャ種別→動作モードの写像と、ラベル文言（操作ラベル・情報ラベル・サイズラベル）の単一所有 |
| `CaptureTypeTraits` | 静的クラス | キャプチャ種別の性質（ハイライト型・区間の流儀・検出対象）の単一所有 |
| `CaptureElementTarget` | 列挙型 | カーソル下の検出対象（画面 / ウィンドウ） |
| `MonitorScalingMath` | 静的クラス | 対象矩形の中心を含むモニタの実表示倍率の解決（純粋関数） |
| `ImageColorMath` | 静的クラス | 画像の平均色（標本間引き） |
| `CaptureClipMath` | 静的クラス | 仮想スクリーン画像からの切り出し（画像ローカル座標への変換） |
| `CaptureClipCache` | クラス | 事前切り出しキャッシュ（同一判定・所有権の受け渡し・旧値の破棄） |
| `CaptureTiming` | 静的クラス | 撮影セッションの方針値（キャプチャのタイムアウト・画面外退避座標） |
| `CropSessionAction` | 列挙型 | 切り出しの 1 操作で実行する内容（開始 / 確定 / キャンセル） |
| `CropSessionActions` | 静的クラス | 切り出しの 1 操作で実行する内容の決定 |
| `CaptureSessionState` | クラス | セッションの実行中・完了の排他（再入禁止・単一完了） |
| `CaptureSessionSlot{TCapture, TCrop}` | クラス | 撮影・切り出しで同時に 1 つだけ実行できる排他スロット |
| `CaptureCompletion{TResult}` | クラス | 結果の単一確定（`TaskCompletionSource` のラップ） |
| `CaptureOwnerScope` | クラス | 撮影中のオーナー退避とキャプチャ除外の手順（`IDisposable` で復帰） |
| `CaptureSettings` | record | 外観・動作設定（全メンバー nullable） |
| `CaptureSettingsMerge` | 静的クラス | 設定の部分更新マージと同値スキップ |
| `CaptureLogMessage` | 静的クラス | 共通ログ定義（撮影・切り出しの開始／確定／キャンセル） |
| `CaptureLogFormat` | 静的クラス | ログに載せる値の文字列化 |
| `CaptureSettingsLogMessage` | 静的クラス | 設定適用のログ定義 |
| `CaptureResult` | record | キャプチャ結果（画像と領域） |
| `ICaptureTarget` | インターフェイス | 切り出し結果の適用先（アプリ側が実装するポート） |
| `ICaptureOwnerWindow` | インターフェイス | 撮影時に退避するオーナーウィンドウ（各 UI が実装するポート） |
| `ImageColorMath` | 静的クラス | 画像の平均色（標本間引き）。 描画の背景色の決定に使う |

名前空間は `EsUtil.SaveEvidence.CaptureSession` です。

## 2. 層の責務の境界

本プロジェクトは「状態・判断・両 UI 共通の値と計算・コア⇔UI の契約・ログ定義」を担います。

| 区分 | 内容 |
| --- | --- |
| 状態 | セッションの実行中・完了（`CaptureSessionState`）、受付スロット（`CaptureSessionSlot`）、結果の確定（`CaptureCompletion`）、ハイライトとクリック確定（`CaptureHighlightSession` / `CaptureClickSelection`）、撮影画像（`CaptureImageHolder`） |
| 判断 | 切り出し範囲（`CropBoundsMath` / `CaptureClipMath`）、ハイライトの反映（`CaptureHighlightSession`）、キー操作（`CaptureKeyActions`）、クリック確定（`CaptureClickSelection`）、種別の性質（`CaptureTypeTraits`）、モニタ倍率（`MonitorScalingMath`）、設定のマージ（`CaptureSettingsMerge`）、退避と除外の順序（`CaptureOwnerScope`）、平均色（`ImageColorMath`）、切り出しと事前キャッシュ（`CaptureClipMath` / `CaptureClipCache`）、取得の打ち切り（`CaptureVirtualScreen`）、切り出しの操作内容（`CropSessionActions`）、方針値（`CaptureTiming`） |
| 契約 | `ICaptureTarget`（結果の適用先）、`ICaptureOwnerWindow`（退避対象）、`CaptureSessionSlot`（排他）、`CaptureCompletion`（単一確定）、`CaptureClipCache`（事前切り出しの受け渡し）、`CaptureImageHolder`（画像の保持）、`CaptureHighlight` / `CaptureHighlightSession`（ハイライト状態）、`CaptureResult.From`（結果の正規化） |
| ログ定義 | `CaptureLogMessage` / `CaptureLogFormat` / `CaptureSettingsLogMessage`（書式・レベル・完了時の出力の組を含む。 呼び出しは UI） |
| ログ定義 | `CaptureLogMessage` / `CaptureLogFormat` / `CaptureSettingsLogMessage`（書式とレベルのみ。 呼び出しは UI） |
| UI 層に残すもの | Window の生成と表示、`Screens` / DPI の取得、ポインタ・キーの購読、`Dispatcher`、カーソル、レイアウト、Bitmap のライフサイクル |

## 3. CaptureLabelTexts

キャプチャ固有のラベル文言とモード写像を単一所有します。 選択矩形の複合コントロールは汎用化により固有の文言を持たないため、ホスト（両 UI のセッション）が本クラスの値をプロバイダで与えます。

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ToSelectionMode` | `FadeMaskDragRectMode ToSelectionMode(CaptureType captureType)` | キャプチャ種別（全画面 / ウィンドウ / 範囲）→ 動作モード。 未定義の種別は範囲選択として扱う（切出しは対象外） |
| `GetOperationText` | `string GetOperationText(FadeMaskDragRectMode mode)` | 操作ラベル（確定・キャンセル方法）。 スクリーン選択・ウィンドウ選択は Enter 系、範囲選択・切出しはダブルクリック系（常に非空） |
| `GetInfoText` | `string? GetInfoText(FadeMaskDragRectMode mode, string? windowTitle)` | 情報ラベルの文言。 ウィンドウ選択のみウィンドウタイトルを返し、他モードは null（描画しない） |
| `ResolveSizeLabel` | `string? ResolveSizeLabel(EsRect? target, string? overrideText, Func<EsRect, double> displayScaling)` | サイズラベルの文言。 **確定対象があれば矩形から生成し、無ければホスト指定の文言**を返す（表示倍率のデリゲートは描画時のみ評価する） |

### 3.1 規則

| # | 規則 |
| --- | --- |
| 1 | モード写像は `CaptureType` の 3 種別を全て扱い、未定義値は範囲選択へフォールバックする |
| 2 | 操作ラベルの文言は 2 系統（Enter 系 / ダブルクリック系）に集約する |
| 3 | 情報ラベルはウィンドウ選択でのみ意味を持つため、他モードでは null を返す（表示可否の最終判定（設定）は描画側） |
| 4 | サイズラベルの文言は「確定対象があれば矩形から生成・無ければホスト指定」の 1 経路に集約する（両 UI が同じ三項演算を書かない） |

## 4. CropBoundsMath

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ComputeCropBounds` | `EsRectInt ComputeCropBounds(EsRect selection, EsSize imageSize, double minSelectionSize)` | 選択矩形から切り出し範囲を求める。 切り出せない場合は `IsEmpty` が true の矩形 |
| `RoundToPixel` | `int RoundToPixel(double value)` | 座標を四捨五入して整数へ直す（0.5 は切り上げ。 詳細は 7.17） |
| `ToPixelRect` | `EsRectInt ToPixelRect(EsRect selection, EsSize imageSize)` | 半開区間のまま画像内の整数矩形へ変換（クランプ・四捨五入） |
| `ToClosedInterval` | `EsRectInt ToClosedInterval(EsRectInt rect)` | 終点の 1 ピクセルを加える（`+1` のみ。 クランプはしない） |
| `ToClosedInterval` | `EsRectInt ToClosedInterval(EsRectInt rect, EsSize imageSize)` | 終点の 1 ピクセルを加える（画像の端でクランプ） |

### 4.1 規則

| # | 規則 |
| --- | --- |
| 1 | 画像サイズが 0 以下の場合は空を返す |
| 2 | 選択矩形が `minSelectionSize` 未満（画像内へクランプする前の値で判定）の場合は空を返す |
| 3 | 座標は四捨五入（0.5 は切り上げ）し、画像の範囲へクランプする |
| 4 | 幅・高さが 1 ピクセル未満の場合は空を返す |
| 5 | `selection.Interval` が `Closed` の場合は終点の 1 ピクセルを加える（画像の右端・下端でクランプ） |

### 4.2 使い分け

| 用途 | `selection.Interval` |
| --- | --- |
| 範囲選択（ドラッグ）の切り出し | `Closed`（始点・終点のピクセルを含む） |
| スクリーン選択・ウィンドウ選択の切り出し | `HalfOpen`（検出結果が全ピクセルを含むため加算しない） |

終点の 1 ピクセルを加えるかの判断は**矩形の** **`EsRectInterval`** が持つため、本クラスは受け取った値に従うだけで判定を持ちません（判定を各所へ散らさない）。 種別から矩形の流儀を導くのは `CaptureTypeTraits.ResolveInterval` です。

確定時の切り出し矩形は、本クラスと種別の性質を組み合わせた `CaptureClipMath.ResolveCaptureRect` が求めます（両 UI が同じ規則を使う唯一の入口。 種別ごとの分岐を UI に書かない）。

## 5. CaptureSettings

外観・動作設定を保持します。 **全メンバーが nullable** で、用途により null の意味が異なります。

| 用途 | null の意味 |
| --- | --- |
| 初期設定（`CaptureSessionControl` のコンストラクタ） | デフォルト値を適用する |
| 既存インスタンスへの適用（`AppearanceSettings` の setter） | 現在値を維持する（部分更新） |

主なメンバー（抜粋）:

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `HideOwnerWindow` | `bool?` | 撮影時にオーナーウィンドウを画面外へ退避するか |
| `MinSelectionSize` | `int?` | 選択矩形の最小サイズ（1 以上） |
| `BorderColor` / `BorderThickness` | `System.Drawing.Color?` / `double?` | 選択枠の色・太さ（DIP） |
| `OverlayColor` | `System.Drawing.Color?` | 暗転オーバーレイの色（不透明度はアルファに統合） |
| `LoupeSize` / `LoupeZoomLevel` / `LoupeOffset` | `int?` / `int?` / `double?` | ルーペのサイズ・倍率・オフセット |
| `LoupeDipToDeviceScaling` | `double?` | ルーペの描画倍率（0.1〜8.0） |
| `SizeLabelVisible` / `SizeLabelColor` / `SizeLabelFontSize` | `bool?` / `System.Drawing.Color?` / `double?` | サイズラベルの表示・色・サイズ |
| `InfoLabelVisible` / `ShowSubLines` / `SubLineFontSize` | `bool?` / `bool?` / `double?` | 情報ラベル・サブライン |
| `SizeLabelOverride` | `string?` | サイズラベルの文言明示指定（null = 自動生成） |
| `DisplayScaleScaling` | `double?` | サイズラベル表示用のスケーリング（0 以下は `DomainToDipScaling` と同値） |

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `Validate` | `void Validate()` | 設定値を検証する。 範囲外の場合は `ArgumentOutOfRangeException`（null は検証対象外） |
| `ToNonNull` | `CaptureSettings ToNonNull()` | null をデフォルト値で埋めた non-null 版を返す |

**色は `System.Drawing.Color`** です（UI 非依存のため）。 `Validate` が検証する範囲は `MinSelectionSize` / `HandleSize` / `HandleHitRadius` / `MarkerLength` / `LoupeSize` / `LoupeZoomLevel` / `LoupeFrameThickness`（1 以上）、`BorderThickness` / `LoupeOffset`（0 以上）、`LoupeDipToDeviceScaling`（0.1〜8.0）です。

## 6. CaptureResult

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `Image` | `EsBitmap?` | キャプチャした画像。 キャンセル時は null |
| `SourceBounds` | `EsRectInt?` | キャプチャした領域（仮想スクリーン座標）。 キャンセル時は 0x0 |

`Image` と `SourceBounds` は常に同時に null または同時に非 null になります。 `SourceBounds` は複数モニターを包含する座標空間のため、プライマリモニターが (0, 0) にない場合は負値になり得ます。

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `From` | `CaptureResult From(EsBitmap? image, EsRectInt? bounds)` | 切り出し結果と領域から結果を生成する。 **キャンセル（画像または領域が無い）は画像 null・領域 0x0 の空結果へ正規化する**（UI が null 判定を書かない） |

## 7. 補助 API

### 7.1 CaptureTypeTraits

キャプチャ種別の性質を単一所有します（種別ごとの分岐を各 UI に書かないための写像）。

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `IsHighlighted` | `bool IsHighlighted(CaptureType captureType)` | 選択矩形を検出対象のハイライトとして描く種別（スクリーン選択・ウィンドウ選択）かどうか。 **選択スタイル（ハイライト型 / ドラッグ型）の分区を単一所有する**ため、UI は十字カーソルの要否・ルーペの利用・確定操作の配線を本結果から導く |
| `ResolveInterval` | `EsRectInterval ResolveInterval(CaptureType captureType)` | 切り出しで終点の 1 ピクセルを加える種別（範囲選択のみ `Closed`）かどうかを区間の流儀で返す |
| `GetElementTarget` | `CaptureElementTarget GetElementTarget(CaptureType captureType)` | カーソル下から検出する対象（スクリーン選択は画面、他はウィンドウ） |
| `FindUnderCursor` | `ElementFinderResults FindUnderCursor(IElementFinder? finder, CaptureType captureType, EsPointInt screenPoint)` | カーソル下の要素を検出する。 **検出関数の呼び分けを単一所有する**（`finder` が null の場合は未検出）。 ホバー・押下の各経路で同じ分岐を書かない |
| `ResolveRect` | `EsRect? ResolveRect(CaptureType captureType, EsRect? highlightRect, EsRect? selectionRect)` | 確定対象の矩形を解決する（ハイライト型はハイライト矩形、それ以外は選択矩形）。 UI は未検出・未選択を null へ正規化して渡す |

### 7.2 MonitorScalingMath

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ResolveForRect` | `double ResolveForRect(EsRectInt target, IReadOnlyList<(EsRectInt Bounds, double Scale)> monitors, double fallback)` | 対象矩形の**中心**を含むモニタの表示倍率を返す。 どのモニタにも含まれない場合は `fallback` |
| `ResolveForDomainRect` | `double ResolveForDomainRect(EsRect domainRect, EsPointInt origin, IReadOnlyList<(EsRectInt Bounds, double Scale)> monitors, double fallback)` | ドメイン座標（画像ピクセル）の矩形を画面物理座標へ戻してから中心を含むモニタの表示倍率を返す。 **原点の加算**も単一所有する（各 UI が書くと判定点が 1 ピクセルずれる） |
| `Collect` | `IReadOnlyList<(EsRectInt Bounds, double Scale)> Collect(IEnumerable<(EsRectInt Bounds, double Scale)> monitors)` | 判定に使うモニタ一覧を整える（**表示倍率が未確定（0 以下）のモニタを除外する**。 入力の順序は保つ） |
| `ResolveScaling` | `double ResolveScaling(IEnumerable<double> scalings)` | 画面のスケーリング一覧から**先頭の値**を解決する（未確定は基準値へ正規化。 画面が 1 枚も無い場合は基準値） |

対象矩形の判定は中心 1 点で行います（左上で判定すると、モニタをまたぐ矩形で主モニタと異なる倍率を拾います）。 倍率未確定の除外を UI ごとに書くと、同じ環境で判定結果が変わります。

### 7.3 CropLabelTexts

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `FormatCropSizeLabel` | `string FormatCropSizeLabel(EsRect selectionDomainRect, double zoomPercent)` | 切り出しのサイズラベル（`LT` / `RB` / `WH` / `Zoom`）。 `WH` は含まれるピクセル数であり、終点を含めるかの判断は矩形の `EsRectInterval` が持つ（`Closed` なら右端−左端＋1） |
| `ResolveSizeLabel` | `string? ResolveSizeLabel(bool hasSelection, EsRect selectionDomainRect, double zoomPercent)` | 選択がある場合はラベル、**未選択の場合は null**（描画しない）を返す |

### 7.4 ImageColorMath

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `AverageColor` | `DrawingColor AverageColor(EsBitmap image)` | 画像全体の平均色。 画素を間引いて標本化する。 画像が null の場合は `ArgumentNullException` |

### 7.5 CaptureSettingsMerge

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `Merge` | `CaptureSettings Merge(CaptureSettings? input, CaptureSettings? current = null)` | 部分更新をマージする。 `current` が null の場合は入力をデフォルトで補完した non-null 版を返し、`input` が null の場合は現在値（無ければデフォルト）をそのまま返す |
| `Apply` | `bool Apply(CaptureSettings input, ref CaptureSettings current, ILogger? logger = null)` | 検証 → マージ → 同値ならスキップ。 適用した場合のみ true を返し、ログを出す |

### 7.6 CaptureSessionState / CaptureSessionSlot

| 型 | メンバー | 内容 |
| --- | --- | --- |
| `CaptureSessionState` | `IsRunning` / `IsCompleted` | 1 セッション内の実行中・完了済みの状態 |
| `CaptureSessionState` | `TryBegin()` / `TryComplete()` / `End()` | 実行の開始（再入禁止）・単一完了・終了（状態をリセット） |
| `CaptureSessionSlot{TCapture, TCrop}` | `HasCapture` / `HasCrop` / `IsBusy` / `Crop` | 受付中のセッションの有無 |
| `CaptureSessionSlot{TCapture, TCrop}` | `TryBeginCapture` / `EndCapture` / `TryBeginCrop` / `EndCrop` | 撮影・切り出しの開始受付と終了。 **同時に実行できるのはどちらか 1 つ** |
| `CaptureSessionSlot{TCapture, TCrop}` | `HasCropSelection(Func<TCrop, bool>)` / `CancelAll(Action<TCapture>?, Action<TCrop>?)` | 切り出しの選択有無の委譲・両セッションの一括キャンセル |

`CaptureSessionState` は 1 セッション内の進行状態、`CaptureSessionSlot` は「どのセッションが実行中か」を持ちます（責務が重なりません）。

### 7.7 CaptureOwnerScope

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `Enter` | `CaptureOwnerScope Enter(IWindowService windowService, EsPlatformHandle handle, ICaptureOwnerWindow owner, bool hideOwner)` | 撮影中のオーナー退避とキャプチャ除外を開始する。 **キャプチャ除外を先に、退避を後に**行う順序を単一所有する |
| `Dispose` | `void Dispose()` | 配置を復帰し、キャプチャ除外を解除する。 `hideOwner` が false の場合も同じスコープを返す |

撮影が例外で終わっても復帰するため、必ず `using` で使います。

### 7.8 CaptureLogMessage / CaptureLogFormat / CaptureSettingsLogMessage

ログの定義（イベント ID・本文書式・完了時の出力の組）を単一所有します。 カテゴリは各 UI が与えます。

| メソッド | 内容 |
| --- | --- |
| `LogCaptureStart(string captureType)` | 撮影の開始 |
| `LogCaptureFinished(CaptureResult? result)` | 撮影セッションの終了。 確定は `CaptureCompleted` → `Result` の 2 件（順序を含めて単一所有）、キャンセル（`result` が null または画像が null）は `CaptureCancelled` の 1 件 |
| `LogConfirmRequested(string rect)` | 選択の確定要求（Enter・ダブルクリック等。 診断用） |
| `LogCancelRequested()` | 選択のキャンセル要求（Esc・非アクティブ化等。 診断用） |
| `LogVirtualScreen(EsPointInt origin, EsBitmap? bitmap)` | 仮想スクリーンの原点とサイズ（診断用）。 **画像未取得はサイズ 0** へ正規化する |
| `LogHighlight(string captureType, bool detected, string rect, string title, string className)` | ハイライトの検出結果（Trace。 診断用。 `CaptureHighlightSession` が呼ぶ） |
| `LogCropStart(EsBitmap image)` | 切り出しの開始（元画像サイズ） |
| `LogCropFinished(EsRectInt bounds)` | 切り出しセッションの終了。 **退化した選択（空の矩形）はキャンセルとして記録する**（UI が空判定とログの呼び分けを書かない） |
| `LogCropCancelled()` | 切り出しのキャンセル（Esc・非アクティブ化・ウィンドウクローズ等） |
| `LogSettingApplied()` | 設定の適用（変化時の 1 回のみ。 同値の適用はログを出さない） |

`CropCompleted` / `CaptureCompleted` / `CaptureCancelled` / `Result` は `LogCropFinished` / `LogCaptureFinished` の内部実装です（UI からは終了 1 回で呼びます）。

| メソッド | 内容 |
| --- | --- |
| `CaptureLogFormat.Rect(EsRectInt)` / `CaptureLogFormat.Rect(EsRect)` | 矩形を `"X,Y,Width,Height"` の 1 行へ整形する |
| `CaptureLogFormat.RectOrEmpty(EsRectInt?)` | 未検出（null）を空文字へ正規化する |
| `CaptureLogFormat.Text(string?)` | 未取得（null）の文字列を空文字へ正規化する（タイトル・クラス名） |

### 7.9 CaptureClipMath

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ResolveCaptureRect` | `EsRectInt ResolveCaptureRect(CaptureType captureType, EsRect screenRect, EsRectInt imageBounds)` | 選択矩形（画面物理座標）から切り出し矩形（画面物理座標）を求める。 **確定時の切り出し矩形を決める唯一の入口**（種別が決める区間の流儀に従った終点の加算・画像内へのクランプ・端数の丸めを組み合わせる）。 切り出せない場合は `IsEmpty` |
| `ResolveCaptureRect`（整数版） | `EsRectInt ResolveCaptureRect(CaptureType captureType, EsRectInt screenRect, EsRectInt imageBounds)` | クリック確定・ホバーの検出矩形（整数）向けの同じ規則の入口（UI が整数矩形を浮動小数矩形へ広げないため） |
| `ImageSize` | `EsSize ImageSize(EsBitmap image)` | 画像のピクセルサイズ（コアの値型）。 コアの型同士の変換を UI に書かせない |
| `ImageBounds` | `EsRectInt ImageBounds(EsBitmap image, EsPointInt origin)` | 画像の占有範囲（原点とサイズ。 切り出しのクランプ境界になる） |
| `ValidateImage` | `void ValidateImage(EsBitmap image, string paramName)` | 画像が切り出せる（サイズが正）ことを検証する。 **幅・高さが 0 以下の場合は `ArgumentException`** |
| `ToLocalRect` | `EsRectInt ToLocalRect(EsRectInt captureRect, EsPointInt origin)` / `EsRect ToLocalRect(EsRect captureRect, EsPointInt origin)` | 画面物理座標の矩形を画像ローカル座標（画面物理 − 原点）へ変換する |
| `ToScreenRect` | `EsRect ToScreenRect(EsRect localRect, EsPointInt origin)` / `EsRectInt ToScreenRect(EsRectInt localRect, EsPointInt origin)` | 画像ローカル座標の矩形を画面物理座標へ変換する（`ToLocalRect` の逆変換。 双方向の対称性を保つ） |
| `Extract` | `EsBitmap? Extract(EsBitmap? virtualScreen, EsPointInt origin, EsRectInt captureRect)` | 仮想スクリーン画像から切り出し矩形の領域を取り出す。 **未取得・破棄済みの場合は null**（呼び出し側が領域指定のキャプチャへフォールバックする。 破棄は例外にしない） |

`ResolveCaptureRect` は最小サイズを判定しません（選択操作そのものの可否は `MarqueeOptions.MinSelectionSize` が持ち、検出矩形は小さい要素も切り出し対象にする）。

**座標空間の契約**: 切り出し矩形の求解は**画面物理座標（仮想スクリーン座標）に統一**します。 選択矩形と占有範囲をともに画面物理座標で渡すため、座標空間の取り違えが構造的に発生しません（原点が負値のマルチモニタでも切り出し位置・幅が正しくなる）。 UI の選択矩形が画像ローカル座標（画像の左上が原点）の場合は、**境界で `ToScreenRect` により画面物理座標へ戻してから**渡します。

結果の矩形も画面物理座標になるため、`CaptureResult.SourceBounds`（契約: 仮想画面座標）やそのままログの矩形として使えます。

### 7.10 CaptureImageHolder

撮影した画像（仮想スクリーン）の保持と所有権を単一所有します。 背景撮影の完了を待たずに表示を始めるため、画像の寿命がウィンドウの寿命と一致しないことへの対処です。

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `Set` | `bool Set(EsBitmap? image, EsPointInt origin)` | 画像と原点を保持する（**旧い画像を破棄する**）。 **破棄済みの場合は受け付けず、渡された画像をその場で破棄して false** を返す |
| `Image` / `Origin` | `EsBitmap?` / `EsPointInt` | 保持している画像と原点（未取得は null・既定値） |
| `Bounds` | `EsRectInt` | 画像の占有範囲。 **未取得の場合はコンストラクタで指定したフォールバック範囲**（背景撮影完了前でも切り出し範囲をクランプできる） |
| `Dispose` | `void Dispose()` | 保持している画像を破棄する（複数回呼んでも安全。 以降の `Set` は渡された画像をその場で破棄する） |

### 7.11 CaptureVirtualScreen

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ResolveBounds` | `EsRectInt? ResolveBounds(IEnumerable<EsRectInt>? screenBounds)` | 撮影対象の範囲（全モニタの包含矩形）を求める。 **モニタが無い場合は null**（撮影する領域が無い） |
| `AwaitAsync` | `Task<EsBitmap?> AwaitAsync(Task<EsBitmap?> captureTask, TimeSpan? timeout = null)` | 仮想スクリーンの取得をタイムアウト付きで待つ。 **間に合わない場合は null を返し、遅れて完了した結果を破棄する**（既定の待ち時間は `CaptureTiming.ScreenCaptureTimeout`） |

### 7.12 CaptureClipCache

「確定前に切り出しておき、確定時は同じ矩形なら再利用する」事前切り出しの状態と判断を単一所有します。

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `HasCached` | `bool HasCached(EsRectInt captureRect)` | 同じ矩形の画像を保持しているか（再切り出しのスキップ判断） |
| `Take` | `EsBitmap? Take(EsRectInt captureRect)` | 一致する画像を**所有権ごと**取り出す（以降の `Dispose` で破棄されない）。 一致しない場合は null |
| `Store` | `void Store(EsRectInt captureRect, EsBitmap? bitmap)` | 切り出した画像を格納する。 **旧いキャッシュを破棄してから**置き換える。 破棄済みの場合は渡された画像をその場で破棄する |
| `HasBitmap` / `StoreCount` | `bool` / `int` | 保持の有無・実際に格納した回数（スキップ判断の検証用） |
| `Dispose` | `void Dispose()` | 保持中の画像を破棄する（複数回呼んでも安全） |

`Store` は「新しい画像を入れてから旧値を破棄する」順序を守るため、同一インスタンスの取り違えが起きません。

### 7.13 CaptureHighlight / CaptureHighlightSession

カーソル下の要素の検出結果から「ハイライトの有無・ハイライト矩形（ドメイン座標）・表示名」を導出する手順を単一所有します（両 UI が同じ手順・同じログになることの根拠）。

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `Update` | `CaptureHighlight Update(CaptureType captureType, ElementFinderResults detection, EsPointInt imageOrigin)` | 検出結果を反映する（ドメイン座標への変換・未検出の正規化・表示名の変化の判定・診断ログの記録） |
| `DomainRect` | `EsRect?` | 直前に検出したハイライト矩形（ドメイン座標 = 画像ローカル物理ピクセル）。 未検出時は null（確定対象の有無の判定も兼ねる） |
| `Title` | `string` | 直前に検出した要素の表示名（未検出時は空文字）。 情報ラベルの文言プロバイダが参照する |

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `DomainRect` | `EsRect?` | ハイライト矩形（ドメイン座標 = 画像ローカル物理ピクセル）。 未検出時は null（**null が「ハイライトなし」を意味する**） |
| `TitleChanged` | `bool` | 直前の検出と比べて表示名が変わったかどうか（情報ラベルの再描画判断） |

UI 層は戻り値を選択コントロールへ設定するだけにし、座標変換・空文字への正規化・変化の判定・診断ログを書きません（直前の状態はセッションのプロパティから読みます）。

### 7.14 CaptureKeyActions / CaptureClickSelection

オーバーレイのキー操作と、クリック確定（押下で固定・解放で確定）の状態を単一所有します。

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `Resolve` | `CaptureKeyAction Resolve(EsKey key, bool hasConfirmTarget)` | 実行すべき操作を返す。 Enter（Return を含む）→ `Confirm`（確認対象が無い場合は `None`）、Escape → `Cancel`、その他 → `None` |

`CaptureKeyAction` は `None` / `Confirm` / `Cancel` の 3 値です（`None` が既定値）。

| メンバー | シグネチャ | 内容 |
| --- | --- | --- |
| `Fix` | `bool Fix(EsRectInt target)` | 押下時に確定対象を固定する。 退化した矩形（幅・高さが 0 以下）は固定せず、前回の固定も破棄する |
| `Take` | `EsRectInt? Take()` | 解放時に固定した矩形を取り出す（1 回限り）。 固定が無い場合は null（確定しない） |

### 7.15 CropSessionActions

1 つのボタン（キー）で「開始・確定・キャンセル」を切り替える判断を単一所有します。

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `Resolve` | `CropSessionAction Resolve(bool isRunning, bool hasSelection)` | 実行すべき操作を返す（未実行→`Start` / 実行中かつ選択あり→`Confirm` / 実行中かつ選択なし→`Cancel`） |

`CropSessionAction` は `Start` / `Confirm` / `Cancel` の 3 値です。 UI 層は返された操作の効果を適用するだけにします。

### 7.16 CaptureTiming

UI が発明してはいけない運用方針値を単一所有します。

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `ScreenCaptureTimeout` | `TimeSpan` | 仮想スクリーン全体の取得のタイムアウト（既定 5 秒）。 DWM 応答不能時に取得を待ち続けないための上限 |
| `OwnerOffScreenCoord` | `int` | オーナーウィンドウを退避させる画面外の座標（`-32000`）。 ヒットテスト・要素検出の対象外になる |

### 7.17 CropBoundsMath.RoundToPixel

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `RoundToPixel` | `int RoundToPixel(double value)` | 座標を四捨五入して整数へ直す（0.5 は切り上げ = `MidpointRounding.AwayFromZero`） |

同じ丸めを UI が `(int)` 切り捨てや既定の `Math.Round` で書くと、同じ座標が UI ごとに 1 ピクセルずれるため公開します。

### 7.18 FadeMaskDragRectOptionsMapper

| メソッド | シグネチャ | 内容 |
| --- | --- | --- |
| `ToFadeMaskDragRectOptions` | `FadeMaskDragRectOptions ToFadeMaskDragRectOptions(this CaptureSettings settings)` | 設定（全メンバー non-null）から複合コントロールの設定（**寸法・表示可否**）を組み立てる。 **色・枠線の太さ・表示可否は写像しない**（描画にだけ必要な設定のため、UI 層のホストが提示層の `MarqueeAppearance` / `LoupeAppearance` を組み立てる） |
