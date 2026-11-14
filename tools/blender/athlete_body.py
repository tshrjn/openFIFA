"""Athlete body generator for OpenFootball (Blender 5.2 + MPFB 2.0.18, headless).

Builds a footballer body from the MakeHuman base mesh (CC0) with MPFB macros and targets, rigs it with MPFB's
Unreal-style "game_engine" skeleton (bone names KitBodyMapPostprocessor and Unity Humanoid auto-mapping already know),
poses it into the T-pose the Quaternius animation clips assume, turns the bare feet into boot lasts, scales it to the
requested height and exports one FBX with three skinned LODs (Unity builds the LOD Group from the _LODn suffixes).

Usage (from the repo root):
  blender -b --factory-startup --python tools/blender/athlete_body.py -- \
      [--out OpenFifa/Assets/Art/Characters/Athlete/Athlete.fbx] [--height-cm 183] [--build average] \
      [--glb path.glb] [--report path.json] [--blend path.blend]

Deterministic: the same arguments give the same mesh. See tools/blender/README.md.
"""
import argparse
import json
import math
import os
import sys

import addon_utils
import bmesh
import bpy
from mathutils import Matrix, Vector

MPFB = "bl_ext.user_default.mpfb"

# Per-build MPFB macros (0..1). MakeHuman: age 0.5 = 25 years, proportions 1 = "ideal", weight 0.5 = average.
BUILDS = {
    "slim":    {"muscle": 0.72, "weight": 0.40},
    "average": {"muscle": 0.86, "weight": 0.48},
    "stocky":  {"muscle": 0.95, "weight": 0.60},
}

# Footballer detail targets (name -> weight): strong thighs and calves, broad shoulders over a narrow male pelvis
# (flat glutes, straight waist), short solid neck. Council feedback 2026-11-14: v2 read slightly female from the back.
# The waist/hip line is pushed past bare anatomy on purpose: the kit is painted on this surface, so the torso has to
# carry the straight silhouette a real shirt would drape over the waist (until kits get garment meshes, queue #5).
# Kept moderate: the kit is painted on this surface, so the torso must read as a shirt, not as bare anatomy.
ATHLETE_TARGETS = {
    "l-upperleg-muscle-incr": 0.55, "r-upperleg-muscle-incr": 0.55,
    "l-lowerleg-muscle-incr": 0.50, "r-lowerleg-muscle-incr": 0.50,
    "torso-vshape-incr": 0.50,
    "measure-shoulder-dist-incr": 0.65,
    "torso-scale-horiz-incr": 0.25,
    "stomach-pregnant-decr": 0.30,
    "measure-waist-circ-incr": 0.60,
    "hip-scale-horiz-decr": 0.55,
    "measure-hips-circ-decr": 0.40,
    "buttocks-volume-decr": 1.00,
    "pelvis-tone-decr": 0.30,
    "measure-neck-circ-incr": 0.55,
    "measure-neck-height-decr": 0.40,
    "l-upperarm-shoulder-muscle-incr": 0.40, "r-upperarm-shoulder-muscle-incr": 0.40,
    "l-upperarm-muscle-incr": 0.25, "r-upperarm-muscle-incr": 0.25,
    "l-lowerarm-muscle-incr": 0.25, "r-lowerarm-muscle-incr": 0.25,
}

# Triangle budgets (LOD0 / LOD1 / LOD2) for 22 skinned players on iPad-class GPUs.
LOD_TRIS = (22000, 9000, 3500)

# Vertex groups of MakeHuman helper geometry we keep: eyeballs, so the eye openings are not see-through.
KEEP_HELPERS = ("helper-l-eye", "helper-r-eye")


def log(*a):
    print("[athlete]", *a, flush=True)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--out", default="OpenFifa/Assets/Art/Characters/Athlete/Athlete.fbx")
    p.add_argument("--height-cm", type=float, default=183.0)
    p.add_argument("--build", choices=sorted(BUILDS), default="average")
    p.add_argument("--name", default="Athlete")
    p.add_argument("--glb", default="")
    p.add_argument("--report", default="")
    p.add_argument("--blend", default="")
    p.add_argument("--no-lods", action="store_true")
    return p.parse_args(argv)


