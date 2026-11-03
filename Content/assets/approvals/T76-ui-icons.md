# Approval packet T76: UI icon sheets for Bug List 1 (6 sheets, 2 options each)

Status: PENDING OWNER APPROVAL. Nothing generated. Only free calls used: `openart_account_get` (balance 4,058), `openart_model_cost` (2K, 1:1, 2 images = 30 credits per call).

## 1. Purpose
Icons replace text/glyphs (owner 2026-11-03). Six sheets, sliced into single icons:
- S1 Shop tabs: Bundles (gift box), Crown (subscription + chests), Character/Power-ups (Momo + power-up bolt together), Gems/Coins (gem + coin together)
- S2 Buttons: Play triangle, Settings gear, Close X, Back arrow, Info "i", Hero card back with "?", Cloud-save, Sign-in person
- S3 Settings rows (12): sound, music, haptic (vibrating phone), reduced motion, left-handed, text size, link account, save progress, reset, restore purchases, admin wrench, daily reward gift
- S4 Admin (8): system test, game test (pad), shop test (cart), character test, economy, level skip, logs, close
- S5 Shop banners (6 wide ribbons, no words): Subscription, Chests, Heroes, Power-ups, Gems, Coins (emblem on yellow ribbon)
- S6 Bundle contents (6): coin stack, power-up bolt, hero card "?", worth star badge, gem, info

## 2. MCP and tool
OpenArt MCP (connector `mcp__73f004bd-92f2-4e13-8abf-f0c13bc6ba8f__`), `openart_generate_image`, 6 calls, one after the other.

## 3. Model and mode
`byte-plus-seedream-5-lite` (Seedream 5 Lite), text2image, 2K, aspect 1:1 (S5: 3:2 if offered, else 1:1), imageCount 2, no seed, reference: none (text only; shop style words in prompt).

## 4. Prompts, word for word
PREFIX (all): "An icon sheet for a cute chibi cartoon 2D mobile game UI, glossy cartoon style, thick consistent dark-navy outline, saturated gradient colors, glossy sparkle accents, top-left light. Layout: a clean grid of separate icons, each centred in its own cell, evenly spaced with generous empty margins, on a flat solid #808080 background, nothing overlapping, every icon the same size and style, no words, no letters, no numbers, no watermark."
S1: " A 2 by 2 grid of four tab icons. Top left: a wrapped gift box with a ribbon bow. Top right: a golden crown with three jewels. Bottom left: a cute round brown-and-cream bear-cub hero face, with a glowing yellow lightning bolt power-up orb overlapping its lower right. Bottom right: a blue faceted gem and a gold coin side by side, overlapping slightly."
S2: " A 4 by 2 grid of eight button icons. Row 1: a big white-green play triangle; a blue-grey settings gear; a red close X; a white back arrow. Row 2: a white letter-less round info icon (a dot above a bar); a blue playing-card back with a big white question mark; a white cloud with an up arrow; a friendly person bust with a small plus."
S3: " A 4 by 3 grid of twelve setting icons. Row 1: a speaker with sound waves; a music note pair; a vibrating phone with shake lines; a snail (reduced motion). Row 2: a left hand with an arrow; a large and a small letter-less text-size symbol made of two bars; a chain link; a floppy-disk save icon. Row 3: a circular reset arrow; a shopping bag with a circular arrow; a wrench; a wrapped gift."
S4: " A 4 by 2 grid of eight admin icons. Row 1: a power switch; a game controller; a shopping cart; a hero face. Row 2: a coin-and-gem pile; a stairs-up arrow; a clipboard with lines; a red close X."
S5: " A 2 by 3 grid of six wide yellow ribbon banners without any words, each with a round emblem on the left of the ribbon: a gold star; an open treasure chest; a crown-wearing hero face; a lightning bolt orb; a blue gem; a gold coin."
S6: " A 3 by 2 grid of six icons: a stack of gold coins; a yellow lightning bolt orb; a card back with a big question mark; a gold starburst badge with a multiply sign; a blue faceted gem; a white round info icon."
SUFFIX (all): " palette: sky blue, sunny yellow, mint green, coral pink, white. cute chibi cartoon, clean vector-like edges, solid flat #808080 mid-grey background"
No competitor names. Each call = PREFIX + sheet + SUFFIX.

## 5. Cost
6 calls x 2 images x 15 = **180 credits** (30 per call). Balance now **4,058**, after **3,878**.

## 6. Output handling
Saved to Art/_candidates/ui_icons/ (S1A S1B ... S6B), contact sheet shown, you pick one option per sheet, then sliced, QA'd (qa_art.py), placed in Resources/Art/Ui, wired into the named icon slots. Logged as T76 in Content/assets/assets_log.csv. Failed sheet: reported, never retried without a new ask.

## 7. Alternatives
- One option per sheet: 90 credits (saves 90).
- Only S1 (the two required tab icons) + S2: 60 credits (saves 120).
