using System;
using System.Collections.Generic;

namespace _777bot
{
    public enum PendingKind
    {
        WaitingForVehiclePaste,
        WaitingForSpecialColorPrices
    }

    public class PendingWycenaState
    {
        public PendingKind Kind { get; set; }
        public int Vuid { get; set; }
        public ulong UserId { get; set; }
        public ulong ChannelId { get; set; }
        public DateTime ExpiresAtUtc { get; set; }

        // when asking for special color prices:
        public List<(SpecialColorType type, string name, string rarity)> MissingSpecialColors { get; set; }
            = new List<(SpecialColorType type, string name, string rarity)>();
    }
}
