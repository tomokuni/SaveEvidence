using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EsUtil.Feature.Annotation;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.AvaloniaUI.ColorSelector;
using EsUtil.Ui.AvaloniaUI.Interop.Imaging;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Sample;

/// <summary>注釈レイヤーのサンプル ウィンドウ（動作と外観を確認する）。</summary>
/// <remarks>
/// <b>確認できること</b>: 枠線の追加・吹き出しの追加・選択（8 方向のハンドル）・移動・サイズ変更・
/// しっぽの先端のドラッグ・本文の編集（ダブルクリックまたは Enter）・**本文の文字単位の書式**（編集中に出る書式バー）。<br/>
/// <b>保存は行わない</b>: 保存（注釈 JSON と合成画像）はホストの仕事であり、本サンプルは表示だけを見る。
/// </remarks>
internal sealed class MainWindow : Window
{
    /// <summary>サンプル画像の幅（ピクセル）。</summary>
    private const int ImageWidth = 960;

    /// <summary>サンプル画像の高さ（ピクセル）。</summary>
    private const int ImageHeight = 640;

    /// <summary>注釈を重ねるレイヤー。</summary>
    private readonly AnnotationLayerControl _layer = new();

    /// <summary>本文の編集に使う入力欄の置き場。</summary>
    private readonly Canvas _overlay = new();

    /// <summary>編集中の入力欄（編集中でなければ null）。</summary>
    private TextBox? _editor;

    /// <summary>編集中に出す書式バー（編集中でなければ null）。</summary>
    private Border? _formatBar;

    /// <summary>本文の編集対象の識別子（編集を始めたときに固定する）。</summary>
    private string? _editingId;

    /// <summary>書式バーの幅（DIP。画像の内側へ収めるために使う）。</summary>
    private const double FormatBarWidth = 700;

    /// <summary>書式の適用中かどうか（適用中はフォーカス喪失で編集を確定しない）。</summary>
    private bool _isApplyingFormat;

    /// <summary>サンプル ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        Title = "Annotation サンプル";
        Width = 1420;
        Height = 900;

        var bitmap = CreateSampleImage();
        _layer.Source = bitmap;
        _layer.SourcePixelWidth = ImageWidth;
        _layer.SourcePixelHeight = ImageHeight;
        _layer.Document = new AnnotationDocument(ImageWidth, ImageHeight, "sample.png")
            .Add(new FrameAnnotation
            {
                Bounds = new EsRect(60, 60, 340, 220),
                Appearance = AnnotationDefaults.FrameAppearance,
            })
            .Add(new BubbleAnnotation
            {
                Bounds = new EsRect(520, 340, 380, 170),
                Appearance = AnnotationDefaults.BubbleAppearance,
                Tail = new BubbleTail(new EsPoint(430, 200), BubbleEdge.Top, 0.3, 26),
                Text = new RichText([
                    new TextRun("ここが重要です。", AnnotationDefaults.TextStyle),
                    new TextRun("文字単位の書式", AnnotationDefaults.TextStyle with { IsBold = true, Color = System.Drawing.Color.FromArgb(255, 200, 0, 0) }),
                    new TextRun("は右クリックで編集できます。", AnnotationDefaults.TextStyle),
                ]),
            });

        _layer.EditTextRequested += (_, _) => BeginEdit();

        var canvas = new Canvas { Background = Brushes.LightGray };
        canvas.Children.Add(_layer);
        canvas.Children.Add(_overlay);
        _layer.Width = ImageWidth;
        _layer.Height = ImageHeight;
        Canvas.SetLeft(_layer, 8);
        Canvas.SetTop(_layer, 8);

        var settings = new ScrollViewer
        {
            Content = new SettingsPane(_layer),
            Width = 344,
            Background = new SolidColorBrush(Color.FromArgb(255, 248, 248, 248)),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        var root = new DockPanel();
        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        DockPanel.SetDock(settings, Dock.Left);
        root.Children.Add(settings);
        root.Children.Add(new ScrollViewer { Content = canvas });
        Content = root;
    }

