# OpenFifa: Asset Pipeline Guide

> How to find, import, and manage game assets. Designed for agent-driven asset discovery.

---

## Art Style

**Stylized low-poly** — clean, colorful, readable at broadcast camera distance. Not photorealistic.

### Placeholder Convention (Phase 1-5)
- Players: Unity Capsule (height 1.8m, radius 0.3m) with team-colored materials
- Ball: Unity Sphere (radius 0.11m, FIFA regulation) with white material
- Pitch: Unity Plane (50m x 30m) with green material
- Goals: Unity Cubes arranged as posts (2.44m high, 5m wide) with white material
- Stadium: None — Poly Haven HDRI skybox only

### Target Art (Phase 6)
- Players: Low-poly humanoids (~5K triangles), distinct team jerseys
- Ball: Soccer ball model with PBR (~1K triangles)
- Pitch: Textured with mowed-band grass, center circle, penalty areas
- Goals: 3D posts with net mesh + physics colliders
- Stadium: Basic stands, floodlights, sideline geometry

---

## Free Asset Sources (Ranked by Priority)

### Characters

| Source | License | Format | Notes |
|--------|---------|--------|-------|
| **Quaternius Universal Base Characters** | CC0 | FBX, glTF | Best option. Male/female/teen humanoids, game-optimized. Pair with Universal Animation Library. |
| **Kenney Animated Characters 1-3** | CC0 | FBX | Modular PNG skin system — easy team jersey creation |
| **Mixamo Characters** | Free (Adobe account) | FBX | 100+ rigged characters, 5K-20K polys. Cannot redistribute raw files, in-game use OK. |
| **Sketchfab: Football Player** by Hiago Tadeu | CC-BY 4.0 | glTF, FBX | Soccer-themed, 36.6K tri. Good for stylized games. |

### Animations

| Source | License | Content | Notes |
|--------|---------|---------|-------|
| **Mixamo** | Free (Adobe) | 2500+ mocap clips | **Critical resource.** Search "soccer" for: Soccer Idle, Pass, Shoot, Slide Tackle, Header, Throw In. Also: Running, Sprinting, Jogging, Strafing. Download as "FBX for Unity", check "In Place". |
| **Rokoko Free Sports Pack** | Commercial (free) | 12 sports mocap | Professional quality, finger capture, Mixamo skeleton. |
| **Quaternius Universal Animation Library** | CC0 | 45 locomotion | Walk, run, jog, sprint in 8 directions, jump, crawl. |
| **CMU Motion Capture Database** | Research/personal | 2500+ clips | Subject #10 has soccer kicks. Use keijiro/CMUMocap GitHub package for Unity-ready format. |
| **Cascadeur** | $8-12/mo indie | AI-assisted authoring | Physics-based posing, great for custom athletic animations. |
| **DeepMotion** | Free tier available | Video → mocap | Film yourself doing soccer moves → 3D animation data. |

**Animation gaps requiring custom work**: dribbling with ball, ball trapping, bicycle kicks, goalkeeper distributing, referee signals.

### Environment

| Source | License | Content | Notes |
|--------|---------|---------|-------|
| **Poly Haven "Stadium 01" HDRI** | CC0 | 16K panoramic | 44K+ downloads. Instant stadium lighting + reflections. Also: "Stadium Exterior", "Orlando Stadium". |
| **TextureCan Soccer Grass** | Free | SBSAR procedural | Specifically designed soccer pitch with mowed bands. Customizable. |
| **ambientCG Grass004** | CC0 | Up to 8K PBR | 298K+ downloads. Full PBR maps. Good for pitch surface. |
| **R-LAB Soccer Stadium** (Sketchfab) | CC-BY | FBX, Blender | Most detailed free stadium. Replaceable billboards. |
| **Poly.pizza Football Stadium** | CC-BY 3.0 | glTF | Low-poly, optimized for macOS & iPad. |

### Soccer Ball

| Source | License | Format | Notes |
|--------|---------|--------|-------|
| **Free3D "Football Ball"** | Free | FBX, OBJ, Blend | 73.6K+ downloads. Standard soccer ball. |
| **B1Blender Soccer Ball** (Sketchfab) | CC-BY | glTF | PBR textures (albedo, normal, roughness). |
| **Unity Asset Store: Low Polygon Soccer Ball** | Free (EULA) | Unity Package | 758 favorites. Ready for Unity import. |

### Audio

