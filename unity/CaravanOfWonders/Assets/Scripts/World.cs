using System.Collections.Generic;
using UnityEngine;

namespace Caravan
{
    // =====================================================================
    //  Small helpers
    // =====================================================================

    public static class Hexc
    {
        public static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }

    /// <summary>Cached flat-shaded materials per color.</summary>
    public static class MatLib
    {
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
        static Shader lit, unlit;

        public static Material Flat(Color c)
        {
            if (cache.TryGetValue(c, out var m)) return m;
            if (lit == null) lit = Shader.Find("Standard");
            m = new Material(lit);
            m.color = c;
            m.SetFloat("_Glossiness", 0.05f);
            cache[c] = m;
            return m;
        }

        public static Material Unlit(Color c)
        {
            if (unlit == null) unlit = Shader.Find("Sprites/Default");
            var m = new Material(unlit);
            m.color = c;
            return m;
        }
    }

    /// <summary>Procedural meshes (faceted, low-poly look).</summary>
    public static class PMesh
    {
        /// <summary>Truncated cone, flat-shaded. r1=0 gives a pyramid/cone tip.</summary>
        public static Mesh Cone(int sides, float r0, float r1, float h)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }
            for (int i = 0; i < sides; i++)
            {
                float a0 = Mathf.PI * 2f * i / sides, a1 = Mathf.PI * 2f * (i + 1) / sides;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * r0, 0, Mathf.Sin(a1) * r0);
                Vector3 t0 = new Vector3(Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1);
                Vector3 t1 = new Vector3(Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
                Tri(b0, t0, b1);
                if (r1 > 0.001f) Tri(b1, t0, t1);
                // bottom cap
                Tri(b1, Vector3.zero, b0);
                // top cap
                if (r1 > 0.001f) Tri(t0, new Vector3(0, h, 0), t1);
            }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Vertical quad with a top→bottom vertex-color gradient (sky).</summary>
        public static Mesh GradientQuad(float w, float h, Color top, Color bottom)
        {
            var m = new Mesh();
            m.vertices = new[]
            {
                new Vector3(-w/2, 0, 0), new Vector3(w/2, 0, 0),
                new Vector3(-w/2, h, 0), new Vector3(w/2, h, 0),
            };
            m.colors = new[] { bottom, bottom, top, top };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            m.RecalculateNormals();
            return m;
        }

