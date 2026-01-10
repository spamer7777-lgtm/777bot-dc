using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace _777bot
{
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
        public int ModelId { get; set; } = 0; // 0 = brak

        public string BodykitMainName { get; set; } = ""; // empty = brak
        public string BodykitAeroName { get; set; } = ""; // empty = brak

        public List<VisualItem> VisualTuning { get; set; } = new List<VisualItem>();
        public List<string> MechanicalTuningRaw { get; set; } = new List<string>();

        public string LightsColorRaw { get; set; } = "";
        public string DashboardColorRaw { get; set; } = "";
    }

    public class VisualItem
    {
        public int Id { get; set; } = 0;      // 0 = brak
        public string Name { get; set; } = "";
        public string Raw { get; set; } = "";
    }

    public class SpecialColorKey
    {
        public SpecialColorType Type { get; set; }
        public string Name { get; set; } = "";
        public string Rarity { get; set; } = "";
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
            return s;
        }
    }
}
