# Parts.CaptureSessionControl.UIBase

`CaptureSessionControl`（撮影・切り出しのセッション）の **UI 間で共有する処理**です。 Avalonia 版と MewUI 版が
同じ設定写像を使うことを保証します。

## 1. 目的

- **設定から描画の設定（色）への写像を 1 箇所へ集約**する。 色は描画にだけ必要な設定であり、描画層（UIBase）の
  `MarqueeAppearance` / `LoupeAppearance` が単一所有するため、写像も 1 つでなければならない（写像漏れはビルドが通り、
  色が無言で効かなくなる）。
- **製品の設定型を再利用ライブラリへ持ち込まない**。 写像元がセッションの設定型（`CaptureSettings`）であるため、
  本層は `CaptureSessionControl` 側の共有層として置く。
- **複合コントロールの共有処理も本層が持つ**。 設定とモードから子コントロールの値を決める手順（`SelectionOverlayChildValues`）と、
  付加情報ラベル（操作・情報）の配置指示（`SelectionOverlayLabelRenderer` / `MarqueeHostLabelRenderer`）は
  同じ「UI 間で共有する処理」であり、本層に集約して両 UI の配線が同じ規則を使うことを保証する。

## 2. 層の位置付け

```text
Parts.CapturePreviewControl.Core ─────────────┐（設定型・寸法・表示可否）
EsUtil.Feature.Marquee.Presenter / EsUtil.Feature.Loupe.Presenter ─┐（描画の設定＝色）
                                             ↓
                              Parts.CaptureSessionControl.UIBase（色の写像）
                                             ↓
              Parts.CapturePreviewControl.AvaloniaUI / .MewUI（ホスト・配線）
```

- `Parts.CapturePreviewControl.Core` は描画層を参照できない（Core → UIBase の逆流になる）。 したがって**色の写像は
  コアに置けない**。
- 製品の設定型（`CaptureSettings`）に依存する写像は、再利用ライブラリではなく本層（セッション側の共有層）に置く。
- **寸法・表示可否は本層が写像しない**（`SelectionOverlayOptionsMapper`（コア） が単一所有する）。 写像は色だけを扱う。

## 3. 構成

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `CapturePaintMapper` | `internal static` | `CaptureSettings` → `MarqueeAppearance` / `LoupeAppearance` の写像 |
| `SelectionOverlayChildValues` | `public readonly record struct` | 設定とモードから内包コントロールへ適用する値を解決する |
| `SelectionOverlayLabelRenderer` | `public static class` | 付加情報ラベル（操作・情報）の縦積みの配置指示を解決する |
| `MarqueeHostLabelRenderer` | `public static class` | ラベル 1 行の体裁（背景 = 文字の実寸 + 内側余白 × 2）の配置指示を解決する |

## 4. 依存関係

```text
Parts.CaptureSessionControl.UIBase
    ├─ Parts.CapturePreviewControl.Core（CaptureSettings / SelectionOverlayOptions / Mode / Modes）
    ├─ EsUtil.Feature.Marquee.Core（MarqueeOptions）
    ├─ EsUtil.Feature.Loupe.Core（LoupeOptions）
    ├─ EsUtil.Feature.Marquee.Presenter（MarqueeAppearance / MarqueeLabelPlacement）
    ├─ EsUtil.Feature.Loupe.Presenter（LoupeAppearance）
    └─ EsUtil.Platform.Core（Es* 幾何型）
```

UI フレームワークへ依存しません。 外部パッケージへの直接依存もありません。

`internal` は `InternalsVisibleTo`（`Parts.CapturePreviewControl.AvaloniaUI` / `Parts.CapturePreviewControl.MewUI` /
`Parts.CaptureSessionControl.UIBase.Tests`）で公開します。

## 5. 主な API

```csharp
MarqueeAppearance marquee = CapturePaintMapper.ToMarqueeAppearance(settings);  // 設定 → 選択矩形の外観
LoupeAppearance loupe = CapturePaintMapper.ToLoupeAppearance(settings, background);     // 設定 → ルーペの色（7 項目）
```

画像外（ペーストボード）の色は**設定（`LoupePasteboardColor`）が単一所有**します（描画層は本色で描画面全体を塗ってから拡大画像を重ねるため、画像の無い部分が本色の無地になります）。
透かすため透明、プレビューの切り出しはテーマ背景色）。

## 6. 設計上の約束

1. **色だけを写像する**。 寸法・表示可否はコアの設定マッパーが単一所有する。
2. **枠線の色は 1 つ**: 選択枠線（ハンドルの枠線）と暗転マスクの外枠は同じ矩形に重なるため、同じ設定値
   （`BorderColor`）を両方へ割り当てる。
3. **内側の塗り・内側の枠線は写像しない**（描画の可否は `MarqueeAppearance` のフラグが単一所有する）。
4. **画像外の色は設定**（`LoupePasteboardColor`。 ホストは色を引数で渡さない）。
5. 引数の `settings` は `ToNonNull` 済みを前提とする（null は例外）。

## 7. テスト

```text
tests/
├── CapturePaintMapper.Tests.cs         # 写像の網羅・枠線の共有・画像外の色・引数の検証
├── SelectionOverlayChildValues.Tests.cs # モードから導出する値の上書き
├── SelectionOverlayLabelRenderer.Tests.cs  # 付加情報ラベルの縦積みと表示可否
└── MarqueeHostLabelRenderer.Tests.cs   # ラベル 1 行の体裁（背景の寸法の検証）
```

```powershell
dotnet test src/Parts.CaptureSessionControl.UIBase/tests/Parts.CaptureSessionControl.UIBase.Tests.csproj
```

UI 依存の検証（ホストの配線・実際の描画）は各 UI のテストプロジェクトが担います。

## 8. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — アーキテクチャ・依存・設計判断

## 9. 使い方

両 UI のホストが、利用者の設定（`CaptureSettings`）を写像して内包コントロールへ適用します。 **本層は公開 API を持たない**ため、写像は同一ソリューションのホストが `InternalsVisibleTo` 経由で呼びます。

```csharp
// 設定 → 色（提示層の外観）の写像
MarqueeAppearance marqueeAppearance = CapturePaintMapper.ToMarqueeAppearance(settings);
LoupeAppearance loupeAppearance = CapturePaintMapper.ToLoupeAppearance(settings);

// 設定とモード → 子コントロールへ適用する値（寸法・表示可否）
SelectionOverlayChildValues values = SelectionOverlayChildValues.Resolve(options, mode, marqueeAppearance);
```

詳細は [docs/SPEC_API.md](docs/SPEC_API.md) を参照してください。

## 10. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
