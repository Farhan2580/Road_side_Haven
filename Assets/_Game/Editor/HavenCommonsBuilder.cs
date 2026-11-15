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
    /// <summary>Replaces the eight primitive industrial sheds with a low-rise public landscape and sculptural pavilions.</summary>
    public static class HavenCommonsBuilder
    {
        public const string Layer="Haven Commons Creative Park v1";
        public const string Folder="Assets/_Game/Generated/HavenCommons";
        private static Material stone,cream,bronze,wood,ink,glass,leaf,grass,water,rubber,coral,warm,asphalt,skin;
        private static int meshIndex;
        private static readonly List<Renderer> bulbs=new List<Renderer>(),waters=new List<Renderer>();
        private static readonly List<Transform> art=new List<Transform>();
        private static readonly Rect[] RoadCuts={Rect.MinMaxRect(606,-230,614,-13),Rect.MinMaxRect(606,160,614,214)};
        [MenuItem("Roadside Haven/Design/Replace Factories with Haven Commons")]
        public static void BuildCurrent(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;var m=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(m==null)return;var scene=m.gameObject.scene;if(File.Exists(scene.path)){Directory.CreateDirectory("Backups/HavenCommons");File.Copy(scene.path,"Backups/HavenCommons/Before-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity");}Build(m);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();}
        [MenuItem("Roadside Haven/Design/Focus Haven Commons")]
        public static void Focus(){var m=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(m==null)return;Selection.activeGameObject=m.transform.Find(Layer)?.gameObject;SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(705,4,-134),new Vector3(500,26,120)),false);}
        [MenuItem("Roadside Haven/Design/Visit Haven Commons in Play Mode")]
        public static void Visit(){if(!EditorApplication.isPlaying)return;var o=UnityEngine.Object.FindFirstObjectByType<OwnerSandboxController>();if(o!=null&&o.IsWalking&&!o.HasSupplyCargo)o.ExitVehicle(new Vector3(520,.2f,-104));}
        public static Transform Build(MotelGameRoot m)
        {
            var old=m.transform.Find(Layer);if(old!=null)return old;if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","HavenCommons");meshIndex=0;bulbs.Clear();waters.Clear();art.Clear();Materials();RetireFactories(m.transform);
            var root=new GameObject(Layer).transform;root.SetParent(m.transform,false);root.gameObject.AddComponent<MotelDistrictArt>();var life=root.gameObject.AddComponent<CommonsParkLife>();RepairPaving(m.transform,root);
            Road(root,RoadCuts[0]);Road(root,RoadCuts[1]);
            Floor(root,"Commons south ground",Rect.MinMaxRect(450,-204,960,-90),.025f,grass);Floor(root,"Commons north ground",Rect.MinMaxRect(450,222,960,296),.025f,grass);
            Path(root,Rect.MinMaxRect(434,-102,960,-92));Path(root,Rect.MinMaxRect(434,210,960,219));
            foreach(float x in new[]{520f,640f,760f,880f}){Path(root,Rect.MinMaxRect(x-2,-124,x+2,-98));Path(root,Rect.MinMaxRect(x-2,214,x+2,245));}
            Path(root,Rect.MinMaxRect(431.04f,-190,600,-186));Path(root,Rect.MinMaxRect(604,-218.96f,608,-98));Path(root,Rect.MinMaxRect(616,-218.96f,620,-98));
            BotanicalDome(root);Amphitheatre(root);SculptureFountain(root);PetalCourt(root);NorthGardens(root);Details(root);
            var people=new TownCitizenAgent[6];for(int i=0;i<6;i++){var go=new GameObject("Commons visitor "+i);go.transform.SetParent(root,false);var cc=go.AddComponent<CharacterController>();cc.enabled=false;people[i]=go.AddComponent<TownCitizenAgent>();}
            Combine(root);life.Configure(people,art.ToArray(),waters.ToArray(),new[]{coral,ink,rubber},skin);RegisterLights(m);
            var physical=RoadSurfacePhysics.Rebuild(m.transform);var saved=Save(physical.SurfaceMesh,Folder+"/CommonsConnectedRoadSurface.asset");physical.UseSavedMesh(saved);foreach(var t in root.GetComponentsInChildren<TMP_Text>())t.ForceMeshUpdate(true,true);AssetDatabase.SaveAssets();return root;
        }
        private static void Materials()
        {
            stone=Mat("Commons travertine",new Color(.7f,.68f,.6f),"Paving");cream=Mat("Sculptural ivory",new Color(.87f,.86f,.79f));cream.SetFloat("_Cull",0);bronze=Mat("Sculpture bronze",new Color(.55f,.34f,.12f));bronze.SetFloat("_Metallic",.65f);bronze.SetFloat("_Smoothness",.7f);wood=Mat("Pavilion cedar",new Color(.34f,.20f,.1f));ink=Mat("Commons graphite",new Color(.04f,.07f,.08f));leaf=AssetDatabase.LoadAssetAtPath<Material>(MotelDistrictArtBuilder.Folder+"/Palm foliage.mat");grass=Mat("Commons meadow",new Color(.6f,.78f,.49f),"BaseGrass");rubber=Mat("Playground sage",new Color(.15f,.35f,.3f));coral=Mat("Terracotta accent",new Color(.67f,.24f,.13f));warm=Mat("Commons lantern glass",new Color(1,.8f,.48f));skin=Mat("Commons visitor skin",new Color(.63f,.43f,.28f));asphalt=Mat("Commons fine asphalt",new Color(.32f,.34f,.36f),"Asphalt");
            glass=Mat("Botanical dome glass",new Color(.24f,.52f,.48f,.24f));glass.SetFloat("_Surface",1);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);glass.SetFloat("_Cull",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;glass.SetShaderPassEnabled("ShadowCaster",false);glass.SetFloat("_Smoothness",.9f);
            water=Mat("Commons turquoise water",new Color(.1f,.45f,.48f));water.SetFloat("_Smoothness",.92f);water.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Water/LakeWaveNormal.png"));water.SetFloat("_BumpScale",.25f);water.EnableKeyword("_NORMALMAP");
        }
        private static void RetireFactories(Transform root)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.parent!=null&&t.parent.name=="Industrial Zone - East"&&(t.name=="Warehouse"||t.name=="Warehouse roof"||t.name=="Factory chimney"||t.name=="Loading yard"))t.gameObject.SetActive(false);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))if(text.text!=null&&text.text.Contains("INDUSTRIAL / EAST"))text.gameObject.SetActive(false);
        }
        private static void BotanicalDome(Transform root)
        {
            var g=Group(root,"Verdant botanical conservatory",new Vector3(520,0,-137));Disc(g,"Conservatory stone floor",Vector3.up*.1f,29,22,stone,true);
            var v=new List<Vector3>();var t=new List<int>();int segments=72,rings=16;
            for(int j=0;j<=rings;j++){float p=j*Mathf.PI*.5f/rings;for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;v.Add(new Vector3(Mathf.Cos(a)*28*Mathf.Cos(p),.2f+Mathf.Sin(p)*11,Mathf.Sin(a)*21*Mathf.Cos(p)));}}
            for(int j=0;j<rings;j++)for(int i=0;i<segments;i++){float a=(i+.5f)*Mathf.PI*2/segments;if(j<5&&Mathf.Abs(a-Mathf.PI*.5f)<.23f)continue;int n=j*(segments+1)+i;t.AddRange(new[]{n,n+segments+1,n+1,n+1,n+segments+1,n+segments+2});}
            MeshPart(g,"Physical curved dome glazing",v.ToArray(),t.ToArray(),glass,true);
            for(int i=0;i<24;i++){float a=i*Mathf.PI/12;for(int j=0;j<16;j++){if(j<5&&Mathf.Abs(a-Mathf.PI*.5f)<.23f)continue;float p=j*Mathf.PI/32,q=(j+1)*Mathf.PI/32;Pipe(g,"Dome bronze rib",new Vector3(Mathf.Cos(a)*28*Mathf.Cos(p),.2f+11*Mathf.Sin(p),Mathf.Sin(a)*21*Mathf.Cos(p)),new Vector3(Mathf.Cos(a)*28*Mathf.Cos(q),.2f+11*Mathf.Sin(q),Mathf.Sin(a)*21*Mathf.Cos(q)),.10f,bronze);}}
            foreach(float h in new[]{.35f,.7f,1.03f})for(int i=0;i<72;i++){float a=i*Mathf.PI/36,b=(i+1)*Mathf.PI/36;Pipe(g,"Dome latitude frame",DomePoint(a,h),DomePoint(b,h),.075f,cream);}
            foreach(float x in new[]{-17f,17f})foreach(float z in new[]{-7f,7f}){Planter(g,new Vector3(x,0,z),7,5);Tree(g,new Vector3(x,.45f,z),4.5f);}
            Disc(g,"Indoor reflecting pond base",new Vector3(0,.2f,-5),6,5,ink,true);var pond=Disc(g,"Indoor pond water",new Vector3(0,.32f,-5),5.7f,4.7f,water,false);waters.Add(pond.GetComponent<Renderer>());Ring(g,"Indoor pond edging",new Vector3(0,.42f,-5),5.7f,6.1f,.15f,stone,true);foreach(float x in new[]{-9f,9f})Bench(g,new Vector3(x,0,-7),x<0?90:-90);
            SignBoard(root,new Vector3(530,0,-108),"VERDANT\nBOTANICAL GARDEN",0);Path(root,Rect.MinMaxRect(515,-117,525,-98));
        }
        private static Vector3 DomePoint(float a,float p)=>new Vector3(Mathf.Cos(a)*28*Mathf.Cos(p),.2f+11*Mathf.Sin(p),Mathf.Sin(a)*21*Mathf.Cos(p));
        private static void Amphitheatre(Transform root)
        {
            var g=Group(root,"Crescent open-air amphitheatre",new Vector3(650,0,-145));Disc(g,"Performance stage",Vector3.up*.2f,17,10,wood,true);var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<=48;i++){float a=i*Mathf.PI/48;foreach(float z in new[]{-7f,9f})v.Add(new Vector3(Mathf.Cos(a)*22,1.5f+Mathf.Sin(a)*10,z));if(i>0){int n=i*2;t.AddRange(new[]{n-2,n,n-1,n-1,n,n+1});}}MeshPart(g,"Curving acoustic shell",v.ToArray(),t.ToArray(),cream,false);
            foreach(float z in new[]{-7f,9f})for(int i=0;i<48;i++){float a=i*Mathf.PI/48,b=(i+1)*Mathf.PI/48;Pipe(g,"Shell structural rib",new Vector3(Mathf.Cos(a)*22,1.5f+Mathf.Sin(a)*10,z),new Vector3(Mathf.Cos(b)*22,1.5f+Mathf.Sin(b)*10,z),.16f,bronze);}
            foreach(int side in new[]{-1,1})foreach(float z in new[]{-7f,9f})Box(g,"Acoustic shell footing",new Vector3(side*22,.8f,z),new Vector3(.35f,1.5f,.35f),bronze,true);foreach(float r in new[]{19f,24f,29f})foreach(float a in new[]{.35f,.7f,1.02f,2.12f,2.44f,2.79f})Bench(g,new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r),-a*Mathf.Rad2Deg+90);
            SignBoard(root,new Vector3(674,0,-107),"CRESCENT STAGE\nOPEN AIR / LIVE EVENTS",0);
        }
        private static void SculptureFountain(Transform root)
        {
            var g=Group(root,"Orbit sculpture fountain garden",new Vector3(760,0,-136));Disc(g,"Fountain promenade",Vector3.up*.1f,25,25,stone,true);Disc(g,"Fountain bowl",Vector3.up*.28f,16,16,ink,true);var w=Disc(g,"Fountain flowing surface",Vector3.up*.38f,15.5f,15.5f,water,false);waters.Add(w.GetComponent<Renderer>());Ring(g,"Rounded fountain rim",Vector3.up*.5f,15.6f,16.2f,.23f,stone,true);
            Box(g,"Sculpture stone plinth",new Vector3(0,1,0),new Vector3(5,1.5f,5),cream,true);Pipe(g,"Orbital sculpture spindle",new Vector3(0,1.7f,0),new Vector3(0,2.6f,0),.2f,bronze);var spin=Group(g,"Rotating orbital sculpture",new Vector3(0,7,0));art.Add(spin);for(int i=0;i<3;i++){var ring=Group(spin,"Orbital bronze ring",Vector3.zero,i*60);VerticalRing(ring,4.8f,.19f,bronze);}
            foreach(float a in new[]{0f,.8f,1.6f,2.4f,3.2f,4f,4.8f,5.6f}){var p=new Vector3(Mathf.Cos(a)*21,0,Mathf.Sin(a)*21);Bench(g,p,90-a*Mathf.Rad2Deg);}
            SignBoard(root,new Vector3(786,0,-107),"ORBIT GARDEN\nART / WATER / SHADE",0);
        }
        private static void PetalCourt(Transform root)
        {
            var g=Group(root,"Petal picnic and refreshment court",new Vector3(880,0,-136));Disc(g,"Petal court stone plaza",Vector3.up*.1f,37,31,stone,true);
            for(int petal=0;petal<6;petal++)
            {
                var roof=Group(g,"Woven petal pavilion",Vector3.zero,petal*60);var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<=18;i++){float q=i/18f;float z=5+q*23,w=Mathf.Sin(q*Mathf.PI)*8.8f;v.Add(new Vector3(-w,4.4f+q*1.6f,z));v.Add(new Vector3(0,5.3f+q*1.6f,z));v.Add(new Vector3(w,4.4f+q*1.6f,z));if(i>0){int n=i*3;t.AddRange(new[]{n-3,n,n-2,n-2,n,n+1,n-2,n+1,n-1,n-1,n+1,n+2});}}MeshPart(roof,"Sculpted shade petal",v.ToArray(),t.ToArray(),cream,false);Pipe(roof,"Petal outer column",new Vector3(0,.1f,28),new Vector3(0,6.9f,28),.14f,bronze);Pipe(roof,"Petal inner column",new Vector3(0,.1f,5),new Vector3(0,5.3f,5),.14f,bronze);
            }
            foreach(float x in new[]{-15f,15f})foreach(float z in new[]{-10f,0f,10f}){Table(g,new Vector3(x,0,z));Bench(g,new Vector3(x-2,0,z),90);Bench(g,new Vector3(x+2,0,z),-90);}
            foreach(float x in new[]{-9f,9f}){Box(g,"Refreshment counter",new Vector3(x,.9f,-22),new Vector3(10,1.6f,2.8f),wood,true);Box(g,"Stone serving counter",new Vector3(x,1.8f,-22),new Vector3(10.4f,.12f,3),stone,false);}
            SignBoard(root,new Vector3(909,0,-105),"PETAL COURT\nPICNIC / REFRESHMENTS",0);
        }
        private static void NorthGardens(Transform root)
        {
            var play=Group(root,"North adventure garden",new Vector3(520,0,260));Disc(play,"Soft adventure ground",Vector3.up*.1f,31,25,rubber,true);
            foreach(int side in new[]{-1,1})foreach(float z in new[]{-4f,4f})Box(play,"Climbing frame post",new Vector3(side*5,2,z),new Vector3(.18f,3.8f,.18f),bronze,true);
            foreach(float z in new[]{-4f,4f})for(int i=0;i<8;i++)Pipe(play,"Climbing crossbar",new Vector3(-5,.5f+i*.45f,z),new Vector3(5,.5f+i*.45f,z),.07f,ink);
            for(int i=0;i<10;i++)Pipe(play,"Monkey bar",new Vector3(-4.5f+i,3.9f,-4),new Vector3(-4.5f+i,3.9f,4),.07f,bronze);
            var ramp=Group(play,"Slide access ramp",new Vector3(14,1.15f,0));ramp.localRotation=Quaternion.Euler(-18,0,0);Box(ramp,"Physical play ramp",Vector3.zero,new Vector3(3,.16f,7),coral,true);foreach(int side in new[]{-1,1})Box(ramp,"Ramp handrail",new Vector3(side*1.4f,.8f,0),new Vector3(.08f,.1f,7),bronze,true);
            foreach(float x in new[]{-23f,23f}){Planter(play,new Vector3(x,0,13),5,3);Tree(play,new Vector3(x,.4f,13),5);}
            var track=Group(root,"Sculpted concrete pump loop",new Vector3(640,0,260));var vertices=new List<Vector3>();var tri=new List<int>();for(int i=0;i<=128;i++){float a=i*Mathf.PI/64,h=.14f+1.4f*Mathf.Pow(Mathf.Sin(a*2),2);foreach(float r in new[]{-2.7f,2.7f})vertices.Add(new Vector3(Mathf.Cos(a)*(22+r),h,Mathf.Sin(a)*(15+r)));if(i>0){int n=i*2;tri.AddRange(new[]{n-2,n,n-1,n-1,n,n+1});}}MeshPart(track,"Rideable wave concrete loop",vertices.ToArray(),tri.ToArray(),cream,true);Disc(track,"Pump loop meadow",Vector3.up*.06f,17,10,grass,false);
            var garden=Group(root,"Wildflower picnic meadow",new Vector3(760,0,260));Disc(garden,"Meadow walking plaza",Vector3.up*.08f,33,25,stone,true);Disc(garden,"Round meadow lawn",Vector3.up*.1f,22,17,grass,false);for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var p=new Vector3(Mathf.Cos(a)*26,0,Mathf.Sin(a)*20);Planter(garden,p,3.3f,2);if(i%3==0)Tree(garden,p+Vector3.up*.5f,5.3f);else Bench(garden,p,90-a*Mathf.Rad2Deg);}
            var wind=Group(root,"Wind sculpture grove",new Vector3(880,0,260));Disc(wind,"Wind garden plaza",Vector3.up*.1f,31,25,stone,true);Box(wind,"Wind sculpture base",Vector3.up*.6f,new Vector3(4,1,4),cream,true);Pipe(wind,"Wind sculpture mast",new Vector3(0,.1f,0),new Vector3(0,7,0),.28f,bronze,true);var spinner=Group(wind,"Kinetic petal crown",Vector3.up*7);art.Add(spinner);for(int i=0;i<8;i++){var blade=Group(spinner,"Wind petal",Vector3.zero,i*45);var petal=Box(blade,"Sculpture leaf",new Vector3(0,.3f,3.8f),new Vector3(2.2f,.12f,6),bronze,false);petal.transform.localRotation=Quaternion.Euler(14,0,8);}
            foreach(float x in new[]{-20f,20f})foreach(float z in new[]{-14f,14f}){Planter(wind,new Vector3(x,0,z),5,3);Tree(wind,new Vector3(x,.4f,z),5.8f);}
            foreach(var p in new[]{new Vector3(520,0,226),new Vector3(640,0,230),new Vector3(760,0,226),new Vector3(880,0,226)})SignBoard(root,p,p.x<600?"ADVENTURE GARDEN":p.x<700?"WAVE PUMP TRACK":p.x<800?"MEADOW WALK":"WIND GARDEN",180);
        }
        private static void Details(Transform root)
        {
            foreach(float x in new[]{580f,700f,825f,943f})foreach(float z in new[]{-155f,278f}){Disc(root,"Groves meadow edge",new Vector3(x,.045f,z),11,9,grass,false);foreach(int side in new[]{-1,1}){Planter(root,new Vector3(x+side*5,0,z+side*3),4.5f,3);Tree(root,new Vector3(x+side*5,.45f,z+side*3),6.4f);}Bench(root,new Vector3(x,0,z+8),0);}
            foreach(float x in new[]{560f,595f,705f,815f,935f})Planter(root,new Vector3(x,0,-108),5.5f,3);
            foreach(float x in new[]{466f,572f,706f,826f,946f})foreach(float z in new[]{-108f,225f}){Planter(root,new Vector3(x,0,z),5,3);CoastalCityBlockBuilder.AddPromenadePalm(root,new Vector3(x,0,z));}
            foreach(float x in new[]{480f,590f,700f,820f,950f})foreach(float z in new[]{-91f,221f})Lantern(root,new Vector3(x,0,z));
            foreach(float x in new[]{627f,960f})foreach(float z in new[]{-105f,224f})Box(root,"Park waste bin",new Vector3(x,.65f,z),new Vector3(.65f,1.1f,.65f),ink,true);
            SignBoard(root,new Vector3(623,0,-41),"HAVEN COMMONS\nGARDENS / STAGE / PUBLIC ART",0);
            foreach(float z in new[]{-28f,-54f,-80f,-120f,-160f,-200f}){Lantern(root,new Vector3(618,0,z));Box(root,"Flush access centre dash",new Vector3(610,.068f,z),new Vector3(.1f,.003f,3),cream,false);}
            for(int i=0;i<5;i++)Box(root,"Single promenade crossing",new Vector3(610,.07f,-99+i*.8f),new Vector3(7.6f,.003f,.4f),cream,false);
            // Flush parking clear of both the road and the promenade.
            Floor(root,"Commons visitor parking",Rect.MinMaxRect(578,180,605,207),.06f,asphalt);for(int i=0;i<5;i++)Box(root,"Parking stall paint",new Vector3(585+i*4.5f,.069f,200),new Vector3(.07f,.003f,9),cream,false);Floor(root,"Commons parking driveway",Rect.MinMaxRect(599,186,605.96f,192),.06f,asphalt);
        }
        private static void RepairPaving(Transform motel,Transform root)
        {
            int i=0;foreach(var f in motel.GetComponentsInChildren<MeshFilter>()){var r=f.GetComponent<MeshRenderer>();if(r==null||!RoadCuts.Any(c=>c.Overlaps(Footprint(r.bounds))))continue;if(GroundSurfaceStyle.SurfaceFor(r.name)==5||r.name=="Low sidewalk curb"||r.name=="Retained public sidewalk")AsterAccessMeshClipper.ClipLowGround(f,RoadCuts,Folder+"/ClippedPaving"+(i++)+".asset");}
            foreach(var c in motel.GetComponentsInChildren<BoxCollider>()){var b=c.bounds;if(!c.enabled||b.max.y>.25f||!RoadCuts.Any(r=>r.Overlaps(Footprint(b))))continue;if(GroundSurfaceStyle.SurfaceFor(c.name)!=5&&c.name!="Low sidewalk curb"&&c.name!="Retained public sidewalk")continue;c.enabled=false;foreach(var p in StreetPavingLayout.ClearRoads(Footprint(b),RoadCuts)){var go=new GameObject("Retained commons approach paving",typeof(BoxCollider));go.transform.SetParent(root,false);var box=go.GetComponent<BoxCollider>();box.center=new Vector3(p.center.x,b.center.y,p.center.y);box.size=new Vector3(p.width,b.size.y,p.height);}}
        }
        private static void Road(Transform root,Rect r)
        {
            var go=Quad(root,"Connected road",r,.06f,asphalt,true);go.GetComponent<Renderer>().enabled=false;go.AddComponent<RoadSurfaceProfile>().Kind=RoadSurfaceKind.City;
            var cuts=root.parent.GetComponentsInChildren<MeshRenderer>().Where(v=>TownPopulationController.RoadName(v.name)&&!v.transform.IsChildOf(root)).Select(v=>{var b=v.bounds;return Rect.MinMaxRect(b.min.x+.04f,b.min.z+.04f,b.max.x-.04f,b.max.z-.04f);}).ToArray();foreach(var p in StreetPavingLayout.ClearRoads(r,cuts))Quad(root,"Commons asphalt finish",p,.06f,asphalt,false);
        }
        private static void Path(Transform root,Rect r){var roads=root.root.GetComponentsInChildren<MeshRenderer>().Where(v=>v.gameObject.activeInHierarchy&&TownPopulationController.RoadName(v.name)).Select(v=>Footprint(v.bounds)).ToArray();foreach(var p in StreetPavingLayout.ClearRoads(r,roads))Floor(root,"Commons walking path",p,.1f,stone);}
        private static Rect Footprint(Bounds b)=>Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);
        private static void VerticalRing(Transform root,float radius,float thickness,Material m)
        {var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<=96;i++){float a=i*Mathf.PI/48;for(int j=0;j<=8;j++){float b=j*Mathf.PI/4,r=radius+Mathf.Cos(b)*thickness;v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,Mathf.Sin(b)*thickness));if(i>0&&j>0){int n=i*9+j;t.AddRange(new[]{n-10,n-1,n-9,n-1,n,n-9});}}}MeshPart(root,"Sculpted orbital ring",v.ToArray(),t.ToArray(),m,false);}
        private static void Ring(Transform root,string name,Vector3 p,float inner,float outer,float h,Material mat,bool solid)
        {var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<=128;i++){float a=i*Mathf.PI/64;for(int j=0;j<=8;j++){float b=j*Mathf.PI/4,r=(inner+outer)*.5f+Mathf.Cos(b)*(outer-inner)*.5f;v.Add(p+new Vector3(Mathf.Cos(a)*r,Mathf.Sin(b)*h,Mathf.Sin(a)*r));if(i>0&&j>0){int n=i*9+j;t.AddRange(new[]{n-10,n-9,n-1,n-9,n,n-1});}}}MeshPart(root,name,v.ToArray(),t.ToArray(),mat,solid);}
        private static GameObject Disc(Transform root,string name,Vector3 p,float rx,float rz,Material mat,bool solid)
        {var v=new Vector3[98];var t=new List<int>();v[0]=p;for(int i=0;i<=96;i++){float a=i*Mathf.PI/48;v[i+1]=p+new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz);if(i>0)t.AddRange(new[]{0,i+1,i});}return MeshPart(root,name,v,t.ToArray(),mat,solid);}
        private static void Tree(Transform root,Vector3 p,float h){Pipe(root,"Garden tree trunk",p,p+Vector3.up*h*.65f,.28f,wood,true);foreach(int s in new[]{-1,1}){Pipe(root,"Garden spreading branch",p+Vector3.up*h*.45f,p+new Vector3(s*1.5f,h*.8f,0),.14f,wood);Box(root,"Spreading tree crown",p+new Vector3(s*1.2f,h*.85f,0),new Vector3(h*.9f,h*.55f,h*.85f),leaf,false,PrimitiveType.Sphere);}}
        private static void Planter(Transform root,Vector3 p,float w,float d){Box(root,"Landscape planter",p+Vector3.up*.25f,new Vector3(w,.4f,d),stone,true);for(int i=0;i<5;i++){Box(root,"Flowering planter shrub",p+new Vector3((i-2)*w*.17f,.65f,0),new Vector3(w*.24f,.6f,d*.7f),leaf,false,PrimitiveType.Sphere);if(i%2==0)Box(root,"Small flower cluster",p+new Vector3((i-2)*w*.17f,.99f,0),new Vector3(.3f,.16f,.3f),coral,false,PrimitiveType.Sphere);}}
        private static void Bench(Transform root,Vector3 p,float yaw){var g=Group(root,"Commons seating",p,yaw);Box(g,"Bench timber seat",new Vector3(0,.58f,0),new Vector3(3.2f,.14f,.8f),wood,true);Box(g,"Bench timber back",new Vector3(0,.95f,.34f),new Vector3(3.2f,.7f,.12f),wood,true);foreach(int side in new[]{-1,1})Box(g,"Bench foot",new Vector3(side*1.2f,.3f,0),new Vector3(.1f,.5f,.65f),ink,false);}
        private static void Table(Transform root,Vector3 p){Box(root,"Picnic table leg",p+Vector3.up*.55f,new Vector3(.18f,1,.18f),bronze,true);Disc(root,"Round picnic tabletop",p+Vector3.up*1.1f,1.4f,1.4f,wood,false);Box(root,"Table planter",p+Vector3.up*1.3f,new Vector3(.3f,.3f,.3f),cream,false);Box(root,"Table herb",p+Vector3.up*1.6f,new Vector3(.4f,.4f,.4f),leaf,false,PrimitiveType.Sphere);}
        private static void Lantern(Transform root,Vector3 p){Box(root,"Commons lamp column",p+Vector3.up*1.9f,new Vector3(.1f,3.6f,.1f),ink,true);var bulb=Box(root,"Commons light diffuser",p+Vector3.up*3.8f,new Vector3(.6f,.13f,.6f),warm,false);bulbs.Add(bulb.GetComponent<Renderer>());}
        private static void SignBoard(Transform root,Vector3 p,string title,float yaw)
        {var g=Group(root,"Mounted park directory",p,yaw);Box(g,"Directory support",new Vector3(0,1.25f,0),new Vector3(.12f,2.4f,.12f),bronze,true);Box(g,"Directory board",new Vector3(0,2.05f,0),new Vector3(4.5f,1.3f,.16f),ink,false);var t=new GameObject(title).AddComponent<TextMeshPro>();t.transform.SetParent(g,false);t.transform.localPosition=new Vector3(0,2.05f,-.1f);t.font=TMP_Settings.GetFontAsset();t.text=title;t.fontSize=1.8f;t.color=cream.color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(4.2f,1.1f);t.raycastTarget=false;var back=new GameObject(title+" reverse").AddComponent<TextMeshPro>();back.transform.SetParent(g,false);back.transform.localPosition=new Vector3(0,2.05f,.1f);back.transform.localRotation=Quaternion.Euler(0,180,0);back.font=t.font;back.text=title;back.fontSize=t.fontSize;back.color=t.color;back.alignment=t.alignment;back.rectTransform.sizeDelta=t.rectTransform.sizeDelta;back.raycastTarget=false;}
        private static void Floor(Transform root,string name,Rect r,float y,Material mat){Quad(root,name,r,y,mat,true);}
        private static GameObject Quad(Transform root,string name,Rect r,float y,Material mat,bool solid)
        {
            int nx=Mathf.Max(1,Mathf.CeilToInt(r.width/64)),nz=Mathf.Max(1,Mathf.CeilToInt(r.height/64));var v=new List<Vector3>();var t=new List<int>();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)v.Add(new Vector3(Mathf.Lerp(r.xMin,r.xMax,x/(float)nx),y,Mathf.Lerp(r.yMin,r.yMax,z/(float)nz)));
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;t.AddRange(new[]{a,c,b,b,c,d});}
            return MeshPart(root,name,v.ToArray(),t.ToArray(),mat,solid);
        }
        private static GameObject MeshPart(Transform root,string name,Vector3[] v,int[] triangles,Material m,bool solid)
        {var mesh=new Mesh{name=name,vertices=v,triangles=triangles,indexFormat=v.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16,uv=v.Select(p=>new Vector2(p.x*.3f,p.z*.3f)).ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=Save(mesh,Folder+"/Part"+(meshIndex++)+".asset");var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=m;if(solid)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;}
        private static Transform Group(Transform root,string name,Vector3 p,float yaw=0){var g=new GameObject(name).transform;g.SetParent(root,false);g.localPosition=p;g.localRotation=Quaternion.Euler(0,yaw,0);return g;}
        private static GameObject Box(Transform root,string name,Vector3 p,Vector3 size,Material mat,bool solid,PrimitiveType shape=PrimitiveType.Cube){var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());if(solid)go.AddComponent<BoxCollider>();return go;}
        private static void Pipe(Transform root,string name,Vector3 a,Vector3 b,float w,Material mat,bool solid=false){var go=Box(root,name,(a+b)*.5f,new Vector3(w,(b-a).magnitude,w),mat,solid);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        private static Material Mat(string name,Color color,string texture=null){string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",.25f);m.enableInstancing=true;if(texture!=null){m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Albedo.png");m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Normal.png"));m.SetFloat("_BumpScale",.15f);m.EnableKeyword("_NORMALMAP");}EditorUtility.SetDirty(m);return m;}
        private static Mesh Save(Mesh mesh,string path){var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}saved.Clear();EditorUtility.CopySerialized(mesh,saved);saved.UploadMeshData(false);if(saved!=mesh)UnityEngine.Object.DestroyImmediate(mesh);return saved;}
        private static void Merge(Transform root,MeshFilter[] parts,Material m,string path)
        {var mesh=new Mesh{name="Commons "+m.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();var saved=Save(mesh,path);var go=new GameObject(saved.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=saved;go.GetComponent<MeshRenderer>().sharedMaterial=m;foreach(var f in parts){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}}
        private static bool Moving(MeshFilter f)=>art.Any(a=>f.transform.IsChildOf(a));
        private static void Combine(Transform root)
        {
            int index=0;foreach(var moving in art)foreach(var g in moving.GetComponentsInChildren<MeshFilter>().GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial).ToArray())Merge(moving,g.ToArray(),g.Key,Folder+"/Kinetic"+(index++)+".asset");
            var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null&&!Moving(f)&&!TownPopulationController.RoadName(f.name)&&!bulbs.Contains(f.GetComponent<Renderer>())&&!waters.Contains(f.GetComponent<Renderer>())).ToArray();foreach(var g in filters.GroupBy(f=>new{Mat=f.GetComponent<MeshRenderer>().sharedMaterial,Bucket=f.GetComponent<MeshRenderer>().sharedMaterial==glass?f.transform.parent.name:""}))Merge(root,g.ToArray(),g.Key.Mat,Folder+"/Static"+(index++)+".asset");
        }
        private static void RegisterLights(MotelGameRoot m){var light=m.GetComponentInChildren<WorldNightLighting>();if(light==null)return;var so=new SerializedObject(light);var a=so.FindProperty("fixtures");var b=so.FindProperty("glowingSurfaces");int ai=a.arraySize,bi=b.arraySize;a.arraySize+=bulbs.Count;b.arraySize+=bulbs.Count;for(int i=0;i<bulbs.Count;i++){a.GetArrayElementAtIndex(ai+i).objectReferenceValue=bulbs[i].transform;b.GetArrayElementAtIndex(bi+i).objectReferenceValue=bulbs[i];}so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
