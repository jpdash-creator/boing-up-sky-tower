using BoingUpSkyTower.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>
    /// The shop's first section: one simple card that links to the Treasure Chest screen (docs/22 owner answer 2). Buying and opening chests lives in
    /// <see cref="TreasureChestScreen"/>. Menu screen only: no per-frame code.
    /// </summary>
    public sealed partial class ShopScreen
    {
        const float TreasureCardH = 700f;

        void BuildChests()
        {
            RectTransform list = MakeList(TreasureCardH + 8f, ListTop);
            RectTransform card = UiKit.Rect(list, "Panel");
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.offsetMin = new Vector2(SideMargin, -TreasureCardH);
            card.offsetMax = new Vector2(-SideMargin, 0f);
            Theme.CardBacking(card);

            AddChestPicture(card, ChestKind.Iron, 0.03f, 0.26f);
            AddChestPicture(card, ChestKind.Golden, 0.25f, 0.48f);

            Text title = UiKit.AddText(card, "Title", "Treasure chests", 50, Theme.Ink, TextAnchor.UpperLeft, true);
            DropOutline(title);
            title.rectTransform.anchorMin = new Vector2(0.5f, 0.74f);
            title.rectTransform.anchorMax = new Vector2(0.97f, 0.96f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            Text desc = UiKit.AddText(card, "Info", "Open chests for power-ups and coins.", 42, Theme.InkSoft, TextAnchor.UpperLeft);
            DropOutline(desc);
            desc.rectTransform.anchorMin = new Vector2(0.5f, 0.30f);
            desc.rectTransform.anchorMax = new Vector2(0.97f, 0.74f);
            desc.rectTransform.offsetMin = desc.rectTransform.offsetMax = Vector2.zero;

            Button open = UiKit.AddButton(card, "OpenTreasure", "Open", ToyKit.Button(1), Color.white, new Vector2(340f, ButtonH), () => { if (host.OpenTreasureChests != null) host.OpenTreasureChests(); }, 44);
            open.GetComponent<A11yLabel>().Text = "Open the treasure chests screen";
            var rt = (RectTransform)open.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.97f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0f, 24f);
            rt.offsetMax = new Vector2(0f, 24f + ButtonH);
        }

        static void AddChestPicture(RectTransform card, ChestKind kind, float x0, float x1)
        {
            Sprite chest = TreasureChestScreen.ChestArt(kind);
            Image pic = UiKit.AddImage(card, kind + "Chest", chest, chest != null ? Color.white : new Color(0.7f, 0.5f, 0.3f, 1f));
            pic.preserveAspect = true;
            pic.rectTransform.anchorMin = new Vector2(x0, 0.25f);
            pic.rectTransform.anchorMax = new Vector2(x1, 0.8f);
            pic.rectTransform.offsetMin = pic.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
