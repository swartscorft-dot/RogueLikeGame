# Research brief A: how Lost Ark builds a class kit

Written 2026-11-19. Three kits taken apart (Gunslinger, Igniter Sorceress, Remaining Energy Deathblade), then tripods, engravings and boss windows, then patterns and two comparisons.

**How to read the numbers.** Cooldowns, input types and stagger grades come from two skill databases. The first is the Fextralife wiki, whose pages were last edited Feb–Sep 2022 (Western launch data). The second is Lost Ark Codex, a datamine updated to Oct 2026. Where I checked both (Focused Shot 27 s, Target Down 36 s, Dual Buckshot 30 s, Sharpshooter 30 s), they agree. These are **base cooldowns at skill level 1**. Endgame cooldowns are shorter because of gems, the Swiftness stat and some upgrades, and I found no source that gives the final numbers. Tripod effects come from 2022 wiki pages. Lost Ark has reworked tripods since then, so treat those as examples of *kinds* of change, not as current values. Build and rotation advice comes from guides dated 2023–2026, with the date given each time.

**Terms used throughout**
- **Identity gauge**: the class-specific meter (key Z).
- **Tripod**: a per-skill upgrade. You pick one of 2–3 options in each of three tiers.
- **Class engraving / Ark Passive "Enlightenment"**: the choice between a class's two playstyles. It used to be an engraving and since 2024 is a passive tree.
- **Synergy**: a debuff or buff that helps the whole party, such as "Weakness Exposure", which lowers the boss's crit resistance by 10 %.
- **Back attack / front attack**: bonus damage for hitting from behind or in front.
- **Stagger**: damage to the boss's stamina bar. Emptying it stuns the boss.
- **Weak point / part break**: damage that breaks boss parts.
- **Counter**: a skill flagged to interrupt a boss attack that glows blue.
- **Paralysis / push immunity**: protection from being knocked down or pushed during a skill.
- **Input types**:
  - Normal: one press.
  - Combo: press again to continue.
  - Chain: may be recast within a short window.
  - Holding: hold the key for the duration.
  - Charge: hold, then release; stronger the longer you hold.
  - Casting: stand still while a cast bar fills.
  - Point: pick a spot, then fire.
  - Perfect Zone: a holding or charge skill with a bonus if released inside a marked band.
- **T skill**: the "Hyper Awakening Technique", an extra long-cooldown skill outside the eight slots.
- **Awakening**: an ultimate on a very long cooldown.

---

## 1. Summary: ten findings for designing our kit

1. **The eight slots are not all damage, but almost none are dead.** In every kit I looked at, the "utility" slots (synergy, mobility, counter) are also damage skills or rotation fillers. Gunslinger's synergy skills are 6–8 s cooldown gun skills that also move her. The counter skills are ordinary damage skills carrying a "Counter: Yes" flag. Pure situational slots number 0–2 of 8.
2. **Cooldowns form a ladder.** There are fillers at 6–15 s, mid skills at 16–24 s, heavy hitters at 27–36 s, and a T skill at 60–75 s. Our eight skills (5–12 s) all sit on the filler rung, so nothing feels like a payoff.
3. **The gauge spend compresses cooldowns; it does not just multiply damage.** Igniter's window cuts current cooldowns by 25 % (50 % with a passive). Deathblade's Surge cuts them by 10/30/50 % depending on orbs. Both also add crit or attack power. The burst window therefore means *more presses*, not just bigger numbers. Our focus gives ×1.5 damage only.
4. **Gauges give skills roles.** Igniter has "builder" skills (meter-gain upgrades, used outside the window) and "spender" skills (bonus damage inside the window). The guide tells you to *avoid* one builder during the window. That simple rule creates real decisions. Our focus fills from any damage, so no skill has a gauge role.
5. **Small builder→spender loops inside the kit.** Time to Hunt Gunslinger collects a stack from every rifle skill and dumps them into one big shot (Rose Blossom) at 20 stacks. Its pistol skills speed up rifle cooldowns. This gives the filler skills a reason to exist beyond damage.
6. **Delayed hits and pre-casting are a core skill.** Igniter casts Doomsday *before* entering the window so the meteor lands inside it. A half-second delay can cost the second Doomsday. Delayed-impact skills plus a short window create timing skill without needing fast hands.
7. **Every kit mixes 3–5 input types**, and the skill expression lives in them:
   - Perfect Zone release
   - aiming three Target Down shots at a weak spot
   - charge or overcharge choices
   - recast windows (Twin Shadows, 4 s)
   - positional bonuses (back attacks)
8. **Tripods change a skill's job, not just its numbers.** Third-tier tripods in particular change the input type (charge→instant, holding→cast, combo→single dash), add recasts or charges, convert damage into stagger or stun, or remove a positional requirement. This is the model for our in-run upgrades.
9. **The second playstyle usually flips tempo and actions per minute (APM).** Maxroll labels one of each pair "high APM" and the other "low APM" again and again (Igniter low / Reflux high; Robust Spirit low / Energy Overflow high; Pinnacle low / Control high). The second engraving often deletes a whole subsystem (a stance, the gauge spend) for a flat bonus.
10. **The developer itself treated APM as a cost.** In January 2023 Smilegate said Gunslinger's stance swapping in fast combat "led to increased difficulty". They pitched the Time to Hunt change as one that "lessens the burden on the player". Every high-APM build carries "High APM" as a *con* in Maxroll's lists. High APM is a valid choice, but a game with a lot of it should also offer a lower-APM path.

---

## 2. The three kits

### 2.1 Gunslinger: Peacemaker and Time to Hunt

