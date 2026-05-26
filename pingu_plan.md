# Pingu Character System - Itemized Implementation Plan

## 1. Overview

Implement a lightweight, hardware-accelerated 3D character system for the OpenLMStudio desktop app using SkiaSharp (no WebGL). Characters render as mid-poly meshes (~812 vertices, ~1200 triangles) with quad-based topology, 512×512 RGBA texture atlas, and 4-bone vertex skinning. System runs in-memory, generates randomizable data files, and penguins can be drawn anywhere in the UI with a permanent "home" square in the bottom-right corner.

## 2. Requirements

### 2.1 Core Rendering
- [ ] Render SkiaSharp SKElement directly in Avalonia visual tree (no interop)
- [ ] GPU-accelerated rendering via SkiaSharp
- [ ] 3D-to-2D projected mesh rendering with texture atlas sampling
- [ ] 60fps target on integrated GPU
- [ ] Mesh data: ~812 vertices, ~1200 triangles, quad-based topology

### 2.2 Data Files (In-Memory, Written on Export)
- [ ] `pingu.mesh` — Binary vertex/normal/UV/skinning data (in-memory, written on export)
- [ ] `pingu.json` — Bone hierarchy, animation clips, physics parameters (in-memory, written on export)
- [ ] `pingu.png` — 512×512 RGBA texture atlas (in-memory, written on export)
- [ ] Data generation is randomizable — can generate "custom" penguins with different colors, proportions, bone structures
- [ ] Files written only when explicitly exported (not on every load)

### 2.3 Mesh Data Model
- [ ] `PinguVertex` — Position (float3), Normal (float3), UV (float2), BoneIndices (byte[4]), BoneWeights (float[4])
- [ ] `PinguTriangle` — Vertex indices for triangle rendering
- [ ] `PinguMeshData` — Vertex array, triangle array, texture atlas reference
- [ ] Quad-based topology with Z-order draw list

### 2.4 Skeletal System
- [ ] `PinguBone` — Name, Parent (nullable), Position (float3), Rotation (Quaternion), Scale (float3)
- [ ] `PinguBoneHierarchy` — Root bones, joint limits, pivot points
- [ ] Dynamic parent-child resolution at load time
- [ ] Arbitrary number of bones (species-agnostic, not just penguins)
- [ ] Bone tree mapped from JSON schema to runtime Bone instances

### 2.5 Animation System
- [ ] `PinguAnimationClip` — Animation name, duration, per-bone keyframes
- [ ] `PinguAnimationState` — Idle, Walk, Run, Sit, Wave, Scratch, Twitch, EarFlick, HeadTurn
- [ ] Animation state machine with seamless blending (SLERP between bone rotations)
- [ ] Concurrent animations without overlap conflicts
- [ ] Built-in random behavioral triggers with weighted probability

### 2.6 Physics Solver
- [ ] `PinguPhysicsParams` — Mass, friction, gravity, IK stiffness (per-character)
- [ ] Inverse Kinematics (CCD) for limbs
- [ ] Distance constraints for body segments
- [ ] Velocity damping for smooth motion
- [ ] Synchronized with Avalonia render loop

