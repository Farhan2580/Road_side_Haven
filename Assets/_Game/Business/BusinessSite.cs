using TMPro;
using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Property board displays ownership in the active universe without replacing the physical sign.</summary>
    public sealed class BusinessSite : MonoBehaviour
    {
        [SerializeField] private int propertyIndex;
        [SerializeField] private string propertyName;
        [SerializeField] private TextMeshPro sign;
        [SerializeField] private BusinessEmpireController empire;
        private float refresh;
        public void Configure(int index,string name,TextMeshPro label,BusinessEmpireController controller){propertyIndex=index;propertyName=name;sign=label;empire=controller;}
        private void Update(){refresh-=Time.deltaTime;if(refresh>0)return;refresh=.5f;if(sign!=null&&empire!=null)sign.text=propertyName+"\n<size=65%>"+empire.SiteStatus(propertyIndex)+"</size>";}
    }
}
