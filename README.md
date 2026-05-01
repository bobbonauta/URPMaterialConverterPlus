# URP Material Converter Plus

A fast, single-pass Unity Editor tool that converts Built-in / Legacy / Mobile / Unlit / Particles shader materials to **Universal Render Pipeline (URP)** equivalents — fixing the "pink/magenta material" problem in seconds.

Designed for projects with thousands of materials where Unity's built-in **Render Pipeline Converter** is too slow, too unstable, or stalls indefinitely.

---

## Why this tool

Unity's official Render Pipeline Converter is single-threaded, I/O-bound, and known to hang for hours on medium-to-large projects (the issue gets worse on Unity 6.4+). This tool focuses on a single job — **swapping shaders + remapping properties on `.mat` files** — and does it in seconds.

It does **not** touch animation clips, scenes, prefabs, or asset reimport graphs. If your only problem is "all my materials are pink in URP", this is what you want.

---

## Features

- **In-place edit** — no `_URP.mat` duplicates, no asset clutter
- **Wide shader coverage** — Standard, Standard (Specular), Legacy/* (10+ variants), Mobile/*, Unlit/*, Particles/*
- **Property mapping** — diffuse, color, normal/bump, metallic/smoothness, occlusion, emission, alpha cutout
- **Surface type detection** — automatic Opaque / Transparent / Alpha-Cutout based on source shader name
- **Dry Run** — preview every change in console before committing
- **Optional `.bak` backup** — every modified `.mat` gets a `.mat.bak` copy
- **Subfolder targeting** — convert just one asset pack instead of the whole project
- **Skip-already-URP** option to avoid touching converted materials twice
- **Detailed report** — counters + list of unmapped shader names for manual follow-up

---

## Installation

### Option 1 — Drag and drop

1. Download `Editor/URPMaterialConverterPlus.cs`
2. Drop it into **`Assets/Editor/`** in your Unity project
3. Wait for Unity to compile

### Option 2 — Git submodule (advanced)

```bash
cd YourUnityProject/Assets
git submodule add https://github.com/bobbonauta/URPMaterialConverterPlus.git URPMaterialConverterPlus
```

Then move `URPMaterialConverterPlus/Editor/URPMaterialConverterPlus.cs` into your `Assets/Editor/` folder, or just keep the submodule at that path (Unity recognizes any subfolder named `Editor`).

---

## Usage

1. Open Unity → menu **Tools → URP Material Converter Plus**
2. Configure:
   - **Target folder**: where to scan (default `Assets`). Use a subfolder for big projects: e.g. `Assets/ThirdParty/SomeAssetPack/Materials`
   - **Include subfolders**: ON to recurse
   - **Skip already-URP**: ON (recommended)
   - **Create `.mat.bak` backup**: ON for safety on first runs
   - **Dry Run**: ON the first time — preview only, no changes
3. Click **EXECUTE DRY RUN** → check the Console output
4. If the report looks correct, turn Dry Run **OFF** and click **EXECUTE CONVERSION**
5. Read the final report:
   - `Converted` — successfully remapped
   - `Already URP` — skipped (already on a URP shader)
   - `Errors` — printed in console with file path
   - `Unmapped shaders` — list of shader names not in the mapping table; handle manually or extend the map (see below)

---

## Shader mapping

Source shader → URP equivalent:

| Source | Target |
|---|---|
| `Standard` | `Universal Render Pipeline/Lit` |
| `Standard (Specular setup)` | `Universal Render Pipeline/Lit` |
| `Legacy Shaders/Diffuse` | `Universal Render Pipeline/Simple Lit` |
| `Legacy Shaders/Bumped Diffuse` | `Universal Render Pipeline/Simple Lit` |
| `Legacy Shaders/Specular` | `Universal Render Pipeline/Lit` |
| `Legacy Shaders/Bumped Specular` | `Universal Render Pipeline/Lit` |
| `Legacy Shaders/VertexLit` | `Universal Render Pipeline/Simple Lit` |
| `Legacy Shaders/Self-Illumin/*` | `Universal Render Pipeline/Simple Lit` |
| `Legacy Shaders/Transparent/*` | `Universal Render Pipeline/Simple Lit` |
| `Mobile/Diffuse`, `Mobile/Bumped Diffuse`, `Mobile/Bumped Specular`, `Mobile/VertexLit` | `Universal Render Pipeline/Simple Lit` |
| `Unlit/Texture`, `Unlit/Color`, `Unlit/Transparent`, `Unlit/Transparent Cutout` | `Universal Render Pipeline/Unlit` |
| `Particles/Standard Surface` | `Universal Render Pipeline/Particles/Lit` |
| `Particles/Standard Unlit` | `Universal Render Pipeline/Particles/Unlit` |

To add custom mappings, edit the `ShaderMap` dictionary in `URPMaterialConverterPlus.cs`.

---

## Property mapping

The tool preserves these material properties when swapping shaders:

| Property | Built-in | URP |
|---|---|---|
| Albedo texture | `_MainTex` | `_BaseMap` |
| Albedo color | `_Color` | `_BaseColor` |
| Tiling / offset | `_MainTex` UV | `_BaseMap` UV |
| Normal map | `_BumpMap` + `_BumpScale` | `_BumpMap` + `_BumpScale` |
| Metallic | `_MetallicGlossMap`, `_Metallic` | same |
| Smoothness | `_Glossiness` (or `_Smoothness`) | `_Smoothness` |
| Occlusion | `_OcclusionMap`, `_OcclusionStrength` | same |
| Emission | `_EmissionMap`, `_EmissionColor` | same (with `_EMISSION` keyword) |
| Alpha cutout | `_Cutoff` | `_Cutoff` + `_AlphaClip` + `_ALPHATEST_ON` keyword |
| Transparency | render queue / blend | `_Surface=1`, blend modes set, `_SURFACE_TYPE_TRANSPARENT` keyword |

---

## Limitations

- **Custom / proprietary shaders are not converted** (e.g. SpeedTree, vendor-specific Synty shaders, third-party water shaders). They appear in the "unmapped shaders" report — handle them manually
- Detail textures (`_DetailAlbedoMap`, etc.) are not transferred
- Smoothness texture from packed `_MetallicGlossMap` channels is not split — assumed identical layout (true in 99% of cases)
- Does **not** update Animation Clips that animate built-in shader properties — use Unity's official converter for that one specific case
- Does **not** rebuild lightmaps — re-bake your scenes after large conversions

---

## Comparison

| Feature | Unity Render Pipeline Converter | twiks228/URP-Material-Converter | This tool |
|---|---|---|---|
| Speed (1k materials) | 30 min – 3+ hours | ~1 min | seconds |
| Hangs / freezes | Common | Rare | No |
| Standard → URP | ✅ | ✅ | ✅ |
| Legacy shaders | ✅ | ❌ | ✅ |
| Mobile shaders | ✅ | ❌ | ✅ |
| Unlit + Particles | ✅ | ❌ | ✅ |
| Emission map preserved | ✅ | ❌ | ✅ |
| Alpha cutout / transparency | ✅ | partial | ✅ |
| In-place edit (no `_URP.mat` clutter) | ✅ | ❌ | ✅ |
| Dry Run preview | ❌ | ❌ | ✅ |
| Backup `.bak` files | ❌ | ❌ | ✅ |
| Subfolder targeting | ❌ | ❌ | ✅ |

---

## Troubleshooting

For a comprehensive reference of common URP errors (pink materials, shader stripping, GPU Resident Drawer warnings, lighting issues, decals, camera stacking, performance, build errors, custom shader migration, and more — 27 categories with solutions and sources), see **[URP_TROUBLESHOOTING.md](URP_TROUBLESHOOTING.md)**.

## Credits

This tool was inspired by [twiks228/URP-Material-Converter-for-Unity](https://github.com/twiks228/URP-Material-Converter-for-Unity), which solved the same problem with a different scope. URP Material Converter Plus was rewritten from scratch with extended shader coverage, in-place editing, dry-run preview, and proper transparency/emission handling.

---

## License

MIT License — see [LICENSE](LICENSE) for details.

---

## Contributing

Pull requests welcome. To add a new shader mapping, extend the `ShaderMap` dictionary at the top of `URPMaterialConverterPlus.cs` and submit a PR with a brief test note.

If you hit a shader the tool can't convert, run a dry run, copy the "unmapped shaders" list from the console, and open an issue.
