# App.Core API リファレンス

`App.Core` は UI フレームワーク非依存のモデル・ユーティリティ・ViewModel を提供します。 名前空間は `App.AvaloniaUI` と同一の `EsUtil.SaveEvidence.*` です。

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `Enum.AppPage` / `Enum.ThemeMode` | `public` | 任意 |
| `Models.Settings` / `Models.FileNameTemplate` / `Models.UndoHistory` / `Models.HotKeyParser` | `public` | 任意 |
| `Services.ISettingsService` / `Services.SettingsService` | `public` | 任意 |
| `Services.IImageWriter` | `public` | 任意 |
| `Services.EsRectIntJsonConverter` | `internal` | `SaveEvidence` / `App.AvaloniaUI.Tests` / `App.Core.Tests` |
| `Models.CapacityLimitedCache` | `internal` | `SaveEvidence` / `App.AvaloniaUI.Tests` / `App.Core.Tests` |
| `Models.ImageFileExtensions` | `internal` | `SaveEvidence` / `EsUtil.SaveEvidence.App.MewUI` / `App.AvaloniaUI.Tests` / `App.Core.Tests` / `App.MewUI.Tests` |
| `ViewModels.ViewModeDisplay` | `internal` | 同上 |
| `ViewModels.FolderViewViewModel` / `ViewModels.UndoViewModel` / `ViewModels.MainViewModel` | `public` | 任意 |

## `Enum.AppPage`（列挙型）

ナビゲーションページの識別子（`Capture` / `FolderView` / `Undo` / `Settings`）。

## `Enum.ThemeMode`（列挙型）

テーマモード。 各メンバーは `System.ComponentModel.DescriptionAttribute` で表示名を保持します。

## `Models.Settings`（クラス）

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

## `Services.ISettingsService` / `Services.SettingsService`（クラス）

設定の読み書きを提供します。 保存先は `%LOCALAPPDATA%\SaveEvidence\settings.json` です。

| メンバー | 説明 |
| --- | --- |
| `SettingsService()` | 反射ベースのシリアル化で初期化する（通常の実行環境向け） |
| `SettingsService(JsonSerializerContext?)` | JSON ソース生成コンテキストを指定して初期化する（トリミング / Native AOT 向け） |
| `Current` | 現在の設定オブジェクト（`Settings`） |
| `Save()` | 設定をファイルへ保存する（前回保存時と同一の JSON なら書き込まない） |

- 排他制御（`Lock`）によりスレッドセーフな読み書きを保証します。
- 読み込み・書き込みの失敗は握りつぶし、既定値または現状のまま続行します（設定ファイル破損で起動できなくならないようにするため）。
- enum は名前で保存します（例: `"ThemeMode": "Dark"`）。 enum の変換器は AOT 対応のジェネリック版（型ごとに指定）を使用します。
- 書式（インデント・コメント許容・末尾カンマ許容・矩形の 4 項目保存）は、ソース生成の有無にかかわらず共通です。

> **トリミング / Native AOT**: コンテキストを注入しないと設定が空（`{}`）になります。 コンテキストは `Settings` を参照する側のアセンブリに置いてください（同一コンパイル内では MVVM のソース生成結果を JSON のソース生成が見られないため）。

## `Services.EsRectIntJsonConverter`（内部・クラス）

`EsRectInt` は読み取り専用プロパティ（`Right` 等）を持つため、専用コンバーターで `X` / `Y` / `Width` / `Height` のみを読み書きします。

- 読み込み時に欠落したプロパティは 0、未知のプロパティは無視します。
- 既存の設定ファイルをそのまま読み込めます。

## `Services.IImageWriter`（インターフェース）

画像（`EsBitmap`）をファイルへ書き込むサービスの抽象です。

| メソッド | 説明 |
| --- | --- |
| `Save(EsBitmap image, string filePath)` | 画像を指定パスへ書き込む（拡張子から形式を自動判別） |

- UI フレームワーク非依存（`EsBitmap` のみに依存）です。 具象実装（Avalonia 版は `AvaloniaImageWriter`）が各 UI プロジェクト側でエンコードします。
- テストではモックに差し替え、ヘッドレス不要で検証できます。

