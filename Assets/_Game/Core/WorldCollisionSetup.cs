using System.Collections;
using UnityEngine;
using UnityEngine.AI;
namespace RoadsideHaven
{
    /// <summary>Repairs generated decoration collision in saved scenes and runtime world upgrades.</summary>
    public sealed class WorldCollisionSetup : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Primitive decorators destroy temporary colliders at frame end; repair only after that pass.
            yield return null;
            Apply(transform,true);
        }
        public static int Apply(Transform root,bool addCarving)
        {
            int repaired=0;
            // The old roadside sign predates the wider highway; move the entire assembly to its shoulder.
            foreach(var marker in root.GetComponentsInChildren<Transform>(true))
                if(marker.name=="Sign post" && Mathf.Abs(marker.position.x+11)<.5f && marker.position.z>-20 && marker.position.z<-6)
                {
                    float shift=-21-marker.position.z;
                    foreach(var part in root.GetComponentsInChildren<Transform>(true))
                        if(part.name=="Sign post"||part.name=="Roadside sign"||part.name=="Motel sign lettering")part.position+=Vector3.forward*shift;
                    break;
                }
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.GetComponent<TMPro.TMP_Text>()!=null||renderer.GetComponentInParent<PhysicalWorldSign>()!=null)continue;
                if(!WorldCollisionRules.IsSolidName(renderer.name))continue;
                // Dynamic actors/vehicles already own their authoritative capsule/box colliders.
                if(renderer.GetComponentInParent<Rigidbody>()!=null||renderer.GetComponentInParent<CharacterController>()!=null||renderer.GetComponentInParent<NavMeshAgent>()!=null)continue;
                var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
                var meshBounds=filter.sharedMesh.bounds;
                var colliders=renderer.GetComponents<Collider>();
                if(colliders.Length==0)
                {
                    var box=renderer.gameObject.AddComponent<BoxCollider>();box.center=meshBounds.center;box.size=meshBounds.size;repaired++;
                }
                else foreach(var collider in colliders)
                    if(!collider.enabled||collider.isTrigger){collider.enabled=true;collider.isTrigger=false;repaired++;}
                // Carve only newly collidable decor touching existing pedestrian navigation.
                // Existing walls/floors were baked already; avoid carving navigation surfaces themselves.
                string name=renderer.name.ToLowerInvariant();
                bool obstacle=name.Contains("tree trunk")||name.Contains("planter")||name.Contains("car body")||name.Contains("bench")||
                    name.Contains("shelf back")||name.Contains("pole")||name.EndsWith(" post")||name=="npc body";
                if(!addCarving||!obstacle||renderer.GetComponent<NavMeshObstacle>()!=null||!renderer.gameObject.activeInHierarchy)continue;
                Vector3 ground=renderer.bounds.center;ground.y=0;
                if(!NavMesh.SamplePosition(ground,out _,.8f,NavMesh.AllAreas))continue;
                var carve=renderer.gameObject.AddComponent<NavMeshObstacle>();carve.shape=NavMeshObstacleShape.Box;
                carve.center=meshBounds.center;carve.size=meshBounds.size;carve.carving=true;carve.carveOnlyStationary=true;
            }
            Physics.SyncTransforms();
            return repaired;
        }
    }
}
