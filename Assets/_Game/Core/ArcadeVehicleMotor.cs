using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Surface-independent arcade drive with body-relative ground probes and low-friction collision contact.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArcadeVehicleMotor : MonoBehaviour
    {
        [SerializeField,Min(.1f)] private float groundReach=.75f;
        [SerializeField,Range(.05f,.75f)] private float maxStepHeight=.45f;
        private readonly Collider[] clearance=new Collider[32];
        private Rigidbody body;
        private Collider[] colliders;
        private PhysicsScene physicsScene;
        private PhysicsMaterial driveMaterial;
        private readonly RaycastHit[] hits=new RaycastHit[24];
        public bool Grounded {get;private set;}
        public void Prepare()
        {
            if(body==null)body=GetComponent<Rigidbody>();
            physicsScene=gameObject.scene.GetPhysicsScene();
            if(colliders==null)colliders=GetComponentsInChildren<Collider>();
            if(driveMaterial==null)driveMaterial=new PhysicsMaterial("Arcade vehicle contact")
                {staticFriction=.02f,dynamicFriction=.02f,bounciness=0,frictionCombine=PhysicsMaterialCombine.Minimum,bounceCombine=PhysicsMaterialCombine.Minimum};
            foreach(var collider in colliders)if(collider!=null&&collider.enabled&&!collider.isTrigger)collider.sharedMaterial=driveMaterial;
            body.useGravity=true;body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
            body.linearDamping=.05f;body.angularDamping=3f;body.interpolation=RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.solverIterations=10;body.solverVelocityIterations=4;
        }
        public void Drive(float throttle,float turn,bool brake,float acceleration,float topSpeed,float reverseSpeed,float steering,float brakeStrength,float grip,float forwardSign,float dt)
        {
            if(body==null)Prepare();
            if(body.isKinematic)return;
            if(RiverGroundPhysics.Submerged(body.worldCenterOfMass)){Grounded=false;var v=body.linearVelocity;body.linearVelocity=new Vector3(v.x*Mathf.Exp(-5*dt),v.y,v.z*Mathf.Exp(-5*dt));return;}
            var bounds=new Bounds(body.worldCenterOfMass,Vector3.one*.1f);bool found=false;
            foreach(var collider in colliders)
            {
                if(collider==null||!collider.enabled||collider.isTrigger)continue;
                if(!found){bounds=collider.bounds;found=true;}else bounds.Encapsulate(collider.bounds);
            }
            Grounded=false;
            // Probe from the actual body bounds, not a prefab pivot that may sit far above the wheels.
            Vector3 origin=new Vector3(bounds.center.x,bounds.min.y+.45f,bounds.center.z);
            int count=physicsScene.Raycast(origin,Vector3.down,hits,.45f+groundReach,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                if(hits[i].collider==null||hits[i].rigidbody==body||hits[i].transform.IsChildOf(transform)||hits[i].normal.y<.5f)continue;
                Grounded=true;break;
            }
            if(!Grounded)return;
            Vector3 forward=Vector3.ProjectOnPlane(transform.forward*forwardSign,Vector3.up).normalized;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            var velocity=body.linearVelocity;float speed=Vector3.Dot(velocity,forward);
            float input=Mathf.Clamp(throttle,-1,1);
            if(!brake && Mathf.Abs(input)>.01f)TryClimbStep(bounds,forward*Mathf.Sign(input));
            velocity=body.linearVelocity;
            float target=brake?0:input*(input>=0?topSpeed:reverseSpeed);
            float change=brake?Mathf.Max(brakeStrength,1)*Mathf.Max(1,Mathf.Abs(speed)):Mathf.Abs(input)>.01f?acceleration:2f;
            float next=Mathf.MoveTowards(speed,target,change*dt);
            float lateral=Vector3.Dot(velocity,right)*Mathf.Exp(-Mathf.Max(0,grip)*dt);
            body.linearVelocity=forward*next+right*lateral+Vector3.up*velocity.y;
            if(Mathf.Abs(input)>.01f)body.WakeUp();
            float rotation=Mathf.Clamp(turn,-1,1)*steering*Mathf.Clamp(next/3f,-1,1)*dt;
            body.MoveRotation(body.rotation*Quaternion.Euler(0,rotation,0));
        }
        private void TryClimbStep(Bounds bounds,Vector3 direction)
        {
            Vector3 right=Vector3.Cross(Vector3.up,direction);
            float front=Mathf.Abs(direction.x)*bounds.extents.x+Mathf.Abs(direction.z)*bounds.extents.z;
            float width=Mathf.Abs(right.x)*bounds.extents.x+Mathf.Abs(right.z)*bounds.extents.z;
            float rise=0;
            for(int probe=-1;probe<=1;probe++)
            {
                Vector3 origin=bounds.center+direction*(front+Mathf.Max(.18f,Mathf.Abs(Vector3.Dot(body.linearVelocity,direction))*Time.fixedDeltaTime+.12f))+right*(probe*width*.65f);
                origin.y=bounds.min.y+maxStepHeight+.12f;
                int count=physicsScene.Raycast(origin,Vector3.down,hits,maxStepHeight+.25f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                float closest=float.MaxValue;float height=0;
                for(int i=0;i<count;i++)
                {
                    if(hits[i].collider==null||hits[i].rigidbody==body||hits[i].transform.IsChildOf(transform)||hits[i].normal.y<.65f)continue;
                    if(hits[i].distance>=closest)continue;
                    closest=hits[i].distance;height=hits[i].point.y-bounds.min.y;
                }
                if(height>maxStepHeight)return;
                if(height>.005f)rise=Mathf.Max(rise,height);
            }
            if(rise<=0)return;
            float lift=rise+.075f;
            int overlaps=physicsScene.OverlapBox(bounds.center+Vector3.up*lift,Vector3.Max(bounds.extents-Vector3.one*.025f,Vector3.one*.02f),clearance,Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            // A saturated query or obstructed headroom rejects the step rather than bypassing collision.
            if(overlaps==clearance.Length)return;
            for(int i=0;i<overlaps;i++)
                if(clearance[i]!=null&&clearance[i].attachedRigidbody!=body&&!clearance[i].transform.IsChildOf(transform))return;
            body.position+=Vector3.up*lift;
            var velocity=body.linearVelocity;velocity.y=Mathf.Max(0,velocity.y);body.linearVelocity=velocity;
        }
        private void OnDestroy(){if(driveMaterial!=null)Destroy(driveMaterial);}
    }
}
