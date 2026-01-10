using System.Globalization;
using System.Text.RegularExpressions;

namespace _777bot;

public static class VehicleCardParser
{
    private static readonly Regex VuidRe = new(@"^\s*VUID\s*(?:\r?\n|\s)+(?<vuid>\d+)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex ModelLineRe = new(@"^\s*Model\s*(?:\r?\n|\s)+(?<val>.+?)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex EngineLineRe = new(@"^\s*Silnik\s*(?:\r?\n|\s)+(?<val>.+?)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex VisualRe = new(@"^\s*Tuning\s+wizualny\s*(?:\r?\n|\s)+(?<val>.+?)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex MechRe = new(@"^\s*Tuning\s+mechaniczny\s*(?:\r?\n|\s)+(?<val>.+?)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex LightsColorRe = new(@"^\s*Kolor\s+świateł\s*(?:\r?\n|\s)+(?<val>.+?)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex DashColorRe = new(@"^\s*Kolor\s+licznika\s*(?:\r?\n|\s)+(?<val>.+?)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex ModelIdRe = new(@"\((?<id>\d+)\)\s*$", RegexOptions.Compiled);
    private static readonly Regex AeroRe = new(@"\bAero\s+(I|II|III|IV)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Dm3Re = new(@"\((?<cc>[\d\.,]+)\s*dm", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParse(string paste, out VehicleCard card, out string error)
    {
        card = new VehicleCard();
        error = "";

        paste = paste.Replace("\u00A0", " "); // nbsp

        var vuidM = VuidRe.Match(paste);
        if (!vuidM.Success || !int.TryParse(vuidM.Groups["vuid"].Value, out var vuid))
        {
            error = "Nie widzę VUID w wklejce.";
            return false;
        }
        card.Vuid = vuid;

        var modelM = ModelLineRe.Match(paste);
        if (!modelM.Success)
        {
            error = "Nie widzę pola Model w wklejce.";
            return false;
        }
        card.ModelRaw = TextNorm.Normalize(modelM.Groups["val"].Value);

        var engM = EngineLineRe.Match(paste);
        if (!engM.Success)
        {
            error = "Nie widzę pola Silnik w wklejce.";
            return false;
        }
        card.EngineRaw = TextNorm.Normalize(engM.Groups["val"].Value);

        var visM = VisualRe.Match(paste);
        if (visM.Success)
            card.VisualTuning = ParseVisualList(visM.Groups["val"].Value);

        var mechM = MechRe.Match(paste);
        if (mechM.Success)
            card.MechanicalTuningRaw = SplitCommaList(mechM.Groups["val"].Value);

        var lightsM = LightsColorRe.Match(paste);
        if (lightsM.Success)
            card.LightsColorRaw = TextNorm.Normalize(lightsM.Groups["val"].Value);

        var dashM = DashColorRe.Match(paste);
        if (dashM.Success)
            card.DashboardColorRaw = TextNorm.Normalize(dashM.Groups["val"].Value);

        ParseModel(card);

        return true;
    }

    private static List<string> SplitCommaList(string s)
    {
        return s.Split(',')
            .Select(x => TextNorm.Normalize(x))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static List<VisualItem> ParseVisualList(string s)
    {
        var tokens = SplitCommaList(s);
        var list = new List<VisualItem>();
        foreach (var t in tokens)
        {
            var item = new VisualItem { Raw = t, Name = t };

            // Try parse last (...) as integer ID
            var m = Regex.Match(t, @"\((?<id>\d+)\)\s*$");
            if (m.Success && int.TryParse(m.Groups["id"].Value, out var id))
            {
                item.Id = id;
                item.Name = TextNorm.Normalize(Regex.Replace(t, @"\s*\(\d+\)\s*$", ""));
            }
            else
            {
                item.Name = t;
            }

            list.Add(item);
        }

        return list;
    }

    private static void ParseModel(VehicleCard card)
    {
        // Extract modelId
        var raw = card.ModelRaw;
        var idM = ModelIdRe.Match(raw);
        if (idM.Success && int.TryParse(idM.Groups["id"].Value, out var mid))
        {
            card.ModelId = mid;
            raw = raw.Substring(0, idM.Index).Trim();
        }

        // BaseModel = first word
        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        card.BaseModel = parts.Length > 0 ? parts[0] : raw;

        // Detect Aero I/II/III/IV anywhere (but in practice at end)
        var aeroM = AeroRe.Match(raw);
        if (aeroM.Success)
        {
            // normalize to "Aero X"
            var aeroToken = aeroM.Value;
            aeroToken = Regex.Replace(aeroToken, @"\s+", " ").Trim();
            // Ensure capitalization "Aero III"
            aeroToken = "Aero " + aeroToken.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
            card.BodykitAeroName = aeroToken;

            // remove aero token from raw to find main bodykit part
            raw = Regex.Replace(raw, @"\bAero\s+(I|II|III|IV)\b", "", RegexOptions.IgnoreCase).Trim();
            raw = Regex.Replace(raw, @"\s+", " ").Trim();
        }

        // After removing base model, remaining can be main bodykit name
        if (parts.Length >= 2)
        {
            var afterBase = raw.Substring(card.BaseModel.Length).Trim();
            if (!string.IsNullOrWhiteSpace(afterBase))
                card.BodykitMainName = afterBase;
        }
    }

    public static bool TryParseEngineDisplacementDm3(string engineRaw, out double dm3)
    {
        dm3 = 0;
        var m = Dm3Re.Match(engineRaw);
        if (!m.Success) return false;

        var s = m.Groups["cc"].Value.Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out dm3);
    }

    public static (string name, string rarity) ParseColorWithRarity(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ("", "");

        var s = TextNorm.Normalize(raw!);
        var parts = s.Split(" - ", StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            var name = parts[0];
            var tag = parts[1];
            // only care about Limitowane/Unikatowe
            if (tag.Contains("Limit", StringComparison.OrdinalIgnoreCase)) return (name, "Limitowane");
            if (tag.Contains("Unikat", StringComparison.OrdinalIgnoreCase)) return (name, "Unikatowe");
            return (name, tag);
        }

        return (s, "");
    }
}
