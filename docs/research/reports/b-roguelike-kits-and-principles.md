# Research brief B: kits that change during a run, and what developers say about rotation design

Researched 2026-11-19. Every claim below comes from a page opened during this research, with links in section 6. "Developer" means someone who worked on the game. "Wiki" or "guide" means a third-party page, which may be out of date. Where a page could not be opened and only a search snippet was seen, the claim is marked **(snippet only)**.

Two terms used throughout:
- **Rotation:** the abilities you press often, in roughly repeating order.
- **Builder/spender:** some buttons fill a resource and others empty it for a payoff.

---

## 1. Summary: the ten most useful findings

1. **The three roguelikes keep a small, fixed set of inputs and change what those inputs do.** Hades has five inputs (attack, special, cast, dash, call), and the run never adds a button. Risk of Rain 2 has four skill slots that stay constant. Shape of Dreams starts with a basic attack, two skills and a dodge, and ends with at most four skills. None of them starts or ends at eight active skills.
2. **The upgrades that change how a game is played do one of a few things.** They change an input's shape or range (Spread Fire turns the Hades rail gun into a short-range spread). They remove or add a management task (Delta Chamber removes reloading). They add charges (Backup Magazine, Essence of Virtue). They tie two buttons together (Essence of Momentum: each basic attack shortens a skill's cooldown). Or they turn a press into an automatic reaction (Essence of Paranoia). Flat damage boosts (Pom of Power, Urge to Kill, Essence of the Giant) change nothing about play.
3. **Choices come as one of three, tied to a slot, with exclusions.** Hades offers three boons per god, and each of the five inputs holds one god's boon, so a new boon replaces the old. Hammer upgrades exclude each other (Spread Fire excludes Flurry Fire, for example), and a run gets about two of them. This structure, more than the size of the pool, is what keeps builds from converging.
4. **Blizzard's long-time lead class designer put the core rotation at "about four" buttons** and said that even after pruning there were "still probably too many". Blizzard pruned abilities in 2014 and again for WoW: Midnight, and in 2019 its director said the 2014 pruning had been overdone (paraphrased, not verbatim). The right number is disputed. Every source agrees that buttons nobody presses should go.
5. **Path of Exile 2's director argues the other way for action games.** He said Path of Exile 1 was "a very one-button build kind of game" and that the sequel needs players "to use many skills together and combos". The disagreement is real, and Kristian's taste sits on Path of Exile 2's side.
6. **A shared burst window makes every kit feel the same.** Final Fantasy XIV's director said aligning all buffs to a 120-second window made "the job rotations extremely similar, and I don't think that's a good thing." Our focus gauge (8 s at 1.5× damage) sits close to that pattern: a flat multiplier that every skill wants to be saved for.
7. **A payoff should change the kit, not only multiply it.** Smilegate describes the Guardianknight burst state as one where "resource restrictions are completely lifted, and her skills fully evolve". It also says the "minor stress" of managing resources beforehand is intentional. Hades II's Omega moves are held versions of the normal buttons that cost Magick. Both change what the buttons do; our focus only scales numbers.
8. **Commitment (being locked into an animation) should be paid for.** God of War's former lead combat designer used the big, uncancellable special attacks "to teach the concept of risk vs. reward", and said cancel rules decide whether a game feels "lagged-out" or "crisp". Our kit locks the character on all eight skills, with a dash on a long cooldown.
9. **Enemies are what make a rotation change from fight to fight.** id Software's Doom Eternal designers: "the guns are the tool and the AI is the problem", "Each AI will push you in different ways", and "every gun, upgrade, and demon is meant to have a purpose". With one enemy type, our kit cannot yet show whether its rotation varies.
10. **Breaking the game late in a run can be a design goal, provided it is earned.** Shape of Dreams' CEO said its skill and modifier systems "are meant to break the game. In our opinion, breaking the game is fun", but such a build "has to take a reasonable amount of effort to achieve." Blizzard and Path of Exile 2 aim for balance instead ("we don't just have, you know, two dominant builds").

---

## 2. Subject 1: how roguelikes let a run change the way a kit is played

### 2.1 Hades (2020) and Hades II (1.0 released 25 Sep 2025)

**Starting kit (Hades).** Per Wikipedia's gameplay summary:
- A primary attack and a "special", both depending on the weapon.
- A ranged "cast".
- A dash with brief invulnerability.
- A "call" that summons a god's power.

That is five inputs, plus a dash-strike (attack while dashing). Each weapon also has its own quirks. The Adamant Rail, a gun, fires up to 12 shots, then needs a reload, and its special lobs a bomb (Fextralife wiki). The basic loop is to attack, use the special on a short timer, cast, and dash out of telegraphed attacks.

