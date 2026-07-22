# システム詳細仕様書 SpecSystem.md

**SaveEvidence** — 画面キャプチャユーティリティ

---

## 目次

- [1. システム概要](#1-システム概要)
- [2. 技術スタック](#2-技術スタック)
- [3. アーキテクチャ](#3-アーキテクチャ)
- [4. 主要モジュール構成](#4-主要モジュール構成)
- [5. 設定システム](#5-設定システム)
- [6. 例外処理ポリシー](#6-例外処理ポリシー)
- [7. パフォーマンス最適化施策](#7-パフォーマンス最適化施策)
- [8. Win32 API 連携](#8-win32-api-連携)
- [9. ホットキーシステム](#9-ホットキーシステム)
- [10. 画像処理詳細](#10-画像処理詳細)
- [11. ズームシステム](#11-ズームシステム)
- [12. CropSelection システム](#12-cropselection-システム)
- [13. アンドゥシステム](#13-アンドゥシステム)
- [14. セキュリティ](#14-セキュリティ)
- [15. ビルド・配置](#15-ビルド配置)

---

## 1. システム概要

SaveEvidence は、Windows デスクトップ上の画面をキャプチャし、プレビュー・編集・保存するための Windows Forms アプリケーションです。  
.NET 10.0 をターゲットとし、CommunityToolkit.Mvvm による MVVM ライクなアーキテクチャを採用しています。

---

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| **言語** | C# 14（.NET 10.0） |
| **UI フレームワーク** | Windows Forms |
| **MVVM フレームワーク** | CommunityToolkit.Mvvm 8.x（ソースジェネレーター） |
| **ターゲットフレームワーク** | `net10.0-windows10.0.17763.0` |
| **ビルドツール** | MSBuild / .NET SDK 10.0 |
| **シリアル化** | System.Text.Json（設定ファイル） |
| **Win32 API** | user32.dll, dwmapi.dll, Shell32.dll |
| **ランタイム** | .NET 10.0（自己完結型配置対応） |

---

## 3. アーキテクチャ

### 3.1 レイヤー構造

```text
┌──────────────────────────────────────────────────────┐
│  View Layer（フォーム）                               │
│  MainForm / SelectionForm / SettingsForm / HotkeyForm │
│  FolderViewForm                                      │
├──────────────────────────────────────────────────────┤
│  ViewModel Layer                                     │
│  MainViewModel / FolderViewViewModel                 │
├──────────────────────────────────────────────────────┤
│  Model / Service Layer                               │
│  CaptureManager / HotKeyManager / Settings            │
│  ImageProcessor / FileNameTemplate / CropSelection    │
│  SettingsService (ISettingsService)                   │
└──────────────────────────────────────────────────────┘
```

### 3.2 レイヤー間の依存関係

- **View** は **ViewModel** に依存する（DataBindings とイベントハンドラ経由）
- **ViewModel** は **Model** と **Service** に依存する
- **Model** は他のレイヤーに依存しない（独立したビジネスロジック）
- **Service** は **Model** に依存する（設定の永続化など）
- 依存性逆転の原則（DIP）に基づき、サービスはインターフェース（`ISettingsService`）を介して注入される

### 3.3 データフロー

#### 3.3.1 キャプチャフロー

```text
ユーザー操作（ボタン or ホットキー）
       │
       ▼
MainForm.ExecuteCapture()
       │
       ├── シングルスクリーン → CaptureManager.CaptureArea()
       │
       └── マルチスクリーン / ウィンドウ / 範囲
              │
              ▼
       SelectionForm（モーダル表示）
              │
              ├── SelectionCompleted → CaptureManager.CaptureArea() or CaptureWindow()
              │
              └── Cancelled → 何もしない
                      │
                      ▼
              MainViewModel.SetPreviewImage()
                      │
                      ▼
              View に PropertyChanged 通知 → UpdatePreviewImage()
```

#### 3.3.2 保存フロー

```text
MainViewModel.SaveImage()
       │
       ├── FileNameTemplate.Generate() → ファイル名生成
       ├── 上書き確認ダイアログ（同名ファイル存在時）
       ├── 保存先フォルダ作成（存在しない場合）
       ├── 拡張子から ImageFormat 判定（PNG/JPEG/BMP/GIF）
       ├── PreviewImage.Save() → ファイル書き出し
       └── FileNameTemplate.IncrementRightmostNumber() → 数値インクリメント
```

#### 3.3.3 設定ファイルフロー

```text
起動時
  └── SettingsService コンストラクタ → LoadInternal() → JSON デシリアライズ
       └── ファイルがない or 読込失敗 → デフォルト設定

稼働中
  └── ViewModel が Settings のプロパティを変更
       └── ViewModel から SettingsService.Save() を呼び出し
            └── SaveInternal() → JSON シリアライズ → ファイル書き出し

終了時
  └── MainForm.FormClosing → SettingsService.Save() で最終状態を保存
```

---

## 4. 主要モジュール構成

### 4.1 Enum（列挙型）

| ファイル | 列挙型 | 説明 |
| --- | --- | --- |
| `CaptureType.cs` | `CaptureType` | キャプチャ種別（SelectScreen / WindowSelect / AreaSelect） |
| `HotKeyModifiers.cs` | `HotKeyModifiers` | ホットキー修飾キーのフラグ（None / Alt / Control / Shift / Windows） |
| `LoupeMode.cs` | `LoupeMode` | ルーペ表示モード（Show / Hide / Auto） |
| `WindowCaptureMode.cs` | `WindowCaptureMode` | ウィンドウキャプチャ方式（PrintWindow / CopyFromScreen） |

### 4.2 Models（モデル）

| ファイル | クラス | 責務 |
| --- | --- | --- |
| `CaptureManager.cs` | `CaptureManager`（static partial） | Win32 API を使用した画面・ウィンドウキャプチャ |
| `HotKeyManager.cs` | `HotKeyManager`（IDisposable） | グローバルホットキーの登録・解除・管理 |
| `HotKeySetting.cs` | `HotKeySetting`（record） | ホットキー設定の値オブジェクト |
| `Settings.cs` | `Settings`（ObservableObject） | アプリケーション設定のデータモデル |
| `ImageProcessor.cs` | `ImageProcessor`（static） | 画像切り出し・ウィンドウ領域自動検出 |
| `FileNameTemplate.cs` | `FileNameTemplate`（static partial） | ファイル名テンプレート解析・生成 |
| `CropSelection.cs` | `CropSelection` | プレビュー切り出し選択状態の管理 |

### 4.3 ViewModels

| ファイル | クラス | 責務 |
| --- | --- | --- |
| `MainViewModel.cs` | `MainViewModel`（ObservableObject） | メイン画面の状態管理・コマンド処理 |
| `FolderViewViewModel.cs` | `FolderViewViewModel`（ObservableObject） | フォルダビューアーの状態管理 |

### 4.4 Views

| ファイル | クラス | 責務 |
| --- | --- | --- |
| `MainForm.cs` | `MainForm` | メインウィンドウ（キャプチャ・プレビュー・編集・保存） |
| `MainForm.Designer.cs` | `MainForm`（partial） | メインフォームのコントロール定義 |
| `SelectionForm.cs` | `SelectionForm` | キャプチャ領域選択用モーダルフォーム |
| `SettingsForm.cs` | `SettingsForm` | 表示設定モーダルダイアログ |
| `HotkeyForm.cs` | `HotkeyForm` | グローバルホットキー設定モーダルダイアログ |
| `FolderViewForm.cs` | `FolderViewForm` | 保存先フォルダ内容表示モードレスダイアログ |

### 4.5 Services

| ファイル | クラス | 責務 |
| --- | --- | --- |
| `ISettingsService.cs` | `ISettingsService`（interface） | 設定管理サービスのインターフェース |
| `SettingsService.cs` | `SettingsService` | JSON ファイルによる設定の永続化 |

---

## 5. 設定システム

### 5.1 設定ファイルの仕様

| 項目 | 仕様 |
| --- | --- |
| **ファイルパス** | `%LOCALAPPDATA%\SaveEvidence\settings.json` |
| **形式** | UTF-8 JSON（整形出力、コメント許容、末尾カンマ許容） |
| **排他制御** | `Lock` によるスレッドセーフな読み書き |
| **読み込みタイミング** | アプリ起動時、`Reload()` メソッド呼び出し時 |
| **保存タイミング** | ホットキー変更時、ファイル名テンプレート変更時、保存先フォルダ変更時、フォーム終了時 |

### 5.2 設定項目一覧

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectScreenHotKey` | `HotKeySetting` | Ctrl+Shift+Q | スクリーン選択キャプチャのホットキー |
| `WindowSelectHotKey` | `HotKeySetting` | Ctrl+Shift+W | ウィンドウ選択キャプチャのホットキー |
| `AreaSelectHotKey` | `HotKeySetting` | Ctrl+Shift+E | 範囲選択キャプチャのホットキー |
| `FileNameTemplate` | `string` | `screenshot_{date}_{time}.png` | ファイル名テンプレート |
| `SaveFolderPath` | `string` | `%USERPROFILE%\Pictures\SaveEvidence` | 保存先フォルダパス |
| `FolderViewModeIndex` | `int` | 1（大） | フォルダビューアー表示モード |
| `FolderSortAscending` | `bool` | true | フォルダビューアーソート順 |
| `MainFormBounds` | `Rectangle?` | null | メインフォームのウィンドウ位置・サイズ |
| `MainFormWindowState` | `FormWindowState` | Normal | メインフォームのウィンドウ状態 |
| `FolderViewFormBounds` | `Rectangle?` | null | フォルダビューアーのウィンドウ位置・サイズ |
| `FolderExtraLargeIconSize` | `int` | 320 | 特大アイコンサイズ（ピクセル） |
| `FolderLargeIconSize` | `int` | 240 | 大アイコンサイズ（ピクセル） |
| `FolderMediumIconSize` | `int` | 160 | 中アイコンサイズ（ピクセル） |
| `CenterAlign` | `bool` | true | プレビュー画像中央寄せ |
| `CaptureBorderColor` | `string` | `White` | キャプチャ選択枠色 |
| `CropBorderColor` | `string` | `White` | 切り出し選択枠色 |
| `LoupeCrossColor` | `string` | `Red` | ルーペ十字線色 |
| `LoupeFrameColor` | `string` | `White` | ルーペ外枠色 |
| `LoupeFrameWidth` | `int` | 2 | ルーペ外枠太さ（ピクセル） |
| `LoupeZoomLevel` | `int` | 8 | ルーペ拡大率 |
| `LoupeSize` | `int` | 170 | ルーペサイズ（ピクセル） |
| `LoupeModeValue` | `LoupeMode` | Hide | ルーペ表示モード |
| `CaptureMode` | `WindowCaptureMode` | PrintWindow | ウィンドウキャプチャ方式 |

---

## 6. 例外処理ポリシー

### 6.1 グローバル例外ハンドラ

```csharp
// Program.cs
Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
```

- UI スレッドとバックグラウンドスレッドの両方の未処理例外をキャッチ
- 例外内容はコンソールに出力

### 6.2 各レイヤーの例外処理

| レイヤー | 方針 |
| --- | --- |
| **View** | ユーザー操作起因の例外は各イベントハンドラ内でキャッチし、catch ブロック内で無視（UI の応答性を維持） |
| **ViewModel** | ファイル保存・画像処理の例外は catch し、`false` または空値を返す |
| **Model** | Win32 API 呼び出しの例外は呼び出し元で処理。PrintWindow の失敗は自動フォールバック |
| **Service** | 設定ファイル読み書きの例外は catch し、デフォルト値で続行 |

### 6.3 キャプチャ中の例外

- キャプチャ動作中に例外が発生した場合、画面の暗転等を元に戻し、Windows の操作が可能な状態に復帰
- `_isExecutingCapture` フラグで二重起動を防止

---

## 7. パフォーマンス最適化施策

### 7.1 キャプチャ処理

- **PrintWindow 方式の優先使用**: DWM から直接描画内容を取得し、CopyFromScreen より高速
- **3 段階フォールバック**: PrintWindow（フラグ 0）→ PW_RENDERFULLCONTENT → PW_CLIENTONLY → CopyFromScreen
- **ビットマップ検証**: 5 箇所サンプリングによる有効性チェックで無駄な後処理を防止

### 7.2 画像処理

- **サンプリング走査**: ウィンドウ領域検出時に 20 ピクセル間隔でサンプリングし、全ピクセル検査を回避
- **類似色判定**: RGB 差分合計 30 未満を「類似」とみなす許容値設定でグラデーション・アンチエイリアスの影響を排除

### 7.3 ファイル名テンプレート

- **`[GeneratedRegex]`**: ソースジェネレーターによる正規表現のコンパイル時最適化
- **`decimal` → `BigInteger` の段階的パース**: 通常の数値は高速な decimal で処理し、巨大数値のみ BigInteger にフォールバック

### 7.4 フォルダビューアー

- **シェルアイコン 2 段階表示**: まず OS のシェルアイコンを高速表示し、その後非同期でサムネイルを差し替え
- **非同期サムネイル読み込み**: `Task.Run` によるバックグラウンド処理で UI スレッドのブロッキングを防止
- **高品質ダウンスケール**: ソースサイズを表示サイズ×2 で読み込み、`HighQualityBicubic` 補間で高品質な縮小を実現
- **アイコンキャッシュ**: 拡張子ベースの辞書キャッシュで同一拡張子のアイコン取得を最適化
- **キャンセル機構**: `CancellationTokenSource` でフォルダ再読み込み時の前回処理を中断
- **ImageList ハンドル強制作成**: 非同期追加時の NullReferenceException を防止

### 7.5 ズーム・描画

- **ダブルバッファリング**: プレビューパネルに DoubleBuffered を有効化し、描画のチラつきを防止
- **クリッピング**: 画像領域のみに描画範囲を制限し、余分な描画処理を削減

### 7.6 リリースビルド最適化

- **PublishSingleFile**: 単一ファイル実行可能ファイルの生成
- **ReadyToRun**: コンパイル済みコードによる起動時間短縮
- **SelfContained**: ランタイム同梱による配置の簡略化
- **アンマネージドコードのトリミング**: 不要コードの削減によるファイルサイズ最適化

---

## 8. Win32 API 連携

### 8.1 使用 Win32 API 一覧

| API | DLL | 用途 |
| --- | --- | --- |
| `GetWindowRect` | user32.dll | ウィンドウの矩形領域取得 |
| `WindowFromPoint` | user32.dll | 指定座標のウィンドウハンドル取得 |
| `GetCursorPos` | user32.dll | マウスカーソル位置取得 |
| `GetAncestor` | user32.dll | トップレベルウィンドウ取得 |
| `PrintWindow` | user32.dll | DWM からのウィンドウ描画取得 |
| `RegisterHotKey` | user32.dll | グローバルホットキー登録 |
| `UnregisterHotKey` | user32.dll | グローバルホットキー解除 |
| `DwmGetWindowAttribute` | dwmapi.dll | 半透明影を除外した可視矩形取得 |
| `SHGetFileInfo` | Shell32.dll | ファイル・フォルダのシェルアイコン取得 |
| `DestroyIcon` | user32.dll | アイコンハンドル破棄 |

### 8.2 内部構造体

| 構造体 | 用途 |
| --- | --- |
| `RECT` | Win32 RECT（Left, Top, Right, Bottom） |
| `POINT` | Win32 POINT（X, Y） |
| `SHFILEINFO` | シェルファイル情報 |

---

## 9. ホットキーシステム

### 9.1 登録・解除の流れ

```text
MainForm 起動
  └── HotKeyManager(Handle) 生成
       └── RegisterAll(settings)
            ├── Register(SelectScreenHotKey, CaptureType.SelectScreen)
            ├── Register(WindowSelectHotKey, CaptureType.WindowSelect)
            └── Register(AreaSelectHotKey, CaptureType.AreaSelect)

WM_HOTKEY 受信（WParam = 登録ID）
  └── MainForm.WndProc → HotKeyManager.ProcessHotKeyMessage()
       └── CaptureType を特定 → ExecuteCapture()

MainForm 破棄
  └── HotKeyManager.Dispose() → UnregisterAll()
```

### 9.2 ホットキー設定の制約

- 修飾キー（Ctrl/Alt/Shift/Win）の組合せ必須（単独キーのホットキーは登録不可）
- 既存のホットキーは上書き登録される（事前に Unregister）
- 同一キャプチャタイプに対して複数のホットキーは不可

---

## 10. 画像処理詳細

### 10.1 ウィンドウ領域自動検出アルゴリズム

1. 画像四隅のピクセル色を取得し、最頻色を背景色として決定
2. 上端から下方向に背景色と異なるピクセルが現れる行を探索
3. 下端から上方向に背景色と異なるピクセルが現れる行を探索
4. 左端から右方向に背景色と異なるピクセルが現れる列を探索
5. 右端から左方向に背景色と異なるピクセルが現れる列を探索
6. 検出領域が 10×10 ピクセル未満の場合は検出失敗として画像全体を返す

### 10.2 色の類似判定

```csharp
// RGB 各成分の差分合計が 30 未満 → 類似とみなす
var diff = Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
return diff < 30;
```

### 10.3 サポート画像形式

| 拡張子 | ImageFormat | 備考 |
| --- | --- | --- |
| `.png` | PNG | 既定の保存形式 |
| `.jpg` / `.jpeg` | JPEG | 非可逆圧縮 |
| `.bmp` | BMP | 無圧縮 |
| `.gif` | GIF | 256 色 |

---

## 11. ズームシステム

### 11.1 ズーム段階

```text
自動(フィット) → 25% → 33% → 50% → 67% → 75% → 80% → 90%
→ 100% → 110% → 125% → 150% → 175% → 200% → 250%
→ 300% → 400% → 500%
```

（0 = 自動、全 18 段階）

### 11.2 表示モード

| モード | 動作 |
| --- | --- |
| **自動フィット**（`_zoomPercent == 0`） | PictureBox を Zoom モードに設定し、パネルサイズに合わせて自動的に画像をフィット。中央寄せ設定時はパネル中央に配置 |
| **手動ズーム**（`_zoomPercent > 0`） | PictureBox を StretchImage モードに設定し、指定倍率で画像を拡大/縮小表示。スクロールバーで領域外をスクロール可能 |

---

## 12. CropSelection システム

### 12.1 8 方向ハンドル

```text
TopLeft       TopCenter       TopRight
      ┌──────────────────┐
      │                  │
MiddleLeft               MiddleRight
      │                  │
      └──────────────────┘
BottomLeft  BottomCenter  BottomRight
```

### 12.2 操作モード

| 操作 | トリガー | 動作 |
| --- | --- | --- |
| 新規選択 | 領域外で MouseDown + ドラッグ | 開始点から現在位置までの矩形 |
| リサイズ | ハンドル上で MouseDown + ドラッグ | 対応方向に矩形を変形 |
| 移動 | 領域内で MouseDown + ドラッグ | 矩形全体を平行移動 |
| 選択解除 | 領域外をクリック | SelectionRect = null |
| 無効化 | 5×5 ピクセル未満の選択 | 自動的に null に設定 |

---

## 13. アンドゥシステム

- 最大 5 段階のアンドゥを `List<Image>` で管理
- 切り出し実行前に `PushUndo()` で現在の画像を退避
- アンドゥ実行時にスタックから画像を取り出して復元
- 新規キャプチャ時にスタックをクリア

---

## 14. セキュリティ

- 信頼できない入力（クリップボードの画像）の検証は OS の種類に任せる
- ファイルパスは `Path.Combine` で安全に結合
- `GetInvalidFileNameChars()` によるファイル名の検証
- ディレクトリトラバーサル対策として標準的な .NET のパス処理に準拠

---

## 15. ビルド・配置

### 15.1 ビルド設定

| 構成 | 設定 |
| --- | --- |
| **Debug** | デバッグシンボル有効、最適化無効 |
| **Release** | 最適化有効、PublishSingleFile、SelfContained、ReadyToRun、トリミング有効 |

### 15.2 発行プロファイル

| プロファイル | 設定値 |
| --- | --- |
| `FolderProfile.pubxml` | 単一ファイル配置、自己完結型、x64、ReadyToRun 有効 |

### 15.3 発行コマンド

```powershell
dotnet publish src/app/app.csproj -c Release -o Publish
```
