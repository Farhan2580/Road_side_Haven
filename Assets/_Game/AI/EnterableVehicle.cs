using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Marks a generated/prefab vehicle that the owner can take over without a purchase check.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class EnterableVehicle : MonoBehaviour
    {
        [SerializeField] private float forwardSign=1;
        [SerializeField,Range(.3f,1.5f)] private float speedScale=1;
        public float ForwardSign=>forwardSign;
        public float SpeedScale=>speedScale;
        public void Configure(float direction,float speed=1){forwardSign=direction<0?-1:1;speedScale=speed;}
        public void ReleaseControl(){var school=GetComponent<SchoolBusService>();if(school!=null)school.ResumeService();}
        public void TakeControl()
        {
            var traffic=GetComponent<RoadTraffic>();if(traffic!=null)traffic.enabled=false;
            var town=GetComponent<TownVehicleAgent>();if(town!=null)town.enabled=false;
            var body=GetComponent<Rigidbody>();body.isKinematic=false;body.useGravity=true;
            body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
            body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            var motor=GetComponent<ArcadeVehicleMotor>();if(motor==null)motor=gameObject.AddComponent<ArcadeVehicleMotor>();motor.Prepare();body.WakeUp();
        }
    }
}
