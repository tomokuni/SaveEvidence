# Feature.MarqueeCore システム仕様書

`MarqueeControl`（ドラッグで矩形を選び、移動・リサイズできるコントロール）の **UI 非依存の状態層**の構造・依存・例外処理・コンポーネント構成を記載します。外部機能と公開 API は [SPEC_ExtFunc](SPEC_ExtFunc.md)、利用手順は [README](README.md) を参照してください。

## 1. システム概要

| 項目 | 内容 |
| --- | --- |
| 目的 | 選択矩形の幾何計算・状態機械・入力の解釈（判断）を 1 箇所に集約し、UI ごとの実装差から切り離す |
| 提供形態 | クラスライブラリ（`Feature.MarqueeCore.dll`） |
| 想定利用者 | UI 実装（`MarqueeControl.AvaloniaUI` / `MarqueeControl.MewUI`）・それらを組み込むホストアプリケーション |
| 汎用性 | 特定のアプリ・UI に依存しない。別コントロールの語彙を持たない（将来そのまま別プロジェクトへ独立できる） |

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| ターゲット | `net10.0` |
| 言語 | C# 14（`Nullable` 有効・`ImplicitUsings` 有効） |
| ルート名前空間 | `EsUtil.Feature.MarqueeCore`。フォルダへ追従し、`.Contract` / `.Decoration` / `.Drawing` / `.Interaction` / `.Logging` / `.Selection` / `.Setting` を持ちます |
| ドキュメント生成 | `GenerateDocumentationFile` 有効（XML は配布物へ出さず `cref` の検証のみに使う） |
| テスト | xUnit v3（UI 初期化不要。`InternalsVisibleTo` で `internal` を検証する） |

## 3. アーキテクチャ

### 3.1 依存方向

層の全体像（Core / UIBase / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（MarqueeControl.AvaloniaUI / MarqueeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    ├──> MarqueeControl.UIBase   どう描くか（UIBase が所有する描画手順）
    ├──> MarqueeControl.UIBase   組み込み先がある場合にだけ必要な規則
    └──> Feature.MarqueeCore ← 本プロジェクト   何を描くか + 判断（状態・値・純粋計算・コア⇔UI の契約）
           └──> Platform.Core（Es* 値型・ICoordTransform・ICursorService）
※ Core は UIBase を参照しない（本層の描画実装がコアの契約を実装する向きだけ）。
```

- 上位層は下位層のみを参照します（逆参照なし）。
- 本コアは UI フレームワークを参照しません。装飾（暗転マスク等）は**本コア内のサブ概念**として同居します（別プロジェクトに分けません）。
- 本コアは描画 API を持ちません。描画の手順は UI 非依存の描画層（`MarqueeControl.UIBase`）が単一所有し、描画層は本コアの描画内容（`MarqueeDrawContent`）と幾何（`MarqueeSelectionGeometry.DirectionalHandles`（読み取り専用の一覧）等）を参照します。
- **本コアは描画層を参照しません**（依存の向きは `UI 層 → MarqueeControl.UIBase → Feature.MarqueeCore` の一方向。本コアは `Common` も `Platform.*` の UI アダプタも参照しません）。**描画は本コアの関心事ではない**ため、本コアは描画内容（`MarqueeDrawContent` と `Layers`）をデータとして返すだけであり、層の順序に従う描画手順は UI 側の共有層（`MarqueeControl.UIBase` の `MarqueeRendererBase`）が単一所有します。

### 3.2 データフロー（入力 → 描画）

```mermaid
sequenceDiagram
    participant UI as UI 層（コントロール）
    participant M as Marquee（ファサード）
    participant S as MarqueeSession（状態機械）
    participant E as IMarqueeEffects（UI 実装）
    participant H as ホスト

    UI->>M: Press / Move / Release / KeyDown（ドメイン座標）
    M->>S: 入力の解釈を委譲
    S->>S: 状態遷移の判断（スナップ・クリック・nudge・最小サイズ）
    S-->>M: MarqueeFeedback（効果）
    S-->>M: MarqueeNotice（通知）
    M->>E: 効果の適用（カーソル → フォーカス → カーソル形状 → 再描画、キャプチャ）
    M-->>UI: 通知を転送
    UI->>H: 公開イベント
    UI->>M: CreateRequest(描画領域のサイズ)
    M->>S: 状態と設定の読み取り
    M-->>UI: MarqueeRenderRequest（設定・状態・描画時の値）
    UI->>M: ResolveContent(request)
    M-->>UI: MarqueeDrawContent（層の順序つき）
    UI->>UI: Layers の順に図形を描き、ラベルは要素として配置
    UI->>H: Rendered（描画内容を渡す）
