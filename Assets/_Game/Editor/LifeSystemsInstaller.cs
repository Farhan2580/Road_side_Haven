using UnityEditor;
using UnityEngine;
namespace RoadsideHaven.Editor
{
    public static partial class VisualPolishInstaller
    {
        private static void InstallLifeSystems(Transform motel,IsometricCameraController camera)
        {
            if(motel.Find("Life Systems v1")!=null)return;
            var owner=motel.GetComponentInChildren<OwnerSandboxController>(true);
            var driving=motel.GetComponentInChildren<WorldDrivingController>(true);
            if(owner==null||driving==null)return;
            var root=new GameObject("Life Systems v1").transform;root.SetParent(motel,false);
            if(!Application.isPlaying)Undo.RegisterCreatedObjectUndo(root.gameObject,"Add vehicle progression and NPC life systems");
            var truck=new GameObject("Owner supply truck").transform;truck.SetParent(root,false);
            Box(truck,"Truck cargo body",new Vector3(0,1.6f,-.9f),new Vector3(2.5f,2.8f,3.8f),ivory);
            Box(truck,"Truck cabin",new Vector3(0,1.2f,2),new Vector3(2.4f,2.2f,1.9f),mint);
            Box(truck,"Truck windshield",new Vector3(0,1.7f,2.97f),new Vector3(2,.7f,.05f),glass);
            for(int side=-1;side<=1;side+=2)for(int axle=-1;axle<=1;axle+=2)
                Shape(truck,"Truck wheel",PrimitiveType.Sphere,new Vector3(side*1.25f,.4f,axle*1.9f),new Vector3(.3f,.8f,.8f),ink);
            truck.position=new Vector3(-10,.3f,-23);
            var collider=truck.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.4f,0);collider.size=new Vector3(2.6f,2.8f,6);
            var body=truck.gameObject.AddComponent<Rigidbody>();body.mass=2600;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
            body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            driving.AppendSupplyTruck(body);
            Npc(root,new Vector3(-8,0,-6),"mechanic","Garage mechanic");
            Npc(root,new Vector3(502,0,-227),"supplier","Supply agent");
            Npc(root,new Vector3(-340,0,4),"city-guide","City guide");
            Npc(root,new Vector3(185,0,695),"highway-driver","Highway driver");
            Npc(root,new Vector3(-344,0,-650),"beach-guide","Beach guide");
            foreach(var point in new[]{new Vector3(-350,0,0),new Vector3(180,0,690),new Vector3(-350,0,-650),new Vector3(500,0,-230)})
            {
                var portal=Material("Travel portal blue",new Color(.08f,.55f,1),true);
                for(int i=0;i<12;i++)
                {float angle=i*Mathf.PI/6;Box(root,"Fast travel portal",point+new Vector3(Mathf.Cos(angle)*1.4f,2+Mathf.Sin(angle)*1.7f,0),new Vector3(.3f,.3f,.3f),portal);}
                LabelWorld(root,"FAST TRAVEL [J]",point+Vector3.up*4.2f,5);
            }
            LabelWorld(root,"OWNER GARAGE [J]",new Vector3(-6,3,-6),5);
            var life=root.gameObject.AddComponent<OwnerLifeController>();life.Configure(motel.GetComponent<MotelGameRoot>(),owner,driving);
            driving.ConfigureLife(life);
            AssetDatabase.SaveAssets();
        }
        private static void Npc(Transform parent,Vector3 position,string id,string name)
        {
            var npc=new GameObject(name).transform;npc.SetParent(parent,false);
            Shape(npc,"NPC body",PrimitiveType.Capsule,Vector3.up*.85f,new Vector3(.55f,.85f,.55f),mint);
            npc.position=position;
            npc.gameObject.AddComponent<WorldNpc>().Configure(id,name);
            LabelWorld(parent,name+" [L]",position+Vector3.up*2.4f,3);
        }
    }
}
