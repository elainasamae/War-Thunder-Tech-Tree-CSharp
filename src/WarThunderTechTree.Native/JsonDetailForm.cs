using System.Text.Json;

namespace WarThunderTechTree.Native;

internal sealed class JsonDetailForm : Form
{
    private static readonly Color WindowBack = Color.FromArgb(9, 15, 20);
    private static readonly Color CardBack = Color.FromArgb(25, 37, 45);
    private static readonly Color CardBackAlt = Color.FromArgb(32, 47, 57);
    private static readonly Color Line = Color.FromArgb(66, 87, 101);
    private static readonly Color Muted = Color.FromArgb(143, 163, 176);
    private static readonly Color Value = Color.FromArgb(207, 224, 234);
    private static readonly Color Accent = Color.FromArgb(213, 167, 95);
    private static readonly Color Good = Color.FromArgb(142, 226, 170);
    private static readonly Lazy<ModDictionaryData> ModDictionary = new(LoadModDictionary);
    private readonly string _section;
    private readonly string _mode;

    public JsonDetailForm(string section, JsonElement data, string? vehicleName = null, string mode = "rb")
    {
        _section = section;
        _mode = mode;
        var resolvedName = vehicleName ?? String(data, "vehicle") ?? section;

        Text = $"{resolvedName} · {section}";
        StartPosition = FormStartPosition.CenterParent;
        Size = section switch
        {
            "辅助设备" => new Size(500, 540),
            "车辆性能" => new Size(470, 650),
            "弹药数据" => new Size(800, 760),
            "挂载数据" => new Size(980, 730),
            "改装件" => new Size(920, 700),
            _ => new Size(570, 610)
        };
        MinimumSize = new Size(410, 360);
        BackColor = WindowBack;
        ForeColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        var content = section switch
        {
            "辅助设备" => BuildAuxiliary(data),
            "车辆性能" => BuildSpecifications(data),
            "弹药数据" => BuildAmmunition(data),
            "挂载数据" => BuildLoadouts(data),
            "改装件" => BuildModifications(data),
            _ => BuildBasic(data)
        };
        content.Dock = DockStyle.Fill;
        Controls.Add(content);
        Controls.Add(BuildHeader(resolvedName, Subtitle(data)));
    }

