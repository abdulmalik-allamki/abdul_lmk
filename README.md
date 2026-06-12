# ✨ Caravan of Wonders — من أقصى الغرب إلى أقصى الشرق

> **🎮 The publishable 3D edition now lives in [`unity/`](unity/README.md)** —
> a bilingual (Arabic/English) Unity project with stylized low-poly 3D, smooth
> 2D→3D camera transitions, animated scene vignettes (the tea-maker really
> pours!), and an ElevenLabs voice pipeline. This page describes the original
> 2D browser prototype (`index.html`), which remains fully playable.

A **2D trivia adventure across the Arab world** — from the farthest city in the
west to the farthest city in the east. Your traveler walks through a living 2D
world: from **Nouadhibou, Mauritania**, where the Sahara meets the Atlantic, all
the way to **Sur, Oman**, where the sun rises on the Arab world before anywhere
else. Along the road you cross **11 countries**, each with its own scenery,
people, and riddles woven into the adventure.

## 🎮 How to Play

Just open **`index.html`** in any modern web browser — no installation, no build
step, no internet connection required.

```bash
# or serve it locally if you prefer:
python3 -m http.server 8000
# then visit http://localhost:8000
```

### Controls

| Action | Keys | Touch |
|---|---|---|
| Walk | ← → or A D | ◀ ▶ buttons |
| Jump (grab high coins!) | ↑, W, or Space | ⤒ button |
| Talk to a character | E or Enter | 💬 button |
| Answer a question | 1–4 or click | tap |

## 🧭 Choose Your Traveler

Each character has a unique perk that changes how you play:

| Character | Perk |
|---|---|
| 🧭 **Rashid the Explorer** | Starts with 4 hearts instead of 3 |
| 📜 **Layla the Scholar** | Carries 3 hints instead of 1 |
| 🪙 **Omar the Merchant** | Earns double gold for every correct answer and coin |
| 🦅 **Zahra the Falconer** | Her falcon (it flies beside her!) saves her once from losing her last heart |

## 🗺️ The Journey — West to East

🇲🇷 Mauritania → 🇲🇦 Morocco → 🇩🇿 Algeria → 🇹🇳 Tunisia → 🇱🇾 Libya → 🇪🇬 Egypt → 🇯🇴 Jordan → 🇱🇧 Lebanon → 🇸🇦 Saudi Arabia → 🇦🇪 UAE → 🇴🇲 Oman

Each country is a walkable 2D level with its own scenery — the iron-ore train of
Mauritania, sunset over Marrakesh, the white houses of Algiers and Sidi Bou Said,
the Roman columns of Leptis Magna, the Pyramids, Petra carved in rose-red rock,
the cedars of Lebanon, starry night dunes in Arabia, the Burj Khalifa skyline,
and the dhow harbor of Sur at dawn.

- 💬 **Three characters** wait in each land — walk up and press **E** to hear
  their story and answer their riddle.
- ✅ Correct answers earn **gold** (with a 🔥 streak bonus from 3 in a row) and
  teach you a **fun fact**.
- 💔 Wrong answers cost a **heart**. Lose all hearts and the desert claims you —
  but you can rest and retry the land where you fell.
- 🪙 **Coins** are scattered along the road — jump to reach the high ones.
- 🔮 **Hints** remove two wrong answers from a question.
- 🚪 Answer all three riddles to **open the gate** to the next country, then
  walk east through it.
- 🌅 Reach **Ras al-Hadd** beyond Sur and earn a rank based on how many riddles
  you solved.

## 🛠️ Adding Your Own Questions & Countries

Everything lives in one file. Open `index.html` and find the `COUNTRIES` array
in the `<script>` section. Each country looks like this:

```js
{
  flag: "🇲🇦", name: "Morocco", arabic: "المغرب",
  scene: "Story text shown when the caravan arrives...",
  theme: {
    sky: ["#topColor", "#bottomColor"],          // sky gradient
    celestial: { type: "sun", x: 700, y: 120, r: 38 }, // or "moon", stars: true
    far: { type: "mountains", color: "#4a2c5e" },     // dunes | mountains | canyon | pyramids | skyline
    sea: "#2a78b8",                               // water color, or null for inland
    ground: "#c08858", groundDark: "#96693f",
    decor: [["minaret", 0.16], ["palm", 0.44]],   // [type, position 0..1]
  },
  encounters: [
    {
      persona: "Storyteller",                     // who you meet in the world
      scene: "Adventure framing for the question...",
      question: "The trivia question?",
      options: ["Correct answer", "Wrong", "Wrong", "Wrong"],
      answer: 0,                                  // index of the correct option
      fact: "Fun fact shown after answering.",
    },
  ],
},
```

Decor types available: `palm`, `minaret`, `stall`, `houseTN`, `houseWhite`,
`pyramid`, `obelisk`, `petra`, `rock`, `cedar`, `dune`, `tent`, `skyscraper`,
`fort`, `dhow`, `incense`, `train`. The journey map, gates, and final score all
adapt automatically when you add countries or encounters.
