using System.Drawing.Drawing2D;

namespace WarThunderTechTree.Native;

internal sealed class VehicleEventArgs(VehicleRecord vehicle) : EventArgs
{
    public VehicleRecord Vehicle { get; } = vehicle;
}

internal sealed class TechTreeCanvas : ScrollableControl
{
    private sealed record CardLayout(VehicleRecord Vehicle, Rectangle Bounds, string? GroupId, int GroupCount);
    private sealed record RankLayout(string Name, int Number, Rectangle Bounds, int Required);

    private readonly List<CardLayout> _cards = [];
    private readonly List<RankLayout> _ranks = [];
    private readonly Dictionary<string, Rectangle> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _expandedGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly ImageCache _images = new();
    private readonly ToolTip _toolTip = new() { InitialDelay = 350, ReshowDelay = 100, AutoPopDelay = 8000 };
    private TreeData? _tree;
    private string? _lastTooltipId;
    private float ScaleFactor => DeviceDpi / 96f;

    public string Mode { get; set; } = "rb";
    public bool ChineseNames { get; set; } = true;
    public IReadOnlyCollection<string> SelectedIds => _selected;

    public event EventHandler? SelectionChanged;
    public event EventHandler<VehicleEventArgs>? VehicleContextRequested;
    public event EventHandler<VehicleEventArgs>? VehicleActivated;