    /// <summary>サンプル画像（グラデーションと市松模様）を生成する。</summary>
    /// <returns>表示用の画像。</returns>
    /// <remarks>注釈の視認性を確かめるため、明暗がはっきりした背景にする。</remarks>
    private static IImage CreateSampleImage()
    {
        var bitmap = EsUtil.Platform.Core.Imaging.EsBitmap.CreateFilled(ImageWidth, ImageHeight, span =>
        {
            for (var y = 0; y < ImageHeight; y++)
            {
                for (var x = 0; x < ImageWidth; x++)
                {
                    var checker = ((x / 80) + (y / 80)) % 2 == 0;
                    var offset = (y * ImageWidth + x) * 4;
                    span[offset] = (byte)(checker ? 200 : 120);
                    span[offset + 1] = (byte)(checker ? 190 : 140);
                    span[offset + 2] = (byte)(checker ? 170 : 160);
                    span[offset + 3] = 255;
                }
            }
        });

        return EsBitmapAdapter.Adopt(bitmap);
    }

    /// <summary>ツールバーを組み立てる。</summary>
    /// <returns>ツールバー。</returns>
    private Control BuildToolbar()
    {
        var addFrame = new Button { Content = "枠線を追加" };
        addFrame.Click += (_, _) => _layer.AddFrame(new EsRect(120, 320, 300, 200));

        var addBubble = new Button { Content = "吹き出しを追加" };
        addBubble.Click += (_, _) => _layer.AddBubble(new EsRect(300, 80, 320, 140), "新しい吹き出し");

        var edit = new Button { Content = "本文を編集" };
        edit.Click += (_, _) => BeginEdit();

        var remove = new Button { Content = "削除" };
        remove.Click += (_, _) => _layer.DeleteSelected();

        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 6, 8, 6),
        };
        bar.Children.Add(addFrame);
        bar.Children.Add(addBubble);
        bar.Children.Add(edit);
        bar.Children.Add(remove);
        bar.Children.Add(new TextBlock
        {
            Text = "右クリックで外観・書式を編集 / ダブルクリックで本文（編集中は上のバーで文字単位の書式）/ Delete で削除 / 方向キーで移動",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Opacity = 0.7,
        });

        return bar;
    }

    /// <summary>選択中の吹き出しの本文編集を開始する（本文の矩形へ入力欄を重ねる）。</summary>
    private void BeginEdit()
    {
        CommitEdit();

        var rect = _layer.GetSelectedBodyRect();
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        _editingId = _layer.SelectedId;
        var text = _layer.Document.Find(_editingId) is BubbleAnnotation bubble
            ? string.Join("\n", bubble.Text.Runs.Select(run => run.Text))
            : string.Empty;

        var editor = new TextBox
        {
            Text = text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.White,
            BorderBrush = Brushes.DodgerBlue,
            BorderThickness = new Thickness(1),
        };

        Canvas.SetLeft(editor, 8 + rect.X);
        Canvas.SetTop(editor, 8 + rect.Y);
        editor.Width = rect.Width;
        editor.Height = rect.Height;

        editor.LostFocus += (_, _) =>
        {
            // 書式バーへ移っただけなら編集を続ける（バーの操作で本文の編集が閉じないようにする）
            // フォーカスの移動は LostFocus の時点では確定していないため、移動後に判定する
            Dispatcher.UIThread.Post(() =>
            {
                if (_isApplyingFormat || IsInFormatBar())
                {
                    return;
                }

                CommitEdit();
            });
        };
        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
            {
                e.Handled = true;
                CancelEdit();
            }
            else if (e.Key == Avalonia.Input.Key.Enter && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
            {
                e.Handled = true;
                CommitEdit();
            }
            else if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control) && TryHandleShortcut(e.Key))
            {
                e.Handled = true;
            }
        };

        // 選択範囲をコントロールへ写す（右クリックの書式編集がこの範囲へ適用される）。
        // TextBox の選択はプロパティで公開されているため、入力・キー操作のたびに読み直す。
        // 書式バーへフォーカスが移ると選択が 0 文字で通知されるため、その通知は無視する
        // （無視しないと「全体」への適用になり、選択した範囲だけに適用できない。 実測で確認）。
        void SyncSelection()
        {
            var length = editor.SelectionEnd - editor.SelectionStart;
            if (!editor.IsFocused && length == 0)
            {
                return;
            }

            _layer.TextSelectionRange = (editor.SelectionStart, length);
        }

        editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty)
            {
                SyncSelection();
            }
        };
        editor.KeyUp += (_, _) => SyncSelection();
        editor.PointerReleased += (_, _) => SyncSelection();
        SyncSelection();

        _overlay.Children.Add(editor);
        _editor = editor;

        // 書式バーを入力欄の上へ置く（本文の書式は「編集中に選択して適用」であることを画面で示す）
        // 画像の右端ではバーが窓の外へ出るため、画像の幅へ収まる位置まで左へ寄せる（実測: 右端の吹き出しで切れた）
        var bar = BuildFormatBar();
        Canvas.SetLeft(bar, Math.Max(8, Math.Min(8 + rect.X, 8 + ImageWidth - FormatBarWidth)));
        Canvas.SetTop(bar, Math.Max(8, (8 + rect.Y) - 62));
        _overlay.Children.Add(bar);
        _formatBar = bar;

        editor.Focus();
    }

    /// <summary>編集中の文字列を選択中の吹き出しへ反映する。</summary>
    private void CommitEdit()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        var text = (editor.Text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);
        CancelEdit();

        if (_editingId is not null)
        {
            _layer.SelectedId = _editingId;
            _layer.SetSelectedText(text);
            _layer.TextSelectionRange = default;
            _editingId = null;
        }
    }

    /// <summary>編集を取り消す。</summary>
    private void CancelEdit()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        _overlay.Children.Remove(editor);
        _editor = null;

        if (_formatBar is { } bar)
        {
            _overlay.Children.Remove(bar);
            _formatBar = null;
        }

        _layer.TextSelectionRange = default;
    }

    /// <summary>編集中のショートカット（Ctrl+B / I / U / S）を処理する。</summary>
    /// <param name="key">押されたキー。</param>
    /// <returns>処理した場合は <see langword="true"/>。</returns>
    /// <remarks>書式バーと同じ処理へ流す（バーを探さなくても、一般的なショートカットで書式を変えられる）。</remarks>
    private bool TryHandleShortcut(Avalonia.Input.Key key)
    {
        // 切り替えの判断は 1 回だけ行う（TransformTextStyle は run ごとに呼ばれるため）
        var style = _layer.SelectedTextStyle;
        return key switch
        {
            Avalonia.Input.Key.B => ApplyAndReport(s => s with { IsBold = !style.IsBold }),
            Avalonia.Input.Key.I => ApplyAndReport(s => s with { IsItalic = !style.IsItalic }),
            Avalonia.Input.Key.U => ApplyAndReport(s => s with { IsUnderline = !style.IsUnderline }),
            _ => false,
        };
    }

    /// <summary>ショートカットから書式を適用する（常に適用したことを返す）。</summary>
    /// <param name="change">書式の変更。</param>
    /// <returns><see langword="true"/>。</returns>
    private bool ApplyAndReport(Func<AnnotationTextStyle, AnnotationTextStyle> change)
    {
        ApplyFormat(change);
        return true;
    }

    /// <summary>現在のフォーカスが書式バーの内側にあるかどうかを判定する。</summary>
    /// <returns>書式バーの中にフォーカスがある場合は <see langword="true"/>。</returns>
    private bool IsInFormatBar()
    {
        if (_formatBar is null || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return false;
        }

        for (var visual = focused; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, _formatBar))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>書式バーを作る（編集中の選択範囲へ書式を適用する）。</summary>
    /// <returns>書式バー。</returns>
    /// <remarks>
    /// <b>操作方法をバーに書く</b>: 「文字を選んで押す」ことが分からないという指摘があったため、
    /// バーの先頭に操作方法を出し、適用すると編集を終了して結果を見せる。
    /// </remarks>
    private Border BuildFormatBar()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        row.Children.Add(new TextBlock
        {
            Text = "書式（文字を選んで押す／選択なしは全体／Ctrl+B・I・U）",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            Margin = new Thickness(0, 0, 4, 0),
        });

        var style = _layer.SelectedTextStyle;
        var fonts = AnnotationFonts.All;
        var fontBox = new ComboBox
        {
            Width = 120,
            ItemsSource = fonts,
            SelectedIndex = Math.Max(0, fonts.ToList().IndexOf(style.FontFamily)),
        };
        fontBox.SelectionChanged += (_, _) =>
        {
            if (fontBox.SelectedIndex >= 0)
            {
                ApplyFormat(s => s with { FontFamily = fonts[fontBox.SelectedIndex] });
            }
        };
        row.Children.Add(fontBox);

        var sizeBox = new NumericUpDown
        {
            Value = (decimal)style.FontSize,
            Minimum = 6m,
            Maximum = 512m,
            Increment = 1m,
            FormatString = "0.#",
            Width = 96,
        };
        sizeBox.ValueChanged += (_, _) =>
        {
            if (sizeBox.Value is { } value)
            {
                ApplyFormat(s => s with { FontSize = (double)value });
            }
        };
        row.Children.Add(sizeBox);

        var color = new ColorSelectorControl
        {
            Width = 82,
            SelectedColor = AnnotationEditorControls.ToHex(style.Color),
            TextFontSize = 12,
        };
        color.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorSelectorControl.SelectedColorProperty
                && EsUtil.Platform.Core.Imaging.DrawingColorExtensions.TryParseHexText(color.SelectedColor, out var value))
            {
                ApplyFormat(s => s with { Color = value });
            }
        };
        row.Children.Add(color);

        row.Children.Add(AnnotationEditorControls.Toggle("B", style.IsBold, () => ApplyFormat(s => s with { IsBold = !style.IsBold }), bold: true));
        row.Children.Add(AnnotationEditorControls.Toggle("I", style.IsItalic, () => ApplyFormat(s => s with { IsItalic = !style.IsItalic }), italic: true));
        row.Children.Add(AnnotationEditorControls.Toggle("U", style.IsUnderline, () => ApplyFormat(s => s with { IsUnderline = !style.IsUnderline }), underline: true));
        row.Children.Add(AnnotationEditorControls.Toggle("S", style.IsStrike, () => ApplyFormat(s => s with { IsStrike = !style.IsStrike }), strike: true));

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = Brushes.DodgerBlue,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 3),
            Child = row,
        };
    }

    /// <summary>編集中の文字列を確定してから、選択範囲へ書式を適用する。</summary>
    /// <param name="change">書式を新しい値へ変える処理（変えない属性は <c>TransformTextStyle</c> が保つ）。</param>
    /// <remarks>
    /// <b>適用したら編集を終了する</b>: 入力欄（TextBox）は書式を持てないため、入力中は結果が見えない。
    /// 適用と同時に確定・終了して、レイヤーの描画で結果を見せる。
    /// </remarks>
    private void ApplyFormat(Func<AnnotationTextStyle, AnnotationTextStyle> change)
    {
        if (_editor is not { } editor || _editingId is not { } id)
        {
            return;
        }

        _isApplyingFormat = true;
        try
        {
            var text = (editor.Text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);
            // 選択は入力欄から直接読む（入力欄はまだ生きているため、フォーカス移動の有無に左右されない）
            var start = editor.SelectionStart;
            var length = Math.Max(0, editor.SelectionEnd - editor.SelectionStart);
            var current = _layer.SelectedTextStyle;

            _overlay.Children.Remove(editor);
            _editor = null;
            if (_formatBar is { } bar)
            {
                _overlay.Children.Remove(bar);
                _formatBar = null;
            }

            _layer.SelectedId = id;
            _layer.SetSelectedText(text);
            _layer.TransformTextStyle(start, length, change);
            _layer.TextSelectionRange = default;
            _editingId = null;
        }
        finally
        {
            _isApplyingFormat = false;
        }
    }
}