| Source | License | Content | Notes |
|--------|---------|---------|-------|
| **Freesound.org** | CC0/CC-BY (varies) | Hundreds of clips | Search: "stadium", "soccer", "whistle", "kick", "crowd". WAV 44.1kHz. Free account required. |
| **Sonniss GDC Bundles** (2015-2024) | Royalty-free | 200+ GB total | Crowd reactions, impacts, whooshes, footsteps, UI sounds. Download all years. |
| **Mixkit** | Free, no attribution | 11 soccer + 36 sports + 36 crowd | WAV format. Also has background music for menus. |
| **Pixabay Audio** | Royalty-free | Ball kicks, stadium ambience, whistles | Includes a 4:58 full-match recording. |
| **OpenGameArt: Crowd Cheering** by Gregor Quendel | Free | 11 files | Purpose-built for layered stadium audio: strong/soft/rhythmic cheering. |

### UI

| Source | License | Content | Notes |
|--------|---------|---------|-------|
| **Kenney UI Pack** | CC0 | 430 assets | Buttons, sliders, panels, progress bars, 2 fonts, 6 sound effects. Gold standard. |
| **game-icons.net** | CC-BY 3.0 | 117 sport SVGs | Soccer ball, player, corner flag, trophy, goalkeeper, jersey, whistle. |
| **Google Fonts** | OFL | Free fonts | Graduate (varsity), Oswald (bold numbers), Teko (condensed display), Montserrat (body). |

### Textures

| Source | License | Notes |
|--------|---------|-------|
| **ambientCG** | CC0 | 2000+ PBR materials up to 8K. Grass, concrete, metal, fabric, leather. |
| **Poly Haven Textures** | CC0 | Photoscanned PBR. Grass, dirt, mud, leather, concrete, metal. |
| **3DTextures.me** | CC0 (1K free) | "Leather Stitched Triblade" for ball. 202+ fabric textures for jerseys. |

---

## Import Workflows

### Mixamo → Unity

1. Go to mixamo.com, sign in with Adobe account
2. Select character (or upload your own FBX)
3. Search animation (e.g., "soccer idle")
4. Download: Format = FBX Binary, Skin = With Skin, Frames = 30, Keyframe Reduction = None
5. Drop FBX into `Assets/Animations/Mixamo/`
6. Select in Unity Inspector → Rig tab → Animation Type = Humanoid → Apply
7. Configure Avatar if needed (usually auto-detects)
8. Animation tab → check "Loop Time" for locomotion, uncheck for one-shots (kick, tackle)
9. Extract animations to `Assets/Animations/` for reuse across characters

### glTF/GLB → Unity

1. Install `com.unity.cloud.gltfast` package (Unity's official glTF importer)
2. Drop .glb/.gltf file into `Assets/Models/`
3. Unity auto-imports with materials and textures
4. Check materials use URP/Lit shader (may need to convert from Standard)

### HDRI → Unity

1. Download .hdr or .exr from Poly Haven
2. Drop into `Assets/Environment/HDRIs/`
3. Create WorldEnvironment node in scene
4. Sky → PanoramaSkyMaterial → assign HDRI texture
5. In import settings: enable "HDR Clamp Exposure" if bright spots sparkle

### Audio → Unity

1. Download WAV files (preferred for SFX) or OGG (for music/ambient)
2. Drop into `Assets/Audio/SFX/` or `Assets/Audio/Music/`
3. Unity auto-imports. For SFX: Force Mono = true, Load Type = Decompress on Load
4. For ambient/music: Load Type = Streaming, Compression = Vorbis

---

## Asset Swap Protocol (Phase 6)

When replacing placeholders with real assets:

1. **Create a new story** (or use US-044 through US-048)
2. **Import the asset** following the workflow above
3. **Create a prefab variant** — don't modify the original prefab
4. **Swap references** in scenes and scripts
5. **Run ALL existing tests** — no regressions
6. **Capture new visual regression baselines** if scene appearance changed
7. **Commit with clear message**: `art(US-044): replace capsule players with Quaternius models`

### Asset Naming Convention

```
Assets/
├── Models/
│   ├── Characters/
│   │   ├── Player_Quaternius.fbx
│   │   └── Goalkeeper_Quaternius.fbx
│   ├── Ball/
│   │   └── SoccerBall_Free3D.fbx
│   └── Environment/
│       ├── GoalPost.fbx
│       └── Stadium_RLAB.fbx
├── Animations/
│   ├── Mixamo/
│   │   ├── SoccerIdle.fbx
│   │   ├── SoccerPass.fbx
│   │   └── SoccerShoot.fbx
│   └── Extracted/          # Reusable AnimationClip assets
├── Materials/
│   ├── TeamA_Jersey.mat
│   ├── TeamB_Jersey.mat
│   ├── Pitch_Grass.mat
│   └── Ball_PBR.mat
├── Textures/
│   ├── Pitch/
│   ├── Characters/
│   └── Ball/
└── Audio/
    ├── SFX/
    │   ├── Whistle_01.wav
    │   ├── Kick_01.wav
    │   └── Crowd_Goal.wav
    └── Music/
        └── Menu_Theme.ogg
```
