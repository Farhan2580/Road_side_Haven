using System;
using UnityEngine;
namespace RoadsideHaven
{
    [CreateAssetMenu(menuName="Roadside Haven/Business Catalog")]
    public sealed class BusinessCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string id,name;
            public BusinessType type;
            [Min(0)]public int price,income;
            [Min(1)]public float interval=60;
            public bool daily,starter;
            [Min(0)]public int unlockOwned;
            [Min(1)]public int upgradeCost=80;
        }
        [Header("Business properties")]
        [SerializeField] private Entry[] properties;
        [SerializeField,Min(1)] private int expansionThreshold=8;
        public int ExpansionThreshold=>expansionThreshold;
        public BusinessSpec[] Build()
        {
            if(properties==null)throw new InvalidOperationException("Business catalog is missing.");
            var result=new BusinessSpec[properties.Length];
            for(int i=0;i<result.Length;i++){var p=properties[i];result[i]=new BusinessSpec(p.id,p.name,p.type,p.price,p.income,p.interval,p.daily,p.unlockOwned,p.upgradeCost,p.starter);}
            return result;
        }
        public void ConfigureDefaults()
        {
            properties=new[]{Make("base-motel","Home Motel",BusinessType.Motel,250,12,60,false,0,80,true),Make("base-shop","Home Shop",BusinessType.Shop,160,8,60,true,0,60,true),
                Make("highway-motel","Highway Motel",BusinessType.Motel,350,25,60,false,2,100),Make("city-shop","City Shop",BusinessType.Shop,220,85,60,true,2,80),
                Make("gas","Highway Gas",BusinessType.GasStation,450,30,75,false,3,120),Make("food","City Fast Food",BusinessType.FastFood,400,22,60,false,3,110),
                Make("helipad","VIP Helipad",BusinessType.Helipad,750,50,90,false,6,180),Make("bank","City Bank",BusinessType.Bank,1200,100,60,true,7,250)};
        }
        private static Entry Make(string id,string name,BusinessType type,int price,int income,float interval,bool daily,int unlock,int upgrade,bool starter=false)
            =>new Entry{id=id,name=name,type=type,price=price,income=income,interval=interval,daily=daily,unlockOwned=unlock,upgradeCost=upgrade,starter=starter};
    }
}
