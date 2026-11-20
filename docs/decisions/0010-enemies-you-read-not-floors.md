# 0010 Enemies you read, not floors

Status: **the direction is Kristian's, said on 2026-11-20 after his first session with the whole day's game (thirteen minutes, two runs, dead on floor 2 and in floor 3's exit room). The attack sets built on it are the producer's proposal; he has not played them.** It changes how pillar 4 is met; the pillar's wording is proposed at the end and is not changed until he says so.

## What he said

"Veldig fin test, liker det. Jeg syntes at det er rart at de med skjold og sverd gjør ranged attack. Også må vi ha flere prosjektiler. Vi kan gå bort i fra så mye telegrafing på fiender. Heller en animasjon eller signalisering på dem at de angriper. Spillet går mer ut på å dodge farger på deg selv enn å dodge det som kommer mot deg.... Ellers veldig bra, liker vanskelighetsnivået på spillet. Det skal være vanskelig. De som traff var tabber jeg gjorde."

In short:

- **The difficulty is right and must stay hard.** The hits he took were his own mistakes. (So the third balance pass, which softened the game, was not merged.)
- **The sword-and-shield enemy should not attack from afar.**
- **More projectiles**: things that come at her and are dodged.
- **Much less telegraphing on the floor.** A tell on the enemy instead: an animation, a signal that it attacks.
- His complaint in one line: the game is about dodging colours on yourself, not about dodging what comes at you.

Asked three questions the same evening:

- **Floor shapes go on ordinary enemies and stay on the boss.** (Of: off on ordinary enemies and kept on the boss; off everywhere; kept but fainter.)
- **Melee enemies**, in his own words: "Melees bør ha vanlig auto attack, komme i range og angripe på lav cd. Ikke mye dmg, men kanskje ha en-to skills som gjør noe spesifikt for den typen de er. Noen drar inn, noen stunner, noen gjør mer dmg osv. De små gutta tenker jeg er et eksempel på bør gjøre litt skade men veldig fort i stedet for å stoppe opp og charge en attack. Man må tenke at de skal ned med en gang de rusher mot oss." So: an ordinary attack on a short cycle once in reach, for little damage; one or two skills per kind that do what that kind is for (pull her in, stun her, hit hard); the small ones bite fast without stopping to wind up, so that killing them at once is the answer when they rush her.
- **Projectiles, all four offered:** arrows that fly; slow, large orbs from the caster; fans and volleys; a wave along the ground from the boss.

## What follows, as rules

1. An ordinary enemy shows no shape on the floor. What it is about to do is read on its body: the wind-up of its animation, a glint at its weapon (red; blue when the attack can be countered), and a sound.
2. A melee enemy has an **ordinary attack**: in reach, on a short cycle, for little damage, with almost no wind-up. Walking out of its reach or out of its front avoids it; so does the dash.
3. A kind has one or two **skills** on their own cooldowns, each doing what that kind is for, each with a clear tell on the body.
4. What is shot or thrown at her **flies**: it has a speed and a size, she can step out of its way, and walls and tall props stop it.
5. The boss keeps its floor shapes for its large attacks.
6. The difficulty stays where it is. Fewer shapes must not mean fewer ways to die.

## The producer's proposal for the first build

All numbers are first guesses. The attacks as they were stay in the code as data behind a switch, so the two can be compared.

| Kind | Ordinary attack | Skills |
|---|---|---|
| **Swarmer** (ghoul) | Bites as it clings: little damage, twice a second, never stopping to wind up. | **Leap**: hops the last few metres to reach her. |
| **Brute** (sword and shield) | A sword stroke about once a second in reach. | **Shield bash** (blue): a short lunge; if it lands she is stunned for a moment. |
| **Guard** (the big one) | A heavy fist, slow. Keeps its stagger bar. | **Drag** (blue): after a tell, pulls her to it if it can see her within nine metres; cover breaks it. **Crush**: a slow overhead blow that hits hard. |
| **Shooter** | none | An **arrow that flies**. On later floors and for elites, every third shot is a **fan of three**. |
| **Caster** | none | A slow, large **bone orb**. Every third cast a **fan of three** (blue), in place of the circles under her feet. |
| **Boss** | as today, with its floor shapes | Its Cleave also sends a **wave along the ground** that she sidesteps or dashes through. |

New for her: being **stunned** and being **pulled**, each short and each shown plainly. Both come only from blue attacks, which Stopping Shot can counter.

## Open, for Kristian

1. Is being stunned, even for half a second, acceptable in a game about tempo? (The alternative for the brute: a knock-back.)
2. Should the boss lose its floor shapes too once the tells have been played?
3. Pillar 4's wording. Today: "Readable combat. Telegraphs, stagger and counter make you lose to your own mistakes." with "Lost Ark bosses with colour-coded telegraphs" as its model. Proposed: "**Readable combat.** You read the enemy and what flies at you: tells on the body, things you can dodge, stagger and counter. You lose to your own mistakes." with "a screen of coloured shapes to step out of" added to what it is not like.
