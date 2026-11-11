using NUnit.Framework;
namespace RoadsideHaven.Tests
{
    public class RoomLifecycleTests
    {
        [Test] public void ReservationIsExclusiveAndOwnerControlled()
        {
            var r=new HotelRoom("101"); Assert.IsTrue(r.TryReserve("A")); Assert.IsFalse(r.TryReserve("B"));
            Assert.IsFalse(r.ReleaseReservation("B")); Assert.IsFalse(r.TryMarkOccupied("B"));
            Assert.IsTrue(r.ReleaseReservation("A")); Assert.IsTrue(r.TryReserve("B"));
        }
        [Test] public void CheckoutPaysExactlyOnceAndOnlyMatchingOccupant()
        {
            var r=new HotelRoom("101"); var a=new GuestSession("A",GuestState.Staying);var wallet=new Wallet(10);
            r.TryReserve(a.Id);r.TryMarkOccupied(a.Id);
            Assert.IsFalse(r.TryCheckOut(new GuestSession("B",GuestState.Staying),wallet,35));
            Assert.IsTrue(r.TryCheckOut(a,wallet,35));Assert.IsFalse(r.TryCheckOut(a,wallet,35));
            Assert.AreEqual(45,wallet.Coins);Assert.IsTrue(a.PaymentSettled);Assert.AreEqual(RoomState.Dirty,r.State);
            r.TryStartCleaning(1);r.TickCleaning(1);r.TryReserve(a.Id);r.TryMarkOccupied(a.Id);
            Assert.IsFalse(r.TryCheckOut(a,wallet,35));Assert.AreEqual(45,wallet.Coins);
        }
        [Test] public void CleaningRequiresDirtyRoomAndCompletesAfterDuration()
        {
            var r=new HotelRoom("101"); Assert.IsFalse(r.TryStartCleaning(3));
            var a=new GuestSession("A",GuestState.Staying);r.TryReserve(a.Id);r.TryMarkOccupied(a.Id);r.TryCheckOut(a,new Wallet(0),35);
            Assert.IsFalse(r.TryStartCleaning(0));Assert.IsTrue(r.TryStartCleaning(3));Assert.IsFalse(r.TryReserve("B"));
            Assert.IsFalse(r.TryStartCleaning(3));Assert.IsFalse(r.TickCleaning(2));Assert.AreEqual(RoomState.Cleaning,r.State);
            Assert.IsTrue(r.TickCleaning(1));Assert.AreEqual(RoomState.Available,r.State);Assert.IsFalse(r.TickCleaning(1));Assert.IsTrue(r.TryReserve("B"));
        }
    }
}