**Starting and ending buttons.** Five at the start and five at the end. Boons and hammers change what the five do and never add a button. Hades II adds held "Omega" versions of attack, special and cast, which cost Magick, plus a Hex, a spell that becomes usable after spending enough Magick in a room. That describes early access, per a GameLeap guide from May 2024, and the 1.0 details were not verified. So Hades II adds depth to the same buttons by holding them, not by adding new ones.

**How choices are offered** (teemo.dev reference built from the final Hades build, v1.38290 / Patch 057, citing the Hades Wiki and game data):
- A god's boon offer shows **three** options. Wikipedia also says "three".
- Attack, special, cast, dash and call each hold **one** boon. A different god's boon for a filled slot replaces it, and the slot's rarity goes up by one.
- Passive boons stack freely.
- **Duo and Legendary** boons only appear once you hold specific earlier boons. For example, a Duo needs one boon from each of its two gods.
- **Pom of Power** raises one boon's level, chosen from three. Each level adds less than the one before.
- **Keepsakes** force the next god offered. **Rerolls** (Fated Persuasion) reroll the three options.
- **Daedalus Hammer:** at most about two per run (one in an early region, one usually in Elysium), with a third only via a shop item. Hammer offers cannot be rerolled, and an upgrade that excludes one you already hold is never offered.

**Upgrades that change how you play** (effects paraphrased from teemo.dev and Fextralife):

| Upgrade | Type | What the player does differently |
|---|---|---|
| **Spread Fire** (Rail hammer) | Weapon hammer | The gun becomes a short spread with 6 fewer ammo, so you fight up close. Excludes Flurry Fire, Delta Chamber, Ricochet Fire and Seeking Fire. |
| **Delta Chamber** (Rail hammer) | Weapon hammer | Fires 3-round bursts and never needs reloading. Reload management, the gun's main rhythm, is gone. |
| **Triple Bomb / Rocket Bomb** (Rail hammers) | Weapon hammer | The special becomes three quick throws (a burst) or a fired rocket, a ranged, aimed shot instead of a lob. |
| **Aspect of Hestia** (Rail) | Weapon aspect, chosen before the run | Reloading manually empowers the next shot, so you reload on purpose instead of when empty. A timing decision that did not exist before. |
| **Aspect of Lucifer** (Rail) | Weapon aspect | A different weapon: a beam attack and Hellfire orbs replace the special. |
| **Aspect of Chiron** (Bow) | Weapon aspect | The special homes in on the last foe your attack hit, which creates a fixed order: attack first, then special. |
| **Flurry Shot** (Bow hammer) | Weapon hammer | Rapid fire with no charged "Power Shot". The timing skill is removed in exchange for volume. |
| **Crush Shot** (Aphrodite cast boon) | Boon | The cast becomes "a wide, short-range blast that inflicts Weak", so a ranged tool becomes a close-range one. |
| **Greatest Reflex** (Hermes) | Boon | "You can Dash more times in a row", so you reposition more aggressively. |
| **Marauder Wallop** (Hades II staff hammer, early-access text) | Weapon hammer | "Hold Attack to strike 50% faster, but cannot use your Omega Attack". The hold input is traded for speed. |

**Upgrades that only change numbers:** Urge to Kill (Ares: attack, special and cast "deal more damage"), Chaos's Strike blessing, Pom of Power levels, Minotaur Rush (+500% Bull Rush damage) and Greater Inferno (+250% Hellfire). A middle category is **conditional damage**. Point-Blank Shot (+150% within 330 units) and Sniper Shot (+200% beyond 675) change where you stand without changing which buttons you press.

**What the developers have said:**
- Greg Kasavin, creative director, 2019: "We really favor designs that let you experiment"; "maybe you get nudged into a new set of tools and abilities that is even better"; variety comes from "defining enough overlapping systems that can interact with each other" (Game Developer, 17 Jan 2019).
- Wikipedia's article cites a 2019 Gamasutra piece for Supergiant's aim to "continue to add in new tricks or tools for the player that would make them reconsider how they have been playing". The original piece was not opened.
- Alice Lai, technical designer, on new boons: the team writes "50-or-so ideas" per god and cuts those that are "too same-y or too complicated". One status-effect idea was dropped because "when we tried to break down how it fit in our combat flow it became too unwieldy" (Game Developer, 28 Feb 2020).
- Kasavin on the boon Rare Crop: "a way of trading off some of your current power level to get even stronger in the future" (same article).
- Kasavin on reworking boons in Hades II: "We think it's important not to be too precious about these things" (GamesRadar headline quote; the article body could not be read).

### 2.2 Shape of Dreams (Lizard Smoothie / NEOWIZ; released 10 Sep 2025; "Starless Path" update listed on Steam)

**Starting kit** (Shape of Dreams wiki.gg, Memories page). Skills are called **Memories**.
- Each character (a "Traveler") starts with an **Identity** Memory (a passive), a **Dodge** Memory and **two** character Memories, plus a basic attack.
- At most **four** ability Memories can be equipped, not counting Dodge and Identity.
- Normal Memories run on cooldowns. "Ultimate" Memories charge from dealing damage, killing or taking damage, which works like our focus gauge.
- Nine Travelers in total; two are unlocked at the start.

