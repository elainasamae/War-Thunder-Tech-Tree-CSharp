using System.Diagnostics;
using System.Text.Json;

namespace WarThunderTechTree.Native;

internal sealed class MainForm : Form
{
    private readonly DataRepository _repository = new();
    private readonly TechTreeCanvas _canvas = new() { Dock = DockStyle.Fill };
    private readonly ToolStrip _toolbar = new();
    private readonly ToolStripComboBox _countryBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly ToolStripStatusLabel _summary = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _dataVersion = new();
    private readonly Dictionary<string, ToolStripButton> _typeButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ToolStripButton> _modeButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly ToolStripButton _languageButton = new("名称：中文");
    private TreeData? _currentTree;
    private string _vehicleType = "ground";
    private string _mode = "rb";
    private bool _chineseNames = true;
    private bool _initialized;

    public MainForm()
    {
        Text = "War Thunder 科技树 · C# 原生版";
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1500;
        Height = 920;
        MinimumSize = new Size(980, 640);
        BackColor = Color.FromArgb(11, 17, 22);
        ForeColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildToolbar();
        var status = new StatusStrip
        {
            SizingGrip = false,
            BackColor = Color.FromArgb(23, 33, 40),
            ForeColor = Color.FromArgb(220, 230, 235)
        };
        status.Items.AddRange([_summary, _dataVersion]);
        Controls.Add(_canvas);
        Controls.Add(status);
        Controls.Add(_toolbar);

        _canvas.SelectionChanged += (_, _) => UpdateSummary();
        _canvas.VehicleContextRequested += CanvasOnVehicleContextRequested;
        _canvas.VehicleActivated += (_, e) => ShowBasicVehicle(e.Vehicle);
        Shown += async (_, _) => await InitializeAsync();
        FormClosed += (_, _) => _repository.Dispose();
    }

    private void BuildToolbar()
    {
        _toolbar.Dock = DockStyle.Top;
        _toolbar.Height = 48;
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _toolbar.Padding = new Padding(10, 7, 10, 7);
        _toolbar.BackColor = Color.FromArgb(24, 35, 42);
        _toolbar.ForeColor = Color.White;
        _toolbar.RenderMode = ToolStripRenderMode.System;
        _toolbar.Items.Add(new ToolStripLabel("国家"));
        _toolbar.Items.Add(_countryBox);
        _toolbar.Items.Add(new ToolStripSeparator());

        AddTypeButton("ground", "陆军");
        AddTypeButton("aviation", "空军");
        AddTypeButton("helicopter", "直升机");
        _toolbar.Items.Add(new ToolStripSeparator());
        AddModeButton("ab", "街机 AB");
        AddModeButton("rb", "历史 RB");
        AddModeButton("sb", "全真 SB");
        _toolbar.Items.Add(new ToolStripSeparator());

        _languageButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
        _languageButton.Click += (_, _) =>
        {
            _chineseNames = !_chineseNames;
            _languageButton.Text = _chineseNames ? "名称：中文" : "Name: English";
            _canvas.ChineseNames = _chineseNames;
            _canvas.Invalidate();
        };
        _toolbar.Items.Add(_languageButton);
        var clear = new ToolStripButton("清空选择");
        clear.Click += (_, _) => _canvas.ClearSelection();
        _toolbar.Items.Add(clear);

        _countryBox.SelectedIndexChanged += async (_, _) =>
        {
            if (_initialized) await LoadTreeAsync();
        };
    }

    private void AddTypeButton(string code, string label)
    {
        var button = new ToolStripButton(label) { CheckOnClick = false, Checked = code == _vehicleType };
        button.Click += async (_, _) =>
        {
            if (_vehicleType == code) return;
            _vehicleType = code;
            foreach (var pair in _typeButtons) pair.Value.Checked = pair.Key == code;
            await LoadTreeAsync();
        };
        _typeButtons[code] = button;
        _toolbar.Items.Add(button);
    }

    private void AddModeButton(string code, string label)
    {
        var button = new ToolStripButton(label) { CheckOnClick = false, Checked = code == _mode };
        button.Click += (_, _) =>
        {
            _mode = code;
            foreach (var pair in _modeButtons) pair.Value.Checked = pair.Key == code;
            _canvas.Mode = code;
            _canvas.Invalidate();
            UpdateSummary();
        };
        _modeButtons[code] = button;
        _toolbar.Items.Add(button);
    }

