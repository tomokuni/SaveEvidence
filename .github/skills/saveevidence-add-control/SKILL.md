---
name: saveevidence-add-control
description: SaveEvidence にコントロールを追加するときの順序（可搬性の判定 → 手順の正本を読む → コアと 4 UI を作る → 一覧を持つ場所を埋める → 1 回だけ検証）。「コントロールを追加する」「Parts を増やす」「4 UI へ広げる」のときに使う。手順の本文は持たず src/README.md と各規約を指す。
---

# コントロール追加の順序（規則は持たない）

## 前提

- 層と置き場所の所有者は [src/README.md](../../../src/README.md) §2〜§5 である。
- 可搬性の所有者は [依存関係.md](../../依存関係.md)（§2・§4）と [copilot-instructions.md](../../copilot-instructions.md) §3.1（プロジェクトの一覧）である。

## 手順

1. 可搬性を判定する（隣のリポジトリ `EsUtil` へ出すか、本リポジトリの `Parts.*` に置くか）。 判断は `saveevidence-layer-placement` の順序で行う。
2. [src/README.md](../../../src/README.md) §2〜§5 を読む。
3. コア → テスト → UI コントロールの順に作る（UI の数と構成は 2 で読んだ §5 の表に従う）。
4. 一覧を持つ場所を埋める（下表）。
5. 文書は [ドキュメント規約.md](../../ドキュメント規約.md) に従って書く（`README.md` / `docs/SPEC_API.md` / `docs/SPEC_System.md`）。
6. `saveevidence-verify` の順序で 1 回だけ検証する。

## 一覧を持つ場所（規則は所有者の節を読む）

| 場所 | 所有者 |
| --- | --- |
| `src/README.md` の層の表 | [src/README.md](../../../src/README.md) §2〜§5 |
| `copilot-instructions.md` の構成表 | [copilot-instructions.md](../../copilot-instructions.md) §3.1 |
| `.vscode/tasks.json` / `.vscode/launch.json` | [検証手順.md](../../検証手順.md) §17 |
| 配布物の一覧（`release-config.json`） | [RELEASE.md](../../RELEASE.md) |
| コアが `Parts.CapturePreview.*` に収録されるか | [src/README.md](../../../src/README.md) §5 |

## 報告の形

追加した型と層、可搬性の判定の根拠、4 UI の対応、埋めた一覧、検証の合否と省略した検査。
