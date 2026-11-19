using System;
using UnityEngine;
namespace RoadsideHaven
{
    public enum HumanRole { Civilian, Student, Doctor, Police, Owner }
    // Child is appended so existing numeric ages keep their meaning.
    public enum HumanAge { Teen, YoungAdult, Adult, Senior, Child }
    public enum HumanTop { TShirt, Shirt, Blazer, Hoodie, Jacket, Dress, Uniform }
    public enum HumanBottom { Jeans, Chinos, Shorts, Skirt, None }
    /// <summary>Stable identity without changing Unity's global random sequence.</summary>
    public struct HumanIdentity
    {
        public int Seed, HairStyle, Outfit;
        public HumanRole Role;
        public HumanAge Age;
        public HumanTop Top;
        public HumanBottom Bottom;
        public bool Feminine, Glasses, Beard, Stubble, Cap, Backpack, Handbag, Phone, Earrings, Umbrella, Print;
        public float Height, Build, FaceWidth, NoseLength, EyeSpacing, Pace, HeadScale, Belly;
        // Personality drives the citizen mind: chatting, curiosity and how quickly they lose patience.
        public float Sociability, Curiosity, Patience;
        public Color Skin, Hair, Shirt, Trousers, Shoes, Iris, Accent, Lips;
        public bool IsChild => Age == HumanAge.Child;
        public static int StableSeed(string name)
        { unchecked { int hash=17; foreach(char c in name??"Citizen")hash=hash*31+c; return hash&int.MaxValue; } }
        static readonly Color[] Fabrics =
        {
            new Color(.93f,.93f,.9f), new Color(.08f,.08f,.09f), new Color(.17f,.24f,.38f), new Color(.55f,.6f,.62f), new Color(.42f,.47f,.33f),
            new Color(.62f,.18f,.17f), new Color(.85f,.72f,.52f), new Color(.24f,.42f,.55f), new Color(.71f,.52f,.6f), new Color(.88f,.62f,.25f),
            new Color(.3f,.22f,.18f), new Color(.5f,.66f,.78f), new Color(.2f,.45f,.38f), new Color(.82f,.8f,.55f), new Color(.45f,.3f,.52f)
        };
        public static HumanIdentity Create(int seed, HumanRole role=HumanRole.Civilian) => Create(seed, role, null);
        public static HumanIdentity Create(int seed, HumanRole role, HumanAge? forcedAge)
        {
            var random=new System.Random(seed); float Next(float a,float b)=>a+(float)random.NextDouble()*(b-a); bool Chance(float p)=>random.NextDouble()<p;
            Color Pick()=>Fabrics[random.Next(Fabrics.Length)];
            var p=new HumanIdentity {Seed=seed,Role=role,Feminine=random.Next(2)==0};
            if(forcedAge.HasValue)p.Age=forcedAge.Value;
            else if(role==HumanRole.Student)p.Age=random.Next(3)==0?HumanAge.YoungAdult:HumanAge.Teen;
            else {float a=Next(0,1);p.Age=a<.12f?HumanAge.Teen:a<.42f?HumanAge.YoungAdult:a<.75f?HumanAge.Adult:HumanAge.Senior;}
            if(role==HumanRole.Doctor||role==HumanRole.Police||role==HumanRole.Owner){if(p.Age==HumanAge.Teen||p.Age==HumanAge.Child)p.Age=HumanAge.YoungAdult;}
            p.Height=Next(p.Feminine?1.59f:1.68f,p.Feminine?1.79f:1.91f);
            if(p.Age==HumanAge.Teen)p.Height*=.91f; if(p.Age==HumanAge.Senior)p.Height*=.97f; if(p.Age==HumanAge.Child)p.Height=Next(1.02f,1.42f);
            p.HeadScale=p.Age==HumanAge.Child?Mathf.Lerp(1.34f,1.2f,(p.Height-1.02f)/.4f):p.Age==HumanAge.Teen?1.06f:1;
            p.Build=p.Age==HumanAge.Child?Next(.8f,.95f):Next(.87f,1.17f);
            p.Belly=(p.Age==HumanAge.Adult||p.Age==HumanAge.Senior)&&p.Build>1.06f&&Chance(.6f)?Next(.4f,1):0;
            p.FaceWidth=Next(.9f,1.12f);p.NoseLength=Next(.85f,1.25f);p.EyeSpacing=Next(.9f,1.12f);
            p.HairStyle=p.Feminine?new[]{0,2,2,4,5,6,7,7}[random.Next(8)]:new[]{0,0,1,1,3,6}[random.Next(6)];
            if(!p.Feminine&&p.Age==HumanAge.Senior&&Chance(.4f))p.HairStyle=3;
            Color[] skins={new Color(.91f,.72f,.55f),new Color(.74f,.48f,.31f),new Color(.46f,.29f,.20f),new Color(.65f,.39f,.24f),new Color(.96f,.81f,.68f),new Color(.32f,.20f,.15f),new Color(.84f,.62f,.44f),new Color(.56f,.36f,.24f)};
            p.Skin=skins[random.Next(skins.Length)]*Next(.94f,1.04f);p.Skin.a=1;
            Color[] hairs={new Color(.035f,.024f,.018f),new Color(.12f,.07f,.04f),new Color(.26f,.15f,.07f),new Color(.6f,.46f,.25f),new Color(.4f,.15f,.06f),new Color(.05f,.04f,.035f)};
            p.Hair=p.Age==HumanAge.Senior?Color.Lerp(new Color(.36f,.34f,.31f),new Color(.82f,.81f,.77f),Next(.3f,1)):hairs[random.Next(hairs.Length)]*Next(.9f,1.1f);p.Hair.a=1;
            if(p.Age==HumanAge.YoungAdult&&Chance(.06f))p.Hair=new Color(.86f,.83f,.76f);
            p.Iris=Color.Lerp(new Color(.15f,.08f,.035f),new Color(.18f,.35f,.36f),Next(0,1));
            p.Glasses=Chance(.18f)&&p.Age!=HumanAge.Child||p.Age==HumanAge.Senior&&Chance(.5f);
            bool grown=p.Age!=HumanAge.Teen&&p.Age!=HumanAge.Child;
            p.Beard=!p.Feminine&&grown&&Chance(.3f);p.Stubble=!p.Feminine&&grown&&!p.Beard&&Chance(.35f);
            p.Pace=(p.Age==HumanAge.Senior?1.1f:p.Age==HumanAge.Teen?1.65f:p.Age==HumanAge.Child?1.25f:1.45f)*Next(.85f,1.15f);
            // Modern everyday clothes by age.
            float t=Next(0,1);
            if(p.Age==HumanAge.Child||p.Age==HumanAge.Teen)p.Top=t<.45f?HumanTop.TShirt:t<.8f?HumanTop.Hoodie:p.Feminine&&t<.92f?HumanTop.Dress:HumanTop.Jacket;
            else if(p.Age==HumanAge.Senior)p.Top=t<.3f?HumanTop.Shirt:t<.6f?HumanTop.Jacket:p.Feminine&&t<.85f?HumanTop.Dress:HumanTop.Blazer;
            else p.Top=t<.25f?HumanTop.TShirt:t<.45f?HumanTop.Shirt:t<.62f?HumanTop.Blazer:t<.76f?HumanTop.Hoodie:t<.88f?HumanTop.Jacket:p.Feminine?HumanTop.Dress:HumanTop.TShirt;
            float b=Next(0,1);
            p.Bottom=p.Top==HumanTop.Dress?HumanBottom.None:p.Feminine&&b<.25f?HumanBottom.Skirt:b<.55f?HumanBottom.Jeans:b<.8f?HumanBottom.Chinos:HumanBottom.Shorts;
            if(p.Age==HumanAge.Senior&&p.Bottom==HumanBottom.Shorts)p.Bottom=HumanBottom.Chinos;
            p.Shirt=Pick()*Next(.92f,1.05f);p.Shirt.a=1;p.Accent=Pick();
            p.Trousers=p.Bottom==HumanBottom.Jeans?Color.Lerp(new Color(.12f,.18f,.3f),new Color(.36f,.46f,.6f),Next(0,1)):p.Bottom==HumanBottom.Chinos?Color.Lerp(new Color(.66f,.58f,.42f),new Color(.16f,.17f,.2f),Next(0,1)):Pick();
            p.Shoes=Chance(.4f)?new Color(.92f,.92f,.9f):Chance(.5f)?new Color(.065f,.055f,.045f):new Color(.3f,.18f,.1f);
            p.Print=p.Top==HumanTop.TShirt&&Chance(.6f);
            p.Lips=p.Feminine&&grown&&Chance(.5f)?Color.Lerp(p.Skin,new Color(.62f,.12f,.18f),.55f):Color.Lerp(p.Skin,new Color(.46f,.14f,.13f),.43f);
            p.Cap=!p.Feminine&&p.HairStyle!=2&&(p.Age==HumanAge.Teen||p.Age==HumanAge.YoungAdult||p.Age==HumanAge.Child)&&Chance(.25f);
            p.Backpack=(p.Age==HumanAge.Child||p.Age==HumanAge.Teen)&&Chance(.6f)||p.Age==HumanAge.YoungAdult&&Chance(.25f);
            p.Handbag=p.Feminine&&grown&&!p.Backpack&&Chance(.45f);
            p.Phone=p.Age!=HumanAge.Child&&Chance(p.Age==HumanAge.Senior?.12f:.42f);
            p.Earrings=p.Feminine&&p.Age!=HumanAge.Child&&Chance(.45f);
            p.Umbrella=grown&&Chance(.55f);
            p.Sociability=Next(.2f,1);p.Curiosity=Next(.1f,1);p.Patience=Next(.3f,1);
            if(p.Age==HumanAge.Senior){p.Sociability=Mathf.Min(1,p.Sociability+.2f);p.Patience=Mathf.Min(1,p.Patience+.2f);}
            if(role==HumanRole.Doctor){p.Shirt=new Color(.91f,.94f,.93f);p.Trousers=new Color(.15f,.35f,.42f);p.Top=HumanTop.Uniform;p.Bottom=HumanBottom.Chinos;}
            if(role==HumanRole.Police){p.Shirt=new Color(.065f,.13f,.23f);p.Trousers=new Color(.055f,.085f,.14f);p.Top=HumanTop.Uniform;p.Bottom=HumanBottom.Chinos;p.Cap=false;}
            if(role==HumanRole.Student){p.Shirt=new Color(.79f,.87f,.92f);p.Trousers=new Color(.09f,.17f,.29f);p.Top=HumanTop.Shirt;p.Bottom=p.Feminine&&random.Next(2)==0?HumanBottom.Skirt:HumanBottom.Chinos;p.Backpack=true;}
            if(role==HumanRole.Owner){p.Shirt=new Color(.17f,.32f,.49f);p.Trousers=new Color(.12f,.16f,.23f);p.Top=HumanTop.Blazer;p.Bottom=HumanBottom.Chinos;p.Cap=false;p.Phone=false;p.Handbag=false;p.Backpack=false;}
            p.Outfit=p.Top==HumanTop.TShirt||p.Top==HumanTop.Dress?0:p.Top==HumanTop.Shirt?1:p.Top==HumanTop.Hoodie?3:2;
            return p;
        }
        public static HumanRole RoleFromName(string name)
        {name=(name??"").ToLowerInvariant();return name.Contains("doctor")?HumanRole.Doctor:name.Contains("police")?HumanRole.Police:name.Contains("student")?HumanRole.Student:HumanRole.Civilian;}
    }
}
