using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private const string TerrainFolder="Assets/_Game/Generated/Terrain";
        private static void InstallLayeredGrass(Transform motel)
        {
            if(!AssetDatabase.IsValidFolder(TerrainFolder))AssetDatabase.CreateFolder("Assets/_Game/Generated","Terrain");
            var geometry=motel.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.gameObject.activeInHierarchy).ToArray();
            var grounds=geometry.Where(r=>GroundSurfaceStyle.SurfaceFor(r.name)==0).Select(r=>r.bounds).ToArray();if(grounds.Length==0)return;
            var largest=grounds.OrderByDescending(b=>b.size.x*b.size.z).First();var world=Rect.MinMaxRect(largest.min.x,largest.min.z,largest.max.x,largest.max.z);
            var roads=new List<Rect>();var buildings=new List<Rect>();var rocks=new List<Rect>();
            foreach(var renderer in geometry)
            {
                var b=renderer.bounds;var rect=Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z);int kind=GroundSurfaceStyle.SurfaceFor(renderer.name);string name=renderer.name.ToLowerInvariant();
                if(kind==1)roads.Add(rect);else if(kind==4)rocks.Add(rect);
                else if(kind<0&&renderer.GetComponentInParent<Rigidbody>()==null&&b.size.y>2.5f&&b.size.x>4&&b.size.z>3&&!name.Contains("river")&&!name.Contains("culvert")&&!name.Contains("bridge")&&!name.Contains("lake")&&!name.Contains("willow")&&!name.Contains("cattail")&&!name.Contains("tree")&&!name.Contains("crown")&&!name.Contains("roof")&&!name.Contains("boundary")&&!name.Contains("sign")&&!name.Contains("canopy")&&!name.Contains("water")&&!name.Contains("portal"))buildings.Add(rect);
            }
            string[] names={"BaseGrass","DryGrass","DirtPebbles","RockyGravel"};var layers=new TerrainLayer[4];float[] sizes={15,12,8,10};
            for(int i=0;i<4;i++)
            {
                string path=TerrainFolder+"/"+names[i]+".terrainlayer";var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if(layer==null){layer=new TerrainLayer{name=names[i]};AssetDatabase.CreateAsset(layer,path);}
                layer.diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/"+names[i]+"_Albedo.png");
                layer.normalMapTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/"+names[i]+"_Normal.png");
                layer.tileSize=Vector2.one*sizes[i];layer.normalScale=i==0?.28f:.45f;layer.smoothness=.08f;layer.metallic=0;layers[i]=layer;EditorUtility.SetDirty(layer);
            }
            string dataPath=TerrainFolder+"/WorldGround.asset";var data=AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);bool fresh=data==null;
            if(fresh){data=new TerrainData{name="Four layer world ground"};AssetDatabase.CreateAsset(data,dataPath);}
            int signature=17;unchecked{foreach(var r in roads.Concat(buildings).Concat(rocks)){signature=signature*31+r.GetHashCode();}signature=signature*31+world.GetHashCode();}
            string stampPath=TerrainFolder+"/WorldGroundLayout.asset";var stamp=AssetDatabase.LoadAssetAtPath<GroundTerrainBakeStamp>(stampPath);
            if(stamp==null){stamp=ScriptableObject.CreateInstance<GroundTerrainBakeStamp>();AssetDatabase.CreateAsset(stamp,stampPath);}
            if(fresh||stamp.Signature!=signature||data.terrainLayers.Length!=4)
            {
                data.heightmapResolution=513;data.alphamapResolution=512;data.baseMapResolution=1024;data.size=new Vector3(world.width,1,world.height);data.terrainLayers=layers;
                float bottom=grounds.Min(b=>b.max.y);var heights=new float[513,513];
                for(int z=0;z<513;z++)for(int x=0;x<513;x++)
                {float wx=Mathf.Lerp(world.xMin,world.xMax,x/512f),wz=Mathf.Lerp(world.yMin,world.yMax,z/512f),top=bottom;foreach(var ground in grounds)if(wx>=ground.min.x&&wx<=ground.max.x&&wz>=ground.min.z&&wz<=ground.max.z)top=Mathf.Max(top,ground.max.y);heights[z,x]=Mathf.Clamp01(top-bottom);}
                data.SetHeights(0,0,heights);data.SetAlphamaps(0,0,GroundLayerPaint.Bake(world,512,roads,buildings,rocks));stamp.Signature=signature;stamp.Bottom=bottom;EditorUtility.SetDirty(data);EditorUtility.SetDirty(stamp);
            }
            var existing=motel.Find("Four Layer Ground v1");var obj=existing==null?new GameObject("Four Layer Ground v1"):existing.gameObject;if(existing==null){obj.transform.SetParent(motel,false);if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(obj,"Add four-layer ground");}
            obj.transform.position=new Vector3(world.xMin,stamp.Bottom,world.yMin);var terrain=obj.GetComponent<Terrain>();if(terrain==null)terrain=obj.AddComponent<Terrain>();terrain.terrainData=data;
            var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");if(shader==null)shader=Shader.Find("Nature/Terrain/Standard");
            string matPath=TerrainFolder+"/LayeredGround.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);}else material.shader=shader;
            terrain.materialTemplate=material;terrain.drawInstanced=true;terrain.heightmapPixelError=10;terrain.basemapDistance=150;terrain.detailObjectDensity=0;terrain.drawTreesAndFoliage=false;terrain.Flush();
            var skin=obj.GetComponent<LayeredGroundTerrain>();if(skin==null)skin=obj.AddComponent<LayeredGroundTerrain>();skin.Apply();if(!Application.isPlaying){EditorUtility.SetDirty(terrain);EditorUtility.SetDirty(material);}
            var oldShoulders=motel.Find("Ground shoulder finish v1");if(oldShoulders!=null)oldShoulders.gameObject.SetActive(false);
        }
    }
}