    public TechTreeCanvas()
    {
        AutoScroll = true;
        BackColor = Color.FromArgb(11, 17, 22);
        ForeColor = Color.White;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    public void SetTree(TreeData tree)
    {
        _tree = tree;
        _selected.Clear();
        _expandedGroups.Clear();
        BuildLayout();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection()
    {
        if (_selected.Count == 0) return;
        _selected.Clear();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetSelection(IEnumerable<string> unitIds)
    {
        _selected.Clear();
        foreach (var id in unitIds) _selected.Add(id);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        BuildLayout();
    }

    private int S(float value) => Math.Max(1, (int)Math.Round(value * ScaleFactor));

    private void BuildLayout()
    {
        _cards.Clear();
        _ranks.Clear();
        _nodes.Clear();
        if (_tree is null)
        {
            AutoScrollMinSize = Size.Empty;
            return;
        }

        var margin = S(24);
        var header = S(54);
        var cardWidth = S(184);
        var cardHeight = S(72);
        var xGap = S(14);
        var yGap = S(12);
        var divider = S(34);
        var researchColumns = _tree.VehicleType == "helicopter" ? 3 : 5;
        var premiumStart = margin + researchColumns * (cardWidth + xGap) + divider;
        var totalWidth = premiumStart + 2 * (cardWidth + xGap) + margin;
        var currentY = margin;

        foreach (var rankGroup in _tree.Vehicles.GroupBy(vehicle => vehicle.RankNumber).OrderBy(group => group.Key))
        {
            var slots = rankGroup
                .GroupBy(vehicle => string.IsNullOrWhiteSpace(vehicle.GroupId) ? $"unit:{vehicle.UnitId}" : $"group:{vehicle.GroupId}")
                .Select(group => group.OrderBy(vehicle => vehicle.GroupPosition ?? int.MaxValue).ThenBy(vehicle => vehicle.TreeOrder).ToList())
                .ToList();
            var effectiveRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var lane in slots.GroupBy(members =>
                         (members[0].TreeSection, Column: Math.Max(1, members[0].TreeColumn ?? 1))))
            {
                var expandedRowsAbove = 0;
                foreach (var members in lane
                             .OrderBy(items => Math.Max(1, items[0].TreeRow ?? 1))
                             .ThenBy(items => items[0].TreeOrder))
                {
                    var first = members[0];
                    effectiveRows[first.UnitId] = Math.Max(1, first.TreeRow ?? 1) + expandedRowsAbove;
                    if (first.GroupId is not null && _expandedGroups.Contains(first.GroupId))
                        expandedRowsAbove += members.Count - 1;
                }
            }
            var maxBottom = 1;
            foreach (var members in slots)
            {
                var first = members[0];
                var row = effectiveRows[first.UnitId];
                var expanded = first.GroupId is not null && _expandedGroups.Contains(first.GroupId);
                maxBottom = Math.Max(maxBottom, row + (expanded ? members.Count - 1 : 0));
            }
            var bandHeight = header + maxBottom * (cardHeight + yGap) + S(22);
            var rankRect = new Rectangle(margin, currentY, totalWidth - margin * 2, bandHeight);
            _ranks.Add(new RankLayout(rankGroup.First().Rank, rankGroup.Key, rankRect, ResearchPlanner.UnlockRequirement(_tree.VehicleType, rankGroup.Key)));

            foreach (var members in slots)
            {
                var first = members[0];
                var column = Math.Max(1, first.TreeColumn ?? 1);
                var row = effectiveRows[first.UnitId];
                var x = first.TreeSection == "premium"
                    ? premiumStart + (column - 1) * (cardWidth + xGap)
                    : margin + (column - 1) * (cardWidth + xGap);
                var y = currentY + header + (row - 1) * (cardHeight + yGap);
                var expanded = first.GroupId is not null && _expandedGroups.Contains(first.GroupId);
                var shown = expanded ? members : [first];
                for (var index = 0; index < shown.Count; index++)
                {
                    var vehicle = shown[index];
                    var rect = new Rectangle(x, y + index * (cardHeight + yGap), cardWidth, cardHeight);
                    _cards.Add(new CardLayout(vehicle, rect, first.GroupId, members.Count));
                    _nodes[vehicle.UnitId] = rect;
                }
                var slotRect = new Rectangle(x, y, cardWidth, expanded ? members.Count * (cardHeight + yGap) - yGap : cardHeight);
                foreach (var member in members) _nodes.TryAdd(member.UnitId, slotRect);
                if (first.GroupId is not null) _nodes[first.GroupId] = slotRect;
            }
            currentY += bandHeight + S(18);
        }
        AutoScrollMinSize = new Size(totalWidth, currentY + margin);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_tree is null) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
        var visible = new Rectangle(-AutoScrollPosition.X, -AutoScrollPosition.Y, ClientSize.Width, ClientSize.Height);

        using var bandBrush = new SolidBrush(Color.FromArgb(16, 25, 32));
        using var borderPen = new Pen(Color.FromArgb(51, 69, 80));
        using var separatorPen = new Pen(Color.FromArgb(64, 81, 92), S(2));
        using var titleBrush = new SolidBrush(Color.FromArgb(220, 230, 235));
        using var titleFont = new Font("Segoe UI Semibold", 12f * ScaleFactor, FontStyle.Bold, GraphicsUnit.Pixel);

        foreach (var rank in _ranks)
        {
            if (!rank.Bounds.IntersectsWith(visible)) continue;
            e.Graphics.FillRectangle(bandBrush, rank.Bounds);
            e.Graphics.DrawRectangle(borderPen, rank.Bounds);
            e.Graphics.DrawString($"等级 {rank.Name}", titleFont, titleBrush, rank.Bounds.X + S(12), rank.Bounds.Y + S(12));
            var isTop = rank.Number == _ranks.Max(item => item.Number);
            if (!isTop)
                e.Graphics.DrawString($"解锁下一级需要 {rank.Required} 辆", Font, Brushes.Gray, rank.Bounds.X + S(105), rank.Bounds.Y + S(15));
        }

        foreach (var card in _cards)
        {
            var requirement = card.Vehicle.RequirementId;
            if (requirement is null || !_nodes.TryGetValue(requirement, out var source)) continue;
            var start = new Point(source.Left + source.Width / 2, source.Bottom);
            var end = new Point(card.Bounds.Left + card.Bounds.Width / 2, card.Bounds.Top);
            if (!visible.Contains(start) && !visible.Contains(end)) continue;
            e.Graphics.DrawLine(separatorPen, start, new Point(start.X, (start.Y + end.Y) / 2));
            e.Graphics.DrawLine(separatorPen, new Point(start.X, (start.Y + end.Y) / 2), new Point(end.X, (start.Y + end.Y) / 2));
            e.Graphics.DrawLine(separatorPen, new Point(end.X, (start.Y + end.Y) / 2), end);
        }

        foreach (var card in _cards)
        {
            if (card.Bounds.IntersectsWith(visible)) DrawCard(e.Graphics, card);
        }
    }

    private void DrawCard(Graphics graphics, CardLayout card)
    {
        var vehicle = card.Vehicle;
        var selected = _selected.Contains(vehicle.UnitId);
        var premium = vehicle.TreeSection == "premium";
        var fill = selected
            ? Color.FromArgb(50, 87, 108)
            : premium ? Color.FromArgb(48, 43, 25) : Color.FromArgb(32, 49, 59);
        var border = selected ? Color.FromArgb(125, 194, 232) : premium ? Color.FromArgb(170, 132, 46) : Color.FromArgb(65, 88, 101);
        using var fillBrush = new SolidBrush(fill);
        using var borderPen = new Pen(border, selected ? S(3) : S(1));
        graphics.FillRectangle(fillBrush, card.Bounds);
        graphics.DrawRectangle(borderPen, card.Bounds);

        var imageRect = new Rectangle(card.Bounds.X + S(5), card.Bounds.Y + S(7), S(69), card.Bounds.Height - S(14));
        var bitmap = _images.GetOrQueue(vehicle.ImageUrl, () =>
        {
            if (!IsDisposed && IsHandleCreated) BeginInvoke(Invalidate);
        });
        if (bitmap is not null)
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(bitmap, imageRect);
        }
        else
        {
            using var placeholder = new SolidBrush(Color.FromArgb(24, 35, 42));
            graphics.FillRectangle(placeholder, imageRect);
        }

        var textX = imageRect.Right + S(7);
        var nameRect = new Rectangle(textX, card.Bounds.Y + S(7), card.Bounds.Right - textX - S(7), S(38));
        using var nameFont = new Font("Segoe UI Semibold", 12f * ScaleFactor, FontStyle.Bold, GraphicsUnit.Pixel);
        using var nameFormat = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.LineLimit
        };
        using var nameBrush = new SolidBrush(Color.White);
        graphics.DrawString(vehicle.DisplayName(ChineseNames), nameFont, nameBrush, nameRect, nameFormat);
        var rating = $"{Mode.ToUpperInvariant()}  {vehicle.Rating(Mode)}";
        using var ratingBrush = new SolidBrush(Color.FromArgb(214, 224, 229));
        graphics.DrawString(rating, Font, ratingBrush, new PointF(textX, card.Bounds.Bottom - S(23)));

