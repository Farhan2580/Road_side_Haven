using System;
using System.Collections.Generic;

namespace RoadsideHaven
{
    /// <summary>Scene-scoped checkout requests. Commands carry the basket or quote the player actually saw.</summary>
    public interface IShopService
    {
        ShopSnapshot GetSnapshot();
        RestockQuote GetRestockQuote(string productId);
        bool TryScan(string basketId, int itemIndex);
        bool TryCollectPayment(string basketId);
        bool TryRestock(RestockQuote quote);
    }

    /// <summary>Read-only view of the shop; basket and stock models expose no public mutation methods.</summary>
    public readonly struct ShopSnapshot
    {
        public readonly int Coins;
        public readonly int CustomerCount;
        public readonly int WaitingCount;
        public readonly ShopBasket Basket;
        public readonly float PatienceRemaining;
        public readonly IReadOnlyList<ShopStock> Stocks;

        public ShopSnapshot(int coins, int customerCount, int waitingCount, ShopBasket basket,
            float patienceRemaining, IReadOnlyList<ShopStock> stocks)
        {
            Coins = coins;
            CustomerCount = customerCount;
            WaitingCount = waitingCount;
            Basket = basket;
            PatienceRemaining = patienceRemaining;
            Stocks = stocks;
        }
    }

    /// <summary>Typed shop notifications owned by the scene's shop controller.</summary>
    public sealed class ShopEventHub
    {
        public event Action<ShopSnapshot> Changed;
        public event Action<HotelFeedback> Feedback;

        public void Publish(ShopSnapshot snapshot) => Changed?.Invoke(snapshot);
        public void Publish(HotelFeedback feedback) => Feedback?.Invoke(feedback);
    }
}
