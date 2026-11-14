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
    /// <summary>Individually designed north and south highway frontage sites with clear public sidewalks and flush driveways.</summary>
    public static class MainRoadFrontageBuilder
    {
        public const string Layer="Main Road Axis and Luma Frontage v1";
        public const string Folder="Assets/_Game/Generated/MainRoadFrontage";
        private static Material pearl,carbon,glass,copper,wood,leaf,paving,asphalt,cyan,warm,steel;
        private static readonly List<Renderer> bulbs=new List<Renderer>();
        private static readonly Rect[] Driveways={Rect.MinMaxRect(267,-2,273,13),Rect.MinMaxRect(327,-2,333,13),Rect.MinMaxRect(327,-39,333,-24)};
        [MenuItem("Roadside Haven/Design/Build Main Road Axis and Luma")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(motel==null)return;
            string path=motel.gameObject.scene.path;if(File.Exists(path)){string backup="Backups/MainRoadFrontage/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/"+Path.GetFileName(path));}
            Build(motel);EditorSceneManager.MarkSceneDirty(motel.gameObject.scene);EditorSceneManager.SaveScene(motel.gameObject.scene);AssetDatabase.SaveAssets();
        }
        [MenuItem("Roadside Haven/Design/Focus Main Road Axis and Luma")]
        public static void Focus()
        {
            var motel=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();var area=motel==null?null:motel.transform.Find(Layer);if(area==null)return;Selection.activeGameObject=area.gameObject;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(298,3,-12),new Vector3(105,22,120)),false);
        }
        public static Transform Build(MotelGameRoot motel)
        {
            var old=motel.transform.Find(Layer);if(old!=null)return old;
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Game/Generated","MainRoadFrontage");
            pearl=Mat("Frontage pearl",new Color(.87f,.89f,.87f),.35f);carbon=Mat("Axis graphite",new Color(.035f,.05f,.065f),.5f);copper=Mat("Luma champagne",new Color(.56f,.35f,.16f),.6f);copper.SetFloat("_Metallic",.6f);
            wood=Load("Oiled cedar");leaf=Load("Palm foliage");paving=Mat("Frontage limestone",new Color(.68f,.7f,.67f),.18f,"Paving");asphalt=Mat("Frontage asphalt",new Color(.5f,.52f,.54f),.16f,"RoadParking");steel=Mat("Service brushed metal",new Color(.38f,.42f,.45f),.55f);steel.SetFloat("_Metallic",.5f);warm=Mat("Frontage warm diffuser",new Color(1,.79f,.5f),.3f);cyan=Mat("Axis cyan signature",new Color(.025f,.55f,.7f),.5f);cyan.EnableKeyword("_EMISSION");cyan.SetColor("_EmissionColor",new Color(.025f,.55f,.7f));
            glass=Mat("Frontage clear glass",new Color(.17f,.4f,.47f,.28f),.9f);glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_PreserveSpecularLighting",0);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);glass.SetFloat("_Cull",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;glass.SetShaderPassEnabled("ShadowCaster",false);
            var root=new GameObject(Layer).transform;root.SetParent(motel.transform,false);root.gameObject.AddComponent<MotelDistrictArt>();bulbs.Clear();
            RepairSidewalks(motel.transform,root);MoveExistingLamps(motel.transform);
            Floor(root,"North frontage foundation",Rect.MinMaxRect(258,2,339,41),.06f,paving);
            Floor(root,"South frontage foundation",Rect.MinMaxRect(258,-64,339,-28),.06f,paving);
            Floor(root,"Gallery arrival court",Rect.MinMaxRect(258,2,339,13),.065f,asphalt);
            Floor(root,"Luma pedestrian court",Rect.MinMaxRect(258,-39,302,-28),.1f,paving);
            Floor(root,"Office parking",Rect.MinMaxRect(304,-39,339,-28),.065f,asphalt);
            foreach(var rect in Driveways){var clipped=rect.yMin< -10?Rect.MinMaxRect(rect.xMin,rect.yMin,rect.xMax,-24.04f):Rect.MinMaxRect(rect.xMin,-1.96f,rect.xMax,rect.yMax);Floor(root,"Flush frontage driveway",clipped,.06f,asphalt);}
            Floor(root,"Gallery pedestrian approach",Rect.MinMaxRect(278.5f,1,281.5f,16),.1f,paving);
            Floor(root,"Cafe pedestrian approach",Rect.MinMaxRect(273.5f,-40,276.5f,-27),.1f,paving);
            Floor(root,"Office pedestrian approach",Rect.MinMaxRect(317.5f,-40,320.5f,-27),.1f,paving);
            Gallery(root);ServiceBays(root);Cafe(root);Office(root);Courtyard(root);
            foreach(var p in new[]{new Vector3(260,0,9),new Vector3(302,0,9),new Vector3(336,0,9),new Vector3(260,0,-34),new Vector3(302,0,-59),new Vector3(337,0,-59)}){Planter(root,p,2.8f,1.7f);CoastalCityBlockBuilder.AddPromenadePalm(root,p);}
            foreach(var p in new[]{new Vector3(280,0,18),new Vector3(321,0,19),new Vector3(275,0,-43),new Vector3(319,0,-43)})Lantern(root,p);
            foreach(float x in new[]{306f,312f,318f,324f})Box(root,"Office parking paint",new Vector3(x,.075f,-34),new Vector3(.065f,.008f,7),pearl,false);
            Car(root,new Vector3(274,.13f,25),1);Car(root,new Vector3(286,.13f,25),2);
            Combine(root);RegisterLights(motel);foreach(var t in root.GetComponentsInChildren<TMP_Text>())t.ForceMeshUpdate(true,true);AssetDatabase.SaveAssets();return root;
        }
        private static Transform Group(Transform root,string name,Vector3 p,float yaw=0){var t=new GameObject(name).transform;t.SetParent(root,false);t.localPosition=p;t.localRotation=Quaternion.Euler(0,yaw,0);return t;}
        private static void Gallery(Transform root)
        {
            var shop=Group(root,"Axis Motor Gallery - cantilever glass pavilion",new Vector3(280,0,25));
            Box(shop,"Gallery physical floor",new Vector3(0,.04f,0),new Vector3(34,.16f,20),paving,true);
            Box(shop,"Gallery rear wall",new Vector3(0,3,10),new Vector3(34,5.8f,.25f),carbon,true);
            foreach(int side in new[]{-1,1})
            {
                Box(shop,"Gallery side glazing",new Vector3(side*17,2.65f,0),new Vector3(.14f,5.1f,20),glass,true);
                Box(shop,"Gallery front glazing",new Vector3(side*10,2.65f,-10),new Vector3(14,5.1f,.14f),glass,true);
                Box(shop,"Gallery entrance jamb",new Vector3(side*3.05f,2.65f,-10.1f),new Vector3(.12f,5.1f,.2f),steel,true);
                for(int i=0;i<4;i++)Box(shop,"Gallery structural mullion",new Vector3(side*(5+i*3.5f),2.7f,-10.12f),new Vector3(.07f,5.2f,.16f),steel,false);
            }
            var roof=Box(shop,"Long tapered gallery roof",new Vector3(0,6,0),new Vector3(37,.32f,23.5f),pearl,false);roof.transform.localRotation=Quaternion.Euler(0,0,-3);
            Box(shop,"Gallery graphite soffit",new Vector3(0,5.6f,-10.8f),new Vector3(35,.2f,3.4f),carbon,false);
            Box(shop,"Axis cyan ribbon",new Vector3(0,5.46f,-12.46f),new Vector3(34,.07f,.07f),cyan,false);
            Box(shop,"Gallery suspended fascia",new Vector3(0,4.9f,-12.44f),new Vector3(27,1,.18f),carbon,false);
            foreach(float x in new[]{-10f,10f})Beam(shop,"Gallery fascia support",new Vector3(x,5.4f,-12.44f),new Vector3(x,5.5f,-12.44f),.05f,steel,false);
            Sign(shop,"AXIS MOTOR GALLERY",new Vector3(0,4.9f,-12.55f),.48f,26,pearl.color);
            foreach(float x in new[]{-6f,6f}){Box(shop,"Display floor inset",new Vector3(x,.13f,0),new Vector3(5,.01f,7),carbon,false);Box(shop,"Display floor LED",new Vector3(x,.14f,3.4f),new Vector3(4.8f,.015f,.06f),cyan,false);}
            Box(shop,"Gallery reception desk",new Vector3(11,.85f,6),new Vector3(5,1.5f,1.3f),pearl,true);
            Planter(shop,new Vector3(-14,0,6),2.5f,2);
        }
        private static void ServiceBays(Transform root)
        {
            var shop=Group(root,"Axis Car Care - open truss workshop",new Vector3(322,0,26));
            Box(shop,"Service floor",new Vector3(0,.04f,0),new Vector3(26,.16f,22),paving,true);
            foreach(int side in new[]{-1,1})Box(shop,"Workshop side wall",new Vector3(side*13,2.4f,0),new Vector3(.25f,4.6f,22),carbon,true);
            foreach(float x in new[]{-12f,0f,12f})Box(shop,"Service front pier",new Vector3(x,2.3f,-11),new Vector3(.3f,4.4f,.3f),steel,true);
            for(int i=0;i<4;i++)
            {
                float z=-8.5f+i*5.6f;Beam(shop,"Exposed truss rafter",new Vector3(-13,4.7f,z),new Vector3(0,6.4f,z),.17f,steel,false);Beam(shop,"Exposed truss rafter",new Vector3(0,6.4f,z),new Vector3(13,4.7f,z),.17f,steel,false);
                foreach(int side in new[]{-1,1}){var roof=Box(shop,"Sawtooth roof panel",new Vector3(side*6.5f,5.7f,z),new Vector3(13.8f,.18f,5.3f),pearl,false);roof.transform.localRotation=Quaternion.Euler(0,0,-side*7.4f);}
            }
            Box(shop,"Workshop header",new Vector3(0,4.35f,-11.4f),new Vector3(25,.75f,.28f),carbon,false);Sign(shop,"AXIS CAR CARE",new Vector3(0,4.4f,-11.59f),.45f,23,pearl.color);
            foreach(float x in new[]{-6f,6f}){Box(shop,"Service bay marking",new Vector3(x,.13f,0),new Vector3(.08f,.008f,17),cyan,false);Box(shop,"Service pit visual",new Vector3(x,.13f,3),new Vector3(3,.008f,5),carbon,false);}
            Box(shop,"Service tool cabinet",new Vector3(0,1,7),new Vector3(3,1.8f,1),cyan,true);
        }
        private static void Cafe(Transform root)
        {
            var shop=Group(root,"Luma Cafe - curved glass and butterfly roof",new Vector3(275,0,-49),180);
            Box(shop,"Cafe physical floor",new Vector3(0,.04f,0),new Vector3(22,.16f,20),paving,true);
            Box(shop,"Cafe rear wall",new Vector3(0,2.2f,8),new Vector3(20,4.2f,.25f),pearl,true);
            foreach(int side in new[]{-1,1})Box(shop,"Cafe side wall",new Vector3(side*10,2.2f,4),new Vector3(.2f,4.2f,8),carbon,true);
            for(int i=0;i<18;i++)
            {
                float a=i*Mathf.PI/18,b=(i+1)*Mathf.PI/18;if(i>=7&&i<=10)continue;var p=new Vector3(Mathf.Cos(a)*10,2.2f,-Mathf.Sin(a)*10);var q=new Vector3(Mathf.Cos(b)*10,2.2f,-Mathf.Sin(b)*10);
                var pane=Box(shop,"Curved cafe glass segment",(p+q)*.5f,new Vector3((q-p).magnitude,4.2f,.12f),glass,true);pane.transform.localRotation=Quaternion.FromToRotation(Vector3.right,q-p);
                Box(shop,"Cafe curved glazing mullion",p,new Vector3(.07f,4.3f,.09f),copper,false);
            }
            Box(shop,"Cafe ceiling plate",new Vector3(0,4.4f,0),new Vector3(22,.2f,20),carbon,false);
            foreach(int side in new[]{-1,1})
            {
                var wing=Box(shop,"Butterfly roof wing",new Vector3(side*5.8f,5.6f,0),new Vector3(12.6f,.24f,22),pearl,false);wing.transform.localRotation=Quaternion.Euler(0,0,side*10);
                for(int i=0;i<3;i++)Box(shop,"Butterfly roof riser",new Vector3(side*9,5.2f,-6+i*6),new Vector3(.15f,1.8f,.15f),copper,false);
            }
            Box(shop,"Cafe entrance fascia",new Vector3(0,3.7f,-10.15f),new Vector3(8,.7f,.2f),carbon,false);Sign(shop,"LUMA CAFE",new Vector3(0,3.75f,-10.29f),.45f,7.4f,pearl.color);
            Box(shop,"Cafe service counter",new Vector3(0,.85f,5),new Vector3(8,1.5f,1.3f),wood,true);
            Table(shop,new Vector3(-5,0,0));Table(shop,new Vector3(5,0,0));Table(root,new Vector3(268,0,-33));Table(root,new Vector3(282,0,-33));
        }
        private static void Office(Transform root)
        {
            var shop=Group(root,"Luma Work Court - faceted office pavilion",new Vector3(319,0,-51),180);
            Box(shop,"Office lobby floor",new Vector3(0,.04f,0),new Vector3(27,.16f,22),paving,true);
            Box(shop,"Office rear core",new Vector3(0,5.8f,5),new Vector3(27,11.4f,10),carbon,true);
            foreach(int side in new[]{-1,1})Box(shop,"Office side core",new Vector3(side*13,5.8f,-1),new Vector3(.3f,11.4f,22),pearl,true);
            foreach(int side in new[]{-1,1})Box(shop,"Office lobby glazing",new Vector3(side*7.6f,2,-11),new Vector3(11.6f,3.7f,.12f),glass,true);
            for(int floor=0;floor<2;floor++)
            {
                Box(shop,"Office upper glazing",new Vector3(0,6.2f+floor*3.5f,-8.8f),new Vector3(26,3.2f,.12f),glass,false);
                Box(shop,"Office projecting floor",new Vector3(0,4.3f+floor*3.5f,-3),new Vector3(28,.25f,17),pearl,false);
            }
            for(int i=0;i<10;i++){var blade=Box(shop,"Faceted office sun screen",new Vector3(-12+i*2.65f,7.7f,-9.5f),new Vector3(.18f,7.6f,2),i%3==0?copper:pearl,false);blade.transform.localRotation=Quaternion.Euler(0,i%2==0?22:-22,0);}
            foreach(int side in new[]{-1,1})foreach(float y in new[]{2.2f,6.2f,9.7f})
            {
                Box(shop,"Office side window frame",new Vector3(side*13.18f,y,0),new Vector3(.13f,2.3f,15),carbon,false);
                Box(shop,"Office side glazing",new Vector3(side*13.28f,y,0),new Vector3(.05f,2.05f,14.6f),glass,false);
            }
            var roof=Box(shop,"Offset office roof",new Vector3(0,11.7f,-1),new Vector3(29,.35f,24),pearl,false);roof.transform.localRotation=Quaternion.Euler(0,5,0);
            Box(shop,"Office entrance canopy",new Vector3(0,3.7f,-12),new Vector3(7,.22f,3),copper,false);Box(shop,"Office entrance fascia",new Vector3(0,3.25f,-13.48f),new Vector3(11,.9f,.18f),carbon,false);Sign(shop,"LUMA WORK COURT",new Vector3(0,3.25f,-13.58f),.4f,10,pearl.color);
            Box(shop,"Office lobby reception",new Vector3(0,.85f,1),new Vector3(6,1.5f,1.2f),pearl,true);
        }
        private static void Courtyard(Transform root)
        {
            Box(root,"Courtyard sculpture plinth",new Vector3(296,.35f,-33),new Vector3(5,.5f,3.2f),pearl,true);
            Box(root,"Courtyard mounting neck",new Vector3(296,.72f,-33),new Vector3(.18f,.28f,.18f),copper,false);
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;for(int j=0;j<8;j++){float b=j*Mathf.PI/4;float r=2.1f+.18f*Mathf.Cos(b);v.Add(new Vector3(296+r*Mathf.Cos(a),3.1f+r*Mathf.Sin(a),-33+.18f*Mathf.Sin(b)));if(i>0){int n=i*8+j,q=i*8+(j+1)%8;t.AddRange(new[]{n-8,n,q-8,q-8,n,q});}}}
            MeshPart(root,"Luma loop sculpture",v,t,copper);
            Planter(root,new Vector3(291,0,-36.5f),3,1.4f);Planter(root,new Vector3(301,0,-36.5f),3,1.4f);
        }
        private static void Car(Transform root,Vector3 position,int style)
        {
            var car=Group(root,"Axis drivable display car "+style,position,180);car.gameObject.AddComponent<Rigidbody>().isKinematic=true;car.GetComponent<Rigidbody>().mass=1200;var box=car.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,.72f,0);box.size=new Vector3(2.18f,1.44f,4.5f);car.gameObject.AddComponent<EnterableVehicle>().Configure(1);car.gameObject.AddComponent<ModernCarVisuals>().Configure(style);
            var paint=Mat("Display paint "+style,style==1?new Color(.05f,.2f,.65f):new Color(.5f,.56f,.6f),.85f);paint.SetFloat("_Metallic",.65f);
            var vertices=new List<Vector3>();var triangles=new List<int>();float[] z={-2.2f,-1.5f,-.7f,.7f,1.7f,2.2f};float[] w={.7f,1,.96f,.92f,.88f,.68f};float[] y={.62f,.85f,.86f,.75f,.6f,.46f};
            for(int i=0;i<z.Length;i++){vertices.AddRange(new[]{new Vector3(-w[i],.28f,z[i]),new Vector3(-w[i],y[i],z[i]),new Vector3(w[i],y[i],z[i]),new Vector3(w[i],.28f,z[i])});if(i>0)for(int s=0;s<4;s++){int n=i*4+s,q=i*4+(s+1)%4;triangles.AddRange(new[]{n-4,n,q-4,q-4,n,q});}}
            triangles.AddRange(new[]{0,1,2,0,2,3,20,22,21,20,23,22});
            string path=Folder+"/DisplayCarBody.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null){mesh=new Mesh{name="Baked display coupe"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,path);}Part(car,"Baked coupe body",mesh,paint);
            var cabin=Box(car,"Baked coupe glass cabin",new Vector3(0,.95f,-.05f),new Vector3(1.45f,.55f,1.8f),glass,false);cabin.transform.localRotation=Quaternion.Euler(-7,0,0);
            foreach(int side in new[]{-1,1})foreach(int axle in new[]{-1,1}){var wheel=Box(car,"Baked coupe wheel",new Vector3(side*1.01f,.36f,axle*1.4f),new Vector3(.7f,.12f,.7f),carbon,false,PrimitiveType.Cylinder);wheel.transform.localRotation=Quaternion.Euler(0,0,90);var hub=Box(car,"Baked coupe alloy",new Vector3(side*1.14f,.36f,axle*1.4f),new Vector3(.48f,.04f,.48f),steel,false,PrimitiveType.Cylinder);hub.transform.localRotation=Quaternion.Euler(0,0,90);}
            foreach(int side in new[]{-1,1})Box(car,"Baked coupe LED",new Vector3(side*.55f,.58f,2.13f),new Vector3(.6f,.055f,.07f),cyan,false);
        }
        private static void RepairSidewalks(Transform motel,Transform root)
        {
            var seen=new HashSet<string>();foreach(var r in motel.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!r.gameObject.activeInHierarchy||(r.name!="Public sidewalk"&&r.name!="Low sidewalk curb"))continue;var b=r.bounds;if(b.max.x<258||b.min.x>339||b.center.z>1.2f||b.center.z< -27.2f)continue;
                var rect=Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);if(!Driveways.Any(d=>d.Overlaps(rect)))continue;r.gameObject.SetActive(false);string key=r.name+"|"+b.center+"|"+b.size;if(!seen.Add(key))continue;
                foreach(var p in StreetPavingLayout.ClearRoads(rect,Driveways))Floor(root,"Retained public sidewalk",p,b.max.y,paving);
            }
        }
        private static void MoveExistingLamps(Transform motel)
        {
            var all=motel.GetComponentsInChildren<MeshRenderer>(true);var posts=all.Where(r=>r.gameObject.activeInHierarchy&&r.name=="Streetlight post"&&r.bounds.center.x>258&&r.bounds.center.x<339&&r.bounds.center.z<1&&r.bounds.center.z>-27).ToArray();
            foreach(var post in posts){var p=post.bounds.center;float x=p.x,z=p.z> -10?2.8f:-28.8f;if(p.z> -10&&x>267&&x<273)x=264;if(p.z> -10&&x>327&&x<333)x=335;foreach(var part in all){if(part.transform.parent!=post.transform.parent||(part.name!="Streetlight post"&&part.name!="Streetlight bulb"))continue;var q=part.bounds.center;if(Mathf.Abs(q.x-p.x)<.02f&&Mathf.Abs(q.z-p.z)<.02f)part.transform.position+=new Vector3(x-p.x,0,z-p.z);}}
        }
        private static void Planter(Transform root,Vector3 p,float w,float d){Box(root,"Frontage planter",p+Vector3.up*.25f,new Vector3(w,.4f,d),pearl,true);for(int i=0;i<5;i++)Box(root,"Frontage foliage",p+new Vector3(-w*.36f+i*w*.18f,.65f,0),new Vector3(w*.26f,.75f,d*.8f),leaf,false,PrimitiveType.Sphere);}
        private static void Table(Transform root,Vector3 p){Box(root,"Cafe table stem",p+Vector3.up*.55f,new Vector3(.15f,1,.15f),carbon,true);Box(root,"Cafe round table",p+Vector3.up*1.08f,new Vector3(1.4f,.08f,1.4f),copper,false,PrimitiveType.Cylinder);foreach(int side in new[]{-1,1}){Box(root,"Cafe chair seat",p+new Vector3(side*1.2f,.6f,0),new Vector3(.65f,.1f,.7f),wood,true);Box(root,"Cafe chair back",p+new Vector3(side*1.45f,.9f,0),new Vector3(.1f,.65f,.7f),carbon,true);}}
        private static void Lantern(Transform root,Vector3 p){float ceiling=p.z>0?(p.x<300?5.5f:5.9f):(p.x<300?4.3f:4.15f);Beam(root,"Pendant suspension",p+Vector3.up*3.15f,p+Vector3.up*ceiling,.025f,steel,false);Box(root,"Entry lantern housing",p+Vector3.up*3,new Vector3(.6f,.3f,.4f),carbon,false);var bulb=Box(root,"Entry warm diffuser",p+new Vector3(0,2.85f,-.05f),new Vector3(.45f,.08f,.3f),warm,false);bulbs.Add(bulb.GetComponent<Renderer>());}
        private static void RegisterLights(MotelGameRoot motel){var light=motel.GetComponentsInChildren<WorldNightLighting>().FirstOrDefault();if(light==null)return;var so=new SerializedObject(light);var points=so.FindProperty("fixtures");var surfaces=so.FindProperty("glowingSurfaces");int p=points.arraySize,s=surfaces.arraySize;points.arraySize+=bulbs.Count;surfaces.arraySize+=bulbs.Count;for(int i=0;i<bulbs.Count;i++){points.GetArrayElementAtIndex(p+i).objectReferenceValue=bulbs[i].transform;surfaces.GetArrayElementAtIndex(s+i).objectReferenceValue=bulbs[i];}so.ApplyModifiedPropertiesWithoutUndo();}
        private static void Floor(Transform root,string name,Rect r,float y,Material mat){var go=new GameObject(name,typeof(BoxCollider));go.transform.SetParent(root,false);var c=go.GetComponent<BoxCollider>();c.center=new Vector3(r.center.x,y-.08f,r.center.y);c.size=new Vector3(r.width,.16f,r.height);var v=new List<Vector3>{new Vector3(r.xMin,y,r.yMin),new Vector3(r.xMax,y,r.yMin),new Vector3(r.xMax,y,r.yMax),new Vector3(r.xMin,y,r.yMax)};MeshPart(root,name+" surface",v,new List<int>{0,2,1,0,3,2},mat);}
        private static void Beam(Transform root,string name,Vector3 a,Vector3 b,float width,Material mat,bool solid){var go=Box(root,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),mat,solid);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        private static GameObject Box(Transform root,string name,Vector3 p,Vector3 size,Material mat,bool solid,PrimitiveType shape=PrimitiveType.Cube){var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());if(solid)go.AddComponent<BoxCollider>();return go;}
        private static void MeshPart(Transform root,string name,List<Vector3> v,List<int> t,Material mat){var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,v.Select(p=>new Vector2(p.x*.4f,p.z*.4f)).ToList());mesh.RecalculateNormals();Part(root,name,mesh,mat);}
        private static void Part(Transform root,string name,Mesh mesh,Material mat){var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;}
        private static void Sign(Transform root,string text,Vector3 p,float size,float width,Color color){var textMesh=new GameObject(text).AddComponent<TextMeshPro>();textMesh.transform.SetParent(root,false);textMesh.transform.localPosition=p;textMesh.font=TMP_Settings.GetFontAsset();textMesh.text=text;textMesh.fontSize=size*10;textMesh.alignment=TextAlignmentOptions.Center;textMesh.color=color;textMesh.rectTransform.sizeDelta=new Vector2(width,size*3);textMesh.raycastTarget=false;}
        private static Material Load(string name){var mat=AssetDatabase.LoadAssetAtPath<Material>(MotelDistrictArtBuilder.Folder+"/"+name+".mat");if(mat==null)throw new InvalidOperationException("Missing coastal material "+name);return mat;}
        private static Material Mat(string name,Color color,float smooth,string texture=null){string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.color=color;mat.SetFloat("_Smoothness",smooth);mat.enableInstancing=true;if(texture!=null){mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Albedo.png");mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Surfaces/"+texture+"_Normal.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.2f);}EditorUtility.SetDirty(mat);return mat;}
        private static Transform Top(Transform t,Transform root){while(t.parent!=null&&t.parent!=root)t=t.parent;return t;}
        private static void Combine(Transform root)
        {
            int index=0;var filters=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null&&f.GetComponentInParent<Rigidbody>()==null&&!bulbs.Contains(f.GetComponent<Renderer>())).ToArray();
            foreach(var group in filters.GroupBy(f=>new{Mat=f.GetComponent<MeshRenderer>().sharedMaterial,Bucket=f.GetComponent<MeshRenderer>().sharedMaterial==glass?Top(f.transform,root).name:""}))
            {var parts=group.ToArray();var mesh=new Mesh{name="Frontage "+group.Key.Mat.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(parts.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.RecalculateBounds();string path=Folder+"/Frontage"+(index++)+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null)AssetDatabase.CreateAsset(mesh,path);else{saved.Clear();EditorUtility.CopySerialized(mesh,saved);saved.UploadMeshData(false);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;}Part(root,mesh.name,mesh,group.Key.Mat);foreach(var f in parts){UnityEngine.Object.DestroyImmediate(f.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(f);}}
        }
    }
}