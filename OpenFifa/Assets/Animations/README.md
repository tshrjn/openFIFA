# Footballer animation: first-import setup

This folder holds the player animation set and the steps to wire it up the first time the project opens in the Unity Editor. Nothing here has been imported yet: no `.meta` files exist, so Unity generates them on first import. Asset sources and licenses are listed in `docs/assets/animation.md`.

```
Assets/Art/Characters/Quaternius/Mannequin_F.fbx   CC0 female mannequin (same rig as UAL2)
Assets/Animations/Quaternius/UAL1/UAL1_Standard.fbx CC0 locomotion library (old DEF-* rig names)
Assets/Animations/Quaternius/UAL2/UAL2_Standard.fbx CC0 library + male mannequin mesh (the player model)
Assets/Animations/Editor/                          import postprocessor + clip/controller builder (editor only)
Assets/Animations/Generated/                       created by the builder: placeholder action clips (.anim)
Assets/Animations/Controllers/Footballer.controller created by the builder
```

## 1. Import (automatic)

`Editor/QuaterniusImportPostprocessor.cs` runs on every import of an FBX under the two `Quaternius/` folders. It:

- sets **Rig > Animation Type = Humanoid** and **Avatar Definition = Create From This Model**;
- strips the `Rig|` and `Armature|` prefixes from clip names (`Armature|Slide_Start` becomes `Slide_Start`);
- loops every `*_Loop` clip;
- turns on **Bake Into Pose** for root rotation, Y and XZ (root motion stays off; the sim owns position);
- sets root rotation **Based Upon: Original** with a **180 deg offset** (`OrientationOffsetDeg`). The Quaternius takes travel along -Z; without the offset every locomotion clip ran backwards relative to the player root (fixed 2026-11-04). "Body Orientation" is not used because it skews the jog/sprint travel 20-30 deg off the axis;
- adds the `OnActionContact` event to `Slide_Start` and `OverhandThrow` at the contact times in `ActionClipLibrary`.

It reapplies on every reimport, so edit the script rather than the Inspector.

After the first import, check each FBX's avatar: **Rig > Configure**, all bones green.

- **UAL2 and Mannequin_F** use Unreal-style names (`pelvis`, `thigh_l`, `calf_l`, `foot_l`, `ball_l`, `upperarm_l`, `lowerarm_l`, `hand_l`, `spine_01..03`, `neck_01`, `Head`). Unity auto-maps these.
- **UAL1** uses Rigify `DEF-*` names. If auto-mapping misses a bone, map it by hand:

| Humanoid slot | UAL1 bone |
|---|---|
| Hips | DEF-hips |
| Spine / Chest / Upper Chest | DEF-spine.001 / DEF-spine.002 / DEF-spine.003 |
| Neck / Head | DEF-neck / DEF-head |
| Left Upper Leg / Lower Leg / Foot / Toes | DEF-thigh.L / DEF-shin.L / DEF-foot.L / DEF-toe.L |
| Left Shoulder / Upper Arm / Lower Arm / Hand | DEF-shoulder.L / DEF-upper_arm.L / DEF-forearm.L / DEF-hand.L |
| Right side | same with `.R` |

Leave the avatar in its T-pose (`A_TPose` exists in both libraries if you need to enforce it).

## 2. Build clips and controller

Run **OpenFifa > Animation > Build Footballer Animator**. In batch mode, use `-executeMethod OpenFifa.Editor.FootballAnimationBuilder.BuildAll`. The builder:

1. Samples the Quaternius idle pose and writes placeholder humanoid clips into `Generated/`. Each clip is keyed as muscle offsets from that pose, so they all start and end on a valid idle. The clips are `Gen_Pass_R`, `Gen_Shot_R`, `Gen_Cross_R`, `Gen_FirstTouch_R`, `Gen_Header`, `Gen_TackleStand_R`, `Gen_GkDive_R` and `Gen_Jockey_R_Loop`. In each clip the contact pose sits exactly at `ActionClipProfile.ContactTime`, and the clip fires `OnActionContact` there.
2. Creates `Controllers/Footballer.controller`:
   - **Parameters:** `Speed`, `Forward`, `Strafe`, `TurnRate`, `LocoRate` (default 1), `ActionRate` (default 1), `Mirror`.
   - **Base layer:** IK Pass is on.
   - **`Locomotion` state:** a 2D Freeform Directional blend tree on (`Strafe`, `Forward`) in m/s:
     - idle at (0, 0);
     - walk at (0, 1.0);
     - jog at (0, 5.9);
     - sprint at (0, 8.5);
     - backpedal at (0, -1.0), which is the walk clip reversed;
     - jockey at (±0.7, 0), with the left side mirrored.

     The positions are the clips' **measured natural ground speeds** (`OpenFifa.Core.LocomotionGaitTable`, measured by `LocomotionClipMeasure` from the planted foot on the UAL2 mannequin). The state's speed multiplier is `LocoRate`; `LocomotionGaitSolver` sets it to ground speed / blended natural speed, so the feet do not slide. The jog clip covers the sim's whole non-sprint range (time-scaled); the sprint clip blends in only from 6.2 m/s (full at 7.6 m/s, capped at 35% unless the sim flags a sprint). After `BuildAll` the log prints each measured speed against the table and a runtime facing check; a mismatch over 12% or a backwards clip is reported.
   - **Action states,** each using the `ActionRate` speed multiplier and the `Mirror` parameter. Each returns to `Locomotion` through an exit-time transition:
     - `Pass`, `Shot`, `Cross`, `FirstTouch`, `Header` and `TackleStand`;
     - `TackleSlide`, then `TackleSlideExit`;
     - `GkDive`, then `GetUp`;
     - `Fall`, then `GetUp`;
     - `Celebrate`;
     - `GkThrow`.

   The state and parameter names live in `OpenFifa.Core.AnimStateNames`. Change them there, never in the controller alone.

