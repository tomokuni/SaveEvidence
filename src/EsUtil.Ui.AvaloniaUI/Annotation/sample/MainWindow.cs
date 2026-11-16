using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Feature.Annotation;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.AvaloniaUI.Interop.Imaging;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Sample;

/// <summary>注釈レイヤーのサンプル ウィンドウ（動作と外観を確認する）。</summary>
/// <remarks>
/// <b>確認できること</b>: 枠線の追加・吹き出しの追加・選択（8 方向のハンドル）・移動・サイズ変更・
/// しっぽの先端のドラッグ・**本文のその場編集**（ダブルクリックまたは Enter）・文字単位の書式・元に戻す。<br/>
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

    /// <summary>編集中に出す書式バー（編集中でなければ null）。</summary>
    private FormatBar? _formatBar;

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

        // 本文の編集の開始・終了に合わせて書式バーを出す・消す
        _layer.EditingChanged += (_, _) => SyncFormatBar();
        _layer.DocumentChanged += (_, _) => _formatBar?.Refresh();

        var canvas = new Canvas { Background = Brushes.LightGray };
        canvas.Children.Add(_layer);
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

    /// <summary>サンプル画像（市松模様）を生成する。</summary>
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
        edit.Click += (_, _) => _layer.BeginTextEditing();

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
            Text = "右クリックで外観・書式を編集 / ダブルクリックで本文をその場編集（選択した文字は書式バーで変更）/ Delete で削除 / 方向キーで移動 / Ctrl+Z で元に戻す",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Opacity = 0.7,
        });

        return bar;
    }

    /// <summary>本文の編集状態に合わせて書式バーを出す・消す。</summary>
    private void SyncFormatBar()
    {
        if (!_layer.IsEditingText)
        {
            if (_formatBar is { } closing)
            {
                (_formatBar.Parent as Panel)?.Children.Remove(closing);
                _formatBar = null;
            }

            return;
        }

        var rect = _layer.GetSelectedBodyRect();
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        if (_formatBar is null)
        {
            var bar = new FormatBar(_layer, _layer.EndTextEditing);
            Canvas.SetLeft(bar, 8 + Math.Max(0, Math.Min(rect.X, ImageWidth - FormatBar.BarWidth)));
            Canvas.SetTop(bar, 8 + Math.Max(0, rect.Y - FormatBar.EstimatedHeight - 6));
            ((Canvas)_layer.Parent!).Children.Add(bar);
            _formatBar = bar;
        }

        _formatBar.Refresh();
    }
}
