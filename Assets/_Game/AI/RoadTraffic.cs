using UnityEngine;

namespace RoadsideHaven
{
    /// <summary>Fixed-step kinematic traffic along a closed decorative road loop.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RoadTraffic : MonoBehaviour
    {
        [Header("Route")]
        [SerializeField] private Vector3[] waypoints;
        [SerializeField, Min(.1f)] private float speed = 5f;
        private Rigidbody vehicle;
        private int next;

        public void Configure(Vector3[] route, float metersPerSecond)
        {
            waypoints = route;
            speed = Mathf.Max(.1f, metersPerSecond);
        }

        private void Awake()
        {
            vehicle = GetComponent<Rigidbody>();
            vehicle.isKinematic = true;
            vehicle.useGravity = false;
            vehicle.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void FixedUpdate()
        {
            if (waypoints == null || waypoints.Length < 2) return;
            Vector3 destination = waypoints[next];
            Vector3 direction = destination - vehicle.position;
            vehicle.MovePosition(Vector3.MoveTowards(vehicle.position, destination, speed * Time.fixedDeltaTime));
            if (direction.sqrMagnitude > .001f)
                vehicle.MoveRotation(Quaternion.RotateTowards(vehicle.rotation,
                    Quaternion.LookRotation(-direction), 140f * Time.fixedDeltaTime));
            if (direction.sqrMagnitude < .04f) next = (next + 1) % waypoints.Length;
        }
    }
}