The generated clips are **blockouts**: the poses are hand-tuned muscle offsets, not mocap. Open each clip in the Animation window on the mannequin and adjust the offsets in `FootballAnimationBuilder.Poses` (muscle signs are documented there), then rebuild. When real kick/tackle mocap arrives, point the controller states at it and update `ActionClipLibrary` with the measured contact times and contact offsets. `ActionClipProfile` describes what to measure.

## 3. Player prefab

```
Player (sim root)       PlayerAnimationDriver  + your IPlayerAnimSource implementation
                        [+ SimMotionMatchingLocomotion, optional, see step 4]
  └─ Visual             empty transform; receives the warp offset (assign to Driver.visualRoot)
      └─ Model          UAL2_Standard (or Mannequin_F) instance: Animator (Footballer.controller,
                        avatar from the same FBX, Apply Root Motion OFF) + PlayerAnimatorHooks
```

- `IPlayerAnimSource` is the only coupling to the sim. Implement it on the sim bridge. It provides sim time, position, velocity, intent velocity, facing yaw (radians, the sim's unit; the driver converts to degrees once), sprint flag and the current committed action. A fixed-step source can also implement `IPlayerAnimInterpolation` (alpha = accumulator remainder / dt, plus dt) and return the raw latest snapshot; the driver then renders between the previous and current snapshots, so a 60 Hz sim stays smooth at 120 Hz. `PlayerAnimAction` carries a fresh `Id` per action, plus the kind, side, contact time, ball position at contact and outgoing direction.
- When a new action appears, the driver calls `AnimationSyncPlanner`. The planner returns:
  - the clip start time and playback rate (wind-up timing);
  - the visual translation and yaw warp, applied to `Visual` and blended in before contact then released after;
  - stride and cadence scaling for the run-up, applied through `LocoRate`;
  - strike-foot, plant-foot, hand and head IK targets, applied in `OnAnimatorIK` through `PlayerAnimatorHooks`.

  The driver never moves the sim. `ContactReached` reports the timing error (seconds) between the animation's contact frame and the sim's contact tick.
- **Foot IK hooks:** `SetExternalIkGoal(AvatarIKGoal, position, weight)` handles dribble touches and foot locks. The `IkPass` event runs custom IK after the built-in contact IK.
- **Jersey and kit materials** belong to the kit owner. This folder only provides the mesh and rig.

## 4. Motion matching (optional locomotion backend)

`Packages/manifest.json` pins `com.jlpm.motionmatching` to commit `35fc8ee`. The bridge `Scripts/Gameplay/Animation/MotionMatching/SimMotionMatchingLocomotion.cs` compiles only when that package is present: its asmdef has a `versionDefines` and `defineConstraints` gate.

1. In **Package Manager > Motion Matching > Samples > Examples**, click **Import**. This adds the `jl_motionMatching_hd` BVH locomotion take and the `JLData` MotionMatchingData asset.
2. Make a copy of `JLData` for OpenFifa. Keep the `FuturePosition` and `FutureDirection` trajectory features, which are built on the simulation bone, and generate the database (MotionMatchingData inspector > Generate Databases).
3. Set up the player hierarchy:
   - On `Player`, add `SimMotionMatchingLocomotion` and point it at a child `MotionMatchingController` that uses the new data asset.
   - On the `Model`, add a `MotionMatchingSkinnedMeshRenderer` that references that controller and has **Avoid Toes Floor Penetration** on.
   - Assign that renderer to `SimMotionMatchingLocomotion.AvatarRenderer`.

The driver then sends locomotion to motion matching instead of the blend tree. Motion matching yields the body to the Animator for full-body actions by disabling the renderer, and inertializes back when the action ends. The bridge clamps the animated body to within 12 cm of the sim position and adds the driver's warp offset.

**Quality caveat:** the only locomotion database available today is the JLPM22 sample take (general walking and jogging, no football-specific cuts, shielding or sprints). Motion matching will look better than the blend tree on turns and starts/stops, but it will not have football style until we record our own dance card (Move.ai or Rokoko; see the research note). Until then the blend-tree path is the default, and it is also the animation LOD for players far from the ball.

## 5. Verify on first open

- Run EditMode tests in category `FID_ANIM`: these are the timing, warp, stride, IK and trajectory solvers.
- Put the mannequin in a scene with a stub `IPlayerAnimSource` and check four things:
  1. no foot skating at 1.0, 5.9 and 8.5 m/s; if the feet skate, re-measure (OpenFootball > Animation > Measure Locomotion Clips) and update `LocomotionGaitTable`, then rebuild;
  2. a scheduled pass hits `ContactReached` with |error| < 1 frame;
  3. the strike foot visibly meets the ball;
  4. a left-footed action plays mirrored.