The ranged starting Traveler, Lacerta, uses firearms. Her two Identity options per a third-party wiki (bloxodes):
- **Salamander Powder:** every 4th basic attack crits and explodes.
- **Double Tap:** after you cast a Memory or dodge, the next basic attack fires twice.

Sources disagree on Lacerta's details, and her starting Memories were not verified. Keybindings were not found.

**Starting and ending buttons.** About four active inputs at the start (basic attack, two Memories, dodge) and six at the end (basic attack, four Memories, dodge). The cap is four slots. Variety comes from swapping Memories and stacking modifiers, not from adding buttons.

**How Memories and modifiers are gained** (wiki.gg):
- Clearing a combat room gives **one random Memory**.
- Some events offer **1 of 3** Memories. Late "Paradox" events offer 1 of 6 Memories and 6 Essences.
- Shops sell 3 Memories and 3 Essences.
- Bosses have a small chance of a "Unique" Memory or Essence.
- Rarity runs from Common to Legendary on a fixed roll table.
- **Essences** are modifiers that attach to a Memory, up to three each ("can be increased by Chaos").
- Memories and Essences "can be equipped, unequipped, swapped, and dismantled at any time".
- Some events reroll your equipped Memories (Mirror of Remorse) or trade one for a random Memory a rarity higher (Altar of Transcendence).

So choices are mostly **free-floating**, not tied to one skill: you decide which Memory an Essence goes on, and you can move it later.

**Upgrades that change how you play** (exact wiki.gg text unless noted):

| Upgrade | What the player does differently |
|---|---|
| **Essence of Momentum:** "Each time you attack an enemy, the cooldown of this Memory is reduced by 0.4" s | Basic attacks now feed a skill, so you weave basic attacks between casts instead of waiting. |
| **Essence of Virtue:** "The number of charges for this Memory increases by 1" | The skill can be cast twice in a row. You can hold it for a burst or spend charges freely. |
| **Essence of Opportunity:** "Casting this Memory reduces the cooldown of your Dodge by 3 seconds. Can only trigger once every 8 seconds." | A damage skill becomes part of your defence, so you cast it to get your dodge back. |
| **Essence of Dusk:** "Casting this Memory fires 2 Dusk shards … each time you attack an enemy for 5 seconds" | Casting opens a 5-second window that rewards fast basic attacks afterwards: a small builder-into-payoff loop. |
| **Essence of Paranoia:** "When you take any damage, this Memory gains 150 Memory Haste for 6 seconds … but it is automatically cast on nearby enemies" | A skill you aimed becomes an automatic reaction to being hit. You lose control in exchange for uptime. |
| **Undo** (a Memory): "Reduces the cooldown of the last used Memory by 5.5 … The next Memory cast within 8 seconds is empowered" | A sequencing skill: its value depends on what you pressed just before and just after. |
| **Double Tap** (Lacerta Identity, third-party wiki) | Every skill or dodge is followed by a basic attack, which interleaves the rotation. |

**Only numbers:** Essence of the Giant ("Increases Maximum Health by 270" plus a damage bonus that scales with health). Memory level from later worlds only shortens cooldowns.

**What the developers have said:**
- Eunseop Shim, CEO, Oct 2025: Memory and Essence systems "are meant to break the game. In our opinion, breaking the game is fun"; such a build "has to take a reasonable amount of effort to achieve"; "Repeating something is only fun until some point, so we need to surprise players." Named influences: "Lost Ark, Elden Ring, Path of Exile, Hades, and last but not least Faster Than Light", and "the biggest of all would be Heroes of the Storm and Risk of Rain 2" (Loot Level Chill).
- AUTOMATON interview, 20 Sep 2026 (Japanese, translated by me):
  - A player-found infinite-parry build was left in after players objected to its removal. The rule is roughly: "if building it is fairly difficult and it is fun enough, it's OK."
  - The power curve is meant to reach "a level that breaks the game balance".
  - Early runs feel like a tense roguelike, and late runs feel like a hack-and-slash power fantasy.
  - One late build triggers skills "about 20 times per second".

### 2.3 Risk of Rain 2 (Hopoo, now Gearbox; wiki pages read Nov 2026)

**Starting kit.** Four skill slots: Primary, Secondary, Utility and Special. Since the "Skills 2.0" update, each slot can be set to an alternate skill in a pre-run **loadout**. The jobs below are my reading of the wiki data. Hopoo's own statement of slot roles was **not found**.

Commando, who also fights with pistols (riskofrain2.wiki.gg):

