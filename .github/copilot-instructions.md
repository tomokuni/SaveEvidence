# Custom Instructions

本リポジトリの規約は**主題ごとに 1 文書**へ分けている。 本ドキュメントは**索引**であり、規則を重複して記載しない（規則の所有者は各文書である）。

## 1. 規約の一覧（主題ごとの所有者）

| 主題 | 所有者 |
| --- | --- |
| コーディング（設計原則・記述スタイル・型設計・性能・非同期/UI・コメント・文章作法） | [`.github/コーディング規約.md`](コーディング規約.md) |
| 命名（名前空間・フォルダ・ファイル・型・メンバー・ログ名・テスト名・用語） | [`.github/命名規約.md`](命名規約.md) |
| テスト（xUnit v3・構造・網羅・ヘルパー・実行） | [`.github/テスト規約.md`](テスト規約.md) |
| ログ（レベル・接頭辞・所有者・書式・定義） | [`.github/ログ規約.md`](ログ規約.md) |
| ドキュメント（README / SPEC_API / SPEC_System の役割・配置・更新・記載範囲） | [`.github/ドキュメント規約.md`](ドキュメント規約.md) |
| 依存関係（プロジェクト参照の許可範囲） | [`.github/依存関係.md`](依存関係.md) |
| ビルド設定（MSBuild プロパティ・TFM） | [`.github/ビルド設定規約.md`](ビルド設定規約.md) |
| 例外処理（送出・捕捉・翻訳） | [`.github/例外処理規約.md`](例外処理規約.md) |
| 検証（ビルド・テスト・lint の手順と合否基準） | [`.github/検証手順.md`](検証手順.md) |
| コミット（メッセージ・ブランチ・タグ） | [`.github/コミット規約.md`](コミット規約.md) |
| リファクタリングの指針（よくある指摘） | [`.github/リファクタリング指針.md`](リファクタリング指針.md) |
| リリース（CI・バージョン・配布物） | [`.github/RELEASE.md`](RELEASE.md) |
| 他のリポジトリへの流用 | [`.github/REUSING.md`](REUSING.md) |
| 層の帰属（Core / Presenter / UI のどれに置くか） | [`../src/README.md`](../src/README.md) |

## 2. GitHub Copilot の動作に関する指示

- 回答は日本語で行う。
- GitHub に Pull Request は行わない。
- ファイルや出力を生成・編集する場合、文字コードは UTF-8 を使う。
- C# / .NET の専門家の視点で回答する。
- **提案が複数ある場合は、おすすめ順で並べて提示する**。
- コード変更は、Model / ViewModel / View の複数レイヤーを修正する必要がある場合でも実施する。
- **作業の前に、対象の規約（上表）を読む**。 規約と実装が食い違う場合は、**規約を先に更新してから**実装を直す。
- **実測してから書く**（推測でドキュメント・規約を書かない）。
- **検証は `pwsh -NoProfile -File buildScript/verify.ps1` を 1 回実行する**（合否基準は [`.github/検証手順.md`](検証手順.md) が所有する。 手順を手作業で書き起こさない）。

## 3. このワークスペース/ソリューションに固有の事項

**本節は「このリポジトリの事実（〜である）」だけを持つ**。 規範（〜する）は各規約が所有する（分担は [`ドキュメント規約.md`](ドキュメント規約.md) が定める）。

### 3.1 ソリューションの構成

SaveEvidence は Windows デスクトップの画面キャプチャ アプリである（実行ファイルは Avalonia UI 版・MewUI 版・WinUI 3 版・WinForms 版の 4 つ）。

**再利用可能な層（共通基盤と EsUtil 系のコントロール）は隣のリポジトリ [`EsUtil`](../../EsUtil/README.md) へ分離されている**。 本ソリューションはそれを**公開パッケージ（`PackageReference`）**で参照する（版は `EsUtilVersion` が単一所有する。 [`.github/依存関係.md`](依存関係.md) §2）。

| 区分 | プロジェクト | 所在 |
| --- | --- | --- |
| 共通基盤（可搬） | `EsUtil.Platform.Core` / `.Windows` | `../EsUtil` |
| 再利用可能なコントロール（可搬） | `EsUtil.Feature.Marquee.Core` / `.Presenter` / `EsUtil.Ui.<UI>.Marquee`、`EsUtil.Feature.Loupe.Core` / `.Presenter` / `EsUtil.Ui.<UI>.Loupe`、`EsUtil.Feature.Thumbnail.Core` / `.Presenter` / `EsUtil.Ui.<UI>.Thumbnail`（`<UI>` = `AvaloniaUI` / `MewUI` / `WinUI3` / `WinForms`）、`EsUtil.Ui.AvaloniaUI.ColorComboBox`（AvaloniaUI のみ） | `../EsUtil` |
| 画像処理（可搬） | `Parts.LineDetection` | 本リポジトリ |
| ブリッジ（可搬） | `EsUtil.Ui.AvaloniaUI.Interop` / `EsUtil.Ui.MewUI.Interop` / `EsUtil.Ui.WinUI3.Interop` / `EsUtil.Ui.WinForms.Interop` | `../EsUtil` |
| 撮影スタック（製品固有） | `Parts.CapturePreview.Core` / `.ControlAvaloniaUI` / `.ControlMewUI` / `.ControlWinUI3` / `.ControlWinForms` | 本リポジトリ |
| アプリ | `App.Core` / `App.AvaloniaUI` / `App.MewUI` / `App.WinUI3` / `App.WinForms` | 本リポジトリ |

