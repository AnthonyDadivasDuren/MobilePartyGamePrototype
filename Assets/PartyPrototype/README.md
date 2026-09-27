# HinkBuddy 0.8.1

Stop Play mode, import all files, wait for compilation, then press Play. Rebuild/reinstall for Android. The title now reads HinkBuddy and the home-screen build marker is HUD 0.8.1. Use the existing Repair stale scripts menu if the old code remains loaded.

## Ranked minigame penalties

Valid Ball Tilt finishers and valid reaction results get one drink per faster valid player, capped at the chosen difficulty's base penalty:

| Difficulty | Penalties in finishing order | Fall / early tap / timeout |
| --- | --- | --- |
| Tipsy | 0, 1, 1, 1, … | 3 |
| Drunk | 0, 1, 2, 2, … | 4 |
| Smashed | 0, 1, 2, 3, 4, 4, … | 6 |

Failed players are excluded from valid ranking. Their maximum penalty is applied once. Equal measured times share a rank: for example two equal fastest reactions both get zero, and the next gets two (subject to the difficulty cap). Scores continue accumulating across a full game.

Quiz scoring is unchanged.

## Visual changes

- Header renamed to HinkBuddy.
- Ball changed to silver so it contrasts with the dark course, cream start marker and green finish.
- Removed the one-attempt/fall-penalty warning, live fall counter and early-tap warning below the reaction area.
- Actual failure outcomes still appear after a fall or early tap.

Validation covers compilation and rules, including eight-player ranking at every difficulty, failed-player penalties and ties. Rendered ball visibility still needs a Play-mode/device check.
