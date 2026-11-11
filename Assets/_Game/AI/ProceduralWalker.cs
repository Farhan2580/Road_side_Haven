using UnityEngine;
using UnityEngine.AI;

namespace RoadsideHaven
{
    /// <summary>Agent-driven articulated placeholder character, ready to be replaced by a rigged model.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ProceduralWalker : MonoBehaviour
    {
        [Header("Walking animation")]
        [SerializeField, Min(.1f)] private float strideLength = 1.5f;
        [SerializeField, Range(0, 45)] private float swingAngle = 28f;
        [Tooltip("Optional rigged character prefab with an Animator. Leave empty for the built-in character.")]
        [SerializeField] private GameObject animatedCharacterPrefab;
        [Tooltip("Optional Animator float parameter receiving agent speed in meters per second.")]
        [SerializeField] private string speedParameter = "Speed";
        private NavMeshAgent agent;
        private Transform leftArm, rightArm, leftLeg, rightLeg, body;
        private float phase;
        private Material clothing;
        private Material skin;
        private Material dark;
        private int archetype;
        public void ConfigureArchetype(int value) => archetype = value;
        private Animator animator;
        private bool hasSpeedParameter;
        private float displayedSpeed;

        /// <summary>Sets the optional visual before Start creates the character.</summary>
        public void ConfigureVisual(GameObject prefab) => animatedCharacterPrefab = prefab;

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            var original = GetComponentsInChildren<Renderer>();
            if (animatedCharacterPrefab != null)
            {
                var character = Instantiate(animatedCharacterPrefab, transform, false);
                // Navigation and selection belong to the wrapper, not the visual prefab.
                foreach (var collider in character.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var rigidbody in character.GetComponentsInChildren<Rigidbody>(true))
                {
                    rigidbody.isKinematic = true;
                    rigidbody.useGravity = false;
                }
                // Navigation and selection belong to the wrapper, not the visual prefab.
                foreach (var collider in character.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var rigidbody in character.GetComponentsInChildren<Rigidbody>(true))
                {
                    rigidbody.isKinematic = true;
                    rigidbody.useGravity = false;
                }
                animator = character.GetComponentInChildren<Animator>();
                foreach (var renderer in original) renderer.enabled = false;
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    foreach (var parameter in animator.parameters)
                        if (parameter.name == speedParameter && parameter.type == AnimatorControllerParameterType.Float)
                            hasSpeedParameter = true;
                }
                return;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { enabled = false; return; }
            Color[] palette = { new Color(.18f, .39f, .47f), new Color(.58f, .24f, .18f),
                new Color(.30f, .35f, .24f), new Color(.42f, .29f, .48f) };
            clothing = new Material(shader) { color = palette[Mathf.Abs(GetInstanceID() % palette.Length)] };
            skin = new Material(shader) { color = new Color(.72f, .48f, .32f) };
            if (archetype == 1) { skin.color = new Color(.48f,.65f,.7f); clothing.color = new Color(.10f,.7f,.85f); }
            if (archetype == 2) clothing.color = new Color(.35f,.32f,.20f);
            if (archetype == 3) { skin.color = new Color(.78f,.72f,.56f); clothing.color = new Color(.23f,.58f,.32f); }
            dark = new Material(shader) { color = new Color(.10f, .08f, .07f) };
            foreach (var renderer in original) renderer.enabled = false;
            body = new GameObject("Animated character").transform;
            body.SetParent(transform, false);
            Part(body, "Torso", new Vector3(0, 1.02f, 0), new Vector3(.45f, .58f, .26f), clothing);
            Part(body, "Head", new Vector3(0, 1.52f, 0), new Vector3(.30f, .34f, .30f), skin);
            if (archetype == 1) Part(body,"Robot antenna",new Vector3(0,1.88f,0),new Vector3(.06f,.28f,.06f),clothing);
            if (archetype == 3) { Part(body,"Elf ear left",new Vector3(-.20f,1.57f,0),new Vector3(.18f,.08f,.08f),skin); Part(body,"Elf ear right",new Vector3(.20f,1.57f,0),new Vector3(.18f,.08f,.08f),skin); }
            Part(body, "Hair", new Vector3(0, 1.66f, -.025f), new Vector3(.32f, .14f, .30f), dark);
            Part(body, "Left eye", new Vector3(-.065f, 1.55f, .143f), new Vector3(.035f, .035f, .025f), dark);
            Part(body, "Right eye", new Vector3(.065f, 1.55f, .143f), new Vector3(.035f, .035f, .025f), dark);
            leftArm = Limb("Left arm", new Vector3(-.30f, 1.23f, 0), .5f, .13f, skin);
            rightArm = Limb("Right arm", new Vector3(.30f, 1.23f, 0), .5f, .13f, skin);
            leftLeg = Limb("Left leg", new Vector3(-.13f, .73f, 0), .65f, .17f, clothing);
            rightLeg = Limb("Right leg", new Vector3(.13f, .73f, 0), .65f, .17f, clothing);
            Part(leftLeg, "Left shoe", new Vector3(0, -.65f, .06f), new Vector3(.21f, .13f, .34f), dark);
            Part(rightLeg, "Right shoe", new Vector3(0, -.65f, .06f), new Vector3(.21f, .13f, .34f), dark);
        }

        private Transform Limb(string name, Vector3 position, float length, float width, Material material)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(body, false);
            pivot.localPosition = position;
            Part(pivot, name + " mesh", Vector3.down * length * .5f, new Vector3(width, length, width), material);
            return pivot;
        }

        private static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(scale.x, scale.y * .5f, scale.z);
            part.GetComponent<Renderer>().sharedMaterial = material;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
        }

        private void LateUpdate()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            float speed = displayedSpeed = Mathf.MoveTowards(displayedSpeed, agent.velocity.magnitude, Time.deltaTime * 6f);
            if (animator != null && hasSpeedParameter)
                animator.SetFloat(speedParameter, speed, .12f, Time.deltaTime);
            if (body == null) return;
            phase += speed * Time.deltaTime * Mathf.PI * 2f / strideLength;
            float swing = Mathf.Sin(phase) * swingAngle * Mathf.Clamp01(speed);
            leftArm.localRotation = rightLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            rightArm.localRotation = leftLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            body.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * .035f * Mathf.Clamp01(speed) +
                Mathf.Sin(Time.time * 2f) * .006f * (1f - Mathf.Clamp01(speed)));
        }

        private void OnDestroy()
        {
            if (clothing != null) Destroy(clothing);
            if (skin != null) Destroy(skin);
            if (dark != null) Destroy(dark);
        }
    }

}
