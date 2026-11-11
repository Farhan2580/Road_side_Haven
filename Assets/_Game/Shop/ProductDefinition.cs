using UnityEngine;

namespace RoadsideHaven
{
    /// <summary>Persistent product identity and designer tuning. Runtime stock lives in ShopLedger.</summary>
    [CreateAssetMenu(menuName = "Roadside Haven/Shop Product", fileName = "ShopProduct")]
    public sealed class ProductDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique ID. Keep this unchanged when renaming the displayed product.")]
        [SerializeField] private string id = "product";
        [Tooltip("Name shown on shelves and checkout buttons.")]
        [SerializeField] private string displayName = "Product";

        [Header("Prices and inventory")]
        [Tooltip("Integer coins paid to purchase one unit for restocking.")]
        [SerializeField, Min(1)] private int purchaseCost = 2;
        [Tooltip("Integer coins earned when one unit is sold.")]
        [SerializeField, Min(1)] private int sellingPrice = 5;
        [Tooltip("Maximum owned units, including units reserved in unpaid baskets.")]
        [SerializeField, Range(1, 100)] private int shelfCapacity = 12;
        [Tooltip("Available units at the start of each Play session.")]
        [SerializeField, Min(0)] private int initialStock = 8;
        [Tooltip("Maximum units bought by one restock request; limited by remaining capacity.")]
        [SerializeField, Min(1)] private int restockQuantity = 4;

        public string Id => id;
        public string DisplayName => displayName;
        public int PurchaseCost => purchaseCost;
        public int SellingPrice => sellingPrice;
        public int ShelfCapacity => shelfCapacity;
        public int InitialStock => initialStock;
        public int RestockQuantity => restockQuantity;

        /// <summary>Copies configuration into a validated plain runtime product.</summary>
        public ShopProduct ToProduct() => new ShopProduct(id, displayName, purchaseCost,
            sellingPrice, shelfCapacity, initialStock, restockQuantity);

        /// <summary>Sets defaults on a newly created asset. Builders must not call this on existing assets.</summary>
        public void ConfigureDefaults(string stableId, string name, int cost, int price,
            int capacity, int stock, int batchQuantity)
        {
            id = stableId;
            displayName = name;
            purchaseCost = cost;
            sellingPrice = price;
            shelfCapacity = capacity;
            initialStock = stock;
            restockQuantity = batchQuantity;
            OnValidate();
        }

        private void OnValidate()
        {
            // Never regenerate or derive IDs from names: existing references must remain stable.
            purchaseCost = Mathf.Clamp(purchaseCost, 1, 1000000);
            sellingPrice = Mathf.Clamp(sellingPrice, 1, 1000000);
            shelfCapacity = Mathf.Clamp(shelfCapacity, 1, 100);
            initialStock = Mathf.Clamp(initialStock, 0, shelfCapacity);
            restockQuantity = Mathf.Clamp(restockQuantity, 1, shelfCapacity);
        }
    }
}
