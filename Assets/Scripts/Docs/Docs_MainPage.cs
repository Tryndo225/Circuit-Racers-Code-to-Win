/*!
\mainpage Circuit-Racers : Code-To-Win
\tableofcontents

# Welcome

Fast, pick-up-and-play top-down racing. Generate tracks, run laps, hit checkpoints, and chase a clean final time.  
This page explains **how to play**, **controls**, and what you’ll see on screen. For developer docs, see:
- \ref track_mng "Track Management"
- \ref level_gen "Level Generation"
- \ref ui "User Interface"
- \ref editor_util "Editor Utilities"
- \ref wheel_deprecated "Deprecated Wheel Prototype"

---

# Quick Start
1. **Start**
   - From the main menu, click **Play** or **Test Track**.

1. **Start / Load a Track**
   - Use the level browser to pick a saved map, or generate a new one.
   - When generating confirm the popup preview to add it to your list or throw it away and generate another one.

2. **Grid / Start**
   - A short countdown appears. When it clears, you’re live.

3. **Race**
   - Follow the **road** to the checkpoints.
   - Cross **checkpoints** (light blue markers) in order.
   - Complete the required **laps**. Your **final time** shows at the finish.

---

# Controls

## Keyboard & Mouse
- **Throttle / Brake:** `W` / `S`  *(Up/Down Arrow also work)*
- **Steer Left / Right:** `A` / `D`  *(Left/Right Arrow also work)*
- **Handbrake:** `Space`
- **Lights (toggle):** `L`
- **Menus / UI:** Standard mouse navigation

## Gamepad (Xbox / DualShock / Generic)
- **Throttle / Brake:** Triggers (Right/Left)
- **Steer:** Left Stick (X)
- **Handbrake:** South / Cross (`A` on Xbox, `X` on PS)
- **Lights (toggle):** D-Pad Up

> The game detects your last used device and adapts steering behavior accordingly.

---

# HUD & What It Means

- **Start Filter & Timer**  
  A large overlay with a countdown. When it’s gone, your car is live.

- **Lap Time**  
  Current lap’s running timer.

- **Track Time**  
  Total time since the first lap started (hidden until you begin your first lap).

- **Lap Counter**  
  `current / total`, e.g., `2/3`.

- **Checkpoint Counter**  
  `current / total`, showing progress through the lap.

- **Finish Screen**  
  Pops up when the race is complete, showing your **Final Time**.

---

# Checkpoints & Respawn

- **Checkpoints**  
  Automatically placed along straights. You must pass them **in order**.
- **Respawn Timer**  
  If you crash, reset, or the race starts, a short **respawn timer** overlay appears.
- **Saved State**  
  On checkpoint claim, your **position**, **rotation**, and **velocities** are captured and can be used for fair respawn behavior.

---

# Track Colors (Map Preview)

- **Grass / Off-track:** Green  
- **Road / Drivable:** Gray  
- **Start:** Light-Green Marker  
- **Finish:** Red Marker (same as Start for circuit tracks)  
- **Checkpoint:** Light-Blue Marker

---

# Level Generation (Player-Facing)

- **Circuit or Point-to-Point**  
  Circuits return to the start; point-to-point ends elsewhere.
- **Size**  
  Width/Height of the grid. Bigger maps -> more complex tracks.
- **Steps**  
  Higher steps carve longer/more varied routes.
- **Preview**  
  A pixel-perfect preview texture is generated so you can verify before saving.

> Under the hood: the generator uses BFS “flooding” to carve a drivable road and ensures spacing so branches don’t crowd.

---

# Menus & UI Bits

- **Level Browser**  
  Grid of level cards. Resize responsively based on window size.
- **Level Popup**  
  Shows a large preview and action buttons (keep / discard).
- **Dropdown Scene Button**  
  A styled dropdown that changes a paired button’s action (e.g., “Play”, “Test Track”, “Quit” by scene).

---

# Tips & Driving

- **Feather the steering** don’t just hold it.
- **Handbrake** helps pivot the car on tight hairpins—use sparingly.
- **Watch the minimap / road** color in the preview to predict corners.
- **Gamepad** offers smoother steering at speed thanks to analog input.

---

# Troubleshooting

- **No Input:**  
  Check that your Input Actions are bound. If using defaults, the game will auto-create bindings. Tap any key/gamepad to re-detect the device.

- **No Sound:**  
  Ensure an AudioListener exists (usually on the main camera) and that volumes aren’t set to 0 in settings.

- **Low FPS with Large Tracks:**  
  Try smaller grid sizes or fewer steps when generating.

- **Missing Levels in Browser:**  
  Ensure you saved/kept the level from the popup after preview.

---

# For Developers

Jump into:
- \ref level_gen — generation algorithm, tiles, checkpoints
- \ref track_mng — race state machine, timers, checkpoint claims
- \ref ui — parallax, HUD, previews, menus
- \ref editor_util — custom property drawers & inspectors
- \ref wheel_deprecated — legacy prototype (not used)

Happy racing!
*/
