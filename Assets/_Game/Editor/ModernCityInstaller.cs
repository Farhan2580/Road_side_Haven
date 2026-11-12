using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallModernCity(Transform motel)
        {
            var oldCity=motel.Find("Regional World 2km v1/City Zone - West");
            if(oldCity!=null){if(!Application.isPlaying)Undo.RecordObject(oldCity.gameObject,"Upgrade generated city");oldCity.gameObject.SetActive(false);}
            if(motel.Find("Modern Downtown v1")!=null)return;
            var root=new GameObject("Modern Downtown v1").transform;root.SetParent(motel,false);
            if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Build modern downtown");
            var concrete=Material("Downtown limestone",new Color(.64f,.67f,.68f));
            var plaza=Material("Downtown plaza stone",new Color(.43f,.48f,.50f));
            var silver=Material("Downtown brushed metal",new Color(.35f,.41f,.46f));
            var canopy=Material("Restaurant cream",new Color(.92f,.84f,.65f));
            Solid(root,"Downtown district paving",new Vector3(-670,-.055f,310),new Vector3(590,.06f,610),plaza);
            foreach(float x in ModernCityLayout.StreetX)
                Road(root,new Vector3(x,0,318.5f),new Vector3(18,.12f,663));
            foreach(float z in ModernCityLayout.StreetZ)
                Road(root,new Vector3(-670,0,z),new Vector3(578,.12f,18));
            // Road markings and sidewalks stay within the reserved road grid.
            foreach(float x in ModernCityLayout.StreetX)
            {
                for(int z=0;z<=630;z+=24)
                    if(new[]{20,160,300,460,600}.All(j=>Mathf.Abs(j-z)>13))
                        Box(root,"Downtown lane marking",new Vector3(x,.071f,z),new Vector3(.18f,.015f,9),ivory);
            }
            foreach(float z in ModernCityLayout.StreetZ)
                for(int x=-935;x<-390;x+=24)
                    if(new[]{-950,-810,-670,-530,-390}.All(j=>Mathf.Abs(j-x)>13))
                        Box(root,"Downtown lane marking",new Vector3(x,.071f,z),new Vector3(9,.015f,.18f),ivory);
            foreach(float x in new[]{-880f,-740f,-600f,-460f})
                foreach(float z in new[]{90f,230f,380f,530f})
                    Solid(root,"City block sidewalk",new Vector3(x,.025f,z),new Vector3(116,.14f,z==380?136:116),concrete);
            var facadeTexture=WindowTexture("DowntownWindows",false);
            var emissionTexture=WindowTexture("DowntownWindowEmission",true);
            var facadeMaterials=new Material[8];
            for(int i=0;i<facadeMaterials.Length;i++)
            {
                var material=Material("Modern glass facade "+i,i%2==0?new Color(.45f,.67f,.80f):new Color(.47f,.57f,.64f));
                material.SetTexture("_BaseMap",facadeTexture);material.SetTexture("_EmissionMap",emissionTexture);
                material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",Color.black);
                material.SetFloat("_Metallic",.55f);material.SetFloat("_Smoothness",.85f);
                material.SetTextureScale("_BaseMap",new Vector2(.75f,(60+i*18)/32f));
                material.SetTextureScale("_EmissionMap",new Vector2(.75f,(60+i*18)/32f));
                EditorUtility.SetDirty(material);facadeMaterials[i]=material;
            }
            int towers=0;
            foreach(var plot in ModernCityLayout.Towers())
            {
                ModernTower(root,new Vector3(plot.X,0,plot.Z),plot.Height,plot.Style,facadeMaterials[plot.Style],concrete,silver,plot.Landmark);
                towers++;
            }
            Campus(root,new Vector3(-880,0,90),"DOWNTOWN SCHOOL",65,8,ivory);
            Campus(root,new Vector3(-880,0,230),"CITY UNIVERSITY",70,14,concrete);
            // Park leaves the existing bank at (-730,420) clear.
            Box(root,"Downtown park lawn",new Vector3(-740,.115f,375),new Vector3(82,.03f,65),leaf);
            Solid(root,"Fountain base",new Vector3(-740,.3f,375),new Vector3(14,.5f,14),concrete);
            Box(root,"Fountain water",new Vector3(-740,.58f,375),new Vector3(12,.06f,12),glass);
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4;
                Tree(root,new Vector3(-740+Mathf.Cos(angle)*32,0,375+Mathf.Sin(angle)*23));
            }
            for(int i=0;i<4;i++)
                Solid(root,"Garden bench",new Vector3(-769+i*19,.5f,348),new Vector3(4,.5f,1.2f),timber);
            string[] restaurants={"EMBER GRILL","CITY CAFE","NOODLE HOUSE","SKYLINE PIZZA","GREEN BOWL","NIGHT BISTRO"};
            for(int i=0;i<6;i++)
            {
                Vector3 position=new Vector3(-635+i%3*34,0,i<3?195:265);
                ModernRestaurant(root,position,restaurants[i],i,canopy,facadeMaterials[1]);
            }
            // Retail plaza next to the existing fast-food franchise; no tower overlaps it.
            ModernRestaurant(root,new Vector3(-485,0,230),"DOWNTOWN MARKET",2,concrete,facadeMaterials[0]);
            ModernRestaurant(root,new Vector3(-485,0,270),"OFFICE CANTEEN",3,canopy,facadeMaterials[2]);
            for(int i=0;i<10;i++)
            {
                Vector3 pos=new Vector3(-630+i%5*15,0,i<5?223:242);
                Solid(root,"Outdoor dining table",pos+Vector3.up*.8f,new Vector3(2,.12f,2),timber);
                for(int side=-1;side<=1;side+=2)Solid(root,"Cafe seat",pos+new Vector3(side*1.5f,.45f,0),new Vector3(.7f,.85f,.8f),mint);
            }
            for(int i=0;i<14;i++)
            {
                Vector3 pos=new Vector3(-925+i%7*78,0,i<7?43:577);
                Plant(root,pos,1.8f);
            }
            LabelWorld(root,"FINANCIAL DISTRICT / WEST",new Vector3(-570,4,36),9);
            // Keep the City Shop portfolio marker at the new retail frontage, not inside the school.
            var shopBoard=motel.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Property board city-shop");
            if(shopBoard!=null)shopBoard.position=new Vector3(-485,2.6f,219);
            AssetDatabase.SaveAssets();
            Debug.Log($"Modern downtown installed: {towers} office towers (60-230m), 8 restaurants/retail venues, university, school and central park. Existing city roads are replaced by a connected boulevard grid.");
        }

        private static void ModernTower(Transform parent,Vector3 position,float height,int style,Material facade,Material stone,Material metal,bool landmark)
        {
            var tower=new GameObject(landmark?"HAVEN ONE - 230m landmark":"Office tower "+style).transform;tower.SetParent(parent,false);tower.position=position;
            var renderers=new List<Renderer>();
            float width=style%2==0?24:20,depth=style%3==0?28:22;
            GameObject podium;
            if(landmark)
            {
                float halfW=(width+8)*.5f,halfD=(depth+8)*.5f;
                podium=Solid(tower,"Office lobby floor",position+Vector3.up*.12f,new Vector3(width+8,.16f,depth+8),stone);
                renderers.Add(podium.GetComponent<Renderer>());
                var back=Solid(tower,"Office lobby back wall",position+new Vector3(0,2.5f,halfD),new Vector3(width+8,5,.25f),stone);renderers.Add(back.GetComponent<Renderer>());
                for(int side=-1;side<=1;side+=2)
                {
                    var wall=Solid(tower,"Office lobby side wall",position+new Vector3(side*halfW,2.5f,0),new Vector3(.25f,5,depth+8),stone);renderers.Add(wall.GetComponent<Renderer>());
                    float pierWidth=halfW-3;
                    var pier=Solid(tower,"Office lobby entrance pier",position+new Vector3(side*(3+pierWidth*.5f),2.5f,-halfD),new Vector3(pierWidth,5,.25f),stone);renderers.Add(pier.GetComponent<Renderer>());
                }
                Solid(tower,"Lobby reception",position+new Vector3(0,.8f,halfD-4),new Vector3(8,1.4f,1.1f),timber);
                Box(tower,"Lamp shade",position+new Vector3(-5,3,-halfD-1),new Vector3(.4f,.3f,.3f),gold);
                Box(tower,"Lamp shade",position+new Vector3(5,3,-halfD-1),new Vector3(.4f,.3f,.3f),gold);
            }
            else { podium=Solid(tower,"Office podium",position+Vector3.up*2.5f,new Vector3(width+8,5,depth+8),stone);renderers.Add(podium.GetComponent<Renderer>()); }
            var body=Solid(tower,"Modern tower facade",position+Vector3.up*(height*.5f+5),new Vector3(width,height,depth),facade);renderers.Add(body.GetComponent<Renderer>());
            var roof=Solid(tower,"Office roof cap",position+Vector3.up*(height+5.4f),new Vector3(width+1,.8f,depth+1),metal);renderers.Add(roof.GetComponent<Renderer>());
            for(int band=1;band<=5;band++)
            {
                var trim=Solid(tower,"Facade marking",position+Vector3.up*(5+height*band/6),new Vector3(width+.25f,.22f,depth+.25f),metal);
                // Facade trim does not need its own physical collider over the solid body.
                var collider=trim.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
                renderers.Add(trim.GetComponent<Renderer>());
            }
            for(int edge=-1;edge<=1;edge+=2)
            {
                var rib=Solid(tower,"Facade marking",position+new Vector3(edge*width*.5f,height*.5f+5,-depth*.5f-.1f),new Vector3(.3f,height,.3f),metal);
                var collider=rib.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Object.Destroy(collider);else Object.DestroyImmediate(collider);
                renderers.Add(rib.GetComponent<Renderer>());
            }
            if(!landmark)Box(tower,"Office entrance glass",position+new Vector3(0,2,-depth*.5f-4.05f),new Vector3(4,3.5f,.08f),glass);
            Box(tower,"Entrance canopy",position+new Vector3(0,4,-depth*.5f-6),new Vector3(10,.22f,5),metal);
            if(landmark)
            {
                Solid(tower,"Landmark crown",position+Vector3.up*(height+10),new Vector3(width*.6f,10,depth*.6f),metal);
                LabelWorld(tower,"HAVEN ONE",position+new Vector3(0,5,-depth*.5f-7),5);
            }
            var lod=tower.gameObject.AddComponent<LODGroup>();
            var simple=new[]{body.GetComponent<Renderer>(),podium.GetComponent<Renderer>(),roof.GetComponent<Renderer>()};
            lod.SetLODs(new[]{new LOD(.10f,renderers.ToArray()),new LOD(.015f,simple),new LOD(.002f,new[]{body.GetComponent<Renderer>()})});lod.RecalculateBounds();
        }

        private static void ModernRestaurant(Transform parent,Vector3 position,string name,int style,Material wall,Material facade)
        {
            Solid(parent,"Restaurant floor",position+Vector3.up*.12f,new Vector3(26,.16f,18),wall);
            Solid(parent,"Restaurant back wall",position+new Vector3(0,3,9),new Vector3(26,6,.25f),wall);
            Solid(parent,"Restaurant roof",position+Vector3.up*6.15f,new Vector3(26,.3f,18),ink);
            for(int side=-1;side<=1;side+=2)
            {
                Solid(parent,"Restaurant side wall",position+new Vector3(side*13,3,0),new Vector3(.25f,6,18),wall);
                Solid(parent,"Restaurant entrance pier",position+new Vector3(side*7.75f,3,-9),new Vector3(10.5f,6,.25f),wall);
                Box(parent,"Modern restaurant glass",position+new Vector3(side*7.75f,2.7f,-9.16f),new Vector3(10.25f,4.6f,.08f),facade);
                Box(parent,"Lamp shade",position+new Vector3(side*3.1f,2.8f,-9.3f),new Vector3(.3f,.35f,.3f),gold);
                for(int row=-1;row<=1;row+=2)
                {
                    Solid(parent,"Indoor dining table",position+new Vector3(side*7,.9f,row*3),new Vector3(2,.15f,2),timber);
                    Solid(parent,"Restaurant chair",position+new Vector3(side*9,.5f,row*3),new Vector3(.8f,1,.8f),mint);
                }
            }
            Solid(parent,"Restaurant service counter",position+new Vector3(0,1,6),new Vector3(10,1.8f,1.2f),timber);
            Box(parent,"Restaurant awning",position+new Vector3(0,5.2f,-11),new Vector3(28,.25f,5),style%2==0?red:mint);
            LabelWorld(parent,name,position+new Vector3(0,6.8f,-9.2f),5);
        }

        private static Texture2D WindowTexture(string name,bool emissive)
        {
            string path=MaterialFolder+"/"+name+".png";
            var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing!=null)return existing;
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                int px=x%16,py=y%16;bool pane=px>2&&px<14&&py>3&&py<14;
                bool lit=((x/16)*13+(y/16)*7)%5<2;
                Color color=emissive?(pane&&lit?Color.white:Color.black):pane?Color.Lerp(new Color(.14f,.27f,.36f),new Color(.48f,.67f,.75f),(float)py/16):new Color(.10f,.14f,.18f);
                texture.SetPixel(x,y,color);
            }
            File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Bilinear;importer.mipmapEnabled=true;importer.sRGBTexture=!emissive;importer.maxTextureSize=128;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
