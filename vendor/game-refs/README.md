# vendor/game-refs (committed Refasmer reference assemblies)

Metadata-only **reference assemblies** for the game-derived DLLs that
`ValheimHeadTracking` compiles against:

- `assembly_valheim.dll` — Valheim's game assembly
- `UnityEngine*.dll` — the Unity engine modules the mod uses

They are generated with [Refasmer](https://github.com/JetBrains/Refasmer)
(`refasmer --all`) from a real Valheim install: **public API signatures only,
no IL bodies** (note the `ReferenceAssemblyAttribute` and the much smaller size
vs the real DLLs). The mod therefore compiles against shapes that match the
real assemblies at runtime — fields stay fields, method signatures are exact —
which a hand-written stub cannot guarantee.

They are committed so the build is **game-free**: `pixi run package` (and CI)
restore these into `src/ValheimHeadTracking/libs/` via `scripts/setup-libs.ps1`
without any Valheim install. They are **not** shipped to end users.

Refresh when Valheim updates and the public API changes:

```
pixi run update-game-refs        # uses VALHEIM_PATH or the default Steam path
```

then review and commit the diff. Same defensible reference-assembly approach
used elsewhere in the org (see `shadows-of-doubt/vendor/unity-ui-stub`).