def mpfb():
    addon_utils.enable(MPFB, default_set=True)
    from bl_ext.user_default.mpfb.services.humanservice import HumanService
    from bl_ext.user_default.mpfb.services.targetservice import TargetService
    return HumanService, TargetService


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    addon_utils.enable(MPFB, default_set=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0


def build_human(args):
    HumanService, TargetService = mpfb()
    macro = TargetService.get_default_macro_info_dict()
    macro.update({"gender": 1.0, "age": 0.5, "proportions": 1.0, "height": 0.6})
    macro.update(BUILDS[args.build])
    basemesh = HumanService.create_human(scale=0.1, macro_detail_dict=macro)
    for name, w in ATHLETE_TARGETS.items():
        path = TargetService.target_full_path(name)
        if path is None:
            raise RuntimeError("MPFB target not found: " + name)
        TargetService.load_target(basemesh, path, weight=w, name=name)
    rig = HumanService.add_builtin_rig(basemesh, "game_engine")
    TargetService.bake_targets(basemesh)
    return basemesh, rig


def strip_helpers(basemesh):
    """Delete MakeHuman helper geometry (joint cubes, teeth, tongue, tights/skirt/hair helpers) but keep the eyes."""
    for m in list(basemesh.modifiers):
        if m.type == 'MASK':
            basemesh.modifiers.remove(m)
    keep = {basemesh.vertex_groups[g].index for g in ("body",) + KEEP_HELPERS}
    me = basemesh.data
    bm = bmesh.new()
    bm.from_mesh(me)
    deform = bm.verts.layers.deform.verify()
    doomed = [v for v in bm.verts if not any(g in keep and w > 0.5 for g, w in v[deform].items())]
    bmesh.ops.delete(bm, geom=doomed, context='VERTS')
    bm.to_mesh(me)
    bm.free()
    # Drop non-bone vertex groups (helper/joint/extra groups): FBX would export them as nothing but noise.
    for vg in list(basemesh.vertex_groups):
        if vg.name.startswith(("helper-", "joint-")) or vg.name in ("body", "HelperGeometry", "JointCubes", "Left", "Mid", "Right"):
            basemesh.vertex_groups.remove(vg)
    for vg in list(basemesh.vertex_groups):
        if vg.name not in {b.name for b in basemesh.parent.data.bones}:
            basemesh.vertex_groups.remove(vg)
    me.update()


def _rotate_bone_to(rig, pb, target_dir):
    """Rotate pose bone pb about its head so its (current, posed) direction becomes target_dir (armature space)."""
    bpy.context.view_layer.update()
    head = pb.head.copy()
    cur = (pb.tail - pb.head).normalized()
    q = cur.rotation_difference(target_dir.normalized())
    m = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head)
    pb.matrix = m @ pb.matrix
    bpy.context.view_layer.update()


def tpose(rig, basemesh):
    """Arms straight out along +-X (palms down), legs straight down: the rest pose of the Quaternius clips."""
    from bl_ext.user_default.mpfb.services.rigservice import RigService
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    pbs = rig.pose.bones
    for side in ("l", "r"):
        # Which way is "out" for this side in armature space: from the spine to the shoulder.
        out = Vector((math.copysign(1.0, pbs["upperarm_" + side].head.x), 0.0, 0.0))
        for bone in ("upperarm_", "lowerarm_", "hand_"):
            _rotate_bone_to(rig, pbs[bone + side], out)
        # Fingers (not thumbs) along the hand, so the Humanoid finger muscles start from a flat hand.
        for f in ("index", "middle", "ring", "pinky"):
            for i in (1, 2, 3):
                _rotate_bone_to(rig, pbs[f"{f}_0{i}_{side}"], out)
        for bone in ("thigh_", "calf_"):
            _rotate_bone_to(rig, pbs[bone + side], Vector((0.0, 0.0, -1.0)))
    bpy.ops.object.mode_set(mode='OBJECT')
    RigService.apply_pose_as_rest_pose(rig)


