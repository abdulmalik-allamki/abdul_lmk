# ✨ Caravan of Wonders — رحلة عبر بلاد العرب

An interactive **trivia adventure across the Arab world**. You join a caravan
traveling from the gates of Marrakesh to the harbor of Muscat. Along the way you
cross **8 Arab countries**, and in each one the trivia questions are woven into
an adventure story — storytellers, fishermen, Bedouin riders, and pearl divers
challenge you with riddles about their lands.

## 🎮 How to Play

Just open **`index.html`** in any web browser — no installation, no build step,
no internet connection required.

```bash
# or serve it locally if you prefer:
python3 -m http.server 8000
# then visit http://localhost:8000
```

## 🧭 Choose Your Traveler

Each character has a unique perk that changes how you play:

| Character | Perk |
|---|---|
| 🧭 **Rashid the Explorer** | Starts with 4 hearts instead of 3 |
| 📜 **Layla the Scholar** | Carries 3 hints instead of 1 |
| 🪙 **Omar the Merchant** | Earns double gold for every correct answer |
| 🦅 **Zahra the Falconer** | Her falcon saves her once from losing her last heart |

## 🗺️ The Journey

🇲🇦 Morocco → 🇹🇳 Tunisia → 🇪🇬 Egypt → 🇯🇴 Jordan → 🇱🇧 Lebanon → 🇸🇦 Saudi Arabia → 🇦🇪 UAE → 🇴🇲 Oman

- Each country opens with a **story scene**, followed by **3 encounters** —
  trivia questions framed as moments in the adventure.
- ✅ Correct answers earn **gold** (with a 🔥 streak bonus from 3 in a row)
  and teach you a **fun fact**.
- 💔 Wrong answers cost a **heart**. Lose all hearts and the desert claims
  you — but you can rest and retry the land where you fell.
- 🔮 **Hints** remove two wrong answers from a question.
- Reach Muscat and earn a **rank** based on how many riddles you solved.

## 🛠️ Adding Your Own Questions

Everything lives in one file. Open `index.html` and find the `COUNTRIES` array
in the `<script>` section. Each country looks like this:

```js
{
  flag: "🇲🇦", name: "Morocco", arabic: "المغرب",
  scene: "Story text shown when the caravan arrives...",
  encounters: [
    {
      scene: "Adventure framing for the question...",
      question: "The trivia question?",
      options: ["Correct answer", "Wrong", "Wrong", "Wrong"],
      answer: 0,            // index of the correct option
      fact: "Fun fact shown after answering.",
    },
    // ...as many encounters per country as you like
  ],
},
```

Add encounters, add countries, or reorder the route — the journey map, the
progress counter, and the final score all adapt automatically.
