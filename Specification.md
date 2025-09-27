# Program Specification — *Circuit Racers: Code to Win*

**Student:** Timotej Kotlín  
**Subjects:** NPRG035 (Basic C#), NPRG038 (Advanced C#)  
**Project Title:** Circuit Racers: Code to Win  
**Repository:** <https://github.com/Tryndo225/Circuit-Racers-Code-to-Win>

---

## Project Summary

A Unity-based educational racing game where players design a driving “AI” using a **visual logic-circuit editor**. Players assemble gates (AND, OR, IF, ACTION, …) to process car inputs (e.g., raycasts, speed, corner distance) and output driving commands (steer, accelerate, brake). The aim is to complete tracks as fast as possible, ideally beating target times. Optionally, players can race their own AI to compare performance.

---

## Motivation

This project blends **game-based learning** with **visual programming**. Instead of text coding or abstract puzzles, players solve a **real-time, spatial** problem—driving—using logic blocks. Inspired by tools like Logicly, but grounded in a physics-driven racing sim with immediate feedback. Few educational games combine logic design with live racing simulation.

---

## Key Features

### Unity Engine
- Built with **Unity 6** and **C#**.

### AI Logic Editor
- Node-based visual programming (Logicly-inspired).
- **Gates:** AND, OR, NOT, IF, ACTION, …
- **Inputs:** Raycasts, speed, distance to corner, car rotation, …
- **Outputs:** Steering, acceleration, braking.

### Game Modes
- **Medal Runs:** AI races on curated tracks to beat predefined target times.  
- **Leaderboards:** Compete on procedurally generated tracks.

### Tracks
- **Predefined** and **procedurally generated** (with threading where needed).

### Progression & Save System
- Unity serialization for saving **AIs**, **scores**, and **procedural maps**.

### Visuals
- Low-poly aesthetic (inspired by *PolyTrack*).

---

## Advanced Technologies (NPRG038)

- **Visual Programming System:** Real-time logic editor with live signal propagation.  
- **Procedural Generation:** Dynamic track generation (threaded where appropriate).  
- **Serialization:** Save/load for AI logic and race results via Unity serialization.

---

## Expected Input / Output

**Input**
- Player-built logic circuits.
- Track selection.
- Manual driving (optional): `WASD` / `↑ ↓ ← →`.

**Output**
- AI-controlled driving behavior.
- Car physics simulation.
- Lap times and leaderboard feedback.

---

## User Interface

- **Main Menu:** Track selection, AI Editor.  
- **AI Editor:** Drag-and-drop node editor with logic components.  
- **Race View:** Follow camera and minimal HUD (lap time, status).

---

## Notes

> **“AI”** refers to the **player-built logic decision circuit**.  
> No machine learning is used; the game implements a **rule-based decision system**, akin to a simple decision tree built from logic gates.
