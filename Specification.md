# Program Specification — *Circuit Racers: Code to Win*

**Student:** Timotej Kotlín  
**Subjects:** NPRG035 (Basic C#), NPRG038 (Advanced C#)  
**Project Title:** Circuit Racers: Code to Win  
**Repository:** <https://github.com/Tryndo225/Circuit-Racers-Code-to-Win>

---

## Project Summary

*Circuit Racers: Code to Win* is a Unity-based racing game focused on repeated driving, procedural track variety, checkpoint-based timing, replay review, and track sharing.

The project was originally motivated by a broader long-term vision: combining a semi-realistic racing game with a visual programming layer, where players could build a rule-based driving system from logic blocks. Within the scope of this bachelor thesis, however, the implemented goal is the racing-game foundation on which such a system could later be built.

The current project therefore focuses on creating an accessible but technique-oriented racing experience. Players can generate or edit tracks, drive them repeatedly, compare times through checkpoint splits, review replays, and share tracks with others through textual export codes.

---

## Motivation

The project is motivated by the idea that racing games can support learning through repeated practice and direct feedback. In a racing context, players can gradually develop an understanding of braking points, racing lines, speed control, throttle control, and smooth vehicle handling.

The design aims to combine selected strengths of arcade and simulation racing games:

- from arcade racing games, it takes accessibility, short attempts, replayability, simple presentation, and fast restarts;
- from simulation-oriented racing games, it takes the idea that better lap times should reward correct driving technique and a basic understanding of vehicle behavior.

The game is not intended to be a full racing simulator. Instead, it aims to sit between arcade and simulation design: approachable enough for less experienced players, but still detailed enough to represent concepts such as understeer, oversteer, braking control, throttle control, traction limits, and racing-line choice.

---

## Thesis Scope

The long-term concept includes a visual programming layer where players would assemble a logic-based decision system from simple elements such as AND, OR, NOT, and IF gates. Such a system could process inputs such as speed, raycasts, or distance to corners and output driving actions such as steering, acceleration, and braking.

Implementing both the racing game and this visual programming system would be too large for the scope of a bachelor thesis. Therefore, this project focuses on the racing-game foundation:

- vehicle handling and driving assists;
- procedural and editable track creation;
- checkpoint and lap timing;
- replay recording and review;
- track import/export;
- saved level and result management;
- user interface for playing, generating, editing, and sharing tracks.

The implementation is designed with extensibility in mind so that the visual programming layer or additional gameplay modes can be added later.

---

## Key Features

### Unity Engine

- Built with **Unity** and **C#**.
- Uses Unity scenes, prefabs, serialized data, Unity UI, TextMeshPro, Unity Input System, and runtime/editor tooling.

### Vehicle Handling

- Player-controlled car with acceleration, braking, steering, and handbrake.
- Third-person follow camera.
- Keyboard and gamepad support.
- Semi-realistic handling approach intended to remain accessible while still rewarding correct driving habits.
- Configurable vehicle parameters in the Unity Editor.
- Driving-assist support, including:
  - traction control (TC);
  - anti-lock braking system (ABS);
  - simplified grip/traction behavior;
  - additional handling helpers such as anti-roll and handbrake behavior.

### Procedural Track Generation

- Grid-based abstract track layout generation.
- Support for both:
  - circuit tracks;
  - point-to-point tracks.
- Track layout validation to ensure connected and playable tracks.
- Intermediate checkpoint generation on suitable straight segments.
- Track size range intended for readable previews and responsive generation.

### Track Placement

- Generated layouts are converted into concrete Unity track prefabs.
- Track placement is deterministic so that the same map data produces the same playable track.
- Start, finish, checkpoints, and road pieces are instantiated from the abstract layout.

### Level Editing

- Manual editing of generated or saved levels.
- Supports editing tiles, start/finish placement, checkpoints, lap count, circuit mode, day/night setting, and level name.
- Validates edited maps before they are saved or used.

### Time Tracking

- Race time and lap time tracking.
- Ordered checkpoint progression.
- Checkpoint split feedback.
- Best-time storage.
- Practice/test map timing support.
- Circuit lap handling and point-to-point finish handling.

### Replay Review

- Replay recording during races.
- Replay storage for best runs.
- Replay playback with a replay car.
- Replay overlay support.
- Useful for reviewing racing line, speed, and mistakes after a completed attempt.

### Track Sharing

- Tracks can be exported into textual codes.
- Exported codes represent the track itself, not player-specific results.
- Tracks can be imported from text or clipboard.
- This supports asynchronous competition: one player can share a track and challenge another player to beat their time.

### Save System

- Stores custom levels.
- Stores best times, checkpoint splits, and best replay data.
- Stores assist settings.
- Supports replacing edited levels and preserving selected map state.

### User Interface

- Main menu.
- Level browser.
- Level generation popup.
- Level import/export UI.
- Level editor UI.
- Race HUD with lap time, track time, lap count, checkpoint count, countdown/respawn overlay, split popup, and result screen.
- Replay HUD support.
- Settings panel for driving assists.
- Notification system.
- Responsive level list layout.
- Low-poly inspired visual style.

### Visual Style

- Low-poly aesthetic.
- Clear, readable track and UI presentation.
- Simple visual language intended to avoid clutter and keep the player's focus on driving.

---

## Advanced Technologies (NPRG038)

### Procedural Generation

The project includes dynamic generation of grid-based racing layouts. The generator creates an abstract map, validates track structure, places checkpoints, and provides map data that can later be converted into physical track prefabs.

### Serialization and Persistence

The project stores custom levels, best times, checkpoint splits, replay data, and assist settings. Since Unity does not directly serialize all required structures conveniently, the project includes supporting serialization logic for maps, dictionaries, and game data.

### Replay System

The replay system records vehicle state over time and reconstructs a run during replay playback. This supports player review and strengthens the improvement loop.

### Runtime and Editor Extensibility

The project includes editor-friendly serialized helpers, custom attributes, custom drawers, and inspector-configurable systems. This supports easier tuning of vehicle behavior, track placement, UI actions, scene references, and generated content.

---

## Expected Input / Output

### Input

- Keyboard or gamepad driving input:
  - throttle;
  - brake/reverse;
  - steering;
  - handbrake;
  - lights;
  - restart/respawn.
- Track selection from saved levels.
- Procedural generation settings, such as size and circuit mode.
- Manual level editor input.
- Imported textual track codes.
- UI menu and settings input.

### Output

- A playable racing track generated or loaded from saved data.
- Player-controlled vehicle behavior.
- Race time and lap time.
- Checkpoint split feedback.
- Final result screen.
- Saved best time and split data.
- Replay data for review.
- Exported textual track code.
- Visual previews of generated and saved levels.

---

## User Interface

### Main Menu

The main menu provides access to the main game flow, including level selection, practice/test track options, and quitting the game.

### Level Browser

The level browser displays saved levels as selectable entries. Each entry can show a preview, level name, best time, and day/night information. The browser also provides actions such as generating, importing, exporting, editing, removing, and starting levels.

### Level Generation Popup

The generation popup allows the player to preview a generated level before saving it. The player can either keep the generated track or discard it and generate another one.

### Level Editor

The level editor allows the player to modify a level manually. It supports painting track tiles, grass, checkpoints, and changing level metadata.

### Race View

The race view uses a third-person follow camera and a minimal HUD. It shows the information needed while driving without overwhelming the player:

- current lap time;
- total track time;
- lap progress;
- checkpoint progress;
- countdown/respawn timer;
- checkpoint split feedback;
- finish or unfinished result panel;
- speed display.

### Replay View

The replay view plays back stored replay data and uses an overlay source adapted for replay timing and replay completion state.

### Settings

The settings panel allows the player to toggle supported driving assists such as ABS and traction control.

---

## Gameplay Loop

The core gameplay loop is based on repeated attempts and measurable improvement:

1. Generate, import, edit, or select a track.
2. Learn the track layout.
3. Drive a full attempt.
4. Read the final time and checkpoint split feedback.
5. Review the replay if available.
6. Retry and improve.
7. Export and share the track to challenge another player.

The outer competitive loop is based on asynchronous track sharing. A player can export a track, send it to another player, and compare times on the same challenge.

---

## Long-Term Vision

The current project intentionally focuses on the racing-game foundation. The long-term vision is to extend this foundation with a visual programming layer.

In that future layer, players would build a rule-based driving controller using visual logic blocks. The controller would process racing-related inputs and produce driving commands. The goal would be to teach programming logic through a concrete racing problem with immediate visual feedback.

No machine learning is planned. The intended system is a rule-based decision system assembled by the player.

---

## Notes

- The current bachelor thesis implementation focuses on the racing game itself, not on the full visual programming layer.
- The project is designed so the visual programming layer can be added later.
- Track sharing exports the track challenge only, not the exporting player's best time, splits, or replay.
- The game is intended to be accessible, replayable, and educational through driving improvement rather than through textual programming syntax.