BOOT_INFLATE = 0.004       # m the boot shell sits outside the foot
BOOT_VOXEL = 0.006         # m voxel size of the boot remesh
BOOT_TRIS = 700            # triangles per boot shell (LOD0)


def _boot_shell(foot_pts, name):
    """Smooth closed boot last around foot_pts: convex hull -> voxel remesh -> smooth -> inflate -> decimate."""
    hb = bmesh.new()
    for co in foot_pts:
        hb.verts.new(co)
    bmesh.ops.convex_hull(hb, input=hb.verts[:])
    hme = bpy.data.meshes.new(name)
    hb.to_mesh(hme)
    hb.free()
    shell = bpy.data.objects.new(name, hme)
    bpy.context.collection.objects.link(shell)
    steps = []
    m = shell.modifiers.new("remesh", 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = BOOT_VOXEL; steps.append(m.name)
    m = shell.modifiers.new("smooth", 'SMOOTH'); m.factor = 0.8; m.iterations = 10; steps.append(m.name)
    m = shell.modifiers.new("inflate", 'DISPLACE'); m.strength = BOOT_INFLATE + 0.002; m.mid_level = 0.0; steps.append(m.name)
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = shell
    shell.select_set(True)
    for n in steps:
        bpy.ops.object.modifier_apply(modifier=n)
    tris = sum(len(p.vertices) - 2 for p in hme.polygons)
    if tris > BOOT_TRIS:
        m = shell.modifiers.new("decimate", 'DECIMATE'); m.ratio = BOOT_TRIS / tris
        bpy.ops.object.modifier_apply(modifier=m.name)
    # Flat sole on the floor.
    for v in hme.vertices:
        v.co.z = max(0.0, v.co.z)
    return shell


def bootify_feet(basemesh, rig):
    """Football boots instead of bare feet. The kit shader paints boots from the ankle down, so bare toes would show
    as painted toes (the toe topology cannot be smoothed shut: it folds). Each foot below the boot line becomes a
    separate smooth shell (hull of the foot, remeshed and inflated) skinned from the nearest foot weights; the body
    faces it hides are deleted. The shell joins the body mesh, so the kit bake classifies it as leg/boot like any
    other vertex."""
    from mathutils.kdtree import KDTree
    me = basemesh.data
    bones = rig.data.bones
    gi = {vg.name: vg.index for vg in basemesh.vertex_groups}
    shells = []
    for side in ("l", "r"):
        foot = bones["foot_" + side]
        cut = foot.head_local.z * 1.15  # boot top just above the ankle joint
        foot_groups = (gi["foot_" + side], gi["ball_" + side])
        idx = []
        for v in me.vertices:
            if v.co.z <= cut and any(g.group in foot_groups and g.weight > 0.3 for g in v.groups):
                idx.append(v.index)
        shell = _boot_shell([me.vertices[i].co.copy() for i in idx], "_boot_" + side)
        # Skin weights: inverse-distance blend of the 6 nearest foot vertices (all bones they use).
        kd = KDTree(len(idx))
        for i in idx:
            kd.insert(me.vertices[i].co, i)
        kd.balance()
        for vg in basemesh.vertex_groups:
            shell.vertex_groups.new(name=vg.name)
        for sv in shell.data.vertices:
            acc = {}
            near = kd.find_n(sv.co, 6)
            tot = 0.0
            for co, i, d in near:
                k = 1.0 / max(d, 1e-4)
                tot += k
                for g in me.vertices[i].groups:
                    acc[g.group] = acc.get(g.group, 0.0) + k * g.weight
            for g, w in acc.items():
                if w / tot > 0.01:
                    shell.vertex_groups[g].add([sv.index], w / tot, 'REPLACE')
        # Delete the body faces entirely inside the boot (all corners at/below the boot line on this foot).
        inside = set(idx)
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        doomed = [f for f in bm.faces if all(v.index in inside for v in f.verts)
                  and max(v.co.z for v in f.verts) < cut * 0.85]
        bmesh.ops.delete(bm, geom=doomed, context='FACES_ONLY')
        loose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
        bm.to_mesh(me)
        bm.free()
        me.update()
        log(f"boot {side}: shell {len(shell.data.vertices)} verts, {len(doomed)} foot faces removed, boot line {cut:.3f} m")
        shells.append(shell)
    # Box-project UVs on the shells (UV0 is for future skin/boot textures; the kit shader uses the baked UV2/UV3).
    for shell in shells:
        uv = shell.data.uv_layers.new(name="UVMap")
        for loop in shell.data.loops:
            co = shell.data.vertices[loop.vertex_index].co
            uv.data[loop.index].uv = (0.9 + co.y * 0.5, co.z * 2.0)
        for p in shell.data.polygons:
            p.use_smooth = True
    # Join the shells into the body (keeps the body's armature modifier and parent).
    bpy.ops.object.select_all(action='DESELECT')
    for shell in shells:
        shell.select_set(True)
    basemesh.select_set(True)
    bpy.context.view_layer.objects.active = basemesh
    bpy.ops.object.join()


def scale_to_height(rig, basemesh, height_m):
    bpy.context.view_layer.update()
    zs = [(basemesh.matrix_world @ v.co).z for v in basemesh.data.vertices]
    h = max(zs) - min(zs)
    s = height_m / h
    for o in (rig, basemesh):
        o.select_set(True)
    rig.scale = (s, s, s)
    rig.location.z -= min(zs) * s
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    basemesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    log(f"height {h:.3f} m -> {height_m:.3f} m (x{s:.4f})")


# Kit space (KitBodyMapPostprocessor / OpenFifaKitSurface.hlsl) is anchored at the Humanoid Neck joint, and the
# shader paints the eyes 0.226 m above it (MannequinHeadSpace / KitPaintHead). The Quaternius rig puts that joint at
# the sternal notch; MPFB's neck_01 sits 7 cm higher, mid-neck. Moving the joint so the modelled eyeballs land on the
# painted eyes also puts the crew collar back at the base of the neck and the neck pivot where the clips expect it.
KIT_EYE_HEIGHT = 0.226


def align_neck_joint(rig, basemesh):
    me = basemesh.data
    eye = [me.vertices[i].co for p in me.polygons if p.material_index == 1 for i in p.vertices]
    eye_z = sum(c.z for c in eye) / len(eye)
    eye_x = sum(abs(c.x) for c in eye) / len(eye)
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    neck = rig.data.edit_bones["neck_01"]
    old = neck.head.z
    neck.head.z = eye_z - KIT_EYE_HEIGHT
    bpy.ops.object.mode_set(mode='OBJECT')
    log(f"neck_01 {old:.3f} -> {eye_z - KIT_EYE_HEIGHT:.3f} m (eyes at {eye_z:.3f} m, x +-{eye_x:.3f})")
    return eye_z, eye_x


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def make_materials(basemesh):
    me = basemesh.data
    me.materials.clear()
    skin = bpy.data.materials.new("M_Body")
    skin.diffuse_color = (0.80, 0.62, 0.50, 1.0)
    eyes = bpy.data.materials.new("M_Eyes")
    eyes.diffuse_color = (0.92, 0.92, 0.90, 1.0)
    me.materials.append(skin)
    me.materials.append(eyes)
    # Eye vertices: the two small closed shells inside the head. Find them as the connected pieces that are not the body.
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    seen, pieces = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, piece = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            piece.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        pieces.append(piece)
    pieces.sort(key=len, reverse=True)
    for piece in pieces[1:]:
        # Eyeballs (in the head); the boot shells stay on the body material.
        if sum(f.calc_center_median().z for f in piece) / len(piece) > 1.0:
            for f in piece:
                f.material_index = 1
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    return len(pieces)


def make_lods(basemesh, rig, name, no_lods):
    from bl_ext.user_default.mpfb.services.rigservice import RigService
    lods = []
    budgets = LOD_TRIS[:1] if no_lods else LOD_TRIS
    full = tri_count(basemesh)
    for i, budget in enumerate(budgets):
        if i == 0:
            obj = basemesh
        else:
            obj = basemesh.copy()
            obj.data = basemesh.data.copy()
            bpy.context.collection.objects.link(obj)
        obj.name = obj.data.name = f"{name}_LOD{i}"
        ratio = min(1.0, budget / full)
        if ratio < 0.999:
            # Decimate before the armature modifier; vertex groups (skin weights) are interpolated by the collapse.
            for m in list(obj.modifiers):
                if m.type == 'ARMATURE':
                    obj.modifiers.remove(m)
            d = obj.modifiers.new("decimate", 'DECIMATE')
            d.decimate_type = 'COLLAPSE'
            d.ratio = ratio
            d.use_symmetry = True
            d.symmetry_axis = 'X'
            d.use_collapse_triangulate = False
            bpy.ops.object.select_all(action='DESELECT')
            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            bpy.ops.object.modifier_apply(modifier=d.name)
            RigService.ensure_armature_modifier(obj, rig)
        obj.parent = rig
        lods.append(obj)
        log(f"{obj.name}: {len(obj.data.vertices)} verts, {tri_count(obj)} tris")
    return lods


def normalize_weights(obj, max_influences=4):
    """Unity skins with 4 bones per vertex by default: keep the 4 strongest and renormalise, so import does not."""
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=max_influences)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=0.001)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)


