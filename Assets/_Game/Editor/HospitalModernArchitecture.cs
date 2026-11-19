using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven.Editor
{
    public static partial class HospitalCampusBuilder
    {
        public const string ModernFolder="Assets/_Game/Generated/HospitalModern";
        static Material bronze,limestone,blueGlass,darkPanel,waterSurface,softGlow;
        static readonly List<Transform> poolRipples=new List<Transform>();
        [MenuItem("Roadside Haven/Hospital/Apply Modern Architecture")]
        public static void ModernizeCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            CreateModernArchitecture();var campus=UnityEngine.Object.FindFirstObjectByType<HospitalCampusLife>();
            if(campus!=null){HospitalArchitecture.Apply(campus);EditorSceneManager.MarkSceneDirty(campus.gameObject.scene);}Focus();
        }
        public static GameObject CreateModernArchitecture()
        {
            if(!AssetDatabase.IsValidFolder(ModernFolder))AssetDatabase.CreateFolder("Assets/_Game/Generated","HospitalModern");
            activeFolder=ModernFolder;temporary.Clear();cubes.Clear();batches.Clear();moving.Clear();bulbs.Clear();poolRipples.Clear();serial=0;Materials();
            bronze=Mat("Champagne anodised aluminium",new Color(.59f,.42f,.23f));bronze.SetFloat("_Metallic",.72f);bronze.SetFloat("_Smoothness",.65f);
            limestone=Mat("Ivory limestone",new Color(.76f,.71f,.60f),"Stucco");darkPanel=Mat("Midnight ceramic panels",new Color(.035f,.065f,.085f));darkPanel.SetFloat("_Metallic",.25f);
            blueGlass=Mat("Solar blue curtain wall",new Color(.065f,.22f,.29f,.48f));blueGlass.SetFloat("_Surface",1);blueGlass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);blueGlass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);blueGlass.SetFloat("_ZWrite",0);blueGlass.SetFloat("_Cull",0);blueGlass.SetFloat("_Metallic",.35f);blueGlass.SetFloat("_Smoothness",.95f);blueGlass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");blueGlass.SetShaderPassEnabled("ShadowCaster",false);blueGlass.renderQueue=3000;
            softGlow=Mat("Warm architectural LED",new Color(1,.84f,.57f));softGlow.EnableKeyword("_EMISSION");softGlow.SetColor("_EmissionColor",new Color(1,.71f,.35f)*1.8f);
            waterSurface=Mat("Reflecting pool water",new Color(.045f,.23f,.28f));waterSurface.SetFloat("_Metallic",.6f);waterSurface.SetFloat("_Smoothness",.96f);
            foreach(var m in new[]{bronze,limestone,darkPanel,blueGlass,softGlow,waterSurface})EditorUtility.SetDirty(m);
            var root=Group(null,HospitalArchitecture.Layer,Vector3.zero);
            try{
                var exterior=Group(root,"Sculpted medical architecture",HospitalCampusLayout.Centre);batches.Add(exterior);
                var plaza=Group(root,"Modern arrival gardens",HospitalCampusLayout.Centre);batches.Add(plaza);
                ModernShell(exterior);for(int f=0;f<6;f++)Stairs(exterior,f);Roof(exterior);RoofGarden(exterior);ArrivalGarden(plaza);
                root.gameObject.AddComponent<HospitalArchitecture>().Configure(poolRipples.ToArray());Combine(root);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/_Game/Resources/"+HospitalArchitecture.Resource+".prefab");AssetDatabase.SaveAssets();return prefab;
            }finally{
                UnityEngine.Object.DestroyImmediate(root.gameObject);foreach(var m in temporary)if(m!=null&&!AssetDatabase.Contains(m))UnityEngine.Object.DestroyImmediate(m);activeFolder=Folder;
            }
        }
        static void ModernShell(Transform p)
        {
            const float height=HospitalCampusLayout.Storey*6;
            // The furnished plan remains rectangular; the skin reads as three distinct architectural volumes.
            for(int f=0;f<6;f++){
                float y=f*HospitalCampusLayout.Storey;
                Box(p,"North clinical glazing",new Vector3(0,y+2.5f,23),new Vector3(88,4.6f,.15f),blueGlass,true);
                Box(p,"North sill",new Vector3(0,y+.48f,23),new Vector3(88,.7f,.25f),white,true);
                Box(p,"North floating floor band",new Vector3(0,y+4.65f,23.2f),new Vector3(88,.24f,.7f),white);
                foreach(int side in new[]{-1,1}){
                    if(f>0||side<0)Box(p,"Side clinical glazing",new Vector3(side*44,y+2.5f,0),new Vector3(.16f,4.6f,46),blueGlass,true);
                    Box(p,"Side shadow reveal",new Vector3(side*44.1f,y+4.65f,0),new Vector3(.5f,.23f,46.5f),darkPanel);
                    for(int z=-21;z<23;z+=4){if(f==0&&side>0&&z< -11)continue;Box(p,"Side solar screen fin",new Vector3(side*44.3f,y+2.45f,z),new Vector3(.7f,4.4f,.08f),side<0?bronze:white);}
                }
                // West wing: warm limestone end and slender champagne vertical fins.
                Box(p,"West wing front glass",new Vector3(-27,y+2.5f,-23),new Vector3(20,4.5f,.15f),blueGlass,true);
                Box(p,"West wing limestone pier",new Vector3(-40,y+2.45f,-23.3f),new Vector3(8,4.8f,.65f),limestone,true);
                Box(p,"West wing sill",new Vector3(-26,y+.48f,-23),new Vector3(22,.72f,.25f),white,true);
                for(float x=-35;x<=-17;x+=3)Box(p,"West champagne sun fin",new Vector3(x,y+2.4f,-23.65f),new Vector3(.10f,4.55f,1.1f),bronze);
                // East wing: darker framed glazing, wide terrace bands and a rhythm distinct from the west.
                Box(p,"East wing front glass",new Vector3(30,y+2.5f,-23),new Vector3(28,4.5f,.15f),blueGlass,true);
                Box(p,"East wing sill",new Vector3(30,y+.48f,-23),new Vector3(28,.72f,.25f),darkPanel,true);
                Box(p,"East wing terrace blade",new Vector3(30,y+4.7f,-24),new Vector3(29,.26f,2.5f),white);
                Box(p,"East wing warm edge light",new Vector3(30,y+4.56f,-25.26f),new Vector3(28,.045f,.045f),softGlow);
                for(float x=18;x<44;x+=4)Box(p,"East facade mullion",new Vector3(x,y+2.6f,-23.18f),new Vector3(.065f,4.5f,.1f),metal);
                if(f>0){float inset=f%2==0?-.4f:.4f;for(int i=0;i<5;i++)BalconyPlanter(p,new Vector3(19+i*5.2f,y+.2f,-24.2f+inset));}
                if(f==0){
                    foreach(int s in new[]{-1,1})Box(p,"Atrium entrance fixed glazing",new Vector3(s*9.7f,2.4f,-23),new Vector3(12.6f,4.5f,.12f),glass,true);
                }else {Box(p,"Inner atrium safety glazing",new Vector3(0,y+2.5f,-23),new Vector3(32,4.6f,.12f),glass,true);CurvedAtrium(p,y);}
                Box(p,"Central clinical transom",new Vector3(0,y+4.65f,-23),new Vector3(32,.22f,.28f),darkPanel);
            }
            // The emergency entrance is kept open and large enough for stretchers.
            Box(p,"ER east glazing north",new Vector3(44,2.5f,6),new Vector3(.16f,4.6f,34),blueGlass,true);
            Box(p,"ER east glazing return",new Vector3(44,2.5f,-21.5f),new Vector3(.16f,4.6f,3),blueGlass,true);
            Box(p,"ER fixed vestibule glazing",new Vector3(44,2.5f,-19.2f),new Vector3(.16f,4.6f,1.6f),glass,true);
            Box(p,"ER fixed vestibule glazing",new Vector3(44,2.5f,-12.3f),new Vector3(.16f,4.6f,2.6f),glass,true);
            var entry=Group(p,"Automatic main entrance",new Vector3(0,.12f,-23));Door(entry,3.4f);
            var emergency=Group(p,"Automatic emergency entrance",new Vector3(44,.12f,-16),90);Door(emergency,2.4f);
            Box(p,"West porcelain crown",new Vector3(-28,height+.6f,23.2f),new Vector3(32,.55f,1.4f),white);
            Box(p,"West outer crown return",new Vector3(-44.3f,height+.6f,-.2f),new Vector3(.9f,.55f,47),white);
            Box(p,"West wrapping frame left",new Vector3(-44.6f,17,-23.8f),new Vector3(1.1f,23,1.4f),white);
            Box(p,"West wrapping frame top",new Vector3(-28,28.2f,-23.8f),new Vector3(34,1.1f,1.4f),white);
            Box(p,"East ceramic end pier",new Vector3(44.1f,14.5f,-23.6f),new Vector3(1.4f,29,1.1f),darkPanel);
            Box(p,"West atrium junction pier",new Vector3(-16.5f,14.4f,-23.1f),new Vector3(1.2f,28.8f,.3f),bronze,true);
            // Faceted structural braces create depth without covering the entrance or any corridor.
            for(int section=0;section<3;section++){
                float y=5.2f+section*7.2f;Beam(p,"East sculptural facade brace",new Vector3(17,y,-25.4f),new Vector3(29,y+6.7f,-25.4f),.22f,bronze);
                Beam(p,"East sculptural facade brace",new Vector3(29,y+6.7f,-25.4f),new Vector3(43,y,-25.4f),.22f,bronze);
            }
            // A quiet vertical landmark replaces a building-wide floating title.
            Box(p,"Medical landmark blade",new Vector3(-40,17,-23.8f),new Vector3(6.5f,25,.45f),darkPanel);
            Cross(p,new Vector3(-40,25,-24.07f),3.1f,softGlow);
            Label(p,"HAVEN\nGRAND\nMEDICAL",new Vector3(-40,18.5f,-24.08f),5.3f,5.4f,0);
            Label(p,"24 / 7",new Vector3(-40,11.9f,-24.08f),4,1,0);
            FreeformCanopy(p);
            var er=Group(p,"Sculpted emergency porte cochere",new Vector3(50,0,-16));
            Box(er,"ER floating roof",new Vector3(5,4.1f,0),new Vector3(16,.27f,12),white,true);Box(er,"ER roof warm soffit",new Vector3(5,3.92f,0),new Vector3(15.5f,.08f,11.5f),wood);
            foreach(float z in new[]{-4f,4f})Beam(er,"ER tapered support",new Vector3(11.5f,.12f,z),new Vector3(13,4,z),.2f,bronze,true);
            Label(er,"EMERGENCY  /  AMBULANCES",new Vector3(5,3.5f,-6.16f),15,.7f,0,red);
            Label(p,"MAIN ENTRANCE",new Vector3(0,3.8f,-23.25f),12,.75f,0,teal);
        }
        static void CurvedAtrium(Transform p,float y)
        {
            const int segments=20;float bottom=y+.25f,top=y+4.55f;
            for(int i=0;i<segments;i++){
                float a=Mathf.PI*i/segments,b=Mathf.PI*(i+1)/segments;
                var left=new Vector3(Mathf.Cos(a)*16,0,-23-Mathf.Sin(a)*5.4f);var right=new Vector3(Mathf.Cos(b)*16,0,-23-Mathf.Sin(b)*5.4f);
                var panel=Box(p,"Curved atrium glazing",(left+right)*.5f+Vector3.up*((bottom+top)*.5f),new Vector3((left-right).magnitude,top-bottom,.08f),blueGlass);
                panel.transform.localRotation=Quaternion.LookRotation(Vector3.Cross(right-left,Vector3.up));
                Beam(p,"Atrium champagne rib",left+Vector3.up*bottom,left+Vector3.up*top,.055f,bronze);
                Beam(p,"Atrium floating rim",left+Vector3.up*(top+.1f),right+Vector3.up*(top+.1f),.12f,white);
            }
        }
        static void FreeformCanopy(Transform p)
        {
            const int across=40,depth=8;var vertices=new List<Vector3>();var tris=new List<int>();var uv=new List<Vector2>();
            for(int layer=0;layer<2;layer++)for(int z=0;z<=depth;z++)for(int x=0;x<=across;x++){
                float u=(float)x/across*2-1,v=(float)z/depth;float front=-33.8f+1.2f*Mathf.Cos(u*Mathf.PI*.5f);
                float height=5.05f+1.05f*(1-u*u)+.35f*Mathf.Sin(v*Mathf.PI);vertices.Add(new Vector3(u*22,height-layer*.22f,Mathf.Lerp(front,-21.7f,v)));uv.Add(new Vector2(u*22,v*12));
                if(x>0&&z>0){int d=vertices.Count-1,c=d-1,b=d-across-1,a=b-1;if(layer==0)tris.AddRange(new[]{a,d,b,a,c,d});else tris.AddRange(new[]{a,b,d,a,d,c});}
            }
            MeshPart(p,"Sweeping porcelain entrance canopy",vertices,tris,uv,white);
            var frontLine=new List<Vector3>();for(int i=0;i<=40;i++){float u=i/40f*2-1;frontLine.Add(new Vector3(u*22,5.05f+1.05f*(1-u*u)-.13f,-33.83f+1.2f*Mathf.Cos(u*Mathf.PI*.5f)));}
            for(int i=1;i<frontLine.Count;i++)Beam(p,"Canopy champagne edge",frontLine[i-1],frontLine[i],.2f,bronze);
            for(int x=-20;x<=20;x++){
                float u=x/22f,y=5.05f+1.05f*(1-u*u)-.26f;float front=-33.5f+1.2f*Mathf.Cos(u*Mathf.PI*.5f);
                Beam(p,"Canopy cedar soffit rib",new Vector3(x,y,front),new Vector3(x,y,-22),.075f,wood);
                if(x%4==0)Beam(p,"Recessed canopy LED",new Vector3(x,y-.06f,front+.3f),new Vector3(x,y-.06f,-22.5f),.04f,softGlow);
            }
            foreach(int side in new[]{-1,1}){
                var foot=new Vector3(side*14,.12f,-30.7f);Beam(p,"Entrance wishbone column",foot,new Vector3(side*19,5.4f,-27),.27f,metal,true);Beam(p,"Entrance wishbone column",foot,new Vector3(side*9,5.75f,-25.5f),.27f,metal,true);
            }
            Label(p,"HAVEN GRAND MEDICAL CENTRE",new Vector3(0,5.55f,-32.7f),19,.8f,0,darkPanel);
        }
        static GameObject MeshPart(Transform p,string name,List<Vector3> vertices,List<int> tris,List<Vector2> uv,Material mat)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();temporary.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(p,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go;
        }
        static void BalconyPlanter(Transform p,Vector3 at)
        {
            Box(p,"Terrace bronze planter",at+Vector3.up*.28f,new Vector3(3.8f,.55f,.6f),bronze);
            for(int i=0;i<3;i++)Shape(p,"Facade terrace greenery",at+new Vector3((i-1)*1.2f,.65f,0),new Vector3(1.7f,.6f,.7f),leaf);
        }
        static void RoofGarden(Transform p)
        {
            float y=HospitalCampusLayout.Ground+6*HospitalCampusLayout.Storey;
            Ground(p,"Roof healing terrace decking",Rect.MinMaxRect(-41,5,-18,21),y+.04f,wood);
            foreach(float z in new[]{6f,20f}){Box(p,"Roof terrace planting edge",new Vector3(-29,y+.4f,z),new Vector3(22,.7f,1),white,true);for(int i=0;i<10;i++)Shape(p,"Rooftop healing garden shrubs",new Vector3(-39+i*2.1f,y+.95f,z),new Vector3(2.2f,.9f,1.4f),leaf);}
            for(int i=0;i<2;i++)Bench(p,new Vector3(-34+i*9,y+.08f,10),0);
            for(int x=-38;x<=-20;x+=2){Beam(p,"Rooftop pergola rib",new Vector3(x,y+3.5f,7),new Vector3(x,y+3.5f,18),.09f,bronze);}
            foreach(int x in new[]{-39,-19})foreach(int z in new[]{7,18})Box(p,"Rooftop pergola column",new Vector3(x,y+1.7f,z),new Vector3(.12f,3.4f,.12f),bronze,true);
            // Crown elements stay beyond the complete 30 m air-ambulance landing square.
            Box(p,"North-west floating roof crown",new Vector3(-29,y+3.5f,22),new Vector3(31,.4f,2.2f),white);
            Box(p,"East floating roof crown",new Vector3(43,y+3.5f,0),new Vector3(2,.35f,46),white);
        }
        static void ArrivalGarden(Transform p)
        {
            foreach(int side in new[]{-1,1}){
                float x=side*30;var pool=Group(p,"Reflecting pool "+side,new Vector3(x,0,-27.7f));
                Oval(pool,"Reflecting water surface",new Vector3(0,.145f,0),new Vector2(7,2.8f),0,waterSurface);
                Ring(pool,"Pool limestone coping",new Vector3(0,.24f,0),new Vector2(7.5f,3.25f),.35f,limestone,true);
                Ring(pool,"Pool recessed edge light",new Vector3(0,.255f,0),new Vector2(7.15f,2.9f),.045f,softGlow);
                for(int i=0;i<3;i++){var g=Group(pool,"Animated pool ripple",new Vector3((i-1)*1.5f,.151f,0));Ring(g,"Water ripple",Vector3.zero,new Vector2(2.1f,1.2f),.025f,light);moving.Add(g);poolRipples.Add(g);}
                var treeAt=new Vector3(side*48,.12f,-27);ModernPalm(p,treeAt);Planter(p,treeAt,new Vector2(4,4));
                Bench(p,new Vector3(side*43,.12f,-26),side<0?90:-90);
                Planter(p,new Vector3(side*20,.12f,-29),new Vector2(2,3));
            }
            // Inlay follows the centre walk and stops before the public road.
            for(int i=0;i<7;i++)Box(p,"Entrance terrazzo inlay",new Vector3(0,.124f,-24-i),new Vector3(11,.003f,.08f),bronze);
            for(float z=-39.5f;z< -32;z+=1)Box(p,"Hospital entrance crossing paint",new Vector3(0,.063f,z),new Vector3(4,.002f,.5f),white);
            var garden=Group(p,"Garden arcade",new Vector3(-70,0,-25));
            Ground(garden,"Covered garden walk",Rect.MinMaxRect(-20,-3,20,0),.13f,tile);
            for(int i=0;i<7;i++){float x=-18+i*6;Beam(garden,"Garden arcade slender column",new Vector3(x,.12f,-2.7f),new Vector3(x,3.4f,-2.7f),.12f,bronze,true);Beam(garden,"Garden arcade cantilever",new Vector3(x,3.4f,-3),new Vector3(x,3.4f,1),.15f,bronze);}
            for(int i=0;i<17;i++)Box(garden,"Garden arcade cedar louvre",new Vector3(-20+i*2.5f,3.5f,-1),new Vector3(.14f,.12f,4),wood);
            Label(p,"PATIENT GARDEN",new Vector3(-57,1.5f,-27),5,.65f,0,teal);
        }
        static void ModernPalm(Transform p,Vector3 at)
        {
            Beam(p,"Arrival palm trunk",at,at+new Vector3(.45f,6.8f,0),.24f,wood,true);
            for(int i=0;i<9;i++){
                float a=i*Mathf.PI*2/9;var centre=at+new Vector3(.45f,6.8f,0);var end=centre+new Vector3(Mathf.Cos(a)*3.8f,-.9f,Mathf.Sin(a)*3.8f);
                var v=new List<Vector3>{centre,Vector3.Lerp(centre,end,.5f)+Vector3.up*.9f+new Vector3(-Mathf.Sin(a)*.6f,0,Mathf.Cos(a)*.6f),end,Vector3.Lerp(centre,end,.5f)+Vector3.up*.9f-new Vector3(-Mathf.Sin(a)*.6f,0,Mathf.Cos(a)*.6f)};
                v.AddRange(v.ToArray());MeshPart(p,"Palm sculpted frond",v,new List<int>{0,1,2,0,2,3,4,6,5,4,7,6},new List<Vector2>{Vector2.zero,Vector2.up,Vector2.one,Vector2.right,Vector2.zero,Vector2.up,Vector2.one,Vector2.right},leaf);
            }
        }
        static void Oval(Transform p,string name,Vector3 at,Vector2 radius,float inner,Material mat)
        {
            const int n=80;var v=new List<Vector3>();var t=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<=n;i++){float a=i*Mathf.PI*2/n;foreach(float scale in new[]{inner,1f}){var point=at+new Vector3(Mathf.Cos(a)*radius.x*scale,0,Mathf.Sin(a)*radius.y*scale);v.Add(point);uv.Add(new Vector2(point.x,point.z));}if(i>0){int d=i*2;t.AddRange(new[]{d-2,d+1,d-1,d-2,d,d+1});}}
            MeshPart(p,name,v,t,uv,mat);
        }
        static void Ring(Transform p,string name,Vector3 at,Vector2 radius,float width,Material mat,bool solid=false)
        {
            Oval(p,name,at,radius,Mathf.Max(.01f,1-width/Mathf.Min(radius.x,radius.y)),mat);
            if(solid){foreach(int side in new[]{-1,1}){Box(p,"Reflecting pool low curb",at+new Vector3(side*6.8f,-.08f,0),new Vector3(.35f,.26f,3.6f),mat,true);Box(p,"Reflecting pool low curb",at+new Vector3(0,-.08f,side*2.65f),new Vector3(11,.26f,.35f),mat,true);}}
        }
    }
}
