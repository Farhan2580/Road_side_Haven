using NUnit.Framework;
namespace RoadsideHaven.Tests
{
    public sealed class MultiverseEconomyTests
    {
        [Test] public void TradeCarriesItemsAcrossIsolatedWalletsAndCannotSellTwice()
        {
            var normal=new Wallet(100);var bank=new MultiverseLedger(normal,120,1);
            Assert.IsTrue(bank.TryBuy(DimensionItem.Supplies));
            Assert.IsFalse(bank.TryBuy(DimensionItem.CyberCore));
            Assert.AreEqual(90,normal.Coins);
            bank.Travel(UniverseId.Neon);
            Assert.IsTrue(bank.TrySell(DimensionItem.Supplies));
            Assert.AreEqual(136,bank.CurrentWallet.Coins);
            Assert.IsFalse(bank.TrySell(DimensionItem.Supplies));
            bank.Travel(UniverseId.Normal);Assert.AreEqual(90,bank.CurrentWallet.Coins);
        }
        [Test] public void OverflowRejectsSaleWithoutDiscardingCargo()
        {
            var bank=new MultiverseLedger(new Wallet(int.MaxValue));
            bank.Travel(UniverseId.Neon);Assert.IsTrue(bank.TryBuy(DimensionItem.CyberCore));
            bank.Travel(UniverseId.Normal);Assert.IsFalse(bank.TrySell(DimensionItem.CyberCore));
            Assert.AreEqual(1,bank.Count(DimensionItem.CyberCore));Assert.AreEqual(int.MaxValue,bank.CurrentWallet.Coins);
        }
        [Test] public void GuestPaysBookedUniverseOnceAfterPortalTravel()
        {
            var normal=new Wallet(100);var bank=new MultiverseLedger(normal);
            var guest=new GuestSession("G",GuestState.Staying){BookedUniverse=0,BookedRoomRate=35};
            var room=new HotelRoom("101");room.TryReserve(guest.Id);room.TryMarkOccupied(guest.Id);
            bank.Travel(UniverseId.Fantasy);
            Assert.IsTrue(room.TryCheckOut(guest,bank.GetWallet((UniverseId)guest.BookedUniverse),guest.BookedRoomRate));
            Assert.AreEqual(135,normal.Coins);Assert.AreEqual(120,bank.CurrentWallet.Coins);
            Assert.IsFalse(room.TryCheckOut(guest,normal,35));
        }
    }
}