**層の帰属（Core / Presenter / UI のどれに置くか）・ラベルと装飾の分担・名前空間とフォルダの関係は [`../src/README.md`](../src/README.md) が所有する**（本節は要約しない）。 EsUtil 側の層の帰属は [`../../EsUtil/EsUtil/README.md`](../../EsUtil/EsUtil/README.md) が所有する。

| 知りたいこと | 所有者 |
| --- | --- |
| プロジェクト間の参照の許可 | [`依存関係.md`](依存関係.md) |
| 名前空間・フォルダ・アセンブリ名の対応 | [`命名規約.md`](命名規約.md) |
| ログの接頭辞 | [`ログ規約.md`](ログ規約.md) |
| 対象フレームワークの割り当て | [`ビルド設定規約.md`](ビルド設定規約.md) |

### 3.2 このリポジトリ固有の事実

- **リポジトリは 2 つで、ワークスペースへ両方を載せる**（[`../SaveEvidence.code-workspace`](../SaveEvidence.code-workspace) が `../EsUtil` を含む）。 ライブラリ側の規約は [EsUtil の作業指示](../../EsUtil/.github/copilot-instructions.md) が所有する。
- **EsUtil は nuget.org の公開パッケージとして参照する**（版は [`Directory.Build.props`](../Directory.Build.props) の `EsUtilVersion`。 未公開の版を試すときだけ、その環境で取得元を足す）。 **ソースの `ProjectReference` はビルドの `DEP0007` が拒否する**（別リポジトリのソースの一致は保証できないため）。
- **パッケージの版は [`Directory.Packages.props`](../Directory.Packages.props) が単一所有し、復元は `packages.lock.json` で固定する**（**コミットする**）。 取得元は [`NuGet.config`](../NuGet.config) の `nuget.org` だけ（`<clear />` でユーザー単位の取得元を打ち消す）。
- **SBOM（CycloneDX）をリリースへ添付する**（`buildScript/generateSbom.ps1`。 ツールの版は [`.config/dotnet-tools.json`](../.config/dotnet-tools.json)）。 配布物から依存を辿れるようにするため。
- **アプリの製品バージョンは `<Version>+esutil.<EsUtilVersion>`**（使った EsUtil の版を exe から辿れるようにする。 SourceLink のコミットは SDK が後ろへ足す）。
- **実行ファイルは 2 つ**（`App.AvaloniaUI` / `App.MewUI`）で、**設定ファイルとログを共有する**（`%LOCALAPPDATA%\SaveEvidence\settings.json` と `%LOCALAPPDATA%\SaveEvidence\logs\`）。
- **実行ファイルの名前はプロジェクト名と一致しない**（`App.AvaloniaUI` → `SaveEvidence.AvaloniaUI.exe` / `App.MewUI` → `SaveEvidence.MewUI.exe`。 `<AssemblyName>` で製品名を明示する。 配布名は [`.github/release-config.json`](release-config.json) が単一所有する）。
- **文字コードは UTF-8（BOM なし）**で統一する（[`.editorconfig`](../.editorconfig) の `charset = utf-8`。 既存の `.cs` / `.md` / `.ps1` はすべて BOM なし）。
- **作業ツリーの改行コードは CRLF**（[`.editorconfig`](../.editorconfig) の `end_of_line = crlf`。 リポジトリのブロブは [`.gitattributes`](../.gitattributes) が LF へ正規化する）。
- **`App.AvaloniaUI`（ホスト）は `CropViewControl` の `Source` を設定し、`ZoomChanged` を購読してズーム率・可否を同期する**（切り出しビューが内包する `ZoomViewControl` へ委譲される）。
- **`AVLN3001` は `App.AvaloniaUI` だけが抑止する**（理由は csproj のコメントに記載。 [`検証手順.md`](検証手順.md) §1）。
- **CI は本リポジトリにリモートが無いため未実行**である（[`workflows/build.yml`](workflows/build.yml) はローカルで `buildScript/verify.ps1` を Release 構成で実行するのと等価）。
- **`EsUtil` 側の検証は EsUtil リポジトリで実行する**（本リポジトリの走査は `src/` 配下だけを対象にし、EsUtil の文書は走査しない）。
