using System;

namespace RoadsideHaven
{
    /// <summary>Explicit lifecycle for one motel room.</summary>
    public enum RoomState
    {
        Available,
        Reserved,
        Occupied,
        Dirty,
        Cleaning
    }

    /// <summary>Lifecycle for a guest actor in the prototype.</summary>
    public enum GuestState
    {
        ToReception,
        Waiting,
        ToRoom,
        Staying,
        Leaving
    }

    /// <summary>Plain runtime room data, separate from its scene visuals.</summary>
    public sealed class HotelRoom
    {
        public string Id { get; }
        public RoomState State { get; private set; } = RoomState.Available;
        public string GuestId { get; private set; }
        public float CleaningRemaining { get; private set; }

        public HotelRoom(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A room must have an identifier.", nameof(id));
            Id = id;
        }

        public bool TryReserve(string guestId)
        {
            if (State != RoomState.Available || string.IsNullOrWhiteSpace(guestId))
                return false;
            State = RoomState.Reserved;
            GuestId = guestId;
            return true;
        }

        public bool ReleaseReservation(string guestId)
        {
            if (State != RoomState.Reserved || GuestId != guestId)
                return false;
            State = RoomState.Available;
            GuestId = null;
            return true;
        }

        public bool TryMarkOccupied(string guestId)
        {
            if (State != RoomState.Reserved || GuestId != guestId)
                return false;
            State = RoomState.Occupied;
            return true;
        }

        /// <summary>Checks out the matching occupant and credits their stay once.</summary>
        public bool TryCheckOut(GuestSession guest, Wallet wallet, int roomRate)
        {
            if (State != RoomState.Occupied || guest == null || wallet == null ||
                GuestId != guest.Id || roomRate < 0 || guest.PaymentSettled)
                return false;

            guest.SettlePayment(wallet, roomRate);
            State = RoomState.Dirty;
            GuestId = null;
            return true;
        }

        public bool TryStartCleaning(float duration)
        {
            if (State != RoomState.Dirty || duration <= 0f)
                return false;
            State = RoomState.Cleaning;
            CleaningRemaining = duration;
            return true;
        }

        /// <returns>True when cleaning finishes and the room becomes available.</returns>
        public bool TickCleaning(float deltaTime)
        {
            if (State != RoomState.Cleaning)
                return false;
            CleaningRemaining -= Math.Max(0f, deltaTime);
            if (CleaningRemaining > 0f)
                return false;
            CleaningRemaining = 0f;
            State = RoomState.Available;
            return true;
        }
    }

    /// <summary>Plain runtime state for one guest; its actor and navigation agent are separate.</summary>
    public sealed class GuestSession
    {
        public string Id { get; }
        public GuestState State { get; set; }
        public string RoomId { get; set; }
        public float PatienceRemaining { get; set; }
        public float StayRemaining { get; set; }
        public float NavigationDeadline { get; set; }
        public int QueueSlot { get; set; } = -1;
        /// <summary>Arrival bay used again when this guest leaves; -1 uses the original exit.</summary>
        public int ParkingBay { get; set; } = -1;
        public bool PaymentSettled { get; private set; }

        public GuestSession(string id, GuestState state)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A guest must have an identifier.", nameof(id));
            Id = id;
            State = state;
        }

        internal void SettlePayment(Wallet wallet, int amount)
        {
            if (PaymentSettled)
                throw new InvalidOperationException("This guest's checkout has already been paid.");
            wallet.Credit(amount);
            PaymentSettled = true;
        }
    }

}