def report(rig, lods, args, pieces):
    bones = rig.data.bones
    r = {
        "generator": "tools/blender/athlete_body.py",
        "blender": bpy.app.version_string,
        "mpfb": "2.0.18",
        "build": args.build,
        "height_cm": args.height_cm,
        "macros": dict(gender=1.0, age=0.5, proportions=1.0, height=0.6, **BUILDS[args.build]),
        "targets": ATHLETE_TARGETS,
        "lods": {o.name: {"verts": len(o.data.vertices), "tris": tri_count(o)} for o in lods},
        "mesh_pieces": pieces,
        "bones": len(bones),
        "joints_m": {n: [round(c, 4) for c in bones[n].head_local] for n in
                     ("pelvis", "spine_03", "neck_01", "head", "upperarm_l", "hand_l", "thigh_l", "calf_l", "foot_l", "ball_l")},
    }
    return r


def main():
    args = parse_args()
    reset_scene()
    basemesh, rig = build_human(args)
    rig.name = rig.data.name = "Armature"
    strip_helpers(basemesh)
    tpose(rig, basemesh)
    bootify_feet(basemesh, rig)
    scale_to_height(rig, basemesh, args.height_cm / 100.0)
    pieces = make_materials(basemesh)
    align_neck_joint(rig, basemesh)
    lods = make_lods(basemesh, rig, args.name, args.no_lods)
    for o in lods:
        normalize_weights(o)
    rep = report(rig, lods, args, pieces)
    log(json.dumps(rep["lods"]), "joints", json.dumps(rep["joints_m"]))

    out = os.path.abspath(args.out)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    for o in lods:
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        use_mesh_modifiers=False, mesh_smooth_type='OFF', use_tspace=True, use_triangles=False,
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
        use_armature_deform_only=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
    log("wrote", out)
    if args.glb:
        bpy.ops.export_scene.gltf(filepath=os.path.abspath(args.glb), use_selection=True, export_format='GLB',
                                  export_animations=False, export_skins=True, export_yup=True)
        log("wrote", args.glb)
    if args.report:
        with open(args.report, "w") as f:
            json.dump(rep, f, indent=2)
    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))


if __name__ == "__main__":
    main()
