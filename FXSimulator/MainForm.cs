using System.Drawing.Drawing2D;
using System.Text.Json;

namespace FXSimulator;

public sealed class MainForm : Form
{
    readonly Color bg = Color.FromArgb(18, 19, 29);
    readonly Color panel = Color.FromArgb(29, 30, 44);
    readonly Color panel2 = Color.FromArgb(37, 38, 54);
    readonly Color pink = Color.FromArgb(255, 83, 151);
    readonly Color text = Color.FromArgb(239, 239, 247);
    readonly Color muted = Color.FromArgb(155, 158, 180);
    readonly Color green = Color.FromArgb(69, 220, 156);
    readonly Color red = Color.FromArgb(255, 96, 117);

    AccountState state = new();
    readonly System.Windows.Forms.Timer marketTimer = new() { Interval = 900 };
    readonly System.Windows.Forms.Timer saveTimer = new() { Interval = 5000 };
    readonly Random rng = new();
    readonly Dictionary<string, double> previousPrices = new();
    readonly List<Candle> candles = new();
    int ticksInCandle = 0;

    readonly record struct Candle(double Open, double High, double Low, double Close);
    readonly Label balanceValue = new();
    readonly Label equityValue = new();
    readonly Label pnlValue = new();
    readonly Label marginValue = new();
    readonly Label marginLevelValue = new();
    readonly Label pairPrice = new();
    readonly Label pairChange = new();
    readonly Label selectedPairValue = new();
    readonly Label tradePriceValue = new();
    readonly Label statusLabel = new();
    readonly ComboBox pairBox = new();
    readonly ComboBox leverageBox = new();
    readonly NumericUpDown lotsBox = new();
    readonly BufferedPanel chart = new();
    readonly DataGridView positionsGrid = new();
    readonly DataGridView historyGrid = new();
    readonly Label bidValue = new();
    readonly Label askValue = new();
    bool loading = true;
    bool forcedLiquidationNotice = false;

    string DataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FXSimulator", "account.json");

    public MainForm()
    {
        Text = "FX Simulator V2.0 | 虚拟外汇交易终端";
        MinimumSize = new Size(1040, 720);
        Size = new Size(1440, 900);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = bg;
        ForeColor = text;
        Font = new Font("Microsoft YaHei UI", 9F);
        DoubleBuffered = true;

        LoadState();
        BuildUi();
        UpdateAll();
        loading = false;

        marketTimer.Tick += (_, _) => TickMarket();
        saveTimer.Tick += (_, _) => SaveState();
        marketTimer.Start();
        saveTimer.Start();
        FormClosing += (_, _) => { SaveState(); marketTimer.Stop(); saveTimer.Stop(); };
    }

    void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = bg, Padding = new Padding(16) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        var title = new Label { Text = "FX  /  SIMULATOR", Font = new Font("Microsoft YaHei UI", 19, FontStyle.Bold), ForeColor = pink, AutoSize = true, Location = new Point(4, 3) };
        var subtitle = new Label { Text = "虚拟市场 · 自由交易 · 风险与保证金模拟", ForeColor = muted, AutoSize = true, Location = new Point(7, 36) };
        var version = new Label { Text = "V2.0  •  OFFLINE MODE", ForeColor = muted, AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        header.Controls.AddRange(new Control[] { title, subtitle, version });
        header.Resize += (_, _) => version.Location = new Point(header.ClientSize.Width - version.Width - 8, 18);
        root.Controls.Add(header, 0, 0);

        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, BackColor = bg, Padding = new Padding(0, 4, 0, 8) };
        for (int i = 0; i < 5; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        root.Controls.Add(metrics, 0, 1);
        AddMetric(metrics, 0, "账户余额", balanceValue, "模拟账户初始资金");
        AddMetric(metrics, 1, "账户净值", equityValue, "余额 + 浮动盈亏");
        AddMetric(metrics, 2, "浮动盈亏", pnlValue, "未平仓交易");
        AddMetric(metrics, 3, "已用保证金", marginValue, "当前持仓占用");
        AddMetric(metrics, 4, "保证金率", marginLevelValue, "低于 50% 触发强平");

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = bg };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        root.Controls.Add(body, 0, 2);