        public static Mesh Sail()
        {
            var m = new Mesh();
            m.vertices = new[] { new Vector3(0, 0, 0), new Vector3(0, 1.6f, 0.15f), new Vector3(0, 0.1f, 1.1f) };
            m.triangles = new[] { 0, 1, 2, 2, 1, 0 };
            m.RecalculateNormals();
            return m;
        }
    }

    /// <summary>Terse primitive builders.</summary>
    public static class B
    {
        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, float rotY = 0)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            go.GetComponent<Renderer>().sharedMaterial = MatLib.Flat(c);
            return go;
        }

        public static GameObject Box(Transform p, Vector3 pos, Vector3 s, Color c, float rotY = 0) => Prim(PrimitiveType.Cube, p, pos, s, c, rotY);
        public static GameObject Sph(Transform p, Vector3 pos, Vector3 s, Color c) => Prim(PrimitiveType.Sphere, p, pos, s, c);
        public static GameObject Cyl(Transform p, Vector3 pos, Vector3 s, Color c) => Prim(PrimitiveType.Cylinder, p, pos, s, c);

        public static GameObject Cone(Transform p, Vector3 pos, int sides, float r0, float r1, float h, Color c)
        {
            var go = new GameObject("cone");
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().mesh = PMesh.Cone(sides, r0, r1, h);
            go.AddComponent<MeshRenderer>().sharedMaterial = MatLib.Flat(c);
            return go;
        }

        public static TextMesh Label(Transform p, Vector3 pos, string text, float size, Color c)
        {
            var go = new GameObject("label");
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, 90, 0); // face the side camera (-z side)
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.fontSize = 48;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = c;
            return tm;
        }
    }

    public class Rotator : MonoBehaviour
    {
        public float speed = 120f;
        void Update() => transform.Rotate(0, speed * Time.deltaTime, 0, Space.World);
    }

    public class Bobber : MonoBehaviour
    {
        public float amp = 0.12f, freq = 3f;
        Vector3 basePos;
        void Start() => basePos = transform.localPosition;
        void Update() => transform.localPosition = basePos + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * freq)) * amp;
    }

    /// <summary>Keeps the sky backdrop glued to the camera on X.</summary>
    public class SkyFollow : MonoBehaviour
    {
        public Transform cam;
        void LateUpdate()
        {
            if (cam != null)
                transform.position = new Vector3(cam.position.x, transform.position.y, transform.position.z);
        }
    }

    // =====================================================================
    //  Character rigs — low-poly people assembled from primitives, with
    //  procedural idle / walk / talk animation.
    // =====================================================================

    [System.Serializable]
    public class Palette
    {
        public Color skin, robe, accent, headC;
        public string head; // turban | hijab | keffiyeh | fez
        public Palette(string skin, string robe, string accent, string headC, string head)
        {
            this.skin = Hexc.C(skin); this.robe = Hexc.C(robe);
            this.accent = Hexc.C(accent); this.headC = Hexc.C(headC);
            this.head = head;
        }
    }

    public class Rig : MonoBehaviour
    {
        public Transform armL, armR, head, body, handR;
        public bool walking, talking;
        public bool overrideArms; // vignettes drive the arms directly
        float phase;
        Vector3 headBase;

        public void CacheBase() { headBase = head.localPosition; }

        void Update()
        {
            if (overrideArms)
            {
                phase += Time.deltaTime * 1.6f;
                body.localPosition = Vector3.up * (Mathf.Sin(phase) * 0.01f);
                return;
            }
            if (walking)
            {
                phase += Time.deltaTime * 9f;
                float s = Mathf.Sin(phase);
                armL.localRotation = Quaternion.Euler(s * 35f, 0, 0);
                armR.localRotation = Quaternion.Euler(-s * 35f, 0, 0);
                body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Cos(phase)) * 0.045f, 0);
            }
            else if (talking)
            {
                phase += Time.deltaTime * 7f;
                float s = Mathf.Sin(phase);
                armR.localRotation = Quaternion.Euler(-35f - s * 20f, 0, -15f);
                armL.localRotation = Quaternion.Euler(0, 0, 12f + Mathf.Sin(phase * 0.7f) * 8f);
                head.localPosition = headBase + Vector3.up * Mathf.Sin(phase * 1.4f) * 0.012f;
            }
            else
            {
                phase += Time.deltaTime * 1.6f;
                float s = Mathf.Sin(phase);
                armL.localRotation = Quaternion.Euler(s * 4f, 0, 4f);
                armR.localRotation = Quaternion.Euler(-s * 4f, 0, -4f);
                body.localPosition = Vector3.up * (Mathf.Sin(phase) * 0.01f);
            }
        }
    }

    public static class RigBuilder
    {
        public static readonly Dictionary<string, Palette> Heroes = new Dictionary<string, Palette>
        {
            { "rashid", new Palette("#c68a5a", "#d8c8a8", "#8a5a2a", "#7a4a2a", "turban") },
            { "layla",  new Palette("#b87848", "#3a5a9a", "#f3c969", "#e8e0d0", "hijab") },
            { "omar",   new Palette("#a86838", "#3a7a5a", "#f3c969", "#c83a3a", "fez") },
            { "zahra",  new Palette("#c08858", "#6a3a7a", "#2ec4a0", "#2a2a3a", "hijab") },
        };

        public static readonly Palette[] NpcM =
        {
            new Palette("#b87848", "#8a4a3a", "#caa84a", "#e8e0d0", "turban"),
            new Palette("#c68a5a", "#4a6a8a", "#d8d0c0", "#f0ece0", "keffiyeh"),
            new Palette("#a86838", "#7a6a3a", "#3a5a4a", "#caa84a", "turban"),
            new Palette("#c08858", "#5a3a6a", "#f3c969", "#d8d0c0", "turban"),
            new Palette("#b87848", "#3a6a5a", "#caa84a", "#b83a3a", "fez"),
            new Palette("#c68a5a", "#6a4a2a", "#e8e0d0", "#ffffff", "keffiyeh"),
        };

        public static readonly Palette[] NpcF =
        {
            new Palette("#c08858", "#7a3a5a", "#f3c969", "#d8d0c0", "hijab"),
            new Palette("#b87848", "#3a5a7a", "#2ec4a0", "#e8d8b8", "hijab"),
        };

        /// <summary>Builds a character at the parent. Faces +Z locally;
        /// rotate the root to face along the road.</summary>
        public static Rig Build(Transform parent, Palette p)
        {
            var root = new GameObject("rig");
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<Rig>();

            var body = new GameObject("body");
            body.transform.SetParent(root.transform, false);
            rig.body = body.transform;
            var bt = body.transform;

            // robe (truncated cone) + belt
            B.Cone(bt, Vector3.zero, 10, 0.34f, 0.17f, 1.05f, p.robe);
            B.Cyl(bt, new Vector3(0, 0.62f, 0), new Vector3(0.42f, 0.035f, 0.42f), p.accent);

            // head
            var headGo = B.Sph(bt, new Vector3(0, 1.27f, 0), Vector3.one * 0.44f, p.skin);
            rig.head = headGo.transform;
            // eyes
            B.Sph(rig.head, new Vector3(-0.18f, 0.05f, 0.4f), Vector3.one * 0.1f, Hexc.C("#241a12"));
            B.Sph(rig.head, new Vector3(0.18f, 0.05f, 0.4f), Vector3.one * 0.1f, Hexc.C("#241a12"));

            // headwear
            switch (p.head)
            {
                case "turban":
                    B.Sph(rig.head, new Vector3(0, 0.28f, 0), new Vector3(1.2f, 0.62f, 1.2f), p.headC);
                    B.Sph(rig.head, new Vector3(0, 0.62f, 0), Vector3.one * 0.4f, p.headC);
                    break;
                case "hijab":
                    B.Sph(rig.head, new Vector3(0, 0.07f, -0.1f), new Vector3(1.22f, 1.22f, 1.12f), p.headC);
                    B.Cone(bt, new Vector3(0, 0.95f, 0), 8, 0.3f, 0.13f, 0.42f, p.headC);
                    break;
                case "keffiyeh":
                    B.Sph(rig.head, new Vector3(0, 0.18f, -0.06f), new Vector3(1.25f, 0.75f, 1.2f), p.headC);
                    B.Cyl(rig.head, new Vector3(0, 0.3f, 0), new Vector3(0.56f, 0.035f, 0.56f), Hexc.C("#2a2a2a"));
                    B.Box(rig.head, new Vector3(0, -0.2f, -0.45f), new Vector3(0.55f, 0.7f, 0.12f), p.headC);
                    break;
                case "fez":
                    B.Cyl(rig.head, new Vector3(0, 0.42f, 0), new Vector3(0.42f, 0.18f, 0.42f), p.headC);
                    B.Sph(rig.head, new Vector3(0.16f, 0.6f, 0), Vector3.one * 0.1f, Hexc.C("#2a2a2a"));
                    break;
            }

            // arms — pivots at the shoulders
            rig.armL = MakeArm(bt, -1, p);
            rig.armR = MakeArm(bt, 1, p);
            var hand = new GameObject("handR");
            hand.transform.SetParent(rig.armR, false);
            hand.transform.localPosition = new Vector3(0, -0.52f, 0);
            rig.handR = hand.transform;

            rig.CacheBase();
            return rig;
        }

        static Transform MakeArm(Transform body, int side, Palette p)
        {
            var pivot = new GameObject(side < 0 ? "armL" : "armR");
            pivot.transform.SetParent(body, false);
            pivot.transform.localPosition = new Vector3(side * 0.3f, 1.0f, 0);
            B.Prim(PrimitiveType.Capsule, pivot.transform, new Vector3(0, -0.26f, 0),
                new Vector3(0.14f, 0.26f, 0.14f), p.robe);
            B.Sph(pivot.transform, new Vector3(0, -0.5f, 0), Vector3.one * 0.13f, p.skin);
            return pivot.transform;
        }
    }

    // =====================================================================
    //  Country themes for the 3D world
    // =====================================================================

    public class Theme
    {
        public Color skyTop, skyBottom, ground, far, fog, sun;
        public bool night, sea, sunset;
        public Color seaColor;
        public (string t, float fx, float z, float s)[] decor;
    }

    public static class Themes
    {
        // order matches content.json countries
        public static readonly Theme[] All =
        {
            new Theme { // Mauritania — Atlantic morning
                skyTop = Hexc.C("#4a78a8"), skyBottom = Hexc.C("#f0d8a8"), ground = Hexc.C("#d8b878"),
                far = Hexc.C("#b89060"), fog = Hexc.C("#e8d2a8"), sun = Hexc.C("#fff3c0"),
                sea = true, seaColor = Hexc.C("#2a6a9a"),
                decor = new[]{ ("train",0.10f,-3.5f,1f), ("tent",0.30f,2f,1f), ("palm",0.42f,-2.5f,1f),
                               ("dune",0.55f,5f,1.4f), ("palm",0.68f,2.5f,1.1f), ("tent",0.80f,-3f,0.9f) } },
            new Theme { // Morocco — Marrakesh dusk
                skyTop = Hexc.C("#2a1a4a"), skyBottom = Hexc.C("#e8833a"), ground = Hexc.C("#c08858"),
                far = Hexc.C("#4a2c5e"), fog = Hexc.C("#a86a52"), sun = Hexc.C("#ffb45a"), sunset = true,
                decor = new[]{ ("minaret",0.16f,4f,1f), ("stall",0.32f,-2.8f,1f), ("palm",0.44f,2.5f,1f),
                               ("stall",0.60f,3.5f,1f), ("palm",0.72f,-2.5f,1.1f), ("palm",0.84f,3f,0.9f) } },
            new Theme { // Algeria — white city day
                skyTop = Hexc.C("#3a88c8"), skyBottom = Hexc.C("#cfe8f8"), ground = Hexc.C("#e0d0a8"),
                far = Hexc.C("#5a7a9a"), fog = Hexc.C("#cfe0e8"), sun = Hexc.C("#fff3c0"),
                sea = true, seaColor = Hexc.C("#2a78b8"),
                decor = new[]{ ("houseWhite",0.14f,3.5f,1f), ("minaret",0.28f,5f,1f), ("houseWhite",0.44f,-3.5f,1f),
                               ("palm",0.58f,2.5f,1f), ("houseWhite",0.70f,4f,1.2f), ("palm",0.82f,-2.5f,1f) } },
            new Theme { // Tunisia — blue & white coast
                skyTop = Hexc.C("#3aa0d8"), skyBottom = Hexc.C("#bfe6f5"), ground = Hexc.C("#e8d8b0"),
                far = Hexc.C("#d8c8a0"), fog = Hexc.C("#d8e8f0"), sun = Hexc.C("#fff3c0"),
                sea = true, seaColor = Hexc.C("#2a88c8"),
                decor = new[]{ ("houseTN",0.15f,3.5f,1f), ("houseTN",0.30f,-3.5f,1f), ("palm",0.42f,2.5f,1f),
                               ("dhow",0.55f,9f,1f), ("houseTN",0.68f,4f,1.1f), ("palm",0.80f,-2.5f,1f) } },
            new Theme { // Libya — hot marble coast
                skyTop = Hexc.C("#c89858"), skyBottom = Hexc.C("#f0e0b0"), ground = Hexc.C("#e8d098"),
                far = Hexc.C("#c8a868"), fog = Hexc.C("#e8d8a8"), sun = Hexc.C("#fff8d8"),
                sea = true, seaColor = Hexc.C("#3a88a8"),
                decor = new[]{ ("columns",0.18f,3f,1f), ("columns",0.30f,-3f,1.2f), ("palm",0.46f,2.5f,1f),
                               ("houseWhite",0.60f,3.5f,1f), ("dune",0.74f,5f,1.2f), ("columns",0.85f,4f,1f) } },
            new Theme { // Egypt — golden Nile
                skyTop = Hexc.C("#e8a33d"), skyBottom = Hexc.C("#f7dca0"), ground = Hexc.C("#dfc07a"),
                far = Hexc.C("#b8863a"), fog = Hexc.C("#ecd098"), sun = Hexc.C("#fff3c0"),
                sea = true, seaColor = Hexc.C("#3a78a8"),
                decor = new[]{ ("obelisk",0.18f,3f,1f), ("palm",0.30f,-2.5f,1f), ("pyramid",0.46f,14f,1f),
                               ("pyramid",0.52f,20f,0.7f), ("palm",0.60f,2.5f,1f), ("obelisk",0.72f,-3f,1f), ("palm",0.84f,3f,1f) } },
            new Theme { // Jordan — rose canyon
                skyTop = Hexc.C("#9a4a3a"), skyBottom = Hexc.C("#e8b48a"), ground = Hexc.C("#c97a5a"),
                far = Hexc.C("#6a2c3a"), fog = Hexc.C("#c08a72"), sun = Hexc.C("#ffd8a8"),
                decor = new[]{ ("rock",0.14f,3.5f,1f), ("rock",0.28f,-3.5f,1.3f), ("petra",0.46f,6f,1f),
                               ("rock",0.64f,4f,1.1f), ("rock",0.80f,-3f,0.9f) } },
            new Theme { // Lebanon — cedars & sea
                skyTop = Hexc.C("#3a78c8"), skyBottom = Hexc.C("#cfe8f8"), ground = Hexc.C("#a8b888"),
                far = Hexc.C("#5a7a9a"), fog = Hexc.C("#c8dce8"), sun = Hexc.C("#fff3c0"),
                sea = true, seaColor = Hexc.C("#2a78c8"),
                decor = new[]{ ("cedar",0.16f,3f,1f), ("cedar",0.33f,-3f,1.2f), ("houseWhite",0.48f,3.5f,1f),
                               ("cedar",0.64f,2.8f,1f), ("cedar",0.80f,-2.8f,1.1f) } },
            new Theme { // Saudi Arabia — starry night dunes
                skyTop = Hexc.C("#0d1030"), skyBottom = Hexc.C("#2a2050"), ground = Hexc.C("#4a3868"),
                far = Hexc.C("#1a1438"), fog = Hexc.C("#241c40"), sun = Hexc.C("#f0ecd8"), night = true,
                decor = new[]{ ("dune",0.14f,4f,1.2f), ("tent",0.30f,-3f,1f), ("dune",0.46f,5f,1.5f),
                               ("tent",0.62f,3f,1f), ("skyscraper",0.80f,5f,1f), ("skyscraper",0.86f,7f,1.3f) } },
            new Theme { // UAE — gulf evening skyline
                skyTop = Hexc.C("#2a1a5a"), skyBottom = Hexc.C("#c85a8a"), ground = Hexc.C("#d8b888"),
                far = Hexc.C("#1a1240"), fog = Hexc.C("#6a4a6a"), sun = Hexc.C("#ffb45a"), sunset = true,
                sea = true, seaColor = Hexc.C("#3a4a8a"),
                decor = new[]{ ("palm",0.18f,2.5f,1f), ("skyscraper",0.34f,6f,1f), ("burj",0.41f,10f,1f),
                               ("palm",0.54f,-2.5f,1f), ("skyscraper",0.70f,7f,1.2f), ("palm",0.82f,2.5f,1f) } },
            new Theme { // Oman — dawn over Sur
                skyTop = Hexc.C("#3a9ab8"), skyBottom = Hexc.C("#f0e8c8"), ground = Hexc.C("#d8c8a0"),
                far = Hexc.C("#6a7a72"), fog = Hexc.C("#d8e0d0"), sun = Hexc.C("#ffd890"), sunset = true,
                sea = true, seaColor = Hexc.C("#2a8898"),
                decor = new[]{ ("fort",0.22f,4f,1f), ("dhow",0.38f,9f,1f), ("palm",0.52f,2.5f,1f),
                               ("incense",0.64f,-2.2f,1f), ("houseWhite",0.76f,3.5f,1f), ("dhow",0.88f,11f,1.2f) } },
        };
    }

    // =====================================================================
    //  Level data + world builder
    // =====================================================================

    public class NpcInstance
    {
        public Encounter enc;
        public Rig rig;
        public float x;
        public bool done;
        public TextMesh mark;
        public GameObject root;
    }

    public class LevelData
    {
        public GameObject root;
        public List<NpcInstance> npcs = new List<NpcInstance>();
        public List<GameObject> coins = new List<GameObject>();
        public float gateX;
        public GameObject gateLock;
        public Renderer gateBanner;
        public Theme theme;
        public int ci;
    }

    public static class WorldBuilder
    {
        public const float LEVEL_W = 130f;

        public static LevelData Build(Content content, int ci, Transform camTransform)
        {
            var c = content.countries[ci];
            var th = Themes.All[Mathf.Min(ci, Themes.All.Length - 1)];
            var data = new LevelData { theme = th, ci = ci };
            var root = new GameObject("Level_" + c.id);
            data.root = root;
            var rt = root.transform;

            ApplyAtmosphere(th);
            BuildSky(th, rt, camTransform);

            // ground: walking strip + back plain
            B.Box(rt, new Vector3(LEVEL_W / 2, -0.5f, 0), new Vector3(LEVEL_W + 40, 1, 8), th.ground);
            var back = th.sea ? th.seaColor : th.ground * 0.92f;
            B.Box(rt, new Vector3(LEVEL_W / 2, th.sea ? -0.62f : -0.55f, 22), new Vector3(LEVEL_W + 60, 1, 36), back);

            // far mounds for depth
            for (int i = 0; i < 14; i++)
            {
                float fx = (i + Random.value) * (LEVEL_W + 30) / 14f - 10f;
                float fz = th.sea ? 38f + Random.value * 8f : 16f + Random.value * 20f;
                float fs = 4f + Random.value * 7f;
                B.Sph(rt, new Vector3(fx, -fs * 0.55f, fz), new Vector3(fs * 2.2f, fs, fs), th.far);
            }

            // decor
            foreach (var d in th.decor)
                Decor.Build(d.t, rt, new Vector3(d.fx * LEVEL_W, 0, d.z), d.s, th);

            // coins
            for (int i = 0; i < 10; i++)
            {
                float x = 12f + i * (LEVEL_W - 28f) / 9f + Mathf.PerlinNoise(ci * 9.7f, i * 3.1f) * 3f;
                float y = (i % 3 == 2) ? 2.1f : 0.6f;
                var coin = B.Cyl(rt, new Vector3(x, y, 0), new Vector3(0.45f, 0.05f, 0.45f), Hexc.C("#f3c969"));
                coin.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var spin = new GameObject("coin");
                spin.transform.SetParent(rt, false);
                spin.transform.position = new Vector3(x, y, 0);
                coin.transform.SetParent(spin.transform, true);
                spin.AddComponent<Rotator>();
                spin.AddComponent<Bobber>().amp = 0.08f;
                data.coins.Add(spin);
            }

            // NPCs
            for (int i = 0; i < c.encounters.Length; i++)
            {
                var enc = c.encounters[i];
                bool female = enc.gender == "f";
                var pal = female
                    ? RigBuilder.NpcF[(ci + i) % RigBuilder.NpcF.Length]
                    : RigBuilder.NpcM[(ci * 3 + i) % RigBuilder.NpcM.Length];

                var holder = new GameObject("npc_" + i);
                holder.transform.SetParent(rt, false);
                float x = LEVEL_W * (0.30f + i * 0.22f);
                holder.transform.position = new Vector3(x, 0, 0.6f);
                holder.transform.rotation = Quaternion.Euler(0, -90, 0); // face west (toward arriving player)
                var rig = RigBuilder.Build(holder.transform, pal);

                var name = B.Label(holder.transform, new Vector3(0, 2.05f, 0), Loc.Fix(Loc.T(enc.persona)), 0.045f, Hexc.C("#f7e8c9"));
                var mark = B.Label(holder.transform, new Vector3(0, 2.45f, 0), "!", 0.09f, Hexc.C("#f3c969"));
                mark.gameObject.AddComponent<Bobber>();

                data.npcs.Add(new NpcInstance { enc = enc, rig = rig, x = x, mark = mark, root = holder });
            }

            // gate
            data.gateX = LEVEL_W - 8f;
            BuildGate(content, ci, rt, data);

            return data;
        }

        static void BuildGate(Content content, int ci, Transform rt, LevelData data)
        {
            float gx = data.gateX;
            var th = data.theme;
            Color stone = Hexc.C("#9a7a4a");
            B.Box(rt, new Vector3(gx - 1.4f, 1.6f, 0), new Vector3(0.6f, 3.2f, 0.6f), stone);
            B.Box(rt, new Vector3(gx + 1.4f, 1.6f, 0), new Vector3(0.6f, 3.2f, 0.6f), stone);
            B.Box(rt, new Vector3(gx, 3.4f, 0), new Vector3(3.6f, 0.5f, 0.7f), stone);

            bool last = ci >= content.countries.Length - 1;
            string label = last ? "🌅" : content.countries[ci + 1].flag;
            string nextName = last ? "Ras al-Hadd" : Loc.T(content.countries[ci + 1].name);
            var bannerGo = B.Box(rt, new Vector3(gx, 4.1f, 0), new Vector3(3.4f, 0.8f, 0.2f), Hexc.C("#7a4a4a"));
            data.gateBanner = bannerGo.GetComponent<Renderer>();
            var t1 = B.Label(rt.transform, new Vector3(gx, 4.1f, -0.18f), Loc.Fix(label + " " + nextName), 0.05f, Color.white);
            t1.transform.rotation = Quaternion.identity;

            // lock bar
            data.gateLock = B.Box(rt, new Vector3(gx, 1.1f, 0), new Vector3(3.0f, 0.18f, 0.18f), Hexc.C("#c84a4a"));
        }

        public static void OpenGateVisual(LevelData data)
        {
            if (data.gateLock != null) Object.Destroy(data.gateLock);
            if (data.gateBanner != null) data.gateBanner.sharedMaterial = MatLib.Flat(Hexc.C("#2ec4a0"));
        }

        static void ApplyAtmosphere(Theme th)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 70f;
            RenderSettings.fogColor = th.fog;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(th.skyBottom, Color.white, th.night ? 0.05f : 0.35f)
                                          * (th.night ? 0.55f : 0.95f);

            var sun = GameObject.Find("Sun") ?? new GameObject("Sun");
            var light = sun.GetComponent<Light>() ?? sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = th.night ? Hexc.C("#8a90c0") : (th.sunset ? Hexc.C("#ffc890") : Color.white);
            light.intensity = th.night ? 0.45f : (th.sunset ? 0.85f : 1.05f);
            sun.transform.rotation = Quaternion.Euler(th.sunset ? 18f : 48f, -35f, 0);
        }

        static void BuildSky(Theme th, Transform rt, Transform camT)
        {
            var sky = new GameObject("Sky");
            sky.transform.SetParent(rt, false);
            sky.transform.position = new Vector3(0, -12f, 58f);
            var mf = sky.AddComponent<MeshFilter>();
            mf.mesh = PMesh.GradientQuad(420f, 90f, th.skyTop, th.skyBottom);
            var mr = sky.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MatLib.Unlit(Color.white);
            sky.AddComponent<SkyFollow>().cam = camT;

            // celestial disc
            var disc = B.Sph(sky.transform, new Vector3(18f, th.sunset ? 22f : 38f, -2f),
                Vector3.one * (th.sunset ? 7f : 4.5f), th.sun);
            disc.GetComponent<Renderer>().sharedMaterial = MatLib.Unlit(th.sun);

            if (th.night)
            {
                for (int i = 0; i < 70; i++)
                {
                    float sx = Random.Range(-180f, 180f), sy = Random.Range(26f, 86f);
                    var star = B.Sph(sky.transform, new Vector3(sx, sy, -1f), Vector3.one * Random.Range(0.18f, 0.4f), Color.white);
                    star.GetComponent<Renderer>().sharedMaterial = MatLib.Unlit(Hexc.C("#f0ecff"));
                }
            }
        }
    }

    // =====================================================================
    //  Decor — low-poly landmarks per type
    // =====================================================================

    public static class Decor
    {
        public static GameObject Build(string type, Transform parent, Vector3 pos, float s, Theme th)
        {
            var go = new GameObject("decor_" + type);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * s;
            var t = go.transform;
            Color white = Hexc.C("#efe9da");

            switch (type)
            {
                case "palm":
                {
                    var trunk = B.Cone(t, Vector3.zero, 6, 0.16f, 0.09f, 2.6f, Hexc.C("#7a5a30"));
                    trunk.transform.localRotation = Quaternion.Euler(0, 0, 7f);
                    for (int i = 0; i < 6; i++)
                    {
                        var frond = B.Box(t, new Vector3(0.3f, 2.65f, 0), new Vector3(1.5f, 0.05f, 0.3f), Hexc.C("#3a7a3a"));
                        frond.transform.localRotation = Quaternion.Euler(0, i * 60f, -22f);
                        frond.transform.localPosition += frond.transform.right * 0.6f;
                    }
                    break;
                }
                case "minaret":
                    B.Cyl(t, new Vector3(0, 2.2f, 0), new Vector3(0.9f, 2.2f, 0.9f), Hexc.C("#d8c8a8"));
                    B.Cyl(t, new Vector3(0, 4.45f, 0), new Vector3(1.3f, 0.12f, 1.3f), Hexc.C("#d8c8a8"));
                    B.Cyl(t, new Vector3(0, 5.0f, 0), new Vector3(0.62f, 0.5f, 0.62f), Hexc.C("#d8c8a8"));
                    B.Sph(t, new Vector3(0, 5.7f, 0), Vector3.one * 0.7f, Hexc.C("#2ec4a0"));
                    break;
                case "stall":
                {
                    B.Box(t, new Vector3(-0.8f, 0.7f, 0), new Vector3(0.1f, 1.4f, 0.1f), Hexc.C("#8a5a30"));
                    B.Box(t, new Vector3(0.8f, 0.7f, 0), new Vector3(0.1f, 1.4f, 0.1f), Hexc.C("#8a5a30"));
                    var roof = B.Box(t, new Vector3(0, 1.5f, 0), new Vector3(2.1f, 0.07f, 1.4f), Hexc.C("#c84a4a"));
                    roof.transform.localRotation = Quaternion.Euler(8f, 0, 0);
                    B.Box(t, new Vector3(-0.3f, 0.3f, 0), new Vector3(0.6f, 0.6f, 0.6f), Hexc.C("#a8763a"));
                    B.Box(t, new Vector3(0.45f, 0.22f, 0.1f), new Vector3(0.45f, 0.45f, 0.45f), Hexc.C("#caa84a"));
                    break;
                }
                case "houseTN":
                    B.Box(t, new Vector3(0, 0.9f, 0), new Vector3(2.4f, 1.8f, 2.2f), Hexc.C("#f2efe6"));
                    B.Sph(t, new Vector3(0, 1.85f, 0), new Vector3(1.5f, 1.0f, 1.5f), Hexc.C("#f2efe6"));
                    B.Box(t, new Vector3(0, 0.55f, -1.12f), new Vector3(0.6f, 1.1f, 0.1f), Hexc.C("#2a6ac8"));
                    B.Box(t, new Vector3(-0.8f, 1.2f, -1.12f), new Vector3(0.4f, 0.4f, 0.08f), Hexc.C("#2a6ac8"));
                    B.Box(t, new Vector3(0.8f, 1.2f, -1.12f), new Vector3(0.4f, 0.4f, 0.08f), Hexc.C("#2a6ac8"));
                    break;
                case "houseWhite":
                    B.Box(t, new Vector3(0, 0.85f, 0), new Vector3(2.2f, 1.7f, 2.0f), white);
                    B.Box(t, new Vector3(0, 0.5f, -1.02f), new Vector3(0.55f, 1.0f, 0.1f), Hexc.C("#b89a6a"));
                    B.Box(t, new Vector3(-0.7f, 1.15f, -1.02f), new Vector3(0.35f, 0.4f, 0.08f), Hexc.C("#7a98b8"));
                    B.Box(t, new Vector3(0.7f, 1.15f, -1.02f), new Vector3(0.35f, 0.4f, 0.08f), Hexc.C("#7a98b8"));
                    break;
                case "pyramid":
                    B.Cone(t, Vector3.zero, 4, 4.6f, 0f, 5.6f, Hexc.C("#d8b060"))
                        .transform.localRotation = Quaternion.Euler(0, 45f, 0);
                    break;
                case "obelisk":
                    B.Cone(t, Vector3.zero, 4, 0.42f, 0.2f, 3.4f, Hexc.C("#c8a868"));
                    B.Cone(t, new Vector3(0, 3.4f, 0), 4, 0.22f, 0f, 0.5f, Hexc.C("#c8a868"));
                    break;
                case "columns":
                {
                    B.Box(t, new Vector3(0, 2.85f, 0), new Vector3(3.4f, 0.3f, 0.8f), Hexc.C("#e2d6b8"));
                    for (int i = 0; i < 4; i++)
                    {
                        float cx = -1.3f + i * 0.87f;
                        B.Cyl(t, new Vector3(cx, 1.4f, 0), new Vector3(0.34f, 1.4f, 0.34f), Hexc.C("#e2d6b8"));
                        B.Box(t, new Vector3(cx, 2.65f, 0), new Vector3(0.5f, 0.16f, 0.5f), Hexc.C("#e2d6b8"));
                    }
                    break;
                }
                case "petra":
                {
                    Color wall = Hexc.C("#8a3a44"), face = Hexc.C("#c87a6a"), deep = Hexc.C("#a85a4a");
                    B.Sph(t, new Vector3(0, 2.5f, 1.6f), new Vector3(11f, 9f, 5f), wall);
                    B.Box(t, new Vector3(0, 2.4f, -0.4f), new Vector3(4.4f, 4.8f, 0.6f), face);
                    for (int i = 0; i < 4; i++)
                        B.Cyl(t, new Vector3(-1.65f + i * 1.1f, 2.1f, -0.78f), new Vector3(0.3f, 2.1f, 0.3f), deep);
                    B.Cone(t, new Vector3(0, 4.8f, -0.55f), 3, 2.6f, 0f, 1.1f, deep)
                        .transform.localRotation = Quaternion.Euler(0, 90f, 0);
                    B.Box(t, new Vector3(0, 0.8f, -0.82f), new Vector3(1.1f, 1.6f, 0.2f), Hexc.C("#3a1a1e"));
                    break;
                }
                case "rock":
                    B.Sph(t, new Vector3(0, 0.8f, 0), new Vector3(2.6f, 2.4f, 2.2f), Hexc.C("#a8504a"));
                    B.Sph(t, new Vector3(1.2f, 0.45f, 0.6f), new Vector3(1.4f, 1.2f, 1.3f), Hexc.C("#985046"));
                    break;
                case "cedar":
                {
                    B.Cyl(t, new Vector3(0, 0.6f, 0), new Vector3(0.3f, 0.6f, 0.3f), Hexc.C("#6a4a2a"));
                    for (int i = 0; i < 4; i++)
                        B.Cone(t, new Vector3(0, 1.0f + i * 0.55f, 0), 8, 1.5f - i * 0.3f, 0.05f, 0.5f, Hexc.C("#2a5a34"));
                    break;
                }
                case "dune":
                    B.Sph(t, new Vector3(0, -0.7f, 0), new Vector3(7f, 2.6f, 4f), th.ground * 1.12f);
                    break;
                case "tent":
                    B.Cone(t, Vector3.zero, 4, 2.2f, 0f, 1.9f, Hexc.C("#5a4030"));
                    B.Box(t, new Vector3(0, 0.45f, -1.0f), new Vector3(0.5f, 0.9f, 0.2f), Hexc.C("#f3c969"));
                    break;
                case "skyscraper":
                {
                    float hgt = 7f;
                    B.Box(t, new Vector3(0, hgt / 2, 0), new Vector3(1.8f, hgt, 1.8f), Hexc.C("#3a4a6a"));
                    for (int f = 0; f < 5; f++)
                        B.Box(t, new Vector3(0, 1f + f * 1.3f, -0.92f), new Vector3(1.2f, 0.5f, 0.05f), Hexc.C("#ffd878"));
                    B.Box(t, new Vector3(0, hgt + 0.5f, 0), new Vector3(0.12f, 1f, 0.12f), Hexc.C("#3a4a6a"));
                    break;
                }
                case "burj":
                {
                    B.Cone(t, Vector3.zero, 6, 1.6f, 0.7f, 6f, Hexc.C("#46587a"));
                    B.Cone(t, new Vector3(0, 6f, 0), 6, 0.7f, 0.25f, 4f, Hexc.C("#46587a"));
                    B.Cone(t, new Vector3(0, 10f, 0), 6, 0.25f, 0.02f, 3f, Hexc.C("#46587a"));
                    break;
                }
                case "fort":
                {
                    Color sand = Hexc.C("#c8a878");
                    B.Box(t, new Vector3(0, 1.1f, 0), new Vector3(5.4f, 2.2f, 2.4f), sand);
                    B.Cyl(t, new Vector3(-3f, 1.7f, 0), new Vector3(1.3f, 1.7f, 1.3f), sand);
                    B.Cyl(t, new Vector3(3f, 1.7f, 0), new Vector3(1.3f, 1.7f, 1.3f), sand);
                    for (int i = 0; i < 6; i++)
                        B.Box(t, new Vector3(-2.2f + i * 0.9f, 2.4f, -1.1f), new Vector3(0.4f, 0.35f, 0.2f), sand);
                    B.Box(t, new Vector3(0, 0.7f, -1.22f), new Vector3(1.0f, 1.4f, 0.2f), Hexc.C("#6a4a2a"));
                    break;
                }
                case "dhow":
                {
                    var hull = B.Sph(t, new Vector3(0, 0.1f, 0), new Vector3(3.4f, 0.9f, 1.1f), Hexc.C("#6a4a2a"));
                    hull.transform.localRotation = Quaternion.Euler(0, 0, 4f);
                    B.Cyl(t, new Vector3(0.2f, 1.4f, 0), new Vector3(0.08f, 1.3f, 0.08f), Hexc.C("#4a3420"));
                    var sailGo = new GameObject("sail");
                    sailGo.transform.SetParent(t, false);
                    sailGo.transform.localPosition = new Vector3(0.25f, 0.9f, 0);
                    sailGo.transform.localRotation = Quaternion.Euler(0, -90, 0);
                    sailGo.transform.localScale = Vector3.one * 1.4f;
                    sailGo.AddComponent<MeshFilter>().mesh = PMesh.Sail();
                    sailGo.AddComponent<MeshRenderer>().sharedMaterial = MatLib.Flat(Hexc.C("#f0ead8"));
                    go.AddComponent<Bobber>().amp = 0.07f;
                    break;
                }
                case "incense":
                {
                    B.Cone(t, Vector3.zero, 6, 0.5f, 0.3f, 0.5f, Hexc.C("#8a5a30"));
                    B.Sph(t, new Vector3(0, 0.6f, 0), Vector3.one * 0.25f, Hexc.C("#e8a23a"));
                    var ps = new GameObject("smoke").AddComponent<ParticleSystem>();
                    ps.transform.SetParent(t, false);
                    ps.transform.localPosition = new Vector3(0, 0.7f, 0);
                    var main = ps.main;
                    main.startSize = 0.25f;
                    main.startSpeed = 0.55f;
                    main.startLifetime = 3.2f;
                    main.startColor = new Color(0.88f, 0.88f, 0.92f, 0.35f);
                    main.maxParticles = 40;
                    var em = ps.emission; em.rateOverTime = 7f;
                    var shape = ps.shape; shape.angle = 8f; shape.radius = 0.05f;
                    var r = ps.GetComponent<ParticleSystemRenderer>();
                    r.material = MatLib.Unlit(new Color(1, 1, 1, 0.3f));
                    break;
                }
                case "train":
                {
                    Color loco = Hexc.C("#7a3a2a"), car = Hexc.C("#6a5242"), wheel = Hexc.C("#3a2a22");
                    B.Box(t, new Vector3(2.5f, 0.02f, 0), new Vector3(14f, 0.08f, 0.5f), Hexc.C("#5a4a3a"));
                    B.Box(t, new Vector3(-2f, 0.75f, 0), new Vector3(1.8f, 1.2f, 1.0f), loco);
                    B.Box(t, new Vector3(-2.6f, 1.55f, 0), new Vector3(0.6f, 0.5f, 0.9f), loco);
                    B.Cyl(t, new Vector3(-2.4f, 0.2f, 0.5f), new Vector3(0.42f, 0.06f, 0.42f), wheel)
                        .transform.localRotation = Quaternion.Euler(90, 0, 0);
                    for (int i = 0; i < 4; i++)
                    {
                        float cx = 0.2f + i * 2.1f;
                        B.Box(t, new Vector3(cx, 0.6f, 0), new Vector3(1.9f, 0.85f, 0.95f), car);
                        B.Sph(t, new Vector3(cx, 1.05f, 0), new Vector3(1.7f, 0.35f, 0.8f), Hexc.C("#4a3828"));
                    }
                    break;
                }
            }
            return go;
        }
    }
}
