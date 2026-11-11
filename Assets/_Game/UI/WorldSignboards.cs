using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
namespace RoadsideHaven
{
    /// <summary>Upgrades generated floating world labels into restrained vertical, double-sided signboards.</summary>
    public sealed class WorldSignboards : MonoBehaviour
    {
        private Material board,trim;
        private IEnumerator Start()
        {
            yield return null; // Wait for all additive world installers to finish.
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)yield break;
            board=new Material(shader){color=new Color(.035f,.115f,.13f)};
            trim=new Material(shader){color=new Color(.63f,.45f,.22f)};
            foreach(var label in GetComponentsInChildren<TextMeshPro>(true))
            {
                if(label.GetComponent<PhysicalWorldSign>()!=null)continue;
                string text=label.text;
                if(text=="ROADSIDE HAVEN" && label.transform.position.y<1){label.gameObject.SetActive(false);continue;}
                if(label.name=="Motel sign lettering")continue;
                bool room=label.GetComponentInParent<RoomSelectable>()!=null;
                bool npc=text.Contains("[L]");
                bool region=text.Contains("/ NORTH")||text.Contains("/ SOUTH")||text.Contains("/ WEST")||text.Contains("/ EAST")||text.Contains("/ MAIN BASE");
                float width=room?1.55f:npc?2.35f:region?7f:3.8f;
                float height=room?.72f:npc?.65f:region?1.2f:1f;
                Vector3 position=label.transform.position;
                if(room){position.x-=1.65f;position.y=1.32f;position.z-=.12f;}
                else if(position.y<1)position.y=2.1f;
                label.transform.position=position;
                label.transform.rotation=Quaternion.identity;
                label.transform.localScale=Vector3.one;
                label.fontStyle=FontStyles.Normal;
                label.alignment=TextAlignmentOptions.Center;
                label.enableAutoSizing=true;label.fontSizeMin=1.3f;label.fontSizeMax=room?3.4f:npc?3.2f:4.5f;
                label.rectTransform.sizeDelta=new Vector2(width-.22f,height-.12f);
                label.margin=new Vector4(.03f,.02f,.03f,.02f);
                label.color=new Color(.98f,.90f,.69f);
                label.raycastTarget=false;
                label.overflowMode=TextOverflowModes.Ellipsis;
                // Keep the TMP object/name intact for room and rival-status update references.
                Box(label.transform,"Signboard",new Vector3(0,0,.09f),new Vector3(width,height,.12f),board);
                Box(label.transform,"Top frame",new Vector3(0,height*.5f,.09f),new Vector3(width+.08f,.045f,.15f),trim);
                Box(label.transform,"Bottom frame",new Vector3(0,-height*.5f,.09f),new Vector3(width+.08f,.045f,.15f),trim);
                if(!room)
                {
                    float length=Mathf.Max(.1f,position.y-height*.5f);
                    if(position.y>5 && !region)length=Mathf.Min(1.2f,length);
                    for(int side=-1;side<=1;side+=2)
                        Box(label.transform,"Sign support",new Vector3(side*(width*.5f-.15f),-height*.5f-length*.5f,.12f),new Vector3(.06f,length,.06f),trim);
                }
                var back=new GameObject("Back lettering").AddComponent<TextMeshPro>();back.transform.SetParent(label.transform,false);
                back.transform.localPosition=new Vector3(0,0,.165f);back.transform.localRotation=Quaternion.Euler(0,180,0);
                back.font=label.font;back.fontStyle=FontStyles.Normal;back.alignment=TextAlignmentOptions.Center;
                back.enableAutoSizing=true;back.fontSizeMin=label.fontSizeMin;back.fontSizeMax=label.fontSizeMax;
                back.rectTransform.sizeDelta=label.rectTransform.sizeDelta;back.raycastTarget=false;back.overflowMode=TextOverflowModes.Ellipsis;
                label.gameObject.AddComponent<PhysicalWorldSign>().Configure(label,back,region?550:npc?18:room?30:65);
            }
        }
        private static void Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;
            obj.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            var collider=obj.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        }
        private void OnDestroy(){if(board!=null)Destroy(board);if(trim!=null)Destroy(trim);}
    }
}
