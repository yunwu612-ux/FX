using System.Text.Json.Serialization;

namespace FXSimulator;

public sealed class TradePosition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Pair { get; set; } = "USD/JPY";
    public string Side { get; set; } = "BUY";
    public double Lots { get; set; } = 0.01;
    public double Entry { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public double Leverage { get; set; } = 100;
}

public sealed class TradeRecord
{
    public DateTime Time { get; set; } = DateTime.Now;
    public string Pair { get; set; } = "";
    public string Action { get; set; } = "";
    public double Lots { get; set; }
    public double Price { get; set; }
    public double Profit { get; set; }
}

public sealed class AccountState
{
    public double Balance { get; set; } = 300000;
    public string SelectedPair { get; set; } = "USD/JPY";
    public double Lots { get; set; } = 0.01;
    public double Leverage { get; set; } = 100;
    public Dictionary<string, double> Prices { get; set; } = new()
    {
        ["USD/JPY"] = 149.500,
        ["EUR/JPY"] = 162.200,
        ["GBP/JPY"] = 188.100
    };
    public List<TradePosition> Positions { get; set; } = new();
    public List<TradeRecord> History { get; set; } = new();
}
