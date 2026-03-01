# Phase B: Visual Quality Upgrade — Primitive Geometry to EA FC 26 Rush Mode Reference

## Executive Summary

Phase B transforms OpenFifa from functional prototype (primitive capsules, spheres, planes) to a visual experience approximating EA FC 26 Rush Mode. Organized into three sub-phases by visual impact:

- **B1 (Immediate Visual Impact)**: Pitch grass shader, goal nets, ball model, URP post-processing, stadium lighting
- **B2 (Stadium and Environment)**: Advertising boards, crowd geometry, arena walls, photographers
- **B3 (Characters and Polish)**: Humanoid player models, HUD redesign, AI pass/through indicators, celebration VFX

## Current State (Post Phase A)

All runtime geometry is created by `MatchOrchestrator.cs` (687 lines):
- Pitch: green Cube scaled to 50x30m, LineRenderer circle/markings, Cube walls
- Goals: Cylinder posts, Quad net (single flat plane)
- Players: Capsule primitives with team color material
- Ball: White sphere primitive
- Camera: BroadcastCameraController at 40° elevation
- HUD: Basic TMP text (score, timer, controls)

## Reference Target

14 labeled frames from EA FC 26 Rush Mode at `docs/references/rush_frames/`.

---

## Sub-Phase B1: Immediate Visual Impact

### B1.1 — URP Post-Processing
- **Files**: `Scripts/Core/PostProcessingConfig.cs` (NEW), `DefaultVolumeProfile.asset` (MODIFY), `QualitySettings.asset` (MODIFY)
- **What**: Add Bloom (threshold 1.2, intensity 0.3), ColorAdjustments (warm lift, cool shadows), Vignette (0.2), AmbientOcclusion
- **Ref**: rush_frames/03-05 — warm night atmosphere

### B1.2 — Stadium Lighting System
- **Files**: `Scripts/Core/StadiumLightingConfig.cs` (NEW), `Scripts/Gameplay/StadiumLightingBuilder.cs` (NEW)
- **What**: 4 spot floodlights at corners + directional fill, cast shadows, warm color temperature
- **Ref**: rush_frames/03, 10 — visible shadows, warm light pools

### B1.3 — Pitch Grass Bands Shader
- **Files**: `Scripts/Core/PitchVisualConfig.cs` (NEW), `Scripts/Gameplay/PitchVisualBuilder.cs` (NEW), `PitchBuilder.cs` (MODIFY)
- **What**: Procedural Texture2D with alternating light/dark green stripes, applied to URP/Lit material on Quad mesh
- **Ref**: rush_frames/03-05 — mowed grass bands

### B1.4 — Pitch Line Markings Upgrade
- **Files**: `Scripts/Core/PitchMarkingsConfig.cs` (NEW), `PitchBuilder.cs` (MODIFY)
- **What**: Dashed halfway line, penalty areas, center circle, corner arcs — thin mesh quads above pitch
- **Ref**: rush_frames/03 — dashed halfway, penalty box outlines

### B1.5 — Goal Net Mesh
- **Files**: `Scripts/Core/GoalNetConfig.cs` (NEW), `Scripts/Gameplay/GoalNetBuilder.cs` (NEW), `MatchOrchestrator.cs` (MODIFY)
- **What**: 3D procedural net mesh (grid of thin quad strips) behind goal frame
- **Ref**: rush_frames/07, 09 — visible net mesh pattern

### B1.6 — Soccer Ball Model
- **Files**: `Scripts/Gameplay/BallVisualSetup.cs` (NEW), `MatchOrchestrator.cs` (MODIFY)
- **What**: URP/Lit material with procedural black/white panel texture on sphere

### B1.7 — Active Player Indicator Ring
- **Files**: `Scripts/Gameplay/PlayerIndicatorRing.cs` (NEW), `MatchOrchestrator.cs` (MODIFY)
- **What**: Flat yellow ring at player's feet (replace floating sphere)
- **Ref**: rush_frames/05 — yellow circle at feet

---

## Sub-Phase B2: Stadium and Environment

### B2.1 — Advertising Boards
- **Files**: `Scripts/Core/AdvertisingBoardConfig.cs` (NEW), `Scripts/Gameplay/AdvertisingBoardBuilder.cs` (NEW)
- **What**: Quads along sidelines with emissive LED-style materials, branded text
- **Ref**: rush_frames/07, 08 — "GilletteLabs" boards

### B2.2 — Arena Walls and Structure
- **Files**: `Scripts/Core/ArenaStructureConfig.cs` (NEW), `Scripts/Gameplay/ArenaStructureBuilder.cs` (NEW)
- **What**: Dark perimeter walls behind ad boards (8m), angular canopy, neon green accents
- **Ref**: rush_frames/03, 10 — enclosed arena feel

