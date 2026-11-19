using System;
using UnityEngine;
namespace RoadsideHaven
{
    public enum HumanRole { Civilian, Student, Doctor, Police, Owner }
    public enum HumanAge { Teen, YoungAdult, Adult, Senior }
    /// <summary>Stable identity without changing Unity's global random sequence.</summary>
    public struct HumanIdentity
    {
        public int Seed, HairStyle, Outfit;
        public HumanRole Role;
        public HumanAge Age;
        public bool Feminine, Glasses, Beard;
        public float Height, Build, FaceWidth, NoseLength, EyeSpacing, Pace;
        public Color Skin, Hair, Shirt, Trousers, Shoes, Iris;
        public static int StableSeed(string name)
        { unchecked { int hash=17; foreach(char c in name??"Citizen")hash=hash*31+c; return hash&int.MaxValue; } }
        public static HumanIdentity Create(int seed, HumanRole role=HumanRole.Civilian)
        {
            var random=new System.Random(seed); float Next(float a,float b)=>a+(float)random.NextDouble()*(b-a);
            var p=new HumanIdentity {Seed=seed,Role=role,Feminine=random.Next(2)==0};
            p.Age=role==HumanRole.Student?(random.Next(3)==0?HumanAge.YoungAdult:HumanAge.Teen):(HumanAge)random.Next(1,4);
            p.Height=Next(p.Feminine?1.59f:1.68f,p.Feminine?1.79f:1.91f); if(p.Age==HumanAge.Teen)p.Height*=.91f; if(p.Age==HumanAge.Senior)p.Height*=.97f;
            p.Build=Next(.87f,1.17f);p.FaceWidth=Next(.9f,1.12f);p.NoseLength=Next(.85f,1.25f);p.EyeSpacing=Next(.9f,1.12f);p.HairStyle=random.Next(4);p.Outfit=random.Next(4);
            Color[] skins={new Color(.91f,.72f,.55f),new Color(.74f,.48f,.31f),new Color(.46f,.29f,.20f),new Color(.65f,.39f,.24f),new Color(.96f,.81f,.68f),new Color(.32f,.20f,.15f)};
            p.Skin=skins[random.Next(skins.Length)]*Next(.94f,1.04f);p.Skin.a=1;
            p.Hair=p.Age==HumanAge.Senior?Color.Lerp(new Color(.28f,.27f,.25f),new Color(.78f,.77f,.73f),Next(.3f,1)):Color.Lerp(new Color(.035f,.024f,.018f),new Color(.33f,.17f,.065f),Next(0,1));
            p.Shirt=Color.HSVToRGB(Next(0,1),Next(.25f,.7f),Next(.35f,.85f));p.Trousers=Color.HSVToRGB(Next(.5f,.7f),Next(.1f,.35f),Next(.12f,.33f));p.Shoes=new Color(.065f,.055f,.045f);
            p.Iris=Color.Lerp(new Color(.15f,.08f,.035f),new Color(.18f,.35f,.36f),Next(0,1));p.Glasses=random.Next(5)==0||p.Age==HumanAge.Senior&&random.Next(2)==0;p.Beard=!p.Feminine&&p.Age!=HumanAge.Teen&&random.Next(3)==0;
            p.Pace=(p.Age==HumanAge.Senior?1.1f:p.Age==HumanAge.Teen?1.65f:1.45f)*Next(.85f,1.15f);
            if(role==HumanRole.Doctor){p.Shirt=new Color(.91f,.94f,.93f);p.Trousers=new Color(.15f,.35f,.42f);p.Outfit=2;}
            if(role==HumanRole.Police){p.Shirt=new Color(.065f,.13f,.23f);p.Trousers=new Color(.055f,.085f,.14f);p.Outfit=2;}
            if(role==HumanRole.Student){p.Shirt=new Color(.79f,.87f,.92f);p.Trousers=new Color(.09f,.17f,.29f);p.Outfit=1;}
            if(role==HumanRole.Owner){p.Shirt=new Color(.17f,.32f,.49f);p.Trousers=new Color(.12f,.16f,.23f);p.Outfit=2;}
            return p;
        }
        public static HumanRole RoleFromName(string name)
        {name=(name??"").ToLowerInvariant();return name.Contains("doctor")?HumanRole.Doctor:name.Contains("police")?HumanRole.Police:name.Contains("student")?HumanRole.Student:HumanRole.Civilian;}
    }
}
