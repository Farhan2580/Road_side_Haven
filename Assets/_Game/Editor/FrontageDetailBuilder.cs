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
    /// <summary>Additive human-scale finishing for the completed Axis/Luma frontage. All driving and doorway corridors are reserved.</summary>
    public static class FrontageDetailBuilder
    {
        public const string Layer="Axis Luma Detail Pass v2";
        public const string Folder="Assets/_Game/Generated/FrontageDetails";
        private static Material pearl,carbon,steel,copper,wood,glass,leaf,led,rubber,leather;
        [MenuItem("Roadside Haven/Design/Add Frontage Details and Road Connections")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            string path=motel.gameObject.scene.path;if(File.Exists(path)){string backup="Backups/FrontageDetails/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/"+Path.GetFileName(path));}
            Build(motel);WorldRoadConnectionsBuilder.Build(motel);EditorSceneManager.MarkSceneDirty(motel.gameObject.scene);EditorSceneManager.SaveScene(motel.gameObject.scene);AssetDatabase.SaveAssets();
        }
        [MenuItem("Roadside Haven/Design/Focus Detailed Axis and Luma")]
        public static void Focus()
        {var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;Selection.activeGameObject=motel.transform.Find(Layer)?.gameObject;if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(298,3,-12),new Vector3(100,22,120)),false);}
        public static Transform Build(MotelGameRoot motel)
        {
            var existing=motel.transform.Find(Layer);if(existing!=null)return existing;MainRoadFrontageBuilder.Build(motel);
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","FrontageDetails");
            pearl=Load("Frontage pearl");carbon=Load("Axis graphite");steel=Load("Service brushed metal");copper=Load("Luma champagne");glass=Load("Frontage clear glass");wood=AssetDatabase.LoadAssetAtPath<Material>(MotelDistrictArtBuilder.Folder+"/Oiled cedar.mat");leaf=AssetDatabase.LoadAssetAtPath<Material>(MotelDistrictArtBuilder.Folder+"/Palm foliage.mat");
            led=Mat("Detail warm LED",new Color(1,.77f,.42f),.4f);led.EnableKeyword("_EMISSION");led.SetColor("_EmissionColor",led.color*1.1f);rubber=Mat("Workshop rubber",new Color(.025f,.03f,.035f),.15f);leather=Mat("Lounge upholstery",new Color(.16f,.23f,.25f),.3f);
            var root=new GameObject(Layer).transform;root.SetParent(motel.transform,false);root.gameObject.AddComponent<MotelDistrictArt>();
            Gallery(root);Garage(root);Cafe(root);Office(root);Street(root);Combine(root);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate(true,true);AssetDatabase.SaveAssets();return root;
        }
        private static void Gallery(Transform root)
        {
            foreach(int side in new[]{-1,1})
            {
                Box(root,"Gallery facade sill",new Vector3(280+side*10,.28f,14.82f),new Vector3(13.8f,.22f,.24f),steel,false);
                Box(root,"Gallery door pull",new Vector3(280+side*3.08f,1.4f,14.75f),new Vector3(.04f,.55f,.12f),copper,false);
                Pipe(root,"Gallery rear downpipe",new Vector3(280+side*17.4f,.15f,35.1f),new Vector3(280+side*17.4f,side<0?6.7f:5.1f,35.1f),.08f,steel);
            }
            Pipe(root,"Gallery gutter",new Vector3(262,6.9f,36.8f),new Vector3(298,5.05f,36.8f),.12f,steel);
            AC(root,new Vector3(270,7.27f,29),new Vector3(2.5f,.85f,1.5f));AC(root,new Vector3(289,6.28f,30),new Vector3(2.5f,.85f,1.5f));
            SpecStand(root,new Vector3(270,0,22.2f),"AXIS GT\nE: ENTER VEHICLE");SpecStand(root,new Vector3(290,0,22.2f),"EXECUTIVE GT\nE: ENTER VEHICLE");
            Sofa(root,new Vector3(267,0,31),0);Box(root,"Lounge coffee table",new Vector3(270.5f,.55f,31),new Vector3(1.1f,.12f,1.6f),copper,true);Pot(root,new Vector3(270.5f,.65f,31),.35f);
            Box(root,"Wheel display backing",new Vector3(269,1.7f,34.75f),new Vector3(8,2.8f,.12f),pearl,false);
            foreach(float x in new[]{266.5f,269f,271.5f})foreach(float y in new[]{1.0f,2.3f})
            {var tire=Box(root,"Display tyre",new Vector3(x,y,34.5f),new Vector3(.9f,.14f,.9f),rubber,false,PrimitiveType.Cylinder);tire.transform.localRotation=Quaternion.Euler(90,0,0);var rim=Box(root,"Display alloy",new Vector3(x,y,34.32f),new Vector3(.62f,.06f,.62f),steel,false,PrimitiveType.Cylinder);rim.transform.localRotation=Quaternion.Euler(90,0,0);for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Pipe(root,"Alloy display spoke",new Vector3(x,y,34.23f),new Vector3(x+Mathf.Cos(a)*.29f,y+Mathf.Sin(a)*.29f,34.23f),.035f,pearl);}}
            Box(root,"Gallery rear brand panel",new Vector3(288.5f,3.2f,34.8f),new Vector3(10,1.45f,.1f),pearl,false);Sign(root,"AXIS\nPERFORMANCE DIVISION",new Vector3(288.5f,3.2f,34.72f),.25f,9,carbon.color);
            for(int i=0;i<15;i++)Box(root,"Reception cedar detail",new Vector3(288.8f+i*.31f,.85f,30.32f),new Vector3(.16f,1.35f,.08f),wood,false);
            Monitor(root,new Vector3(291,1.65f,31));Box(root,"Reception undercounter LED",new Vector3(291,1.55f,30.28f),new Vector3(4.7f,.035f,.035f),led,false);
        }
        private static void Garage(Transform root)
        {
            foreach(float x in new[]{316f,328f})
            {
                foreach(int side in new[]{-1,1}){Box(root,"Vehicle lift column",new Vector3(x+side*1.95f,1.75f,27),new Vector3(.28f,3.25f,.4f),steel,true);Box(root,"Lift motor housing",new Vector3(x+side*2.03f,2.6f,27.15f),new Vector3(.38f,.5f,.45f),carbon,false);Box(root,"Lift floor arm visual",new Vector3(x+side*1.1f,.14f,27),new Vector3(1.5f,.025f,.16f),copper,false);}
                Box(root,"Bay number plate",new Vector3(x,3.45f,14.65f),new Vector3(1.1f,.65f,.12f),pearl,false);Sign(root,x<322?"01":"02",new Vector3(x,3.46f,14.55f),.3f,.9f,carbon.color);
            }
            Box(root,"Workshop pegboard",new Vector3(322,2.55f,33.35f),new Vector3(3,1.2f,.1f),steel,false);
            for(int i=0;i<7;i++){Pipe(root,"Hanging tool",new Vector3(320.85f+i*.37f,2.85f,33.22f),new Vector3(320.9f+i*.37f,2.35f,33.22f),.045f,pearl);Box(root,"Tool handle",new Vector3(320.9f+i*.37f,2.3f,33.2f),new Vector3(.09f,.17f,.06f),carbon,false);}
            var trolley=Group(root,"Mobile tool trolley",new Vector3(320.5f,0,31));Box(trolley,"Trolley cabinet",new Vector3(0,.8f,0),new Vector3(1.3f,1.1f,.75f),copper,true);for(int i=0;i<4;i++){Box(trolley,"Trolley drawer",new Vector3(0,.4f+i*.24f,-.39f),new Vector3(1.15f,.18f,.06f),steel,false);Box(trolley,"Drawer handle",new Vector3(0,.44f+i*.24f,-.44f),new Vector3(.55f,.03f,.04f),carbon,false);}foreach(int side in new[]{-1,1})foreach(int end in new[]{-1,1})Box(trolley,"Trolley caster",new Vector3(side*.47f,.22f,end*.23f),new Vector3(.2f,.2f,.16f),rubber,false,PrimitiveType.Sphere);
            Box(root,"Tyre stack collision",new Vector3(332.5f,.75f,33),new Vector3(.85f,1.2f,.85f),rubber,true);for(int i=0;i<3;i++)Box(root,"Stacked workshop tyre",new Vector3(332.5f,.4f+i*.36f,33),new Vector3(1,.16f,1),rubber,false,PrimitiveType.Cylinder);
            Box(root,"Air compressor tank",new Vector3(311,.85f,33),new Vector3(.8f,.6f,.8f),cyanMaterial(),true,PrimitiveType.Cylinder);Pipe(root,"Compressor handle",new Vector3(310.7f,1.5f,33),new Vector3(311.3f,1.5f,33),.055f,steel);
            for(int i=0;i<8;i++)Box(root,"Workshop side vent",new Vector3(309.83f,1.5f+i*.13f,29),new Vector3(.04f,.06f,2),steel,false);
            var hose=new Vector3(311.2f,.45f,32.6f);for(int i=0;i<16;i++){float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;Pipe(root,"Workshop coiled hose",hose+new Vector3(Mathf.Cos(a)*.4f,0,Mathf.Sin(a)*.4f),hose+new Vector3(Mathf.Cos(b)*.4f,0,Mathf.Sin(b)*.4f),.035f,rubber);}
        }
        private static Material cyanMaterial()=>AssetDatabase.LoadAssetAtPath<Material>(MainRoadFrontageBuilder.Folder+"/Axis cyan signature.mat");
        private static void Cafe(Transform root)
        {
            var cafe=Group(root,"Cafe counter and furniture details",new Vector3(275,0,-49),180);
            Box(cafe,"Cafe menu frame",new Vector3(0,2.8f,7.82f),new Vector3(5.2f,1.55f,.12f),copper,false);Box(cafe,"Cafe menu board",new Vector3(0,2.8f,7.73f),new Vector3(5,1.35f,.04f),carbon,false);Sign(cafe,"LUMA COFFEE\nESPRESSO   LATTE   COLD BREW\nFRESH BAKES  /  ALL DAY",new Vector3(0,2.8f,7.69f),.19f,4.6f,pearl.color);
            Box(cafe,"Espresso machine",new Vector3(-2,1.95f,5),new Vector3(2,.65f,.85f),steel,false);Box(cafe,"Coffee machine fascia",new Vector3(-2,1.98f,4.55f),new Vector3(1.8f,.45f,.08f),carbon,false);
            foreach(int side in new[]{-1,1}){Pipe(cafe,"Espresso group head",new Vector3(-2+side*.43f,1.99f,4.45f),new Vector3(-2+side*.43f,1.84f,4.45f),.095f,steel);Cup(cafe,new Vector3(-2+side*.43f,1.615f,4.44f));}
            Box(cafe,"Pastry case base",new Vector3(2.2f,1.65f,5),new Vector3(2.4f,.12f,1.1f),pearl,false);Box(cafe,"Pastry display glass",new Vector3(2.2f,1.99f,5),new Vector3(2.35f,.55f,1.05f),glass,false);for(int i=0;i<6;i++)Box(cafe,"Pastry display",new Vector3(1.35f+(i%3)*.8f,1.8f,4.7f+(i/3)*.5f),new Vector3(.45f,.12f,.25f),copper,false,PrimitiveType.Capsule);
            foreach(float x in new[]{-3.6f,3.6f})Pipe(cafe,"Cafe entry pull",new Vector3(x,.95f,-9.45f),new Vector3(x,1.5f,-9.45f),.045f,copper);
            foreach(float x in new[]{-9.7f,9.7f})Pipe(cafe,"Cafe rear downpipe",new Vector3(x,.15f,8.16f),new Vector3(x,4.4f,8.16f),.065f,steel);
            foreach(var p in new[]{new Vector3(268,0,-33),new Vector3(282,0,-33),new Vector3(270,0,-49),new Vector3(280,0,-49)}){Cup(root,p+new Vector3(.25f,1.17f,.1f));Pot(root,p+new Vector3(-.25f,1.17f,-.1f),.22f);foreach(int side in new[]{-1,1})foreach(int dx in new[]{-1,1})foreach(int dz in new[]{-1,1})Pipe(root,"Cafe chair support",p+new Vector3(side*1.2f+dx*.24f,.1f,dz*.25f),p+new Vector3(side*1.2f+dx*.24f,.56f,dz*.25f),.045f,steel);}
            Pot(root,new Vector3(268,.1f,-30),.9f);Pot(root,new Vector3(282,.1f,-30),.9f);
        }
        private static void Office(Transform root)
        {
            Box(root,"Lobby reception island",new Vector3(319,.75f,-49),new Vector3(5,1.25f,1.2f),pearl,true);Box(root,"Lobby stone counter",new Vector3(319,1.43f,-49),new Vector3(5.25f,.12f,1.35f),steel,false);
            for(int i=0;i<16;i++)Box(root,"Lobby timber reception slat",new Vector3(316.65f+i*.31f,.75f,-48.36f),new Vector3(.14f,1.1f,.06f),wood,false);
            Monitor(root,new Vector3(319,1.55f,-49));Box(root,"Lobby counter lighting",new Vector3(319,1.37f,-48.3f),new Vector3(4.8f,.035f,.035f),led,false);
            Sofa(root,new Vector3(309,0,-46),180);Box(root,"Lobby coffee table",new Vector3(312,.6f,-46),new Vector3(1.4f,.12f,1.4f),copper,true);Pot(root,new Vector3(312,.68f,-46),.3f);Pot(root,new Vector3(328,.12f,-48),1);
            foreach(float x in new[]{313f,322f})AC(root,new Vector3(x,12.44f,-55),new Vector3(2.8f,.8f,1.7f));
            Pipe(root,"Office roof gutter",new Vector3(306,11.88f,-62.5f),new Vector3(332,11.88f,-62.5f),.1f,steel);
            foreach(float x in new[]{306.1f,331.9f})Pipe(root,"Office rear downpipe",new Vector3(x,.15f,-61.2f),new Vector3(x,11.85f,-61.2f),.08f,steel);
        }
        private static void Street(Transform root)
        {
            foreach(var p in new[]{new Vector3(278.1f,0,1.8f),new Vector3(281.9f,0,1.8f),new Vector3(273.1f,0,-28.2f),new Vector3(276.9f,0,-28.2f),new Vector3(317.1f,0,-28.2f),new Vector3(320.9f,0,-28.2f)}){Box(root,"Pedestrian approach bollard",p+Vector3.up*.48f,new Vector3(.12f,.82f,.12f),steel,true);Box(root,"Bollard LED cap",p+Vector3.up*.89f,new Vector3(.13f,.055f,.13f),led,false);}
            foreach(var p in new[]{new Vector3(261,0,-1.35f),new Vector3(281,0,-1.35f),new Vector3(305,0,-1.35f),new Vector3(337,0,-1.35f),new Vector3(261,0,-24.65f),new Vector3(290,0,-24.65f),new Vector3(310,0,-24.65f)})
            {Box(root,"Flush drain surround",p+Vector3.up*.126f,new Vector3(1,.006f,.35f),steel,false);for(int i=0;i<9;i++)Box(root,"Drain slot marking",p+new Vector3(-.4f+i*.1f,.131f,0),new Vector3(.04f,.003f,.26f),carbon,false);}
            BikeRack(root,new Vector3(265,0,-37.2f));BikeRack(root,new Vector3(306,0,-38));
            foreach(var p in new[]{new Vector3(262,0,12),new Vector3(304,0,12),new Vector3(289,0,-36)}){Box(root,"Street litter bin",p+Vector3.up*.6f,new Vector3(.55f,1.05f,.55f),carbon,true);Box(root,"Bin lid",p+Vector3.up*1.16f,new Vector3(.61f,.08f,.61f),steel,false);}
            foreach(float x in new[]{286f,292f,299f}){Box(root,"Visitor stall paint",new Vector3(x,.073f,5.5f),new Vector3(.065f,.007f,5),pearl,false);GroundNumber(root,((x-286)/6+1).ToString("0"),new Vector3(x+1,.077f,7.6f));}
            GroundNumber(root,"01",new Vector3(309,.077f,-31));GroundNumber(root,"02",new Vector3(325,.077f,-31));
            foreach(float x in new[]{269.3f,270.7f,329.3f,330.7f})Box(root,"Soft tyre wear marking",new Vector3(x,.068f,x<300?5:-33),new Vector3(.055f,.004f,3.2f),rubber,false);
            Box(root,"Delivery drop shelf",new Vector3(336.5f,.9f,36),new Vector3(2,1.5f,.7f),wood,true);Box(root,"Delivery sign plaque",new Vector3(336.5f,2.2f,36.25f),new Vector3(2.2f,.65f,.12f),carbon,false);Sign(root,"DELIVERIES",new Vector3(336.5f,2.2f,36.16f),.18f,2,pearl.color);
        }
        private static void AC(Transform root,Vector3 p,Vector3 size){Box(root,"Rooftop equipment base",p-Vector3.up*(size.y*.5f+.08f),new Vector3(size.x+.2f,.16f,size.z+.2f),carbon,false);Box(root,"Rooftop AC housing",p,size,steel,false);for(int i=0;i<8;i++)Box(root,"AC ventilation fin",p+new Vector3(-size.x*.42f+i*size.x*.12f,0,-size.z*.51f),new Vector3(.08f,size.y*.8f,.035f),carbon,false);Box(root,"AC top fan grille",p+Vector3.up*(size.y*.5f+.01f),new Vector3(size.z*.65f,.025f,size.z*.65f),carbon,false,PrimitiveType.Cylinder);}
        private static void SpecStand(Transform root,Vector3 p,string title){Box(root,"Spec stand foot",p+Vector3.up*.17f,new Vector3(.5f,.1f,.45f),steel,false);Box(root,"Spec stand stem",p+Vector3.up*.65f,new Vector3(.1f,.95f,.1f),steel,true);var board=Group(root,"Angled specification plaque",p+Vector3.up*1.2f,0);board.localRotation=Quaternion.Euler(20,0,0);Box(board,"Spec plaque",Vector3.zero,new Vector3(1.35f,.65f,.08f),carbon,false);Sign(board,title,new Vector3(0,0,-.055f),.12f,1.2f,pearl.color);}
        private static void Sofa(Transform root,Vector3 p,float yaw){var g=Group(root,"Visitor lounge sofa",p,yaw);Box(g,"Sofa base",new Vector3(0,.4f,0),new Vector3(3.2f,.45f,1),carbon,true);Box(g,"Sofa back",new Vector3(0,.9f,.4f),new Vector3(3.2f,.85f,.25f),leather,true);for(int i=0;i<3;i++)Box(g,"Seat cushion",new Vector3(-1+i,.69f,-.05f),new Vector3(.94f,.22f,.8f),leather,false);foreach(int side in new[]{-1,1})Box(g,"Sofa arm",new Vector3(side*1.55f,.85f,0),new Vector3(.25f,.55f,1),wood,false);}
        private static void Monitor(Transform root,Vector3 p){Box(root,"Desk terminal base",p,new Vector3(.55f,.07f,.35f),carbon,false);Box(root,"Desk terminal stem",p+Vector3.up*.25f,new Vector3(.06f,.45f,.06f),steel,false);Box(root,"Desk terminal screen",p+Vector3.up*.54f,new Vector3(.85f,.5f,.08f),carbon,false);Box(root,"Terminal display glass",p+new Vector3(0,.54f,-.047f),new Vector3(.76f,.41f,.012f),glass,false);}
        private static void Cup(Transform root,Vector3 p){Box(root,"Coffee cup",p+Vector3.up*.08f,new Vector3(.13f,.08f,.13f),pearl,false,PrimitiveType.Cylinder);Box(root,"Coffee surface",p+Vector3.up*.164f,new Vector3(.105f,.005f,.105f),wood,false,PrimitiveType.Cylinder);Box(root,"Cup saucer",p+Vector3.up*.01f,new Vector3(.22f,.01f,.22f),steel,false,PrimitiveType.Cylinder);}
        private static void Pot(Transform root,Vector3 p,float size){Box(root,"Ceramic herb planter",p+Vector3.up*size*.22f,new Vector3(size*.7f,size*.22f,size*.7f),pearl,false,PrimitiveType.Cylinder);for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;var tip=p+new Vector3(Mathf.Cos(a)*size*.45f,size*(.6f+(i%2)*.2f),Mathf.Sin(a)*size*.45f);Pipe(root,"Ornamental grass blade",p+Vector3.up*size*.4f,tip,Mathf.Max(.012f,size*.04f),leaf);}}
        private static void BikeRack(Transform root,Vector3 p){for(int i=0;i<3;i++){float x=p.x+i*.6f;Pipe(root,"Cycle rack upright",new Vector3(x,.15f,p.z),new Vector3(x,.9f,p.z),.055f,steel);Pipe(root,"Cycle rack top",new Vector3(x,.9f,p.z),new Vector3(x,.9f,p.z+.55f),.055f,steel);Pipe(root,"Cycle rack return",new Vector3(x,.9f,p.z+.55f),new Vector3(x,.15f,p.z+.55f),.055f,steel);}}
        private static void GroundNumber(Transform root,string text,Vector3 p){var t=Sign(root,text,p,.23f,.9f,pearl.color);t.transform.localRotation=Quaternion.Euler(90,0,0);}
        private static Transform Group(Transform root,string name,Vector3 p,float yaw=0){var g=new GameObject(name).transform;g.SetParent(root,false);g.localPosition=p;g.localRotation=Quaternion.Euler(0,yaw,0);return g;}
        private static void Pipe(Transform root,string name,Vector3 a,Vector3 b,float width,Material m){var g=Box(root,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),m,false);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        private static GameObject Box(Transform root,string name,Vector3 p,Vector3 size,Material m,bool solid,PrimitiveType shape=PrimitiveType.Cube){var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());if(solid)go.AddComponent<BoxCollider>();return go;}
        private static TMP_Text Sign(Transform root,string title,Vector3 p,float size,float width,Color color){var t=new GameObject(title).AddComponent<TextMeshPro>();t.transform.SetParent(root,false);t.transform.localPosition=p;t.font=TMP_Settings.GetFontAsset();t.text=title;t.fontSize=size*10;t.color=color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(width,size*3.5f);t.raycastTarget=false;return t;}
        private static Material Load(string name){var m=AssetDatabase.LoadAssetAtPath<Material>(MainRoadFrontageBuilder.Folder+"/"+name+".mat");if(m==null)throw new InvalidOperationException("Missing frontage material "+name);return m;}
        private static Material Mat(string name,Color color,float smooth){string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;}
        private static void Combine(Transform root)
        {
            int index=0;var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null).ToArray();foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
            {var parts=group.ToArray();var mesh=new Mesh{name="Detail "+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();string path=Folder+"/Details"+(index++)+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null)AssetDatabase.CreateAsset(mesh,path);else{saved.Clear();EditorUtility.CopySerialized(mesh,saved);saved.UploadMeshData(false);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=group.Key;foreach(var f in parts){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}}
        }
    }
}