```

### 3.3 状態遷移

```mermaid
stateDiagram-v2
    [*] --> NoSelection
    NoSelection --> Dragging: Press（ハンドル外）
    Dragging --> Selected: Release（最小サイズ以上）
    Dragging --> NoSelection: Release（最小サイズ未満＝キャンセル）
    NoSelection --> StartPointNudging: KeyDown（矢印）
    Dragging --> DraggingNudging: KeyDown（矢印）
    Selected --> SelectedNudging: KeyDown（矢印）
    StartPointNudging --> NoSelection: EndNudge / Press / Reset
    DraggingNudging --> Dragging: EndNudge / Press / Reset
    SelectedNudging --> Selected: EndNudge
    Selected --> NoSelection: RequestConfirm / RequestCancel / Reset
```

- 状態は `MarqueeInteractionState` として外部へ公開します（nudge・ドラッグ・選択の合成）。
- nudge の終了時は**必ずカーソル再表示を効果として要求**します（非表示の残留を防ぐ）。

## 4. コンポーネント構成

| ファイル | 型 | 可視性 | 責務 |
| --- | --- | --- | --- |
| `src/Marquee.cs`（ルート） | `Marquee` | public | ファサード。設定・環境値の保持、入力の通知、外部操作、描画内容の解決、描画実装の保持、効果の適用 |
| `src/Interaction/MarqueeSession.cs` | `MarqueeSession` | internal | 入力の解釈と状態 遷移の判断。通知と効果を返す |
| `src/Interaction/MarqueeState.cs` | `MarqueeState` | internal | 選択矩形・ドラッグ・nudge の状態保持。**状態は操作（遷移メソッド）でのみ変わり、読み取りはプロパティ**（矛盾した状態を作れない） |
| `src/Marquee.DrawContent.cs`（ルート） | `Marquee`（**partial**） | internal | 描画内容の**内部手順**（装飾・穴あけ矩形・ハンドル・テキストの配置。描くかどうかの判断を単一所有し、穴あけ矩形とポートは 1 回だけ解決する）。公開の入口 `ResolveContent` は `Marquee.cs` が持つ |
| `src/Drawing/MarqueeRenderRequest.cs` | `MarqueeRenderRequest` | public | フレーム解決の入力（設定・状態・描画時に決まる値）。`MarqueeRenderRequest.Create` が値の組を 1 箇所に閉じ、`Marquee.CreateRequest` が状態を写して生成する。**本値から導出する値（座標変換ポートの決定・穴あけ矩形の決定・DIP 変換）は本値のインスタンスメンバーが単一所有する**（`Hole` / `ResolveHoleRectDip` / `ToDipRect` / `ToDomainRect`） |
| `src/Setting/MarqueeOptions.cs` | `MarqueeOptions` | public | 設定の値オブジェクト。既定値を単一所有し、生成時に安全域（`Marquee.Limits`）へ補正する（体裁は描画層の `MarqueePaint` が単一所有する） |
| `src/Drawing/MarqueeDrawContent.cs` | `MarqueeDrawContent` | public | 描画契約（装飾・穴あけ矩形・ハンドル・マーカー・テキストの配置・層の順序） |
| `src/Drawing/MarqueeHandles.cs` | `MarqueeHandles` | public | ドラッグハンドルの描画契約（8 方向の矩形・枠線の太さ） |
| `src/Drawing/MarqueeMarker.cs` | `MarqueeMarker` | public | 始点マーカー（十字線）の描画契約（中心・太さ・半幅） |
| `src/Drawing/MarqueeTextPlacement.cs` | `MarqueeTextPlacement` | public | コアが UI 層へ渡すテキスト（文言・基準点・アライン・描画領域）。**要素として置くか canvas へ描くかは UI 層が決める**（フォントサイズ・色は描画層の `MarqueePaint` が持つ） |
| `src/Interaction/MarqueeSnapshot.cs` | `MarqueeSnapshot` | public | 状態のスナップショット（`readonly record struct`。値型のため通知でヒープを確保しない） |
| `src/Interaction/MarqueeNotice.cs` | `MarqueeNotice` | public | 通知（種別・通知座標・スナップショット。値型） |
| `src/Interaction/MarqueeNoticeKind.cs` | `MarqueeNoticeKind` | public | 通知の種別 |
| `src/Interaction/MarqueeFeedback.cs` | `MarqueeFeedback` | internal | 効果（適用順を単一所有。適用先は `IMarqueeEffects`） |
| `src/Interaction/MarqueeCursorAction.cs` | `MarqueeCursorAction` | internal | 要求するカーソル操作のフラグ |
| `src/Interaction/MarqueeCaptureAction.cs` | `MarqueeCaptureAction` | internal | 要求するポインターキャプチャの操作 |
| `src/Contract/IMarqueeEffects.cs` | `IMarqueeEffects` | public | 効果の適用先（フォーカス・再描画・カーソル操作・カーソル形状・ポインターキャプチャ・座標変換） |
| `src/Contract/MarqueeEffectsBase.cs` | `MarqueeEffectsBase` | public abstract | 効果の既定実装（全メンバーが virtual の空実装） |
| `src/Drawing/MarqueeDrawLayer.cs` | `MarqueeDrawLayer` | public | 描画層の順序（`Decoration` / `Handles` / `Marker` / `Label`。`Label` はテキストがある場合のみ） |
| `src/Interaction/HitTarget.cs` | `HitTarget` | public | ヒット対象 |
| `src/Interaction/MarqueeCursorKind.cs` | `MarqueeCursorKind` | public | カーソル形状の種別 |
| `src/Interaction/MarqueeInteractionState.cs` | `MarqueeInteractionState` | public | 対話状態 |
| `src/Logging/MarqueeLogMessage.cs` | `MarqueeLogMessage` | internal | LoggerMessage 定義（入力の判断・設定変更） |
| `src/Selection/MarqueeSelectionGeometry.cs` | `MarqueeSelectionGeometry` | internal | 純粋幾何計算（ハンドル一覧とハンドル位置を描画内容の解決が使い、ヒットテスト・ドラッグ変形・境界クランプは状態機械の内部手順であるため internal） |
| `src/Decoration/MarqueeDecoOptions.cs` | `MarqueeDecoOptions` | public | 装飾の設定（寸法・描画可否フラグ・ハイライト・内側） |
| `src/Decoration/MarqueeOuterDecoAppearance.cs` / `MarqueeInnerDecoAppearance.cs` | `MarqueeOuterDecoAppearance` / `MarqueeInnerDecoAppearance` | public | 外側/内側の装飾の寸法（枠線の太さのみ。**色は持たない**） |
| `src/Decoration/MarqueeOuterDecoFrame.cs` / `MarqueeInnerDecoFrame.cs` | `MarqueeOuterDecoFrame` / `MarqueeInnerDecoFrame` | public | 装飾の描画内容（**純粋データ**。暗転領域・枠線・塗りを保持し、描くかどうかは `Marquee.ResolveContent` が判断する） |
| `src/Decoration/MarqueeDecoGeometry.cs` | `MarqueeDecoGeometry` | internal | 装飾の幾何計算（穴あけ矩形・暗転領域・枠線矩形）。**枠線の太さの補正は持たない**（装飾の外観の生成時に Marquee.Limits の安全域へ補正される） |
| `src/Drawing/MarqueeHoleRect.cs` | `MarqueeHoleRect` | internal | 穴あけ矩形と閉区間の指定（どちらの矩形を優先するかと +1 の判断を 1 値で持つ） |

## 5. 単一所有（同じ真実を 2 箇所に持たない）

| 対象 | 所有者 |
| --- | --- |
| 選択の状態遷移の判断 | `MarqueeSession` |
| 選択矩形・ドラッグ・nudge の状態（遷移は操作メソッドでのみ変更できる） | `MarqueeState` |
| 純粋な幾何計算（正規化・ハンドル位置・境界クリップ・ドラッグ変形） | `MarqueeSelectionGeometry` |
| 描画する層とその順序（層にある ⇔ 描く値がある） | `Marquee.ResolveLayers`（`MarqueeDrawContent.Layers`） |
| 描くかどうかの判断（非表示・選択なし・nudge・装飾の描画可否フラグ・穴あけ 矩形なし） | `Marquee.ResolveContent` |
| ラベルの表示可否（設定値と穴あけ矩形の有無） | `MarqueeOptions.ShouldShowSizeLabel` / `ShouldShowCoordLabel` |
| 設定の既定値・範囲補正・固定値 | `Marquee.Limits`（`MarqueeOptions` と装飾の外観の生成時の `init` アクセサが参照する） |
| 未設定（0 以下）の解決規則（表示スケールが未指定のときに倍率を使う） | `Marquee.Limits.ResolveDisplayScale`（`Limits` は数値スキーマに加えて本規則を持つ） |
| 装飾の枠線の太さの安全域（0.5〜64.0・既定 2.0） | `Marquee.Limits`（補正は装飾の外観（`MarqueeOuterDecoAppearance` / `MarqueeInnerDecoAppearance`）の生成時の `init` アクセサが適用する） |
| 枠線の 4 辺の矩形（太さは面の短辺で上限を取る） | `MarqueeDecoGeometry.ComputeBorderRects`（辺の式は `EsRect.ComputeInnerEdges` が単一所有する） |
| ラベルの既定色（文字色 = 白、背景色 = 黒 60%）とフォントサイズの既定（14.0 / 12.0） | 描画層の `MarqueePaint` |
| フォントサイズの安全域（6〜48） | 描画層の `MarqueePaint` |
| ラベルの体裁（背景 = 実寸 + 余白×2、アライン、クランプ、行送り） | 描画層 `MarqueeControl.UIBase` の `MarqueePaint` / `MarqueeLabelPlan` |
| 装飾の DIP 変換と穴あけ矩形の決定 | `MarqueeRenderRequest` のインスタンスメンバー（`Hole` / `ResolveHoleRectDip` / `ToDipRect` / `ToDomainRect`。描画入力から導出する値は入力型が単一所有する。解決規則のうちポートの決定と穴あけ矩形の優先順は static メンバー（`ResolvedPort` / `ResolveHoleRect`）が単一所有する） |
| 装飾の描画可否（明示フラグ。透明色を代用にしない） | `MarqueeDecoOptions`（暗転の可否は述語 `ShouldDrawOverlay(hasHoleRect)`、それ以外は明示フラグ `ShowOuterBorder` / `ShowInnerFill` / `ShowInnerBorder`） |
| 色（ハンドル・始点マーカー・ラベル・装飾） | 描画層 `MarqueeControl.UIBase` の `MarqueePaint`（本コアは色を持つ型を公開しない） |
| サイズラベルの文言と自動生成（書式を含む） | `Marquee.ResolveContent` の `ResolveSizeLabelText`（書式は private の `FormatSizeLabel`） |
| 効果の適用順 | `MarqueeFeedback.ApplyCursor` / `ApplyVisual`（適用先は `IMarqueeEffects`。デリゲートを生成しない） |
| 描画 API の呼び出し（層の順序と写し方、色の参照） | 描画層 `MarqueeControl.UIBase`（`IMarqueeRenderer` / `MarqueeRendererBase` / `MarqueeRenderer` 等）。本コアは描画内容と層の順序（`MarqueeDrawContent.Layers`）をデータとして渡すだけである |
| 入力の判断に伴うログと設定変更のログ | `MarqueeLogMessage` |
| 設定変更の差分検出（変化した項目だけを出力） | `MarqueeLogMessage.LogOptionsChanged` / `LogDecoOptionsChanged`（出力の契機は `Marquee.Options` / `DecoOptions` / 環境値の setter） |

## 6. 例外処理ポリシー

| 状況 | 扱い |
| --- | --- |
| `Marquee` の `effects` が null | `ArgumentNullException`（呼び出し側の実装誤り） |
| カーソルサービス未指定 | 例外にせず、カーソル操作を効果へ載せない（UI 初期化なしの検証を可能にする） |
| ロガー未設定 | `NullLogger` へ吸収する（呼び出し側は null を設定してよい） |
| 座標変換ポート未設定 | 等価変換（1 ドメインピクセル = 1 DIP・境界なし）として扱う |
| 未定義の `EsKey` | 何もしない（`default` の効果を返す） |
| 設定値の範囲外・非数 | 生成時に補正する（例外にしない）。安全域は `Marquee.Limits` が単一所有し、`MarqueeOptions` と装飾の外観が `init` アクセサで適用する。フォントサイズは 6〜48、非数は既定値 |

## 7. 可視性ポリシー

- 外部が使わない型・メンバーは `internal` とし、単体テストにのみ `InternalsVisibleTo`（`Feature.MarqueeCore.Tests`）で公開します。
- **クラスが `internal` の場合、そのメンバーも `internal` とします**（`public` と書いても外部から見えないため、可視性の宣言を実態と一致させる）。
- `[EditorBrowsable]` による「見えにくくする」対処は行いません（公開面も契約も変わらず、見つけにくくなるだけのため）。
- **層 3（内部ヘルパー）は外部参照 0 件を実測して絞り込んだ結果**です。UI 実装が参照するのは `Marquee`・描画契約・列挙型・`MarqueeOptions`・`MarqueeDecoOptions` のみです（幾何計算は `internal` であり UI 実装からは参照できません）。

## 8. テスト方針

| 方針 | 内容 |
| --- | --- |
| UI 初期化不要 | ヘッドレス実行を必要としないため `[Fact]` / `[Theory]` で並列実行する |
| 単位 | 純粋計算（`MarqueeSelectionGeometry` / `MarqueeDecoGeometry` / `MarqueeRenderRequest`）・状態機械・ファサード・描画内容の解決 |
| 配置 | **ソースのフォルダを鏡映**する（`tests/Contract` / `tests/Decoration` / `tests/Drawing` / `tests/Interaction` / `tests/Logging` / `tests/Selection` / `tests/Setting`。ファサードの partial のテストはルート） |
| 合計 | 324 件（`[Theory]` は展開後のケース数） |

```powershell
dotnet test src/Feature.MarqueeCore/tests/Feature.MarqueeCore.Tests.csproj
```

## 9. 設計判断（UI 非依存の境界）

本プロジェクトの型は、UI フレームワークの型をシグネチャに持たないことを境界の条件としています。

| 判断 | 内容 |
| --- | --- |
| 座標・寸法の型 | コア（`EsUtil.Platform.Core.Geometry`）の `EsPoint` / `EsRect` / `EsSize` を使い、UI 型は境界で 1 行変換する |
| キー入力の型 | コアの `EsKey` を使い、UI のキー型は境界で変換する |
| カーソル操作 | `ICursorService` 抽象にのみ依存する（未指定はカーソル操作を行わない）。UI のカーソル型への写像は効果（`IMarqueeEffects.SetCursorShape`）を受けた UI が行う |
| 効果の適用 | UI 固有の操作（フォーカス・再描画・ポインターキャプチャ・画面座標変換）は `IMarqueeEffects` を唯一の境界とし、コアは UI 型を知らない（不要なメンバーの空実装は `MarqueeEffectsBase` で省略できる） |
| 文字の描画 | コアは文言・基準点・アラインだけを持ち、実寸 → 配置矩形の写像は描画層（`MarqueeControl.UIBase` の `MarqueeLabelPlan`）が、実寸測定と要素の配置は UI 層が行う（実寸は描画 API 依存のため）。色は描画層の `MarqueePaint` が与える |
| 再描画のバッチ化 | UI のディスパッチャーに依存するため `MarqueeControl.UIBase` が単一所有する（コアは同期計算のみ） |
| 色 | コアは色を持たない（色は描画にだけ必要な設定であり、描画層 `MarqueeControl.UIBase` の `MarqueePaint` が単一所有する）。色の型は UI 非依存の `System.Drawing.Color` とし、UI の色型への変換は描画ポートの境界で行う |
| 選択矩形の閉区間 | 「終点を含む」規則（DIP への変換時の +1）はコア（`MarqueeHoleRect` / `MarqueeSelectionGeometry`）が単一所有する（UI ごとに +1 すると片方だけずれる） |
| ラベルの体裁 | 「背景 = 文字の実寸 + 内側余白 × 2」「アラインの適用」「行送り」「描画領域へのクランプ」は描画にだけ必要な規則のため、描画層（`MarqueeControl.UIBase` / `LoupeControl.UIBase`）が単一所有する（コアは文言・基準点・アライン・クランプ領域だけを渡す） |
| 設定では変更しない固定値 | **公開しません**。コア内だけで使う値（ハンドル枠の太さ・カーソル座標ラベルのオフセット・始点マーカーの太さ）は描画内容の解決（`Marquee.DrawContent.cs`）の private 定数とし、UI 層は解決済みの値（`MarqueeHandles.BorderThickness` 等）を受け取る |
| フォントサイズの安全域 | 6〜48 と「非数は既定値」は描画層（`MarqueePaint` / `LoupePaint`）が単一所有する |
| ドメイン境界 | 利用者が座標変換ポート（`ICoordTransform.Bounds`）で定義し、コアは `ClampToDomainBounds` が有効な場合だけクリップする |

## 10. ライセンス

もう一方のコア（`Feature.LoupeCore`）との機能差に由来する非対称は、README の「機能差に由来する非対称（もう一方のコアとの違い）」を参照してください。

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
