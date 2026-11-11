using UnityEngine;
using UnityEngine.InputSystem;
namespace RoadsideHaven
{
    /// <summary>Portal travel, dimensional market, visual overlays and lightweight resource/defense encounters.</summary>
    public sealed class MultiverseController : MonoBehaviour
    {
        [Header("Portal")]
        [SerializeField] private MotelGameRoot motel;
        [SerializeField] private OwnerSandboxController owner;
        [SerializeField] private Vector3 portalPosition = new Vector3(-16,0,-6);
        [SerializeField, Min(1)] private float portalRadius = 5;
        [Header("Economy and risk")]
        [SerializeField, Min(0)] private int startingBalance = 120;
        [SerializeField, Min(1)] private int cargoCapacity = 12;
        [SerializeField, Min(5)] private float encounterInterval = 30;
        [SerializeField, Min(0)] private int neonSecurityCost = 8;
        [SerializeField, Min(1)] private int defenseCost = 10;
        [SerializeField] private GameObject[] universeOverlays;
        private MultiverseLedger ledger;
        private bool panel;
        private readonly float[] encounterTimers = new float[4];
        public string Status => message;
        public bool IsPortalPanelOpen => panel;
        public void ClosePortal()=>panel=false;
        public void TogglePortal(){if(!AtPortal)return; motel.GetComponentInChildren<BusinessEmpireController>(true)?.ClosePanel(); motel.GetComponentInChildren<OwnerLifeController>(true)?.ClosePanel();panel=!panel;}
        private int threat, defenses;
        private int neonHeat;
        private string message = "Walk to the purple portal (-16,-6), then press T.";
        public UniverseId Current => ledger == null ? UniverseId.Normal : ledger.Current;
        public float RoomMultiplier => Current == UniverseId.Neon ? (neonHeat > 0 ? 1.5f : 2f) : Current == UniverseId.Apocalypse ? (threat >= 3 ? .5f : 1.3f) : Current == UniverseId.Fantasy ? 1.8f : 1f;
        public Wallet WalletFor(int universe) => ledger.GetWallet((UniverseId)universe);
        public void Configure(MotelGameRoot root, OwnerSandboxController player, GameObject[] overlays)
        { motel=root; owner=player; universeOverlays=overlays; }
        private void Start()
        {
            ledger=new MultiverseLedger(motel.SessionWallet,startingBalance,cargoCapacity);
            motel.BindMultiverse(this);
            for(int i=0;i<4;i++)encounterTimers[i]=encounterInterval;
            RefreshOverlays();
        }
        private bool AtPortal => owner!=null && owner.IsWalking &&
            Vector2.Distance(new Vector2(owner.transform.position.x,owner.transform.position.z),new Vector2(portalPosition.x,portalPosition.z))<=portalRadius;
        private void Update()
        {
            if(ledger==null)return;
            if(Keyboard.current!=null && Keyboard.current.tKey.wasPressedThisFrame && AtPortal) TogglePortal();
            if(panel && !AtPortal)panel=false;
            encounterTimers[(int)Current]-=Time.deltaTime;
            if(encounterTimers[(int)Current]>0)return;
            encounterTimers[(int)Current]=Mathf.Max(5,encounterInterval);
            if(Current==UniverseId.Neon)
            {
                neonHeat=motel.TryDebit(neonSecurityCost)?0:1;
                message=neonHeat==0 ? "Cyber security upkeep paid." : "Security shortfall: cyber room premium reduced until next upkeep.";
            }
            if(Current==UniverseId.Apocalypse)
            {
                if(defenses>0){defenses--;message="Barricade repelled a zombie threat.";}
                else {threat=Mathf.Min(3,threat+1);message="Zombie threat increased. High threat reduces new room rates; build defenses.";}
            }
        }
        private void Travel(UniverseId destination)
        {
            if(!AtPortal || destination==Current)return;
            ledger.Travel(destination); motel.ApplyUniverseWallet(ledger.CurrentWallet);
            RefreshOverlays();
            message=$"Entered {destination}. Existing guests retain their original rate and payout currency.";
        }
        private void RefreshOverlays()
        {
            if(universeOverlays==null)return;
            for(int i=0;i<universeOverlays.Length;i++)if(universeOverlays[i]!=null)universeOverlays[i].SetActive(i==(int)Current);
        }
        public string CustomerType => Current==UniverseId.Neon ? "Robot" : Current==UniverseId.Apocalypse ? "Survivor" : Current==UniverseId.Fantasy ? "Elf" : "Guest";
        private void OnGUI()
        {
            if(ledger==null || !panel || !AtPortal)return;
            var old=GUI.matrix;float scale=Mathf.Max(.35f,Mathf.Min(Screen.width/1280f,Screen.height/700f));
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1))*Matrix4x4.Translate(new Vector3(0,-210,0));
            if(panel && AtPortal)
            {
                GUI.Box(new Rect(260,350,760,320),"PORTAL & INTER-UNIVERSE MARKET");
                if(GUI.Button(new Rect(920,355,85,28),"CLOSE"))panel=false;
                for(int i=0;i<4;i++)
                {
                    var id=(UniverseId)i;
                    if(GUI.Button(new Rect(275+i*180,380,170,38),id.ToString()))Travel(id);
                    var item=(DimensionItem)i;
                    GUI.Label(new Rect(278,430+i*42,280,32),$"{item} ({ledger.Count(item)})");
                    if(GUI.Button(new Rect(565,430+i*42,180,32),$"BUY {ledger.BuyPrice(item)}"))message=ledger.TryBuy(item)?"Item bought; cargo crosses portals.":"Not enough currency or cargo full.";
                    if(GUI.Button(new Rect(755,430+i*42,180,32),$"SELL {ledger.SellPrice(item)}"))message=ledger.TrySell(item)?"Sold for local currency.":"No item to sell or wallet full.";
                }
                if(Current==UniverseId.Apocalypse)
                {
                    if(GUI.Button(new Rect(278,610,250,38),$"BARRICADE ({defenseCost} Scrap)"))
                    {
                        if(defenses>=3)message="Three barricades already ready.";
                        else if(motel.TryDebit(defenseCost)){defenses++;threat=Mathf.Max(0,threat-1);message="Barricade built.";}
                        else message="Not enough Scrap.";
                    }
                    if(GUI.Button(new Rect(545,610,250,38),"USE SURVIVOR KIT"))
                    {if(ledger.TryConsume(DimensionItem.SurvivorKit)){threat=0;message="Survivors supplied; threat cleared.";}else message="Buy or import a SurvivorKit first.";}
                }
            }
            GUI.matrix=old;
        }
    }
}
