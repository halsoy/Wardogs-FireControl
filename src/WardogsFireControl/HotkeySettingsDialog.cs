using System.Runtime.InteropServices;

namespace WardogsFireControl;

public sealed class HotkeySettingsDialog : Form
{
    private readonly HotkeyCaptureBox _target;
    private readonly HotkeyCaptureBox _gun;
    private readonly Label _validation = new()
    {
        AutoSize = true,
        ForeColor = Color.FromArgb(242, 120, 110),
        Margin = new Padding(3, 5, 3, 0)
    };

    public HotkeySettings Result { get; private set; }

    public HotkeySettingsDialog(HotkeySettings current)
    {
        Result = Copy(current);
        _target = new HotkeyCaptureBox(new HotkeyBinding(current.TargetKey, current.TargetModifiers));
        _gun = new HotkeyCaptureBox(new HotkeyBinding(current.GunKey, current.GunModifiers));

        Text = "Capture hotkeys";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(430, 235);
        BackColor = Color.FromArgb(19, 24, 29);
        ForeColor = Color.FromArgb(224, 230, 235);
        Font = new Font("Segoe UI", 9F);

        var help = new Label
        {
            Text = "Click a field, then press any keyboard key or combination.\nSingle keys work; combinations are recommended to avoid conflicts.\nModifier-only bindings are not supported.",
            AutoSize = true,
            ForeColor = Color.FromArgb(155, 170, 180),
            Margin = new Padding(3, 0, 3, 10)
        };

        var save = CreateButton("Save");
        var cancel = CreateButton("Cancel");
        save.Click += (_, _) => Save();
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0)
        };
        actions.Controls.AddRange([save, cancel]);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(help, 0, 0);
        layout.SetColumnSpan(help, 2);
        layout.Controls.Add(CreateLabel("Capture target"), 0, 1);
        layout.Controls.Add(_target, 1, 1);
        layout.Controls.Add(CreateLabel("Capture gun"), 0, 2);
        layout.Controls.Add(_gun, 1, 2);
        layout.Controls.Add(_validation, 0, 3);
        layout.SetColumnSpan(_validation, 2);
        layout.Controls.Add(actions, 0, 4);
        layout.SetColumnSpan(actions, 2);
        Controls.Add(layout);

        Shown += (_, _) =>
        {
            TopMost = Owner?.TopMost == true;
            BringToFront();
            Activate();
            _target.Focus();
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkWindowChrome.Apply(this);
    }

    private void Save()
    {
        if (_target.Binding == _gun.Binding)
        {
            _validation.Text = "Target and gun must use different key combinations.";
            System.Media.SystemSounds.Hand.Play();
            return;
        }

        Result = new HotkeySettings
        {
            TargetKey = _target.Binding.Key,
            TargetModifiers = _target.Binding.Modifiers,
            GunKey = _gun.Binding.Key,
            GunModifiers = _gun.Binding.Modifiers
        };
        DialogResult = DialogResult.OK;
    }

    private static HotkeySettings Copy(HotkeySettings settings) => new()
    {
        TargetKey = settings.TargetKey,
        TargetModifiers = settings.TargetModifiers,
        GunKey = settings.GunKey,
        GunModifiers = settings.GunModifiers
    };

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        ForeColor = Color.FromArgb(200, 210, 215)
    };

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MinimumSize = new Size(88, 30),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(38, 47, 55),
        ForeColor = Color.FromArgb(230, 235, 238),
        FlatAppearance = { BorderColor = Color.FromArgb(75, 88, 98) }
    };
}

public sealed class HotkeyCaptureBox : TextBox
{
    private static readonly HashSet<Keys> PureModifierKeys =
    [
        Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
        Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
        Keys.Menu, Keys.LMenu, Keys.RMenu,
        Keys.LWin, Keys.RWin
    ];

    public HotkeyBinding Binding { get; private set; }

    public HotkeyCaptureBox(HotkeyBinding binding)
    {
        Binding = binding;
        ReadOnly = true;
        ShortcutsEnabled = false;
        TabStop = true;
        TextAlign = HorizontalAlignment.Center;
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = Color.FromArgb(55, 63, 70);
        ForeColor = Color.FromArgb(245, 185, 75);
        Font = new Font("Segoe UI Semibold", 10F);
        Dock = DockStyle.Fill;
        Margin = new Padding(3, 7, 3, 7);
        UpdateText();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
        var key = e.KeyCode & Keys.KeyCode;
        if (key == Keys.None || PureModifierKeys.Contains(key)) return;

        var modifiers = HotkeyModifiers.None;
        if (e.Control) modifiers |= HotkeyModifiers.Control;
        if (e.Alt) modifiers |= HotkeyModifiers.Alt;
        if (e.Shift) modifiers |= HotkeyModifiers.Shift;
        if (IsPressed(Keys.LWin) || IsPressed(Keys.RWin)) modifiers |= HotkeyModifiers.Win;
        Binding = new HotkeyBinding(key, modifiers);
        UpdateText();
        SelectAll();
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) == Keys.Tab || base.IsInputKey(keyData);

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        BackColor = Color.FromArgb(67, 76, 84);
        SelectAll();
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        BackColor = Color.FromArgb(55, 63, 70);
        SelectionLength = 0;
    }

    private void UpdateText() => Text = Binding.ToString();

    private static bool IsPressed(Keys key) => (GetKeyState((int)key) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
}
