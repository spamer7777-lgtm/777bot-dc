using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace _777bot
{
    public static class PriceCatalogLoader
    {
        public static PriceCatalog LoadFromDataFolder(string dataDir)
        {
            var cat = new PriceCatalog();

            TryLoadSalon(Path.Combine(dataDir, "salon_prices.csv"), cat);
            TryLoadEngine(Path.Combine(dataDir, "engine_upgrades.csv"), cat);
            TryLoadVisualId(Path.Combine(dataDir, "visual_id_prices.csv"), cat);
            TryLoadMech(Path.Combine(dataDir, "mech_prices.csv"), cat);
            TryLoadVisualName(Path.Combine(dataDir, "visual_name_prices.csv"), cat);
            TryLoadBodykits(Path.Combine(dataDir, "bodykits.csv"), cat);

            cat.BuildIndexes();
            return cat;
        }

        private static void TryLoadSalon(string path, PriceCatalog cat)
        {
            if (!File.Exists(path)) return;

            foreach (var row in ReadCsv(path))
            {
                if (row.Length < 4) continue;
                cat.Salon.Add(new SalonRow
                {
                    Model = row[0],
                    Vehicle = row[1],
                    Engine = row[2],
                    Price = PriceCatalog.ParseMoney(row[3])
                });
            }
        }

        private static void TryLoadEngine(string path, PriceCatalog cat)
        {
            if (!File.Exists(path)) return;

            foreach (var row in ReadCsv(path))
            {
                if (row.Length < 4) continue;

                double from, to;
                if (!double.TryParse(row[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out from)) continue;
                if (!double.TryParse(row[2].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out to)) continue;

                cat.EngineUpgrades.Add(new EngineUpgradeRow
                {
                    ModelKeys = row[0],
                    From = from,
                    To = to,
                    Price = PriceCatalog.ParseMoney(row[3])
                });
            }
        }

        private static void TryLoadVisualId(string path, PriceCatalog cat)
        {
            if (!File.Exists(path)) return;

            foreach (var row in ReadCsv(path))
            {
                if (row.Length < 2) continue;
                int id;
                if (!int.TryParse(row[0], out id)) continue;
                cat.VisualById[id] = PriceCatalog.ParseMoney(row[1]);
            }
        }

        private static void TryLoadMech(string path, PriceCatalog cat)
        {
            if (!File.Exists(path)) return;

            foreach (var row in ReadCsv(path))
            {
                if (row.Length < 2) continue;
                var key = TextNorm.NormalizeKey(row[0]);
                cat.MechByKey[key] = PriceCatalog.ParseMoney(row[1]);
            }
        }

        private static void TryLoadVisualName(string path, PriceCatalog cat)
        {
            if (!File.Exists(path)) return;

            foreach (var row in ReadCsv(path))
            {
                if (row.Length < 2) continue;
                var key = TextNorm.NormalizeKey(row[0]);
                cat.VisualByName[key] = PriceCatalog.ParseMoney(row[1]);
            }
        }

        private static void TryLoadBodykits(string path, PriceCatalog cat)
        {
            if (!File.Exists(path)) return;

            foreach (var row in ReadCsv(path))
            {
                if (row.Length < 4) continue;
                int lvl;
                if (!int.TryParse(row[2], out lvl)) continue;

                cat.Bodykits.Add(new BodykitRow
                {
                    BaseModel = row[0],
                    Name = row[1],
                    Level = lvl,
                    Price = PriceCatalog.ParseMoney(row[3])
                });
            }
        }

        private static IEnumerable<string[]> ReadCsv(string path)
        {
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.TrimStart().StartsWith("#")) continue;

                var sep = line.Contains(';') ? ';' : ',';
                var parts = line.Split(sep).Select(p => p.Trim()).ToArray();

                if (parts.Length > 0 && parts[0].Equals("Model", StringComparison.OrdinalIgnoreCase)) continue;
                if (parts.Length > 0 && parts[0].Equals("Modele", StringComparison.OrdinalIgnoreCase)) continue;
                if (parts.Length > 0 && parts[0].Equals("Id", StringComparison.OrdinalIgnoreCase)) continue;
                if (parts.Length > 0 && parts[0].Equals("BaseModel", StringComparison.OrdinalIgnoreCase)) continue;
                if (parts.Length > 0 && parts[0].Equals("Key", StringComparison.OrdinalIgnoreCase)) continue;

                yield return parts;
            }
        }
    }
}
