using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace RoadsideHaven
{
    /// <summary>One scene-owned compact action menu; feature panels remain hidden until requested.</summary>
    public sealed class CompactGameHUD : MonoBehaviour
    {
        private GameObject menu,help;
        private TMP_Text clock,toast;
        private string lastOwner,lastDriver,lastLife,lastUniverse;
        private float toastUntil;
        private OwnerSandboxController owner;
        private WorldDrivingController driving;
        private OwnerLifeController life;
        private MultiverseController multiverse;
        private MotelAtmosphere atmosphere;
        private IsometricCameraController cameraController;
        public bool MenuOpen => menu!=null&&menu.activeSelf || help!=null&&help.activeSelf;
        private void Start()
        {
            owner=GetComponentInChildren<OwnerSandboxController>(true);
            driving=GetComponentInChildren<WorldDrivingController>(true);
            life=GetComponentInChildren<OwnerLifeController>(true);
            multiverse=GetComponentInChildren<MultiverseController>(true);
            atmosphere=GetComponent<MotelAtmosphere>();
            foreach(var candidate in Object.FindObjectsByType<IsometricCameraController>(FindObjectsSortMode.None))
                if(candidate.gameObject.scene==gameObject.scene){cameraController=candidate;break;}
            var canvas=new GameObject("Compact Gameplay HUD",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=40;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var safe=Rect(canvas.transform,"Safe area",new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
            safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;safe.offsetMin=Vector2.zero;safe.offsetMax=Vector2.zero;safe.gameObject.AddComponent<SafeAreaFitter>();
            var label=Rect(safe,"Time",new Vector2(0,1),new Vector2(20,-66),new Vector2(600,26));label.pivot=new Vector2(0,1);
            clock=Label(label,"",17);clock.alignment=TextAlignmentOptions.Left;
            Button(safe,"MENU",new Vector2(-90,-75),new Vector2(130,36),()=>{bool open=!MenuOpen;Close();menu.SetActive(open);});
            var panel=Rect(safe,"Actions",new Vector2(1,1),new Vector2(-20,-100),new Vector2(250,326));panel.pivot=new Vector2(1,1);Background(panel);
            menu=panel.gameObject;
            string[] names={"Walk / Map (M / P)","Enter / Exit vehicle (F)","Interact nearby (G)","Garage & quests (J)","Talk to NPC (L)","Dimension portal (T)","Reset vehicle (R)","Controls"};
            UnityEngine.Events.UnityAction[] actions={()=>owner?.ToggleWalking(),()=>owner?.VehicleInteraction(),()=>owner?.UseNearby(),()=>life?.TogglePanel(),()=>life?.OpenDialogue(),()=>multiverse?.TogglePortal(),()=>driving?.ResetActiveVehicle(),()=>help.SetActive(true)};
            for(int i=0;i<names.Length;i++)
            {
                int index=i;var button=Rect(panel,names[i],new Vector2(.5f,1),new Vector2(0,-23-i*39),new Vector2(230,32));
                MakeButton(button,names[i],()=>{Close();actions[index]();});
            }
            var info=Rect(safe,"Controls",new Vector2(.5f,.5f),Vector2.zero,new Vector2(620,300));Background(info);help=info.gameObject;
            var text=Rect(info,"Help",new Vector2(.5f,.5f),new Vector2(0,20),new Vector2(570,210));
            Label(text,"CONTROLS\n\nMouse: look   Scroll: camera distance\nWASD: move/drive   Shift: sprint\nSpace: jump / vehicle brake   F: enter/exit\nG: interact   J: garage   L: talk   T: portal\nM / P: map / walk   Escape: release cursor\nClick the game to resume mouse look",20);
            var done=Rect(info,"Close",new Vector2(.5f,0),new Vector2(0,28),new Vector2(150,34));MakeButton(done,"CLOSE",Close);
            var notice=Rect(safe,"Notice",new Vector2(.5f,0),new Vector2(0,130),new Vector2(940,40));
            toast=Label(notice,"",17);toast.enableAutoSizing=true;toast.fontSizeMin=13;toast.fontSizeMax=17;
            Close();
        }
        private void Update()
        {
            if(owner==null)owner=GetComponentInChildren<OwnerSandboxController>(true);
            if(driving==null)driving=GetComponentInChildren<WorldDrivingController>(true);
            if(life==null)life=GetComponentInChildren<OwnerLifeController>(true);
            if(multiverse==null)multiverse=GetComponentInChildren<MultiverseController>(true);
            if(atmosphere==null)atmosphere=GetComponent<MotelAtmosphere>();
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){Close();life?.ClosePanel();multiverse?.ClosePortal();}
            Watch(ref lastOwner,owner==null?null:owner.Status);
            Watch(ref lastDriver,driving==null?null:driving.Status);
            Watch(ref lastLife,life==null?null:life.Status);
            Watch(ref lastUniverse,multiverse==null?null:multiverse.Status);
            if(toast!=null)toast.gameObject.SetActive(Time.unscaledTime<toastUntil);
            bool modal=(life!=null&&life.PanelOpen)||(multiverse!=null&&multiverse.IsPortalPanelOpen);
            if(modal&&MenuOpen)Close();
            if(cameraController!=null)cameraController.SetOverlayInputBlocked(MenuOpen||modal);
            if(clock!=null&&atmosphere!=null)
            {int minutes=Mathf.FloorToInt(atmosphere.Hour*60);clock.text=$"{atmosphere.Season} | Day {atmosphere.Day} | {minutes/60:00}:{minutes%60:00}"+(multiverse!=null&&multiverse.Current!=UniverseId.Normal?$" | {multiverse.Current}":"");}
        }
        private void Watch(ref string previous,string current)
        {
            if(current==null)return;if(previous==null){previous=current;return;}
            if(previous==current)return;previous=current;if(toast!=null){toast.text=current;toastUntil=Time.unscaledTime+5f;}
        }
        private void Close(){if(menu!=null)menu.SetActive(false);if(help!=null)help.SetActive(false);}
        private static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=position;r.sizeDelta=size;return r;}
        private static void Background(RectTransform rect){rect.gameObject.AddComponent<Image>().color=new Color(.035f,.09f,.105f,.97f);}
        private static TMP_Text Label(RectTransform rect,string text,float size)
        {var t=rect.gameObject.AddComponent<TextMeshProUGUI>();t.font=TMP_Settings.GetFontAsset();t.text=text;t.fontSize=size;t.color=new Color(.94f,.93f,.86f);t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;}
        private static void MakeButton(RectTransform rect,string text,UnityEngine.Events.UnityAction action)
        {
            rect.gameObject.AddComponent<Image>().color=new Color(.13f,.27f,.29f);
            rect.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(action);
            var label=Rect(rect,"Label",new Vector2(.5f,.5f),Vector2.zero,rect.sizeDelta);Label(label,text,17);
        }
        private static void Button(Transform parent,string text,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
        {var rect=Rect(parent,text,new Vector2(1,1),position,size);MakeButton(rect,text,action);}
        private void OnDisable(){if(cameraController!=null)cameraController.SetOverlayInputBlocked(false);}
    }
}
