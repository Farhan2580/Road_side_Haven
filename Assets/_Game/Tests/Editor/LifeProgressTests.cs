using NUnit.Framework;
namespace RoadsideHaven.Tests
{
    public sealed class LifeProgressTests
    {
        [Test] public void OwnershipAndUpgradeCommandsRejectDuplicateCharges()
        {
            var wallet=new Wallet(500);var progress=new LifeProgress(wallet);
            Assert.IsTrue(progress.Owns(1));Assert.IsFalse(progress.Owns(0));
            Assert.IsTrue(progress.Buy(0));Assert.AreEqual(420,wallet.Coins);
            Assert.IsFalse(progress.Buy(0));Assert.AreEqual(420,wallet.Coins);
            for(int i=0;i<3;i++)Assert.IsTrue(progress.Upgrade(0));
            int balance=wallet.Coins;Assert.IsFalse(progress.Upgrade(0));Assert.AreEqual(balance,wallet.Coins);
        }
        [Test] public void DeliveryQuestPaysOnceAndRelationshipsHaveGreetingCooldown()
        {
            var wallet=new Wallet(100);var progress=new LifeProgress(wallet);
            Assert.IsTrue(progress.AcceptQuest());Assert.IsFalse(progress.AcceptQuest());
            Assert.IsTrue(progress.CompleteDelivery());Assert.IsFalse(progress.CompleteDelivery());Assert.AreEqual(135,wallet.Coins);
            Assert.IsTrue(progress.Greet("A",0));Assert.IsFalse(progress.Greet("A",1));Assert.IsTrue(progress.Greet("A",60));
            Assert.AreEqual(2,progress.Relationship("A"));
        }
        [Test] public void UnpaidGuardLeavesAndFineRequiresThresholdAndFunds()
        {
            var wallet=new Wallet(30);var progress=new LifeProgress(wallet);
            Assert.IsTrue(progress.HireGuard());Assert.IsFalse(progress.PayGuardWages());Assert.AreEqual(0,progress.Guards);
            Assert.IsFalse(progress.PayFine());for(int i=0;i<3;i++)progress.RecordTrouble();
            Assert.IsFalse(progress.PayFine());Assert.AreEqual(3,progress.Trouble);
            wallet.Credit(40);Assert.IsTrue(progress.PayFine());Assert.AreEqual(0,progress.Trouble);
        }
    }
}
