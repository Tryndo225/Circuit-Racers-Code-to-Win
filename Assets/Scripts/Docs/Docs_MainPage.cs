/*!
\mainpage Circuit Racers: Code-To-Win
\tableofcontents

# Welcome

Circuit Racers: Code-To-Win is a fast, pick-up-and-play racing game about generating tracks,
running clean laps, hitting checkpoints, and improving your final time.

This page explains how to play, what the controls do, and what the race HUD means.

For developer documentation, see:
- \ref systems "Systems"
- \ref car_ctrl "Vehicle Control"
- \ref track_mng "Track Management"
- \ref level_gen "Level Generation"
- \ref game_data "Game Data"
- \ref replay_system "Replay System"
- \ref audio_mgr "Audio Manager"
- \ref scene_mgmt "Scene Management"
- \ref ui "User Interface"
- \ref ui_levels "Level UI"
- \ref tools "Tools"
- \ref editor_attrs "Editor Attributes"
- \ref editor_util "Editor Utilities"
- \ref core_utils "Core Utilities"

---

# Quick Start

1. **Start from the main menu**
   - Choose **Play**, **Test Track**, or another available scene/action button.

2. **Choose or create a track**
   - Pick a saved level from the level browser.
   - Or generate a new track and inspect its preview.
   - Keep the generated track to add it to your saved level list, or discard it and generate another one.

3. **Wait for the countdown**
   - A full-screen countdown/respawn overlay appears before the car becomes active.

4. **Race**
   - Follow the road.
   - Pass checkpoints in order.
   - Complete the required number of laps.
   - The finish screen shows your final time.

---

# Controls

## Keyboard

- **Throttle:** `W` or `Up Arrow`
- **Brake / Reverse:** `S` or `Down Arrow`
- **Steer Left / Right:** `A` / `D` or `Left Arrow` / `Right Arrow`
- **Handbrake:** `Space`
- **Lights Toggle:** `L`
- **Menus / UI:** Mouse navigation
- **Open unfinished result screen during race:** `Escape`

## Gamepad

- **Throttle:** Right Trigger
- **Brake / Reverse:** Left Trigger
- **Steer:** Left Stick X axis
- **Handbrake:** South Button / Cross
- **Lights Toggle:** D-Pad Up

The vehicle controller detects the last used input device and uses that information to adjust steering behaviour.

---

# HUD and What It Means

## Countdown / Respawn Overlay

A full-screen filter and timer appear before the race starts and during respawn.
When the timer reaches zero, the car is live again.

## Lap Time

Shows the current lap's running time.

## Track Time

Shows the total elapsed track time.
It is hidden until the first lap has started.

## Lap Counter

Shows race lap progress in the form:

\code
current / total
\endcode

Example:

\code
2/3
\endcode

## Checkpoint Counter

Shows checkpoint progress through the current lap in the form:

\code
current / total
\endcode

Example:

\code
4/8
\endcode

## Checkpoint Split Popup

When a checkpoint is passed, a temporary split popup may appear.
It shows the current split time and the difference compared to the stored reference split.

## Finish Screen

When the race is complete, the finish screen shows the final race time.
Pressing `Escape` during an unfinished race may open the result screen as unfinished.

---

# Checkpoints, Laps, and Respawn

## Checkpoints

Checkpoints must be passed in order.
Generated intermediate checkpoints are placed along suitable straight sections of the track.
The placed start and finish blocks also provide checkpoint behaviour in the playable scene.

## Laps

Circuit tracks may require multiple laps.
Point-to-point tracks end at a separate finish location.

## Respawn

The race system stores checkpoint-related state so the player can be returned to a fair recent position
after crashes, resets, or race restarts.

---

# Track Preview Colors

The level preview uses simple colors to make the generated layout easy to read:

- **Grass / Off-track:** green
- **Road / Drivable track:** gray
- **Start:** light-green marker
- **Finish:** red marker
- **Checkpoint:** light-blue marker

For circuit tracks, the start and finish are represented by the same tile.

---

# Level Generation

The generator creates grid-based racing tracks.

## Track Type

- **Circuit:** the road closes back to the start.
- **Point-to-point:** the road starts and finishes at different locations.

## Size

Width and height control the grid size.
Larger maps can produce longer and more complex tracks.

## Steps

The step count controls how many times the generator attempts to extend the road.
Higher values usually create longer or more varied tracks.

## Preview

A preview texture is generated before saving so the layout can be checked visually.

Under the hood, generation uses flood-fill style path search, backtracking, spacer tiles, validation,
and checkpoint post-processing to produce playable layouts.

---

# Menus and UI

## Level Browser

Shows saved levels as level cards.
Use it to select, play, edit, delete, or inspect levels depending on the current menu.

## Level Popup

Shows a larger preview of a generated or selected level and provides action buttons such as keep,
discard, play, or edit depending on context.

## Settings

Settings can control driving assists such as ABS and traction control when available.

## Scene Buttons and Dropdowns

Some menu buttons can change their target action through a dropdown, for example switching between
play, test track, or quit actions.

---

# Tips for Driving

- Do not hold steering fully through every corner; smoother input is usually faster.
- Brake before the corner, then steer.
- Use the handbrake only when you need a sharp rotation.
- Gamepad steering gives smoother analog control.
- Watch checkpoint order and avoid cutting across the track.
- Replays and saved best times are useful for comparing your improved runs.

---

# Troubleshooting

## No Input

- Check that input actions are assigned.
- If using automatic defaults, make sure default binding creation is enabled.
- Press any keyboard or gamepad control to let the vehicle detect the active device.

## No Sound

- Make sure an AudioListener exists, usually on the main camera.
- Check music, SFX, and engine volumes.
- Check AudioMixer routing.
- Check that the relevant clips are assigned.

## Car Does Not Move

- Check that the car prefab has a Rigidbody.
- Check that all four WheelCollider references are assigned.
- Check that at least one wheel is marked as powered.
- Check that the wheels are touching a collider.

## Lights Do Not Toggle

- Check the lights input binding.
- Check that the light lists contain valid Light references.
- Check that the vehicle has a LightsController.

## Missing Levels in Browser

- Make sure the generated level was kept/saved from the popup.
- Imported levels should be valid before being added to the saved list.

## Low FPS with Large Tracks

- Use smaller grid sizes.
- Use fewer generation steps.
- Avoid excessive preview/debug generation during gameplay.

---

# For Developers

Main runtime groups:
- \ref systems "Systems"
- \ref car_ctrl "Vehicle Control"
- \ref track_mng "Track Management"
- \ref level_gen "Level Generation"
- \ref game_data "Game Data"
- \ref replay_system "Replay System"
- \ref audio_mgr "Audio Manager"
- \ref scene_mgmt "Scene Management"
- \ref ui "User Interface"
- \ref ui_levels "Level UI"
- \ref core_utils "Core Utilities"

Tooling groups:
- \ref tools "Tools"
- \ref editor_attrs "Editor Attributes"
- \ref editor_util "Editor Utilities"

Happy racing!
*/