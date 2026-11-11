using UnityEngine;
using UnityEngine.InputSystem;
namespace RoadsideHaven
{
    /// <summary>Hidden-by-default business portfolio, tied to the world calendar and current dimensional wallet.</summary>
    public sealed class BusinessEmpireController : MonoBehaviour
    {
        [SerializeField] private MotelGameRoot motel;
        [SerializeField] private BusinessCatalog catalog;
        private MotelAtmosphere atmosphere;
        private MultiverseController multiverse;
        private BusinessLedger ledger;
        private bool panel;
        private int viewedUniverse;
        private string message="Buy properties, collect income and build your empire.";
        public bool PanelOpen=>panel;
        public string Status=>message;
        public int ActiveUniverse=>multiverse==null?0:(int)multiverse.Current;
        public void Configure(MotelGameRoot root,BusinessCatalog definitions){motel=root;catalog=definitions;}
        private void Start()
        {
            atmosphere=motel.GetComponent<MotelAtmosphere>();multiverse=motel.GetComponentInChildren<MultiverseController>(true);
            if(catalog==null){enabled=false;return;}
            ledger=new BusinessLedger(catalog.Build(),WalletFor,atmosphere==null?1:atmosphere.Day,catalog.ExpansionThreshold);
        }
        private Wallet WalletFor(int universe)=>multiverse==null?motel.SessionWallet:multiverse.WalletFor(universe);
        public void ClosePanel()=>panel=false;
        public void TogglePanel()
        {
            if(ledger==null)return;
            if(motel.GetComponentInChildren<ShopHUD>(true)?.IsOpen ?? false){message="Close checkout before opening the business portfolio.";return;}
            motel.GetComponentInChildren<OwnerLifeController>(true)?.ClosePanel();multiverse?.ClosePortal();
            panel=!panel;viewedUniverse=ActiveUniverse;
        }
        public string SiteStatus(int index)
        {
            if(ledger==null||index<0||index>=ledger.Count)return "PROPERTY";
            var h=ledger.Holding(ActiveUniverse,index);
            return h.Owned?$"OWNED - LEVEL {h.Level}":$"FOR SALE - {ledger.Spec(index).Price} {MultiverseLedger.Currency((UniverseId)ActiveUniverse)}";
        }
        private void Update()
        {
            if(ledger==null)return;
            if(Keyboard.current!=null&&Keyboard.current.bKey.wasPressedThisFrame)TogglePanel();
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)ClosePanel();
            if(panel&&((motel.GetComponentInChildren<OwnerLifeController>(true)?.PanelOpen ?? false)||(multiverse?.IsPortalPanelOpen ?? false)))panel=false;
            ledger.Tick(Time.deltaTime,atmosphere==null?1:atmosphere.Day);
        }
        private bool CanTransact()
        {if(viewedUniverse==ActiveUniverse)return true;message="Travel to this universe before buying, collecting or investing.";return false;}
        private void OnGUI()
        {
            if(!panel||ledger==null)return;
            var previous=GUI.matrix;float scale=Mathf.Max(.3f,Mathf.Min(Screen.width/1280f,Screen.height/720f));GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            var previousColor=GUI.color;GUI.color=new Color(.035f,.09f,.11f,.98f);GUI.DrawTexture(new Rect(90,105,1100,540),Texture2D.whiteTexture);GUI.color=previousColor;
            int count=ledger.OwnedCount(0);string stage=count<3?"STARTER":count<6?"MID GAME":count<catalog.ExpansionThreshold?"CITY EMPIRE":"MULTIVERSE EMPIRE";
            GUI.Label(new Rect(110,115,1000,30),$"BUSINESS EMPIRE | {stage} | Normal properties {count}/{ledger.Count}");
            if(GUI.Button(new Rect(1090,115,80,30),"CLOSE"))ClosePanel();
            for(int i=0;i<4;i++)if(GUI.Button(new Rect(110+i*180,150,170,30),((UniverseId)i).ToString()))viewedUniverse=i;
            GUI.Label(new Rect(835,150,330,30),$"{MultiverseLedger.Currency((UniverseId)viewedUniverse)} {WalletFor(viewedUniverse).Coins}");
            GUI.Label(new Rect(110,185,1050,30),message);
            GUI.Label(new Rect(110,215,1050,25),"PROPERTY                         LEVEL      INCOME                 SAFE CASH       ACTIONS");
            for(int i=0;i<ledger.Count;i++)
            {
                var spec=ledger.Spec(i);var holding=ledger.Holding(viewedUniverse,i);float y=245+i*36;
                GUI.Label(new Rect(110,y,230,30),spec.Name);
                GUI.Label(new Rect(350,y,65,30),holding.Owned?holding.Level.ToString():"-");
                GUI.Label(new Rect(430,y,190,30),$"{ledger.Income(viewedUniverse,i)} / {(spec.Daily?"day":spec.Interval+"s")}");
                GUI.Label(new Rect(630,y,100,30),holding.Pending.ToString());
                bool previousEnabled=GUI.enabled;
                GUI.enabled=previousEnabled&&viewedUniverse==ActiveUniverse;
                if(!holding.Owned)
                {
                    GUI.enabled=GUI.enabled&&ledger.CanBuy(viewedUniverse,i);
                    if(GUI.Button(new Rect(750,y,200,29),$"BUY {spec.Price}"))message=CanTransact()&&ledger.Buy(viewedUniverse,i)?"Property purchased.":"Not enough local currency or property locked.";
                    GUI.enabled=previousEnabled;
                    if(!ledger.CanBuy(viewedUniverse,i))GUI.Label(new Rect(965,y,210,30),viewedUniverse==0?$"Need {spec.UnlockOwned} properties":"Complete Normal empire");
                }
                else
                {
                    if(GUI.Button(new Rect(750,y,100,29),"COLLECT"))message=CanTransact()&&ledger.Collect(viewedUniverse,i)?"Income collected.":"No income ready or wallet full.";
                    GUI.enabled=GUI.enabled&&holding.Level<spec.MaxLevel;
                    if(GUI.Button(new Rect(860,y,170,29),holding.Level>=spec.MaxLevel?"MAX LEVEL":$"UPGRADE {ledger.UpgradePrice(viewedUniverse,i)}"))message=CanTransact()&&ledger.Upgrade(viewedUniverse,i)?"Business upgraded.":"Upgrade rejected: check local balance.";
                }
                GUI.enabled=previousEnabled;
            }
            int bank=-1;for(int i=0;i<ledger.Count;i++)if(ledger.Spec(i).Type==BusinessType.Bank){bank=i;break;}
            if(bank>=0&&ledger.Holding(viewedUniverse,bank).Owned)
            {
                GUI.Label(new Rect(110,545,450,30),$"Bank investment: {ledger.Holding(viewedUniverse,bank).Investment} | Fictional return 5% / game day");
                if(GUI.Button(new Rect(650,545,220,30),"INVEST 100"))message=CanTransact()&&ledger.Invest(viewedUniverse,bank,100)?"Investment deposited.":"Investment rejected.";
                if(GUI.Button(new Rect(880,545,220,30),"WITHDRAW PRINCIPAL"))message=CanTransact()&&ledger.Withdraw(viewedUniverse,bank)?"Principal returned.":"No principal or wallet full.";
            }
            GUI.Label(new Rect(110,585,1050,40),"Starter motel/shop earnings still come from gameplay. Portfolio upgrades add management income.\nIncome accrues in property safes across universes; collection pays that universe's currency. B / Escape closes.");
            GUI.matrix=previous;
        }
    }
}
