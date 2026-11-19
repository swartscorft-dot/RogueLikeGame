# 0008 Floors, acts and what each reward is worth

Status: **the direction is Kristian's, said on 2026-11-19 after he had played a generated level with the new kit, the new enemies and the props. The numbers and the structure built on it are the producer's proposal; he has not confirmed them.**

## What he said

"Morro - spilt ferdig. Jeg mener egentlig at alt det vi spilte nå er et nivå. Etter et slikt run skal man få en relativt sterk belønning (som nytt spell osv). Skattekistene kan være minor stat upgrades tenker jeg. Dette blir som en floor i Slay the Spire. Det kan være mye mer fiender. 3-4 ganger. Og flere kombinasjoner av dem på en gang. Jeg synes det skal være litt mer lineært hvordan man [går] per 'floor'. Vet ikke hvor mange jeg ser for meg før det blir en act 1 boss. Jeg tror også vi må bli enige om ordbruk, terminologier her."

In short:

- Fun.
- What was built as a whole "level" (a start room, five combat rooms, four side rooms, a boss room) is one unit of a run: a floor, as in Slay the Spire.
- Finishing a floor gives a relatively strong reward, such as a new skill.
- The chests give small stat upgrades.
- Many more enemies: three to four times as many, and more kinds mixed at once.
- The way through a floor should be more linear.
- He does not know how many floors come before the act 1 boss.
- The words need agreeing on: see `docs/vision/glossary.md`.

Asked afterwards about the things nobody had been able to check, he answered:

- **The room sizes fit** ("romstørrelsen passer"). So the 4 m grid and rooms of 20 to 28 m stand; they were guesses until now.
- **Good variety** in the rooms.
- **Pillars and statues do not hide her** from the camera.
- **The guard's stagger felt fine.**
- **The quicker pick-up after shooting is the right direction**: "det føler jeg du har endret på ja. Riktig retning." The value (`MovingShotRecovery` 0.25 s) is the one he meant; whether it should go further he did not say.

## The producer's proposal

**An act is five floors.** Floors 1 to 4 each end with a floor reward, and floor 5 ends with the act boss.

- This fits the kit he has already decided (decision 0005): a run starts with four skills and ends with eight. Four floor rewards are the four skills, one of three offered each time.
- Five floors of about three to four minutes each make an act of fifteen to twenty minutes. That is a guess, to be measured with the play-session recorder.

**A floor is a chain, not a maze.**

- Rooms in one line from the entrance to the exit: an entrance room, four or five combat rooms, an exit room with the floor's hardest fight.
- One or two small treasure rooms hang off the chain as short dead ends: a few steps off the path, never a fork that hides the way on.
- The way forward is always clear from where she stands.

**Fights are bigger and mixed.**

- Three to four times the enemies of the first version: a combat room has eight to twelve where it had one to three.
- Every room mixes kinds, and the mix is what makes rooms differ: a pack with shooters behind it, brutes with a guard, and so on.
- Enemy health comes down so a room still takes twenty to forty seconds. The numbers are guesses.
- Later floors of an act have more and tougher mixes than earlier ones.

**Rewards have three sizes.**

| Reward | Where | Size |
|---|---|---|
| Chest | A treasure room | Small: a stat step that can be felt but does not change how she plays (a little health, a little damage, a shorter cooldown on one skill) |
| Floor reward | After the last fight of a floor | Large: a new skill, one of three |
| Act reward | After the act boss | Not decided |

Upgrades to a skill (the "tripods" of the first documents) are not placed yet. They may become a second kind of floor reward, or a choice at a chest. Open.

## What this changes

- `docs/vision/core-loop.md` says: room, pick a tripod, next room, boss. The loop is now: floor, floor reward, next floor, act boss. Not rewritten until he confirms.
- The generator's plan (start, five combat rooms, boss, four reward rooms) becomes a floor plan with a chain and one or two treasure rooms, and a run strings five floors together.
- The first boss (the horned knight) is the act 1 boss, at the end of floor 5, not at the end of every floor.

## Open, for Kristian

1. Five floors to an act: right, or more?
2. Does a floor end with a harder fight (an elite room), or just with its last room?
3. Where do skill upgrades come from?
4. What does the act boss give?
