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
    /// <summary>Persistent, furnished six-storey hospital, constructed only in the editor.</summary>
    public static partial class HospitalCampusBuilder
    {
        public const string Folder="Assets/_Game/Generated/HospitalCampus";
        static string activeFolder=Folder;
        static Material white,teal,ink,glass,metal,wood,tile,asphalt,lawn,leaf,soil,red,blue,cream,light,yellow,rubber;
        static readonly List<Mesh> temporary=new List<Mesh>();
        static readonly Dictionary<string,Mesh> cubes=new Dictionary<string,Mesh>();
        static readonly List<Transform> batches=new List<Transform>(),moving=new List<Transform>();
        static readonly List<Renderer> bulbs=new List<Renderer>();
        static int serial;static Mesh sphere,cylinder;
        [MenuItem("Roadside Haven/Hospital/Build Grand Medical Centre")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            Directory.CreateDirectory("Backups/HospitalCampus");if(File.Exists(motel.gameObject.scene.path))File.Copy(motel.gameObject.scene.path,"Backups/HospitalCampus/Before-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity",true);
            Build(motel);EditorSceneManager.MarkSceneDirty(motel.gameObject.scene);EditorSceneManager.SaveScene(motel.gameObject.scene);AssetDatabase.SaveAssets();Focus();
        }
        [MenuItem("Roadside Haven/Hospital/Focus Hospital")]
        public static void Focus()=>HospitalEditorAccess.Focus();
        public static Transform Build(MotelGameRoot motel)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Build the hospital in Edit Mode.");
            var old=motel.transform.Find(HospitalCampusLayout.RootName);if(old!=null)return old;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","HospitalCampus");
            activeFolder=Folder;temporary.Clear();cubes.Clear();batches.Clear();moving.Clear();bulbs.Clear();serial=0;Materials();
            var worldRoads=motel.GetComponentsInChildren<MeshRenderer>().Where(r=>r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name)).Select(r=>new Rect(r.bounds.min.x,r.bounds.min.z,r.bounds.size.x,r.bounds.size.z)).ToArray();
            if(worldRoads.Any(r=>r.Overlaps(HospitalCampusLayout.Building)))throw new InvalidOperationException("Hospital parcel overlaps a public road.");
            // Existing highway-end lamps must move with their bulbs before the new connector is paved.
            var lamps=motel.GetComponentsInChildren<MeshRenderer>().Where(r=>r.gameObject.activeInHierarchy&&(r.name=="Streetlight post"||r.name=="Streetlight bulb")).ToArray();
            foreach(var post in lamps.Where(r=>r.name=="Streetlight post")){var at=post.bounds.center;if(!HospitalCampusLayout.Roads.Any(r=>r.Contains(new Vector2(at.x,at.z))))continue;foreach(var part in lamps){var q=part.bounds.center;if(part.transform.parent==post.transform.parent&&Mathf.Abs(q.x-at.x)<.03f&&Mathf.Abs(q.z-at.z)<.03f)part.transform.position+=new Vector3(992-at.x,0,0);}}
            var root=Group(motel.transform,HospitalCampusLayout.RootName,Vector3.zero);root.gameObject.AddComponent<MotelDistrictArt>();
            var exterior=Group(root,"Medical campus architecture",HospitalCampusLayout.Centre);batches.Add(exterior);
            var landscape=Group(root,"Healing gardens and parking",Vector3.zero);batches.Add(landscape);
            Roads(root,landscape);Campus(landscape);Shell(exterior);
            var floors=new Transform[7];
            for(int i=0;i<7;i++){floors[i]=Group(root,"Hospital level "+i+" — "+HospitalCampusLayout.Departments[i],HospitalCampusLayout.Centre+Vector3.up*(HospitalCampusLayout.Storey*i));batches.Add(floors[i]);Floor(floors[i],i);}
            for(int i=0;i<6;i++)Stairs(exterior,i);Roof(exterior);Helicopter(root);Shops(landscape);
            Combine(root);RegisterLights(motel);
            var life=root.gameObject.AddComponent<HospitalCampusLife>();life.Configure(floors,exterior.GetComponentsInChildren<Renderer>().Concat(landscape.GetComponentsInChildren<Renderer>()).ToArray());
            var surface=RoadSurfacePhysics.Rebuild(motel.transform);var saved=Save(UnityEngine.Object.Instantiate(surface.SurfaceMesh),Folder+"/HospitalConnectedRoads.asset");surface.UseSavedMesh(saved);
            foreach(var mesh in temporary)if(mesh!=null&&!AssetDatabase.Contains(mesh))UnityEngine.Object.DestroyImmediate(mesh);AssetDatabase.SaveAssets();return root;
        }
        static void Materials()
        {
            white=Mat("Porcelain facade",new Color(.88f,.92f,.90f),"Stucco");teal=Mat("Medical teal",new Color(.035f,.36f,.39f));ink=Mat("Graphite frames",new Color(.035f,.065f,.08f));metal=Mat("Brushed aluminium",new Color(.48f,.56f,.58f));metal.SetFloat("_Metallic",.65f);metal.SetFloat("_Smoothness",.65f);
            wood=Mat("Warm oak",new Color(.52f,.33f,.18f),"CedarGrain");tile=Mat("Pale terrazzo",new Color(.73f,.79f,.77f),"Paving");asphalt=Mat("Campus asphalt",new Color(.16f,.20f,.23f),"Asphalt");lawn=Mat("Healing garden grass",new Color(.62f,.78f,.48f),"BaseGrass");leaf=Mat("Plant foliage",new Color(.15f,.36f,.14f));soil=Mat("Planter soil",new Color(.34f,.23f,.14f),"DirtPebbles");red=Mat("Emergency coral",new Color(.82f,.09f,.08f));blue=Mat("Ambulance beacon blue",new Color(.08f,.28f,.95f));cream=Mat("Clinical bedding",new Color(.78f,.89f,.88f));light=Mat("LED diffuser",new Color(.81f,.95f,1));yellow=Mat("Safety yellow",new Color(.98f,.73f,.14f));rubber=Mat("Tyre rubber",new Color(.025f,.03f,.035f));
            light.EnableKeyword("_EMISSION");light.SetColor("_EmissionColor",new Color(.7f,.9f,1)*1.7f);
            glass=Mat("Medical blue glazing",new Color(.12f,.36f,.42f,.24f));glass.SetFloat("_Surface",1);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);glass.SetFloat("_Cull",0);glass.SetFloat("_Smoothness",.88f);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;glass.SetShaderPassEnabled("ShadowCaster",false);
        }
        static Material Mat(string name,Color color,string texture=null)
        {
            string path=activeFolder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",.28f);m.enableInstancing=true;
            if(texture!=null){bool d=texture=="Stucco"||texture=="CedarGrain";string prefix=d?"Assets/_Game/Generated/MotelDistrict/":"Assets/_Game/Art/Surfaces/";m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+texture+(d?".png":"_Albedo.png"));var n=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+texture+(d?"Normal.png":"_Normal.png"));if(n!=null){m.SetTexture("_BumpMap",n);m.SetFloat("_BumpScale",.18f);m.EnableKeyword("_NORMALMAP");}}EditorUtility.SetDirty(m);return m;
        }
        static Transform Group(Transform p,string name,Vector3 at,float yaw=0){var g=new GameObject(name).transform;g.SetParent(p,false);g.localPosition=at;g.localRotation=Quaternion.Euler(0,yaw,0);return g;}
        static GameObject Box(Transform p,string name,Vector3 at,Vector3 size,Material mat,bool solid=false)
        {
            var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(p,false);g.transform.localPosition=at;g.GetComponent<MeshFilter>().sharedMesh=Cube(size);g.GetComponent<MeshRenderer>().sharedMaterial=mat;if(solid)g.AddComponent<BoxCollider>().size=size;return g;
        }
        static Mesh Cube(Vector3 size)
        {
            string key=size.ToString("F3");if(cubes.TryGetValue(key,out var mesh))return mesh;
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);mesh=UnityEngine.Object.Instantiate(g.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(g);var v=mesh.vertices;for(int i=0;i<v.Length;i++)v[i]=Vector3.Scale(v[i],size);mesh.vertices=v;
            var uv=mesh.uv;var normals=mesh.normals;for(int i=0;i<v.Length;i++)uv[i]=Mathf.Abs(normals[i].y)>.5f?new Vector2(v[i].x,v[i].z):Mathf.Abs(normals[i].x)>.5f?new Vector2(v[i].z,v[i].y):new Vector2(v[i].x,v[i].y);mesh.uv=uv;mesh.RecalculateBounds();mesh.name="Medical cuboid";temporary.Add(mesh);cubes[key]=mesh;return mesh;
        }
        static GameObject Shape(Transform p,string name,Vector3 at,Vector3 scale,Material mat,bool ball=true)
        {
            if(sphere==null||cylinder==null){var s=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere=s.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(s);s=GameObject.CreatePrimitive(PrimitiveType.Cylinder);cylinder=s.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(s);}
            var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localScale=scale;g.GetComponent<MeshFilter>().sharedMesh=ball?sphere:cylinder;g.GetComponent<MeshRenderer>().sharedMaterial=mat;return g;
        }
        static void Beam(Transform p,string n,Vector3 a,Vector3 b,float width,Material mat,bool solid=false){var g=Box(p,n,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),mat,solid);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        static void Label(Transform p,string text,Vector3 at,float w,float h,float yaw=0,Material backing=null)
        {
            var t=new GameObject("Sign — "+text).AddComponent<TextMeshPro>();t.transform.SetParent(p,false);t.transform.localPosition=at;t.transform.localRotation=Quaternion.Euler(0,yaw,0);t.font=TMP_Settings.GetFontAsset();t.text=text;t.color=new Color(.9f,.98f,.97f);t.fontSize=4;t.enableAutoSizing=true;t.fontSizeMin=.7f;t.fontSizeMax=h*5;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(w,h);t.raycastTarget=false;t.overflowMode=TextOverflowModes.Ellipsis;
            if(backing!=null){var b=Box(p,"Sign backing",at+Quaternion.Euler(0,yaw,0)*Vector3.forward*.06f,new Vector3(w+.25f,h+.15f,.08f),backing);b.transform.localRotation=Quaternion.Euler(0,yaw,0);}
        }
        static void Cross(Transform p,Vector3 at,float size,Material mat){Box(p,"Medical cross",at,new Vector3(size*.3f,size,.12f),mat);Box(p,"Medical cross",at,new Vector3(size,size*.3f,.12f),mat);}
        static void Ground(Transform p,string name,Rect r,float top,Material mat,bool solid=true){Box(p,name,new Vector3(r.center.x,top-.09f,r.center.y),new Vector3(r.width,.18f,r.height),mat,solid);}
        static void Roads(Transform root,Transform site)
        {
            foreach(var r in HospitalCampusLayout.Roads){var g=Box(root,"Connected road",new Vector3(r.center.x,.015f,r.center.y),new Vector3(r.width,.09f,r.height),asphalt);moving.Add(g.transform);
                bool ns=r.height>r.width;float a=ns?r.yMin:r.xMin,b=ns?r.yMax:r.xMax;for(float n=a+8;n<b-6;n+=12){var p=ns?new Vector3(r.center.x,.063f,n):new Vector3(n,.063f,r.center.y);Box(site,"Road paint — no collision",p,ns?new Vector3(.12f,.002f,4):new Vector3(4,.002f,.12f),yellow);}}
            // Footpaths are cut around every junction, keeping all transverse paving out of driving lanes.
            var all=HospitalCampusLayout.Roads.ToList();foreach(var r in HospitalCampusLayout.Roads){bool ns=r.height>r.width;foreach(int side in new[]{-1,1}){var walk=ns?new Rect(side<0?r.xMin-3:r.xMax,r.yMin,3,r.height):new Rect(r.xMin,side<0?r.yMin-3:r.yMax,r.width,3);foreach(var cut in StreetPavingLayout.ClearRoads(walk,all))Ground(site,"Campus roadside walk",cut,.12f,tile);}}
            for(float z=-490;z<-35;z+=45)Lamp(site,new Vector3(991,0,z),7);
            for(float x=750;x<970;x+=45)Lamp(site,new Vector3(x,0,-532),7);
            Label(site,"HAVEN GRAND MEDICAL CENTRE  →",new Vector3(965,3,-30),25,1.8f,180,teal);
        }
        static void Campus(Transform p)
        {
            Ground(p,"Medical campus foundation",HospitalCampusLayout.Campus,.015f,lawn);
            Ground(p,"Main entrance plaza",Rect.MinMaxRect(546,-467,660,-458),.12f,tile);
            Ground(p,"Emergency plaza",Rect.MinMaxRect(648,-466,693,-420),.12f,tile);
            Ground(p,"Patient healing garden",Rect.MinMaxRect(482,-466,541,-376),.04f,lawn);
            Ground(p,"Garden promenade",Rect.MinMaxRect(506,-490,510,-378),.12f,tile);
            Ground(p,"Entry accessible walk",Rect.MinMaxRect(584,-513,616,-475),.12f,tile);
            Parking(p,Rect.MinMaxRect(545,-510,690,-483),false);Parking(p,Rect.MinMaxRect(545,-554,690,-533),true);
            for(int n=0;n<4;n++)Ambulance(p,new Vector3(660+n*8,.12f,-435),180,n);
            for(int i=0;i<5;i++){float z=-450+i*16;Planter(p,new Vector3(493,0,z),new Vector2(6,5));Tree(p,new Vector3(493,0,z));Bench(p,new Vector3(503,.12f,z),90);}
            for(int i=0;i<6;i++){float x=552+i*20;Planter(p,new Vector3(x,0,-398),new Vector2(5,5));Tree(p,new Vector3(x,0,-398));Lamp(p,new Vector3(x,0,-389),4);}
            for(float x=555;x<690;x+=24)Lamp(p,new Vector3(x,0,-478),5);
            for(float z=-450;z<-380;z+=24)Lamp(p,new Vector3(513,0,z),4);
            var pavilion=Group(p,"Healing garden pavilion",new Vector3(520,.12f,-390));for(int s=-1;s<=1;s+=2)for(int t=-1;t<=1;t+=2)Box(pavilion,"Pergola column",new Vector3(s*5,1.7f,t*4),new Vector3(.22f,3.4f,.22f),wood,true);for(int i=0;i<12;i++)Box(pavilion,"Pergola shade beam",new Vector3(-5.5f+i,3.45f,0),new Vector3(.18f,.18f,9),wood);Bench(pavilion,new Vector3(0,0,2),0);
            Label(p,"HAVEN GRAND\nMEDICAL CENTRE\n24 HOUR EMERGENCY",new Vector3(696,3.6f,-506),8,5,0,teal);Cross(p,new Vector3(696,7.5f,-506),2,red);
        }
        static void Parking(Transform p,Rect area,bool south)
        {
            Ground(p,"Visitor parking asphalt",area,.06f,asphalt);int count=27;
            for(int i=0;i<=count;i++){float x=area.xMin+i*area.width/count;foreach(float z in new[]{area.yMin+3,area.yMax-3})Box(p,"Parking bay paint",new Vector3(x,.063f,z),new Vector3(.09f,.002f,5.7f),white);}
            for(int i=0;i<8;i++){if(i==3||i==6)continue;float x=area.xMin+8+i*17;var car=Group(p,"Visitor car",new Vector3(x,.08f,south?area.yMin+3:area.yMax-3),south?0:180);Car(car,i);}
            var sign=new Vector3(area.xMax+2,2,area.yMax-2);Label(p,south?"VISITOR PARKING B":"PARKING A\nACCESSIBLE BAYS",sign,4,1.1f,0,teal);Box(p,"Parking sign post",sign+Vector3.down*1.1f,new Vector3(.1f,2.2f,.1f),metal,true);
        }
        static void Shell(Transform p)
        {
            for(int f=0;f<6;f++){
                float y=f*HospitalCampusLayout.Storey;
                foreach(int edge in new[]{-1,1}){Box(p,"Facade horizontal ribbon",new Vector3(0,y+4.6f,edge*23.2f),new Vector3(89,.4f,.8f),white);Box(p,"Side horizontal ribbon",new Vector3(edge*44.2f,y+4.6f,0),new Vector3(.8f,.4f,46.8f),white);}
                foreach(int s in new[]{-1,1}){
                    Box(p,"Opaque north/south sill",new Vector3(0,y+.65f,s*23),new Vector3(88,1.05f,.28f),white,true);
                    if(f>0||s>0)Box(p,"Facade clinical glazing",new Vector3(0,y+2.7f,s*23),new Vector3(87,2.9f,.16f),glass,true);
                    else foreach(int side in new[]{-1,1})Box(p,"Entrance glazing",new Vector3(side*25.5f,2.7f,-23),new Vector3(37,2.9f,.16f),glass,true);
                    for(int x=-42;x<=42;x+=6){if(f==0&&s<0&&Mathf.Abs(x)<8)continue;Box(p,"Vertical solar fin",new Vector3(x,y+2.5f,s*23.5f),new Vector3(.16f,4.1f,1),f%2==0?white:teal);}
                }
                foreach(int s in new[]{-1,1}){Box(p,"Side facade glazing",new Vector3(s*44,y+2.6f,0),new Vector3(.18f,4.5f,46),glass,true);for(int z=-21;z<=21;z+=6)Box(p,"Side facade fin",new Vector3(s*44.35f,y+2.5f,z),new Vector3(.8f,4.1f,.16f),white);}
            }
            // Remove the ground-level sill across the actual doorway instead of leaving an invisible barrier.
            var sill=p.GetComponentsInChildren<BoxCollider>().First(c=>c.name=="Opaque north/south sill"&&c.transform.localPosition.z<0&&c.transform.localPosition.y<1);UnityEngine.Object.DestroyImmediate(sill.gameObject);
            foreach(int side in new[]{-1,1})Box(p,"Entrance low wall",new Vector3(side*25.5f,.65f,-23),new Vector3(37,1.05f,.28f),white,true);
            Box(p,"Hospital entrance canopy",new Vector3(0,4.1f,-28),new Vector3(30,.30f,12),white,true);foreach(int s in new[]{-1,1})Box(p,"Canopy support",new Vector3(s*13,2,-31.5f),new Vector3(.35f,4,.35f),metal,true);
            Label(p,"HAVEN GRAND MEDICAL CENTRE",new Vector3(0,5.7f,-24.3f),52,1.9f,0,teal);Cross(p,new Vector3(-35,6,-24.35f),3,red);
            var entrance=Group(p,"Automatic main entrance",new Vector3(0,.12f,-23));Door(entrance,3.4f);
            foreach(int side in new[]{-1,1})Box(p,"Fixed entrance side glazing",new Vector3(side*5.2f,2.02f,-23),new Vector3(3.6f,3.6f,.10f),glass,true);
            var er=Group(p,"Emergency covered ambulance entrance",new Vector3(49,0,-16));Box(er,"Emergency canopy",new Vector3(6,3.7f,0),new Vector3(15,.24f,11),teal,true);Label(er,"EMERGENCY  /  AMBULANCES ONLY",new Vector3(6,3,-5.6f),14,.9f,0,red);
            // Glazing on the east is opened only at the ground emergency bay.
            var east=p.GetComponentsInChildren<BoxCollider>().First(c=>c.name=="Side facade glazing"&&c.transform.localPosition.x>0&&c.transform.localPosition.y<3);UnityEngine.Object.DestroyImmediate(east.gameObject);
            Box(p,"ER east glazing",new Vector3(44,2.6f,6),new Vector3(.18f,4.5f,34),glass,true);Box(p,"ER east south glazing",new Vector3(44,2.6f,-21.5f),new Vector3(.18f,4.5f,3),glass,true);
            var erdoor=Group(p,"Automatic emergency entrance",new Vector3(44,.12f,-16),90);Door(erdoor,2.4f);
        }
        static void Floor(Transform p,int level)
        {
            const float top=.12f;
            Ground(p,"Hospital walkable floor",Rect.MinMaxRect(-44,-23,29,23),top,tile);
            Ground(p,"Stair bottom landing",Rect.MinMaxRect(29,-23,44,-9),top,tile);
            Ground(p,"Stair north gallery",Rect.MinMaxRect(29,1.8f,44,23),top,tile);
            Ground(p,"Stair west edge",Rect.MinMaxRect(29,-9,31,1.8f),top,tile);
            Ground(p,"Stair east edge",Rect.MinMaxRect(42.5f,-9,44,1.8f),top,tile);
            Label(p,level+"  /  "+HospitalCampusLayout.Departments[level],new Vector3(-12,3.1f,3.9f),30,1,0,teal);
            Label(p,"LIFT  ←      STAIRS  →",new Vector3(8,3.1f,-3.9f),22,.8f,180,ink);
            // A spacious lift lobby on every level; floor changes are fade-assisted to avoid physics snaps.
            Box(p,"Lift rear wall",new Vector3(-36,2.15f,3),new Vector3(8,4,.18f),metal,true);
            foreach(int s in new[]{-1,1})Box(p,"Lift side wall",new Vector3(-36+s*4,2.15f,0),new Vector3(.18f,4,6),metal,true);
            Label(p,level==6?"ROOF / HELIPAD":"LIFT  /  LEVEL "+level,new Vector3(-36,3.5f,-3.15f),7,.7f,0,ink);
            Box(p,"Lift destination panel",new Vector3(-33,1.2f,-3),new Vector3(.35f,1.2f,.12f),teal);Label(p,"0 1 2\n3 4 5 R",new Vector3(-33,1.3f,-3.08f),.3f,.7f);
            for(float x=-24;x<27;x+=10)Box(p,"Clinical ceiling LED",new Vector3(x,4.35f,0),new Vector3(3,.06f,.55f),light);
            if(level==6){Label(p,"AIR AMBULANCE\nKEEP PAD CLEAR",new Vector3(-20,2,4),9,1.4f,0,teal);return;}
            if(level==0){Lobby(p);return;}
            for(int i=0;i<4;i++)Room(p,level,i,-23+i*14,1);
            for(int i=0;i<3;i++)Room(p,level,i+4,-23+i*14,-1);
            // Clear east corridor joins the stair landing, independent of every patient's room.
            Box(p,"Stair wayfinding stripe",new Vector3(27,.126f,-6),new Vector3(.18f,.003f,16),teal);
            Label(p,"STAIRS / ROOF",new Vector3(28,2.8f,-12),5,.8f,-90,teal);
            Person(p,level,3000+level,true,false,false,new Vector3(-18,.14f,0),new[]{new Vector3(-18,.14f,0),new Vector3(20,.14f,0)});
            Person(p,level,3100+level,true,false,false,new Vector3(13,.14f,1.7f),null);
            Bench(p,new Vector3(-26,.12f,-2.5f),0);Planter(p,new Vector3(-29,.12f,2),new Vector2(1.2f,1.2f));
        }
        static void Lobby(Transform p)
        {
            Box(p,"Reception desk stone plinth",new Vector3(0,.65f,1),new Vector3(16,1.1f,2.2f),white,true);Box(p,"Reception desk oak face",new Vector3(0,.72f,-.15f),new Vector3(15.5f,.8f,.1f),wood);Box(p,"Reception worktop",new Vector3(0,1.25f,1),new Vector3(16.4f,.12f,2.5f),teal,true);
            for(int i=0;i<3;i++)Computer(p,new Vector3(-5+i*5,1.34f,1));
            Label(p,"RECEPTION  /  INFORMATION",new Vector3(0,3.1f,3.75f),16,.8f,0,teal);
            for(int i=0;i<3;i++)Person(p,0,3220+i,true,false,false,new Vector3(-5+i*5,.14f,3),null);
            for(int side=-1;side<=1;side+=2)for(int row=0;row<3;row++)Bench(p,new Vector3(side*16,.12f,-8-row*4),side<0?90:-90);
            for(int i=0;i<5;i++)Person(p,0,3300+i,false,false,false,new Vector3(-22+i*9,.14f,-7-i%2*5),null);
            for(int i=0;i<3;i++){Room(p,0,i,-23+i*17,1);}
            Label(p,"EMERGENCY  →",new Vector3(22,3,-6),10,1,0,red);Label(p,"PHARMACY / CAFÉ\nOUTSIDE WEST ENTRANCE",new Vector3(-25,2.6f,-20),12,1.6f,180,teal);
            for(int i=0;i<2;i++){Bed(p,new Vector3(35,.12f,-18+i*5),false);Person(p,0,3400+i,false,true,true,new Vector3(35,.12f,-18+i*5),null);}
            Person(p,0,3420,true,false,false,new Vector3(32,.14f,-17),null);
            Planter(p,new Vector3(-25,.12f,-20),new Vector2(2,2));Planter(p,new Vector3(24,.12f,-20),new Vector2(2,2));
            Label(p,"WAYFINDING\n0 Emergency / Reception\n1 Diagnostics / Clinics\n2 General wards\n3 Surgery / Recovery\n4 ICU / Cardiac\n5 Maternity / Family\nR Air ambulance",new Vector3(-29,2.1f,3.8f),8,3.4f,0,ink);
        }
        static void Room(Transform p,int floor,int index,float x,int side)
        {
            float z=side*13;var g=Group(p,"Clinical room "+floor+"-"+(index+1),new Vector3(x,0,z));
            foreach(int s in new[]{-1,1})Box(g,"Room dividing wall",new Vector3(s*6.8f,2.2f,0),new Vector3(.16f,4.15f,17),white,true);
            // Each room has a 2.6 m clear doorway onto the main corridor.
            foreach(int s in new[]{-1,1})Box(g,"Room doorway wall",new Vector3(s*4.15f,1.7f,-side*8.6f),new Vector3(5.7f,3.1f,.16f),white,true);
            Box(g,"Room door lintel",new Vector3(0,3.85f,-side*8.6f),new Vector3(13.6f,.7f,.16f),teal,true);
            string kind=floor==0?"CONSULTATION":floor==1?(index%2==0?"DIAGNOSTICS":"OUTPATIENT"):floor==4?"INTENSIVE CARE":floor==5?"FAMILY WARD":"PATIENT WARD";
            Label(g,kind+"  "+floor+"0"+(index+1),new Vector3(0,3.3f,-side*8.72f),11,.55f,side>0?0:180,teal);
            Box(g,"Room LED panel",new Vector3(0,4.35f,0),new Vector3(3,.05f,1.4f),light);
            if(floor<=1){Desk(g,new Vector3(-3,.12f,2));Computer(g,new Vector3(-3,1.16f,2));Bed(g,new Vector3(3,.12f,1),floor==1&&index%2==0);Person(g,floor,4000+floor*20+index,true,false,false,new Vector3(-3,.14f,3.4f),null);if(index%2==0)Person(g,floor,4400+floor*20+index,false,true,true,new Vector3(3,.12f,1),null);}
            else for(int bed=0;bed<2;bed++){float bx=bed==0?-3.3f:3.3f;Bed(g,new Vector3(bx,.12f,1.5f),floor==4);if(bed==0||index%3==0)Person(g,floor,5000+floor*100+index*2+bed,false,true,true,new Vector3(bx,.12f,1.5f),null);}
            Cabinet(g,new Vector3(-4.8f,.12f,6.3f));Box(g,"Handwash basin pedestal",new Vector3(4.8f,.65f,6.5f),new Vector3(1.3f,1.05f,.9f),white,true);Shape(g,"Clinical sink bowl",new Vector3(4.8f,1.19f,6.5f),new Vector3(1,.12f,.65f),metal);Beam(g,"Tap",new Vector3(4.8f,1.2f,6.7f),new Vector3(4.8f,1.5f,6.7f),.06f,metal);
            Box(g,"Hygiene dispenser",new Vector3(5.9f,1.5f,-side*7.7f),new Vector3(.25f,.4f,.18f),teal);Bench(g,new Vector3(0,.12f,-5),0);
        }
        static void Bed(Transform p,Vector3 at,bool monitor)
        {
            var g=Group(p,"Patient hospital bed",at);Box(g,"Bed chassis",new Vector3(0,.57f,0),new Vector3(1.35f,.22f,2.5f),metal,true);Box(g,"Clinical mattress",new Vector3(0,.81f,0),new Vector3(1.23f,.28f,2.35f),cream,true);Box(g,"Fresh pillow",new Vector3(0,1,.85f),new Vector3(1,.18f,.45f),white);
            Box(g,"Bed teal footboard",new Vector3(0,.95f,-1.26f),new Vector3(1.4f,.6f,.08f),teal,true);Box(g,"Bed headboard",new Vector3(0,1,.99f),new Vector3(1.4f,.8f,.08f),white,true);
            foreach(int s in new[]{-1,1})foreach(int t in new[]{-1,1}){Shape(g,"Caster",new Vector3(s*.53f,.22f,t*.9f),new Vector3(.18f,.18f,.18f),rubber);Beam(g,"Bed rail",new Vector3(s*.68f,1.05f,-.8f),new Vector3(s*.68f,1.05f,.7f),.06f,metal);}
            Box(g,"Bedside cabinet",new Vector3(1.15f,.56f,.7f),new Vector3(.7f,1,.7f),wood,true);Box(g,"Water cup",new Vector3(1.15f,1.12f,.7f),new Vector3(.16f,.22f,.16f),white);
            Beam(g,"IV pole",new Vector3(-1,.16f,.7f),new Vector3(-1,2.4f,.7f),.045f,metal);Beam(g,"IV top",new Vector3(-1.25f,2.35f,.7f),new Vector3(-.75f,2.35f,.7f),.035f,metal);Box(g,"IV fluid bag",new Vector3(-1.15f,2.1f,.7f),new Vector3(.22f,.38f,.10f),glass);
            if(monitor){Beam(g,"Monitor stand",new Vector3(1.25f,.12f,-.6f),new Vector3(1.25f,1.6f,-.6f),.05f,metal);Box(g,"Vital signs monitor",new Vector3(1.25f,1.7f,-.6f),new Vector3(.8f,.5f,.16f),ink);Label(g,"HR 72  /  SpO2 98\n~ ~ ~ ~ ~",new Vector3(1.25f,1.7f,-.69f),.72f,.38f);}
        }
        static void Stairs(Transform p,int floor)
        {
            float y=.12f+floor*HospitalCampusLayout.Storey;
            for(int i=1;i<=12;i++){
                float h=i*.2f;Box(p,"Stair tread A "+floor+"/"+i,new Vector3(33.25f,y+h*.5f,-9+(i-.5f)*.6f),new Vector3(3.5f,h,.6f),tile,true);
                Box(p,"Stair tread B "+floor+"/"+i,new Vector3(40.5f,y+2.4f+h*.5f,-1.8f-(i-.5f)*.6f),new Vector3(3.5f,h,.6f),tile,true);
            }
            Box(p,"Stair mid landing",new Vector3(36.75f,y+2.3f,0),new Vector3(11.5f,.2f,3.6f),tile,true);
            for(int side=0;side<2;side++){
                float x=side==0?31.45f:35.05f;Beam(p,"Stair handrail A",new Vector3(x,y+1,-9),new Vector3(x,y+3.4f,-1.8f),.065f,metal,true);
                x=side==0?38.7f:42.3f;Beam(p,"Stair handrail B",new Vector3(x,y+5.8f,-9),new Vector3(x,y+3.4f,-1.8f),.065f,metal,true);
            }
            Box(p,"Mid landing safety guard",new Vector3(36.75f,y+2.95f,1.78f),new Vector3(11.5f,1.1f,.10f),glass,true);
            for(int i=0;i<=6;i++){float z=-9+i*1.2f;Beam(p,"Stair baluster",new Vector3(35.05f,y+i*.4f,z),new Vector3(35.05f,y+i*.4f+1,z),.05f,metal);Beam(p,"Stair baluster",new Vector3(38.7f,y+4.8f-i*.4f,z),new Vector3(38.7f,y+5.8f-i*.4f,z),.05f,metal);}
        }
        static void Roof(Transform p)
        {
            float y=.12f+6*HospitalCampusLayout.Storey;
            foreach(int s in new[]{-1,1}){Box(p,"Roof safety parapet",new Vector3(0,y+.65f,s*22.9f),new Vector3(88,1.1f,.25f),white,true);Box(p,"Roof side parapet",new Vector3(s*43.9f,y+.65f,0),new Vector3(.25f,1.1f,46),white,true);}
            Ground(p,"Elevated helipad foundation",Rect.MinMaxRect(-15,-8,15,22),y+.12f,teal);
            const int n=64;for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;Beam(p,"Helipad safety circle",new Vector3(Mathf.Cos(a)*12,y+.135f,7+Mathf.Sin(a)*12),new Vector3(Mathf.Cos(b)*12,y+.135f,7+Mathf.Sin(b)*12),.18f,yellow);}
            foreach(int s in new[]{-1,1})Box(p,"Helipad H paint",new Vector3(s*2.5f,y+.14f,7),new Vector3(.7f,.003f,7),white);Box(p,"Helipad H centre",new Vector3(0,y+.14f,7),new Vector3(5,.003f,.7f),white);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var bulb=Shape(p,"Helipad perimeter beacon",new Vector3(Mathf.Cos(a)*14,y+.22f,7+Mathf.Sin(a)*14),new Vector3(.22f,.18f,.22f),light);bulbs.Add(bulb.GetComponent<Renderer>());}
            for(int i=0;i<3;i++){Box(p,"Rooftop HVAC enclosure",new Vector3(-30+i*5,y+1,-15),new Vector3(4,2,4),white,true);for(int j=0;j<5;j++)Box(p,"HVAC louvre",new Vector3(-30+i*5,y+.4f+j*.3f,-17.05f),new Vector3(3.6f,.08f,.1f),ink);}
            Box(p,"Roof medical transfer canopy",new Vector3(-26,y+3,-1),new Vector3(12,.18f,8),white);Label(p,"ROOFTOP MEDICAL TRANSFER",new Vector3(-26,y+2,-5),11,.8f,0,teal);
            Box(p,"Roof stair access canopy",new Vector3(36.5f,y+3.3f,-9.5f),new Vector3(15.2f,.2f,27),white,true);
            foreach(float z in new[]{-23f,4f})Box(p,"Roof stair enclosure end",new Vector3(36.5f,y+1.6f,z),new Vector3(15,3.2f,.18f),glass,true);
            Box(p,"Roof stair enclosure east",new Vector3(44,y+1.6f,-9.5f),new Vector3(.18f,3.2f,27),glass,true);
            Box(p,"Roof stair enclosure west",new Vector3(29,y+1.6f,-3.75f),new Vector3(.18f,3.2f,15.5f),glass,true);
            Box(p,"Roof stair entrance return",new Vector3(29,y+1.6f,-18.75f),new Vector3(.18f,3.2f,8.5f),glass,true);
            Label(p,"STAIRS / ALL FLOORS",new Vector3(28.85f,y+2.6f,-13),6,.6f,90,teal);
        }
        static void Person(Transform p,int floor,int seed,bool doctor,bool patient,bool bed,Vector3 at,Vector3[] route)
        {var g=Group(p,(doctor?"Doctor / Nurse ":patient?"Patient ":"Visitor ")+seed,at);g.gameObject.AddComponent<HospitalPerson>().Configure(seed,floor,doctor,patient,bed,route);}
        static void Door(Transform p,float half)
        {
            var a=Box(p,"Sliding safety glass left",new Vector3(-half*.5f,1.9f,0),new Vector3(half-.08f,3.6f,.1f),glass,true).transform;
            var b=Box(p,"Sliding safety glass right",new Vector3(half*.5f,1.9f,0),new Vector3(half-.08f,3.6f,.1f),glass,true).transform;
            foreach(var t in new[]{a,b}){Box(t,"Door high visibility band",new Vector3(0,0,-.07f),new Vector3(half-.1f,.12f,.035f),teal);Box(t,"Door bottom rail",new Vector3(0,-1.65f,0),new Vector3(half,.12f,.16f),metal);}
            p.gameObject.AddComponent<DistrictSlidingDoor>().Configure(a,b,half+.1f);moving.Add(p);
        }
        static void Bench(Transform p,Vector3 at,float yaw)
        {
            var g=Group(p,"Waiting bench",at,yaw);Box(g,"Upholstered seats",new Vector3(0,.48f,0),new Vector3(3.2f,.18f,.65f),teal,true);Box(g,"Bench backrest",new Vector3(0,.9f,.3f),new Vector3(3.2f,.75f,.14f),teal,true);foreach(int s in new[]{-1,1}){Box(g,"Bench legs",new Vector3(s*1.2f,.23f,0),new Vector3(.12f,.46f,.45f),metal);Box(g,"Bench armrest",new Vector3(s*1.55f,.73f,0),new Vector3(.06f,.1f,.75f),metal);}
        }
        static void Desk(Transform p,Vector3 at){Box(p,"Consultation desk",at+new Vector3(0,1,0),new Vector3(3.2f,.12f,1.4f),wood,true);foreach(int s in new[]{-1,1})Box(p,"Desk pedestal",at+new Vector3(s*1.15f,.48f,0),new Vector3(.5f,.96f,1.2f),white,true);}
        static void Computer(Transform p,Vector3 at){Box(p,"Computer display",at+new Vector3(0,.48f,.1f),new Vector3(.9f,.55f,.1f),ink);Box(p,"Medical display glow",at+new Vector3(0,.48f,.04f),new Vector3(.8f,.46f,.015f),teal);Box(p,"Display stand",at+new Vector3(0,.17f,.1f),new Vector3(.12f,.3f,.08f),metal);Box(p,"Keyboard",at+new Vector3(0,.02f,-.25f),new Vector3(.65f,.04f,.22f),ink);}
        static void Cabinet(Transform p,Vector3 at){Box(p,"Clinical storage cabinet",at+new Vector3(0,1,0),new Vector3(1.6f,2,.6f),white,true);for(int i=0;i<3;i++){Box(p,"Storage drawer reveal",at+new Vector3(0,.35f+i*.55f,-.31f),new Vector3(1.45f,.025f,.02f),metal);Box(p,"Drawer pull",at+new Vector3(0,.55f+i*.55f,-.35f),new Vector3(.28f,.035f,.04f),metal);}}
        static void Planter(Transform p,Vector3 at,Vector2 size){Box(p,"Garden planter",at+Vector3.up*.3f,new Vector3(size.x,.6f,size.y),white,true);Box(p,"Planter soil",at+Vector3.up*.61f,new Vector3(size.x-.18f,.03f,size.y-.18f),soil);for(int i=0;i<3;i++)Shape(p,"Trimmed garden planting",at+new Vector3((i-1)*size.x*.25f,.85f,0),new Vector3(size.x*.4f,.65f,size.y*.7f),leaf);}
        static void Tree(Transform p,Vector3 at){Beam(p,"Garden tree trunk",at,at+Vector3.up*4,.28f,wood,true);for(int i=0;i<3;i++)Shape(p,"Garden tree crown",at+new Vector3((i-1)*1.2f,5.5f+(i%2),0),new Vector3(4.5f,3.7f,4.5f),leaf);}
        static void Lamp(Transform p,Vector3 at,float height){Box(p,"Campus lamp post",at+Vector3.up*(height*.5f),new Vector3(.12f,height,.12f),ink,true);Box(p,"Campus lamp canopy",at+Vector3.up*height,new Vector3(1.2f,.12f,1.2f),ink);var b=Box(p,"Campus light diffuser",at+Vector3.up*(height-.10f),new Vector3(.8f,.06f,.8f),light);bulbs.Add(b.GetComponent<Renderer>());}
        static void Car(Transform p,int index)
        {
            var mat=index%3==0?teal:index%3==1?white:ink;Box(p,"Parked car lower body",new Vector3(0,.65f,0),new Vector3(1.85f,.7f,4.3f),mat,true);Shape(p,"Parked car cabin",new Vector3(0,1.2f,-.25f),new Vector3(1.7f,1.1f,2.5f),ink);
            foreach(int s in new[]{-1,1})foreach(int t in new[]{-1,1}){var w=Shape(p,"Car wheel",new Vector3(s*.9f,.4f,t*1.35f),new Vector3(.65f,.15f,.65f),rubber,false);w.transform.localRotation=Quaternion.Euler(0,0,90);Shape(p,"Wheel alloy",new Vector3(s*1.04f,.4f,t*1.35f),new Vector3(.4f,.08f,.4f),metal,false).transform.localRotation=Quaternion.Euler(0,0,90);}
            foreach(int s in new[]{-1,1})Box(p,"Car running light",new Vector3(s*.6f,.75f,2.16f),new Vector3(.48f,.12f,.04f),light);
        }
        static void Ambulance(Transform p,Vector3 at,float yaw,int index)
        {
            var g=Group(p,"Emergency ambulance "+(index+1),at,yaw);
            Box(g,"Ambulance body",new Vector3(0,1.45f,-.55f),new Vector3(2.5f,2.35f,4.4f),white,true);Box(g,"Ambulance driving cabin",new Vector3(0,1.2f,2.3f),new Vector3(2.3f,1.9f,1.65f),white,true);Box(g,"Ambulance hood",new Vector3(0,.9f,3.3f),new Vector3(2.3f,.65f,.6f),white,true);
            Box(g,"Wide ambulance windshield",new Vector3(0,1.8f,3.16f),new Vector3(2.06f,.85f,.08f),ink);
            foreach(int s in new[]{-1,1}){
                Box(g,"Ambulance reflective stripe",new Vector3(s*1.26f,1.05f,-.3f),new Vector3(.02f,.35f,4.8f),red);Box(g,"Paramedic cabin side window",new Vector3(s*1.16f,1.85f,2.4f),new Vector3(.025f,.7f,1),ink);
                Box(g,"Ambulance side medical cross",new Vector3(s*1.265f,1.9f,-.8f),new Vector3(.025f,.8f,.23f),red);Box(g,"Ambulance side medical cross",new Vector3(s*1.265f,1.9f,-.8f),new Vector3(.025f,.23f,.8f),red);
                Box(g,"Ambulance mirror",new Vector3(s*1.38f,1.95f,2.9f),new Vector3(.25f,.35f,.2f),ink);
                Box(g,"Ambulance LED lamp",new Vector3(s*.8f,.9f,3.62f),new Vector3(.5f,.25f,.08f),light);
                for(int t=-1;t<=1;t+=2){var w=Shape(g,"Ambulance wheel",new Vector3(s*1.13f,.48f,t*2.05f),new Vector3(.9f,.2f,.9f),rubber,false);w.transform.localRotation=Quaternion.Euler(0,0,90);Shape(g,"Ambulance alloy wheel",new Vector3(s*1.32f,.48f,t*2.05f),new Vector3(.55f,.07f,.55f),metal,false).transform.localRotation=Quaternion.Euler(0,0,90);}
            }
            Box(g,"Ambulance light bar",new Vector3(0,2.78f,1),new Vector3(1.6f,.12f,.35f),metal);foreach(int s in new[]{-1,1})Box(g,"Ambulance blue beacon",new Vector3(s*.55f,2.9f,1),new Vector3(.45f,.22f,.35f),blue);
            Box(g,"Rear double door seam",new Vector3(0,1.6f,-2.77f),new Vector3(.04f,2,.02f),ink);Label(g,"AMBULANCE\nHAVEN MEDICAL",new Vector3(0,1.6f,-2.8f),2,1.2f,0,teal);
        }
        static void Shops(Transform p)
        {
            for(int i=0;i<3;i++){
                float x=486+i*18;var g=Group(p,i==0?"Grand Medical Pharmacy":i==1?"Family Café":"Flowers and convenience",new Vector3(x,.12f,-493));
                Box(g,"Shop floor",new Vector3(0,-.06f,0),new Vector3(16,.12f,13),tile,true);Box(g,"Shop back wall",new Vector3(0,2.5f,6.5f),new Vector3(16,5,.18f),white,true);foreach(int s in new[]{-1,1}){Box(g,"Shop side wall",new Vector3(s*8,2.5f,0),new Vector3(.18f,5,13),white,true);Box(g,"Shop front glass",new Vector3(s*5.1f,2.1f,-6.5f),new Vector3(5.8f,4.2f,.1f),glass,true);}
                Box(g,"Shop roof",new Vector3(0,5,0),new Vector3(16.5f,.2f,13.5f),white,true);Label(g,i==0?"GRAND MEDICAL PHARMACY":i==1?"FAMILY CAFÉ":"BLOOM / GIFTS & ESSENTIALS",new Vector3(0,4.6f,-6.7f),15,.8f,0,teal);
                var d=Group(g,"Shop automatic entrance",new Vector3(0,0,-6.5f));Door(d,2.1f);Box(g,"Retail counter",new Vector3(0,.6f,2.5f),new Vector3(9,1.2f,1.1f),wood,true);Computer(g,new Vector3(2,1.25f,2.5f));
                for(int s=-1;s<=1;s+=2){Box(g,"Display cabinet",new Vector3(s*6.5f,1.3f,2),new Vector3(1.3f,2.6f,6),white,true);for(int shelf=0;shelf<4;shelf++)for(int n=0;n<5;n++){Box(g,i==0?"Medicine packaging":"Retail stock",new Vector3(s*6.5f,.35f+shelf*.6f,-.3f+n),new Vector3(.8f,.4f,.5f),(n+shelf)%2==0?teal:cream);}}
                Person(g,0,7100+i,i==0,false,false,new Vector3(0,.14f,4),null);
            }
            Ground(p,"Pharmacy promenade",Rect.MinMaxRect(477,-512,532,-499.6f),.12f,tile);
        }
        static void Helicopter(Transform root)
        {
            var heli=Group(root,"Haven air ambulance helicopter",HospitalCampusLayout.Pad);var body=Group(heli,"Air ambulance fuselage",Vector3.zero);batches.Add(body);moving.Add(heli);
            Shape(body,"Rescue helicopter fuselage",new Vector3(0,1.8f,0),new Vector3(3.1f,2.8f,6.4f),white);Shape(body,"Panoramic cockpit",new Vector3(0,2.1f,2.1f),new Vector3(2.7f,2.1f,2.6f),ink);
            Box(body,"Rescue belly",new Vector3(0,1,-.2f),new Vector3(2.2f,.4f,4.2f),red);Box(body,"Twin engine cowling",new Vector3(0,3,-1),new Vector3(2.1f,.7f,2.5f),teal);
            Beam(body,"Tapered tail boom",new Vector3(0,2,-2.5f),new Vector3(0,2.8f,-8.4f),.55f,white);Box(body,"Tail vertical stabiliser",new Vector3(0,3.4f,-8.1f),new Vector3(.18f,2.2f,1.5f),red);Box(body,"Tail stabiliser",new Vector3(0,2.7f,-7.2f),new Vector3(3.7f,.12f,.6f),white);
            foreach(int s in new[]{-1,1}){
                Box(body,"Cabin rescue stripe",new Vector3(s*1.51f,1.6f,-.4f),new Vector3(.06f,.4f,3.3f),red);Box(body,"Rescue cabin side window",new Vector3(s*1.46f,2.3f,-.5f),new Vector3(.06f,.8f,2),ink);
                Beam(body,"Landing skid",new Vector3(s*1.5f,.1f,-2.8f),new Vector3(s*1.5f,.1f,2),.17f,metal);
                foreach(float z in new[]{-1.7f,1})Beam(body,"Skid strut",new Vector3(s*1.5f,.16f,z),new Vector3(s*.9f,1.25f,z),.12f,metal);
                Shape(body,"Engine exhaust",new Vector3(s*.65f,3,-2.3f),new Vector3(.35f,.35f,.6f),ink);
            }
            Beam(body,"Main rotor mast",new Vector3(0,3,0),new Vector3(0,3.9f,0),.18f,metal);
            var rotor=Group(heli,"Animated main rotor",new Vector3(0,3.95f,0));batches.Add(rotor);for(int i=0;i<4;i++){var blade=Group(rotor,"Blade "+i,Vector3.zero,i*90);Box(blade,"Rotor aerofoil",new Vector3(0,0,3.5f),new Vector3(.32f,.06f,7),ink);Box(blade,"Rotor yellow tip",new Vector3(0,0,6.8f),new Vector3(.33f,.07f,.35f),yellow);}
            var tail=Group(heli,"Animated tail rotor",new Vector3(.5f,3.4f,-8.1f));batches.Add(tail);for(int i=0;i<2;i++){var blade=Box(tail,"Tail rotor blade",Vector3.zero,new Vector3(.055f,2.2f,.16f),ink);blade.transform.localRotation=Quaternion.Euler(i*90,0,0);}
            var c=heli.gameObject.AddComponent<BoxCollider>();c.center=new Vector3(0,1.8f,0);c.size=new Vector3(3.2f,3.4f,6.8f);heli.gameObject.AddComponent<HospitalHelicopter>().Configure(heli,rotor,tail,HospitalCampusLayout.Pad);
        }
        static Mesh Save(Mesh mesh,string path)
        {var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}old.Clear();EditorUtility.CopySerialized(mesh,old);old.UploadMeshData(false);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(mesh);return old;}
        static void Combine(Transform root)
        {
            // Geometry is combined per floor/material, retaining every physical wall and stair collider.
            foreach(var batch in batches){var filters=batch.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null&&f.GetComponent<MeshRenderer>()!=null&&!moving.Any(m=>f.transform.IsChildOf(m)&&!batch.IsChildOf(m))).ToArray();
                foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial)){
                    var mesh=new Mesh{name="Hospital "+batch.name+" / "+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=batch.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();
                    var g=new GameObject("Batched "+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(batch,false);g.GetComponent<MeshFilter>().sharedMesh=Save(mesh,activeFolder+"/Batch"+(serial++)+".asset");g.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
                    foreach(var f in group){var o=f.gameObject;UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);if(o.transform.childCount==0&&o.GetComponents<Component>().Length==1)UnityEngine.Object.DestroyImmediate(o);}
                }
            }
            var saved=new Dictionary<Mesh,Mesh>();foreach(var f in root.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null&&!AssetDatabase.Contains(f.sharedMesh)){var original=f.sharedMesh;if(!saved.TryGetValue(original,out var m)){m=Save(UnityEngine.Object.Instantiate(original),activeFolder+"/Fixture"+(serial++)+".asset");saved.Add(original,m);}f.sharedMesh=m;}
        }
        static void RegisterLights(MotelGameRoot motel)
        {
            // The existing nearest-light pool is shared. Interior illumination has its own small fixed pool.
            var night=motel.GetComponentInChildren<WorldNightLighting>();if(night==null)return;var so=new SerializedObject(night);var f=so.FindProperty("fixtures");var s=so.FindProperty("glowingSurfaces");int a=f.arraySize,b=s.arraySize;
            // Bulbs were combined; register stable fixture transforms and the batch renderer containing their emission.
            var emissive=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.transform.IsChildOf(motel.transform.Find(HospitalCampusLayout.RootName))&&r.GetComponentInParent<HospitalPerson>()==null&&r.sharedMaterial==light&&!r.transform.parent.name.StartsWith("Hospital level ")).ToArray();
            var positions=new List<Vector3>();for(float z=-490;z<-35;z+=45)positions.Add(new Vector3(991,6.9f,z));for(float x=555;x<690;x+=24)positions.Add(new Vector3(x,4.9f,-478));for(float z=-450;z<-380;z+=24)positions.Add(new Vector3(513,3.9f,z));
            f.arraySize+=positions.Count;s.arraySize+=emissive.Length;for(int i=0;i<positions.Count;i++)f.GetArrayElementAtIndex(a+i).objectReferenceValue=Group(motel.transform.Find(HospitalCampusLayout.RootName),"Hospital night fixture",positions[i]);for(int i=0;i<emissive.Length;i++)s.GetArrayElementAtIndex(b+i).objectReferenceValue=emissive[i];so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
