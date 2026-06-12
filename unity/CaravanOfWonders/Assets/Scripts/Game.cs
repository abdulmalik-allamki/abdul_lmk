using System.Collections;
using UnityEngine;

namespace Caravan
{
    // =====================================================================
    //  Camera director — the "smooth 2D → 3D" heart of the game.
    //  Travel plays like a flat side-scroller (distant camera, narrow FOV),
    //  and when a conversation starts the camera swings around into a
    //  cinematic 3D over-the-shoulder view. Exponential damping keeps every
    //  move continuous, whatever state it starts from.
    // =====================================================================

    public class CameraDirector : MonoBehaviour
    {
        public Camera cam;
        Vector3 targetPos;
        Quaternion targetRot;
        float targetFov = 28f;
        float posSpeed = 3f, rotSpeed = 3f;
        bool vignette;
        Vector3 vignetteAnchor, vignetteLook;
        float driftSeed;

        public static CameraDirector Create()
        {
            var go = new GameObject("GameCamera");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 250f;
            go.AddComponent<AudioListener>();
            var d = go.AddComponent<CameraDirector>();
            d.cam = cam;
            go.transform.position = new Vector3(0, 3.2f, -16f);
            return d;
        }

        /// <summary>Flat, side-on travel view (the "2D" feel).</summary>
        public void FollowSide(Vector3 playerPos)
        {
            if (vignette) return;
            targetPos = new Vector3(playerPos.x + 2.2f, 3.1f, -15.5f);
            targetRot = Quaternion.Euler(6f, 0f, 0f);
            targetFov = 28f;
            posSpeed = 4.5f; rotSpeed = 4.5f;
        }

        /// <summary>Swing into a 3D conversation shot between two characters.</summary>
        public void EnterVignette(Vector3 npcPos, Vector3 playerPos)
        {
            vignette = true;
            driftSeed = Random.value * 10f;
            Vector3 mid = (npcPos + playerPos) * 0.5f + Vector3.up * 1.25f;
            // orbit ~40° off the side axis, closer in
            Vector3 offset = Quaternion.Euler(0f, -42f, 0f) * (Vector3.back * 5.2f) + Vector3.up * 0.9f;
            vignetteAnchor = mid + offset;
            vignetteLook = npcPos + Vector3.up * 1.35f;
            targetFov = 44f;
            posSpeed = 2.2f; rotSpeed = 2.2f;
        }

        public void ExitVignette()
        {
            vignette = false;
        }

