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

        public double? BaseSalonDm3 { get; set; }

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
            ["ogranicznik predkosci"] = "ogranicznik",

            ["wykrywacz fotoradarów"] = "wykrywacz_fotoradarow",
            ["wykrywacz fotoradarow"] = "wykrywacz_fotoradarow",

            // alias -> klucz w mech_prices.csv
            ["moduł zmiany napędu"] = "zmiana_napedu:mzn",
            ["modul zmiany napedu"] = "zmiana_napedu:mzn",
            ["mzn"] = "zmiana_napedu:mzn",

            ["system abs"] = "system_abs",
            ["abs"] = "system_abs",

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

            res.SalonAvg = ComputeSalonAvg(card, res, out var baseSalonDm3);
            res.BaseSalonDm3 = baseSalonDm3;

            (res.EngineUpgradePrice, res.EngineUpgradeMarket) = ComputeEngineUpgrade(card, res, baseSalonDm3);

            await ComputeBodykitsAsync(card, res);
            await ComputeVisualAsync(card, res);
            ComputeMechanical(card, res);

            return res;
        }

        private long ComputeSalonAvg(VehicleCard card, ValuationResult res, out double? baseSalonDm3)
        {
            baseSalonDm3 = null;

            var vehicleName = card.ModelRaw;
            var idx = vehicleName.LastIndexOf('(');
            if (idx > 0) vehicleName = vehicleName.Substring(0, idx).Trim();

            var vehicleKey = TextNorm.NormalizeKey(vehicleName);
            var engineKey = TextNorm.NormalizeKey(card.EngineRaw);

            var strict = _cat.Salon
                .Where(r =>
                    TextNorm.NormalizeKey(r.Vehicle) == vehicleKey &&
                    TextNorm.NormalizeKey(r.Engine) == engineKey)
                .ToList();

            if (strict.Count > 0)
            {
                baseSalonDm3 = ExtractMinDm3(strict.Select(x => x.Engine));
                return (long)Math.Round(strict.Select(x => x.Price).Average());
            }

            var byVehicle = _cat.Salon
                .Where(r => TextNorm.NormalizeKey(r.Vehicle) == vehicleKey)
                .ToList();

            if (byVehicle.Count == 0)
            {
                var bmKey = TextNorm.NormalizeKey(card.BaseModel);
                byVehicle = _cat.Salon
                    .Where(r => TextNorm.NormalizeKey(r.Vehicle) == bmKey)
                    .ToList();
            }

            if (byVehicle.Count == 0)
            {
                res.MissingPrices.Add($"Salon: brak w salon_prices.csv dla '{vehicleName}' (szukam po nazwie auta)");
                return 0;
            }

            baseSalonDm3 = ExtractMinDm3(byVehicle.Select(x => x.Engine));
            return (long)Math.Round(byVehicle.Select(x => x.Price).Average());
        }

        private double? ExtractMinDm3(IEnumerable<string> engines)
        {
            double? min = null;

            foreach (var e in engines)
            {
                if (string.IsNullOrWhiteSpace(e)) continue;

                if (VehicleCardParser.TryParseEngineDisplacementDm3(e, out var dm3))
                {
                    if (!min.HasValue || dm3 < min.Value) min = dm3;
                    continue;
                }

                if (double.TryParse(
                    e.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var dm3plain))
                {
                    if (!min.HasValue || dm3plain < min.Value) min = dm3plain;
                }
            }

            return min;
        }

        private List<string> GetEngineModelCandidates(VehicleCard card)
        {
            var list = new List<string>();

            void Add(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return;
                var k = TextNorm.NormalizeKey(s);
                if (!string.IsNullOrWhiteSpace(k) && !list.Contains(k))
                    list.Add(k);
            }

            Add(card.BaseModel);

            var vehicleName = card.ModelRaw;
            var idx = vehicleName.LastIndexOf('(');
            if (idx > 0) vehicleName = vehicleName.Substring(0, idx).Trim();
            Add(vehicleName);

            var firstToken = vehicleName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            Add(firstToken);

            var firstTokenBm = (card.BaseModel ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            Add(firstTokenBm);

            return list;
        }

        private (long basePrice, long marketPrice) ComputeEngineUpgrade(VehicleCard card, ValuationResult res, double? baseSalonDm3)
        {
            if (!VehicleCardParser.TryParseEngineDisplacementDm3(card.EngineRaw, out var currentDm3))
            {
                res.MissingPrices.Add($"Silnik: nie umiem odczytać dm³ z '{card.EngineRaw}'");
                return (0, 0);
            }

            if (!baseSalonDm3.HasValue)
            {
                var single = GetEngineUpgradePriceForDm3(card, res, currentDm3);
                return (single, (long)Math.Round(single * 0.5));
            }

            var baseDm3 = baseSalonDm3.Value;

            if (currentDm3 <= baseDm3 + 0.0001)
                return (0, 0);

            var candidates = GetEngineModelCandidates(card);

            var steps = _cat.EngineUpgrades
                .Where(r =>
                {
                    var keys = r.ModelKeys.Split(',')
                        .Select(k => TextNorm.NormalizeKey(k))
                        .ToList();
                    return keys.Any(k => candidates.Contains(k));
                })
                .OrderBy(r => r.From)
                .ThenBy(r => r.To)
                .ToList();

            if (steps.Count == 0)
            {
                res.MissingPrices.Add($"Silnik: brak wpisu w engine_upgrades.csv dla modelu '{card.BaseModel}' (kandydaci: {string.Join(", ", candidates)})");
                return (0, 0);
            }

            var sum = SumEngineUpgradeSteps(steps, baseDm3, currentDm3, card, res);
            return (sum, (long)Math.Round(sum * 0.5));
        }

        private long SumEngineUpgradeSteps(List<EngineUpgradeRow> steps, double baseDm3, double targetDm3, VehicleCard card, ValuationResult res)
        {
            long sum = 0;

            var selected = steps
                .Where(s => s.From >= baseDm3 - 0.0001 && s.To <= targetDm3 + 0.0001)
                .OrderBy(s => s.From)
                .ThenBy(s => s.To)
                .ToList();

            if (selected.Count == 0)
            {
                res.MissingPrices.Add($"Silnik: brak kroków upgrade w engine_upgrades.csv dla '{card.BaseModel}' od {baseDm3:0.##} do {targetDm3:0.##} dm³");
                return 0;
            }

            double cur = baseDm3;

            foreach (var s in selected)
            {
                if (s.From > cur + 0.0002)
                    res.MissingPrices.Add($"Silnik: dziura w krokach upgrade dla '{card.BaseModel}' (oczekiwane od {cur:0.##}, a jest od {s.From:0.##})");

                sum += s.Price;
                cur = s.To;
            }

            if (cur < targetDm3 - 0.0002)
                res.MissingPrices.Add($"Silnik: kroki upgrade nie dochodzą do {targetDm3:0.##} dm³ (doszły do {cur:0.##}) dla '{card.BaseModel}'");

            return sum;
        }

        private long GetEngineUpgradePriceForDm3(VehicleCard card, ValuationResult res, double dm3)
        {
            var candidates = GetEngineModelCandidates(card);

            var candidatesRows = _cat.EngineUpgrades.Where(r =>
            {
                var keys = r.ModelKeys.Split(',')
                    .Select(k => TextNorm.NormalizeKey(k))
                    .ToList();
                return keys.Any(k => candidates.Contains(k));
            }).ToList();

            if (candidatesRows.Count == 0)
            {
                res.MissingPrices.Add($"Silnik: brak wpisu w engine_upgrades.csv dla modelu '{card.BaseModel}' (kandydaci: {string.Join(", ", candidates)})");
                return 0;
            }

            var match = candidatesRows.FirstOrDefault(r => dm3 >= r.From && dm3 <= r.To);
            if (match == null)
            {
                res.MissingPrices.Add($"Silnik: brak przedziału dla {dm3:0.##}dm³ w '{card.BaseModel}'");
                return 0;
            }

            return match.Price;
        }

        private async Task ComputeBodykitsAsync(VehicleCard card, ValuationResult res)
        {
            if (!string.IsNullOrWhiteSpace(card.BodykitMainName))
            {
                if (_cat.TryGetBodykit(card.BaseModel, card.BodykitMainName, out var bk))
                {
                    var mult = bk.Level >= 40 ? 1.0 : 0.5;
                    res.Bodykits.Add(($"{card.BaseModel} {bk.Name}", bk.Price, (long)Math.Round(bk.Price * mult), bk.Level >= 40 ? "(100%)" : "(50%)"));
                }
            }

            if (!string.IsNullOrWhiteSpace(card.BodykitAeroName))
            {
                if (_cat.TryGetBodykit("Spoiler", card.BodykitAeroName, out var bk))
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
                    if (_cat.VisualById.TryGetValue(v.Id, out var idPrice))
                        res.VisualItems.Add(($"{v.Name} ({v.Id})", idPrice, (long)Math.Round(idPrice * 0.5)));
                    else
                        res.MissingPrices.Add($"Wizualne ID: brak ceny dla {v.Id} ({v.Name})");

                    continue;
                }

                var s = TextNorm.Normalize(v.Name);

                var widen = System.Text.RegularExpressions.Regex.Match(
                    s,
                    @"^Poszerzenia\s*\(\s*(?<f>\d)\s*,\s*(?<r>\d)\s*\)\s*$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (widen.Success)
                {
                    var f = widen.Groups["f"].Value;
                    var r = widen.Groups["r"].Value;

                    var keyF = TextNorm.NormalizeKey("poszerzenia_przod:" + f);
                    if (_cat.VisualByName.TryGetValue(keyF, out var priceF))
                        res.VisualItems.Add(($"Poszerzenia przód ({f})", priceF, (long)Math.Round(priceF * 0.5)));
                    else
                        res.MissingPrices.Add($"Wizualne: brak ceny dla 'Poszerzenia przód ({f})' (klucz '{keyF}')");

                    var keyR = TextNorm.NormalizeKey("poszerzenia_tyl:" + r);
                    if (_cat.VisualByName.TryGetValue(keyR, out var priceR))
                        res.VisualItems.Add(($"Poszerzenia tył ({r})", priceR, (long)Math.Round(priceR * 0.5)));
                    else
                        res.MissingPrices.Add($"Wizualne: brak ceny dla 'Poszerzenia tył ({r})' (klucz '{keyR}')");

                    continue;
                }

                string mappedKey;
                string key;

                if (TryMapSpecialVisualName(v.Name, out mappedKey))
                    key = TextNorm.NormalizeKey(mappedKey);
                else
                    key = TextNorm.NormalizeKey(v.Name);

                if (_cat.VisualByName.TryGetValue(key, out var namePrice))
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

                if (!_cat.VisualByName.TryGetValue(key, out var p))
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

        // ======= KLUCZ: solidne wykrywanie "Przeniesienie/Zmiana napędu (AWD/FWD/RWD)" =======
        private static bool TryParseDriveChange(string s, out string mode)
        {
            mode = null;
            if (string.IsNullOrWhiteSpace(s)) return false;

            // próbujemy na kilku wariantach tekstu (bo Normalize/Key mogą zmieniać znaki)
            var variants = new[]
            {
                s,
                TextNorm.Normalize(s),
                ToAsciiPl(TextNorm.Normalize(s)),
                ToAsciiPl(s)
            };

            foreach (var v in variants)
            {
                var m = System.Text.RegularExpressions.Regex.Match(
                    v,
                    @"\b(zmiana|przeniesienie)\s+napedu\s*\(\s*(awd|fwd|rwd)\s*\)\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (m.Success)
                {
                    mode = m.Groups[2].Value.ToLowerInvariant(); // awd/fwd/rwd
                    return true;
                }
            }

            return false;
        }

        private static bool IsMznItem(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;

            var variants = new[]
            {
                s,
                TextNorm.Normalize(s),
                ToAsciiPl(TextNorm.Normalize(s)),
                ToAsciiPl(s)
            };

            foreach (var v in variants)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        v,
                        @"^\s*(modul|moduł)\s+zmiany\s+napedu\s*$|^\s*mzn\s*$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    return true;
            }

            return false;
        }

        private void ComputeMechanical(VehicleCard card, ValuationResult res)
        {
            // jeśli jest drive-change, nie pokazuj MZN
            bool hasDriveChange = card.MechanicalTuningRaw.Any(r => TryParseDriveChange(r, out _));

            foreach (var raw in card.MechanicalTuningRaw)
            {
                var name = TextNorm.Normalize(raw);

                // ======================
                // Opony (...) -> wizualne
                // ======================
                var tires = System.Text.RegularExpressions.Regex.Match(
                    name,
                    @"^Opony\s*\(\s*(?<v>.+?)\s*\)\s*$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (tires.Success)
                {
                    var v = TextNorm.NormalizeKey(tires.Groups["v"].Value);
                    v = ToAsciiPl(v);

                    if (v.Contains("sport")) v = "sportowe";
                    else if (v.Contains("teren")) v = "terenowe";
                    else if (v.Contains("drift")) v = "driftowe";

                    var tireKey = TextNorm.NormalizeKey("opony:" + v);

                    if (_cat.VisualByName.TryGetValue(tireKey, out var tireBase))
                    {
                        var tireMarket = (long)Math.Round(tireBase * 0.5);
                        res.VisualItems.Add(($"Opony ({FirstUpper(v)})", tireBase, tireMarket));
                    }
                    else
                    {
                        res.MissingPrices.Add($"Wizualne: brak ceny dla '{name}' (klucz '{tireKey}')");
                    }

                    continue;
                }

                // ======================
                // Drive change -> mech key "naped:awd/fwd/rwd"
                // ======================
                if (TryParseDriveChange(raw, out var mode) || TryParseDriveChange(name, out mode))
                {
                    var keyDrive = $"naped:{mode}";

                    if (_cat.MechByKey.TryGetValue(keyDrive, out var baseDrive))
                    {
                        var marketDrive = (long)Math.Round(baseDrive * 0.5);
                        res.MechItems.Add((TextNorm.Normalize(raw), baseDrive, marketDrive, " (50%)"));
                    }
                    else
                    {
                        res.MissingPrices.Add($"Mechaniczne: brak ceny dla '{TextNorm.Normalize(raw)}' (klucz '{keyDrive}')");
                    }

                    continue;
                }

                // ======================
                // jeśli jest drive-change, ukryj MZN
                // ======================
                if (hasDriveChange && IsMznItem(raw))
                    continue;

                // ======================
                // Standard mechaniczne
                // ======================
                var key = TextNorm.NormalizeKey(name);
                key = ToAsciiPl(key);

                if (MechAliases.TryGetValue(key, out var alias))
                    key = TextNorm.NormalizeKey(alias);

                var tank = System.Text.RegularExpressions.Regex.Match(
                    key,
                    @"powiekszony\s+bak\s*\(\s*(\d{2,3})l\s*\)");
                if (tank.Success)
                    key = $"bak_paliwa:{tank.Groups[1].Value}l";

                var lpg = System.Text.RegularExpressions.Regex.Match(
                    key,
                    @"butla\s+lpg\s*\(\s*(\d{2,3})l\s*\)");
                if (lpg.Success)
                    key = $"lpg:{lpg.Groups[1].Value}l";

                // MZN (jeśli naprawdę wpisany jako item i NIE ma drive-change)
                if (key.Contains("modul_zmiany_napedu") || key.Contains("modul zmiany napedu") || key == "mzn")
                    key = "mzn";

                if (key == "aplikacja transportowa" || key == "aplikacja_transportowa")
                    key = "aplikacja_transportowa";

                key = NormalizeMechKey(key);

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

        private static string FirstUpper(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            if (s.Length == 1) return s.ToUpperInvariant();
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
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

        private static bool TryMapSpecialVisualName(string rawName, out string mappedKey)
        {
            mappedKey = "";
            if (string.IsNullOrWhiteSpace(rawName)) return false;

            var s = TextNorm.Normalize(rawName);

            var tint = System.Text.RegularExpressions.Regex.Match(
                s,
                @"^Przyciemnienie\s+szyb\s*\(\s*(?<p>\d{1,3})\s*%\s*\)\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (tint.Success)
            {
                mappedKey = "przyciemnienie_szyb:" + tint.Groups["p"].Value;
                return true;
            }

            var rimSize = System.Text.RegularExpressions.Regex.Match(
                s,
                @"^Rozmiar\s+felg\s*\(\s*(?<v>.+?)\s*\)\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (rimSize.Success)
            {
                var v = TextNorm.NormalizeKey(rimSize.Groups["v"].Value);

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
                .Replace("ą", "a").Replace("ć", "c").Replace("ę", "e")
                .Replace("ł", "l").Replace("ń", "n").Replace("ó", "o")
                .Replace("ś", "s").Replace("ż", "z").Replace("ź", "z");
        }
    }
}