    private async Task InitializeAsync()
    {
        try
        {
            UseWaitCursor = true;
            await _repository.InitializeAsync();
            _countryBox.Items.AddRange(_repository.Countries.Countries.Cast<object>().ToArray());
            _countryBox.SelectedIndex = Math.Max(0, _repository.Countries.Countries.FindIndex(country => country.Code == "usa"));
            _initialized = true;
            _dataVersion.Text = $"游戏数据 {_repository.Countries.GameDataVersion ?? "未知版本"}";
            await LoadTreeAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"载入数据失败：\n{exception.Message}", "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task LoadTreeAsync()
    {
        if (_countryBox.SelectedItem is not CountryEntry country) return;
        try
        {
            _toolbar.Enabled = false;
            UseWaitCursor = true;
            _currentTree = await _repository.GetTreeAsync(country.Code, _vehicleType);
            _canvas.Mode = _mode;
            _canvas.ChineseNames = _chineseNames;
            _canvas.SetTree(_currentTree);
            UpdateSummary();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"无法载入科技树：\n{exception.Message}", "数据错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _toolbar.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void UpdateSummary()
    {
        if (_currentTree is null)
        {
            _summary.Text = "正在载入…";
            return;
        }
        var selected = _currentTree.Vehicles.Where(vehicle => _canvas.SelectedIds.Contains(vehicle.UnitId)).ToList();
        var rp = selected.Sum(vehicle => vehicle.ResearchRp ?? 0);
        var sl = selected.Sum(vehicle => vehicle.PurchaseSl ?? 0);
        _summary.Text = $"已选择 {selected.Count} 辆　研发点 {rp:N0}　银狮 {sl:N0}　当前模式 {_mode.ToUpperInvariant()}";
    }

    private void CanvasOnVehicleContextRequested(object? sender, VehicleEventArgs e)
    {
        if (_currentTree is null || _countryBox.SelectedItem is not CountryEntry country) return;
        var vehicle = e.Vehicle;
        Task<VehicleDetails>? detailsTask = null;
        Task<VehicleDetails> Details() => detailsTask ??= _repository.GetDetailsAsync(country.Code, _vehicleType, vehicle);

        var menu = new ContextMenuStrip();
        menu.Items.Add($"{vehicle.DisplayName(_chineseNames)}  ·  {_mode.ToUpperInvariant()} {vehicle.Rating(_mode)}").Enabled = false;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("查看车辆基本数据", null, (_, _) => ShowBasicVehicle(vehicle));
        AddDetailItem(menu, "查看辅助设备", "辅助设备", vehicle, async () => (await Details()).Auxiliary);
        AddDetailItem(menu, "查看车辆性能", "车辆性能", vehicle, async () => (await Details()).Specifications);
        AddDetailItem(menu, "查看弹药数据", "弹药数据", vehicle, async () => (await Details()).Ammunition);
        AddDetailItem(menu, "查看挂载数据", "挂载数据", vehicle, async () => (await Details()).Loadouts);
        AddDetailItem(menu, "查看改装件", "改装件", vehicle, async () => (await Details()).Modifications);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("选择最快研发路径", null, (_, _) =>
        {
            var route = ResearchPlanner.Fastest(_currentTree, vehicle);
            _canvas.SetSelection(route.Vehicles.Select(item => item.UnitId));
            _summary.Text = $"最快路径 {route.Vehicles.Count} 辆（补充 {route.ExtraCount} 辆）　研发点 {route.TotalRp:N0}　银狮 {route.TotalSl:N0}";
        });
        menu.Items.Add("复制载具 ID", null, (_, _) => Clipboard.SetText(vehicle.UnitId));
        if (!string.IsNullOrWhiteSpace(vehicle.WikiUrl))
        {
            menu.Items.Add("打开 War Thunder Wiki", null, (_, _) =>
            {
                Process.Start(new ProcessStartInfo(vehicle.WikiUrl) { UseShellExecute = true });
            });
        }
        menu.Closed += (_, _) =>
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(() => menu.Dispose());
            else
                menu.Dispose();
        };
        menu.Show(_canvas, _canvas.PointToClient(Cursor.Position));
    }

    private void AddDetailItem(ContextMenuStrip menu, string label, string section, VehicleRecord vehicle, Func<Task<JsonElement?>> load)
    {
        menu.Items.Add(label, null, async (_, _) =>
        {
            try
            {
                UseWaitCursor = true;
                var data = await load();
                if (data is null)
                {
                    MessageBox.Show(this, "该载具暂无这类资料。", label, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                using var form = new JsonDetailForm(section, data.Value, vehicle.DisplayName(_chineseNames), _mode);
                form.ShowDialog(this);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, $"读取资料失败：\n{exception.Message}", label, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        });
    }

    private void ShowBasicVehicle(VehicleRecord vehicle)
    {
        var data = JsonSerializer.SerializeToElement(new
        {
            中文名称 = vehicle.NameChinese,
            英文名称 = vehicle.NameEnglish,
            载具ID = vehicle.UnitId,
            等级 = vehicle.Rank,
            街机权重 = vehicle.BattleRatingAb,
            历史权重 = vehicle.BattleRatingRb,
            全真权重 = vehicle.BattleRatingSb,
            研发点 = vehicle.ResearchRp,
            购买银狮 = vehicle.PurchaseSl,
            前置载具 = vehicle.RequirementName,
            官网 = vehicle.WikiUrl
        });
        using var form = new JsonDetailForm("载具资料", data, vehicle.DisplayName(_chineseNames), _mode);
        form.ShowDialog(this);
    }
}