## `Models.FileNameTemplate`（静的クラス）

| メソッド | 説明 |
| --- | --- |
| `Generate(string template, DateTime? now = null)` | テンプレートから実際のファイル名を生成する（`{ }` 内の日時トークンを置換する。 `now` を省略すると現在日時を使う） |
| `IncrementRightmostNumber(string text)` | 右端の数値連続を +1 する（桁数を維持。 `decimal` で扱えない桁数は `BigInteger`） |
| `IsValidTemplate(string template, out string? errorMessage)` | ファイル名として使用可能かを検証する（使用不可文字を検出） |

日時を引数（`now`）で受け取るのは、呼び出し側が時刻を固定して結果を再現できるようにするためです（本番の呼び出しは現在日時を使います）。

### テンプレートの書式

| 記法 | 意味 |
| --- | --- |
| `{date}` / `{time}` | `yyyyMMdd` / `HHmmss` の省略形 |
| `{yyyy}` `{MM}` `{dd}` `{HH}` `{mm}` `{ss}` | 個別の日時トークン |
| `{{` / `}}` | リテラルの `{` / `}` |

## `Models.UndoHistory`（クラス）

| メンバー | 説明 |
| --- | --- |
| `MaxCount` | Undo の最大保存段数（5） |
| `Count` / `RedoCount` | 現在の Undo / Redo スタック深度 |
| `IsRestoring` | Undo 復元中かどうか（復元中の `Push` を無視するために使用） |
| `GetUndoHistory()` / `GetRedoHistory()` | スタック内の画像を古い順に取得する（読み取り専用。 変更・破棄してはならない） |
| `Push(EsBitmap? currentImage)` | 現在の画像を Undo スタックへプッシュする（画素コピーで複製。 Redo 履歴はクリア） |
| `Undo(EsBitmap? currentImage)` | 現在の画像を Redo スタックへ退避し、Undo スタックの最新画像を返す（所有権は呼び出し側） |
| `Redo()` | Redo スタックから最新の画像を返す（所有権は呼び出し側） |
| `Clear()` | 全画像を破棄して両スタックを空にする |

- スレッドセーフではありません（UI スレッドからのみ操作します）。

## `Models.HotKeyParser`（静的クラス）

| メソッド | 説明 |
| --- | --- |
| `TryParse(string text, out HotKeySetting setting)` | ホットキー文字列を `HotKeySetting` へ変換する（成功時 true） |

- "Ctrl + Shift + Q" / "Ctrl+Shift+Q" 形式に対応します。 大文字小文字は区別しません。
- 最低 1 つの修飾キーが必要です。 キー名が解釈できない場合は false を返します。

## `Models.CapacityLimitedCache`（内部・静的クラス）

| メソッド | 説明 |
| --- | --- |
| `GetOrAdd<TKey, TValue>(ConcurrentDictionary<TKey, TValue> cache, int capacity, TKey key, Func<TKey, TValue> factory)` | キャッシュに値を取得または追加する。 容量超過時はキャッシュ全体をクリアして肥大化を防ぐ |

## `Models.ImageFileExtensions`（内部・静的クラス）

フォルダビューの一覧に含める画像ファイルの拡張子判定。 判定は拡張子のみで行い、大文字・小文字は区別しません。

| メンバー | 説明 |
| --- | --- |
| `All` | 対象拡張子の一覧（読み取り専用）。 `.png` / `.jpg` / `.jpeg` / `.bmp` / `.gif` / `.tiff` / `.tif` / `.webp` |
| `IsImageFile(string path)` | 拡張子が対象のいずれかなら true |

- 拡張子の抽出には `Path.GetExtension` を使用するため、`.png` のようにドットで始まる名前も画像として扱われます（末尾のドット `a.` は空文字のため対象外）。

## `ViewModels.ViewModeDisplay`（内部・静的クラス）

| メンバー | 説明 |
| --- | --- |
| `Count` | 表示モードの数（有効なインデックスは `0` 〜 `Count - 1`） |
| `GetText(int index)` | 表示モード（特大 / 大 / 中）の表示文字列を返す |
| `ClampIndex(int index)` | 表示モードのインデックスを有効範囲へクランプする |
| `GetItemCountText(int count)` | 件数の表示文字列を返す |

