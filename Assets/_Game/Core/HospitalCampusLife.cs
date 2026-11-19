using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace RoadsideHaven
{
    /// <summary>Floor-aware interior visibility and a bounded light pool keep the large campus affordable.</summary>
    public sealed class HospitalCampusLife:MonoBehaviour
    {
        [SerializeField] Transform[] interiors;
        [SerializeField] Renderer[] outside;
        Renderer[][] indoor;HospitalPerson[] people;OwnerSandboxController owner;Camera view;
        Light[] lamps;float refresh;GameObject liftPanel;TMP_Text caption;int selected;Image fade;float fadeTime;int pending=-1;
        public string Status {get;private set;}="";
        public int CurrentFloor=>owner==null?0:Mathf.Clamp(Mathf.RoundToInt((owner.transform.position.y-HospitalCampusLayout.Ground)/HospitalCampusLayout.Storey),0,HospitalCampusLayout.Floors);
        public void Configure(Transform[] floors,Renderer[] exterior){interiors=floors;outside=exterior;}
        public void RefreshExterior()
        {
            var renderers=new List<Renderer>();
            foreach(Transform child in transform){if(!child.gameObject.activeInHierarchy||child.name.StartsWith("Hospital level ")||child.GetComponent<HospitalHelicopter>()!=null)continue;renderers.AddRange(child.GetComponentsInChildren<Renderer>());}
            outside=renderers.ToArray();
        }
        void Start()
        {
            HospitalArchitecture.Apply(this);
            indoor=new Renderer[interiors.Length][];for(int i=0;i<interiors.Length;i++)indoor[i]=interiors[i].GetComponentsInChildren<Renderer>(true);
            people=GetComponentsInChildren<HospitalPerson>(true);lamps=new Light[Application.isMobilePlatform||Application.platform==RuntimePlatform.WebGLPlayer?4:6];
            for(int i=0;i<lamps.Length;i++){var l=new GameObject("Pooled hospital interior light").AddComponent<Light>();l.transform.SetParent(transform,false);l.type=LightType.Point;l.range=16;l.intensity=3;l.color=new Color(.84f,.95f,1);l.shadows=LightShadows.None;l.enabled=false;lamps[i]=l;}
            BuildLiftUI();
        }
        public void VisitHospital()
        {
            var motel=GetComponentInParent<MotelGameRoot>();
            if(owner==null)owner=motel==null?FindFirstObjectByType<OwnerSandboxController>():motel.GetComponentInChildren<OwnerSandboxController>(true);
            if(owner==null){Status="The player is not ready yet. Try HOSPITAL again.";return;}
            if(motel!=null){motel.GetComponent<CompactGameHUD>()?.Close();motel.GetComponentInChildren<ShopHUD>(true)?.Close();motel.GetComponentInChildren<OwnerLifeController>(true)?.ClosePanel();motel.GetComponentInChildren<MultiverseController>(true)?.ClosePortal();motel.GetComponentInChildren<BusinessEmpireController>(true)?.ClosePanel();}
            var driving=motel==null?null:motel.GetComponentInChildren<WorldDrivingController>(true);
            if(driving!=null)driving.TravelOnFoot(HospitalCampusLayout.Entrance);else owner.ExitVehicle(HospitalCampusLayout.Entrance);
            owner.transform.rotation=Quaternion.identity;
            if(Camera.main!=null)Camera.main.GetComponent<PcThirdPersonCamera>()?.ResetWalkingView();
            Status="Welcome to Haven Grand Medical Centre. Walk through the main doors. Lift: Page Up / Page Down, Enter.";
        }
        public bool RequestFloor(int floor)
        {
            if(owner==null)owner=FindFirstObjectByType<OwnerSandboxController>();if(owner==null||!owner.IsWalking||pending>=0)return false;
            var p=owner.transform.position-HospitalCampusLayout.Centre;if(Mathf.Abs(p.x+36)>3.8f||Mathf.Abs(p.z)>3.8f)return false;
            pending=Mathf.Clamp(floor,0,HospitalCampusLayout.Floors);fadeTime=.8f;return true;
        }
        void Update()
        {
            if(owner==null)owner=FindFirstObjectByType<OwnerSandboxController>();var keys=Keyboard.current;
            if(Time.unscaledTime>=refresh){refresh=Time.unscaledTime+.35f;Visibility();}
            bool near=false;if(owner!=null&&owner.IsWalking){var p=owner.transform.position-HospitalCampusLayout.Centre;near=Mathf.Abs(p.x+36)<3.8f&&Mathf.Abs(p.z)<3.8f;}
            if(liftPanel!=null)liftPanel.SetActive(near);
            if(near&&keys!=null){if(keys.pageUpKey.wasPressedThisFrame)selected=(selected+1)%7;if(keys.pageDownKey.wasPressedThisFrame)selected=(selected+6)%7;if(keys.enterKey.wasPressedThisFrame)RequestFloor(selected);}
            if(caption!=null&&near)caption.text="LIFT  |  " + CurrentFloor + " → " + selected + "  " + HospitalCampusLayout.Departments[selected]+"\nPage Up / Down: select   Enter: travel   |   Stairs beside east corridor";
            if(pending>=0){fadeTime-=Time.unscaledDeltaTime;if(fadeTime<=.4f){owner.ExitVehicle(HospitalCampusLayout.LiftLanding(pending));selected=pending;Status=HospitalCampusLayout.Departments[pending];pending=-1;}}
            if(fadeTime>0&&pending<0)fadeTime-=Time.unscaledDeltaTime;if(fade!=null){float a=fadeTime>.4f?( .8f-fadeTime)/.4f:fadeTime/.4f;fade.color=new Color(.02f,.06f,.08f,Mathf.Clamp01(a));fade.gameObject.SetActive(fadeTime>0);}
        }
        void Visibility()
        {
            if(view==null)view=Camera.main;var p=owner!=null&&owner.IsWalking?owner.transform.position:view!=null?view.transform.position:Vector3.zero;
            var d=p-HospitalCampusLayout.Centre;bool close=d.x*d.x+d.z*d.z<200*200;bool inside=owner!=null&&owner.IsWalking&&Mathf.Abs(d.x)<46&&Mathf.Abs(d.z)<25&&p.y<HospitalCampusLayout.Storey*7;int floor=CurrentFloor;
            if(outside!=null)foreach(var r in outside)if(r!=null)r.enabled=d.x*d.x+d.z*d.z<1100*1100;
            for(int i=0;i<indoor.Length;i++){
                bool shown=close&&(inside?Mathf.Abs(i-floor)<=1:i<=1);
                // Structural slabs remain visible from the street and map, even when furniture is culled.
                foreach(var r in indoor[i])if(r!=null)r.enabled=r.name=="Batched Pale terrazzo"?d.x*d.x+d.z*d.z<1100*1100:shown;
            }
            foreach(var person in people)person.SetVisible(close&&(inside?Mathf.Abs(person.Floor-floor)<=1:person.Floor==0));
            for(int i=0;i<lamps.Length;i++){lamps[i].enabled=inside&&close;if(!lamps[i].enabled)continue;float x=i<3?-22+i*22:-22+(i-3)*22;float z=i<3?0:12;lamps[i].transform.position=HospitalCampusLayout.Point(x,HospitalCampusLayout.Ground+floor*HospitalCampusLayout.Storey+3.5f,z);}
        }
        void BuildLiftUI()
        {
            var canvas=new GameObject("Hospital lift controls",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=43;
            var scale=canvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);
            var panel=Rect(canvas.transform,"Lift destinations",new Vector2(820,150),new Vector2(0,80));liftPanel=panel.gameObject;panel.gameObject.AddComponent<Image>().color=new Color(.025f,.09f,.12f,.94f);
            var t=Rect(panel,"Lift instructions",new Vector2(790,54),new Vector2(0,38));caption=t.gameObject.AddComponent<TextMeshProUGUI>();caption.fontSize=17;caption.alignment=TextAlignmentOptions.Center;
            for(int i=0;i<7;i++){int target=i;var rect=Rect(panel,"Floor "+i,new Vector2(100,44),new Vector2(-330+i*110,-30));rect.gameObject.AddComponent<Image>().color=new Color(.08f,.42f,.46f);var b=rect.gameObject.AddComponent<Button>();b.onClick.AddListener(()=>RequestFloor(target));var label=Rect(rect,"Label",new Vector2(100,44),Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>();label.text=i==6?"ROOF":"FLOOR "+i;label.fontSize=16;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;}
            fade=Rect(canvas.transform,"Lift fade",Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();var f=(RectTransform)fade.transform;f.anchorMin=Vector2.zero;f.anchorMax=Vector2.one;f.offsetMin=f.offsetMax=Vector2.zero;fade.raycastTarget=false;fade.gameObject.SetActive(false);liftPanel.SetActive(false);
        }
        static RectTransform Rect(Transform parent,string name,Vector2 size,Vector2 pos){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
    }
}
