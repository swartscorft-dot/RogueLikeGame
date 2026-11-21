# 0014 Weapon attachments: what lasts between runs, fitted to her pistols

Status: **the producer's proposal, 2026-11-21. The idea is Kristian's (2026-11-20). On 2026-11-21 he ticked "Våpen-attachments" when asked what to build next. It was offered to him as built switched off by default, together with what lasts between runs (decision 0009, a trial that is also off by default). The slots, the parts, the numbers and how they are earned are the producer's proposal. He has not seen them.**

## What he said

After playing, 2026-11-20: "en ide til metaprogresjon, kan være attachments til våpen. laser sikte = bedre range, osv. noe å ta med til senere." (an idea for meta progression could be attachments for weapons. laser sight = better range, and so on. something to take along for later). Decision 0012 records the same words under "For the between-runs design".

## What follows

1. **Attachments live where decision 0009 lives:** on the one screen between runs, paid with its one currency (Echoes). They are not a second economy.
2. **His example is a sight that helps reach.** It is kept, and it gets a cost, so that it is a choice and not just more power.
3. **The pillars set the limits.** Meta "mostly opens new choices, plus some raw stats in noticeable, capped steps". It must not be "permanent upgrades of a few percent that cannot be felt". The anti-goals say "no gear or loot with stats". So an attachment is never dropped, never random and never rolled. It is a fixed, named part, bought once, fitted between runs, and felt from the first shot.
4. **An attachment must not copy a chest step.** A chest already offers reach (Longer barrel, +1 m, at most 3), attack speed, flat damage and crits (work order 050). A plain "+range" sight would just be Longer barrel bought for good.

## The producer's proposal

**Her pistols get three slots: sight, barrel and grip, one part in each.** An empty slot is the stock gun, which is the gun as it is today. A magazine slot is left out: the game has no ammunition or reloading, so a magazine would need a mechanic first. Each slot asks one question about how she fights:
- the sight: how far against how forgiving;
- the barrel: how fast against how heavy;
- the grip: standing against moving.

The parts act on the basic shot only, as attack speed does (0012). The skills are not touched.

| Slot | Part | Price | What it does | What it costs |
|---|---|---|---|---|
| Sight | **Laser Sight** | 30 | A thin line in her colour runs from the gun along her aim and shows where the basic shot would end. It ends on the first enemy it would hit (a brighter dot there), at the cover that would stop it, or at the end of its reach. Reach goes from 10 to 12 m. | The shot's line narrows from 1.0 to 0.7 m wide (half width 0.5 to 0.35), so she must aim truer. |
| Sight | **Ghost Ring** | 30 | The shot's line widens to 1.6 m (half width 0.8). This forgives at close range and against swarmers. | Reach goes from 10 to 8 m. |
| Barrel | **Bull Barrel** | 40 | Each basic shot hits 35 % harder (40 to 54) and staggers 3, as Quick Shot does. | The shot is 35 % slower: 0.225 to 0.30 s between shots (0.265 to 0.357 s at the slower start of 0012). Marks from basic shots come later in time. |
| Grip | **Runner's Grip** | 50 | Shooting on the move slows her to 75 % of run speed, not 50 % (4.5 m/s, not 3). The backpedal cap also rises to 75 %. | A basic shot fired on the move deals 20 % less (40 to 32). |

**Why these four.** Each one changes how she plays or what she can see, and none is a flat percentage:
- The laser is his own example, and it is a way to read the shot. By the producer's arithmetic from the camera's numbers, the view ends about 8 m below her on the screen, so today's 10 m reach already runs past that edge. The line shows how far it goes.
- The two sights pull opposite ways, so the first slot is a real choice.
- The barrel is the "rate for weight" trade the brief named. It is not range, because range is the sight's question.
- The grip answers what he has asked for twice: to stay fast while shooting (0005: Triple Tap "does not slow her"; work order 040).
- Every number is a first guess.
- All four are built from five knobs: reach, line width, damage and interval, stagger, and speed on the move. Later parts can then be one line of data each.

