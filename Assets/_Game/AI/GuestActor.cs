using UnityEngine;
using UnityEngine.AI;

namespace RoadsideHaven
{
    /// <summary>Small visual wrapper around a guest's NavMeshAgent.</summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(GuestSelectable))]
    public sealed class GuestActor : MonoBehaviour
    {
        public NavMeshAgent Agent { get; private set; }

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            if (GetComponent<ProceduralWalker>() == null) gameObject.AddComponent<ProceduralWalker>();
        }
    }
}
