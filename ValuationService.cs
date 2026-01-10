using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace _777bot
{
    public class ValuationResult
    {
        public long SalonAvg { get; set; }
        public long EngineUpgradePrice { get; set; }
        public long EngineUpgradeMarket { get; set; }

        public List<(string name, long basePrice, long marketPrice, string note)> Bodykits { get; set; }
            = new List<(string name, long basePrice, long marketPrice, string note)>();

        public List<(string name, long basePrice, long marketPrice)> VisualItems { get; set; }
            = new List<(string name, long basePrice, long marketPrice)>();

        public List<(string name, long basePrice, long marketPrice, string note)> MechItems { get; set; }
            = new List<(string name, long basePrice, long marketPrice, string note)>();

        public List<string> MissingPrices { get; set; } = new List<string>();

        public long Total =>
            SalonAvg
            + EngineUpgradeMarket
            + Bodykits.Sum(b => b.marketPrice)
            + VisualItems.Sum(v => v.marketPrice)
            + MechItems.Sum(m => m.marketPrice);

        public Embed BuildEmbed(int vuid, VehicleCard card)
        {
            var eb = new EmbedBuilder()
                .WithTitle($"Wycena VUID {vuid}")
                .WithDescription($"{card.ModelRaw}\nSilnik: {card.EngineRaw}")
                .WithColor(Color.Gold);

            eb.AddField("Salon (średnia)", $"{SalonAvg:N0}$", true);
            eb.AddField("Silnik upgrade", $"{EngineUpgradeMarket:N0}$ (bazowo {EngineUpgradePrice:N0}$)", true);

            if (Bodykits.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var b in Bodykits)
                    sb.AppendLine($"{b.name}: {b.marketPrice:N0}$ (bazowo {b.basePrice:N0}$) {b.note}");
                eb.AddField("Bodykity", sb.ToString(), false);
            }

            if (VisualItems.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var v in VisualItems.Take(15))
                    sb.AppendLine($"{v.name}: {v.marketPrice:N0}$ (bazowo {v.basePrice:N0}$)");
                if (VisualItems.Count > 15) sb.AppendLine($"... +{VisualItems.Count - 15} więcej");
                eb.AddField("Wizualne", sb.ToString(), false);
            }

            if (MechItems.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var m in MechItems.Take(15))
                    sb.AppendLine($"{m.name}: {m.marketPrice:N0}$ (bazowo {m.basePrice:N0}$){m.note}");
                if (MechItems.Count > 15) sb.AppendLine($"... +{MechItems.Count - 15} więcej");
                eb.AddField("Mechaniczne", sb.ToString(), false);
            }

            eb.AddField("Suma", $"{Total:N0}$", false);

            if (MissingPrices.Count > 0)
            {
                eb.AddField("Brakujące ceny",
                    string.Join("\n", MissingPrices.Take(15)) + (MissingPrices.Count > 15 ? "\n..." : ""),
                    false);
            }

            return eb.Build();
        }
    }

    public class ValuationService
    {
        private readonly PriceCatalog _cat;
        private readonly VehicleMongoStore _store;

        private static readonly Dictionary<string, string> MechAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ogranicznik prędkości"] = "ogranicznik",
            ["cb radio"] = "cb-radio",
            ["gwint. zawieszenie"] = "gwintowane zawieszenie",
            ["gwintowane zawieszenie"] = "gwintowane zawieszenie",
        };

        public ValuationService(PriceCatalog cat, VehicleMongoStore store)
        {
            _cat = cat;
            _store = store;
        }

        public async Task<ValuationResult> EvaluateAsync(VehicleCard card)
        {
            var res = new ValuationResult();

            res.SalonAvg = ComputeSalonAvg(card, res);
            (res.EngineUpgradePrice, res.EngineUpgradeMarket) = ComputeEngineUpgrade(card, res);

            await ComputeBodykitsAsync(card, res);
            await ComputeVisualAsync(card, res);
            ComputeMechanical(card, res);

            return res;
        }

        private long ComputeSalonAvg(VehicleCard card, ValuationResult res)
        {
            var vehicleName = card.ModelRaw;
            var idx = vehicleName.LastIndexOf('(');
            if (idx > 0) vehicleName = vehicleName.Substring(0, idx).Trim();

            var vehicleKey = TextNorm.NormalizeKey(vehicleName);
            var engineKey = TextNorm.NormalizeKey(card.EngineRaw);

            var matches = _cat.Salon
                .Where(r =>
                    TextNorm.NormalizeKey(r.Vehicle) == vehicleKey &&
                    TextNorm.NormalizeKey(r.Engine) == engineKey)
                .Select(r => r.Price)
                .ToList();

            if (matches.Count == 0)
            {
                matches = _cat.Salon
                    .Where(r =>
                        TextNorm.NormalizeKey(r.Model) == TextNorm.NormalizeKey(card.BaseModel) &&
                        TextNorm.NormalizeKey(r.Engine) == engineKey)
                    .Select(r => r.Price)
                    .ToList();
            }

            if (matches.Count == 0)
            {
                res.MissingPrices.Add($"Salon: brak w salon_prices.csv dla '{vehicleName}' + '{card.EngineRaw}'");
                return 0;
            }

            return (long)Math.Round(matches.Average());
        }

        private (long basePrice, long marketPrice) ComputeEngineUpgrade(VehicleCard card, ValuationResult res)
        {
            double dm3;
            if (!VehicleCardParser.TryParseEngineDisplacementDm3(card.EngineRaw, out dm3))
            {
                res.MissingPrices.Add($"Silnik: nie umiem odczytać dm³ z '{card.EngineRaw}'");
                return (0, 0);
            }

            var bm = TextNorm.NormalizeKey(card.BaseModel);

            var candidates = _cat.EngineUpgrades.Where(r =>
            {
                var keys = r.ModelKeys.Split(',')
                    .Select(k => TextNorm.NormalizeKey(k))
                    .ToList();
                return keys.Contains(bm);
            }).ToList();

            if (candidates.Count == 0)
            {
                res.MissingPrices.Add($"Silnik: brak wpisu w engine_upgrades.csv dla modelu '{card.BaseModel}'");
                return (0, 0);
            }

            var match = candidates.FirstOrDefault(r => dm3 >= r.From && dm3 <= r.To);
            if (match == null)
            {
                res.MissingPrices.Add($"Silnik: brak przedziału dla {dm3:0.##}dm³ w '{card.BaseModel}'");
                return (0, 0);
            }

            var basePrice = match.Price;
            var marketPrice = (long)Math.Round(basePrice * 0.5);
            return (basePrice, marketPrice);
        }

        private async Task ComputeBodykitsAsync(VehicleCard card, ValuationResult res)
        {
            // main bodykit: BaseModel + BodykitMainName
            if (!string.IsNullOrWhiteSpace(card.BodykitMainName))
            {
                BodykitRow bk;
                if (_cat.TryGetBodykit(card.BaseModel, card.BodykitMainName, out bk))
                {
                    var mult = bk.Level >= 40 ? 1.0 : 0.5;
                    res.Bodykits.Add(($"{card.BaseModel} {bk.Name}", bk.Price, (long)Math.Round(bk.Price * mult), bk.Level >= 40 ? "(100%)" : "(50%)"));
                }
            }

            // aero: baseModel = "Spoiler", name = "Aero III"
            if (!string.IsNullOrWhiteSpace(card.BodykitAeroName))
            {
                BodykitRow bk;
                if (_cat.TryGetBodykit("Spoiler", card.BodykitAeroName, out bk))
                {
                    var mult = bk.Level >= 40 ? 1.0 : 0.5;
                    res.Bodykits.Add(($"Spoiler {bk.Name}", bk.Price, (long)Math.Round(bk.Price * mult), bk.Level >= 40 ? "(100%)" : "(50%)"));
                }
                else
                {
                    res.MissingPrices.Add($"Bodykit: brak 'Spoiler {card.BodykitAeroName}' w bodykits.csv");
                }
            }

            await Task.CompletedTask;
        }

        private async Task ComputeVisualAsync(VehicleCard card, ValuationResult res)
        {
            foreach (var v in card.VisualTuning)
            {
                if (v.Id != 0)
                {
                    long price;
                    if (_cat.VisualById.TryGetValue(v.Id, out price))
                        res.VisualItems.Add(($"{v.Name} ({v.Id})", price, (long)Math.Round(price * 0.5)));
                    else
                        res.MissingPrices.Add($"Wizualne ID: brak ceny dla {v.Id} ({v.Name})");
                }
                else
                {
                    var key = TextNorm.NormalizeKey(v.Name);
                    long price;
                    if (_cat.VisualByName.TryGetValue(key, out price))
                        res.VisualItems.Add((v.Name, price, (long)Math.Round(price * 0.5)));
                    else
                        res.MissingPrices.Add($"Wizualne: brak ceny dla '{v.Name}' (visual_name_prices.csv)");
                }
            }

            await AddColorAsync(res, SpecialColorType.Lights, card.LightsColorRaw);
            await AddColorAsync(res, SpecialColorType.Dashboard, card.DashboardColorRaw);
        }

        private async Task AddColorAsync(ValuationResult res, SpecialColorType type, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            var parsed = VehicleCardParser.ParseColorWithRarity(raw);
            var name = parsed.name;
            var rarity = parsed.rarity;

            if (string.IsNullOrWhiteSpace(name)) return;

            var isLimitedOrUnique =
                rarity.Equals("Limitowane", StringComparison.OrdinalIgnoreCase) ||
                rarity.Equals("Unikatowe", StringComparison.OrdinalIgnoreCase);

            long basePrice;

            if (isLimitedOrUnique)
            {
                var saved = await _store.GetSpecialColorPriceAsync(type, name, rarity);
                if (!saved.HasValue)
                {
                    res.MissingPrices.Add($"{(type == SpecialColorType.Lights ? "Kolor świateł" : "Kolor licznika")} '{name} - {rarity}': PODAJ CENĘ");
                    return;
                }
                basePrice = saved.Value;
            }
            else
            {
                var keyPrefix = type == SpecialColorType.Lights ? "kolor_swiatel:" : "kolor_licznika:";
                var key = TextNorm.NormalizeKey(keyPrefix + name);

                long p;
                if (!_cat.VisualByName.TryGetValue(key, out p))
                {
                    res.MissingPrices.Add($"{(type == SpecialColorType.Lights ? "Kolor świateł" : "Kolor licznika")}: brak ceny dla '{name}' (klucz '{keyPrefix}{name}')");
                    return;
                }
                basePrice = p;
            }

            var mult = isLimitedOrUnique ? 1.0 : 0.5;
            var market = (long)Math.Round(basePrice * mult);

            var label = type == SpecialColorType.Lights ? "Kolor świateł" : "Kolor licznika";
            var n = isLimitedOrUnique ? $"{label}: {name} - {rarity}" : $"{label}: {name}";
            res.VisualItems.Add((n, basePrice, market));
        }

        private void ComputeMechanical(VehicleCard card, ValuationResult res)
        {
            foreach (var raw in card.MechanicalTuningRaw)
            {
                var name = TextNorm.Normalize(raw);
                var key = TextNorm.NormalizeKey(name);

                string alias;
                if (MechAliases.TryGetValue(key, out alias))
                    key = TextNorm.NormalizeKey(alias);

                key = NormalizeMechKey(key);

                long basePrice;
                if (_cat.MechByKey.TryGetValue(key, out basePrice))
                {
                    var isFull =
                        key.StartsWith("c.f.i:", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("zestaw:torowy", StringComparison.OrdinalIgnoreCase);

                    var mult = isFull ? 1.0 : 0.5;
                    var market = (long)Math.Round(basePrice * mult);

                    res.MechItems.Add((name, basePrice, market, isFull ? " (100%)" : " (50%)"));
                }
                else
                {
                    res.MissingPrices.Add($"Mechaniczne: brak ceny dla '{name}' (klucz '{key}')");
                }
            }
        }

        private static string NormalizeMechKey(string key)
        {
            if (key.StartsWith("ecu", StringComparison.OrdinalIgnoreCase))
            {
                var v = ExtractV(key);
                return v != "" ? "ecu:" + v : "ecu";
            }
            if (key.StartsWith("turbo", StringComparison.OrdinalIgnoreCase))
            {
                var v = ExtractV(key);
                return v != "" ? "turbo:" + v : "turbo";
            }
            if (key.StartsWith("lpg", StringComparison.OrdinalIgnoreCase))
            {
                var cap = ExtractLiters(key);
                return cap != "" ? "lpg:" + cap : "lpg";
            }
            if (key.StartsWith("zestaw", StringComparison.OrdinalIgnoreCase))
            {
                var kind = key.Replace("zestaw", "").Trim().Trim(':');
                if (string.IsNullOrWhiteSpace(kind)) return "zestaw";
                return "zestaw:" + TextNorm.NormalizeKey(kind);
            }
            if (key.StartsWith("c.f.i", StringComparison.OrdinalIgnoreCase) || key.StartsWith("cfi", StringComparison.OrdinalIgnoreCase))
            {
                var v = ExtractV(key);
                return v != "" ? "c.f.i:" + v : "c.f.i";
            }

            return key;
        }

        private static string ExtractV(string key)
        {
            var m = System.Text.RegularExpressions.Regex.Match(key, @"\bv\s*(\d)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return "";
            return "v" + m.Groups[1].Value;
        }

        private static string ExtractLiters(string key)
        {
            var m = System.Text.RegularExpressions.Regex.Match(key, @"\b(\d{2,3})\s*l\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return "";
            return m.Groups[1].Value + "l";
        }
    }
}