| Slot | Skill | Job |
|---|---|---|
| Primary | **Double Tap** | Sustained damage, no cooldown, roughly 6 shots/s per the wiki |
| Secondary | **Phase Round** (3 s) | Piercing burst shot |
| Utility | **Tactical Dive** (4 s) | Mobility |
| Special | **Suppressive Fire** (9 s) | A volley that stuns and interrupts |

The base loop is to hold primary, fire secondary whenever it is ready, dive out of danger, and use the special when a big target or a crowd needs stopping.

**Starting and ending buttons.** Four skills at the start and four at the end. Items change their behaviour, and a few items replace a skill outright.

**Alternate skills and items that change how you play:**

| Upgrade | What the player does differently |
|---|---|
| **Phase Blast** (Commando alternate secondary) | Two close-range blasts with falloff instead of a long piercing shot. The secondary becomes a shotgun and you fight up close. |
| **Tactical Slide** (alternate utility) | "Can fire while sliding", so the dodge no longer interrupts damage. |
| **Sabotage** (alternate special) | Throws 3 sticky explosives that go off "when hit by Commando or an ally". Throw first, then shoot: a two-step combo. |
| **Railgunner's scope and active reload** | The secondary is a scope that swaps the primary to a 1000% sniper shot. After firing, a reload bar sweeps across. Hit the "perfect" zone for +500% on the next shot; miss, and "you cannot fire until the cursor finishes its sweep". The zone widens with attack speed. A real timing skill with a clear cost for failure. |
| **Backup Magazine:** "+1 charge of your Secondary skill" | The secondary can be fired twice in a row, a small burst. Stacks. |
| **Hardlight Afterburner:** "+2 charges of your Utility skill. Reduces Utility skill cooldown by 33%" | The dodge becomes the main way to move around. |
| **Brainstalks:** "Upon killing an elite monster, enter a frenzy for 4s … where skills have no cooldowns" (really a 0.5 s minimum) | A proc (a random or conditional trigger) creates a burst window. You go after elites to open it. |
| **Visions of Heresy:** "Replace your Primary Skill with Hungering Gaze" (12 tracking shards that recharge together) | The primary changes from hold-to-fire to burst-and-wait. The wiki notes this is good for some survivors and a "direct downgrade" for others. |

**Only numbers:** the many plain stat items were not examined. Purity ("All skill cooldowns are reduced by 2 … All random effects are rolled +1 times for an unfavorable outcome") is a number change with a cost attached.

**How items are offered** in a run (chests, printers and so on) was **not verified** in this research.

**What the developers have said** (Hopoo, Game Rant, 13 May 2023):
- Paul Morse on survivor design: "probably our most favorite type of content to design and create".
- Morse on unlocking alternate skills through challenges: "it teaches the player and shows them what the potential of that alternate ability is."
- Duncan Drummond: "We still want all the characters to function in single-player"; "There are a couple of items in Risk of Rain 2 that changed your skills."
- Morse said Bandit's "Lights Out" special resets its cooldown on a kill and suited aiming at low-health enemies in 3D (**snippet only**).

### 2.4 What the three have in common

- **Few inputs, fixed for the whole run:** five in Hades, four in Risk of Rain 2, and two to four skills in Shape of Dreams. Depth comes from changing what each input does, from holding a button (Hades II), and from charges.
- **Slots constrain choices.** In Hades, a slot holds one boon and hammers exclude each other. In Risk of Rain 2, a skill belongs to one slot and items target a slot ("your Secondary"). In Shape of Dreams, there are four Memory slots with three Essences each. A slot is what makes a new reward a real trade instead of free power.
- **The upgrades that change play** reshape range, remove or add a management task, add charges, link two buttons, or automate a skill. Damage-only upgrades exist in all three and act as filler.
- **Variety comes from:** small random offers (usually three options); prerequisites (Duo and Legendary boons); exclusions (hammers); a few run-defining choices made before the run (aspects, loadouts, Traveler identity); and late "break the game" builds that are possible but not guaranteed.

---

## 3. Subject 2: what developers say makes a rotation feel good

### 3.1 Principles (rule, source, why)

