using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using Rect = Avalonia.Rect;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.CaptureSession.AvaloniaUI;
using EsUtil.SaveEvidence.ZoomView.AvaloniaUI;
using EsUtil.SaveEvidence.CaptureSession;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.Icons;
using EsUtil.SaveEvidence.Logging;
using EsUtil.SaveEvidence.Models;
using EsUtil.SaveEvidence.Services;
using EsUtil.SaveEvidence.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using EsUtil.SaveEvidence.CropView.AvaloniaUI;
using EsUtil.SaveEvidence.CropView;
using EsUtil.Feature.Marquee.Core.Decoration;
using EsUtil.Feature.Marquee.Presenter;
using EsUtil.SaveEvidence.ZoomView;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Marquee.Core.Setting;
using EsUtil.Platform.AvaloniaUI.Interop;

namespace EsUtil.SaveEvidence.Views.Pages;

/// <summary>
/// 画像プレビュー・切出し・ズーム・ルーペ機能を提供するキャプチャページ。
/// </summary>
/// <remarks>
/// MainWindow から分離された画像キャプチャ関連の全機能を提供する。<br/>
/// CaptureSessionControl による領域選択・切出し、CropViewControl（ズーム + 領域選択 + ルーペを内包）
/// によるズーム制御、ルーペ表示を統一的に管理する。<br/>
/// MainWindow とは <see cref="MainWindow.Initialize"/>（CapturePage.Initialize）と
/// <see cref="MainWindow.StartSelectionMode"/>（キャプチャ開始）で連携する。<br/>
/// Ctrl ショートカット（保存 / コピー / 貼り付け / Undo / Redo / 切り出し / ズーム）は
/// <see cref="OnGlobalKeyDown"/> で一括処理する。<br/>
/// Undo / Redo スタックは DI シングルトン（<see cref="UndoStack"/>）から取得し、UndoPage と共有する。
/// </remarks>
public sealed partial class CapturePage : UserControl
{
    // ====================================================================
    // 依存
    // ====================================================================
    private MainViewModel _viewModel = null!;

    private ISettingsService _settingsService = null!;

    /// <summary>CaptureSessionControl.AvaloniaUI の公開 Facade（Crop セッションの窓口）。</summary>
    private CaptureSessionControl _captureSessionControl = null!;

    /// <summary>進行中の Crop セッションの Task。</summary>
    private Task<EsRectInt?>? _cropTask;

    // Undo スタック（DI から取得するシングルトン。UndoPage と共有）
    private readonly UndoStack _undoStack = null!;

    // 親Window参照（Loaded まで待たずに Initialize で確保）
    private Window? _parentWindow;

    // キャプチャ中フラグ
    private bool _isCaptureInProgress;

    // アプリケーションロガー
    private static readonly ILogger _logger = AppLog.CreateLogger("CapturePage");

    /// <summary>市松模様背景（DrawingBrush）キャッシュの最大保持数。</summary>
    private const int CheckerBrushCacheCapacity = 16;

    private static readonly ConcurrentDictionary<(Color Light, Color Dark, int Size), DrawingBrush> s_checkerBrushCache = new();

    // ====================================================================
    // 構築
    // ====================================================================

    public CapturePage()
    {
        InitializeComponent();

        if (Design.IsDesignMode) return;

        // Undo / Redo スタックは DI から取得する（UndoPage と同一インスタンスを共有するため）
        _undoStack = Program.ServiceProvider.GetRequiredService<UndoStack>();
    }

    /// <summary>MainWindow から初期化される。</summary>
    public void Initialize(MainViewModel viewModel, ISettingsService settingsService, Window parentWindow, CaptureSessionControl captureSessionControl)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _parentWindow = parentWindow;
        _captureSessionControl = captureSessionControl;

        SetupViewModelCallbacks();

        // CropViewControl（自己完結型複合コントロール）にロガーを注入する
        CropView.Log = _logger;

        // ズーム率の変化をスライダ・⊕⊖ ボタン・ViewModel へ反映する
        // （CropViewControl は特定コントロールに依存しないため、ホスト側で同期する）
        CropView.ZoomChanged += (_, e) =>
        {
            SyncZoomSlider(e.ZoomPercent);
            UpdateZoomButtonState(e.CanZoomIn, e.CanZoomOut);
            _viewModel.ZoomPercent = e.ZoomPercent;
            // 表示文字列はホスト側の書式（整数%表記）で生成する（e.ZoomPercent は実効ズーム率のため Fit 中も 0 にならない）
            _viewModel.ZoomDisplayText = $"{e.ZoomPercent:0}%";
        };

