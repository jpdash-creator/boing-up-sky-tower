using UnityEngine;
using UnityEngine.UI;

namespace BoingUpSkyTower.Gameplay
{
    /// <summary>The Subscription button of the shop header (docs/26 section 2). Kept in its own file so a restyle of the header keeps one call: AddSubscriptionButton().</summary>
    public sealed partial class ShopScreen
    {
        /// <summary>
        /// A 140 px blue button with the crown (ticket) icon at the top right, under the wallet pills and over the shop scene; opens the Subscription
        /// page. Skipped when the host has no OpenSubscription. Its accessibility label is "Subscription". More than 8 dp from the wallet row (118 px above it).
        /// </summary>
        void AddSubscriptionButton()
        {
            if (host == null || host.OpenSubscription == null) return;
            Sprite icon = ToyKit.Get("icon_ticket_monthly") ?? ToyKit.Get("icon_crown");
            Button b = ToyKit.IconButton(screen, "SubscriptionButton", "Subscription", 2, icon, new Vector2(140f, 140f), host.OpenSubscription, 0.62f);
            UiKit.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(-24f, -150f), new Vector2(140f, 140f));
        }
    }
}
