"""Granny 3D animations (.gr2), sampled through RAD's own runtime (granny2.dll, x64) for the viewport.

The client's animations are Granny files: each holds the skeleton it was made on and one animation, a transform
track per bone with orientation, position and scale/shear curves local to the parent bone, in the file's space
(Y up, metres), the same as the .dsk skeletons. The curves come in many compressed encodings and the files' sections
are Oodle-compressed, so they're read and sampled with the runtime rather than decoded here. Objects are walked from
the runtime's own type tables (Granny*Type exports), so no struct layout is hard-coded. After
FreeRealmsMayaMCP/fr_maya_mcp/formats/gr2.py.

The DLL is found at FR_GRANNY_DLL, else D:/FreeRealms/granny2.dll; without it, nothing animates.
"""

from __future__ import annotations

import base64
import ctypes as C
import os
import struct
import threading
from functools import lru_cache
from pathlib import Path

import numpy as np

DLL_PATH = Path(os.environ.get("FR_GRANNY_DLL", r"D:\FreeRealms\granny2.dll"))
FPS = 30                                  # samples per second of a baked clip
_DEF_SIZE = 44                            # a granny_data_type_definition
_SCALARS = {10: ("f", 4), 11: ("b", 1), 12: ("B", 1), 13: ("b", 1), 14: ("B", 1), 15: ("h", 2), 16: ("H", 2),
            17: ("h", 2), 18: ("H", 2), 19: ("i", 4), 20: ("I", 4)}
# The runtime isn't known to be thread-safe; the editor's server is threaded.
_lock = threading.Lock()


def available() -> bool:
    return DLL_PATH.is_file()


@lru_cache(maxsize=1)
def _runtime() -> C.CDLL:
    if not available():
        raise FileNotFoundError(f"granny2.dll (x64) isn't at {DLL_PATH}; set FR_GRANNY_DLL")
    dll = C.CDLL(str(DLL_PATH))
    dll.GrannyReadEntireFileFromMemory.restype = C.c_void_p
    dll.GrannyReadEntireFileFromMemory.argtypes = [C.c_int32, C.c_void_p]
    dll.GrannyGetFileInfo.restype = C.c_void_p
    dll.GrannyGetFileInfo.argtypes = [C.c_void_p]
    dll.GrannyFreeFile.restype = None
    dll.GrannyFreeFile.argtypes = [C.c_void_p]
    dll.GrannyGetMemberTypeSize.restype = C.c_int32
    dll.GrannyGetMemberTypeSize.argtypes = [C.c_void_p]
    dll.GrannyGetTotalObjectSize.restype = C.c_int32
    dll.GrannyGetTotalObjectSize.argtypes = [C.c_void_p]
    dll.GrannyCurveIsIdentity.restype = C.c_bool
    dll.GrannyCurveIsIdentity.argtypes = [C.c_void_p]
    dll.GrannyEvaluateCurveAtT.restype = None
    dll.GrannyEvaluateCurveAtT.argtypes = [C.c_int32, C.c_bool, C.c_bool, C.c_void_p, C.c_bool, C.c_float, C.c_float,
                                           C.POINTER(C.c_float)]
    return dll


def _u64(address: int) -> int:
    return C.c_uint64.from_address(address).value


def _i32(address: int) -> int:
    return C.c_int32.from_address(address).value


def _string(address: int) -> str | None:
    return C.string_at(address).decode("latin-1") if address else None


def _type(symbol: str) -> int:
    """The definition array behind a Granny*Type export (the export holds a pointer to it)."""
    return _u64(C.cast(_runtime()[symbol], C.c_void_p).value)


def _members(definition: int) -> list:
    members, offset, i = [], 0, 0
    while True:
        member = definition + i * _DEF_SIZE
        kind = _i32(member)
        if kind == 0:
            return members
        members.append({"kind": kind, "name": _string(_u64(member + 4)), "ref": _u64(member + 12),
                        "width": _i32(member + 20), "offset": offset})
        offset += _runtime().GrannyGetMemberTypeSize(C.c_void_p(member))
        i += 1


