using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Small, clear medical-staff routes; no city-wide path search inside furnished rooms.</summary>
    public sealed class HospitalPerson:MonoBehaviour
    {
        [SerializeField] int seed,floor;
        [SerializeField] bool doctor,patient,resting;
        [SerializeField] Vector3[] route;
        HumanVisual human;int next;float wait;bool sleeping;
        public int Floor=>floor;
        public HumanVisual Human=>human;
        public void Configure(int id,int level,bool medic,bool sick,bool bed,Vector3[] points){seed=id;floor=level;doctor=medic;patient=sick;resting=bed;route=points;}
        void CreateHuman()
        {
            if(human!=null)return;
            var identity=HumanIdentity.Create(seed,doctor?HumanRole.Doctor:HumanRole.Civilian);
            if(patient){identity.Shirt=new Color(.62f,.81f,.82f);identity.Top=HumanTop.Shirt;identity.Trousers=new Color(.45f,.64f,.70f);identity.Cap=false;identity.Backpack=false;identity.Handbag=false;identity.Phone=false;identity.Umbrella=false;}
            human=HumanVisual.Create(transform,identity,true);
            if(resting){human.AutomaticAnimation=false;human.transform.localRotation=Quaternion.Euler(-90,180,0);human.transform.localPosition=new Vector3(0,1.03f,-.83f);}
            else if(route==null||route.Length==0)human.Gesture=doctor?HumanGesture.Talk:HumanGesture.Relaxed;
        }
        public void SetVisible(bool visible)
        {sleeping=!visible;if(visible)CreateHuman();if(human!=null&&human.gameObject.activeSelf!=visible)human.gameObject.SetActive(visible);}
        void Update()
        {
            if(sleeping||human==null)return;
            if(resting){human.transform.localPosition=new Vector3(0,1.03f+Mathf.Sin(Time.time*1.3f+seed)*.006f,-.83f);return;}
            if(route==null||route.Length==0)return;
            if(wait>0){wait-=Time.deltaTime;return;}var delta=route[next]-transform.localPosition;delta.y=0;
            if(delta.sqrMagnitude<.04f){next=(next+1)%route.Length;wait=1.5f+(seed%5)*.35f;return;}
            transform.localPosition+=delta.normalized*Mathf.Min(delta.magnitude,Time.deltaTime*(.8f+(seed%4)*.12f));transform.localRotation=Quaternion.Slerp(transform.localRotation,Quaternion.LookRotation(delta),Time.deltaTime*5);
        }
    }
}
