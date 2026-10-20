# App.Core API リファレンス

`App.Core` は UI フレームワーク非依存のモデル・ユーティリティ・ViewModel を提供します。 名前空間は `App.AvaloniaUI` と同一の `EsUtil.SaveEvidence.*` です。

## 目次 <!-- omit from toc -->

- [1. 公開 API の可視性](#1-公開-api-の可視性)
- [2. `Enums.AppPage`（列挙型）](#2-enumsapppage列挙型)
- [3. `Enums.ThemeMode`（列挙型）](#3-enumsthememode列挙型)
- [4. `Models.Settings`（クラス）](#4-modelssettingsクラス)
- [5. `Services.ISettingsService` / `Services.SettingsService`（クラス）](#5-servicesisettingsservice--servicessettingsserviceクラス)
- [6. `Services.EsRectIntJsonConverter`（内部・クラス）](#6-servicesesrectintjsonconverter内部クラス)
- [7. `Services.IImageWriter`（インターフェース）](#7-servicesiimagewriterインターフェース)
- [8. `Models.FileNameTemplate`（静的クラス）](#8-modelsfilenametemplate静的クラス)
  - [8.1 テンプレートの書式](#81-テンプレートの書式)
- [9. `Models.UndoHistory`（クラス）](#9-modelsundohistoryクラス)
- [10. `Models.HotKeyParser`（静的クラス）](#10-modelshotkeyparser静的クラス)
- [11. `Models.CapacityLimitedCache`（内部・静的クラス）](#11-modelscapacitylimitedcache内部静的クラス)
- [12. `Models.ThumbnailScheduler`（内部・クラス）](#12-modelsthumbnailscheduler内部クラス)
- [13. `Models.ThumbnailPriority`（内部・構造体）](#13-modelsthumbnailpriority内部構造体)
- [14. `Models.ImageFileExtensions`（内部・静的クラス）](#14-modelsimagefileextensions内部静的クラス)
- [15. `ViewModels.ViewModeDisplay`（内部・静的クラス）](#15-viewmodelsviewmodedisplay内部静的クラス)
- [16. `ViewModels.FolderViewViewModel`（クラス）](#16-viewmodelsfolderviewviewmodelクラス)
- [17. `ViewModels.UndoViewModel`（クラス）](#17-viewmodelsundoviewmodelクラス)
- [18. `ViewModels.MainViewModel`（クラス）](#18-viewmodelsmainviewmodelクラス)
  - [18.1 コンストラクター](#181-コンストラクター)
  - [18.2 主なプロパティ](#182-主なプロパティ)
  - [18.3 主な操作](#183-主な操作)
  - [18.4 UI 依存の注入点](#184-ui-依存の注入点)
- [19. `Services.StatusMessages`（静的クラス）](#19-servicesstatusmessages静的クラス)
  - [19.1 通知と画像の差し替えの順序](#191-通知と画像の差し替えの順序)
- [20. `Services.AppPalette`（静的クラス）](#20-servicesapppalette静的クラス)
- [21. `Services.NavPanelMetrics`（静的クラス）](#21-servicesnavpanelmetrics静的クラス)
- [22. `Services.StartupPreviewImage`（静的クラス）](#22-servicesstartuppreviewimage静的クラス)
- [23. `Services.EnumDisplayNames`（静的クラス）](#23-servicesenumdisplaynames静的クラス)
- [24. `Services.StatusBarMetrics`（静的クラス）](#24-servicesstatusbarmetrics静的クラス)
- [25. `Services.SettingsMetrics`（静的クラス）](#25-servicessettingsmetrics静的クラス)
- [26. `Services.ThumbnailTileMetrics`（静的クラス）](#26-servicesthumbnailtilemetrics静的クラス)
- [27. `Services.AppIdentity`（静的クラス）](#27-servicesappidentity静的クラス)
- [28. `Services.WindowMetrics` / `Services.WindowLayout`（静的クラス）](#28-serviceswindowmetrics--serviceswindowlayout静的クラス)
- [29. `Services.WindowMenu`](#29-serviceswindowmenu)
- [30. `Icons.SvgIconLoader`（内部）](#30-iconssvgiconloader内部)
- [31. `Services.ZoomMenuLevels`（静的クラス）](#31-serviceszoommenulevels静的クラス)
- [32. 依存関係](#32-依存関係)
- [33. `Services.ToolbarMetrics`（静的クラス）](#33-servicestoolbarmetrics静的クラス)
- [34. `Services.WindowPlacementRule`（静的クラス）](#34-serviceswindowplacementrule静的クラス)
- [35. `Icons.CaptureIconFiles`（静的クラス）](#35-iconscaptureiconfiles静的クラス)

## 1. 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `Enums.AppPage` / `Enums.ThemeMode` | `public` | 任意 |
| `Models.Settings` / `Models.FileNameTemplate` / `Models.UndoHistory` / `Models.HotKeyParser` | `public` | 任意 |
| `Services.ISettingsService` / `Services.SettingsService` | `public` | 任意 |
| `Services.StatusMessages` / `Services.AppPalette` / `Services.NavPanelMetrics` / `Services.StartupPreviewImage` / `Services.EnumDisplayNames` / `Services.SettingsMetrics` / `Services.ToolbarMetrics` / `Services.ZoomMenuLevels` / `Services.WindowPlacementRule` / `Icons.CaptureIconFiles` | `public` | 任意 |
| `Services.IImageWriter` | `public` | 任意 |
| `Services.EsRectIntJsonConverter` | `internal` | `SaveEvidence` / `App.AvaloniaUI.Tests` / `App.Core.Tests` |
| `Models.CapacityLimitedCache` | `internal` | `SaveEvidence` / `App.AvaloniaUI.Tests` / `App.Core.Tests` |
| `Models.ThumbnailScheduler` | `internal` | 同上 |
| `Models.ThumbnailPriority` | `internal` | 同上 |
| `Models.ImageFileExtensions` | `internal` | `SaveEvidence` / `EsUtil.SaveEvidence.App.MewUI` / `App.AvaloniaUI.Tests` / `App.Core.Tests` / `App.MewUI.Tests` |
| `ViewModels.ViewModeDisplay` | `internal` | 同上 |
| `ViewModels.FolderViewViewModel` / `ViewModels.UndoViewModel` / `ViewModels.MainViewModel` | `public` | 任意 |

## 2. `Enums.AppPage`（列挙型）

ナビゲーションページの識別子（`Capture` / `FolderView` / `Undo` / `Settings`）。

## 3. `Enums.ThemeMode`（列挙型）

テーマモード。 各メンバーは `System.ComponentModel.DescriptionAttribute` で表示名を保持します。

## 4. `Models.Settings`（クラス）

アプリケーションの設定を表すデータモデルです（`ObservableObject` を継承し、`[ObservableProperty]` で変更通知を発行）。

UI フレームワークの型ではなく、UI 非依存の型（`EsRectInt` / `EsWindowState`）で保持します。 永続化ロジックは含まず、読み書きは `Services.SettingsService` を介します。

| プロパティ（抜粋） | 型 | 説明 |
| --- | --- | --- |
| `MainWindowBounds` | `EsRectInt?` | MainWindow の位置とウィンドウ枠を含む外寸（画面物理ピクセル。 4 UI 版で共通） |
| `MainWindowState` | `EsWindowState` | MainWindow のウィンドウ状態 |
| `SelectScreenHotKey` / `WindowSelectHotKey` / `AreaSelectHotKey` | `HotKeySetting` | グローバルホットキー |
| `FileNameTemplate` / `SaveFolderPath` | `string` | ファイル名テンプレート・保存先フォルダ |
| `ThemeMode` | `ThemeMode` | テーマモード |
| `IsNavigationExpanded` | `bool` | ナビゲーションパネルの展開状態 |
| `StartupPreview` | `StartupPreviewMode` | 起動直後のキャプチャ ページに置く画像（`Gradient` = グラデーション画像 / `Blank` = 空白。 既定は `Gradient`） |
| `FolderExtraLargeIconSize` / `FolderLargeIconSize` / `FolderMediumIconSize` | `int` | フォルダビューのアイコンサイズ |

## 5. `Services.ISettingsService` / `Services.SettingsService`（クラス）

設定の読み書きを提供します。 保存先は `%LOCALAPPDATA%\SaveEvidence\settings.json` です。

| メンバー | 説明 |
| --- | --- |
| `SettingsService(JsonSerializerContext)` | JSON ソース生成コンテキストを指定して初期化する（必須。 既定の引数は持たない） |
| `Current` | 現在の設定オブジェクト（`Settings`） |
| `Save()` | 設定をファイルへ保存する（前回保存時と同一の JSON なら書き込まない） |

- 排他制御（`Lock`）によりスレッドセーフな読み書きを保証します。
- 読み込み・書き込みの失敗は握りつぶし、既定値または現状のまま続行します（設定ファイル破損で起動できなくならないようにするため）。
- enum は名前で保存します（例: `"ThemeMode": "Dark"`）。 enum の変換器は AOT 対応のジェネリック版（型ごとに指定）を使用します。
- **ソース生成コンテキストは必須です**（反射ベースのシリアル化はトリミング / Native AOT で公開プロパティが削除され、設定が空 `{}` になるため、本体は反射経路を持ちません）。
- 書式（インデント・コメント許容・末尾カンマ許容・矩形の 4 項目保存）は、ソース生成の有無にかかわらず共通です。

> **トリミング / Native AOT**: コンテキストを注入しないと設定が空（`{}`）になります。 コンテキストは `Settings` を参照する側のアセンブリに置いてください（同一コンパイル内では MVVM のソース生成結果を JSON のソース生成が見られないため）。

## 6. `Services.EsRectIntJsonConverter`（内部・クラス）

`EsRectInt` は読み取り専用プロパティ（`Right` 等）を持つため、専用コンバーターで `X` / `Y` / `Width` / `Height` のみを読み書きします。

- 読み込み時に欠落したプロパティは 0、未知のプロパティは無視します。
- 既存の設定ファイルをそのまま読み込めます。

## 7. `Services.IImageWriter`（インターフェース）

画像（`EsReadOnlyBitmap`）をファイルへ書き込むサービスの抽象です。

| メソッド | 説明 |
| --- | --- |
| `Save(EsReadOnlyBitmap image, string filePath)` | 画像を指定パスへ書き込む（**拡張子が形式を決める**。 実体はコアの `EsReadOnlyBitmap.Save` へ委譲し、未対応の拡張子は `NotSupportedException`） |

- UI フレームワーク非依存（コアの画像のみに依存）です。 本番の実装は **`EsBitmapImageWriter`（本プロジェクトの 1 型）だけ**で、コアの保存へ委譲します（UI ごとの実装を持たないため、保存結果が UI で食い違わない）。
- テストではモックに差し替え、ヘッドレス不要で検証できます。

**`EsBitmapImageWriter`（sealed class : `IImageWriter`。 唯一の本番実装）**:

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Save` | `void Save(EsReadOnlyBitmap image, string filePath)` | 画像を指定パスへ書き込む（コアの `EsReadOnlyBitmap.Save` へ委譲） |

- **Windows 専用です**（`SupportedOSPlatform("windows")`）。 保存の実体（`System.Drawing.Common`）が Windows 専用のためです。 利用側の両アプリは `net10.0-windows` です。
- **1 型に集約する理由**: 実装を UI ごとに分けると保存結果が食い違いうるため、`IImageWriter` の実装は 1 型（`EsBitmapImageWriter`）が担い、保存はコアへ委譲する。

## 8. `Models.FileNameTemplate`（静的クラス）

| メソッド | 説明 |
| --- | --- |
| `Generate(string template, DateTime? now = null)` | テンプレートから実際のファイル名を生成する（`{ }` 内の日時トークンを置換する。 `now` を省略すると現在日時を使う） |
| `IncrementRightmostNumber(string text)` | 右端の数値連続を +1 する（桁数を維持。 `decimal` で扱えない桁数は `BigInteger`） |
| `IsValidTemplate(string template, out string? errorMessage)` | ファイル名として使用可能かを検証する（使用不可文字を検出） |

日時を引数（`now`）で受け取るのは、呼び出し側が時刻を固定して結果を再現できるようにするためです（本番の呼び出しは現在日時を使います）。

### 8.1 テンプレートの書式

| 記法 | 意味 |
| --- | --- |
| `{date}` / `{time}` | `yyyyMMdd` / `HHmmss` の省略形 |
| `{yyyy}` `{MM}` `{dd}` `{HH}` `{mm}` `{ss}` | 個別の日時トークン |
| `{{` / `}}` | リテラルの `{` / `}` |

## 9. `Models.UndoHistory`（クラス）

| メンバー | 説明 |
| --- | --- |
| `MaxCount` | Undo の最大保存段数（5） |
| `Count` / `RedoCount` | 現在の Undo / Redo スタック深度 |
| `IsRestoring` | Undo 復元中かどうか（復元中の `Push` を無視するために使用） |
| `GetUndoHistory()` / `GetRedoHistory()` | スタック内の画像を古い順に取得する（読み取り専用。 変更・破棄してはならない） |
| `Push(EsReadOnlyBitmap? currentImage)` | 現在の画像を Undo スタックへプッシュする（**複製**（`Copy`）して保持。 Redo 履歴はクリア） |
| `Undo(EsReadOnlyBitmap? currentImage)` | 現在の画像を Redo スタックへ退避し、Undo スタックの最新画像を返す（所有権は呼び出し側） |
| `Redo()` | Redo スタックから最新の画像を返す（所有権は呼び出し側） |
| `Clear()` | 全画像を破棄して両スタックを空にする |

- スレッドセーフではありません（UI スレッドからのみ操作します）。

## 10. `Models.HotKeyParser`（静的クラス）

| メソッド | 説明 |
| --- | --- |
| `TryParse(string text, out HotKeySetting setting)` | ホットキー文字列を `HotKeySetting` へ変換する（成功時 true） |

- "Ctrl + Shift + Q" / "Ctrl+Shift+Q" 形式に対応します。 大文字小文字は区別しません。
- 最低 1 つの修飾キーが必要です。 キー名が解釈できない場合は false を返します。
- **キーは正規化してから採用します**（`EsKeyExtensions.Normalize`）。 文字列からの解釈は `Enum.TryParse` を使うため**数値文字列を受理してしまい**、列挙に無い値（例: `"999"`）がキーとして通ります。 正規化で未定義値を拒否します。

## 11. `Models.CapacityLimitedCache`（内部・静的クラス）

| メソッド | 説明 |
| --- | --- |
| `GetOrAdd<TKey, TValue>(ConcurrentDictionary<TKey, TValue> cache, int capacity, TKey key, Func<TKey, TValue> factory)` | キャッシュに値を取得または追加する。 容量超過時はキャッシュ全体をクリアして肥大化を防ぐ |

- **容量の判定・破棄・登録はロックで直列化します**（ファクトリの実行はロックの外）。 複数スレッドから呼んでも、同じ値を 2 回破棄したり、先に入れた値を上書きして漏らしたりしません。 生成（復号など）は並列のままです。
- **ファクトリの実行中に同じキーが登録された場合は、先に登録された値を返し、後から作った値は破棄します**（同じキーに 2 つの値を持たない）。
- **`capacity` は呼び出し側が渡します**。 一覧が表示する件数より小さい上限を渡すと、表示中の値が破棄されます（例: サムネイルの生成中に表示中の画像が破棄される）。

## 12. `Models.ThumbnailScheduler`（内部・クラス）

背景の処理（サムネイルの生成）を、ページの表示状態に応じた負荷で実行するスケジューラ。 UI フレームワークに依存しません。

| メンバー | 説明 |
| --- | --- |
| `ThumbnailScheduler(Action<Exception> onError, int maxParallelism = 0, TimeSpan? idleInterval = null)` | 生成する。 `maxParallelism` が 0 以下なら `Environment.ProcessorCount`、`idleInterval` を省略すると 100 ms |
| `IsActive` | 表示中かどうか。 true = 同時実行の上限まで並列、false = 1 件ずつ + 待機（`idleInterval` 分を分割して待つ） |
| `PendingCount` | 引き受け済みでまだ実行を開始していない要求の数（実行中は数えない） |
| `Enqueue(Func<CancellationToken, ValueTask> work)` | 要求を追加する（見えていない項目として積む。 結果の反映（UI スレッドへの転送）は呼び出し側が行う） |
| `Enqueue(ThumbnailPriority priority, Func<CancellationToken, ValueTask> work)` | 優先度を指定して要求を追加する |
| `Reprioritize(ThumbnailRequest request, ThumbnailPriority priority)` | 未実行の要求の優先度を変える（実行が始まった要求・同じ優先度の場合は何もしない） |
| `Clear()` | 未実行の要求を捨て、実行中の要求へ中断を伝える（世代番号を進める）。 古い世代の要求は実行しない |
| `Dispose()` | 要求の受付を終了し、待機中のループを止める（冪等） |

- **優先度の小さい順に実行します**（見えている項目が先、その中では上方の項目が先。 `ThumbnailPriority`）。
- **非表示では 1 件ずつ、表示中は並列**に実行します。 非表示の待機は 100 ms を 10 ms ずつに分けて待つため、表示へ切り替わると待機の残りを待たずに次の要求を開始します。
- **表示中は同時実行の空きを先に確保してから取り出します**（取り出してから空きを待つと、待っている間の要求は実行前にもかかわらず `Reprioritize` を受け付けない）。
- **起動時は仕事をしません**: 生成は要求（`Enqueue`）が積まれて初めて動きます。 生成ループは要求が無い間は信号待ちであり、ページを表示するまで負荷をかけません（起動を遅くしない）。
- **失敗は `onError` へ渡して次の要求へ進みます**（1 件の失敗でループを止めません）。
- **`Clear()` は実行中の要求のトークンをキャンセルしますが、CTS を破棄しません**（実行側がトークンを参照しているため。 世代番号で古い結果を無効化する）。 `Dispose()` も同様です。
- 用途はフォルダビュー / Undo 一覧のサムネイル生成（両 UI で共用）です。 表示中の全力生成と、**起動後の先読み**（ウィンドウを表示した後に低負荷で生成しておき、ページの切り替えを待たせない）の両方に使います。 `Enqueue` が返す `ThumbnailRequest`（入れ子型）は `Reprioritize` の識別子として呼び出し側が保持します。

## 13. `Models.ThumbnailPriority`（内部・構造体）

サムネイル生成の優先度（値が小さいほど先に実行する）。

| メンバー | 説明 |
| --- | --- |
| `VisibleBand` / `BackgroundBand` | 見えている項目の帯（0）/ 見えていない項目の帯（1） |
| `Visible(int index)` / `Background(int index)` | 優先度を生成する（`index` は一覧内の位置。 0 が最上部） |
| `FromRange(itemTop, itemBottom, viewportTop, viewportBottom, index)` | 項目の表示範囲と可視領域から優先度を求める（一部でも重なれば見えている扱い） |
| `Compare(left, right)` | 2 つの優先度を比べる（`PriorityQueue` へ渡す） |

- **2 つの帯で並べます**: 見えている項目を先に、見えていない項目を後に実行し、**同じ帯では位置の小さい順（上方優先）**です。
- **可視判定は純粋関数**（表示範囲の重なりだけ）のため、画面なしで境界まで検証できます。 端が一致するだけの場合は重なりとみなしません。
- `IComparable<T>` は実装しません（比較演算子の実装を避け、`Compare` へ一本化する）。

## 14. `Models.ImageFileExtensions`（内部・静的クラス）

フォルダビューの一覧に含める画像ファイルの拡張子判定。 判定は拡張子のみで行い、大文字・小文字は区別しません。

| メンバー | 説明 |
| --- | --- |
| `All` | 対象拡張子の一覧（読み取り専用）。 `.png` / `.jpg` / `.jpeg` / `.bmp` / `.gif` / `.tiff` / `.tif` / `.webp` |
| `IsImageFile(string path)` | 拡張子が対象のいずれかなら true |

- 拡張子の抽出には `Path.GetExtension` を使用するため、`.png` のようにドットで始まる名前も画像として扱われます（末尾のドット `a.` は空文字のため対象外）。

## 15. `ViewModels.ViewModeDisplay`（内部・静的クラス）

| メンバー | 説明 |
| --- | --- |
| `Count` | 表示モードの数（有効なインデックスは `0` 〜 `Count - 1`） |
| `GetText(int index)` | 表示モード（特大 / 大 / 中）の表示文字列を返す |
| `ClampIndex(int index)` | 表示モードのインデックスを有効範囲へクランプする |
| `GetItemCountText(int count)` | 件数の表示文字列を返す |

## 16. `ViewModels.FolderViewViewModel`（クラス）

フォルダビューの表示モード・ソート順の状態を保持し、変更を通知します（`ObservableObject`）。

| メンバー | 説明 |
| --- | --- |
| `ItemCount` / `ItemCountText` | 項目数と表示文字列 |
| `SelectedViewModeIndex` / `ViewModeText` | 表示モードのインデックスと表示文字列 |
| `IsSortAscending` / `SortStatusText` | ソート順（昇順 / 降順）と表示文字列 |
| `FolderPathText` | フォルダパスの表示文字列 |

## 17. `ViewModels.UndoViewModel`（クラス）

Undo 一覧の表示モード状態を保持し、変更を通知します（`ObservableObject`）。

| メンバー | 説明 |
| --- | --- |
| `ItemCount` / `ItemCountText` | 項目数と表示文字列 |
| `SelectedViewModeIndex` / `ViewModeText` | 表示モードのインデックスと表示文字列 |

## 18. `ViewModels.MainViewModel`（クラス）

メイン画面の状態を保持し、コマンドと変更通知を提供します（`ObservableObject` + `[RelayCommand]` / `[ObservableProperty]`）。

### 18.1 コンストラクター

```csharp
public MainViewModel(ISettingsService settingsService, IImageWriter imageWriter)
```

`IImageWriter` には本番の実装（本プロジェクトの `EsBitmapImageWriter`）を注入します（Avalonia 版は `Program.cs` の DI で登録、MewUI 版は `Program.cs` で直接生成）。 **保存形式の判別とエンコードはコアの `EsReadOnlyBitmap.Save` が単一所有します**（本プロジェクトの実装は委譲するだけの薄い適合で、保存形式は両 UI で同じ ）。

### 18.2 主なプロパティ

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `PreviewImage` | `EsReadOnlyBitmap?` | プレビュー画像（所有権は本 ViewModel。 差し替え時に旧画像を破棄。 表示・保存・切り出し・検出は**読むだけ**であり、編集は新しい画像を作るため読み取り専用の型で保持する） |
| `Settings` | `Settings` | 設定オブジェクト（`ISettingsService.Current`） |
| `HasPreviewImage` / `CanCopy` / `CanSave` / `CanPaste` | `bool` | 各操作の可否（コマンドの `CanExecute`） |
| `DetectedRects` | `IReadOnlyList<EsRectInt>` | 自動切り出しで検出した矩形候補群（画像座標・面積昇順・最大 256 件） |
| `HoveredRect` | `EsRectInt?` | ホバー中の矩形（面積最小） |
| `IsAutoCropActive` | `bool` | 自動切り出しモード中かどうか |
| `StatusMessage` / `ZoomDisplayText` / `PointerDisplayText` | `string` | 画面表示用テキスト |
| `IsSaved` / `SavedFileName` / `SaveStatusText` | `bool` / `string` | 保存済み状態 |
| `FileNameTemplateText` | `string` | ファイル名テンプレート（変更時に検証と保存） |
| `SaveFolderPath` / `SaveFolderPathResolved` / `HasInvalidSaveFolder` | `string` / `bool` | 保存先フォルダ |
| `ValidationMessage` / `HasValidTemplate` / `HasInvalidTemplate` | `string?` / `bool` | テンプレート検証結果 |
| `CenterAlign` | `bool` | Fit 時の画像配置（設定へ委譲） |
| `NavPanelWidth` / `IsNavigationExpanded` | `double` / `bool` | ナビパネル（幅の根拠は `NavPanelMetrics`（App.Core）が単一所有。 展開 136 / 収納 40 DIP） |
| `ThemeModes` | `IReadOnlyList<ThemeMode>` | テーマモードの選択肢 |

### 18.3 主な操作

| メンバー | 説明 |
| --- | --- |
| `SetPreviewImage(EsReadOnlyBitmap)` / `SetPreviewImage(EsBitmap)` | プレビュー画像を設定する（所有権が本 ViewModel へ移る）。 書き換え可能な画像を渡す版は、渡した実体を**凍結**して 0 コピーで共有する |
| `SaveImageAsync()` | プレビュー画像を保存する（テンプレートからファイル名を生成。 同名時は `ConfirmOverwriteAsync`）。 失敗時は `StatusMessage` へ通知する |
| `CropPreviewCommand` / `ConfirmAutoCropCommand` | 矩形で切り出す（画像座標 `EsRectInt`）。 成功時に `切り出しました（W x H）` を通知する |
| `AutoCropWindowCommand` / `CancelAutoCropCommand` | 窓枠の自動検出（`LogicLineDetector` + `LineRectangleReconstructor`）。 検出結果とキャンセルを通知する |
| `HitDetectedRect(double x, double y)` | 指定座標（画像ピクセル）を含む検出矩形のうち面積が最小のものを返す（無ければ null）。 両 UI のホバー強調と確定・キャンセル判定が共用する |
| `CopyToClipboardAsync` / `PasteFromClipboardAsync` | クリップボード操作（実体はコールバックへ委譲）。 貼り付けの成否は `PasteFromClipboardAsync` が通知する |

### 18.4 UI 依存の注入点

| メンバー | 型 | 既定 |
| --- | --- | --- |
| `CopyToClipboardAsync` | `Func<EsReadOnlyBitmap, Task<bool>>?` | null（未設定時は何もしない） |
| `PasteFromClipboardAsync` | `Func<Task<EsBitmap?>>?` | null（貼り付けは画像を**作る**操作であるため、書き換え可能な型を返すコールバックを受ける） |
| `ConfirmOverwriteAsync` | `Func<string, Task<bool>>?` | null（未設定時は上書きしない） |
| `BeforeEditAction` | `Action?` | null |
| `StartSelectionMode` | `Action<CaptureType>?` | null |

## 19. `Services.StatusMessages`（静的クラス）

ステータスバーへ出す通知メッセージ（操作の結果）の文言を単一所有します。 UI 版ごとに文言・有無がずれないよう、**実際の設定もコア（`MainViewModel`）が行い**、UI 層は結果の表示だけを担います。

| メソッド | 通知する場面 | 文言（例） |
| --- | --- | --- |
| `CaptureCompleted(int width, int height)` | 撮影の確定（各 UI の撮影完了ハンドラーが使う） | `キャプチャしました（640 x 480）` |
| `CropCompleted(int width, int height)` | 切り出しの確定（手動・自動の両方） | `切り出しました（120 x 90）` |
| `PasteCompleted()` / `PasteUnavailable()` | クリップボードからの貼り付けの成否 | `クリップボードの画像を貼り付けました。` / `クリップボードに画像がありません。` |
| `SaveFailed()` | 保存の失敗（例外） | `保存できませんでした（保存先フォルダを確認してください）。` |
| `SaveUnavailable(bool hasInvalidTemplate, string? validationMessage)` | 保存を実行できない状態（UI がボタンを無効にする代わりに出す） | `保存できる状態ではありません（…）` |
| `AutoCropDetected(int count)` / `AutoCropNotFound()` | 自動切り出しの検出結果 | `自動切り出し: 3 個のウィンドウを検出しました。…` |
| `AutoCropCanceled()` | 自動切り出しのキャンセル（**実行中だった場合のみ**） | `自動切り出しをキャンセルしました。` |

### 19.1 通知と画像の差し替えの順序

`MainViewModel` はプレビュー画像が変わると `StatusMessage` を空にします（古い通知を残さないため）。 したがって通知は**画像の差し替えより後**に設定します。

- `CropPreview` / `ConfirmAutoCrop` は `ApplyPixelEdit`（画像の差し替え）が成功した後に `CropCompleted` を設定する
- 撮影完了は UI 側のハンドラーが `SetPreviewImage` の後に `CaptureCompleted` を設定する
- `StatusMessages` のサイズ表記はカルチャに依存させない（桁区切り・全角数字を出さない）

## 20. `Services.AppPalette`（静的クラス）

4 つの UI 版で共通に使う配色を単一所有します（色は UI 非依存の `0xAARRGGBB` で保持し、各 UI 版が自分の色型へ変換する）。 **明色で指定された色を暗色でも使うと画面がまぶしく暗色テーマの意味が無くなる**ため、テーマ別に値を持ちます。

| 面 | 明色 | 暗色 | 取得 |
| --- | --- | --- | --- |
| 画像領域（プレビューの下地） | `#F7F7F7` | `#202020` | `ImageArea(bool isDark)` / `ImageAreaDrawingColor(bool isDark)` |
| ナビゲーション パネル | `#F0F0F0` | `#2B2B2B` | `NavPanel(bool isDark)` |
| ページ（ウィンドウ）の下地 | `#FFFFFF` | `#202020` | `Page(bool isDark)` |
| ナビゲーション項目の選択中の背景（薄いグレー） | `#DEDEDE` | `#373737` | `NavItemSelected(bool isDark)` |
| ナビゲーション項目の選択マーク（左端の青） | `#0067C0` | `#4CC2FF` | `NavItemSelectionAccent(bool isDark)` |
| 一覧（フォルダビュー / Undo 一覧）の背景 | `#F2F2F2` | `#2B2B2B` | `ListBackground(bool isDark)` |
| ボタンの面（通常時） | `#FFFFFF` | `#2D2D2D` | `ButtonFace(bool isDark)` |
| ボタンの枠線 | `#D6D6D6` | `#303030` | `ButtonBorder(bool isDark)` |
| 選択中のトグル ボタンの面 | `#0067C0` | `#4CC2FF` | `ButtonCheckedFace(bool isDark)` |
| 選択中のトグル ボタンの枠線 | `#003E73` | `#42A7DC` | `ButtonCheckedBorder(bool isDark)` |
| 選択中のトグル ボタンの文字 | `#FFFFFF` | `#000000` | `ButtonCheckedForeground(bool isDark)` |
| ボタンの面（ホバー） | `#E9E9E9` | `#3A3A3A` | `ToolbarButtonBackground(bool isDark, bool isPressed)` |
| ボタンの面（押下） | `#DBDBDB` | `#4E4E4E` | `ToolbarButtonBackground(bool isDark, bool isPressed)` |

`ToolbarButtonCornerRadius`（4 DIP）はボタンの角丸です。 ボタンは **WinUI 3 の既定のボタンと同じ外観**（面・1 DIP の枠線・選択中はアクセント色）にし、マウスを重ねると薄いグレー、押している間はさらに濃くします（暗色では「濃く」を「明るく」で表す）。 面・枠線・アクセント色は WinUI 3 の実測値です（半透明の面を下地と重ねた結果を opaque な値として保持する。 半透明のまま配ると下地が変わったときに UI 版でずれる）。

`ImageAreaDrawingColor` はプレビューの下地（`ZoomViewAppearance.BackgroundColor`、UI 非依存の `System.Drawing.Color`）向けです。 変換を各 UI 版へ書き分けないよう本クラスが担います。 選択中の 2 色は WinUI 3 の `NavigationViewItem` の実測値です（AvaloniaUI / MewUI が同じ表示にする）。

## 21. `Services.NavPanelMetrics`（静的クラス）

ナビゲーション パネルの幅と項目の寸法を単一所有します（UI 版ごとに幅・大きさがずれないようにするため）。

| 定数 | 値（DIP） | 意味 |
| --- | --- | --- |
| `ItemFontSize` | 14 | 項目の文字サイズ（AvaloniaUI 版の項目と同じ大きさ） |
| `ItemHorizontalPadding` | 12 | 項目の左右の余白 |
| `ItemHeight` | 40 | 項目の高さ（開閉・文字の有無で変わらない） |
| `ItemsMarginTop` | 8 | 項目の一覧の上下の余白 |
| `SelectionMarkWidth` / `SelectionMarkLeft` | 3 / 4 | 選択中のマークの幅と左端 |
| `IconSize` | 16 | 項目のアイコンの一辺（開閉ボタン・左上のアプリ アイコン・ツールバーも同じ値） |
| `IconSpacing` | 8 | アイコンと文字の間隔 |
| `LabelWidth` | 68 | 最も長い項目名（「フォルダビュー」）の実測幅 |
| `TrailingSlack` | 20 | UI ごとの内部余白の差を吸収する α |
| `CompactWidth` | 40 | 収納時の幅（左右の余白 + アイコン） |
| `ExpandedWidth` | 136 | 展開時の幅（左右の余白 + アイコン + 間隔 + 文字 + α） |

`Width(bool isExpanded)` が展開・収納を切り替えます。 `MainViewModel.NavPanelWidth` は本クラスへ委譲します。

項目の高さを `ItemHeight` に固定するのは、**文字の有無で高さが変わると開閉で項目の位置がずれる**ためです（AvaloniaUI 版の実測で 33.6 → 36.8 DIP と変わっていました）。

## 22. `Services.StartupPreviewImage`（静的クラス）

起動直後のキャプチャ ページに置くプレースホルダ画像（グラデーション）を生成します。 **4 つの UI 版が同じ画像を使います**（UI ごとに別の画像を作らない）。

| メンバー | 説明 |
| --- | --- |
| `Width` / `Height` | 画像の大きさ（640 × 400 ピクセル） |
| `CreateGradient()` | 左上（`#1E90FF`）から右下（`#FF8C00`）への線形グラデーションを生成する（画素は BGRA8888 を直接書くため Skia / GDI+ に依存しない） |
| `Apply(MainViewModel, StartupPreviewMode)` | 設定に従って画像を設定する（`Blank` の場合は何もしない）。 4 UI の起動時に 1 回だけ呼ぶ |

設定 `StartupPreview`（`Enums.StartupPreviewMode`）は「グラデーション画像」と「空白」の 2 択で、設定画面（画像設定）で選びます。 **切り替えは次回起動から反映**します（撮影中の画像を勝手に差し替えないため）。

## 23. `Services.EnumDisplayNames`（静的クラス）

列挙値の表示名（`DescriptionAttribute`）を取得します。 表示名は列挙の定義側が単一所有し、UI ごとに文言を書き分けません。

| メンバー | 説明 |
| --- | --- |
| `Get<T>(T value)` / `Get(Enum value)` | 表示名を返す（属性が無い場合は列挙の名前） |

## 24. `Services.StatusBarMetrics`（静的クラス）

ステータス バーの文字サイズを単一所有します（UI 版ごとに文字の大きさがずれないようにするため）。

| 定数 | 値 | 意味 |
| --- | --- | --- |
| FontSize | 12 DIP | ステータス バーの文字サイズ（4 つの UI 版で共通） |

**経緯**: AvaloniaUI 版だけがテーマの既定（14 DIP）を使い、他の UI 版より 1 回り大きく見えました。 4 UI 版とも同じ値を使います。

WinForms の Font はポイント単位のため、使う側で DIP × 72 ÷ 96 に換算します（`AppStyles.StatusBarFont` が換算済みのフォントを 1 つだけ生成して共用する）。

## 25. `Services.SettingsMetrics`（静的クラス）

設定ページの文字サイズと共通の文言を単一所有します（UI 版ごとに見た目がずれないようにするため）。 文字サイズは **AvaloniaUI 版（Fluent テーマの既定 14 DIP）を基準**にします。

| 定数 | 値 | 意味 |
| --- | --- | --- |
| FontSize | 14 DIP | 設定ページの文字サイズ（見出し・項目名・チェック ボックス・コンボ ボックス） |
| HintFontSize | 11 DIP | 補足（例・説明）の文字サイズ |
| SaveFolderHint | 文字列 | 保存先フォルダの入力欄の下へ出す補足（例: {MyPictures}\SaveEvidence（…）） |

WinForms の Font はポイント単位のため、使う側で DIP × 72 ÷ 96 に換算します。

## 26. `Services.ThumbnailTileMetrics`（静的クラス）

一覧（フォルダビュー / Undo 一覧）のサムネイル 1 件の見た目の寸法を単一所有します（UI 版ごとに影の濃さ・名前の位置がずれないようにするため）。 **画像の周りに枠線・下地は置きません**（影だけを付けます）。

| 定数 | 値 | 意味 |
| --- | --- | --- |
| `LabelFontSize` | 11 DIP | 名前の文字サイズ |
| `LabelHeight` | 30 DIP | 名前の行の高さ（2 行分を固定し、行内の名前の位置を揃える） |
| `LabelHorizontalPadding` | 1 DIP | 名前の左右の余白 |
| `ItemMargin` / `ItemPadding` | 2 / 12 DIP | 画像の外側の余白 / 一覧が確保する余白（AvaloniaUI 版の `ListBoxItem` と同じ） |
| `ItemGap` | 28 DIP | 隣り合う画像の間隔（`(ItemPadding + ItemMargin) × 2`） |
| `HoverBackground(bool isDark)` | `#DADADA` / `#303030` | マウスを重ねたときの帯の色（AvaloniaUI 版の一覧の項目と同じ） |

`ImageHeight(double width, int pixelWidth, int pixelHeight)` は、幅を固定したときの表示高さ（縦横比を保つ）を返します。 一覧のアイテムはこの高さを持ち、行の高さは「その行の最大アイテム高さ」、アイテムは行内で下端揃えになります（AvaloniaUI 版の `BottomAlignWrapPanel` と同じ並べ方）。

**影は `Services.ShadowPlan` が単一所有します**（下記「`Services.ShadowPlan`」）。 Undo 一覧も同じ寸法を使い、現在表示中の画像は**枠を変えずに名前を太字にする**ため、影・ホバーがフォルダビューと一致します。

**`Services.ShadowPlan`（静的クラス）と影の計画**

画像の 4 辺と 4 隅へ落とす影の**唯一の定義**です（キャプチャページの画像・フォルダビュー・Undo 一覧のサムネイルが同じ影になる）。 値は AvaloniaUI 版の `BoxShadow`（ぼかし 10 DIP・黒 54%）を実測して写したものです。

| メンバー | 値 | 意味 |
| --- | --- | --- |
| `BlurRadius` | 10 DIP | 影の広がり（画像の端からこの距離まで落とす） |
| `ColorArgb` | `#8A000000` | 影の色（黒 54%） |
| `Insets` | 4 辺とも 10 DIP | 画像の外側へ確保する影の余白 |
| `Edges` / `Edge(ShadowSide)` | 4 辺 | 辺ごとの計画（幅・濃さの停止点・グラデーションの向き） |
| `CornerStops` | 11 件 | 隅の放射グラデーションの停止点（中心が最も濃い。 隅を 1 つの要素で塗る WinUI 3 の一覧だけが使う） |
| `AlphaAtDistance(double)` | 端 0.245 → 10 DIP で 0.005 | 画像の端から指定距離の不透明度（実測プロファイル） |
| `ColorAtDistance(double)` | 黒 + 上記の透過度 | 指定距離の色（`0xAARRGGBB`） |
| `BuildDeviceBands(x, y, w, h, scaling)` | 4 × 画素数 + 4 + 隅の行数 | 4 辺と 4 隅を**1 画素の帯**（`ShadowBand`）へ落とした計画。 各 UI 版は単色で塗るだけ（隅の濃さは画像の隅の頂点からの斜めの距離で決める） |

**型（1 型 1 ファイル）**: `ShadowSide`（辺）・`ShadowStop`（停止点 = 位置と色）・`ShadowInsets`（余白）・`ShadowEdgePlan`（辺の計画）・`ShadowBand`（1 画素の帯）・`ShadowPlan`（上表）。

**4 辺を同じ濃さにする理由**: 影を下方向へずらすと上辺だけが薄くなり、画像と影の間に白っぽい隙間が見えます（実測で指摘）。 4 辺とも画像の端で最も濃く、外側へ向けて同じ割合で薄くします。

**描き方**は 1 通りです。 `BuildDeviceBands` の帯（1 画素）を**単色で塗るだけ**です（4 辺も 4 隅も同じ帯。 隅の帯は行ごとに同じ濃さの画素をまとめて 1 本にしてあります）。 各 UI 版は自分の描画 API へ写します（AvaloniaUI は `DrawRectangle` と `BoxShadow`（一覧のサムネイル）、WinUI 3 は `Border` / Skia の `DrawRect` と `RadialGradientBrush`（一覧の隅だけ）、WinForms と MewUI は GDI+ / Skia の `FillRectangle`）。

**隅に放射グラデーションを使わない理由**: 放射グラデーションは描画 API ごとに半径・中心の解釈が揺れ、隅だけ濃い点や薄い筋として見えました（実測）。 隅の画素の濃さは**画像の隅の頂点からの距離**（斜めの距離 `√(dx²+dy²)`）で決めます。 辺と同じ濃さの変化が斜めにもそのまま続くため、四角い塊や濃い点になりません（辺の帯と隅の帯は重ねません。 重ねると隅だけ濃くなります）。

**重ねない**: 辺の帯どうし・辺と隅の帯・画像の端を覆う帯は重なりません（重なるとその画素だけ濃くなります。 実測: 隅の 1 画素が濃い点になった）。

**画像と影の間**には枠線・余白を置きません（辺と隅の矩形は画像の境界から外側へ連続します）。 画像の外側に影の余地が無い場合（画像が領域の端まで達している場合）は、その辺の影は見えません。

**画素の格子へ揃える**（キャプチャページの画像）: 画像は端数のある位置へ描かれるため、影の矩形を端数のまま描くと端の画素が半透明になり、画像との間に「白い線」または「黒い線」として見えます。 `BuildDeviceBands` が**物理ピクセルの整数矩形**を返すため、各 UI 版は丸めを持ちません（DIP へ戻すときだけ倍率で割ります）。 アンチエイリアスは切って画像の端と隙間なく接させます。 さらに**画像の端の画素（切り捨てた行・列）も帯で覆い**ます: 画像の描画側は端の画素を切り捨てたり切り上げたりするため、`floor` の行・列が画像でも影でも塗られず**背景（白）が残る**ことがあります（実測: 拡大率によって画像の上辺に 1 画素の白い線）。 覆った分は画像が塗る場合は画像に隠れます。

## 27. `Services.AppIdentity`（静的クラス）

アプリの**名前・タスク バーの識別子・アイコン・保存先**の唯一の定義です（4 つの UI 版が同じ値を使う）。 UI 版ごとに文字列を書くと、綴りの違いで設定・ログ・タスク バーのグループ分けがずれます。

| メンバー | 値 | 用途 |
| --- | --- | --- |
| `ProductName` | `SaveEvidence` | ウィンドウ タイトル・フォルダ名・ログ ファイル名 |
| `AppUserModelId` | `EsUtil.SaveEvidence` | タスク バーのグループ分け（逆ドメイン形式） |
| `IconFileName` | `ic_fluent_scan_table_24_regular.svg` | ウィンドウ（タスク バー）アイコン（4 UI 版が共有） |
| `LocalAppDataFolder` | `%LOCALAPPDATA%\SaveEvidence` | 設定・ログの置き場 |
| `LogsFolder` / `LogFilePath` | `…\logs` / `…\logs\SaveEvidence.log` | ログ出力（Serilog） |
| `SettingsFilePath` | `…\settings.json` | 設定ファイル |
| `TempFolder` | `%TEMP%\SaveEvidence` | 実行時に組み立てるファイル（ICO 等） |

`TaskbarIdentity.Apply()` は **`SetCurrentProcessExplicitAppUserModelID`** でプロセスの AUMID を設定します（4 UI 版が同じ値を使うため、タスク バーでは 1 つのアプリにまとまります）。 **ウィンドウを作る前**に各 UI 版のエントリ ポイントが 1 回呼びます。 失敗（API 不在等）しても例外にせず、起動を妨げません（タスク バーのグループ分けが既定のままになるだけです）。

## 28. `Services.WindowMetrics` / `Services.WindowLayout`（静的クラス）

メイン ウィンドウの寸法と配置の**唯一の定義**です（4 つの UI 版で同じ値を使う）。

| メンバー | 値 | 意味 |
| --- | --- | --- |
| `WindowMetrics.MinimumWidth` / `MinimumHeight` | 640 / 400 DIP | ウィンドウ枠を含む外寸の最小サイズ（WinUI 3 / WinForms は物理ピクセルへ換算し、Avalonia / MewUI は枠を差し引いたクライアント領域の最小サイズを指定する） |
| `WindowMetrics.DefaultWidth` / `DefaultHeight` | 1200 / 800 DIP | 復元する配置が無いときの既定外寸 |
| `WindowLayout.ToolbarRowTop` / `ToolbarButtonHeight` | 6 / 32 DIP | ページのツールバーの行の上端とボタンの高さ（アプリ アイコンの縦位置の基準） |
| `WindowLayout.AppIconRowHeight` | 44 DIP | 左上のアプリ アイコンを置く行の高さ（中心がツールバーのアイコンと一致する 6 + 32 + 6） |
| `WindowLayout.AppIconSize` | 16 DIP | 左上のアプリ アイコン（タスク バーと同じアイコン）の一辺。 **ナビゲーションのアイコンと同じ大きさ** |
| `WindowLayout.AppIconColumnWidth` | 40 DIP | 左上のアプリ アイコンの列の幅（開閉アイコンと同じ列に置く） |
| `WindowLayout.NavToggleHeight` / `NavToggleWidth` | 40 / 40 DIP | ナビ領域の一番上に置く開閉ボタンの大きさ |
| `WindowLayout.TitleBarHeight` | 32 DIP | 上端のドラッグ領域の高さ（タイトル バーの帯は作りません） |

**ナビゲーション パネルの配置**: 展開しても**ページ領域は動きません**（収納時の幅の位置に留まります）。 展開したパネルはページ領域の上へ重なり、上端はページ領域の先頭、下端はステータス バーに被ります。 パネルの一番上にアプリ アイコン、その下に開閉ボタン（アイコンのみ）を置き、**展開時は開閉ボタンの右にアプリ名を表示**します。 アプリ アイコンは**ナビゲーションのアイコンと同じ大きさ**にし、**縦位置をツールバーのアイコンと揃え**（`AppIconRowHeight` の中で中央）、**横位置を開閉アイコンと同じ列**（`AppIconColumnWidth` の中で中央）に置きます。 展開中にナビ領域の外を操作すると収納へ戻ります。

## 29. `Services.WindowMenu`

ウィンドウ左上のアイコンの押下で出す標準メニューの**項目・並び・有効/無効**の唯一の定義です（4 つの UI 版で同じメニューになる）。

| メンバー | 内容 |
| --- | --- |
| `Items(bool isMaximized, ...)` | 元のサイズに戻す / 移動 / サイズ変更 / 最小化 / 最大化 / 閉じる（閉じるの前だけ区切り線） |
| `WindowMenuAction` | 実行する操作（`Restore` / `Move` / `Size` / `Minimize` / `Maximize` / `Close`） |
| `WindowMenuEntry` | 1 項目（操作・表示名・有効かどうか・区切りの有無） |

**移動とサイズ変更**は `EsUtil.Platform.Core` の `SystemWindowCommands` が OS のモード（`WM_SYSCOMMAND`）へ入ります（アプリ側で追従を実装しない）。

## 30. `Icons.SvgIconLoader`（内部）

アイコンの SVG の**読み込みと解析**の唯一の定義です（4 つの UI 版が同じファイルを使う）。

| メンバー | 内容 |
| --- | --- |
| `Load(string fileName)` | `SvgIcon`（viewBox・パス データ・塗りの色）を返す。 見つからない場合は空のアイコン |
| `Enumerate()` | 埋め込みリソースのファイル名の一覧 |
| `SvgIcon` | viewBox の大きさ・パス データ（`d` 属性）・塗りの色（`0xAARRGGBB`） |

**アイコンのファイルは `src/App.Core/src/Icons/` に置きます**（埋め込みリソース。 論理名は `EsUtil.SaveEvidence.App.Core.Icons.<ファイル名>`）。 各 UI 版はパス データを自分の描画 API へ変換して描くだけです（UI 版ごとに SVG を複製すると、片方だけ直したときに見た目がずれます）。

## 31. `Services.ZoomMenuLevels`（静的クラス）

ステータス バーの**拡大率の押下で出す選択肢**（%）の唯一の定義です（4 つの UI 版で同じ選択肢になる）。 メニューの先頭には「自動」（Fit 表示へ戻す）を別途置きます。

| メンバー | 内容 |
| --- | --- |
| `All` | 10 / 15 / 25 / 35 / 50 / 75 / 100 / 150 / 200 / 250 / 350 / 500（%・昇順） |

⊖⊕ ボタンの段階（`ZoomTransition.DefaultButtonZoomLevels`・23 段階）とは目的が異なるため別に持ちます。

## 32. 依存関係

| 依存 | 用途 |
| --- | --- |
| `EsUtil.Platform.Core`（`EsUtil.Platform.Core.*`） | コアの画像（保持・切出し）・`EsRectInt` / `EsPoint`（座標）・`HotKeySetting` / `HotKeyModifiers` / `EsKey`（ホットキー解析）・`EsWindowState`（設定モデル）・`CaptureType`（キャプチャ種別） |
| `LineDetection`（`EsUtil.SaveEvidence.Parts.LineDetection`） | 窓枠の自動検出（`LogicLineDetector`）と矩形復元（`LineRectangleReconstructor`） |
| `CommunityToolkit.Mvvm` | `ObservableObject` / `[RelayCommand]` / `[ObservableProperty]` |

UI フレームワーク（Avalonia / MewUI）への依存はありません。

## 33. `Services.ToolbarMetrics`（静的クラス）

ツールバーの共通寸法・文字サイズを単一所有します（4 UI 版は同じサイズを使い、各フレームワークの標準スタイルで描画します）。

| 定数 | 値 | 意味 |
| --- | --- | --- |
| `ButtonHeight` | 32 DIP | ツールバー ボタンとテンプレート入力欄の高さ |
| `IconSize` | 16 DIP | ツールバー アイコンの一辺 |
| `PaddingX` / `PaddingY` | 8 / 4 DIP | 文字付きツールバー ボタンの内側余白 |
| `CornerRadius` | 4 DIP | アイコン ボタンの角丸（`AppPalette.ToolbarButtonCornerRadius` を参照） |
| `FontSize` | 12 DIP | ツールバーのボタンとファイル名テンプレート入力欄の文字サイズ |

MewUI の既定値 12 DIP に揃えます。 AvaloniaUI と WinUI 3 のテーマ既定は 14 DIP です。 WinForms の `Font` はポイント単位のため、使う側で DIP × 72 ÷ 96 に換算します。 アイコン単独のボタンは 32 × 32 DIP・16 DIP アイコン・内側余白 0 とし、文字付きボタンには表の余白を使います。

## 34. `Services.WindowPlacementRule`（静的クラス）

ウィンドウ配置の復元判定・可視領域への補正と、外寸・クライアント寸法間の単位変換を単一所有します。 保存する配置矩形はウィンドウ枠を含む外寸で、画面物理ピクセルです。

| メンバー | 内容 |
| --- | --- |
| `CanRestore` / `ClampToVisibleArea` | 配置を復元できるかの判定 / 表示領域内への位置補正 |
| `ToPhysicalLength` | DIP から物理ピクセルへの換算 |
| `ToClientLength(double, int, double)` | 外寸 DIP と枠の物理ピクセルからクライアント DIP への換算 |
| `ToClientLength(int, int, double)` | 外寸物理ピクセルと枠からクライアント DIP への換算 |
| `ToOuterPhysicalLength` | クライアント DIP と枠から外寸物理ピクセルへの換算 |

## 35. `Icons.CaptureIconFiles`（静的クラス）

キャプチャ ページのツールバーとメニューで共用するアイコン ファイル名です。 アイコン画像は `SvgIconLoader` が扱う埋め込み SVG から各 UI 版が描画します。

| 定数 | 用途 |
| --- | --- |
| `Save` | 保存 |
| `ScreenSelect` | スクリーン選択キャプチャ |
| `WindowSelect` | ウィンドウ選択キャプチャ |
| `AreaSelect` | 領域選択キャプチャ |
| `Crop` | 切り出し |