1. **Every tool needs one purpose the player can name.** Hugo Martin (id Software, Doom Eternal): "every gun, upgrade, and demon is meant to have a purpose". *Why:* when a button's job is unclear, it either gets ignored or pressed on cooldown without thought.
2. **Keep a weapon's core identity when tuning it.** Ryozo Tsujimoto (Monster Hunter producer): "You have to find the core pillar of each weapon's gameplay and not betray that" (**snippet only**). *Why:* upgrades that erase the pillar leave the class without a character.
3. **The core rotation is about four buttons, and the rest is situational.** Greg "Ghostcrawler" Street (WoW lead systems designer, 2011): "about four is the sweet spot", while stressing four is not magic. *Why:* past that, players stop making choices and start following a list.
4. **Situational skills can still count in the rotation if skipping them costs damage.** Ghostcrawler on Kidney Shot, a stun: "It's rotational in that your DPS will drop if you ignore it, but you can't spam it." *Why:* a stun or stagger skill earns its slot when using it at the right moment also pays off in damage.
5. **Extra buttons should answer specific situations.** Path of Exile 2, as summarised by Maxroll, not Jonathan Rogers' own words: "Additional skills are about having a response for specific situations". Rogers: "you have to be able to use many skills together and combos". *Why:* combos and situational answers are what separate an action game from one-button play.
6. **Complexity is acceptable when it is a choice, not a list.** Ghostcrawler (2010): "DPS rotations are made complex because making those choices to max your DPS is part of the gameplay." Ion Hazzikostas (WoW director, Midnight): "a huge portion of 'skill' in WoW has always rested in moment-to-moment decision-making." *Why:* a fixed sequence can be learned once, while a priority (pick the best available action now) keeps asking for judgement.
7. **Keep cognitive load manageable, especially while the player is also dodging.** Hazzikostas: "fewer things occurring simultaneously to keep cognitive load manageable". Ghostcrawler gave tanks simple rotations because "tanks are already stretched in more directions at once". *Why:* in our game every enemy telegraph competes with the rotation for the player's attention, much as threat and positioning do for a tank.
8. **Give the burst window its own rules, not only bigger numbers.** Smilegate (Lost Ark Guardianknight, 30 Jan 2026): in Incarnation "all resource restrictions are completely lifted, and her skills fully evolve", which "is the payoff for mastering resource management". *Why:* a burst that changes the buttons is felt; a 1.5× multiplier is only read in the numbers.
9. **Don't sync every burst to one shared timer.** Naoki Yoshida (Final Fantasy XIV), machine-translated via PC Gamer: aligning buffs to 120 s made "the job rotations extremely similar, and I don't think that's a good thing." *Why:* a single burst cadence flattens kits. Yoshida's team needed it for party play, which a solo game does not.
10. **Fewer stacked cooldowns, more decisions.** Blizzard (Warlords of Draenor pruning post, 2014): a stated goal was to "reduce the amount of cooldown stacking", and the team wants players to "face interesting decisions during combat." *Why:* stacking several cooldowns onto one window becomes a single macro, not a decision.
11. **Commitment is the price of a big payoff, and everything else should be cancellable.** Eric Williams (former God of War lead combat designer, 2008): uncancellable specials teach "risk vs. reward". How cancels are built decides whether a game feels "lagged-out" or "crisp". A good cancel system "allows the player to be super creative". *Why:* locking the character on every skill makes all skills feel heavy, including the ones that should not be.
12. **A timing window needs a visible target, a real reward and a clear cost for failure.** Railgunner (Risk of Rain 2 wiki): a sweeping bar with a marked zone; success gives +500% on the next shot; failure means waiting out the sweep. Gears of War's Rod Fergusson tied active reload to a game where "every shot counts" (**snippet only**). *Why:* the window feels like skill, not chance, when you can see it, practise it and feel the price of missing it.
13. **Strong effects should come with trade-offs, but too many make a kit clunky.** Riot (League of Legends, 14 May 2021): "Impact and counterplay should often be proportional", yet "too much leads to a kit that feels clunky or overly unreliable." *Why:* Riot wrote this about opponents, but it maps onto our charge times and cast delays.
14. **Upgrades should make players reconsider how they play.** Supergiant, per Wikipedia citing Gamasutra 2019: "add in new tricks or tools for the player that would make them reconsider how they have been playing"; Kasavin: "nudged into a new set of tools". *Why:* this is the job of a roguelike reward, and number-only rewards fail it.
15. **Enemies should force different answers.** Martin (Doom Eternal), citing Marty Stratton: "the guns are the tool and the AI is the problem"; "Each AI will push you in different ways." *Why:* without enemy-specific problems, the best rotation is the same everywhere.

### 3.2 Known failure modes

- **Button bloat.** Blizzard, 2014: "the game's complexity has steadily increased", players are "starting to get overwhelmed", some feel they need "dozens of keybinds", and many abilities "are barely used at all". Ghostcrawler, 2011: "there are still probably too many overall."
- **Skills nobody takes.** Ghostcrawler on early WoW: abilities "situational or, to be honest, not used at all", with "a lot of pressure to fix neglected abilities rather than cutting them". Jay Wilson (Diablo III director) on Diablo II's tree: "The tree also had a lot of unwanted skills, but you had to take them even if you didn't want them" (**snippet only**).
- **The same rotation everywhere.** Yoshida on the 2-minute alignment (above). Rogers on Path of Exile 1 as "a very one-button build kind of game". Martin wanted to stop players relying on one weapon, Doom 2016's super shotgun (**snippet only**).
- **Upkeep buffs as chores.** **Not found as a developer quote.** The nearest is press reporting that WoW: Midnight cut "the number of abilities and buffs each class has to manage" (**snippet only**). This failure mode is not sourced to a developer here.
- **High demands that shut players out.** Ghostcrawler, 2011: depth "can feel cool to a veteran player", but "for the new or returning player, it just becomes incomprehensible." Blizzard now ships a "One-Button Rotation" tool to "help players learn new specializations" (Hazzikostas, Midnight).
- **Cooldown stacking.** Blizzard 2014 (above).
- **Kitchen-sink abilities** (one button doing too many things): warned against in the 2011 spellbook post.
- **Over-pruning.** Hazzikostas in 2019 (MeinMMO, paraphrasing a Blizzard video): the team capped abilities too rigidly and removed ones central to class identity. The headline reads "We overdid it and missed the target."
- **Easy, unearned exploits of a timing reward.** Cliff Bleszinski called Gears of War's multiplayer active reload a mistake, because players could fire once, reload and get the bonus (**snippet only**; the article returned 403).