**How they are earned: Echoes, as 0009 has them.** A run pays for each floor cleared (10), each elite room (15) and the act boss (100), and nothing is lost on death. While attachments are on, a run also pays **3 for each ordinary combat room cleared**, so a run that dies early still brings something home. Prices are set so that a first run that clears floor 1 can buy a sight. That is about 10 + 4 × 3 + 15 = 37, the producer's arithmetic for a floor of four combat rooms and an elite room. Buying a part fits it at once, so one click on the screen is felt in the next run. The whole set costs 150, about three runs. The rack's ten pieces cost about 830.

**How they are chosen and shown.** The screen between runs gets a second panel to the right of the rack: the weapon. Each slot has a heading, its parts in rows, and what is fitted. The panel uses the same panel, rows and keys as the rack (arrows and Enter or a click: buy, fit, take off), in the kinetic skin and in the old look alike. The end screen adds one line: what the run's Echoes can buy now, or how many more the next part needs. In a run, the pause menu's stats list names the parts fitted and shows reach and rate as they make them.

**Stored** in the same `between-runs.json`, in two lists (owned and fitted). A save with no attachment is written exactly as today. Tests only ever use a temporary save path, as `BetweenRunsTests` does, and check that the real file is untouched.

**Power and skill.** Every part keeps the basic shot's damage per second at a standing dummy within 5 % of the stock gun. Bull Barrel's +35 % hit and 35 % slower rate cancel out. A test pins this for each part. No part touches a timing window (the clean press, the perfect band, the recast window, the dash), health or a skill. So a tenth-run player has more ways to play, not more power from her gun. The only raw power between runs stays 0009's two capped stat pieces. Decision 0009 says why that matters: "If the game cannot be won on an empty rack, the stat steps stop being a choice and become a requirement. The act should be winnable with nothing equipped, by a good player." The floors do not scale with what she owns. The test pilot flies with the stock gun, and every balance pass is tuned against it.

**Off.** One switch, off in code, with its own line in the pause menu ("Weapon attachments (trial)"). Turning the line on turns the between-runs trial on too. Off, nothing is new: no weapon panel, no room pay, no line on the end screen, and her shot is as it is. The save keeps any parts already owned and does not apply them. The pause menu's line is the only trace.

## Considered and not chosen

- **Laser sight as plain +range.** This is his literal words, but it would be the chest's Longer barrel made permanent and free. The line it draws, and the narrower shot, turn it into a choice.
- **Attachments as rack pieces that cost points.** Rejected because the slot already gives the "own more than you can fit" of 0009, and a sight competing with Steady Start for points reads oddly. Whether the whole rack should become the weapon is left open (below).
- **Attachments found in runs, or with random stats.** This is the anti-goal "no gear or loot with stats".
- **A magazine slot.** There is no ammunition to change.
- **Parts for the shotgun or the skills now.** That doubles the first order. They are listed as later orders.
- **A free first part.** Rejected because it is a gift, not something earned. The room pay covers the early death instead.

## Later orders, if he likes the first

1. More parts in the three slots: a short barrel (faster, lighter), and a bore that passes through the first enemy to the next for half damage.
2. A fourth slot, "rounds": every third basic hit marks, not every fifth, for less damage.
3. A slot for the shotgun: a choke for Scatter Blast, narrower and longer.
4. Parts reaching the pistol skills (Quick Shot, Triple Tap, Backstep).
5. Fitting in the practice room, to try a part before a run.
6. The parts drawn on her gun (art).

## Open, for Kristian

1. Is an attachment "gear with stats", which the anti-goals rule out? The producer reads it as not: it is fixed, named, bought and never dropped.
2. Should every part cost something, as here, or may some be plain gains (a laser that only adds range)?
3. Should the parts later reach her skills and the shotgun, or stay on the basic shot?
4. Should the rack of 0009 become the weapon: every piece a part in a slot (0012: attachments "give them a face")?
5. The pace: the whole set in about three runs. Faster or slower?
