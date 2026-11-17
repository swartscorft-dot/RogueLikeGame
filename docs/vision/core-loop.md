# Core loop

```mermaid
flowchart LR
    Hub["Hub: choose loadout"] --> Room["Room: combat"]
    Room --> Pick["Pick a tripod"]
    Pick -- "next room" --> Room
    Pick -- "last room" --> Boss["Boss"]
    Boss --> End["Run ends"]
    End -- "win or death gives lasting progress" --> Meta["Meta: new choices"]
    Meta --> Hub
```

You choose a loadout in the hub, fight room by room and pick a tripod after each room.
The run ends at the boss or at death. Death ends the run. Both give something lasting that opens new choices for the next run.
The build grows inside the run. Meta mostly opens choices, with some raw stats in noticeable, capped steps.