### 3.3 Where the sources disagree

1. **How many buttons.** Ghostcrawler (about four core) and Blizzard's repeated pruning disagree with Path of Exile 2 (many skills and combos). Blizzard itself has gone both ways: pruning in 2014, "we overdid it" in 2019, and pruning again in Midnight. No source gives a number for an action game with dodging.
2. **Stress as a goal.** Smilegate: "This tension and minor stress are intentional design elements", which turns into "a sense of exhilaration." Blizzard aims to keep "cognitive load manageable" and offers a one-button helper.
3. **Breaking the game.** Shape of Dreams treats an earned broken build as the point. Path of Exile 2 wants "build diversity … very high" and no "two dominant builds". Supergiant sits between them: experiment freely, and rework rather than cut ("not too precious").
4. **Fixed or free slots.** Diablo III launched with category-locked slots and added "Elective Mode" because players objected (**snippet only**). David Sirlin, a designer writing as an analyst, called removing the categories "strictly better". Risk of Rain 2 and Hades keep strict slots; Shape of Dreams lets any Memory go in any slot.
5. **Complexity and mastery, from the same designer.** Ghostcrawler in 2010: more complex rotations give "more engaging play". In 2011: complexity becomes "incomprehensible" for newcomers.
6. **Shared burst windows.** Final Fantasy XIV's team chose them for group play and now regrets the sameness. Lost Ark builds bursts around a class's own resource. Neither is wrong for its genre. A solo game has no party to align with.

---

## 4. Against our kit, against Kristian's taste, and ten questions

### 4.1 Where our current kit fails or has no answer (I have not seen the game; guesses are marked)

- **Eight skills built by input type, not by job (principles 1, 3, 5).** No source supports eight rotational buttons. With eight cooldowns of 5–12 s, the likely result is pressing whatever is ready (*guess*). That is a list, not a priority. There is no stated job per skill, and no skill is designated situational.
- **Focus is a number multiplier that every skill wants to be saved for (principles 8, 9, 10).** An 8 s window at 1.5× damage invites holding cooldowns for the window, which is cooldown stacking (*guess at the actual play*). Focus fills from any damage, so the building half of the loop involves no choice. Compare Guardianknight's skills that "fully evolve", Hades II's held Omega moves, and Shape of Dreams' Ultimate Memories.
- **Every skill locks the character until its last hit, and the dash is on a long cooldown (principle 11).** Commitment is the same for all skills, so it cannot signal a risk-for-reward move. With one long-cooldown dash, a misjudged lock may have no way out (*guess at feel*). The sources tie commitment to big payoffs and cancels to responsiveness.
- **No timing skill yet (principle 12).** "Charge" and "hold" exist as input types, but no source here says whether ours have a visible window, a reward and a cost for failure. *Not known.*
- **One enemy (principle 15).** Whether the rotation changes from fight to fight cannot be tested yet. Bosses with stagger and counter checks are the planned answer and need skills whose job matches them (principle 4).
- **No slot rule for in-run rewards (Subject 1).** With eight skills equipped from the start, a new skill reward has no slot to compete for, and tripods would be the only lever. Every roguelike studied keeps slots scarce so a reward is a trade.
- **A 4.4/s click-to-fire basic shot alongside eight cooldowns and a gauge** means a lot of input before any dodging (principle 7). Whether that is fun or tiring needs a playtest (*guess*).

### 4.2 Things in the sources that cut against Kristian's taste

- **High actions per minute.** Blizzard's direction is explicitly toward lower cognitive load and fewer buttons. Ghostcrawler kept rotations simple for roles that already have to move and react, which is our situation. Support for Kristian comes from Smilegate ("minor stress" as intent) and Path of Exile 2 (combos). His taste is defensible, but the sources say it needs a floor for less skilled players, such as Blizzard's one-button helper.
- **Situational skills.** Blizzard pruned "niche abilities that could theoretically be useful in some rare scenario". The lesson is not "no situational skills" but that they must matter often: the Kidney Shot rule. A stagger skill should also open a damage window, or it will sit unused.
- **"Upgrades that change the optimal rotation."** In Hades, about two hammers per run do this, plus aspects chosen before the run. Most boon picks and all Poms change numbers. Rotation-changing upgrades are a minority in every game studied, so expecting every pick to change the rotation goes beyond what these games do.
- **Eight equipped skills.** None of the three roguelikes runs a kit this large, and Hades II added depth by holding existing buttons, not by adding new ones.

