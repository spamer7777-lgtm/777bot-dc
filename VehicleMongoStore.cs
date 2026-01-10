using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace _777bot
{
    public class VehicleMongoStore
    {
        private readonly IMongoDatabase _db;
        private readonly IMongoCollection<BsonDocument> _vehicles;
        private readonly IMongoCollection<BsonDocument> _specialColors;

        public VehicleMongoStore()
        {
            var mongoUrl = Environment.GetEnvironmentVariable("MONGO_URL") ?? "";
            var mongoDbName = Environment.GetEnvironmentVariable("MONGO_DB") ?? "777bot";

            if (string.IsNullOrWhiteSpace(mongoUrl))
                throw new Exception("Brak MONGO_URL w env.");

            if (!mongoUrl.Contains("authSource=", StringComparison.OrdinalIgnoreCase))
            {
                mongoUrl += mongoUrl.Contains('?') ? "&authSource=admin" : "?authSource=admin";
            }

            var client = new MongoClient(mongoUrl);
            _db = client.GetDatabase(mongoDbName);

            _vehicles = _db.GetCollection<BsonDocument>("vehicles_cache");
            _specialColors = _db.GetCollection<BsonDocument>("special_color_prices");
        }

        public async Task<VehicleCard> GetVehicleAsync(int vuid)
        {
            var f = Builders<BsonDocument>.Filter.Eq("vuid", vuid);
            var doc = await _vehicles.Find(f).FirstOrDefaultAsync();
            if (doc == null) return null;

            var card = new VehicleCard
            {
                Vuid = vuid,
                ModelRaw = doc.GetValue("modelRaw", "").AsString,
                EngineRaw = doc.GetValue("engineRaw", "").AsString,
                BaseModel = doc.GetValue("baseModel", "").AsString,
                ModelId = doc.Contains("modelId") ? doc["modelId"].AsInt32 : 0,
                BodykitMainName = doc.Contains("bodykitMain") ? doc["bodykitMain"].AsString : "",
                BodykitAeroName = doc.Contains("bodykitAero") ? doc["bodykitAero"].AsString : "",
                LightsColorRaw = doc.Contains("lightsColor") ? doc["lightsColor"].AsString : "",
                DashboardColorRaw = doc.Contains("dashColor") ? doc["dashColor"].AsString : "",
            };

            if (doc.Contains("visual") && doc["visual"].IsBsonArray)
            {
                foreach (var v in doc["visual"].AsBsonArray)
                {
                    if (!v.IsBsonDocument) continue;
                    var d = v.AsBsonDocument;

                    card.VisualTuning.Add(new VisualItem
                    {
                        Raw = d.GetValue("raw", "").AsString,
                        Name = d.GetValue("name", "").AsString,
                        Id = d.Contains("id") && d["id"].IsInt32 ? d["id"].AsInt32 : 0
                    });
                }
            }

            if (doc.Contains("mech") && doc["mech"].IsBsonArray)
            {
                card.MechanicalTuningRaw = doc["mech"].AsBsonArray.Select(x => x.AsString).ToList();
            }

            return card;
        }

        public async Task UpsertVehicleAsync(VehicleCard card)
        {
            var doc = new BsonDocument
            {
                ["vuid"] = card.Vuid,
                ["modelRaw"] = card.ModelRaw,
                ["engineRaw"] = card.EngineRaw,
                ["baseModel"] = card.BaseModel,
                ["updatedAt"] = DateTime.UtcNow,
                ["modelId"] = card.ModelId
            };

            if (!string.IsNullOrWhiteSpace(card.BodykitMainName)) doc["bodykitMain"] = card.BodykitMainName;
            if (!string.IsNullOrWhiteSpace(card.BodykitAeroName)) doc["bodykitAero"] = card.BodykitAeroName;
            if (!string.IsNullOrWhiteSpace(card.LightsColorRaw)) doc["lightsColor"] = card.LightsColorRaw;
            if (!string.IsNullOrWhiteSpace(card.DashboardColorRaw)) doc["dashColor"] = card.DashboardColorRaw;

            doc["visual"] = new BsonArray(card.VisualTuning.Select(v =>
            {
                var vd = new BsonDocument
                {
                    ["raw"] = v.Raw,
                    ["name"] = v.Name
                };
                if (v.Id != 0) vd["id"] = v.Id;
                return vd;
            }));

            doc["mech"] = new BsonArray(card.MechanicalTuningRaw);

            var f = Builders<BsonDocument>.Filter.Eq("vuid", card.Vuid);
            await _vehicles.ReplaceOneAsync(f, doc, new ReplaceOptions { IsUpsert = true });
        }

        public async Task<long?> GetSpecialColorPriceAsync(SpecialColorType type, string name, string rarity)
        {
            var f = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("type", type.ToString()),
                Builders<BsonDocument>.Filter.Eq("nameKey", TextNorm.NormalizeKey(name)),
                Builders<BsonDocument>.Filter.Eq("rarity", rarity)
            );

            var doc = await _specialColors.Find(f).FirstOrDefaultAsync();
            if (doc == null) return null;
            if (!doc.Contains("price")) return null;
            return doc["price"].AsInt64;
        }

        public async Task UpsertSpecialColorPriceAsync(SpecialColorType type, string name, string rarity, long price, ulong addedByUserId)
        {
            var doc = new BsonDocument
            {
                ["type"] = type.ToString(),
                ["name"] = name,
                ["nameKey"] = TextNorm.NormalizeKey(name),
                ["rarity"] = rarity,
                ["price"] = price,
                ["updatedAt"] = DateTime.UtcNow,
                ["addedBy"] = (long)addedByUserId
            };

            var f = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("type", type.ToString()),
                Builders<BsonDocument>.Filter.Eq("nameKey", TextNorm.NormalizeKey(name)),
                Builders<BsonDocument>.Filter.Eq("rarity", rarity)
            );

            await _specialColors.ReplaceOneAsync(f, doc, new ReplaceOptions { IsUpsert = true });
        }
    }
}
