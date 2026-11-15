using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Six extra walkers on reserved garden loops and subtle gestures on seated visitors/vendors.</summary>
    public sealed class CommonsDetailLife : MonoBehaviour
    {
        [SerializeField] private TownCitizenAgent[] walkers;
        [SerializeField] private Vector3[] frontLoop, rearLoop;
        [SerializeField] private Material[] clothes;
        [SerializeField] private Material skin;
        [SerializeField] private Transform[] idleArms;
        private Vector3[][] routes;private Quaternion[] rests;
        public int WalkerCount=>walkers==null?0:walkers.Length;
        public void Configure(TownCitizenAgent[] people,Vector3[] front,Vector3[] rear,Material[] shirts,Material skinMaterial,Transform[] arms){walkers=people;frontLoop=front;rearLoop=rear;clothes=shirts;skin=skinMaterial;idleArms=arms;}
        private void Start()
        {
            rests=new Quaternion[idleArms.Length];for(int i=0;i<idleArms.Length;i++)rests[i]=idleArms[i].localRotation;
            routes=new Vector3[walkers.Length][];for(int i=0;i<walkers.Length;i++){var loop=i<3?frontLoop:rearLoop;int offset=(i%3)*(loop.Length-1)/3;routes[i]=new Vector3[loop.Length];for(int j=0;j<loop.Length;j++)routes[i][j]=loop[(j+offset)%(loop.Length-1)];walkers[i].Initialize(clothes[i%clothes.Length],skin,false);walkers[i].SetJogging(i==5);walkers[i].Warp(routes[i][0]);walkers[i].Walk(routes[i]);}
        }
        private void Update(){if(routes==null)return;for(int i=0;i<walkers.Length;i++)if(walkers[i]!=null&&walkers[i].Arrived)walkers[i].Walk(routes[i]);for(int i=0;i<idleArms.Length;i++)if(idleArms[i]!=null)idleArms[i].localRotation=rests[i]*Quaternion.Euler(Mathf.Sin(Time.time*1.3f+i)*3.5f,0,Mathf.Sin(Time.time*.8f+i)*2);}
    }
}
