using System.ComponentModel;
using System.Drawing.Drawing2D;
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
    private const int BaseIconSize = (int)ToolbarMetrics.IconSize;

    /// <summary>このボタンが表示するアイコン（null の場合は文字だけを表示する）。</summary>
    private readonly AppIcon? _icon;

    /// <summary>ツールバーのボタンとして塗る（面・枠線・選択色）かどうか。</summary>
    private bool _isToolbarButton;

    /// <summary>ツールバーの枠線を表示しないかどうか。</summary>
    private bool _isBorderless;

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
            ? (caption.Length == 0 ? (int)ToolbarMetrics.ButtonHeight : (caption.Length * 16) + 14)
            : caption.Length == 0 ? (int)ToolbarMetrics.ButtonHeight : (int)ToolbarMetrics.ButtonHeight + (caption.Length * 16);
        Size = new Size(width, (int)ToolbarMetrics.ButtonHeight);
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

    /// <summary>ツールバーのボタンとして塗る（面・枠線・選択色）かどうかを取得または設定する。</summary>
    /// <value>true の場合は WinUI 3 の既定のボタンと同じ外観（面・枠線・選択時はアクセント色）で描く。</value>
    /// <remarks>
    /// ナビゲーションの項目（<c>false</c>）は下地を持たない（選択の帯だけで示す）。<br/>
    /// ツールバーのボタンは面を持つため、同じ <see cref="ToolButton"/> でも描き分ける。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsToolbarButton
    {
        get => _isToolbarButton;
        set
        {
            if (_isToolbarButton == value)
            {
                return;
            }

            _isToolbarButton = value;
            ApplyTheme();
        }
    }

    /// <summary>ツールバーの枠線を表示しないかどうかを取得または設定する。</summary>
    /// <value>true の場合は枠線を表示しない。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsBorderless
    {
        get => _isBorderless;
        set
        {
            if (_isBorderless == value)
            {
                return;
            }

            _isBorderless = value;
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
        if (_isToolbarButton)
        {
            ApplyToolbarRegion();
            if (_isBorderless)
            {
                BackColor = AppStyles.BarBackground;
                ForeColor = AppStyles.Foreground;
                FlatAppearance.BorderSize = 0;
                FlatAppearance.BorderColor = AppStyles.BarBackground;
                FlatAppearance.MouseOverBackColor = AppStyles.ButtonHover;
                FlatAppearance.MouseDownBackColor = AppStyles.ButtonPressed;
                RefreshIcon();
                return;
            }

            // ツールバーのボタンは WinUI 3 の既定のボタンと同じ外観にする（面 + 枠線 + 選択時はアクセント色）
            BackColor = _isSelected ? AppStyles.SelectedButtonFace : AppStyles.ButtonFace;
            ForeColor = _isSelected ? AppStyles.SelectedButtonForeground : AppStyles.Foreground;
            FlatAppearance.BorderSize = 1;
            FlatAppearance.BorderColor = _isSelected ? AppStyles.SelectedButtonBorder : AppStyles.ButtonBorder;
            FlatAppearance.MouseOverBackColor = _isSelected ? AppStyles.SelectedButtonFace : AppStyles.ButtonHover;
            FlatAppearance.MouseDownBackColor = _isSelected ? AppStyles.SelectedButtonFace : AppStyles.ButtonPressed;
            RefreshIcon();
            return;
        }

        ApplyToolbarRegion();
        // ナビゲーションの項目はナビゲーション パネルの下地をそのまま見せる（選択の帯だけで示す）
        BackColor = _isSelected ? AppStyles.NavItemSelected : AppStyles.NavBackground;
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
        ApplyToolbarRegion();
        RefreshIcon();
    }

    /// <summary>WinUI 3 と同じ角丸をツールバー ボタンへ適用する。</summary>
    private void ApplyToolbarRegion()
    {
        var previous = Region;
        if (!_isToolbarButton || Width <= 0 || Height <= 0)
        {
            Region = null;
            previous?.Dispose();
            return;
        }

        var radius = Math.Max(1, (int)Math.Round(ToolbarMetrics.CornerRadius * DeviceDpi / 96.0));
        var diameter = Math.Min(radius * 2, Math.Min(Width, Height));
        using var path = new GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(Width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(Width - diameter, Height - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, Height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        Region = new Region(path);
        previous?.Dispose();
    }

    /// <summary>大きさの変更時に角丸を作り直す。</summary>
    /// <param name="e">イベント引数。</param>
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyToolbarRegion();
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
            Region?.Dispose();
            Region = null;
        }

        base.Dispose(disposing);
    }
}
