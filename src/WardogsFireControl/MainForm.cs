using System.Media;

namespace WardogsFireControl;

public sealed class MainForm : Form
{
    private const int WmHotkey = 0x0312;
    private const int MaxGunsPerMap = 6;
    private readonly StateStore _store = new();
    private readonly AppState _state;
    private readonly BallisticsRepository _ballistics;
    private readonly KnownTargetRepository _knownTargets;
    private CoordinateOcrService? _ocr;
    private HotkeyService? _hotkeys;
    private readonly SemaphoreSlim _captureLock = new(1, 1);

    private readonly ComboBox _map = new DarkComboBox { Width = 125 };
    private readonly ComboBox _weapon = new DarkComboBox { Width = 140 };
    private readonly ComboBox _guns = new DarkComboBox { Width = 270 };
    private readonly DataGridView _knownGrid = CreateDataGrid(150);
    private readonly DataGridView _otherGunsGrid = CreateDataGrid(90);
    private readonly DataGridView _targets = CreateDataGrid(135);
    private readonly TextBox _gunName = new() { Width = 150 };
    private readonly TextBox _targetName = new() { Width = 150 };
    private readonly Label _gunCoordinate = ValueLabel("No gun captured");
    private readonly Label _targetCoordinate = ValueLabel("No target selected");
    private readonly Label _distance = SolutionLabel("— m");
    private readonly Label _azimuth = SolutionLabel("—°");
    private readonly Label _mils = SolutionLabel("— mil");
    private readonly Label _rangeStatus = ValueLabel("Awaiting gun and target");
    private readonly Label _status = ValueLabel("Ready");
    private readonly Button _captureTarget = new() { AutoSize = true };
    private readonly Button _captureGun = new() { AutoSize = true };
    private readonly CheckBox _topmost = new() { Text = "Always on top", AutoSize = true };
    private MapCoordinate? _activeTarget;
    private bool _refreshingKnownTargets;

    private string CurrentMapId => (_map.SelectedItem as MapChoice)?.Id ?? "bakurani";
    private string CurrentWeaponId => (_weapon.SelectedItem as WeaponSummary)?.Id ?? "mortar";
    private SavedPosition? SelectedGun => _guns.SelectedItem as SavedPosition;
    private SavedPosition? SelectedSavedTarget => _targets.SelectedRows.Count == 0
        ? null
        : _targets.SelectedRows[0].Tag as SavedPosition;

    public MainForm()
    {
        Text = "WARDOGS Fire Control";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 940);
        MaximizeBox = false;
        MinimizeBox = true;
        ShowIcon = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        BackColor = Color.FromArgb(19, 24, 29);
        ForeColor = Color.FromArgb(224, 230, 235);
        Font = new Font("Segoe UI", 9F);

        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        _ballistics = new BallisticsRepository(Path.Combine(dataDirectory, "weapons.json"));
        _knownTargets = new KnownTargetRepository(Path.Combine(dataDirectory, "maps"));
        _state = _store.Load();

