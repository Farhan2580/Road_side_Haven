using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven.Editor
{
    public static class BiomeMeshAuthoring
    {
        public const string Folder="Assets/_Game/Generated/Vegetation";
        public static Mesh MeshFor(VegetationKind kind,int variant,bool foliage)
        {
            string path=Folder+"/"+kind+(kind==VegetationKind.Bamboo?"_Natural":"")+"_"+variant+(foliage?"_Leaves":"_Wood")+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved!=null)return saved;
            var root=new GameObject("Vegetation mesh source");var wood=new GameObject("Wood").transform;wood.SetParent(root.transform,false);var leaves=new GameObject("Leaves").transform;leaves.SetParent(root.transform,false);
            Build(kind,variant,wood,leaves);var source=foliage?leaves:wood;var parts=new List<CombineInstance>();
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>())parts.Add(new CombineInstance{mesh=filter.sharedMesh,transform=filter.transform.localToWorldMatrix});
            var mesh=new Mesh{name=kind+" "+variant+(foliage?" foliage":" branches"),indexFormat=IndexFormat.UInt32};if(parts.Count>0)mesh.CombineMeshes(parts.ToArray());
            AssetDatabase.CreateAsset(mesh,path);var temporary=new HashSet<Mesh>();foreach(var filter in root.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null&&(filter.sharedMesh.name=="Arched narrow leaf"||filter.sharedMesh.name=="Tiered conifer foliage"))temporary.Add(filter.sharedMesh);Object.DestroyImmediate(root);foreach(var generated in temporary)Object.DestroyImmediate(generated);return mesh;
        }
        private static void Part(Transform root,PrimitiveType type,Vector3 p,Vector3 scale,Quaternion rotation)
        {var obj=GameObject.CreatePrimitive(type);obj.transform.SetParent(root,false);obj.transform.localPosition=p;obj.transform.localScale=scale;obj.transform.localRotation=rotation;Object.DestroyImmediate(obj.GetComponent<Collider>());}
        private static void Sphere(Transform root,Vector3 p,Vector3 scale)=>Part(root,PrimitiveType.Sphere,p,scale,Quaternion.identity);
        private static void Limb(Transform root,Vector3 a,Vector3 b,float width)
        {var d=b-a;Part(root,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(width,d.magnitude*.5f,width),Quaternion.FromToRotation(Vector3.up,d));}
        private static void Custom(Transform root,Mesh mesh,Vector3 position,Vector3 scale)
        {var obj=new GameObject("Leaf geometry");obj.transform.SetParent(root,false);obj.transform.localPosition=position;obj.transform.localScale=scale;obj.AddComponent<MeshFilter>().sharedMesh=mesh;}
        private static void Build(VegetationKind kind,int variant,Transform wood,Transform leaves)
        {
            float variation=1+variant*.045f;
            if(kind==VegetationKind.StreetTree)
            {Limb(wood,Vector3.zero,Vector3.up*4.5f,.42f);for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3+variant*.4f;var p=new Vector3(Mathf.Cos(a)*.65f,4.8f+(i%2)*.25f,Mathf.Sin(a)*.6f);Limb(wood,new Vector3(0,3,0),p,.16f);Sphere(leaves,p,new Vector3(3.6f,3.5f,3.6f)*variation);}return;}
            if(kind==VegetationKind.Oak)
            {Limb(wood,Vector3.zero,new Vector3(.18f,4,0),.82f);Sphere(leaves,new Vector3(0,6.5f,0),new Vector3(5.8f,3.6f,5.7f));for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7+variant*.4f;var end=new Vector3(Mathf.Cos(a)*3.4f,5.1f+(i%3)*.55f,Mathf.Sin(a)*3.1f);Limb(wood,new Vector3(.1f,2.8f,0),end,.25f);Limb(wood,end,end+Vector3.up*1,.10f);Sphere(leaves,end+Vector3.up*.5f,new Vector3(4.0f,2.9f,3.8f)*variation);}return;}
            if(kind==VegetationKind.Pine)
            {Limb(wood,Vector3.zero,Vector3.up*9,.42f);var cone=Cone();for(int i=0;i<5;i++)Custom(leaves,cone,new Vector3(0,1.5f+i*1.35f,0),new Vector3((4.8f-i*.77f)*variation,3.5f-i*.27f,(4.8f-i*.77f)*variation));return;}
            if(kind==VegetationKind.Palm)
            {Vector3 previous=Vector3.zero;for(int i=1;i<=7;i++){var p=new Vector3(Mathf.Sin(i*.19f)*.7f,i*1.12f,Mathf.Sin(i*.32f)*.25f);Limb(wood,previous,p,.34f-i*.018f);previous=p;}var frond=Frond();for(int i=0;i<9;i++){var obj=new GameObject("Arched palm frond");obj.transform.SetParent(leaves,false);obj.transform.localPosition=previous;obj.transform.localRotation=Quaternion.Euler(0,i*40+variant*11,0);obj.AddComponent<MeshFilter>().sharedMesh=frond;}Sphere(leaves,previous,new Vector3(.8f,.6f,.8f));return;}
            if(kind==VegetationKind.TrimmedBush)
            {Part(leaves,PrimitiveType.Cube,new Vector3(0,.6f,0),new Vector3(1.6f,1.0f,.95f),Quaternion.identity);Sphere(leaves,new Vector3(-.8f,.6f,0),new Vector3(.9f,1.05f,.95f));Sphere(leaves,new Vector3(.8f,.6f,0),new Vector3(.9f,1.05f,.95f));return;}
            if(kind==VegetationKind.Bamboo)
            {for(int stem=0;stem<7;stem++){float a=stem*2.4f;var p=new Vector3(Mathf.Cos(a)*(stem%3)*.4f,0,Mathf.Sin(a)*(stem%3)*.4f);float height=4.2f+(stem%3)*.65f;Limb(wood,p,p+Vector3.up*height,.10f);for(float y=.7f;y<height;y+=.65f)Part(wood,PrimitiveType.Cylinder,p+Vector3.up*y,new Vector3(.14f,.035f,.14f),Quaternion.identity);for(int leaf=0;leaf<4;leaf++){var q=p+Vector3.up*(height-1.8f+leaf*.5f);Limb(wood,q,q+new Vector3(Mathf.Cos(a+leaf)*.9f,.25f,Mathf.Sin(a+leaf)*.9f),.025f);var blade=Frond();var spray=new GameObject("Bamboo leaf spray");spray.transform.SetParent(leaves,false);spray.transform.localPosition=q;spray.transform.localRotation=Quaternion.Euler(0,(a+leaf)*Mathf.Rad2Deg,0);spray.transform.localScale=new Vector3(.26f,.35f,.42f);spray.AddComponent<MeshFilter>().sharedMesh=blade;}}return;}
            if(kind==VegetationKind.FlowerBed)
            {Part(wood,PrimitiveType.Cube,new Vector3(0,.08f,0),new Vector3(3.4f,.16f,1.2f),Quaternion.identity);for(int i=0;i<12;i++){var p=new Vector3(-1.4f+(i%6)*.55f,.35f+(i%3)*.08f,(i/6-.5f)*.5f);Limb(wood,new Vector3(p.x,.15f,p.z),p,.035f);for(int petal=0;petal<5;petal++){float a=petal*Mathf.PI*2/5;Sphere(leaves,p+new Vector3(Mathf.Cos(a)*.10f,0,Mathf.Sin(a)*.10f),new Vector3(.15f,.10f,.15f));}}}
        }
        private static Mesh Cone()
        {
            const int n=12;var vertices=new Vector3[n+2];vertices[0]=Vector3.up;vertices[n+1]=Vector3.zero;var triangles=new List<int>();
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;vertices[i+1]=new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f);triangles.AddRange(new[]{0,(i+1)%n+1,i+1,n+1,i+1,(i+1)%n+1});}
            var mesh=new Mesh{name="Tiered conifer foliage"};mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();return mesh;
        }
        private static Mesh Frond()
        {
            const int n=12;var vertices=new Vector3[(n+1)*2];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            for(int i=0;i<=n;i++){float t=i/(float)n;float width=Mathf.Sin(t*Mathf.PI)*.48f;var center=new Vector3(0,Mathf.Sin(t*Mathf.PI)*.42f-t*t*1.3f,t*4.2f);vertices[i*2]=center+Vector3.left*width;vertices[i*2+1]=center+Vector3.right*width;uv[i*2]=new Vector2(0,t);uv[i*2+1]=new Vector2(1,t);if(i<n){int a=i*2;triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}}
            var mesh=new Mesh{name="Arched narrow leaf"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();return mesh;
        }
    }
}
