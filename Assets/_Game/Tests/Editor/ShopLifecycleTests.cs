using System;
using NUnit.Framework;

namespace RoadsideHaven.Tests
{
    public sealed class ShopLifecycleTests
    {
        private static ShopProduct Water(int stock = 2, int capacity = 4) =>
            new ShopProduct("water", "Water", 3, 7, capacity, stock, 2);

        [Test]
        public void ReservationCannotOversellAndRejectsForeignBasket()
        {
            var ledger = new ShopLedger(new[] { Water(1) }, new Wallet(20));
            var first = ledger.CreateBasket("A");
            var second = ledger.CreateBasket("B");
            Assert.IsTrue(ledger.TryReserve(first, "water"));
            Assert.IsFalse(ledger.TryReserve(second, "water"));
            Assert.AreEqual(0, ledger.Stocks[0].Available);
            Assert.AreEqual(1, ledger.Stocks[0].Reserved);
            var other = new ShopLedger(new[] { Water() }, new Wallet(20));
            Assert.IsFalse(other.TryReserve(first, "water"));
            Assert.AreEqual(2, other.Stocks[0].Available);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AbandonReturnsUnpaidItemsExactlyOnce(bool scanned)
        {
            var wallet = new Wallet(20);
            var ledger = new ShopLedger(new[] { Water() }, wallet);
            var basket = ledger.CreateBasket("A");
            ledger.TryReserve(basket, "water");
            ledger.TryReserve(basket, "water");
            if (scanned)
            {
                ledger.MarkReady(basket);
                ledger.TryScan(basket, 0);
            }
            Assert.IsTrue(ledger.Abandon(basket));
            Assert.IsFalse(ledger.Abandon(basket));
            Assert.IsFalse(ledger.TryPay(basket));
            Assert.IsFalse(ledger.TryReserve(basket, "water"));
            Assert.AreEqual(2, ledger.Stocks[0].Available);
            Assert.AreEqual(0, ledger.Stocks[0].Reserved);
            Assert.AreEqual(20, wallet.Coins);
        }

        [Test]
        public void EveryItemMustBeScannedAndPaymentIsExactAndOnce()
        {
            var wallet = new Wallet(20);
            var ledger = new ShopLedger(new[] { Water(), new ShopProduct("chips", "Chips", 4, 9, 4, 2, 2) }, wallet);
            var basket = ledger.CreateBasket("A");
            ledger.TryReserve(basket, "water");
            ledger.TryReserve(basket, "chips");
            Assert.IsFalse(ledger.TryScan(basket, 0));
            ledger.MarkReady(basket);
            Assert.IsFalse(ledger.TryPay(basket));
            Assert.IsTrue(ledger.TryScan(basket, 0));
            Assert.IsFalse(ledger.TryScan(basket, 0));
            Assert.IsFalse(ledger.TryPay(basket));
            Assert.IsTrue(ledger.TryScan(basket, 1));
            Assert.AreEqual(16, basket.Total);
            Assert.IsTrue(ledger.TryPay(basket));
            Assert.IsFalse(ledger.TryPay(basket));
            Assert.IsFalse(ledger.Abandon(basket));
            Assert.AreEqual(36, wallet.Coins);
            Assert.AreEqual(1, ledger.Stocks[0].Available);
            Assert.AreEqual(0, ledger.Stocks[0].Reserved);
            Assert.AreEqual(1, ledger.Stocks[1].Available);
        }

        [Test]
        public void InsufficientFundsCannotPartiallyRestockOrSpend()
        {
            var wallet = new Wallet(5);
            var ledger = new ShopLedger(new[] { Water() }, wallet);
            RestockQuote quote = ledger.GetRestockQuote("water");
            Assert.AreEqual(2, quote.Quantity);
            Assert.AreEqual(6, quote.TotalCost);
            Assert.IsFalse(ledger.TryRestock(quote));
            Assert.AreEqual(5, wallet.Coins);
            Assert.AreEqual(2, ledger.Stocks[0].Available);
            wallet.Credit(1);
            Assert.IsTrue(ledger.TryRestock(quote));
            Assert.AreEqual(0, wallet.Coins);
            Assert.AreEqual(4, ledger.Stocks[0].Available);
        }

        [Test]
        public void ReservationsStillConsumeCapacitySoAbandonedUnitsAlwaysFit()
        {
            var wallet = new Wallet(50);
            var ledger = new ShopLedger(new[] { Water(4) }, wallet);
            var basket = ledger.CreateBasket("A");
            ledger.TryReserve(basket, "water");
            Assert.AreEqual(3, ledger.Stocks[0].Available);
            Assert.AreEqual(1, ledger.Stocks[0].Reserved);
            Assert.AreEqual(0, ledger.GetRestockQuote("water").Quantity);
            Assert.IsFalse(ledger.TryRestock(new RestockQuote("water", 1, 3)));
            ledger.Abandon(basket);
            Assert.AreEqual(4, ledger.Stocks[0].Available);
            Assert.AreEqual(50, wallet.Coins);
        }

        [Test]
        public void TamperedAndStaleQuotesAreRejected()
        {
            var wallet = new Wallet(50);
            var ledger = new ShopLedger(new[] { Water(1) }, wallet);
            var quote = ledger.GetRestockQuote("water");
            Assert.IsFalse(ledger.TryRestock(new RestockQuote("water", quote.Quantity, 0)));
            Assert.IsTrue(ledger.TryRestock(quote));
            Assert.IsFalse(ledger.TryRestock(quote)); // Only one unit now fits.
            Assert.AreEqual(44, wallet.Coins);
            Assert.AreEqual(3, ledger.Stocks[0].Available);
            var finalUnit = ledger.GetRestockQuote("water");
            Assert.AreEqual(1, finalUnit.Quantity);
            Assert.AreEqual(3, finalUnit.TotalCost);
        }

        [Test]
        public void EconomyCallbackCannotReenterPaymentOrAbandonment()
        {
            var economy = new ReentrantEconomy();
            var ledger = new ShopLedger(new[] { Water() }, economy);
            var basket = ledger.CreateBasket("A");
            ledger.TryReserve(basket, "water");
            ledger.MarkReady(basket);
            ledger.TryScan(basket, 0);
            economy.OnCredit = () =>
            {
                Assert.IsFalse(ledger.TryPay(basket));
                Assert.IsFalse(ledger.Abandon(basket));
            };
            Assert.IsTrue(ledger.TryPay(basket));
            Assert.AreEqual(7, economy.Coins);
            Assert.AreEqual(0, ledger.Stocks[0].Reserved);
        }

        [Test]
        public void OverflowRejectsPaymentWithoutConsumingInventory()
        {
            var wallet = new Wallet(int.MaxValue);
            var ledger = new ShopLedger(new[] { Water() }, wallet);
            var basket = ledger.CreateBasket("A");
            ledger.TryReserve(basket, "water");
            ledger.MarkReady(basket);
            ledger.TryScan(basket, 0);
            Assert.IsFalse(ledger.TryPay(basket));
            Assert.AreEqual(int.MaxValue, wallet.Coins);
            Assert.AreEqual(1, ledger.Stocks[0].Reserved);
            Assert.IsTrue(ledger.Abandon(basket));
            Assert.AreEqual(2, ledger.Stocks[0].Available);
        }

        private sealed class ReentrantEconomy : IEconomyService
        {
            public int Coins { get; private set; }
            public Action OnCredit;
            public void Credit(int amount) { OnCredit?.Invoke(); Coins += amount; }
            public bool TryDebit(int amount)
            {
                if (amount < 0 || amount > Coins) return false;
                Coins -= amount;
                return true;
            }
        }
    }
}
