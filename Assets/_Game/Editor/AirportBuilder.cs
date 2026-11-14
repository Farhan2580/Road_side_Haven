using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven.Editor
{
    /// <summary>Bakes an airport in the vacant southeast precinct, away from the city and main roads.</summary>
    public static class AirportBuilder
    {
        public const string Folder="Assets/_Game/Generated/Airport";
        private static Material white,navy,glass,concrete,asphalt,paint,metal,green,orange,skin;
        private static Mesh fuselage,wing;
        [MenuItem("Roadside Haven/Airport/Build Airport")]
        public static void BuildCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var root=UnityEngine.Object.FindFirstObjectByType<MotelGameRoot>();if(root==null){Debug.LogError("Open the motel gameplay scene first.");return;}
            var path=root.gameObject.scene.path;if(File.Exists(path)){string backup="Backups/Airport/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(path,backup+"/"+Path.GetFileName(path),true);}
            Build(root.transform);EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);AssetDatabase.SaveAssets();
        }
        public static Transform Build(Transform motel)
        {
            var existing=motel.Find(AirportOperations.RootName);
            EnsureFolder(Folder);
            white=Mat("Pearl white",new Color(.88f,.91f,.92f),.35f);navy=Mat("Haven blue",new Color(.025f,.14f,.24f),.5f);glass=Mat("Architectural glass",new Color(.075f,.23f,.29f),.85f);concrete=Mat("Airport concrete",new Color(.46f,.49f,.48f),.15f);asphalt=Mat("Runway asphalt",new Color(.065f,.078f,.085f),.1f);paint=Mat("Runway paint",new Color(.9f,.91f,.85f),.05f);metal=Mat("Aircraft metal",new Color(.39f,.43f,.47f),.6f);green=Mat("Navigation green",new Color(.05f,.9f,.5f),.2f,true);orange=Mat("Warm lights",new Color(1,.66f,.22f),.2f,true);
            glass.SetFloat("_Surface",1);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;glass.SetColor("_BaseColor",new Color(.075f,.23f,.29f,.38f));glass.SetShaderPassEnabled("ShadowCaster",false);
            skin=Mat("Passenger skin",new Color(.65f,.44f,.30f),.15f);
            fuselage=MakeFuselage();wing=MakeWing();
            if(existing!=null){ExpandNortheast(existing,motel);return existing;}
            var root=new GameObject(AirportOperations.RootName).transform;root.SetParent(motel,false);
            var ops=root.gameObject.AddComponent<AirportOperations>();Undo.RegisterCreatedObjectUndo(root.gameObject,"Create international airport");
            Box(root,"Airport runway",new Vector3(730,-.015f,-515),new Vector3(450,.16f,44),asphalt,true);
            Box(root,"Airport apron",new Vector3(795,-.025f,-370),new Vector3(290,.18f,116),concrete,true);
            Box(root,"Airport taxiway",new Vector3(810,-.015f,-430),new Vector3(240,.16f,20),asphalt,true);
            Box(root,"Airport taxiway connector",new Vector3(920,-.015f,-470),new Vector3(20,.16f,90),asphalt,true);
            for(int i=0;i<22;i++)Box(root,"Runway center marking",new Vector3(520+i*20,.075f,-515),new Vector3(8,.008f,.4f),paint,false);
            foreach(int side in new[]{-1,1})
            {
                Box(root,"Runway edge marking",new Vector3(730,.075f,-515+side*20),new Vector3(440,.008f,.22f),paint,false);
                for(int i=0;i<9;i++)foreach(float x in new[]{524f,936f})Box(root,"Threshold marking",new Vector3(x,.08f,-531+i*4),new Vector3(9,.008f,1.5f),paint,false);
                for(int i=0;i<23;i++)Part(root,"Runway edge bulb",PrimitiveType.Sphere,new Vector3(510+i*20,.16f,-515+side*23),new Vector3(.4f,.16f,.4f),orange,false);
            }
            var n=Text(root,"27",new Vector3(908,.09f,-515),6,12,paint.color);n.transform.rotation=Quaternion.Euler(90,0,90);
            n=Text(root,"09",new Vector3(552,.09f,-515),6,12,paint.color);n.transform.rotation=Quaternion.Euler(90,0,-90);
            for(int i=0;i<12;i++)Box(root,"Taxi yellow marking",new Vector3(680+i*22,.08f,-430),new Vector3(8,.008f,.25f),orange,false);
            // Three connected public approach segments; airside taxiways remain private.
            Box(root,"Connected road",new Vector3(495,0,-330),new Vector3(150,.12f,8),asphalt,true);
            Box(root,"Connected road",new Vector3(570,0,-280),new Vector3(8,.12f,108),asphalt,true);
            // Existing industrial boulevard at Z=-230 serves the terminal frontage.
            Box(root,"Airport pedestrian promenade",new Vector3(790,.015f,-248),new Vector3(185,.22f,8),concrete,true);
            Terminal(root,out var doors,out var board);
            Hangar(root);Tower(root);
            Box(root,"Helipad apron",new Vector3(600,.015f,-455),new Vector3(32,.18f,32),concrete,true);
            var h=Text(root,"H",new Vector3(600,.12f,-455),10,16,Color.white);h.transform.rotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<32;i++){float a=i*Mathf.PI/16;Box(root,"Helipad ring marking",new Vector3(600+Mathf.Cos(a)*12,.13f,-455+Mathf.Sin(a)*12),new Vector3(.6f,.008f,1.6f),paint,false).transform.rotation=Quaternion.Euler(0,-i*11.25f,0);}
            var jet=Plane(root,"HV101 scheduled passenger jet",AirportOperations.Gate,1,out var gear);
            Plane(root,"HV202 parked passenger jet",new Vector3(890,.12f,-350),1,out _);
            Plane(root,"Hangar regional aircraft",new Vector3(600,.12f,-385),.58f,out _);
            var heli=Helicopter(root,out var rotor,out var tail);
            var bridge=Jetbridge(root);
            var people=new List<Transform>();var legs=new List<Transform>();
            for(int i=0;i<12;i++)Person(root,i,people,legs);
            var staff=new List<Transform>();var staffLegs=new List<Transform>();for(int i=0;i<4;i++){Person(root,12+i,staff,staffLegs);staff[i].name="Airport check-in staff "+i;staff[i].position=new Vector3(720+i*12,.2f,-272);}
            var lights=new List<Light>();
            foreach(float x in new[]{680f,800f,925f})
            {
                Box(root,"Airport floodlight pole",new Vector3(x,8,-410),new Vector3(.25f,16,.25f),metal,true);
                Box(root,"Airport floodlight head",new Vector3(x,16,-410),new Vector3(3,.4f,1),white,false);
                var light=new GameObject("Airport apron spotlight").AddComponent<Light>();light.transform.SetParent(root,false);light.transform.position=new Vector3(x,15.5f,-410);light.type=LightType.Spot;light.range=95;light.spotAngle=100;light.intensity=8;light.color=new Color(1,.83f,.61f);light.transform.rotation=Quaternion.Euler(75,0,0);light.shadows=LightShadows.None;light.enabled=false;lights.Add(light);
            }
            // Fence gates leave the access road open; perimeter stays within the map.
            Fence(root,new Vector3(495,0,-540),80,true);Fence(root,new Vector3(495,0,-430),100,true);Fence(root,new Vector3(495,0,-285),60,true);
            Fence(root,new Vector3(965,0,-420),315,true);Fence(root,new Vector3(730,0,-580),470,false);
            for(int i=0;i<10;i++)
            {
                float x=620+i*7;Box(root,"Visitor parking marking",new Vector3(x,.08f,-267),new Vector3(.12f,.008f,8),paint,false);
                if(i%3==0){Part(root,"Airport landscape tree trunk",PrimitiveType.Cylinder,new Vector3(x,2,-250),new Vector3(.45f,2,.45f),metal,true);Part(root,"Airport landscape tree crown",PrimitiveType.Sphere,new Vector3(x,5,-250),new Vector3(4,4,4),green,false);}
            }
            Text(root,"HAVEN INTERNATIONAL AIRPORT",new Vector3(620,5,-319),2.6f,38,Color.white);
            Text(root,"TERMINAL / DEPARTURES  →",new Vector3(590,3,-300),1.5f,22,Color.white);
            ops.Configure(jet,heli,rotor,tail,gear,bridge,people.ToArray(),legs.ToArray(),doors,lights.ToArray(),board);
            ExpandNortheast(root,motel);
            RoadClearance.Resolve(motel);
            var surface=RoadSurfacePhysics.Rebuild(motel);string roadPath=Folder+"/PublicRoadCollision.asset";var savedRoad=AssetDatabase.LoadAssetAtPath<Mesh>(roadPath);
            if(savedRoad==null){savedRoad=surface.SurfaceMesh;AssetDatabase.CreateAsset(savedRoad,roadPath);}else EditorUtility.CopySerialized(surface.SurfaceMesh,savedRoad);
            surface.UseSavedMesh(savedRoad);
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate(true,true);
            AssetDatabase.SaveAssets();return root;
        }
        [MenuItem("Roadside Haven/Airport/Focus Airport")]
        public static void FocusAirport()
        {
            var airport=UnityEngine.Object.FindFirstObjectByType<AirportOperations>();
            if(airport==null){Debug.LogWarning("Open RoadsideHavenPrototype and Build Airport first.");return;}
            Selection.activeGameObject=airport.gameObject;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(airport.transform.TransformPoint(new Vector3(730,0,-430)),new Vector3(1950,100,800)),false);
        }
        private static void ExpandNortheast(Transform root,Transform motel)
        {
            if(root.Find("Northeast airport expansion")!=null)return;
            // Preserve metre-scale aircraft and terminal; extend the runways independently.
            var remove=new List<GameObject>();
            foreach(Transform child in root)
                if(child.name=="Connected road"||child.name.Contains("Fence")||child.name.Contains("fence")||child.name=="Airport perimeter mesh"||child.name=="Runway center marking"||child.name=="Runway edge marking"||child.name=="Threshold marking"||child.name=="Runway edge bulb"||child.name=="27"||child.name=="09")remove.Add(child.gameObject);
            foreach(var go in remove)UnityEngine.Object.DestroyImmediate(go);
            var runway=root.Find("Airport runway");runway.localScale=new Vector3(1800,.16f,45);
            Box(root,"Secondary runway 1500m",new Vector3(730,-.015f,-850),new Vector3(1500,.16f,40),asphalt,true);
            RunwayDetails(root,730,-515,1800,45,"01L / 19R");RunwayDetails(root,730,-850,1500,40,"01R / 19L");
            Box(root,"Secondary taxi connector",new Vector3(1430,-.015f,-680),new Vector3(23,.16f,340),asphalt,true);
            var ground=Mat("Airport meadow",new Color(.23f,.39f,.16f),.05f);
            Box(root,"Airport extension foundation",new Vector3(750,-.35f,-800),new Vector3(2200,.5f,1400),ground,true);
            Box(root,"Airport cargo warehouse",new Vector3(1090,8,-1080),new Vector3(100,16,65),concrete,true);
            Text(root,"AIR CARGO",new Vector3(1090,13,-1047),3,65,Color.white);
            for(int i=0;i<3;i++)Box(root,"Cargo loading shutter",new Vector3(1060+i*30,4,-1047),new Vector3(15,8,.2f),navy,true);
            for(int i=0;i<3;i++){Part(root,"Fuel storage tank",PrimitiveType.Cylinder,new Vector3(350+i*20,6,-1080),new Vector3(14,6,14),white,true);}
            Box(root,"Visitor parking",new Vector3(850,0,-180),new Vector3(180,.12f,55),asphalt,true);
            for(int i=0;i<25;i++)Box(root,"Visitor parking marking",new Vector3(765+i*7,.075f,-180),new Vector3(.12f,.008f,15),paint,false);
            // Public access stays north of the terminal and outside both airside runways.
            Box(root,"Airport access road",new Vector3(-200,0,-785),new Vector3(14,.12f,1144),asphalt,true);
            Box(root,"Airport access road",new Vector3(350,0,-220),new Vector3(1114,.12f,14),asphalt,true);
            Box(root,"Airport access road",new Vector3(-540,0,-1350),new Vector3(694,.12f,14),asphalt,true);
            Box(root,"Access road foundation",new Vector3(-540,-.35f,-1350),new Vector3(700,.5f,24),ground,true);
            Text(root,"AIRPORT / TERMINAL",new Vector3(-600,5,-1315),3,65,Color.white);
            new GameObject("Northeast airport expansion").transform.SetParent(root,false);
            root.position=new Vector3(1300,0,2000);
            // Open only the two old northeast boundary walls; west and south remain intact.
            foreach(var t in motel.GetComponentsInChildren<Transform>(true))
                if((t.name=="Regional side boundary"&&t.position.x>0)||(t.name=="Regional end boundary"&&t.position.z>0))t.gameObject.SetActive(false);
            foreach(var camera in UnityEngine.Object.FindObjectsByType<IsometricCameraController>(FindObjectsSortMode.None))camera.Configure(Vector3.zero,new Vector2(3300,2200),new Vector2(6,1800));
            var surface=RoadSurfacePhysics.Rebuild(motel);string path=Folder+"/PublicRoadCollision.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){saved=surface.SurfaceMesh;AssetDatabase.CreateAsset(saved,path);}else EditorUtility.CopySerialized(surface.SurfaceMesh,saved);
            surface.UseSavedMesh(saved);EditorUtility.SetDirty(root.gameObject);AssetDatabase.SaveAssets();
        }
        private static void RunwayDetails(Transform root,float x,float z,float length,float width,string number)
        {
            for(float p=-length*.5f+30;p<length*.5f-30;p+=30)Box(root,"Runway center marking",new Vector3(x+p,.075f,z),new Vector3(12,.008f,.45f),paint,false);
            foreach(int side in new[]{-1,1})
            {
                Box(root,"Runway edge marking",new Vector3(x,.075f,z+side*(width*.5f-1)),new Vector3(length-10,.008f,.25f),paint,false);
                for(float p=-length*.5f+10;p<length*.5f;p+=30)Part(root,"Runway edge bulb",PrimitiveType.Sphere,new Vector3(x+p,.16f,z+side*(width*.5f+1)),new Vector3(.45f,.18f,.45f),orange,false);
                for(int i=0;i<8;i++)Box(root,"Threshold marking",new Vector3(x+side*(length*.5f-25),.08f,z-width*.4f+i*width*.8f/7),new Vector3(18,.008f,1.5f),paint,false);
            }
            var label=Text(root,number,new Vector3(x-length*.5f+70,.09f,z),6,24,Color.white);label.transform.rotation=Quaternion.Euler(90,0,-90);
        }
        private static void Terminal(Transform root,out Transform[] doors,out TMP_Text board)
        {
            var terminal=new GameObject("Airport terminal").transform;terminal.SetParent(root,false);
            Box(terminal,"Terminal floor",new Vector3(790,.04f,-275),new Vector3(170,.24f,48),concrete,true);
            Box(terminal,"Floating roof",new Vector3(790,15,-275),new Vector3(180,1.2f,56),white,true);
            Box(terminal,"Roof clerestory",new Vector3(790,17,-278),new Vector3(150,3,30),navy,true);
            foreach(int side in new[]{-1,1})Box(terminal,"Terminal side wall",new Vector3(790+side*85,7.5f,-275),new Vector3(.5f,15,48),concrete,true);
            for(int i=0;i<18;i++)
            {
                float x=709+i*9;
                Box(terminal,"Terminal structural column",new Vector3(x,7.5f,-253),new Vector3(.45f,15,.6f),metal,true);
                if(Mathf.Abs(x-790)>7)Box(terminal,"Terminal front glazing",new Vector3(x+4.2f,7,-251.3f),new Vector3(8,13,.18f),glass,true);
                if(Mathf.Abs(x-750)>6)Box(terminal,"Terminal rear glazing",new Vector3(x+4.2f,7,-298.7f),new Vector3(8,13,.18f),glass,true);
            }
            var doorRoot=new GameObject("Sliding entrance doors").transform;doorRoot.SetParent(terminal,false);doorRoot.position=new Vector3(790,0,-251);
            doors=new Transform[2];for(int i=0;i<2;i++)doors[i]=Box(doorRoot,"Sliding glass door",new Vector3(790+(i==0?-2:2),3,-251),new Vector3(3.8f,6,.15f),glass,false).transform;
            for(int i=0;i<6;i++)
            {
                float x=720+i*12;Box(terminal,"Check in desk",new Vector3(x,1,-270),new Vector3(7,2,2),white,true);Box(terminal,"Check in monitor",new Vector3(x,2.6f,-270),new Vector3(1.8f,1,.12f),navy,false);
                Text(terminal,"CHECK IN "+(i+1),new Vector3(x,4,-269),.8f,9,Color.white);
            }
            for(int row=0;row<3;row++)for(int i=0;i<7;i++)Box(terminal,"Departure lounge seat",new Vector3(805+i*7,1,-274-row*6),new Vector3(4,.8f,1.5f),navy,true);
            var title=Text(terminal,"HAVEN INTERNATIONAL",new Vector3(790,12.8f,-250.8f),3.5f,150,Color.white);title.transform.rotation=Quaternion.Euler(0,180,0);
            Text(terminal,"DEPARTURES  /  ARRIVALS",new Vector3(790,7.5f,-250.7f),1.6f,70,Color.white).transform.rotation=Quaternion.Euler(0,180,0);
            Text(terminal,"GATE A1",new Vector3(750,8,-299.1f),1.4f,22,Color.white);
            Text(terminal,"GATE B2",new Vector3(850,8,-299.1f),1.4f,22,Color.white);
            board=Text(terminal,"HV101 / BOARDING / A1",new Vector3(790,9,-292),1.3f,48,new Color(.8f,.94f,1));
            Box(terminal,"Flight information screen",new Vector3(790,9,-291.8f),new Vector3(49,4,.3f),navy,false);
        }
        private static void Hangar(Transform root)
        {
            Box(root,"Hangar foundation",new Vector3(600,.03f,-385),new Vector3(67,.2f,58),concrete,true);
            Box(root,"Hangar roof",new Vector3(600,14,-385),new Vector3(67,1,58),white,true);
            foreach(int side in new[]{-1,1})Box(root,"Hangar wall",new Vector3(600+side*33,7,-385),new Vector3(.6f,14,58),metal,true);
            Box(root,"Hangar back wall",new Vector3(600,7,-413),new Vector3(66,14,.6f),metal,true);
            Text(root,"HAVEN AIR / MAINTENANCE",new Vector3(600,12,-355.5f),1.8f,60,Color.white).transform.rotation=Quaternion.Euler(0,180,0);
        }
        private static void Tower(Transform root)
        {
            Box(root,"ATC tower base",new Vector3(637,15,-310),new Vector3(12,30,12),white,true);
            Box(root,"ATC control room",new Vector3(637,33,-310),new Vector3(22,7,22),glass,true);
            Box(root,"ATC roof",new Vector3(637,37,-310),new Vector3(25,.6f,25),navy,true);
            Part(root,"ATC antenna",PrimitiveType.Cylinder,new Vector3(637,42,-310),new Vector3(.15f,5,.15f),metal,false);
            var radar=new GameObject("ATC rotating radar").transform;radar.SetParent(root,false);radar.position=new Vector3(637,40,-310);
            LocalBox(radar,"Radar reflector",new Vector3(0,1,0),new Vector3(9,2.5f,.35f),white,false);
        }
        private static Transform Jetbridge(Transform root)
        {
            var all=new GameObject("Gate A1 skybridge").transform;all.SetParent(root,false);
            BridgePart(all,new Vector3(750,3.2f,-308),new Vector3(750,3.2f,-325));
            BridgePart(all,new Vector3(750,3.2f,-325),new Vector3(772,3.2f,-325));
            var moving=new GameObject("Retractable jetbridge").transform;moving.SetParent(all,false);
            BridgePart(moving,new Vector3(772,3.2f,-325),new Vector3(778,3.2f,-325));
            BridgePart(moving,new Vector3(778,3.2f,-325),new Vector3(778,3.2f,-340));
            var ramp=Box(all,"Boarding ramp",new Vector3(750,1.55f,-298),new Vector3(3,.3f,20),concrete,true);ramp.transform.rotation=Quaternion.Euler(9,0,0);
            return moving;
        }
        private static void BridgePart(Transform root,Vector3 a,Vector3 b)
        {
            var node=new GameObject("Enclosed boarding gallery").transform;node.SetParent(root,false);node.position=(a+b)*.5f;node.rotation=Quaternion.LookRotation(b-a);
            float length=Vector3.Distance(a,b);
            LocalBox(node,"Gallery floor",new Vector3(0,-.2f,0),new Vector3(3,.3f,length),concrete,true);
            LocalBox(node,"Gallery roof",new Vector3(0,3,0),new Vector3(3.3f,.3f,length),white,false);
            foreach(int side in new[]{-1,1})LocalBox(node,"Gallery glass",new Vector3(side*1.5f,1.4f,0),new Vector3(.12f,2.8f,length),glass,true);
        }
        private static Transform Plane(Transform root,string title,Vector3 p,float scale,out Transform gear)
        {
            var plane=new GameObject(title).transform;plane.SetParent(root,false);plane.position=p;plane.localScale=Vector3.one*scale;
            var body=plane.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            var hull=plane.gameObject.AddComponent<CapsuleCollider>();hull.direction=2;hull.center=new Vector3(0,3.2f,0);hull.radius=1.85f;hull.height=35;
            var skin=new GameObject("Sculpted fuselage",typeof(MeshFilter),typeof(MeshRenderer));skin.transform.SetParent(plane,false);skin.GetComponent<MeshFilter>().sharedMesh=fuselage;skin.GetComponent<Renderer>().sharedMaterial=white;
            foreach(int side in new[]{-1,1})
            {
                var w=new GameObject("Swept main wing",typeof(MeshFilter),typeof(MeshRenderer));w.transform.SetParent(plane,false);w.transform.localScale=new Vector3(side,1,1);w.GetComponent<MeshFilter>().sharedMesh=wing;w.GetComponent<Renderer>().sharedMaterial=metal;
                var wingCollision=new GameObject("Wing collision");wingCollision.transform.SetParent(plane,false);var wc=wingCollision.AddComponent<BoxCollider>();wc.center=new Vector3(side*7,2.7f,-2);wc.size=new Vector3(12,.35f,7);
                LocalBox(plane,"Wing navigation light",new Vector3(side*14,2.65f,-7),new Vector3(.3f,.2f,.5f),side<0?orange:green,false);
                LocalBox(plane,"Blue fuselage stripe",new Vector3(side*1.84f,3,-1),new Vector3(.08f,.25f,25),navy,false);
                for(int i=0;i<18;i++)LocalBox(plane,"Cabin window",new Vector3(side*1.79f,3.8f,-11+i*1.35f),new Vector3(.055f,.48f,.65f),glass,false);
                var engine=LocalPart(plane,"Turbofan nacelle",PrimitiveType.Cylinder,new Vector3(side*5.4f,2.1f,-.5f),new Vector3(1.8f,2,1.8f),white);engine.localRotation=Quaternion.Euler(90,0,0);
                LocalPart(plane,"Engine intake",PrimitiveType.Cylinder,new Vector3(side*5.4f,2.1f,1.55f),new Vector3(1.5f,.05f,1.5f),navy).localRotation=Quaternion.Euler(90,0,0);
                LocalBox(plane,"Tailplane",new Vector3(side*3.6f,3.2f,-13.5f),new Vector3(7,.2f,2.3f),metal,false);
            }
            LocalBox(plane,"Passenger boarding door",new Vector3(-1.86f,3.2f,10),new Vector3(.12f,2.2f,1.3f),navy,false);
            LocalBox(plane,"Cockpit glazing",new Vector3(0,4,15),new Vector3(2.4f,.75f,2),glass,false);
            var fin=LocalBox(plane,"Blue tail fin",new Vector3(0,6,-13.8f),new Vector3(.35f,6,4),navy,false);fin.transform.localRotation=Quaternion.Euler(0,0,0);
            LocalBox(plane,"Airline livery",new Vector3(0,4.95f,-2),new Vector3(1.2f,.13f,23),navy,false);
            gear=new GameObject("Retractable landing gear").transform;gear.SetParent(plane,false);
            foreach(var wheel in new[]{new Vector3(0,.65f,10),new Vector3(-2,.65f,-3),new Vector3(2,.65f,-3)})
            {LocalPart(gear,"Landing wheel",PrimitiveType.Cylinder,wheel,new Vector3(1.2f,.3f,1.2f),navy).localRotation=Quaternion.Euler(0,0,90);}
            var detailed=plane.GetComponentsInChildren<Renderer>(true);
            var proxy=new GameObject("Distant aircraft").transform;proxy.SetParent(plane,false);
            var hullProxy=new GameObject("Distant fuselage",typeof(MeshFilter),typeof(MeshRenderer));hullProxy.transform.SetParent(proxy,false);hullProxy.GetComponent<MeshFilter>().sharedMesh=fuselage;hullProxy.GetComponent<Renderer>().sharedMaterial=white;
            LocalBox(proxy,"Distant wings",new Vector3(0,2.7f,-2),new Vector3(28,.2f,4),metal,false);
            LocalBox(proxy,"Distant tail",new Vector3(0,6,-14),new Vector3(.3f,6,3),navy,false);
            var lod=plane.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.08f,detailed),new LOD(.005f,proxy.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
            return plane;
        }
        private static Transform Helicopter(Transform root,out Transform rotor,out Transform tail)
        {
            var heli=new GameObject("Haven rescue helicopter").transform;heli.SetParent(root,false);heli.position=new Vector3(600,.12f,-455);var body=heli.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            var collider=heli.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,2,0);collider.size=new Vector3(3,3,7);
            LocalPart(heli,"Helicopter cabin",PrimitiveType.Sphere,new Vector3(0,2.2f,.5f),new Vector3(3,3,6),white);
            LocalPart(heli,"Bubble cockpit",PrimitiveType.Sphere,new Vector3(0,2.4f,2.2f),new Vector3(2.7f,2.4f,2.7f),glass);
            LocalBox(heli,"Tail boom",new Vector3(0,2,-5),new Vector3(.6f,.6f,7),orange,false);
            LocalBox(heli,"Tail fin",new Vector3(0,3,-8),new Vector3(.18f,3,2),navy,false);
            foreach(int side in new[]{-1,1}){LocalBox(heli,"Landing skid",new Vector3(side*1.7f,.3f,0),new Vector3(.15f,.2f,6),metal,false);LocalBox(heli,"Skid strut",new Vector3(side*1.2f,1,0),new Vector3(1.2f,.15f,.15f),metal,false);}
            rotor=new GameObject("Main rotor").transform;rotor.SetParent(heli,false);rotor.localPosition=new Vector3(0,4,0);
            for(int i=0;i<4;i++){var blade=LocalBox(rotor,"Rotor blade",new Vector3(0,0,3),new Vector3(.35f,.07f,6),navy,false);blade.transform.RotateAround(rotor.position,Vector3.up,i*90);}
            tail=new GameObject("Tail rotor").transform;tail.SetParent(heli,false);tail.localPosition=new Vector3(.45f,2.7f,-8);
            for(int i=0;i<2;i++){var blade=LocalBox(tail,"Tail blade",Vector3.zero,new Vector3(.1f,2.8f,.22f),navy,false);blade.transform.localRotation=Quaternion.Euler(i*90,0,0);}
            return heli;
        }
        private static void Person(Transform root,int index,List<Transform> people,List<Transform> legs)
        {
            var person=new GameObject("Boarding passenger "+index).transform;person.SetParent(root,false);person.position=new Vector3(750+(index%3)*.55f,.2f,-282-(index/3)*.65f);people.Add(person);
            LocalPart(person,"Passenger coat",PrimitiveType.Capsule,new Vector3(0,1.1f,0),new Vector3(.48f,.65f,.3f),index%2==0?navy:metal);
            LocalPart(person,"Passenger head",PrimitiveType.Sphere,new Vector3(0,1.75f,0),Vector3.one*.35f,skin);
            foreach(int side in new[]{-1,1}){var pivot=new GameObject("Walking leg pivot").transform;pivot.SetParent(person,false);pivot.localPosition=new Vector3(side*.13f,.8f,0);LocalPart(pivot,"Walking leg",PrimitiveType.Capsule,new Vector3(0,-.35f,0),new Vector3(.15f,.4f,.16f),navy);legs.Add(pivot);}
            foreach(int side in new[]{-1,1})LocalPart(person,"Passenger arm",PrimitiveType.Capsule,new Vector3(side*.32f,1.2f,0),new Vector3(.16f,.38f,.16f),navy);
            LocalBox(person,"Cabin suitcase",new Vector3(.42f,.35f,-.15f),new Vector3(.35f,.6f,.25f),orange,false);
        }
        private static void Fence(Transform root,Vector3 center,float length,bool vertical)
        {
            for(float d=-length*.5f;d<length*.5f;d+=8){var p=center+(vertical?Vector3.forward:Vector3.right)*d;Box(root,"Airport perimeter fence post",p+Vector3.up*1.5f,new Vector3(.15f,3,.15f),metal,true);}
            Box(root,"Airport perimeter mesh",center+Vector3.up*1.5f,vertical?new Vector3(.1f,3,length):new Vector3(length,3,.1f),metal,true);
        }
        private static Mesh MakeFuselage()
        {
            string path=Folder+"/SculptedFuselage.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved!=null)return saved;
            float[] z={-17,-15,-12,-8,5,11,15,17,18};float[] radius={.08f,.8f,1.65f,1.85f,1.85f,1.65f,1.2f,.6f,.02f};const int sides=24;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int row=0;row<z.Length;row++)for(int side=0;side<sides;side++){float a=side*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Cos(a)*radius[row],3.2f+Mathf.Sin(a)*radius[row],z[row]));if(row>0){int p=row*sides+side,q=row*sides+(side+1)%sides;triangles.AddRange(new[]{p-sides,q-sides,p,q-sides,q,p});}}
            var mesh=new Mesh{name="Smooth passenger jet fuselage"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static Mesh MakeWing()
        {
            string path=Folder+"/SweptWing.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved!=null)return saved;
            var v=new[]{new Vector3(1,2.8f,3),new Vector3(14,2.9f,-5),new Vector3(14,2.9f,-7),new Vector3(1,2.8f,-5),new Vector3(1,2.5f,3),new Vector3(14,2.7f,-5),new Vector3(14,2.7f,-7),new Vector3(1,2.5f,-5)};
            var mesh=new Mesh{name="Swept airfoil wing",vertices=v,triangles=new[]{0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0}};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        private static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material mat,bool solid)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
        private static GameObject LocalBox(Transform parent,string name,Vector3 p,Vector3 size,Material mat,bool solid)
        {var go=Box(parent,name,Vector3.zero,size,mat,solid);go.transform.localPosition=p;return go;}
        private static Transform Part(Transform parent,string name,PrimitiveType type,Vector3 p,Vector3 size,Material mat,bool solid)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;}
        private static Transform LocalPart(Transform parent,string name,PrimitiveType type,Vector3 p,Vector3 size,Material mat){var t=Part(parent,name,type,Vector3.zero,size,mat,false);t.localPosition=p;return t;}
        private static TMP_Text Text(Transform root,string value,Vector3 p,float size,float width,Color color)
        {var text=new GameObject(value).AddComponent<TextMeshPro>();text.transform.SetParent(root,false);text.transform.position=p;text.text=value;text.font=TMP_Settings.GetFontAsset();text.fontSize=size*10;text.alignment=TextAlignmentOptions.Center;text.color=color;text.rectTransform.sizeDelta=new Vector2(width,size*2);text.raycastTarget=false;return text;}
        private static Material Mat(string name,Color color,float smooth,bool emission=false)
        {string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Cull",0);m.enableInstancing=true;if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*2);}EditorUtility.SetDirty(m);return m;}
        private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace((char)92,'/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    }
}