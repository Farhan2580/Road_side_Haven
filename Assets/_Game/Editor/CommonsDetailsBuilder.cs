using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven.Editor
{
    /// <summary>Additive, densely planted garden rooms and street amenities. Existing roads, entrances and visitor loops are reserved.</summary>
    public static class CommonsDetailsBuilder
    {
        public const string Layer="Haven Commons Gardens and Street Details v1";
        public const string Folder="Assets/_Game/Generated/CommonsDetails";
        private static Material stone,cream,ink,wood,bronze,leaf,soil,lawn,rose,gold,violet,linen,water,skin,warm,asphalt,shirt,groundCover;
        private static int index;
        private static readonly List<Renderer> lamps=new List<Renderer>();
        private static readonly List<Transform> arms=new List<Transform>();
        public static Vector3[] FrontRoute=>EllipsePoints(new Vector3(760,0,-58),30,19,80);
        public static Vector3[] RearRoute=>EllipsePoints(new Vector3(790,0,-184),150,9,128);
        [MenuItem("Roadside Haven/Design/Add Haven Commons Garden Details")]
        public static void BuildCurrent(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;var m=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(m==null)return;var s=m.gameObject.scene;if(File.Exists(s.path)){Directory.CreateDirectory("Backups/CommonsDetails");File.Copy(s.path,"Backups/CommonsDetails/Before-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity");}Build(m);EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);AssetDatabase.SaveAssets();}
        [MenuItem("Roadside Haven/Design/Focus Haven Commons Garden Details")]
        public static void Focus(){var m=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(m==null)return;Selection.activeGameObject=m.transform.Find(Layer)?.gameObject;SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(785,4,-108),new Vector3(390,22,220)),false);}
        [MenuItem("Roadside Haven/Design/Visit Commons Garden Details in Play Mode")]
        public static void Visit()
        {
            if(!EditorApplication.isPlaying){Debug.Log("Press Play first to visit the garden details.");return;}
            var owner=UnityEngine.Object.FindFirstObjectByType<OwnerSandboxController>();if(owner!=null&&owner.IsWalking&&!owner.HasSupplyCargo)owner.ExitVehicle(new Vector3(760,.2f,-87));
        }
        public static Transform Build(MotelGameRoot m)
        {
            var old=m.transform.Find(Layer);if(old!=null)return old;if(m.transform.Find(HavenCommonsBuilder.Layer)==null)throw new InvalidOperationException("Build Haven Commons before adding its details.");if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","CommonsDetails");index=0;lamps.Clear();arms.Clear();Materials();
            var root=new GameObject(Layer).transform;root.SetParent(m.transform,false);root.gameObject.AddComponent<MotelDistrictArt>();var life=root.gameObject.AddComponent<CommonsDetailLife>();
            ArrivalGardens(root,m);GardenConnections(root);KiosksAndPergolas(root);VenueDetails(root);NorthDetails(root);Woodland(root);
            var people=new TownCitizenAgent[6];for(int i=0;i<people.Length;i++){var g=new GameObject("Commons detail walker "+i);g.transform.SetParent(root,false);g.AddComponent<CharacterController>().enabled=false;people[i]=g.AddComponent<TownCitizenAgent>();}
            Combine(root);life.Configure(people,FrontRoute,RearRoute,new[]{shirt,linen,violet},skin,arms.ToArray());RegisterLights(m);foreach(var t in root.GetComponentsInChildren<TMP_Text>())t.ForceMeshUpdate(true,true);AssetDatabase.SaveAssets();return root;
        }
        private static void Materials()
        {
            stone=Load(HavenCommonsBuilder.Folder,"Commons travertine");cream=Load(HavenCommonsBuilder.Folder,"Sculptural ivory");ink=Load(HavenCommonsBuilder.Folder,"Commons graphite");wood=Load(HavenCommonsBuilder.Folder,"Pavilion cedar");bronze=Load(HavenCommonsBuilder.Folder,"Sculpture bronze");leaf=Load(MotelDistrictArtBuilder.Folder,"Palm foliage");lawn=Load(HavenCommonsBuilder.Folder,"Commons meadow");asphalt=Load(HavenCommonsBuilder.Folder,"Commons fine asphalt");water=Load(HavenCommonsBuilder.Folder,"Commons turquoise water");skin=Load(HavenCommonsBuilder.Folder,"Commons visitor skin");warm=Load(HavenCommonsBuilder.Folder,"Commons lantern glass");
            soil=Mat("Rich planting soil",new Color(.15f,.12f,.085f));rose=Mat("Coral flower petals",new Color(.75f,.19f,.2f));gold=Mat("Golden meadow flowers",new Color(.94f,.66f,.18f));violet=Mat("Lavender flowers",new Color(.41f,.27f,.6f));linen=Mat("Sage cushions",new Color(.28f,.38f,.31f));shirt=Mat("Visitor denim",new Color(.16f,.28f,.38f));groundCover=Mat("Evergreen ground cover",new Color(.35f,.5f,.27f));groundCover.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/BaseGrass_Albedo.png");
        }
        private static void ArrivalGardens(Transform root,MotelGameRoot m)
        {
            Floor(root,"Arrival garden ground",Rect.MinMaxRect(624,-89,962,-28),.04f,lawn);
            Floor(root,"Visitor parking asphalt",Rect.MinMaxRect(628,-78,664,-36),.06f,asphalt);Floor(root,"Flush visitor parking entrance",Rect.MinMaxRect(614.04f,-50,646,-44),.06f,asphalt);
            for(int i=0;i<11;i++)foreach(float x in new[]{634f,658f})Box(root,"Parking stall marking",new Vector3(x,.068f,-39-i*3.5f),new Vector3(10,.003f,.065f),cream,false);
            for(int i=0;i<4;i++)Box(root,"Parking aisle dash",new Vector3(646,.068f,-55-i*6),new Vector3(.1f,.003f,2),cream,false);
            var cars=m.transform.Find(MainRoadFrontageBuilder.Layer)?.GetComponentsInChildren<EnterableVehicle>();if(cars!=null&&cars.Length>=2)for(int i=0;i<2;i++){var car=UnityEngine.Object.Instantiate(cars[i].gameObject,root);car.name="Commons visitor car "+(i+1);car.transform.position=new Vector3(i==0?634:658,.07f,i==0?-61.75f:-68.75f);car.transform.rotation=Quaternion.Euler(0,90,0);}
            foreach(var p in new[]{new Vector3(627,0,-79),new Vector3(666,0,-79),new Vector3(627,0,-34)})Tree(root,p,6.5f,0);
            Ribbon(root,"Rose garden walking loop",FrontRoute,1.9f,.105f,stone,true);Ribbon(root,"Rose garden central walk",new[]{new Vector3(760,0,-27),new Vector3(760,0,-95)},2,.108f,stone,true);
            Ribbon(root,"Garden entrance east walk",Smooth(new[]{new Vector3(880,0,-27),new Vector3(878,0,-43),new Vector3(877,0,-64),new Vector3(886,0,-80),new Vector3(880,0,-97)},8),2,.105f,stone,true);
            Ribbon(root,"Arrival garden crosswalk",Smooth(new[]{new Vector3(676,0,-84),new Vector3(705,0,-85),new Vector3(732,0,-84),new Vector3(760,0,-80),new Vector3(812,0,-81),new Vector3(851,0,-84),new Vector3(882,0,-85),new Vector3(955,0,-84)},8),1.6f,.105f,stone,true);
            Bed(root,new Vector3(746,0,-58),8,12,0,42);Bed(root,new Vector3(774,0,-58),8,12,2,42);
            foreach(float z in new[]{-50f,-58f,-66f}){var arch=Group(root,"Rose arcade arch",new Vector3(760,0,z));for(int i=0;i<24;i++){float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;Pipe(arch,"Ivory arch rib",new Vector3(Mathf.Cos(a)*6,.16f+Mathf.Sin(a)*5.5f,0),new Vector3(Mathf.Cos(b)*6,.16f+Mathf.Sin(b)*5.5f,0),.2f,cream);}foreach(int side in new[]{-1,1})Box(arch,"Rose arch footing",new Vector3(side*6,.3f,0),new Vector3(.55f,.4f,.6f),stone,true);}
            for(int side=-1;side<=1;side+=2)Pipe(root,"Arcade connecting rail",new Vector3(760+side*5.2f,2.95f,-50),new Vector3(760+side*5.2f,2.95f,-66),.075f,bronze);
            foreach(float x in new[]{710f,805f}){Pergola(root,new Vector3(x,0,-58),10,10);Bench(root,new Vector3(x-3,0,-58),90);Bench(root,new Vector3(x+3,0,-58),-90);Bed(root,new Vector3(x,0,-68),7,2.5f,1,22);Tree(root,new Vector3(x,0,-43),7,1);}
            ChessTerrace(root);Fitness(root);
            foreach(var p in new[]{new Vector3(696,0,-33),new Vector3(811,0,-33),new Vector3(852,0,-34),new Vector3(951,0,-33),new Vector3(697,0,-78),new Vector3(815,0,-74),new Vector3(942,0,-82)})Tree(root,p,7.5f,((int)p.x)%3);
            foreach(var p in new[]{new Vector3(683,0,-41),new Vector3(799,0,-38),new Vector3(864,0,-73),new Vector3(949,0,-70)})Bed(root,p,6,3,((int)p.x)%3,28);
            foreach(var p in new[]{new Vector3(670,0,-39),new Vector3(724,0,-37),new Vector3(796,0,-37),new Vector3(897,0,-36),new Vector3(959,0,-78)})Lantern(root,p);
            Board(root,new Vector3(680,0,-34),"HAVEN COMMONS\nGARDEN / PICNIC / EVENTS",180);Board(root,new Vector3(767,0,-32),"ROSE ARCADE\nGARDEN WALK",180);
        }
        private static void ChessTerrace(Transform root)
        {
            var g=Group(root,"Garden chess terrace",new Vector3(846,0,-58));Disc(g,"Chess terrace paving",Vector3.up*.105f,15,15,stone,true);Pergola(g,new Vector3(0,0,0),23,22);
            int n=0;foreach(float x in new[]{-6f,6f})foreach(float z in new[]{-6f,6f}){Box(g,"Chess table pedestal",new Vector3(x,.56f,z),new Vector3(.28f,1,.28f),bronze,true);Box(g,"Chess table stone slab",new Vector3(x,1.08f,z),new Vector3(1.8f,.12f,1.8f),stone,true);for(int i=0;i<8;i++)for(int j=0;j<8;j++)Box(g,"Chess square",new Vector3(x-.7f+i*.2f,1.145f,z-.7f+j*.2f),new Vector3(.195f,.003f,.195f),(i+j)%2==0?cream:ink,false);
                for(int side=-1;side<=1;side+=2)for(int i=0;i<8;i++){float px=x-.7f+i*.2f,pz=z+side*.5f;Box(g,"Chess pawn",new Vector3(px,1.21f,pz),new Vector3(.085f,.065f,.085f),side<0?bronze:cream,false,PrimitiveType.Cylinder);Box(g,"Chess pawn head",new Vector3(px,1.30f,pz),Vector3.one*.095f,side<0?bronze:cream,false,PrimitiveType.Sphere);}
                foreach(int side in new[]{-1,1})Chair(g,new Vector3(x+side*1.65f,0,z),side*90);if(n++<2)Person(g,new Vector3(x-1.65f,0,z),-90,true,shirt);
            }
            Board(root,new Vector3(856,0,-40),"CHESS TERRACE\nTAKE A SEAT",180);
        }
        private static void Fitness(Transform root)
        {
            var g=Group(root,"Outdoor fitness garden",new Vector3(920,0,-58));Disc(g,"Fitness rubber court",Vector3.up*.10f,22,15,Load(HavenCommonsBuilder.Folder,"Playground sage"),true);
            foreach(float x in new[]{-9f,0f,9f}){foreach(int s in new[]{-1,1})Pipe(g,"Fitness bar upright",new Vector3(x+s*2,.1f,-5),new Vector3(x+s*2,2.6f,-5),.1f,bronze,true);Pipe(g,"Fitness pull-up bar",new Vector3(x-2,2.6f,-5),new Vector3(x+2,2.6f,-5),.08f,ink);}
            foreach(float x in new[]{-8f,8f})foreach(int s in new[]{-1,1}){Pipe(g,"Parallel bar leg",new Vector3(x+s*.7f,.1f,2),new Vector3(x+s*.7f,1.25f,2),.08f,bronze,true);Pipe(g,"Parallel bar leg",new Vector3(x+s*.7f,.1f,8),new Vector3(x+s*.7f,1.25f,8),.08f,bronze,true);Pipe(g,"Parallel exercise rail",new Vector3(x+s*.7f,1.25f,2),new Vector3(x+s*.7f,1.25f,8),.08f,ink);}
            Bench(g,new Vector3(0,0,11),180);Board(root,new Vector3(942,0,-40),"FITNESS GARDEN\nSTRETCH / TRAIN / RELAX",180);BottleStation(root,new Vector3(894,0,-55));
        }
        private static void GardenConnections(Transform root)
        {
            var west=Smooth(new[]{new Vector3(475,0,-174),new Vector3(472,0,-140),new Vector3(480,0,-112),new Vector3(501,0,-104.5f),new Vector3(547,0,-104.5f),new Vector3(567,0,-118),new Vector3(568,0,-143),new Vector3(562,0,-168),new Vector3(545,0,-182),new Vector3(507,0,-182),new Vector3(475,0,-174)},8);Ribbon(root,"Conservatory garden loop",west,1.7f,.105f,stone,true);
            Ribbon(root,"Woodland rear promenade",RearRoute,1.8f,.105f,stone,true);
            foreach(float x in new[]{650f,752f,880f})Ribbon(root,"Rear venue connection",new[]{new Vector3(x,0,-184),new Vector3(x,0,-163)},1.7f,.105f,stone,true);
            foreach(var p in new[]{new Vector3(577,0,-135),new Vector3(700,0,-139),new Vector3(818,0,-142),new Vector3(942,0,-137)}){Pergola(root,p,11,9);Bench(root,p+new Vector3(-3.5f,0,0),90);Bench(root,p+new Vector3(3.5f,0,0),-90);Table(root,p);Bed(root,p+new Vector3(0,0,7),6,2.2f,((int)p.x)%3,24);}
            foreach(var p in new[]{new Vector3(483,0,-128),new Vector3(555,0,-166),new Vector3(691,0,-166),new Vector3(720,0,-152),new Vector3(803,0,-156),new Vector3(833,0,-165),new Vector3(933,0,-170)})Bed(root,p,7,4,((int)p.x)%3,32);
            foreach(var p in new[]{new Vector3(489,0,-163),new Vector3(561,0,-132),new Vector3(692,0,-148),new Vector3(816,0,-128),new Vector3(931,0,-149)}){Tree(root,p,7,1);Bench(root,p+new Vector3(0,0,5),0);}
            foreach(float a in new[]{.25f,.65f,1.02f,2.1f,2.5f,2.9f,3.35f,3.8f,5.2f,5.7f,6f}){var p=new Vector3(760+Mathf.Cos(a)*29,0,-136+Mathf.Sin(a)*29);Bed(root,p,3.6f,2.2f,((int)(a*10))%3,18);}
            foreach(float x in new[]{629f,684f,731f,801f,848f,922f,953f})Lantern(root,new Vector3(x,0,-193));
            Ribbon(root,"Botanical kiosk link",Smooth(new[]{new Vector3(573,0,-113),new Vector3(555,0,-108),new Vector3(536,0,-111),new Vector3(524,0,-112)},8),1.4f,.105f,stone,true);
        }
        private static void KiosksAndPergolas(Transform root)
        {
            Kiosk(root,new Vector3(573,0,-117),"VERDANT COFFEE",0,wood);Kiosk(root,new Vector3(690,0,-116),"CRESCENT TICKETS",1,cream);Kiosk(root,new Vector3(809,0,-116),"ART & BOOKS",2,wood);Kiosk(root,new Vector3(937,0,-119),"FRESH JUICE",0,rose);
            Kiosk(root,new Vector3(704,0,-167),"LOCAL MAKERS",2,wood,0);Kiosk(root,new Vector3(810,0,-166),"GARDEN MARKET",1,cream,0);
            BikeRack(root,new Vector3(594,0,-113));BikeRack(root,new Vector3(680,0,-32));BikeRack(root,new Vector3(897,0,-79));
            Bike(root,new Vector3(681,0,-32.3f),90);Bike(root,new Vector3(898,0,-79.3f),0);
        }
        private static void Kiosk(Transform root,Vector3 p,string title,int kind,Material accent,float yaw=180)
        {
            var g=Group(root,title+" kiosk",p,yaw);Floor(g,"Kiosk stone pad",Rect.MinMaxRect(-6,-5.5f,6,5),.105f,stone);
            foreach(int side in new[]{-1,1})Box(g,"Kiosk slender column",new Vector3(side*4.6f,1.9f,1.9f),new Vector3(.14f,3.5f,.14f),bronze,true);
            Box(g,"Kiosk counter",new Vector3(0,.75f,0),new Vector3(8.6f,1.25f,1.7f),accent,true);Box(g,"Counter stone cap",new Vector3(0,1.43f,0),new Vector3(9,.12f,2),stone,false);Box(g,"Display shelving back",new Vector3(0,1.5f,3),new Vector3(8.4f,2.6f,.22f),wood,true);
            for(int row=0;row<3;row++){Box(g,"Kiosk shelf",new Vector3(0,.8f+row*.65f,2.65f),new Vector3(8,.08f,.8f),bronze,false);for(int i=0;i<9;i++)Box(g,kind==2?"Books and craft wares":"Jars and packets",new Vector3(-3.5f+i*.85f,1.05f+row*.65f,2.5f),new Vector3(kind==2?.23f:.3f,.42f,.35f),i%2==0?cream:kind==0?gold:violet,false);}
            var v=new[]{new Vector3(-5,3.4f,4),new Vector3(5,3.4f,4),new Vector3(5.4f,3.15f,-4),new Vector3(-5.4f,3.15f,-4),new Vector3(0,4.1f,0)};MeshPart(g,"Curved kiosk awning",v,new[]{0,4,1,1,4,2,2,4,3,3,4,0},kind==1?cream:linen,false);
            Box(g,"Mounted kiosk fascia",new Vector3(0,2.9f,-3.8f),new Vector3(9,.65f,.12f),ink,false);Text(g,title,new Vector3(0,2.92f,-3.89f),.24f,8.5f,cream.color);
            Box(g,"Standing menu plaque",new Vector3(-5,1.2f,-3.5f),new Vector3(.9f,1.5f,.12f),ink,true);Text(g,kind==0?"COFFEE\nFRESH BAKES":kind==1?"TODAY\nIN THE PARK":"HANDMADE\nLOCAL ART",new Vector3(-5,1.4f,-3.58f),.10f,.8f,cream.color);
            if(kind==0){CoffeeMachine(g,new Vector3(-2,1.5f,0));Box(g,"Pastry glass case",new Vector3(2.3f,1.85f,0),new Vector3(2.6f,.6f,1.4f),Load(GrandBoulevardBuilder.Folder,"Galleria clear glazing"),false);for(int i=0;i<6;i++)Box(g,"Bakery display bun",new Vector3(1.4f+(i%3)*.85f,1.6f,-.3f+(i/3)*.6f),new Vector3(.5f,.2f,.4f),gold,false,PrimitiveType.Capsule);}else{Box(g,"Counter terminal",new Vector3(2,1.85f,0),new Vector3(.65f,.55f,.12f),ink,false);}
            Person(g,new Vector3(0,0,1.7f),0,false,kind==0?linen:shirt);Bin(g,new Vector3(5,0,3.5f));var start=g.TransformPoint(new Vector3(0,0,-5));var end=new Vector3(start.x,0,p.z> -150?-102:p.z-12);Ribbon(root,"Kiosk approach",new[]{start,end},1.3f,.105f,stone,true);
        }
        private static void VenueDetails(Transform root)
        {
            // Stage equipment has its own space, leaving the audience centre aisle clear.
            foreach(float x in new[]{637f,663f}){Box(root,"Stage PA cabinet",new Vector3(x,1.1f,-143),new Vector3(1.1f,1.8f,.9f),ink,true);foreach(float y in new[]{.6f,1.4f})Box(root,"PA speaker driver",new Vector3(x,y,-142.52f),new Vector3(.65f,.045f,.65f),bronze,false,PrimitiveType.Cylinder).transform.localRotation=Quaternion.Euler(90,0,0);}
            Pipe(root,"Microphone stand",new Vector3(650,.2f,-140),new Vector3(650,1.9f,-140),.045f,ink,true);Pipe(root,"Microphone boom",new Vector3(650,1.9f,-140),new Vector3(650.7f,2,-140),.035f,bronze);Box(root,"Microphone",new Vector3(650.8f,2,-140),new Vector3(.2f,.08f,.08f),ink,false);
            Box(root,"Stage drum body",new Vector3(650,.85f,-149),new Vector3(1.1f,.6f,1.1f),rose,false,PrimitiveType.Cylinder).transform.localRotation=Quaternion.Euler(90,0,0);foreach(float x in new[]{648.5f,651.5f}){Pipe(root,"Cymbal stand",new Vector3(x,.2f,-148),new Vector3(x,1.6f,-148),.035f,bronze);Box(root,"Brass cymbal",new Vector3(x,1.65f,-148),new Vector3(.85f,.02f,.85f),gold,false,PrimitiveType.Cylinder);}
            Box(root,"Event control table",new Vector3(685,.9f,-135),new Vector3(3,.15f,1.2f),wood,true);Box(root,"Sound mixer",new Vector3(685,1.03f,-135),new Vector3(2.6f,.12f,1),ink,false);for(int i=0;i<8;i++)Box(root,"Mixer fader",new Vector3(684+i*.28f,1.13f,-135),new Vector3(.07f,.05f,.3f),cream,false);
            foreach(float a in new[]{.35f,.7f,2.12f,2.44f}){var p=new Vector3(650+Mathf.Cos(a)*24,0,-145+Mathf.Sin(a)*24);Person(root,p,90-a*Mathf.Rad2Deg,true,a<1?shirt:linen);}
            foreach(float a in new[]{.35f,.7f,1.02f,2.12f,2.44f,2.79f})Bench(root,new Vector3(650+Mathf.Cos(a)*35,0,-145+Mathf.Sin(a)*35),90-a*Mathf.Rad2Deg);
            for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;FountainJet(root,new Vector3(760+Mathf.Cos(a)*10,.42f,-136+Mathf.Sin(a)*10));}
            foreach(float x in new[]{871f,889f}){CoffeeMachine(root,new Vector3(x-2,1.87f,-158));Box(root,"Petal service menu",new Vector3(x,2.9f,-159.3f),new Vector3(4.5f,1.1f,.12f),ink,false);Text(root,"PETAL REFRESHMENTS\nCOFFEE / JUICE / SNACKS",new Vector3(x,2.9f,-159.4f),.16f,4.2f,cream.color);}
            foreach(var p in new[]{new Vector3(503,0,-125),new Vector3(537,0,-125),new Vector3(497,0,-139),new Vector3(543,0,-139)}){Bed(root,p,3.4f,2.2f,2,26);Fern(root,p,.95f);}
            foreach(var p in new[]{new Vector3(508,0,-125),new Vector3(534,0,-125)})Board(root,p,"TROPICAL COLLECTION\nFERNS / PALMS / HERBS",180,.65f);
            foreach(var p in new[]{new Vector3(553,0,-106),new Vector3(692,0,-104),new Vector3(807,0,-105),new Vector3(930,0,-106)}){Bin(root,p);Lantern(root,p+Vector3.right*4);}
        }
        private static void NorthDetails(Transform root)
        {
            Ribbon(root,"North garden linking trail",Smooth(new[]{new Vector3(464,0,233),new Vector3(560,0,232),new Vector3(586,0,235),new Vector3(650,0,236),new Vector3(705,0,233),new Vector3(760,0,231),new Vector3(823,0,232),new Vector3(880,0,232),new Vector3(949,0,234)},8),1.65f,.105f,stone,true);
            foreach(var p in new[]{new Vector3(570,0,254),new Vector3(700,0,259),new Vector3(820,0,258),new Vector3(940,0,248)}){Pergola(root,p,10,8);Bench(root,p+new Vector3(-3,0,0),90);Bench(root,p+new Vector3(3,0,0),-90);Table(root,p);Bed(root,p+new Vector3(0,0,6),6,2.5f,1,24);}
            foreach(var p in new[]{new Vector3(478,0,196),new Vector3(548,0,196),new Vector3(686,0,194),new Vector3(762,0,193),new Vector3(838,0,194),new Vector3(920,0,194)}){Bed(root,p,13,5,((int)p.x)%3,48);Tree(root,p+new Vector3(0,0,-7),6.5f,1);}
            foreach(var p in new[]{new Vector3(478,0,239),new Vector3(580,0,238),new Vector3(698,0,238),new Vector3(819,0,238),new Vector3(940,0,241)}){Tree(root,p,6,2);Lantern(root,p+Vector3.right*3);}
            BottleStation(root,new Vector3(559,0,236));BottleStation(root,new Vector3(683,0,242));BikeRack(root,new Vector3(676,0,236));
            foreach(float x in new[]{505f,535f}){Bench(root,new Vector3(x,0,235),180);Bin(root,new Vector3(x+2,0,237));}
        }
        private static void Woodland(Transform root)
        {
            int n=0;for(float x=458;x<959;x+=18){float z=-199+Mathf.Sin(x*.12f)*2;Tree(root,new Vector3(x,0,z),6+(n%4)*.7f,n++%3);if(Mathf.Abs(x-610)>15)Tree(root,new Vector3(x+3,0,292+Mathf.Sin(x*.07f)*1.2f),6.2f+(n%3),n%3);}
            foreach(var p in new[]{new Vector3(475,0,-118),new Vector3(578,0,-171),new Vector3(707,0,-159),new Vector3(828,0,-166),new Vector3(952,0,-150),new Vector3(476,0,273),new Vector3(697,0,282),new Vector3(817,0,279)}){Bed(root,p,8,4,2,32);RockCluster(root,p+Vector3.right*6);}
        }
        private static void Bed(Transform root,Vector3 p,float rx,float rz,int color,int count)
        {
            count=Mathf.Max(count,Mathf.CeilToInt(rx*rz*2.6f));var g=Group(root,"Curved planted garden bed",p);Disc(g,"Garden soil",Vector3.up*.16f,rx,rz,soil,true);Disc(g,"Lush bed ground cover",Vector3.up*.165f,rx*.94f,rz*.94f,groundCover,false);var rim=new List<Vector3>();var tr=new List<int>();for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;foreach(float side in new[]{0f,.28f})rim.Add(new Vector3(Mathf.Cos(a)*(rx+side),.22f,Mathf.Sin(a)*(rz+side)));if(i>0){int n=i*2;tr.AddRange(new[]{n-2,n,n-1,n-1,n,n+1});}}MeshPart(g,"Low garden edging",rim.ToArray(),tr.ToArray(),stone,false);
            var leaves=new List<Vector3>();var li=new List<int>();var petals=new List<Vector3>();var pi=new List<int>();for(int i=0;i<count;i++){float a=i*2.399963f,r=Mathf.Sqrt((i+.5f)/count)*.92f;var basePoint=new Vector3(Mathf.Cos(a)*rx*r,.17f,Mathf.Sin(a)*rz*r);float height=.3f+(i%4)*.09f;for(int j=0;j<4;j++){float b=j*Mathf.PI*.5f+a;var side=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));int n=leaves.Count;leaves.Add(basePoint-side*.1f);leaves.Add(basePoint+side*.1f);leaves.Add(basePoint+side*.24f+Vector3.up*height);leaves.Add(basePoint+Vector3.up*(height+.12f));li.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}
                var tip=basePoint+Vector3.up*(height+.16f);for(int j=0;j<6;j++){float b=j*Mathf.PI/3;var axis=new Vector3(Mathf.Cos(b),.18f,Mathf.Sin(b));var tangent=new Vector3(-Mathf.Sin(b),0,Mathf.Cos(b));int n=petals.Count;petals.Add(tip);petals.Add(tip+axis*.19f+tangent*.08f);petals.Add(tip+axis*.26f);petals.Add(tip+axis*.19f-tangent*.08f);pi.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}}
            MeshPart(g,"Mixed planting leaves",leaves.ToArray(),li.ToArray(),leaf,false);MeshPart(g,"Flower meadow petals",petals.ToArray(),pi.ToArray(),color==0?rose:color==1?gold:violet,false);
        }
        private static void Tree(Transform root,Vector3 p,float h,int type)
        {
            var g=Group(root,type==0?"Broadleaf shade tree":type==1?"Flowering park tree":"Slender evergreen",p);Pipe(g,"Solid tree trunk",Vector3.up*.05f,new Vector3(.15f,h*.67f,0),.24f,wood,true);
            if(type==2){for(int i=0;i<4;i++)Crown(g,new Vector3(.15f,h*(.52f+i*.13f),0),new Vector3(h*(.68f-i*.12f),h*.42f,h*(.68f-i*.12f)));}
            else for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;var tip=new Vector3(Mathf.Cos(a)*h*.23f,h*(.7f+(i%2)*.08f),Mathf.Sin(a)*h*.23f);Pipe(g,"Visible tree branch",Vector3.up*h*.45f,tip,.11f,wood);Crown(g,tip,new Vector3(h*.55f,h*.42f,h*.55f));if(type==1)for(int j=0;j<3;j++)Box(g,"Tree blossom cluster",tip+new Vector3((j-1)*h*.13f,h*.12f,.1f),new Vector3(.65f,.25f,.6f),j%2==0?rose:cream,false,PrimitiveType.Sphere);}
            Disc(g,"Mulched tree ring",Vector3.up*.055f,1.35f,1.35f,soil,false);
        }
        private static void Crown(Transform root,Vector3 p,Vector3 size)
        {
            var v=new List<Vector3>();var t=new List<int>();const int rows=6,cols=12;for(int y=0;y<=rows;y++){float phi=y*Mathf.PI/rows;for(int x=0;x<=cols;x++){float a=x*Mathf.PI*2/cols,r=1+.065f*Mathf.Sin(a*5+phi*3);v.Add(p+Vector3.Scale(new Vector3(Mathf.Cos(a)*Mathf.Sin(phi)*r,Mathf.Cos(phi),Mathf.Sin(a)*Mathf.Sin(phi)*r),size*.5f));}}
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++){int a=y*(cols+1)+x,b=a+1,c=a+cols+1,d=c+1;t.AddRange(new[]{a,b,c,b,d,c});}MeshPart(root,"Organic leafy crown",v.ToArray(),t.ToArray(),leaf,false);
        }
        private static void Fern(Transform root,Vector3 p,float scale){for(int i=0;i<9;i++){float a=i*Mathf.PI*2/9;var v=new[]{p+Vector3.up*.2f,p+new Vector3(Mathf.Cos(a-.1f)*scale*.55f,scale*.85f,Mathf.Sin(a-.1f)*scale*.55f),p+new Vector3(Mathf.Cos(a)*scale,scale*.7f,Mathf.Sin(a)*scale),p+new Vector3(Mathf.Cos(a+.1f)*scale*.55f,scale*.85f,Mathf.Sin(a+.1f)*scale*.55f)};MeshPart(root,"Broad fern frond",v,new[]{0,1,2,0,2,3},leaf,false);}}
        private static void Pergola(Transform root,Vector3 p,float w,float d)
        {
            var g=Group(root,"Vine-covered garden pergola",p);foreach(int s in new[]{-1,1})foreach(int e in new[]{-1,1})Box(g,"Pergola timber column",new Vector3(s*w/2,1.9f,e*d/2),new Vector3(.18f,3.6f,.18f),wood,true);
            foreach(int s in new[]{-1,1})Box(g,"Pergola side beam",new Vector3(s*w/2,3.7f,0),new Vector3(.2f,.22f,d+.6f),bronze,false);
            for(float x=-w/2;x<=w/2;x+=.55f)Box(g,"Pergola roof louver",new Vector3(x,3.85f,0),new Vector3(.12f,.18f,d+.8f),wood,false);
            for(int i=0;i<8;i++){float x=-w*.43f+(i%4)*w*.28f,z=i<4?-d*.43f:d*.43f;Box(g,"Roof climbing vine",new Vector3(x,4,z),new Vector3(w*.3f,.35f,1.7f),leaf,false,PrimitiveType.Sphere);}
            foreach(int s in new[]{-1,1})for(int i=0;i<4;i++)Box(g,"Climbing vine foliage",new Vector3(s*w*.5f,.8f+i*.8f,-d*.5f),new Vector3(.5f,1,.5f),leaf,false,PrimitiveType.Sphere);
        }
        private static void Person(Transform root,Vector3 p,float yaw,bool seated,Material clothes)
        {
            var g=Group(root,seated?"Seated park visitor":"Kiosk attendant",p,yaw);float hip=seated?.66f:.82f,shoulder=seated?1.15f:1.37f,head=seated?1.45f:1.68f;
            Box(g,"Visitor hips",new Vector3(0,hip,0),new Vector3(.39f,.22f,.28f),ink,false);Box(g,"Visitor clothed torso",new Vector3(0,shoulder-.2f,0),new Vector3(.47f,.63f,.3f),clothes,true,PrimitiveType.Capsule);Box(g,"Visitor shirt collar",new Vector3(0,shoulder+.1f,-.09f),new Vector3(.22f,.08f,.15f),cream,false);
            Box(g,"Visitor head",new Vector3(0,head,0),new Vector3(.29f,.34f,.3f),skin,false,PrimitiveType.Sphere);Box(g,"Visitor hair",new Vector3(0,head+.13f,.02f),new Vector3(.3f,.14f,.3f),ink,false,PrimitiveType.Sphere);foreach(int s in new[]{-1,1})Box(g,"Visitor eye",new Vector3(s*.055f,head+.025f,-.145f),new Vector3(.025f,.025f,.012f),ink,false);
            foreach(int side in new[]{-1,1}){var knee=new Vector3(side*.12f,seated?.5f:.45f,seated?-.4f:0);var ankle=new Vector3(side*.13f,.16f,seated?-.48f:0);Pipe(g,"Visitor trouser thigh",new Vector3(side*.12f,hip,0),knee,.16f,ink);Pipe(g,"Visitor trouser calf",knee,ankle,.14f,ink);Box(g,"Visitor shoe",ankle+new Vector3(0,-.045f,-.07f),new Vector3(.17f,.12f,.28f),ink,false);
                var arm=Group(g,"Visitor gesture arm",new Vector3(side*.27f,shoulder,0));Pipe(arm,"Sleeved upper arm",Vector3.zero,new Vector3(side*.05f,-.25f,-.15f),.13f,clothes);Pipe(arm,"Visitor forearm",new Vector3(side*.05f,-.25f,-.15f),new Vector3(side*.02f,-.35f,-.4f),.1f,skin);Box(arm,"Visitor hand",new Vector3(side*.02f,-.35f,-.43f),Vector3.one*.1f,skin,false,PrimitiveType.Sphere);if(!seated&&side==1){arms.Add(arm);Cup(arm,new Vector3(side*.02f,-.34f,-.48f));}}
            if(seated){Box(g,"Visitor reading book",new Vector3(0,.8f,-.48f),new Vector3(.32f,.035f,.25f),cream,false);}
        }
        private static void CoffeeMachine(Transform root,Vector3 p){Box(root,"Espresso machine body",p+Vector3.up*.36f,new Vector3(1.8f,.65f,.8f),bronze,false);Box(root,"Espresso machine fascia",p+new Vector3(0,.35f,-.44f),new Vector3(1.6f,.4f,.08f),ink,false);foreach(int s in new[]{-1,1}){Pipe(root,"Coffee group head",p+new Vector3(s*.4f,.45f,-.51f),p+new Vector3(s*.4f,.28f,-.51f),.08f,bronze);Cup(root,p+new Vector3(s*.4f,.04f,-.5f));}for(int i=0;i<4;i++)Cup(root,p+new Vector3(-.6f+i*.4f,.7f,0));}
        private static void Cup(Transform root,Vector3 p){Box(root,"Ceramic coffee cup",p+Vector3.up*.08f,new Vector3(.13f,.08f,.13f),cream,false,PrimitiveType.Cylinder);Box(root,"Coffee surface",p+Vector3.up*.163f,new Vector3(.11f,.003f,.11f),wood,false,PrimitiveType.Cylinder);}
        private static void Table(Transform root,Vector3 p){Box(root,"Garden table pedestal",p+Vector3.up*.55f,new Vector3(.16f,1,.16f),bronze,true);Box(root,"Garden table top",p+Vector3.up*1.1f,new Vector3(1.8f,.10f,1.8f),stone,true,PrimitiveType.Cylinder);Cup(root,p+new Vector3(.3f,1.21f,0));Box(root,"Table flower pot",p+new Vector3(-.3f,1.25f,0),new Vector3(.25f,.14f,.25f),cream,false,PrimitiveType.Cylinder);}
        private static void Bench(Transform root,Vector3 p,float yaw){var g=Group(root,"Detail garden bench",p,yaw);Box(g,"Timber bench seat",new Vector3(0,.58f,0),new Vector3(3.1f,.15f,.8f),wood,true);Box(g,"Timber bench back",new Vector3(0,.95f,.33f),new Vector3(3.1f,.65f,.12f),wood,true);foreach(int s in new[]{-1,1})Box(g,"Bench metal leg",new Vector3(s*1.2f,.3f,0),new Vector3(.1f,.5f,.7f),bronze,false);}
        private static void Chair(Transform root,Vector3 p,float yaw){var g=Group(root,"Chess terrace chair",p,yaw);Box(g,"Chair cushion",new Vector3(0,.58f,0),new Vector3(.8f,.14f,.8f),linen,true);Box(g,"Chair curved back",new Vector3(0,.95f,.32f),new Vector3(.8f,.65f,.12f),wood,false);foreach(int s in new[]{-1,1})foreach(int e in new[]{-1,1})Box(g,"Chair leg",new Vector3(s*.28f,.3f,e*.28f),new Vector3(.055f,.5f,.055f),bronze,false);}
        private static void BottleStation(Transform root,Vector3 p){Box(root,"Drinking water pedestal",p+Vector3.up*.7f,new Vector3(.5f,1.2f,.5f),stone,true);Box(root,"Drinking fountain bowl",p+Vector3.up*1.35f,new Vector3(.65f,.12f,.55f),bronze,false);Pipe(root,"Water tap",p+new Vector3(.2f,1.4f,0),p+new Vector3(.2f,1.7f,0),.05f,bronze);Board(root,p+Vector3.right,"DRINKING WATER",180,.6f);}
        private static void Bin(Transform root,Vector3 p){Box(root,"Detail waste bin",p+Vector3.up*.65f,new Vector3(.62f,1.1f,.62f),ink,true);Box(root,"Bin lid",p+Vector3.up*1.25f,new Vector3(.68f,.08f,.68f),bronze,false);}
        private static void Lantern(Transform root,Vector3 p){Box(root,"Garden lamp mast",p+Vector3.up*1.8f,new Vector3(.1f,3.4f,.1f),ink,true);var b=Box(root,"Detail lamp diffuser",p+Vector3.up*3.6f,new Vector3(.55f,.12f,.55f),warm,false);lamps.Add(b.GetComponent<Renderer>());}
        private static void RockCluster(Transform root,Vector3 p){for(int i=0;i<4;i++){var rock=Box(root,"Smooth garden boulder",p+new Vector3((i%2)*.8f,.25f,(i/2)*.7f),new Vector3(.9f,.55f,.8f),stone,true,PrimitiveType.Sphere);rock.transform.localRotation=Quaternion.Euler(i*8,i*37,i*5);}}
        private static void BikeRack(Transform root,Vector3 p){for(int i=0;i<3;i++){float x=p.x+i*.7f;Pipe(root,"Cycle stand upright",new Vector3(x,.1f,p.z),new Vector3(x,.9f,p.z),.055f,bronze);Pipe(root,"Cycle stand crossbar",new Vector3(x,.9f,p.z),new Vector3(x,.9f,p.z+.6f),.055f,bronze);Pipe(root,"Cycle stand return",new Vector3(x,.9f,p.z+.6f),new Vector3(x,.1f,p.z+.6f),.055f,bronze);}}
        private static void Bike(Transform root,Vector3 p,float yaw){var g=Group(root,"Parked city bicycle",p,yaw);foreach(int s in new[]{-1,1}){var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<=40;i++){float a=i*Mathf.PI/20;foreach(float r in new[]{.36f,.4f})v.Add(new Vector3(s*.65f+Mathf.Cos(a)*r,.5f+Mathf.Sin(a)*r,0));if(i>0){int n=i*2;t.AddRange(new[]{n-2,n,n-1,n-1,n,n+1});}}MeshPart(g,"Bicycle tyre",v.ToArray(),t.ToArray(),ink,false);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Pipe(g,"Cycle wheel spoke",new Vector3(s*.65f,.5f,0),new Vector3(s*.65f+Mathf.Cos(a)*.36f,.5f+Mathf.Sin(a)*.36f,0),.015f,bronze);}}
            foreach(var line in new[]{new[]{new Vector3(-.65f,.5f,0),new Vector3(-.25f,1.05f,0)},new[]{new Vector3(-.25f,1.05f,0),new Vector3(.15f,.5f,0)},new[]{new Vector3(.15f,.5f,0),new Vector3(-.65f,.5f,0)},new[]{new Vector3(-.25f,1.05f,0),new Vector3(.5f,1.02f,0)},new[]{new Vector3(.5f,1.02f,0),new Vector3(.15f,.5f,0)},new[]{new Vector3(.5f,1.02f,0),new Vector3(.65f,.5f,0)}})Pipe(g,"Bicycle frame",line[0],line[1],.045f,rose);Box(g,"Cycle saddle",new Vector3(-.25f,1.14f,0),new Vector3(.27f,.07f,.2f),ink,false);Pipe(g,"Cycle handlebar",new Vector3(.5f,1.16f,-.23f),new Vector3(.5f,1.16f,.23f),.035f,bronze);
        }
        private static void FountainJet(Transform root,Vector3 p)
        {
            var go=new GameObject("Commons fountain water spray");go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(-90,0,0);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.loop=true;main.playOnAwake=true;main.duration=2;main.startLifetime=new ParticleSystem.MinMaxCurve(.8f,1.2f);main.startSpeed=new ParticleSystem.MinMaxCurve(5,7);main.startSize=new ParticleSystem.MinMaxCurve(.045f,.08f);main.startColor=new Color(.65f,.88f,.91f,.72f);main.gravityModifier=.8f;main.maxParticles=28;main.simulationSpace=ParticleSystemSimulationSpace.World;main.cullingMode=ParticleSystemCullingMode.Automatic;var em=ps.emission;em.rateOverTime=18;var sh=ps.shape;sh.shapeType=ParticleSystemShapeType.Cone;sh.angle=8;sh.radius=.08f;
            string path=Folder+"/Fountain droplets.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");if(shader==null)shader=Shader.Find("Universal Render Pipeline/Unlit");mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.SetColor("_BaseColor",new Color(.65f,.88f,.91f,.75f));mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        private static void Board(Transform root,Vector3 p,string title,float yaw,float height=1.5f)
        {
            var g=Group(root,"Mounted detail information board",p,yaw);Box(g,"Information board post",new Vector3(0,height*.55f,0),new Vector3(.08f,height,.08f),bronze,true);Box(g,"Information board panel",new Vector3(0,height,0),new Vector3(height<1?1.5f:3.6f,height<1?.55f:1.05f,.1f),ink,false);float w=height<1?1.3f:3.3f,s=height<1?.08f:.17f;Text(g,title,new Vector3(0,height,-.065f),s,w,cream.color);Text(g,title,new Vector3(0,height,.065f),s,w,cream.color,180);
        }
        private static void Text(Transform root,string title,Vector3 p,float size,float width,Color color,float yaw=0){var t=new GameObject(title).AddComponent<TextMeshPro>();t.transform.SetParent(root,false);t.transform.localPosition=p;t.transform.localRotation=Quaternion.Euler(0,yaw,0);t.font=TMP_Settings.GetFontAsset();t.text=title;t.fontSize=size*10;t.color=color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(width,size*4);t.raycastTarget=false;}
        private static Vector3[] EllipsePoints(Vector3 p,float rx,float rz,int count){var points=new Vector3[count+1];for(int i=0;i<=count;i++){float a=i*Mathf.PI*2/count;points[i]=p+new Vector3(Mathf.Cos(a)*rx,.12f,Mathf.Sin(a)*rz);}return points;}
        private static Vector3[] Smooth(Vector3[] points,int samples)
        {
            bool closed=(points[0]-points[points.Length-1]).sqrMagnitude<.01f;int count=closed?points.Length-1:points.Length;var result=new List<Vector3>();for(int i=0;i<points.Length-1;i++){Vector3 a=points[closed?(i-1+count)%count:Mathf.Max(0,i-1)],b=points[i],c=points[i+1],d=points[closed?(i+2)%count:Mathf.Min(count-1,i+2)];for(int j=0;j<samples;j++){float t=j/(float)samples;result.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));}}result.Add(points[points.Length-1]);return result.ToArray();
        }
        private static void Ribbon(Transform root,string name,Vector3[] source,float half,float y,Material mat,bool solid)
        {
            var points=new List<Vector3>();for(int i=0;i<source.Length-1;i++){int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(source[i],source[i+1])/5));for(int j=0;j<steps;j++)points.Add(Vector3.Lerp(source[i],source[i+1],j/(float)steps));}points.Add(source[source.Length-1]);
            for(int start=0;start<points.Count-1;start+=8){int end=Mathf.Min(start+8,points.Count-1);var v=new List<Vector3>();var tri=new List<int>();for(int i=start;i<=end;i++){var tangent=points[Mathf.Min(points.Count-1,i+1)]-points[Mathf.Max(0,i-1)];tangent.y=0;var side=Vector3.Cross(Vector3.up,tangent.normalized)*half;var p=points[i];p.y=y;v.Add(p-side);v.Add(p+side);if(i>start){int n=(i-start)*2;tri.AddRange(new[]{n-2,n,n-1,n-1,n,n+1});}}MeshPart(root,name,v.ToArray(),tri.ToArray(),mat,solid);}
        }
        private static void Disc(Transform root,string name,Vector3 p,float rx,float rz,Material mat,bool solid){var v=new Vector3[66];var t=new List<int>();v[0]=p;for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;v[i+1]=p+new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz);if(i>0)t.AddRange(new[]{0,i+1,i});}MeshPart(root,name,v,t.ToArray(),mat,solid);}
        private static void Floor(Transform root,string name,Rect r,float y,Material mat)
        {
            int nx=Mathf.Max(1,Mathf.CeilToInt(r.width/64)),nz=Mathf.Max(1,Mathf.CeilToInt(r.height/64));var v=new List<Vector3>();var t=new List<int>();for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)v.Add(new Vector3(Mathf.Lerp(r.xMin,r.xMax,x/(float)nx),y,Mathf.Lerp(r.yMin,r.yMax,z/(float)nz)));for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;t.AddRange(new[]{a,c,b,b,c,d});}var go=MeshPart(root,name,v.ToArray(),t.ToArray(),mat,false);var box=go.AddComponent<BoxCollider>();box.center=new Vector3(r.center.x,y-.08f,r.center.y);box.size=new Vector3(r.width,.16f,r.height);
        }
        private static GameObject MeshPart(Transform root,string name,Vector3[] vertices,int[] triangles,Material mat,bool solid)
        {
            var mesh=new Mesh{name=name,indexFormat=vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.triangles=triangles;mesh.uv=vertices.Select(p=>new Vector2(p.x*.3f,p.z*.3f)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();if(solid)mesh=Save(mesh,Folder+"/Collision"+(index++)+".asset");var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;if(solid)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
        }
        private static Transform Group(Transform root,string name,Vector3 p,float yaw=0){var t=new GameObject(name).transform;t.SetParent(root,false);t.localPosition=p;t.localRotation=Quaternion.Euler(0,yaw,0);return t;}
        private static GameObject Box(Transform root,string name,Vector3 p,Vector3 size,Material m,bool solid,PrimitiveType shape=PrimitiveType.Cube)
        {
            var g=GameObject.CreatePrimitive(shape);g.name=name;g.transform.SetParent(root,false);g.transform.localPosition=p;if(shape==PrimitiveType.Capsule)size.y*=.5f;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());if(solid){if(shape==PrimitiveType.Sphere)g.AddComponent<SphereCollider>();else if(shape==PrimitiveType.Capsule)g.AddComponent<CapsuleCollider>();else g.AddComponent<BoxCollider>();}return g;
        }
        private static void Pipe(Transform root,string name,Vector3 a,Vector3 b,float width,Material m,bool solid=false){var g=Box(root,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),m,solid);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        private static Material Load(string folder,string name){var m=AssetDatabase.LoadAssetAtPath<Material>(folder+"/"+name+".mat");if(m==null)throw new InvalidOperationException("Required material missing: "+name);return m;}
        private static Material Mat(string name,Color c){string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=c;m.SetFloat("_Smoothness",.2f);m.SetFloat("_Cull",0);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;}
        private static Mesh Save(Mesh mesh,string path){var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}saved.Clear();EditorUtility.CopySerialized(mesh,saved);saved.UploadMeshData(false);if(mesh!=saved)UnityEngine.Object.DestroyImmediate(mesh);return saved;}
        private static void Merge(Transform root,MeshFilter[] parts,Material m,string path)
        {
            var mesh=new Mesh{name="Commons detail "+m.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();var saved=Save(mesh,path);var obj=new GameObject(saved.name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(root,false);obj.GetComponent<MeshFilter>().sharedMesh=saved;obj.GetComponent<MeshRenderer>().sharedMaterial=m;foreach(var f in parts){var source=f.sharedMesh;UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);if(source!=null&&!AssetDatabase.Contains(source)&&!source.name.StartsWith("Cube")&&!source.name.StartsWith("Sphere")&&!source.name.StartsWith("Cylinder")&&!source.name.StartsWith("Capsule"))UnityEngine.Object.DestroyImmediate(source);}
        }
        private static bool Moving(MeshFilter f)=>arms.Any(a=>f.transform.IsChildOf(a));
        private static void Combine(Transform root)
        {
            int n=0;foreach(var arm in arms)foreach(var g in arm.GetComponentsInChildren<MeshFilter>().GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial).ToArray())Merge(arm,g.ToArray(),g.Key,Folder+"/Gesture"+(n++)+".asset");
            var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null&&f.GetComponentInParent<Rigidbody>()==null&&!Moving(f)&&!lamps.Contains(f.GetComponent<Renderer>())).ToArray();
            foreach(var g in filters.GroupBy(f=>{var r=f.GetComponent<MeshRenderer>();var p=r.bounds.center;var mat=r.sharedMaterial;bool split=mat==leaf||(mat.HasProperty("_Surface")&&mat.GetFloat("_Surface")>.5f);return new{Mat=mat,X=split?Mathf.FloorToInt(p.x/160):0,Zone=p.z>0?2:p.z> -90?0:1};}))Merge(root,g.ToArray(),g.Key.Mat,Folder+"/Cell"+(n++)+".asset");
        }
        private static void RegisterLights(MotelGameRoot m){var lighting=m.GetComponentInChildren<WorldNightLighting>();if(lighting==null)return;var so=new SerializedObject(lighting);var a=so.FindProperty("fixtures");var b=so.FindProperty("glowingSurfaces");int ai=a.arraySize,bi=b.arraySize;a.arraySize+=lamps.Count;b.arraySize+=lamps.Count;for(int i=0;i<lamps.Count;i++){a.GetArrayElementAtIndex(ai+i).objectReferenceValue=lamps[i].transform;b.GetArrayElementAtIndex(bi+i).objectReferenceValue=lamps[i];}so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