## `ViewModels.FolderViewViewModel`（クラス）

フォルダビューの表示モード・ソート順の状態を保持し、変更を通知します（`ObservableObject`）。

| メンバー | 説明 |
| --- | --- |
| `ItemCount` / `ItemCountText` | 項目数と表示文字列 |
| `SelectedViewModeIndex` / `ViewModeText` | 表示モードのインデックスと表示文字列 |
| `IsSortAscending` / `SortStatusText` | ソート順（昇順 / 降順）と表示文字列 |
| `FolderPathText` | フォルダパスの表示文字列 |

## `ViewModels.UndoViewModel`（クラス）

Undo 一覧の表示モード状態を保持し、変更を通知します（`ObservableObject`）。

| メンバー | 説明 |
| --- | --- |
| `ItemCount` / `ItemCountText` | 項目数と表示文字列 |
| `SelectedViewModeIndex` / `ViewModeText` | 表示モードのインデックスと表示文字列 |

## `ViewModels.MainViewModel`（クラス）

メイン画面の状態を保持し、コマンドと変更通知を提供します（`ObservableObject` + `[RelayCommand]` / `[ObservableProperty]`）。

### コンストラクター

```csharp
public MainViewModel(ISettingsService settingsService, IImageWriter imageWriter)
```

`IImageWriter` は UI 実装を注入します（Avalonia 版は `App.AvaloniaUI` の `AvaloniaImageWriter`。 `Program.cs` の DI で登録）。 既定実装の自動生成は行いません（UI 非依存を保つため）。

### 主なプロパティ

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `PreviewImage` | `EsBitmap?` | プレビュー画像（所有権は本 ViewModel。 差し替え時に旧画像を破棄） |
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

### 主な操作

| メンバー | 説明 |
| --- | --- |
| `SetPreviewImage(EsBitmap)` | プレビュー画像を設定する（所有権が本 ViewModel へ移る） |
| `SaveImageAsync()` | プレビュー画像を保存する（テンプレートからファイル名を生成。 同名時は `ConfirmOverwriteAsync`） |
| `CropPreviewCommand` / `ConfirmAutoCropCommand` | 矩形で切り出す（画像座標 `EsRectInt`） |
| `AutoCropWindowCommand` / `CancelAutoCropCommand` | 窓枠の自動検出（`LogicLineDetector` + `LineRectangleReconstructor`） |
| `HitDetectedRect(double x, double y)` | 指定座標（画像ピクセル）を含む検出矩形のうち面積が最小のものを返す（無ければ null）。 両 UI のホバー強調と確定・キャンセル判定が共用する |
| `CopyToClipboard()` / `PasteFromClipboard()` | クリップボード操作（実体はコールバックへ委譲） |

### UI 依存の注入点

| メンバー | 型 | 既定 |
| --- | --- | --- |
| `CopyToClipboardAsync` | `Func<EsBitmap, Task<bool>>?` | null（未設定時は何もしない） |
| `PasteFromClipboardAsync` | `Func<Task<EsBitmap?>>?` | null |
| `ConfirmOverwriteAsync` | `Func<string, Task<bool>>?` | null（未設定時は上書きしない） |
| `BeforeEditAction` | `Action?` | null |
| `StartSelectionMode` | `Action<CaptureType>?` | null |

## 依存関係

| 依存 | 用途 |
| --- | --- |
| `EsUtil.Platform.Core`（`EsUtil.Platform.Core.*`） | `EsBitmap`（画像の保持・切出し）・`EsRectInt` / `EsPoint`（座標）・`HotKeySetting` / `HotKeyModifiers` / `EsKey`（ホットキー解析）・`EsWindowState`（設定モデル）・`CaptureType`（キャプチャ種別） |
| `LineDetection`（`EsUtil.SaveEvidence.Parts.LineDetection`） | 窓枠の自動検出（`LogicLineDetector`）と矩形復元（`LineRectangleReconstructor`） |
| `CommunityToolkit.Mvvm` | `ObservableObject` / `[RelayCommand]` / `[ObservableProperty]` |

UI フレームワーク（Avalonia / MewUI）への依存はありません。
