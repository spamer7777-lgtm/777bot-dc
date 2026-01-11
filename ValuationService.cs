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
    // ogranicznik
    ["ogranicznik prędkości"] = "ogranicznik",
    ["ogranicznik predkosci"] = "ogranicznik",

    // wykrywacz
    ["wykrywacz fotoradarów"] = "wykrywacz_fotoradarow",
    ["wykrywacz fotoradarow"] = "wykrywacz_fotoradarow",

    // MZN
    ["moduł zmiany napędu"] = "zmiana_napedu:mzn",
    ["modul zmiany napedu"] = "zmiana_napedu:mzn",
    ["mzn"] = "zmiana_napedu:mzn",
    // ===== SYSTEM ABS =====
    ["system abs"] = "system_abs",
    ["abs"] = "system_abs",
    // ===== GWINT =====
    ["gwint. zawieszenie"] = "gwintowane_zawieszenie",
    ["gwint zawieszenie"] = "gwintowane_zawieszenie",
    ["gwintowane zawieszenie"] = "gwintowane_zawieszenie",      
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
            if (!string.IsNullOrWhiteSpace(card.BodykitMainName))
            {
                BodykitRow bk;
                if (_cat.TryGetBodykit(card.BaseModel, card.BodykitMainName, out bk))
                {
                    var mult = bk.Level >= 40 ? 1.0 : 0.5;
                    res.Bodykits.Add(($"{card.BaseModel} {bk.Name}", bk.Price, (long)Math.Round(bk.Price * mult), bk.Level >= 40 ? "(100%)" : "(50%)"));
                }
            }

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
                // ======================
                // 1) WIZUALNE PO ID
                // ======================
                if (v.Id != 0)
                {
                    long idPrice;
                    if (_cat.VisualById.TryGetValue(v.Id, out idPrice))
                        res.VisualItems.Add(($"{v.Name} ({v.Id})", idPrice, (long)Math.Round(idPrice * 0.5)));
                    else
                        res.MissingPrices.Add($"Wizualne ID: brak ceny dla {v.Id} ({v.Name})");

                    continue;
                }

                // ======================
                // 2) SPECJALNE: Poszerzenia (2,2) -> przód + tył
                // ======================
                var s = TextNorm.Normalize(v.Name);

                var widen = System.Text.RegularExpressions.Regex.Match(
                    s,
                    @"^Poszerzenia\s*\(\s*(?<f>\d)\s*,\s*(?<r>\d)\s*\)\s*$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (widen.Success)
                {
                    var f = widen.Groups["f"].Value;
                    var r = widen.Groups["r"].Value;

                    // przód
                    var keyF = TextNorm.NormalizeKey("poszerzenia_przod:" + f);
                    long priceF;
                    if (_cat.VisualByName.TryGetValue(keyF, out priceF))
                        res.VisualItems.Add(($"Poszerzenia przód ({f})", priceF, (long)Math.Round(priceF * 0.5)));
                    else
                        res.MissingPrices.Add($"Wizualne: brak ceny dla 'Poszerzenia przód ({f})' (klucz '{keyF}')");

                    // tył
                    var keyR = TextNorm.NormalizeKey("poszerzenia_tyl:" + r);
                    long priceR;
                    if (_cat.VisualByName.TryGetValue(keyR, out priceR))
                        res.VisualItems.Add(($"Poszerzenia tył ({r})", priceR, (long)Math.Round(priceR * 0.5)));
                    else
                        res.MissingPrices.Add($"Wizualne: brak ceny dla 'Poszerzenia tył ({r})' (klucz '{keyR}')");

                    continue;
                }

                // ======================
                // 3) POZOSTAŁE: nazwa / mapowanie (przyciemnienie, rozmiar felg)
                // ======================
                string mappedKey;
                string key;

                if (TryMapSpecialVisualName(v.Name, out mappedKey))
                    key = TextNorm.NormalizeKey(mappedKey);
                else
                    key = TextNorm.NormalizeKey(v.Name);

                long namePrice;
                if (_cat.VisualByName.TryGetValue(key, out namePrice))
                    res.VisualItems.Add((v.Name, namePrice, (long)Math.Round(namePrice * 0.5)));
                else
                    res.MissingPrices.Add($"Wizualne: brak ceny dla '{v.Name}' (klucz '{key}')");
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

        // normalizacja + ascii (bez polskich znaków)
        var key = TextNorm.NormalizeKey(name);
        key = ToAsciiPl(key);

        // aliasy
        if (MechAliases.TryGetValue(key, out var alias))
            key = TextNorm.NormalizeKey(alias);

        // ===== SPECJALNE MAPOWANIA =====

        // Powiększony bak (150l)
        var tank = System.Text.RegularExpressions.Regex.Match(
            key,
            @"powiekszony\s+bak\s*\(\s*(\d{2,3})l\s*\)");
        if (tank.Success)
            key = $"bak_paliwa:{tank.Groups[1].Value}l";

        // Butla LPG (75l)
        var lpg = System.Text.RegularExpressions.Regex.Match(
            key,
            @"butla\s+lpg\s*\(\s*(\d{2,3})l\s*\)");
        if (lpg.Success)
            key = $"lpg:{lpg.Groups[1].Value}l";

        // Przeniesienie napędu (AWD/FWD/RWD)
        var drive = System.Text.RegularExpressions.Regex.Match(
            key,
            @"przeniesienie\s+napedu\s*\(\s*(awd|fwd|rwd)\s*\)");
        if (drive.Success)
            key = $"naped:{drive.Groups[1].Value}";

        // Moduł zmiany napędu (MZN)
        if (key.Contains("modul zmiany napedu"))
            key = "mzn";

        // Aplikacja transportowa
        if (key == "aplikacja transportowa")
            key = "aplikacja_transportowa";

        // ECU / Turbo / LPG / Zestawy / CFI
        key = NormalizeMechKey(key);

        // ===== CENA =====
        if (_cat.MechByKey.TryGetValue(key, out var basePrice))
        {
            var full =
                key.StartsWith("c.f.i:") ||
                key.StartsWith("zestaw:");

            var mult = full ? 1.0 : 0.5;
            var market = (long)Math.Round(basePrice * mult);

            res.MechItems.Add((
                name,
                basePrice,
                market,
                full ? " (100%)" : " (50%)"
            ));
        }
        else
        {
            res.MissingPrices.Add(
                $"Mechaniczne: brak ceny dla '{name}' (klucz '{key}')"
            );
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

                if (string.IsNullOrWhiteSpace(kind))
                    return "zestaw";

                kind = kind.Trim().Trim('(', ')').Trim();

                var k = TextNorm.NormalizeKey(kind);
                if (k == "t") k = "torowy";
                else if (k == "u") k = "uliczny";
                else if (k == "w") k = "wyscigowy";
                else if (k == "d") k = "drifterski";

                return "zestaw:" + TextNorm.NormalizeKey(k);
            }
            if (key.StartsWith("c.f.i", StringComparison.OrdinalIgnoreCase) || key.StartsWith("cfi", StringComparison.OrdinalIgnoreCase))
            {
                var v = ExtractV(key);
                return v != "" ? "c.f.i:" + v : "c.f.i";
            }

            return key;
        }

        // ======= VISUAL SPECIAL PARSING (Przyciemnienie / Rozmiar felg) =======
        private static bool TryMapSpecialVisualName(string rawName, out string mappedKey)
        {
            mappedKey = "";
            if (string.IsNullOrWhiteSpace(rawName)) return false;

            var s = TextNorm.Normalize(rawName);

            // Przyciemnienie szyb (70%)
            var tint = System.Text.RegularExpressions.Regex.Match(
                s,
                @"^Przyciemnienie\s+szyb\s*\(\s*(?<p>\d{1,3})\s*%\s*\)\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (tint.Success)
            {
                mappedKey = "przyciemnienie_szyb:" + tint.Groups["p"].Value;
                return true;
            }

            // Rozmiar felg (Duże)
            var rimSize = System.Text.RegularExpressions.Regex.Match(
                s,
                @"^Rozmiar\s+felg\s*\(\s*(?<v>.+?)\s*\)\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (rimSize.Success)
            {
                var v = TextNorm.NormalizeKey(rimSize.Groups["v"].Value);

                // spolszczenia -> ascii (na wszelki wypadek)
                v = v.Replace("ą", "a").Replace("ę", "e").Replace("ł", "l").Replace("ń", "n")
                     .Replace("ó", "o").Replace("ś", "s").Replace("ż", "z").Replace("ź", "z");

                if (v.Contains("bardzo") && v.Contains("male")) v = "bardzo_male";
                else if (v.Contains("male")) v = "male";
                else if (v.Contains("duze")) v = "duze";
                else if (v.Contains("standard")) v = "standardowe";

                mappedKey = "rozmiar_felg:" + v;
                return true;
            }

            return false;
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

        private static string ToAsciiPl(string s)
        {
    return s
        .Replace("ą","a").Replace("ć","c").Replace("ę","e")
        .Replace("ł","l").Replace("ń","n").Replace("ó","o")
        .Replace("ś","s").Replace("ż","z").Replace("ź","z");
        }
    }
}
