# app 外部機能仕様書

## 目次

- [app 外部機能仕様書](#app-外部機能仕様書)
  - [目次](#目次)
  - [1. 文書概要](#1-文書概要)
  - [2. 目的](#2-目的)
  - [3. ズーム遷移ルール](#3-ズーム遷移ルール)
    - [3.1 フライアウトの選択肢（13 段階）](#31-フライアウトの選択肢13-段階)
    - [3.2 ⊖⊕ ボタンの段階（23 段階）](#32--ボタンの段階23-段階)
    - [3.3 Ctrl+ホイールの遷移（×1.1 ／ ÷1.1）](#33-ctrlホイールの遷移11--11)
    - [3.4 Ctrl+キーの遷移（Ctrl + + ／ Ctrl + -）](#34-ctrlキーの遷移ctrl----ctrl---)
  - [4. ズーム表示方式（ZoomableView）](#4-ズーム表示方式zoomableview)
    - [4.1 座標系](#41-座標系)
    - [4.2 ズーム中心維持](#42-ズーム中心維持)
    - [4.3 パンとスクロールバー](#43-パンとスクロールバー)
  - [5. Fit 表示](#5-fit-表示)
    - [5.1 Fit 実効倍率の計算](#51-fit-実効倍率の計算)
    - [5.2 Fit 中の遷移](#52-fit-中の遷移)
  - [6. ステータスバーの拡大率表示](#6-ステータスバーの拡大率表示)
  - [7. 画像内判定・座標表示](#7-画像内判定座標表示)
  - [8. 公開 API](#8-公開-api)
    - [8.1 ZoomManager](#81-zoommanager)
    - [8.2 ZoomableView](#82-zoomableview)
    - [8.3 ZoomMath / ZoomTransition / ScrollMetrics（純粋関数）](#83-zoommath--zoomtransition--scrollmetrics純粋関数)
  - [9. ログ](#9-ログ)
    - [9.1 LogLevel 設定ポリシー](#91-loglevel-設定ポリシー)
    - [9.2 ログ定義一覧](#92-ログ定義一覧)
  - [10. 制約事項](#10-制約事項)
  - [11. テーマ（外観設定）](#11-テーマ外観設定)
    - [11.1 テーマ対応コンポーネント](#111-テーマ対応コンポーネント)
  - [12. フォルダビュー](#12-フォルダビュー)
    - [12.1 表示モード・ソート順の保存と復元](#121-表示モードソート順の保存と復元)
    - [12.2 一覧表示](#122-一覧表示)
    - [12.3 Undo 一覧](#123-undo-一覧)
    - [12.4 ラベル設定（キャプチャ・切出し時の左上ラベル）](#124-ラベル設定キャプチャ切出し時の左上ラベル)
    - [12.5 ルーペ表示モード](#125-ルーペ表示モード)
    - [12.6 キーボードショートカット（Ctrl キー）](#126-キーボードショートカットctrl-キー)
  - [13. ファイル名テンプレートのバリデーション](#13-ファイル名テンプレートのバリデーション)
    - [13.1 プレースホルダ](#131-プレースホルダ)
  - [14. 保存先フォルダのプレースホルダ](#14-保存先フォルダのプレースホルダ)
    - [14.1 ToolTip](#141-tooltip)
    - [14.2 パス存在バリデーション](#142-パス存在バリデーション)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `ZoomManager` / `ZoomableView` / `CapturePage`（ズーム関連） |
| プロジェクト | `app/app.csproj` |
| 名前空間 | `app.Controls` / `app.Views.Components` / `app.Views.Pages` |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主な用途 | 画像プレビューのズームイン・アウト・Fit 表示・ズーム中心維持・パン・スクロールバー・ステータスバー連動 |

## 2. 目的

画像プレビューのズーム操作（⊖⊕ ボタン・Ctrl+ホイール・Ctrl+キー・フライアウト・スライダ）を統一的に管理し、以下の機能を提供します。

- ズーム率の段階遷移（⊖⊕ は 23 段階、Ctrl+ホイールは ×1.1 連続遷移）
- ズーム中心維持（マウスポインタ位置 / ビューポート中心）
- パン（マウスドラッグ・ホイール・スクロールバー）
- Fit 表示と実効倍率の計算・表示
- ステータスバーの拡大率表示（Fit 中も % 表示、「自動」表記なし）

## 3. ズーム遷移ルール

### 3.1 フライアウトの選択肢（13 段階）

拡大率ボタンをクリックしたときの Flyout の選択肢です（`MainViewModel.ZoomLevels`）。

```text
自動(フィット) → 10% → 15% → 25% → 35% → 50% → 75% → 100%
→ 150% → 200% → 250% → 350% → 500%
```

（0 = 自動、全 13 段階）

### 3.2 ⊖⊕ ボタンの段階（23 段階）

⊖⊕ ボタンは以下の 23 段階を順に遷移します（`ZoomTransition.ButtonZoomLevels`）。Fit は遷移に含めません。

```text
10 → 12 → 15 → 20 → 25 → 33 → 40 → 50 → 62 → 75 → 80 → 90
→ 100 → 110 → 125 → 150 → 200 → 250 → 300 → 350 → 400 → 450 → 500
```

- 現在値がリスト内 → 隣の段階へ移動
- 現在値がリスト外（スライダの中間値） → 次に大きい／小さい段階へ移動
- リスト先頭（10%）で ⊖ 停止、リスト末尾（500%）で ⊕ 停止
- Fit 表示中 → 実効倍率より大きい／小さい最初／最後の段階へ移動

### 3.3 Ctrl+ホイールの遷移（×1.1 ／ ÷1.1）

Ctrl+ホイールは現在の拡大率を基準に連続遷移します（`ZoomManager.ZoomInByWheel` / `ZoomOutByWheel`）。

- 拡大: `Round(現在の拡大率 × 1.1)`
- 縮小: `Round(現在の拡大率 ÷ 1.1)`
- 結果は 10〜500% にクランプされる
- Fit 表示中 → 実効倍率を基準に ×1.1 ／ ÷1.1 する（500% 以上の Fit は拡大不可、10% 以下の Fit は縮小不可）
- Fit は遷移に含めない（⊖⊕・ホイールでは Fit 表示を考慮しない）

### 3.4 Ctrl+キーの遷移（Ctrl + + ／ Ctrl + -）

`CapturePage.OnGlobalKeyDown`（Ctrl ショートカットの一括処理）で Ctrl+（OemPlus/Add）・Ctrl+-（OemMinus/Subtract）を捕捉し、⊖⊕ ボタンと同じ 23 段階遷移（`ZoomInByButton` / `ZoomOutByButton`）を実行します。詳細は「13.5 キーボードショートカット（Ctrl キー）」を参照。

## 4. ズーム表示方式（ZoomableView）

`ZoomableView`（`app.Controls`）は **ScrollViewer を使用しない**自己完結型ビューポートです。ズーム・パンを RenderTransform（ScaleTransform + TranslateTransform）で実現し、ScrollViewer のレイアウト処理によるオフセット上書きが**原理的に発生しない**ため、ズーム中心維持の防御コード（保留オフセット再設定・収束ループ・Dispatcher.Post 保険）が不要です。

### 4.1 座標系

```text
- コンテンツ座標 = 元画像ピクセル（ズーム率に依存しない不変座標）
- ビューポート座標 = ZoomableView のローカル座標（表示座標）
- 描画位置 = 実効スケール × (コンテンツ座標 − オフセット)
- オフセット = 「ビューポート左上に対応するコンテンツ座標」。スクロール範囲
  （コンテンツ − ビューポート / 実効スケール）にクランプされる
```

- `ViewportToContent(Point)` / `ContentToViewport(Point)` で双方向変換
- 実効スケール（`EffectiveScale`）: 固定倍率中は `ズーム率 / 100`、Fit 中は `FitScale`（ビューポートに収まる Uniform スケール）。Fit 中の座標変換でゼロ除算が発生しない

### 4.2 ズーム中心維持

- **画像内にマウスポインタがある場合**: マウスポインタ位置を中心に拡大縮小
- **画像外（ビューポートの空き領域）**: ビューポート中心を基準に拡大縮小
- `ZoomRequested` イベント（`ZoomRequestedEventArgs`: `ZoomIn` + `PointerPosition`）でマウス位置を通知
- ズーム中心の計算は `ZoomMath.ComputeOffsetAfterScaleChange`（純粋関数）:

  ```text
  コンテンツ座標 c = offset + center / oldScale（不変に保つ）
  offset' = c − center / newScale（スクロール範囲にクランプ）
  ```

### 4.3 パンとスクロールバー

- **マウスドラッグ（grab 方式）**: 左ボタン押下 + 移動でパン（ポインタキャプチャ付き）
  - 押下時に掴んだ位置の**コンテンツ座標（元画像ピクセル）**を保存し、その位置がポインタに追従するようオフセットを再計算する（`オフセット = 掴んだコンテンツ座標 − ポインタ位置 ÷ 実効スケール`）
  - **ドラッグ方向と同じ方向へコンテンツが移動**する（右下へドラッグ → コンテンツも右下へ。直観一致）
  - スクロール範囲端ではクランプで停止し、方向を戻すと再び追従する
  - Fit 中はパン不可（画像がビューポートに収まるため。オフセット 0）
- **ホイール**: 縦パン / Shift+ホイールで横パン（Ctrl+ホイールはズーム要求）
- **スクロールバー**: 縦横の `ScrollBar`（オーバーレイ表示）。コンテンツがビューポートより大きい場合のみ表示
  - 範囲（Maximum / ViewportSize）・サムサイズは `ScrollMetrics`（純粋関数）で計算
  - スクロールバーのドラッグ・矢印・ページ送りでパン（`ValueChanged` → オフセット更新）
  - ズーム・パン時にスクロールバーの値と範囲を同期

## 5. Fit 表示

### 5.1 Fit 実効倍率の計算

Fit 実効倍率は現在の表示状態（Bounds・ZoomPercent）に依存せず、**ビューポートと元画像サイズから一定に計算**します（`ZoomMath.FitZoomPercent`）。

```text
scaleX = ビューポート幅 ÷ 元画像幅
scaleY = ビューポート高さ ÷ 元画像高さ
実効倍率 = min(scaleX, scaleY) × 100   ※Uniform（アスペクト比維持）
```

### 5.2 Fit 中の遷移

- ⊖⊕ / Ctrl+キー: 実効倍率を基準に 23 段階リストを移動
- Ctrl+ホイール: 実効倍率 ×1.1 ／ ÷1.1 へ移動

## 6. ステータスバーの拡大率表示

- 現在の拡大率を % で表示する（例: `100%`）。「自動」の表記は行わない
- Fit 表示中は実効倍率の % を表示する
- ビューポートリサイズ時（Fit 実効倍率が変わる）も表示を自動更新する（`ZoomManager.OnZoomableViewSizeChanged`）
- 表示値は `MainViewModel.ZoomDisplayText` プロパティに設定し、View はバインディングで表示する

## 7. 画像内判定・座標表示

`CapturePage.OnImagePointerMoved` で、ステータスバーにポインタのドメイン座標（画像ピクセル）を表示します。

- `ZoomableView.ImageRectViewport()` で画像の表示矩形（原点 + サイズ・ビューポート座標）を取得（Fit 中は中央寄せ / 左上寄せに応じて計算）
- 範囲判定で画像内/外を判別（画像外は `-`）
- 画像内の場合は `ZoomableView.ViewportToContent()` でドメイン座標（画像ピクセル）へ変換
- スケールは実効スケール（Fit 中は FitScale）を使用するため、**Fit 表示中でも座標表示が正しく更新される**

## 8. 公開 API

### 8.1 ZoomManager

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `ZoomIn` | `void()` | ⊖⊕ ボタンのズームイン（23 段階） |
| `ZoomOut` | `void()` | ⊖⊕ ボタンのズームアウト（23 段階） |
| `ZoomInByWheel` | `void()` | Ctrl+ホイールのズームイン（×1.1） |
| `ZoomOutByWheel` | `void()` | Ctrl+ホイールのズームアウト（÷1.1） |
| `ApplyZoomFromSlider` | `void(double)` | スライダ値（10〜500）によるズーム適用 |
| `ApplyZoomFromText` | `bool(string?)` | テキスト入力（数値 or 数値+%）のパース・適用（10〜500） |
| `ApplyZoom` | `void()` | 現在の ZoomPercent を ZoomableView に適用（再入防止） |
| `SyncZoomSlider` | `void()` | ZoomPercent をスライダに反映（再入防止） |
| `UpdateImageAlignment` | `void()` | 中央寄せ設定を ZoomableView に反映 |
| `OnZoomableViewSizeChanged` | `void(SizeChangedEventArgs)` | リサイズ時の Fit 拡大率表示更新 |

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `ViewModel` | `MainViewModel` | メイン ViewModel（ZoomPercent / ZoomLevels / ZoomDisplayText 等） |
| `ZoomableView` | `ZoomableView` | ズーム・パン対応ビューポート（ScrollViewer 不使用） |
| `ZoomSlider` | `Slider` | 拡大率スライダ |
| `PointerPosition` | `Point?` | ズーム中心（ビューポートローカル DIP）。null = ビューポート中心 |

### 8.2 ZoomableView

| メンバー | 説明 |
| --- | --- |
| `ImageControl` | 画像表示コントロール（`Image`。Crop セッション等の外部参照用） |
| `SetSource(IImage?)` | 画像ソースを設定（Fit 表示にリセット） |
| `SetZoom(double, Point?)` | ズーム率を設定（ズーム中心維持込み。null 中心 = ビューポート中心） |
| `SetOffset(Vector)` | パン位置を設定（スクロール範囲にクランプ） |
| `PanBy(Vector)` | パン（コンテンツ座標の移動量） |
| `ViewportToContent(Point)` / `ContentToViewport(Point)` | ビューポート ↔ コンテンツ座標変換 |
| `DisplaySize()` | 画像の表示サイズ（ビューポート座標） |
| `ImageOriginViewport()` | 画像原点のビューポート座標 |
| `ImageRectViewport()` | 画像の表示矩形（原点 + サイズ・ビューポート座標。表示サイズと原点を 1 回の計算で取得） |
| `ZoomPercent` | 現在のズーム率（%）。0 = Fit |
| `Offset` | 現在のパン位置（コンテンツ座標。ビューポート左上に対応） |
| `Scale` / `EffectiveScale` | ズーム率スケール / 実効スケール（Fit 中は FitScale） |
| `CenterAlign` | Fit 時の中央寄せ / 左上寄せ |
| `ZoomRequested` | Ctrl+ホイールのズーム要求イベント（`ZoomRequestedEventArgs`） |
| `ViewportChanged` | ビューポート（パン・ズーム・表示状態）変化イベント。表示適用後に発火し、Crop オーバーレイ（暗転マスク・選択枠・ルーペ）の追従に使用する |

### 8.3 ZoomMath / ZoomTransition / ScrollMetrics（純粋関数）

| クラス | 関数 | 説明 |
| --- | --- | --- |
| `ZoomMath` | `FitZoomPercent` | Fit 実効倍率（%）の計算 |
| `ZoomMath` | `ComputeImagePoint` | ズーム中心の画像内の点（中央寄せ補正込み） |
| `ZoomMath` | `ComputeOffsetAfterZoom` | 画像サイズ変化時のオフセット計算 |
| `ZoomMath` | `ComputeOffsetAfterScaleChange` | スケール変化時のオフセット計算（ズーム中心維持） |
| `ZoomMath` | `ClampOffset` | オフセットのスクロール範囲クランプ |
| `ZoomTransition` | `NextLevel` / `PrevLevel` | ⊖⊕ 23 段階の遷移 |
| `ZoomTransition` | `WheelZoomIn` / `WheelZoomOut` | Ctrl+ホイール ×1.1 の遷移 |
| `ScrollMetrics` | `MaxOffset` / `ViewportSize` / `IsScrollable` / `ThumbRatio` | スクロールバーの幾何計算 |

## 9. ログ

本プロジェクト（app）のログ定義は `Logging/AppLog.cs`（LoggerMessage ソース生成）に集約し、`ILogger` 経由で出力する。
出力先は NLog（`%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log`、7日で自動アーカイブ・削除）で、`nlog.config` の `minLevel` で制御する。

### 9.1 LogLevel 設定ポリシー

トレースログは LogLevel.Trace に限定せず、「重要度 × 発生頻度」でレベルを使い分ける。

| LogLevel | 対象 | 本番での意図 |
| --- | --- | --- |
| `Error` | 例外・失敗（処理継続不可） | 常時出力し、障害を確実に残す |
| `Warning` | 異常だがフォールバックで継続 | 常時出力し、異常発生の痕跡を残す |
| `Information` | ユーザー操作の主要イベント（低頻度） | 常時出力し、操作手順の再現を可能にする |
| `Debug` | 操作イベントの詳細（マウス / キー / 設定変更） | 障害時のみ有効化して解析 |
| `Trace` | 高頻度・極詳細（ホイール・描画内部・座標変換・Down 直前/Up 直後の移動位置） | 特別な性能・座標解析時のみ有効化 |

判断基準:

1. 例外・警告は常時残す（`Error` / `Warning`）
2. 操作の起点イベント（低頻度）は `Information`
3. マウス・キー等の操作イベント（中頻度）は `Debug`
4. 高頻度・極詳細（マウス移動相当・ホイール連続・描画内部）は `Trace`
5. 設定変更（変更前後）は `Debug`

マウス移動の高頻度ログは行わない。ただし、Down 直前・Up 直後の位置は Down / Up イベント内で記録する（`Trace`）。

### 9.2 ログ定義一覧

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Information` | `_logger.LogZoomIn` / `LogZoomOut` | ズームイン/アウト（⊖⊕ボタン）の変更前後 % |
| `Information` | `_logger.LogZoomInWheel` / `LogZoomOutWheel` | Ctrl+ホイールズームの変更前後 % |
| `Information` | `_logger.LogCaptureMenuScreen` / `LogCaptureMenuWindow` / `LogCaptureMenuArea` | キャプチャメニュー（全画面/ウィンドウ/範囲選択）クリック |
| `Information` | `_logger.LogPreviewPointerPressed` | プレビュー上のポインタ押下（右ボタン / 切出し中） |
| `Information` | `_logger.LogHotKeyReceived` / `LogHotKeyTriggered` | WM_HOTKEY 受信 / 対応するキャプチャ種別 |
| `Information` | `_logger.LogActivated` / `LogDeactivated` | ウィンドウのアクティブ化 / 非アクティブ化 |
| `Information` | `_logger.LogStartSelectionMode` / `LogSessionReturned` | キャプチャ開始種別 / セッション戻り値（画像サイズ・矩形） |
| `Error` | `_logger.LogSettingsApplyError` | 設定変更の SnipCapture 反映で例外 |

## 10. 制約事項

- ズーム率の適用は `ZoomPercent` の PropertyChanged（CapturePage のハンドラ）経由で `ApplyZoom()` が 1 回だけ実行される（直接呼び出しと重複しないよう再入防止フラグで保護）
- スライダの範囲は 10〜500%（0=自動はフライアウトでのみ選択）。Fit 中はスライダ最小値に合わせる
- Fit 中はパン不可（画像がビューポートに収まるため）。スクロールバーも非表示
- スクロールバーはオーバーレイ表示のため、画像の表示領域を圧迫しない
- `Ctrl + マウスホイール` は ZoomableView が捕捉し、通常のパンには伝播しない（Ctrl を離すとパンに戻る）

## 11. テーマ（外観設定）

設定画面「外観設定」でテーマモードを選択できます。選択は即時反映され、次回起動時に復元されます。

| モード | 挙動 |
| --- | --- |
| `ライト` | 常にライトテーマ |
| `ダーク` | 常にダークテーマ |
| `システム` | OS のダーク / ライト設定に追従（既定値） |

- **反映方式**: `ThemeMode`（`Settings.ThemeMode`）→ `App.RequestedThemeVariant` へ反映。FluentTheme の DynamicResource により全コントロールの色が自動的に切り替わる
- **設定ファイル**: enum は文字列で保存（`"ThemeMode": "Dark"`）

### 11.1 テーマ対応コンポーネント

| コンポーネント | 標準リソース | ライト | ダーク |
| --- | --- | --- | --- |
| フォルダリスト背景 | `SystemControlBackgroundAltHighBrush` | 白 | 黒 |
| ファイル名ラベル背景 | `SystemControlBackgroundAltHighBrush`（リストと同一色） | 白 | 黒 |
| ファイル名文字 | `SystemControlForegroundBaseHighBrush` | 黒 | 白 |
| サムネイル枠背景 | `SystemControlBackgroundChromeMediumLowBrush` | 薄いグレー（`#F2F2F2`） | 暗いグレー（`#FF2B2B2B`） |
| サムネイル枠線 | `SystemControlForegroundChromeGrayBrush` | グレー（`#767676`） | グレー（`#767676`） |
| サムネイル影 | `ThumbnailShadow`（App リソース・テーマ非依存） | 単一値（`0 3 10 0 #8A000000`） | 単一値（同左） |

- 背景・枠線・文字色は **Fluent テーマの標準リソース（DynamicResource）** を参照し、テーマ切替に自動追従する（カスタム ThemeDictionaries は不使用）
- サムネイル影のみ、Fluent テーマに BoxShadows の標準リソースが存在しないため、テーマ非依存の単一値として App リソース（`ThumbnailShadow`）で定義する
- ホバー時は枠線がアクセントブルー（`#0078D4`）にハイライトされ、影は非表示になる（テーマ非依存）

## 12. フォルダビュー

### 12.1 表示モード・ソート順の保存と復元

表示モード（特大 / 大 / 中）とソート順（昇順 / 降順）の選択状態は自動保存され、次回起動時に復元されます。

| 項目 | 設定キー | 既定値 |
| --- | --- | --- |
| 表示モード | `Settings.FolderViewModeIndex`（0=特大, 1=大, 2=中） | 1（大） |
| ソート順 | `Settings.FolderViewSortAscending`（true=昇順, false=降順） | true（昇順） |

- **保存タイミング**: 表示モード・ソート切替操作のたびに保存（`SaveViewSettings`）
- **復元タイミング**: ページ初期化 / `Loaded` / ナビゲーションで表示されるたび（`IsVisible` 変更時）
- **復元時の同期**: 復元した表示モード・ソート順を ToggleButton の選択状態（チェック）へ同期する。同期対象は対応するボタン群のみ（表示モードボタンは Tag=0〜2、ソートボタンは Tag=asc/desc。他グループには触れない）

### 12.2 一覧表示

- サムネイル + ファイル名を `BottomAlignWrapPanel` で表示（行内下端揃え・行高は最大アイテム高さ）
- ダブルクリックで既定アプリによりファイルを開く

### 12.3 Undo 一覧

ナビパネル「Undo 一覧」から、キャプチャページの Undo / Redo スタックと現在のプレビュー画像をサムネイル一覧表示します。

- **表示順**: 時系列順で「新しい → 古い」の固定。`Redo（新しい）→ 現在 → Undo（古い）` の順に並ぶ
  - Redo: `_redoStack[0]`（現在に最も近い未来）から順に「Redo 1」「Redo 2」…
  - 現在: 現在のプレビュー画像（ラベル「現在」）
  - Undo: `_stack[^1]`（現在の直前）から順に「Undo 1」「Undo 2」…
- **現在の画像の強調**: 現在のキャプチャページに表示中の画像は青枠（`BorderBrush #0078D4`・`BorderThickness 3`）で強調表示する
- **表示モード**: ツールバーの特大 / 大 / 中 ボタンで切替可能。選択状態は `Settings.UndoViewModeIndex`（0=特大, 1=大, 2=中、既定 1=大）に保存・復元される。アイコンサイズはフォルダビューと同じ値（`FolderExtraLargeIconSize` / `FolderLargeIconSize` / `FolderMediumIconSize`）を使用する
- **外部ツール表示**: サムネイルのダブルクリックで一時フォルダへ PNG として保存し、OS の既定アプリ（外部ツール）で開く（Undo / Redo スタックの画像はファイルではなく Bitmap のため）
- **更新タイミング**: ナビゲーションで Undo 一覧を表示するたびに最新の履歴を読み込む（`UndoPage` が `IsVisible` 変更を監視し、自身で `Refresh()` を呼び出し再構築する。UndoStack / MainViewModel は DI シングルトンから取得）
- **表示モードの保存・復元**: 表示モード切替操作のたびに保存（`SaveViewSettings`）。復元はページ初期化 / `Refresh` 時（範囲チェック付き）

### 12.4 ラベル設定（キャプチャ・切出し時の左上ラベル）

キャプチャ選択画面の左上に表示される最大 3 行のラベルは、設定画面「ラベル設定」で行ごとに設定できます。

| 行 | ラベル | 文字色の設定キー | 背景色の設定キー（不透明度は色に統合） | フォントサイズ |
| --- | --- | --- | --- | --- |
| 1 行目 | サイズラベル | `CaptureTextColor`（ラベル設定の 1 行目に一本化） | `SizeLabelBackgroundColor` | `SizeLabelFontSize` |
| 2 行目 | 操作ラベル | `SubLineColor` | `SubLineBackgroundColor` | `SubLineFontSize` |
| 3 行目 | 情報ラベル | `InfoLabelColor` | `InfoLabelBackgroundColor` | `InfoLabelFontSize` |

- **背景色**: 色と不透明度を統合した `#AARRGGBB` 形式（例: 黒 60% = `#99000000`）。カラーピッカー（ColorPickerControl）の Flyout で不透明度（アルファ）も設定できる
- **キャプチャテキストの色の一本化**: キャプチャテキストの色は 1 行目（サイズラベル）の文字色に一本化する（設定キー `CaptureTextColor` は「ラベル設定」の 1 行目へマップ）

### 12.5 ルーペ表示モード

キャプチャ選択画面のルーペ表示は、起動時の初期状態として `Settings.LoupeModeValue` で制御します。

| 値 | 意味 | CaptureSettings.LoupeVisible への変換 |
| --- | --- | --- |
| 0 | 非表示（既定） | `false` |
| 1 | 表示 | `true` |
| 2 | 自動 | `true`（0 以外は表示として扱う） |

- 反映経路: `MainWindow.ApplySnipCaptureSettings` がセッション開始前に `CaptureSettings.LoupeVisible`（`LoupeModeValue != 0`）へ変換し、SnipCaptureUI へ渡す
- 設定画面にはルーペ表示モードの UI はない（起動時デフォルトとしてのみ使用）
- キャプチャセッション中のルーペ表示は SnipCaptureUI 側（`LoupePositionMoved` による表示開始等）が制御する

### 12.6 キーボードショートカット（Ctrl キー）

メニューを開かずに効く Ctrl ショートカットを提供します。`Ctrl` とメニュー項目のアクセラレータ文字（`S` / `C` / `V` / `Z` / `Y` / `R`）の組み合わせ、およびズーム（`Ctrl + / Ctrl -`）です。

| ショートカット | 操作 |
| --- | --- |
| `Ctrl+S` | File - 画像を保存 |
| `Ctrl+C` | Clipboard - コピー |
| `Ctrl+V` | Clipboard - 貼り付け |
| `Ctrl+Z` | Image - 元に戻す |
| `Ctrl+Y` | Image - 元に戻すを取り消す |
| `Ctrl+R` | Crop - 切り出し |
| `Ctrl +` | ズームイン（⊖⊕ボタンと同じ 23 段階遷移） |
| `Ctrl -` | ズームアウト（⊖⊕ボタンと同じ 23 段階遷移） |

- **Capture 系はホットキーに一本化**: `Capture - スクリーン / ウィンドウ / 範囲選択` は Ctrl ショートカット（Ctrl+Q/W/E）を実装しない。グローバルホットキー（WM_HOTKEY）がアプリ内外で動作するため、ショートカットの重複を避ける

- **実装**: 全ショートカットは `CapturePage.OnGlobalKeyDown`（`KeyDown` を Tunnel で購読）で一括処理する。テキスト入力中（ファイル名テンプレート等）はテキスト編集ショートカット（Ctrl+C/V/Z/Y）を優先するため無視する
- **メニュー項目への表記**: 各メニュー項目の `InputGesture` に `KeyGesture` を設定し、メニュー展開時に項目の右側へショートカットキーを表示する。`InputGesture` は表示のみでキー処理は行わない（処理は `OnGlobalKeyDown` またはグローバルホットキーが担当）。ショートカット未実装の項目（Crop - 自動切り出し 等）には表示しない
- **Capture 系メニューはホットキー設定を表示**: `Capture - スクリーン / ウィンドウ / 範囲選択` の `InputGesture` は**ホットキー設定**（`SelectScreenHotKey` / `WindowSelectHotKey` / `AreaSelectHotKey`、settings.json の値）を表示する。設定画面でホットキーを変更するとメニュー表示も即時更新される。ホットキーが未設定（`Key.None`）の場合は表示しない。キー操作はグローバルホットキー（WM_HOTKEY）が担当する
- **Alt アクセラレータは無効**: メニューの Header にアクセラレータ文字（`(_X)`）を含めないため、メニュー展開中の `Alt+S` 等は機能しない（Alt はメニュー展開専用）
- **ホットキーとの関係**: 設定画面のホットキー（`SelectScreenHotKey` 等・WM_HOTKEY のグローバル登録）とは独立。Capture 系以外の Ctrl ショートカットはホットキーと競合しないキーを使用する

## 13. ファイル名テンプレートのバリデーション

保存ファイル名のテンプレート文字列を検証し、無効な場合は視覚的に通知します。

- **無効条件**: 空文字、またはファイル名に使用できない文字（`\ / : * ? " < > |`）を含む
- **赤枠表示**: バリデーションエラー時はテキストボックスの枠線が赤くなる（`validation-error` クラス）
- **保存ボタン無効化**: `CanSave` は画像あり（`PreviewImage` 非 null）+ テンプレート有効（`HasValidTemplate`）+ 未保存（`IsSaved` が false）の場合のみ true
  - 保存が完了すると `IsSaved` が true になり、保存ボタン・メニューの「画像を保存」が無効化される（同一画像の二重保存防止）
  - 画像の変更（新規キャプチャ・切り出し・貼り付け・Undo / Redo 復元）で `IsSaved` が false に戻り、再び保存可能になる
- **エラーメッセージ**: `ValidationMessage`（例: 「ファイル名に使用できない文字 ('/') が含まれています。」）
- **即時反映**: テキスト入力中も含め即時検証・反映され、エラー解消で自動的に通常表示へ戻る
- **オートインクリメント**: 保存成功後に、テンプレート文字列の**右端の数値**をインクリメントして次のファイル名を自動生成する（`FileNameTemplate.IncrementRightmostNumber`。桁数は維持: `screenshot_001.png` → `screenshot_002.png`）。数値がなければそのまま維持される

### 13.1 プレースホルダ

ファイル名テンプレートでは、`{ }` で囲われた文字列がプレースホルダとして扱われ、内部の日時トークンが現在日時へ置換されます。トークンは自由に組み合わせられます。

| トークン | 内容 | 例 |
| --- | --- | --- |
| `date` | 日付（yyyyMMdd） | 20250101 |
| `time` | 時刻（HHmmss・24 時間制） | 120000 |
| `yyyy` | 年 4 桁 | 2025 |
| `MM` | 月 2 桁 | 01 |
| `dd` | 日 2 桁 | 01 |
| `HH` | 時 2 桁（24 時間制） | 12 |
| `mm` | 分 2 桁 | 34 |
| `ss` | 秒 2 桁 | 56 |

- プレースホルダ内でトークンを自由に組み合わせられる（例: `{yyyyMMdd}` → `20260819`、`{HHmm}` → `2352`、`{date}` → `20260819`）
- 未定義の文字列（例: `{example}`）はトークン置換されず、中括弧が外れてそのまま出力される
- 日時は保存実行時点の現在日時で置換される（`FileNameTemplate.Generate`）
- ToolTip にはプレースホルダの説明が表示される（`{date}` / `{time}` 等は XAML マークアップ拡張と衝突するためコードビハインドで構築）

### 13.2 エスケープ

プレースホルダの開始・終了を示す中括弧自体をリテラルとして含める場合は、二重中括弧でエスケープします。

| 入力 | 出力 |
| --- | --- |
| `{{` | `{`（リテラル） |
| `}}` | `}`（リテラル） |
| `{{date}}` | `{date}`（プレースホルダとして置換されない） |

- エスケープはプレースホルダ置換より先に解決されるため、`{{date}}` のようにエスケープで囲んだ文字列はプレースホルダとして誤置換されない
- 例: `abcd{{e}}f{yyyyMMdd}-{HHmm}` → `abcd{e}f20260819-2352`
- `{` `}` は Windows のファイル名で使用可能なため、エスケープ後の検証も有効と判定される

## 14. 保存先フォルダのプレースホルダ

設定画面「保存設定」の保存先フォルダには、**プレースホルダ `{MyPictures}`** を使用できます。

- `{MyPictures}` は `Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)` のフォルダ（ピクチャフォルダ）へ展開される
- 大文字小文字は区別しない（`{mypictures}` / `{MYPICTURES}` も展開）
- パスの一部に含められる（例: `{MyPictures}\SaveEvidence`）
- 解決は `MainViewModel.ResolveSaveFolderPath` / `SaveFolderPathResolved` が行い、保存・フォルダ一覧（FolderViewPage）・保存ボタンの ToolTip・エクスプローラーで開く操作はすべて解決済みパスを使用する
- 設定値（`Settings.SaveFolderPath`）にはプレースホルダのまま保存される

### 14.1 ToolTip

保存先フォルダのテキストボックスには、プレースホルダの説明を示す ToolTip が表示される。

- `{MyPictures}` はピクチャフォルダへ展開される旨（展開先の実パス付き）と使用例（`{MyPictures}\SaveEvidence`）、および「存在しないパスは枠が赤くなる」ことを説明
- `{MyPictures}` は XAML のマークアップ拡張（`{...}`）と衝突するため、ToolTip はコードビハインド（`SettingsPage.BuildSaveFolderToolTip`）で構築する

### 14.2 パス存在バリデーション

保存先フォルダのテキストボックスは、入力値（プレースホルダ解決後）が**存在しない場合に枠線が赤くなる**（`validation-error` クラス）。

- 判定: `MainViewModel.HasInvalidSaveFolder`（解決後パスが空、または `Directory.Exists` が false の場合 true）
- 表示: テキストボックスの枠線が赤くなる（`TextBox.validation-error /template/ Border#PART_BorderElement` スタイル。フォーカス・ホバー状態でも優先）
- 入力中も即時反映される（`SaveFolderPath` の変更通知に連動）
