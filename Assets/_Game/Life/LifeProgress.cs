using System;
using System.Collections.Generic;
namespace RoadsideHaven
{
    /// <summary>Session progression with atomic purchases and exactly-once quest completion.</summary>
    public sealed class LifeProgress
    {
        private readonly IEconomyService economy;
        private readonly bool[] owned={false,true,false};
        private readonly int[] levels=new int[3];
        private readonly Dictionary<string,int> relations=new Dictionary<string,int>();
        private readonly Dictionary<string,float> greetingTimes=new Dictionary<string,float>();
        public int Reputation {get;private set;}
        public int Trouble {get;private set;}
        public int Guards {get;private set;}
        public bool DeliveryQuest {get;private set;}
        public LifeProgress(IEconomyService wallet){economy=wallet??throw new ArgumentNullException(nameof(wallet));}
        public bool Owns(int index)=>index>=0&&index<3&&owned[index];
        public int UpgradeLevel(int index)=>index>=0&&index<3?levels[index]:0;
        public int Price(int index)=>index==0?80:index==2?100:0;
        public int UpgradePrice(int index)=>40*(UpgradeLevel(index)+1);
        public bool Buy(int index){if(index<0||index>=3||owned[index]||!economy.TryDebit(Price(index)))return false;owned[index]=true;return true;}
        public bool Upgrade(int index){if(!Owns(index)||levels[index]>=3||!economy.TryDebit(UpgradePrice(index)))return false;levels[index]++;return true;}
        public int Relationship(string id)=>id!=null&&relations.TryGetValue(id,out int value)?value:0;
        public bool Greet(string id,float time)
        {
            if(string.IsNullOrWhiteSpace(id)||greetingTimes.TryGetValue(id,out float last)&&time-last<60)return false;
            greetingTimes[id]=time;relations[id]=Math.Min(10,Relationship(id)+1);return true;
        }
        public void Insult(string id){if(string.IsNullOrWhiteSpace(id))return;relations[id]=Math.Max(-10,Relationship(id)-2);Trouble=Math.Min(3,Trouble+1);Reputation=Math.Max(-20,Reputation-2);}
        public bool AcceptQuest(){if(DeliveryQuest)return false;DeliveryQuest=true;return true;}
        public bool CompleteDelivery(){if(!DeliveryQuest)return false;economy.Credit(35);DeliveryQuest=false;Reputation=Math.Min(20,Reputation+3);return true;}
        public bool HireGuard(){if(Guards>=2||!economy.TryDebit(30))return false;Guards++;return true;}
        public bool PayGuardWages(){if(Guards==0)return true;if(economy.TryDebit(Guards*10))return true;Guards=0;return false;}
        public bool PayFine(){if(Trouble<3||!economy.TryDebit(40))return false;Trouble=0;return true;}
        public void RecordTrouble(){Trouble=Math.Min(3,Trouble+1);}
        public void Harassed(){Reputation=Math.Max(-20,Reputation-1);}
        public void Defended(){Reputation=Math.Min(20,Reputation+1);}
    }
}
