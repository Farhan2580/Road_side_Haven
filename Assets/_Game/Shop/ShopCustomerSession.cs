namespace RoadsideHaven
{
    /// <summary>Explicit walk-in lifecycle; the actor owns navigation, this object owns runtime timing.</summary>
    public enum ShopCustomerState { ToShelf, Browsing, ToCounter, Waiting, Leaving }

    public sealed class ShopCustomerSession
    {
        public ShopBasket Basket { get; }
        public ShopCustomerState State { get; set; }
        public int DesiredItems { get; }
        public int ShelfIndex { get; set; }
        public int QueueSlot { get; set; } = -1;
        public float BrowseRemaining { get; set; }
        public float PatienceRemaining { get; set; }
        public float NavigationDeadline { get; set; }

        public ShopCustomerSession(ShopBasket basket, int desiredItems)
        {
            Basket = basket;
            DesiredItems = desiredItems;
        }
    }
}
