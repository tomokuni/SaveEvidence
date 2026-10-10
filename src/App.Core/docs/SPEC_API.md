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
- [19. 依存関係](#19-依存関係)

## 1. 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `Enums.AppPage` / `Enums.ThemeMode` | `public` | 任意 |
| `Models.Settings` / `Models.FileNameTemplate` / `Models.UndoHistory` / `Models.HotKeyParser` | `public` | 任意 |
| `Services.ISettingsService` / `Services.SettingsService` | `public` | 任意 |
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
| `MainWindowBounds` | `EsRectInt?` | MainWindow の位置・サイズ（画面物理座標） |
| `MainWindowState` | `EsWindowState` | MainWindow のウィンドウ状態 |
| `SelectScreenHotKey` / `WindowSelectHotKey` / `AreaSelectHotKey` | `HotKeySetting` | グローバルホットキー |
| `FileNameTemplate` / `SaveFolderPath` | `string` | ファイル名テンプレート・保存先フォルダ |
| `ThemeMode` | `ThemeMode` | テーマモード |
| `IsNavigationExpanded` | `bool` | ナビゲーションパネルの展開状態 |
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

画像（`IEsBitmap`）をファイルへ書き込むサービスの抽象です。

| メソッド | 説明 |
| --- | --- |
| `Save(IEsBitmap image, string filePath)` | 画像を指定パスへ書き込む（**拡張子が形式を決める**。 実体はコアの `EsBitmapFileExtensions.Save` へ委譲し、未対応の拡張子は `NotSupportedException`） |

- UI フレームワーク非依存（コアの画像のみに依存）です。 本番の実装は **`EsBitmapImageWriter`（本プロジェクトの 1 型）だけ**で、コアの保存へ委譲します（UI ごとの実装を持たないため、保存結果が UI で食い違わない）。
- テストではモックに差し替え、ヘッドレス不要で検証できます。

**`EsBitmapImageWriter`（sealed class : `IImageWriter`。 唯一の本番実装）**:

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Save` | `void Save(IEsBitmap image, string filePath)` | 画像を指定パスへ書き込む（コアの `EsBitmapFileExtensions.Save` へ委譲） |

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
| `Push(EsWritableBitmap? currentImage)` | 現在の画像を Undo スタックへプッシュする（画素コピーで複製。 Redo 履歴はクリア） |
| `Undo(EsWritableBitmap? currentImage)` | 現在の画像を Redo スタックへ退避し、Undo スタックの最新画像を返す（所有権は呼び出し側） |
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

`IImageWriter` には本番の実装（本プロジェクトの `EsBitmapImageWriter`）を注入します（Avalonia 版は `Program.cs` の DI で登録、MewUI 版は `Program.cs` で直接生成）。 **保存形式の判別とエンコードはコアの `EsBitmapFileExtensions.Save` が単一所有します**（本プロジェクトの実装は委譲するだけの薄い適合で、保存形式は両 UI で同じ ）。

### 18.2 主なプロパティ

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `PreviewImage` | `EsWritableBitmap?` | プレビュー画像（所有権は本 ViewModel。 差し替え時に旧画像を破棄） |
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
| `NavPanelWidth` / `IsNavigationExpanded` | `double` / `bool` | ナビパネル |
| `ThemeModes` | `IReadOnlyList<ThemeMode>` | テーマモードの選択肢 |

### 18.3 主な操作

| メンバー | 説明 |
| --- | --- |
| `SetPreviewImage(EsWritableBitmap)` | プレビュー画像を設定する（所有権が本 ViewModel へ移る） |
| `SaveImageAsync()` | プレビュー画像を保存する（テンプレートからファイル名を生成。 同名時は `ConfirmOverwriteAsync`） |
| `CropPreviewCommand` / `ConfirmAutoCropCommand` | 矩形で切り出す（画像座標 `EsRectInt`） |
| `AutoCropWindowCommand` / `CancelAutoCropCommand` | 窓枠の自動検出（`LogicLineDetector` + `LineRectangleReconstructor`） |
| `HitDetectedRect(double x, double y)` | 指定座標（画像ピクセル）を含む検出矩形のうち面積が最小のものを返す（無ければ null）。 両 UI のホバー強調と確定・キャンセル判定が共用する |
| `CopyToClipboardAsync` / `PasteFromClipboardAsync` | クリップボード操作（実体はコールバックへ委譲） |

### 18.4 UI 依存の注入点

| メンバー | 型 | 既定 |
| --- | --- | --- |
| `CopyToClipboardAsync` | `Func<IEsBitmap, Task<bool>>?` | null（未設定時は何もしない） |
| `PasteFromClipboardAsync` | `Func<Task<EsWritableBitmap?>>?` | null |
| `ConfirmOverwriteAsync` | `Func<string, Task<bool>>?` | null（未設定時は上書きしない） |
| `BeforeEditAction` | `Action?` | null |
| `StartSelectionMode` | `Action<CaptureType>?` | null |

## 19. 依存関係

| 依存 | 用途 |
| --- | --- |
| `EsUtil.Platform.Core`（`EsUtil.Platform.Core.*`） | コアの画像（保持・切出し）・`EsRectInt` / `EsPoint`（座標）・`HotKeySetting` / `HotKeyModifiers` / `EsKey`（ホットキー解析）・`EsWindowState`（設定モデル）・`CaptureType`（キャプチャ種別） |
| `LineDetection`（`EsUtil.SaveEvidence.Parts.LineDetection`） | 窓枠の自動検出（`LogicLineDetector`）と矩形復元（`LineRectangleReconstructor`） |
| `CommunityToolkit.Mvvm` | `ObservableObject` / `[RelayCommand]` / `[ObservableProperty]` |

UI フレームワーク（Avalonia / MewUI）への依存はありません。
