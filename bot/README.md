# Trump → Gold Alert Bot 🥇 (100% free)

Watches for new Trump statements/actions (like the "calling off the Iran
strikes" news), scores how gold (XAU/USD) is likely to react, and sends you
an alert by Telegram and/or email.

**Everything is free**: GitHub Actions (runner), Google News RSS (headlines),
gold-api.com (gold price), Telegram (notifications). No paid API keys.

## How it works

Every 15 minutes a GitHub Actions job:

1. Pulls fresh Trump headlines from Google News RSS (last 24h).
2. Skips anything it already alerted on (state in `seen_items.json`).
3. Keeps only headlines matching gold-relevant keywords (Iran, strike,
   tariff, Fed, sanctions, war, rates, dollar, ...).
4. Scores each one with a built-in rules engine:

   | Story type | Gold call | Why |
   |---|---|---|
   | De-escalation (calls off strikes, ceasefire, peace deal) | 📉 BEARISH | Safe-haven bid unwinds |
   | Escalation (strikes, attack, war, missiles, troops) | 📈 BULLISH | Safe-haven demand spikes |
   | Tariffs / sanctions / trade war | 📈 BULLISH | Inflation + uncertainty |
   | Pressure on the Fed / rate-cut talk | 📈 BULLISH | Weaker dollar, lower real yields |
   | Shutdown / debt-ceiling / emergency | 📈 BULLISH (weak) | Fiscal uncertainty |

   De-escalation is checked **first**, so "Trump calls off strikes on Iran"
   is correctly read as bearish even though it contains the word "strikes".
5. Sends one notification per story type per 6 hours (so 20 outlets
   covering the same story don't mean 20 pings).

The very first run only seeds the state (no alerts), so you don't get
spammed with old news.

## Setup (one time, ~10 minutes)

### 1. Add repository secrets

GitHub → your repo → **Settings → Secrets and variables → Actions →
New repository secret**. You need at least one channel:

**Telegram (recommended — instant phone push, free):**

| Secret | How to get it |
|---|---|
| `TELEGRAM_BOT_TOKEN` | Message **@BotFather** on Telegram, send `/newbot`, follow the prompts, copy the token |
| `TELEGRAM_CHAT_ID` | Send any message to your new bot, then open `https://api.telegram.org/bot<TOKEN>/getUpdates` in a browser and copy the `"chat":{"id":...}` number |

**Email (optional):**

| Secret | How to get it |
|---|---|
| `GMAIL_ADDRESS` | Your Gmail address (used as sender) |
| `GMAIL_APP_PASSWORD` | A Gmail **app password** — NOT your normal password. Create at https://myaccount.google.com/apppasswords (requires 2-step verification) |
| `ALERT_EMAIL_TO` | Optional; defaults to `GMAIL_ADDRESS` |

### 2. Activate the schedule

GitHub only runs scheduled workflows on the repository's **default branch**.
Merge this branch into your default branch (e.g. `main`) to start the
15-minute cron. You can also test immediately from any branch:
**Actions → Trump Gold Alert Bot → Run workflow**.

> Note: public repos get unlimited free Actions minutes. Private repos get
> 2,000 free minutes/month — if this repo is private, change the cron in
> `.github/workflows/trump-gold-bot.yml` to `"*/30 * * * *"` to stay well
> inside the free tier. By default GitHub just stops (doesn't charge) if
> you run out.

### 3. Run it locally (optional)

```bash
pip install -r bot/requirements.txt
export TELEGRAM_BOT_TOKEN=... TELEGRAM_CHAT_ID=...   # or GMAIL_* vars
python bot/trump_gold_bot.py
```

## Tuning

- Add/remove trigger words in `KEYWORDS` (what counts as relevant at all).
- Add patterns to `RULES` in `trump_gold_bot.py` (what fires an alert and
  in which direction). First matching rule wins.
- `ALERT_COOLDOWN_HOURS` controls how often the same story type can re-alert.

## ⚠️ Read this before trusting it with money

- **Markets move faster than any bot.** Gold reprices within *seconds* of a
  headline like the Iran de-escalation news. By the time this (or any
  free news bot) alerts you, the initial move has almost always happened.
  Use the alerts for **situational awareness and managing open positions** —
  not as entry signals.
- **Rules are simple-minded.** This engine matches words, it doesn't
  understand context. It will sometimes call the direction wrong or alert
  on sarcasm/opinion pieces. Markets also routinely do the opposite of the
  textbook.
- **Protect yourself structurally**: if one headline can wipe out most of
  your account, the position was too big. Use stop losses and size positions
  so a single Trump headline can't take you out.
- Not financial advice.