        void LateUpdate()
        {
            if (vignette)
            {
                // slow cinematic drift while talking
                float t = Time.time + driftSeed;
                targetPos = vignetteAnchor
                          + transform.right * Mathf.Sin(t * 0.35f) * 0.22f
                          + Vector3.up * Mathf.Sin(t * 0.5f) * 0.1f;
                targetRot = Quaternion.LookRotation(vignetteLook - targetPos, Vector3.up);
            }
            float kp = 1f - Mathf.Exp(-Time.deltaTime * posSpeed);
            float kr = 1f - Mathf.Exp(-Time.deltaTime * rotSpeed);
            transform.position = Vector3.Lerp(transform.position, targetPos, kp);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, kr);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, kp);
            cam.backgroundColor = RenderSettings.fogColor;
        }
    }

    // =====================================================================
    //  Game manager — state machine, player control, scoring, dialogue flow
    // =====================================================================

    public class GameManager : MonoBehaviour
    {
        public enum State { Menu, Arrive, Play, Dialogue, GameOver, Victory }

        public Content content;
        public State state = State.Menu;

        // run state
        public CharacterDef hero;
        public int hearts, hints, gold, streak, bestStreak, correct, countryIdx;
        public bool falconUsed;
        int startGold, startCorrect, startHints;

        // scene objects
        public LevelData level;
        public Rig playerRig;
        GameObject playerRoot, falcon;
        float px, py, vy;
        int facing = 1;
        bool onGround = true;

        public CameraDirector camDir;
        public VoicePlayer voice;
        public GameUI ui;
        public VignettePlayer vignettes;
        NpcInstance activeNpc;
        bool awaitingAnswer, hintUsed;

        const float SPEED = 5.2f, JUMP = 7.2f, GRAVITY = 19f;
        const int GOLD_CORRECT = 10, GOLD_STREAK = 5, GOLD_COIN = 2;

        void Awake()
        {
            Application.targetFrameRate = 60;
            content = Content.Load();
            camDir = CameraDirector.Create();
            voice = gameObject.AddComponent<VoicePlayer>();
            vignettes = gameObject.AddComponent<VignettePlayer>();
            ui = GameUI.Build(this);
            ui.ShowMenu();
        }

        // ---------------- menu ----------------

        public void OnToggleLanguage()
        {
            Loc.Ar = !Loc.Ar;
            ui.RefreshAll();
            RefreshLevelTexts();
        }

        public void OnBegin(CharacterDef ch)
        {
            hero = ch;
            hearts = ch.hearts; hints = ch.hints;
            gold = 0; streak = 0; bestStreak = 0; correct = 0; falconUsed = false;
            ui.HideMenu();
            LoadCountry(0);
        }

        // ---------------- country / level ----------------

        public void LoadCountry(int ci)
        {
            countryIdx = ci;
            startGold = gold; startCorrect = correct; startHints = hints;
            if (level != null) Destroy(level.root);
            if (playerRoot != null) Destroy(playerRoot);

            level = WorldBuilder.Build(content, ci, camDir.transform);

            playerRoot = new GameObject("Player");
            playerRoot.transform.position = new Vector3(4f, 0, 0);
            playerRoot.transform.rotation = Quaternion.Euler(0, 90, 0);
            playerRig = RigBuilder.Build(playerRoot.transform, RigBuilder.Heroes[hero.id]);
            px = 4f; py = 0f; vy = 0f; facing = 1; onGround = true;

            if (hero.falconSave && !falconUsed) BuildFalcon();

            camDir.transform.position = new Vector3(px + 2f, 3.2f, -15.5f);
            camDir.ExitVignette();
            camDir.FollowSide(playerRoot.transform.position);

            state = State.Arrive;
            ui.ShowArrive(content.countries[ci]);
            ui.UpdateHud();
            voice.Play($"c{ci:00}_scene");
        }

        void BuildFalcon()
        {
            falcon = new GameObject("Falcon");
            var body = B.Sph(falcon.transform, Vector3.zero, new Vector3(0.3f, 0.18f, 0.5f), Hexc.C("#6a4a2a"));
            B.Box(falcon.transform, new Vector3(-0.3f, 0.05f, 0), new Vector3(0.5f, 0.03f, 0.25f), Hexc.C("#5a3e22"));
            B.Box(falcon.transform, new Vector3(0.3f, 0.05f, 0), new Vector3(0.5f, 0.03f, 0.25f), Hexc.C("#5a3e22"));
            B.Sph(falcon.transform, new Vector3(0, 0.06f, 0.28f), Vector3.one * 0.14f, Hexc.C("#e8c860"));
        }

        public void OnEnterCountry()
        {
            ui.HideArrive();
            voice.Stop();
            state = State.Play;
        }

        void RefreshLevelTexts()
        {
            if (level == null) return;
            foreach (var n in level.npcs)
            {
                var label = n.root.GetComponentInChildren<TextMesh>();
                if (label != null) label.text = Loc.Fix(Loc.T(n.enc.persona));
            }
        }

        // ---------------- per-frame ----------------

        void Update()
        {
            if (state == State.Play) UpdatePlay();
            if (falcon != null && playerRoot != null)
            {
                float t = Time.time;
                Vector3 target = playerRoot.transform.position
                    + new Vector3(Mathf.Sin(t * 0.9f) * 1.6f, 2.6f + Mathf.Sin(t * 1.7f) * 0.3f, -0.6f);
                falcon.transform.position = Vector3.Lerp(falcon.transform.position, target, Time.deltaTime * 2.5f);
                falcon.transform.rotation = Quaternion.Euler(0, t * 60f % 360f, Mathf.Sin(t * 8f) * 12f);
            }
        }

        void UpdatePlay()
        {
            float h = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(h) > 0.1f)
            {
                px += h * SPEED * Time.deltaTime;
                facing = h > 0 ? 1 : -1;
                playerRig.walking = true;
                playerRoot.transform.rotation = Quaternion.Slerp(
                    playerRoot.transform.rotation,
                    Quaternion.Euler(0, facing * 90, 0),
                    Time.deltaTime * 10f);
            }
            else playerRig.walking = false;

            // jump + gravity
            if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) && onGround)
            {
                vy = JUMP; onGround = false;
            }
            vy -= GRAVITY * Time.deltaTime;
            py += vy * Time.deltaTime;
            if (py <= 0f) { py = 0f; vy = 0f; onGround = true; }

            // clamp + locked gate
            px = Mathf.Clamp(px, 1.5f, WorldBuilder.LEVEL_W - 1.5f);
            if (!GateOpen() && px > level.gateX - 2f) px = level.gateX - 2f;

            playerRoot.transform.position = new Vector3(px, py, 0);
            camDir.FollowSide(playerRoot.transform.position);

            // coins
            for (int i = level.coins.Count - 1; i >= 0; i--)
            {
                var coin = level.coins[i];
                if (coin == null) { level.coins.RemoveAt(i); continue; }
                Vector3 d = coin.transform.position - playerRoot.transform.position;
                d.y -= 0.9f;
                if (d.magnitude < 1.0f)
                {
                    Destroy(coin);
                    level.coins.RemoveAt(i);
                    gold += GOLD_COIN * hero.goldMult;
                    Sfx.Coin();
                    ui.UpdateHud();
                }
            }

            // npc proximity + talk
            var near = NearestNpc();
            ui.ShowPrompt(near != null);
            if (near != null && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return)))
                StartDialogue(near);

            // gate crossing
            if (GateOpen() && px > level.gateX + 2.5f)
                StartCoroutine(CrossGate());
        }

        NpcInstance NearestNpc()
        {
            NpcInstance best = null;
            float bd = 2.6f;
            foreach (var n in level.npcs)
            {
                if (n.done) continue;
                float d = Mathf.Abs(n.x - px);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        bool GateOpen()
        {
            foreach (var n in level.npcs) if (!n.done) return false;
            return true;
        }

        IEnumerator CrossGate()
        {
            state = State.Arrive; // freeze input
            yield return ui.Fade(1f, 0.7f);
            int next = countryIdx + 1;
            if (next < content.countries.Length)
            {
                LoadCountry(next);
                yield return ui.Fade(0f, 0.7f);
            }
            else
            {
                ShowVictory();
                yield return ui.Fade(0f, 0.7f);
            }
        }

        // ---------------- dialogue (animated vignettes) ----------------

        public void StartDialogue(NpcInstance npc)
        {
            state = State.Dialogue;
            activeNpc = npc;
            awaitingAnswer = false;
            hintUsed = false;
            playerRig.walking = false;

            // face each other
            playerRoot.transform.rotation = Quaternion.Euler(0, npc.x > px ? 90 : -90, 0);
            npc.root.transform.rotation = Quaternion.Euler(0, npc.x > px ? -90 : 90, 0);
            npc.rig.talking = true;

            camDir.EnterVignette(npc.root.transform.position, playerRoot.transform.position);
            vignettes.Play(npc, level, playerRoot.transform);

            ui.ShowSay(npc);
            float len = voice.Play(LineId(npc, "say"));
            StartCoroutine(SayThenQuestion(len <= 0 ? 3.2f : len + 0.4f));
        }

        IEnumerator SayThenQuestion(float wait)
        {
            float t0 = Time.time;
            while (Time.time - t0 < wait)
            {
                if (state != State.Dialogue) yield break;
                if (ui.SkipRequested) break;
                yield return null;
            }
            ShowQuestion();
        }

        void ShowQuestion()
        {
            if (activeNpc == null || awaitingAnswer) return;
            awaitingAnswer = true;
            ui.ShowQuestion(activeNpc, hints > 0);
            voice.Play(LineId(activeNpc, "q"));
        }

        public void OnHint()
        {
            if (hints <= 0 || hintUsed) return;
            hints--; hintUsed = true;
            ui.ApplyHint(activeNpc.enc.answer);
            ui.UpdateHud();
        }

        public void OnAnswer(int optionIdx)
        {
            if (!awaitingAnswer) return;
            awaitingAnswer = false;
            var enc = activeNpc.enc;
            bool right = optionIdx == enc.answer;
            string verdict;

            if (right)
            {
                correct++;
                streak++;
                bestStreak = Mathf.Max(bestStreak, streak);
                int earned = GOLD_CORRECT * hero.goldMult;
                string bonus = "";
                if (streak >= 3)
                {
                    earned += GOLD_STREAK * hero.goldMult;
                    bonus = " " + Loc.F(content.ui.streakBonus, streak);
                }
                gold += earned;
                verdict = Loc.F(content.ui.correct, earned) + bonus;
                Sfx.Correct();
            }
            else
            {
                streak = 0;
                hearts--;
                if (hearts <= 0 && hero.falconSave && !falconUsed)
                {
                    falconUsed = true;
                    hearts = 1;
                    verdict = Loc.T(content.ui.wrong) + " " + Loc.T(content.ui.falconSave);
                    if (falcon != null) { Destroy(falcon); falcon = null; }
                }
                else verdict = Loc.T(content.ui.wrong);
                Sfx.Wrong();
            }

            activeNpc.done = true;
            activeNpc.mark.text = "✓";
            activeNpc.mark.color = Hexc.C("#2ec4a0");

            ui.ShowFeedback(right, optionIdx, activeNpc.enc.answer, verdict, Loc.T(enc.fact));
            voice.Play(LineId(activeNpc, "fact"));
            ui.UpdateHud();
        }

        public void OnDialogueContinue()
        {
            if (state != State.Dialogue) return;
            var npc = activeNpc;
            activeNpc = null;
            vignettes.Stop();
            voice.Stop();
            npc.rig.talking = false;
            ui.HideDialogue();
            camDir.ExitVignette();

            if (hearts <= 0)
            {
                state = State.GameOver;
                ui.ShowGameOver();
                return;
            }

            state = State.Play;
            if (GateOpen())
            {
                WorldBuilder.OpenGateVisual(level);
                bool last = countryIdx >= content.countries.Length - 1;
                ui.Toast(last
                    ? Loc.T(content.ui.gateOpenFinal)
                    : Loc.F(content.ui.gateOpen, Loc.T(content.countries[countryIdx + 1].name)));
                Sfx.Gate();
            }
        }

        public string LineId(NpcInstance npc, string part)
        {
            int ei = level.npcs.IndexOf(npc);
            return $"c{countryIdx:00}_e{ei}_{part}";
        }

        // ---------------- end states ----------------

        public void OnRetry()
        {
            gold = startGold; correct = startCorrect; hints = startHints;
            hearts = hero.hearts; streak = 0;
            ui.HideGameOver();
            LoadCountry(countryIdx);
        }

        public void OnNewJourney()
        {
            if (level != null) { Destroy(level.root); level = null; }
            if (playerRoot != null) Destroy(playerRoot);
            if (falcon != null) Destroy(falcon);
            ui.HideAllPanels();
            state = State.Menu;
            ui.ShowMenu();
        }

        void ShowVictory()
        {
            state = State.Victory;
            int total = 0;
            foreach (var c in content.countries) total += c.encounters.Length;
            float ratio = (float)correct / total;
            RankDef rank = content.ranks[content.ranks.Length - 1];
            foreach (var r in content.ranks) { if (ratio >= r.min) { rank = r; break; } }
            ui.ShowVictory(total, rank);
        }
    }

    // =====================================================================
    //  Tiny procedural sound effects (no audio assets needed)
    // =====================================================================

    public static class Sfx
    {
        static AudioSource src;

        static AudioSource Src()
        {
            if (src == null)
            {
                var go = new GameObject("Sfx");
                Object.DontDestroyOnLoad(go);
                src = go.AddComponent<AudioSource>();
            }
            return src;
        }

        static void Tone(float freq, float dur, float vol = 0.16f)
        {
            int rate = 44100;
            int n = (int)(rate * dur);
            var clip = AudioClip.Create("tone", n, 1, rate, false);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float env = 1f - (float)i / n;
                data[i] = Mathf.Sin(2 * Mathf.PI * freq * i / rate) * env * vol;
            }
            clip.SetData(data, 0);
            Src().PlayOneShot(clip);
        }

        public static void Coin() => Tone(880, 0.1f);
        public static void Correct() { Tone(523, 0.12f); Tone(784, 0.2f); }
        public static void Wrong() => Tone(150, 0.3f);
        public static void Gate() { Tone(392, 0.14f); Tone(659, 0.22f); }
    }
}