### 4.3 Ten questions the design has to answer, most important first

1. **Does a run start with all eight skills, or with a few (two to four) plus slots filled during the run?** This decides whether new skills can be rewards at all.
2. **What single job does each skill have, and which three or four form the core damage loop?** The rest should be named as situational: mobility, stagger, counter, area damage.
3. **What does focus do while active: change the kit (skills evolve, cooldowns reset, new properties) or only multiply damage? And what builds it?** Is there a choice in filling it?
4. **What decision does the player make every few seconds?** Is the loop a priority shaped by procs (random or conditional triggers) and resets, or a fixed order? Name the proc or reset in the base kit.
5. **Which skills lock her, which can be cancelled by dash, and does the lock scale with payoff?** Should the dash be shorter on cooldown or have charges?
6. **How are in-run upgrades offered?** How many options and how often; tied to a skill (tripods) or free like Essences; with what exclusions; and how many rotation-changing picks per run (Hades: about two hammers)?
7. **What do enemies and bosses demand that changes the rotation?** Stagger checks, counter windows, armour, crowds: each should call on a different skill.
8. **Where does timing skill live** (active reload, perfect-timed shot, combo windows), what is the visible window, and what does missing it cost?
9. **What is the input load at the start of a run compared with the end, and what is the floor for a less skilled player?**
10. **Are game-breaking late-run builds allowed, and how hard must they be to assemble?**

---

## 5. What could not be found or verified

- **Hopoo's own rationale for the four slots** and its "Dev Thoughts" posts: the Steam pages returned only navigation. The slot jobs in 2.3 are my reading of the wiki data.
- **The Hades Fandom wiki** blocked access (HTTP 402). Hades data comes from teemo.dev (a third-party reference citing that wiki and game files) and Fextralife. Hades II hammer text is from a May 2024 early-access guide; current 1.0 values were not verified. How many gods can appear in one Hades run was not found.
- **Shape of Dreams:** keybindings, Lacerta's starting Memories (third-party sources conflict), and most Essence effects beyond the six opened. The developers' interviews say little about how a build changes play, beyond what is quoted above.
- **No developer quote found on upkeep buffs being chores.**
- **No source gives a number** for actions per minute or decisions per second, or a rule for how often burst windows should come, beyond Final Fantasy XIV's 120 s.
- **No GDC talk on rotation design was found or opened.** Sources are blogs, interviews and patch posts.
- **Snippet only, not opened:** Rod Fergusson and Cliff Bleszinski on active reload, Paul Morse on Bandit's reset, Jay Wilson on Diablo II's tree, Tsujimoto's "core pillar", Doom 2016's super shotgun, and the Midnight "fewer buffs" report.
- **Partly verified:** Yoshida's quotes are machine translations quoted by PC Gamer, read via a mirror. Hazzikostas's 2019 "overdid it" is a German site's paraphrase of a video. Several Lost Ark and Smilegate statements cover new classes only. Lost Ark kits were left to the other researcher.

---

## 6. Sources

**Hades / Hades II**
- https://en.wikipedia.org/wiki/Hades_(video_game): the five inputs, the three-option boon offer, and the Gamasutra 2019 "reconsider" quote.
- https://teemo.dev/game-design/hades/systems/weapons-and-aspects-reference/: aspects, all Rail, Shield and Bow hammers, exclusions, hammers per run (build v1.38290).
- https://teemo.dev/game-design/hades/systems/boons-and-gods-reference/: slot rules, Pom, Duo/Legendary prerequisites, keepsakes, rerolls, example boons.
- https://hades.wiki.fextralife.com/Exagryph,+the+Adamant+Rail: Rail controls, 12 shots, aspects.
- https://www.gamedeveloper.com/design/supergiant-s-fourth-outing-i-hades-i-introduces-a-more-mature-organized-dev-process: Kasavin quotes, 2019.
- https://www.gamedeveloper.com/design/how-supergiant-added-new-goddess-demeter-to-i-hades-i-pantheon: Lai and Kasavin on boon design, 2020.
- https://www.gamesradar.com/games/hades/hades-2-lead-says-the-best-boons-from-early-access-werent-killed-just-reskinned-we-think-its-important-not-to-be-too-precious-about-these-things: "not too precious" quote.
- https://store.steampowered.com/app/1145350/Hades_II/: Hades II release dates.
- https://gameleap.com/articles/hades-2-what-are-omega-moves: Omega moves and Hex (guide, May 2024).
- https://gameriv.com/hades-2-all-daedalus-hammer-upgrades: Hades II hammer text (guide, May 2024).