        if (card.GroupCount > 1)
        {
            var expanded = card.GroupId is not null && _expandedGroups.Contains(card.GroupId);
            var groupText = expanded ? "▲" : $"▼ {card.GroupCount}";
            using var groupBrush = new SolidBrush(Color.FromArgb(236, 197, 96));
            using var groupFormat = new StringFormat { Alignment = StringAlignment.Far };
            graphics.DrawString(groupText, Font, groupBrush,
                new RectangleF(card.Bounds.Right - S(38), card.Bounds.Y + S(4), S(34), S(20)), groupFormat);
        }
    }

    private Point ContentPoint(Point clientPoint) => new(clientPoint.X - AutoScrollPosition.X, clientPoint.Y - AutoScrollPosition.Y);

    private CardLayout? HitTest(Point clientPoint)
    {
        var point = ContentPoint(clientPoint);
        for (var index = _cards.Count - 1; index >= 0; index--)
            if (_cards[index].Bounds.Contains(point)) return _cards[index];
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var card = HitTest(e.Location);
        var id = card?.Vehicle.UnitId;
        if (id == _lastTooltipId) return;
        _lastTooltipId = id;
        _toolTip.SetToolTip(this, card is null ? null : $"{card.Vehicle.NameEnglish}\n研发：{card.Vehicle.ResearchDisplay ?? "—"}\n购买：{card.Vehicle.PurchaseDisplay ?? "—"}\n右键查看详细资料");
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var card = HitTest(e.Location);
        if (card is null) return;
        var content = ContentPoint(e.Location);
        var groupExpanded = card.GroupId is not null && _expandedGroups.Contains(card.GroupId);
        var groupButtonClicked = content.X >= card.Bounds.Right - S(52);
        if (e.Button == MouseButtons.Left && card.GroupCount > 1 && card.GroupId is not null &&
            (!groupExpanded || groupButtonClicked))
        {
            var scrollOffset = new Point(-AutoScrollPosition.X, -AutoScrollPosition.Y);
            if (!_expandedGroups.Add(card.GroupId)) _expandedGroups.Remove(card.GroupId);
            BuildLayout();
            AutoScrollPosition = scrollOffset;
            Invalidate();
            return;
        }
        if (e.Button == MouseButtons.Left)
        {
            if (!_selected.Add(card.Vehicle.UnitId)) _selected.Remove(card.Vehicle.UnitId);
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (e.Button == MouseButtons.Right)
        {
            VehicleContextRequested?.Invoke(this, new VehicleEventArgs(card.Vehicle));
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var card = HitTest(e.Location);
        if (card is not null) VehicleActivated?.Invoke(this, new VehicleEventArgs(card.Vehicle));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            _images.Dispose();
        }
        base.Dispose(disposing);
    }
}
