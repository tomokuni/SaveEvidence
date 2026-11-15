# SaveEvidence

**画面キャプチャユーティリティ** — 画面の任意領域・ウィンドウをキャプチャし、連番ファイルとして保存します。

![Release](https://github.com/tomokuni/SaveEvidence/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/SaveEvidence/actions/workflows/build.yml/badge.svg?event=pull_request)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-11.0-512BD4)
![Avalonia](https://img.shields.io/badge/UI-Avalonia-8D46D6)
![MewUI](https://img.shields.io/badge/UI-MewUI-00BFFF)
![WinUI3](https://img.shields.io/badge/UI-WinUI%203-0078D4)
![WinForms](https://img.shields.io/badge/UI-WinForms-512BD4)
![License](https://img.shields.io/badge/License-MIT-3DA639)

---

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール方法](#4-インストール方法)
  - [4.1 配布物の実行](#41-配布物の実行)
  - [4.2 ソースからのビルド](#42-ソースからのビルド)
  - [4.3 検証（変更をコミットする前）](#43-検証変更をコミットする前)
- [5. クイックスタート](#5-クイックスタート)
- [6. 設定ファイルとログ](#6-設定ファイルとログ)
- [7. アーキテクチャ概要](#7-アーキテクチャ概要)
- [8. ドキュメント](#8-ドキュメント)
- [9. トラブルシューティングと問い合わせ](#9-トラブルシューティングと問い合わせ)
- [10. 開発への参加](#10-開発への参加)
- [11. ライセンス](#11-ライセンス)

---

## 1. 概要

SaveEvidence は Windows デスクトップの画面をキャプチャし、プレビュー・編集・保存するアプリです。  スクリーン全体・特定ウィンドウ・任意の矩形領域の 3 種類を、グローバルホットキーから即座に開始できます。

**Avalonia UI / MewUI / WinUI 3 / WinForms の 4 つの UI 実装**を持ち、コア（設定・保存・履歴・撮影制御）を共有します。  設定ファイルは 4 つで共通、ログは実行ファイルごとに分かれます。

## 2. 機能一覧

詳細（操作方法・設定項目・ショートカット）は各 UI の README が持ちます。

| 機能 | 内容 | 詳細 |
| --- | --- | --- |
| 画面キャプチャ | スクリーン選択 / ウィンドウ選択 / 範囲選択（既定 `Ctrl + Shift + Q` / `W` / `E`、設定で変更可） | [Avalonia UI](src/App.AvaloniaUI/README.md) · [MewUI](src/App.MewUI/README.md) · [WinUI 3](src/App.WinUI3/README.md) · [WinForms](src/App.WinForms/README.md) |
| プレビューと編集 | 切り出し（8 方向ハンドル）・自動ウィンドウ検出切り出し・ズーム / パン・Fit・ルーペ | 同上（[ズーム・切り出しのコントロール仕様](src/Parts.CapturePreview.ControlAvaloniaUI/docs/SPEC_API.md)） |
| クリップボード | 画像のコピー / 貼り付け（`Ctrl + C` / `Ctrl + V`）。 撮影・切り出し後の自動コピーも設定可 | 同上 |
| 履歴 | アンドゥ / リドゥ（最大 5 段階。 `Ctrl + Z` / `Ctrl + Y`）。  Undo 一覧ページ | 同上 |
| 保存 | ファイル名テンプレート（日時プレースホルダ・オートインクリメント・検証）・上書き確認・自動保存・保存先フォルダ | [ファイル名テンプレート](src/App.AvaloniaUI/README.md#81-ファイル名テンプレート) |
| 一覧 | フォルダビュー（サムネイル・表示モード・ソート・ごみ箱へ移動） | [Avalonia UI](src/App.AvaloniaUI/README.md) |
| 外観 | テーマ（ライト / ダーク / システム）・選択枠 / 暗転 / ハンドル / ルーペ / ラベルの色と寸法 | [設定項目の対応表](src/App.AvaloniaUI/docs/SPEC_SettingTargetTable.md) |
| タスク バー | 撮影時にタスク バーを隠す（ツールバーと設定ページで共通の設定） | 各 UI の README |

## 3. システム要件

| 項目 | 要件 |
| --- | --- |
| **OS** | Windows Server 2022 / Windows 11（ビルド 20348）以降 |
| **.NET ランタイム** | .NET 11.0 以降（自己完結の配布物は同梱のため不要） |
| **アーキテクチャ** | x64 |

## 4. インストール方法

### 4.1 配布物の実行

[Releases](https://github.com/tomokuni/SaveEvidence/releases) の zip を展開し、同じフォルダーの中身をまとめて配置してください。

| UI | 実行ファイル | 構成 |
| --- | --- | --- |
| MewUI 版 | `SaveEvidence.MewUI.exe` | exe + ネイティブ DLL（Native AOT） |
| Avalonia UI 版 | `SaveEvidence.AvaloniaUI.exe` | exe + ネイティブ DLL（Native AOT） |
| WinForms 版 | `SaveEvidence.WinForms.exe` | exe 1 個（自己完結・単一ファイル） |
| WinUI 3 版 | `SaveEvidence.WinUI3.exe` | **AOT 版**: exe + ネイティブ DLL / `.pri`（Windows App Runtime 2.6.5 以降が必要） / **Single 版**: exe 1 個（Windows App Runtime と .NET を同梱） |

### 4.2 ソースからのビルド

```powershell
git clone https://github.com/tomokuni/SaveEvidence.git
cd SaveEvidence
# UI ごとの配布物を作る（方式は可能なものが自動で選ばれる）
cmd /c buildScript\publish_AvaloniaUI.bat
cmd /c buildScript\publish_MewUI.bat
cmd /c buildScript\publish_WinUI3.bat
cmd /c buildScript\publish_WinForms.bat
```

出力先は `publish/<UI>-win-x64-<方式>` です（例: `publish/avaloniaui-win-x64-aot`。  **WinUI 3 版だけは AOT と Single の 2 形態**）。  方式の選択と制約は [`.github/RELEASE.md`](.github/RELEASE.md) が単一所有します。

### 4.3 検証（変更をコミットする前）

```powershell
pwsh -NoProfile -File .github/verify/verify.ps1
```

ビルド・テスト・文書・配布物の起動をまとめて確かめます。  合否基準は [`.github/検証手順.md`](.github/検証手順.md) が単一所有します（CI も同じスクリプトを Release 構成で実行します）。

## 5. クイックスタート

1. 使う UI の実行ファイルを起動します（前回のウィンドウ位置・サイズが復元されます）。
2. ツールバーのキャプチャ ボタン、またはグローバルホットキーで撮影します（撮影中はメイン ウィンドウが隠れます。  `Esc` でキャンセル）。
3. プレビューで切り出し・ズームを行います。
4. 「保存」ボタンまたは `Ctrl + S` で保存します（保存後にファイル名の数値が +1 されます）。

## 6. 設定ファイルとログ

- **設定**（4 つの UI で共有）: `%LOCALAPPDATA%\SaveEvidence\settings.json`（変更は即時保存。  壊れた場合は削除すると既定値で再作成されます）。
- **ログ**（実行ファイルごと）: `%LOCALAPPDATA%\SaveEvidence\logs\<アセンブリ名>.<yyyyMMdd>.log`（例: `SaveEvidence.WinUI3.20261109.log`。  日次ローテーション・7 日保持）。

## 7. アーキテクチャ概要

UI 非依存のコア（`App.Core`）と撮影スタック（`Parts.CapturePreview.*`）を 4 つの UI が共有します。  層の帰属と依存の向きは [`src/README.md`](src/README.md) が単一所有します。

```text
App.AvaloniaUI / App.MewUI / App.WinUI3 / App.WinForms
        │
        ├── Parts.CapturePreview.Control<UI>（撮影セッション・切り出しビュー）
        ├── App.Core（設定・保存・履歴・表示用 ViewModel）
        └── Parts.CapturePreview.Core（撮影・切り出し・ズームの規則）
```

## 8. ドキュメント

| 文書 | 役割 |
| --- | --- |
| [`src/README.md`](src/README.md) | 層構成（Core / Presenter / UI）と置き場所の規則 |
| [`src/App.Core/docs/SPEC_API.md`](src/App.Core/docs/SPEC_API.md) | コアの公開 API と設定モデル |
| 各プロジェクトの `README.md` / `docs/SPEC_*.md` | 利用仕様・API・システム仕様（[App.AvaloniaUI](src/App.AvaloniaUI/README.md) など） |
| [`.github/`](.github) | 規約（コーディング・命名・テスト・ログ・文書・コミット・検証・リリース） |
| [`CHANGELOG.md`](CHANGELOG.md) | 変更履歴 |

## 9. トラブルシューティングと問い合わせ

| 問題 | 原因と対処 |
| --- | --- |
| ホットキーが反応しない | 他のアプリが同じ組み合わせを使用しています。  設定画面で別のキーへ変更してください。 |
| 設定ファイルが壊れた | `settings.json` を削除して再起動すると、既定値で再作成されます。 |
| 不具合の調査 | 上記のログ（`%LOCALAPPDATA%\SaveEvidence\logs\`）を添えて [Issues](https://github.com/tomokuni/SaveEvidence/issues) へ報告してください。  起動時に版・環境・設定の読み込み結果が記録されます。 |

## 10. 開発への参加

- 開発は `dev` ブランチで行い、Pull Request で `main` へマージします（[`.github/コミット規約.md`](.github/コミット規約.md)）。
- コミット前に [`.github/verify/verify.ps1`](.github/verify/verify.ps1) を通してください（[`.github/検証手順.md`](.github/検証手順.md)）。
- コード・文書の規約は [`.github/copilot-instructions.md`](.github/copilot-instructions.md) から辿れます。

## 11. ライセンス

[MIT License](LICENSE)
