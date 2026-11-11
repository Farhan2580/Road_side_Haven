using UnityEngine;

namespace RoadsideHaven
{
    /// <summary>Designer-tunable settings for the first motel prototype.</summary>
    [CreateAssetMenu(menuName = "Roadside Haven/Hotel Settings", fileName = "HotelGameSettings")]
    public sealed class HotelGameSettings : ScriptableObject
    {
        [Header("Economy")]
        [Tooltip("Integer coins the motel has when a new game begins.")]
        [SerializeField, Min(0)] private int startingCoins = 120;
        [Tooltip("Integer coins earned when one guest checks out.")]
        [SerializeField, Min(1)] private int roomRate = 35;

        [Header("Guest Flow")]
        [Tooltip("Maximum number of guests in the scene at once, including guests leaving.")]
        [SerializeField, Range(1, 12)] private int maximumGuests = 8;
        [Tooltip("Seconds between guest arrival attempts.")]
        [SerializeField, Min(1f)] private float guestArrivalInterval = 10f;
        [Tooltip("Seconds a guest waits at reception before leaving.")]
        [SerializeField, Min(5f)] private float guestPatienceSeconds = 55f;
        [Tooltip("Seconds a guest stays in an assigned room.")]
        [SerializeField, Min(5f)] private float guestStaySeconds = 45f;
        [Tooltip("Seconds required to clean a checked-out room.")]
        [SerializeField, Min(1f)] private float cleaningSeconds = 8f;
        [Tooltip("Maximum wait for a guest to complete a navigation trip.")]
        [SerializeField, Min(3f)] private float navigationTimeoutSeconds = 18f;

        [Header("Performance")]
        [Tooltip("Requested frame rate for the mobile prototype.")]
        [SerializeField, Range(15, 60)] private int targetFrameRate = 30;

        public int StartingCoins => startingCoins;
        public int RoomRate => roomRate;
        public int MaximumGuests => maximumGuests;
        public float GuestArrivalInterval => guestArrivalInterval;
        public float GuestPatienceSeconds => guestPatienceSeconds;
        public float GuestStaySeconds => guestStaySeconds;
        public float CleaningSeconds => cleaningSeconds;
        public float NavigationTimeoutSeconds => navigationTimeoutSeconds;
        public int TargetFrameRate => targetFrameRate;

        private void OnValidate()
        {
            startingCoins = Mathf.Max(0, startingCoins);
            roomRate = Mathf.Max(1, roomRate);
            maximumGuests = Mathf.Clamp(maximumGuests, 1, 12);
            guestArrivalInterval = Mathf.Max(1f, guestArrivalInterval);
            guestPatienceSeconds = Mathf.Max(5f, guestPatienceSeconds);
            guestStaySeconds = Mathf.Max(5f, guestStaySeconds);
            cleaningSeconds = Mathf.Max(1f, cleaningSeconds);
            navigationTimeoutSeconds = Mathf.Max(3f, navigationTimeoutSeconds);
            targetFrameRate = Mathf.Clamp(targetFrameRate, 15, 60);
        }
    }
}
