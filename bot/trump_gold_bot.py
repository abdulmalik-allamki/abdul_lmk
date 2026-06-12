#!/usr/bin/env python3
"""Trump -> gold market alert bot.

Pulls fresh Trump-related headlines from Google News RSS, filters for
market-relevant ones, asks Claude to assess the likely impact on gold
(XAU/USD), and sends an alert via email and/or Telegram.

Designed to run on a schedule (GitHub Actions cron). State (already-seen
headlines) is kept in seen_items.json next to this script and committed
back by the workflow so reruns don't re-alert on the same news.

Environment variables:
  ANTHROPIC_API_KEY     required - Claude API key
  GMAIL_ADDRESS         optional - Gmail address used to send the alert
  GMAIL_APP_PASSWORD    optional - Gmail "app password" (not your login password)
  ALERT_EMAIL_TO        optional - recipient; defaults to GMAIL_ADDRESS
  TELEGRAM_BOT_TOKEN    optional - Telegram bot token for push alerts
  TELEGRAM_CHAT_ID      optional - Telegram chat to send alerts to

NOT FINANCIAL ADVICE. Markets typically reprice within seconds of a major
headline, before any bot can alert you. Use this for awareness and risk
management, never as a trade signal.
"""

import json
import os
import smtplib
import sys
import xml.etree.ElementTree as ET
from email.mime.text import MIMEText
from pathlib import Path
from typing import List, Literal

import requests
from pydantic import BaseModel

import anthropic

STATE_FILE = Path(__file__).parent / "seen_items.json"
MAX_SEEN = 800

FEEDS = [
    # Broad Trump news from the last day
    "https://news.google.com/rss/search?q=%22Trump%22+when:1d&hl=en-US&gl=US&ceid=US:en",
]

# A headline must contain at least one of these (case-insensitive) to be
# considered potentially market-moving for gold. Keeps API costs down and
# noise out.
KEYWORDS = [
    "iran", "israel", "strike", "attack", "bomb", "military", "war",
    "ceasefire", "missile", "nuclear",
    "tariff", "trade", "sanction", "china", "russia", "ukraine",
    "fed", "powell", "interest rate", "rates", "inflation", "dollar",
    "treasury", "debt", "deficit", "shutdown", "emergency",
    "oil", "gold", "executive order", "troops", "deploy",
]


class HeadlineAnalysis(BaseModel):
    headline: str
    market_moving: bool
    gold_impact: Literal["bullish", "bearish", "neutral"]
    confidence: Literal["low", "medium", "high"]
    reasoning: str


class GoldAnalysis(BaseModel):
    items: List[HeadlineAnalysis]
    overall_summary: str


def load_seen() -> list:
    if STATE_FILE.exists():
        return json.loads(STATE_FILE.read_text())
    return []


def save_seen(seen: list) -> None:
    STATE_FILE.write_text(json.dumps(seen[-MAX_SEEN:], indent=0) + "\n")


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


def analyze(headlines: list, gold_price: float | None) -> GoldAnalysis:
    client = anthropic.Anthropic()

    price_note = (
        f"Current gold spot (XAU/USD) is approximately ${gold_price:,.2f}."
        if gold_price else
        "Current gold spot price is unavailable."
    )
    headline_block = "\n".join(
        f"- {h['title']} (published: {h['published']})" for h in headlines
    )

    response = client.messages.parse(
        model="claude-opus-4-8",
        max_tokens=16000,
        thinking={"type": "adaptive"},
        system=(
            "You are a macro analyst specializing in gold (XAU/USD). You assess "
            "how political headlines - especially statements and actions by "
            "Donald Trump - are likely to move gold. Consider the standard "
            "transmission channels: geopolitical risk and safe-haven demand "
            "(escalation is typically bullish for gold, de-escalation bearish), "
            "USD strength, Fed policy and rate expectations, tariffs and "
            "inflation expectations, and fiscal deficits. Mark market_moving "
            "true only for headlines plausibly large enough to move gold; mark "
            "duplicates of the same underlying story only once. Keep each "
            "reasoning to 2-3 plain sentences a retail trader can act on, and "
            "note when the move has likely already been priced in."
        ),
        messages=[{
            "role": "user",
            "content": (
                f"{price_note}\n\n"
                "Assess the likely gold impact of each fresh Trump headline "
                f"below:\n\n{headline_block}"
            ),
        }],
        output_format=GoldAnalysis,
    )
    return response.parsed_output


def format_alert(flagged: List[HeadlineAnalysis], summary: str,
                 gold_price: float | None) -> tuple[str, str]:
    arrows = {"bullish": "📈 BULLISH", "bearish": "📉 BEARISH", "neutral": "➖ NEUTRAL"}
    top = flagged[0]
    subject = f"🥇 Gold Alert: {arrows[top.gold_impact]} — {top.headline[:80]}"

    lines = ["TRUMP → GOLD ALERT", ""]
    if gold_price:
        lines.append(f"Gold spot now: ${gold_price:,.2f}")
        lines.append("")
    lines.append(f"Summary: {summary}")
    lines.append("")
    for item in flagged:
        lines.append(f"{arrows[item.gold_impact]} ({item.confidence} confidence)")
        lines.append(f"Headline: {item.headline}")
        lines.append(f"Analysis: {item.reasoning}")
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
    seen = load_seen()
    seen_set = set(seen)

    items = fetch_headlines()
    print(f"Fetched {len(items)} feed items")

    new_items = [i for i in items if i["id"] not in seen_set]
    seen.extend(i["id"] for i in new_items)
    save_seen(seen)

    if first_run:
        print("First run: seeded state with current headlines, no alerts sent.")
        return 0

    relevant = [i for i in new_items if is_relevant(i["title"])]
    print(f"{len(new_items)} new items, {len(relevant)} pass the keyword filter")
    if not relevant:
        return 0

    gold_price = get_gold_spot()
    analysis = analyze(relevant, gold_price)
    flagged = [a for a in analysis.items if a.market_moving]
    print(f"Claude flagged {len(flagged)} headline(s) as market-moving")
    if not flagged:
        return 0

    subject, body = format_alert(flagged, analysis.overall_summary, gold_price)
    delivered = send_telegram(body)
    delivered = send_email(subject, body) or delivered
    if not delivered:
        print("WARNING: no notification channel configured "
              "(set GMAIL_* or TELEGRAM_* secrets). Alert body follows:")
        print(body)
    return 0


if __name__ == "__main__":
    sys.exit(main())
