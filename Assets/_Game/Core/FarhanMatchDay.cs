using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace RoadsideHaven
{
    /// <summary>
    /// Match days at Farhan Stadium. Fixtures follow the in-game calendar (every odd day, kick-off
    /// at 17:00). On a match day the gates open in the afternoon, fans walk in from the car park,
    /// queue at the ticket kiosks and enter; the player can buy a ticket at a kiosk (G) and the gate
    /// scans it. The match is played by 22 footballers and a referee (passing, dribbling, tackles,
    /// shots, goalkeeper saves, goals, half time), filmed by a broadcast camera shown live on the
    /// stadium screens, the street billboards and a picture-in-picture window (B) anywhere in the
    /// city, with a LIVE score bug, goal flashes, crowd noise and whistles. Billboards cycle adverts,
    /// the fixture list and live scores all the time.
    /// </summary>
    public sealed class FarhanMatchDay : MonoBehaviour
    {
        public enum Phase { NoMatch, GatesOpen, FirstHalf, HalfTime, SecondHalf, FullTime }
        public const string Home = "FARHAN FC";
        public static readonly string[] Opponents = { "HAVEN UNITED", "LAKESIDE ROVERS", "VALLEY ATHLETIC", "COASTAL CITY", "ASTER WANDERERS", "MERIDIAN FC", "PALM GROVE TOWN" };
        static readonly Color[] OpponentKits = { new Color(.85f, .12f, .12f), new Color(.95f, .8f, .1f), new Color(.1f, .6f, .25f), new Color(.95f, .95f, .95f), new Color(.55f, .15f, .6f), new Color(.95f, .45f, .1f), new Color(.1f, .1f, .12f) };
        public const float GatesOpenHour = 14f, KickoffHour = 17f, LastKickoffHour = 23f;
        public const float HalfSeconds = 150f, HalfTimeSeconds = 25f, FullTimeSeconds = 80f;
        public const int TicketPrice = 30;
        const float HalfLength = HavenExpansionLayout.PitchHalfZ, HalfWidth = HavenExpansionLayout.PitchHalfX, GoalHalfWidth = 3.66f, GoalHeight = 2.44f;

        public static bool IsMatchDay(int day) => day % 2 == 1;
        public static string Opponent(int day) => Opponents[Mathf.Abs(day / 2) % Opponents.Length];
        public static Color OpponentKit(int day) => OpponentKits[Mathf.Abs(day / 2) % OpponentKits.Length];
        public static List<(int day, string opponent)> Fixtures(int fromDay, int count)
        {
            var list = new List<(int, string)>(); for (int d = Mathf.Max(1, fromDay); list.Count < count; d++) if (IsMatchDay(d)) list.Add((d, Opponent(d))); return list;
        }

        // ----- state
        public Phase Current { get; private set; }
        public int HomeGoals { get; private set; }
        public int AwayGoals { get; private set; }
        public string Away { get; private set; } = Opponents[0];
        public int Minute => Current == Phase.FirstHalf ? Mathf.Min(45, 1 + (int)(timer / HalfSeconds * 45)) : Current == Phase.SecondHalf ? Mathf.Min(90, 46 + (int)(timer / HalfSeconds * 45)) : Current == Phase.HalfTime ? 45 : Current == Phase.FullTime ? 90 : 0;
        public bool Live => Current == Phase.FirstHalf || Current == Phase.SecondHalf || Current == Phase.HalfTime;
        public bool Ready { get; private set; }
        public bool HasTicket => ticketDay > 0 && ticketDay == Today;
        public bool Simulating { get; private set; }
        public bool PictureInPicture { get; private set; }
        public int FansActive { get { int n = 0; foreach (var f in fans) if (f.Agent != null && f.Agent.gameObject.activeSelf) n++; return n; } }
        public int FansAdmitted { get; private set; }
        public RenderTexture BroadcastFeed => feed;
        public Material BroadcastMaterial => broadcastMaterial;
        public Vector3 BallWorld => StadiumUpgrade.Centre + new Vector3(ball.x, ball.y, ball.z);
        public IReadOnlyList<Transform> Footballers { get { var l = new List<Transform>(); foreach (var p in squad) l.Add(p.T); return l; } }
        public IReadOnlyList<Billboard> Billboards => billboards;
        public string Banner => Time.unscaledTime < bannerUntil ? banner : null;
        public string Prompt { get; private set; }
        /// <summary>What the big screen headline currently says.</summary>
        public string BigScreenText => arena != null && arena.ScreenHeadlines.Count > 0 ? arena.ScreenHeadlines[0].text : "";
        public int Today => atmosphere != null ? atmosphere.Day : 1;
        float Hour => atmosphere != null ? atmosphere.Hour : 12;

        private StadiumUpgrade arena; private MotelAtmosphere atmosphere; private MotelGameRoot motel; private OwnerSandboxController owner;
        private float timer, bannerUntil, toastUntil, nextFan, roar, nextScan; private string banner, toast;
        private int playedDay = -1, matchDay = -1, ticketDay = -1, lastGateScanned = -1; private int attack = 1; // home attacks +z in the first half
        private Transform root;

        // ================================================================ lifecycle

        private void Update()
        {
            if (!Ready)
            {
                if (arena == null) arena = GetComponent<StadiumUpgrade>();
                if (arena == null || !arena.Done || arena.Stadium == null) return;
                Init();
            }
            float dt = Time.deltaTime;
            UpdatePhase(dt);
            arena.SetCrowd(Current != Phase.NoMatch);
            UpdateFans();
            UpdateMatch(dt);
            UpdateScreens();
            UpdateBillboards();
            UpdateBroadcast();
            UpdateAudio(dt);
            UpdateOwner();
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame) TogglePictureInPicture();
        }

        private void Init()
        {
            atmosphere = GetComponent<MotelAtmosphere>(); motel = GetComponent<MotelGameRoot>(); owner = GetComponentInChildren<OwnerSandboxController>(true);
            root = new GameObject("Farhan Stadium match day").transform; root.SetParent(arena.Root, false);
            BuildSquads(); BuildBall(); BuildBroadcast(); BuildBillboards(); BuildAudio();
            Ready = true;
        }

        // ================================================================ calendar

        private void UpdatePhase(float dt)
        {
            int day = Today; float hour = Hour;
            switch (Current)
            {
                case Phase.NoMatch:
                case Phase.GatesOpen:
                    if (IsMatchDay(day) && playedDay != day)
                    {
                        if (hour >= KickoffHour && hour < LastKickoffHour) Kickoff(day);
                        else Current = hour >= GatesOpenHour && hour < KickoffHour ? Phase.GatesOpen : Phase.NoMatch;
                    }
                    else Current = Phase.NoMatch;
                    break;
                case Phase.FirstHalf:
                    timer += dt; if (timer >= HalfSeconds) { Current = Phase.HalfTime; timer = 0; Whistle(2); Announce("HALF TIME   " + Score()); FormUp(true); }
                    break;
                case Phase.HalfTime:
                    timer += dt; if (timer >= HalfTimeSeconds) { Current = Phase.SecondHalf; timer = 0; attack = -attack; Restart(1); Whistle(1); }
                    break;
                case Phase.SecondHalf:
                    timer += dt; if (timer >= HalfSeconds) { Current = Phase.FullTime; timer = 0; playedDay = matchDay; Whistle(3); Announce("FULL TIME   " + Score()); roar = .8f; FormUp(true); }
                    break;
                case Phase.FullTime:
                    timer += dt; if (timer >= FullTimeSeconds) { Current = Phase.NoMatch; ShowSquads(false); }
                    break;
            }
        }

        /// <summary>Starts today's fixture straight away (late arrivals, testing).</summary>
        public void StartMatchNow() { if (!Ready) return; Kickoff(Today); }

        private void Kickoff(int day)
        {
            matchDay = day; Away = Opponent(day); HomeGoals = AwayGoals = 0; timer = 0; attack = 1; Current = Phase.FirstHalf;
            Dress(OpponentKit(day)); ShowSquads(true); Restart(0); Whistle(1); Announce("KICK OFF   " + Home + " vs " + Away); roar = .6f;
        }

        public string Score() => Home + "  " + HomeGoals + " - " + AwayGoals + "  " + Away;

        private void Announce(string text) { banner = text; bannerUntil = Time.unscaledTime + 4.5f; }
        private void Toast(string text) { toast = text; toastUntil = Time.unscaledTime + 4; }

        // ================================================================ footballers and ball

        private sealed class Footballer
        {
            public Transform T; public HumanVisual Visual; public int Team, Slot; public bool Keeper, Referee; public Vector2 Pos, Vel; public float Cooldown;
        }
        private readonly List<Footballer> squad = new List<Footballer>();
        private static readonly Vector2[] Formation = { new Vector2(0, -50), new Vector2(-22, -35), new Vector2(-8, -37), new Vector2(8, -37), new Vector2(22, -35), new Vector2(-24, -16), new Vector2(-8, -19), new Vector2(8, -19), new Vector2(24, -16), new Vector2(-7, -3), new Vector2(7, -3) };
        private Vector3 ball, ballVel; private Footballer holder, lastTouch; private Transform ballT; private float decide, celebrateUntil; private int restartTeam; private bool shotSaved = true;

        private void BuildSquads()
        {
            for (int team = 0; team < 2; team++)
                for (int i = 0; i < 11; i++) squad.Add(MakePlayer(team, i, false));
            squad.Add(MakePlayer(2, 0, true));
            ShowSquads(false);
        }

        private Footballer MakePlayer(int team, int slot, bool referee)
        {
            var id = HumanIdentity.Create(9100 + team * 31 + slot * 7, HumanRole.Civilian, HumanAge.YoungAdult);
            id.Top = HumanTop.TShirt; id.Bottom = HumanBottom.Shorts; id.Cap = id.Backpack = id.Handbag = id.Phone = id.Glasses = id.Umbrella = id.Earrings = false; id.Print = true; id.Accent = Color.white; id.Beard = false;
            id.Shirt = referee ? new Color(.08f, .08f, .09f) : team == 0 ? new Color(.1f, .35f, .95f) : Color.red; id.Trousers = referee ? new Color(.08f, .08f, .09f) : team == 0 ? Color.white : new Color(.1f, .1f, .12f);
            if (slot == 0 && !referee) id.Shirt = team == 0 ? new Color(.2f, .85f, .3f) : new Color(.95f, .5f, .8f);
            id.Shoes = new Color(.08f, .08f, .08f); id.Outfit = 0;
            var go = new GameObject(referee ? "Referee" : (team == 0 ? Home : "Away") + " player " + (slot + 1)); go.transform.SetParent(root, false);
            var visual = HumanVisual.Create(go.transform, id, true);
            return new Footballer { T = go.transform, Visual = visual, Team = team, Slot = slot, Keeper = slot == 0 && !referee, Referee = referee };
        }

        private void Dress(Color awayKit)
        {
            foreach (var p in squad)
            {
                if (p.Team != 1) continue; var id = p.Visual.Identity; id.Shirt = p.Keeper ? new Color(.95f, .5f, .8f) : awayKit; id.Trousers = awayKit.grayscale > .6f ? new Color(.1f, .1f, .12f) : Color.white;
                var parent = p.Visual.transform.parent; Destroy(p.Visual.gameObject); p.Visual = HumanVisual.Create(parent, id, true);
            }
        }

        private void BuildBall()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "Match ball"; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false); go.transform.localScale = Vector3.one * .22f;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.white }; m.SetFloat("_Smoothness", .6f); go.GetComponent<MeshRenderer>().sharedMaterial = m;
            ballT = go.transform; go.SetActive(false);
        }

        private void ShowSquads(bool show) { foreach (var p in squad) p.T.gameObject.SetActive(show && Simulating); if (ballT != null) ballT.gameObject.SetActive(show && Simulating); }

        private Vector2 Slot(Footballer p, bool kickoffShape)
        {
            int dir = p.Team == 0 ? attack : -attack; var f = Formation[p.Slot];
            var basePos = new Vector2(f.x * dir, f.y * dir);
            if (kickoffShape) return basePos;
            var b = new Vector2(ball.x, ball.z);
            float push = holder != null && holder.Team == p.Team ? 7 : 0;
            if (p.Keeper) return new Vector2(Mathf.Clamp(b.x * .12f, -3, 3), -HalfLength * dir + 1.2f * dir);
            return new Vector2(Mathf.Clamp(basePos.x + b.x * .3f, -HalfWidth + 2, HalfWidth - 2), Mathf.Clamp(basePos.y + Mathf.Clamp(b.y * .55f, -26, 26) + push * dir, -HalfLength + 4, HalfLength - 4));
        }

        private void FormUp(bool teleport)
        {
            foreach (var p in squad) { if (p.Referee) { p.Pos = new Vector2(-10, 4); continue; } var s = Slot(p, true); if (teleport) p.Pos = s; p.Vel = Vector2.zero; }
            holder = null; ball = new Vector3(0, .11f, 0); ballVel = Vector3.zero; Place();
        }

        private void Restart(int team)
        {
            restartTeam = team; FormUp(true);
            Footballer taker = null; foreach (var p in squad) if (p.Team == team && p.Slot == 9) taker = p;
            if (taker != null) { taker.Pos = new Vector2(0, -.6f * (team == 0 ? attack : -attack)); holder = taker; }
            decide = Time.time + 1.5f; celebrateUntil = 0;
        }

        private void UpdateMatch(float dt)
        {
            bool near = Camera.main != null && (Camera.main.transform.position - StadiumUpgrade.Centre).sqrMagnitude < 560 * 560;
            bool playing = Current == Phase.FirstHalf || Current == Phase.SecondHalf;
            bool visible = near && (Live || Current == Phase.FullTime && timer < 25);
            if (visible != Simulating) { Simulating = visible; ShowSquads(Current != Phase.NoMatch && Current != Phase.GatesOpen); }
            if (!playing) { if (Simulating) Animate(dt, false); return; }
            if (!Simulating)
            {
                // Off screen: the score still moves on, roughly two to three goals a game.
                if (Random.value < dt / HalfSeconds * 1.3f) Goal(Random.value < .55f ? 0 : 1);
                return;
            }
            if (Time.time < celebrateUntil) { Animate(dt, false); if (Time.time + dt >= celebrateUntil) Restart(restartTeam); return; }
            Animate(dt, true);
            StepBall(dt);
        }

        private void Animate(float dt, bool play)
        {
            var b = new Vector2(ball.x, ball.z);
            Footballer[] chaser = new Footballer[2];
            if (play)
                for (int t = 0; t < 2; t++)
                {
                    float best = float.MaxValue;
                    foreach (var p in squad) if (p.Team == t && !p.Keeper) { float d = (p.Pos - b).sqrMagnitude; if (d < best) { best = d; chaser[t] = p; } }
                }
            foreach (var p in squad)
            {
                Vector2 target; float speed;
                if (p.Referee) { target = b + new Vector2(-9, 5); speed = 5.5f; }
                else if (!play) { target = Time.time < celebrateUntil && lastTouch != null && p.Team == lastTouch.Team ? p.Pos + (new Vector2(Mathf.Sign(p.Pos.x + .01f) * HalfWidth, p.Pos.y) - p.Pos).normalized * 3 : Slot(p, Current != Phase.FirstHalf && Current != Phase.SecondHalf); speed = 3; }
                else if (p == holder) { target = Dribble(p); speed = 5.6f; }
                else if (p == chaser[p.Team] && (holder == null || holder.Team != p.Team)) { target = b; speed = 6.8f; }
                else if (p.Keeper) { target = Slot(p, false); if (!shotSaved && holder == null && ballVel.sqrMagnitude > 100) target += new Vector2(-Mathf.Sign(ball.x) * 2, 0); speed = 5; }
                else { target = Slot(p, false); speed = 4.4f; }
                var to = target - p.Pos; float dist = to.magnitude;
                var desired = dist > .2f ? to / dist * Mathf.Min(speed, dist * 2.5f) : Vector2.zero;
                p.Vel = Vector2.MoveTowards(p.Vel, desired, dt * 14); p.Pos += p.Vel * dt; p.Cooldown -= dt;
                Vector3 world = StadiumUpgrade.Centre + new Vector3(p.Pos.x, .1f, p.Pos.y);
                p.T.position = world;
                Vector3 face = p.Vel.sqrMagnitude > .05f ? new Vector3(p.Vel.x, 0, p.Vel.y) : new Vector3(b.x - p.Pos.x, 0, b.y - p.Pos.y);
                if (face.sqrMagnitude > .01f) p.T.rotation = Quaternion.Slerp(p.T.rotation, Quaternion.LookRotation(face), dt * 8);
            }
        }

        private Vector2 Dribble(Footballer p)
        {
            int dir = p.Team == 0 ? attack : -attack; var goal = new Vector2(0, HalfLength * dir);
            float weave = Mathf.Sin(Time.time * 1.3f + p.Slot) * 4;
            return p.Pos + ((goal - p.Pos).normalized * 6 + new Vector2(weave, 0));
        }

        private void StepBall(float dt)
        {
            if (holder != null)
            {
                var face = holder.Vel.sqrMagnitude > .1f ? holder.Vel.normalized : new Vector2(0, holder.Team == 0 ? attack : -attack);
                ball = new Vector3(holder.Pos.x + face.x * .55f, .11f, holder.Pos.y + face.y * .55f); ballVel = new Vector3(holder.Vel.x, 0, holder.Vel.y);
                // Opponents nearby may win the ball.
                foreach (var p in squad) if (p.Team != holder.Team && !p.Referee && (p.Pos - holder.Pos).sqrMagnitude < 1.2f * 1.2f && Random.value < dt * 1.3f)
                    { lastTouch = holder; holder.Cooldown = .8f; holder = null; ballVel = new Vector3(Random.Range(-5f, 5f), .5f, Random.Range(-5f, 5f)); break; }
                if (holder != null && Time.time >= decide) Decide(holder);
            }
            else
            {
                ballVel.y -= 9.81f * dt; ball += ballVel * dt;
                if (ball.y < .11f) { ball.y = .11f; ballVel.y = ballVel.y < -1.5f ? -ballVel.y * .45f : 0; float f = Mathf.Exp(-1.1f * dt); ballVel.x *= f; ballVel.z *= f; }
                var b = new Vector2(ball.x, ball.z);
                foreach (var p in squad)
                {
                    if (p.Referee || p.Cooldown > 0) continue;
                    float reach = p.Keeper ? 1.5f : .75f; float speed = new Vector2(ballVel.x, ballVel.z).magnitude;
                    if (p.Keeper && !shotSaved && speed > 10) continue;
                    if ((p.Pos - b).sqrMagnitude < reach * reach && ball.y < (p.Keeper ? 2.5f : 1.2f) && (speed < 12 || p.Keeper)) { holder = p; lastTouch = p; shotSaved = true; decide = Time.time + Random.Range(.35f, .9f); if (p.Keeper) roar = Mathf.Max(roar, .55f); break; }
                }
                // Goal or out of play.
                if (Mathf.Abs(ball.z) > HalfLength + .11f)
                {
                    int end = ball.z > 0 ? 1 : -1;
                    if (Mathf.Abs(ball.x) < GoalHalfWidth && ball.y < GoalHeight) { Goal(attack == end ? 0 : 1); return; }
                    var keeper = squad.Find(p => p.Keeper && (p.Team == 0 ? -attack : attack) == end);
                    if (keeper != null) { keeper.Pos = new Vector2(0, (HalfLength - 5) * end); holder = keeper; decide = Time.time + 1.2f; }
                }
                else if (Mathf.Abs(ball.x) > HalfWidth + .11f)
                {
                    int team = lastTouch != null ? 1 - lastTouch.Team : 0; Footballer taker = null; float best = float.MaxValue;
                    foreach (var p in squad) if (p.Team == team && !p.Keeper) { float d = (p.Pos - b).sqrMagnitude; if (d < best) { best = d; taker = p; } }
                    if (taker != null) { taker.Pos = new Vector2(Mathf.Sign(ball.x) * (HalfWidth - .5f), Mathf.Clamp(ball.z, -HalfLength + 2, HalfLength - 2)); holder = taker; decide = Time.time + 1; }
                }
            }
            Place();
        }

        private void Decide(Footballer p)
        {
            int dir = p.Team == 0 ? attack : -attack; var goal = new Vector2(0, HalfLength * dir); float dist = (goal - p.Pos).magnitude;
            decide = Time.time + Random.Range(.4f, 1f);
            if (p.Keeper) { var target = squad.FindAll(q => q.Team == p.Team && q.Slot >= 5)[Random.Range(0, 6)]; Kick(p, target.Pos + target.Vel * .8f, Random.Range(18f, 24f), Random.Range(4f, 7f)); return; }
            if (dist < 27 && Random.value < .35f + (27 - dist) / 45f)
            {
                shotSaved = Random.value < Mathf.Lerp(.75f, .45f, (27 - dist) / 27f);
                Kick(p, new Vector2(Random.Range(-3.3f, 3.3f), (HalfLength + 1) * dir), Random.Range(20f, 28f), Random.Range(.8f, 4.5f)); roar = Mathf.Max(roar, .7f); return;
            }
            if (Random.value < .5f)
            {
                Footballer best = null; float score = float.MinValue;
                foreach (var q in squad)
                {
                    if (q.Team != p.Team || q == p || q.Keeper) continue; float d = (q.Pos - p.Pos).magnitude; if (d < 5 || d > 38) continue;
                    float pressure = 0; foreach (var o in squad) if (o.Team != p.Team && !o.Referee) pressure += Mathf.Max(0, 4 - (o.Pos - q.Pos).magnitude);
                    float s = (q.Pos.y - p.Pos.y) * dir * .6f - d * .15f - pressure * 2 + Random.value * 4;
                    if (s > score) { score = s; best = q; }
                }
                if (best != null) { float d = (best.Pos - p.Pos).magnitude; Kick(p, best.Pos + best.Vel * (d / 16f), Mathf.Clamp(d * 1.5f, 9, 22), d > 22 ? Random.Range(2f, 5f) : Random.Range(0, .6f)); }
            }
        }

        private void Kick(Footballer p, Vector2 target, float speed, float lift)
        {
            var dir = (target - p.Pos).normalized; ballVel = new Vector3(dir.x * speed, lift, dir.y * speed);
            p.Cooldown = .45f; lastTouch = p; holder = null;
        }

        private void Goal(int team)
        {
            if (team == 0) HomeGoals++; else AwayGoals++;
            Announce("GOAL!   " + (team == 0 ? Home : Away) + "   " + HomeGoals + " - " + AwayGoals);
            roar = 1.2f; restartTeam = 1 - team; celebrateUntil = Time.time + 5; holder = null; ballVel = Vector3.zero;
            if (Simulating) Whistle(1);
            arena.SetMediaMood(team == 0 ? new Color(.4f, 1.6f, 3.6f) : new Color(3, 1, .4f)); moodUntil = Time.time + 6;
        }
        private float moodUntil;

        /// <summary>Scores for a side (testing and debugging).</summary>
        public void ForceGoal(bool home) => Goal(home ? 0 : 1);

        private void Place() { if (ballT != null) { ballT.position = BallWorld; if (ballVel.sqrMagnitude > .01f) ballT.Rotate(new Vector3(ballVel.z, 0, -ballVel.x) * Time.deltaTime * 260, Space.World); } }

        // ================================================================ fans

        private sealed class Fan { public TownCitizenAgent Agent; public int Stage; public float Wait; public int Kiosk; public bool Leaving; }
        private readonly List<Fan> fans = new List<Fan>();
        private static Vector3 G(float x, float z) => new Vector3(x, .12f, z);

        private void UpdateFans()
        {
            bool arriving = Current == Phase.GatesOpen || Current == Phase.FirstHalf && timer < 40;
            bool leaving = Current == Phase.FullTime && timer < 55;
            if ((arriving || leaving) && Time.time >= nextFan && FansActive < 30)
            {
                nextFan = Time.time + (leaving ? .8f : 1.1f);
                var fan = fans.Find(f => f.Agent != null && !f.Agent.gameObject.activeSelf);
                if (fan == null && fans.Count < 34)
                {
                    var go = new GameObject("Football fan " + fans.Count); go.transform.SetParent(root, false); var agent = go.AddComponent<TownCitizenAgent>(); agent.Initialize(null, null, false);
                    fan = new Fan { Agent = agent }; fans.Add(fan);
                }
                if (fan != null)
                {
                    fan.Agent.gameObject.SetActive(true); fan.Leaving = leaving; fan.Stage = 0; fan.Kiosk = Random.Range(0, 2);
                    if (!leaving) { fan.Agent.Warp(G(100 + Random.Range(-1.4f, 1.4f), -152 - Random.Range(0, 40))); fan.Agent.Walk(new[] { G(100, -249), G(128, -250.5f), G(152, -248), StadiumUpgrade.Kiosks[fan.Kiosk] + new Vector3(Random.Range(1f, 2.6f), .12f, Random.Range(-.9f, .9f)) }); }
                    else { var gate = WestGate(Random.Range(0, 2)); fan.Agent.Warp(gate + new Vector3(-4, .12f, Random.Range(-2, 2))); fan.Agent.Walk(new[] { G(152, -250 + Random.Range(-6, 6)), G(128, -250.5f), G(100, -249), G(100 + Random.Range(-1f, 1f), -150 - Random.Range(0, 30)) }); }
                }
            }
            foreach (var f in fans)
            {
                if (f.Agent == null || !f.Agent.gameObject.activeSelf || !f.Agent.Arrived) continue;
                if (f.Leaving || f.Stage == 2) { f.Agent.gameObject.SetActive(false); if (!f.Leaving) FansAdmitted++; continue; }
                if (f.Stage == 0) { f.Stage = 1; f.Wait = Time.time + Random.Range(1.5f, 3.5f); continue; } // buying a ticket
                if (f.Stage == 1 && Time.time >= f.Wait) { f.Stage = 2; var gate = WestGate(f.Kiosk); f.Agent.Walk(new[] { gate + new Vector3(-4.5f, .12f, Random.Range(-2.2f, 2.2f)) }); }
            }
            if (Current == Phase.NoMatch) foreach (var f in fans) if (f.Agent != null && f.Agent.gameObject.activeSelf && !IsVisible(f.Agent.transform.position)) f.Agent.gameObject.SetActive(false);
        }

        private Vector3 WestGate(int i)
        {
            Vector3 best = StadiumUpgrade.Centre; int n = 0;
            foreach (var g in arena.GatePoints) if (g.x < StadiumUpgrade.Centre.x) { if (n == i) return g; best = g; n++; }
            return best;
        }

        private static bool IsVisible(Vector3 p) { var c = Camera.main; if (c == null) return false; var v = c.WorldToViewportPoint(p); return v.z > 0 && v.x > -.1f && v.x < 1.1f && v.y > -.1f && v.y < 1.1f && v.z < 120; }

        // ================================================================ tickets and gates

        /// <summary>Buys today's match ticket from the player's coins.</summary>
        public bool TryBuyTicket(out string message)
        {
            if (!IsMatchDay(Today)) { var next = Fixtures(Today, 1)[0]; message = "No match today. Next: Day " + next.day + " 17:00 vs " + next.opponent; return false; }
            if (playedDay == Today && Current == Phase.NoMatch) { message = "Today's match is over. Next: Day " + Fixtures(Today + 1, 1)[0].day; return false; }
            if (HasTicket) { message = "You already have a ticket. Head to Gate A or B."; return false; }
            if (motel == null || !motel.TryDebit(TicketPrice)) { message = "Not enough coins (" + TicketPrice + " needed)."; return false; }
            ticketDay = Today; message = "Ticket bought: " + Home + " vs " + Away + ". Gates A/B, enjoy the match!"; return true;
        }

        private void UpdateOwner()
        {
            Prompt = null; if (owner == null || !owner.IsWalking) return;
            var p = owner.transform.position;
            foreach (var k in StadiumUpgrade.Kiosks)
            {
                if ((new Vector2(k.x - p.x, k.z - p.z)).sqrMagnitude > 4.5f * 4.5f) continue;
                Prompt = HasTicket ? "Ticket ready - walk to Gate A or B" : IsMatchDay(Today) ? "G: Buy match ticket (" + TicketPrice + " coins)  -  " + Home + " vs " + Opponent(Today) : "Ticket office closed - no match today";
                if (!HasTicket && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) { TryBuyTicket(out var msg); Toast(msg); }
            }
            int gate = arena.NearestGate(p, 5.5f);
            if (gate < 0) { lastGateScanned = -1; return; }
            bool open = Current != Phase.NoMatch && HasTicket;
            if (open) { if (lastGateScanned != gate || Time.time >= nextScan) { arena.OpenGate(gate, 7); nextScan = Time.time + 5; if (lastGateScanned != gate) Toast("Ticket scanned - welcome to Farhan Stadium!"); lastGateScanned = gate; } }
            else Prompt = !IsMatchDay(Today) || Current == Phase.NoMatch ? "Stadium closed - next match Day " + Fixtures(playedDay == Today ? Today + 1 : Today, 1)[0].day + " at 17:00" : "Ticket required - buy one at the ticket kiosks";
        }

        /// <summary>Opens the gate nearest a point if the player holds a ticket (used by the gate scanners).</summary>
        public bool ScanTicketAt(Vector3 p)
        {
            int gate = arena.NearestGate(p, 6); if (gate < 0 || !HasTicket || Current == Phase.NoMatch) return false;
            arena.OpenGate(gate, 7); return true;
        }

        // ================================================================ broadcast

        private Camera broadcastCam; private RenderTexture feed; private Material broadcastMaterial; private int frame;

        private void BuildBroadcast()
        {
            feed = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32) { name = "Farhan TV live feed", antiAliasing = 2 }; feed.Create();
            var go = new GameObject("Farhan TV broadcast camera"); go.transform.SetParent(root, false);
            broadcastCam = go.AddComponent<Camera>(); if (Camera.main != null) broadcastCam.CopyFrom(Camera.main);
            broadcastCam.enabled = false; broadcastCam.rect = new Rect(0, 0, 1, 1); broadcastCam.orthographic = false; broadcastCam.usePhysicalProperties = false; broadcastCam.targetTexture = feed; broadcastCam.fieldOfView = 28; broadcastCam.nearClipPlane = .5f; broadcastCam.farClipPlane = 600;
            broadcastMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Live broadcast", color = Color.black };
            broadcastMaterial.SetTexture("_BaseMap", feed); broadcastMaterial.EnableKeyword("_EMISSION"); broadcastMaterial.SetTexture("_EmissionMap", feed); broadcastMaterial.SetColor("_EmissionColor", Color.white * 1.05f); broadcastMaterial.SetFloat("_Smoothness", .2f);
        }

        private void UpdateBroadcast()
        {
            bool on = (Live || Current == Phase.FullTime) && (PictureInPicture || Simulating);
            if (!on || broadcastCam == null) return;
            if (++frame % 2 != 0) return;
            var focus = Simulating ? BallWorld : StadiumUpgrade.Centre;
            var target = StadiumUpgrade.Centre + new Vector3(-(StadiumUpgrade.UpperFront + 2), StadiumUpgrade.UpperFirst + 9, Mathf.Clamp(focus.z - StadiumUpgrade.Centre.z, -40, 40) * .8f);
            broadcastCam.transform.position = Vector3.Lerp(broadcastCam.transform.position, target, .2f);
            var look = Quaternion.LookRotation(focus + Vector3.up - broadcastCam.transform.position);
            broadcastCam.transform.rotation = Quaternion.Slerp(broadcastCam.transform.rotation, look, .35f);
            broadcastCam.Render();
        }

        public void TogglePictureInPicture() { PictureInPicture = !PictureInPicture && (Live || Current == Phase.FullTime); }

        // ================================================================ screens and billboards

        private void UpdateScreens()
        {
            bool showFeed = (Live || Current == Phase.FullTime) && Simulating;
            for (int i = 0; i < arena.ScreenPanels.Count; i++)
            {
                var panel = arena.ScreenPanels[i]; var mat = showFeed ? broadcastMaterial : arena.ScreenIdleMaterial; if (panel.sharedMaterial != mat) panel.sharedMaterial = mat;
                var head = arena.ScreenHeadlines[i]; var detail = arena.ScreenDetails[i];
                string h, d;
                if (Live || Current == Phase.FullTime) { h = Home + "  " + HomeGoals + " - " + AwayGoals + "  " + Away + "   " + (Current == Phase.HalfTime ? "HT" : Current == Phase.FullTime ? "FT" : Minute + "'"); d = showFeed ? "" : Current == Phase.FullTime ? "FULL TIME - THANK YOU FOR COMING" : "LIVE ON FARHAN TV"; }
                else if (Current == Phase.GatesOpen) { h = "MATCH DAY"; d = Home + " vs " + Opponent(Today) + "  -  KICK OFF 17:00"; }
                else { var ad = Ads[(int)(Time.time / 6) % Ads.Length]; h = ad.title; d = ad.body; }
                if (head.text != h) head.text = h; if (detail.text != d) detail.text = d;
                head.rectTransform.localPosition = head.rectTransform.localPosition; // keep in place
            }
            if (Time.time > moodUntil) arena.SetMediaMood(Current != Phase.NoMatch ? new Color(1.6f, 1.6f, 2.2f) : Color.white * 1.6f);
        }

        private static readonly (string title, string body, Color colour)[] Ads =
        {
            ("HAVEN MOTEL", "Rooms from 40 coins  -  Pool  -  Free WiFi", new Color(.1f, .35f, .95f)),
            ("PIT STOP SHOP", "Snacks, drinks and fuel  -  open 24/7", new Color(.95f, .5f, .08f)),
            ("CITY FAST FOOD", "Burgers and fries  -  Downtown", new Color(.85f, .12f, .12f)),
            ("HAVEN AIRWAYS", "Fly from Haven International", new Color(.05f, .6f, .65f)),
            ("ASTER MARKET", "Fresh food every day  -  Aster Quarter", new Color(.15f, .6f, .25f)),
        };

        public sealed class Billboard
        {
            public Transform Root; public MeshRenderer[] Panels; public TextMeshPro[] Titles, Bodies; public Material Idle; public string Title => Titles[0].text; public string Body => Bodies[0].text;
        }
        private readonly List<Billboard> billboards = new List<Billboard>();

        private void BuildBillboards()
        {
            foreach (var (pos, yaw) in new[] { (new Vector3(128, 0, -205), 90f), (new Vector3(66, 0, -90), 0f), (new Vector3(157, 0, -250), 90f) })
            {
                var go = new GameObject("Digital billboard"); go.transform.SetParent(root, false);
                float ground = 0; if (Physics.Raycast(pos + Vector3.up * 30, Vector3.down, out var hit, 60, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
                go.transform.SetPositionAndRotation(new Vector3(pos.x, ground, pos.z), Quaternion.Euler(0, yaw, 0));
                var steel = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.25f, .27f, .3f) }; steel.SetFloat("_Metallic", .7f); steel.SetFloat("_Smoothness", .5f);
                foreach (float x in new[] { -4.5f, 4.5f })
                {
                    var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder); leg.name = "Billboard leg"; leg.transform.SetParent(go.transform, false);
                    leg.transform.localPosition = new Vector3(x, 3.5f, 0); leg.transform.localScale = new Vector3(.5f, 3.5f, .5f); leg.GetComponent<MeshRenderer>().sharedMaterial = steel;
                }
                var frame = GameObject.CreatePrimitive(PrimitiveType.Cube); frame.name = "Billboard frame"; frame.transform.SetParent(go.transform, false);
                frame.transform.localPosition = new Vector3(0, 11, 0); frame.transform.localScale = new Vector3(16.6f, 8.6f, .7f); frame.GetComponent<MeshRenderer>().sharedMaterial = steel;
                var idle = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Billboard LED", color = Color.black }; idle.EnableKeyword("_EMISSION"); idle.SetColor("_EmissionColor", Ads[0].colour * 1.4f);
                var bb = new Billboard { Root = go.transform, Panels = new MeshRenderer[2], Titles = new TextMeshPro[2], Bodies = new TextMeshPro[2], Idle = idle };
                for (int side = 0; side < 2; side++)
                {
                    float s = side == 0 ? -1 : 1;
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "Billboard screen"; Destroy(quad.GetComponent<Collider>()); quad.transform.SetParent(go.transform, false);
                    quad.transform.localPosition = new Vector3(0, 11, s * .37f); quad.transform.localRotation = Quaternion.Euler(0, side == 0 ? 0 : 180, 0); quad.transform.localScale = new Vector3(16, 8, 1);
                    bb.Panels[side] = quad.GetComponent<MeshRenderer>(); bb.Panels[side].sharedMaterial = idle; bb.Panels[side].shadowCastingMode = ShadowCastingMode.Off;
                    bb.Titles[side] = Label(go.transform, new Vector3(0, 12.9f, s * .4f), side == 0 ? 0 : 180, 26, 15);
                    bb.Bodies[side] = Label(go.transform, new Vector3(0, 9.6f, s * .4f), side == 0 ? 0 : 180, 12, 15);
                }
                billboards.Add(bb);
            }
        }

        private static TextMeshPro Label(Transform parent, Vector3 local, float yaw, float size, float width)
        {
            var t = new GameObject("Billboard text").AddComponent<TextMeshPro>(); t.transform.SetParent(parent, false); t.transform.localPosition = local; t.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
            t.alignment = TextAlignmentOptions.Center; t.enableAutoSizing = true; t.fontSizeMin = .5f; t.fontSizeMax = size; t.color = Color.white; t.fontStyle = FontStyles.Bold;
            t.rectTransform.sizeDelta = new Vector2(width, size > 20 ? 2.8f : 4.4f); t.textWrappingMode = TextWrappingModes.Normal; return t;
        }

        private void UpdateBillboards()
        {
            int slide = (int)(Time.time / 7) % 6;
            string title, body; Color colour; bool video = false;
            switch (slide)
            {
                case 1:
                {
                    title = "MATCH SCHEDULE"; colour = new Color(.05f, .1f, .3f); var sb = new System.Text.StringBuilder();
                    foreach (var f in Fixtures(playedDay == Today ? Today + 1 : Today, 3)) sb.Append("DAY ").Append(f.day).Append("   17:00   ").Append(Home).Append(" vs ").Append(f.opponent).Append('\n');
                    body = sb.ToString().TrimEnd(); break;
                }
                case 3:
                    if (Live || Current == Phase.FullTime) { title = Current == Phase.FullTime ? "FULL TIME" : "LIVE  -  FARHAN TV"; body = Home + "  " + HomeGoals + " - " + AwayGoals + "  " + Away + "   " + (Current == Phase.HalfTime ? "HT" : Current == Phase.FullTime ? "FT" : Minute + "'"); colour = new Color(.75f, .05f, .08f); video = Simulating; }
                    else if (IsMatchDay(Today) && playedDay != Today) { title = "MATCH TODAY  -  17:00"; body = Home + " vs " + Opponent(Today) + "\nTickets " + TicketPrice + " coins at Farhan Stadium"; colour = new Color(.75f, .05f, .08f); }
                    else { var next = Fixtures(Today + 1, 1)[0]; title = "FARHAN STADIUM"; body = "Next match: Day " + next.day + "  17:00\n" + Home + " vs " + next.opponent; colour = new Color(.05f, .25f, .7f); }
                    break;
                default:
                {
                    var ad = Ads[(slide / 2 + (int)(Time.time / 42)) % Ads.Length]; title = ad.title; body = ad.body; colour = ad.colour; break;
                }
            }
            foreach (var bb in billboards)
            {
                bb.Idle.SetColor("_EmissionColor", colour * 1.3f);
                for (int s = 0; s < 2; s++)
                {
                    var mat = video ? broadcastMaterial : bb.Idle; if (bb.Panels[s].sharedMaterial != mat) bb.Panels[s].sharedMaterial = mat;
                    if (bb.Titles[s].text != title) bb.Titles[s].text = title; if (bb.Bodies[s].text != body) bb.Bodies[s].text = body;
                }
            }
        }

        // ================================================================ sound

        private AudioSource crowd; private AudioClip whistle;

        private void BuildAudio()
        {
            const int rate = 22050; int n = rate * 4; var data = new float[n]; var r = new System.Random(3); float low = 0, band = 0;
            for (int i = 0; i < n; i++)
            {
                float white = (float)r.NextDouble() * 2 - 1; low += (white - low) * .06f; band += (low - band) * .3f;
                float swell = .7f + .3f * Mathf.Sin(i / (float)rate * 1.7f) * Mathf.Sin(i / (float)rate * .6f + 1);
                data[i] = (low - band * .5f) * 2.2f * swell;
            }
            int fade = rate / 2; for (int i = 0; i < fade; i++) { float t = i / (float)fade; data[n - fade + i] = data[n - fade + i] * (1 - t) + data[i] * t; }
            var clip = AudioClip.Create("Stadium crowd", n - fade, 1, rate, false); var trimmed = new float[n - fade]; System.Array.Copy(data, fade, trimmed, 0, n - fade); for (int i = 0; i < fade; i++) trimmed[n - 2 * fade + i] = data[n - fade + i]; clip.SetData(trimmed, 0);
            var go = new GameObject("Stadium crowd sound"); go.transform.SetParent(root, false); go.transform.position = StadiumUpgrade.Centre + Vector3.up * 15;
            crowd = go.AddComponent<AudioSource>(); crowd.clip = clip; crowd.loop = true; crowd.spatialBlend = 1; crowd.rolloffMode = AudioRolloffMode.Linear; crowd.minDistance = 40; crowd.maxDistance = 520; crowd.volume = 0; crowd.dopplerLevel = 0; crowd.Play();
            int wn = rate / 2; var w = new float[wn]; for (int i = 0; i < wn; i++) { float t = i / (float)rate; float env = Mathf.Clamp01(t * 30) * Mathf.Clamp01((wn - i) / (rate * .05f)); w[i] = Mathf.Sin(2 * Mathf.PI * 2850 * t + 3 * Mathf.Sin(2 * Mathf.PI * 32 * t)) * env * .5f; }
            whistle = AudioClip.Create("Referee whistle", wn, 1, rate, false); whistle.SetData(w, 0);
        }

        private void Whistle(int times) { if (crowd != null && whistle != null) StartCoroutine(Blow(times)); }
        private IEnumerator Blow(int times) { for (int i = 0; i < times; i++) { crowd.PlayOneShot(whistle, .9f); yield return new WaitForSeconds(.65f); } }

        private void UpdateAudio(float dt)
        {
            if (crowd == null) return;
            float baseVolume = Current == Phase.GatesOpen ? .22f : Live ? (Current == Phase.HalfTime ? .32f : .5f) : Current == Phase.FullTime ? .4f : 0;
            roar = Mathf.MoveTowards(roar, 0, dt * .18f);
            crowd.volume = Mathf.MoveTowards(crowd.volume, Mathf.Clamp01(baseVolume + roar * .5f), dt * .6f);
            crowd.pitch = 1 + roar * .08f;
        }

        // ================================================================ HUD

        private GUIStyle bug, big, small;
        private void OnGUI()
        {
            if (!Ready) return;
            if (bug == null)
            {
                bug = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; bug.normal.textColor = Color.white;
                big = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; big.normal.textColor = new Color(1, .9f, .3f);
                small = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true }; small.normal.textColor = Color.white;
            }
            bool near = Camera.main != null && (Camera.main.transform.position - StadiumUpgrade.Centre).sqrMagnitude < 900 * 900;
            if ((Live || Current == Phase.FullTime) && (near || PictureInPicture))
                GUI.Box(new Rect(Screen.width * .5f - 260, 10, 520, 34), (Current == Phase.FullTime ? "FT  " : "<color=#ff3333>●</color> LIVE  ") + Score() + "   " + (Current == Phase.HalfTime ? "HT" : Current == Phase.FullTime ? "" : Minute + "'"), Rich(bug));
            if (Live && !PictureInPicture) GUI.Label(new Rect(Screen.width * .5f - 260, 46, 520, 22), "B: watch live on Farhan TV", Centered());
            if (PictureInPicture && feed != null)
            {
                var r = new Rect(Screen.width - 500, Screen.height - 300, 480, 270);
                GUI.Box(new Rect(r.x - 4, r.y - 26, r.width + 8, r.height + 30), "FARHAN TV  -  LIVE");
                GUI.DrawTexture(r, feed, ScaleMode.ScaleToFit, false);
            }
            var b = Banner; if (b != null) { GUI.color = new Color(0, 0, 0, .55f); GUI.DrawTexture(new Rect(0, Screen.height * .2f, Screen.width, 70), Texture2D.whiteTexture); GUI.color = Color.white; GUI.Label(new Rect(0, Screen.height * .2f, Screen.width, 70), b, big); }
            if (Prompt != null) GUI.Box(new Rect(Screen.width * .5f - 300, Screen.height - 120, 600, 34), Prompt, small);
            if (Time.unscaledTime < toastUntil) GUI.Box(new Rect(Screen.width * .5f - 300, Screen.height - 160, 600, 34), toast, small);
        }
        private static GUIStyle Rich(GUIStyle s) { s.richText = true; return s; }
        private GUIStyle centered; private GUIStyle Centered() { if (centered == null) { centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 }; centered.normal.textColor = Color.white; } return centered; }

        private void OnDestroy()
        {
            if (feed != null) { feed.Release(); Destroy(feed); }
            if (broadcastMaterial != null) Destroy(broadcastMaterial);
            if (root != null) Destroy(root.gameObject);
        }
    }
}
