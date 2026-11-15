using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Small fixed population, flowing fountain normals and two slowly rotating public art pieces.</summary>
    public sealed class CommonsParkLife : MonoBehaviour
    {
        [SerializeField]private TownCitizenAgent[] citizens;
        [SerializeField]private Transform[] sculptures;
        [SerializeField]private Renderer[] water;
        [SerializeField]private Material[] coats;
        [SerializeField]private Material skin;
        private Material[] original,flowing;private Vector3[][] routes;
        public int VisitorCount=>citizens==null?0:citizens.Length;
        public void Configure(TownCitizenAgent[] people,Transform[] art,Renderer[] surfaces,Material[] clothes,Material skinMaterial){citizens=people;sculptures=art;water=surfaces;coats=clothes;skin=skinMaterial;}
        private void Start(){original=new Material[water.Length];flowing=new Material[water.Length];for(int i=0;i<water.Length;i++)if(water[i]!=null){original[i]=water[i].sharedMaterial;flowing[i]=new Material(original[i]);water[i].sharedMaterial=flowing[i];}routes=new Vector3[citizens.Length][];for(int i=0;i<citizens.Length;i++){float a=i<3?-100:212,b=i<3?-94:217;var loop=new[]{new Vector3(650,.12f,a),new Vector3(945,.12f,a),new Vector3(945,.12f,b),new Vector3(650,.12f,b)};int offset=i%3;routes[i]=new Vector3[5];for(int j=0;j<5;j++)routes[i][j]=loop[(j+offset)%4];citizens[i].Initialize(coats[i%coats.Length],skin,false);citizens[i].SetJogging(i%3==2);citizens[i].Warp(routes[i][0]);citizens[i].Walk(routes[i]);}}
        private void Update(){for(int i=0;i<flowing.Length;i++)if(flowing[i]!=null){var offset=new Vector2(Time.time*.025f,-Time.time*.035f);flowing[i].SetTextureOffset("_BaseMap",offset);flowing[i].SetTextureOffset("_BumpMap",offset);}foreach(var art in sculptures)if(art!=null)art.Rotate(0,Time.deltaTime*12,0,Space.Self);if(routes!=null)for(int i=0;i<citizens.Length;i++)if(citizens[i]!=null&&citizens[i].Arrived)citizens[i].Walk(routes[i]);}
        private void OnDestroy(){if(water==null||flowing==null)return;for(int i=0;i<water.Length;i++){if(water[i]!=null)water[i].sharedMaterial=original[i];if(flowing[i]!=null)Destroy(flowing[i]);}}
    }
}
