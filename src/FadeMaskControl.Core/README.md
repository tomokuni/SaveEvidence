# FadeMaskControl.Core

`FadeMaskControl.AvaloniaUI`（Avalonia UI 実装）と `FadeMaskControl`（MewUI 実装）が共有する、**UI フレームワーク非依存の暗転オーバーレイ幾何**を提供するプロジェクトです。

## 目的

暗転オーバーレイの計算のうち、UI フレームワークに依存しない部分（穴あけ矩形の決定・暗転領域の帯分割・枠線矩形・論理座標から DIP への変換・ラベル文言・ラベルの行配置・サブラインの組み立て・**1 フレーム分の描画内容の決定**・描画状態変更のログ定義）を本プロジェクトへ切り出すことで、同じロジックを MewUI など別 UI からも再利用できます。

## 収録する型

| 型 | 公開 | 責務 |
| --- | --- | --- |
| `FadeMaskRenderMode` | `public` | オーバーレイ描画のモード（AreaSelect / ScreenSelect / WindowSelect / Crop） |
| `FadeMaskSubLine` | `public` | サイズラベルの下（2 行目以降）に表示する 1 行分の値（文言・フォントサイズ・文字色・背景色） |
| `FadeMaskAppearance` | `public` | 外観（暗転色・枠線・ラベル）の単一所有と `BuildSubLines`（ラベル行の組立） |
| `FadeMaskOverlayGeometry` | `internal` | 論理座標 → DIP 変換（閉区間の扱いを含む）・穴あけ矩形の決定・暗転領域（4 帯）の計算・枠線矩形の計算・座標変換ポートの決定（ポート優先・無ければ倍率） |
| `FadeMaskFrame` | `internal` | **1 フレーム分の描画内容**（暗転領域・枠線・サイズラベルの文言・サブライン）の決定。入力は `FadeMaskRequest` |
| `FadeMaskLabels` | `internal` | 情報ラベルの表示判定・操作ラベルの文言・サイズラベルの文言（`FormatSizeLabel`）・表示スケールの解決（`ResolveDisplayScaling`）・サブラインの組み立て（`BuildSubLines`）の単一所有 |
| `FadeMaskLabelStyle` | `internal` | ラベル 1 行分の外観（フォントサイズ・文字色・背景色）。フレームの受け渡し専用 |
| `FadeMaskLabelLayout` | `internal` | ラベルの行配置（内側余白 `Padding`・背景矩形・文字の描画原点・次の行の Y 座標）の単一所有 |
| `LogMessage` | `internal` | 描画状態変更（選択矩形・ハイライト矩形・座標変換ポート）の LoggerMessage 定義（両 UI で同一書式） |

`internal` の型は UI 実装（`FadeMaskControl` / `FadeMaskControl.MewUI`）とアプリ（`SaveEvidence.MewUI`）、テストからのみ `InternalsVisibleTo` で参照できます。

色は UI 非依存の `System.Drawing.Color`（`System.Drawing.Primitives`。Windows 専用の `System.Drawing.Common` ではない）で扱い、各 UI の色型への変換は境界で行います。

## 層の責務の境界

両 UI（Avalonia / MewUI）は「外観と状態を `FadeMaskRequest` へ詰めて `FadeMaskFrame.Resolve` を呼び、返されたフレームを描く」だけです。

| 責務 | 所有者 | 内容 |
| --- | --- | --- |
| 状態 | UI | 外観（`Appearance`）・選択矩形・ハイライト矩形・有効フラグ・タイトル・モード・全面暗転（フレームワークのプロパティ機構で保持する） |
| 判断・計算 | Core | 論理矩形 → DIP 変換（選択は閉区間 +1 / ハイライトは +0）・穴あけ矩形の優先順（ハイライト → 選択）・座標変換ポートの決定（ポート優先・無ければ倍率）・透明指定の解決・サイズラベルの文言決定（自動生成含む）・表示スケールの解決・サブラインの組立・枠線矩形・暗転領域・行配置 |
| 契約 | Core | `FadeMaskRequest`（入力）・`FadeMaskFrame`（描画内容）・`FadeMaskAppearance`（外観）・`FadeMaskSubLine`・`LogMessage`（ログ書式） |
| 描画 | UI | フレームの各項目を自 UI の描画 API へ写す（`FillRectangle` / `DrawRect` / `DrawText` 等） |
| ホスト結線 | UI | プロパティ機構（`StyledProperty` / `MewProperty`）・DPI 追従・キャンバスのスケーリング・ブラシキャッシュ・テキストの実寸測定とフォント解決 |

UI 層に残る分岐は次の 4 種のみで、いずれも Core へ移せません。

1. **空のコレクションのスキップ**（ブラシ生成を避ける性能ガード）
2. **提示位置の有無**（`LabelAnchor` / `SizeLabelText` が null なら描かない＝描画の前提条件）
3. **フレームワーク固有の値の解決**（DPI の 0 以下フォールバック・色型変換）
4. **テスト計量差の吸収**（`FormattedText` と `SKFont` で行の高さが異なる）

## 依存方向

```text
FadeMaskControl.AvaloniaUI（Avalonia 実装） / FadeMaskControl.MewUI
  └──> FadeMaskControl.Core（本プロジェクト）
         └──> Platform.Core（コア幾何型・座標変換）
```

本プロジェクトが UI フレームワークへ依存することはありません。

## 座標型

UI フレームワークの幾何型ではなく、`Platform.Core` のコア幾何型 `EsRect` / `EsPoint` / `EsSize` を使用します。

UI 実装側は、境界で 1 行の相互変換（`.ToEsRect()` / `.ToAvaloniaRect()`）を行います。

## ビルド

```powershell
dotnet build src/FadeMaskControl.Core/src/FadeMaskControl.Core.csproj
```

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。UI 初期化が不要なため、すべて `[Fact]` で並列実行されます。

```powershell
dotnet test src/FadeMaskControl.Core/tests/FadeMaskControl.Core.Tests.csproj
```
