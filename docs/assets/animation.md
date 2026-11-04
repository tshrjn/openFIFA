# Animation assets and packages (third-party)

Everything here is free for commercial use and allowed in a public repo. Mixamo is **not** used: it needs an Adobe login, and its terms forbid redistributing the raw files.

## Files in the repo

| File in repo | Source | Author | License | Size | SHA-256 |
|---|---|---|---|---|---|
| `OpenFifa/Assets/Animations/Quaternius/UAL1/UAL1_Standard.fbx` (renamed from `Animation Library[Standard]/Unity/AnimationLibrary_Unity_Standard.fbx`) | Universal Animation Library, Standard. Downloaded from the OpenGameArt mirror https://opengameart.org/content/universal-animation-library (zip `universal_animation_librarystandard.zip`, sha256 `18ff1a72…a3ac2`). Official page: https://quaternius.com/packs/universalanimationlibrary.html | Quaternius | CC0 1.0 (stated on both pages and in the bundled `License.txt`) | 20.6 MB | `42f16a4ed61d1e9f5fc7c2526894c5487148ac6ba82b64281ceb7943570f3c37` |
| `OpenFifa/Assets/Animations/Quaternius/UAL2/UAL2_Standard.fbx` (also contains the male mannequin mesh used as the player model) | Universal Animation Library 2, Standard. https://opengameart.org/content/universal-animation-library-2 (zip `universal_animation_library_2standard.zip`, sha256 `ec0e40d6…a9217`) | Quaternius | CC0 1.0 | 24.4 MB | `d4a2dd67bb12bf0c01891bc59ee697e04db679d26883d30bd937c2f3fb6fec90` |
| `OpenFifa/Assets/Art/Characters/Quaternius/Mannequin_F.fbx` | Same UAL2 zip, `Female Mannequin/Unity/Mannequin_F.fbx` (mesh and rig, no clips; same rig as UAL2) | Quaternius | CC0 1.0 | 0.6 MB | `890784432838b1f4f9194fd6011769888872a7ba7f3159689771e1998c96a42c` |

Each folder also holds a copy of the pack's `License.txt`. Total added: about 45.6 MB of binary FBX.

Both OpenGameArt zips are older than Quaternius's current itch.io builds. UAL1 v2.0 (2026-01-23) renamed the rig to match UAL2, but this mirror still has the older Rigify-style `DEF-*` bone names, so the UAL1 and UAL2 rigs differ. Unity's Humanoid retargeting handles that, because each FBX gets its own avatar. The itch.io page answered 403 to non-browser fetches.

### Clips available (Standard tiers)

- **UAL1 (46):** Idle_Loop, Walk_Loop, Jog_Fwd_Loop (0.93 s), Sprint_Loop (0.67 s), Crouch_*, Jump_Start/Loop/Land, Roll, Death01, Hit_Chest/Head, Dance_Loop, Punch_*, Sword_*, Pistol_*, Spell_*, Swim_*, Sitting_*, and others. All loops are in place.
- **UAL2 (43):** Slide_Start/Loop/Exit, Hit_Knockback(+_RM), LayToIdle, OverhandThrow, NinjaJump_*, Yes, Idle_FoldArms_Loop, Melee_*, Sword_*, Shield_*, Zombie_*, Farm_*, and others.

**Mapped to football:** idle, walk, jog and sprint come from UAL1; backpedal is Walk_Loop reversed; the slide tackle is UAL2 Slide_Start then Slide_Exit; the fall is Hit_Knockback then LayToIdle; the celebration is Dance_Loop; the goalkeeper throw is OverhandThrow.
**Missing from every free CC0 source found:** kick, pass, shot, cross, header, standing tackle, goalkeeper dive, jockey/strafe, turn-in-place and start/stop clips. `Assets/Animations/Editor/FootballAnimationBuilder.cs` generates placeholder humanoid blockouts for these. Turns, starts and stops come from the sim-driven warp and the blend tree, or from motion matching when it is enabled.

## Package

| Package | Source | License | Pin |
|---|---|---|---|
| `com.jlpm.motionmatching` 0.3.2 (Motion Matching for Unity, Jose Luis Ponton) | https://github.com/JLPM22/MotionMatching (`?path=/com.jlpm.motionmatching`) | MIT (`LICENSE`, © 2022-present Jose Luis Ponton) | commit `35fc8ee9a0fd97f12de458ea81eaef7c8219d87a` (main, 2026-04-24; the repo has no tags) |

The package pulls in `com.unity.burst`, `com.unity.splines` and `com.unity.inputsystem` from the Unity registry. Its sample (Package Manager > Samples > Examples) includes `jl_motionMatching_hd` (12.6 MB BVH, about 7 min of unstructured locomotion), which is the author's own capture and is covered by the repo's MIT license. That take is the motion-matching locomotion database. The sample is imported into `Assets/Samples/` on demand and is not committed by this change.

## Evaluated and not used

- **MxM fork (Frost-Blade-Studios/Motion-Matching-for-Unity):** MIT, 63 stars, pushed 2026-07. It consumes AnimationClips, so it could use the Quaternius clips directly, but those clips are one loop per gait with no turns, starts, stops or strafes. That is far too sparse for a motion-matching search to look good. JLPM22 ships a real locomotion dance card and is Unity 6-native, which matters more.
- **CMU Graphics Lab mocap** (has soccer kicks): the license allows commercial use but forbids reselling the data, which is not a CC0/MIT/CC-BY grant. The BVH would also need Blender to convert to FBX, and Blender is not on CodeBox. A future option that needs owner sign-off.
- **AI4Animation, LAFAN1, Bandai-Namco motion dataset:** non-commercial or no-derivatives licenses. Excluded.
- **Quaternius UAL Pro and Source tiers:** paid ($9.99+). Not needed for this pass.
