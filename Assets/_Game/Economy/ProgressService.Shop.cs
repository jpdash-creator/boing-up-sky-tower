using System.Collections.Generic;
using FluffyClimbers.Content;

namespace FluffyClimbers.Economy
{
    /// <summary>First-purchase double of the gem packs (docs/22 4.1). Switch OFF by default; the flag is stored per pack in the account's save.</summary>
    public sealed partial class ProgressService
    {
        /// <summary>
        /// First-purchase switch (default OFF, owner 2026-10-25). While on, the first purchase of each gem pack of 50 to 1,000 gems on this
        /// account credits double the gems, once per pack. The game turns it on from GameSettings.FirstPurchaseDouble.
        /// </summary>
        public bool FirstPurchaseDoubleActive { get; set; }

        /// <summary>Saves written before the first-purchase flag (or with a null list) get an empty list.</summary>
        List<string> FirstPurchaseList
        {
            get
            {
                if (Data.FirstPurchase == null) Data.FirstPurchase = new List<string>();
                return Data.FirstPurchase;
            }
        }

        /// <summary>True when this pack already received its first-purchase double on this account.</summary>
        public bool FirstPurchaseUsed(string packId)
        {
            return packId != null && FirstPurchaseList.Contains(packId);
        }

        /// <summary>True when buying this pack now would double its gems: switch on, pack eligible (50 to 1,000 gems) and not bought before.</summary>
        public bool IsFirstPurchaseAvailable(GemPackDef pack)
        {
            return FirstPurchaseDoubleActive && pack != null && pack.Gems > 0 && pack.FirstPurchaseDouble && !FirstPurchaseUsed(pack.Id);
        }

        /// <summary>Gems the next purchase of this pack credits: double the pack while the first-purchase double is available, the plain amount otherwise.</summary>
        public int GemsFromPack(GemPackDef pack)
        {
            if (pack == null || pack.Gems <= 0) return 0;
            long gems = IsFirstPurchaseAvailable(pack) ? 2L * pack.Gems : pack.Gems;
            return gems > int.MaxValue ? int.MaxValue : (int)gems;
        }

        /// <summary>
        /// Credits a paid gem pack (a TEST purchase today, the server-validated purchase later): the gems, doubled the first time when the
        /// switch is on, and the once-only flag, in one step and one save. Returns Invalid for an empty pack and changes nothing.
        /// </summary>
        public ShopResult CreditGemPack(GemPackDef pack)
        {
            if (pack == null || pack.Gems <= 0) return ShopResult.Invalid;
            int gems = GemsFromPack(pack);
            if (IsFirstPurchaseAvailable(pack)) FirstPurchaseList.Add(pack.Id);
            Data.Gems = SaturatingAdd(Data.Gems, gems);
            Commit();
            return ShopResult.Ok;
        }
    }
}
