using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
namespace RoadsideHaven
{
    /// <summary>Hospital access also works in older scenes which do not yet contain the campus.</summary>
    public sealed class HospitalTravel:MonoBehaviour
    {
        public const string ResourceName="HavenHospital";
        public string Status {get;private set;}="";
        bool travelling;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneLoaded+=SceneLoaded;Install();}
        static void SceneLoaded(Scene scene,LoadSceneMode mode)=>Install();
        public static HospitalTravel Install(MotelGameRoot motel=null)
        {
            if(!Application.isPlaying)return null;
            if(motel==null)motel=FindFirstObjectByType<MotelGameRoot>();if(motel==null)return null;
            var travel=motel.GetComponent<HospitalTravel>();return travel!=null?travel:motel.gameObject.AddComponent<HospitalTravel>();
        }
        public static void Visit(MotelGameRoot motel){var travel=Install(motel);if(travel!=null)travel.Go();}
        public void Go(){if(!travelling)StartCoroutine(Arrive());}
        public static HospitalCampusLife EnsureCampus(MotelGameRoot motel)
        {
            if(motel==null)return null;var existing=motel.GetComponentInChildren<HospitalCampusLife>(true);
            if(existing!=null){existing.gameObject.SetActive(true);existing.enabled=true;HospitalArchitecture.Apply(existing);return existing;}
            var prefab=Resources.Load<GameObject>(ResourceName);if(prefab==null)return null;
            var campus=Instantiate(prefab,motel.transform,false);campus.name=HospitalCampusLayout.RootName;
            var lamps=motel.GetComponentsInChildren<MeshRenderer>();
            foreach(var post in lamps){if(post.name!="Streetlight post"||post.transform.IsChildOf(campus.transform))continue;var at=post.bounds.center;bool blocked=false;foreach(var road in HospitalCampusLayout.Roads)if(road.Contains(new Vector2(at.x,at.z))){blocked=true;break;}if(!blocked)continue;
                foreach(var part in lamps){if(part.name!="Streetlight post"&&part.name!="Streetlight bulb")continue;var q=part.bounds.center;if(part.transform.parent==post.transform.parent&&Mathf.Abs(q.x-at.x)<.03f&&Mathf.Abs(q.z-at.z)<.03f)part.transform.position+=new Vector3(992-at.x,0,0);}}
            // Legacy scenes have their older road collider. Rebuild once so the new approach is drivable.
            RoadSurfacePhysics.Rebuild(motel.transform);var life=campus.GetComponent<HospitalCampusLife>();HospitalArchitecture.Apply(life);return life;
        }
        IEnumerator Arrive()
        {
            travelling=true;Status="Opening hospital…";
            var motel=GetComponent<MotelGameRoot>();var campus=EnsureCampus(motel);
            if(campus==null){Status="Hospital could not load. Check that HavenHospital is installed in Resources.";travelling=false;yield break;}
            // A newly loaded campus and owner finish their Start callbacks before teleporting.
            yield return null;
            campus.VisitHospital();Status=campus.Status;travelling=false;
        }
        void Update(){var keys=Keyboard.current;if(keys!=null&&keys.f11Key.wasPressedThisFrame)Go();}
    }
}
