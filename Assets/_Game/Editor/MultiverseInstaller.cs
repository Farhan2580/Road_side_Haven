using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallMultiverse(Transform motel)
        {
            if(motel.Find("Multiverse Portals v1")!=null)return;
            var owner=motel.GetComponentInChildren<OwnerSandboxController>(true);
            if(owner==null)return;
            var root=new GameObject("Multiverse Portals v1").transform;root.SetParent(motel,false);
            if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Add multiverse portals");
            var purple=Material("Portal violet",new Color(.64f,.16f,1),true);
            var cyan=Material("Cyber cyan",new Color(.05f,.85f,1),true);
            var magic=Material("Magic emerald",new Color(.20f,1,.54f),true);
            foreach(var glow in new[] {purple,cyan,magic}) { glow.SetColor("_EmissionColor",glow.color*4); EditorUtility.SetDirty(glow); }
            for(int i=0;i<20;i++)
            {
                float angle=i*Mathf.PI*2/20;
                Box(root,"Portal ring",new Vector3(-16+Mathf.Cos(angle)*1.6f,2+Mathf.Sin(angle)*1.8f,-6),new Vector3(.40f,.40f,.22f),purple);
            }
            LabelWorld(root,"DIMENSION PORTAL [T]",new Vector3(-16,4.5f,-6),5);
            var layers=new GameObject[4];
            for(int i=0;i<4;i++)
            {
                layers[i]=new GameObject(((UniverseId)i)+" dimension overlay");layers[i].transform.SetParent(root,false);
                if(i==0)continue;
                var layer=layers[i].transform;
                var filter=i==1 ? new Color(.65f,.8f,1) : i==2 ? new Color(.8f,.72f,.5f) : new Color(.78f,1,.85f);
                DimensionVolume(layer,(UniverseId)i,filter);
                if(i==1)
                {
                    for(int p=0;p<6;p++)
                    {
                        float x=-20-p*2;
                        Box(layer,"Cyber neon tower",new Vector3(x,2+p*.3f,8),new Vector3(.35f,4+p*.6f,.35f),p%2==0?cyan:purple);
                    }
                    LabelWorld(layer,"CYBER MOTEL / ROBOT CLIENTS",new Vector3(0,4,8),7);
                }
                else if(i==2)
                {
                    for(int b=0;b<5;b++)
                    {
                        var barrier=Solid(layer,"Wasteland barricade",new Vector3(-22-b*2,1,8),new Vector3(1.4f,2,.35f),timber);
                        // Visual barrier only: keep existing pedestrian and driving paths intact.
                        var collider=barrier.GetComponent<Collider>(); collider.enabled=false;
                        if(Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
                    }
                    LabelWorld(layer,"SURVIVOR MOTEL / DEFEND SUPPLIES",new Vector3(0,4,8),7);
                }
                else
                {
                    for(int c=0;c<6;c++)
                    {
                        var crystal=Solid(layer,"Mana crystal",new Vector3(-22-c*1.7f,1.3f,8),new Vector3(.6f,2.2f,.6f),magic);
                        crystal.transform.rotation=Quaternion.Euler(0,45,c%2==0?15:-15);
                        var collider=crystal.GetComponent<Collider>(); collider.enabled=false;
                        if(Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
                    }
                    LabelWorld(layer,"FANTASY MOTEL / ELF TRAVELLERS",new Vector3(0,4,8),7);
                }
                layers[i].SetActive(false);
            }
            root.gameObject.AddComponent<MultiverseController>().Configure(motel.GetComponent<MotelGameRoot>(),owner,layers);
            AssetDatabase.SaveAssets();
        }
        private static void DimensionVolume(Transform parent,UniverseId universe,Color filter)
        {
            string path=MaterialFolder+"/"+universe+"Dimension.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(profile==null)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);
                var color=profile.Add<ColorAdjustments>(true);color.colorFilter.Override(filter);
                color.saturation.Override(universe==UniverseId.Apocalypse?-35:12);
                var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(universe==UniverseId.Neon?.65f:.3f);
                foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
                EditorUtility.SetDirty(profile);
            }
            var volume=parent.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=25;volume.sharedProfile=profile;
        }
    }
}
