# Caravan of Wonders — Unity 3D Edition · قافلة العجائب

The publishable 3D version of the game: a stylized low-poly journey across the
Arab world, from **Nouadhibou (Mauritania)** in the far west to **Sur (Oman)**
in the far east — fully bilingual **Arabic / English**.

## ▶️ Running the project

1. Install **Unity 2022.3 LTS** (any 2022.3.x via Unity Hub).
2. In Unity Hub: **Add → Add project from disk** → select `unity/CaravanOfWonders`.
3. Open the project, create/open any empty scene, and press **Play**.

There is intentionally **no scene setup**: the whole game bootstraps itself at
runtime (`Boot.cs` uses `RuntimeInitializeOnLoadMethod`), building the world,
characters, UI, and camera procedurally. This keeps the entire game reviewable
as plain C# — no opaque binary scene files.

### Controls

| Action | Input |
|---|---|
| Walk | ← → or A / D |
| Jump | Space / W / ↑ |
| Talk / interact | E or Enter |
| Answer | click (1–4 buttons) |
| Switch language | العربية / English button (top-right & menu) |

## 🎬 What replaced the text descriptions

Scene descriptions are no longer typed or narrated — they are **performed as
3D vignettes** next to the dialogue:

- `tea_pour` — the tea-maker raises the pot high and pours a golden stream,
  foam particles rising from the glass (the example you asked for).
- `train_pass` — the Mauritanian ore train rolls past in the background, dust
  trailing, while the driver points at it.
- `show_item` — manuscripts, frankincense, coffee cups… raised, turned and
  glinting in the speaker's hand.
- `point_far` — the speaker gestures at a far landmark marked by a soft beacon.
- `generic_talk` — natural talking gestures.

Each encounter's vignette is assigned in `content.json` (`"vignette"` field),
so adding new staged animations is one enum value + one coroutine in
`VignettePlayer.cs`.

## 🎥 The smooth 2D → 3D transition

Travel is framed like a flat side-scroller — a distant camera with a narrow
field of view. The moment you talk to a character, `CameraDirector` swings the
camera around (~42°), dollies in, and widens the FOV into a cinematic
over-the-shoulder 3D shot with a slow drift — then glides back to the flat
side view when the conversation ends. All motion uses exponential damping, so
the transition is seamless from any starting state.

## 🌍 Localization

- All content lives in `Assets/Resources/content.json` — every string carries
  `en` and `ar`. The Arabic is professionally written MSA (فصحى).
- `ArabicShaper.cs` performs dependency-free Arabic glyph shaping
  (presentation forms + lam-alef ligatures) and RTL visual reordering, since
  Unity's legacy Text doesn't shape Arabic.
- **For production**, consider swapping the text pipeline to
  [RTLTMPro](https://github.com/pnarimani/RTLTMPro) (TextMeshPro-based) for
  perfect paragraph wrapping, and ship a beautiful Arabic font (e.g. Cairo,
  Amiri, or Noto Naskh Arabic).

## 🎙️ Character voices (ElevenLabs)

Voices are **pre-baked, not synthesized at runtime** — the professional
pipeline:

```bash
export ELEVEN_API_KEY=your_key_here
python3 tools/generate_voices.py --dry-run   # preview the ~270 lines
python3 tools/generate_voices.py             # generate EN + AR audio
```

Clips land in `Assets/Resources/Voice/{lineId}_{en|ar}.mp3` and the game picks
them up automatically (`VoicePlayer.cs`); without them the game simply plays
silently. Voice casting per persona (elders deep and slow, youths lively,
female personas female voices) is configured at the top of
`tools/generate_voices.py` — swap in any voices from the ElevenLabs library.
The full script costs well under $5 to generate.

## 🗂️ Code map

```
Assets/Scripts/
  Core.cs         Boot, content models, localization, Arabic shaper, voice playback
  World.cs        themes, procedural meshes, decor (Petra, pyramids, train…), character rigs
  Game.cs         GameManager (state, scoring, dialogue flow), CameraDirector (2D→3D), sound
  UIVignettes.cs  VignettePlayer (staged 3D scene animations), GameUI (bilingual uGUI)
Assets/Resources/
  content.json    the single source of truth — all bilingual game content
  Voice/          generated voice clips (not committed until you run the tool)
tools/
  generate_voices.py  ElevenLabs batch generation
```

## 🚀 Path to publishing

- **Web build**: WebGL target works out of the box (procedural everything, no
  heavy assets) — publishable on itch.io or your own site.
- **Desktop/mobile stores**: add an app icon, splash, and platform settings.
- **Art upgrades**: the rig/decor builders can be swapped piecemeal for real
  models (e.g. Mixamo characters with pour/point/talk animation clips) without
  touching game logic — `RigBuilder.Build` and `Decor.Build` are the only seams.
- The original 2D web prototype remains at the repo root (`index.html`).
