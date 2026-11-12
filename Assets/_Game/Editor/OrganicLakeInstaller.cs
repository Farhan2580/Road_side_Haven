using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private const string LakeFolder="Assets/_Game/Generated/Lake";
        private static void InstallOrganicLake(Transform motel)
        {
            LakeTextureAuthoring.Generate();var shader=AssetDatabase.LoadAssetAtPath<Shader>(LakeTextureAuthoring.Folder+"/OrganicLake.shadergraph");
            if(shader==null||ShaderUtil.ShaderHasError(shader)){Debug.LogError("Lake water Shader Graph must finish importing before applying the lake upgrade.");return;}
            var oldLake=motel.Find("Willow Lake 80x60 v1");if(oldLake!=null){oldLake.gameObject.SetActive(false);if(Application.isPlaying)Object.Destroy(oldLake.gameObject);else Undo.DestroyObjectImmediate(oldLake.gameObject);}
            var installed=motel.Find("Willow Lake 80x60 v2");if(installed!=null){CutLakeTerrain(motel,installed.position,OrganicLakeShape.Outline());return;}
            if(!AssetDatabase.IsValidFolder(LakeFolder))AssetDatabase.CreateFolder("Assets/_Game/Generated","Lake");
            var root=new GameObject("Willow Lake 80x60 v2").transform;root.SetParent(motel,false);root.position=new Vector3(-200,.02f,-180);if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Create organic willow lake");
            foreach(var old in motel.GetComponentsInChildren<Transform>(true))if(old.name=="Pond water"||old.name=="Village pond bank")old.gameObject.SetActive(false);
            var water=LakeMaterial("Lake water",shader,Color.white);water.SetColor("_ShallowColor",new Color(79/255f,195/255f,212/255f));water.SetColor("_DeepColor",new Color(26/255f,82/255f,118/255f));water.SetTexture("_WaveNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(LakeTextureAuthoring.Folder+"/LakeWaveNormal.png"));
            LakeMesh(root,"Lake water surface",OrganicLakeShape.Surface(),water,4);
            var bed=LakeMaterial("Lake pebbled bed",Shader.Find("Universal Render Pipeline/Lit"),new Color(.78f,.73f,.58f));bed.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureAuthoring.Folder+"/DirtPebbles_Albedo.png"));bed.SetTextureScale("_BaseMap",Vector2.one*12);bed.SetFloat("_Smoothness",.15f);
            LakeMesh(root,"Lake bed water",OrganicLakeShape.Surface(true),bed,0);LakeMesh(root,"Lake shore marking",OrganicLakeShape.Bank(),bed,0);
            var green=LakeMaterial("Willow foliage",Shader.Find("Universal Render Pipeline/Lit"),new Color(.24f,.43f,.13f));var reed=LakeMaterial("Cattail leaves",green.shader,new Color(.25f,.39f,.13f));var brown=LakeMaterial("Willow bark",green.shader,new Color(.27f,.19f,.105f));var heads=LakeMaterial("Cattail seed heads",green.shader,new Color(.22f,.12f,.055f));var stone=LakeMaterial("Wet smooth lake stones",green.shader,new Color(.43f,.45f,.39f));stone.SetFloat("_Smoothness",.38f);
            var outline=OrganicLakeShape.Outline();
            for(int i=0;i<outline.Length;i+=3)
            {
                var point=outline[i];var p=root.position+new Vector3(point.x*1.035f,-.10f,point.y*1.035f);float size=.7f+(i%5)*.22f;
                LakePart(root,"Smooth lake rock",PrimitiveType.Sphere,p+Vector3.up*.18f,new Vector3(size,.42f,size*.8f),stone,true);
                if(i%6==0)for(int stem=0;stem<3;stem++)
                {var s=p+new Vector3(stem*.23f,0,Mathf.Sin(i+stem)*.3f);float h=1.2f+(i%4)*.12f;LakePart(root,"Cattail plant marking",PrimitiveType.Cylinder,s+Vector3.up*h*.5f,new Vector3(.035f,h*.5f,.035f),reed,false);LakePart(root,"Cattail seed marking",PrimitiveType.Capsule,s+Vector3.up*h,new Vector3(.11f,.22f,.11f),heads,false);var leaf=LakePart(root,"Cattail leaf marking",PrimitiveType.Capsule,s+new Vector3(.12f,h*.48f,0),new Vector3(.065f,h*.43f,.10f),reed,false);leaf.rotation=Quaternion.Euler(0,i*11,18);}
            }
            foreach(int index in new[]{7,29,54,77})Willow(root,outline[index]*1.22f,brown,green);
            var white=LakeMaterial("Duck feathers",green.shader,new Color(.88f,.84f,.66f));var wing=LakeMaterial("Duck wings",green.shader,new Color(.22f,.19f,.12f));var orange=LakeMaterial("Duck bill",green.shader,new Color(.93f,.50f,.06f));var ducks=new List<Transform>();
            for(int i=0;i<6;i++)
            {
                var duck=new GameObject("Lake duck "+(i+1)).transform;duck.SetParent(root,false);duck.localPosition=new Vector3(Mathf.Cos(i*1.17f)*15,.035f,Mathf.Sin(i*1.17f)*12);ducks.Add(duck);
                LakePart(duck,"Duck body marking",PrimitiveType.Sphere,duck.position+Vector3.up*.13f,new Vector3(.42f,.30f,.72f),white,false);LakePart(duck,"Duck head marking",PrimitiveType.Sphere,duck.position+new Vector3(0,.36f,.29f),new Vector3(.23f,.25f,.23f),i%2==0?green:white,false);LakePart(duck,"Duck beak marking",PrimitiveType.Sphere,duck.position+new Vector3(0,.32f,.46f),new Vector3(.13f,.07f,.20f),orange,false);
                for(int side=-1;side<=1;side+=2)LakePart(duck,"Duck wing marking",PrimitiveType.Sphere,duck.position+new Vector3(side*.19f,.14f,-.02f),new Vector3(.055f,.18f,.45f),wing,false);
            }
            var probe=new GameObject("Lake realtime reflection").AddComponent<ReflectionProbe>();probe.transform.SetParent(root,false);probe.transform.localPosition=Vector3.up*2;probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=128;probe.size=new Vector3(105,18,85);probe.boxProjection=true;probe.blendDistance=12;probe.farClipPlane=160;probe.cullingMask=~(1<<4);probe.intensity=.8f;
            root.gameObject.AddComponent<LakeAmbientMotion>().Configure(ducks.ToArray(),probe);
            LakeParticles(root,false);LakeParticles(root,true);CutLakeTerrain(motel,root.position,outline);CombineLakeShore(root);
            if(!Application.isPlaying){EditorUtility.SetDirty(water);EditorUtility.SetDirty(probe);}
        }
        private static Material LakeMaterial(string name,Shader shader,Color color)
        {string path=LakeFolder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}material.shader=shader;if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);material.enableInstancing=true;return material;}
        private static void LakeMesh(Transform parent,string name,Mesh mesh,Material material,int layer)
        {
            string path=LakeFolder+"/"+mesh.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){saved=mesh;AssetDatabase.CreateAsset(saved,path);}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.layer=layer;obj.AddComponent<MeshFilter>().sharedMesh=saved;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        private static Transform LakePart(Transform root,string name,PrimitiveType type,Vector3 p,Vector3 scale,Material material,bool solid)
        {var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(root,false);obj.transform.position=p;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;if(!solid){var c=obj.GetComponent<Collider>();c.enabled=false;if(Application.isPlaying)Object.Destroy(c);else Object.DestroyImmediate(c);}return obj.transform;}
        private static void Willow(Transform root,Vector2 point,Material bark,Material leaves)
        {
            var p=root.position+new Vector3(point.x,-.07f,point.y);LakePart(root,"Willow trunk",PrimitiveType.Cylinder,p+Vector3.up*2.5f,new Vector3(.65f,2.5f,.65f),bark,true);
            LakePart(root,"Willow tree crown",PrimitiveType.Sphere,p+Vector3.up*5.2f,new Vector3(6.5f,2.8f,5.8f),leaves,false);
            for(int branch=0;branch<10;branch++)
            {float a=branch*Mathf.PI*2/10;var q=p+new Vector3(Mathf.Cos(a)*2.8f,3.7f,Mathf.Sin(a)*2.4f);for(int segment=0;segment<4;segment++){var leafPoint=q+new Vector3(Mathf.Cos(a)*segment*.10f,-segment*.58f,Mathf.Sin(a)*segment*.10f);var drop=LakePart(root,"Willow tree crown hanging",PrimitiveType.Capsule,leafPoint,new Vector3(.26f,.49f,.24f),leaves,false);drop.rotation=Quaternion.Euler(Mathf.Sin(a)*12,0,Mathf.Cos(a)*12);}}
        }
        private static void CutLakeTerrain(Transform motel,Vector3 center,Vector2[] outline)
        {
            foreach(var terrain in motel.GetComponentsInChildren<Terrain>(true))
            {
                var data=terrain.terrainData;if(data==null)continue;var origin=terrain.transform.position;int n=data.holesResolution;var holes=data.GetHoles(0,0,n,n);bool changed=false;
                int x0=Mathf.Clamp(Mathf.FloorToInt((center.x-42-origin.x)/data.size.x*n),0,n-1),x1=Mathf.Clamp(Mathf.CeilToInt((center.x+42-origin.x)/data.size.x*n),0,n-1);int z0=Mathf.Clamp(Mathf.FloorToInt((center.z-32-origin.z)/data.size.z*n),0,n-1),z1=Mathf.Clamp(Mathf.CeilToInt((center.z+32-origin.z)/data.size.z*n),0,n-1);
                for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){var p=new Vector2(origin.x+(x+.5f)/n*data.size.x-center.x,origin.z+(z+.5f)/n*data.size.z-center.z);if(OrganicLakeShape.Contains(p,outline)){holes[z,x]=false;changed=true;}}
                if(changed){data.SetHoles(0,0,holes);if(!Application.isPlaying)EditorUtility.SetDirty(data);}
            }
        }
        private static void CombineLakeShore(Transform root)
        {
            var groups=root.GetComponentsInChildren<MeshRenderer>().Where(r=>!r.name.StartsWith("Duck")&&!r.name.Contains("water")&&!r.name.Contains("shore")&&r.GetComponentInParent<ParticleSystem>()==null).GroupBy(r=>r.sharedMaterial);
            foreach(var group in groups)
            {
                var parts=group.ToArray();if(parts.Length<3)continue;var combine=parts.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=root.worldToLocalMatrix*r.localToWorldMatrix}).ToArray();var mesh=new Mesh{name="Combined lake "+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combine);string path=LakeFolder+"/"+mesh.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){saved=mesh;AssetDatabase.CreateAsset(saved,path);}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
                var obj=new GameObject("Lake vegetation marking "+group.Key.name);obj.transform.SetParent(root,false);obj.AddComponent<MeshFilter>().sharedMesh=saved;obj.AddComponent<MeshRenderer>().sharedMaterial=group.Key;foreach(var part in parts)part.enabled=false;
            }
        }
        private static void LakeParticles(Transform root,bool fish)
        {
            var obj=new GameObject(fish?"Underwater fish particles":"Lake ripple particles");obj.transform.SetParent(root,false);obj.layer=4;obj.transform.localPosition=Vector3.up*(fish?-.22f:.025f);obj.transform.localRotation=Quaternion.Euler(90,0,0);var system=obj.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.maxParticles=fish?14:28;main.startLifetime=fish?25:3;main.startSpeed=fish?.12f:0;main.startSize=fish?.28f:1.2f;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startColor=fish?new Color(.13f,.24f,.25f,.7f):new Color(.9f,.98f,1,.3f);var emission=system.emission;emission.rateOverTime=fish?.45f:3;var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=1;shape.scale=new Vector3(26,18,1);
            var size=system.sizeOverLifetime;size.enabled=!fish;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.2f,1,1.8f));var color=system.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(fish?.65f:.25f,.2f),new GradientAlphaKey(0,1)});color.color=gradient;
            var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");var material=LakeMaterial(fish?"Underwater fish":"Water ripple",shader,Color.white);material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;if(!fish)material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(LakeTextureAuthoring.Folder+"/LakeRipple.png"));var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=fish?ParticleSystemRenderMode.Stretch:ParticleSystemRenderMode.HorizontalBillboard;renderer.lengthScale=fish?2:1;renderer.shadowCastingMode=ShadowCastingMode.Off;system.Play();
        }
    }
}
