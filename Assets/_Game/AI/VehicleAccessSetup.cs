using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Registers traffic and previously decorative parked vehicles after scene composition.</summary>
    public sealed class VehicleAccessSetup : MonoBehaviour
    {
        private IEnumerator Start(){yield return null;Refresh();}
        public void Refresh()
        {
            var driving=GetComponentInChildren<WorldDrivingController>(true);if(driving==null)return;
            // Saved scenes contain parked cars as ungrouped meshes; combine only known vehicle pieces.
            foreach(var body in GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!body.gameObject.activeInHierarchy||body.name!="Car body"||body.GetComponentInParent<Rigidbody>()!=null)continue;
                Vector3 center=body.transform.position-Vector3.up*.45f;
                var siblings=body.transform.parent.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r=>r.GetComponentInParent<Rigidbody>()==null&&IsCarPart(r.name)&&(r.bounds.center-center).sqrMagnitude<10).ToArray();
                MakeVehicle(siblings,"Enterable parked car",-1,1);
            }
            foreach(var cab in GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!cab.gameObject.activeInHierarchy||cab.name!="Parked truck cab"||cab.GetComponentInParent<Rigidbody>()!=null)continue;
                var trailer=cab.transform.parent.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name=="Parked truck trailer"&&r.GetComponentInParent<Rigidbody>()==null&&Mathf.Abs(r.bounds.center.x-cab.bounds.center.x)<1&&(r.bounds.center-cab.bounds.center).sqrMagnitude<1000);
                if(trailer!=null)MakeVehicle(new[]{cab,trailer},"Enterable highway truck",-1,.65f);
            }
            int carStyle=0;
            foreach(var body in GetComponentsInChildren<Rigidbody>(true))
            {
                if(!body.gameObject.activeInHierarchy)continue;
                var access=body.GetComponent<EnterableVehicle>();
                bool traffic=body.GetComponent<RoadTraffic>()!=null;
                if(access==null&&!traffic&&!driving.IsRegistered(body))continue;
                if(access==null){access=body.gameObject.AddComponent<EnterableVehicle>();access.Configure(traffic?-1:1);}
                driving.RegisterVehicle(body);
                if(body.GetComponent<ModernCarVisuals>()==null && !body.name.Contains("CNG") && body.GetComponentsInChildren<MeshRenderer>(true).Any(r=>r.name=="Car body"))
                    body.gameObject.AddComponent<ModernCarVisuals>().Configure(carStyle++);
            }
            Physics.SyncTransforms();
        }
        private static bool IsCarPart(string name)=>name=="Car body"||name=="Car cabin"||name=="Car roof"||name=="Tire"||name=="Headlight";
        private void MakeVehicle(MeshRenderer[] parts,string name,float forward,float speed)
        {
            if(parts.Length==0)return;
            var bounds=parts[0].bounds;foreach(var part in parts)bounds.Encapsulate(part.bounds);
            var root=new GameObject(name).transform;root.SetParent(transform,false);root.position=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var part in parts)
            {
                part.transform.SetParent(root,true);
                foreach(var obstacle in part.GetComponents<UnityEngine.AI.NavMeshObstacle>()){obstacle.enabled=false;Destroy(obstacle);}
                foreach(var collider in part.GetComponents<Collider>()){collider.enabled=false;Destroy(collider);}
            }
            var box=root.gameObject.AddComponent<BoxCollider>();box.center=bounds.center-root.position;box.size=bounds.size;
            var body=root.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.mass=speed<1?3000:1100;
            root.gameObject.AddComponent<EnterableVehicle>().Configure(forward,speed);
        }
    }
}
