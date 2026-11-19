using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Named local dialogue/quest giver.</summary>
    public sealed class WorldNpc : MonoBehaviour
    {
        [SerializeField] private string npcId;
        [SerializeField] private string displayName;
        private void Start()
        {
            if(GetComponent<TownCitizenAgent>()!=null||GetComponent<ProceduralWalker>()!=null||GetComponentInChildren<HumanVisual>()!=null)return;
            HumanVisual.HideOldVisuals(transform);
            HumanVisual.Create(transform,HumanIdentity.StableSeed(npcId??name),HumanIdentity.RoleFromName(displayName??name));
        }
        public string Id=>npcId;
        public string DisplayName=>displayName;
        public void Configure(string id,string label){npcId=id;displayName=label;}
    }
}
