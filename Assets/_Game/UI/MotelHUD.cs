using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoadsideHaven
{
    /// <summary>Generated, safe-area-aware mobile HUD for the first playable.</summary>
    public sealed class MotelHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private TMP_Text occupancyText;
        [SerializeField] private TMP_Text waitingText;
        [SerializeField] private TMP_Text selectionText;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private TMP_Text actionButtonText;
        [SerializeField] private Button actionButton;

        private IHotelService _hotelService;
        private HotelEventHub _events;
        private string _selectedRoomId;
        private float _feedbackRemaining;

        public void Bind(IHotelService hotelService, HotelEventHub events)
        {
            Unbind();
            ApplyCompactLayout();
            _hotelService = hotelService;
            _events = events;
            if (_events != null)
            {
                _events.SnapshotChanged += UpdateSnapshot;
                _events.FeedbackRaised += ShowFeedback;
            }
            if (actionButton != null)
                actionButton.onClick.AddListener(CleanSelectedRoom);
            if (instructionText != null)
                instructionText.text = "Select a waiting guest, then tap an available room. Clean dirty rooms to reopen them.";
        }

        private void Update()
        {
            if (_feedbackRemaining <= 0f || feedbackText == null)
                return;
            _feedbackRemaining -= Time.deltaTime;
            if (_feedbackRemaining <= 0f)
                feedbackText.text = string.Empty;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (_events != null)
            {
                _events.SnapshotChanged -= UpdateSnapshot;
                _events.FeedbackRaised -= ShowFeedback;
            }
            if (actionButton != null)
                actionButton.onClick.RemoveListener(CleanSelectedRoom);
            _events = null;
            _hotelService = null;
        }

        private void UpdateSnapshot(HotelSnapshot snapshot)
        {
            if (moneyText != null)
                moneyText.text = $"{snapshot.CurrencyName.ToUpperInvariant()}  {snapshot.Coins}";
            if (occupancyText != null)
                occupancyText.text = $"ROOMS  {snapshot.OccupiedRooms}/{snapshot.TotalRooms}";
            if (waitingText != null)
                waitingText.text = $"WAITING  {snapshot.WaitingGuests}";
            if (selectionText != null)
            {
                selectionText.text = snapshot.SelectionDetails;
                var root = GetComponentInParent<MotelGameRoot>();
                bool exploring = root != null && ((root.GetComponentInChildren<OwnerSandboxController>(true)?.IsWalking ?? false) || (root.GetComponentInChildren<WorldDrivingController>(true)?.IsDriving ?? false));
                bool selected = !string.IsNullOrEmpty(snapshot.SelectedRoomId) || (snapshot.SelectionDetails?.StartsWith("Guest ") ?? false);
                selectionText.transform.parent.gameObject.SetActive(selected && !exploring && !(root?.GetComponentInChildren<BusinessEmpireController>(true)?.PanelOpen ?? false));
            }
            if (instructionText != null)
            {
                instructionText.enableAutoSizing = true;
                instructionText.fontSizeMin = 14f;
                instructionText.fontSizeMax = 21f;
                instructionText.text = snapshot.RoomOverview;
            }
            if (actionButton != null)
                actionButton.gameObject.SetActive(snapshot.CanCleanSelectedRoom);
            if (actionButtonText != null)
                actionButtonText.text = snapshot.CanCleanSelectedRoom ? "CLEAN ROOM" : "CLEANING";

            _selectedRoomId = snapshot.SelectedRoomId;
        }

        private void ShowFeedback(HotelFeedback feedback)
        {
            if (feedback.Message.StartsWith("Guest ") && (feedback.Message.Contains("waiting") || feedback.Message.Contains("arrived") || feedback.Message.Contains("checked into"))) return;
            if (feedbackText == null)
                return;
            feedbackText.text = feedback.Message;
            _feedbackRemaining = feedback.Duration;
        }

        private void ApplyCompactLayout()
        {
            if(moneyText!=null)
            {
                var top=moneyText.transform.parent as RectTransform;
                if(top!=null){top.anchorMin=new Vector2(.02f,.93f);top.anchorMax=new Vector2(.98f,.99f);}
            }
            foreach(var text in new[]{moneyText,occupancyText,waitingText})
                if(text!=null){text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=22;}
            if(selectionText!=null)
            {
                var bottom=selectionText.transform.parent as RectTransform;
                if(bottom!=null){bottom.anchorMin=new Vector2(.02f,.02f);bottom.anchorMax=new Vector2(.98f,.15f);}
                selectionText.enableAutoSizing=true;selectionText.fontSizeMin=15;selectionText.fontSizeMax=20;
            }
            if(feedbackText!=null)
            {
                feedbackText.rectTransform.anchorMin=new Vector2(.18f,.18f);feedbackText.rectTransform.anchorMax=new Vector2(.82f,.23f);
                feedbackText.fontSize=18;
            }
        }
        private void CleanSelectedRoom()
        {
            if (!string.IsNullOrEmpty(_selectedRoomId))
                _hotelService?.TryBeginCleaning(_selectedRoomId);
        }

        /// <summary>Connects all text and button fields when the Editor creates the HUD prefab.</summary>
        public void Configure(TMP_Text money, TMP_Text occupancy, TMP_Text waiting, TMP_Text selection,
            TMP_Text instruction, TMP_Text feedback, Button action, TMP_Text actionLabel)
        {
            moneyText = money;
            occupancyText = occupancy;
            waitingText = waiting;
            selectionText = selection;
            instructionText = instruction;
            feedbackText = feedback;
            actionButton = action;
            actionButtonText = actionLabel;
        }
    }
}

