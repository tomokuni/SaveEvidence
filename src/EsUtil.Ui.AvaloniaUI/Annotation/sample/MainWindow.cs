using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using EsUtil.Feature.Annotation;
using EsUtil.Platform.Core.Geometry;
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
    private FormatBar? _formatBar;

    /// <summary>本文の編集対象の識別子（編集を始めたときに固定する）。</summary>
    private string? _editingId;

    /// <summary>書式バーの幅（DIP。画像の内側へ収めるために使う）。</summary>
    private const double FormatBarWidth = FormatBar.BarWidth;

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

        // 入力欄と書式バーの外側を押したら編集を確定する（フォーカスの喪失ではポップアップで閉じてしまう）
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        var canvas = new Canvas { Background = Brushes.LightGray };
        canvas.Children.Add(_layer);
        canvas.Children.Add(_overlay);
        _layer.Width = ImageWidth;
        _layer.Height = ImageHeight;
        Canvas.SetLeft(_layer, 8);
        Canvas.SetTop(_layer, 8);

        // 入力欄の置き場は画像と同じ大きさにする（大きさが 0 だと子要素がマウスを受け取れない）
        _overlay.Width = ImageWidth;
        _overlay.Height = ImageHeight;
        Canvas.SetLeft(_overlay, 8);
        Canvas.SetTop(_overlay, 8);

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

        var undo = new Button { Content = "元に戻す" };
        undo.Click += (_, _) => _layer.Undo();

        var redo = new Button { Content = "やり直す" };
        redo.Click += (_, _) => _layer.Redo();

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
        bar.Children.Add(undo);
        bar.Children.Add(redo);
        bar.Children.Add(new TextBlock
        {
            Text = "右クリックで外観・書式を編集 / ダブルクリックで本文（編集中は上のバーで文字単位の書式）/ Delete で削除 / 方向キーで移動 / Ctrl+Z で元に戻す",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Opacity = 0.7,
        });

        return bar;
    }

    /// <summary>選択中の吹き出しの本文編集を開始する（本文の矩形へ入力欄と書式バーを重ねる）。</summary>
    /// <remarks>
    /// <b>本文は <see cref="RichText.PlainText"/> から作る</b>: run を改行で連結すると、run の境界へ
    /// 改行が入り、編集のたびに本文が崩れる（実測で指摘）。
    /// </remarks>
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
            ? bubble.Text.PlainText
            : string.Empty;

        var editor = new TextBox
        {
            Text = text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            // 内側の余白を 0 にして本文の矩形へぴったり重ねる（余白があると下の本文が左端に覗く。 実測で確認）
            Padding = new Thickness(0),
            Background = Brushes.White,
            BorderBrush = Brushes.DodgerBlue,
            BorderThickness = new Thickness(1),
        };

        // 入力欄は本文の矩形より少し左へ広げる（TextBox の内側の余白の分だけ下の本文が左端に覗くため。 実測で確認）
        const double EditorInsetLeft = 10;
        Canvas.SetLeft(editor, 8 + rect.X - EditorInsetLeft);
        Canvas.SetTop(editor, 8 + rect.Y);
        editor.Width = rect.Width + EditorInsetLeft;
        editor.Height = rect.Height;

        // 先に受け取る（TextBox が Enter を改行として扱うより前に、Ctrl+Enter を確定として扱う）
        editor.AddHandler(
            KeyDownEvent,
            (_, e) => OnEditorKeyDown(e),
            Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // 選択範囲をコントロールへ写す（右クリックのフライアウトも同じ範囲へ適用する）
        void SyncSelection()
        {
            _layer.TextSelectionRange = (editor.SelectionStart, Math.Max(0, editor.SelectionEnd - editor.SelectionStart));
            _formatBar?.Refresh();
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

        _overlay.Children.Add(editor);
        _editor = editor;

        // 書式バーを入力欄の上へ置く（右端・上端では画像の内側へ寄せる。 実測: はみ出して切れた）
        var bar = new FormatBar(_layer, ApplyFormat, CommitEdit);
        Canvas.SetLeft(bar, Math.Max(8, Math.Min(8 + rect.X, 8 + ImageWidth - FormatBarWidth)));
        Canvas.SetTop(bar, Math.Max(8, (8 + rect.Y) - FormatBar.EstimatedHeight - 8));
        _overlay.Children.Add(bar);
        _formatBar = bar;

        _layer.TextSelectionRange = default;
        editor.CaretIndex = text.Length;
        editor.Focus();
        bar.Refresh();
    }

    /// <summary>本文の入力欄でキーが押されたときの処理。</summary>
    /// <param name="e">キーの引数。</param>
    private void OnEditorKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelEdit();
            return;
        }

        if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            e.Handled = true;
            CommitEdit();
            return;
        }

        if ((e.KeyModifiers & KeyModifiers.Control) == 0)
        {
            return;
        }

        // 切り替えの判断は 1 回だけ行う（TransformTextStyle は run ごとに呼ばれるため）
        var style = _layer.SelectedTextStyle;
        switch (e.Key)
        {
            case Key.B:
                ApplyFormat(s => s with { IsBold = !style.IsBold });
                e.Handled = true;
                break;
            case Key.I:
                ApplyFormat(s => s with { IsItalic = !style.IsItalic });
                e.Handled = true;
                break;
            case Key.U:
                ApplyFormat(s => s with { IsUnderline = !style.IsUnderline });
                e.Handled = true;
                break;
            default:
                break;
        }
    }

    /// <summary>編集を確定して入力欄と書式バーを閉じる。</summary>
    private void CommitEdit()
    {
        if (_editor is not { } editor)
        {
            return;
        }

        var text = (editor.Text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);
        var id = _editingId;
        CancelEdit();

        if (id is not null)
        {
            _layer.SelectedId = id;

            // 文字が変わっていなければ本文を置き換えない（履歴に同じ内容の手が増えないようにする）
            if (_layer.Document.Find(id) is BubbleAnnotation bubble && !string.Equals(bubble.Text.PlainText, text, StringComparison.Ordinal))
            {
                _layer.SetSelectedText(text);
            }

            _editingId = null;
        }
    }

    /// <summary>編集を取り消す（本文へは反映しない）。</summary>
    private void CancelEdit()
    {
        if (_editor is { } editor)
        {
            _overlay.Children.Remove(editor);
            _editor = null;
        }

        if (_formatBar is { } bar)
        {
            _overlay.Children.Remove(bar);
            _formatBar = null;
        }

        _editingId = null;
        _layer.TextSelectionRange = default;
    }

    /// <summary>編集中の選択範囲へ書式を適用する（編集は続ける）。</summary>
    /// <param name="change">書式を新しい値へ変える処理（変えない属性は <c>TransformTextStyle</c> が保つ）。</param>
    /// <remarks>
    /// <b>編集を閉じない</b>: 書式は入力欄の選択範囲に対して続けて適用できる（太字にしてから色を変える等）。
    /// 入力欄は書式を持てないため、結果は確定後（完了・外側のクリック・<b>Ctrl+Enter</b>）に本文へ出る。
    /// </remarks>
    private void ApplyFormat(Func<AnnotationTextStyle, AnnotationTextStyle> change)
    {
        if (_editor is not { } editor || _editingId is not { } id)
        {
            return;
        }

        var text = (editor.Text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = editor.SelectionStart;
        var length = Math.Max(0, editor.SelectionEnd - editor.SelectionStart);

        _layer.SelectedId = id;

        _layer.SetSelectedText(text);

        _layer.TextSelectionRange = (start, length);
        _layer.TransformTextStyle(start, length, change);
        _formatBar?.Refresh();

        editor.Focus();
    }

    /// <summary>書式バーと入力欄の外側が押されたときに編集を確定する。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">ポインターの引数。</param>
    /// <remarks>
    /// <b>フォーカスの喪失では確定しない</b>: 書式バーの選択肢（ポップアップ）へフォーカスが移ると
    /// 入力欄が閉じ、選んだ書式が適用されない（実測で確認）。 押した位置で判定する。
    /// </remarks>
    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_editor is not { } editor)
        {
            return;
        }

        if (IsInside(e.Source as Visual, editor) || IsInside(e.Source as Visual, _formatBar))
        {
            return;
        }

        CommitEdit();
    }

    /// <summary>指定された要素が指定された祖先の内側にあるかどうかを判定する。</summary>
    /// <param name="visual">判定する要素。</param>
    /// <param name="ancestor">祖先（<see langword="null"/> なら常に <see langword="false"/>）。</param>
    /// <returns>内側にある場合は <see langword="true"/>。</returns>
    private static bool IsInside(Visual? visual, Visual? ancestor)
    {
        if (ancestor is null)
        {
            return false;
        }

        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