### 2.7 Behavioral System
- [ ] Pseudo-random lifelike movement (breathing, body shifts)
- [ ] Eyes track mouse cursor continuously
- [ ] Body tilts toward cursor/movement direction
- [ ] Random twitches, head turns, leg scratches, ear flicks
- [ ] Periodic sitting down/up sequences
- [ ] Context awareness (won't scratch mid-stride, sits only on stable ground)
- [ ] Weighted probability per behavior with duration ranges

### 2.8 NPC System
- [ ] `PinguNPC` — Character data with role, tool, task state
- [ ] `Penguins.Pingu.MoveTo(x, y, duration)` — Navigation
- [ ] `SetRole(role)`, `EquipTool(tool)`, `QueueTask(task)` — NPC control
- [ ] Tool holding (pickaxe, sledgehammer, poke stick) with bone-bound animations
- [ ] Hat animations on role transitions
- [ ] Up to 3 guest penguins simultaneously
- [ ] Queue-based task execution with priority
- [ ] Concurrent behavior execution

### 2.9 Home Scene
- [ ] Permanent square in bottom-right corner of UI
- [ ] Decorated with static objects: igloo, sink, rug, ball
- [ ] Creative decorations (additional props as needed)
- [ ] Penguins interact with home scene objects

### 2.10 Pingu Character
- [ ] Primary always-present character
- [ ] Full 3D mesh rendering
- [ ] Hat support with animations
- [ ] Tool holding with bone-bound animations
- [ ] Breathing cycles, subtle shifts
- [ ] Idle sway animation
- [ ] Customizable appearance (color, proportions)

### 2.11 UI Integration
- [ ] Home square always visible in bottom-right
- [ ] Penguins drawable anywhere in UI
- [ ] Left-side UI Tab remains for agentic integration (not affected)
- [ ] No new tabs needed for character system

### 2.12 Schema-Driven Loader
- [ ] JSON schema for bone definitions
- [ ] Loader maps JSON to runtime Bone instances
- [ ] New characters added purely through data (no code changes)
- [ ] Supports bipeds, quadrupeds, stylized creatures

## 3. File Structure (Flat Hierarchy — No "Pingu" Subfolders)

### Domain Models (src/Domain/Models/)
- [ ] `PinguMesh.cs` — PinguVertex, PinguTriangle, PinguMeshData
- [ ] `PinguBone.cs` — PinguBone, PinguBoneHierarchy
- [ ] `PinguSkinWeights.cs` — Per-vertex skin weight data
- [ ] `PinguAnimationClip.cs` — PinguAnimationClip, AnimationKeyframe
- [ ] `PinguAnimationState.cs` — PinguAnimationState enum
- [ ] `PinguPhysicsParams.cs` — Physics parameters
- [ ] `PinguNPC.cs` — PinguNPC, PinguRole, PinguTool
- [ ] `PinguHomeScene.cs` — PinguHomeScene, PinguHomeObject
- [ ] `PinguHat.cs` — PinguHat
- [ ] `PinguTool.cs` — PinguToolDefinition

### Infrastructure Services (src/Infrastructure/Services/)
- [ ] `PinguMeshGenerator.cs` — Generates mesh/JSON/PNG data
- [ ] `PinguBoneLoader.cs` — Schema-driven bone loader
- [ ] `PinguAnimationSystem.cs` — Skeletal animation + skinning
- [ ] `PinguInverseKinematics.cs` — IK solver
- [ ] `PinguPhysicsSolver.cs` — Physics + constraints
- [ ] `PinguAnimationStateMachine.cs` — State machine + blending
- [ ] `PinguBehaviorTriggers.cs` — Pseudo-random behaviors
- [ ] `PinguNPCManager.cs` — NPC collection + task queue
- [ ] `PinguToolHolder.cs` — Tool holding system
- [ ] `PinguHomeSceneRenderer.cs` — Home scene decoration

### Desktop Controls (src/Desktop/Controls/)
- [ ] `PinguCharacterView.axaml` — SKElement host
- [ ] `PinguCharacterView.axaml.cs` — Render loop integration
- [ ] `PinguHomeSquare.axaml` — Home square with decorations
- [ ] `PinguHomeSquare.axaml.cs` — Home square logic
- [ ] `PinguCharacter.axaml` — Pingu character (enhanced)
- [ ] `PinguCharacter.axaml.cs` — Pingu controller

### Integration Updates
- [ ] `src/Infrastructure/DependencyInjection.cs` — Register new services
- [ ] `src/Desktop/DependencyInjection.cs` — Register SKElement control
- [ ] `src/Desktop/MainWindow.axaml.cs` — Update Pingu integration

### INDEX.md Updates
- [ ] `src/Domain/INDEX.md` — Add new model files
- [ ] `src/Infrastructure/INDEX.md` — Add new service files
- [ ] `src/Desktop/INDEX.md` — Add new control files
- [ ] `pingu_plan.md` — This file (already created)

## 4. Implementation Order

### Phase 1: Data Models (Foundation)
1. PinguMesh.cs
2. PinguBone.cs
3. PinguSkinWeights.cs
4. PinguAnimationClip.cs
5. PinguAnimationState.cs
6. PinguPhysicsParams.cs
7. PinguNPC.cs
8. PinguHomeScene.cs
9. PinguHat.cs
10. PinguTool.cs

### Phase 2: Data Generation & Loading
11. PinguMeshGenerator.cs (generates mesh/JSON/PNG in-memory)
12. PinguBoneLoader.cs (schema-driven loader)

### Phase 3: Animation & Physics
13. PinguAnimationSystem.cs
14. PinguInverseKinematics.cs
15. PinguPhysicsSolver.cs
16. PinguAnimationStateMachine.cs
17. PinguBehaviorTriggers.cs

### Phase 4: NPC System
18. PinguNPCManager.cs
19. PinguToolHolder.cs

### Phase 5: UI Controls
20. PinguCharacterView.axaml + axaml.cs
21. PinguHomeSquare.axaml + axaml.cs
22. PinguCharacter.axaml + axaml.cs

### Phase 6: Integration
23. DependencyInjection updates
24. MainWindow.axaml.cs updates
25. INDEX.md updates

## 5. Technical Notes

- Use `System.Numerics.Vectors` for vector/quaternion math
- Use `System.Buffers.Binary` for binary mesh format
- SKElement integration follows Avalonia 12.0.3 patterns
- In-memory data stored as byte arrays (not written until export)
- Random seed parameter for custom penguin generation
- Quad-based mesh topology: vertices stored as quads, converted to triangles for rendering
- Texture atlas sampling uses bilinear filtering
- Bone transformations use column-major matrices for SkiaSharp compatibility

# Initial Prompt
Implement a lightweight, hardware-accelerated character system in the existing .NET 8 Avalonia desktop app using SkiaSharp (no WebGL). Render characters as mid-poly 3D meshes (~812 vertices, ~1200 triangles) with quad-based topology, a 512×512 RGBA texture atlas, and 4-bone vertex skinning. Embed a SkiaSharp SKElement directly in the Avalonia visual tree for zero-interop GPU rendering. Ensure the entire system runs portably from within a single self-contained .exe, with no external file dependencies, browser controls, or installation requirements.

The agentic AI must generate all required data files at build or runtime if they are missing. Include a self-contained generator that produces pingu.mesh (binary vertex/normal/UV/skinning data), pingu.png (texture atlas), and pingu.json (bone hierarchy, animation clips, physics parameters) from the provided image and schema. Store these files as embedded resources or in a local ./pingu/ folder relative to the executable. The mesh generator should output correct topology, UV mapping, and Z-order draw lists, ensuring the system remains fully portable and version-controlled without requiring manual file placement.

Design the skeletal system to support an arbitrary number and position of bones, making it species- and character-agnostic. Each character should define its own bone tree, joint limits, and pivot points, with dynamic parent-child resolution at load time. The rig must handle flexible bone counts (from simple bipeds to complex quadrupeds or stylized creatures) while maintaining consistent 4-bone vertex skinning. Provide a schema-driven loader that maps JSON bone definitions to runtime Bone instances, allowing new characters to be added purely through data without code changes.

Build a real-time animation and physics solver that drives lifelike, pseudo-random movement. The system must implement inverse kinematics (IK) for limbs, distance constraints, and velocity damping, synchronized with Avalonia’s render loop for stable 60fps performance. Characters should exhibit natural breathing cycles, subtle body shifts, and cursor/position awareness: eyes continuously track the mouse cursor, and the entire body subtly rotates/tilts toward cursor or movement direction. Physics parameters (mass, friction, gravity, IK stiffness) are loaded per-character and applied dynamically during motion.

Implement a robust animation state machine with seamless blending and built-in random behavioral triggers. Characters should cycle through idle, walk, run, sit, and wave states, with smooth transitions and overlapping motion. Introduce lifelike pseudo-random behaviors: random twitches, head turns, leg scratches, ear flicks, and periodic sitting down/up sequences. Each behavior should have weighted probability, duration ranges, and context awareness (e.g., won’t scratch while mid-stride, sits only on stable ground). The state system must support concurrent animations without overlap conflicts.

Maintain Pingu as the always-present primary character, supporting up to 3 additional guest penguins simultaneously. Each active penguin must run on a robust, API-driven NPC system that manages movement, task priorities, and concurrent behavior execution. Expose a clean public API: Penguins.Pingu.MoveTo(x, y, duration) for navigation, alongside SetRole(), EquipTool(), and QueueTask() for NPC control. Penguins must dynamically hold and use primitive tools (pickaxe, sledgehammer, poke stick) with fluid, bone-bound animations that snap correctly to UI elements. Include animated hat changes that trigger on role transitions, with proper mesh blending and idle sway. The NPC system must be fully exposed to the System AI, allowing it to queue tasks, monitor completion states, and adjust priorities while work executes in the background. Ensure the entire architecture remains under 30MB, dependency-light, and ready for direct integration into existing Avalonia UI code.

Some of this has been partially implemented (search for Pingu) - reuse or remove as much of that as you can. The Agentic stuff regarding pingu is separate from the animation system.

# Followup Guidance
do not use "Pingu" subfolders- match the existing heirarchy. Generated data files should remain entirely in memory- and only be written, when exported.
data file generation should be randomizable- to generate "custom" penguins.
the penguins should be able to be drawn anywhere within the ui, but their "Home" should be a square in the bottom right, that is always there. decorate it with various static objects- igloo, sink, rug, ball for pingu to play with - get creative.
the UI Tab on the left side, is for the agentic integration- and does not really apply to this.

before you begin, please write a complete itemized plan, with requirements to pingu_plan.md- for tracking.
