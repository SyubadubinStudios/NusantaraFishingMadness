# Visual polish / Pemolesan visual

Ariana Mischa Fadhila, Syubadubin Studios.

## Terrain and materials / Medan dan material

Island silhouettes now have asymmetric shoulders, saddles and erosion. Rendering and climbing continue to use the same `Landmark.IslandSurface` function in Core; the navigable waterline is preserved. Indexed terrain meshes replace the previous duplicated triangle/material bands. Limestone, moss and wet sand blend continuously by height and slope, with world-space detail that avoids stretched UVs and strong repeated texture patterns.

The original Rodin karst remains available. Blender MCP prepared a separate **1,799-triangle, 51,676-byte** derivative with softened edges and normalized bounds. The smaller outcrops fit inside the existing collision footprint; a recessed rock mass closes gaps between cliff outcrops. Editable source: `assets/sources/weathered-karst.blend`; regeneration script: `assets/sources/polish-karst.py`.

**ID:** Bentuk pulau memiliki punggungan dan cekungan tidak simetris. Geometri visual dan aturan memanjat tetap memakai fungsi ketinggian yang sama. Warna pasir basah, batu kapur, dan lumut menyatu menurut ketinggian serta kemiringan. Aset batu Rodin asli dipertahankan; versi Blender yang lebih ringan digunakan untuk tebing dan batu di tepi pulau.

## Moving water / Aliran air

Waterfalls use curved, subdivided sheets with flowing streaks, turbulence and fading edges in place of the opaque-looking rectangular curtain and moving boxes. Existing mist, plunge foam and water-surface ripples remain connected. Flow uses the same reduced-motion clock as the vegetation. Nature shader IDs are reserved before region-dependent geometry is built, avoiding cross-region source/ID collisions in the ThreeNet 0.12 cache.

**ID:** Air terjun memakai permukaan melengkung dengan guratan aliran, turbulensi, dan tepi transparan yang memudar. Busa, kabut percikan, serta riak di bawahnya tetap aktif. Mode reduced motion menghentikan gerak pola aliran. Shader dibuat dalam urutan tetap sebelum geometri wilayah agar cache antarwilayah tidak tertukar.

Native low-power captures from direct region loads on Intel UHD 620 (598 × 329 viewport):

![Polished Raja Ampat limestone islands](images/nature-ampat-polished.png)

![Polished Air Terjun water, foam and mist](images/nature-waterfall-polished.png)

## Verification / Pemeriksaan

Release build: zero warnings/errors. **68 tests pass** (61 Core, seven Desktop), including continuity and finite heights around island crowns/shorelines. **88 GLBs** pass asset validation; the new karst is checked for its triangle budget, isolated mesh and absence of embedded textures. Both custom shaders pass the native WGSL validator.

Direct Terjun and Raja Ampat loads rendered their island and waterfall views without asset or renderer errors. The low-power run measured **25.8 FPS**, p95 **64.6 ms**, and first frame at **62.3 seconds** on Intel UHD 620. The same-condition reference captured before this pass was 26.2 FPS / p95 49.0 ms; this pass therefore makes no performance-improvement claim and p95 was higher in the final run. See `docs/performance/visual-polish-low-power.json` and the reference in ignored `artifacts/visual-polish/before-clean.json`. These improvements retain a stylized presentation; character clothing, foliage silhouettes, wildlife interactions and remaining simple architecture still need further work. This pass does not close the broader fidelity or release gates in `PLAN.md`.
