using System.Runtime.InteropServices;

namespace WardogsFireControl;

public sealed class DarkComboBox : ComboBox
{
    private static readonly Color Surface = Color.FromArgb(55, 63, 70);
    private static readonly Color SurfaceHover = Color.FromArgb(67, 76, 84);
    private static readonly Color TextColor = Color.FromArgb(235, 239, 242);

    public DarkComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        BackColor = Surface;
        ForeColor = TextColor;
        ItemHeight = 20;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? SurfaceHover : Surface);
        using var foreground = new SolidBrush(TextColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        var text = GetItemText(Items[e.Index]);
        e.Graphics.DrawString(text, Font, foreground, e.Bounds.Left + 5, e.Bounds.Top + 2);
        e.DrawFocusRectangle();
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg != 0x000F || DropDownStyle != ComboBoxStyle.DropDownList) return; // WM_PAINT

        using var graphics = Graphics.FromHwnd(Handle);
        var buttonWidth = SystemInformation.VerticalScrollBarWidth + 2;
        var button = new Rectangle(ClientRectangle.Right - buttonWidth, 1, buttonWidth - 1, ClientRectangle.Height - 2);
        using var brush = new SolidBrush(Enabled ? SurfaceHover : Color.FromArgb(47, 53, 58));
        graphics.FillRectangle(brush, button);
        using var pen = new Pen(Color.FromArgb(210, 216, 220), 1.5f);
        var centerX = button.Left + button.Width / 2;
        var centerY = button.Top + button.Height / 2;
        graphics.DrawLines(pen,
        [
            new Point(centerX - 4, centerY - 2),
            new Point(centerX, centerY + 2),
            new Point(centerX + 4, centerY - 2)
        ]);
    }
}

public static class DarkWindowChrome
{
    public static void Apply(Form form)
    {
        if (!OperatingSystem.IsWindows()) return;
        var enabled = 1;
        DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)); // immersive dark mode

        // COLORREF uses BGR byte order. These attributes are supported on Windows 11.
        var caption = ToColorRef(Color.FromArgb(55, 63, 70));
        var text = ToColorRef(Color.FromArgb(235, 239, 242));
        DwmSetWindowAttribute(form.Handle, 35, ref caption, sizeof(int));
        DwmSetWindowAttribute(form.Handle, 36, ref text, sizeof(int));
    }

    private static int ToColorRef(Color color) => color.R | color.G << 8 | color.B << 16;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