def _read(obj: int, definition: int, depth: int = 0):
    """A Granny object as plain Python; inline curves (members named *Curve) stay addresses, for sampling."""
    if not obj or depth > 8:
        return None
    out = {}
    for m in _members(definition):
        address, kind, width, name = obj + m["offset"], m["kind"], max(1, m["width"]), m["name"]
        if kind in _SCALARS:
            fmt, size = _SCALARS[kind]
            values = list(struct.unpack("<" + fmt * width, C.string_at(address, size * width)))
            out[name] = values[0] if m["width"] == 0 else values
        elif kind == 8:
            out[name] = _string(_u64(address))
        elif kind == 1:
            out[name] = address if name.endswith("Curve") else _read(address, m["ref"], depth + 1)
        elif kind == 2:
            out[name] = _read(_u64(address), m["ref"], depth + 1)
        elif kind == 3:                   # an int32 count and a pointer to an inline array
            count, pointer = _i32(address), _u64(address + 4)
            size = _runtime().GrannyGetTotalObjectSize(C.c_void_p(m["ref"]))
            out[name] = [_read(pointer + j * size, m["ref"], depth + 1) for j in range(count)]
        elif kind == 4:                   # an int32 count and a pointer to an array of pointers
            count, pointer = _i32(address), _u64(address + 4)
            out[name] = [_read(_u64(pointer + 8 * j), m["ref"], depth + 1) for j in range(count)]
        else:                             # variants, transforms and the rest: not needed here
            out[name] = None
    return out


def _sample(curve: int, dimension: int, duration: float, t: float) -> list:
    dll = _runtime()
    if dll.GrannyCurveIsIdentity(C.c_void_p(curve)):
        return {3: [0.0, 0.0, 0.0], 4: [0.0, 0.0, 0.0, 1.0], 9: [1.0, 0, 0, 0, 1.0, 0, 0, 0, 1.0]}[dimension]
    out = (C.c_float * dimension)()
    dll.GrannyEvaluateCurveAtT(dimension, dimension == 4, False, C.c_void_p(curve), False, duration, t, out)
    return list(out)


def _track(values: np.ndarray):
    """One channel of a baked track: a single value when it never changes, else every frame (base64 float32)."""
    if np.allclose(values, values[0], atol=1e-5):
        return [round(float(v), 6) for v in values[0]]
    return base64.b64encode(values.astype("<f4").tobytes()).decode("ascii")


def bake(blob: bytes, fps: int = FPS) -> dict:
    """A .gr2's animation sampled fps times a second: {duration, fps, frames, tracks: [{bone (upper case), rotation
    (x, y, z, w), position, scale}]}, each channel one value or frames x its size, local to the parent bone."""
    with _lock:
        dll = _runtime()
        buffer = C.create_string_buffer(blob, len(blob))
        handle = dll.GrannyReadEntireFileFromMemory(len(blob), buffer)
        if not handle:
            raise ValueError("Granny couldn't read this file")
        try:
            info = _read(dll.GrannyGetFileInfo(C.c_void_p(handle)), _type("GrannyFileInfoType"))
            if not info.get("Animations"):
                raise ValueError("no animation in this file")
            animation = info["Animations"][0]
            duration = float(animation["Duration"]) or 0.0
            frames = max(1, round(duration * fps)) + 1
            times = [duration * i / (frames - 1) for i in range(frames)] if frames > 1 else [0.0]
            tracks = []
            for group in animation["TrackGroups"] or []:
                for track in group["TransformTracks"] or []:
                    rotation = np.array([_sample(track["OrientationCurve"], 4, duration, t) for t in times])
                    position = np.array([_sample(track["PositionCurve"], 3, duration, t) for t in times])
                    # Scale/shear is a 3x3; its diagonal is the scale (shear is rare and left out).
                    shear = np.array([_sample(track["ScaleShearCurve"], 9, duration, t) for t in times])
                    tracks.append({"bone": (track["Name"] or "").upper(), "rotation": _track(rotation),
                                   "position": _track(position), "scale": _track(shear[:, [0, 4, 8]])})
            return {"duration": duration, "fps": fps, "frames": frames, "tracks": tracks}
        finally:
            dll.GrannyFreeFile(C.c_void_p(handle))
