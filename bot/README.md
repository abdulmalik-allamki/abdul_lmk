# Trump → Gold Alert Bot 🥇

Watches for new Trump statements/actions (like the "not bombing Iran" news),
asks Claude how gold (XAU/USD) is likely to react, and sends you an alert by
email and/or Telegram.

## How it works

Every 15 minutes a GitHub Actions job:

1. Pulls fresh Trump headlines from Google News RSS (last 24h).
2. Skips anything it already alerted on (state in `seen_items.json`).
3. Keeps only headlines matching gold-relevant keywords (Iran, strike,
   tariff, Fed, sanctions, war, rates, dollar, ...).
4. Sends the new ones to Claude, which returns for each: **bullish /
   bearish / neutral for gold**, a confidence level, and 2-3 sentences of
   reasoning (safe-haven demand, USD, rates, tariffs/inflation).
5. If anything is judged market-moving, you get a notification.

The very first run only seeds the state (no alerts), so you don't get
spammed with old news.

## Setup (one time, ~10 minutes)

### 1. Add repository secrets

GitHub → your repo → **Settings → Secrets and variables → Actions →
New repository secret**:

| Secret | Required | What it is |
|---|---|---|
| `ANTHROPIC_API_KEY` | ✅ | Claude API key from https://platform.claude.com |
| `GMAIL_ADDRESS` | for email | Your Gmail address (sender) |
| `GMAIL_APP_PASSWORD` | for email | A Gmail **app password** — NOT your normal password. Create one at https://myaccount.google.com/apppasswords (requires 2-step verification enabled) |
| `ALERT_EMAIL_TO` | optional | Where to send alerts; defaults to `GMAIL_ADDRESS` |
| `TELEGRAM_BOT_TOKEN` | optional | For instant phone push — message @BotFather on Telegram, send `/newbot`, copy the token |
| `TELEGRAM_CHAT_ID` | optional | Message your new bot once, then open `https://api.telegram.org/bot<TOKEN>/getUpdates` and copy `chat.id` |

You need at least one channel configured (email or Telegram). **Telegram is
recommended** — it arrives as a phone push within seconds; email can lag.

### 2. Activate the schedule

GitHub only runs scheduled workflows on the repository's **default branch**.
Merge this branch into your default branch (e.g. `main`) to start the
15-minute cron. You can also test immediately from any branch:
**Actions → Trump Gold Alert Bot → Run workflow**.

### 3. Run it locally (optional)

```bash
pip install -r bot/requirements.txt
export ANTHROPIC_API_KEY=sk-ant-...
export TELEGRAM_BOT_TOKEN=... TELEGRAM_CHAT_ID=...   # or GMAIL_* vars
python bot/trump_gold_bot.py
```

## ⚠️ Read this before trusting it with money

- **Markets move faster than any bot.** Gold reprices within *seconds* of a
  headline like the Iran de-escalation news. By the time this (or any
  free news bot) alerts you, the initial move has almost always happened.
  Use the alerts for **situational awareness and managing open positions** —
  not as entry signals.
- **The analysis can be wrong.** It's a model's best read of likely
  direction, not a prediction. "De-escalation = bearish gold" is the usual
  pattern, but markets routinely do the opposite of the textbook.
- **Protect yourself structurally**: if one headline can wipe out most of
  your account, the position was too big. Use stop losses and size positions
  so a single Trump tweet can't take you out.
- Not financial advice.
