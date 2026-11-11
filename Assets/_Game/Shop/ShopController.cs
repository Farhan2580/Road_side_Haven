using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RoadsideHaven
{
    /// <summary>Scene-scoped shop service. Composed by the motel root after its shared wallet is ready.</summary>
    [DisallowMultipleComponent]
    public sealed class ShopController : MonoBehaviour, IShopService
    {
        [Header("Configuration")]
        [Tooltip("Products, bounded customer flow, basket sizes, patience and travel deadlines.")]
        [SerializeField] private ShopSettings settings;
        [Tooltip("Shared shop customer material. Motel guests retain their existing appearance.")]
        [SerializeField] private Material customerMaterial;
        [Tooltip("Optional meter-scale animated character prefab. Animator may use a Speed float parameter.")]
        [SerializeField] private GameObject customerCharacterPrefab;
        [Header("Shop-only Navigation")]
        [SerializeField, Tooltip("Walk-in arrival point on the existing NavMesh.")] private Transform spawn;
        [SerializeField, Tooltip("Departure point on the existing NavMesh.")] private Transform exit;
        [SerializeField, Tooltip("Browse positions in product-definition order.")] private Transform[] shelves;
        [SerializeField, Tooltip("Dedicated shop queue, front to back; never reception positions.")] private Transform[] queue;
        [Header("Interaction")]
        [SerializeField, Tooltip("Existing visual counter that opens checkout when tapped.")] private Collider counter;
        [SerializeField, Tooltip("Safe-area checkout and stock panel.")] private ShopHUD hud;

        private sealed class Customer
        {
            public ShopCustomerSession Session;
            public ShopCustomerActor Actor;
        }

        private readonly List<Customer> customers = new List<Customer>(6);
        private readonly List<Customer> checkoutQueue = new List<Customer>(6);
        private readonly ShopEventHub events = new ShopEventHub();
        private ShopLedger ledger;
        private IEconomyService economy;
        private IsometricCameraController cameraController;
        private Camera worldCamera;
        private bool running;
        private int nextCustomer = 1;
        private float arrivalTimer = 4f;
        private float snapshotTimer;

        public bool IsConfigured => settings != null && settings.Products != null && settings.Products.Length == 4 &&
            spawn != null && exit != null && ValidPoints(shelves, settings.Products.Length) &&
            ValidPoints(queue, 1) && counter != null && customerMaterial != null && hud != null;

        private static bool ValidPoints(Transform[] points, int minimum)
        {
            if (points == null || points.Length < minimum) return false;
            for (int i = 0; i < points.Length; i++) if (points[i] == null) return false;
            return true;
        }

        /// <summary>Wires new/missing scene references without replacing existing configured values on upgrades.</summary>
        public void Configure(ShopSettings config, Transform arrival, Transform departure, Transform[] browsePoints,
            Transform[] queuePoints, Collider counterCollider, Material material, ShopHUD shopHud)
        {
            if (settings == null) settings = config;
            if (spawn == null) spawn = arrival;
            if (exit == null) exit = departure;
            if (!ValidPoints(shelves, 4)) shelves = browsePoints;
            if (!ValidPoints(queue, 1)) queue = queuePoints;
            if (counter == null) counter = counterCollider;
            if (customerMaterial == null) customerMaterial = material;
            if (hud == null) hud = shopHud;
        }

        /// <summary>Creates runtime stock once, sharing the motel economy through its service interface.</summary>
        public void Initialize(IEconomyService sharedEconomy, IsometricCameraController camera)
        {
            if (ledger != null) return;
            if (!IsConfigured || sharedEconomy == null || camera == null)
            {
                Debug.LogError("Shop references are missing. Run Roadside Haven > Add or Update Shop in Current Scene.", this);
                return;
            }
            try
            {
                var products = new ShopProduct[settings.Products.Length];
                for (int i = 0; i < products.Length; i++) products[i] = settings.Products[i].ToProduct();
                ledger = new ShopLedger(products, sharedEconomy);
            }
            catch (Exception exception)
            {
                Debug.LogError("Invalid shop configuration: " + exception.Message, this);
                return;
            }
            economy = sharedEconomy;
            cameraController = camera;
            worldCamera = camera.GetComponent<Camera>();
            if (isActiveAndEnabled) Resume();
        }

        private void OnEnable()
        {
            if (ledger != null) Resume();
        }

        private void Resume()
        {
            if (running) return;
            running = true;
            cameraController.Tapped += HandleTap;
            hud.Bind(this, events, cameraController);
            Publish();
        }

        private void OnDisable()
        {
            running = false;
            if (cameraController != null) cameraController.Tapped -= HandleTap;
            if (hud != null) hud.Unbind();
            for (int i = 0; i < customers.Count; i++)
            {
                ledger?.Abandon(customers[i].Session.Basket);
                if (customers[i].Actor != null) Destroy(customers[i].Actor.gameObject);
            }
            customers.Clear();
            checkoutQueue.Clear();
        }

        private void Update()
        {
            if (!running) return;
            float dt = Time.deltaTime;
            arrivalTimer -= dt;
            if (arrivalTimer <= 0f)
            {
                arrivalTimer = settings.ArrivalInterval;
                TrySpawn();
            }
            // Removal is only performed here, in reverse order. Queue moves never remove list entries.
            for (int i = customers.Count - 1; i >= 0; i--) TickCustomer(i, dt);
            snapshotTimer += dt;
            if (snapshotTimer >= 0.5f)
            {
                snapshotTimer = 0f;
                Publish();
            }
        }

        private void TrySpawn()
        {
            if (customers.Count >= Mathf.Min(settings.MaximumCustomers, queue.Length) || FindStockIndex() < 0) return;
            if (!NavMesh.SamplePosition(spawn.position, out var hit, 0.7f, NavMesh.AllAreas))
            {
                Feedback("Shop arrival is off the walkable area. Move its marker onto the NavMesh.");
                return;
            }
            // Avoid spawning inside a shopper still leaving through the entrance.
            for (int i = 0; i < customers.Count; i++)
                if (customers[i].Actor != null && (customers[i].Actor.transform.position - hit.position).sqrMagnitude < 1f)
                    return;
            string id = "S" + nextCustomer++;
            var obj = new GameObject("Shop customer " + id);
            obj.transform.SetParent(transform, true);
            obj.transform.position = hit.position;
            var agent = obj.AddComponent<NavMeshAgent>();
            agent.radius = 0.28f;
            agent.height = 1.5f;
            agent.speed = 2.5f;
            agent.acceleration = 10f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.15f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Shopper";
            body.transform.SetParent(obj.transform, false);
            body.transform.localPosition = Vector3.up * 0.8f;
            body.transform.localScale = new Vector3(0.62f, 0.8f, 0.62f);
            body.GetComponent<Renderer>().sharedMaterial = customerMaterial;
            var customer = new Customer
            {
                Actor = obj.AddComponent<ShopCustomerActor>(),
                Session = new ShopCustomerSession(ledger.CreateBasket(id), UnityEngine.Random.Range(1, settings.BasketSize + 1))
            };
            customers.Add(customer);
            obj.GetComponent<ProceduralWalker>().ConfigureVisual(customerCharacterPrefab);
            if (!MoveToShelf(customer)) BeginLeaving(customer);
            Publish();
        }

        private int FindStockIndex()
        {
            int offset = UnityEngine.Random.Range(0, ledger.Stocks.Count);
            for (int i = 0; i < ledger.Stocks.Count; i++)
            {
                int index = (i + offset) % ledger.Stocks.Count;
                if (ledger.Stocks[index].Available > 0) return index;
            }
            return -1;
        }

        private bool MoveToShelf(Customer customer)
        {
            int index = FindStockIndex();
            if (index < 0 || customer.Actor == null || !customer.Actor.TryMove(shelves[index].position)) return false;
            customer.Session.ShelfIndex = index;
            customer.Session.State = ShopCustomerState.ToShelf;
            customer.Session.NavigationDeadline = Time.time + settings.NavigationTimeoutSeconds;
            return true;
        }

        private void TickCustomer(int index, float dt)
        {
            var customer = customers[index];
            var state = customer.Session;
            if (customer.Actor == null)
            {
                ledger.Abandon(state.Basket);
                checkoutQueue.Remove(customer);
                customers.RemoveAt(index);
                RepositionQueue();
                Publish();
                return;
            }
            if (state.State == ShopCustomerState.Leaving)
            {
                if (customer.Actor.HasArrived || customer.Actor.HasFailedPath || Time.time >= state.NavigationDeadline)
                {
                    Destroy(customer.Actor.gameObject);
                    customers.RemoveAt(index);
                    Publish();
                }
                return;
            }
            if (state.State == ShopCustomerState.ToCounter || state.State == ShopCustomerState.Waiting)
            {
                state.PatienceRemaining -= dt;
                if (state.PatienceRemaining <= 0f)
                {
                    Feedback("A shopper left the queue. Their reserved items returned to stock.");
                    BeginLeaving(customer);
                    return;
                }
            }
            if (state.State == ShopCustomerState.Waiting) return;
            if (state.State == ShopCustomerState.Browsing)
            {
                state.BrowseRemaining -= dt;
                if (state.BrowseRemaining > 0f) return;
                // Reserve only after reaching the shelf. Another shopper may have taken the last item.
                bool picked = ledger.TryReserve(state.Basket, ledger.Stocks[state.ShelfIndex].Product.Id);
                if (picked && state.Basket.Items.Count < state.DesiredItems && MoveToShelf(customer))
                {
                    Publish();
                    return;
                }
                if (state.Basket.Items.Count > 0) JoinQueue(customer);
                else BeginLeaving(customer);
                return;
            }
            if (customer.Actor.HasFailedPath || Time.time >= state.NavigationDeadline)
            {
                Feedback("A shopper could not reach their destination. Reserved stock was returned.");
                BeginLeaving(customer);
                return;
            }
            if (!customer.Actor.HasArrived) return;
            customer.Actor.Stop();
            if (state.State == ShopCustomerState.ToShelf)
            {
                state.State = ShopCustomerState.Browsing;
                state.BrowseRemaining = settings.BrowseSeconds;
            }
            else if (state.State == ShopCustomerState.ToCounter)
            {
                state.State = ShopCustomerState.Waiting;
                ledger.MarkReady(state.Basket);
                Publish();
            }
        }

        private void JoinQueue(Customer customer)
        {
            if (checkoutQueue.Count >= queue.Length) { BeginLeaving(customer); return; }
            customer.Session.PatienceRemaining = settings.PatienceSeconds;
            checkoutQueue.Add(customer);
            RepositionQueue();
            Publish();
        }

        private void RepositionQueue()
        {
            // Each index is unique. Repath only when its slot changes, without resetting patience.
            for (int i = 0; i < checkoutQueue.Count; i++)
            {
                var customer = checkoutQueue[i];
                if (customer.Session.QueueSlot == i) continue;
                customer.Session.QueueSlot = i;
                customer.Session.State = ShopCustomerState.ToCounter;
                customer.Session.NavigationDeadline = Time.time + settings.NavigationTimeoutSeconds;
                if (customer.Actor == null || !customer.Actor.TryMove(queue[i].position))
                    customer.Session.NavigationDeadline = Time.time;
            }
        }

        private void BeginLeaving(Customer customer)
        {
            // Abandon is a no-op for paid baskets and for previously abandoned baskets.
            ledger.Abandon(customer.Session.Basket);
            checkoutQueue.Remove(customer);
            customer.Session.QueueSlot = -1;
            customer.Session.State = ShopCustomerState.Leaving;
            customer.Session.NavigationDeadline = Time.time + settings.NavigationTimeoutSeconds;
            if (customer.Actor == null || !customer.Actor.TryMove(exit.position))
                customer.Session.NavigationDeadline = Time.time;
            RepositionQueue();
            Publish();
        }

        private Customer ReadyCustomer => checkoutQueue.Count > 0 &&
            checkoutQueue[0].Session.State == ShopCustomerState.Waiting ? checkoutQueue[0] : null;

        public ShopSnapshot GetSnapshot()
        {
            var ready = ReadyCustomer;
            return new ShopSnapshot(economy.Coins, customers.Count, checkoutQueue.Count,
                ready?.Session.Basket, ready == null ? 0f : ready.Session.PatienceRemaining, ledger.Stocks);
        }

        public RestockQuote GetRestockQuote(string productId) => ledger.GetRestockQuote(productId);

        public bool TryScan(string basketId, int itemIndex)
        {
            var ready = ReadyCustomer;
            if (ready == null || ready.Session.Basket.Id != basketId || !ledger.TryScan(ready.Session.Basket, itemIndex))
            {
                Feedback("That basket is no longer at the counter, or the item is already scanned.");
                Publish();
                return false;
            }
            Publish();
            return true;
        }

        public bool TryCollectPayment(string basketId)
        {
            var ready = ReadyCustomer;
            if (ready == null || ready.Session.Basket.Id != basketId || !ledger.TryPay(ready.Session.Basket))
            {
                Feedback("Scan every item in the current basket before collecting payment.");
                Publish();
                return false;
            }
            Feedback("Payment collected: +" + ready.Session.Basket.Total + " coins.");
            BeginLeaving(ready);
            return true;
        }

        public bool TryAcceptOwnerDelivery(string productId, int quantity)
        {
            if (!running || ledger == null || !ledger.TryAcceptDelivery(productId, quantity)) return false;
            Publish();
            return true;
        }
        public bool TryRestock(RestockQuote quote)
        {
            if (!ledger.TryRestock(quote))
            {
                Feedback("Restock rejected: insufficient coins, shelf full, or quantity changed. Check the updated quote.");
                Publish();
                return false;
            }
            Feedback("Restocked " + quote.Quantity + " items for " + quote.TotalCost + " coins.");
            Publish();
            return true;
        }

        private void HandleTap(Vector2 position)
        {
            if (!running || hud.IsOpen || worldCamera == null) return;
            var ray = worldCamera.ScreenPointToRay(position);
            if (!Physics.Raycast(ray, out var hit, 100f)) return;
            // Goods stand on the existing counter as siblings, so include its vertical display area.
            var displayBounds = counter.bounds;
            displayBounds.Expand(new Vector3(0.2f, 1.5f, 0.2f));
            if (hit.collider == counter || displayBounds.Contains(hit.point)) hud.Open();
        }

        private void Publish() => events.Publish(GetSnapshot());
        private void Feedback(string message) => events.Publish(new HotelFeedback(message, 3.5f));
    }
}
