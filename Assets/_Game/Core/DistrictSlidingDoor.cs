using UnityEngine;
namespace RoadsideHaven
{
    /// <summary>Physical entrance leaves slide clear before the walking owner reaches the doorway.</summary>
    public sealed class DistrictSlidingDoor : MonoBehaviour
    {
        [SerializeField] private Transform left,right;
        [SerializeField] private float travel=2.2f;
        [SerializeField] private Vector3 leftClosed,rightClosed;
        private OwnerSandboxController owner;private float searchAt;
        public float OpenAmount {get;private set;}
        private float heldUntil;
        /// <summary>Keeps the doors open for staff, stretchers and visitors who are not the player.</summary>
        public void HoldOpen(float seconds){heldUntil=Mathf.Max(heldUntil,Time.time+seconds);}
        public void Configure(Transform a,Transform b,float distance){left=a;right=b;travel=distance;leftClosed=a.localPosition;rightClosed=b.localPosition;}
        private void Update(){if(left==null||right==null)return;if(owner==null&&Time.unscaledTime>=searchAt){owner=FindFirstObjectByType<OwnerSandboxController>();searchAt=Time.unscaledTime+1;}bool near=false;if(owner!=null&&owner.IsWalking){var p=transform.InverseTransformPoint(owner.transform.position);near=Mathf.Abs(p.x)<5&&Mathf.Abs(p.z)<5&&Mathf.Abs(p.y)<3;}near|=Time.time<heldUntil;OpenAmount=Mathf.MoveTowards(OpenAmount,near?1:0,Time.deltaTime*1.8f);left.localPosition=leftClosed+Vector3.left*(travel*OpenAmount);right.localPosition=rightClosed+Vector3.right*(travel*OpenAmount);}
    }
}
