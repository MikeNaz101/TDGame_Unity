# Subliminal Input Architecture (v0.1)
**Developer:** Subliminal Sarcasm Studios
**Tech Stack:** Unity / C# / Deterministic Simulation

## Overview
This middleware solution addresses the "Input Fidelity Gap" in cross-platform competitive gaming. It replaces standard float-based input polling with a quantized, deterministic frame structure, enabling high-precision netcode features without the overhead of full state serialization.

## Key Modules

### 1. The Input Normalizer (SubliminalInputs/System)
* **NormalizedInputFrame:** A 14-byte, GC-free struct that packs 16 buttons and 4 axes into a standardized packet.
* **Quantization:** Converts floating-point inputs into 16-bit integers (`short`) to guarantee mathematical determinism across different CPU architectures (Intel vs ARM).

### 2. The Predictor & Reconciler (SubliminalInputs/Core)
* **ClientSidePredictor.cs:** Implements "Move-First, Verify-Later" logic.
* **Server Reconciliation:** Detects drift > 0.05 units and performs a "Rollback and Replay" loop to enforce server authority while maintaining client responsiveness.
* **NetworkEmulator.cs:** Simulates variable latency (Ping/Jitter) to stress-test the prediction logic.

### 3. The "Black Box" Recorder (SubliminalInputs/Core)
* **InputRecorder.cs:** Serializes input history to JSON.
* **Use Case:** Allows QA teams to record a bug in-game and send the lightweight JSON file to engineering for 1:1 deterministic reproduction.

## How to Test (The Latency Lab)
1.  Open `Scenes/LatencyTestLab`.
2.  Select `NetworkSim` and set Latency to **200ms**.
3.  Press Play.
    * **Green Capsule:** Represents the Client (Zero Lag).
    * **Red Capsule:** Represents the Server (Simulated Lag).
4.  Observe that the Green capsule moves instantly, while the Red capsule trails behind, proving the prediction logic is decoupled from network confirmation.

## Integration
This system is designed as an engine-agnostic logic layer. While implemented in C# for Unity, the `NormalizedInputFrame` byte structure is compatible with C++ backend sockets.