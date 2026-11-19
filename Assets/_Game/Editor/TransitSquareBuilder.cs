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
    /// <summary>Three compact, usable frontage parcels beside the existing residential bus pickup.</summary>
    public static class TransitSquareBuilder
    {
        public const string Folder="Assets/_Game/Generated/TransitSquare";
        private static Material cream,ink,teal,coral,wood,stone,metal,glass,leaf,lawn,soil,gold,white,pink,water,ripple,warm;
        private static readonly List<Mesh> temporary=new List<Mesh>();
        private static readonly Dictionary<string,Mesh> cubes=new Dictionary<string,Mesh>();
        private static readonly List<Renderer> bulbs=new List<Renderer>();
        private static readonly List<Transform> moving=new List<Transform>(),fans=new List<Transform>(),ripples=new List<Transform>(),roofs=new List<Transform>();
        private static Transform hourHand,minuteHand;
        private static Mesh sphere,smallSphere;
        private static int meshIndex;
        [MenuItem("Roadside Haven/Design/Build Residential Transit Square")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            Directory.CreateDirectory("Backups/TransitSquare");if(File.Exists(motel.gameObject.scene.path))File.Copy(motel.gameObject.scene.path,"Backups/TransitSquare/Before-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity",true);
            Build(motel);EditorSceneManager.MarkSceneDirty(motel.gameObject.scene);EditorSceneManager.SaveScene(motel.gameObject.scene);AssetDatabase.SaveAssets();Focus();
        }
        [MenuItem("Roadside Haven/Design/Focus Residential Transit Square")]
        public static void Focus()
        {
            var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();var root=motel==null?null:motel.transform.Find(TransitSquareLayout.Layer);if(root==null)return;
            Selection.activeGameObject=root.gameObject;SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(-133,3,-45),new Vector3(178,18,60)),false);
        }
        public static Transform Build(MotelGameRoot motel)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Build the transit square in Edit Mode.");
            var old=motel.transform.Find(TransitSquareLayout.Layer);if(old!=null){foreach(var label in old.GetComponentsInChildren<TMP_Text>())FormatLabel(label);return old;}
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","TransitSquare");
            temporary.Clear();cubes.Clear();bulbs.Clear();moving.Clear();fans.Clear();ripples.Clear();roofs.Clear();meshIndex=0;Materials();
            var roads=motel.GetComponentsInChildren<MeshRenderer>().Where(r=>r.gameObject.activeInHierarchy&&TownPopulationController.RoadName(r.name)).Select(r=>Rect.MinMaxRect(r.bounds.min.x,r.bounds.min.z,r.bounds.max.x,r.bounds.max.z)).ToList();
            for(int i=0;i<3;i++)if(roads.Any(r=>r.Overlaps(TransitSquareLayout.Building(i))))throw new InvalidOperationException("A retail parcel overlaps a public road.");
            RelocateOakAndLantern(motel.transform);
            var root=Group(motel.transform,TransitSquareLayout.Layer,Vector3.zero);root.gameObject.AddComponent<MotelDistrictArt>();
            var retail=Group(root,"Haven Stop Market",Vector3.zero);var garden=Group(root,"Clock Garden",Vector3.zero);var court=Group(root,"Weekend Court",Vector3.zero);
            Ground(retail,"Market pedestrian paving",TransitSquareLayout.Retail,.10f,stone,roads);Ground(garden,"Clock garden paving",TransitSquareLayout.ClockGarden,.10f,stone,roads);Ground(court,"Weekend court paving",TransitSquareLayout.WeekendCourt,.10f,stone,roads);
            Ground(root,"Bus stop square connection",Rect.MinMaxRect(-104,-33.8f,-96,-29.9f),.10f,stone,roads);
            Ground(root,"Clock garden main walk connection",Rect.MinMaxRect(-140.5f,-36,-137.5f,-29),.10f,stone,roads);
            Ground(root,"Weekend court main walk connection",Rect.MinMaxRect(-200.5f,-36,-197.5f,-29),.10f,stone,roads);
            Ground(root,"Neighbourhood square promenade",Rect.MinMaxRect(-199,-36,-100,-33),.10f,stone,roads);
            for(float x=-115.5f;x<-110;x+=1)Box(root,"Square pedestrian crossing paint",new Vector3(x,.063f,-34.5f),new Vector3(.5f,.002f,3),white);
            for(int i=0;i<3;i++)Shop(retail,i);
            ClockGarden(garden);WeekendCourt(court);StreetDetails(retail);
            root.gameObject.AddComponent<TransitSquareLife>().Configure(hourHand,minuteHand,fans.ToArray(),ripples.ToArray(),roofs.ToArray());
            Combine(root);RegisterLights(motel);foreach(var label in root.GetComponentsInChildren<TMP_Text>())label.ForceMeshUpdate(true,true);
            foreach(var mesh in temporary)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);temporary.Clear();cubes.Clear();sphere=smallSphere=null;AssetDatabase.SaveAssets();return root;
        }
        private static void Materials()
        {
            cream=Mat("Transit warm plaster",new Color(.83f,.78f,.65f),"Stucco");ink=Mat("Transit graphite",new Color(.045f,.075f,.08f));teal=Mat("Transit glazed teal",new Color(.07f,.36f,.36f));coral=Mat("Transit burnt orange",new Color(.72f,.29f,.13f));wood=Mat("Transit oiled cedar",new Color(.47f,.29f,.14f),"CedarGrain");stone=Mat("Transit limestone",new Color(.74f,.73f,.66f),"Paving");metal=Mat("Transit brushed metal",new Color(.43f,.49f,.48f));metal.SetFloat("_Metallic",.65f);metal.SetFloat("_Smoothness",.6f);
            leaf=Mat("Transit foliage",new Color(.20f,.36f,.13f));lawn=Mat("Transit garden grass",new Color(.72f,.83f,.55f),"BaseGrass");soil=Mat("Transit planting earth",new Color(.51f,.32f,.17f),"DirtPebbles");gold=Mat("Transit brass",new Color(.83f,.60f,.21f));gold.SetFloat("_Metallic",.65f);white=Mat("Transit ivory",new Color(.95f,.91f,.78f));pink=Mat("Transit blossom rose",new Color(.79f,.27f,.43f));water=Mat("Transit fountain water",new Color(.12f,.47f,.50f));water.SetFloat("_Smoothness",.88f);water.SetFloat("_Metallic",.25f);warm=Mat("Transit lantern diffuser",new Color(1,.79f,.4f));
            glass=Transparent("Transit shop glass",new Color(.18f,.48f,.49f,.18f));glass.SetFloat("_Smoothness",.9f);ripple=Transparent("Transit fountain ripple",new Color(.8f,.96f,1,.5f));
        }
        private static Material Mat(string name,Color color,string texture=null)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",.2f);m.enableInstancing=true;
            if(texture!=null){bool district=texture=="Stucco"||texture=="CedarGrain";string dir=district?"Assets/_Game/Generated/MotelDistrict/":"Assets/_Game/Art/Surfaces/";m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(dir+texture+(district?".png":"_Albedo.png"));var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(dir+texture+(district?"Normal.png":"_Normal.png"));if(normal!=null){m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",.25f);m.EnableKeyword("_NORMALMAP");}}EditorUtility.SetDirty(m);return m;
        }
        private static Material Transparent(string name,Color color)
        {var m=Mat(name,color);m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;m.SetShaderPassEnabled("ShadowCaster",false);return m;}
        private static Transform Group(Transform parent,string name,Vector3 position,float yaw=0)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;t.localRotation=Quaternion.Euler(0,yaw,0);return t;}
        private static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material mat,bool solid=false)
        {var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=p;go.GetComponent<MeshFilter>().sharedMesh=Cube(size);go.GetComponent<MeshRenderer>().sharedMaterial=mat;if(solid)go.AddComponent<BoxCollider>().size=size;return go;}
        private static Mesh Cube(Vector3 size)
        {
            string key=size.ToString("F3");if(cubes.TryGetValue(key,out var mesh))return mesh;var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();var h=size*.5f;
            Action<Vector3,Vector3,Vector3,Vector3> face=(a,b,c,d)=>{int n=v.Count;v.AddRange(new[]{a,b,c,d});var normal=Vector3.Cross(b-a,d-a).normalized;foreach(var q in new[]{a,b,c,d})uv.Add(Mathf.Abs(normal.y)>.5f?new Vector2(q.x,q.z):Mathf.Abs(normal.x)>.5f?new Vector2(q.z,q.y):new Vector2(q.x,q.y));tri.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});};
            face(new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,-h.y,-h.z));face(new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z),new Vector3(-h.x,-h.y,h.z));face(new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,h.y,h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(-h.x,-h.y,-h.z));face(new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,-h.y,h.z));face(new Vector3(-h.x,h.y,-h.z),new Vector3(-h.x,h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,h.y,-h.z));face(new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,-h.y,h.z));
            mesh=new Mesh{name="Transit cuboid"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();temporary.Add(mesh);cubes[key]=mesh;return mesh;
        }
        private static void Ground(Transform parent,string name,Rect area,float height,Material mat,List<Rect> roads)
        {foreach(var r in StreetPavingLayout.ClearRoads(area,roads))Box(parent,name,new Vector3(r.center.x,(height-.025f)*.5f,r.center.y),new Vector3(r.width,height+.025f,r.height),mat,true);}
        private static void Beam(Transform root,string name,Vector3 a,Vector3 b,float width,Material mat,bool solid=false)
        {var go=Box(root,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),mat,solid);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        private static GameObject Ball(Transform parent,string name,Vector3 p,Vector3 scale,Material mat)
        {if(sphere==null){var temp=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere=temp.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(temp);}var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<MeshFilter>().sharedMesh=name.Contains("flower")||name.Contains("fruit")||name.Contains("bread")||name.Contains("Chess piece")?SmallSphere():sphere;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go;}
        private static Transform Label(Transform root,string title,Vector3 p,float width,float height,Color color)
        {var t=new GameObject(title).AddComponent<TextMeshPro>();t.transform.SetParent(root,false);t.transform.localPosition=p;t.font=TMP_Settings.GetFontAsset();t.text=title;t.fontSize=height*6;t.color=color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(width,height);t.raycastTarget=false;FormatLabel(t);return t.transform;}
        private static void FormatLabel(TMP_Text text)
        {text.enableAutoSizing=true;text.fontSizeMin=1.1f;text.fontSizeMax=Mathf.Max(1.2f,text.rectTransform.sizeDelta.y*6);text.overflowMode=TextOverflowModes.Ellipsis;text.ForceMeshUpdate(true,true);}
        private static void Shop(Transform parent,int index)
        {
            var g=Group(parent,TransitSquareLayout.ShopNames[index],TransitSquareLayout.Shops[index],180);float half=index==0?9:index==1?8:6;Material accent=index==0?coral:index==1?teal:gold;
            Box(g,"Shop walkable floor",Vector3.up*.045f,new Vector3(half*2,.15f,7),stone,true);
            Box(g,"Shop back wall",new Vector3(0,2.2f,3.45f),new Vector3(half*2,4.25f,.2f),cream,true);
            foreach(int side in new[]{-1,1})
            {
                Box(g,"Shop side wall",new Vector3(side*half,2.2f,0),new Vector3(.2f,4.25f,7),index==1?teal:cream,true);
                Box(g,"Shop front glazing",new Vector3(side*(half+1.55f)*.5f,1.62f,-3.5f),new Vector3(half-1.55f,3,.10f),glass,true);
                for(int n=0;n<3;n++)Box(g,"Shop glazing mullion",new Vector3(side*(2+n*(half-2)/2),1.65f,-3.6f),new Vector3(.065f,3.2f,.12f),metal);
                Box(g,"Doorway jamb",new Vector3(side*1.53f,1.5f,-3.6f),new Vector3(.10f,2.85f,.14f),ink,true);
            }
            Box(g,"Shop sign fascia",new Vector3(0,3.55f,-3.62f),new Vector3(half*2,1,.25f),ink);Label(g,TransitSquareLayout.ShopNames[index],new Vector3(0,3.58f,-3.77f),half*2-1,.7f,white.color);
            Box(g,"Shop accent reveal",new Vector3(0,3.02f,-3.78f),new Vector3(half*2,.07f,.06f),accent);
            var door=Group(g,"Automatic shop entrance",new Vector3(0,0,-3.55f));var left=Box(door,"Sliding glass left",new Vector3(-.73f,1.45f,0),new Vector3(1.4f,2.65f,.08f),glass,true).transform;var right=Box(door,"Sliding glass right",new Vector3(.73f,1.45f,0),new Vector3(1.4f,2.65f,.08f),glass,true).transform;
            foreach(var leaf in new[]{left,right})Box(leaf,"Door safety bar",new Vector3(0,.0f,-.065f),new Vector3(1.35f,.055f,.05f),metal);
            door.gameObject.AddComponent<DistrictSlidingDoor>().Configure(left,right,1.6f);moving.Add(door);
            var roof=Group(g,"Shop roof "+index,Vector3.zero);roofs.Add(roof);moving.Add(roof);
            // Closes the wall-to-roof gap from outside; it hides with the roof so the inside camera stays clear.
            Box(roof,"Shop ceiling slab",new Vector3(0,4.4f,0),new Vector3(half*2+.2f,.16f,7.2f),cream);Box(roof,"Shop front parapet",new Vector3(0,4.22f,-3.6f),new Vector3(half*2,.4f,.2f),ink);
            if(index==0)
            {
                var v=new List<Vector3>();var t=new List<int>();for(int step=0;step<=16;step++){float x=-half-1+(half*2+2)*step/16;float y=4.6f+.8f*Mathf.Sin(step*Mathf.PI/16);v.Add(new Vector3(x,y,-4.7f));v.Add(new Vector3(x,y,4.2f));if(step>0){int n=step*2;t.AddRange(new[]{n-2,n-1,n,n,n-1,n+1,n,n-1,n-2,n+1,n-1,n});}}MeshPart(roof,"Arched cafe canopy",Vector3.zero,v.ToArray(),t.ToArray(),coral);
                for(int side=-1;side<=1;side+=2)Beam(g,"Canopy tension stay",new Vector3(side*(half-.3f),3.9f,-3.4f),new Vector3(side*(half+.8f),4.65f,-4.65f),.06f,metal);
            }
            else if(index==1){Box(roof,"Market floating roof",new Vector3(0,4.48f,-.1f),new Vector3(half*2+1.5f,.22f,9),teal);for(int n=0;n<8;n++)Box(roof,"Market roof screen",new Vector3(-half+1+n*2,4.85f,2.9f),new Vector3(.13f,.6f,2),wood);}
            else{var panel=Box(roof,"Cycle shop sloped roof",new Vector3(0,4.6f,-.2f),new Vector3(half*2+1.6f,.20f,8.5f),gold);panel.transform.localRotation=Quaternion.Euler(0,0,-5);}
            Box(g,"Shop interior counter",new Vector3(0,.67f,1.55f),new Vector3(half*1.2f,1.1f,.9f),index==0?wood:teal,true);Box(g,"Counter stone top",new Vector3(0,1.27f,1.55f),new Vector3(half*1.2f+.2f,.10f,1.1f),stone);
            var detail=Group(g,"Close-up props",Vector3.zero);
            if(index==0){CoffeeMachine(detail,new Vector3(-2,1.33f,1.55f));Table(g,new Vector3(-5,0,-.8f),true);Table(g,new Vector3(5,0,-.8f),true);MenuBoard(g,new Vector3(6.3f,2.5f,3.29f));Fan(g,new Vector3(0,3.8f,0));}
            else if(index==1){Shelf(g,new Vector3(-5.8f,0,1.0f),detail);Shelf(g,new Vector3(5.8f,0,1),detail);Box(detail,"Checkout screen",new Vector3(1.8f,1.61f,1.5f),new Vector3(.5f,.5f,.10f),ink);for(int n=0;n<4;n++)Box(detail,"Paper shopping bag",new Vector3(-2+n,1.55f,1.7f),new Vector3(.3f,.5f,.25f),wood);Fan(g,new Vector3(0,3.8f,0));}
            else{Bicycle(g,new Vector3(-3,0,0),teal);Bicycle(g,new Vector3(3,0,0),coral);Box(g,"Cycle workbench",new Vector3(-3,.65f,2.5f),new Vector3(3,1,.6f),wood,true);for(int n=0;n<5;n++)Box(detail,"Workshop hanging tool",new Vector3(-4+n*.45f,2.1f,3.26f),new Vector3(.07f,.6f,.07f),metal);}
            for(int side=-1;side<=1;side+=2)Light(g,new Vector3(side*(half-.6f),2.75f,-3.8f),false);
        }
        private static void CoffeeMachine(Transform root,Vector3 p)
        {
            Box(root,"Espresso machine body",p+Vector3.up*.26f,new Vector3(1.15f,.52f,.52f),metal);Box(root,"Coffee machine black fascia",p+new Vector3(0,.31f,-.28f),new Vector3(1,.3f,.05f),ink);
            for(int side=-1;side<=1;side+=2){Beam(root,"Espresso tap",p+new Vector3(side*.3f,.24f,-.3f),p+new Vector3(side*.3f,.09f,-.3f),.04f,metal);Box(root,"Coffee cup",p+new Vector3(side*.3f,.08f,-.35f),new Vector3(.14f,.16f,.14f),white);}
            for(int n=0;n<4;n++)Box(root,"Stacked cafe cup",p+new Vector3(.8f,.08f+n*.12f,0),new Vector3(.16f,.13f,.16f),white);
            Box(root,"Coffee grinder",p+new Vector3(-.85f,.24f,.04f),new Vector3(.35f,.45f,.3f),ink);Ball(root,"Grinder bean hopper",p+new Vector3(-.85f,.59f,.04f),new Vector3(.32f,.4f,.32f),wood);
        }
        private static void MenuBoard(Transform root,Vector3 p)
        {Box(root,"Cafe menu board",p,new Vector3(2.8f,1.6f,.09f),ink);Label(root,"COFFEE  /  BAKERY\nESPRESSO    COLD BREW\nFRESH EVERY MORNING",p+Vector3.back*.055f,2.5f,1.35f,white.color);}
        private static void Shelf(Transform root,Vector3 p,Transform props)
        {
            Box(root,"Market shelving back",p+Vector3.up*1.25f,new Vector3(2.4f,2.3f,.2f),wood,true);
            for(int row=0;row<4;row++)
            {
                float y=.4f+row*.55f;Box(root,"Market shelf board",p+new Vector3(0,y,-.25f),new Vector3(2.5f,.08f,.6f),cream);
                for(int n=0;n<6;n++)Box(props,"Market packaged goods",p+new Vector3(-1+n*.4f,y+.19f,-.25f),new Vector3(.24f,.3f,.24f),(n+row)%3==0?coral:(n+row)%3==1?teal:gold);
            }
        }
        private static void Fan(Transform root,Vector3 p)
        {
            Beam(root,"Ceiling fan stem",p+Vector3.up*.35f,p,.05f,metal);var pivot=Group(root,"Rotating ceiling fan",p);fans.Add(pivot);moving.Add(pivot);Ball(pivot,"Fan hub",Vector3.zero,new Vector3(.25f,.16f,.25f),ink);
            for(int n=0;n<3;n++){var blade=Box(pivot,"Fan blade",new Vector3(.55f,0,0),new Vector3(.95f,.035f,.18f),wood);blade.transform.RotateAround(pivot.position,Vector3.up,n*120);}
        }
        private static void Bench(Transform root,Vector3 p,float yaw=0,float width=2.6f)
        {
            var g=Group(root,"Timber public bench",p,yaw);for(int n=0;n<4;n++)Box(g,"Bench seat slat",new Vector3(0,.56f,-.24f+n*.16f),new Vector3(width,.08f,.12f),wood,n==1);
            for(int n=0;n<3;n++)Box(g,"Bench back slat",new Vector3(0,.83f+n*.16f,.37f),new Vector3(width,.12f,.08f),wood,true);
            for(int side=-1;side<=1;side+=2){Box(g,"Bench iron leg",new Vector3(side*width*.36f,.29f,0),new Vector3(.10f,.58f,.54f),ink,true);Box(g,"Bench armrest",new Vector3(side*width*.45f,.8f,0),new Vector3(.06f,.07f,.65f),metal);}
        }
        private static void Table(Transform root,Vector3 p,bool chairs)
        {
            var g=Group(root,"Cafe seating table",p);Box(g,"Cafe table foot",new Vector3(0,.18f,0),new Vector3(.65f,.13f,.65f),ink);Box(g,"Cafe table pedestal",new Vector3(0,.57f,0),new Vector3(.10f,.8f,.10f),metal,true);Box(g,"Cafe table top",new Vector3(0,.98f,0),new Vector3(1.25f,.09f,1.25f),wood,true);
            if(chairs)for(int side=-1;side<=1;side+=2){Box(g,"Cafe chair seat",new Vector3(side*1.0f,.54f,0),new Vector3(.5f,.1f,.55f),teal,true);Box(g,"Cafe chair back",new Vector3(side*1.24f,.83f,0),new Vector3(.09f,.65f,.55f),teal,true);for(int end=-1;end<=1;end+=2)Box(g,"Cafe chair legs",new Vector3(side*1f,.29f,end*.21f),new Vector3(.055f,.5f,.055f),metal);}
        }
        private static void Planter(Transform root,Vector3 p,Vector2 size,bool flowers=false)
        {
            Box(root,"Stone planted border",p+Vector3.up*.28f,new Vector3(size.x,.38f,size.y),cream,true);Box(root,"Planter rich soil",p+Vector3.up*.48f,new Vector3(size.x-.16f,.025f,size.y-.16f),soil);
            int count=Mathf.Max(2,Mathf.FloorToInt(size.x/.7f));for(int n=0;n<count;n++)Ball(root,"Trimmed plaza shrub",p+new Vector3(-size.x*.42f+size.x*.84f*n/Mathf.Max(1,count-1),.72f,0),new Vector3(.7f,.68f,size.y*.9f),leaf);
            if(flowers){var detail=Group(root,"Close-up props",p);for(int n=0;n<count*3;n++)Ball(detail,"Plaza flower",new Vector3(-size.x*.42f+(n%(count*2))*.3f,.93f,(n%3-1)*.16f),new Vector3(.18f,.18f,.18f),n%3==0?pink:n%3==1?white:gold);}
        }
        private static void Light(Transform root,Vector3 p,bool bollard)
        {
            if(bollard){Box(root,"Square light bollard",p+Vector3.up*.47f,new Vector3(.13f,.75f,.13f),ink,true);p.y+=.88f;}
            var r=Box(root,"Transit warm lamp glass",p,new Vector3(.23f,.25f,.23f),warm).GetComponent<Renderer>();bulbs.Add(r);Box(root,"Transit lamp top",p+Vector3.up*.15f,new Vector3(.3f,.06f,.3f),ink);
        }
        private static void StreetDetails(Transform root)
        {
            Planter(root,new Vector3(-86,0,-40.8f),new Vector2(1.6f,5),true);Planter(root,new Vector3(-67,0,-40.8f),new Vector2(1.6f,5),false);
            Bench(root,new Vector3(-57,0,-32),180,3.3f);Box(root,"Cycle parking rack foot",new Vector3(-51.5f,.14f,-39.5f),new Vector3(1.6f,.1f,10),stone,true);
            for(int n=0;n<4;n++){float z=-43+n*2.2f;Beam(root,"Bicycle rack upright",new Vector3(-52.1f,.2f,z),new Vector3(-52.1f,1.15f,z),.055f,metal,true);Beam(root,"Bicycle rack upper rail",new Vector3(-52.1f,1.15f,z),new Vector3(-50.8f,1.15f,z),.055f,metal);Beam(root,"Bicycle rack upright",new Vector3(-50.8f,1.15f,z),new Vector3(-50.8f,.2f,z),.055f,metal);}
            Bicycle(root,new Vector3(-51.5f,0,-36.5f),coral);Bicycle(root,new Vector3(-51.5f,0,-42.5f),teal);
            Box(root,"Market litter bin",new Vector3(-70,.6f,-31.3f),new Vector3(.6f,1,.6f),ink,true);Box(root,"Bin metal lid",new Vector3(-70,1.12f,-31.3f),new Vector3(.65f,.08f,.65f),metal);
            for(int n=0;n<3;n++)Box(root,"Accessible tactile paving",new Vector3(-99+n*.36f,.11f,-31.4f),new Vector3(.1f,.015f,1),gold);
            Vendor(root,new Vector3(-98,.12f,-44.7f),"transit-barista","Cafe barista");
        }
        private static void Bicycle(Transform root,Vector3 p,Material color,float yaw=0)
        {
            var g=Group(root,"Parked city bicycle",p,yaw);
            for(int side=-1;side<=1;side+=2){var wheel=Ring(g,"Bicycle tyre",new Vector3(0,.6f,side*.84f),.48f,.40f,ink,true);wheel.localRotation=Quaternion.Euler(0,0,90);Ring(g,"Bicycle wheel rim",new Vector3(0,.6f,side*.84f),.408f,.39f,metal,true).localRotation=Quaternion.Euler(0,0,90);}
            var a=new Vector3(.04f,.6f,-.84f);var b=new Vector3(.04f,1.12f,-.32f);var c=new Vector3(.04f,.6f,.15f);var d=new Vector3(.04f,1.2f,.58f);var e=new Vector3(.04f,.6f,.84f);
            foreach(var pair in new[]{new[]{a,b},new[]{a,c},new[]{b,c},new[]{b,d},new[]{c,d},new[]{d,e}})Beam(g,"Bicycle painted frame",pair[0],pair[1],.045f,color);
            Beam(g,"Bicycle seat post",b,b+Vector3.up*.17f,.035f,metal);Box(g,"Bicycle saddle",b+Vector3.up*.19f,new Vector3(.22f,.07f,.31f),ink);Beam(g,"Bicycle handlebar stem",d,d+Vector3.up*.23f,.035f,metal);Beam(g,"Bicycle handlebars",d+new Vector3(-.27f,.23f,0),d+new Vector3(.27f,.23f,0),.035f,ink);
            var collision=Group(g,"Bicycle parking collision",Vector3.zero).gameObject.AddComponent<BoxCollider>();collision.center=new Vector3(0,.65f,0);collision.size=new Vector3(.3f,1.05f,2.1f);
            var detail=Group(g,"Close-up props",Vector3.zero);foreach(int side in new[]{-1,1})for(int n=0;n<8;n++){float spokeAngle=n*Mathf.PI/4;Beam(detail,"Bicycle wheel spoke",new Vector3(0,.6f,side*.84f),new Vector3(0,.6f+Mathf.Sin(spokeAngle)*.39f,side*.84f+Mathf.Cos(spokeAngle)*.39f),.008f,metal);}
        }
        private static void ClockGarden(Transform root)
        {
            foreach(var p in new[]{new Vector3(-172,0,-40),new Vector3(-127,0,-40),new Vector3(-172,0,-71),new Vector3(-127,0,-71)})Planter(root,p,new Vector2(4.0f,2.0f),true);
            foreach(var p in new[]{new Vector3(-163,0,-56),new Vector3(-153,0,-66),new Vector3(-135,0,-58)})Bench(root,p,p.x< -150?90:-90,3.2f);
            for(int n=0;n<3;n++){Box(root,"Garden inset lawn",new Vector3(-163+n*12,.12f,-42),new Vector3(8,.02f,4),lawn);Box(root,"Garden limestone inset edge",new Vector3(-163+n*12,.12f,-44.2f),new Vector3(8,.03f,.15f),cream);}
            Fountain(root,new Vector3(-154,0,-49));Clock(root,new Vector3(-143,0,-59));
            var shade=Group(root,"Garden reading canopy",new Vector3(-155,0,-64));for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)Box(shade,"Reading canopy post",new Vector3(side*3.7f,1.9f,end*2.1f),new Vector3(.11f,3.6f,.11f),ink,true);
            MeshPart(shade,"Folded reading shade",Vector3.zero,new[]{new Vector3(-4,3.6f,-2.6f),new Vector3(4,4.3f,-2.6f),new Vector3(4,3.6f,2.6f),new Vector3(-4,4.3f,2.6f),new Vector3(0,4.0f,0)},new[]{0,4,1,1,4,2,2,4,3,3,4,0,1,4,0,2,4,1,3,4,2,0,4,3},teal);
            ChessTable(root,new Vector3(-159,0,-63));ChessTable(root,new Vector3(-151,0,-63));NoticeBoard(root,new Vector3(-137,0,-47),"CLOCK GARDEN\nA LITTLE TIME OUT");
            foreach(var p in new[]{new Vector3(-174,0,-33),new Vector3(-124.5f,0,-33),new Vector3(-174,0,-72),new Vector3(-124.5f,0,-72),new Vector3(-146,0,-46)})Light(root,p,true);
            foreach(var p in new[]{new Vector3(-170,0,-50),new Vector3(-170,0,-62),new Vector3(-128,0,-46),new Vector3(-128,0,-64),new Vector3(-160,0,-72),new Vector3(-142,0,-72)})ShadeTree(root,p);
        }
        private static void ShadeTree(Transform root,Vector3 p)
        {
            var g=Group(root,"Clock garden shade tree",p);Box(g,"Shade tree trunk",Vector3.up*1.6f,new Vector3(.38f,3.2f,.38f),wood,true);Box(g,"Shade tree stone surround",Vector3.up*.16f,new Vector3(1.6f,.12f,1.6f),cream);
            Ball(g,"Shade tree crown",Vector3.up*3.9f,new Vector3(3.4f,2.6f,3.4f),leaf);Ball(g,"Shade tree crown",new Vector3(.8f,3.4f,.4f),new Vector3(2.2f,1.8f,2.2f),leaf);Ball(g,"Shade tree crown",new Vector3(-.7f,3.5f,-.5f),new Vector3(2.3f,1.9f,2.3f),leaf);
        }
        private static void Clock(Transform root,Vector3 p)
        {
            var g=Group(root,"Haven garden town clock",p);Box(g,"Clock stone pedestal",Vector3.up*.26f,new Vector3(2.7f,.35f,2.5f),stone,true);Box(g,"Clock tapered tower",Vector3.up*2.75f,new Vector3(1.35f,5.2f,1.3f),cream,true);
            for(int side=-1;side<=1;side+=2)Box(g,"Clock bronze vertical fin",new Vector3(side*.73f,2.8f,0),new Vector3(.12f,4.8f,1.5f),gold);
            var face=Group(g,"Clock north dial",new Vector3(0,5.75f,.85f));face.localRotation=Quaternion.Euler(90,0,0);Ring(face,"Clock outer bezel",Vector3.zero,1.18f,1.03f,gold,false);Disc(face,"Clock ivory face",Vector3.zero,1.04f,white);
            // Hands use a world-facing XY plane; the ring helper lies in XZ.
            var dial=Group(g,"Clock hands plane",new Vector3(0,5.75f,.88f));
            for(int n=0;n<12;n++){float a=n*Mathf.PI/6;var tick=Box(dial,"Clock hour index",new Vector3(Mathf.Sin(a)*.87f,Mathf.Cos(a)*.87f,0),new Vector3(.05f,.16f,.03f),ink);tick.transform.localRotation=Quaternion.Euler(0,0,-n*30);}
            hourHand=Group(dial,"Town clock hour hand",Vector3.zero);minuteHand=Group(dial,"Town clock minute hand",Vector3.forward*.03f);moving.Add(hourHand);moving.Add(minuteHand);
            Box(hourHand,"Clock hour needle",new Vector3(0,.28f,0),new Vector3(.07f,.62f,.04f),ink);Box(minuteHand,"Clock minute needle",new Vector3(0,.39f,0),new Vector3(.045f,.84f,.04f),coral);Ball(dial,"Clock brass centre",new Vector3(0,0,.08f),Vector3.one*.18f,gold);hourHand.localRotation=Quaternion.Euler(0,0,-90);
            Box(g,"Clock cap",new Vector3(0,7.05f,0),new Vector3(2.2f,.15f,1.7f),ink);
        }
        private static void Fountain(Transform root,Vector3 p)
        {
            var g=Group(root,"Clock garden fountain",p);Ring(g,"Fountain stone basin",Vector3.up*.35f,2.35f,1.8f,stone,false);Disc(g,"Fountain water surface",Vector3.up*.36f,1.82f,water);
            var collision=new GameObject("Fountain basin collision").transform;collision.SetParent(g,false);var box=collision.gameObject.AddComponent<BoxCollider>();box.center=Vector3.up*.3f;box.size=new Vector3(4.7f,.5f,4.7f);
            Box(g,"Fountain central plinth",Vector3.up*.59f,new Vector3(.5f,.95f,.5f),gold,true);Ball(g,"Fountain crown",Vector3.up*1.12f,Vector3.one*.38f,metal);
            for(int n=0;n<4;n++){float a=n*Mathf.PI*.5f;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var points=new Vector3[13];for(int step=0;step<points.Length;step++){float t=step/12f;points[step]=dir*t*1.5f+Vector3.up*(1.1f+.6f*Mathf.Sin(t*Mathf.PI)-t*.73f);}Tube(g,"Fountain arcing stream",points,.025f,ripple);}
            for(int n=0;n<2;n++){var ring=Ring(g,"Animated fountain ripple",new Vector3(0,.375f+n*.002f,0),1.38f,1.35f,ripple,false);ripples.Add(ring);moving.Add(ring);}
        }
        private static void ChessTable(Transform root,Vector3 p)
        {
            Table(root,p,false);var detail=Group(root,"Close-up props",p);
            for(int x=0;x<8;x++)for(int z=0;z<8;z++)Box(detail,"Stone chess square",new Vector3(-.49f+x*.14f,1.04f,-.49f+z*.14f),new Vector3(.138f,.009f,.138f),(x+z)%2==0?white:ink);
            foreach(int side in new[]{-1,1}){Box(root,"Chess stool base",p+new Vector3(side*1.05f,.37f,0),new Vector3(.45f,.55f,.45f),stone,true);Box(root,"Chess stool top",p+new Vector3(side*1.05f,.68f,0),new Vector3(.55f,.09f,.55f),wood);}
            for(int n=0;n<5;n++){Ball(detail,"Chess piece",new Vector3(-.42f+n*.14f,1.12f,n%2==0?-.35f:.35f),new Vector3(.07f,.15f,.07f),n%2==0?ink:white);}
        }
        private static void WeekendCourt(Transform root)
        {
            var court=Group(root,"Weekend market stalls",Vector3.zero);
            for(int n=0;n<2;n++)
            {
                var g=Group(court,n==0?"Oak and Oven bakery stall":"Neighbourhood produce stall",new Vector3(-204+n*16,0,-48),180);Material accent=n==0?coral:teal;
                foreach(int side in new[]{-1,1})foreach(int end in new[]{-1,1})Box(g,"Stall cedar post",new Vector3(side*3.7f,1.65f,end*2.3f),new Vector3(.15f,3.1f,.15f),wood,true);
                Box(g,"Stall lower back",new Vector3(0,.9f,2.2f),new Vector3(7.5f,1.6f,.15f),cream,true);Box(g,"Stall counter",new Vector3(0,.71f,-1.7f),new Vector3(7.4f,1.2f,1),wood,true);Box(g,"Stall counter stone",new Vector3(0,1.36f,-1.7f),new Vector3(7.6f,.10f,1.15f),stone);
                var canopy=Box(g,"Folded stall roof",new Vector3(0,3.45f,0),new Vector3(8.6f,.18f,6.2f),accent);canopy.transform.localRotation=Quaternion.Euler(-5,0,0);Box(g,"Stall sign board",new Vector3(0,2.73f,-2.56f),new Vector3(7.8f,.6f,.14f),ink);Label(g,n==0?"OAK & OVEN":"FARMER'S TABLE",new Vector3(0,2.75f,-2.65f),7.5f,.45f,white.color);
                var detail=Group(g,"Close-up props",Vector3.zero);for(int crate=0;crate<4;crate++){float x=-2.7f+crate*1.8f;Box(g,"Timber produce crate",new Vector3(x,1.49f,-1.7f),new Vector3(1.5f,.19f,.85f),wood);for(int item=0;item<7;item++)Ball(detail,n==0?"Fresh baked bread":"Market produce fruit",new Vector3(x-.5f+(item%4)*.3f,1.69f,-1.9f+(item/4)*.35f),n==0?new Vector3(.27f,.18f,.18f):Vector3.one*.21f,n==0?gold:crate%2==0?coral:leaf);}
                Light(g,new Vector3(-3,2.5f,-2.5f),false);Light(g,new Vector3(3,2.5f,-2.5f),false);Vendor(g,new Vector3(0,.12f,.5f),"transit-vendor-"+n,n==0?"Neighbourhood baker":"Produce seller",180);
            }
            Bench(root,new Vector3(-209,0,-56),180,3.2f);Bench(root,new Vector3(-187,0,-56),180,3.2f);Planter(root,new Vector3(-214,0,-39),new Vector2(2,8),true);Planter(root,new Vector3(-183,0,-39),new Vector2(2,8),true);
            NoticeBoard(root,new Vector3(-197,0,-58),"WEEKEND COURT\nBAKERY  /  FRESH PRODUCE");foreach(float x in new[]{-214f,-183f})Light(root,new Vector3(x,0,-60),true);
        }
        private static void NoticeBoard(Transform root,Vector3 p,string message)
        {var g=Group(root,"Community framed notice board",p,180);foreach(int side in new[]{-1,1})Box(g,"Notice board post",new Vector3(side*1.6f,1.2f,0),new Vector3(.10f,2.2f,.10f),ink,true);Box(g,"Community notice board",new Vector3(0,1.9f,0),new Vector3(3.7f,1.25f,.16f),wood,true);Label(g,message,new Vector3(0,1.9f,-.10f),3.4f,1,white.color);}
        private static void Vendor(Transform root,Vector3 p,string id,string displayName,float yaw=0)
        {var go=Group(root,displayName,p,yaw).gameObject;var body=go.AddComponent<CapsuleCollider>();body.height=1.8f;body.radius=.23f;body.center=Vector3.up*.9f;go.AddComponent<WorldNpc>().Configure(id,displayName);}
        private static Mesh SmallSphere()
        {
            if(smallSphere!=null)return smallSphere;float t=(1+Mathf.Sqrt(5))*.5f;var v=new[]{new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)}.Select(p=>p.normalized*.5f).ToArray();
            smallSphere=new Mesh{name="Low polygon plaza details",vertices=v,normals=v.Select(p=>p.normalized).ToArray(),triangles=new[]{0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1}};smallSphere.RecalculateBounds();temporary.Add(smallSphere);return smallSphere;
        }
        private static Transform MeshPart(Transform root,string name,Vector3 p,Vector3[] vertices,int[] indices,Material mat)
        {
            var seen=new HashSet<string>();var triangles=new List<int>();bool doubleSided=false;
            for(int n=0;n<indices.Length;n+=3){var sorted=new[]{indices[n],indices[n+1],indices[n+2]};Array.Sort(sorted);string key=sorted[0]+"/"+sorted[1]+"/"+sorted[2];if(!seen.Add(key)){doubleSided=true;continue;}triangles.AddRange(new[]{indices[n],indices[n+1],indices[n+2]});}
            if(doubleSided){mat.SetFloat("_Cull",0);EditorUtility.SetDirty(mat);}
            var mesh=new Mesh{name=name,vertices=vertices,triangles=triangles.ToArray(),uv=vertices.Select(v=>new Vector2(v.x,v.z)).ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();temporary.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.transform.localPosition=p;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go.transform;
        }
        private static Transform Disc(Transform root,string name,Vector3 p,float radius,Material mat)
        {
            const int steps=32;var v=new List<Vector3>{Vector3.zero};var t=new List<int>();for(int n=0;n<=steps;n++){float a=n*Mathf.PI*2/steps;v.Add(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));if(n>0)t.AddRange(new[]{0,n+1,n});}return MeshPart(root,name,p,v.ToArray(),t.ToArray(),mat);
        }
        private static Transform Ring(Transform root,string name,Vector3 p,float outer,float inner,Material mat,bool rounded)
        {
            const int segments=32,profile=8;var v=new List<Vector3>();var t=new List<int>();float radius=(outer+inner)*.5f,width=(outer-inner)*.5f;float height=rounded ? width : mat==ripple ? .001f : mat==stone ? .25f : .045f;
            for(int n=0;n<=segments;n++)for(int k=0;k<=profile;k++){float a=n*Mathf.PI*2/segments,b=k*Mathf.PI*2/profile;float r=radius+Mathf.Cos(b)*width;v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(b)*height,Mathf.Sin(a)*r));if(n>0&&k>0){int d=n*(profile+1)+k,c=d-1,before=d-profile-1,previous=before-1;t.AddRange(new[]{previous,before,c,c,before,d});}}
            return MeshPart(root,name,p,v.ToArray(),t.ToArray(),mat);
        }
        private static void Tube(Transform root,string name,Vector3[] points,float radius,Material mat)
        {
            const int sides=6;var v=new List<Vector3>();var t=new List<int>();
            for(int n=0;n<points.Length;n++)
            {
                var direction=(points[Mathf.Min(n+1,points.Length-1)]-points[Mathf.Max(0,n-1)]).normalized;var right=Vector3.Cross(direction,Vector3.forward).normalized;if(right.sqrMagnitude<.01f)right=Vector3.right;var up=Vector3.Cross(right,direction).normalized;
                for(int side=0;side<=sides;side++){float a=side*Mathf.PI*2/sides;v.Add(points[n]+(Mathf.Cos(a)*right+Mathf.Sin(a)*up)*radius);if(n>0&&side>0){int d=n*(sides+1)+side,c=d-1,b=d-sides-1,e=b-1;t.AddRange(new[]{e,c,b,c,d,b});}}
            }
            MeshPart(root,name,Vector3.zero,v.ToArray(),t.ToArray(),mat);
        }
        private static void RelocateOakAndLantern(Transform motel)
        {
            var renderers=motel.GetComponentsInChildren<MeshRenderer>(true);
            foreach(var trunk in renderers)
            {
                if(!trunk.gameObject.activeInHierarchy||trunk.name!="Tree trunk"||Mathf.Abs(trunk.bounds.center.x+92)>.1f||Mathf.Abs(trunk.bounds.center.z+43.4f)>.1f)continue;
                var tree=trunk.GetComponentInParent<BiomePlant>();var group=tree!=null?tree.transform:trunk.transform.parent;
                if(group==null||!group.name.StartsWith("Avenue "))throw new InvalidOperationException("Cannot safely relocate the retail parcel's existing oak.");
                group.position+=new Vector3(-78,0,-21.6f);break;
            }
            foreach(var part in renderers)
            {
                if(!part.gameObject.activeInHierarchy||(!part.name.Contains("bollard")&&!part.name.Contains("Bollard")))continue;
                var p=part.bounds.center;if(Mathf.Abs(p.x+91)<.1f&&Mathf.Abs(p.z+42.4f)<.1f)part.transform.position+=new Vector3(-78,0,-21.6f);
            }
        }
        private static bool Moving(Transform t)=>moving.Any(m=>t.IsChildOf(m));
        private static bool Near(Transform t,Transform root){for(var p=t;p!=null&&p!=root;p=p.parent)if(p.name=="Close-up props")return true;return false;}
        private static Transform Parcel(Transform t,Transform root){while(t.parent!=null&&t.parent!=root)t=t.parent;return t.parent==root?t:root;}
        private static Mesh Save(Mesh mesh,string path)
        {
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}old.Clear();EditorUtility.CopySerialized(mesh,old);old.UploadMeshData(false);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(mesh);return old;
        }
        private static void Combine(Transform root)
        {
            var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null&&f.GetComponent<MeshRenderer>()!=null&&!Moving(f.transform)&&!bulbs.Contains(f.GetComponent<Renderer>())).ToArray();
            foreach(var group in filters.GroupBy(f=>new{Material=f.GetComponent<MeshRenderer>().sharedMaterial,Parent=Parcel(f.transform,root),Detail=Near(f.transform,root)}))
            {
                var mesh=new Mesh{name="Transit "+group.Key.Material.name,indexFormat=IndexFormat.UInt32};var parent=group.Key.Parent;
                mesh.CombineMeshes(group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=parent.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();var saved=Save(mesh,Folder+"/Parcel"+(meshIndex++)+".asset");
                var go=new GameObject((group.Key.Detail?"Transit near batch ":"Transit static batch ")+group.Key.Material.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=saved;go.GetComponent<MeshRenderer>().sharedMaterial=group.Key.Material;
                foreach(var f in group){var original=f.gameObject;UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);if(original.transform.childCount==0&&original.GetComponents<Component>().Length==1)UnityEngine.Object.DestroyImmediate(original);}
            }
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null&&!AssetDatabase.Contains(f.sharedMesh))f.sharedMesh=Save(UnityEngine.Object.Instantiate(f.sharedMesh),Folder+"/Fixture"+(meshIndex++)+".asset");
        }
        private static void RegisterLights(MotelGameRoot motel)
        {
            var lighting=motel.GetComponentInChildren<WorldNightLighting>();if(lighting==null)return;var so=new SerializedObject(lighting);var fixtures=so.FindProperty("fixtures");var surfaces=so.FindProperty("glowingSurfaces");int a=fixtures.arraySize,b=surfaces.arraySize;fixtures.arraySize+=bulbs.Count;surfaces.arraySize+=bulbs.Count;
            for(int n=0;n<bulbs.Count;n++){fixtures.GetArrayElementAtIndex(a+n).objectReferenceValue=bulbs[n].transform;surfaces.GetArrayElementAtIndex(b+n).objectReferenceValue=bulbs[n];}so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
