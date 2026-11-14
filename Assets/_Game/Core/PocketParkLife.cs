using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Animates the park's shared water surface and loops three lightweight citizens on a clear paved route.</summary>
    public sealed class PocketParkLife : MonoBehaviour
    {
        [SerializeField] private Renderer water;
        [SerializeField] private TownCitizenAgent[] citizens;
        [SerializeField] private Vector3[] loop;
        [SerializeField] private Material[] clothing;
        [SerializeField] private Material skin;
        private Material originalWater,flowingWater;
        private Vector3[][] routes;
        public int CitizenCount=>citizens==null?0:citizens.Length;
        public void Configure(Renderer waterRenderer,TownCitizenAgent[] people,Vector3[] waypoints,Material[] coats,Material skinMaterial)
        {water=waterRenderer;citizens=people;loop=waypoints;clothing=coats;skin=skinMaterial;}
        private void Start()
        {
            if(water!=null&&water.sharedMaterial!=null){originalWater=water.sharedMaterial;flowingWater=new Material(originalWater);water.sharedMaterial=flowingWater;}
            if(citizens==null||loop==null||loop.Length<3||clothing==null||clothing.Length==0)return;
            routes=new Vector3[citizens.Length][];
            for(int i=0;i<citizens.Length;i++)
            {
                if(citizens[i]==null)continue;int offset=i*loop.Length/citizens.Length;routes[i]=new Vector3[loop.Length+1];
                for(int j=0;j<loop.Length;j++)routes[i][j]=loop[(j+offset)%loop.Length];routes[i][loop.Length]=routes[i][0];
                citizens[i].Initialize(clothing[i%clothing.Length],skin,false);citizens[i].SetJogging(i==citizens.Length-1);citizens[i].Warp(routes[i][0]);citizens[i].Walk(routes[i]);
            }
        }
        private void Update()
        {
            if(flowingWater!=null)flowingWater.SetTextureOffset("_BaseMap",new Vector2(Time.time*.025f,-Time.time*.3f));
            if(routes==null)return;
            for(int i=0;i<citizens.Length;i++)if(citizens[i]!=null&&routes[i]!=null&&citizens[i].Arrived)citizens[i].Walk(routes[i]);
        }
        private void OnDestroy(){if(water!=null&&originalWater!=null)water.sharedMaterial=originalWater;if(flowingWater!=null)Destroy(flowingWater);}
    }
}