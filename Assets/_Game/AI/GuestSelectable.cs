using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Scene marker identifying a currently spawned guest actor.</summary>
    public sealed class GuestSelectable : MonoBehaviour
    {
        public string GuestId { get; private set; }

        public void Configure(string id) => GuestId = id;
    }
}

