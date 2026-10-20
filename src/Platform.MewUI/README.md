# Platform.MewUI

`EsUtil.Platform.Core`（UI フレームワーク非依存の抽象・値型）と **MewUI**（`Aprillz.MewUI`）の型を相互変換する Interop 層です。

## 1. 目的

コアロジック（`EsPoint` / `EsKey` / `EsWindowState` 等）は UI フレームワークに依存しないため、UI 境界でのみ型変換が必要です。
Avalonia 版の `Platform.AvaloniaUI` と同じ役割を MewUI 向けに提供します。

## 2. 収録する型

| 型 | 内容 |
| --- | --- |
| `EsKeyMewUIInterop` | `EsKey` ⇔ `Aprillz.MewUI.Key`（`ToMewUIKey()` / `ToEsKey()` / `IsRepresentable()`） |
| `EsGeometryMewUIInterop` | `EsPoint` / `EsSize` / `EsVector` / `EsRect` ⇔ MewUI の対応型。 色（`System.Drawing.Color` ⇔ `Aprillz.MewUI.Color`）の変換も含む |
| `EsWindowStateMewUIInterop` | `EsWindowState` ⇔ `Aprillz.MewUI.Controls.WindowState` |
| `EsPlatformHandleMewUIInterop` | `Aprillz.MewUI.Window` → `EsPlatformHandle`（`Window.Handle` は `nint`） |
| `WindowPlacementMath` | ウィンドウ配置の DIP ⇔ 画面物理ピクセルの変換、スケールの正規化、復元可否の判定（純粋関数。 `WindowStartupLocation` / `WindowSize` へ DIP を渡すために使う）。 **換算と正規化は `EsUtil.Platform.Core` の `DpiScaler`（両 UI 共通の単一所有者）へ委譲する** |
| `Imaging.SkiaImageView` | `EsReadOnlyBitmap` を親が割り当てた領域へ描画する表示ビュー（`SkiaCanvasView` 派生。 `Source` を設定するだけで描く） |
| `Imaging.SkiaImageRenderer`（internal） | `EsReadOnlyBitmap` を Skia キャンバスへ敷く描画層（`SkiaImageView` とその派生が共用。 画素で検証できる）。 **表示境界の画像型はコアの画像（`EsReadOnlyBitmap`）そのもの**（Avalonia のような表示アダプタは不要）。 描画に使う `SKBitmap` は非所有ビューで、描画の直後に破棄する |
| `Imaging.ClientPixelScale` | 描画面（物理ピクセル）と表示領域（DIP）の比から、描画に使う実効倍率を解決する。 **DPI の通知は追加直後のコントロールへ届かないため、保持値だけに頼ると 100% で描いて画像とずれる** |
| `Imaging.MewUITextMeasure` | MewUI のテキスト エンジンで文字列の実寸を測る（ラベルの配置の唯一の入口）。 **描画に使うエンジンで測る**ため、別のエンジンで測った値より広い値を返す（文字要素の背景を実寸から作る場合に必要。 グラフィックス バックエンドの登録が前提） |

依存方向:

```text
Platform.MewUI（本プロジェクト）
  └──> EsUtil.Platform.Core（コア型・画像の配置計算）
  └──> Aprillz.MewUI.Core（MewUI の型）
  └──> Aprillz.MewUI.Skia（SkiaCanvasView）
```

UI 実装（MewUI ホスト）は本プロジェクトを参照し、境界で 1 行変換します。

## 3. 重要な非対称性（`EsKey`）

`EsKey` は Avalonia / Win32 のキー体系（**223 メンバー**）に合わせているのに対し、MewUI の `Key` は
**抽象化された小さな集合（91 メンバー・独自採番。 Win32 の仮想キーコードではない）** です。 そのため:

| 項目 | 内容 |
| --- | --- |
| 対応付け | **値ではなく名前**で行う（採番体系が異なるため） |
| 名前が異なるキー | `EsKey.Back` ⇔ `MewUI.Key.Backspace` の **1 組のみ**（別名表で吸収） |
| 対応先がないキー | MewUI が表現できない `EsKey` **133 メンバー**（`BrowserBack` / `DbeRoman` 等）は `MewUI.Key.None` へフォールバック |
| 未定義値 | `EnumCanonical.TryParse`（`EsUtil.Platform.Core`）で検出し `None` へフォールバック（`Enum.TryParse` は数値文字列を受け付けるため） |

**ホットキーの設定ファイル互換性**: `EsKey` の名前を変えなければ、既存の `settings.json` をそのまま読み込めます
（`JsonStringEnumConverter` が名前で保存するため）。 ただし MewUI ホストで扱えないキーは `None` になるため、
MewUI 版のホットキー設定 UI では利用可能なキーに制限がかかります。

## 4. MewUI 固有の挙動（注意点）

| 項目 | 内容 |
| --- | --- |
| 負のサイズ | MewUI の `Rect` / `Size` は**負のサイズを 0 にクランプ**する（Avalonia は保持する）。 空の矩形（負のサイズ）を MewUI へ渡すと 0 サイズになる（X / Y は保持） |
| ウィンドウハンドル | `Window.Handle` は `nint` のため `EsPlatformHandle` へそのまま変換できる |

## 5. ビルド

```powershell
dotnet build src/Platform.MewUI/src/Platform.MewUI.csproj
```

## 6. テスト

`tests/` が本プロジェクトの単体テストを持ちます。 UI 初期化が不要なため、すべて `[Fact]` で並列実行されます。

```powershell
dotnet test src/Platform.MewUI/tests/Platform.MewUI.Tests.csproj
```

## 7. バージョン固定

MewUI は API が安定化中（0.x）のため、**パッケージのバージョンは固定**しています（`Aprillz.MewUI.Core` `0.21.1`）。
アップグレード時は本プロジェクトのテスト（キー数 91 の回帰検知を含む）で破壊的変更を検出します。

## 8. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — アーキテクチャ・依存・設計判断

## 9. 使い方

コア型と MewUI の型は**名前ベースで相互変換**します（数値は互換ではないため、値による対応付けをしません）。 拡張メソッドとして呼びます。

```csharp
// キー（Back ⇔ Backspace だけ別名表で解決する。 未定義値・未対応は None）
Aprillz.MewUI.Key key = esKey.ToMewUIKey();

// 幾何・色（コア型 ⇔ MewUI の型）
Aprillz.MewUI.Point point = esPoint.ToMewUIPoint();
Aprillz.MewUI.Color color = drawingColor.ToMewUIColor();
```

詳細は [docs/SPEC_API.md](docs/SPEC_API.md) を参照してください。

## 10. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