**Identity.** She has three weapon stances: pistol (mobility and utility), shotgun (close-range damage) and rifle (long-range heavy hits). Each skill belongs to one stance, and using it switches to that stance. ([Maxroll class overview](https://maxroll.gg/lost-ark/resources/class-overview))

- **Peacemaker (PM)** uses all three stances. Switching stance grants a buff: shotgun +10 % crit and +24 % damage, rifle +28 % damage, pistol +16 % attack speed. The "Pacifist" node makes the buff give all three at once. (Maxroll PM guide, updated 13 Jun 2026.) The buff's duration was not found. A Mokokompendium guide (20 Oct 2026) says that after dodging you should "swap weapons and back to maintain the Peacemaker buff — all damage should be done under the Peacemaker buff."
- **Time to Hunt (TtH)** disables the shotgun stance. The stance swap gives +45 % crit, +15 % stagger and −12 % MP cost to rifle skills.
  - Each rifle skill adds a "Quick Scope" stack on cast (some add more on hit). "Rose Blossom should be used when you have 20 stacks." Rose Blossom sits on the stance wheel where the shotgun was, so it does not take one of the eight slots. ([Nexus guide via search](https://nexus-guide-site.pages.dev/guides/gunslinger); the page itself failed to load)
  - Pistol skills grant "Rifle Cooling" for 4 s, which reduces rifle cooldowns by 0.2 s per second. (Maxroll TtH guide, 13 Jun 2026)
  - Rose Blossom: rifle stance, 1 min cooldown, Mid stagger, push immunity. ([Codex](https://lostarkcodex.com/us/skill/38340/))

**Standard eight (Peacemaker).** I assembled this bar from Maxroll's role lists and Mokokompendium's skill levels. The pages render the actual bar as images, so no source lists the eight explicitly. The guides disagree on the 7th damage slot (Catastrophe / Spiral Flame / Perfect Shot).

| # | Skill (stance) | Job | Input type | Base CD | Stagger / other flags | Lock (how long she is busy) | Sets up / pays off |
|---|---|---|---|---|---|---|---|
| 1 | Sharpshooter (shotgun) | Main damage | Normal, 7 shotgun blasts | 30 s | **High** stagger, back-attack skill, paralysis immunity | Not published; can be cancelled by swapping stance | Top gem priority. Cashes in the shotgun buff |
| 2 | Dual Buckshot (shotgun) | Main damage | **Combo** (2 shots, then reload-and-fire follow-up) | 30 s | Low stagger, weak point 1, back attack | Not published | "In a Tight Spot" tripod removes the back-attack requirement and adds crit, so it fires from any side |
| 3 | Focused Shot (rifle) | Main damage | Normal, 2 shots + finisher | 27 s | Mid stagger, weak point 1 | Not published; slows the target −80 % for 2 s before the finisher | High priority. Builds Quick Scope in TtH |
| 4 | Target Down (rifle) | Main damage, the long-lock nuke | **Point**: sniper stance with 3 bullets, aim up to 2.5 s each | 36 s | Mid stagger, paralysis immunity | Up to ~7.5 s if every aim is used to the full (published aim time; my sum) | Tripod rewards "direct hits" in the centre of the circle. Needs a long boss window |
| 5 | Catastrophe (rifle) | Secondary damage | Point (throw claymore at a spot) | 24 s | Mid (Concussion tripod raises it to Mid-high), weak point 1 | Not published | Area damage. Can be pushed into a stagger role |
| 6 | Bullet Rain (pistol) | Secondary damage | **Holding**, fires forward for 2 s | 22 s | Back attack, paralysis immunity | 2 s (published duration) | A pistol skill that still does real damage |
| 7 | Dexterous Shot (pistol) | **Synergy** + mobility + filler | **Combo** ×2 (×3 with tripod), glides 6 m each | 6 s | Back attack | Short (not published) | Keeps the party crit debuff up. Repositions |
| 8 | Spiral Tracker (pistol) | **Synergy** + filler | Point (throw at a spot within 10 m) | 8 s | — | Short | Crit debuff on the boss. "Heavy Duty" tripod gives 2 charges |
| alt | Perfect Shot (rifle) | Secondary damage | **Holding with Perfect Zone** | 30 s | Low (Mid with tripod) | Not published | 274 dmg on a normal release vs 1,099 inside the Perfect Zone (level-1 values) |
| alt | Spiral Flame (rifle) | Damage | Normal | 27 s | Mid | — | Used in TtH with the "Dead Center" node |
| alt | Peacekeeper (pistol) | **Counter** | Combo, 3 parts; counter on the first hit | 12 s | None base; Low with tripod | — | Situational slot |
| alt | Last Request (shotgun) | **Stagger** + counter | Normal | 36 s | **Mid-high**, part break 1, counter | — | Maxroll: "can significantly boost stagger contribution" |
| T | "Seven Shotguns" (PM) | T skill | — | not found | — | — | Mokokompendium: prioritise "your T skill". Name and slot inferred from its rotation; not verified |

**Time to Hunt changes to the bar** (Maxroll, 13 Jun 2026):
- Shotgun skills drop out.
- Damage skills: Focused Shot, Target Down, Perfect Shot, Catastrophe, Bullet Rain and Spiral Flame (only with Dead Center).
- Two pistol synergies from Dexterous Shot, Spiral Tracker and Equilibrium (16 s, pistol).
- Rose Blossom sits outside the bar; "Bullseye" is listed as main damage but its slot was not verified.

**Rotation in plain words**
- **Peacemaker** (Mokokompendium, Oct 2026): "NOT a strict rotation — Gunslinger works on a skill priority system, weaving Synergy → Damage → Synergy."
  - Their written order: Dexterous Shot, Focused Shot, T skill, Sharpshooter, Dual Buckshot, then AT02 Grenade, Target Down, Catastrophe, Spiral Flame, Bullet Rain, Spiral Tracker.
  - Before the pull, cast two pistol skills to stack buffs ("Adrenaline", an attack-power stack, and "Destiny", an Ark Grid trigger).
  - **Peaks**: the 27–36 s rifle and shotgun skills, so roughly every 30 s.
  - **Between peaks**: 6–8 s pistol synergies, stance swaps to refresh the buff, dodging.
- **Time to Hunt**:
  - The peak is Rose Blossom at 20 Quick Scope stacks, with a 1 min cooldown, so about once a minute.
  - Between peaks: cycle pistol skills to trigger "Rifle Cooling" while rifle skills are on cooldown.
  - Maxroll lists "relatively long downtime" as a con.

**APM.** No source gives a number. My count from base cooldowns, assuming each skill is used on cooldown, with no cooldown reduction and no basic attacks or dodges:
- PM bar above: about **31 skill casts per minute**, about **45–50 key presses** once combo follow-ups and the three Target Down shots are counted. Most of it comes from Dexterous Shot (10 casts per minute) and Spiral Tracker (7.5 per minute).
- TtH: about the same, roughly 32 casts per minute.
- Maxroll labels PM "high APM". TtH is not labelled high APM.

**Slots doing no rotation damage: 0 of 8 in the standard bar.**
- The two synergy slots are low-damage fillers that also move her.
- The counter (Peacekeeper, Last Request) and the pure mobility skills (Somersault Shot, Quick Step) are *alternatives*, not default picks.
- Stagger checks are covered by swapping in Last Request or Meteor Stream, or by a battle item (the "Whirlwind Grenade"; Maxroll: "can be used if equipped").

**What the identity does to the rotation**
- PM: the identity is a buff you keep up by swapping stances, so it adds presses and a habit (swap back after a dodge) rather than a timing plan.
- TtH: turns rifle casts into a counted resource with a big dump at the end. The player plans around reaching 20 stacks when Rose Blossom is off cooldown; Maxroll suggests casting rifle skills *before* the fight to start with stacks.

**Skill expression**
- **Distance management**: Maxroll says PM is about "constantly identifying what distance from the enemy is safe and positioning yourself accordingly".
- **Aimed shots**: Target Down's direct-hit bonus; the Perfect Zone release on Perfect Shot.
- **Cancels**: swapping stance cancels Sharpshooter.
- **Keeping the synergy debuff up** while weaving in damage.
- **Pre-fight stacking** of Adrenaline, Destiny and Quick Scope.

**Developer view.** Jan 2023 KR patch: "the stance switching playstyle in combination with the fast-paced combat led to increased difficulty." The Time to Hunt rework "lessens the burden on the player". ([Maxroll Jan 2023 KR patch](https://maxroll.gg/lost-ark/news/january-2023-korean-class-balance-patch))

---

### 2.2 Igniter Sorceress: the "build, then burst" gauge

**Why this class.** Its whole loop is our focus gauge made deeper. Builder skills fill the Arcane Meter. Spending it opens a timed window, Arcane Rupture, which changes how every skill behaves for its duration. Maxroll: the build "revolves around gathering Arcane Meter with your skills to release devastating Casting spells under the influence of Arcane Rupture." Deathblade and Berserker also have build-then-spend gauges. I chose Igniter because its window is the cleanest separation of "fill phase" and "burst phase". Deathblade's Surge spend is covered in 2.3.

**The window** (Maxroll Igniter guide, updated 22 Sep 2026). For "its entire duration":
- greatly shortened cast time
- +18 % damage on elemental skills
- fire skills +1 weak-point level; lightning skills +1 stagger level
- **−25 % to all current cooldowns at activation** (−50 % with the Igniter node)
- +30 % crit rate and +55 % crit damage

The window is toggled off by hand. Its length: 15 s per a PC Gamer guide from Oct 2022, which I saw only as a search result and could not open, so **not verified**. The minimum activation of 30 % meter comes from secondary guides (search results only); not verified.

| # | Skill | Job | Input type | Base CD | Stagger / flags | Lock | Sets up / pays off |
|---|---|---|---|---|---|---|---|
| 1 | Doomsday | **Spender**, main burst | **Casting** (meteor at cursor within 14 m) | 32 s | **Mid-high**, paralysis immunity | Cast time not published; meteor lands with a delay | Cast just *before* the window so it lands inside. Cast twice per window. "Magick Amplification" tripod adds damage, with an extra +20 % inside the identity state |
| 2 | Explosion | Spender | Casting ("Backfire" tripod → Normal) | 28 s | Mid, weak point 2 | Not published | Only inside the window |
| 3 | Punishing Strike | Spender | Casting ("Unavoidable Fate" tripod → Normal) | 28 s | **High** | Not published | Delayed second hit. Guide: end the window after its second hit lands |
| 4 | Rime Arrow | Builder + damage | Point | 22 s | Mid, paralysis immunity | Not published | "Enlightenment" tripod: +30 % meter on hit. Cast right after entering the window |
| 5 | Frost's Call | **Builder** | **Holding** (zone that ticks; "Unstable Rule" tripod → instant 2 s zone) | 24 s | Mid | Ticks over up to 4 s ([Codex](https://lostarkcodex.com/us/skill/37340/)) | Main meter source. Guide: avoid it during the window |
| 6 | Inferno / Seraphic Hail / Esoteric Reaction | Builder | Point / Normal / Point | 18 / 18 / 24 s | Mid | Not published | Fill the meter. Inferno and Hail may also be cast inside the window |
| 7 | Blaze | **Synergy** + damage | Normal | 10 s | — | Short | Party damage +6 % for 8 s (2022 values). "Reapply when Synergy is about to fall off" |
| 8 | Squall | **Counter** | Normal (3-hit whirlwind + knockback) | 14 s | Counter | Not published | Situational. A 2023 guide: "can be swapped for Inferno" |
| T | Lava Blast | T skill, burst | Point (magma at target) | 75 s ([Codex](https://lostarkcodex.com/us/skill/37400/)) | — | Delayed damage | "Always used during Arcane Rupture" |

**Rotation in plain words** (Maxroll, Sep 2026; [Lostarkfire, Feb 2023](https://www.lostarkfire.com/build/raid-pve-igniter-sorceress-154))
1. **Build phase**: "no real rotation, just prioritize the highest meter generating spells." Keep Blaze up. Do not waste spenders.
2. **Enter**: cast Doomsday → enter Arcane Rupture so the meteor lands inside the buffs → Rime Arrow.
3. **Inside**: Explosion, Punishing Strike, Lava Blast, the second Doomsday (its cooldown returns inside the window thanks to the cooldown cut). Never Frost's Call.
4. **Exit**: toggle the window off once the second Doomsday or Rime Arrow lands, so the meter starts refilling sooner.

**Peak**: one window per meter fill. Its frequency in seconds was not found; Doomsday's 32 s cooldown sets the rhythm. A 2023 guide says the meter is usually rebuilt before Doomsday returns.

Alternative "Incomplete Combustion" build (Maxroll): enter at only 35–40 % meter for a shorter, more frequent burst, and wait until Punishing Strike's cooldown is under 3 s before entering. **The same kit with a different gauge threshold changes the tempo.**

**APM.** Maxroll labels it **low APM**. My base-cooldown count: about **21 casts per minute** for slots 1–7 plus the T skill, not counting the window's cooldown cut. Most skills are casts, so the hands are slow but timing is strict.

**Slots doing no rotation damage: 1 (Squall, the counter).** Blaze is synergy but also damage. The builders are the rotation.

**What the identity does.** It *is* the rotation. Every skill is either a builder (fill outside) or a spender (fire inside), plus one rule against using a builder in the window. The player plans each window, and Maxroll lists "requires raid knowledge" and "reliant on party" as cons, because the window has to land when the boss is hittable.

**Skill expression**
- Pre-casting delayed skills.
- Getting two Doomsdays into one window: a 2023 guide says even "a half second" delay can make the second miss it.
- Ending the window early.
- Holding the meter for a boss vulnerability phase.
- Choosing *not* to press a ready skill.

---

### 2.3 Remaining Energy Deathblade: high APM, combo / chain / charge skills

**Why this class.** Maxroll lists "high APM" as a con, and a community Reddit thread (Jan 2025, search snippet only) named Remaining Energy as having more casts per minute than Surge. Its kit is unusually rich in exactly the input types Kristian named:
- charge skills with an "overcharge" stage: Soul Absorber, Void Strike, Blitz Rush
- a **chain** skill recast within 4 s: Twin Shadows
- **combo** skills with extra recasts from tripods: Spincutter
- positional back-attack bonuses

Glaivier and Scrapper were alternatives, but each has a second stance or gauge system that overlaps with what Gunslinger already shows.

**Identity** (Maxroll RE guide, 13 Jun 2026):
- Every skill hit fills 3 Death Orbs.
- Pressing Z casts **Surge** at once. With full orbs it restores 15/25/45 % MP and cuts **all current cooldowns by 10/30/50 %**.
- It also grants a 30 s buff: +12 % move and attack speed, +16/32/48 % attack power for 1/2/3 orbs.
- Remaining Energy "skips Death Trance, quickly fills orbs, and uses Surge repeatedly as part of the normal rotation" ([Maxroll class overview](https://maxroll.gg/lost-ark/resources/class-overview)).
- Community guide (Sep 2025): Surge produces "one massive damage number every 6 – 10 seconds."

| # | Skill | Job | Input type | Base CD | Stagger / flags | Lock | Sets up / pays off |
|---|---|---|---|---|---|---|---|
| 1 | Soul Absorber | **Builder** + damage | **Charge** (358 uncharged vs 1,431 overcharged at level 1) | 24 s | Mid, weak point 1, back attack | Charge time not published | Maxroll: "your biggest meter generators, make sure you don't miss them" |
| 2 | Void Strike | Builder + damage | **Charge**: release midway for the base attack or hold for the overcharge | 27 s | Mid (High with Concussion), back attack | Not published | "Dark Dimension" tripod → holding (up to 1.5 s) that pulls enemies in |
| 3 | Death Sentence | Damage | Normal (leap 4 m + stab) | 18 s ([Codex](https://lostarkcodex.com/us/skill/25080/)) | Mid, back attack | Not published | Main damage-gem skill |
| 4 | Twin Shadows | Damage | **Chain**: can be used once more within 4 s | 12 s | Mid, back attack | Short | Recast window = timing |
| 5 | Turning Slash | **Synergy** + damage | Normal ("Point Slash" tripod → combo with steering) | 15 s | Mid, back attack | Not published | Party damage-taken debuff. In the 2026 "Fatal Wave" variant it resets Fatal Wave's cooldown |
| 6 | Maelstrom | **Buff** | Normal (6 s tornado around her) | 30 s | — | Short cast | Raises meter gain, attack and move speed. "Keep Maelstrom active" across the whole cycle; Surge with about 1.5 s of buff left |
| 7 | Spincutter | **Mobility** (filler with a bleed rune) | **Combo** ×2 (×3 with "Triple Spin") | 12 s | Back attack, paralysis immunity | Short | Community guide: use it in the rotation only if running Bleed, "otherwise keep it for mobility" |
| 8 | Earth Cleaver | **Counter / stagger / part break** flex + filler | Normal | 16 s | Mid (Mid-high with Concussion), part break 1, counter | Not published | Maxroll: Concussion and Darkness Injection "trade damage for Stagger" |
| T | Deathly Slash | T skill | Point dash | 60 s ([Codex](https://lostarkcodex.com/us/skill/25440/)) | — | "short animation lock" (community guide) | Cast before Surge to benefit from the cooldown cut |

**Rotation in plain words**
- **Jan 2023 (pre-Ark Passive)**, attributed to a Korean top player ([community post](https://routesociety.pages.dev/posts/lost-ark-how-to-play-deathblade-the-nerd-stash1979799)):
  1. Spincutter ×3
  2. Maelstrom
  3. Soul Absorber
  4. Void Strike
  5. Blitz Rush
  6. Awakening
  7. Surge at 3 orbs
  8. repeat
- **2026 Maxroll**: a continuous loop that "ends each cycle with Surge". In the Fatal Wave variant, the key goal is to cast Fatal Wave twice per cycle by resetting it with Turning Slash.

**Peak**: Surge, every 6–10 s. **Between peaks**: chain, combo and charge skills, all of which fill orbs, inside the Maelstrom buff, from behind the boss.

**APM.** No number published. My base-cooldown count for the eight: about **28 casts per minute**, about 40 key presses with chain and combo recasts. Add 6–10 Surges per minute, each cutting current cooldowns by up to 50 %, so the real rate is clearly higher; I could not quantify how much.

**Slots doing no rotation damage: about 2** (Maelstrom = buff, Spincutter = mobility). Earth Cleaver is a counter/stagger flex that also deals damage. Turning Slash is synergy plus damage. The Fatal Wave variant puts the counter on Head Hunt instead.

**What the identity does.** The gauge is the metronome: build orbs → Surge (which cuts cooldowns) → the same skills return sooner → build orbs. The player plans around Maelstrom's 6 s buff and orb count, not around boss windows. This is the opposite of Igniter.

**Skill expression**
- **Back attacks**: "Easy to whiff and miss back attacks" is listed as a con.
- **Timing**: "try sneaking in a back attack in the 2 seconds" window after entering the identity (2023 guide).
- **Charge timing**: overcharge vs early release.
- **Recast windows**.
- **Not pre-casting** Turning Slash when the boss is not reachable, because it does not refresh the back-attack synergy.

---

## 3. Tripods, engravings, boss windows

### 3.1 Tripods that change a skill's job (2022 wiki data unless noted)

Lost Ark's tripods unlock at skill levels 4, 7 and 10. A 2022 design analysis describes the tiers as moving from "numeric tuning" (cooldown, mana) to "effect" changes to "core mechanic" changes, such as "decreasing/canceling casting requirements" ([Deconstructor of Fun](https://www.deconstructoroffun.com/blog/2022/5/16/the-lost-ark-has-found-its-way)). Examples where the *use* changes:

| Skill | Tripod | Change of job / input |
|---|---|---|
| Perfect Shot (Gunslinger) | **Perfect Aim** | Holding skill with a Perfect Zone → **1 s casting skill that always counts as a Perfect Zone hit**. Timing skill becomes a reliable one |
| Soul Absorber (Deathblade) | **Power Stab** | Charge → **Normal (instant)**, wider, more crit damage |
| Blitz Rush (Deathblade) | **All-round** | Charge → Normal, "always Overcharged" |
| Explosion / Punishing Strike (Sorceress) | **Backfire** / **Unavoidable Fate** | Casting → Normal with a short cast and a new shape (cone on the spot / diagonal lightning line) |
| Void Strike (Deathblade) | **Dark Dimension** | Charge → **holding up to 1.5 s that pulls enemies 7 times**, then overcharge. Becomes a grouping / area tool |
| Spincutter (Deathblade) | **Triple Spin** / **King Spin** | +1 recast / combo becomes one 8 m dash. Same slot, different mobility |
| Turning Slash (Deathblade) | **Point Slash** / **Concentrated Attack** | Adds steering between spins / removes the movement entirely for +100 % damage (mobility → stationary damage) |
| Dual Buckshot (Gunslinger) | **In a Tight Spot** / **Recoil Evasion** | Removes the back-attack requirement (non-positional) / last shot throws her 6 m backward (damage → escape) |
| Earth Cleaver, Peacekeeper | **Concussion**, **Stun Effect** | Moves the skill into a stagger or stun role. Stun Effect on Peacekeeper costs +6 s cooldown |
| Spiral Tracker, Blaze, Inferno | **Heavy Duty**, **Charge Flame** | Skill gets **2 charges** |
| Frost's Call (Sorceress) | **Unstable Rule** / **Rule Enhancement** | Holding → instant 2 s zone / a toggled zone that drains MP |
| Target Down (Gunslinger) | **Semi-automatic Rifle**; current **High-Capacity Magazine** (Maxroll 2026) | Removes manual aiming, fires continuously / more shots, "significantly slower" |
| Doomsday, Explosion, Punishing Strike | **Magick Amplification** | Ties the skill to the gauge: extra damage inside the identity state |
| Twin Shadows, Void Strike, Rime Arrow | **Orb Control**, **Enlightenment** | Ties the skill to the gauge as a builder (+50–75 % orb gain; +30 % meter) |

The last two rows matter most for us: **tripods are how a skill joins or leaves the gauge loop**.

### 3.2 Two playstyles per class: what the second engraving changes

From Maxroll's class overview unless noted.

| Class | Playstyle A | Playstyle B | What changes |
|---|---|---|---|
| Gunslinger | Peacemaker: 3 stances, high APM | Time to Hunt: shotgun removed, rifle stacks → Rose Blossom | A whole stance removed. The swap buff turns into a stack-and-dump loop. Tempo: burstier, "long downtime". Dev: "lessens the burden" |
| Sorceress | Igniter: fill meter → burst window, low APM | Reflux: "Disables Arcane Rupture", +10/15/20 % damage and −3/6/10 % cooldown (Jan 2023 KR); meter used for Blink; high APM | Gauge spend deleted for a flat bonus. Tempo flips from burst to constant casting while moving |
| Deathblade | Surge: Death Trance, build 60 stacks → one big Surge | Remaining Energy: skip Trance, Surge "repeatedly as part of the normal rotation" | Same gauge, different spend size: one big dump vs many small ones |
| Striker | Esoteric Flurry: many skills spending 1 orb each | Deathblow: each skill spends all orbs | Spend granularity |
| Soulfist | Robust Spirit: jump to level 3, big burst, long exhaustion, low APM | Energy Overflow: stay in Hype, constant attacks, high APM | Burst vs sustain; APM flips |
| Glaivier | Pinnacle: both stance buffs merged, low APM | Control: one stance locked, meter powers specific skills, high APM | Stance system simplified one way or the other |
| Breaker ([official article, Mar 2024](https://playlostark.com/en-gb/news/articles/breaker-breakdown)) | Brawl King: burst that "blasts all of its damage in just a few seconds" | Asura: gauge fills "on cast instead of on hit"; "fast-paced non-stop action" | Fill rule changes (on hit vs on cast); burst vs sustained |

**General pattern.** The second engraving changes one of four things:
- the **spend size** (one big vs many small)
- the **fill rule**
- whether a **subsystem exists at all**
- the **tempo**

Maxroll's labels show the pair often splits into a low-APM and a high-APM version of the same kit.

### 3.3 Boss side: windows, stagger, counters (kept general)

- **Counters.**
  - A boss attack glows blue; you must face the boss and hit it during the glow with a skill tagged "Counter: Yes". No skill is literally named "counter".
  - On success you take no damage and the boss goes down for "a few seconds". Every class has at least one, for example Deathblade's Head Hunt and Earth Cleaver, Sorceress's Squall, Gunslinger's Last Request and Peacekeeper. ([MeinMMO, Feb 2022](https://mein-mmo.de/en/lost-ark-how-to-counter-attacks-from-bosses,787486))
  - A Dec 2024 Steam thread (search snippet only) reports counters failing when pressed while spamming other skills.
- **Stagger.**
  - Bosses have a stamina bar; emptying it leaves them "vulnerable for a few seconds". Long-charging and long-lasting attacks contribute most. ([Gamepressure, Mar 2022](https://gamepressure.com/lost-ark/stagger/zbf7f8))
  - Stagger grades are labels (Low → Mid → Mid-high → High); no numeric values were found.
  - **Stagger checks** are deadlines. Search-result summaries mention a 6 s check in Vykas P3 and a wipe on failure in Ivory Tower G3; I did not open those guides.
  - Builds keep a separate "Stagger Check Rotation" (Maxroll, all three kits). Players cover stagger by swapping a tripod (Concussion), swapping one skill (Last Request, Earth Cleaver), or using a battle item (Whirlwind Grenade). They rarely keep a permanent stagger-only slot.
- **Damage windows of different lengths.**
  - Burst builds are flagged "requires raid knowledge": Igniter, Robust Spirit Soulfist. Their window must line up with the boss being hittable, and the Lostarkfire guide warns half a second can lose a skill.
  - Sustained builds (RE Deathblade) instead lose damage when the boss turns or moves, because of positional bonuses. The Breaker article notes Asura's 4–6 s punch string needs the boss to stay in front, and some bosses are "particularly unfair for frontal attackers".
  - How players fit skills into windows: guides say "improvise" when the fight breaks the order (Maxroll) and treat the rotation as a priority list (Mokokompendium).
  - **My inference, not stated as a rule by a source**: the long-lock skills (Target Down's three aimed shots, casting nukes) go into the long windows, and the 6–15 s fillers, charges and delayed-impact skills fill the short gaps.

---

## 4. Patterns and comparisons

### 4.1 Patterns seen across the three kits

1. **Cooldown ladder.** Fillers at 6–15 s (Dexterous 6, Spiral Tracker 8, Blaze 10, Twin Shadows / Spincutter 12), mid skills at 16–24 s, heavy hitters at 27–36 s, a T skill at 60–75 s, and an awakening above that. Peaks come from the top rungs.
2. **The gauge spend compresses cooldowns.** Igniter −25/50 %, Deathblade −10/30/50 % plus a buff. The burst is "more skills sooner", which makes the window feel fast even for a low-APM class.
3. **Skills have gauge roles.** Builder tripods (Enlightenment, Orb Control), spender tripods (Magick Amplification), and a "do not use in window" rule (Frost's Call).
4. **Micro builder→spender loops inside the kit.** Quick Scope stacks → Rose Blossom. Pistol skills → Rifle Cooling. Maelstrom → orb gain → Surge.
5. **Delayed hits and pre-casting.** Doomsday cast before the window; Punishing Strike and Lava Blast hit late; the T skill goes before Surge. Timing skill without speed.
6. **Utility rides on damage skills.** Synergy, mobility and counter are mostly properties of skills that also deal damage, so pure dead slots are 0–2 of 8.
7. **3–5 input types per kit.** Normal, combo, chain, charge, holding, casting, point, Perfect Zone. Each type carries its own small skill test.
8. **Positional or aim tests with an opt-out.** Back attacks, direct hits and the Perfect Zone reward skill, and tripods exist to remove them (In a Tight Spot, Perfect Aim, Semi-automatic Rifle).
9. **A short-cooldown "keep it up" skill.** The synergy debuff (Spiral Tracker, Dexterous Shot, Turning Slash, Blaze) gives a reason to press fillers on time even when damage is low.
10. **Stagger and counter are tags plus tripods, not a separate kit.** A grade on every skill; Concussion raises it one step. Stagger checks are met by swapping tripods or one skill, or with consumables.
11. **The rotation is a priority list, not a script.** Guides say so explicitly (Mokokompendium, Maxroll "improvise", Lostarkfire "isn't set in stone").
12. **Two playstyles = same kit, different tempo or APM.** Spend size, fill rule or a deleted subsystem (3.2).

### 4.2 Against our kit (current eight skills + focus)

| Pattern | Our kit today | Gap |
|---|---|---|
| Cooldown ladder (1) | All eight at 5–12 s | No heavy rung and no T-skill rung. Every press weighs the same, so there is no peak. Biggest structural gap |
| Spend compresses cooldowns (2) | Focus = ×1.5 damage for 8 s | The window does not change *what* you press or how often. No cooldown cut, no cast-speed change, no skill unlocked only inside it |
| Gauge roles (3) | Focus fills from any damage | No builder or spender skills, no "don't use it in the window" rule. Every skill is the same to the gauge |
| Micro loops (4) | None | No stack-and-dump, no skill that speeds another up |
| Delayed hits / pre-cast (5) | The thrown explosive and the 1 s cast are close | Nothing lands *later* than it is pressed, so you cannot pre-cast into the focus window. Small change, big payoff |
| Utility on damage skills (6) | Backward hop that shoots = mobility + damage | No counter, no stagger tags, no "keep-up" debuff. (Counter and stagger are planned for bosses; the evidence says put them on existing damage skills or tripods, not new dead slots) |
| Input variety (7) | Covered: tap, point, hold, charge, cast, cone | Missing: **recast / chain window** and a **Perfect Zone**. These are the two Kristian named. The three-shot burst could become a chain (recast within ~4 s) |
| Positional / aim test (8) | Cursor aim on everything | Aim exists, but there is no bonus for precise aim (direct-hit centre, weak spot) and no positional bonus |
| Keep-up skill (9) | None | A short-cooldown mark on the enemy (+damage taken) would give fillers a timing job |
| Tripods change jobs (3.1) | Planned | Plan upgrades that change input type, add charges or recasts, move a skill in or out of the focus loop, or convert damage → stagger. Not just +X % |
| Two tempos (12) | One class, no variant | A late-run "keystone" upgrade could flip spend size or tempo (many small focus spends vs one big one). This is how Lost Ark does it |

**Every skill locks her until its last hit.** Lost Ark has cancels: stance swap cancels Sharpshooter, Deathblade's T skill has a "short animation lock" chosen for that reason, and Bullet Rain has a cancel-related tripod. Without a weapon swap we have no cancel button. A dash or dodge that cancels a skill is the obvious stand-in. The evidence on how Lost Ark handles cancels is thin (one wiki line, one guide line).

**Where the evidence is thin.** Our kit is known only from the brief. Lost Ark lock times are not published. Cooldowns are base values. The "short skills in short windows" rule is my inference.

### 4.3 Against Kristian's taste

- **High APM costs effort, and the developer has said so.** Smilegate (Jan 2023): Gunslinger stance swapping in fast combat "led to increased difficulty". The TtH change "lessens the burden on the player". Maxroll lists "High APM" as a **con** for every high-APM build (PM, RE Deathblade, Reflux, Energy Overflow, Control Glaivier, Taijutsu Scrapper, First Intention Wardancer), and "strict rotations" as a con for Wardancer ESE.
  - The other side exists too: Steam players say they "love the High APM playstyle" of Striker speed builds.
  - Lost Ark's answer is not to lower APM but to **offer both tempos** inside one class.
- **The low-APM burst class is the one that needs boss knowledge.** Igniter and Robust Spirit Soulfist carry "requires raid knowledge". In a roguelike where bosses change between runs and the player sees each boss less, a long, rare burst window may feel worse than in Lost Ark. A shorter, more frequent window (like Igniter's 35–40 % variant) fits a run better. This is my inference.
- **Situational slots cost damage, and players minimise them.** Maxroll describes the stagger tripods as trading damage for stagger. Guides keep a separate stagger rotation and swap skills per raid rather than slotting a stagger skill permanently. A battle item (grenade) covers stagger so nobody gives up a slot. A forum thread (search snippet only; the page did not load) calls a mobility-only skill a "wasted slot".
  - Kristian wants situational skills, but the evidence says they are liked when they **also deal damage** (counter on a damage skill, stagger on a tripod) and disliked as dead slots.
  - In a roguelike, offering a counter or stagger *upgrade* only when a boss needs it may suit better than a fixed slot.
- **Positional demands frustrate players when bosses move.** Breaker Asura: some bosses are "particularly unfair for frontal attackers". Deathblade: "easy to whiff and miss back attacks". Our class is ranged and cursor-aimed, so aim-precision bonuses are a safer test than back-attack bonuses.
- **Repetition.** One design analysis says Lost Ark boss fights feature "repetitive mechanisms" early on. A Hades-style run with varied bosses can partly avoid this.

---

## 5. What I could not find or verify

- **Animation lock times**: none published for any skill. The only published durations are Bullet Rain 2 s, Target Down aim up to 2.5 s × 3, Void Strike's Dark Dimension hold up to 1.5 s, the Twin Shadows recast window of 4 s, and Frost's Call ticking up to 4 s.
- **Endgame cooldowns** after gems, Swiftness and Ark Passive: not found. All cooldowns here are base values.
- **Current tripod values**: the tripod tables are from 2022 wiki pages, and tripods have since been reworked. Effects named in 2026 guides are used where available.
- **Exact eight-skill bars**: Maxroll, the main current source, shows the bars and rotations as images that could not be read. The PM, TtH and RE bars are assembled from role lists and skill levels. The guides differ on 1–2 slots.
- **Gauge numbers**: the Arcane Rupture duration (15 s, unverified), the minimum threshold (30 %, unverified), the Peacemaker swap-buff duration, and the window frequency in seconds were not found.
- **APM**: no published numbers. Mine are base-cooldown arithmetic, which excludes basic attacks, dodges and cooldown reduction.
- **T skills**: "Seven Shotguns" (PM) and "Bullseye" (TtH) are inferred to be Gunslinger T skills. Not verified.
- **Stagger numbers and post-stagger stun length**: only labels and "a few seconds".
- **Blocked or failed sources**: Icy Veins, Mobalytics, the Lost Ark Fandom wiki, NamuWiki, Reddit (and its mirrors) and the official forum all refused automated access. I relied on search-result snippets only where marked.
- **Korean sources (Inven)** were not reached.

---

## 6. Sources

- [Maxroll – Peacemaker Gunslinger raid guide (upd. 13 Jun 2026)](https://maxroll.gg/lost-ark/build-guides/peacemaker-gunslinger-raid-guide) – PM roles, stance buffs, pros/cons, gem priority.
- [Maxroll – Time to Hunt Gunslinger raid guide (upd. 13 Jun 2026)](https://maxroll.gg/lost-ark/build-guides/time-to-hunt-gunslinger-raid-guide) – Quick Scope, Rose Blossom at 20, Rifle Cooling, TtH roles.
- [Mokokompendium – Gunslinger guide (upd. 20 Oct 2026)](https://mokokompendium.pages.dev/guides/classes/gunslinger) – current PM skill levels and tripods, priority rotation, "swap back after dodge".
- [Lostarkfire – PM Gunslinger build (Mar 2023)](https://www.lostarkfire.com/build/raid-pve-peacemaker-gunslinger-160) – older eight-skill bar, "rotation isn't set in stone".
- [Maxroll – Igniter Sorceress raid guide (upd. 22 Sep 2026)](https://maxroll.gg/lost-ark/build-guides/igniter-sorceress-raid-guide) – window effects, builders/spenders, rotation, 35–40 % variant.
- [Lostarkfire – Igniter build (Feb 2023)](https://www.lostarkfire.com/build/raid-pve-igniter-sorceress-154) – eight-skill bar with roles, build/burst phases, half-second note.
- [Maxroll – Remaining Energy Deathblade raid guide (upd. 13 Jun 2026)](https://maxroll.gg/lost-ark/build-guides/remaining-energy-deathblade-raid-guide) – Surge effects, skill roles, Fatal Wave variant, stagger tripods.
- [Deathblade Community Guide (Google Doc, Sep 2025)](https://docs.google.com/document/d/1ZLOhLB9bb9AN_Q7_eKukUnqI5E5w5Fb1oqryIA1ml8A/mobilebasic) – slot roles, Surge every 6–10 s, timing tips.
- [Community post on Deathblade rotations (Jan 2023)](https://routesociety.pages.dev/posts/lost-ark-how-to-play-deathblade-the-nerd-stash1979799) – written RE and Surge rotations, back-attack window.
- [Maxroll – Class overview](https://maxroll.gg/lost-ark/resources/class-overview) – identity and both engravings for 10 classes, APM labels.
- [Maxroll – KR balance patch Jan 2023](https://maxroll.gg/lost-ark/news/january-2023-korean-class-balance-patch) – developer comments on Gunslinger difficulty, Reflux numbers.
- [Official – Breaker Breakdown (Mar 2024)](https://playlostark.com/en-gb/news/articles/breaker-breakdown) – fill-on-cast vs fill-on-hit, burst vs sustained, frontal-boss complaint.
- [MeinMMO – countering bosses (Feb 2022)](https://mein-mmo.de/en/lost-ark-how-to-counter-attacks-from-bosses,787486) – how counters work, counter skills per class.
- [Gamepressure – Stagger (Mar 2022)](https://gamepressure.com/lost-ark/stagger/zbf7f8) – stamina bar, stagger checks, "few seconds" vulnerability.
- [Deconstructor of Fun – "The Lost Ark Has Found its Way" (2022)](https://www.deconstructoroffun.com/blog/2022/5/16/the-lost-ark-has-found-its-way) – tripod tiers numeric → effect → mechanic; repetitive bosses.
- Fextralife wiki skill pages (2022), used for base cooldown, input type, stagger and tripods:
  - Gunslinger: [Focused Shot](https://lostark.wiki.fextralife.com/Focused+Shot), [Target Down](https://lostark.wiki.fextralife.com/Target+Down), [Dual Buckshot](https://lostark.wiki.fextralife.com/Dual+Buckshot), [Perfect Shot](https://lostark.wiki.fextralife.com/Perfect+Shot), [Catastrophe](https://lostark.wiki.fextralife.com/Catastrophe), [Bullet Rain](https://lostark.wiki.fextralife.com/Bullet+Rain), [Spiral Tracker](https://lostark.wiki.fextralife.com/Spiral+Tracker), [Dexterous Shot](https://lostark.wiki.fextralife.com/Dexterous+Shot), [Equilibrium](https://lostark.wiki.fextralife.com/Equilibrium), [Peacekeeper](https://lostark.wiki.fextralife.com/Peacekeeper), [Last Request](https://lostark.wiki.fextralife.com/Last+Request), [Spiral Flame](https://lostark.wiki.fextralife.com/Spiral+Flame)
  - Sorceress: [Doomsday](https://lostark.wiki.fextralife.com/Doomsday), [Explosion](https://lostark.wiki.fextralife.com/Explosion), [Punishing Strike](https://lostark.wiki.fextralife.com/Punishing+Strike), [Rime Arrow](https://lostark.wiki.fextralife.com/Rime+Arrow), [Frost's Call](https://lostark.wiki.fextralife.com/Frost's+Call), [Inferno](https://lostark.wiki.fextralife.com/Inferno), [Seraphic Hail](https://lostark.wiki.fextralife.com/Seraphic+Hail), [Esoteric Reaction](https://lostark.wiki.fextralife.com/Esoteric+Reaction), [Blaze](https://lostark.wiki.fextralife.com/Blaze), [Squall](https://lostark.wiki.fextralife.com/Squall)
  - Deathblade: [Soul Absorber](https://lostark.wiki.fextralife.com/Soul+Absorber), [Void Strike](https://lostark.wiki.fextralife.com/Void+Strike), [Twin Shadows](https://lostark.wiki.fextralife.com/Twin+Shadows), [Turning Slash](https://lostark.wiki.fextralife.com/Turning+Slash), [Maelstrom](https://lostark.wiki.fextralife.com/Maelstrom), [Spincutter](https://lostark.wiki.fextralife.com/Spincutter), [Earth Cleaver](https://lostark.wiki.fextralife.com/Earth+Cleaver), [Blitz Rush](https://lostark.wiki.fextralife.com/Blitz+Rush)
- Lost Ark Codex (datamine, site updated Oct 2026), used to check cooldowns and newer skills: [Rose Blossom](https://lostarkcodex.com/us/skill/38340/), [Sharpshooter](https://lostarkcodex.com/us/skill/38110/), [Death Sentence](https://lostarkcodex.com/us/skill/25080/). The pages for Focused Shot, Target Down, Dual Buckshot, Lava Blast, Frost's Call and Deathly Slash were seen through search-result extracts only.
- Search-result snippets only (pages blocked or not opened, flagged where used): the Reddit APM thread (Jan 2025), the official forum "extra row for skills" thread, the Steam counter thread (Dec 2024), Steam "Any Striker mains" thread, the PC Gamer Sorceress guide (15 s window), and the Nexus guide (Rose Blossom on the stance wheel).
