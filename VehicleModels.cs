using System.Text.RegularExpressions;

namespace _777bot;

public enum SpecialColorType
{
    Lights,
    Dashboard
}

public class VehicleCard
{
    public int Vuid { get; set; }
    public string ModelRaw { get; set; } = "";
    public string EngineRaw { get; set; } = "";

    public string BaseModel { get; set; } = "";
    public int? ModelId { get; set; }

    public string? BodykitMainName { get; set; } // e.g. "GT"
    public string? BodykitAeroName { get; set; } // e.g. "Aero III"

    public List<VisualItem> VisualTuning { get; set; } = new();
    public List<string> MechanicalTuningRaw { get; set; } = new();

    public string? LightsColorRaw { get; set; }
    public string? DashboardColorRaw { get; set; }
}

public class VisualItem
{
    public int? Id { get; set; }              // if parsed like "(1079)"
    public string Name { get; set; } = "";    // item name
    public string Raw { get; set; } = "";     // original token
}

public class SpecialColorKey
{
    public SpecialColorType Type { get; set; }
    public string Name { get; set; } = "";        // normalized color name
    public string Rarity { get; set; } = "";      // "", "Limitowane", "Unikatowe"
}

public static class TextNorm
{
    public static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        s = Regex.Replace(s, @"\s+", " ");
        return s;
    }

    public static string NormalizeKey(string s)
    {
        s = Normalize(s).ToLowerInvariant();
        // keep Polish chars as-is; only normalize whitespace and lowercase
        return s;
    }
}
