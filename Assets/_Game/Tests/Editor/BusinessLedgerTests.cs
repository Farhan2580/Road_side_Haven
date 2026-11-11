using NUnit.Framework;
namespace RoadsideHaven.Tests
{
    public sealed class BusinessLedgerTests
    {
        private static BusinessSpec[] Specs()=>new[]{new BusinessSpec("home","Home",BusinessType.Motel,100,10,60,false,0,20,true),new BusinessSpec("shop","Shop",BusinessType.Shop,100,15,60,true,1,20),new BusinessSpec("bank","Bank",BusinessType.Bank,200,20,60,true,2,30)};
        [Test] public void DailyIncomeAndCollectionAreExactlyOnce()
        {
            var wallet=new Wallet(500);var ledger=new BusinessLedger(Specs(),_=>wallet,1,3);
            Assert.IsFalse(ledger.Buy(0,2));Assert.AreEqual(500,wallet.Coins);
            Assert.IsTrue(ledger.Buy(0,1));Assert.IsFalse(ledger.Buy(0,1));
            ledger.Tick(120,2);ledger.Tick(120,2);
            Assert.AreEqual(15,ledger.Holding(0,1).Pending);Assert.AreEqual(0,ledger.Holding(0,0).Pending);
            Assert.IsTrue(ledger.Collect(0,1));Assert.IsFalse(ledger.Collect(0,1));Assert.AreEqual(415,wallet.Coins);
        }
        [Test] public void InvestmentInterestAndPrincipalCannotBeCollectedTwice()
        {
            var wallet=new Wallet(1000);var ledger=new BusinessLedger(Specs(),_=>wallet,1,3);
            ledger.Buy(0,1);ledger.Buy(0,2);Assert.IsTrue(ledger.Invest(0,2,100));
            ledger.Tick(0,2);ledger.Tick(0,2);Assert.AreEqual(25,ledger.Holding(0,2).Pending);
            Assert.IsTrue(ledger.Withdraw(0,2));Assert.IsFalse(ledger.Withdraw(0,2));
        }
        [Test] public void ExpansionUsesLocalWalletAndOverflowPreservesSafe()
        {
            var wallets=new[]{new Wallet(1000),new Wallet(1000),new Wallet(1000),new Wallet(1000)};
            var ledger=new BusinessLedger(Specs(),u=>wallets[u],1,3);
            Assert.IsFalse(ledger.Buy(1,0));ledger.Buy(0,1);ledger.Buy(0,2);Assert.IsTrue(ledger.Buy(1,0));
            Assert.AreEqual(900,wallets[1].Coins);Assert.AreEqual(700,wallets[0].Coins);
            wallets[1].Credit(int.MaxValue-900);ledger.Tick(60,1);
            Assert.IsFalse(ledger.Collect(1,0));Assert.AreEqual(10,ledger.Holding(1,0).Pending);
        }
    }
}
