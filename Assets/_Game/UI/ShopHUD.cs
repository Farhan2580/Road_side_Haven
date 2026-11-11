using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RoadsideHaven
{
    /// <summary>Safe-area checkout and stock controls. The modal blocks all world input while open.</summary>
    public sealed class ShopHUD : MonoBehaviour
    {
        [Header("Generated UI References")]
        [Tooltip("Full-screen raycast blocker containing the safe-area shop panel.")]
        [SerializeField] private GameObject modal;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text customersText;
        [SerializeField] private TMP_Text basketText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private TMP_Text paymentText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button paymentButton;
        [SerializeField] private Button[] scanButtons;
        [SerializeField] private TMP_Text[] scanLabels;
        [SerializeField] private TMP_Text[] stockLabels;
        [SerializeField] private Button[] restockButtons;
        [SerializeField] private TMP_Text[] restockLabels;

        private IShopService service;
        private ShopEventHub events;
        private IsometricCameraController cameraController;
        private string displayedBasketId;
        private readonly RestockQuote[] displayedQuotes = new RestockQuote[4];
        private UnityAction[] scanActions;
        private UnityAction[] restockActions;
        private float feedbackUntil;

        public bool IsOpen => modal != null && modal.activeSelf;

        /// <summary>Connects this HUD to the shared scene services; rebinding removes old subscriptions first.</summary>
        public void Bind(IShopService shopService, ShopEventHub shopEvents, IsometricCameraController camera)
        {
            Unbind();
            service = shopService;
            events = shopEvents;
            cameraController = camera;
            if (events != null)
            {
                events.Changed += Refresh;
                events.Feedback += ShowFeedback;
            }
            closeButton.onClick.AddListener(Close);
            paymentButton.onClick.AddListener(CollectPayment);
            scanActions = new UnityAction[scanButtons.Length];
            for (int i = 0; i < scanButtons.Length; i++)
            {
                int index = i;
                scanActions[i] = () => Scan(index);
                scanButtons[i].onClick.AddListener(scanActions[i]);
            }
            restockActions = new UnityAction[restockButtons.Length];
            for (int i = 0; i < restockButtons.Length; i++)
            {
                int index = i;
                restockActions[i] = () => Restock(index);
                restockButtons[i].onClick.AddListener(restockActions[i]);
            }
        }

        public void Open()
        {
            if (service == null || modal == null) return;
            modal.SetActive(true);
            cameraController?.SetModalInputBlocked(true);
            feedbackText.text = string.Empty;
            Refresh(service.GetSnapshot());
        }

        public void Close()
        {
            if (modal != null) modal.SetActive(false);
            displayedBasketId = null;
            cameraController?.SetModalInputBlocked(false);
        }

        public void Unbind()
        {
            Close();
            if (events != null)
            {
                events.Changed -= Refresh;
                events.Feedback -= ShowFeedback;
            }
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (paymentButton != null) paymentButton.onClick.RemoveListener(CollectPayment);
            if (scanActions != null)
                for (int i = 0; i < scanActions.Length; i++)
                    if (scanButtons[i] != null) scanButtons[i].onClick.RemoveListener(scanActions[i]);
            if (restockActions != null)
                for (int i = 0; i < restockActions.Length; i++)
                    if (restockButtons[i] != null) restockButtons[i].onClick.RemoveListener(restockActions[i]);
            scanActions = null;
            restockActions = null;
            service = null;
            events = null;
            cameraController = null;
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();

        private void Update()
        {
            if (IsOpen && feedbackUntil > 0f && Time.unscaledTime >= feedbackUntil)
            {
                feedbackUntil = 0f;
                feedbackText.text = string.Empty;
            }
        }

        private void Refresh(ShopSnapshot snapshot)
        {
            if (!IsOpen) return;
            coinsText.text = $"COINS  {snapshot.Coins}";
            customersText.text = $"IN SHOP  {snapshot.CustomerCount}    QUEUED  {snapshot.WaitingCount}";
            ShopBasket basket = snapshot.Basket;
            displayedBasketId = basket == null ? null : basket.Id;
            basketText.text = basket == null
                ? "CHECKOUT\nWaiting for a customer at the counter"
                : $"{basket.Id}  |  Patience {Mathf.CeilToInt(snapshot.PatienceRemaining)}s\nTap each item to scan";

            for (int i = 0; i < scanButtons.Length; i++)
            {
                bool hasItem = basket != null && i < basket.Items.Count;
                scanButtons[i].gameObject.SetActive(hasItem);
                if (!hasItem) continue;
                var item = basket.Items[i];
                scanLabels[i].text = $"{(item.Scanned ? "SCANNED" : "SCAN")}  {item.DisplayName}  |  {item.UnitPrice} coins";
                scanButtons[i].interactable = !item.Scanned;
            }
            paymentText.text = basket == null ? "COLLECT PAYMENT"
                : $"COLLECT {basket.Total} COINS";
            paymentButton.interactable = basket != null && basket.AllScanned;

            for (int i = 0; i < stockLabels.Length; i++)
            {
                bool hasStock = snapshot.Stocks != null && i < snapshot.Stocks.Count;
                stockLabels[i].gameObject.SetActive(hasStock);
                restockButtons[i].gameObject.SetActive(hasStock);
                if (!hasStock) continue;
                var stock = snapshot.Stocks[i];
                stockLabels[i].text = $"{stock.Product.DisplayName}\nShelf {stock.Available}  |  Reserved {stock.Reserved}\nCapacity {stock.TotalOwned}/{stock.Product.ShelfCapacity}";
                var quote = service.GetRestockQuote(stock.Product.Id);
                displayedQuotes[i] = quote;
                restockLabels[i].text = quote.Quantity > 0
                    ? $"RESTOCK +{quote.Quantity}\n{quote.TotalCost} coins"
                    : "SHELF FULL";
                // Unaffordable orders remain tappable so the service can explain why they were rejected.
                restockButtons[i].interactable = quote.Quantity > 0;
            }
        }

        private void Scan(int index)
        {
            if (!IsOpen || string.IsNullOrEmpty(displayedBasketId)) return;
            service?.TryScan(displayedBasketId, index);
        }

        private void CollectPayment()
        {
            if (!IsOpen || string.IsNullOrEmpty(displayedBasketId)) return;
            service?.TryCollectPayment(displayedBasketId);
        }

        private void Restock(int index)
        {
            if (!IsOpen) return;
            service?.TryRestock(displayedQuotes[index]);
        }

        private void ShowFeedback(HotelFeedback feedback)
        {
            if (!IsOpen) return;
            feedbackText.text = feedback.Message;
            feedbackUntil = Time.unscaledTime + feedback.Duration;
        }

        /// <summary>Builds and wires a persistent HUD hierarchy for the scene installer, without Editor dependencies.</summary>
        public static ShopHUD Create(Transform parent, TMP_FontAsset font)
        {
            var canvasObject = new GameObject("Shop HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            var hud = canvasObject.AddComponent<ShopHUD>();

            var blocker = Panel("Checkout Modal", canvasObject.transform, Vector2.zero, Vector2.one,
                new Color(.015f, .035f, .035f, .78f));
            hud.modal = blocker.gameObject;
            var safe = Rect("Safe Area", blocker, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var panel = Panel("Shop Panel", safe, new Vector2(.025f, .04f), new Vector2(.975f, .96f),
                new Color(.07f, .17f, .17f, 1f));

            Text("PIT STOP", panel, new Vector2(.025f, .86f), new Vector2(.24f, .98f), font, 32);
            hud.coinsText = Text("COINS", panel, new Vector2(.25f, .86f), new Vector2(.43f, .98f), font, 26);
            hud.customersText = Text("CUSTOMERS", panel, new Vector2(.44f, .86f), new Vector2(.81f, .98f), font, 22);
            hud.closeButton = MakeButton("CLOSE", panel, new Vector2(.83f, .865f), new Vector2(.98f, .98f), font, out _);

            var checkout = Rect("Checkout", panel, new Vector2(.025f, .19f), new Vector2(.48f, .84f));
            hud.basketText = Text("CHECKOUT", checkout, new Vector2(0f, .84f), Vector2.one, font, 24);
            hud.scanButtons = new Button[3];
            hud.scanLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                float top = .82f - i * .21f;
                hud.scanButtons[i] = MakeButton("SCAN ITEM", checkout, new Vector2(0f, top - .18f),
                    new Vector2(1f, top), font, out hud.scanLabels[i]);
            }
            hud.paymentButton = MakeButton("COLLECT PAYMENT", checkout, Vector2.zero, new Vector2(1f, .18f),
                font, out hud.paymentText);

            var shelves = Rect("Shelves", panel, new Vector2(.52f, .19f), new Vector2(.975f, .84f));
            Text("SHELF STOCK & RESTOCK", shelves, new Vector2(0f, .88f), Vector2.one, font, 24);
            hud.stockLabels = new TMP_Text[4];
            hud.restockButtons = new Button[4];
            hud.restockLabels = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                float top = .86f - i * .215f;
                hud.stockLabels[i] = Text("Stock", shelves, new Vector2(0f, top - .20f),
                    new Vector2(.56f, top), font, 21);
                hud.restockButtons[i] = MakeButton("RESTOCK", shelves, new Vector2(.58f, top - .19f),
                    new Vector2(1f, top - .01f), font, out hud.restockLabels[i]);
            }
            hud.feedbackText = Text(string.Empty, panel, new Vector2(.03f, .09f), new Vector2(.97f, .18f), font, 24);
            Text("Scan every item, then collect payment. Abandoned items return to stock.", panel,
                new Vector2(.03f, .02f), new Vector2(.97f, .085f), font, 21);
            hud.modal.SetActive(false);
            return hud;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var rect = Rect(name, parent, min, max);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private static TMP_Text Text(string value, Transform parent, Vector2 min, Vector2 max,
            TMP_FontAsset font, float size)
        {
            var rect = Rect(string.IsNullOrEmpty(value) ? "Feedback" : value, parent, min, max);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = size - 3f;
            label.fontSizeMax = size;
            label.color = new Color(.97f, .93f, .82f);
            label.alignment = TextAlignmentOptions.Center;
            label.margin = new Vector4(6f, 2f, 6f, 2f);
            label.raycastTarget = false;
            return label;
        }

        private static Button MakeButton(string text, Transform parent, Vector2 min, Vector2 max,
            TMP_FontAsset font, out TMP_Text label)
        {
            var rect = Panel(text, parent, min, max, new Color(.66f, .28f, .17f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.disabledColor = new Color(.5f, .55f, .5f, .65f);
            button.colors = colors;
            label = Text(text, rect, Vector2.zero, Vector2.one, font, 24);
            return button;
        }
    }
}
