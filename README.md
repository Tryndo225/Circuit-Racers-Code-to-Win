# Circuit Racers: Code to Win

*Circuit Racers: Code to Win* is a Unity-based racing game focused on procedural tracks, repeated driving attempts, checkpoint timing, replay review, and track sharing.

The project was originally motivated by a long-term idea of combining racing with a visual programming layer. For the bachelor thesis scope, the implementation focuses on the racing-game foundation: vehicle handling, generated/editable tracks, timing, replays, import/export, saved game data, and a player-facing UI.

---

## Documentation

Find the generated project documentation here:

<https://tryndo225.github.io/Circuit-Racers-Code-to-Win/>

Read the full project specification here:

[Specification.md](./Specification.md)

---

## Main Features

- Unity/C# racing game.
- Semi-realistic but accessible vehicle handling.
- Keyboard and gamepad support.
- Traction control and ABS assist settings.
- Procedural grid-based track generation.
- Circuit and point-to-point track support.
- Manual level editing.
- Track validation.
- Checkpoint and lap timing.
- Checkpoint split feedback.
- Replay recording and replay review.
- Track import/export through textual codes.
- Saved levels, best times, splits, replays, and assist settings.
- Low-poly inspired visual style.
- Runtime UI for menus, level browsing, generation, editing, racing, settings, and replay.

---

## Gameplay Loop

1. Generate, import, edit, or select a track.
2. Learn the layout.
3. Drive a full attempt.
4. Compare the final time and checkpoint splits.
5. Review the replay when available.
6. Retry to improve.
7. Export and share the track to challenge another player.

The game is designed around repeated improvement. Players are encouraged to learn braking points, racing lines, speed control, throttle control, and smooth driving through direct feedback from their own results.

---

## Thesis Scope

The long-term concept includes a visual programming layer where players would build a rule-based driving controller from logic blocks. That layer is not the main implementation scope of the current thesis.

The current version focuses on the systems needed for the racing foundation:

- vehicle control;
- procedural and editable track creation;
- deterministic track placement;
- race flow and checkpoints;
- timing and checkpoint splits;
- replay recording and playback;
- track sharing;
- saved data;
- user interface;
- editor/runtime extensibility.

---

## Technology

- Unity
- C#
- Unity Input System
- TextMeshPro
- Unity UI
- Doxygen documentation

---

## Repository

<https://github.com/Tryndo225/Circuit-Racers-Code-to-Win>