        BuildInterface();
        LoadStateIntoControls();
        ApplyTheme(this);
        Shown += OnShown;
        FormClosed += OnFormClosed;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkWindowChrome.Apply(this);
    }

    private void BuildInterface()
    {
        _targets.Columns.Add(new DataGridViewTextBoxColumn { Name = "Target", HeaderText = "TARGET", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 160 });
        _targets.Columns.Add(new DataGridViewTextBoxColumn { Name = "X", HeaderText = "X", Width = 85 });
        _targets.Columns.Add(new DataGridViewTextBoxColumn { Name = "Y", HeaderText = "Y", Width = 85 });
        _targets.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "RemoveRow",
            HeaderText = string.Empty,
            Text = "Remove",
            UseColumnTextForButtonValue = true,
            Width = 78,
            FlatStyle = FlatStyle.Flat,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(72, 55, 55),
                ForeColor = Color.FromArgb(245, 225, 225),
                SelectionBackColor = Color.FromArgb(105, 62, 62),
                SelectionForeColor = Color.White,
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(3)
            }
        });

        _knownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tower", HeaderText = "TOWER", Width = 100 });
        _knownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Distance", HeaderText = "RANGE", Width = 85 });
        _knownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Azimuth", HeaderText = "AZIMUTH", Width = 85 });
        _knownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Elevation", HeaderText = "ELEVATION", Width = 145 });
        _knownGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 180 });
        _knownGrid.ScrollBars = ScrollBars.None;

        _otherGunsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Gun", HeaderText = "GUN", Width = 120 });
        _otherGunsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Distance", HeaderText = "RANGE", Width = 85 });
        _otherGunsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Azimuth", HeaderText = "AZIMUTH", Width = 85 });
        _otherGunsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Elevation", HeaderText = "ELEVATION", Width = 145 });
        _otherGunsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 180 });

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 8
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var selectors = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        selectors.Controls.AddRange([Caption("MAP"), _map, Caption("WEAPON"), _weapon]);
        root.Controls.Add(selectors);

        var capture = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        _captureTarget.Click += async (_, _) => await CaptureTargetAsync();
        _captureGun.Click += async (_, _) => await CaptureGunAsync();
        var manualTarget = new Button { Text = "Manual target", AutoSize = true };
        var manualGun = new Button { Text = "Manual gun", AutoSize = true };
        manualTarget.Click += (_, _) => AddManualTarget();
        manualGun.Click += (_, _) => AddManualGun();
        capture.Controls.AddRange([_captureTarget, _captureGun, manualTarget, manualGun]);
        root.Controls.Add(capture);

        var solution = new GroupBox { Text = "FIRING SOLUTION · FLAT TABLE", Dock = DockStyle.Fill, AutoSize = true };
        var solutionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Padding = new Padding(8) };
        solutionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        solutionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        solutionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        solutionLayout.Controls.Add(Caption("DISTANCE"), 0, 0);
        solutionLayout.Controls.Add(Caption("AZIMUTH"), 1, 0);
        solutionLayout.Controls.Add(Caption("ELEVATION"), 2, 0);
        solutionLayout.Controls.Add(_distance, 0, 1);
        solutionLayout.Controls.Add(_azimuth, 1, 1);
        solutionLayout.Controls.Add(_mils, 2, 1);
        solutionLayout.Controls.Add(_rangeStatus, 0, 2);
        solutionLayout.SetColumnSpan(_rangeStatus, 3);
        solution.Controls.Add(solutionLayout);
        root.Controls.Add(solution);

        var otherGunsGroup = new GroupBox
        {
            Text = "OTHER GUN SOLUTIONS · CURRENT TARGET",
            Dock = DockStyle.Fill,
            AutoSize = false,
            MinimumSize = new Size(0, 145)
        };
        otherGunsGroup.Controls.Add(_otherGunsGrid);
        root.Controls.Add(otherGunsGroup);

        var gunGroup = new GroupBox { Text = $"GUN POSITIONS · MAX {MaxGunsPerMap} PER MAP", Dock = DockStyle.Fill, AutoSize = true };
        var gunLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        var renameGun = new Button { Text = "Rename", AutoSize = true };
        var removeGun = new Button { Text = "Remove", AutoSize = true };
        renameGun.Click += (_, _) => RenameGun();
        removeGun.Click += (_, _) => RemoveGun();
        gunLayout.Controls.AddRange([_guns, _gunCoordinate, _gunName, renameGun, removeGun]);
        gunGroup.Controls.Add(gunLayout);
        root.Controls.Add(gunGroup);

        var knownGroup = new GroupBox
        {
            Text = "KNOWN TOWERS · SELECTED GUN · CLICK A ROW TO TARGET",
            Dock = DockStyle.Fill,
            AutoSize = false,
            MinimumSize = new Size(0, 195)
        };
        knownGroup.Controls.Add(_knownGrid);
        root.Controls.Add(knownGroup);

        var targetsGroup = new GroupBox
        {
            Text = "SAVED TARGETS · MAX 20 PER MAP",
            Dock = DockStyle.Fill,
            AutoSize = false,
            MinimumSize = new Size(0, 200)
        };
        var targetsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = false, Padding = new Padding(8), ColumnCount = 1, RowCount = 2 };
        targetsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        targetsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        var targetTools = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 42,
            MinimumSize = new Size(0, 42),
            ColumnCount = 2
        };
        targetTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        targetTools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var targetEditTools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var renameTarget = new Button { Text = "Rename", AutoSize = true };
        var removeTarget = new Button { Text = "Remove", AutoSize = true };
        var removeAllTargets = new Button
        {
            Text = "Remove all targets…",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(24, 3, 0, 3)
        };
        renameTarget.Click += (_, _) => RenameTarget();
        removeTarget.Click += (_, _) => RemoveTarget();
        removeAllTargets.Click += (_, _) => RemoveAllTargets();
        targetEditTools.Controls.AddRange([_targetCoordinate, _targetName, renameTarget, removeTarget]);
        targetTools.Controls.Add(targetEditTools, 0, 0);
        targetTools.Controls.Add(removeAllTargets, 1, 0);
        targetsLayout.Controls.Add(_targets);
        targetsLayout.Controls.Add(targetTools);
        targetsGroup.Controls.Add(targetsLayout);
        root.Controls.Add(targetsGroup);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var hotkeys = new Button { Text = "Hotkeys…", AutoSize = true };
        hotkeys.Click += (_, _) => ConfigureHotkeys();
        _topmost.CheckedChanged += (_, _) =>
        {
            TopMost = _topmost.Checked;
            _state.AlwaysOnTop = _topmost.Checked;
            Save();
        };
        footer.Controls.AddRange([hotkeys, _topmost, _status]);
        root.Controls.Add(footer);

        Controls.Add(root);

        _map.SelectedIndexChanged += (_, _) => ChangeMap();
        _weapon.SelectedIndexChanged += (_, _) =>
        {
            _state.SelectedWeaponId = CurrentWeaponId;
            Save();
            UpdateSolution();
            RefreshKnownTargets();
        };
        _guns.SelectedIndexChanged += (_, _) => SelectGun();
        _knownGrid.SelectionChanged += (_, _) => SelectKnownTarget();
        _targets.SelectionChanged += (_, _) => SelectSavedTarget();
        _targets.CellContentClick += (_, eventArgs) => RemoveTargetFromRow(eventArgs);
    }

    private void LoadStateIntoControls()
    {
        _map.Items.AddRange(KnownTargetRepository.Maps.Select(map => new MapChoice(map.Key, map.Value)).Cast<object>().ToArray());
        _weapon.Items.AddRange(_ballistics.Weapons.Cast<object>().ToArray());
        _map.SelectedItem = _map.Items.Cast<MapChoice>().FirstOrDefault(item => item.Id == _state.SelectedMapId) ?? _map.Items[0];
        _weapon.SelectedItem = _weapon.Items.Cast<WeaponSummary>().FirstOrDefault(item => item.Id == _state.SelectedWeaponId) ?? _weapon.Items[0];
        _topmost.Checked = _state.AlwaysOnTop;
        TopMost = _state.AlwaysOnTop;
        RefreshMapData();
        UpdateHotkeyLabels();
    }

    private void OnShown(object? sender, EventArgs e)
    {
        var warnings = new List<string>();
        try
        {
            _ocr = new CoordinateOcrService();
        }
        catch (Exception exception)
        {
            warnings.Add("OCR: " + exception.GetBaseException().Message);
            WriteDiagnostic("OCR initialization", exception);
        }

        try
        {
            _hotkeys = new HotkeyService(Handle);
            _hotkeys.Register(_state.Hotkeys);
        }
        catch (Exception exception)
        {
            warnings.Add("Hotkeys: " + exception.GetBaseException().Message);
            WriteDiagnostic("Hotkey initialization", exception);
        }

        SetStatus(
            warnings.Count == 0
                ? "Ready · open the map and hover a coordinate"
                : string.Join(" · ", warnings),
            warnings.Count > 0);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey)
        {
            var id = message.WParam.ToInt32();
            if (id == HotkeyService.TargetHotkeyId) BeginInvoke(async () => await CaptureTargetAsync());
            if (id == HotkeyService.GunHotkeyId) BeginInvoke(async () => await CaptureGunAsync());
        }
        base.WndProc(ref message);
    }

    private async Task CaptureTargetAsync()
    {
        var coordinate = await CaptureCoordinateAsync("target");
        if (coordinate is not null) AddTarget(coordinate);
    }

    private async Task CaptureGunAsync()
    {
        var coordinate = await CaptureCoordinateAsync("gun");
        if (coordinate is not null) AddOrReplaceGun(coordinate);
    }

    private async Task<MapCoordinate?> CaptureCoordinateAsync(string kind)
    {
        if (_ocr is null)
        {
            SetStatus("OCR is unavailable. Use manual entry.", true);
            return null;
        }
        if (!await _captureLock.WaitAsync(0)) return null;
        try
        {
            SetStatus($"Reading {kind} coordinates…");
            var result = await Task.Run(_ocr.CaptureAtMouse);
            if (!result.IsSuccess)
            {
                SetStatus(result.Message, true);
                SystemSounds.Hand.Play();
                return null;
            }
            SystemSounds.Asterisk.Play();
            SetStatus($"Captured {kind}: {result.Coordinate}");
            return result.Coordinate;
        }
        catch (Exception exception)
        {
            SetStatus("Capture failed: " + exception.Message, true);
            return null;
        }
        finally
        {
            _captureLock.Release();
        }
    }

    private void AddTarget(MapCoordinate coordinate)
    {
        var current = _state.Targets.Where(item => item.MapId == CurrentMapId).ToList();
        if (current.Count >= 20)
        {
            SetStatus("This map already has 20 saved targets. Remove one first.", true);
            return;
        }
        var target = new SavedPosition
        {
            MapId = CurrentMapId,
            Name = $"Target {current.Count + 1}",
            X = coordinate.X,
            Y = coordinate.Y
        };
        _state.Targets.Add(target);
        Save();
        RefreshTargets(target.Id);
    }

    private void AddOrReplaceGun(MapCoordinate coordinate)
    {
        var current = _state.Guns.Where(item => item.MapId == CurrentMapId).ToList();
        SavedPosition gun;
        if (current.Count < MaxGunsPerMap)
        {
            gun = new SavedPosition { MapId = CurrentMapId, Name = $"Gun {current.Count + 1}" };
            _state.Guns.Add(gun);
        }
        else if (SelectedGun is not null)
        {
            gun = SelectedGun;
        }
        else
        {
            SetStatus($"{MaxGunsPerMap} guns are saved. Select one to replace or remove one.", true);
            return;
        }
        gun.X = coordinate.X;
        gun.Y = coordinate.Y;
        Save();
        RefreshGuns(gun.Id);
    }

    private void AddManualTarget()
    {
        using var dialog = new CoordinateDialog("Add target coordinates");
        dialog.TopMost = TopMost;
        if (dialog.ShowDialog(this) == DialogResult.OK) AddTarget(dialog.Coordinate);
    }

    private void AddManualGun()
    {
        using var dialog = new CoordinateDialog("Add gun coordinates");
        dialog.TopMost = TopMost;
        if (dialog.ShowDialog(this) == DialogResult.OK) AddOrReplaceGun(dialog.Coordinate);
    }

    private void ChangeMap()
    {
        _state.SelectedMapId = CurrentMapId;
        _activeTarget = null;
        Save();
        RefreshMapData();
    }

    private void RefreshMapData()
    {
        RefreshGuns();
        RefreshTargets();
        RefreshKnownTargets();
        UpdateSolution();
    }

    private void RefreshGuns(Guid? selectId = null)
    {
        var guns = _state.Guns.Where(item => item.MapId == CurrentMapId).ToList();
        _guns.DataSource = null;
        _guns.DataSource = guns;
        if (selectId.HasValue)
            _guns.SelectedItem = guns.FirstOrDefault(item => item.Id == selectId.Value);
        _guns.SelectedIndex = _guns.Items.Count == 0 ? -1 : Math.Max(0, _guns.SelectedIndex);
        SelectGun();
    }

    private void RefreshTargets(Guid? selectId = null)
    {
        _targets.Rows.Clear();
        foreach (var target in _state.Targets.Where(item => item.MapId == CurrentMapId))
        {
            var rowIndex = _targets.Rows.Add(target.Name, target.X.ToString("0.00"), target.Y.ToString("0.00"));
            var row = _targets.Rows[rowIndex];
            row.Tag = target;
            if (target.Id == selectId)
            {
                row.Selected = true;
                _targets.CurrentCell = row.Cells[0];
            }
        }
        if (!selectId.HasValue)
        {
            _targets.ClearSelection();
            _targets.CurrentCell = null;
        }
    }

    private void SelectGun()
    {
        _gunCoordinate.Text = SelectedGun?.Coordinate.ToString() ?? "No gun captured";
        _gunName.Text = SelectedGun?.Name ?? string.Empty;
        UpdateSolution();
        RefreshKnownTargets();
    }

    private void SelectKnownTarget()
    {
        if (_refreshingKnownTargets || _knownGrid.SelectedRows.Count == 0) return;
        if (_knownGrid.SelectedRows[0].Tag is not KnownTarget target) return;
        _targets.ClearSelection();
        _activeTarget = target.Coordinate;
        _targetCoordinate.Text = $"{target.Name} · {target.Coordinate}";
        _targetName.Text = target.Name;
        UpdateSolution();
    }

    private void SelectSavedTarget()
    {
        var target = SelectedSavedTarget;
        if (target is null) return;
        _knownGrid.ClearSelection();
        _activeTarget = target.Coordinate;
        _targetCoordinate.Text = target.Coordinate.ToString();
        _targetName.Text = target.Name;
        UpdateSolution();
    }

    private void RenameGun()
    {
        if (SelectedGun is null || string.IsNullOrWhiteSpace(_gunName.Text)) return;
        SelectedGun.Name = _gunName.Text.Trim();
        Save();
        RefreshGuns(SelectedGun.Id);
    }

    private void RemoveGun()
    {
        if (SelectedGun is null) return;
        _state.Guns.Remove(SelectedGun);
        Save();
        RefreshGuns();
    }

    private void RenameTarget()
    {
        var target = SelectedSavedTarget;
        if (target is null || string.IsNullOrWhiteSpace(_targetName.Text)) return;
        target.Name = _targetName.Text.Trim();
        Save();
        RefreshTargets(target.Id);
    }

    private void RemoveTarget()
    {
        var target = SelectedSavedTarget;
        if (target is null) return;
        RemoveTarget(target);
    }

    private void RemoveTargetFromRow(DataGridViewCellEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 || eventArgs.ColumnIndex < 0 ||
            _targets.Columns[eventArgs.ColumnIndex].Name != "RemoveRow") return;
        if (_targets.Rows[eventArgs.RowIndex].Tag is SavedPosition target)
            RemoveTarget(target);
    }

    private void RemoveTarget(SavedPosition target)
    {
        _state.Targets.Remove(target);
        if (SelectedSavedTarget?.Id == target.Id ||
            (_activeTarget is not null &&
             Math.Abs(_activeTarget.X - target.X) < 0.0001 &&
             Math.Abs(_activeTarget.Y - target.Y) < 0.0001))
            _activeTarget = null;
        Save();
        RefreshTargets();
        UpdateSolution();
    }

    private void RemoveAllTargets()
    {
        var count = _state.Targets.Count(item => item.MapId == CurrentMapId);
        if (count == 0)
        {
            SetStatus("There are no saved targets on this map.");
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Remove all {count} saved targets from {KnownTargetRepository.Maps[CurrentMapId]}?\n\nThis cannot be undone.",
            "Remove all targets",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        _state.Targets.RemoveAll(item => item.MapId == CurrentMapId);
        _activeTarget = null;
        _targetCoordinate.Text = "No target selected";
        _targetName.Text = string.Empty;
        Save();
        RefreshTargets();
        UpdateSolution();
        SetStatus($"Removed {count} targets from {KnownTargetRepository.Maps[CurrentMapId]}.");
    }

    private void UpdateSolution()
    {
        if (SelectedGun is null || _activeTarget is null)
        {
            _distance.Text = "— m";
            _azimuth.Text = "—°";
            _mils.Text = "— mil";
            _rangeStatus.Text = "Awaiting gun and target";
            RefreshOtherGunSolutions();
            return;
        }

        var solution = _ballistics.Calculate(CurrentWeaponId, SelectedGun.Coordinate, _activeTarget);
        _distance.Text = $"{solution.DistanceMeters:0} m";
        _azimuth.Text = $"{solution.AzimuthDegrees:000.0}°";
        _mils.Text = solution.Mils.Count == 0
            ? "— mil"
            : string.Join(Environment.NewLine, solution.Mils.Select(item => $"{item.Arc} {item.Mil:0} mil"));
        _rangeStatus.Text = solution.RangeStatus + " · TERRAIN NOT APPLIED";
        _rangeStatus.ForeColor = solution.IsWithinTable ? Color.FromArgb(130, 210, 145) : Color.FromArgb(242, 156, 70);
        RefreshOtherGunSolutions();
    }

    private void RefreshKnownTargets()
    {
        if (_knownGrid.Columns.Count == 0) return;
        _refreshingKnownTargets = true;
        try
        {
            _knownGrid.Rows.Clear();
            DataGridViewRow? activeRow = null;
            foreach (var target in _knownTargets.ForMap(CurrentMapId))
            {
                var values = new object[] { target.Name, "—", "—", "—", "SET A GUN" };
                FiringSolution? solution = null;
                if (SelectedGun is not null)
                {
                    solution = _ballistics.Calculate(CurrentWeaponId, SelectedGun.Coordinate, target.Coordinate);
                    values =
                    [
                        target.Name,
                        $"{solution.DistanceMeters:0} m",
                        $"{solution.AzimuthDegrees:000.0}°",
                        FormatMils(solution),
                        CompactStatus(solution)
                    ];
                }

                var rowIndex = _knownGrid.Rows.Add(values);
                var row = _knownGrid.Rows[rowIndex];
                row.Tag = target;
                if (solution is not null)
                    row.DefaultCellStyle.ForeColor = solution.IsWithinTable
                        ? Color.FromArgb(160, 225, 170)
                        : Color.FromArgb(242, 165, 90);
                if (_activeTarget is not null && SelectedSavedTarget is null &&
                    Math.Abs(_activeTarget.X - target.Coordinate.X) < 0.0001 &&
                    Math.Abs(_activeTarget.Y - target.Coordinate.Y) < 0.0001)
                    activeRow = row;
            }
            _knownGrid.ClearSelection();
            if (activeRow is not null)
            {
                activeRow.Selected = true;
                _knownGrid.CurrentCell = activeRow.Cells[0];
            }
            else
            {
                _knownGrid.CurrentCell = null;
            }
        }
        finally
        {
            _refreshingKnownTargets = false;
        }
    }

    private void RefreshOtherGunSolutions()
    {
        if (_otherGunsGrid.Columns.Count == 0) return;
        _otherGunsGrid.Rows.Clear();
        if (_activeTarget is null) return;

        foreach (var gun in _state.Guns.Where(item => item.MapId == CurrentMapId && item.Id != SelectedGun?.Id))
        {
            var solution = _ballistics.Calculate(CurrentWeaponId, gun.Coordinate, _activeTarget);
            var rowIndex = _otherGunsGrid.Rows.Add(
                gun.Name,
                $"{solution.DistanceMeters:0} m",
                $"{solution.AzimuthDegrees:000.0}°",
                FormatMils(solution),
                CompactStatus(solution));
            _otherGunsGrid.Rows[rowIndex].DefaultCellStyle.ForeColor = solution.IsWithinTable
                ? Color.FromArgb(200, 215, 220)
                : Color.FromArgb(242, 165, 90);
        }
        _otherGunsGrid.ClearSelection();
    }

    private static string FormatMils(FiringSolution solution) => solution.Mils.Count == 0
        ? "—"
        : string.Join(" / ", solution.Mils.Select(item => $"{item.Arc} {item.Mil:0}"));

    private static string CompactStatus(FiringSolution solution)
    {
        if (solution.IsWithinTable) return "IN RANGE";
        if (!solution.IsWithinReportedEnvelope)
            return solution.RangeStatus.StartsWith("TOO CLOSE", StringComparison.Ordinal) ? "TOO CLOSE" : "OUT OF RANGE";
        return "NO MIL TABLE";
    }

    private void ConfigureHotkeys()
    {
        _hotkeys?.Unregister();
        using var dialog = new HotkeySettingsDialog(_state.Hotkeys);
        dialog.TopMost = TopMost;
        try
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                _hotkeys?.Register(_state.Hotkeys);
                return;
            }
            _hotkeys?.Register(dialog.Result);
            _state.Hotkeys = dialog.Result;
            Save();
            UpdateHotkeyLabels();
            SetStatus("Hotkeys updated.");
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, true);
            try { _hotkeys?.Register(_state.Hotkeys); } catch { }
        }
    }

    private void UpdateHotkeyLabels()
    {
        _captureTarget.Text = $"Capture target [{new HotkeyBinding(_state.Hotkeys.TargetKey, _state.Hotkeys.TargetModifiers)}]";
        _captureGun.Text = $"Capture gun [{new HotkeyBinding(_state.Hotkeys.GunKey, _state.Hotkeys.GunModifiers)}]";
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.ForeColor = error ? Color.FromArgb(242, 120, 110) : Color.FromArgb(155, 170, 180);
    }

    private void Save()
    {
        try { _store.Save(_state); }
        catch (Exception exception) { SetStatus("Could not save: " + exception.Message, true); }
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _hotkeys?.Dispose();
        _ocr?.Dispose();
        _captureLock.Dispose();
    }

    private static void WriteDiagnostic(string area, Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WardogsFireControl");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "diagnostics.log"),
                $"{DateTimeOffset.Now:O} · {area}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never prevent the application from starting.
        }
    }

    private static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.FromArgb(130, 145, 155),
        Font = new Font("Segoe UI Semibold", 8F),
        Margin = new Padding(4, 8, 4, 0)
    };

    private static Label ValueLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.FromArgb(200, 210, 215),
        Margin = new Padding(4, 8, 4, 4)
    };

    private static Label SolutionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.FromArgb(255, 180, 65),
        Font = new Font("Segoe UI Semibold", 15F),
        Margin = new Padding(4, 4, 4, 8)
    };

    private static DataGridView CreateDataGrid(int height)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            Height = height,
            MinimumSize = new Size(0, height),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            MultiSelect = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false,
            BackgroundColor = Color.FromArgb(20, 25, 30),
            BorderStyle = BorderStyle.FixedSingle,
            CellBorderStyle = DataGridViewCellBorderStyle.Single,
            GridColor = Color.FromArgb(70, 82, 91),
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 30,
            RowTemplate = { Height = 27 }
        };
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(58, 67, 75),
            ForeColor = Color.FromArgb(238, 241, 243),
            SelectionBackColor = Color.FromArgb(58, 67, 75),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 8.5F),
            Padding = new Padding(5, 0, 0, 0)
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(27, 33, 39),
            ForeColor = Color.FromArgb(222, 228, 232),
            SelectionBackColor = Color.FromArgb(82, 91, 98),
            SelectionForeColor = Color.White,
            Padding = new Padding(5, 0, 3, 0)
        };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(34, 41, 47),
            ForeColor = Color.FromArgb(222, 228, 232),
            SelectionBackColor = Color.FromArgb(82, 91, 98),
            SelectionForeColor = Color.White,
            Padding = new Padding(5, 0, 3, 0)
        };
        return grid;
    }

    private static void ApplyTheme(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = Color.FromArgb(38, 47, 55);
                    button.ForeColor = Color.FromArgb(230, 235, 238);
                    button.FlatAppearance.BorderColor = Color.FromArgb(75, 88, 98);
                    break;
                case TextBox textBox:
                    textBox.BackColor = Color.FromArgb(29, 36, 42);
                    textBox.ForeColor = Color.White;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = Color.FromArgb(29, 36, 42);
                    comboBox.ForeColor = Color.White;
                    comboBox.FlatStyle = FlatStyle.Flat;
                    break;
                case GroupBox groupBox:
                    groupBox.ForeColor = Color.FromArgb(160, 174, 184);
                    break;
            }
            ApplyTheme(control);
        }
    }

    private sealed record MapChoice(string Id, string Name)
    {
        public override string ToString() => Name;
    }
}
