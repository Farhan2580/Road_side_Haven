using UnityEngine;
using UnityEngine.SceneManagement;
namespace RoadsideHaven
{
    /// <summary>A replaceable exterior preserves furnished floors, staff, lift and air-ambulance references.</summary>
    public sealed class HospitalArchitecture:MonoBehaviour
    {
        public const string Layer="Hospital modern architecture v2";
        public const string Resource="HospitalModernArchitecture";
        [SerializeField] Transform[] ripples;
        Vector3[] sizes;Camera view;float nextFrame;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;ApplyAll();}
        static void Loaded(Scene scene,LoadSceneMode mode)=>ApplyAll();
        static void ApplyAll(){foreach(var campus in FindObjectsByType<HospitalCampusLife>(FindObjectsSortMode.None))Apply(campus);}
        public static bool Apply(HospitalCampusLife campus)
        {
            if(campus==null||campus.transform.Find(Layer)!=null)return false;
            var prefab=Resources.Load<GameObject>(Resource);if(prefab==null)return false;
            var style=Instantiate(prefab,campus.transform,false);style.name=Layer;
            var old=campus.transform.Find("Medical campus architecture");if(old!=null)old.gameObject.SetActive(false);
            campus.RefreshExterior();return true;
        }
        public void Configure(Transform[] waterRipples){ripples=waterRipples;}
        void Start(){if(ripples==null)return;sizes=new Vector3[ripples.Length];for(int i=0;i<ripples.Length;i++)sizes[i]=ripples[i].localScale;}
        void Update()
        {
            if(sizes==null||Time.unscaledTime<nextFrame)return;nextFrame=Time.unscaledTime+1f/24;
            if(view==null)view=Camera.main;if(view!=null&&(view.transform.position-HospitalCampusLayout.Centre).sqrMagnitude>180*180)return;
            for(int i=0;i<ripples.Length;i++)if(ripples[i]!=null){float phase=Mathf.Repeat(Time.time*.12f+i*.27f,1);ripples[i].localScale=sizes[i]*(.55f+phase*.45f);}
        }
    }
}
