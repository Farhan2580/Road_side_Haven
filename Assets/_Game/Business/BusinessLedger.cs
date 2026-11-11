using System;
namespace RoadsideHaven
{
    public enum BusinessType { Motel, Shop, GasStation, FastFood, Helipad, Bank }
    public sealed class BusinessSpec
    {
        public readonly string Id,Name;
        public readonly BusinessType Type;
        public readonly int Price,Income,UnlockOwned,UpgradeCost,MaxLevel;
        public readonly double Interval;
        public readonly bool Daily,Starter;
        public BusinessSpec(string id,string name,BusinessType type,int price,int income,double interval,bool daily,int unlockOwned,int upgradeCost,bool starter=false,int maxLevel=3)
        {
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(name)||price<0||income<0||double.IsNaN(interval)||double.IsInfinity(interval)||interval<1||unlockOwned<0||upgradeCost<1||maxLevel<0||maxLevel>10||(long)income*(maxLevel+1)>int.MaxValue||(long)upgradeCost*Math.Max(1,maxLevel)>int.MaxValue||(int)type<0||(int)type>5)throw new ArgumentException("Invalid business configuration.");
            Id=id;Name=name;Type=type;Price=price;Income=income;Interval=interval;Daily=daily;UnlockOwned=unlockOwned;UpgradeCost=upgradeCost;Starter=starter;MaxLevel=maxLevel;
        }
    }
    public sealed class BusinessHolding
    {
        public bool Owned {get;internal set;}
        public int Level {get;internal set;}
        public int Pending {get;internal set;}
        public int Investment {get;internal set;}
        internal double Timer;
    }
    /// <summary>Four-universe portfolio. Purchases and collections validate before mutation; clock deltas cannot pay twice.</summary>
    public sealed class BusinessLedger
    {
        private readonly BusinessSpec[] specs;
        private readonly BusinessHolding[,] holdings;
        private readonly Func<int,Wallet> wallets;
        private readonly int expansionThreshold;
        private int lastDay;
        public int Count=>specs.Length;
        public BusinessSpec Spec(int index)=>specs[index];
        public BusinessHolding Holding(int universe,int index)=>holdings[universe,index];
        public int OwnedCount(int universe){int total=0;for(int i=0;i<Count;i++)if(holdings[universe,i].Owned)total++;return total;}
        public bool MultiverseUnlocked=>OwnedCount(0)>=expansionThreshold;
        public BusinessLedger(BusinessSpec[] definitions,Func<int,Wallet> walletProvider,int startingDay=1,int unlockExpansionAt=8)
        {
            if(definitions==null||definitions.Length==0||walletProvider==null||startingDay<1||unlockExpansionAt<1)throw new ArgumentException();
            specs=(BusinessSpec[])definitions.Clone();wallets=walletProvider;lastDay=startingDay;expansionThreshold=unlockExpansionAt;
            var ids=new System.Collections.Generic.HashSet<string>();
            foreach(var spec in specs)if(spec==null||!ids.Add(spec.Id))throw new ArgumentException("Business IDs must be unique.");
            holdings=new BusinessHolding[4,Count];
            for(int u=0;u<4;u++)for(int i=0;i<Count;i++)holdings[u,i]=new BusinessHolding{Owned=u==0&&specs[i].Starter};
        }
        public bool CanBuy(int u,int i)
        {
            if(u<0||u>3||i<0||i>=Count||holdings[u,i].Owned)return false;
            return u==0?OwnedCount(0)>=specs[i].UnlockOwned:MultiverseUnlocked;
        }
        public bool Buy(int u,int i)
        {if(!CanBuy(u,i)||!wallets(u).TryDebit(specs[i].Price))return false;holdings[u,i].Owned=true;return true;}
        public int UpgradePrice(int u,int i)=>checked(specs[i].UpgradeCost*(holdings[u,i].Level+1));
        public bool Upgrade(int u,int i)
        {
            var holding=holdings[u,i];
            if(!holding.Owned||holding.Level>=specs[i].MaxLevel||!wallets(u).TryDebit(UpgradePrice(u,i)))return false;
            holding.Level++;return true;
        }
        public int Income(int u,int i)=>checked(specs[i].Income*(u==0&&specs[i].Starter?holdings[u,i].Level:holdings[u,i].Level+1));
        public void Tick(double seconds,int day)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0||day<1)throw new ArgumentOutOfRangeException();
            int passed=Math.Max(0,day-lastDay);lastDay=Math.Max(lastDay,day);
            for(int u=0;u<4;u++)for(int i=0;i<Count;i++)
            {
                var h=holdings[u,i];if(!h.Owned)continue;var spec=specs[i];
                double periods;
                if(spec.Daily)periods=passed;
                else{h.Timer+=seconds;periods=Math.Floor(h.Timer/spec.Interval);h.Timer%=spec.Interval;}
                double earned=periods*Income(u,i);
                if(spec.Type==BusinessType.Bank)earned+=(long)h.Investment*5/100L*passed;
                h.Pending=(int)Math.Min(int.MaxValue,(double)h.Pending+earned);
            }
        }
        public bool Collect(int u,int i)
        {
            var h=holdings[u,i];var wallet=wallets(u);
            if(!h.Owned||h.Pending<=0||(long)wallet.Coins+h.Pending>int.MaxValue)return false;
            wallet.Credit(h.Pending);h.Pending=0;return true;
        }
        public bool Invest(int u,int i,int amount)
        {
            var h=holdings[u,i];if(!h.Owned||specs[i].Type!=BusinessType.Bank||amount<=0||(long)h.Investment+amount>int.MaxValue||!wallets(u).TryDebit(amount))return false;
            h.Investment+=amount;return true;
        }
        public bool Withdraw(int u,int i)
        {
            var h=holdings[u,i];var wallet=wallets(u);
            if(!h.Owned||specs[i].Type!=BusinessType.Bank||h.Investment<=0||(long)wallet.Coins+h.Investment>int.MaxValue)return false;
            wallet.Credit(h.Investment);h.Investment=0;return true;
        }
    }
}
