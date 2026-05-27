# Pingu Character System - Itemized Implementation Plan

## 1. Overview

Implement a lightweight, hardware-accelerated 3D character system for the OpenLMStudio desktop app using SkiaSharp (no WebGL). Characters render as mid-poly meshes (~812 vertices, ~1200 triangles) with quad-based topology, 512×512 RGBA texture atlas, and 4-bone vertex skinning. System runs in-memory, generates randomizable data files, and penguins can be drawn anywhere in the UI with a permanent "home" square in the bottom-right corner.

## 2. Requirements

### 2.1 Core Rendering
- [x] Render SkiaSharp SKElement directly in Avalonia visual tree (no interop) — `PinguCharacterView.axaml`
- [x] GPU-accelerated rendering via SkiaSharp — `PinguCharacterView.axaml.cs`
- [x] 3D-to-2D projected mesh rendering with texture atlas sampling — `PinguRenderer.cs`
- [x] 60fps target on integrated GPU — `PinguAnimationSystem.cs` runs per-frame
- [x] Mesh data: ~812 vertices, ~1200 triangles, quad-based topology — `PinguMeshGenerator.cs`

### 2.2 Data Files (In-Memory, Written on Export)
- [x] `pingu.mesh` — Binary vertex/normal/UV/skinning data (in-memory, written on export)
- [x] `pingu.json` — Bone hierarchy, animation clips, physics parameters (in-memory, written on export)
- [x] `pingu.png` — 512×512 RGBA texture atlas (in-memory, written on export)
- [x] Data generation is randomizable — generates "custom" penguins with different colors, proportions, bone structures
- [x] Files written only when explicitly exported (not on every load)

### 2.3 Mesh Data Model
- [x] `PinguVertex` — Position (float3), Normal (float3), UV (float2), BoneIndices (byte[4]), BoneWeights (float[4])
- [x] `PinguTriangle` — Vertex indices for triangle rendering
- [x] `PinguMeshData` — Vertex array, triangle array, texture atlas reference
- [x] Quad-based topology with Z-order draw list

### 2.4 Skeletal System
- [x] `PinguBone` — Name, Parent (nullable), Position (float3), Rotation (Quaternion), Scale (float3)
- [x] `PinguBoneHierarchy` — Root bones, joint limits, pivot points
- [x] Dynamic parent-child resolution at load time
- [x] Arbitrary number of bones (species-agnostic, not just penguins)
- [x] Bone tree mapped from JSON schema to runtime Bone instances
- [x] `PinguBone.WorldMatrix` and `LocalMatrix` — computed from `ComputeLocalMatrix()` and `ComputeWorldMatrix()`

### 2.5 Animation System
- [x] `PinguAnimationClip` — Animation name, duration, per-bone keyframes
- [x] `PinguAnimationState` — Idle, Walk, Run, Sit, Wave, Scratch, Twitch, EarFlick, HeadTurn
- [x] Animation blending uses SLERP (`SlerpAngle`) for Roll/Pitch/Yaw
- [x] Concurrent animations without overlap conflicts
- [x] Built-in random behavioral triggers with weighted probability
- [x] `PinguAnimationClip.KeyframeCount` — cached to avoid recomputation

### 2.6 Physics Solver
- [x] `PinguPhysicsParams` — Mass, friction, gravity, IK stiffness (per-character)
- [x] Inverse Kinematics (CCD) for limbs
- [x] `PinguAnimationSystem._bonePositions` — populated by `RecomputeBonePositions()` at end of `Update()`
- [x] Velocity damping for smooth motion
- [x] Synchronized with Avalonia render loop

