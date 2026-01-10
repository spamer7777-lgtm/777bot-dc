using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace _777bot
{
    public static class VehicleCardParser
    {
        private static readonly Regex VuidRe = new Regex(@"^\s*VUID\s*(?:\r?\n|\s)+(?<vuid>\d+)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static readonly Regex ModelLineRe = new Regex(@"^\s*Model\s*(?:\r?\n|\s)+(?<val>.+?)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static readonly Regex EngineLineRe = new Regex(@"^\s*Silnik\s*(?:\r?\n|\s)+(?<val>.+?)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static readonly Regex VisualRe = new Regex(@"^\s*Tuning\s+wizualny\s*(?:\r?\n|\s)+(?<val>.+?)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static readonly Regex MechRe = new Regex(@"^\s*Tuning\s+mechaniczny\s*(?:\r?\n|\s)+(?<val>.+?)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static readonly Regex LightsColorRe = new Regex(
            @"^\s*Kolor\s+świateł[ \t]+(?<val>.+?)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static readonly Regex DashColorRe = new Regex(
            @"^\s*Kolor\s+licznika[ \t]+(?<val>.+?)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static readonly Regex ModelIdRe = new Regex(@"\((?<id>\d+)\)\s*$", RegexOptions.Compiled);
        private static readonly Regex AeroRe = new Regex(@"\bAero\s+(I|II|III|IV)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Dm3Re = new Regex(@"\((?<cc>[\d\.,]+)\s*dm", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool TryParse(string paste, out VehicleCard card, out string error)
        {
            card = new VehicleCard();
            error = "";

            paste = paste.Replace("\u00A0", " "); // nbsp

            var vuidM = VuidRe.Match(paste);
            if (!vuidM.Success || !int.TryParse(vuidM.Groups["vuid"].Value, out int vuid))
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

            // ✅ Fallback: układ tabelkowy/łamanie linii potrafi zepsuć regexy kolorów
            FixColorsFallback(paste, card);

            ParseModel(card);
            return true;
        }

        private static void FixColorsFallback(string paste, VehicleCard card)
        {
            // jeżeli złapaliśmy ewidentnie złą wartość (np. "Kolor licznika")
            bool lightsBad =
                string.IsNullOrWhiteSpace(card.LightsColorRaw) ||
                TextNorm.NormalizeKey(card.LightsColorRaw).StartsWith("kolor_licznika") ||
                TextNorm.NormalizeKey(card.LightsColorRaw).StartsWith("kolor_swiatel");

            bool dashBad =
                string.IsNullOrWhiteSpace(card.DashboardColorRaw) ||
                TextNorm.NormalizeKey(card.DashboardColorRaw).StartsWith("kolor_swiatel") ||
                TextNorm.NormalizeKey(card.DashboardColorRaw).StartsWith("kolor_licznika");

            if (!lightsBad && !dashBad) return;

            TryParseColorsByLines(paste, out var lights, out var dash);

            if (lightsBad && !string.IsNullOrWhiteSpace(lights))
                card.LightsColorRaw = lights;

            if (dashBad && !string.IsNullOrWhiteSpace(dash))
                card.DashboardColorRaw = dash;

            // dodatkowy bezpiecznik: jeśli po fallback nadal jest label — wyczyść
            if (TextNorm.NormalizeKey(card.LightsColorRaw).StartsWith("kolor_licznika") ||
                TextNorm.NormalizeKey(card.LightsColorRaw).StartsWith("kolor_swiatel"))
                card.LightsColorRaw = "";

            if (TextNorm.NormalizeKey(card.DashboardColorRaw).StartsWith("kolor_licznika") ||
                TextNorm.NormalizeKey(card.DashboardColorRaw).StartsWith("kolor_swiatel"))
                card.DashboardColorRaw = "";
        }

        private static void TryParseColorsByLines(string paste, out string lights, out string dash)
        {
            lights = "";
            dash = "";

            var lines = paste.Replace("\r", "")
                .Split('\n')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                if (line.StartsWith("Kolor świateł", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = line.Substring("Kolor świateł".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(rest))
                    {
                        lights = TextNorm.Normalize(rest);
                    }
                    else if (i + 1 < lines.Count)
                    {
                        var next = lines[i + 1];
                        if (!next.StartsWith("Kolor licznika", StringComparison.OrdinalIgnoreCase))
                            lights = TextNorm.Normalize(next);
                    }
                }

                if (line.StartsWith("Kolor licznika", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = line.Substring("Kolor licznika".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(rest))
                    {
                        dash = TextNorm.Normalize(rest);
                    }
                    else if (i + 1 < lines.Count)
                    {
                        var next = lines[i + 1];
                        if (!next.StartsWith("Kolor świateł", StringComparison.OrdinalIgnoreCase))
                            dash = TextNorm.Normalize(next);
                    }
                }
            }
        }

        private static List<string> SplitCommaList(string s)
        {
            var res = new List<string>();
            if (string.IsNullOrWhiteSpace(s)) return res;

            var sb = new System.Text.StringBuilder();
            int depth = 0;

            foreach (var ch in s)
            {
                if (ch == '(') depth++;
                else if (ch == ')') depth = Math.Max(0, depth - 1);

                if (ch == ',' && depth == 0)
                {
                    var part = TextNorm.Normalize(sb.ToString());
                    if (!string.IsNullOrWhiteSpace(part))
                        res.Add(part);

                    sb.Clear();
                    continue;
                }

                sb.Append(ch);
            }

            var last = TextNorm.Normalize(sb.ToString());
            if (!string.IsNullOrWhiteSpace(last))
                res.Add(last);

            return res;
        }

        private static List<VisualItem> ParseVisualList(string s)
        {
            var tokens = SplitCommaList(s);
            var list = new List<VisualItem>();

            foreach (var t in tokens)
            {
                var item = new VisualItem { Raw = t, Name = t };

                var m = Regex.Match(t, @"\((?<id>\d+)\)\s*$");
                if (m.Success && int.TryParse(m.Groups["id"].Value, out int id))
                {
                    item.Id = id;
                    item.Name = TextNorm.Normalize(Regex.Replace(t, @"\s*\(\d+\)\s*$", ""));
                }
                else
                {
                    item.Id = 0;
                    item.Name = t;
                }

                list.Add(item);
            }

            return list;
        }

        private static void ParseModel(VehicleCard card)
        {
            var raw = card.ModelRaw;

            var idM = ModelIdRe.Match(raw);
            if (idM.Success && int.TryParse(idM.Groups["id"].Value, out int mid))
            {
                card.ModelId = mid;
                raw = raw.Substring(0, idM.Index).Trim();
            }
            else
            {
                card.ModelId = 0;
            }

            var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            card.BaseModel = parts.Length > 0 ? parts[0] : raw;

            // aero token
            var aeroM = AeroRe.Match(raw);
            if (aeroM.Success)
            {
                var aeroToken = aeroM.Value;
                aeroToken = Regex.Replace(aeroToken, @"\s+", " ").Trim();
                aeroToken = "Aero " + aeroToken.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
                card.BodykitAeroName = aeroToken;

                raw = Regex.Replace(raw, @"\bAero\s+(I|II|III|IV)\b", "", RegexOptions.IgnoreCase).Trim();
                raw = Regex.Replace(raw, @"\s+", " ").Trim();
            }
            else
            {
                card.BodykitAeroName = "";
            }

            // main bodykit = everything after BaseModel
            if (raw.Length > card.BaseModel.Length)
            {
                var afterBase = raw.Substring(card.BaseModel.Length).Trim();
                card.BodykitMainName = afterBase;
            }
            else
            {
                card.BodykitMainName = "";
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

        public static (string name, string rarity) ParseColorWithRarity(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return ("", "");

            var s = TextNorm.Normalize(raw);
            var parts = s.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var name = parts[0].Trim();
                var tag = parts[1].Trim();

                if (tag.IndexOf("Limit", StringComparison.OrdinalIgnoreCase) >= 0) return (name, "Limitowane");
                if (tag.IndexOf("Unikat", StringComparison.OrdinalIgnoreCase) >= 0) return (name, "Unikatowe");
                return (name, tag);
            }

            return (s, "");
        }
    }
}
