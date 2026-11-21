# 0013 Tiers on skills: a rarer, stronger skill at the floor reward

Status: **the direction is Kristian's: he named tiers on skills on 2026-11-20 and ticked them as the next thing to build on 2026-11-21. The design and the numbers are the producer's proposal; nothing of it is built, and he has not seen it.**

## What he said

On 2026-11-20, after playing, in the message decision 0012 grew from: "vi har ikke noe tiers på upgrades og spells. men det kunne være noe vi så litt mer på." In the same message he asked for more variety in upgrades and more stats (decision 0012, built by work order 050: the chest's stat steps come as common, rare or epic, and luck leans the odds).

On 2026-11-21, asked what to build next, he ticked **"Tiers på ferdigheter"**.

Decision 0012 left this open on purpose: "Skills get tiers later and in their own way (the elite rooms' skill upgrades are already a second layer on a skill)."

## What follows

1. A skill, like a chest's step, can come **common, rare or epic**: the same three words and colours as the chest's (white, teal, gold), and the same luck leans them. One tier vocabulary in the game, not two.
2. The tier must be **the game's kind of tier.** The pillars ask that an upgrade "changes what a skill is for" (pillar 3) and that a stronger thing is read, not hidden (pillar 4). A bigger number is the smallest step; the step worth waiting for changes how the skill is pressed.
3. It must not be a second system beside the elite room's upgrades. A skill already has one list of what has been done to it in a run (`RunSkillUpgrades`); a tier goes into that list.

## The producer's proposal

**A tier is drawn when a skill is offered, and it is fixed when she takes it.** The floor reward's three cards each draw a tier. A rare or epic card is the same skill, stronger, for the whole run. *Why:* the floor reward is the run's big moment already ("en relativt sterk belønning"); a gold card among three is the "a rare one!" moment, and it makes the choice harder in a good way: the rare skill, or the one the build wants?

**What each tier is:**

| Tier | What she gets | Why |
|---|---|---|
| Common | The skill as it is today. The card is today's card. | Most cards; nothing to read. |
| Rare | The skill with **+30 % damage and stagger** (a skill that deals none, Slide: **20 % shorter cooldown**). | One plain number, the same for every skill, read at a glance. |
| Epic | The rare's step **and one of the skill's own "change" upgrades already on it**, drawn from the elite room's table (an epic Long Shot may come with Overcharge or Snap Shot; an epic Powder Keg with Twin Kegs). The card names it. | An epic that changes how the skill is played is worth more than any number, and these changes already exist, are written, hooked into the game and tested. Nothing new has to be invented to make epic mean something. |

**How it meets the elite room: one system.** The tier's step and the epic's change are written into the run's list of skill upgrades, the list the elite room already reads. So the elite room never offers the change the epic brought (nor one it excludes: Snap Shot and Overcharge), and the epic's change shows as the gold pip an upgrade always shows. The rare's numbers step is in the list too, but it is not an upgrade: it has no pip and no name in the lists. The elite room's own cards stay as they are: no tiers on them.

**Luck and the odds.** Each card draws its tier from **60 / 30 / 10** (common / rare / epic), leaned by her luck exactly as a chest's are (the same rule, the same luck stat; with the stats switched off her luck is 0). About three floor rewards in four show a rare or better; about one in four shows an epic. **Floor 1's reward always holds at least one rare or better**, so the tiers are met in the first minutes of every run, and he meets one on his first evening with it.

**The card.** A rare or epic card's large numeral is in the tier's colour; its text begins with the tier's word and what it adds ("RARE   +30 % damage and stagger"); an epic's text ends with its change's name and line. The cooldown on the card is the tiered skill's. A common card is today's card, without a word: most cards are common, and a word on every card would hide the one that matters. The placeholder offer bell (`RareOffer`) rings once when a floor reward with a rare card comes up and twice for an epic, as it does for a chest.

**A skill she already has.** In this first version a higher tier is never offered for it: the floor reward only offers skills she lacks, and a skill keeps the tier it was taken at for the run. The start four are always common. A later order can let the elite room raise a skill she has (below).

**Eight slots stay readable.** Nothing new on the skill bar. The epic's change is a pip, because it is an upgrade; the tier itself is written where she looks things up: beside the skill in the pause menu's list of keys ("W  Long Shot  epic  (Overcharge)"), on the death and act-cleared screens, and in the session log. The bar shows what she must react to now; a tier is something she chose once.

**Switchable.** One switch (`RunValues.SkillTiers`); off, the game is exactly what it was. The tiers are drawn from a stream of their own, so the same run seed offers the same three skills with the switch on or off; only their tiers differ.

## Considered and not chosen

- **A tier a skill rises to by taking it again** (a duplicate at the floor reward). A run is four skills to eight (decision 0005); a duplicate would leave a key empty, and the floor reward would become "a new key or a stronger old one", a second kind of choice with rules of its own.
- **Tiers on the elite room's upgrade cards** (a rare Heavy Round of +80 % instead of +60 %). That is two layers saying the same thing: a numbers upgrade with a numbers tier on it. And a change cannot be made "rarer" by scaling it.
- **A tier counted from the upgrades a skill has** (no upgrade common, one rare, two epic). It only renames the pips, and nothing is ever a surprise.
- **A rare and an epic written by hand for each of the twelve skills.** The richest, and twenty-four new rows with new hooks and tests. The existing changes are such rows already; if the drawn ones feel samey after playing, this is the later step.
- **Rare as the skill's own "numbers" upgrade taken early.** Varied, but Long Shot and Breaker Slug have none, and they differ widely in size (Third Wind against Deep Tag). One plain step is one number to tune.
- **Rare as a shorter cooldown** (more presses: pillar 2). Considered seriously; damage was chosen because it shows in the numbers she sees, and a cooldown cut on the skill would stack past the chests' cooldown floor. Open for him below.
- **A tier mark on the skill bar.** Teal already means "live" there and gold means an upgrade; a third meaning on eight small slots costs more than it tells.
- **The drop on the ground in the tier's colour**, a beam before she picks it up. A good moment, but gold is the elite room's drop; it needs a look of its own. Later.

## Later, if he likes it

1. **The elite room can raise a skill she has**: one of its three cards may be "Quick Shot: common to rare" or "Long Shot: rare to epic, with Overcharge". This is how the start four get tiers, and the answer for a skill she already has. Still one list.
2. **The drop shows its best tier** on the ground before she picks it up.
3. **Hand-written epics** for the skills whose drawn changes feel flat.

## Open, for Kristian

1. Rare as **+30 % damage**, or as **a shorter cooldown** (more presses)?
2. The odds **60 / 30 / 10**, and floor 1 always showing a rare or better: too generous, too stingy?
3. Should the start four ever get a tier (the elite room's "raise" card, later order 1)?
4. Does he want to see the tier on the skill bar after all?
5. The word: "tier" here means how rare a thing is. Decision 0005 used "two tiers" for the tripod's two rows of choices; the glossary should say which meaning the word has from now on.
