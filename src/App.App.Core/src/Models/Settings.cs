using CommunityToolkit.Mvvm.ComponentModel;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Enum;

namespace EsUtil.SaveEvidence.App.Core.Models;

/// <summary>
/// アプリケーションの設定を表すデータモデル（変更通知対応）。
/// </summary>
/// <remarks>
/// CommunityToolkit.Mvvm の <see cref="ObservableObject"/> を継承し、
/// <c>[ObservableProperty]</c> 属性により自動的に変更通知を発行する。<br/>
/// 永続化ロジックは一切含まず、読み書きは <see cref="Services.SettingsService"/> を介して行う。<br/>
/// <b>依存分離</b>: UI フレームワークの型（<c>Avalonia.PixelRect</c> / <c>WindowState</c>）ではなく、<br/>
/// UI 非依存の <see cref="EsRectInt"/> / <see cref="EsWindowState"/> で保持する。<br/>
/// </remarks>
public sealed partial class Settings : ObservableObject
{
    /// <summary>スクリーン選択キャプチャのホットキー設定を取得または設定する。</summary>
    [ObservableProperty]
    private HotKeySetting _selectScreenHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, EsKey.Q);

    /// <summary>ウィンドウ選択キャプチャのホットキー設定を取得または設定する。</summary>
    [ObservableProperty]
    private HotKeySetting _windowSelectHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, EsKey.W);

    /// <summary>範囲選択キャプチャのホットキー設定を取得または設定する。</summary>
    [ObservableProperty]
    private HotKeySetting _areaSelectHotKey = new(HotKeyModifiers.Control | HotKeyModifiers.Shift, EsKey.E);

    /// <summary>ファイル名テンプレート文字列を取得または設定する。 {date}や{time}のプレースホルダを使用可能。</summary>
    [ObservableProperty]
    private string _fileNameTemplate = "screenshot_007_{date}-{time}.png";

    /// <summary>キャプチャ画像の保存先フォルダパスを取得または設定する。</summary>
    /// <remarks>デフォルトは <c>{MyPictures}\SaveEvidence</c>（設定画面の現在値）。<c>{MyPictures}</c> はピクチャフォルダへ展開される。</remarks>
    [ObservableProperty]
    private string _saveFolderPath = "{MyPictures}\\SaveEvidence";

    /// <summary>MainWindow のウィンドウ位置・サイズを取得または設定する。</summary>
    /// <remarks>画面物理座標（整数ピクセル）。 UI 実装側で自 UI の型へ変換して使用する。</remarks>
    [ObservableProperty]
    private EsRectInt? _mainWindowBounds;

    /// <summary>MainWindow のウィンドウ状態を取得または設定する。</summary>
    [ObservableProperty]
    private EsWindowState _mainWindowState = EsWindowState.Normal;

    /// <summary>ナビゲーションパネルの展開状態（true = 展開 / false = アイコンのみ収納）を取得または設定する。</summary>
    [ObservableProperty]
    private bool _isNavigationExpanded = true;

    /// <summary>テーマモード（Light / Dark / System）を取得または設定する。</summary>
    /// <remarks>アプリ全体のテーマ（<c>RequestedThemeVariant</c>）に反映される。 既定は OS 追従（System）。</remarks>
    [ObservableProperty]
    private ThemeMode _themeMode = ThemeMode.System;

    /// <summary>FolderViewForm の特大アイコンサイズ（ピクセル）。</summary>
    [ObservableProperty]
    private int _folderExtraLargeIconSize = 320;

    /// <summary>FolderViewForm の大アイコンサイズ（ピクセル）。</summary>
    [ObservableProperty]
    private int _folderLargeIconSize = 240;

    /// <summary>FolderViewForm の中アイコンサイズ（ピクセル）。</summary>
    [ObservableProperty]
    private int _folderMediumIconSize = 160;

    /// <summary>FolderViewForm の表示モード（0=特大, 1=大, 2=中）。</summary>
    /// <remarks>フォルダビューの表示モード選択状態。 起動時に復元される。</remarks>
    [ObservableProperty]
    private int _folderViewModeIndex = 1;

    /// <summary>FolderViewForm のソート順（true=昇順, false=降順）。</summary>
    /// <remarks>フォルダビューのソート順選択状態。 起動時に復元される。</remarks>
    [ObservableProperty]
    private bool _folderViewSortAscending = true;

    /// <summary>Undo 一覧の表示モード（0=特大, 1=大, 2=中）。</summary>
    /// <remarks>Undo 一覧ページの表示モード選択状態。 起動時に復元される。</remarks>
    [ObservableProperty]
    private int _undoViewModeIndex = 1;

    /// <summary>プレビュー画像の中央寄せ（true）／左上寄せ（false）を取得または設定する。</summary>
    /// <remarks>デフォルトは false（左上寄せ。 設定画面の現在値）。</remarks>
    [ObservableProperty]
    private bool _centerAlign;

    /// <summary>キャプチャ時（SnipOverlayWindow）の選択枠境界線色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: DarkGray = <c>#FFA9A9A9</c>）。</remarks>
    [ObservableProperty]
    private string _captureBorderColor = "#FFA9A9A9";

    /// <summary>キャプチャ時（SnipOverlayWindow）のテキスト色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 白 100% = <c>#FFFFFFFF</c>）。</remarks>
    [ObservableProperty]
    private string _captureTextColor = "#FFFFFFFF";

    /// <summary>キャプチャ時の選択枠の太さ（DIP）を取得または設定する。</summary>
    /// <remarks>デフォルトはコントロール既定（MarqueeControl / MarqueeOuterDecoAppearance）と一致させる。</remarks>
    [ObservableProperty]
    private double _captureBorderThickness = 2.0;

    /// <summary>キャプチャ時の暗転オーバーレイの色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 黒 50%）。</remarks>
    [ObservableProperty]
    private string _captureOverlayColor = "#80000000";

    /// <summary>選択矩形の最小サイズ（AreaSelect: 物理 px / Crop: 論理 px）を取得または設定する。</summary>
    /// <remarks>デフォルトは 6（設定画面の現在値）。</remarks>
    [ObservableProperty]
    private int _minSelectionSize = 6;

    /// <summary>ドラッグハンドルの色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 白 100% = <c>#FFFFFFFF</c>）。</remarks>
    [ObservableProperty]
    private string _handleColor = "#FFFFFFFF";

    /// <summary>ドラッグハンドルの一辺サイズ（DIP）を取得または設定する。</summary>
    [ObservableProperty]
    private double _handleSize = 10.0;

    /// <summary>ハンドルヒットテスト半径（DIP）を取得または設定する。</summary>
    [ObservableProperty]
    private double _handleHitRadius = 12.0;

    /// <summary>画像外の選択を禁止するかどうか（クリップ）を取得または設定する。</summary>
    /// <remarks>デフォルトは true（設定画面の現在値）。</remarks>
    [ObservableProperty]
    private bool _clampToSourceBounds = true;

    /// <summary>nudge 中の十字マーカーの色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 白 100% = <c>#FFFFFFFF</c>）。</remarks>
    [ObservableProperty]
    private string _markerColor = "#FFFFFFFF";

    /// <summary>nudge 中の十字マーカーの長さ（全長・DIP）を取得または設定する。</summary>
    [ObservableProperty]
    private double _markerLength = 16.0;

    /// <summary>カーソルの右側にカーソルの対象座標ラベル（"xy (x, y)"）を表示するか（Marquee）。</summary>
    [ObservableProperty]
    private bool _showCoordLabel;

    /// <summary>カーソル座標ラベルの文字色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 白 100% = <c>#FFFFFFFF</c>）。</remarks>
    [ObservableProperty]
    private string _coordLabelColor = "#FFFFFFFF";

    /// <summary>カーソル座標ラベルの背景色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 黒 60%）。</remarks>
    [ObservableProperty]
    private string _coordLabelBackgroundColor = "#99000000";

    /// <summary>カーソル座標ラベルのフォントサイズ（DIP）を取得または設定する。</summary>
    [ObservableProperty]
    private double _coordLabelFontSize = 12.0;

    /// <summary>ルーペ十字線の色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 赤 40%）。</remarks>
    [ObservableProperty]
    private string _loupeCrossColor = "#66FF0000";

    /// <summary>ルーペ外枠の色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: LightGray = <c>#FFD3D3D3</c>）。</remarks>
    [ObservableProperty]
    private string _loupeFrameColor = "#FFD3D3D3";

    /// <summary>ルーペ外枠の太さ（ピクセル）を取得または設定する。</summary>
    [ObservableProperty]
    private int _loupeFrameWidth = 2;

    /// <summary>ルーペの拡大率（4〜32、偶数推奨）を取得または設定する。</summary>
    [ObservableProperty]
    private int _loupeZoomLevel = 4;

    /// <summary>ルーペの一辺のサイズ（ピクセル、64〜512）を取得または設定する。</summary>
    [ObservableProperty]
    private int _loupeSize = 160;

    /// <summary>ルーペのカーソルからのオフセット（DIP）を取得または設定する。</summary>
    [ObservableProperty]
    private double _loupeOffset = 20.0;

    /// <summary>プレビュー市松模様の色 A（明色）を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: Gray 30% = <c>#4C808080</c>）。</remarks>
    [ObservableProperty]
    private string _previewCheckerColorA = "#4C808080";

    /// <summary>プレビュー市松模様の色 B（暗色）を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: Silver 30% = <c>#4CA9A9A9</c>）。</remarks>
    [ObservableProperty]
    private string _previewCheckerColorB = "#4CA9A9A9";

    /// <summary>ルーペの画像外（ペーストボード）の色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 中性グレー <c>#FF535353</c>。 透明にすると背後の要素が見える）。</remarks>
    [ObservableProperty]
    private string _loupePasteboardColor = "#FF535353";

    /// <summary>ルーペ画像上の選択矩形破線の色 A（明色）を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 白 100%）。</remarks>
    [ObservableProperty]
    private string _loupeSelectionRectColorA = "#FFFFFFFF";

    /// <summary>ルーペ画像上の選択矩形破線の色 B（暗色）を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 黒 100%）。</remarks>
    [ObservableProperty]
    private string _loupeSelectionRectColorB = "#FF000000";

    /// <summary>ルーペの上側に中心座標ラベル（"xy (x, y)"）を表示するか。</summary>
    /// <remarks>デフォルトは true（設定画面の現在値）。</remarks>
    [ObservableProperty]
    private bool _loupeShowCoordLabel = true;

    /// <summary>ルーペ座標ラベルの文字色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 白 100% = <c>#FFFFFFFF</c>）。</remarks>
    [ObservableProperty]
    private string _loupeCoordLabelColor = "#FFFFFFFF";

    /// <summary>ルーペ座標ラベルの背景色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 黒 60%）。</remarks>
    [ObservableProperty]
    private string _loupeCoordLabelBackgroundColor = "#99000000";

    /// <summary>ルーペ座標ラベルのフォントサイズ（DIP）を取得または設定する。</summary>
    [ObservableProperty]
    private double _loupeCoordLabelFontSize = 12.0;

    /// <summary>サイズラベルの表示状態を取得または設定する。</summary>
    [ObservableProperty]
    private bool _sizeLabelVisible = true;

    /// <summary>サイズラベル（1 行目）の背景色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 黒 60% = <c>#99000000</c>）。</remarks>
    [ObservableProperty]
    private string _sizeLabelBackgroundColor = "#99000000";

    /// <summary>サイズラベルのフォントサイズを取得または設定する。</summary>
    [ObservableProperty]
    private double _sizeLabelFontSize = 14.0;

    /// <summary>情報ラベルの表示状態を取得または設定する。</summary>
    [ObservableProperty]
    private bool _infoLabelVisible = true;

    /// <summary>情報ラベルの文字色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 白 100% = <c>#FFFFFFFF</c>）。</remarks>
    [ObservableProperty]
    private string _infoLabelColor = "#FFFFFFFF";

    /// <summary>情報ラベル（3 行目）の背景色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 黒 60% = <c>#99000000</c>）。</remarks>
    [ObservableProperty]
    private string _infoLabelBackgroundColor = "#99000000";

    /// <summary>情報ラベルのフォントサイズを取得または設定する。</summary>
    [ObservableProperty]
    private double _infoLabelFontSize = 14.0;

    /// <summary>サブライン（サイズラベルの下の 2 行目以降）の表示状態を取得または設定する。</summary>
    [ObservableProperty]
    private bool _showSubLines = true;

    /// <summary>サブラインのフォントサイズを取得または設定する。</summary>
    /// <remarks>デフォルトは 12（設定画面の現在値）。</remarks>
    [ObservableProperty]
    private double _subLineFontSize = 12.0;

    /// <summary>サブラインの文字色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: Gainsboro = <c>#FFDCDCDC</c>）。</remarks>
    [ObservableProperty]
    private string _subLineColor = "#FFDCDCDC";

    /// <summary>サブライン（2 行目）の背景色を取得または設定する。</summary>
    /// <remarks>色と不透明度を統合した <c>#AARRGGBB</c> 形式（デフォルト: 黒 60% = <c>#99000000</c>）。</remarks>
    [ObservableProperty]
    private string _subLineBackgroundColor = "#99000000";

    /// <summary>プレビュー市松模様の1タイルサイズ（ピクセル、1〜20）を取得または設定する。</summary>
    [ObservableProperty]
    private int _previewCheckerSize = 6;

    /// <summary>キャプチャ時にメインウィンドウを非表示にするかどうかを取得または設定する。</summary>
    /// <remarks>デフォルトは true（設定画面の現在値）。</remarks>
    [ObservableProperty]
    private bool _hideMainWindowOnCapture = true;

    /// <summary>外観設定（選択枠・暗転・ハンドル・マーカー・ルーペ・プレビューの市松模様・ラベル）をデフォルト値へ戻す。</summary>
    /// <remarks>
    /// 設定画面の外観設定にある「デフォルト値に戻す」ボタンから呼び出される。<br/>
    /// デフォルト値は本クラスのフィールド初期化子（各 <c>[ObservableProperty]</c> フィールドの初期値）を真のソースとし、
    /// 新規インスタンスからコピーする（DRY 原則・二重管理を避ける）。<br/>
    /// 設定変更は <see cref="SettingsService"/> の <c>PropertyChanged</c> 購読により自動保存される。
    /// </remarks>
    public void ResetAppearanceToDefaults()
    {
        // フィールド初期化子（= デフォルト値）を持つ新規インスタンスから外観プロパティをコピーする
        var d = new Settings();

        // 選択枠・暗転
        CaptureBorderColor = d.CaptureBorderColor;
        CaptureBorderThickness = d.CaptureBorderThickness;
        MinSelectionSize = d.MinSelectionSize;
        CaptureOverlayColor = d.CaptureOverlayColor;
        // ドラッグハンドル・マーカー
        HandleColor = d.HandleColor;
        HandleSize = d.HandleSize;
        HandleHitRadius = d.HandleHitRadius;
        MarkerColor = d.MarkerColor;
        MarkerLength = d.MarkerLength;
        // ルーペ
        LoupeFrameColor = d.LoupeFrameColor;
        LoupeFrameWidth = d.LoupeFrameWidth;
        LoupeCrossColor = d.LoupeCrossColor;
        LoupePasteboardColor = d.LoupePasteboardColor;
        LoupeSelectionRectColorA = d.LoupeSelectionRectColorA;
        LoupeSelectionRectColorB = d.LoupeSelectionRectColorB;
        LoupeZoomLevel = d.LoupeZoomLevel;
        LoupeSize = d.LoupeSize;
        LoupeOffset = d.LoupeOffset;
        // 市松模様（プレビュー）
        PreviewCheckerColorA = d.PreviewCheckerColorA;
        PreviewCheckerColorB = d.PreviewCheckerColorB;
        PreviewCheckerSize = d.PreviewCheckerSize;
        // 選択矩形内のラベル（1〜3 行目）
        CaptureTextColor = d.CaptureTextColor;
        SizeLabelBackgroundColor = d.SizeLabelBackgroundColor;
        SizeLabelFontSize = d.SizeLabelFontSize;
        SubLineColor = d.SubLineColor;
        SubLineBackgroundColor = d.SubLineBackgroundColor;
        SubLineFontSize = d.SubLineFontSize;
        InfoLabelColor = d.InfoLabelColor;
        InfoLabelBackgroundColor = d.InfoLabelBackgroundColor;
        InfoLabelFontSize = d.InfoLabelFontSize;
        // カーソル位置の座標ラベル（Marquee）
        CoordLabelColor = d.CoordLabelColor;
        CoordLabelBackgroundColor = d.CoordLabelBackgroundColor;
        CoordLabelFontSize = d.CoordLabelFontSize;
        // ルーペの中心座標ラベル
        LoupeCoordLabelColor = d.LoupeCoordLabelColor;
        LoupeCoordLabelBackgroundColor = d.LoupeCoordLabelBackgroundColor;
        LoupeCoordLabelFontSize = d.LoupeCoordLabelFontSize;
    }

    /// <summary>表示モードインデックスに対応するアイコンサイズを取得する。</summary>
    /// <param name="viewModeIndex">表示モード（0=特大, 1=大, 2=中）。 範囲外は中アイコン。</param>
    /// <returns>アイコンサイズ（ピクセル）。</returns>
    /// <remarks>フォルダビューと Undo 一覧のサムネイルサイズで共用する（単一所有・DRY）。</remarks>
    public int GetIconSize(int viewModeIndex) => viewModeIndex switch
    {
        0 => FolderExtraLargeIconSize,
        1 => FolderLargeIconSize,
        _ => FolderMediumIconSize,
    };
}

