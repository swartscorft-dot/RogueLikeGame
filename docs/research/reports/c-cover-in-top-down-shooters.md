# Cover: what other games do, and three rule sets for ours

Research report, 2026-11-20. Read-only look at the project. I fetched the web sources myself; where I only saw a search snippet, or a page would not load, I say so.

## 0. What the game does today (from the code)

- **Two kinds of "cover" in the code.** `Cover` sits on every wall (at any height, the 1 m south wall included) and on every prop at least 1.2 m tall that is not slender (`LevelProp.IsCover`, `PropValues.CoverHeight = 1.2`). These stop everyone's shots all the time, hers and the enemies'. `Shelter` sits on every wall and on every prop that blocks walking, tall or low. It decides where Left Ctrl puts her.
- **The key.** You hold Left Ctrl. If she is free and within 1.5 m (`CoverReach`) of any shelter's edge, she moves in 0.15 s to the nearest point of the *nearest* shelter's edge, 0.6 m out, on whatever side she is already standing (`TakeCoverRules.StandPlace`). There she ducks. The move keys slide her along that shelter at 40 % speed. A key more than 60° off the face does nothing. A shot or a skill stands her up for its length plus 0.25 s. Letting go of the key, dashing or using a moving skill takes her out of cover.
- **What it stops.** Ducked, the shelter stops a shot when it lies on the straight line between her and the shooter (`TakeCoverRules.Shields`). Shooters and casters then stop seeing her and walk round until they do. It works against arrows, fans and orbs. It does **not** work against the boss's wave: `ThingsThatFly` skips her cover for waves, and only walls stop a wave. It does nothing against melee. The Guard's Drag is broken by a wall or a tall prop, not by ducking.
- **Why ducking behind a pillar is pointless.** For a tall shelter, `Shields` is `(ducked || tall) && crosses`. A tall prop already stops the shot through `Cover`, so ducking behind a pillar, a statue or a wall adds nothing. That is his complaint, and it is built into the code. Ducking does something only behind the nine low props: bone pile 0.6, rubble 0.8, skull pile 0.88, chest 0.9, sarcophagus 1.0, coffin 1.0, basin 1.07, brazier 1.1 and urns 1.1 m.
- **Why it is hard to read.** The 1.2 m line falls inside a cluster of heights. Altar 1.3, grave marker 1.3 and broken pillar 1.2 are "tall", while brazier and urns at 1.1 are "low". From 28 m up nobody can tell 1.1 m from 1.3 m. The cage (2.0 m) stops shots, though a cage looks see-through. A 1 m wall stops shots, but a 1.1 m brazier does not. The only signals are a crouch pose and a HUD box that reads "COVER ducked / ducked, low / up".
- **Why it is hard to tell where she will end up.** The key takes the nearest thing within 1.5 m, wall or prop, with no preview. It also keeps her on the side she already stands on, whichever side the enemies are on. So she can duck on the wrong side and get no protection.
- One small mismatch: decision 0010 and `EnemyKinds.cs` both give the arrow 24 m/s, not about 13 m/s.

## 1. Enter the Gungeon

