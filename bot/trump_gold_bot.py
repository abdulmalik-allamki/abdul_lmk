#!/usr/bin/env python3
"""Trump -> gold market alert bot (100% free, no API keys for analysis).

Pulls fresh Trump-related headlines from Google News RSS, filters for
market-relevant ones, scores the likely impact on gold (XAU/USD) with a
built-in rules engine, and sends an alert via email and/or Telegram.

Designed to run on a schedule (GitHub Actions cron). State (already-seen
headlines + recently-sent alert types) is kept in seen_items.json next to
this script and committed back by the workflow so reruns don't re-alert
on the same news.

Environment variables (only needed for the channel you use):
  GMAIL_ADDRESS         Gmail address used to send the alert
  GMAIL_APP_PASSWORD    Gmail "app password" (not your login password)
  ALERT_EMAIL_TO        recipient; defaults to GMAIL_ADDRESS
  TELEGRAM_BOT_TOKEN    Telegram bot token for push alerts
  TELEGRAM_CHAT_ID      Telegram chat to send alerts to

NOT FINANCIAL ADVICE. Markets typically reprice within seconds of a major
headline, before any bot can alert you. Use this for awareness and risk
management, never as a trade signal.
"""

import json
import os
import smtplib
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from email.mime.text import MIMEText
from pathlib import Path

import requests

STATE_FILE = Path(__file__).parent / "seen_items.json"
MAX_SEEN = 800
# Don't repeat an alert for the same story type (e.g. "bearish/de-escalation")
# more than once per this window, even as more outlets cover it.
ALERT_COOLDOWN_HOURS = 6

FEEDS = [
    # Broad Trump news from the last day
    "https://news.google.com/rss/search?q=%22Trump%22+when:1d&hl=en-US&gl=US&ceid=US:en",
]

# A headline must contain at least one of these (case-insensitive) to be
# considered potentially market-moving for gold. Keeps noise out.
KEYWORDS = [
    "iran", "israel", "strike", "attack", "bomb", "military", "war",
    "ceasefire", "missile", "nuclear",
    "tariff", "trade", "sanction", "china", "russia", "ukraine",
    "fed", "powell", "interest rate", "rates", "inflation", "dollar",
    "treasury", "debt", "deficit", "shutdown", "emergency",
    "oil", "gold", "executive order", "troops", "deploy",
]

# Rules are checked in order; the FIRST match wins. De-escalation comes
# before escalation on purpose: "Trump calls off strikes on Iran" contains
# the word "strikes" but is de-escalation news (bearish for gold).
RULES = [
    {
        "name": "de-escalation",
        "impact": "bearish",
        "confidence": "high",
        "patterns": [
            "calls off", "called off", "cancels", "canceled", "cancelled",
            "ceasefire", "cease-fire", "peace deal", "peace agreement",
            "truce", "de-escalat", "deal to end", "ends war", "end the war",
            "deal is near", "deal is close", "halts strikes", "halt strikes",
            "backs off", "backs down", "stands down", "calls for calm",
            "agreement reached", "tensions ease",
        ],
        "reasoning": (
            "De-escalation kills the safe-haven bid: gold typically SELLS OFF "
            "when war risk fades. If you're long gold on geopolitical fear, "
            "this is the headline that unwinds it — and the first leg of the "
            "move usually happens within seconds."
        ),
    },
    {
        "name": "escalation",
        "impact": "bullish",
        "confidence": "high",
        "patterns": [
            "strike", "strikes", "attack", "bomb", "bombing", "missile",
            "military action", "military operation", "declares war",
            "war with", "invasion", "invades", "troops", "deploys",
            "nuclear", "retaliat", "threatens to", "ultimatum",
        ],
        "reasoning": (
            "Escalation drives safe-haven demand: gold typically RALLIES on "
            "war/strike threats. Watch for the reverse move if Trump later "
            "walks it back — these spikes often fully unwind on de-escalation."
        ),
    },
    {
        "name": "tariffs-sanctions",
        "impact": "bullish",
        "confidence": "medium",
        "patterns": [
            "tariff", "sanction", "trade war", "export ban", "import ban",
            "trade restrictions",
        ],
        "reasoning": (
            "Tariffs and sanctions raise inflation expectations and economic "
            "uncertainty — historically supportive for gold, though the move "
            "is usually slower and smaller than a war headline."
        ),
    },
    {
        "name": "fed-pressure",
        "impact": "bullish",
        "confidence": "medium",
        "patterns": [
            "fires powell", "fire powell", "firing powell", "replace powell",
            "rate cut", "cut rates", "cuts rates", "lower rates",
            "lower interest", "slams fed", "slams powell", "pressures fed",
            "attacks fed", "blasts fed", "blasts powell",
        ],
        "reasoning": (
            "Pressure on the Fed / rate-cut talk weakens the dollar and "
            "lowers real yields — both bullish for gold. Threats to Fed "
            "independence add an extra fear premium."
        ),
    },
    {
        "name": "fiscal-stress",
        "impact": "bullish",
        "confidence": "low",
        "patterns": [
            "shutdown", "debt ceiling", "default on", "debt crisis",
            "national emergency",
        ],
        "reasoning": (
            "Fiscal stress and emergency politics tend to support gold "
            "modestly via uncertainty and dollar weakness."
        ),
    },
]


