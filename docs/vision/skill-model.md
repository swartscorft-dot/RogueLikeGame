# Skill model: input types and skill effects

How skills are cast, and what the special effects on them mean. Every skill in the game is built from this.
Given by Kristian on 2026-11-17, with the references to other games taken out. Changes only through a numbered decision file, like the pillars.

Serves pillar 1 (committed control) and pillar 4 (readable combat).

## Input types

Five types, named by what happens when the skill is cast.

### Normal

Press the skill key and the skill fires. If it has a direction, it fires toward the cursor.
Example: a skill that shoots a cone of projectiles toward the cursor.

### Point

A Point skill needs a place. The first press starts the skill, and a **second click** picks the place where it lands.
Example: a leap that carries the character to the chosen spot and damages enemies in the impact zone.

### Combo

A Combo skill can be cast again inside a window after the first cast, and each recast adds an effect.
When the window runs out, the skill goes on cooldown.
Example: an ultimate that can be cast two more times after the first cast.

### Holding

A Holding skill keeps going while its key is held. There are two kinds.

- **Sustained.** The effect continues as long as the key is down.
  Example: a cone of fire in front of the character for as long as the key is held.
- **Aimed on release.** The key is held to choose an area, and the effect happens when the key goes up.
  Example: hold to pick the "sweet spot" of an explosion. On release the explosion goes off, and enemies in the sweet spot take extra damage and are knocked up.

### Charging

A Charging skill is not a Holding skill, for two reasons.

1. Holding the key for the first time starts a **charging period**, not the skill effect.
2. The longer the charge, the stronger the effect.

Example: a ray that is longer and hits harder the longer it was charged.

## Skill effects

Some skills carry an effect named in the lower part of their description. These are the terms.

### Stagger

Many bosses have a thin purple bar under their health bar: the **stagger gauge**.
A skill with the Stagger effect drains it when it hits. How much depends on the stagger strength, ranked: low, medium-low, medium, medium-high, high, high+.
When the gauge is empty, the boss is dazed. It does not attack or move, and the player can spend the time on damage without worrying about anything else.

### Counter

Counter has more conditions than Stagger.

1. The skill must be cast in front of the boss.
2. It only works if it hits the boss while the boss is **charging an attack**. A charging boss is highlighted bright blue.

If both hold, the boss's charging attack is cancelled.
Counter is how the player answers attacks that cannot be survived: for example a shockwave over the whole arena that nothing else stops. Learning the timing is what beats the hardest bosses.

## Where the game stands against this (G3 build, 2026-11-17)

The G3 skills (`docs/slices/g3-skills.md`) were built before this text arrived. They match it in part.

| Type | In the game | Matches? |
|---|---|---|
| Normal | Quick Shot (Q) | Yes |
| Point | Powder Keg (W) | **No.** It lands on the cursor at the press. This text wants a second click to place it |
| Combo | Not built | Waits. First-class table allows it |
| Holding | Fan Fire (E) | Sustained kind only. The aimed-on-release kind is not built |
| Charging | Long Shot (R) | Yes |
| (Casting) | Heavy Volley (D) | **No type for it here.** A long cast with a bar, then one hit. See open points |
| Stagger, Counter | Not built | Needs enemies that act. Not before the boss slice |

## Open points for Kristian

1. **Casting.** The earlier plan had Casting and Chain as input types. This text has five types and neither of those. Heavy Volley (D) has no home. Options: drop the skill, make it a Normal skill with a long startup, or keep Casting as a sixth type.
2. **Point with a second click.** Change Powder Keg to press, then pick, then click? It costs one more input before the skill is committed. When does the lock begin, and does the cooldown start on the first or second click?
3. **Aimed-on-release Holding.** Build it in G4 as a second Holding skill, or leave it out of the first class?