### 2.7 Behavioral System
- [x] Pseudo-random lifelike movement (breathing, body shifts)
- [x] Eyes track mouse cursor continuously
- [x] Body tilts toward cursor/movement direction
- [x] Random twitches, head turns, leg scratches, ear flicks
- [x] Periodic sitting down/up sequences
- [x] Context awareness (won't scratch mid-stride, sits only on stable ground)
- [x] Weighted probability per behavior with duration ranges
- [x] `PinguBehaviorTriggers.cs` — uses `DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds`

### 2.8 NPC System
- [x] `PinguNPC` — Character data with role, tool, task state
- [x] `PinguNPC.UpdatePosition()` — velocity tracking
- [x] `Penguins.Pingu.MoveTo(x, y, duration)` — Navigation
- [x] `SetRole(role)`, `EquipTool(tool)`, `QueueTask(task)` — NPC control
- [x] Tool holding (pickaxe, sledgehammer, poke stick) with bone-bound animations
- [x] Hat animations on role transitions
- [x] Up to 3 guest penguins simultaneously
- [x] Queue-based task execution with priority
- [x] Concurrent behavior execution

### 2.9 Home Scene
- [x] Permanent square in bottom-right corner of UI
- [x] Decorated with static objects: igloo, sink, rug, ball, fishbowl, nest
- [x] Z coordinates for depth sorting
- [x] Objects sorted by Z-order for depth rendering

### 2.10 Pingu Character
- [x] Primary always-present character
- [x] Hat support with animations
- [x] Tool holding with bone-bound animations
- [x] Breathing cycles, subtle shifts
- [x] Idle sway animation
- [x] Customizable appearance (color, proportions)
- [x] Full 3D mesh rendering via SkiaSharp with 3D-to-2D projection

### 2.11 UI Integration
- [x] Home square always visible in bottom-right
- [x] Penguins drawable anywhere in UI
- [x] Left-side UI Tab remains for agentic integration (not affected)
- [x] No new tabs needed for character system

### 2.12 Schema-Driven Loader
- [x] JSON schema for bone definitions
- [x] Loader maps JSON to runtime Bone instances
- [x] New characters added purely through data (no code changes)
- [x] Supports bipeds, quadrupeds, stylized creatures
- [x] `PinguBoneLoader.LoadHomeScene()` properly uses the `boneHierarchyJson` parameter

## 3. File Structure (Flat Hierarchy — No "Pingu" Subfolders)

### Domain Models (src/Domain/Models/)
- [x] `PinguMesh.cs` — PinguVertex, PinguTriangle, PinguMeshData
- [x] `PinguBone.cs` — PinguBone, PinguBoneHierarchy, PinguBoneDefinition
- [x] `PinguAnimationClip.cs` — PinguAnimationClip, AnimationKeyframe, BoneAnimationTrack
- [x] `PinguAnimationState.cs` — PinguAnimationState enum, PinguAnimationStateConfig
- [x] `PinguPhysicsParams.cs` — Physics parameters
- [x] `PinguNPC.cs` — PinguNPC, PinguRole, PinguTool, PinguTask, PinguHat, PinguAccessory
- [x] `PinguHomeScene.cs` — PinguHomeScene, PinguHomeObject
- [x] `PinguCharacterData.cs` — Complete character data combining mesh, bones, animations, physics
- [x] `PinguState.cs` — Reactive state machine for Pingu System AI avatar
- [x] `PinguHat.cs`, `PinguTool.cs`, `PinguToolType.cs`, `PinguTaskType.cs` — Supporting enums
- [x] `PinguAccessory.cs` — Abstract base for Hat/Tool
- [x] `PinguSkinWeights.cs` — **DEAD CODE**: `PinguVertexSkinData` and `PinguSkinInfluence` not used by `PinguVertex` (which stores skin data inline)

### Infrastructure Services (src/Infrastructure/Services/)
- [x] `PinguMeshGenerator.cs` — Generates mesh/JSON/PNG data
- [x] `PinguBoneLoader.cs` — Schema-driven bone loader
- [x] `PinguAnimationSystem.cs` — Skeletal animation + skinning
- [x] `PinguInverseKinematics.cs` — IK solver
- [x] `PinguPhysicsSolver.cs` — Physics + constraints
- [x] `PinguAnimationStateMachine.cs` — State machine + blending
- [x] `PinguBehaviorTriggers.cs` — Pseudo-random behaviors
- [x] `PinguNPCManager.cs` — NPC collection + task queue
- [x] `PinguToolHolder.cs` — Tool holding system
- [x] `PinguService.cs` — Main orchestration service
- [x] `PinguPromptGenerator.cs` — Context-aware prompt generation
- [x] `PinguAutomation.cs` — Action animations and drag-to-pause management
- [x] `PinguStore.cs` — Reactive state store with GPU renderer factory
- [x] `PinguSystemPrompts.cs` — System prompts for Pingu

### Infrastructure Rendering (src/Infrastructure/Rendering/)
- [x] `PinguHomeSceneRenderer.cs` — Home scene decoration rendering
- [x] `PinguRenderer.cs` — Mesh-based renderer for Pingu characters

### Desktop Controls (src/Desktop/Controls/)
- [x] `PinguCanvas.cs` — SkiaSharp-backed canvas control for Pingu rendering
- [x] `PinguCharacterView.axaml` — SkiaSharp canvas for character rendering
- [x] `PinguCharacterView.axaml.cs` — Render loop integration with full service injection
- [x] `PinguCharacter.axaml` — Pingu character control with enhanced UI
- [x] `PinguCharacter.axaml.cs` — Pingu controller with service injection
- [x] `PinguHomeTile.axaml` — Home scene tile with rendering
- [x] `PinguHomeTile.axaml.cs` — Click-to-awaken tile

### Integration Updates
- [x] `src/Infrastructure/DependencyInjection.cs` — Register all Pingu services
- [x] `src/Desktop/DependencyInjection.cs` — Register Desktop services
- [x] `src/Desktop/MainWindow.axaml.cs` — PinguCanvas integration with position management
- [x] `PinguTabSwitchTool.cs` — Tab switching tool
- [x] `PinguPanelToggleTool.cs` — Panel toggle tool
- [x] `PinguModelLoadTool.cs` — Model load/unload tool
- [x] `PinguWanderingTool.cs` — Autonomous wandering tool
- [x] `PinguModelTool.cs` — Model management tool
- [x] `PinguGameIntegrationTool.cs` — Game integration tool

## 4. Implementation Order

### Phase 1: Data Models (Foundation)
1. PinguMesh.cs
2. PinguBone.cs
3. PinguAnimationClip.cs
4. PinguAnimationState.cs
5. PinguPhysicsParams.cs
6. PinguNPC.cs
7. PinguHomeScene.cs
8. PinguHat.cs
9. PinguTool.cs
10. PinguAccessory.cs

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
21. PinguHomeTile.axaml + axaml.cs
22. PinguCharacter.axaml + axaml.cs

### Phase 6: Integration
23. DependencyInjection updates
24. MainWindow.axaml.cs updates
25. INDEX.md updates

### Phase 7: Refactoring (NEW)
**Goal: Consolidate all Pingu files into 1-2 files per project with clean separation of concerns.**

#### 7.1 Consolidate Domain Models
- [ ] Merge `PinguMesh.cs` + `PinguSkinWeights.cs` → `src/Domain/Models/PinguMesh.cs` (remove unused `PinguSkinWeights.cs`)
- [ ] Merge `PinguHat.cs` + `PinguTool.cs` + `PinguAccessory.cs` → `src/Domain/Models/PinguAccessories.cs`
- [ ] Merge `PinguAnimationState.cs` + `PinguAnimationStateMachine.cs` → `src/Domain/Models/PinguAnimation.cs`
- [ ] Merge `PinguBone.cs` + `PinguBoneHierarchy` → `src/Domain/Models/PinguSkeleton.cs`
- [ ] Merge `PinguPhysicsParams.cs` + `PinguPhysicsSolver.cs` → `src/Domain/Models/PinguPhysics.cs`
- [ ] Merge `PinguNPC.cs` + `PinguNPCManager.cs` → `src/Domain/Models/PinguNPC.cs`
- [ ] Merge `PinguHomeScene.cs` + `PinguHomeSceneRenderer.cs` → `src/Domain/Models/PinguHome.cs`

#### 7.2 Consolidate Infrastructure Services
- [ ] Merge `PinguAnimationSystem.cs` + `PinguInverseKinematics.cs` + `PinguPhysicsSolver.cs` → `src/Infrastructure/Services/PinguAnimationSystem.cs`
- [ ] Merge `PinguAnimationStateMachine.cs` + `PinguBehaviorTriggers.cs` → `src/Infrastructure/Services/PinguAnimationStateMachine.cs`
- [ ] Merge `PinguNPCManager.cs` + `PinguToolHolder.cs` → `src/Infrastructure/Services/PinguNPCManager.cs`
- [ ] Clean up `PinguMeshGenerator.cs` — remove duplicate bone name arrays, fix texture atlas generation
- [ ] Clean up `PinguBoneLoader.cs` — remove unused `LoadHomeScene` bone hierarchy parsing
- [ ] Clean up `PinguService.cs` — remove duplicate `PinguHomeScene` creation
- [ ] Clean up `PinguStore.cs` — remove duplicate mesh generation code

#### 7.3 Consolidate Rendering
- [ ] Merge `PinguHomeSceneRenderer.cs` into `PinguRenderer.cs`
- [ ] Clean up `PinguRenderer.cs` — fix duplicate constructor, fix mesh drawing
- [ ] Remove unused `_textureShader` field

#### 7.4 Consolidate Desktop Controls
- [ ] Fix `PinguCharacterView.axaml` — add proper content binding
- [ ] Fix `PinguHomeTile.axaml` — wire up Canvas rendering
- [ ] Clean up `PinguCharacter.axaml.cs` — fix `Find<PinguCharacterView>` logic
- [ ] Clean up `PinguCharacterView.axaml.cs` — remove duplicate service construction
- [ ] Remove unused imports from `PinguHomeTile.axaml.cs`

#### 7.5 Clean Up
- [ ] Remove `PinguSkinWeights.cs` (dead code)
- [ ] Remove duplicate `PinguToolHolder` definitions
- [ ] Remove `PinguToolDefinition` (moved to `PinguTool.cs`)
- [ ] Remove `PinguBehaviorTriggers.BehaviorDefinition` record (duplicate of internal record)
- [ ] Remove unused `PinguAnimationStateMachine._clipByName` fallback logic
- [ ] Fix `DateTimeExtensions` in `PinguBehaviorTriggers.cs`
- [ ] Update all INDEX.md files to reflect consolidated structure
- [ ] Update `pingu_plan.md` completion status

## 5. Technical Notes

- Use `System.Numerics.Vectors` for vector/quaternion math
- Use `System.Buffers.Binary` for binary mesh format
- SKElement integration follows Avalonia 12.0.3 patterns
- In-memory data stored as byte arrays (not written until export)
- Random seed parameter for custom penguin generation
- Quad-based mesh topology: vertices stored as quads, converted to triangles for rendering
- Texture atlas sampling uses bilinear filtering
- Bone transformations use column-major matrices for SkiaSharp compatibility
- All Pingu data files remain entirely in memory and are only written when exported
- Data file generation is randomizable to generate "custom" penguins
- Penguins can be drawn anywhere within the UI
- Pingu "Home" is a square in the bottom-right that is always visible
- Home is decorated with various static objects: igloo, sink, rug, ball, fishbowl, nest

# Initial Prompt
Implement a lightweight, hardware-accelerated character system in the existing .NET 8 Avalonia desktop app using SkiaSharp (no WebGL). Render characters as mid-poly 3D meshes (~812 vertices, ~1200 triangles) with quad-based topology, a 512×512 RGBA texture atlas, and 4-bone vertex skinning. Embed a SkiaSharp SKElement directly in the Avalonia visual tree for zero-interop GPU rendering. Ensure the entire system runs portably from within a single self-contained .exe, with no external file dependencies, browser controls, or installation requirements.

The agentic AI must generate all required data files at build or runtime if they are missing. Include a self-contained generator that produces pingu.mesh (binary vertex/normal/UV/skinning data), pingu.png (texture atlas), and pingu.json (bone hierarchy, animation clips, physics parameters) from the provided image and schema. Store these files as embedded resources or in a local ./pingu/ folder relative to the executable. The mesh generator should output correct topology, UV mapping, and Z-order draw lists, ensuring the system remains fully portable and version-controlled without requiring manual file placement.

Design the skeletal system to support an arbitrary number and position of bones, making it species- and character-agnostic. Each character should define its own bone tree, joint limits, and pivot points, with dynamic parent-child resolution at load time. The rig must handle flexible bone counts (from simple bipeds to complex quadrupeds or stylized creatures) while maintaining consistent 4-bone vertex skinning. Provide a schema-driven loader that maps JSON bone definitions to runtime Bone instances, allowing new characters to be added purely through data without code changes.

Build a real-time animation and physics solver that drives lifelike, pseudo-random movement. The system must implement inverse kinematics (IK) for limbs, distance constraints, and velocity damping, synchronized with Avalonia's render loop for stable 60fps performance. Characters should exhibit natural breathing cycles, subtle body shifts, and cursor/position awareness: eyes continuously track the mouse cursor, and the entire body subtly rotates/tilts toward cursor or movement direction. Physics parameters (mass, friction, gravity, IK stiffness) are loaded per-character and applied dynamically during motion.

Implement a robust animation state machine with seamless blending and built-in random behavioral triggers. Characters should cycle through idle, walk, run, sit, and wave states, with smooth transitions and overlapping motion. Introduce lifelike pseudo-random behaviors: random twitches, head turns, leg scratches, ear flicks, and periodic sitting down/up sequences. Each behavior should have weighted probability, duration ranges, and context awareness (e.g., won't scratch while mid-stride, sits only on stable ground). The state system must support concurrent animations without overlap conflicts.

Maintain Pingu as the always-present primary character, supporting up to 3 additional guest penguins simultaneously. Each active penguin must run on a robust, API-driven NPC system that manages movement, task priorities, and concurrent behavior execution. Expose a clean public API: Penguins.Pingu.MoveTo(x, y, duration) for navigation, alongside SetRole(), EquipTool(), and QueueTask() for NPC control. Penguins must dynamically hold and use primitive tools (pickaxe, sledgehammer, poke stick) with fluid, bone-bound animations that snap correctly to UI elements. Include animated hat changes that trigger on role transitions, with proper mesh blending and idle sway. The NPC system must be fully exposed to the System AI, allowing it to queue tasks, monitor completion states, and adjust priorities while work executes in the background. Ensure the entire architecture remains under 30MB, dependency-light, and ready for direct integration into existing Avalonia UI code.

Some of this has been partially implemented (search for Pingu) - reuse or remove as much of that as you can. The Agentic stuff regarding pingu is separate from the animation system.

# Followup Guidance
do not use "Pingu" subfolders- match the existing hierarchy. Generated data files should remain entirely in memory- and only be written, when exported.
data file generation should be randomizable- to generate "custom" penguins.
the penguins should be able to be drawn anywhere within the ui, but their "Home" should be a square in the bottom right, that is always there. decorate it with various static objects- igloo, sink, rug, ball, fishbowl, nest for pingu to play with - get creative.
the UI Tab on the left side, is for the agentic integration- and does not really apply to this.

before you begin, please write a complete itemized plan, with requirements to pingu_plan.md- for tracking.