**Shape of Dreams**
- https://store.steampowered.com/app/2444750/: release date and update list.
- https://shapeofdreams.wiki.gg/wiki/Memories: slots, starting kit, acquisition sources, rarity, Ultimate Memories.
- https://shapeofdreams.wiki.gg/wiki/Essences: three Essences per Memory, free swapping.
- https://shapeofdreams.wiki.gg/wiki/Essence_of_Momentum, …/Essence_of_Virtue, …/Essence_of_Opportunity, …/Essence_of_Dusk, …/Essence_of_Paranoia, …/Essence_of_the_Giant, …/Undo: exact effect texts.
- https://shapeofdreams.wiki.gg/wiki/Travelers: nine Travelers, two unlocked at the start.
- https://bloxodes.com/wiki/shape-of-dreams/travelers: Lacerta's Identity options (third party).
- https://lootlevelchill.com/?p=10167: Shim interview, Oct 2025.
- https://automaton-media.com/articles/newsjp/20260920-468943/: developer interview, Sep 2026 (Japanese).

**Risk of Rain 2**
- https://riskofrain2.wiki.gg/wiki/Commando: Commando skills, alternates, patch history.
- https://riskofrain2.wiki.gg/wiki/Railgunner: scope and active reload timing.
- https://riskofrain2.wiki.gg/wiki/Visions_of_Heresy, …/Brainstalks, …/Backup_Magazine, …/Hardlight_Afterburner, …/Shuriken, …/Purity: item effects.
- https://riskofrain2.wiki.gg/wiki/Skills: Skills 2.0 loadouts.
- https://gamerant.com/risk-rain-returns-remake-interview/: Morse and Drummond quotes, May 2023.

**Rotation and kit principles**
- https://www.engadget.com/2011-04-26-dev-watercooler-ghostcrawler-discusses-the-number-of-player-abi.html: "about four is the sweet spot", Kidney Shot, bloat.
- https://www.engadget.com/2010-11-08-ghostcrawler-on-the-evolution-of-rotation-complexity.html: complexity as choice; simpler tank rotations.
- https://www.engadget.com/2014-02-27-dev-watercooler-pruning-the-gardens-of-war.html: 2014 pruning, cooldown stacking, interesting decisions.
- https://news.blizzard.com/en-us/article/24246290/combat-philosophy-and-addon-disarmament-in-midnight: Hazzikostas on decision-making, cognitive load, the one-button tool.
- https://mein-mmo.de/en/wow-gesteht-uebertrieben-ziel-verfehlt,361285: 2019 "overdid it" (paraphrase).
- https://gjos.pages.dev/posts/yoshi-p-says-that-the-final-fantasy-14-team-is-taking-aim-at-the-games-same-y-combat-jobs-and-2-minute-meta-but-thatll-come-after-dawntrail-tackles-its-encounter-design: mirror of PC Gamer, 7 Jun 2024; Yoshida quotes.
- https://www.playlostark.com/en-us/news/articles/guardianknight-class-behind-the-scenes: Smilegate on resource tension and the Incarnation payoff, Jan 2026.
- https://www.cgmagonline.com/interviews/the-plan-for-path-of-exile-2-decade/: Rogers on one-button play vs combos and build diversity.
- https://maxroll.gg/poe/news/wudijo-path-of-exile-2-interview-with-jonathan-rogers: Maxroll's summary of Rogers ("response for specific situations").
- https://www.gamedeveloper.com/game-platforms/combat-canceled-i-god-of-war-i-action-game-design: Eric Williams on cancels and commitment, 2008.
- https://www.gamedeveloper.com/design/the-aggressive-resource-management-of-i-doom-eternal-i-: Martin and Stratton on purpose per tool and enemies as the problem, 2019.
- https://www.gamedeveloper.com/design/q-a-evolving-the-combat-design-of-id-software-s-i-doom-eternal-i-: Martin on pushing a play style and on mastery, 2020.
- https://www.leagueoflegends.com/en-us/news/dev/quick-gameplay-thoughts-may-14/: Riot on proportional counterplay and clunkiness, 2021.
- https://www.gamedeveloper.com/design/diablo-3-s-ability-system: Sirlin (analyst) on Diablo III slots and runes, 2012.
- https://rpgsite.net/news/14093-blizzard-shares-details-on-classes-and-skills-in-diablo-iv: Diablo IV basic skills as resource builders, core skills as spenders, 2023.
- Search snippets only, not opened: Destructoid on Bleszinski and active reload; Time/Techland on Fergusson; Prima Games on Morse and Bandit; Siliconera on Tsujimoto; PC Gamer on the Doom 2016 super shotgun; Blizzard's Diablo III Elective Mode blog.
