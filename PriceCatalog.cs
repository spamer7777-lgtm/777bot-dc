using System.Globalization;

namespace _777bot;

public class SalonRow
{
    public string Model { get; set; } = "";
    public string Vehicle { get; set; } = "";
    public string Engine { get; set; } = "";
    public long Price { get; set; }
}

public class EngineUpgradeRow
{
    public string ModelKeys { get; set; } = ""; // e.g. "Infernus, Infernus Kakimoto, ..."
    public double From { get; set; }
    public double To { get; set; }
    public long Price { get; set; }
}

public class BodykitRow
{
    public string BaseModel { get; set; } = "";
    public string Name { get; set; } = ""; // exact: "GT", "F-250 Classic", "Aero III"
    public int Level { get; set; }
    public long Price { get; set; }
}

public class PriceCatalog
{
    public List<SalonRow> Salon { get; set; } = new();
    public List<EngineUpgradeRow> EngineUpgrades { get; set; } = new();
    public Dictionary<int, long> VisualById { get; set; } = new();
    public Dictionary<string, long> MechByKey { get; set; } = new(); // normalized key -> price
    public Dictionary<string, long> VisualByName { get; set; } = new(); // normalized key -> price
    public List<BodykitRow> Bodykits { get; set; } = new();

    // Normalized quick lookups:
    private Dictionary<(string baseModel, string name), BodykitRow> _bodykitMap = new();

    public void BuildIndexes()
    {
        _bodykitMap = Bodykits.ToDictionary(
            b => (TextNorm.NormalizeKey(b.BaseModel), TextNorm.NormalizeKey(b.Name)),
            b => b);
    }

    public bool TryGetBodykit(string baseModel, string name, out BodykitRow row)
    {
        row = default!;
        if (string.IsNullOrWhiteSpace(baseModel) || string.IsNullOrWhiteSpace(name)) return false;
        return _bodykitMap.TryGetValue((TextNorm.NormalizeKey(baseModel), TextNorm.NormalizeKey(name)), out row);
    }

    public static long ParseMoney(string s)
    {
        s = s.Trim()
            .Replace("$", "")
            .Replace(" ", "")
            .Replace("\t", "")
            .Replace("\u00A0", "");
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
        // also allow "57,500"
        s = s.Replace(",", "");
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;
        throw new FormatException($"Nie umiem sparsować kwoty: {s}");
    }
}
