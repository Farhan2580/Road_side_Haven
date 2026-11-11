using System;

namespace RoadsideHaven
{
    /// <summary>Immutable UI-facing snapshot of the current hotel state.</summary>
    public readonly struct HotelSnapshot
    {
        public readonly int Coins;
        public readonly int OccupiedRooms;
        public readonly int TotalRooms;
        public readonly int WaitingGuests;
        public readonly string SelectionDetails;
        public readonly bool CanCleanSelectedRoom;
        public readonly string SelectedRoomId;
        public readonly string RoomOverview;

        public HotelSnapshot(int coins, int occupiedRooms, int totalRooms, int waitingGuests,
            string selectionDetails, bool canCleanSelectedRoom, string selectedRoomId,
            string roomOverview = "")
        {
            Coins = coins;
            OccupiedRooms = occupiedRooms;
            TotalRooms = totalRooms;
            WaitingGuests = waitingGuests;
            SelectionDetails = selectionDetails;
            CanCleanSelectedRoom = canCleanSelectedRoom;
            SelectedRoomId = selectedRoomId;
            RoomOverview = roomOverview;
        }
    }

    /// <summary>Short-lived player feedback notification.</summary>
    public readonly struct HotelFeedback
    {
        public readonly string Message;
        public readonly float Duration;

        public HotelFeedback(string message, float duration = 2.5f)
        {
            Message = message;
            Duration = duration;
        }
    }

    /// <summary>Scene-scoped typed notifications; the composition root owns this instance.</summary>
    public sealed class HotelEventHub
    {
        public event Action<HotelSnapshot> SnapshotChanged;
        public event Action<HotelFeedback> FeedbackRaised;

        public void Publish(HotelSnapshot snapshot) => SnapshotChanged?.Invoke(snapshot);
        public void Publish(HotelFeedback feedback) => FeedbackRaised?.Invoke(feedback);
    }

    /// <summary>Requests that change room availability and guest assignments.</summary>
    public interface IHotelService
    {
        bool TryAssignGuestToRoom(string guestId, string roomId);
        bool TryBeginCleaning(string roomId);
    }

    /// <summary>Scene-owned integer-coin economy shared by the motel and convenience shop.</summary>
    public interface IEconomyService
    {
        int Coins { get; }
        /// <summary>Returns false without changing the balance if the amount is invalid or unaffordable.</summary>
        bool TryDebit(int amount);
        /// <summary>Credits a nonnegative amount; overflow must fail before changing the balance.</summary>
        void Credit(int amount);
    }
}