@dataclass
class Alert:
    headline: str
    link: str
    rule: str
    impact: str       # bullish | bearish
    confidence: str   # low | medium | high
    reasoning: str


def load_state() -> dict:
    if STATE_FILE.exists():
        data = json.loads(STATE_FILE.read_text())
        if isinstance(data, list):  # migrate old list-only format
            return {"seen": data, "recent_alerts": {}}
        return data
    return {"seen": [], "recent_alerts": {}}


def save_state(state: dict) -> None:
    state["seen"] = state["seen"][-MAX_SEEN:]
    STATE_FILE.write_text(json.dumps(state, indent=0) + "\n")


def fetch_headlines() -> list:
    """Return all current feed items as dicts with id/title/link/published."""
    items = []
    for url in FEEDS:
        resp = requests.get(url, timeout=20, headers={"User-Agent": "Mozilla/5.0"})
        resp.raise_for_status()
        root = ET.fromstring(resp.content)
        for entry in root.iter("item"):
            title = (entry.findtext("title") or "").strip()
            link = (entry.findtext("link") or "").strip()
            item_id = (entry.findtext("guid") or "").strip() or link or title
            if not item_id:
                continue
            items.append({
                "id": item_id,
                "title": title,
                "link": link,
                "published": (entry.findtext("pubDate") or "").strip(),
            })
    return items


def is_relevant(title: str) -> bool:
    lower = title.lower()
    return any(kw in lower for kw in KEYWORDS)


def analyze_headline(item: dict) -> Alert | None:
    """First matching rule wins; returns None if no rule matches."""
    lower = item["title"].lower()
    for rule in RULES:
        if any(p in lower for p in rule["patterns"]):
            return Alert(
                headline=item["title"],
                link=item["link"],
                rule=rule["name"],
                impact=rule["impact"],
                confidence=rule["confidence"],
                reasoning=rule["reasoning"],
            )
    return None


def get_gold_spot() -> float | None:
    """Current gold price in USD from free keyless APIs. Best-effort."""
    try:
        resp = requests.get("https://api.gold-api.com/price/XAU", timeout=10)
        resp.raise_for_status()
        return float(resp.json()["price"])
    except Exception:
        pass
    try:
        resp = requests.get(
            "https://query1.finance.yahoo.com/v8/finance/chart/GC=F"
            "?interval=1d&range=1d",
            timeout=10,
            headers={"User-Agent": "Mozilla/5.0"},
        )
        resp.raise_for_status()
        meta = resp.json()["chart"]["result"][0]["meta"]
        return float(meta["regularMarketPrice"])
    except Exception:
        return None


