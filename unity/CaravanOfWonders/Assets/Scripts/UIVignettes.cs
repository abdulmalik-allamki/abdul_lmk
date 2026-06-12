using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Caravan
{
    // =====================================================================
    //  Vignette player — the scene DESCRIPTION is not read or typed;
    //  it is performed. Each encounter plays a small staged 3D animation
    //  next to the dialogue: the tea-maker actually pours with foam rising,
    //  the ore train rolls past, items are presented, landmarks pointed at.
    // =====================================================================

    public class VignettePlayer : MonoBehaviour
    {
        GameObject props;
        Coroutine routine;
        Rig actor;

        public void Play(NpcInstance npc, LevelData level, Transform player)
        {
            Stop();
            actor = npc.rig;
            actor.overrideArms = true;
            props = new GameObject("VignetteProps");
            Vector3 origin = npc.root.transform.position;
            Vector3 toPlayer = (player.position - origin).normalized;

            switch (npc.enc.vignette)
            {
                case "tea_pour":   routine = StartCoroutine(TeaPour(npc, origin, toPlayer)); break;
                case "train_pass": routine = StartCoroutine(TrainPass(npc, origin, level)); break;
                case "show_item":  routine = StartCoroutine(ShowItem(npc)); break;
                case "point_far":  routine = StartCoroutine(PointFar(npc, origin)); break;
                default:
                    actor.overrideArms = false; // generic_talk uses the rig's talk gestures
                    break;
            }
        }

        public void Stop()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            if (actor != null) { actor.overrideArms = false; actor = null; }
            if (props != null) { Destroy(props); props = null; }
        }

        // ---- the tea ceremony: pot raised high, a golden stream, foam rising ----
        IEnumerator TeaPour(NpcInstance npc, Vector3 origin, Vector3 toPlayer)
        {
            var rig = npc.rig;
            Transform hand = rig.handR;

            // tray with three small glasses between the two characters
            Vector3 trayPos = origin + toPlayer * 1.0f;
            B.Cyl(props.transform, trayPos + Vector3.up * 0.12f, new Vector3(0.55f, 0.02f, 0.55f), Hexc.C("#c98a2d"));
            var cups = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                float a = (i - 1) * 0.8f;
                Vector3 cupPos = trayPos + new Vector3(Mathf.Cos(a) * 0.16f, 0.14f, Mathf.Sin(a) * 0.16f);
                cups[i] = B.Cyl(props.transform, cupPos + Vector3.up * 0.06f, new Vector3(0.08f, 0.06f, 0.08f), Hexc.C("#e8e4d8")).transform;
            }

            // teapot held in the right hand
            var pot = new GameObject("teapot");
            pot.transform.SetParent(hand, false);
            B.Sph(pot.transform, Vector3.zero, new Vector3(0.3f, 0.24f, 0.3f), Hexc.C("#b8b0a0"));
            var spout = B.Cyl(pot.transform, new Vector3(0.18f, 0.05f, 0), new Vector3(0.05f, 0.12f, 0.05f), Hexc.C("#b8b0a0"));
            spout.transform.localRotation = Quaternion.Euler(0, 0, -55f);

            // the stream
            var streamGo = new GameObject("stream");
            streamGo.transform.SetParent(props.transform, false);
            var lr = streamGo.AddComponent<LineRenderer>();
            lr.material = MatLib.Unlit(Hexc.C("#d8a93a"));
            lr.startWidth = 0.035f; lr.endWidth = 0.02f;
            lr.positionCount = 12;
            lr.enabled = false;

            // rising foam at the cup
            var foam = NewParticles(props.transform, Vector3.zero,
                new Color(1f, 0.97f, 0.85f, 0.6f), 0.06f, 0.5f, 1.2f, 14f);
            foam.Stop();

            int cupIdx = 0;
            while (true)
            {
                var cup = cups[cupIdx % 3];
                cupIdx++;

                // raise the arm high (the famous high Mauritanian pour)
                yield return ArmTo(rig.armR, Quaternion.Euler(-150f, 0, -18f), 0.9f);

                // pour: animate the stream from spout to cup
                lr.enabled = true;
                foam.transform.position = cup.position + Vector3.up * 0.1f;
                foam.Play();
                float t0 = Time.time;
                Vector3 spoutTip = Vector3.zero;
                while (Time.time - t0 < 2.4f)
                {
                    spoutTip = spout.transform.position + spout.transform.up * 0.14f;
                    Vector3 target = cup.position + Vector3.up * 0.1f;
                    for (int i = 0; i < lr.positionCount; i++)
                    {
                        float f = i / (float)(lr.positionCount - 1);
                        Vector3 p = Vector3.Lerp(spoutTip, target, f);
                        p.x += Mathf.Sin(Time.time * 14f + f * 9f) * 0.012f * f;
                        lr.SetPosition(i, p);
                    }
                    yield return null;
                }
                lr.enabled = false;
                foam.Stop();

                // lower, small nod, repeat
                yield return ArmTo(rig.armR, Quaternion.Euler(-30f, 0, -8f), 0.8f);
                yield return new WaitForSeconds(0.7f);
            }
        }

        // ---- the desert train thunders past in the background ----
        IEnumerator TrainPass(NpcInstance npc, Vector3 origin, LevelData level)
        {
            var rig = npc.rig;
            // point at the horizon
            StartCoroutine(ArmTo(rig.armR, Quaternion.Euler(-80f, 0, -40f), 1.0f));

            while (true)
            {
                var train = Decor.Build("train", props.transform,
                    new Vector3(origin.x - 45f, 0, 7f), 1.1f, level.theme);
                var dust = NewParticles(train.transform, new Vector3(-2.6f, 1.6f, 0),
                    new Color(0.85f, 0.85f, 0.85f, 0.4f), 0.3f, 0.8f, 2.5f, 9f);
                dust.Play();
                float t0 = Time.time;
                while (Time.time - t0 < 9f)
                {
                    train.transform.position += Vector3.right * 10f * Time.deltaTime;
                    yield return null;
                }
                Destroy(train);
                yield return new WaitForSeconds(1.2f);
            }
        }

        // ---- presenting a treasure: raised, turning, glinting ----
        IEnumerator ShowItem(NpcInstance npc)
        {
            var rig = npc.rig;
            var item = new GameObject("item");
            item.transform.SetParent(rig.handR, false);
            item.transform.localPosition = new Vector3(0, -0.05f, 0.1f);
            // a golden book / treasure block with a glow
            B.Box(item.transform, Vector3.zero, new Vector3(0.28f, 0.07f, 0.2f), Hexc.C("#caa84a"));
            B.Box(item.transform, new Vector3(0, 0.045f, 0), new Vector3(0.24f, 0.025f, 0.17f), Hexc.C("#f3e9c9"));
            var sparkle = NewParticles(item.transform, Vector3.up * 0.1f,
                new Color(1f, 0.9f, 0.5f, 0.8f), 0.045f, 0.35f, 1.0f, 6f);
            sparkle.Play();

            // raise to chest height and slowly turn it
            yield return ArmTo(rig.armR, Quaternion.Euler(-95f, 0, -20f), 1.1f);
            while (true)
            {
                item.transform.Rotate(0, 40f * Time.deltaTime, 0, Space.World);
                rig.armR.localRotation = Quaternion.Euler(-95f + Mathf.Sin(Time.time * 1.2f) * 6f, 0, -20f);
                yield return null;
            }
        }

        // ---- pointing at a far landmark with a guiding beacon of light ----
        IEnumerator PointFar(NpcInstance npc, Vector3 origin)
        {
            var rig = npc.rig;
            // beacon over the nearest big decor to the east, or simply far east
            Vector3 target = origin + new Vector3(9f, 0, 5f);
            var beacon = B.Cyl(props.transform, target + Vector3.up * 3f, new Vector3(0.5f, 3f, 0.5f), Color.white);
            var mr = beacon.GetComponent<Renderer>();
            mr.material = MatLib.Unlit(new Color(1f, 0.85f, 0.45f, 0.28f));

            yield return ArmTo(rig.armR, Quaternion.Euler(-95f, -30f, 0), 1.0f);
            while (true)
            {
                float pulse = 0.18f + 0.14f * Mathf.Sin(Time.time * 2.4f);
                mr.material.color = new Color(1f, 0.85f, 0.45f, pulse);
                rig.armR.localRotation = Quaternion.Euler(-95f + Mathf.Sin(Time.time * 0.9f) * 5f, -30f, 0);
                yield return null;
            }
        }

        IEnumerator ArmTo(Transform arm, Quaternion target, float dur)
        {
            Quaternion from = arm.localRotation;
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                arm.localRotation = Quaternion.Slerp(from, target, Mathf.SmoothStep(0, 1, t / dur));
                yield return null;
            }
        }

        static ParticleSystem NewParticles(Transform parent, Vector3 localPos, Color c,
            float size, float speed, float life, float rate)
        {
            var go = new GameObject("particles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSize = size;
            main.startSpeed = speed;
            main.startLifetime = life;
            main.startColor = c;
            main.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = rate;
            var shape = ps.shape; shape.angle = 12f; shape.radius = 0.03f;
            ps.GetComponent<ParticleSystemRenderer>().material = MatLib.Unlit(new Color(1, 1, 1, 0.5f));
            return ps;
        }
    }

    // =====================================================================
    //  Programmatic uGUI — menus, HUD, dialogue panel, end screens.
    //  Every label goes through Loc.Fix so Arabic renders shaped + RTL.
    // =====================================================================

    public class GameUI : MonoBehaviour
    {
        GameManager gm;
        Font font;
        RectTransform root, menuPanel, arrivePanel, dialoguePanel, overPanel, winPanel;
        Text hudText, promptText, toastText, nameText, sayText, feedbackText;
        Text arriveTitle, arriveBody, overBody, winBody, winStats, winRank;
        Button hintBtn, contBtn, skipBtn;
        Text hintLabel, contLabel;
        readonly Button[] answerBtns = new Button[4];
        readonly Text[] answerLabels = new Text[4];
        readonly Image[] answerImgs = new Image[4];
        Image fadeImg;
        CanvasGroup toastGroup;
        public bool SkipRequested;
        readonly List<(Text txt, Func<string> get)> bindings = new List<(Text, Func<string>)>();
        Country arriveCountry;

        static readonly Color PANEL = new Color(0.10f, 0.075f, 0.19f, 0.94f);
        static readonly Color GOLD = new Color(0.953f, 0.788f, 0.412f);
        static readonly Color SAND = new Color(0.969f, 0.910f, 0.788f);
        static readonly Color OASIS = new Color(0.18f, 0.77f, 0.63f);
        static readonly Color DANGER = new Color(0.894f, 0.341f, 0.369f);

        public static GameUI Build(GameManager gm)
        {
            var go = new GameObject("UI");
            var ui = go.AddComponent<GameUI>();
            ui.gm = gm;
            ui.Construct();
            return ui;
        }

        void Construct()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasGo.AddComponent<GraphicRaycaster>();
            root = canvas.GetComponent<RectTransform>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            BuildHud();
            BuildMenu();
            BuildArrive();
            BuildDialogue();
            BuildGameOver();
            BuildVictory();

            // toast
            toastText = MakeText(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -70), new Vector2(800, 44), "", 22, GOLD, TextAnchor.MiddleCenter);
            toastGroup = toastText.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0;

            // prompt
            promptText = MakeText(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 90), new Vector2(420, 40), "", 22, GOLD, TextAnchor.MiddleCenter);
            promptText.gameObject.SetActive(false);

            // fade
            var fadeGo = MakePanel(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.black);
            fadeImg = fadeGo.GetComponent<Image>();
            fadeImg.raycastTarget = false;
            fadeImg.color = new Color(0, 0, 0, 0);
        }

        // ---------------- construction helpers ----------------

        RectTransform MakePanel(RectTransform parent, Vector2 aMin, Vector2 aMax,
            Vector2 offset, Vector2 size, Color c)
        {
            var go = new GameObject("panel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.anchoredPosition = offset;
            if (size != Vector2.zero) rt.sizeDelta = size;
            else { rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
            var img = go.AddComponent<Image>();
            img.color = c;
            return rt;
        }

        Text MakeText(RectTransform parent, Vector2 aMin, Vector2 aMax,
            Vector2 offset, Vector2 size, string content, int fs, Color c, TextAnchor anchor)
        {
            var go = new GameObject("text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = fs;
            t.color = c;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = content;
            return t;
        }

        Button MakeButton(RectTransform parent, Vector2 aMin, Vector2 aMax,
            Vector2 offset, Vector2 size, string label, int fs, Action onClick, Color bg, out Text txt)
        {
            var rt = MakePanel(parent, aMin, aMax, offset, size, bg);
            rt.name = "btn_" + label;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = rt.GetComponent<Image>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.05f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.8f);
            btn.colors = colors;
            txt = MakeText(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, label, fs,
                bg.grayscale > 0.5f ? new Color(0.1f, 0.07f, 0.19f) : SAND, TextAnchor.MiddleCenter);
            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        void Bind(Text t, Func<string> get)
        {
            bindings.Add((t, get));
            t.text = Loc.Fix(get());
        }

        public void RefreshAll()
        {
            foreach (var (t, get) in bindings)
                if (t != null) t.text = Loc.Fix(get());
            UpdateHud();
            if (arrivePanel.gameObject.activeSelf && arriveCountry != null)
                FillArrive(arriveCountry);
        }

        // ---------------- HUD ----------------

        void BuildHud()
        {
            hudText = MakeText(root, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -26), new Vector2(-40, 40), "", 24, SAND, TextAnchor.MiddleLeft);
            hudText.rectTransform.anchoredPosition = new Vector2(20, -26);

            MakeButton(root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -30),
                new Vector2(116, 38), "", 20, () => gm.OnToggleLanguage(), new Color(1, 1, 1, 0.12f), out var langTxt);
            Bind(langTxt, () => Loc.T(gm.content.ui.language));
        }

        public void UpdateHud()
        {
            if (gm.hero == null) { hudText.text = ""; return; }
            string hearts = new string('♥', Mathf.Max(0, gm.hearts))
                          + new string('♡', Mathf.Max(0, gm.hero.hearts - gm.hearts));
            var country = gm.content.countries[gm.countryIdx];
            int done = 0;
            if (gm.level != null) foreach (var n in gm.level.npcs) if (n.done) done++;
            string place = $"{Loc.T(country.name)} {done}/3 · {gm.countryIdx + 1}/{gm.content.countries.Length}";
            hudText.text = Loc.Fix($"{hearts}   ◆ {gm.gold}   ★ {gm.hints}   |   {place}");
        }

        public void ShowPrompt(bool on)
        {
            promptText.gameObject.SetActive(on);
            if (on) promptText.text = Loc.Fix("E — " + Loc.T(gm.content.ui.talk));
        }

        public void Toast(string msg)
        {
            toastText.text = Loc.Fix(msg);
            StopCoroutine(nameof(ToastRoutine));
            StartCoroutine(nameof(ToastRoutine));
        }

        IEnumerator ToastRoutine()
        {
            toastGroup.alpha = 1f;
            yield return new WaitForSeconds(3.2f);
            while (toastGroup.alpha > 0) { toastGroup.alpha -= Time.deltaTime * 2f; yield return null; }
        }

        public IEnumerator Fade(float target, float dur)
        {
            float from = fadeImg.color.a, t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                fadeImg.color = new Color(0, 0, 0, Mathf.Lerp(from, target, t / dur));
                yield return null;
            }
            fadeImg.color = new Color(0, 0, 0, target);
        }

        // ---------------- menu ----------------

        void BuildMenu()
        {
            menuPanel = MakePanel(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.08f, 0.06f, 0.16f, 0.97f));

            var title = MakeText(menuPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -70), new Vector2(900, 60), "", 46, GOLD, TextAnchor.MiddleCenter);
            Bind(title, () => "✦ " + Loc.T(gm.content.ui.title) + " ✦");

            var tagline = MakeText(menuPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -120), new Vector2(900, 36), "", 22, SAND, TextAnchor.MiddleCenter);
            Bind(tagline, () => Loc.T(gm.content.ui.tagline));

            var intro = MakeText(menuPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -205), new Vector2(960, 130), "", 19, SAND, TextAnchor.UpperCenter);
            Bind(intro, () => Loc.T(gm.content.ui.introBody));

            var choose = MakeText(menuPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -310), new Vector2(600, 34), "", 24, GOLD, TextAnchor.MiddleCenter);
            Bind(choose, () => Loc.T(gm.content.ui.chooseTraveler));

            for (int i = 0; i < gm.content.characters.Length; i++)
            {
                var ch = gm.content.characters[i];
                float x = (i - 1.5f) * 280f;
                var card = MakeButton(menuPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(x, -440), new Vector2(255, 190), "", 20,
                    () => gm.OnBegin(ch), new Color(1, 1, 1, 0.07f), out _);
                var nameT = MakeText(card.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(0, -32), new Vector2(240, 34), "", 25, GOLD, TextAnchor.MiddleCenter);
                Bind(nameT, () => Loc.T(ch.name));
                var titleT = MakeText(card.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(0, -64), new Vector2(240, 28), "", 18, SAND, TextAnchor.MiddleCenter);
                Bind(titleT, () => Loc.T(ch.title));
                var perkT = MakeText(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0, 50), new Vector2(235, 80), "", 16, OASIS, TextAnchor.MiddleCenter);
                Bind(perkT, () => Loc.T(ch.perk));
            }

            var beginHint = MakeText(menuPanel, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 70), new Vector2(700, 32), "", 20, SAND, TextAnchor.MiddleCenter);
            Bind(beginHint, () => "▲ " + Loc.T(gm.content.ui.begin) + " ▲");

            MakeButton(menuPanel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28),
                new Vector2(140, 38), "", 20, () => gm.OnToggleLanguage(), new Color(1, 1, 1, 0.12f), out var langTxt2);
            Bind(langTxt2, () => Loc.T(gm.content.ui.language));
        }

        public void ShowMenu() => menuPanel.gameObject.SetActive(true);
        public void HideMenu() => menuPanel.gameObject.SetActive(false);

        // ---------------- country arrival ----------------

        void BuildArrive()
        {
            arrivePanel = MakePanel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(860, 380), PANEL);
            arriveTitle = MakeText(arrivePanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -54), new Vector2(800, 50), "", 36, GOLD, TextAnchor.MiddleCenter);
            arriveBody = MakeText(arrivePanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -180), new Vector2(790, 190), "", 20, SAND, TextAnchor.UpperCenter);
            MakeButton(arrivePanel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 44),
                new Vector2(320, 52), "", 24, () => gm.OnEnterCountry(), GOLD, out var contT);
            Bind(contT, () => Loc.T(gm.content.ui.@continue) + " →");
            arrivePanel.gameObject.SetActive(false);
        }

        void FillArrive(Country c)
        {
            string other = Loc.Ar ? c.name.en : c.name.ar;
            arriveTitle.text = Loc.Fix(Loc.T(c.name)) + "  ·  " + (Loc.Ar ? other : ArabicShaper.Shape(other));
            arriveBody.text = Loc.Fix(Loc.T(c.scene));
        }

        public void ShowArrive(Country c)
        {
            arriveCountry = c;
            FillArrive(c);
            arrivePanel.gameObject.SetActive(true);
        }

        public void HideArrive() => arrivePanel.gameObject.SetActive(false);

        // ---------------- dialogue ----------------

        void BuildDialogue()
        {
            dialoguePanel = MakePanel(root, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 150), new Vector2(-60, 290), PANEL);
            dialoguePanel.anchoredPosition = new Vector2(0, 155);

            nameText = MakeText(dialoguePanel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(150, -26), new Vector2(280, 32), "", 22, OASIS, TextAnchor.MiddleLeft);
            sayText = MakeText(dialoguePanel, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -72), new Vector2(-60, 64), "", 20, SAND, TextAnchor.UpperLeft);
            sayText.rectTransform.anchoredPosition = new Vector2(30, -72);

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                float bx = (i % 2 == 0) ? -300f : 300f;
                float by = (i < 2) ? -130f : -185f;
                answerBtns[i] = MakeButton(dialoguePanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(bx, by), new Vector2(560, 48), "", 19,
                    () => gm.OnAnswer(idx), new Color(1, 1, 1, 0.08f), out answerLabels[i]);
                answerImgs[i] = answerBtns[i].GetComponent<Image>();
            }

            feedbackText = MakeText(dialoguePanel, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(30, 46), new Vector2(-330, 64), "", 17, SAND, TextAnchor.UpperLeft);

            hintBtn = MakeButton(dialoguePanel, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-260, 40), new Vector2(170, 44), "", 19, () => gm.OnHint(),
                new Color(1, 1, 1, 0.12f), out hintLabel);
            contBtn = MakeButton(dialoguePanel, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-110, 40), new Vector2(170, 44), "", 19, () => gm.OnDialogueContinue(),
                GOLD, out contLabel);
            skipBtn = MakeButton(dialoguePanel, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-50, -30), new Vector2(64, 40), "▼", 22, () => SkipRequested = true,
                new Color(1, 1, 1, 0.12f), out _);

            dialoguePanel.gameObject.SetActive(false);
        }

        public void ShowSay(NpcInstance npc)
        {
            SkipRequested = false;
            dialoguePanel.gameObject.SetActive(true);
            nameText.text = Loc.Fix("✦ " + Loc.T(npc.enc.persona));
            sayText.text = Loc.Fix("“" + Loc.T(npc.enc.say) + "”");
            sayText.fontStyle = FontStyle.Italic;
            feedbackText.text = "";
            foreach (var b in answerBtns) b.gameObject.SetActive(false);
            hintBtn.gameObject.SetActive(false);
            contBtn.gameObject.SetActive(false);
            skipBtn.gameObject.SetActive(true);
        }

        public void ShowQuestion(NpcInstance npc, bool hintAvailable)
        {
            skipBtn.gameObject.SetActive(false);
            sayText.fontStyle = FontStyle.Normal;
            sayText.text = Loc.Fix(Loc.T(npc.enc.question));
            sayText.color = GOLD;
            for (int i = 0; i < 4; i++)
            {
                answerBtns[i].gameObject.SetActive(true);
                answerBtns[i].interactable = true;
                answerImgs[i].color = new Color(1, 1, 1, 0.08f);
                answerLabels[i].text = Loc.Fix($"{i + 1}.  {Loc.T(npc.enc.options[i])}");
                answerLabels[i].color = SAND;
            }
            hintBtn.gameObject.SetActive(hintAvailable);
            hintLabel.text = Loc.Fix("★ " + Loc.T(gm.content.ui.hint) + $" ({gm.hints})");
            contBtn.gameObject.SetActive(false);
        }

        public void ApplyHint(int answerIdx)
        {
            var wrong = new List<int>();
            for (int i = 0; i < 4; i++) if (i != answerIdx) wrong.Add(i);
            for (int k = 0; k < 2; k++)
            {
                int pick = wrong[UnityEngine.Random.Range(0, wrong.Count)];
                wrong.Remove(pick);
                answerBtns[pick].interactable = false;
                answerLabels[pick].color = new Color(1, 1, 1, 0.25f);
            }
            hintBtn.gameObject.SetActive(false);
        }

        public void ShowFeedback(bool right, int chosen, int answerIdx, string verdict, string fact)
        {
            foreach (var b in answerBtns) b.interactable = false;
            answerImgs[answerIdx].color = new Color(OASIS.r, OASIS.g, OASIS.b, 0.45f);
            if (!right) answerImgs[chosen].color = new Color(DANGER.r, DANGER.g, DANGER.b, 0.4f);
            feedbackText.text = Loc.Fix(verdict + "\n" + fact);
            feedbackText.color = right ? new Color(0.62f, 0.94f, 0.86f) : new Color(1f, 0.7f, 0.72f);
            hintBtn.gameObject.SetActive(false);
            contBtn.gameObject.SetActive(true);
            contLabel.text = Loc.Fix(Loc.T(gm.content.ui.@continue) + " →");
        }

        public void HideDialogue()
        {
            sayText.color = SAND;
            dialoguePanel.gameObject.SetActive(false);
        }

        // ---------------- game over / victory ----------------

        void BuildGameOver()
        {
            overPanel = MakePanel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(740, 360), PANEL);
            var title = MakeText(overPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -56), new Vector2(680, 48), "", 32, DANGER, TextAnchor.MiddleCenter);
            Bind(title, () => Loc.T(gm.content.ui.gameOverTitle));
            overBody = MakeText(overPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -140), new Vector2(660, 110), "", 20, SAND, TextAnchor.UpperCenter);
            MakeButton(overPanel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 110),
                new Vector2(420, 50), "", 21, () => gm.OnRetry(), GOLD, out var retryT);
            Bind(retryT, () => "⛺ " + Loc.F(gm.content.ui.retry,
                gm.content == null ? "" : Loc.T(gm.content.countries[gm.countryIdx].name)));
            MakeButton(overPanel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 48),
                new Vector2(420, 50), "", 21, () => gm.OnNewJourney(), new Color(1, 1, 1, 0.1f), out var newT);
            Bind(newT, () => Loc.T(gm.content.ui.newJourney));
            overPanel.gameObject.SetActive(false);
        }

        public void ShowGameOver()
        {
            overBody.text = Loc.Fix(Loc.F(gm.content.ui.gameOverBody,
                Loc.T(gm.hero.name), Loc.T(gm.content.countries[gm.countryIdx].name)));
            RefreshAll();
            overPanel.gameObject.SetActive(true);
        }

        public void HideGameOver() => overPanel.gameObject.SetActive(false);

        void BuildVictory()
        {
            winPanel = MakePanel(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(820, 460), PANEL);
            var title = MakeText(winPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -52), new Vector2(760, 48), "", 30, GOLD, TextAnchor.MiddleCenter);
            Bind(title, () => "🌅 " + Loc.T(gm.content.ui.victoryTitle));
            winBody = MakeText(winPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -140), new Vector2(740, 110), "", 19, SAND, TextAnchor.UpperCenter);
            winStats = MakeText(winPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -240), new Vector2(740, 44), "", 24, GOLD, TextAnchor.MiddleCenter);
            winRank = MakeText(winPanel, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -320), new Vector2(740, 90), "", 21, OASIS, TextAnchor.UpperCenter);
            MakeButton(winPanel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 44),
                new Vector2(380, 52), "", 21, () => gm.OnNewJourney(), GOLD, out var againT);
            Bind(againT, () => Loc.T(gm.content.ui.newJourney) + " 🐪");
            winPanel.gameObject.SetActive(false);
        }

        public void ShowVictory(int total, RankDef rank)
        {
            winBody.text = Loc.Fix(Loc.F(gm.content.ui.victoryBody,
                Loc.T(gm.hero.name), gm.content.countries.Length));
            winStats.text = Loc.Fix(
                $"◆ {gm.gold} {Loc.T(gm.content.ui.goldEarned)}   ·   {gm.correct}/{total} {Loc.T(gm.content.ui.riddlesSolved)}   ·   🔥 {gm.bestStreak}");
            winRank.text = Loc.Fix(Loc.T(gm.content.ui.singWill) + "\n" + Loc.T(rank.name) + "\n" + Loc.T(rank.line));
            winPanel.gameObject.SetActive(true);
        }

        public void HideAllPanels()
        {
            HideArrive(); HideDialogue(); HideGameOver();
            winPanel.gameObject.SetActive(false);
            fadeImg.color = new Color(0, 0, 0, 0);
        }
    }
}
