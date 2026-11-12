using System.Collections.Generic;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Coach/school-bus visual based on a modern coach silhouette and a yellow bonneted school bus.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BusVisuals : MonoBehaviour
    {
        [SerializeField] private bool schoolBus;
        private Transform door;
        private bool open;
        private readonly List<Material> materials=new List<Material>();
        private readonly List<Transform> wheels=new List<Transform>();
        private Rigidbody body;
        private Mesh roundedShell;
        public void Configure(bool school)=>schoolBus=school;
        public Vector3 DoorPoint=>transform.TransformPoint(new Vector3(2.2f,0,3.7f));
        public void SetDoor(bool value)=>open=value;
        private Material Mat(string name,Color color,float shine=0)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.color=color;m.SetFloat("_Smoothness",shine);materials.Add(m);return m;}
        private void Start()
        {
            body=GetComponent<Rigidbody>();roundedShell=RoundedShell();
            var paint=Mat("Bus paint",schoolBus?new Color(1,.64f,.025f):new Color(.70f,.74f,.77f),.7f);
            var glass=Mat("Coach panoramic glass",new Color(.025f,.075f,.10f),.92f);
            var dark=Mat("Bus trim",new Color(.025f,.03f,.035f));var metal=Mat("Wheel chrome",new Color(.65f,.69f,.72f),.85f);
            var lamp=Mat("Headlights",new Color(.8f,.94f,1));lamp.EnableKeyword("_EMISSION");lamp.SetColor("_EmissionColor",new Color(1,1,1)*2);
            var red=Mat("Rear lights",new Color(.9f,.025f,.015f));
            Box("Bus lower body",new Vector3(0,1.0f,0),new Vector3(2.65f,1.55f,10.8f),paint);
            Box("Panoramic window band",new Vector3(0,2.45f,-.15f),new Vector3(2.62f,1.35f,9.7f),glass);
            Box("Rounded roof cap",new Vector3(0,3.2f,-.1f),new Vector3(2.75f,.28f,10.3f),paint);
            Box("Wide windshield",new Vector3(0,2.25f,5.02f),new Vector3(2.5f,1.75f,.14f),glass);
            Box("Front fascia",new Vector3(0,1.0f,5.34f),new Vector3(2.55f,.7f,.18f),paint);
            Box("Front grille",new Vector3(0,.83f,5.45f),new Vector3(1.4f,.25f,.06f),dark);
            for(int side=-1;side<=1;side+=2)
            {
                for(int window=0;window<10;window++)
                {
                    Box("Window mullion",new Vector3(side*1.33f,2.45f,-4.4f+window*.95f),new Vector3(.035f,1.4f,.055f),schoolBus?paint:dark);
                    Box("Luggage compartment",new Vector3(side*1.34f,.85f,-3.8f+window*.78f),new Vector3(.025f,.65f,.025f),dark);
                }
                foreach(float y in schoolBus?new[]{1.45f,1.6f,1.75f}:new[]{1.6f})Box("Side trim stripe",new Vector3(side*1.35f,y,0),new Vector3(.025f,.055f,10.2f),dark);
                Box("Front headlamp",new Vector3(side*.95f,.95f,schoolBus?6.46f:5.46f),new Vector3(.45f,.22f,.075f),lamp);
                Box("Rear lamp",new Vector3(side*1.03f,1.05f,-5.46f),new Vector3(.2f,.55f,.06f),red);
                Box("Coach mirror arm",new Vector3(side*1.5f,2.9f,4.8f),new Vector3(.4f,.06f,.08f),dark);
                Box("Coach mirror",new Vector3(side*1.7f,2.7f,4.8f),new Vector3(.18f,.4f,.22f),paint);
                foreach(float axle in new[]{-3.5f,3.45f})
                {
                    var spin=new GameObject("Bus rolling wheel").transform;spin.SetParent(transform,false);spin.localPosition=new Vector3(side*1.32f,.52f,axle);wheels.Add(spin);
                    var tire=Primitive("Bus tire",PrimitiveType.Cylinder,Vector3.zero,new Vector3(1.04f,.17f,1.04f),dark);tire.SetParent(spin,false);tire.localRotation=Quaternion.Euler(0,0,90);
                    var hub=Primitive("Chrome hub",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.68f,.025f,.68f),metal);hub.SetParent(spin,false);hub.localPosition=new Vector3(side*.18f,0,0);hub.localRotation=Quaternion.Euler(0,0,90);
                }
            }
            if(schoolBus)
            {
                Box("School bonnet",new Vector3(0,1.25f,5.4f),new Vector3(2.1f,.85f,1.9f),paint);
                Box("Bonnet radiator",new Vector3(0,1.3f,6.38f),new Vector3(1,.65f,.06f),dark);
                for(int side=-1;side<=1;side+=2)Box("School warning lamp",new Vector3(side*.9f,3.18f,5.12f),new Vector3(.18f,.14f,.08f),red);
            }
            door=new GameObject("Bus passenger door").transform;door.SetParent(transform,false);door.localPosition=new Vector3(1.38f,1.8f,3.7f);
            var panel=Box("Door glass",Vector3.zero,new Vector3(.09f,2.35f,1.05f),glass);panel.SetParent(door,false);panel.localPosition=Vector3.zero;
            var destination=new GameObject("Bus destination").AddComponent<TMPro.TextMeshPro>();destination.transform.SetParent(transform,false);destination.transform.localPosition=new Vector3(0,3.0f,5.12f);destination.transform.localRotation=Quaternion.Euler(0,180,0);destination.font=TMPro.TMP_Settings.GetFontAsset();destination.text=schoolBus?"SCHOOL / COLLEGE":"HAVEN CITY COACH";destination.fontSize=2.2f;destination.alignment=TMPro.TextAlignmentOptions.Center;destination.rectTransform.sizeDelta=new Vector2(2.3f,.35f);
        }
        private Transform Box(string name,Vector3 p,Vector3 s,Material m)=>Primitive(name,PrimitiveType.Cube,p,s,m);
        private Transform Primitive(string name,PrimitiveType type,Vector3 p,Vector3 s,Material m)
        {var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(transform,false);obj.transform.localPosition=p;obj.transform.localScale=s;if(type==PrimitiveType.Cube&&(name=="Bus lower body"||name=="Rounded roof cap"||name=="Panoramic window band"||name=="School bonnet"))obj.GetComponent<MeshFilter>().sharedMesh=roundedShell;obj.GetComponent<Renderer>().sharedMaterial=m;var c=obj.GetComponent<Collider>();c.enabled=false;if(Application.isPlaying)Destroy(c);else DestroyImmediate(c);return obj.transform;}
        private static Mesh RoundedShell()
        {
            var profile=new[]{new Vector2(-.4f,-.5f),new Vector2(.4f,-.5f),new Vector2(.5f,-.35f),new Vector2(.5f,.35f),new Vector2(.4f,.5f),new Vector2(-.4f,.5f),new Vector2(-.5f,.35f),new Vector2(-.5f,-.35f)};
            var vertices=new Vector3[34];var triangles=new List<int>();
            for(int ring=0;ring<4;ring++)for(int i=0;i<8;i++)
            {float scale=ring==0||ring==3?.94f:1;float z=ring==0?-.5f:ring==1?-.47f:ring==2?.47f:.5f;vertices[ring*8+i]=new Vector3(profile[i].x*scale,profile[i].y*scale,z);}
            vertices[32]=new Vector3(0,0,-.5f);vertices[33]=new Vector3(0,0,.5f);
            for(int ring=0;ring<3;ring++)for(int i=0;i<8;i++)
            {int a=ring*8+i,b=ring*8+(i+1)%8,c=b+8,d=a+8;triangles.AddRange(new[]{a,b,c,a,c,d});}
            for(int i=0;i<8;i++){triangles.AddRange(new[]{32,(i+1)%8,i,33,24+i,24+(i+1)%8});}
            var mesh=new Mesh{name="Rounded bus shell"};mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private void LateUpdate()
        {if(door!=null)door.localRotation=Quaternion.Slerp(door.localRotation,Quaternion.Euler(0,open?75:0,0),Time.deltaTime*5);if(body!=null)foreach(var wheel in wheels)wheel.Rotate(Vector3.right,Vector3.Dot(body.linearVelocity,transform.forward)/.52f*Mathf.Rad2Deg*Time.deltaTime,Space.Self);}
        private void OnDestroy(){if(roundedShell!=null){if(Application.isPlaying)Destroy(roundedShell);else DestroyImmediate(roundedShell);}foreach(var m in materials)if(m!=null){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}}
    }
}
