using System.ComponentModel;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.WinForms.Icons;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>アイコンを持つツールバー用のボタン。</summary>
/// <remarks>
/// <b>DPI とテーマへの追従</b>: アイコンはビットマップであり、コントロールの拡大（DPI）と<br/>
/// テーマの切り替え（色）に自動で追従しない。 ハンドルの生成時・DPI の変更時・テーマの変更時に<br/>
/// 描き直す（<see cref="RefreshIcon"/>）。<br/>
/// <b>選択状態</b>: 一覧の表示モードのように「いずれか 1 つが選ばれている」ボタンは<br/>
/// <see cref="IsSelected"/> で背景色を変える。
/// </remarks>
internal sealed class ToolButton : Button
{
    /// <summary>アイコンの一辺の基準サイズ（DIP）。</summary>
    private const int BaseIconSize = (int)NavPanelMetrics.IconSize;

    /// <summary>このボタンが表示するアイコン（null の場合は文字だけを表示する）。</summary>
    private readonly AppIcon? _icon;

    /// <summary>選択状態を示す背景色で描くかどうか。</summary>
    private bool _isSelected;

    /// <summary>選択中に左端のマークを描くかどうか。</summary>
    private bool _showSelectionMark;

    /// <summary>選択中のマークの幅（DIP）。 他の UI 版と同じ 3 DIP。</summary>
    private const int MarkWidth = 3;

    /// <summary>選択中のマークの高さ（DIP）。 他の UI 版と同じ 16 DIP。</summary>
    private const int MarkHeight = 16;

    /// <summary>選択中のマークの左端（DIP）。 他の UI 版と同じ 4 DIP。</summary>
    private const int MarkLeft = 4;

    /// <summary>ツールバー用のボタンを生成する。</summary>
    /// <param name="icon">表示するアイコン（null の場合は文字だけを表示する）。</param>
    /// <param name="toolTip">ツールヒントの文言。</param>
    /// <param name="caption">アイコンの右へ表示する文字（省略時はアイコンのみ）。</param>
    /// <remarks>配色は <see cref="AppStyles"/> に追従する（テーマの変更で描き直す）。</remarks>
    public ToolButton(AppIcon? icon, string toolTip, string caption = "")
    {
        _icon = icon;
        Text = caption;
        AutoSize = false;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        ImageAlign = ContentAlignment.MiddleCenter;
        TextImageRelation = TextImageRelation.ImageBeforeText;
        TabStop = false;
        Margin = new Padding(1, 1, 1, 1);

        // サイズはアイコンと文字の実寸から決める（DPI へは AutoScaleMode が追従する）
        var width = icon is null
            ? (caption.Length == 0 ? 30 : (caption.Length * 16) + 14)
            : caption.Length == 0 ? 30 : 30 + (caption.Length * 16);
        Size = new Size(width, 30);
        UiFactory.AttachToolTip(this, toolTip);
        ApplyTheme();
    }

    /// <summary>選択状態かどうかを取得または設定する。</summary>
    /// <value>true の場合は選択中の背景色で描く。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            ApplyTheme();
        }
    }

    /// <summary>選択中に左端のマーク（ナビゲーションの選択表示）を描くかどうかを取得または設定する。</summary>
    /// <value>true の場合は選択中に左端へアクセント色の縦線を描く。</value>
    /// <remarks>ナビゲーションの項目だけが true にする（ツールバーのボタンは背景色だけで示す）。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowSelectionMark
    {
        get => _showSelectionMark;
        set
        {
            if (_showSelectionMark == value)
            {
                return;
            }

            _showSelectionMark = value;
            Invalidate();
        }
    }

    /// <summary>選択中のマークを描く。</summary>
    /// <param name="e">描画の情報。</param>
    /// <remarks>
    /// マークはボタンの内側（文字とアイコンの左）に描く。 位置と大きさは DIP で決め、<br/>
    /// 画面の DPI で物理ピクセルへ換算する（<c>AutoScaleMode.Dpi</c> は描画コードを拡大しない）。
    /// </remarks>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);

        if (!_isSelected || !_showSelectionMark)
        {
            return;
        }

        var scaling = DeviceDpi / 96.0;
        var width = Math.Max(1, (int)Math.Round(MarkWidth * scaling));
        var height = (int)Math.Round(MarkHeight * scaling);
        var left = (int)Math.Round(MarkLeft * scaling);
        var top = (Height - height) / 2;

        // 上下を丸めた縦長の帯（上は 180° から 180°、下は 0° から 180° の弧で閉じる）
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(left, top, width, width, 180, 180);
        path.AddArc(left, top + height - width, width, width, 0, 180);
        path.CloseFigure();
        using var brush = new SolidBrush(AppStyles.Accent);
        var smoothing = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        try
        {
            e.Graphics.FillPath(brush, path);
        }
        finally
        {
            e.Graphics.SmoothingMode = smoothing;
        }
    }

    /// <summary>アイコンを現在の DPI とテーマの色で描き直す。</summary>
    internal void RefreshIcon()
    {
        var previous = Image;
        if (_icon is not { } icon)
        {
            // 文字だけのボタンは画像を持たない
            Image = null;
            previous?.Dispose();
            return;
        }

        var size = Math.Max(12, (int)Math.Round(BaseIconSize * DeviceDpi / 96.0));
        var color = Enabled ? AppStyles.Foreground : AppStyles.Border;
        Image = IconLoader.Create(icon, size, color);
        previous?.Dispose();
    }

    /// <summary>配色を現在のテーマへ合わせる。</summary>
    private void ApplyTheme()
    {
        BackColor = _isSelected ? AppStyles.NavItemSelected : AppStyles.BarBackground;
        ForeColor = AppStyles.Foreground;
        FlatAppearance.MouseOverBackColor = AppStyles.NavItemHover;
        FlatAppearance.MouseDownBackColor = AppStyles.NavItemSelected;
        RefreshIcon();
    }

    /// <summary>ハンドルが生成されたときにアイコンを描く。</summary>
    /// <param name="e">イベント引数。</param>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTheme();
    }

    /// <summary>親の DPI が変わったときにアイコンを描き直す。</summary>
    /// <param name="e">イベント引数。</param>
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        RefreshIcon();
    }

    /// <summary>有効・無効が変わったときにアイコンの色を合わせる。</summary>
    /// <param name="e">イベント引数。</param>
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        RefreshIcon();
    }

    /// <summary>保持している資源を解放する。</summary>
    /// <param name="disposing">マネージドの資源も解放する場合は true。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Image?.Dispose();
            Image = null;
        }

        base.Dispose(disposing);
    }
}