def filter_cooldown(alerts: list[Alert], state: dict) -> list[Alert]:
    """Drop alerts whose story type already fired within the cooldown window,
    so 20 outlets covering the same story don't mean 20 notifications."""
    now = datetime.now(timezone.utc)
    cutoff = now - timedelta(hours=ALERT_COOLDOWN_HOURS)
    recent = {
        sig: ts for sig, ts in state.get("recent_alerts", {}).items()
        if datetime.fromisoformat(ts) > cutoff
    }
    fresh = []
    for alert in alerts:
        sig = f"{alert.impact}|{alert.rule}"
        if sig in recent:
            continue
        recent[sig] = now.isoformat()
        fresh.append(alert)
    state["recent_alerts"] = recent
    return fresh


def format_alert(alerts: list[Alert], gold_price: float | None) -> tuple[str, str]:
    arrows = {"bullish": "📈 BULLISH", "bearish": "📉 BEARISH"}
    top = alerts[0]
    subject = f"🥇 Gold Alert: {arrows[top.impact]} — {top.headline[:80]}"

    lines = ["TRUMP → GOLD ALERT", ""]
    if gold_price:
        lines.append(f"Gold spot now: ${gold_price:,.2f}")
        lines.append("")
    for alert in alerts[:6]:
        lines.append(f"{arrows[alert.impact]} ({alert.confidence} confidence — {alert.rule})")
        lines.append(f"Headline: {alert.headline}")
        lines.append(f"Why: {alert.reasoning}")
        if alert.link:
            lines.append(f"Link: {alert.link}")
        lines.append("")
    lines.append("—" * 30)
    lines.append(
        "Not financial advice. Major headlines are usually priced into gold "
        "within seconds — treat this as situational awareness for managing "
        "open positions, not as an entry signal."
    )
    return subject, "\n".join(lines)


def send_email(subject: str, body: str) -> bool:
    sender = os.environ.get("GMAIL_ADDRESS")
    password = os.environ.get("GMAIL_APP_PASSWORD")
    recipient = os.environ.get("ALERT_EMAIL_TO") or sender
    if not (sender and password and recipient):
        return False
    msg = MIMEText(body)
    msg["Subject"] = subject
    msg["From"] = sender
    msg["To"] = recipient
    with smtplib.SMTP_SSL("smtp.gmail.com", 465) as smtp:
        smtp.login(sender, password)
        smtp.send_message(msg)
    print(f"Email alert sent to {recipient}")
    return True


def send_telegram(body: str) -> bool:
    token = os.environ.get("TELEGRAM_BOT_TOKEN")
    chat_id = os.environ.get("TELEGRAM_CHAT_ID")
    if not (token and chat_id):
        return False
    resp = requests.post(
        f"https://api.telegram.org/bot{token}/sendMessage",
        json={"chat_id": chat_id, "text": body},
        timeout=15,
    )
    resp.raise_for_status()
    print("Telegram alert sent")
    return True


def main() -> int:
    first_run = not STATE_FILE.exists()
    state = load_state()
    seen_set = set(state["seen"])

    items = fetch_headlines()
    print(f"Fetched {len(items)} feed items")

    new_items = [i for i in items if i["id"] not in seen_set]
    state["seen"].extend(i["id"] for i in new_items)

    if first_run:
        save_state(state)
        print("First run: seeded state with current headlines, no alerts sent.")
        return 0

    relevant = [i for i in new_items if is_relevant(i["title"])]
    print(f"{len(new_items)} new items, {len(relevant)} pass the keyword filter")

    alerts = [a for a in (analyze_headline(i) for i in relevant) if a]
    alerts = filter_cooldown(alerts, state)
    save_state(state)
    print(f"{len(alerts)} alert(s) after rules + cooldown")
    if not alerts:
        return 0

    gold_price = get_gold_spot()
    subject, body = format_alert(alerts, gold_price)
    delivered = send_telegram(body)
    delivered = send_email(subject, body) or delivered
    if not delivered:
        print("WARNING: no notification channel configured "
              "(set GMAIL_* or TELEGRAM_* secrets). Alert body follows:")
        print(body)
    return 0


if __name__ == "__main__":
    sys.exit(main())
