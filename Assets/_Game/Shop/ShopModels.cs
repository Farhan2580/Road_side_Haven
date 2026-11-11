using System;
using System.Collections.Generic;

namespace RoadsideHaven
{
    /// <summary>Validated immutable configuration copied from a product asset at session start.</summary>
    public sealed class ShopProduct
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int PurchaseCost { get; }
        public int SellingPrice { get; }
        public int ShelfCapacity { get; }
        public int InitialStock { get; }
        public int RestockQuantity { get; }

        public ShopProduct(string id, string displayName, int purchaseCost, int sellingPrice,
            int shelfCapacity, int initialStock, int restockQuantity)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Products require a stable ID and display name.");
            if (purchaseCost <= 0 || sellingPrice <= 0 || shelfCapacity <= 0 ||
                initialStock < 0 || initialStock > shelfCapacity || restockQuantity <= 0 ||
                restockQuantity > shelfCapacity || (long)purchaseCost * shelfCapacity > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(purchaseCost), "Invalid product price or inventory limits.");
            Id = id;
            DisplayName = displayName;
            PurchaseCost = purchaseCost;
            SellingPrice = sellingPrice;
            ShelfCapacity = shelfCapacity;
            InitialStock = initialStock;
            RestockQuantity = restockQuantity;
        }
    }

    /// <summary>Stock counts include unpaid reservations so returned items always fit on their shelf.</summary>
    public sealed class ShopStock
    {
        public ShopProduct Product { get; }
        public int Available { get; internal set; }
        public int Reserved { get; internal set; }
        public int TotalOwned => Available + Reserved;

        internal ShopStock(ShopProduct product)
        {
            Product = product;
            Available = product.InitialStock;
        }
    }

    /// <summary>Only Ready baskets can be scanned or paid; terminal states cannot be reused.</summary>
    public enum ShopBasketStatus { Shopping, Ready, Paid, Abandoned }

    /// <summary>One reserved unit, carrying the selling price captured at pickup.</summary>
    public sealed class ShopBasketItem
    {
        public string ProductId { get; }
        public string DisplayName { get; }
        public int UnitPrice { get; }
        public bool Scanned { get; internal set; }

        internal ShopBasketItem(ShopProduct product)
        {
            ProductId = product.Id;
            DisplayName = product.DisplayName;
            UnitPrice = product.SellingPrice;
        }
    }

    /// <summary>Plain basket state. All mutations pass through its owning ShopLedger.</summary>
    public sealed class ShopBasket
    {
        internal readonly List<ShopBasketItem> MutableItems = new List<ShopBasketItem>(3);
        public string Id { get; }
        public IReadOnlyList<ShopBasketItem> Items { get; }
        public int Total { get; internal set; }
        public ShopBasketStatus Status { get; internal set; }
        public bool AllScanned
        {
            get
            {
                if (MutableItems.Count == 0) return false;
                for (int i = 0; i < MutableItems.Count; i++)
                    if (!MutableItems[i].Scanned) return false;
                return true;
            }
        }

        internal ShopBasket(string id)
        {
            Id = id;
            Items = MutableItems.AsReadOnly();
        }
    }

    /// <summary>Exact quantity and total displayed before the player requests restocking.</summary>
    public readonly struct RestockQuote
    {
        public string ProductId { get; }
        public int Quantity { get; }
        public int TotalCost { get; }

        public RestockQuote(string productId, int quantity, int totalCost)
        {
            ProductId = productId;
            Quantity = quantity;
            TotalCost = totalCost;
        }
    }

    /// <summary>
    /// Main-thread shop transactions. Reservations, abandonment and payment conserve inventory.
    /// Terminal baskets are removed from the active registry so long sessions do not accumulate customer records.
    /// </summary>
    public sealed class ShopLedger
    {
        private readonly IEconomyService economy;
        private readonly Dictionary<string, ShopStock> stockById = new Dictionary<string, ShopStock>();
        private readonly Dictionary<string, ShopBasket> activeBaskets = new Dictionary<string, ShopBasket>();
        private bool transactionInProgress;
        public IReadOnlyList<ShopStock> Stocks { get; }

        public ShopLedger(ShopProduct[] products, IEconomyService economy)
        {
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
            if (products == null || products.Length == 0)
                throw new ArgumentException("At least one product is required.", nameof(products));
            var stocks = new List<ShopStock>(products.Length);
            for (int i = 0; i < products.Length; i++)
            {
                var product = products[i];
                if (product == null || stockById.ContainsKey(product.Id))
                    throw new ArgumentException("Products must be non-null and have unique IDs.", nameof(products));
                var stock = new ShopStock(product);
                stockById.Add(product.Id, stock);
                stocks.Add(stock);
            }
            Stocks = stocks.AsReadOnly();
        }

        public ShopBasket CreateBasket(string customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId))
                throw new ArgumentException("A customer ID is required.", nameof(customerId));
            if (transactionInProgress || activeBaskets.ContainsKey(customerId))
                throw new InvalidOperationException("This customer already has a basket or a transaction is in progress.");
            var basket = new ShopBasket(customerId);
            activeBaskets.Add(customerId, basket);
            return basket;
        }

        /// <summary>Moves exactly one unit from available stock to this customer's basket.</summary>
        public bool TryReserve(ShopBasket basket, string productId)
        {
            if (transactionInProgress || !Owns(basket) || basket.Status != ShopBasketStatus.Shopping ||
                productId == null || !stockById.TryGetValue(productId, out ShopStock stock) ||
                stock.Available <= 0 || (long)basket.Total + stock.Product.SellingPrice > int.MaxValue)
                return false;
            var item = new ShopBasketItem(stock.Product);
            basket.MutableItems.Add(item);
            basket.Total += item.UnitPrice;
            stock.Available--;
            stock.Reserved++;
            return true;
        }

        /// <summary>Called only when the customer has arrived at the checkout queue.</summary>
        public bool MarkReady(ShopBasket basket)
        {
            if (transactionInProgress || !Owns(basket) || basket.Status != ShopBasketStatus.Shopping ||
                basket.Items.Count == 0) return false;
            basket.Status = ShopBasketStatus.Ready;
            return true;
        }

        public bool TryScan(ShopBasket basket, int itemIndex)
        {
            if (transactionInProgress || !Owns(basket) || basket.Status != ShopBasketStatus.Ready ||
                itemIndex < 0 || itemIndex >= basket.Items.Count || basket.Items[itemIndex].Scanned)
                return false;
            basket.MutableItems[itemIndex].Scanned = true;
            return true;
        }

        /// <summary>Credits the exact basket total once, after every item has been scanned.</summary>
        public bool TryPay(ShopBasket basket)
        {
            if (transactionInProgress || !Owns(basket) || basket.Status != ShopBasketStatus.Ready ||
                !basket.AllScanned || (long)economy.Coins + basket.Total > int.MaxValue)
                return false;
            transactionInProgress = true;
            try
            {
                // The economy contract commits or throws before mutation. Guard against reentrant UI callbacks.
                economy.Credit(basket.Total);
                for (int i = 0; i < basket.Items.Count; i++)
                    stockById[basket.Items[i].ProductId].Reserved--;
                basket.Status = ShopBasketStatus.Paid;
                activeBaskets.Remove(basket.Id);
                return true;
            }
            finally { transactionInProgress = false; }
        }

        /// <summary>Returns all unpaid units exactly once, including any already scanned units.</summary>
        public bool Abandon(ShopBasket basket)
        {
            if (transactionInProgress || !Owns(basket) ||
                (basket.Status != ShopBasketStatus.Shopping && basket.Status != ShopBasketStatus.Ready))
                return false;
            for (int i = 0; i < basket.Items.Count; i++)
            {
                ShopStock stock = stockById[basket.Items[i].ProductId];
                stock.Reserved--;
                stock.Available++;
            }
            basket.Status = ShopBasketStatus.Abandoned;
            activeBaskets.Remove(basket.Id);
            return true;
        }

        public RestockQuote GetRestockQuote(string productId)
        {
            if (productId == null || !stockById.TryGetValue(productId, out ShopStock stock))
                return new RestockQuote(productId, 0, 0);
            int quantity = Math.Min(stock.Product.RestockQuantity, stock.Product.ShelfCapacity - stock.TotalOwned);
            return new RestockQuote(productId, quantity, quantity * stock.Product.PurchaseCost);
        }

        /// <summary>Revalidates the displayed quote and affordability before changing coins or stock.</summary>
        public bool TryRestock(RestockQuote quote)
        {
            if (transactionInProgress || quote.ProductId == null || quote.Quantity <= 0 ||
                !stockById.TryGetValue(quote.ProductId, out ShopStock stock)) return false;
            RestockQuote current = GetRestockQuote(quote.ProductId);
            if (quote.Quantity != current.Quantity || quote.TotalCost != current.TotalCost) return false;
            transactionInProgress = true;
            try
            {
                if (!economy.TryDebit(quote.TotalCost)) return false;
                stock.Available += quote.Quantity;
                return true;
            }
            finally { transactionInProgress = false; }
        }

        /// <summary>Accepts already purchased owner cargo atomically; reservations still consume capacity.</summary>
        public bool TryAcceptDelivery(string productId, int quantity)
        {
            if (transactionInProgress || quantity <= 0 || productId == null ||
                !stockById.TryGetValue(productId, out var stock) ||
                quantity > stock.Product.ShelfCapacity - stock.TotalOwned) return false;
            stock.Available += quantity;
            return true;
        }
        private bool Owns(ShopBasket basket) => basket != null &&
            activeBaskets.TryGetValue(basket.Id, out ShopBasket owned) && ReferenceEquals(owned, basket);
    }
}
