using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace _777bot
{
    public class SalonRow
    {
        public string Model { get; set; } = "";
        public string Vehicle { get; set; } = "";
        public string Engine { get; set; } = "";
        public long Price { get; set; }
    }

    public class EngineUpgradeRow
    {
        public string ModelKeys { get; set; } = "";
        public double From { get; set; }
        public double To { get; set; }
        public long Price { get; set; }
    }

    public class BodykitRow
    {
        public string BaseModel { get; set; } = "";
        public string Name { get; set; } = "";
        public int Level { get; set; }
        public long Price { get; set; }
    }

    public class PriceCatalog
    {
        public List<SalonRow> Salon { get; set; } = new List<SalonRow>();
        public List<EngineUpgradeRow> EngineUpgrades { get; set; } = new List<EngineUpgradeRow>();
        public Dictionary<int, long> VisualById { get; set; } = new Dictionary<int, long>();
        public Dictionary<string, long> MechByKey { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, long> VisualByName { get; set; } = new Dictionary<string, long>();
        public List<BodykitRow> Bodykits { get; set; } = new List<BodykitRow>();

        private Dictionary<(string baseModel, string name), BodykitRow> _bodykitMap = new Dictionary<(string, string), BodykitRow>();

        public void BuildIndexes()
        {
            _bodykitMap = Bodykits.ToDictionary(
                b => (TextNorm.NormalizeKey(b.BaseModel), TextNorm.NormalizeKey(b.Name)),
                b => b);
        }

        public bool TryGetBodykit(string baseModel, string name, out BodykitRow row)
        {
            row = null;
            if (string.IsNullOrWhiteSpace(baseModel) || string.IsNullOrWhiteSpace(name)) return false;

            BodykitRow found;
            if (_bodykitMap.TryGetValue((TextNorm.NormalizeKey(baseModel), TextNorm.NormalizeKey(name)), out found))
            {
                row = found;
                return true;
            }
            return false;
        }

        public static long ParseMoney(string s)
        {
            s = s.Trim()
                .Replace("$", "")
                .Replace(" ", "")
                .Replace("\t", "")
                .Replace("\u00A0", "");

            long v;
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;

            s = s.Replace(",", "");
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;

            throw new FormatException("Nie umiem sparsować kwoty: " + s);
        }
    }
}
