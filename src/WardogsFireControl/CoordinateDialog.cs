namespace WardogsFireControl;

public sealed class CoordinateDialog : Form
{
    private readonly NumericUpDown _x = CreateCoordinateInput();
    private readonly NumericUpDown _y = CreateCoordinateInput();

    public MapCoordinate Coordinate => new((double)_x.Value, (double)_y.Value);

    public CoordinateDialog(string title)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(340, 205);
        BackColor = Color.FromArgb(19, 24, 29);
        ForeColor = Color.FromArgb(224, 230, 235);
        Font = new Font("Segoe UI", 9F);

        var help = new Label
        {
            Text = "Enter the coordinate shown on the tactical map.",
            AutoSize = true,
            ForeColor = Color.FromArgb(155, 170, 180),
            Margin = new Padding(3, 0, 3, 10)
        };
        var add = CreateButton("Add");
        var cancel = CreateButton("Cancel");
        add.Click += (_, _) => DialogResult = DialogResult.OK;
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };
        actions.Controls.AddRange([add, cancel]);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(help, 0, 0);
        layout.SetColumnSpan(help, 2);
        layout.Controls.Add(CreateLabel("X"), 0, 1);
        layout.Controls.Add(_x, 1, 1);
        layout.Controls.Add(CreateLabel("Y"), 0, 2);
        layout.Controls.Add(_y, 1, 2);
        layout.Controls.Add(actions, 0, 3);
        layout.SetColumnSpan(actions, 2);
        Controls.Add(layout);

        Shown += (_, _) =>
        {
            TopMost = Owner?.TopMost == true;
            BringToFront();
            Activate();
            _x.Focus();
            _x.Select(0, _x.Text.Length);
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkWindowChrome.Apply(this);
    }

    private static NumericUpDown CreateCoordinateInput() => new()
    {
        DecimalPlaces = 2,
        Increment = 0.01m,
        Maximum = 163.84m,
        Width = 150,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        BackColor = Color.FromArgb(55, 63, 70),
        ForeColor = Color.FromArgb(245, 185, 75),
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI Semibold", 10F),
        TextAlign = HorizontalAlignment.Center
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
