using UnityEngine;
using UnityEngine.InputSystem;
namespace RoadsideHaven
{
    /// <summary>Owner vehicle progression, nearby NPC dialogue, linked delivery quests, fast travel and optional timed conflicts.</summary>
    public sealed class OwnerLifeController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private MotelGameRoot motel;
        [SerializeField] private OwnerSandboxController owner;
        [SerializeField] private WorldDrivingController driving;
        [SerializeField] private MotelAtmosphere atmosphere;
        [Header("Optional conflicts")]
        [SerializeField] private bool conflictsEnabled;
        [SerializeField,Min(20)] private float encounterInterval=90;
        [SerializeField,Min(5)] private float defendSeconds=20;
        private LifeProgress progress;
        private Wallet normalWallet;
        private bool panel,dialogue;
        private string npcId,npcName;
        private Transform npc;
        private float encounterTimer,raidRemaining,tauntReady;
        private int guardPaidDay=1;
        private string message="J: owner panel | L: talk near an NPC | H: defend near shop";
        public string Status => message;
        public bool PanelOpen=>panel||dialogue;
        public void ClosePanel(){panel=false;dialogue=false;}
        public void TogglePanel(){ motel.GetComponentInChildren<BusinessEmpireController>(true)?.ClosePanel(); if(motel.GetComponentInChildren<MultiverseController>(true)?.IsPortalPanelOpen ?? false)return; dialogue=false;panel=!panel; }
        public void OpenDialogue()=>Talk();
        public bool IsNormal => (motel.GetComponentInChildren<MultiverseController>(true)?.Current ?? UniverseId.Normal) == UniverseId.Normal;
        public void Configure(MotelGameRoot root,OwnerSandboxController player,WorldDrivingController vehicles)
        {motel=root;owner=player;driving=vehicles;atmosphere=root.GetComponent<MotelAtmosphere>();}
        private void Start(){normalWallet=motel.SessionWallet;progress=new LifeProgress(normalWallet);encounterTimer=encounterInterval;driving.ConfigureLife(this);}
        public bool Owns(int index)=>progress!=null&&progress.Owns(index);
        public float VehicleMultiplier(int index)=>1+.15f*(progress==null?0:progress.UpgradeLevel(index));
        public void Delivered(){if(progress!=null&&progress.CompleteDelivery())message="NPC quest completed: +35 Coins, reputation +3.";}
        public void ReportTrouble(){progress?.RecordTrouble();}
        public float ArrivalFactor=>progress==null?1:Mathf.Lerp(1.2f,.85f,Mathf.InverseLerp(-20,20,progress.Reputation));
        private void Update()
        {
            if(progress==null)return;
            if(motel.GetComponentInChildren<MultiverseController>(true)?.IsPortalPanelOpen ?? false){panel=false;dialogue=false;}
            var keyboard=Keyboard.current;
            if(keyboard!=null)
            {
                if(keyboard.jKey.wasPressedThisFrame && !(motel.GetComponentInChildren<MultiverseController>(true)?.IsPortalPanelOpen ?? false))panel=!panel;
                if(keyboard.lKey.wasPressedThisFrame)Talk();
                if(keyboard.hKey.wasPressedThisFrame)Defend();
            }
            if(dialogue&&(npc==null||!owner.IsWalking||Vector3.Distance(owner.transform.position,npc.position)>6))dialogue=false;
            if(!conflictsEnabled){raidRemaining=0;return;}
            if(atmosphere!=null&&atmosphere.Day>guardPaidDay)
            {guardPaidDay=atmosphere.Day;if(!progress.PayGuardWages())message="Unpaid guards left. Hire replacements.";}
            if(!IsNormal)return;
            if(progress.Trouble>=3) message = progress.PayFine()?"Police issued and collected a 40 Coin fine.":"Police fine pending: need 40 Coins; fast travel suspended.";
            if(raidRemaining>0)
            {
                raidRemaining-=Time.deltaTime;
                if(raidRemaining<=0){int loss=Mathf.Min(25,normalWallet.Coins);normalWallet.TryDebit(loss);message=$"Shop raid: lost {loss} Coins. Hire guards or defend next time.";}
            }
            encounterTimer-=Time.deltaTime;
            if(encounterTimer>0)return;
            encounterTimer=encounterInterval;
            if(atmosphere!=null&&(atmosphere.Hour>=19||atmosphere.Hour<6))
            {
                if(progress.Guards>0)message="Your guard prevented a shop raid.";
                else {raidRemaining=defendSeconds;message="Raid warning! Go near the shop and press H before the timer expires.";}
            }
            else { if(progress.Guards==0)progress.Harassed(); message="Rival gossip spread. Guards deter harassment; quests improve your reputation."; }
        }
        private bool Near(Vector3 point,float range)=>owner.IsWalking&&Vector2.Distance(new Vector2(owner.transform.position.x,owner.transform.position.z),new Vector2(point.x,point.z))<=range;
        private void Defend()
        {if(raidRemaining>0&&Near(new Vector3(7,0,-8),8)){raidRemaining=0;progress.Defended();message="Raid repelled without combat. Reputation +1.";}else message="No active raid here, or move closer to your shop.";}
        private void Talk()
        {
            if(motel.GetComponentInChildren<MultiverseController>(true)?.IsPortalPanelOpen ?? false)return;
            if(!owner.IsWalking){message="Walk near an NPC before talking.";return;}
            npc=null;float nearest=4;
            foreach(var candidate in Object.FindObjectsByType<WorldNpc>(FindObjectsSortMode.None))
                Consider(candidate.transform,candidate.Id,candidate.DisplayName,ref nearest);
            foreach(var candidate in Object.FindObjectsByType<GuestSelectable>(FindObjectsSortMode.None))
                Consider(candidate.transform,"guest:"+candidate.GuestId,candidate.name,ref nearest);
            foreach(var candidate in Object.FindObjectsByType<ShopCustomerActor>(FindObjectsSortMode.None))
                Consider(candidate.transform,"shop:"+candidate.name,candidate.name,ref nearest);
            dialogue=npc!=null;if(!dialogue)message="No NPC within four meters.";
        }
        private void Consider(Transform candidate,string id,string label,ref float nearest)
        {float distance=Vector3.Distance(owner.transform.position,candidate.position);if(distance>nearest)return;nearest=distance;npc=candidate;npcId=id;npcName=label;}
        [SerializeField] private Vector3 garagePosition=new Vector3(-5,0,-6);
        [SerializeField] private Vector3 normalPortalPosition=new Vector3(-16,0,-6);
        [SerializeField] private Vector3[] travelPortalPositions={new Vector3(-350,0,0),new Vector3(180,0,690),new Vector3(-350,0,-650),new Vector3(500,0,-230)};
        public void OffsetBaseLocations(Vector3 delta){garagePosition+=delta;normalPortalPosition+=delta;}
        public void OffsetNearbyPortal(Vector3 from,Vector3 delta)
        {for(int i=0;i<travelPortalPositions.Length;i++)if(Vector2.Distance(new Vector2(from.x,from.z),new Vector2(travelPortalPositions[i].x,travelPortalPositions[i].z))<8)travelPortalPositions[i]+=delta;}
        private bool NearTravelPortal{get{for(int i=0;i<travelPortalPositions.Length;i++)if(Near(travelPortalPositions[i],5))return true;return false;}}
        private bool AtGarage=>Near(garagePosition,6)&&IsNormal&&!driving.IsDriving;
        private bool AtPortal => !driving.IsDriving && (Near(normalPortalPosition,5)||NearTravelPortal);
        private void Travel(Vector3 destination)
        {
            if(!AtPortal||!IsNormal||owner.HasSupplyCargo||(conflictsEnabled&&progress.Trouble>=3)){message="Use a Normal portal, unload cargo and clear any police fine before travel.";return;}
            if(!Physics.Raycast(destination+Vector3.up*8,Vector3.down,out var hit,20,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return;
            var point=hit.point+Vector3.up*.1f;
            if(Physics.CheckCapsule(point+Vector3.up*.4f,point+Vector3.up*1.5f,.32f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){message="Destination obstructed.";return;}
            owner.ExitVehicle(point);panel=false;message="Fast travel complete. Vehicles stay where parked.";
        }
        private void OnGUI()
        {
            if(progress==null || !PanelOpen)return;
            var old=GUI.matrix;float scale=Mathf.Max(.35f,Mathf.Min(Screen.width/1280f,Screen.height/800f));GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1))*Matrix4x4.Translate(new Vector3(0,-210,0));
            if(!panel&&!dialogue){GUI.matrix=old;return;}
            GUI.Box(new Rect(230,340,830,440),string.Empty);
            GUI.Label(new Rect(245,350,795,40),$"Coins {normalWallet.Coins} | Reputation {progress.Reputation} | Trouble {progress.Trouble}/3 | Guards {progress.Guards}\n{message}");
            if(GUI.Button(new Rect(960,390,85,30),"CLOSE")){panel=false;dialogue=false;}
            if(dialogue)
            {
                int relationship=progress.Relationship(npcId);
                GUI.Label(new Rect(245,400,700,40),$"{npcName}: {(relationship>=3?"Friend":relationship<=-3?"Enemy":"Acquaintance")} ({relationship})\nTown gossip: your reputation is {progress.Reputation}. Need a delivery job?");
                if(GUI.Button(new Rect(245,450,220,40),"FRIENDLY GREETING"))message=progress.Greet(npcId,Time.time)?"Relationship +1.":"Give them a minute before greeting again.";
                if(GUI.Button(new Rect(475,450,220,40),"ACCEPT SUPPLY QUEST"))message=progress.AcceptQuest()?"Use the truck: load at supplier, return to shop. Quest bonus +35.":"Finish your existing delivery quest.";
                if(GUI.Button(new Rect(705,450,220,40),"TAUNT" )&&Time.time>=tauntReady){tauntReady=Time.time+20;progress.Insult(npcId);message="Relationship and reputation fell; trouble increased.";}
            }
            else
            {
                GUI.Label(new Rect(245,400,700,30),"Buy/upgrade at your motel garage. CNG is starter-owned. Upgrade cap: 3.");
                for(int i=0;i<3;i++)
                {
                    string label=i==0?"Car":i==1?"CNG":"Supply truck";
                    GUI.Label(new Rect(245,435+i*42,220,35),$"{label} | Level {progress.UpgradeLevel(i)}");
                    if(GUI.Button(new Rect(470,435+i*42,220,35),progress.Owns(i)?"OWNED":$"BUY {progress.Price(i)}"))
                        message=AtGarage&&progress.Buy(i)?"Vehicle purchased.":"Visit the Normal motel garage; check coins/ownership.";
                    if(GUI.Button(new Rect(700,435+i*42,220,35),$"UPGRADE {progress.UpgradePrice(i)}"))message=AtGarage&&progress.Upgrade(i)?"Acceleration and top speed upgraded.":"Garage required; check ownership, coins and level cap.";
                }
                GUI.Label(new Rect(245,565,720,25),"Fast travel from purple portal only; no loaded supply cargo.");
                var destinations=new[]{normalPortalPosition,travelPortalPositions[0],travelPortalPositions[1],travelPortalPositions[2],travelPortalPositions[3]};
                var names=new[]{"MOTEL","CITY","HIGHWAY","BEACH","SUPPLIER"};
                for(int i=0;i<5;i++)if(GUI.Button(new Rect(245+i*152,595,145,35),names[i]))Travel(destinations[i]);
                conflictsEnabled=GUI.Toggle(new Rect(245,640,250,30),conflictsEnabled,"Enable optional conflicts");
                if(GUI.Button(new Rect(505,640,180,35),"HIRE GUARD 30"))message=AtGarage&&progress.HireGuard()?"Guard hired. Wage: 10 Coins per day.":"Garage required; cap two guards; check coins.";
                if(GUI.Button(new Rect(695,640,180,35),"PAY POLICE FINE 40"))message=progress.PayFine()?"Fine paid; trouble cleared.":"Fine applies at 3 trouble; need 40 Coins.";
                if(GUI.Button(new Rect(245,690,240,35),$"DEFEND (H) {raidRemaining:0}s"))Defend();
                GUI.Label(new Rect(500,690,520,45),"Guard wages/raids run only with conflicts enabled.\nQuest bonus is paid once per accepted delivery.");
            }
            GUI.matrix=old;
        }
    }
}
