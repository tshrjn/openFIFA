# Blender asset pipeline

Headless Blender scripts that build game assets reproducibly. The `.py` files are the source of truth; the exported
FBX files in `OpenFifa/Assets/Art/Characters/` are build outputs, committed so a Unity checkout works without Blender.

## Setup (once per machine, no sudo)

```bash
# Blender 5.2 LTS, portable
curl -LO https://download.blender.org/release/Blender5.2/blender-5.2.4-linux-x64.tar.xz   # check the .sha256 next to it
tar -xf blender-5.2.4-linux-x64.tar.xz -C ~/Blender && ln -sfn ~/Blender/blender-5.2.4-linux-x64/blender ~/.local/bin/blender
# MPFB 2.0.18 (MakeHuman for Blender) from extensions.blender.org
blender -b --factory-startup --command extension install-file -r user_default --enable add-on-mpfb-v2.0.18.zip
```

Scripts enable MPFB themselves. Don't pass `--addons` on the command line: MPFB fails to register before its
preferences exist.

## athlete_body.py: the player body (asset 1)

```bash
blender -b --factory-startup --python tools/blender/athlete_body.py -- \
    [--out OpenFifa/Assets/Art/Characters/Athlete/Athlete.fbx] [--height-cm 183] [--build slim|average|stocky] \
    [--glb out.glb] [--report out.json] [--blend out.blend]
```

The script takes about 7 s and is deterministic. It runs these steps:

1. MPFB human: male, 25 y, "ideal" proportions, muscle/weight per `--build`, plus footballer targets (thighs,
   calves, V-taper, flat stomach, neck).
2. MPFB `game_engine` rig: 53 bones with Unreal-style names (`pelvis`, `spine_01..03`, `neck_01`, `head`,
   `upperarm_l`, `thigh_l`, `ball_l`, fingers). Unity Humanoid auto-maps these, and `KitBodyMapPostprocessor`
   already has them as fallback names.
3. Strips the MakeHuman helper geometry (joint cubes, teeth, tongue, tights) but keeps the eyeballs.
4. Poses the rig into a T-pose (arms along ±X, legs straight) and applies it as the rest pose, because the Quaternius
   clips and Unity's Humanoid muscle space assume a T-pose.
5. Boots: each foot below the ankle becomes a smooth shell (foot hull → voxel remesh → inflate), skinned from the
   nearest foot weights, and the hidden toe faces are deleted. The kit shader paints it as a boot.
6. Scales to `--height-cm` (default 183, the height the locomotion gait was measured on).
7. Moves `neck_01` so the modelled eyes sit 0.226 m above it, the height where `KitPaintHead` paints them. This also
   puts the crew collar at the base of the neck and the neck pivot where the clips expect it.
8. LODs: `Athlete_LOD0/1/2` (≈22k / 8k / 3k triangles), made with symmetric collapse decimation, which
   interpolates the skin weights. Each LOD keeps at most 4 influences per vertex, normalised. Unity builds the LOD
   Group from the suffixes.
9. FBX export: metres, -Z forward, Y up, no leaf bones, no animation, and two material slots (`M_Body`, `M_Eyes`).
   On import, `KitBodyMapPostprocessor` replaces both slots with the kit body material.

### Unity side

- `Assets/Art/Characters/Athlete/` is handled like the Quaternius folders (`QuaterniusImportPostprocessor.AthleteRoot`):
  Humanoid avatar created from the model, file scale 1:1, and the kit body map baked into UV2/UV3 on import.
- The capture scene uses the athlete unless `OPENFIFA_PLAYER_MODEL=mannequin` is set at build time (for A/B renders).
- Check an import with
  `Unity -batchmode -projectPath OpenFifa -executeMethod OpenFifa.Editor.AthleteImportReport.Run`.

## Conventions for further assets

- **Units and axes**: metres, Blender Z-up / -Y front. Export FBX with `-Z` forward and `Y` up; don't bake space
  transforms on armatures.
- **Format**: use FBX for anything skinned (Unity's native importer, Humanoid avatars). glTF (`com.unity.cloud.gltfast`)
  is only worth adding for static props that need PBR materials to come across automatically.
- **Rig**: the MPFB `game_engine` skeleton for every human (players, referees, crowd impostors), in a T-pose rest
  pose, at most 4 weights per vertex.
- **Materials**: the project is URP (17.3, `URPRendererData`). Export placeholder materials only, and assign URP
  Lit or kit shader materials in an import postprocessor so a re-import never loses them.
- **Naming**: `<Asset>_LOD<n>` for LOD meshes, `M_<Name>` for material slots, and one folder per asset under
  `Assets/Art/`.
- **Binaries**: the repo doesn't use git-lfs yet. Keep the FBX small (Athlete.fbx is about 2.5 MB). Before textures
  and bakes arrive, add `*.fbx *.png *.exr *.blend filter=lfs` to `.gitattributes`.
- **Licences**: MakeHuman / MPFB assets (base mesh, targets, rigs, weights) are CC0. The MPFB code is GPL-3 but
  it never ships in the game; only its output does.