        // Market card uses a dedicated toolbar row so the currency selector is always visible.
        var chartCard = new Panel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(12), Margin = new Padding(0, 0, 10, 10) };
        var marketLayout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = panel, ColumnCount = 1, RowCount = 3, Padding = new Padding(0) };
        marketLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        marketLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        marketLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var marketHeading = new Label { Text = "MARKET  /  虚拟行情与 K 线", Dock = DockStyle.Fill, ForeColor = pink, Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        var marketToolbar = new Panel { Dock = DockStyle.Fill, BackColor = panel2, Padding = new Padding(10, 6, 10, 6), Margin = new Padding(0, 0, 0, 6) };
        var pairCaption = new Label { Text = "交易货币对", ForeColor = muted, AutoSize = true, Location = new Point(10, 12) };
        pairBox.DropDownStyle = ComboBoxStyle.DropDownList;
        pairBox.Items.AddRange(new object[] { "USD/JPY", "EUR/JPY", "GBP/JPY" });
        pairBox.SelectedItem = state.SelectedPair;
        pairBox.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
        pairBox.Size = new Size(145, 30);
        pairBox.Location = new Point(95, 6);
        StyleCombo(pairBox);
        pairBox.Dock = DockStyle.None;
        pairBox.Size = new Size(145, 30);
        pairBox.Location = new Point(95, 6);
        pairBox.SelectedIndexChanged += (_, _) => { if (!loading && pairBox.SelectedItem is string pair) { state.SelectedPair = pair; selectedPairValue.Text = pair; ResetChart(); UpdateAll(); SaveState(); } };
        pairPrice.ForeColor = text; pairPrice.Font = new Font("Consolas", 17, FontStyle.Bold); pairPrice.AutoSize = true;
        pairChange.ForeColor = green; pairChange.Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold); pairChange.AutoSize = true;
        var quoteGroup = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = panel2, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(270, 6), Height = 32, Padding = new Padding(4, 0, 4, 0) };
        quoteGroup.Controls.AddRange(new Control[] { pairPrice, pairChange });
        marketToolbar.Controls.AddRange(new Control[] { pairCaption, pairBox, quoteGroup });
        marketToolbar.Resize += (_, _) => quoteGroup.Location = new Point(Math.Max(260, marketToolbar.ClientSize.Width - quoteGroup.Width - 8), 6);
        chart.Dock = DockStyle.Fill;
        chart.BackColor = panel;
        chart.Paint += DrawChart;
        marketLayout.Controls.Add(marketHeading, 0, 0);
        marketLayout.Controls.Add(marketToolbar, 0, 1);
        marketLayout.Controls.Add(chart, 0, 2);
        chartCard.Controls.Add(marketLayout);
        body.Controls.Add(chartCard, 0, 0);

        var tradeCard = MakeCard("ORDER PANEL  /  下单");
        BuildTradePanel(tradeCard);
        body.Controls.Add(tradeCard, 1, 0);

        var posCard = MakeCard("OPEN POSITIONS  /  当前持仓");
        BuildPositionsGrid();
        posCard.Controls.Add(positionsGrid);
        body.Controls.Add(posCard, 0, 1);

        var historyCard = MakeCard("TRADE LOG  /  最近记录");
        BuildHistoryGrid();
        historyCard.Controls.Add(historyGrid);
        body.Controls.Add(historyCard, 1, 1);

        statusLabel.Text = "● 离线虚拟行情运行中  |  账户自动保存已启用";
        statusLabel.ForeColor = muted;
        statusLabel.Dock = DockStyle.Bottom;
        statusLabel.Height = 24;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(statusLabel, 0, 3);
    }

    void AddMetric(TableLayoutPanel table, int col, string caption, Label value, string hint)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = panel, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(12) };
        var cap = new Label { Text = caption, ForeColor = muted, AutoSize = true, Location = new Point(12, 10) };
        value.Text = "—";
        value.ForeColor = text;
        value.Font = new Font("Microsoft YaHei UI", 15, FontStyle.Bold);
        value.AutoSize = true;
        value.Location = new Point(12, 32);
        var small = new Label { Text = hint, ForeColor = muted, Font = new Font("Microsoft YaHei UI", 8), AutoSize = true, Location = new Point(12, 69) };
        card.Controls.AddRange(new Control[] { cap, value, small });
        table.Controls.Add(card, col, 0);
    }

    Panel MakeCard(string heading)
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(12), Margin = new Padding(0, 0, 10, 10) };
        var label = new Label { Text = heading, Dock = DockStyle.Top, Height = 28, ForeColor = pink, Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold) };
        card.Controls.Add(label);
        return card;
    }

    void BuildTradePanel(Panel card)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            BackColor = panel,
            Padding = new Padding(2, 4, 2, 2),
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 61));
        // Reserve enough vertical space for the action buttons so their labels stay readable.
        // Five input rows use 8% each, four action rows 13% each, and the note row 8%.
        for (int i = 0; i < 10; i++)
        {
            float height = i <= 4 ? 8f : i <= 8 ? 13f : 8f;
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, height));
        }
        card.Controls.Add(layout);

        AddTradeLabel(layout, 0, "当前交易货币对");
        selectedPairValue.Text = state.SelectedPair;
        selectedPairValue.ForeColor = pink;
        selectedPairValue.Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold);
        selectedPairValue.Dock = DockStyle.Fill;
        selectedPairValue.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(selectedPairValue, 1, 0);

        AddTradeLabel(layout, 1, "当前报价");
        var quotePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = panel, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        tradePriceValue.Text = "";
        tradePriceValue.ForeColor = text;
        tradePriceValue.Font = new Font("Consolas", 14, FontStyle.Bold);
        tradePriceValue.AutoSize = true;
        quotePanel.Controls.Add(tradePriceValue);
        layout.Controls.Add(quotePanel, 1, 1);

        AddTradeLabel(layout, 2, "买入价 / 卖出价");
        var bidAsk = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        bidValue.ForeColor = green; bidValue.AutoSize = true;
        askValue.ForeColor = red; askValue.AutoSize = true;
        bidAsk.Controls.AddRange(new Control[] { bidValue, askValue });
        layout.Controls.Add(bidAsk, 1, 2);

        AddTradeLabel(layout, 3, "交易手数");
        lotsBox.DecimalPlaces = 2; lotsBox.Increment = 0.01M; lotsBox.Minimum = 0.01M; lotsBox.Maximum = 10M; lotsBox.Value = (decimal)Math.Clamp(state.Lots, 0.01, 10);
        lotsBox.Dock = DockStyle.Fill; lotsBox.BackColor = panel2; lotsBox.ForeColor = text; lotsBox.BorderStyle = BorderStyle.FixedSingle;
        lotsBox.ValueChanged += (_, _) => { if (!loading) { state.Lots = (double)lotsBox.Value; SaveState(); } };
        layout.Controls.Add(lotsBox, 1, 3);

        AddTradeLabel(layout, 4, "杠杆");
        leverageBox.DropDownStyle = ComboBoxStyle.DropDownList;
        leverageBox.Items.AddRange(new object[] { "25x", "50x", "100x", "200x", "500x" });
        leverageBox.SelectedItem = $"{state.Leverage:0}x";
        if (leverageBox.SelectedIndex < 0) leverageBox.SelectedItem = "100x";
        StyleCombo(leverageBox);
        leverageBox.SelectedIndexChanged += (_, _) => { if (!loading && leverageBox.SelectedItem is string s && double.TryParse(s.TrimEnd('x'), out var v)) { state.Leverage = v; SaveState(); UpdateAll(); } };
        layout.Controls.Add(leverageBox, 1, 4);

        var buy = MakeButton("买入 / 做多  ▲", Color.FromArgb(20, 160, 105), Color.FromArgb(34, 190, 128));
        buy.Click += (_, _) => OpenPosition("BUY");
        layout.Controls.Add(buy, 0, 5); layout.SetColumnSpan(buy, 2);
        var sell = MakeButton("卖出 / 做空  ▼", Color.FromArgb(205, 48, 70), Color.FromArgb(232, 65, 88));
        sell.Click += (_, _) => OpenPosition("SELL");
        layout.Controls.Add(sell, 0, 6); layout.SetColumnSpan(sell, 2);
        var close = MakeButton("平掉所选持仓", pink, Color.FromArgb(255, 112, 169));
        close.Click += (_, _) => CloseSelectedPosition();
        layout.Controls.Add(close, 0, 7); layout.SetColumnSpan(close, 2);
        var closeAll = MakeButton("全部平仓", Color.FromArgb(75, 77, 101), Color.FromArgb(95, 98, 128));
        closeAll.Click += (_, _) => { foreach (var p in state.Positions.ToList()) ClosePosition(p, "手动平仓"); };
        layout.Controls.Add(closeAll, 0, 8); layout.SetColumnSpan(closeAll, 2);

        var note = new Label { Text = "模拟点差：0.02 JPY  ·  强平线：保证金率 50%", ForeColor = muted, Font = new Font("Microsoft YaHei UI", 8), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        layout.Controls.Add(note, 0, 9); layout.SetColumnSpan(note, 2);
    }

    void AddTradeLabel(TableLayoutPanel layout, int row, string value)
    {
        var l = new Label { Text = value, ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        layout.Controls.Add(l, 0, row);
    }

    void StyleCombo(ComboBox box)
    {
        box.Dock = DockStyle.Fill;
        box.BackColor = panel2;
        box.ForeColor = text;
        box.FlatStyle = FlatStyle.Flat;
    }

    Button MakeButton(string label, Color color, Color hoverColor) => new()
    {
        Text = label,
        Dock = DockStyle.Fill,
        FlatStyle = FlatStyle.Flat,
        BackColor = color,
        ForeColor = Color.White,
        Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleCenter,
        AutoEllipsis = false,
        MinimumSize = new Size(0, 32),
        Margin = new Padding(2, 2, 2, 2),
        Cursor = Cursors.Hand,
        UseVisualStyleBackColor = false,
        FlatAppearance =
        {
            BorderSize = 1,
            BorderColor = Color.FromArgb(220, 230, 240),
            MouseOverBackColor = hoverColor,
            MouseDownBackColor = Color.FromArgb(
                Math.Max(0, color.R - 25),
                Math.Max(0, color.G - 25),
                Math.Max(0, color.B - 25))
        }
    };

    void BuildPositionsGrid()
    {
        positionsGrid.Dock = DockStyle.Fill;
        StyleGrid(positionsGrid);
        positionsGrid.AutoGenerateColumns = false;
        positionsGrid.Columns.Add(TextCol("pair", "货币对", 80));
        positionsGrid.Columns.Add(TextCol("side", "方向", 55));
        positionsGrid.Columns.Add(TextCol("lots", "手数", 55));
        positionsGrid.Columns.Add(TextCol("entry", "开仓价", 85));
        positionsGrid.Columns.Add(TextCol("current", "现价", 85));
        positionsGrid.Columns.Add(TextCol("pnl", "浮盈亏", 90));
        positionsGrid.Columns.Add(TextCol("margin", "保证金", 85));
    }

    void BuildHistoryGrid()
    {
        historyGrid.Dock = DockStyle.Fill;
        StyleGrid(historyGrid);
        historyGrid.AutoGenerateColumns = false;
        historyGrid.Columns.Add(TextCol("time", "时间", 100));
        historyGrid.Columns.Add(TextCol("pair", "货币对", 72));
        historyGrid.Columns.Add(TextCol("action", "操作", 78));
        historyGrid.Columns.Add(TextCol("lots", "手数", 50));
        historyGrid.Columns.Add(TextCol("profit", "盈亏", 75));
    }

    DataGridViewTextBoxColumn TextCol(string name, string header, int width) => new()
    {
        Name = name, HeaderText = header, Width = width, DataPropertyName = name,
        SortMode = DataGridViewColumnSortMode.NotSortable
    };

    void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = panel;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Color.FromArgb(55, 56, 75);
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = panel2;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = pink;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 8, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = panel;
        grid.DefaultCellStyle.ForeColor = text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(76, 48, 75);
        grid.DefaultCellStyle.SelectionForeColor = text;
        grid.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 8);
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowTemplate.Height = 26;
        grid.ColumnHeadersHeight = 30;
        grid.ScrollBars = ScrollBars.Both;
    }

    void LoadState()
    {
        try
        {
            if (File.Exists(DataPath))
            {
                var loaded = JsonSerializer.Deserialize<AccountState>(File.ReadAllText(DataPath));
                if (loaded is not null) state = loaded;
            }
        }
        catch { state = new AccountState(); }
        foreach (var pair in new[] { "USD/JPY", "EUR/JPY", "GBP/JPY" })
        {
            if (!state.Prices.ContainsKey(pair)) state.Prices[pair] = pair == "USD/JPY" ? 149.5 : pair == "EUR/JPY" ? 162.2 : 188.1;
            previousPrices[pair] = state.Prices[pair];
        }
        state.Positions ??= new();
        state.History ??= new();
        if (!new[] { "USD/JPY", "EUR/JPY", "GBP/JPY" }.Contains(state.SelectedPair)) state.SelectedPair = "USD/JPY";
        ResetChart();
    }

    void SaveState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
            File.WriteAllText(DataPath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { statusLabel.Text = "自动保存失败：" + ex.Message; }
    }

    void ResetChart()
    {
        candles.Clear();
        ticksInCandle = 0;
        double price = CurrentPrice(state.SelectedPair);
        // Seed a compact, synthetic history around the current virtual quote.
        double cursor = price;
        for (int i = 0; i < 48; i++)
        {
            double open = cursor;
            double close = i == 47 ? price : Math.Max(0.01, open + (rng.NextDouble() - 0.5) * 0.035);
            double high = Math.Max(open, close) + rng.NextDouble() * 0.012;
            double low = Math.Max(0.01, Math.Min(open, close) - rng.NextDouble() * 0.012);
            candles.Add(new Candle(open, high, low, close));
            cursor = close;
        }
        if (chart is not null) chart.Invalidate();
    }

    void TickMarket()
    {
        foreach (var pair in state.Prices.Keys.ToArray())
        {
            previousPrices[pair] = state.Prices[pair];
            double volatility = pair == "GBP/JPY" ? 0.055 : pair == "EUR/JPY" ? 0.040 : 0.035;
            double shock = rng.NextDouble() < 0.025 ? (rng.NextDouble() - 0.5) * volatility * 5 : 0;
            double move = (rng.NextDouble() - 0.5) * volatility + shock;
            state.Prices[pair] = Math.Max(0.01, state.Prices[pair] + move);
        }
        double close = CurrentPrice(state.SelectedPair);
        if (candles.Count == 0) candles.Add(new Candle(close, close, close, close));
        var last = candles[^1];
        candles[^1] = last with { High = Math.Max(last.High, close), Low = Math.Min(last.Low, close), Close = close };
        ticksInCandle++;
        if (ticksInCandle >= 5)
        {
            candles.Add(new Candle(close, close, close, close));
            if (candles.Count > 64) candles.RemoveAt(0);
            ticksInCandle = 0;
        }
        UpdateAll();
        CheckMarginCall();
    }

    double CurrentPrice(string pair) => state.Prices.TryGetValue(pair, out var p) ? p : 149.5;
    double Profit(TradePosition p)
    {
        double delta = CurrentPrice(p.Pair) - p.Entry;
        if (p.Side == "SELL") delta = -delta;
        return delta * p.Lots * 10000;
    }
    double UsedMargin(TradePosition p) => CurrentPrice(p.Pair) * p.Lots * 10000 / Math.Max(1, p.Leverage);
    double FloatingPnl() => state.Positions.Sum(Profit);
    double TotalMargin() => state.Positions.Sum(UsedMargin);
    double Equity() => state.Balance + FloatingPnl();
    double MarginLevel() => TotalMargin() <= 0 ? double.PositiveInfinity : Equity() / TotalMargin() * 100;

    void OpenPosition(string side)
    {
        var pair = state.SelectedPair;
        var lots = (double)lotsBox.Value;
        var price = CurrentPrice(pair) + (side == "BUY" ? 0.01 : -0.01);
        var leverage = state.Leverage;
        var required = price * lots * 10000 / leverage;
        if (Equity() - required <= 0)
        {
            MessageBox.Show("净值不足，无法开仓。请降低手数或杠杆。", "保证金不足", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var p = new TradePosition { Pair = pair, Side = side, Lots = lots, Entry = price, Leverage = leverage };
        state.Positions.Add(p);
        AddHistory(pair, side == "BUY" ? "开多" : "开空", lots, price, 0);
        UpdateAll(); SaveState();
    }

    void CloseSelectedPosition()
    {
        if (positionsGrid.SelectedRows.Count == 0) { MessageBox.Show("请先在持仓表中选择一笔持仓。"); return; }
        if (positionsGrid.SelectedRows[0].Tag is TradePosition p) ClosePosition(p, "手动平仓");
    }

    void ClosePosition(TradePosition p, string action)
    {
        if (!state.Positions.Contains(p)) return;
        var pnl = Profit(p);
        state.Balance += pnl;
        state.Positions.Remove(p);
        AddHistory(p.Pair, action + (p.Side == "BUY" ? "（多）" : "（空）"), p.Lots, CurrentPrice(p.Pair), pnl);
        UpdateAll(); SaveState();
    }

    void AddHistory(string pair, string action, double lots, double price, double profit)
    {
        state.History.Insert(0, new TradeRecord { Time = DateTime.Now, Pair = pair, Action = action, Lots = lots, Price = price, Profit = profit });
        if (state.History.Count > 300) state.History = state.History.Take(300).ToList();
    }

    void CheckMarginCall()
    {
        if (state.Positions.Count == 0) return;
        if (MarginLevel() < 50 || Equity() <= 0)
        {
            var old = state.Positions.ToList();
            foreach (var p in old) ClosePosition(p, "系统强制平仓");
            state.Balance = Math.Max(0, state.Balance);
            statusLabel.Text = "⚠ 已触发保证金强平！所有持仓已平仓。";
            statusLabel.ForeColor = red;
            if (!forcedLiquidationNotice)
            {
                forcedLiquidationNotice = true;
                MessageBox.Show("保证金率低于 50%，已触发模拟强制平仓。", "爆仓警报", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else
        {
            forcedLiquidationNotice = false;
        }
    }

    void UpdateAll()
    {
        double pnl = FloatingPnl(), margin = TotalMargin(), equity = Equity(), level = MarginLevel();
        balanceValue.Text = state.Balance.ToString("N2") + " ¥";
        equityValue.Text = equity.ToString("N2") + " ¥";
        pnlValue.Text = (pnl >= 0 ? "+" : "") + pnl.ToString("N2") + " ¥";
        pnlValue.ForeColor = pnl >= 0 ? green : red;
        marginValue.Text = margin.ToString("N2") + " ¥";
        marginLevelValue.Text = double.IsPositiveInfinity(level) ? "—" : level.ToString("N1") + "%";
        marginLevelValue.ForeColor = level < 100 ? red : text;

        double price = CurrentPrice(state.SelectedPair);
        pairPrice.Text = price.ToString("F3");
        double diff = price - (previousPrices.TryGetValue(state.SelectedPair, out var old) ? old : price);
        pairChange.Text = (diff >= 0 ? "▲ +" : "▼ ") + diff.ToString("F3");
        pairChange.ForeColor = diff >= 0 ? green : red;
        selectedPairValue.Text = state.SelectedPair;
        tradePriceValue.Text = price.ToString("F3");
        bidValue.Text = "BID " + (price - 0.01).ToString("F3") + "   ";
        askValue.Text = "ASK " + (price + 0.01).ToString("F3");
        chart.Invalidate();

        // Rebuild position rows only when the set of open positions changes.
        var ordered = state.Positions.OrderByDescending(x => x.OpenedAt).ToArray();
        bool rebuildPositions = positionsGrid.Rows.Count != ordered.Length;
        if (!rebuildPositions)
            for (int i = 0; i < ordered.Length; i++)
                if (positionsGrid.Rows[i].Tag is not TradePosition existing || existing.Id != ordered[i].Id) { rebuildPositions = true; break; }
        if (rebuildPositions)
        {
            positionsGrid.Rows.Clear();
            foreach (var p in ordered)
            {
                int row = positionsGrid.Rows.Add(p.Pair, p.Side == "BUY" ? "做多" : "做空", p.Lots.ToString("0.00"), p.Entry.ToString("F3"), CurrentPrice(p.Pair).ToString("F3"), Profit(p).ToString("N2"), UsedMargin(p).ToString("N2"));
                positionsGrid.Rows[row].Tag = p;
            }
        }
        for (int i = 0; i < ordered.Length; i++)
        {
            var p = ordered[i]; double tradePnl = Profit(p);
            var row = positionsGrid.Rows[i];
            row.Cells[3].Value = p.Entry.ToString("F3");
            row.Cells[4].Value = CurrentPrice(p.Pair).ToString("F3");
            row.Cells[5].Value = tradePnl.ToString("N2");
            row.Cells[6].Value = UsedMargin(p).ToString("N2");
            row.DefaultCellStyle.ForeColor = tradePnl >= 0 ? green : red;
        }

        var recent = state.History.Take(100).ToArray();
        bool rebuildHistory = historyGrid.Rows.Count != recent.Length ||
            (recent.Length > 0 && (historyGrid.Rows.Count == 0 || !ReferenceEquals(historyGrid.Rows[0].Tag, recent[0])));
        if (rebuildHistory)
        {
            historyGrid.Rows.Clear();
            foreach (var h in recent)
            {
                int row = historyGrid.Rows.Add(h.Time.ToString("HH:mm:ss"), h.Pair, h.Action, h.Lots.ToString("0.00"), h.Profit.ToString("N2"));
                historyGrid.Rows[row].Tag = h;
                historyGrid.Rows[row].DefaultCellStyle.ForeColor = h.Profit >= 0 ? green : red;
            }
        }
    }

    void DrawChart(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(panel);
        int w = chart.ClientSize.Width, h = chart.ClientSize.Height;
        if (w < 140 || h < 120 || candles.Count < 2) return;
        int left = 64, right = 72, top = 18, bottom = 30;
        int plotW = w - left - right, plotH = h - top - bottom;
        if (plotW <= 0 || plotH <= 0) return;
        double min = double.MaxValue, max = double.MinValue;
        for (int i = 0; i < candles.Count; i++) { min = Math.Min(min, candles[i].Low); max = Math.Max(max, candles[i].High); }
        double pad = Math.Max((max - min) * 0.08, 0.015);
        min -= pad; max += pad;
        double range = Math.Max(0.000001, max - min);
        float Y(double value) => top + (float)((max - value) / range) * plotH;
        float slot = plotW / (float)candles.Count;
        float bodyWidth = Math.Max(3f, Math.Min(11f, slot * 0.62f));
        using var gridPen = new Pen(Color.FromArgb(52, 53, 72), 1);
        using var axisPen = new Pen(Color.FromArgb(75, 77, 98), 1);
        using var textBrush = new SolidBrush(muted);
        using var font = new Font("Consolas", 8f);
        using var upPen = new Pen(green, 1.2f);
        using var downPen = new Pen(red, 1.2f);
        using var upBrush = new SolidBrush(green);
        using var downBrush = new SolidBrush(red);
        for (int i = 0; i < 5; i++)
        {
            int y = top + i * plotH / 4;
            g.DrawLine(gridPen, left, y, left + plotW, y);
            double value = max - range * i / 4.0;
            g.DrawString(value.ToString("F3"), font, textBrush, left + plotW + 6, y - 7);
        }
        for (int i = 0; i <= 6; i++)
        {
            int x = left + i * plotW / 6;
            g.DrawLine(gridPen, x, top, x, top + plotH);
        }
        g.DrawLine(axisPen, left, top + plotH, left + plotW, top + plotH);
        for (int i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            float x = left + (i + 0.5f) * slot;
            bool up = c.Close >= c.Open;
            var pen = up ? upPen : downPen;
            var brush = up ? upBrush : downBrush;
            g.DrawLine(pen, x, Y(c.High), x, Y(c.Low));
            float yOpen = Y(c.Open), yClose = Y(c.Close);
            float bodyTop = Math.Min(yOpen, yClose);
            float bodyHeight = Math.Max(1.5f, Math.Abs(yOpen - yClose));
            g.FillRectangle(brush, x - bodyWidth / 2, bodyTop, bodyWidth, bodyHeight);
        }
        var last = candles[^1];
        using var currentPen = new Pen(pink, 1) { DashStyle = DashStyle.Dash };
        g.DrawLine(currentPen, left, Y(last.Close), left + plotW, Y(last.Close));
        using var currentBrush = new SolidBrush(pink);
        using var chartTitleFont = new Font("Microsoft YaHei UI", 9, FontStyle.Bold);
        g.DrawString(state.SelectedPair + "  ·  OHLC 蜡烛图", chartTitleFont, currentBrush, left, 1);
        g.DrawString("最新 " + last.Close.ToString("F3"), font, currentBrush, left + plotW - 86, Math.Max(top, Y(last.Close) - 16));
        g.DrawString("随机模拟行情 · 非真实汇率", font, textBrush, left, h - bottom + 8);
    }

}


internal sealed class BufferedPanel : Panel
{
    public BufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}
