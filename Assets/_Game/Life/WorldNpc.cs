using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Named local dialogue/quest giver.</summary>
    public sealed class WorldNpc : MonoBehaviour
    {
        [SerializeField] private string npcId;
        [SerializeField] private string displayName;
        public string Id=>npcId;
        public string DisplayName=>displayName;
        public void Configure(string id,string label){npcId=id;displayName=label;}
    }
}
