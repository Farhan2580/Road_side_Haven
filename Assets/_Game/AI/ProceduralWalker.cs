using UnityEngine;
using UnityEngine.AI;
namespace RoadsideHaven
{
    /// <summary>Navigation-driven human animation, with optional authored rigged prefab support.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ProceduralWalker : MonoBehaviour
    {
        [SerializeField] private GameObject animatedCharacterPrefab;
        [SerializeField] private string speedParameter="Speed";
        private NavMeshAgent agent;
        private HumanVisual human;
        private Animator animator;
        private bool hasSpeedParameter;
        private float displayedSpeed;
        private int archetype;
        public void ConfigureArchetype(int value)=>archetype=value;
        public void ConfigureVisual(GameObject prefab)=>animatedCharacterPrefab=prefab;
        private void Start()
        {
            agent=GetComponent<NavMeshAgent>();HumanVisual.HideOldVisuals(transform);
            if(animatedCharacterPrefab!=null)
            {
                var character=Instantiate(animatedCharacterPrefab,transform,false);
                foreach(var collider in character.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                foreach(var body in character.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
                animator=character.GetComponentInChildren<Animator>();
                if(animator!=null){animator.applyRootMotion=false;foreach(var p in animator.parameters)if(p.name==speedParameter&&p.type==AnimatorControllerParameterType.Float)hasSpeedParameter=true;}
            }
            else
            {
                int seed=HumanIdentity.StableSeed(name);var identity=HumanIdentity.Create(seed);
                if(archetype==1){identity.Skin=new Color(.48f,.65f,.7f);identity.Shirt=new Color(.1f,.7f,.85f);}
                if(archetype==2)identity.Shirt=new Color(.35f,.32f,.2f);
                if(archetype==3){identity.Skin=new Color(.78f,.72f,.56f);identity.Shirt=new Color(.23f,.58f,.32f);}
                var visual=new GameObject("Guest human");visual.transform.SetParent(transform,false);human=visual.AddComponent<HumanVisual>();human.AutomaticAnimation=false;human.Initialize(identity);
            }
            agent.avoidancePriority=25+HumanIdentity.StableSeed(name)%50;agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }
        private void LateUpdate()
        {
            if(agent==null||!agent.enabled||!agent.isOnNavMesh)return;
            displayedSpeed=Mathf.MoveTowards(displayedSpeed,agent.velocity.magnitude,Time.deltaTime*6);
            if(animator!=null&&hasSpeedParameter)animator.SetFloat(speedParameter,displayedSpeed,.12f,Time.deltaTime);
            if(human!=null)human.Animate(displayedSpeed);
        }
    }
}