### B2.3 — Crowd Geometry
- **Files**: `Scripts/Core/CrowdGeometryConfig.cs` (NEW), `Scripts/Gameplay/CrowdBillboardBuilder.cs` (NEW)
- **What**: Billboard sprite rows behind ad boards, GPU instanced, procedural silhouette textures
- **Ref**: rush_frames/03, 13 — crowd density

### B2.4 — Photographer/Sideline Detail
- **Files**: `Scripts/Gameplay/SidelineDetailBuilder.cs` (NEW)
- **What**: Silhouette quads along near touchline, camera flash on goal
- **Ref**: rush_frames/03, 04 — photographers near corners

### B2.5 — Dark Arena Skybox
- **Files**: `StadiumBuilder.cs` (MODIFY)
- **What**: Procedural dark blue-gray gradient skybox for indoor night atmosphere

---

## Sub-Phase B3: Characters, HUD, and Polish

### B3.1 — Player Name Tags
- **Files**: `Scripts/Core/NameTagConfig.cs` (NEW), `Scripts/UI/PlayerNameTag.cs` (NEW)
- **What**: World-space Canvas with TMP text above each player, billboard-faces camera
- **Ref**: rush_frames/03, 05 — white text on dark background

### B3.2 — AI Pass/Through Indicators
- **Files**: `Scripts/Core/PassIndicatorLogic.cs` (NEW), `Scripts/UI/PassIndicatorUI.cs` (NEW)
- **What**: Green labels ("Pass"/"Through") above teammates when human has ball
- **Ref**: rush_frames/05, 13 — green labels

### B3.3 — HUD Redesign
- **Files**: `Scripts/Core/HUDStyleConfig.cs` (NEW), `Scripts/UI/RushScoreboard.cs` (NEW)
- **What**: Styled scoreboard bar (green accent), team labels, timer, player name strip
- **Ref**: rush_frames/03 — "HOM 0-0 AWY" bar

### B3.4 — Humanoid Player Models
- **Files**: `Scripts/Core/HumanoidMeshConfig.cs` (NEW), `Scripts/Gameplay/ProceduralHumanoidBuilder.cs` (NEW)
- **What**: Low-poly humanoid mesh (boxes/cylinders for body parts), team color jerseys
- **Ref**: rush_frames/03, 05, 06 — human-shaped silhouettes

### B3.5 — Goal Celebration VFX
- **Files**: `Scripts/UI/GoalSplashUI.cs` (NEW), `Scripts/Gameplay/ConfettiEffect.cs` (NEW)
- **What**: "GOAL!" splash with green chevron design, confetti particles
- **Ref**: rush_frames/08, 12 — celebration splash

### B3.6 — Camera Polish
- **Files**: `CameraConfigData.cs` (MODIFY), `BroadcastCameraController.cs` (MODIFY)
- **What**: Dynamic FOV (wider midfield, tighter near goal), celebration zoom

---

## Implementation Order

| Session | Steps | Impact |
|---------|-------|--------|
| 1 | B1.1 + B1.2 | Scene lighting transformed |
| 2 | B1.3 + B1.4 | Pitch becomes soccer field |
| 3 | B1.5 + B1.6 + B1.7 | Goals and ball look real |
| 4 | B2.1 + B2.4 | Sideline visual edge |
| 5 | B2.2 + B2.5 | Arena enclosure complete |
| 6 | B2.3 | Stadium populated |
| 7 | B3.1 + B3.2 | Players have identity |
| 8 | B3.3 | HUD matches reference |
| 9 | B3.4 | Players become humanoid |
| 10 | B3.5 + B3.6 | Celebrations and camera |

## Dependencies

```
B1.1 → B1.2 (needs HDR pipeline)
B1.3 → B1.4 (same file)
B2.1 → B2.2 → B2.3 (stadium layers)
B3.1 → B3.2 (similar UI pattern)
B3.3 → B3.5 (HUD for splash)
```

## PRD Story Mapping

| PRD Story | Phase B Steps |
|-----------|--------------|
| US-044 (Character models) | B3.4 |
| US-046 (Stadium environment) | B1.2, B1.3, B1.5, B2.1-B2.5 |
| US-047 (Ball model) | B1.6 |
| US-052 (Dynamic lighting) | B1.1, B1.2 |
| US-053 (Crowd animation) | B2.3 |

## Risk Mitigation

1. **Shader fallback**: All materials try URP/Lit first, fall back to Sprites/Default for batch mode
2. **Performance**: Stadium geometry uses static batching, crowd uses GPU instancing
3. **MatchOrchestrator size**: Extract builders into separate classes rather than adding inline
4. **Mesh cleanup**: Procedural meshes use ObjectPool pattern, Destroy on unload
