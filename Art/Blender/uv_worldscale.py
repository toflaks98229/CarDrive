# -*- coding: utf-8 -*-
"""
World-scale box UVs for the mechs, so a tiling brutalist texture reads as
poured material instead of as decals.

WHY NOT smart_project
  Blender's Smart UV Project unwraps and then packs each mesh into its own
  0..1 square. Two parts of very different size therefore get the SAME uv
  area, so the texel density differs by whatever their size ratio is. With a
  tiling concrete texture the aggregate would be fist-sized on the sensor cap
  and sand-sized on the hull. `density_report()` measures exactly that.

WHAT THIS DOES INSTEAD
  Every face is projected down its dominant world axis and divided by `tile`,
  so one texture repeat is always `tile` metres of real surface - on every
  face of every part of both machines. No packing, no islands to place, and
  the grain runs continuously across part boundaries because neighbouring
  parts share the same world-space mapping.

  The projection uses REST-POSE world coordinates. UVs are static vertex data,
  so once baked the grain is locked to the part and travels with it.

Run standalone:
    blender -b <file.blend> --python uv_worldscale.py -- <tile> [fbx_out]
"""
import bpy, math, sys
from mathutils import Vector

# One texture repeat per metre of surface. A 1024 map is then ~1 mm per texel,
# which is the right order for board-form marks and exposed aggregate.
DEFAULT_TILE = 1.0


def box_uv(obj, tile=DEFAULT_TILE, layer="UVMap"):
    """Project every face down its dominant world axis, scaled to metres."""
    me = obj.data
    uv = me.uv_layers.get(layer) or me.uv_layers.new(name=layer)
    mw = obj.matrix_world
    rot = mw.to_3x3()

    for poly in me.polygons:
        n = rot @ poly.normal
        if n.length < 1e-9:
            continue
        n.normalize()

        axis = max(range(3), key=lambda i: abs(n[i]))

        for li in poly.loop_indices:
            v = mw @ me.vertices[me.loops[li].vertex_index].co

            # Flip the second axis on back-facing sides so the texture is not
            # mirrored between the two faces of a slab.
            if axis == 0:
                u, w = v.y, v.z
                if n.x < 0.0:
                    u = -u
            elif axis == 1:
                u, w = v.x, v.z
                if n.y > 0.0:
                    u = -u
            else:
                u, w = v.x, v.y
                if n.z < 0.0:
                    w = -w

            uv.data[li].uv = (u / tile, w / tile)

    me.update()


def _tri_area(a, b, c):
    return (b - a).cross(c - a).length * 0.5


def density_report(objs, layer="UVMap"):
    """UV area per square metre of surface, per object.

    A tiling texture only reads as one material if this number is the same
    everywhere. Returns (name -> density) plus the spread.
    """
    out = {}
    for obj in objs:
        me = obj.data
        uv = me.uv_layers.get(layer)
        if uv is None:
            continue
        mw = obj.matrix_world

        world_area = 0.0
        uv_area = 0.0
        for poly in me.polygons:
            loops = list(poly.loop_indices)
            pts = [mw @ me.vertices[me.loops[li].vertex_index].co for li in loops]
            uvs = [Vector((uv.data[li].uv[0], uv.data[li].uv[1], 0.0)) for li in loops]
            for i in range(1, len(loops) - 1):
                world_area += _tri_area(pts[0], pts[i], pts[i + 1])
                uv_area += _tri_area(uvs[0], uvs[i], uvs[i + 1])

        if world_area > 1e-9:
            out[obj.name] = uv_area / world_area

    if out:
        lo, hi = min(out.values()), max(out.values())
        spread = hi / max(lo, 1e-9)
    else:
        lo = hi = spread = 0.0

    return out, dict(low=lo, high=hi, spread=spread)


def apply_all(tile=DEFAULT_TILE):
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    before = density_report(meshes)[1]
    for obj in meshes:
        box_uv(obj, tile)
    after = density_report(meshes)[1]
    return dict(objects=len(meshes), tile=tile, before=before, after=after)


def export_fbx(path):
    """Same settings the Unity importer was verified against - see the READMEs.
    bake_space_transform MUST stay on or the meshes arrive still Z-up."""
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, use_visible=False,
        object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
        bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
        apply_scale_options='FBX_SCALE_NONE', global_scale=1.0,
        axis_forward='-Z', axis_up='Y', path_mode='AUTO')


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    tile = float(argv[0]) if argv else DEFAULT_TILE

    print("UV", apply_all(tile))

    if len(argv) > 1:
        bpy.ops.wm.save_mainfile()
        export_fbx(argv[1])
        print("FBX", argv[1])
