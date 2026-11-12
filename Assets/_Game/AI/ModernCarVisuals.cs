using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    public enum ModernCarStyle { WedgeSupercar, HyperCoupe, ExecutiveGT, SportSedan, CitySport }
    /// <summary>Original procedural sports-car silhouettes, detailed wheels and animated steering/rolling.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ModernCarVisuals : MonoBehaviour
    {
        [SerializeField] private ModernCarStyle style;
        private Rigidbody body;
        private Transform shell;
        private readonly List<Material> materials=new List<Material>();
        private readonly List<Mesh> meshes=new List<Mesh>();
        private readonly List<Transform> wheels=new List<Transform>(),frontMounts=new List<Transform>();
        public void Configure(int variant)=>style=(ModernCarStyle)Mathf.Abs(variant%5);
        private void Start()
        {
            body=GetComponent<Rigidbody>();
            var previous=GetComponentsInChildren<Renderer>();foreach(var renderer in previous)renderer.enabled=false;
            foreach(var collider in GetComponentsInChildren<Collider>())if(collider.transform!=transform&&collider.attachedRigidbody==body)collider.enabled=false;
            var rootBox=GetComponent<BoxCollider>();if(rootBox!=null){rootBox.center=new Vector3(0,.72f,0);rootBox.size=new Vector3(2.18f,1.44f,4.5f);}
            shell=new GameObject("Modern car design - "+style).transform;shell.SetParent(transform,false);
            shell.localRotation=Quaternion.Euler(0,(GetComponent<EnterableVehicle>()?.ForwardSign ?? 1)<0?180:0,0);
            Color[] colors={new Color(.78f,.98f,.08f),new Color(.04f,.18f,.65f),new Color(.42f,.48f,.53f),new Color(.92f,.94f,.96f),new Color(.83f,.06f,.04f)};
            var paint=Mat("Body paint",colors[(int)style],.62f,.92f);
            var carbon=Mat("Carbon and grille",new Color(.022f,.03f,.035f),.2f,.6f);
            var glass=Mat("Smoked glass",new Color(.035f,.09f,.14f),.7f,.95f);
            var metal=Mat("Alloy",new Color(.62f,.66f,.7f),.9f,.8f);
            var rubber=Mat("Tire rubber",new Color(.018f,.02f,.024f),0,.25f);
            var white=Mat("LED white",new Color(.75f,.92f,1),0,.5f,4);
            var red=Mat("LED red",new Color(1,.025f,.01f),0,.5f,3);
            bool sedan=style==ModernCarStyle.SportSedan||style==ModernCarStyle.CitySport;
            float roof=sedan?1.40f:style==ModernCarStyle.ExecutiveGT?1.3f:1.18f;
            float[] z={-2.2f,-1.7f,-.9f,.45f,1.55f,2.22f};
            float[] widths={.70f,.98f,1f,.96f,.91f,.73f};
            float[] heights=style==ModernCarStyle.WedgeSupercar?new[]{.62f,.88f,.86f,.70f,.53f,.42f}:new[]{.62f,.85f,.82f,.76f,.66f,.52f};
            SectionMesh("Sculpted body",z,widths,heights,.27f,paint);
            SectionMesh("Glass cabin",new[]{-.95f,-.48f,.45f,1.05f},new[]{.76f,.63f,.60f,.72f},new[]{.81f,roof,roof-.025f,.77f},.72f,glass);
            SectionMesh("Painted roof",new[]{-.48f,.45f},new[]{.635f,.605f},new[]{roof+.035f,roof+.01f},roof-.01f,paint);
            Box("Front splitter",new Vector3(0,.25f,2.14f),new Vector3(1.92f,.07f,.36f),carbon);
            Box("Rear diffuser",new Vector3(0,.28f,-2.17f),new Vector3(1.80f,.16f,.20f),carbon);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Side skirt",new Vector3(side*.98f,.27f,-.05f),new Vector3(.10f,.11f,3.4f),carbon);
                Box("Door handle",new Vector3(side*1.005f,.81f,-.12f),new Vector3(.035f,.04f,.22f),metal);
                Box("Mirror stem",new Vector3(side*.91f,.96f,.65f),new Vector3(.16f,.04f,.05f),carbon);
                Box("Wing mirror",new Vector3(side*1.07f,.98f,.65f),new Vector3(.22f,.10f,.26f),paint);
                Box("Tail LED",new Vector3(side*.52f,.65f,-2.205f),new Vector3(.68f,.055f,.035f),red);
                Box("Front intake",new Vector3(side*.61f,.40f,2.17f),new Vector3(.4f,.18f,.07f),carbon);
                for(int vent=0;vent<3;vent++)Box("Intake slat",new Vector3(side*.61f,.34f+vent*.05f,2.215f),new Vector3(.35f,.012f,.02f),metal);
                Box("Rear exhaust",new Vector3(side*.57f,.29f,-2.29f),new Vector3(.22f,.12f,.15f),metal);
                if(style==ModernCarStyle.WedgeSupercar)
                {
                    for(int led=0;led<3;led++)
                    {
                        var strip=Box("Angular LED",new Vector3(side*(.50f+led*.10f),.59f-led*.027f,1.91f+led*.03f),new Vector3(.04f,.025f,.36f),white);
                        strip.localRotation=Quaternion.Euler(0,side*-32,0);
                    }
                }
                else
                {
                    Box("LED headlamp",new Vector3(side*.58f,.67f,2.05f),new Vector3(.60f,.065f,.09f),white);
                    if(style==ModernCarStyle.HyperCoupe)
                        for(int led=0;led<4;led++)Box("Quad LED element",new Vector3(side*(.35f+led*.12f),.73f,2.07f),new Vector3(.065f,.055f,.055f),white);
                }
                for(int axle=-1;axle<=1;axle+=2)
                {
                    Wheel(side,axle,rubber,metal,carbon,red);
                    Fender(side,axle,paint);
                }
            }
            if(style==ModernCarStyle.HyperCoupe)
            {
                Ring("Horseshoe intake",new Vector3(0,.49f,2.225f),.23f,.25f,.035f,metal);
                Box("Center grille",new Vector3(0,.47f,2.23f),new Vector3(.32f,.36f,.035f),carbon);
            }
            else if(style==ModernCarStyle.SportSedan)
                for(int side=-1;side<=1;side+=2){Box("Twin grille surround",new Vector3(side*.18f,.49f,2.23f),new Vector3(.32f,.28f,.055f),metal);Box("Twin grille inset",new Vector3(side*.18f,.49f,2.265f),new Vector3(.26f,.23f,.025f),carbon);}
            else Box("Wide front grille",new Vector3(0,.44f,2.23f),new Vector3(.65f,.19f,.04f),carbon);
            if(style==ModernCarStyle.WedgeSupercar||style==ModernCarStyle.HyperCoupe)
            {
                for(int side=-1;side<=1;side+=2)Box("Spoiler mount",new Vector3(side*.65f,.96f,-1.73f),new Vector3(.05f,.25f,.12f),carbon);
                Box("Rear aero wing",new Vector3(0,1.10f,-1.76f),new Vector3(2,.07f,.38f),carbon);
            }
            var all=shell.GetComponentsInChildren<Renderer>();
            var medium=new List<Renderer>();foreach(var renderer in all)if(renderer.name=="Sculpted body"||renderer.name=="Glass cabin"||renderer.name=="Painted roof"||renderer.name=="Performance tire")medium.Add(renderer);
            var far=new List<Renderer>();foreach(var renderer in all)if(renderer.name=="Sculpted body"||renderer.name=="Glass cabin")far.Add(renderer);
            var lod=shell.gameObject.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.12f,all),new LOD(.025f,medium.ToArray()),new LOD(.004f,far.ToArray())});lod.RecalculateBounds();
            var motor=GetComponent<ArcadeVehicleMotor>();if(motor!=null)motor.Prepare();
        }
        private Material Mat(string name,Color color,float metallic,float smooth,float emission=0)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.name=name;material.color=color;
            material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smooth);
            if(emission>0){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*emission);}
            materials.Add(material);return material;
        }
        private Transform Box(string name,Vector3 position,Vector3 scale,Material material)=>Primitive(name,PrimitiveType.Cube,position,scale,material);
        private Transform Primitive(string name,PrimitiveType shape,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(shape);part.name=name;part.transform.SetParent(shell,false);part.transform.localPosition=position;part.transform.localScale=scale;
            part.GetComponent<Renderer>().sharedMaterial=material;var collider=part.GetComponent<Collider>();collider.enabled=false;Destroy(collider);return part.transform;
        }
        private void Wheel(int side,int axle,Material rubber,Material metal,Material dark,Material red)
        {
            var mount=new GameObject("Wheel steering mount").transform;mount.SetParent(shell,false);mount.localPosition=new Vector3(side*1.01f,.37f,axle*1.35f);
            if(axle>0)frontMounts.Add(mount);
            var spin=new GameObject("Wheel roll").transform;spin.SetParent(mount,false);wheels.Add(spin);
            var tire=Primitive("Performance tire",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.74f,.12f,.74f),rubber);tire.SetParent(spin,false);tire.localPosition=Vector3.zero;tire.localRotation=Quaternion.Euler(0,0,90);
            var rim=Primitive("Alloy rim",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.54f,.015f,.54f),dark);rim.SetParent(spin,false);rim.localPosition=new Vector3(side*.13f,0,0);rim.localRotation=Quaternion.Euler(0,0,90);
            for(int spoke=0;spoke<5;spoke++)
            {
                var part=Box("Alloy spoke",Vector3.zero,new Vector3(.025f,.49f,.045f),metal);part.SetParent(spin,false);part.localPosition=new Vector3(side*.15f,0,0);part.localRotation=Quaternion.Euler(spoke*72,0,0);
            }
            var caliper=Box("Brake caliper",Vector3.zero,new Vector3(.06f,.22f,.10f),red);caliper.SetParent(mount,false);caliper.localPosition=new Vector3(side*.10f,.02f,-.14f);
        }
        private void Fender(int side,int axle,Material material)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();const int segments=24;
            for(int i=0;i<=segments;i++)
            {
                float angle=i*Mathf.PI/segments;
                foreach(float x in new[]{side*.99f,side*1.10f})
                    foreach(float radius in new[]{.395f,.47f})vertices.Add(new Vector3(x,.37f+Mathf.Sin(angle)*radius,axle*1.35f+Mathf.Cos(angle)*radius));
            }
            for(int i=0;i<segments;i++)
            {
                int a=i*4,b=a+4;
                triangles.AddRange(new[]{a,a+1,b+1,a,b+1,b,a+2,b+3,a+3,a+2,b+2,b+3,a+1,a+3,b+3,a+1,b+3,b+1,a,b+2,a+2,a,b,b+2});
            }
            if(side<0)for(int i=0;i<triangles.Count;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
            var mesh=new Mesh{name="Curved fender"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var part=new GameObject("Curved fender",typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(shell,false);part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        private void Ring(string name,Vector3 center,float width,float height,float thickness,Material material)
        {
            for(int i=0;i<20;i++)
            {float angle=i*Mathf.PI*2/20;var part=Box(name,center+new Vector3(Mathf.Cos(angle)*width,Mathf.Sin(angle)*height,0),new Vector3(thickness,.07f,.035f),material);part.localRotation=Quaternion.Euler(0,0,i*18);}
        }
        private void SectionMesh(string name,float[] z,float[] widths,float[] heights,float bottom,Material material)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            int ring=name=="Sculpted body"?8:6;
            for(int i=0;i<z.Length;i++)
            {
                float w=widths[i],top=heights[i],mid=Mathf.Lerp(bottom,top,.42f);
                if(ring==8)
                {
                    float shoulder=top+(Mathf.Abs(z[i])<1.9f?.24f:.10f);
                    vertices.AddRange(new[]{new Vector3(-w*.80f,bottom,z[i]),new Vector3(w*.80f,bottom,z[i]),new Vector3(w,mid,z[i]),new Vector3(w*.95f,shoulder,z[i]),new Vector3(w*.55f,top,z[i]),new Vector3(-w*.55f,top,z[i]),new Vector3(-w*.95f,shoulder,z[i]),new Vector3(-w,mid,z[i])});
                }
                else vertices.AddRange(new[]{new Vector3(-w*.80f,bottom,z[i]),new Vector3(w*.80f,bottom,z[i]),new Vector3(w,mid,z[i]),new Vector3(w*.89f,top,z[i]),new Vector3(-w*.89f,top,z[i]),new Vector3(-w,mid,z[i])});
            }
            for(int row=0;row<z.Length-1;row++)for(int edge=0;edge<ring;edge++)
            {int a=row*ring+edge,b=row*ring+(edge+1)%ring,c=(row+1)*ring+(edge+1)%ring,d=(row+1)*ring+edge;triangles.AddRange(new[]{a,b,c,a,c,d});}
            for(int i=1;i<ring-1;i++){triangles.AddRange(new[]{0,i+1,i});int end=(z.Length-1)*ring;triangles.AddRange(new[]{end,end+i,end+i+1});}
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(shell,false);obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        private void LateUpdate()
        {
            if(body==null||shell==null)return;
            float speed=Vector3.Dot(body.linearVelocity,shell.forward);
            foreach(var wheel in wheels)wheel.Rotate(Vector3.right,speed/.37f*Mathf.Rad2Deg*Time.deltaTime,Space.Self);
            float steer=Mathf.Clamp(body.angularVelocity.y*14,-28,28);
            foreach(var mount in frontMounts)mount.localRotation=Quaternion.Slerp(mount.localRotation,Quaternion.Euler(0,steer,0),Time.deltaTime*8);
        }
        private void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}
