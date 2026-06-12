using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Caravan
{
    /// <summary>
    /// Entry point — no scene setup required. Open any scene and press Play;
    /// the whole game constructs itself procedurally.
    /// </summary>
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (UnityEngine.Object.FindObjectOfType<GameManager>() != null) return;
            var go = new GameObject("CaravanGame");
            go.AddComponent<GameManager>();
        }
    }

    // =====================================================================
    //  Content models — these mirror Assets/Resources/content.json exactly.
    // =====================================================================

    [Serializable] public class LocText { public string en; public string ar; }

    [Serializable]
    public class UIStrings
    {
        public LocText title, subtitle, tagline, chooseTraveler, begin, walkEast, talk;
        public LocText @continue, hint, correct, streakBonus, wrong, falconSave;
        public LocText gateOpen, gateOpenFinal, gateLocked;
        public LocText gameOverTitle, gameOverBody, retry, newJourney;
        public LocText victoryTitle, victoryBody, goldEarned, riddlesSolved, bestStreak;
        public LocText singWill, landsCrossed, language, introBody;
    }

    [Serializable]
    public class CharacterDef
    {
        public string id;
        public LocText name, title, perk;
        public int hearts, hints, goldMult;
        public bool falconSave;
    }

    [Serializable]
    public class Encounter
    {
        public LocText persona;
        public string gender;     // "m" | "f"
        public bool elder;
        public string vignette;   // tea_pour | train_pass | show_item | point_far | generic_talk
        public LocText say, question;
        public LocText[] options;
        public int answer;
        public LocText fact;
    }

    [Serializable]
    public class Country
    {
        public string id, flag;
        public LocText name, scene;
        public Encounter[] encounters;
    }

    [Serializable] public class RankDef { public float min; public LocText name, line; }

    [Serializable]
    public class Content
    {
        public UIStrings ui;
        public CharacterDef[] characters;
        public Country[] countries;
        public RankDef[] ranks;

        public static Content Load()
        {
            var ta = Resources.Load<TextAsset>("content");
            if (ta == null) { Debug.LogError("content.json missing from Resources"); return null; }
            return JsonUtility.FromJson<Content>(ta.text);
        }
    }

    // =====================================================================
    //  Localization
    // =====================================================================

    public static class Loc
    {
        public static bool Ar = false;

        public static string T(LocText t) => t == null ? "" : (Ar ? t.ar : t.en);
        public static string F(LocText t, params object[] args) => string.Format(T(t), args);

        /// <summary>Prepare a string for rendering in a Unity Text/TextMesh
        /// (Arabic needs glyph shaping + visual reordering).</summary>
        public static string Fix(string s) => Ar ? ArabicShaper.Shape(s) : s;
    }

    // =====================================================================
    //  Arabic shaper — converts logical Arabic text into presentation forms
    //  (Unicode FB50–FEFF) and reverses it into visual order so Unity's
    //  LTR text components render it correctly. Handles lam-alef ligatures,
    //  transparent diacritics, embedded Latin/digit runs and mirrored
    //  brackets. For production-grade paragraph wrapping consider RTLTMPro;
    //  this implementation is dependency-free and covers game UI well.
    // =====================================================================

    public static class ArabicShaper
    {
        // forms: isolated, final, initial, medial (0 = form doesn't exist)
        static readonly Dictionary<char, int[]> Forms = new Dictionary<char, int[]>
        {
            {'ء', new[]{0xFE80, 0,      0,      0     }}, // ء
            {'آ', new[]{0xFE81, 0xFE82, 0,      0     }}, // آ
            {'أ', new[]{0xFE83, 0xFE84, 0,      0     }}, // أ
            {'ؤ', new[]{0xFE85, 0xFE86, 0,      0     }}, // ؤ
            {'إ', new[]{0xFE87, 0xFE88, 0,      0     }}, // إ
            {'ئ', new[]{0xFE89, 0xFE8A, 0xFE8B, 0xFE8C}}, // ئ
            {'ا', new[]{0xFE8D, 0xFE8E, 0,      0     }}, // ا
            {'ب', new[]{0xFE8F, 0xFE90, 0xFE91, 0xFE92}}, // ب
            {'ة', new[]{0xFE93, 0xFE94, 0,      0     }}, // ة
            {'ت', new[]{0xFE95, 0xFE96, 0xFE97, 0xFE98}}, // ت
            {'ث', new[]{0xFE99, 0xFE9A, 0xFE9B, 0xFE9C}}, // ث
            {'ج', new[]{0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0}}, // ج
            {'ح', new[]{0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4}}, // ح
            {'خ', new[]{0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8}}, // خ
            {'د', new[]{0xFEA9, 0xFEAA, 0,      0     }}, // د
            {'ذ', new[]{0xFEAB, 0xFEAC, 0,      0     }}, // ذ
            {'ر', new[]{0xFEAD, 0xFEAE, 0,      0     }}, // ر
            {'ز', new[]{0xFEAF, 0xFEB0, 0,      0     }}, // ز
            {'س', new[]{0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4}}, // س
            {'ش', new[]{0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8}}, // ش
            {'ص', new[]{0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC}}, // ص
            {'ض', new[]{0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0}}, // ض
            {'ط', new[]{0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4}}, // ط
            {'ظ', new[]{0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8}}, // ظ
            {'ع', new[]{0xFEC9, 0xFECA, 0xFECB, 0xFECC}}, // ع
            {'غ', new[]{0xFECD, 0xFECE, 0xFECF, 0xFED0}}, // غ
            {'ف', new[]{0xFED1, 0xFED2, 0xFED3, 0xFED4}}, // ف
            {'ق', new[]{0xFED5, 0xFED6, 0xFED7, 0xFED8}}, // ق
            {'ك', new[]{0xFED9, 0xFEDA, 0xFEDB, 0xFEDC}}, // ك
            {'ل', new[]{0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0}}, // ل
            {'م', new[]{0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4}}, // م
            {'ن', new[]{0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8}}, // ن
            {'ه', new[]{0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC}}, // ه
            {'و', new[]{0xFEED, 0xFEEE, 0,      0     }}, // و
            {'ى', new[]{0xFEEF, 0xFEF0, 0,      0     }}, // ى
            {'ي', new[]{0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4}}, // ي
        };

        // lam-alef ligatures: alef variant -> {isolated, final}
        static readonly Dictionary<char, int[]> LamAlef = new Dictionary<char, int[]>
        {
            {'آ', new[]{0xFEF5, 0xFEF6}}, // لآ
            {'أ', new[]{0xFEF7, 0xFEF8}}, // لأ
            {'إ', new[]{0xFEF9, 0xFEFA}}, // لإ
            {'ا', new[]{0xFEFB, 0xFEFC}}, // لا
        };

        static bool IsTransparent(char c) =>
            (c >= 'ً' && c <= 'ٟ') || c == 'ٰ'; // harakat etc.

        static bool IsArabicLetter(char c) => Forms.ContainsKey(c);

        // joins forward (to the following letter) = has an initial form
        static bool JoinsForward(char c) => Forms.TryGetValue(c, out var f) && f[2] != 0;

        // joins backward (to the preceding letter) = has a final form
        static bool JoinsBackward(char c) => Forms.TryGetValue(c, out var f) && f[1] != 0;

        public static string Shape(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var shaped = ShapeForms(input);
            return ReorderVisual(shaped);
        }

        static string ShapeForms(string s)
        {
            var sb = new StringBuilder(s.Length);
            int n = s.Length;
            for (int i = 0; i < n; i++)
            {
                char c = s[i];
                if (!IsArabicLetter(c)) { sb.Append(c); continue; }

                // previous non-transparent char
                int pi = i - 1;
                while (pi >= 0 && IsTransparent(s[pi])) pi--;
                bool prevLinks = pi >= 0 && IsArabicLetter(s[pi]) && JoinsForward(s[pi]);

                // lam-alef ligature
                if (c == 'ل')
                {
                    int ni0 = i + 1;
                    while (ni0 < n && IsTransparent(s[ni0])) ni0++;
                    if (ni0 < n && LamAlef.TryGetValue(s[ni0], out var lig))
                    {
                        sb.Append((char)(prevLinks ? lig[1] : lig[0]));
                        // keep any diacritics that sat between lam and alef
                        for (int d = i + 1; d < ni0; d++) sb.Append(s[d]);
                        i = ni0;
                        continue;
                    }
                }

                // next non-transparent char
                int ni = i + 1;
                while (ni < n && IsTransparent(s[ni])) ni++;
                bool nextLinks = ni < n && IsArabicLetter(s[ni]) && JoinsBackward(s[ni]);

                var f = Forms[c];
                int form;
                if (prevLinks && nextLinks) form = f[3] != 0 ? f[3] : f[1];
                else if (prevLinks)         form = f[1] != 0 ? f[1] : f[0];
                else if (nextLinks)         form = f[2] != 0 ? f[2] : f[0];
                else                        form = f[0];
                sb.Append((char)form);
            }
            return sb.ToString();
        }

        static bool IsLtrChar(char c) =>
            (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
            (c >= '٠' && c <= '٩'); // Arabic-Indic digits keep LTR order too

        static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')'; case ')': return '(';
                case '[': return ']'; case ']': return '[';
                case '{': return '}'; case '}': return '{';
                case '<': return '>'; case '>': return '<';
                default: return c;
            }
        }

        /// <summary>Reverse into visual order for an LTR renderer while
        /// keeping Latin/digit runs readable and mirroring brackets.</summary>
        static string ReorderVisual(string s)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                if (IsLtrChar(s[i]))
                {
                    int j = i;
                    while (j < s.Length && (IsLtrChar(s[j]) || (s[j] == '.' && j + 1 < s.Length && IsLtrChar(s[j + 1])))) j++;
                    tokens.Add(s.Substring(i, j - i));
                    i = j;
                }
                else
                {
                    tokens.Add(Mirror(s[i]).ToString());
                    i++;
                }
            }
            tokens.Reverse();
            var sb = new StringBuilder(s.Length);
            foreach (var t in tokens) sb.Append(t);
            return sb.ToString();
        }
    }

    // =====================================================================
    //  Voice playback — plays pre-baked clips generated by
    //  tools/generate_voices.py (ElevenLabs). Looks for
    //  Resources/Voice/{lineId}_{en|ar}. Silent if the clip is absent,
    //  so the game works before audio has been generated.
    // =====================================================================

    public class VoicePlayer : MonoBehaviour
    {
        AudioSource src;

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.playOnAwake = false;
        }

        /// <returns>clip length in seconds, or 0 when no clip exists</returns>
        public float Play(string lineId)
        {
            Stop();
            var clip = Resources.Load<AudioClip>("Voice/" + lineId + (Loc.Ar ? "_ar" : "_en"));
            if (clip == null) return 0f;
            src.clip = clip;
            src.Play();
            return clip.length;
        }

        public void Stop()
        {
            if (src != null && src.isPlaying) src.Stop();
        }

        public bool IsPlaying => src != null && src.isPlaying;
    }
}