        // ViewModel のプロパティ変更時にズーム設定を適用する
        _viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.PreviewImage):
                    OnPreviewImageChanged();
                    // PreviewImage（EsBitmap）を表示境界で Avalonia 画像（IImage）へ変換して CropView へ設定する。
                    // WriteableBitmap を生成せず、EsBitmap を内包した EsBitmapAvaloniaImage を設定する（Skia 直描画）。
                    // 所有権は ViewModel（PreviewImage）が保持し、本コントロールは共有参照のみ（Dispose しない）。
                    CropView.Source = _viewModel.PreviewImage is { } bmp ? new EsBitmapAvaloniaImage(bmp) : null;
                    break;
                case nameof(MainViewModel.SaveFolderPath):
                    // 保存先フォルダの変更を保存ボタンの ToolTip に反映する
                    UpdateSaveButtonToolTip();
                    break;
                case nameof(MainViewModel.IsAutoCropActive):
                case nameof(MainViewModel.DetectedRects):
                case nameof(MainViewModel.HoveredRect):
                    // 自動切り出しの矩形オーバーレイを更新する
                    UpdateAutoCropOverlay();
                    break;
            }
        };

        // 設定変更時に市松模様背景を更新
        SubscribeCheckerSizeChange();

        // 設定変更時（中央寄せ/原点寄せ）に画像配置を即時反映する
        SubscribeCenterAlignChange();

        // 初期のズーム率をスライダに反映し、⊕⊖ ボタン・表示テキストの状態を CropViewControl で初期化する
        SyncZoomSlider(_viewModel.ZoomPercent);
        UpdateZoomButtonState(CropView.CanZoomIn, CropView.CanZoomOut);

        // マウスポインタ移動でステータスバーの座標表示（ドメイン座標）と自動切り出しのホバーを更新する
        // （旧実装は自動切り出し用の Canvas が入力を占有していたため、ホイールでズーム・パンができなかった）
        CropView.PointerMoved += OnImagePointerMoved;
        CropView.PointerExited += OnImagePointerExited;
        CropView.PointerPressed += OnImageDoubleClicked;

        // ビューポート（パン・ズーム・表示サイズ）の変化を Crop オーバーレイへ通知する
        // （暗転マスク・選択枠・ルーペの追従。ScrollViewer 連携の ScrollChanged に相当する中継）。
        // Crop 未実行時は CaptureSessionControl 側で無視されるため、常時購読で問題ない。
        CropView.ViewportChanged += (_, _) =>
        {
            _captureSessionControl.NotifyCropViewportChanged();
            UpdateAutoCropOverlay();
        };

        // 初期の市松模様背景を設定
        UpdateCheckerboardBackground();

        // 切出しボタンの初期ツールチップ・ラベルを設定する（AXAML の既定値ではなく UpdateCropButtonText の値を反映）
        UpdateCropButtonText();

        // ツールバーのアイコン（SVG）を設定する
        SetupToolbarIcons();

        // ファイル名テンプレートの初期幅をテキスト内容に合わせて設定する
        UpdateFileNameTemplateWidth(FileNameTemplateBox);

        // ファイル名テンプレートの ToolTip を設定する（{date}/{time} はマークアップ拡張と衝突するためコード側で構築）
        ToolTip.SetTip(FileNameTemplateBox, BuildFileNameTemplateToolTip());

        // 保存ボタンの ToolTip（保存先フォルダ付き）を設定する
        UpdateSaveButtonToolTip();

        // メニューを開かずに効く Ctrl ショートカット（Ctrl+S 等）を登録する
        // Tunnel で登録し、メニューのアクセラレータ文字（(_X)）は Alt 用として削除済み（Alt は無効化）
        AddHandler(InputElement.KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Capture メニューにホットキー設定（SelectScreenHotKey 等）を表示する
        // ホットキーはグローバル登録（WM_HOTKEY）のため、Ctrl ショートカット（Ctrl+Q 等）とは独立して動作する
        UpdateCaptureHotKeyGestures();
        _settingsService.Current.PropertyChanged -= OnSettingsPropertyChanged;
        _settingsService.Current.PropertyChanged += OnSettingsPropertyChanged;
    }

    /// <summary>設定プロパティ変更時のハンドラ。ホットキー設定の変更をメニュー表示へ反映する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">プロパティ変更イベント。</param>
    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Settings.SelectScreenHotKey):
            case nameof(Settings.WindowSelectHotKey):
            case nameof(Settings.AreaSelectHotKey):
                UpdateCaptureHotKeyGestures();
                break;
        }
    }

    /// <summary>Capture メニュー項目の InputGesture にホットキー設定を反映する。</summary>
    /// <remarks>
    /// ホットキーが未設定（Key.None）の場合は InputGesture を表示しない。<br/>
    /// メニュー項目の InputGesture は表示のみで、キー処理はグローバルホットキー（WM_HOTKEY）が担当する。
    /// </remarks>
    private void UpdateCaptureHotKeyGestures()
    {
        CaptureScreenMenuItem.InputGesture = ToKeyGesture(_settingsService.Current.SelectScreenHotKey);
        CaptureWindowMenuItem.InputGesture = ToKeyGesture(_settingsService.Current.WindowSelectHotKey);
        CaptureAreaMenuItem.InputGesture = ToKeyGesture(_settingsService.Current.AreaSelectHotKey);
    }

    /// <summary>ホットキー設定を KeyGesture に変換する。未設定（Key.None）の場合は null を返す。</summary>
    /// <param name="setting">ホットキー設定。</param>
    /// <returns>KeyGesture。未設定の場合は null。</returns>
    private static KeyGesture? ToKeyGesture(HotKeySetting setting)
    {
        if (setting.Key == EsKey.None) return null;
        return new KeyGesture(setting.Key.ToAvaloniaKey(), ToKeyModifiers(setting.Modifiers));
    }

    /// <summary>HotKeyModifiers を KeyModifiers に変換する。</summary>
    /// <param name="modifiers">ホットキー修飾キー。</param>
    /// <returns>Avalonia の KeyModifiers。</returns>
    private static KeyModifiers ToKeyModifiers(HotKeyModifiers modifiers)
    {
        var result = KeyModifiers.None;
        if ((modifiers & HotKeyModifiers.Control) != 0) result |= KeyModifiers.Control;
        if ((modifiers & HotKeyModifiers.Alt) != 0) result |= KeyModifiers.Alt;
        if ((modifiers & HotKeyModifiers.Shift) != 0) result |= KeyModifiers.Shift;
        if ((modifiers & HotKeyModifiers.Windows) != 0) result |= KeyModifiers.Meta;
        return result;
    }

    /// <summary>メニューを開かずに効く Ctrl ショートカットと Escape（自動切り出しのキャンセル）を処理する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">キーイベント。</param>
    /// <remarks>
    /// Ctrl + メニューのアクセラレータ文字（S/C/V/Z/Y/R）の組み合わせで動作する。<br/>
    /// Capture 系（スクリーン / ウィンドウ / 範囲選択）はグローバルホットキー（WM_HOTKEY）で動作するため対象外。<br/>
    /// Escape は自動切り出し中のキャンセルとして扱う（切り出しモードの Escape と同じ操作）。<br/>
    /// テキスト入力中（<see cref="FileNameTemplateBox"/> 等）はテキスト編集ショートカット（Ctrl+C/V/Z/Y）を優先するため無視する。<br/>
    /// Alt はメニュー展開用のため使用しない（Alt アクセラレータは Header の (_X) 削除で無効化済み）。<br/>
    /// 修飾キー判定は <see cref="IsCtrlShortcut"/> に委譲する（単体テスト対象）。
    /// </remarks>
    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        // 自動切り出し中は Escape でキャンセルする（切り出しモードと同じ操作）。
        // 自動切り出しはオーバーレイコントロールを持たない（ViewModel の状態で表示する）ため、
        // テキスト入力中でも優先して処理する。
        if (e.Key == Key.Escape && _viewModel.IsAutoCropActive)
        {
            e.Handled = true;
            _viewModel.CancelAutoCropCommand.Execute(null);
            return;
        }

        // テキスト入力中はテキスト編集ショートカットを優先する（Ctrl+C/V/Z/Y 等を横取りしない）
        if (e.Source is TextBox) return;

        // Ctrl 押下時のみ処理（Alt / Shift との併用は対象外）
        if (!IsCtrlShortcut(e.KeyModifiers)) return;

        switch (e.Key)
        {
            case Key.S: // File - 画像を保存
                e.Handled = true;
                _ = _viewModel.SaveImageAsync();
                break;
            case Key.C: // Clipboard - コピー
                e.Handled = true;
                _ = _viewModel.CopyToClipboard();
                break;
            case Key.V: // Clipboard - 貼り付け
                e.Handled = true;
                _ = PasteFromClipboard();
                break;
            case Key.Z: // Image - 元に戻す
                e.Handled = true;
                RestoreUndo();
                break;
            case Key.Y: // Image - 元に戻すを取り消す
                e.Handled = true;
                RestoreRedo();
                break;
            case Key.R: // Crop - 切り出し
                e.Handled = true;
                OnCropButtonClick(sender, e);
                break;
            case Key.OemPlus: // Ctrl + = ズームイン（⊖⊕ボタンと同じ段階遷移）
            case Key.Add:
                e.Handled = true;
                ZoomInByButton();
                break;
            case Key.OemMinus: // Ctrl + - = ズームアウト
            case Key.Subtract:
                e.Handled = true;
                ZoomOutByButton();
                break;
        }
    }

    /// <summary>Ctrl ショートカットの対象となる修飾キーかどうかを判定する。</summary>
    /// <param name="modifiers">キーイベントの修飾キー。</param>
    /// <returns>Ctrl のみ押下中（Alt / Shift との併用なし）の場合に true。</returns>
    /// <remarks>
    /// Ctrl 押下時のみ処理し、Alt / Shift との併用は対象外とする。<br/>
    /// Alt はメニュー展開用（Alt アクセラレータは Header の (_X) 削除で無効化済み）、
    /// Shift はテキスト選択等の別操作と競合するため除外する。<br/>
    /// 単体テストから検証するため internal で公開する。
    /// </remarks>
    internal static bool IsCtrlShortcut(KeyModifiers modifiers)
    {
        if ((modifiers & KeyModifiers.Control) == 0) return false;
        if ((modifiers & (KeyModifiers.Alt | KeyModifiers.Shift)) != 0) return false;
        return true;
    }

    /// <summary>保存ボタンの ToolTip（保存先フォルダ付き）を更新する。</summary>
    /// <remarks>
    /// 「画像を保存」と現在の保存先フォルダ（<see cref="MainViewModel.SaveFolderPathResolved"/>）を表示する。<br/>
    /// 保存先フォルダは <see cref="MainViewModel.PropertyChanged"/> の <c>SaveFolderPath</c> 通知時に再構築される。
    /// </remarks>
    private void UpdateSaveButtonToolTip()
    {
        var folder = _viewModel.SaveFolderPathResolved;
        var tip = string.IsNullOrEmpty(folder)
            ? "画像を保存"
            : $"画像を保存\n保存先: {folder}";
        ToolTip.SetTip(SaveButton, tip);
    }

    /// <summary>ツールバー（保存 / キャプチャ / 切り出し）とステータスバー（ズーム）のアイコンを SVG から設定する。</summary>
    /// <remarks>各アイコンのジオメトリは <see cref="Icons.IconLoader.LoadGeometry"/> で読み込む。</remarks>
    private void SetupToolbarIcons()
    {
        SaveIcon.Data = IconLoader.LoadGeometry("ic_fluent_save_24_regular.svg");
        ScreenSelectIcon.Data = IconLoader.LoadGeometry("ic_fluent_rectangle_landscape_24_regular.svg");
        WindowSelectIcon.Data = IconLoader.LoadGeometry("ic_fluent_window_24_regular.svg");
        AreaSelectIcon.Data = IconLoader.LoadGeometry("ic_fluent_select_object_24_regular.svg");
        CropIcon.Data = IconLoader.LoadGeometry("ic_fluent_crop_24_regular.svg");

        // ステータスバーのズーム（⊕⊖）アイコン
        ZoomOutIcon.Data = IconLoader.LoadGeometry("ic_fluent_subtract_circle_24_regular.svg");
        ZoomInIcon.Data = IconLoader.LoadGeometry("ic_fluent_add_circle_24_regular.svg");
    }

    /// <summary>ファイル名テンプレートの ToolTip（プレースホルダ・オートインクリメントの説明）を構築する。</summary>
    /// <returns>ToolTip として表示するパネル。</returns>
    /// <remarks>
    /// <c>{date}</c> / <c>{time}</c> は XAML ではマークアップ拡張と解釈されるため、<br/>
    /// コードビハインドで構築する（AXAML の属性/要素構文では表現できない）。
    /// </remarks>
    private static object BuildFileNameTemplateToolTip()
    {
        var now = DateTime.Now;

        // 幅はコンテンツの自然幅（最長の NoWrap 行）に合わせ、長い行は折り返す（画面からはみ出さない上限）
        var panel = new StackPanel { MaxWidth = 600 };

        // 見出し
        panel.Children.Add(new TextBlock
        {
            Text = "保存ファイル名テンプレート",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 4),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "テンプレート文字列から保存ファイル名を生成します。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 6),
        });

        // プレースホルダ
        panel.Children.Add(new TextBlock
        {
            Text = "プレースホルダ",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 2),
        });
        panel.Children.Add(new TextBlock
        {
            Text =$"{{date}} → 日付（例: {now:yyyyMMdd} = yyyyMMdd）",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text =$"{{time}} → 時刻（例: {now:HHmmss} = HHmmss）",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{{yyyy}} → 年（例: {now:yyyy} = yyyy）",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{{MM}} → 月（例: {now:MM} = MM）",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{{dd}} → 日（例: {now:dd} = dd）",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{{HH}} → 時（例: {now:HH} = HH）",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{{mm}} → 分（例: {now:mm} = mm）",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{{ss}} → 秒（例: {now:ss} = ss）",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 6),
        });

        // オートインクリメント
        panel.Children.Add(new TextBlock
        {
            Text = "オートインクリメント",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 2),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "一番右の数値連続が保存ごとに +1 されます。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "必要桁数が増えない限り桁数は維持されます。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = "（01 → 02、99 → 100、099 → 100）",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 6),
        });

        // 具体例
        panel.Children.Add(new TextBlock
        {
            Text = "例",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 2),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"screenshot_{{date}}_{{time}}_01.png →",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 1),
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"screenshot_{now:yyyyMMdd}_{now:HHmmss}_01.png",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 0),
        });

        return panel;
    }

    /// <summary>ViewModel にコールバックを設定する。</summary>
    private void SetupViewModelCallbacks()
    {
        _viewModel.StartSelectionMode = captureType =>
        {
            if (_parentWindow is MainWindow mw)
                mw.StartSelectionMode(captureType);
        };
    }

    // ====================================================================
    // PropertyChanged
    // ====================================================================

    /// <summary>ViewModel の PreviewImage 変更時に元画像参照を同期する。</summary>
    private void OnPreviewImageChanged()
    {
        if (_isCaptureInProgress) return;
        CancelCropSession();

        UpdateCropButtonText();
    }

    // ====================================================================
    // 市松模様背景
    // ====================================================================

    private void SubscribeCheckerSizeChange()
    {
        // 再読込時に再購読しても重複登録されないよう、先に解除してから購読する
        _viewModel.Settings.PropertyChanged -= OnCheckerSizePropertyChanged;
        _viewModel.Settings.PropertyChanged += OnCheckerSizePropertyChanged;
    }

    private void OnCheckerSizePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 市松模様のサイズ・色 A・色 B のいずれが変更されても再描画する（ルーペ設定から画像設定へ移管）
        if (e.PropertyName is nameof(Settings.PreviewCheckerSize)
            or nameof(Settings.PreviewCheckerColorA)
            or nameof(Settings.PreviewCheckerColorB))
        {
            UpdateCheckerboardBackground();
        }
    }

    /// <summary>設定変更（中央寄せ/原点寄せ）を監視して画像配置へ即時反映する。</summary>
    /// <remarks>
    /// Settings.CenterAlign は設定画面の CheckBox から変更される。<br/>
    /// 設定変更時はズームが変化しないため、ここで明示的に <see cref="CropViewControl.CenterAlign"/> へ反映する<br/>
    /// （スクロール不要時のみの配置切替）。
    /// </remarks>
    private void SubscribeCenterAlignChange()
    {
        _viewModel.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Settings.CenterAlign))
                CropView.CenterAlign = _viewModel.CenterAlign;
        };
    }

    private void UpdateCheckerboardBackground()
    {
        try
        {
            var settings = _viewModel.Settings;

            // 市松模様のタイルサイズ規則（0 以下 = 描かない / 1 は下限へ切り上げ）はコアが単一所有する
            // （旧実装は本メソッドが 0 を 2 へ読み替えており、MewUI の「0 = 描かない」と意味が食い違っていた）
            var checkerSize = ZoomViewAppearance.ResolveCheckerTileSize(settings.PreviewCheckerSize);
            if (checkerSize <= 0)
            {
                CropView.ViewportBackground = null;
                return;
            }

            // 市松模様の色は画像設定（PreviewCheckerColorA / B）から取得する（ルーペと共通）
            var lightColor = Color.Parse(settings.PreviewCheckerColorA);
            var darkerColor = Color.Parse(settings.PreviewCheckerColorB);
            (Color Light, Color Dark, int Size) key = (lightColor, darkerColor, checkerSize);

            var brush = CapacityLimitedCache.GetOrAdd(
                s_checkerBrushCache, CheckerBrushCacheCapacity, key,
                static k => BuildCheckerBrush(k.Light, k.Dark, k.Size));

            CropView.ViewportBackground = brush;
        }
        catch
        {
            // 市松模様の描画リソース構築失敗（不正な色コード等）時は背景を更新しない
        }
    }

    /// <summary>市松模様のタイル描画ブラシを構築する。</summary>
    /// <param name="light">明色（色 A）。</param>
    /// <param name="dark">暗色（色 B）。</param>
    /// <param name="size">1 タイルのサイズ（ピクセル）。</param>
    /// <returns>タイル表示の <see cref="DrawingBrush"/>。</returns>
    private static DrawingBrush BuildCheckerBrush(Color light, Color dark, int size)
    {
        var lightBrush = new SolidColorBrush(light);
        var darkerBrush = new SolidColorBrush(dark);

        var drawingGroup = new DrawingGroup();
        drawingGroup.Children.Add(new GeometryDrawing
        {
            Brush = darkerBrush,
            Geometry = new RectangleGeometry(new Rect(0, 0, size * 2, size * 2))
        });
        drawingGroup.Children.Add(new GeometryDrawing
        {
            Brush = lightBrush,
            Geometry = new RectangleGeometry(new Rect(0, 0, size, size))
        });
        drawingGroup.Children.Add(new GeometryDrawing
        {
            Brush = lightBrush,
            Geometry = new RectangleGeometry(new Rect(size, size, size, size))
        });

        return new DrawingBrush
        {
            Drawing = drawingGroup,
            TileMode = TileMode.Tile,
            DestinationRect = new RelativeRect(0, 0, size * 2, size * 2, RelativeUnit.Absolute)
        };
    }

    /// <summary>現在の画像をUndoスタックに保存する。</summary>
    public void PushUndo()
    {
        if (_viewModel.PreviewImage is { } image)
            _undoStack.Push(image);
    }

    /// <summary>Undo スタックから画像を復元する（現在の画像は Redo スタックへ退避される）。</summary>
    public void RestoreUndo()
    {
        // 現在のプレビュー画像（EsBitmap）を Undo へ渡す（Undo スタックは EsBitmap を保持）。
        var current = _viewModel.PreviewImage;
        using var prev = _undoStack.Undo(current);
        if (prev is not null) RestoreImage(prev);
    }

    /// <summary>Redo スタックから画像を復元する。</summary>
    public void RestoreRedo()
    {
        using var redo = _undoStack.Redo();
        if (redo is not null) RestoreImage(redo);
    }

    /// <summary>復元した画像へプレビューを差し替える。</summary>
    /// <param name="image">復元する画像（EsBitmap）。所有権は ViewModel へ移る。</param>
    private void RestoreImage(EsBitmap image)
    {
        _viewModel.PreviewImage?.Dispose();
        _viewModel.SetPreviewImage(image);
    }

    /// <summary>キャプチャ中フラグを設定する（MainWindowから呼ばれる）。</summary>
    public void SetIsCaptureInProgress(bool value) => _isCaptureInProgress = value;

    // ====================================================================
    // 画像系ボタン
    // ====================================================================

    private void OnSaveButtonClick(object? sender, RoutedEventArgs e) => _ = _viewModel.SaveImageAsync();
    private void OnCopyButtonClick(object? sender, RoutedEventArgs e) => _ = _viewModel.CopyToClipboard();
    private async void OnPasteButtonClick(object? sender, RoutedEventArgs e) => await PasteFromClipboard();
    private void OnUndoButtonClick(object? sender, RoutedEventArgs e) => RestoreUndo();

    /// <summary>Redo ボタンのクリック時に画像を Redo スタックから復元する。</summary>
    private void OnRedoButtonClick(object? sender, RoutedEventArgs e) => RestoreRedo();

    /// <summary>ファイル名テンプレートのテキスト変更時に幅を自動調整する（TextChanged イベント）。</summary>
    /// <param name="sender">イベント送信元（TextBox）。</param>
    /// <param name="e">テキスト変更イベント。</param>
    /// <remarks>テキスト幅の計算は <see cref="UpdateFileNameTemplateWidth"/> に委譲する。</remarks>
    private void OnFileNameTemplateTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box) UpdateFileNameTemplateWidth(box);
    }

    /// <summary>ファイル名テンプレートの幅をテキスト内容に応じて自動調整する。</summary>
    /// <param name="box">対象の TextBox。</param>
    /// <remarks>
    /// <see cref="FormattedText"/> でテキスト幅を計測し、左右パディングを加算して
    /// MinWidth〜MaxWidth の範囲にクランプした値を Width に設定する。<br/>
    /// テキストが短い場合は MinWidth（220）、長い場合は MaxWidth（260）に固定され、<br/>
    /// 中間の長さではテキスト全体が収まる幅になる（超過時は TextBox 内部でスクロール）。
    /// </remarks>
    private void UpdateFileNameTemplateWidth(TextBox box)
    {
        // TextBox の実際のフォント情報を使用してテキスト幅を計測する
        var typeface = new Typeface(box.FontFamily, box.FontStyle, box.FontWeight);
        var formatted = new FormattedText(
            box.Text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, box.FontSize, Brushes.Black);

        // 左右パディング（TextBox テンプレート内の余白・ボーダー）+ 余裕
        const double horizontalPadding = 24;
        var width = Math.Ceiling(formatted.Width) + horizontalPadding;
        box.Width = Math.Clamp(width, box.MinWidth, box.MaxWidth);
    }

    /// <summary>クリップボードから貼り付けを行う。</summary>
    public async Task PasteFromClipboard()
        => await _viewModel.PasteFromClipboard();

    // ====================================================================
    // 切出し (Crop)
    // ====================================================================

    private async void OnCropButtonClick(object? sender, RoutedEventArgs e)
    {
        // どの操作を行うか（開始 / 確定 / キャンセル）の判断はコア（CropSessionActions）が単一所有する。
        // ホストは返された操作を効果へ写すだけにする（同じ 3 分岐を UI に書かない）
        switch (_captureSessionControl.ResolveCropAction())
        {
            case CropSessionAction.Start:
                await StartCropSessionAsync();
                break;
            case CropSessionAction.Confirm:
                // 開始済み・矩形あり → 現在の選択矩形で確定（既に待機しているタスクが完了する）
                _captureSessionControl.Confirm();
                break;
            default:
                // 開始済み・矩形なし → キャンセル（矩形選択前にボタンを押した場合の抜け道）
                CancelCropSession();
                break;
        }
    }

    /// <summary>
    /// 切出しセッションを開始し、確定・キャンセルを待機する。
    /// </summary>
    /// <remarks>
    /// StartCropAsync はイベント購読 → モード開始 → 確定待機 → 後片付けを完結して行う。<br/>
    /// 確定（Enter / ダブルクリック / Confirm）で切り出し範囲（コア型）、キャンセルで 0x0 の空矩形を返す。<br/>
    /// 開始できなかった場合（他のセッションが実行中）は結果 null が即時返る（無駄な待機をしない）。
    /// </remarks>
    private async Task StartCropSessionAsync()
    {
        // 設定は MainWindow の Settings.PropertyChanged 購読で常に最新のため、ここでは同期しない
        // プレビュー状態（ScrollOffset / ZoomPercent）は CaptureSessionControl.AvaloniaUI が Marquee イベント / 表示状態から導出する
        // 切出し中のホイール操作（Ctrl+ホイールのズーム・通常ホイールのパン）は CropViewControl が内部で完結する
        // 切り出しの対象画像は表示中の画像（CropView.Source）から解決されるため、画像を別引数で渡さない
        _cropTask = _captureSessionControl.StartCropAsync(CropView, _parentWindow!);
        UpdateCropButtonText();

        var result = await _cropTask;
        _cropTask = null;
        UpdateCropButtonText();

        // 確定時のみ切り出しを実行する（開始できなかった場合は結果 null、キャンセルは空の矩形。
        // 空の判定はコアの契約（EsRectInt.IsEmpty）が単一所有する）
        if (result is { } bounds && !bounds.IsEmpty)
        {
            ExecuteCropFromBounds(bounds);
        }
    }

    /// <summary>確定した切り出し範囲（コア型の画面物理矩形）で画像を切り出す。</summary>
    /// <param name="bounds">切り出す矩形（コア型。UI 型へ変換せずそのまま適用先へ渡す）。</param>
    /// <remarks>
    /// 適用先（<c>CropPreviewCommand</c>）はコア型（<see cref="EsRectInt"/>）を要求するため、<br/>
    /// 境界（UI 層）で変換せずコア型のまま渡す（UI 型を渡すと型が一致せず例外になる）。
    /// </remarks>
    private void ExecuteCropFromBounds(EsRectInt bounds)
    {
        var srcBmp = _viewModel.PreviewImage;
        if (srcBmp is null) return;

        _undoStack.Push(srcBmp);
        _viewModel.CropPreviewCommand.Execute(bounds);
        UpdateCropButtonText();
    }

    private void UpdateCropButtonText()
    {
        // 「切り出し中か」の真のソースは排他スロット（Facade がコアの規則を適用する）
        var active = _captureSessionControl.IsCropping;
        if (CropButton is not null)
            ToolTip.SetTip(CropButton, active
                ? "切り出し（確定して切り抜く）"
                : "切り出し（切出しモード開始）");
    }

    /// <summary>進行中の Crop セッションをキャンセルする。</summary>
    private void CancelCropSession()
    {
        if (_captureSessionControl.IsCropping)
        {
            _captureSessionControl.Cancel();
            _cropTask = null;
            UpdateCropButtonText();
        }
    }

    // ====================================================================
    // メニューハンドラ
    // ====================================================================

    private async void OnPasteMenuClick(object? sender, RoutedEventArgs e)
    {
        try { await PasteFromClipboard(); }
        catch
        {
            // 貼り付け失敗（クリップボードに画像がない等）は何もしない
        }
    }

    private void OnUndoMenuClick(object? sender, RoutedEventArgs e) => RestoreUndo();

    /// <summary>メニュー「Image - 元に戻すの取り消し」のクリック時に画像を Redo スタックから復元する。</summary>
    private void OnRedoMenuClick(object? sender, RoutedEventArgs e) => RestoreRedo();

    private void OnCaptureScreenClick(object? sender, RoutedEventArgs e)
        => StartCapture(CaptureType.ScreenSelect, _logger.LogCaptureMenuScreen);

    private void OnCaptureWindowClick(object? sender, RoutedEventArgs e)
        => StartCapture(CaptureType.WindowSelect, _logger.LogCaptureMenuWindow);

    private void OnCaptureAreaClick(object? sender, RoutedEventArgs e)
        => StartCapture(CaptureType.AreaSelect, _logger.LogCaptureMenuArea);

    /// <summary>メニューからのキャプチャ開始を共通処理する（ログ記録 + MainWindow への委譲）。</summary>
    /// <param name="captureType">開始するキャプチャ種別。</param>
    /// <param name="log">メニュークリックのログメソッド。</param>
    private void StartCapture(CaptureType captureType, Action log)
    {
        log();
        if (_parentWindow is MainWindow mw)
            mw.StartSelectionMode(captureType);
    }

    private void OnCropMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.CanCopy)
            OnCropButtonClick(sender, e);
    }

    // ====================================================================
    // ズーム
    // ====================================================================

    private void OnZoomInButtonClick(object? sender, RoutedEventArgs e) => CropView.ZoomIn();
    private void OnZoomOutButtonClick(object? sender, RoutedEventArgs e) => CropView.ZoomOut();

    /// <summary>Fit 表示ボタンのクリック時に画像をウィンドウに合わせて表示する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ルーティングイベント。</param>
    /// <remarks>ズーム率を 0（自動 Fit）に設定する。ズーム率は CropViewControl が適用し、ZoomChanged でスライダ・ViewModel へ同期される。</remarks>
    private void OnZoomFitButtonClick(object? sender, RoutedEventArgs e) => CropView.ZoomFit();

    /// <summary>ズームインする（Ctrl+キー等の外部からの要求用。⊖⊕ボタンと同じ遷移）。</summary>
    public void ZoomInByButton() => CropView.ZoomIn();

    /// <summary>ズームアウトする（Ctrl+キー等の外部からの要求用。⊖⊕ボタンと同じ遷移）。</summary>
    public void ZoomOutByButton() => CropView.ZoomOut();

    /// <summary>拡大率スライダの値変更時にズームを適用する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">値変更イベント。</param>
    /// <remarks>プログラム側の同期（<see cref="SyncZoomSlider"/>）による変更は無視する。</remarks>
    private void OnZoomSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSyncingZoomSlider) return;
        // ズーム率は double で設定する（ApplyZoomFromSlider は廃止・ZoomPercent に統合）
        CropView.ZoomPercent = ZoomSlider.Value;
    }

    /// <summary>スライダ同期中の再入防止フラグ。</summary>
    private bool _isSyncingZoomSlider;

    /// <summary>スライダ同期時の値比較許容差。</summary>
    private const double ZoomSliderEpsilon = 0.01;

    /// <summary>現在のズーム率をスライダに反映する（再入防止付きラッパー）。</summary>
    /// <param name="zoom">現在の実効ズーム率（%）。Fit 中は実効倍率。</param>
    /// <remarks>
    /// 実効ズーム率の変化（<see cref="ZoomViewControl.ZoomChanged"/>）を受けて、スライダ値を現在のズーム率へ同期する。<br/>
    /// Fit 中も実際に表示されている実効倍率をスライダに反映する（スライダ範囲 <see cref="ZoomSlider.Minimum"/> 〜 <see cref="ZoomSlider.Maximum"/> にクランプ）。<br/>
    /// スライダへの値設定で発火する <see cref="OnZoomSliderValueChanged"/> を無視するためにフラグで保護する。
    /// </remarks>
    private void SyncZoomSlider(double zoom)
    {
        _isSyncingZoomSlider = true;
        try
        {
            var sliderValue = Math.Clamp(zoom, ZoomSlider.Minimum, ZoomSlider.Maximum);
            if (Math.Abs(ZoomSlider.Value - sliderValue) > ZoomSliderEpsilon)
            {
                ZoomSlider.Value = sliderValue;
            }
        }
        finally
        {
            _isSyncingZoomSlider = false;
        }
    }

    /// <summary>ズームイン・アウトボタンの Enable/Disable をズーム可否に応じて更新する。</summary>
    /// <param name="canZoomIn">ズームイン可能かどうか。</param>
    /// <param name="canZoomOut">ズームアウト可能かどうか。</param>
    /// <remarks>
    /// <see cref="ZoomViewControl.ZoomChanged"/> で通知される可否、または <see cref="ZoomViewControl.CanZoomIn"/> /
    /// <see cref="ZoomViewControl.CanZoomOut"/>（初期表示時の参照）に基づいて、⊖⊕ ボタンの <c>IsEnabled</c> を更新する。<br/>
    /// 可否はコア（<c>EsUtil.SaveEvidence.ZoomView</c> のズーム遷移仕様）で判定済みであり、本クラスは判定を持たない。
    /// </remarks>
    private void UpdateZoomButtonState(bool canZoomIn, bool canZoomOut)
    {
        ZoomInButton.IsEnabled = canZoomIn;
        ZoomOutButton.IsEnabled = canZoomOut;
    }

    /// <summary>拡大率フライアウトメニューの選択時にズーム率を設定する。</summary>
    /// <param name="sender">選択された MenuItem。</param>
    /// <param name="e">ルーティングイベント。</param>
    /// <remarks>Tag は拡大率の数値（0 = 自動）。自動選択後はスライダを最小値に合わせる。</remarks>
    private void OnZoomFlyoutItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: { } tag }) return;
        if (!int.TryParse(tag.ToString(), out var zoom)) return;
        // ズーム率は CropViewControl が適用し、ZoomChanged でスライダ・ViewModel へ同期される
        CropView.ZoomPercent = zoom;
    }

    /// <summary>マウスポインタ移動時に、ポインタ位置のドメイン座標（画像ピクセル）をステータスバーへ表示する。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    /// <remarks>
    /// ビューポート座標 → コンテンツ座標（元画像ピクセル）の変換は CropView が行う。<br/>
    /// 画像外（ビューポートの空き領域）では "-" を表示する。<br/>
    /// 画像サイズ（ドメイン座標）は <see cref="MainViewModel.ImageSizeDisplayText"/> にバインディングで表示される。<br/>
    /// 自動切り出し中は、同じ位置の検出矩形をホバーとして設定する（判定は <see cref="MainViewModel.HitDetectedRect"/> が単一所有する）。
    /// </remarks>
    private void OnImagePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel.PreviewImage is not { } img)
        {
            _viewModel.PointerDisplayText = "-";
            UpdateAutoCropHover(null);
            return;
        }

        var viewportPos = e.GetPosition(CropView);
        // 画像内かどうかを判定する（画像原点を起点とした表示サイズ内か。矩形は 1 回の計算で取得）
        var imageRect = CropView.ImageRectViewport();
        var displayPoint = viewportPos - imageRect.Position;
        if (displayPoint.X < 0 || displayPoint.Y < 0 ||
            displayPoint.X >= imageRect.Width || displayPoint.Y >= imageRect.Height)
        {
            _viewModel.PointerDisplayText = "-";
            UpdateAutoCropHover(null);
            return;
        }

        // 表示座標 → ドメイン座標（元画像ピクセル）へ変換し、画像範囲内にクランプする
        var contentPos = ViewportToContentDomain(viewportPos);
        var x = (int)Math.Clamp(contentPos.X, 0, img.Size.Width - 1);
        var y = (int)Math.Clamp(contentPos.Y, 0, img.Size.Height - 1);
        _viewModel.PointerDisplayText = $"{x}, {y}";
        UpdateAutoCropHover(new Point(x, y));
    }

    /// <summary>マウスポインタがプレビュー領域から外れたときに、座標表示と自動切り出しのホバーを消す。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    /// <remarks>移動イベントは領域外では発生しないため、ホバー強調の残留を防ぐために本ハンドラを購読する。</remarks>
    private void OnImagePointerExited(object? sender, PointerEventArgs e)
    {
        _viewModel.PointerDisplayText = "-";
        UpdateAutoCropHover(null);
    }

    // ====================================================================
    // ポインターイベント
    // ====================================================================

    private void OnImageAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(CropView);
        var isRight = point.Properties.IsRightButtonPressed;
        var hasCropTask = _cropTask is not null;
        _logger.LogPreviewPointerPressed(isRight, hasCropTask);
        if (isRight && hasCropTask)
        {
            ShowCropContextMenu();
            e.Handled = true;
        }
        else if (isRight && _viewModel.IsAutoCropActive)
        {
            // 自動切り出しモード中は右クリックでキャンセルする
            _viewModel.CancelAutoCropCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ====================================================================
    // 自動切り出し（LineDetection 矩形選択）
    // ====================================================================

    /// <summary>自動切り出しで検出された矩形の枠線色（通常・水色）。</summary>
    private static readonly DrawingColor s_autoCropBorderColor = DrawingColor.FromArgb(255, 0, 191, 255);

    /// <summary>自動切り出しでホバー中の矩形の枠線色（赤色）。</summary>
    private static readonly DrawingColor s_autoCropHoverBorderColor = DrawingColor.FromArgb(255, 255, 59, 48);

    /// <summary>自動切り出しでホバー中の矩形の塗り色（赤色の 16%）。</summary>
    private static readonly DrawingColor s_autoCropHoverFillColor = DrawingColor.FromArgb(40, 255, 59, 48);

    /// <summary>自動切り出しの矩形枠線の太さ（DIP）。</summary>
    private const double AutoCropStrokeThickness = 2.0;

    /// <summary>自動切り出しのホバー強調に使う外観（内側の塗りと枠線）。</summary>
    /// <remarks>色・枠線の太さ・表示可否は描画にだけ必要な設定であるため、外観（<see cref="MarqueeAppearance"/>）が単一所有する。</remarks>
    private static readonly MarqueeAppearance s_autoCropHighlightAppearance = MarqueeAppearance.Default with
    {
        ShowSizeLabel = false,
        ShowCoordLabel = false,
        ShowOverlay = false,
        ShowOuterBorder = false,
        ShowInnerFill = true,
        ShowInnerBorder = true,
        InnerFillColor = s_autoCropHoverFillColor,
        InnerBorderColor = s_autoCropHoverBorderColor,
        InnerBorderThickness = AutoCropStrokeThickness,
    };

    /// <summary>自動切り出しで強調しないときの外観（枠線も内側の塗りもラベルも描かない）。</summary>
    private static readonly MarqueeAppearance s_autoCropIdleAppearance = MarqueeAppearance.Default with
    {
        ShowSizeLabel = false,
        ShowCoordLabel = false,
        ShowOverlay = false,
        ShowOuterBorder = false,
    };

    /// <summary>自動切り出しの矩形オーバーレイ（枠線とホバー強調）を現在のビューポート状態に合わせて更新する。</summary>
    /// <remarks>
    /// 1. 検出矩形の枠線は <see cref="CropViewControl.Rects"/> へ<b>ドメイン座標</b>で渡す（座標変換は CropView が単一所有する）。<br/>
    /// 2. ホバー中の 1 件は <see cref="CropViewControl.Rects"/> から外し、<see cref="AutoCropHighlight"/>（Marquee）の<br/>
    ///    ハイライト矩形 + 内側の外観（<see cref="MarqueeAppearance"/>）で「枠線 + 半透明の塗り」として強調する<br/>
    ///    （ハイライト矩形は DIP 座標で渡す。Marquee の座標変換ポートは未指定・倍率 1 のため等価変換になる）。<br/>
    /// 3. 自動切り出しが無効な場合はハイライトを消し、矩形の一覧も空にする。
    /// </remarks>
    private void UpdateAutoCropOverlay()
    {
        var hovered = _viewModel.IsAutoCropActive ? _viewModel.HoveredRect : null;

        // 検出矩形の枠線（ホバー中の 1 件を除く。ホバー分は Marquee のハイライトが描く）
        CropView.Rects = _viewModel.IsAutoCropActive
            ? [.. _viewModel.DetectedRects
                .Where(rect => rect != hovered)
                .Select(rect => new OverlayRect(
                    new EsRect(rect.X, rect.Y, rect.Width, rect.Height), OverlayLineStyle.Solid,
                    s_autoCropBorderColor, DrawingColor.Transparent, AutoCropStrokeThickness))]
            : [];

        if (hovered is not { } target)
        {
            // ハイライトなし（外側・内側とも描かない。サイズ・座標ラベルも表示しない）
            AutoCropHighlight.MarqueeAppearance = s_autoCropIdleAppearance;
            AutoCropHighlight.LogicalHighlightRect = null;
            return;
        }

        // ハイライト矩形は DIP 座標へ変換して渡す
        var topLeft = ContentToViewportDomain(new Point(target.X, target.Y));
        var bottomRight = ContentToViewportDomain(new Point(target.Right, target.Bottom));

        AutoCropHighlight.MarqueeAppearance = s_autoCropHighlightAppearance;
        AutoCropHighlight.LogicalHighlightRect = new Rect(
            topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>自動切り出し中に、ポインタ位置（ドメイン座標）の検出矩形をホバーとして設定する。</summary>
    /// <param name="contentPos">ポインタ位置のドメイン座標（画像外の場合は null）。</param>
    /// <remarks>
    /// 矩形の判定（面積が最小のものを選ぶ）は <see cref="MainViewModel.HitDetectedRect"/>（コア）が単一所有する。<br/>
    /// 値が変化した場合のみ設定する（変更通知経由の再描画を最小限にする）。
    /// </remarks>
    private void UpdateAutoCropHover(Point? contentPos)
    {
        if (!_viewModel.IsAutoCropActive || contentPos is not { } target)
        {
            if (_viewModel.HoveredRect is not null)
            {
                _viewModel.HoveredRect = null;
            }

            return;
        }

        var hovered = _viewModel.HitDetectedRect(target.X, target.Y);
        if (_viewModel.HoveredRect != hovered)
        {
            _viewModel.HoveredRect = hovered;
        }
    }

    /// <summary>自動切り出しモード中のダブルクリックで、領域内の最小矩形を確定する（領域外はキャンセル）。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">ポインタイベント。</param>
    /// <remarks>
    /// クリック位置（ドメイン座標）を含む矩形のうち<b>面積が最小</b>のものを確定し、
    /// <see cref="MainViewModel.ConfirmAutoCropCommand"/> で切り出す。<br/>
    /// 矩形を含まない領域のダブルクリックは<b>キャンセル</b>とする<br/>
    /// （切り出しモードの「矩形外クリック: キャンセル」と同じ操作）。<br/>
    /// 本ハンドラは <see cref="CropViewControl"/> の <c>PointerPressed</c> へ購読するため、<br/>
    /// ポインター位置の座標系は CropView のローカル DIP である（旧オーバーレイの Canvas と同一領域）。
    /// </remarks>
    private void OnImageDoubleClicked(object? sender, PointerPressedEventArgs e)
    {
        if (!_viewModel.IsAutoCropActive)
        {
            return;
        }

        // ダブルクリックのみ確定・キャンセル（単一クリックでは何もしない）
        var point = e.GetCurrentPoint(CropView);
        if (!point.Properties.IsLeftButtonPressed || e.ClickCount < 2)
        {
            return;
        }

        var viewportPos = e.GetPosition(CropView);
        var contentPos = ViewportToContentDomain(viewportPos);

        // 矩形の判定はコア（MainViewModel.HitDetectedRect）が単一所有する
        if (_viewModel.HitDetectedRect(contentPos.X, contentPos.Y) is { } hit)
        {
            _viewModel.ConfirmAutoCropCommand.Execute(hit);
            return;
        }

        // 矩形を含まない領域のダブルクリックはキャンセルとする
        _viewModel.CancelAutoCropCommand.Execute(null);
    }

    /// <summary>ビューポート座標をドメイン座標（元画像ピクセル）へ変換する。</summary>
    /// <param name="viewportPos">ビューポート座標。</param>
    /// <returns>ドメイン座標。</returns>
    /// <remarks>
    /// 画像表示矩形の原点を基準に変換する（Fit 中央寄せの原点補正を含む）。<br/>
    /// 変換は <see cref="CropViewControl.ViewportToContent"/> に委譲する（座標変換の単一所有）。
    /// </remarks>
    private Point ViewportToContentDomain(Point viewportPos)
        => CropView.ViewportToContent(viewportPos);

    /// <summary>ドメイン座標（元画像ピクセル）をビューポート座標へ変換する。</summary>
    /// <param name="contentPos">ドメイン座標。</param>
    /// <returns>ビューポート座標。</returns>
    /// <remarks>
    /// 画像表示矩形の原点を基準に変換する（Fit 中央寄せの原点補正を含む）。<br/>
    /// 変換は <see cref="CropViewControl.ContentToViewport"/> に委譲する（座標変換の単一所有）。
    /// </remarks>
    private Point ContentToViewportDomain(Point contentPos)
        => CropView.ContentToViewport(contentPos);

    // ====================================================================
    // コンテキストメニュー
    // ====================================================================

    private ContextMenu? _cropContextMenu;

    private void ShowCropContextMenu()
    {
        _cropContextMenu?.Close();
        _cropContextMenu = null;

        var menu = new ContextMenu();
        // 確定可否の事前判定は行わず、Confirm 側で MinSelectionSize 未満なら無効な選択として無視される（仕様 3.5）
        var confirmItem = new MenuItem { Header = "切出し確定" };
        confirmItem.Click += (_, _) => _captureSessionControl.Confirm();
        var cancelItem = new MenuItem { Header = "キャンセル" };
        cancelItem.Click += (_, _) => CancelCropSession();
        menu.Items.Add(confirmItem);
        menu.Items.Add(cancelItem);
        menu.Closed += (_, _) => _cropContextMenu = null;
        menu.Open(CropView);
        _cropContextMenu = menu;
    }

    // ====================================================================
    // 公開ラッパーメソッド（MainWindow からのアクセス用）
    // ====================================================================

    /// <summary>切出しモードを終了する。</summary>
    public void ExitCropMode() => CancelCropSession();

    /// <summary>キャプチャリソースを解放する（Closing 時に MainWindow から呼ばれる）。</summary>
    public void CleanupCaptureResources()
    {
        CancelCropSession();
        _undoStack.Clear();
        // 元画像 Bitmap は ViewModel（PreviewImage）が管理するため、ここでは破棄しない
    }
}

