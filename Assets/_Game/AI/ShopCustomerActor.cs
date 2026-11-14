using UnityEngine;
using UnityEngine.AI;

namespace RoadsideHaven
{
    /// <summary>Navigation wrapper for a walk-in shopper, independent of motel guest queues.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ShopCustomerActor : MonoBehaviour
    {
        private NavMeshAgent agent;
        private NavMeshPath path;

        public bool IsOnMesh => agent != null && agent.enabled && agent.isOnNavMesh;
        public bool HasArrived => IsOnMesh && !agent.pathPending &&
            agent.pathStatus == NavMeshPathStatus.PathComplete &&
            agent.remainingDistance <= agent.stoppingDistance + 0.12f;
        public bool HasFailedPath => !IsOnMesh || (!agent.pathPending &&
            agent.pathStatus != NavMeshPathStatus.PathComplete);

        private void Awake()
        {
            path = new NavMeshPath();
            agent = GetComponent<NavMeshAgent>();
            if (GetComponent<ProceduralWalker>() == null) gameObject.AddComponent<ProceduralWalker>();
        }

        /// <summary>Rejects partial paths and destinations snapped onto a different nearby surface.</summary>
        public bool TryMove(Vector3 destination)
        {
            if (!IsOnMesh || !NavMesh.SamplePosition(destination, out var hit, 0.7f, agent.areaMask))
                return false;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;
            agent.isStopped = false;
            return agent.SetPath(path);
        }

        public void Stop()
        {
            if (IsOnMesh) agent.isStopped = true;
        }
    }
}
