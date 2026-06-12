#!/usr/bin/env python3
"""
Generate character voice lines for Caravan of Wonders with ElevenLabs.

Reads the bilingual game script from
  unity/CaravanOfWonders/Assets/Resources/content.json
and writes one MP3 per line per language to
  unity/CaravanOfWonders/Assets/Resources/Voice/{lineId}_{en|ar}.mp3

The game (VoicePlayer.cs) picks these up automatically — no code changes
needed after generating. The script is idempotent: existing files are
skipped, so you can re-run it after editing content.

Usage:
  export ELEVEN_API_KEY=...        # your ElevenLabs API key
  python3 tools/generate_voices.py            # generate everything
  python3 tools/generate_voices.py --lang ar  # one language only
  python3 tools/generate_voices.py --dry-run  # list lines without calling the API

Cost note: the whole script (~270 lines, EN+AR) is roughly 40k characters —
well within ElevenLabs' starter tier.
"""

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT = os.path.join(ROOT, "unity/CaravanOfWonders/Assets/Resources/content.json")
OUT_DIR = os.path.join(ROOT, "unity/CaravanOfWonders/Assets/Resources/Voice")

API = "https://api.elevenlabs.io/v1/text-to-speech/{voice_id}"
MODEL = "eleven_multilingual_v2"  # handles Arabic and English

# ---------------------------------------------------------------------------
# Voice casting — ElevenLabs premade voice IDs. Replace freely with your own
# (see https://elevenlabs.io/app/voice-library). Categories are chosen per
# persona from the gender/elder flags in content.json.
# ---------------------------------------------------------------------------
VOICES = {
    "narrator":  "pNInz6obpgDQGcFmaJgB",   # Adam — warm storyteller
    "elder":    ["VR6AewLTigWG4xSOukaG",   # Arnold — deep, weathered
                 "TxGEqnHWrfWFTfGW9XjX"],  # Josh — low, calm
    "male":     ["ErXwobaYiN019PkySvjV",   # Antoni
                 "yoZ06aMxZJJ28mfd3POQ",   # Sam
                 "pNInz6obpgDQGcFmaJgB"],  # Adam
    "female":   ["21m00Tcm4TlvDq8ikWAM",   # Rachel
                 "EXAVITQu4vr4xnSDxMaL"],  # Bella
}

# voice_settings tweaks per category: elders slower/steadier, youths livelier
SETTINGS = {
    "narrator": {"stability": 0.55, "similarity_boost": 0.8, "style": 0.35},
    "elder":    {"stability": 0.7,  "similarity_boost": 0.8, "style": 0.25},
    "male":     {"stability": 0.5,  "similarity_boost": 0.8, "style": 0.4},
    "female":   {"stability": 0.45, "similarity_boost": 0.8, "style": 0.5},
}


def pick(category: str, seed: int) -> str:
    v = VOICES[category]
    return v if isinstance(v, str) else v[seed % len(v)]


def collect_lines(content: dict):
    """Yield (line_id, category, seed, {'en': text, 'ar': text})."""
    for ci, country in enumerate(content["countries"]):
        yield (f"c{ci:02d}_scene", "narrator", 0, country["scene"])
        for ei, enc in enumerate(country["encounters"]):
            cat = ("female" if enc["gender"] == "f"
                   else "elder" if enc.get("elder") else "male")
            seed = sum(ord(ch) for ch in enc["persona"]["en"])
            for part in ("say", "question", "fact"):
                key = {"say": "say", "question": "q", "fact": "fact"}[part]
                yield (f"c{ci:02d}_e{ei}_{key}", cat, seed, enc[part])


def tts(api_key: str, voice_id: str, text: str, settings: dict) -> bytes:
    body = json.dumps({
        "text": text,
        "model_id": MODEL,
        "voice_settings": settings,
    }).encode("utf-8")
    req = urllib.request.Request(
        API.format(voice_id=voice_id),
        data=body,
        headers={
            "xi-api-key": api_key,
            "Content-Type": "application/json",
            "Accept": "audio/mpeg",
        },
    )
    with urllib.request.urlopen(req, timeout=120) as resp:
        return resp.read()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--lang", choices=["en", "ar"], help="generate one language only")
    ap.add_argument("--dry-run", action="store_true", help="list lines, no API calls")
    args = ap.parse_args()

    with open(CONTENT, encoding="utf-8") as f:
        content = json.load(f)

    langs = [args.lang] if args.lang else ["en", "ar"]
    lines = list(collect_lines(content))
    todo = []
    for line_id, cat, seed, texts in lines:
        for lang in langs:
            out = os.path.join(OUT_DIR, f"{line_id}_{lang}.mp3")
            if not os.path.exists(out):
                todo.append((line_id, cat, seed, texts[lang], lang, out))

    total_chars = sum(len(t[3]) for t in todo)
    print(f"{len(lines)} lines x {len(langs)} language(s); "
          f"{len(todo)} files to generate (~{total_chars} characters)")

    if args.dry_run:
        for line_id, cat, _seed, text, lang, _out in todo:
            print(f"  [{lang}] {line_id} ({cat}): {text[:60]}…")
        return

    api_key = os.environ.get("ELEVEN_API_KEY")
    if not api_key:
        sys.exit("Set ELEVEN_API_KEY in the environment first.")

    os.makedirs(OUT_DIR, exist_ok=True)
    done = 0
    for line_id, cat, seed, text, lang, out in todo:
        voice_id = pick(cat, seed)
        try:
            audio = tts(api_key, voice_id, text, SETTINGS[cat])
        except urllib.error.HTTPError as e:
            sys.exit(f"FAILED on {line_id}_{lang}: HTTP {e.code} — {e.read()[:300]!r}")
        with open(out, "wb") as f:
            f.write(audio)
        done += 1
        print(f"  [{done}/{len(todo)}] {os.path.basename(out)} ({len(audio)//1024} KB)")
        time.sleep(0.4)  # be gentle with rate limits

    print(f"Done — {done} clips written to {OUT_DIR}")


if __name__ == "__main__":
    main()
