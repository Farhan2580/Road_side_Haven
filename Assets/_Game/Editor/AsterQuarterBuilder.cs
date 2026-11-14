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
    /// <summary>Four individually authored modern retail pavilions around a pedestrian square, linked to Palm Grove.</summary>
    public static class AsterQuarterBuilder
    {
        public const string Layer="Aster Quarter Modern Market v1";
        public const string Folder="Assets/_Game/Generated/AsterQuarter";
        private static Material pearl,carbon,copper,timber,glass,leaf,paving,asphalt,green,cyan,violet,warm,garden;
        private static readonly List<Renderer> lamps=new List<Renderer>();
        [MenuItem("Roadside Haven/Design/Build Aster Quarter Modern Market")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            string path=motel.gameObject.scene.path;if(File.Exists(path)){string backup="Backups/AsterQuarter/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/"+Path.GetFileName(path));}
            Build(motel);EditorSceneManager.MarkSceneDirty(motel.gameObject.scene);EditorSceneManager.SaveScene(motel.gameObject.scene);AssetDatabase.SaveAssets();
        }
        [MenuItem("Roadside Haven/Design/Focus Aster Quarter Modern Market")]
        public static void Focus()
        {
            var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();var area=motel==null?null:motel.transform.Find(Layer);if(area==null)return;Selection.activeGameObject=area.gameObject;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(291,3,180),new Vector3(125,22,100)),false);
        }
        public static Transform Build(MotelGameRoot motel)
        {
            var old=motel.transform.Find(Layer);if(old!=null)return old;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","AsterQuarter");
            pearl=Mat("Pearl ceramic",new Color(.88f,.9f,.87f),.38f);carbon=Mat("Graphite aluminium",new Color(.035f,.055f,.075f),.55f);copper=Mat("Brushed champagne",new Color(.55f,.35f,.16f),.65f);copper.SetFloat("_Metallic",.65f);
            timber=Load("Oiled cedar");leaf=Load("Palm foliage");paving=Mat("Limestone plaza",new Color(.67f,.69f,.66f),.18f,"Paving");asphalt=Mat("Retail asphalt",new Color(.5f,.53f,.56f),.15f,"RoadParking");green=Mat("Living green",new Color(.11f,.27f,.13f),.15f);cyan=Mat("Satin teal",new Color(.055f,.34f,.38f),.5f);cyan.SetFloat("_Cull",0);
            violet=Mat("Violet accent",new Color(.45f,.18f,.95f),.4f);violet.EnableKeyword("_EMISSION");violet.SetColor("_EmissionColor",new Color(.45f,.18f,.95f)*1.2f);warm=Mat("Warm diffuser",new Color(1,.78f,.48f),.4f);
            glass=Mat("Clear architectural glass",new Color(.19f,.43f,.5f,.3f),.92f);glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_PreserveSpecularLighting",0);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);glass.SetFloat("_Cull",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;glass.SetShaderPassEnabled("ShadowCaster",false);
            garden=Mat("Roof and plaza lawn",new Color(.6f,.78f,.53f),.12f,"BaseGrass");
            var root=new GameObject(Layer).transform;root.SetParent(motel.transform,false);root.gameObject.AddComponent<MotelDistrictArt>();lamps.Clear();
            Box(root,"Connected road",new Vector3(343.5f,0,90),new Vector3(17,.12f,8),asphalt,true);
            Box(root,"Connected road",new Vector3(348,0,153.5f),new Vector3(8,.12f,127),asphalt,true);
            Floor(root,"Market foundation",Rect.MinMaxRect(240,136,340,225),.06f,paving);
            Floor(root,"Public plaza walking floor",Rect.MinMaxRect(240,136,340,175),.1f,paving);
            // A direct pedestrian link runs in the existing gap between residential lots.
            Floor(root,"Residential walking connection",Rect.MinMaxRect(268.2f,96.5f,271.8f,145),.1f,paving);
            Floor(root,"Market east sidewalk",Rect.MinMaxRect(341.1f,94.05f,343.5f,217),.12f,paving);
            Floor(root,"Market opposite sidewalk",Rect.MinMaxRect(352.5f,94.05f,354.9f,217),.12f,paving);
            Floor(root,"Market plaza entry",Rect.MinMaxRect(336,161,343.96f,165),.1f,paving);
            Floor(root,"Market visitor parking",Rect.MinMaxRect(240,206,340,224),.06f,asphalt);
            Floor(root,"Market loading alley",Rect.MinMaxRect(240,197.2f,340,203.5f),.06f,asphalt);
            Floor(root,"Parking driveway",Rect.MinMaxRect(337,211,343.96f,217),.06f,asphalt);
            Floor(root,"Loading driveway",Rect.MinMaxRect(337,198,343.96f,202.5f),.06f,asphalt);
            for(int i=0;i<20;i++)Box(root,"Parking paint marking",new Vector3(242+i*4.8f,.071f,218),new Vector3(.07f,.008f,9),pearl,false);
            for(int i=0;i<6;i++)Box(root,"Loading bay marking",new Vector3(246+i*14,.071f,200),new Vector3(6,.008f,.08f),warm,false);
            Grocery(root);Bakery(root);Pharmacy(root);Restaurant(root);ShadeSculpture(root);EllipticGarden(root,new Vector3(255,0,149));EllipticGarden(root,new Vector3(318,0,149));
            foreach(var p in new[]{new Vector3(247,0,141),new Vector3(263,0,141),new Vector3(309,0,141),new Vector3(332,0,141)})
            {Planter(root,p,3.6f,2.2f);CoastalCityBlockBuilder.AddPromenadePalm(root,p);}
            foreach(var p in new[]{new Vector3(243,0,162),new Vector3(266,0,170),new Vector3(290,0,171),new Vector3(315,0,170),new Vector3(335,0,158)})Planter(root,p,3.6f,1.3f);
            foreach(float x in new[]{245f,258f,283f,295f,321f,333f})Lantern(root,new Vector3(x,0,143));
            Box(root,"Quarter entrance monolith",new Vector3(277,1.5f,138),new Vector3(6,2.8f,.45f),carbon,true);
            Sign(root,"ASTER QUARTER",new Vector3(277,2.1f,137.74f),.43f,5.5f,pearl.color);Sign(root,"MARKET  /  FOOD  /  DESIGN",new Vector3(277,1.25f,137.74f),.18f,5.5f,warm.color);
            Box(root,"Quarter violet signature",new Vector3(277,2.95f,138),new Vector3(5.8f,.065f,.5f),violet,false);
            Combine(root);RegisterLights(motel);
            var road=RoadSurfacePhysics.Rebuild(motel.transform);string path=Folder+"/MarketRoadSurface.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){saved=road.SurfaceMesh;AssetDatabase.CreateAsset(saved,path);}else{saved.Clear();EditorUtility.CopySerialized(road.SurfaceMesh,saved);saved.UploadMeshData(false);}road.UseSavedMesh(saved);
            foreach(var t in root.GetComponentsInChildren<TMP_Text>())t.ForceMeshUpdate(true,true);AssetDatabase.SaveAssets();return root;
        }
        private static Transform Group(Transform root,string name,Vector3 p){var t=new GameObject(name).transform;t.SetParent(root,false);t.localPosition=p;return t;}
        private static void Shell(Transform root,float width,float depth,float height,Material wall)
        {
            Box(root,"Shop floor",new Vector3(0,.04f,0),new Vector3(width,.16f,depth),paving,true);
            Box(root,"Shop back wall",new Vector3(0,height*.5f+.1f,depth*.5f),new Vector3(width,height,.22f),wall,true);
            foreach(int side in new[]{-1,1})
            {
                Box(root,"Shop side wall",new Vector3(side*width*.5f,height*.5f+.1f,0),new Vector3(.22f,height,depth),wall,true);
                float pane=(width-3.6f)*.5f;float glazed=Mathf.Min(4.2f,height-.25f),frameY=.12f+glazed*.5f;
                Box(root,"Physical front glazing",new Vector3(side*(1.8f+pane*.5f),frameY,-depth*.5f),new Vector3(pane,glazed,.12f),glass,true);
                Box(root,"Entrance jamb",new Vector3(side*1.85f,frameY,-depth*.5f-.04f),new Vector3(.1f,glazed,.18f),carbon,true);
                for(int i=0;i<3;i++)Box(root,"Facade mullion",new Vector3(side*(3+i*2),frameY,-depth*.5f-.08f),new Vector3(.065f,glazed+.05f,.16f),carbon,false);
            }
        }
        private static void Grocery(Transform root)
        {
            var shop=Group(root,"Verde Market - terraced living roof",new Vector3(252,0,184));Shell(shop,20,18,5.8f,pearl);
            Box(shop,"Cantilevered market roof",new Vector3(0,6,0),new Vector3(22,.4f,20),pearl,false);
            Box(shop,"Timber soffit",new Vector3(0,4.7f,-10.2f),new Vector3(21,.18f,3.8f),timber,false);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<7;i++)Box(shop,"Vertical timber fin",new Vector3(side*(3.2f+i*.85f),2.8f,-9.35f),new Vector3(.16f,5.3f,.65f),timber,false);
            Box(shop,"Market fascia",new Vector3(0,5.25f,-9.6f),new Vector3(19,1,.25f),carbon,false);
            Sign(shop,"VERDE MARKET",new Vector3(0,5.3f,-9.77f),.55f,17,pearl.color);
            Surface(shop,"Living roof meadow",Rect.MinMaxRect(-9,1,9,8),6.23f,garden);
            for(int i=0;i<6;i++)Planter(shop,new Vector3(-8+i*3.2f,6.25f,5.8f),2.6f,2);
            Box(shop,"Living green wall",new Vector3(-10.18f,2.8f,0),new Vector3(.25f,5.4f,14),green,false);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)Box(shop,"Grocery shelving",new Vector3(side*6,1.1f,-2+i*3.3f),new Vector3(2,2,1),timber,true);
            Box(shop,"Grocery checkout counter",new Vector3(0,.75f,5.6f),new Vector3(6,1.3f,1),carbon,true);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<6;i++)Box(shop,"Produce display",new Vector3(side*6+.3f*(i%3),2.2f,-2+i/3*.3f),Vector3.one*.2f,i%2==0?green:copper,false,PrimitiveType.Sphere);
        }
        private static void Bakery(Transform root)
        {
            var shop=Group(root,"Aurelia Bakery - ceramic vault",new Vector3(276,0,185));Shell(shop,18,18,4.3f,pearl);
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=48;i++){float a=i*Mathf.PI/48;foreach(float z in new[]{-10f,10f})v.Add(new Vector3(Mathf.Cos(a)*10,4.25f+Mathf.Sin(a)*3.6f,z));if(i>0){int n=i*2;t.AddRange(new[]{n-2,n,n-1,n-1,n,n+1});}}
            var roof=Mat("Vault ceramic double sided",new Color(.94f,.91f,.83f),.35f);roof.SetFloat("_Cull",0);MeshPart(shop,"Sculpted barrel vault",v,t,roof);
            foreach(float face in new[]{-9.95f,9.95f})
            {
                var arch=new List<Vector3>{new Vector3(0,4.2f,face)};var faces=new List<int>();
                for(int i=0;i<=32;i++){float a=i*Mathf.PI/32;arch.Add(new Vector3(Mathf.Cos(a)*9.75f,4.2f+Mathf.Sin(a)*3.55f,face));if(i>0)faces.AddRange(new[]{0,i+1,i});}
                MeshPart(shop,"Arched clerestory glazing",arch,faces,glass);
            }
            foreach(float z in new[]{-10.05f,0,10.05f})for(int i=0;i<24;i++){float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;Beam(shop,"Vault champagne rib",new Vector3(Mathf.Cos(a)*10,4.25f+Mathf.Sin(a)*3.6f,z),new Vector3(Mathf.Cos(b)*10,4.25f+Mathf.Sin(b)*3.6f,z),.13f,copper,false);}
            foreach(float x in new[]{-5.5f,5.5f})Beam(shop,"Bakery sign suspension",new Vector3(x,5.1f,-10.2f),new Vector3(x,7.2f,-10.2f),.065f,copper,false);
            Box(shop,"Bakery suspended fascia",new Vector3(0,4.7f,-10.2f),new Vector3(13,.85f,.2f),carbon,false);Sign(shop,"AURELIA BAKERY",new Vector3(0,4.75f,-10.34f),.48f,12,pearl.color);
            foreach(int side in new[]{-1,1}){Box(shop,"Bakery display plinth",new Vector3(side*5,.7f,-4),new Vector3(4,1.2f,2),timber,true);Box(shop,"Display case glass",new Vector3(side*5,1.6f,-4),new Vector3(4,.7f,2),glass,false);for(int i=0;i<5;i++)Box(shop,"Bread display",new Vector3(side*5-1.3f+i*.65f,1.4f,-4),new Vector3(.4f,.22f,.8f),copper,false,PrimitiveType.Capsule);}
            Box(shop,"Bakery service bar",new Vector3(0,.75f,5.5f),new Vector3(8,1.3f,1.2f),pearl,true);
            Table(root,new Vector3(270,0,170),pearl);Table(root,new Vector3(282,0,169),copper);
        }
        private static void Pharmacy(Transform root)
        {
            var shop=Group(root,"Nova Health - folded pearl facade",new Vector3(302,0,184));Shell(shop,18,18,6.5f,carbon);
            for(int i=0;i<6;i++)
            {
                var panel=Box(shop,"Folded roof panel",new Vector3(-7.5f+i*3,7.2f,-.5f),new Vector3(3.35f,.24f,20),pearl,false);panel.transform.localRotation=Quaternion.Euler(0,0,i%2==0?12:-12);
            }
            foreach(int side in new[]{-1,1})for(int i=0;i<3;i++){var blade=Box(shop,"Angled facade blade",new Vector3(side*(4.5f+i*1.7f),3.25f,-9.5f),new Vector3(.25f,6.7f,1.5f),pearl,false);blade.transform.localRotation=Quaternion.Euler(0,side*25,0);}
            Box(shop,"Health digital fascia",new Vector3(0,5.7f,-10.5f),new Vector3(14,.85f,.2f),cyan,false);Sign(shop,"NOVA HEALTH",new Vector3(0,5.72f,-10.64f),.48f,13,pearl.color);
            Box(shop,"Health signature cross",new Vector3(7,7.5f,-10),new Vector3(.25f,2,.2f),violet,false);Box(shop,"Health signature cross",new Vector3(7,7.5f,-10),new Vector3(1.5f,.25f,.2f),violet,false);
            foreach(int side in new[]{-1,1})for(int i=0;i<3;i++)Box(shop,"Pharmacy shelving",new Vector3(side*6,1.2f,-2+i*3),new Vector3(2,2.2f,.8f),pearl,true);
            Box(shop,"Health reception desk",new Vector3(0,.8f,5.5f),new Vector3(6,1.4f,1.2f),cyan,true);
        }
        private static void Restaurant(Transform root)
        {
            var shop=Group(root,"Orbit Dining - cantilevered glass terrace",new Vector3(328,0,184));Shell(shop,18,20,3.85f,carbon);
            Box(shop,"Upper dining terrace",new Vector3(0,4.05f,1),new Vector3(20,.2f,24),pearl,true);
            var upper=Group(shop,"Upper glass dining pavilion",new Vector3(-2,4.16f,2));Shell(upper,12,12,3.5f,glass);
            var roof=Box(upper,"Rotated cantilever roof",new Vector3(0,3.8f,0),new Vector3(17,.3f,15),carbon,false);roof.transform.localRotation=Quaternion.Euler(0,-8,0);
            Box(shop,"Orbit fascia",new Vector3(0,3.15f,-10.3f),new Vector3(17,1,.2f),copper,false);Sign(shop,"ORBIT DINING",new Vector3(0,3.18f,-10.45f),.55f,16,pearl.color);
            foreach(int side in new[]{-1,1}){Box(shop,"Terrace front railing",new Vector3(side*5,4.75f,-11),new Vector3(9.8f,1.1f,.08f),glass,true);Box(shop,"Terrace back railing",new Vector3(side*5,4.75f,13),new Vector3(9.8f,1.1f,.08f),glass,true);}
            Box(shop,"Terrace west railing",new Vector3(-10,4.75f,1),new Vector3(.08f,1.1f,24),glass,true);
            foreach(var span in new[]{new Vector2(-11,-2),new Vector2(4,13)})Box(shop,"Terrace east railing",new Vector3(10,4.75f,(span.x+span.y)*.5f),new Vector3(.08f,1.1f,span.y-span.x),glass,true);
            // An accessible ramp reaches the terrace; the last 3m have an open side landing.
            var ramp=Group(root,"Terrace access ramp",new Vector3(338.8f,2.05f,176));ramp.localRotation=Quaternion.Euler(-Mathf.Atan2(3.8f,18)*Mathf.Rad2Deg,0,0);
            Box(ramp,"Walkable terrace ramp",Vector3.zero,new Vector3(2,.18f,Mathf.Sqrt(18*18+3.8f*3.8f)),pearl,true);
            foreach(int side in new[]{-1,1})Box(ramp,"Ramp handrail",new Vector3(side*.95f,1,-1.5f),new Vector3(.07f,.07f,14),copper,true);
            Table(shop,new Vector3(-6,4.15f,-7),pearl);Table(shop,new Vector3(2,4.15f,-7),pearl);Table(shop,new Vector3(6,4.15f,7),pearl);
            Table(shop,new Vector3(-5,0,-3),copper);Table(shop,new Vector3(5,0,-3),copper);Box(shop,"Dining bar",new Vector3(-5,.8f,6),new Vector3(6,1.4f,1),copper,true);
            foreach(var p in new[]{new Vector3(-8,4.15f,10),new Vector3(8,4.15f,10)})Planter(shop,p,2,1.2f);
        }
        private static void ShadeSculpture(Transform root)
        {
            var canopy=Group(root,"Sculpted ribbon plaza canopy",new Vector3(289,0,155));var v=new List<Vector3>();var tri=new List<int>();
            for(int x=0;x<=24;x++)for(int z=0;z<=12;z++)v.Add(new Vector3(x-12,4.4f+.8f*Mathf.Sin(x*Mathf.PI/12)+.35f*Mathf.Cos(z*Mathf.PI/6),z-6));
            for(int x=0;x<24;x++)for(int z=0;z<12;z++){int a=x*13+z;tri.AddRange(new[]{a,a+1,a+13,a+1,a+14,a+13});}MeshPart(canopy,"Twisting ribbon roof",v,tri,cyan);
            foreach(int side in new[]{-1,1})foreach(int end in new[]{-1,1}){float x=side*10.5f,z=end*5.4f,y=4.4f+.8f*Mathf.Sin((x+12)*Mathf.PI/12)+.35f*Mathf.Cos((z+6)*Mathf.PI/6);Beam(canopy,"Splayed canopy column",new Vector3(side*9,.1f,end*4.5f),new Vector3(x,y,z),.18f,pearl,true);}
            foreach(float x in new[]{-7f,7f}){Table(canopy,new Vector3(x,0,-1),pearl);Bench(canopy,new Vector3(x,0,4));}
            for(int i=0;i<24;i++){float a=4.75f+.8f*Mathf.Sin(i*Mathf.PI/12),b=4.75f+.8f*Mathf.Sin((i+1)*Mathf.PI/12);Beam(canopy,"Violet canopy edge",new Vector3(i-12,a,-6.03f),new Vector3(i-11,b,-6.03f),.055f,violet,false);}
        }
        private static void EllipticGarden(Transform root,Vector3 p)
        {
            var basePlate=Box(root,"Elliptic ceramic garden",p+Vector3.up*.28f,new Vector3(12,.18f,6),pearl,false,PrimitiveType.Cylinder);
            basePlate.AddComponent<MeshCollider>().sharedMesh=basePlate.GetComponent<MeshFilter>().sharedMesh;
            Box(root,"Elliptic planted lawn",p+Vector3.up*.47f,new Vector3(11.5f,.02f,5.5f),garden,false,PrimitiveType.Cylinder);
            Bench(root,p+new Vector3(0,0,-3.6f));
        }
        private static void Table(Transform root,Vector3 p,Material top)
        {
            Box(root,"Table stem",p+Vector3.up*.55f,new Vector3(.16f,1,.16f),carbon,true);Box(root,"Table top",p+Vector3.up*1.08f,new Vector3(1.5f,.08f,1.5f),top,false,PrimitiveType.Cylinder);
            foreach(int side in new[]{-1,1}){Box(root,"Dining chair seat",p+new Vector3(side*1.25f,.6f,0),new Vector3(.65f,.1f,.7f),timber,true);Box(root,"Dining chair back",p+new Vector3(side*1.5f,.9f,0),new Vector3(.1f,.65f,.7f),carbon,true);foreach(float z in new[]{-.28f,.28f})Box(root,"Chair support",p+new Vector3(side*1.25f,.3f,z),new Vector3(.055f,.55f,.055f),carbon,false);}
        }
        private static void Bench(Transform root,Vector3 p){Box(root,"Plaza bench seat",p+Vector3.up*.6f,new Vector3(3,.16f,.7f),timber,true);Box(root,"Plaza bench back",p+new Vector3(0,1,.32f),new Vector3(3,.7f,.1f),carbon,true);}
        private static void Planter(Transform root,Vector3 p,float w,float d){Box(root,"Architectural planter",p+Vector3.up*.25f,new Vector3(w,.4f,d),pearl,true);for(int i=0;i<5;i++)Box(root,"Planter foliage",p+new Vector3(-w*.38f+i*w*.19f,.65f,0),new Vector3(w*.25f,.8f,d*.8f),leaf,false,PrimitiveType.Sphere);}
        private static void Lantern(Transform root,Vector3 p){Box(root,"Quarter lighting mast",p+Vector3.up*1.8f,new Vector3(.12f,3.5f,.12f),carbon,true);var bulb=Box(root,"Quarter warm diffuser",p+Vector3.up*3.6f,new Vector3(.5f,.16f,.5f),warm,false);lamps.Add(bulb.GetComponent<Renderer>());Box(root,"Lighting mast cap",p+Vector3.up*3.75f,new Vector3(.7f,.1f,.7f),carbon,false);}
        private static void RegisterLights(MotelGameRoot motel){var light=motel.GetComponentsInChildren<WorldNightLighting>().FirstOrDefault();if(light==null)return;var so=new SerializedObject(light);var points=so.FindProperty("fixtures");var surfaces=so.FindProperty("glowingSurfaces");int p=points.arraySize,s=surfaces.arraySize;points.arraySize+=lamps.Count;surfaces.arraySize+=lamps.Count;for(int i=0;i<lamps.Count;i++){points.GetArrayElementAtIndex(p+i).objectReferenceValue=lamps[i].transform;surfaces.GetArrayElementAtIndex(s+i).objectReferenceValue=lamps[i];}so.ApplyModifiedPropertiesWithoutUndo();}
        private static void Floor(Transform root,string name,Rect r,float y,Material mat){var go=new GameObject(name,typeof(BoxCollider));go.transform.SetParent(root,false);var c=go.GetComponent<BoxCollider>();c.center=new Vector3(r.center.x,y-.08f,r.center.y);c.size=new Vector3(r.width,.16f,r.height);Surface(root,name+" surface",r,y,mat);}
        private static void Surface(Transform root,string name,Rect r,float y,Material mat){var v=new List<Vector3>{new Vector3(r.xMin,y,r.yMin),new Vector3(r.xMax,y,r.yMin),new Vector3(r.xMax,y,r.yMax),new Vector3(r.xMin,y,r.yMax)};MeshPart(root,name,v,new List<int>{0,2,1,0,3,2},mat);}
        private static void Beam(Transform root,string name,Vector3 a,Vector3 b,float width,Material mat,bool solid){var go=Box(root,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),mat,solid);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        private static GameObject Box(Transform root,string name,Vector3 p,Vector3 size,Material mat,bool solid,PrimitiveType shape=PrimitiveType.Cube){var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());if(solid)go.AddComponent<BoxCollider>();return go;}
        private static void MeshPart(Transform root,string name,List<Vector3> vertices,List<int> triangles,Material mat){var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,vertices.Select(v=>new Vector2(v.x*.4f,v.z*.4f)).ToList());mesh.RecalculateNormals();mesh.RecalculateBounds();Part(root,name,mesh,mat);}
        private static void Part(Transform root,string name,Mesh mesh,Material mat){var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;}
        private static void Sign(Transform root,string text,Vector3 p,float size,float width,Color color){var label=new GameObject(text).AddComponent<TextMeshPro>();label.transform.SetParent(root,false);label.transform.localPosition=p;label.font=TMP_Settings.GetFontAsset();label.text=text;label.fontSize=size*10;label.color=color;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(width,size*3);label.raycastTarget=false;}
        private static Material Load(string name){var m=AssetDatabase.LoadAssetAtPath<Material>(MotelDistrictArtBuilder.Folder+"/"+name+".mat");if(m==null)throw new InvalidOperationException("Missing material "+name);return m;}
        private static Material Mat(string name,Color color,float smooth,string texture=null){string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;if(texture!=null){m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Albedo.png");m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Normal.png"));m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.2f);}EditorUtility.SetDirty(m);return m;}
        private static Transform TopGroup(Transform t,Transform root){while(t.parent!=null&&t.parent!=root)t=t.parent;return t;}
        private static void Combine(Transform root)
        {
            int index=0;var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null&&!TownPopulationController.RoadName(f.name)&&!lamps.Contains(f.GetComponent<Renderer>())).ToArray();
            foreach(var group in filters.GroupBy(f=>new{Material=f.GetComponent<MeshRenderer>().sharedMaterial,Bucket=f.GetComponent<MeshRenderer>().sharedMaterial==glass?TopGroup(f.transform,root).name:""}))
            {
                var parts=group.ToArray();var mesh=new Mesh{name="Aster "+group.Key.Material.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();string path=Folder+"/Market"+(index++)+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null)AssetDatabase.CreateAsset(mesh,path);else{saved.Clear();EditorUtility.CopySerialized(mesh,saved);saved.UploadMeshData(false);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}Part(root,mesh.name,mesh,group.Key.Material);foreach(var f in parts){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}
            }
        }
    }
}