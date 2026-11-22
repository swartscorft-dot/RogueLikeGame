# 0011 The interface: kinetic, small, at the figure

Status: **the direction is Kristian's, chosen on 2026-11-20 over four rounds of pictures. Nothing of it is built yet; the build is a work order of its own, after the parts it dresses exist.**

## What he said

He asked for four or five different interface styles to choose between, "slik at det oppleves litt unikt". Codex drew them from the game's style reference.

- Round 1, five skins on one layout (carved bone and iron, the gunslinger's ledger, spirit glass, a stained-glass reliquary, ink and woodcut): **"Jeg likte ingen av dem, gi meg 5-6 nye."**
- Round 2, six with a layout of their own each (everything around her, kinetic anime, polished action-MMO, flat and modern, a hand of occult cards, painted and warm): of the first three he chose **kinetic anime**, of the last three **flat and modern**. What matters most to him: **it covers little of the screen; the information is at the figure; it has a clear identity of its own.** In his words: "Jeg likte stilen til kinetisk anime, bare litt mindre. Lag en font/tall-stil i den duren også."
- Round 3, two smaller kinetic versions and a sheet of type and numbers (`docs/art/concepts/ui/`): **"Jeg likte stilen til den første, men det som var under karakteren på 2 bedre. Hakket mindre enn 1."** The type and numbers: **"Ja, men roligere."**

## What follows

1. **The style is kinetic:** slanted shapes with hard diagonal cuts, heavy condensed italic lettering, flat black and white with one hot teal and one red, halftone only as a small accent. `ui_kinetic_small_1.png` is the reference for the style and the layout.
2. **Small.** Everything a notch smaller than in that picture. The fight is what the screen is for.
3. **At the figure.** Her health and her focus are two thin slanted strokes side by side under her feet with one small number, as in `ui_kinetic_small_2.png`. The cast bar is a short slanted blade just above her head. Enemies carry a small thin slanted health bar with their marks as tiny pips beside it.
4. **At the edges, little:** the eight skills as one small slanted strip at the bottom centre; the map as a tiny row of room marks top right; an offer of upgrades as slim tilted cards with large numerals at the right edge.
5. **Type and numbers** as on `ui_kinetic_type_sheet.png`, calmer: the same heavy condensed italic everywhere; a slanted plate behind a word only for the few that matter most (PERFECT, COUNTER); damage numbers smaller than on the sheet. One typeface, with an open licence, kept in the project with its licence beside it (proposed: Barlow Condensed ExtraBold Italic).
6. The palette is the sheet's: white, hot teal, red, black. Gold and red keep their meanings on doors, the map and rewards (treasure, elite).

## How to take a picture of the interface (work order 068, 2026-11-21)

Kristian allowed pictures of the game's interface for interface work ("Ja, for grensesnittet"); nothing else is verified this way, and no sound is played. The cheapest way works without a window, through the ordinary test helper:

```
$env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\<order>\round1'
powershell -NoProfile -ExecutionPolicy Bypass -File C:/Users/kriss/.claude-automation/runner/run-tests.ps1 PlayMode -Minutes 20 -Filter "InterfacePicturesTests"
```

`Assets/Game/Tests/PlayMode/InterfacePicturesTests.cs` is skipped unless that variable names a folder outside the project (so no ordinary suite takes a picture or leaves a file). It stages moments through the game's intents (a fight with an elite, Triple Tap's chain, Backstep, a full gauge with a hit taken, an offer, the pause menu) and writes one PNG per moment, 1920 by 1080 (and one at 1280 by 720), plus `pictures.txt` with what was drawn in which order. `ROGUELIKE_CAPTURE_BEFORE=1` adds a picture with work order 063's look for comparison. Add a moment by staging it and calling `Picture("name", 1920, 1080)`.

What it does and what was learnt: a batch run with a graphics device renders cameras (the main camera into a 1920 by 1080 render texture works), but it sends no OnGUI events and yields no end of frame (`WaitForEndOfFrame` throws in batch mode), and its `Screen` is not 1920 by 1080. So the test tells the interface the screen's size (`InterfaceStyle.ScreenForPictures`; every piece reads the screen's size through `InterfaceStyle.ScreenWidth`/`ScreenHeight`), sets the camera's target to the texture, lets every piece work out its frame, renders the camera, and then calls every `OnGUI` in the scene by hand inside an IMGUI container (the engine's internal `GUIUtility.BeginContainer`/`BeginGUI`/`EndGUI`/`EndContainer`, by reflection: the way UI Toolkit's IMGUIContainer runs IMGUI outside a window), a layout pass then a repaint pass in the pieces' `GUI.depth` order, with the texture active and a pixel matrix loaded. A picture costs tokens to read: take the few that answer a question.

## Open

- The skills' icons: the pictures invent them. The twelve skills need real ones in this style.
- Whether the upgrade cards stay at the right edge or come to the figure, once the reward is a drop that is picked up.
- The exact typeface, once he has seen it in the game.
