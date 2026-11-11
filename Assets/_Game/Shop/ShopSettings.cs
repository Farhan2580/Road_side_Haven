using UnityEngine;

namespace RoadsideHaven
{
    /// <summary>Bounded shop customer flow and product assortment for Android.</summary>
    [CreateAssetMenu(menuName = "Roadside Haven/Shop Settings", fileName = "ShopSettings")]
    public sealed class ShopSettings : ScriptableObject
    {
        [Header("Products")]
        [Tooltip("The four shop products. Each definition must have a unique stable ID.")]
        [SerializeField] private ProductDefinition[] products;

        [Header("Customer flow")]
        [Tooltip("Maximum simultaneous shop customers, including those leaving. Separate from motel guests.")]
        [SerializeField, Range(1, 6)] private int maximumCustomers = 4;
        [Tooltip("Seconds between attempts to spawn a shop customer.")]
        [SerializeField, Min(2f)] private float arrivalInterval = 12f;
        [Tooltip("Seconds a customer tolerates waiting to pay before returning their reserved items.")]
        [SerializeField, Min(5f)] private float patienceSeconds = 60f;
        [Tooltip("Seconds spent browsing at each product shelf.")]
        [SerializeField, Min(0.1f)] private float browseSeconds = 2f;
        [Tooltip("Maximum seconds for one navigation trip before abandoning the purchase.")]
        [SerializeField, Min(3f)] private float navigationTimeoutSeconds = 20f;
        [Tooltip("Maximum items in one basket. Every reserved item must be scanned individually.")]
        [SerializeField, Range(1, 3)] private int basketSize = 2;

        public ProductDefinition[] Products => products;
        public int MaximumCustomers => maximumCustomers;
        public float ArrivalInterval => arrivalInterval;
        public float PatienceSeconds => patienceSeconds;
        public float BrowseSeconds => browseSeconds;
        public float NavigationTimeoutSeconds => navigationTimeoutSeconds;
        public int BasketSize => basketSize;

        /// <summary>Sets the assortment on a newly created asset without affecting existing tuning.</summary>
        public void ConfigureDefaults(ProductDefinition[] definitions) => products = definitions;

        private void OnValidate()
        {
            maximumCustomers = Mathf.Clamp(maximumCustomers, 1, 6);
            arrivalInterval = Mathf.Max(2f, arrivalInterval);
            patienceSeconds = Mathf.Max(5f, patienceSeconds);
            browseSeconds = Mathf.Max(0.1f, browseSeconds);
            navigationTimeoutSeconds = Mathf.Max(3f, navigationTimeoutSeconds);
            basketSize = Mathf.Clamp(basketSize, 1, 3);
        }
    }
}