    private Control BuildHeader(string vehicleName, string subtitle)
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Color.FromArgb(28, 42, 52) };
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(Line);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(13, 10),
            Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
            ForeColor = Color.White,
            Text = vehicleName
        });
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(13, 36),
            ForeColor = Muted,
            Text = subtitle
        });
        return header;
    }

    private string Subtitle(JsonElement data) => _section switch
    {
        "车辆性能" => $"车辆数据 · {ModeName(_mode)}",
        "弹药数据" => $"弹药数据 · {String(data, "weapon") ?? "主武器"}",
        "挂载数据" => $"挂载 · 最大 {String(data, "maximum_load") ?? "无数据"}",
        "改装件" => $"配件总览 · {Number(data, "mod_count") ?? "0"} 项",
        "辅助设备" => "辅助设备",
        _ => "载具资料"
    };

    private Control BuildAuxiliary(JsonElement data)
    {
        var rows = new List<DetailRow>();
        if (Try(data, "equipment", out var equipment) && equipment.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in equipment.EnumerateArray())
            {
                var label = String(item, "label") ?? "设备";
                if (Try(item, "quantity", out var quantity) && quantity.ValueKind == JsonValueKind.Object)
                {
                    rows.Add(new DetailRow(label, $"{Number(quantity, "perSalvo") ?? "—"}/{Number(quantity, "total") ?? "—"}", Value));
                }
                else
                {
                    var present = Boolean(item, "present");
                    rows.Add(new DetailRow(label, present ? "✓" : "无", present ? Good : Muted, present ? 15f : 9f));
                }
            }
        }

        var thermals = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (Try(data, "thermal_imaging", out var thermalArray) && thermalArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var thermal in thermalArray.EnumerateArray())
            {
                var station = String(thermal, "station");
                if (station is not null) thermals[station] = thermal;
            }
        }
        var platform = String(data, "platform");
        var stations = platform == "ground"
            ? new[] { (Id: "gunner", Label: "炮长"), (Id: "commander", Label: "车长") }
            : thermals.Select(pair => (Id: pair.Key, Label: String(pair.Value, "label") ?? pair.Key)).ToArray();
        foreach (var station in stations)
        {
            if (!thermals.TryGetValue(station.Id, out var thermal))
            {
                rows.Add(new DetailRow($"热成像（{station.Label}）", "无", Muted));
                continue;
            }
            var quality = String(thermal, "quality") == "high" ? "高清" : "低清";
            var details = string.Join(" · ", new[] { quality, String(thermal, "resolution") }.Where(text => !string.IsNullOrWhiteSpace(text)));
            rows.Add(new DetailRow($"热成像（{station.Label}）", details, quality == "高清" ? Good : Color.FromArgb(229, 170, 117)));
        }
        return BuildScrollableRows(rows, $"{(platform == "ground" ? "游戏配置数据" : "官网特征与游戏配置数据")} · {String(data, "vehicle") ?? ""}");
    }

    private Control BuildSpecifications(JsonElement data)
    {
        var rows = new[]
        {
            new DetailRow("前进极速", ModeValue(data, "forward_speed"), Value),
            new DetailRow("倒车极速", ModeValue(data, "reverse_speed"), Value),
            new DetailRow("战斗全重", ModeRange(data, "weight"), Value),
            new DetailRow("发动机功率", ModeRange(data, "engine_power"), Value),
            new DetailRow("推重比", ModeRange(data, "power_to_weight"), Value),
            new DetailRow("炮塔方向机（水平）", ModeRange(data, "turret_horizontal"), Value),
            new DetailRow("炮塔高低机（垂直）", ModeRange(data, "turret_vertical"), Value),
            new DetailRow("主炮装填（基础→王牌）", PairValue(data, "reload", "basic", "aces"), Value),
            new DetailRow("主炮俯仰角", GuidanceValue(data), Value)
        };
        return BuildScrollableRows(rows,
            $"当前为{ModeName(_mode)}数据；最低/最高取决于改装状态与乘员熟练度\r\n官方规格数据 · {String(data, "vehicle") ?? ""}");
    }

    private Control BuildAmmunition(JsonElement data)
    {
        var list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8),
            BackColor = WindowBack
        };
        if (!Try(data, "ammunition", out var ammunition) || ammunition.ValueKind != JsonValueKind.Array)
        {
            list.Controls.Add(EmptyLabel("暂无弹药数据"));
            return list;
        }

        var rounds = ammunition.EnumerateArray().ToArray();
        var preferred = rounds.Any(round => String(round, "name") == "M829A3") ? "M829A3" : String(rounds.FirstOrDefault(), "name");
        foreach (var round in rounds)
            list.Controls.Add(BuildRoundCard(round, String(round, "name") == preferred));
        void ResizeCards()
        {
            var width = Math.Max(520, list.ClientSize.Width - 28);
            foreach (Control control in list.Controls) control.Width = width;
        }
        list.ClientSizeChanged += (_, _) => ResizeCards();
        Shown += (_, _) => ResizeCards();
        return list;
    }

    private Control BuildRoundCard(JsonElement round, bool initiallyOpen)
    {
        var card = new Panel { Height = 48, BackColor = CardBack, Margin = new Padding(0, 0, 0, 7) };
        var name = String(round, "name") ?? "弹药";
        var type = String(round, "type") ?? "";
        var mass = String(round, "projectile_mass") ?? "无数据";
        var speed = String(round, "muzzle_velocity") ?? "无数据";
        var header = new Button
        {
            Dock = DockStyle.Top,
            Height = 48,
            FlatStyle = FlatStyle.Flat,
            BackColor = CardBackAlt,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0),
            Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold)
        };
        header.FlatAppearance.BorderColor = Line;
        var body = BuildRoundBody(round);
        body.Location = new Point(0, header.Height);
        body.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.Add(body);
        card.Controls.Add(header);
        void SetOpen(bool open)
        {
            body.Visible = open;
            header.Text = $"{(open ? "▼" : "▶")}  {name}   {type}                         重量 {mass}   ·   弹速 {speed}";
            card.Height = header.Height + (open ? body.Height : 0);
        }
        header.Click += (_, _) => SetOpen(!body.Visible);
        card.SizeChanged += (_, _) => body.Width = card.ClientSize.Width;
        SetOpen(initiallyOpen);
        return card;
    }

    private Control BuildRoundBody(JsonElement round)
    {
        var body = new FlowLayoutPanel
        {
            AutoSize = false,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = CardBack,
            Padding = new Padding(8),
            Width = 720
        };
        if (Try(round, "characteristics", out var characteristics) && characteristics.ValueKind == JsonValueKind.Array)
        {
            var rows = characteristics.EnumerateArray()
                .Select(item => new DetailRow(TranslateCharacteristic(String(item, "label") ?? "参数"), TranslateValue(String(item, "value") ?? "—"), Value))
                .ToArray();
            if (rows.Length > 0)
            {
                var table = CreateRows(rows);
                table.Width = 690;
                body.Controls.Add(table);
            }
        }
        if (Try(round, "fragmentation_penetration_mm", out var fragmentation) && fragmentation.ValueKind == JsonValueKind.Number)
        {
            var fragmentationLabel = SectionLabel($"高爆破片穿深：{fragmentation.GetRawText()} 毫米", Accent);
            fragmentationLabel.Width = 690;
            body.Controls.Add(fragmentationLabel);
        }

        if (Try(round, "penetration_profiles", out var profiles) && profiles.ValueKind == JsonValueKind.Array)
        {
            foreach (var profile in profiles.EnumerateArray())
            {
                var mechanism = String(profile, "mechanism") switch
                {
                    "Cumulative jet" => "破甲射流",
                    "Kinetic" => "弹体动能",
                    "Armor penetration (max.)" => "最大",
                    var other => other ?? "弹体"
                };
                var penetrationLabel = SectionLabel($"{mechanism}穿深（毫米）", Color.FromArgb(185, 203, 214));
                penetrationLabel.Width = 690;
                body.Controls.Add(penetrationLabel);
                body.Controls.Add(BuildPenetrationGrid(profile));
            }
        }
        body.Height = Math.Max(80, body.PreferredSize.Height + 12);
        return body;
    }

    private Control BuildPenetrationGrid(JsonElement profile)
    {
        var grid = NewGrid();
        grid.Height = 182;
        grid.Width = 690;
        grid.Columns.Add("distance", "距离");
        var angles = Try(profile, "angles_deg", out var angleArray) && angleArray.ValueKind == JsonValueKind.Array
            ? angleArray.EnumerateArray().Select(angle => angle.GetRawText()).ToArray()
            : [];
        foreach (var angle in angles) grid.Columns.Add($"angle_{angle}", $"{angle}°");
        if (Try(profile, "penetration_mm", out var values) && values.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in values.EnumerateArray())
            {
                var cells = new List<string> { $"{Number(row, "distance_m") ?? "—"} 米" };
                var byAngle = Try(row, "by_angle", out var element) ? element : default;
                cells.AddRange(angles.Select(angle => byAngle.ValueKind == JsonValueKind.Object && byAngle.TryGetProperty(angle, out var value) ? value.GetRawText() : "—"));
                grid.Rows.Add(cells.Cast<object>().ToArray());
            }
        }
        return grid;
    }

    private Control BuildLoadouts(JsonElement data)
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = WindowBack, ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var limits = SectionLabel($"单侧机翼上限：{String(data, "wing_load_maximum") ?? "无数据"}      左右最大差值：{String(data, "maximum_imbalance") ?? "无数据"}", Muted);
        limits.Dock = DockStyle.Fill;
        root.Controls.Add(limits, 0, 0);

        var weapons = Try(data, "weapons", out var weaponArray) && weaponArray.ValueKind == JsonValueKind.Array
            ? weaponArray.EnumerateArray().Select((weapon, index) => new WeaponItem(index, weapon.Clone())).ToList()
            : [];
        var slots = weapons.SelectMany(item => IntArray(item.Weapon, "available_slots")).Distinct().OrderBy(value => value).ToArray();
        var slotBar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 6, 8, 4), BackColor = CardBack, WrapContents = false };
        root.Controls.Add(slotBar, 0, 1);
        var status = SectionLabel("点击挂点查看可安装的武器和设备", Muted);
        status.Dock = DockStyle.Fill;
        root.Controls.Add(status, 0, 2);
        var split = new SplitContainer { Dock = DockStyle.Fill, BackColor = Line };
        split.SizeChanged += (_, _) =>
        {
            if (split.ClientSize.Width > 600)
                split.SplitterDistance = Math.Clamp((int)(split.ClientSize.Width * 0.54), 260, split.ClientSize.Width - 260);
        };
        root.Controls.Add(split, 0, 3);
        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = CardBack,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 52,
            Font = new Font(Font.FontFamily, 9.3f)
        };
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            var item = (WeaponItem)list.Items[e.Index];
            var selected = (e.State & DrawItemState.Selected) != 0;
            var cardRect = Rectangle.Inflate(e.Bounds, -4, -3);
            using var background = new SolidBrush(selected ? CardBackAlt : CardBack);
            using var border = new Pen(selected ? Accent : Line);
            using var titleBrush = new SolidBrush(Color.White);
            using var detailBrush = new SolidBrush(Muted);
            using var titleFont = new Font(list.Font, FontStyle.Bold);
            e.Graphics.FillRectangle(background, cardRect);
            e.Graphics.DrawRectangle(border, cardRect);
            e.Graphics.DrawString(String(item.Weapon, "name") ?? "挂载", titleFont, titleBrush,
                new RectangleF(cardRect.X + 8, cardRect.Y + 6, cardRect.Width - 16, 18));
            var slotsText = string.Join("/", IntArray(item.Weapon, "available_slots"));
            e.Graphics.DrawString($"{String(item.Weapon, "weight") ?? "无重量数据"} · {(slotsText.Length > 0 ? $"挂点 {slotsText}" : "预设挂载")}",
                list.Font, detailBrush, new RectangleF(cardRect.X + 8, cardRect.Y + 27, cardRect.Width - 16, 17));
        };
        split.Panel1.Controls.Add(list);
        var detailHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = WindowBack, Padding = new Padding(10) };
        split.Panel2.Controls.Add(detailHost);
        int? selectedSlot = null;

        void ShowDetail(WeaponItem? item)
        {
            detailHost.Controls.Clear();
            detailHost.Controls.Add(item is null ? EmptyLabel("点击左侧具体武器或弹药查看参数") : BuildWeaponDetails(item.Weapon));
        }
        void Filter()
        {
            var visible = selectedSlot is null ? weapons : weapons.Where(item => IntArray(item.Weapon, "available_slots").Contains(selectedSlot.Value)).ToList();
            list.Items.Clear();
            list.Items.AddRange(visible.Cast<object>().ToArray());
            status.Text = selectedSlot is null ? "点击挂点查看可安装的武器和设备" : $"挂点 {selectedSlot} · 可安装 {visible.Count} 种挂载";
            ShowDetail(null);
        }
        foreach (var slot in slots)
        {
            var button = new Button { Text = slot.ToString(), Width = 45, Height = 29, FlatStyle = FlatStyle.Flat, ForeColor = Value, BackColor = CardBackAlt, Tag = slot };
            button.FlatAppearance.BorderColor = Line;
            button.Click += (_, _) =>
            {
                selectedSlot = selectedSlot == slot ? null : slot;
                foreach (Button item in slotBar.Controls) item.BackColor = selectedSlot == (int)item.Tag! ? Color.FromArgb(45, 145, 177) : CardBackAlt;
                Filter();
            };
            slotBar.Controls.Add(button);
        }
        list.SelectedIndexChanged += (_, _) => ShowDetail(list.SelectedItem as WeaponItem);
        Filter();
        return root;
    }

    private Control BuildWeaponDetails(JsonElement weapon)
    {
        var rows = new List<DetailRow>
        {
            new("挂载重量", String(weapon, "weight") ?? "无数据", Value),
            new("可用挂点", string.Join("、", IntArray(weapon, "available_slots")), Accent)
        };
        if (Try(weapon, "parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Array)
            rows.AddRange(parameters.EnumerateArray().Select(parameter => new DetailRow(TranslateCharacteristic(String(parameter, "label") ?? "参数"), TranslateValue(String(parameter, "value") ?? "—"), Value)));
        var panel = new Panel { Dock = DockStyle.Top, BackColor = WindowBack };
        var table = CreateRows(rows);
        var title = SectionLabel(String(weapon, "name") ?? "挂载详情", Color.White, 11f);
        title.Location = Point.Empty;
        title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        table.Location = new Point(0, title.Height);
        table.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        panel.Height = title.Height + table.Height;
        panel.Resize += (_, _) =>
        {
            title.Width = panel.ClientSize.Width;
            table.Width = panel.ClientSize.Width;
        };
        panel.Controls.Add(table);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildModifications(JsonElement data)
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = WindowBack, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var status = SectionLabel($"全部配件 {Number(data, "mod_count") ?? "0"} 项 · 研发合计 {Number(data, "total_research_rp") ?? "0"} RP · 购买合计 {Number(data, "total_purchase_sl") ?? "0"} 银狮", Muted);
        status.Dock = DockStyle.Fill;
        root.Controls.Add(status, 0, 0);

        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = WindowBack };
        root.Controls.Add(viewport, 0, 1);

        var categoryItems = new List<(string Name, List<JsonElement> Mods, int Columns)>();
        if (Try(data, "categories", out var categories) && categories.ValueKind == JsonValueKind.Array)
        {
            foreach (var category in categories.EnumerateArray())
            {
                if (!Try(category, "mods", out var mods) || mods.ValueKind != JsonValueKind.Array) continue;
                var items = mods.EnumerateArray().Select(item => item.Clone()).ToList();
                if (items.Count == 0) continue;
                categoryItems.Add((
                    TranslateCategory(String(category, "name") ?? "配件"),
                    items,
                    Math.Max(1, items.Select(item => Int(item, "column")).DefaultIfEmpty(1).Max())));
            }
        }

        if (categoryItems.Count == 0)
        {
            var empty = SectionLabel("暂无配件数据", Muted);
            empty.Location = new Point(12, 12);
            empty.AutoSize = true;
            viewport.Controls.Add(empty);
            return root;
        }

        const int margin = 10;
        const int tierWidth = 48;
        const int tileWidth = 108;
        const int tileHeight = 108;
        const int columnGap = 6;
        const int rowGap = 8;
        const int categoryGap = 12;
        const int headerHeight = 38;

        var maxTier = Math.Max(1, categoryItems.SelectMany(category => category.Mods).Select(mod => Int(mod, "tier")).DefaultIfEmpty(1).Max());
        var contentHeight = margin + headerHeight + maxTier * (tileHeight + rowGap) + margin;
        var canvas = new Panel { Location = Point.Empty, BackColor = WindowBack, Height = contentHeight };

        var corner = SectionLabel("等级", Muted, 9f);
        corner.SetBounds(margin, margin, tierWidth, headerHeight - 4);
        corner.TextAlign = ContentAlignment.MiddleCenter;
        canvas.Controls.Add(corner);

        for (var tier = 1; tier <= maxTier; tier++)
        {
            var tierLabel = SectionLabel(Roman(tier), Color.White, 11f);
            tierLabel.SetBounds(margin, margin + headerHeight + (tier - 1) * (tileHeight + rowGap), tierWidth, tileHeight);
            tierLabel.TextAlign = ContentAlignment.MiddleCenter;
            canvas.Controls.Add(tierLabel);
        }

        var categoryX = margin + tierWidth + columnGap;
        for (var categoryIndex = 0; categoryIndex < categoryItems.Count; categoryIndex++)
        {
            var category = categoryItems[categoryIndex];
            var categoryWidth = category.Columns * tileWidth + (category.Columns - 1) * columnGap;
            var header = SectionLabel(category.Name, Color.White, 10.5f);
            header.SetBounds(categoryX, margin, categoryWidth, headerHeight - 4);
            header.TextAlign = ContentAlignment.MiddleCenter;
            header.BackColor = CardBackAlt;
            canvas.Controls.Add(header);

            foreach (var mod in category.Mods.OrderBy(item => Int(item, "tier")).ThenBy(item => Int(item, "column")))
            {
                var rp = Number(mod, "research_rp") ?? "免费";
                var sl = Number(mod, "purchase_sl") ?? "免费";
                var modName = String(mod, "name") ?? "配件";
                var tier = Math.Max(1, Int(mod, "tier"));
                var column = Math.Max(1, Int(mod, "column"));
                var button = new Button
                {
                    Location = new Point(
                        categoryX + (column - 1) * (tileWidth + columnGap),
                        margin + headerHeight + (tier - 1) * (tileHeight + rowGap)),
                    Size = new Size(tileWidth, tileHeight),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = CardBack,
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4),
                    Font = new Font("Microsoft YaHei UI", 8f),
                    Text = $"{TranslateModName(modName, String(mod, "mod_id"))}\r\n等级 {Roman(tier)}\r\n研发 {rp}\r\n购买 {sl}"
                };
                button.FlatAppearance.BorderColor = Line;
                button.Click += (_, _) =>
                {
                    var selected = button.BackColor == CardBackAlt;
                    button.BackColor = selected ? CardBack : CardBackAlt;
                    button.FlatAppearance.BorderColor = selected ? Line : Accent;
                };
                canvas.Controls.Add(button);
            }

            categoryX += categoryWidth;
            if (categoryIndex < categoryItems.Count - 1)
            {
                var separator = new Label
                {
                    BackColor = Line,
                    Location = new Point(categoryX + categoryGap / 2, margin),
                    Size = new Size(1, contentHeight - margin * 2)
                };
                canvas.Controls.Add(separator);
                categoryX += categoryGap;
            }
        }

        canvas.Width = categoryX + margin;
        viewport.AutoScrollMinSize = canvas.Size;
        viewport.Controls.Add(canvas);
        return root;
    }

    private Control BuildBasic(JsonElement data)
    {
        var rows = data.ValueKind == JsonValueKind.Object
            ? data.EnumerateObject().Select(property => new DetailRow(property.Name, Display(property.Value), property.Name.Contains("官网") ? Color.FromArgb(103, 201, 232) : Value)).ToArray()
            : [new DetailRow("内容", Display(data), Value)];
        return BuildScrollableRows(rows, "载具基础资料");
    }

    private Control BuildScrollableRows(IEnumerable<DetailRow> rows, string footerText)
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = WindowBack, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = WindowBack, Padding = new Padding(10) };
        var table = CreateRows(rows);
        table.Dock = DockStyle.Top;
        scroll.Controls.Add(table);
        root.Controls.Add(scroll, 0, 0);
        var footer = SectionLabel(footerText, Muted);
        footer.Padding = new Padding(11, 7, 11, 7);
        footer.Dock = DockStyle.Fill;
        root.Controls.Add(footer, 0, 1);
        return root;
    }

    private Panel CreateRows(IEnumerable<DetailRow> rows)
    {
        var rowList = rows.ToArray();
        var table = new Panel
        {
            Height = Math.Max(1, rowList.Length) * 41,
            BackColor = CardBack
        };
        for (var index = 0; index < rowList.Length; index++)
        {
            var row = rowList[index];
            var rowPanel = new Panel
            {
                Height = 41,
                Location = new Point(0, index * 41),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = CardBack
            };
            var label = RowLabel(row.Label, Muted, ContentAlignment.MiddleLeft, 9f);
            var value = RowLabel(row.Value, row.Color, ContentAlignment.MiddleRight, row.FontSize);
            label.Dock = DockStyle.None;
            value.Dock = DockStyle.None;
            void LayoutRow()
            {
                var split = Math.Max(120, (int)(rowPanel.ClientSize.Width * 0.57));
                label.Bounds = new Rectangle(0, 0, split, 40);
                value.Bounds = new Rectangle(split + 1, 0, Math.Max(1, rowPanel.ClientSize.Width - split - 1), 40);
            }
            rowPanel.Resize += (_, _) => LayoutRow();
            rowPanel.Paint += (_, e) =>
            {
                using var pen = new Pen(Line);
                e.Graphics.DrawLine(pen, 0, rowPanel.Height - 1, rowPanel.Width, rowPanel.Height - 1);
                e.Graphics.DrawLine(pen, label.Right, 0, label.Right, rowPanel.Height);
            };
            rowPanel.Controls.Add(value);
            rowPanel.Controls.Add(label);
            table.Controls.Add(rowPanel);
            LayoutRow();
        }
        table.Resize += (_, _) =>
        {
            foreach (Control row in table.Controls) row.Width = table.ClientSize.Width;
        };
        return table;
    }

    private static Label RowLabel(string text, Color color, ContentAlignment alignment, float size)
        => new()
        {
            Dock = DockStyle.Fill,
            BackColor = CardBack,
            ForeColor = color,
            Font = new Font("Microsoft YaHei UI", size, alignment == ContentAlignment.MiddleRight ? FontStyle.Bold : FontStyle.Regular),
            Text = text,
            TextAlign = alignment,
            Padding = new Padding(9, 4, 9, 4),
            AutoEllipsis = true
        };

    private static Label SectionLabel(string text, Color color, float size = 9f)
        => new()
        {
            AutoSize = false,
            Height = 34,
            Width = 200,
            BackColor = WindowBack,
            ForeColor = color,
            Font = new Font("Microsoft YaHei UI", size, size > 10 ? FontStyle.Bold : FontStyle.Regular),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 2, 4, 2)
        };

    private static Label EmptyLabel(string text)
        => new() { Dock = DockStyle.Fill, ForeColor = Muted, BackColor = WindowBack, TextAlign = ContentAlignment.MiddleCenter, Text = text };

    private static DataGridView NewGrid()
    {
        var grid = new DataGridView
        {
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.None,
            BackgroundColor = CardBack,
            GridColor = Line,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeight = 30,
            EnableHeadersVisualStyles = false,
            ScrollBars = ScrollBars.Vertical
        };
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(45, 61, 72), ForeColor = Color.White, Alignment = DataGridViewContentAlignment.MiddleCenter };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = CardBack, ForeColor = Value, SelectionBackColor = CardBackAlt, SelectionForeColor = Color.White, Alignment = DataGridViewContentAlignment.MiddleCenter };
        return grid;
    }

    private string ModeValue(JsonElement root, string property)
    {
        if (!Try(root, property, out var value) || value.ValueKind != JsonValueKind.Object) return "无数据";
        var unit = String(value, "unit") ?? "";
        return value.TryGetProperty(_mode, out var selected) ? $"{Display(selected)} {unit}".Trim() : "无数据";
    }

    private string ModeRange(JsonElement root, string property)
    {
        if (!Try(root, property, out var value) || value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(_mode, out var selected)) return "无数据";
        var minimum = Number(selected, "minimum");
        var maximum = Number(selected, "maximum");
        var unit = String(value, "unit") ?? "";
        if (minimum is null || maximum is null) return "无数据";
        return minimum == maximum ? $"{minimum} {unit}".Trim() : $"{minimum}–{maximum} {unit}".Trim();
    }

    private static string PairValue(JsonElement root, string property, string first, string second)
    {
        if (!Try(root, property, out var value) || value.ValueKind != JsonValueKind.Object) return "无数据";
        var left = Number(value, first);
        var right = Number(value, second);
        var unit = String(value, "unit") ?? "";
        if (left is null || right is null) return "无数据";
        return left == right ? $"{left} {unit}".Trim() : $"{left}→{right} {unit}".Trim();
    }

    private static string GuidanceValue(JsonElement root)
    {
        if (!Try(root, "vertical_guidance", out var value) || value.ValueKind != JsonValueKind.Object) return "无数据";
        var minimum = Number(value, "minimum");
        var maximum = Number(value, "maximum");
        var unit = String(value, "unit") ?? "";
        if (minimum is null || maximum is null) return "无数据";
        return $"{minimum} / {(maximum.StartsWith('-') ? maximum : "+" + maximum)}{unit}";
    }

    private static string TranslateCharacteristic(string value) => value switch
    {
        "Caliber" => "口径", "Projectile Mass" => "弹体重量", "Fuze Delay" => "引信延迟", "Fuze Sensitivity" => "引信灵敏度",
        "Guidance" => "制导方式", "Band" => "雷达波段", "Shoot down" => "攻击范围", "Aspect" => "攻击范围",
        "Lock range" => "锁定距离", "Lock range in rear-aspect" => "尾追锁定距离", "Lock range in all-aspect" => "全向锁定距离",
        "IRCCM" => "抗红外干扰", "Launch range" => "发射距离", "Firing range" => "发射距离", "Maximum overload" => "最大过载",
        "Maximum speed" => "最大速度", "Missile guidance time" => "制导时间", "Explosive Type" => "炸药类型",
        "Explosive Mass" => "炸药重量", "TNT equivalent" => "TNT 当量", "Ammunition" => "弹药量", _ => value
    };

    private static string TranslateValue(string value) => value switch
    {
        "Yes" => "有", "No" => "无", "All-aspects" => "全向", "Front-aspect" => "迎头", "Rear-aspect" => "尾追", _ => value
    };

    private static string TranslateCategory(string value)
        => ModDictionary.Value.Categories.TryGetValue(value, out var translated) ? translated : value;

    private static string TranslateModName(string name, string? id)
    {
        if (ModDictionary.Value.Mods.TryGetValue(name, out var translated)) return translated;
        if (id is not null && ModDictionary.Value.Mods.TryGetValue(id, out translated)) return translated;
        return name;
    }

    private static ModDictionaryData LoadModDictionary()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "mod-names-zh.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            static Dictionary<string, string> ReadMap(JsonElement root, string property)
            {
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                if (!root.TryGetProperty(property, out var map) || map.ValueKind != JsonValueKind.Object) return result;
                foreach (var item in map.EnumerateObject())
                    if (item.Value.ValueKind == JsonValueKind.String) result[item.Name] = item.Value.GetString() ?? item.Name;
                return result;
            }
            return new ModDictionaryData(ReadMap(document.RootElement, "categories"), ReadMap(document.RootElement, "mods"));
        }
        catch
        {
            return new ModDictionaryData(new Dictionary<string, string>(), new Dictionary<string, string>());
        }
    }

    private static string ModeName(string mode) => mode switch { "ab" => "街机", "sb" => "全真", _ => "历史" };
    private static string Roman(int number) => number switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => number.ToString() };
    private static int Int(JsonElement element, string property) => int.TryParse(Number(element, property), out var value) ? value : 0;

    private static IEnumerable<int> IntArray(JsonElement element, string property)
    {
        if (!Try(element, property, out var array) || array.ValueKind != JsonValueKind.Array) return [];
        return array.EnumerateArray().Where(value => value.TryGetInt32(out _)).Select(value => value.GetInt32()).ToArray();
    }

    private static bool Try(JsonElement element, string property, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out value)) return true;
        value = default;
        return false;
    }

    private static string? String(JsonElement element, string property)
        => Try(element, property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? Number(JsonElement element, string property)
        => Try(element, property, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetRawText() : null;

    private static bool Boolean(JsonElement element, string property)
        => Try(element, property, out var value) && value.ValueKind == JsonValueKind.True;

    private static string Display(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "—",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "✓",
        JsonValueKind.False => "无",
        JsonValueKind.Null => "无",
        _ => value.GetRawText()
    };

    private sealed record DetailRow(string Label, string Value, Color Color, float FontSize = 9f);
    private sealed record ModDictionaryData(Dictionary<string, string> Categories, Dictionary<string, string> Mods);
    private sealed record WeaponItem(int Index, JsonElement Weapon)
    {
        public override string ToString()
        {
            var name = String(Weapon, "name") ?? "挂载";
            var weight = String(Weapon, "weight") ?? "无重量数据";
            var slots = string.Join("/", IntArray(Weapon, "available_slots"));
            return $"{name}    {weight}{(slots.Length > 0 ? $" · 挂点 {slots}" : "")}";
        }
    }
}