**Flipping.** You stand next to a table and press the interact key (E). "The side you will flip is outlined in a white line", and the table falls away from that side, so away from you ([Steam guide, 2016](https://steamcommunity.com/sharedfiles/filedetails/?id=660679727)). She does not move: the table turns into a wall on the far side of where she already stands. Coffins in the Hollow "can only be flipped sideways" ([wiki.gg, Tables](https://enterthegungeon.wiki.gg/wiki/Tables)).

**The flip is a defensive action in itself.**
- Developer Dave Crooks: "the impact of the table being flipped actually deletes enemy bullets in a small radius" ([Game Developer Q&A](https://www.gamedeveloper.com/design/q-a-the-guns-and-dungeons-of-i-enter-the-gungeon-i-)).
- The wiki says the same, and adds that the player "will become invincible and absorb bullets for a few frames" after flipping ([wiki.gg, Tables](https://enterthegungeon.wiki.gg/wiki/Tables)). Forum posters disagree about the invincibility, so I treat it as unconfirmed.
- The Steam guide puts the cleared area at about three tables across.

**What a flipped table blocks.**
- "Flipped tables block bullets the same way walls do" ([Steam guide](https://steamcommunity.com/sharedfiles/filedetails/?id=660679727)). It is a physical object, not a rule about sides.
- The player's own shots are stopped too. I found no page that says so outright, but two wiki notes imply it. Bullet Kin flip tables "in order to take cover from the players bullets" ([wiki.gg, Bullet Kin](https://enterthegungeon.wiki.gg/wiki/Bullet_Kin)). Table Tech Shotgun's own pellets are destroyed when they overlap the table ([wiki.gg](https://enterthegungeon.wiki.gg/wiki/Table_Tech_Shotgun)).
- I could not confirm whether a table also blocks shots from behind. Being a solid collider, it presumably does.

**How it breaks.**
- Tables are destroyed "after taking enough damage" ([wiki.gg](https://enterthegungeon.wiki.gg/wiki/Tables)).
- Their health is the same whichever side is shot. Rapid-fire and shotgun enemies break them much faster. "Tables are not permanent cover. Do not act like they are." ([Steam guide](https://steamcommunity.com/sharedfiles/filedetails/?id=660679727))
- A table also absorbs bullets as it breaks ([wiki.gg](https://enterthegungeon.wiki.gg/wiki/Tables)).

**Sliding over.**
- A dodge roll into an unflipped table slides the player over its top and damages it. A wooden table survives three slides, a stone one two. Rolling again while on the table breaks it at once ([wiki.gg, Tables](https://enterthegungeon.wiki.gg/wiki/Tables); [wiki.gg, Dodge Roll](https://enterthegungeon.wiki.gg/wiki/Dodge_Roll_(Move))).
- The invulnerable part of the roll is its first half ([wiki.gg, Dodge Roll](https://enterthegungeon.wiki.gg/wiki/Dodge_Roll_(Move))).

**Pushing.** A flipped table "can be pushed around". Several at once "significantly slows the player" ([wiki.gg](https://enterthegungeon.wiki.gg/wiki/Tables)).

**Enemies use the same cover.** Bullet Kin "will occasionally walk to tables and flip them" for cover ([wiki.gg, Bullet Kin](https://enterthegungeon.wiki.gg/wiki/Bullet_Kin)).

**Why it exists.** Crooks again: "We want the player to feel like the place they are having the gunfight is important". He lists tables, explosive barrels and chandeliers in the first chamber ([Game Developer](https://www.gamedeveloper.com/design/q-a-the-guns-and-dungeons-of-i-enter-the-gungeon-i-)). Eight Table Tech items build on the flip ([wiki.gg category](https://enterthegungeon.wiki.gg/wiki/Category:Table_Tech)).

**The core of it.** Gungeon has no cover *stance*: no snapping, no sliding, no crouch. Cover is a *wall you make* with one press, in a direction the outline shows before you press. It is temporary, and the press itself is a small defensive burst.

## 2. Other games

**The Ascent (isometric; crouch behind low cover; instructively weak).**
- Left Ctrl crouches "to take cover behind low obstacles". Aim High shoots over cover "even while crouching" and adds stagger ([Fextralife](https://theascent.wiki.fextralife.com/Combat)). Shots fired without aiming high may hit the object the enemy hides behind ([Gamepressure](https://www.gamepressure.com/ascent/raising-weapons-how-does-it-work/zdeadc)).
- PCGamesN: "you don't actually *stick* to cover", it "only really works to cheese through certain encounters", and with the isometric view "it can be really difficult to tell if you're in cover, and if so, whether that cover can be fired over" ([PCGamesN review](https://www.pcgamesn.com/the-ascent/review-pc)).
- The lesson: from a high camera, height is the hardest thing to read. A cover rule that depends on how tall something is fails unless the heights are far apart.

**Hades (top-down; no cover key).**
- Pillars stop shots and are used as cover. Fextralife's advice: "the walls and pillars are your best friends here".
- Pillars break when hit and drop debris that can hurt you. Elysium's pillars "can regenerate after being destroyed" ([Fextralife walkthrough](https://hades.wiki.fextralife.com/Walkthrough)).
- Players complain that "Pillars are notoriously far thinner than they look, ... making safely hiding behind them a little unintuitive" ([Steam thread](https://steamcommunity.com/app/1145360/discussions/0/5185299686162074064)).
- The lessons: with no key, cover is purely a question of where you stand. It must break, so you cannot camp behind it, and its collider must match its silhouette.

**Foxhole (top-down; directional, numeric cover; a counter-example for us).**
- "Pretty much any physical obstacle" gives cover. A crouch-height structure "will provide zero cover if you are standing".
- Turning your back to it removes the cover. Cover comes in three levels and absorbs a *percentage* of incoming shots.
- A shield icon appears after about a second ([Foxhole wiki](https://foxhole.wiki.gg/wiki/Heavy_Suppression)).
- The lessons: the icon showing your state is good. A delay and chance-based absorption are the opposite of "you lose to your own mistakes".

**Nuclear Throne (top-down; walls only, and walls can go).**
- Explosive and strong energy weapons "blow out chunks" of the tile walls, and some bosses charge through them ([PC Gamer](https://www.pcgamer.com/why-nuclear-throne-is-still-the-best-roguelike-around)).
- The fan wiki's enemies page says Big Bandit charges "if they break line of sight", destroying walls in its path. I saw this only in a search snippet; the page itself ([fandom](https://nuclear-throne.fandom.com/wiki/Enemies)) would not load.
- The lesson: the enemy answers hiding. Breaking line of sight sets off the attack that punishes hiding.

**Not used.** I found nothing on cover in Synthetik. I left out Helldivers, Ruiner, Brigador and Hotline Miami for lack of time and sources.

## 3. What the good cases share

1. **Cover is a physical object, not a stance.** Gungeon's table and Hades's pillar stop shots because they are in the way, for anyone. The Ascent and Foxhole add a stance and a height rule, and players cannot tell whether they are covered.
2. **What counts is obvious before you act.** Gungeon outlines the edge you will flip. Hades counts only big upright things. Nobody has to judge 1.1 m against 1.3 m.
3. **The character does not get moved.** Gungeon flips from where you stand, and Hades has nothing to snap to. Our snap to the nearest edge is exactly what "where you end up ducking" complains about.
4. **It costs tempo once, then runs down.** Gungeon's flip is one press, and the table breaks. Hades's pillars crumble. Nuclear Throne's walls go, and its bosses punish broken line of sight.
5. **Enemies take part.** Bullet Kin flip tables. Our shooters already walk round to her side, and that is worth keeping.
6. **From a high camera, height cannot be a fine rule.** Tall must look tall (long shadow, hides the floor behind it), and low must be clearly low.

## 4. Three proposals

All three keep what exists:
- The `Cover` component on walls and tall props stops everyone's shots.
- Shooters and casters walk round when they cannot see her.
- The wave is stopped only by walls.
- The dash passes projectiles.

### Proposal A: "Kick the coffin" (nearest to Gungeon)

**Rule.**
- Coffins (2.15 × 1.15 m, 1.0 m tall; Gungeon's own Hollow coffins flip sideways) and, if wanted, sarcophagi are *kickable*.
- She presses Left Ctrl (a tap, not a hold) within 1.5 m of one. She stays where she is, plays a 0.25 s kick and cannot shoot during it.
- The coffin tips over across its long side, away from her. It always falls onto the long side nearer to her, as Gungeon's coffins do.
- It is now a 2.15 m wide, 0.4 m deep slab that counts as tall cover (the `Cover` component on). It stops everyone's arrows, orbs and fans, hers included.
- When it lands, it removes enemy projectiles within 2 m of its centre, the way the flip clears bullets in Gungeon.

**How it breaks.** Every projectile it stops costs it hits: an arrow 1, an orb 3, a shot of hers 1. At 8 it breaks with a splinter burst and leaves rubble that blocks walking only. It cannot be put back up.

**What it does not stop.** Melee. The wave, so the boss is still dashed. The Guard's Drag is broken by it, since it is now a tall prop.

**Which props take part.**
- Coffins, and sarcophagi if wanted, are the only props that change. The level generator should place one or two per fight room.
- Tall props (pillar, statue, throne, bookshelf, cage) stay permanent cover that you stand behind. No key is needed for them, and that is what they are for.
- Low props stay obstacles you shoot over.
- The duck stance and `Shelter` snapping are removed.

**What is shown.** No floor marks.
- Within reach, a pale rim light runs along the coffin's top edge on her side: Gungeon's white outline, on the model. It tells her it can be kicked and which way it will fall.
- The slab shows cracks at 4 and 6 hits.
- A dull wooden "thock" plays for every projectile it stops.

**Tempo and difficulty.** One press makes a 2 m wall that lasts a few seconds against one shooter and less against an elite's volleys. Using it is a decision: *this* spot, *this* way. You cannot camp behind it, because it breaks, enemies walk round, and her own shots chip it.

**Size of the job.**
- A component on the coffin prefab: reach test, rim light, a tween of 90° about the bottom edge, `Cover` enabled, a hit counter.
- One call that ends the pooled projectiles within a radius.
- The generator places coffins.
- Two days at most.

### Proposal B: "Stand behind it" (no key)

**Rule.**
- Remove the cover key and `Shelter` entirely. Cover is geometry: whatever is tall stops shots for everyone, and whatever is low stops nothing. She protects herself by where she stands, as in Hades.
- Make the height split unmissable. **Tall means 2 m or more**: pillar 3.0, statue 2.6, throne 2.6, bookshelf 2.17, cage 2.0. **Low means 1.1 m or less.**
- The three in between (altar 1.3, grave marker 1.3, broken pillar 1.2) go low: they stop no shot. Alternatively their models are scaled out of the band. This is an open question.
- The 1 m south wall becomes low too, so she and enemies shoot over it.

**What tall props become.**
- **Tall floor props crumble.** A pillar takes 12 enemy hits (an orb counts 3), shows chips at a third and two thirds, and breaks into a broken-pillar stump that is low. Her own shots do not wear it down, so it stays her cover, not her obstacle.
- Walls and props standing against a wall never break.

**What it stops.** Arrows, orbs and fans, whenever the tall thing is on the line between them. Not the wave, and not melee. The Guard's Drag is broken by a tall thing between them, as today.

**What is shown.**
- Only the chips and a stone "crack" sound as pillars wear.
- A tall prop between the camera and her could fade to a silhouette. That is general readability, not part of this rule.

**Tempo and difficulty.**
- No press at all, so the dash and the skills keep every key.
- Hiding works for a few seconds before the cover is gone, or the shooter has walked round, or the caster's slow orb has curved the fight.
- It matches "enemies you read": you read where the shot comes from and put stone between you and it.

**Size of the job.**
- Delete the key path, the duck pose and the HUD box.
- Change two height numbers and the props' classification.
- Add a health component on tall floor props that swaps in the stump.
- One day.

### Proposal C: "Duck the shot" (a timed duck, not a stance)

**Rule.**
- Tap Left Ctrl. If a low prop is within 1.0 m, she drops *on the spot* (no snap) for 0.5 s, then rises. The key then waits 1.0 s.
- While she is down, a low prop between her and a shooter stops **arrows and fans**. **Orbs** (1.8 m across) pass over low props, because they are about moving. The wave, melee and Drag are unaffected.
- She cannot shoot while down. Her first basic shot within 0.4 s of rising staggers like a "peek shot", a reward for the timing. This is optional.
- With no low prop in reach, the tap does nothing. Tall props get no duck; they already stop the shot.

**What it is.** A second answer to a bow draw you have read. The dash moves you through the arrow; the duck keeps your spot so you can shoot straight back.

**What is shown.**
- Within 1.0 m, a faint rim light on the low prop tells her a tap will work there.
- When something is stopped, a spark and a wooden or bone sound play on the prop.

**Tempo and difficulty.**
- Half a second down for every use. It rewards reading the draw and punishes ducking late or on the wrong side.
- Ducking early is wasted, since she is up again before the arrow arrives.

**Size of the job.** Most of today's code stays: `Shields`, `CoverEntry` and the projectile stop. The hold, snap and slide are removed, and a timer is added. One day.

### Which first

**Build B first, then A on top of it.**
- B is the cheapest, and it removes both of his complaints at the root: there is no question of which prop or where she ends up, because nothing moves her and only plainly tall things cover.
- It also gives the pillars a clear job. They are *the* cover, and they wear out.
- A then adds one active verb on top of B's clean rule: turn a low thing into a tall one, when and where you choose. It is close to the Gungeon he remembers.
- C is the fallback if he wants the duck key to stay.

**What to playtest.** He plays two runs with B on, then two runs with A + B. Read the session log.
- **Readability.** Ask him after each fight: "When you stood behind something, did you know it would stop the arrow?" Count the hits he calls unfair.
- **Use.** In A, count kicks per room (healthy: 0 to 2) and ask whether the coffin fell where he expected. Log how many seconds a kicked coffin lasts: 2 to 6 s against one shooter means it is a breather, not a bunker.
- **Not dominant.** In B, the time she spends with a tall prop between her and the nearest shooter should stay under about a third of a fight. In A, the time behind a coffin should be under about 15 %. If either is higher, cover is camping.
- **Difficulty.** Deaths per floor should not fall noticeably. The difficulty must stay where it is (decision 0010).

## 5. Questions for Kristian

1. **What to build first?** (a) B, stand behind tall things, no key; (b) A, kick the coffin; (c) B and then A; (d) C, keep a duck key, but as a quick duck.
2. **Should tall floor props break?** (a) No, never; (b) yes, into a low stump, from enemy shots only; (c) yes, from everyone's shots.
3. **Altar, grave marker and broken pillar (1.2 to 1.3 m)?** (a) Make them low, so they stop nothing; (b) make the models taller so they read as tall; (c) leave them as they are.
4. **Should a kicked coffin stop her own shots?** (a) Yes, as in Gungeon; (b) no, she shoots through her own cover; (c) yes, and her shots wear it down.
5. **Should anything of hers ever stop the boss's wave?** (a) No, it is always dashed or sidestepped; (b) a kicked coffin stops it and breaks.
