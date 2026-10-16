# Aset / Assets

| File | Asal / Source | Pemakaian / Usage |
|---|---|---|
| `models/jukung.glb` | Rodin MCP, `Generate3DFromPrompt` | Player boat; normalized to four scene units at import |
| `models/tuna.glb` | Blender 5.2 through Blender MCP | Stylized tuna, two-bone rig, 33-frame swim loop |
| `sources/boat-prompt.json` | Original prompt | Reproducible authoring request |
| `sources/create_fish.py` | Original Blender script | Geometry, materials, rig, animation, GLB export |
| `textures/nusantara-sky.hdr` | Original analytic RGBE sky | Editable HDR environment source |
| `textures/nusantara-sky-scaled.png` | Fixed-exposure encoding of original HDR | Filterable runtime environment, inverse intensity restores radiance |
| `audio/*.wav` | Original synthesized music, ambience and effects | Water/rain/night/birds, theme, fishing/UI cues |
| `sources/generate_environment.py` | Original standard-library Python generator | Rebuilds environment and audio without external artwork/samples |

**ID:** Perahu dibuat melalui server Rodin yang didefinisikan dalam `.mcp.json`, lalu disalin dari output lokal ke folder model. Jangan bergantung pada file temporer Rodin untuk distribusi. Tuna dibuat di scene Blender terpisah; scene yang sedang dibuka pengguna dipulihkan setelah ekspor. Script dan GLB disertakan agar Blender tidak menjadi kebutuhan runtime.

**EN:** The boat was generated using the workspace's configured Rodin MCP. Its temporary output was copied into this repository. The tuna was created in an isolated Blender scene, with the user's original scene restored afterward. Source scripts and runtime GLBs are shipped; Blender is not needed to play.

## Regenerasi / Regeneration

### Design alignment assets

The runtime inventory now contains **87 GLBs**: fourteen earlier models, nine additional characters, 58 catch/boss models, three regional/exhibition props and three island-life models. Eleven characters use separate Rodin meshes. Ten Rodin creature originals supply 33 catch variants; the other 25 catch exports use original Blender geometry. `sources/catch-manifest.json` records each model's anatomy source and triangle count.

| Editable source | Output |
|---|---|
| `sources/rodin/`, `rodin-*.json` | Preserved Rodin originals and exact generation prompts |
| `sources/build-character-library.py`, `character-library.blend` | Nine additional character rigs; shared rig helpers in `build-visual-assets.py` |
| `sources/build-catch-library.py`, `catch-library.blend` | All 58 catch exports; elongated Rodin creatures have four-bone Swim clips |
| `sources/build-regional-props.py`, `regional-props.blend` | Raft house, government outpost and tournament trophy; buildings embed Rodin teak texture |
| `sources/generate_soundtrack.py` | Fourteen regional WAV loops plus boss and DJ themes; original synthesized audio |

Run Blender scripts through the Blender MCP or inside Blender with this repository as the working directory. Keep the source originals before regenerating exports. `generate-character-sources.py` and `generate-creature-sources.py` reuse existing originals; missing sources invoke paid Rodin generation. Audio regeneration uses `python assets/sources/generate_soundtrack.py`. Run both validation and audit tools after changing runtime files.

**ID:** Seluruh 58 model tangkapan/boss tersedia, tetapi 33 varian berbagi sepuluh anatomi Rodin dan 25 masih memakai bentuk prosedural. Sebelas karakter memiliki mesh berbeda. Musik regional, boss dan DJ dibuat secara sintetis tanpa sampel rekaman pihak lain. Sumber Blender, prompt dan model asli disimpan terpisah dari ekspor runtime.

### Living islands / Pulau yang hidup

| Editable source | Output |
|---|---|
| `sources/rodin-island-undergrowth.json`, `rodin-banana-plant.json`, `rodin-macaque.json` | Exact Rodin prompts; originals preserved as `sources/rodin/island-undergrowth.glb`, `banana-plant.glb`, `macaque.glb` |
| `sources/build-island-life.py`, `island-life.blend` | `models/wild-undergrowth.glb` (2,600 triangles), `banana-plant.glb` (3,000), `macaque.glb` (4,200, ten-bone rig with Walk/Idle clips) |
| `sources/generate_island_textures.py` | Original tileable `textures/jungle-floor.jpg` and `textures/wet-rock.jpg` (numpy + Pillow) |

Run `build-island-life.py` through the Blender MCP with `runpy` (or inside Blender 5.2 from the repository root); it builds each asset in its own scene, restores the active scene, and writes `artifacts/blender-island-life.log`. The macaque rig uses deterministic distance skinning measured from the Rodin original; the two plants stay unrigged because the renderer bends them in the wind through instanced transforms. Regenerating the textures needs no provider: `python assets/sources/generate_island_textures.py`.

**ID:** Tiga model Rodin (rumpun tanaman liar, pohon pisang, monyet ekor panjang) disimpan aslinya di `sources/rodin/`. Blender mengurangi poligon, menormalkan ukuran, dan memberi monyet rig sepuluh tulang dengan klip Walk/Idle. Tekstur lantai hutan dan batu kapur dibuat secara prosedural tanpa sumber pihak ketiga. Biawak liar memakai ulang model tangkapan `catch-biawak.glb`.

### Visual upgrade

`sources/polish-karst.py` prepares `models/karst-weathered.glb` from the existing Rodin karst through Blender MCP. The derivative has 1,799 triangles, softened edges, normalized bounds and no embedded textures; `sources/weathered-karst.blend` preserves the editable scene. Runtime limestone/moss shading lives in `NatureShaders.cs`, uses world coordinates to avoid stretched UVs, and retains PBR lighting. The original Rodin export remains intact.

**ID:** Batu kapur baru berasal dari aset Rodin yang sudah ada, dihaluskan dan diringankan melalui Blender MCP. Warna batu, lumut, dan pasir basah dihitung menurut posisi serta kemiringan permukaan sehingga sambungannya lebih halus. Sumber Blender dan aset asli tetap tersedia.

Rodin supplied `fisherman.glb`, `rina.glb`, `karst.glb`, `coral-sand.png`, `weathered-teak.png` and `harbor-keyart.png`. Exact prompts are in `sources/rodin-*.json`; original generated meshes are preserved under `sources/rodin/`. The key art appears in the travel atlas. The pier/pavilion exports embed the Rodin teak texture.

`sources/build-visual-assets.py` runs inside Blender 5.2 through MCP, creates isolated scenes, optimizes characters to approximately 7k triangles, authors sixteen-bone rigs and Idle/Walk/Fishing clips, and exports only the active scene. It also builds palms, the pier, pavilions, jetski, surfboard, articulated gull/dolphin, coral and reef fish. `sources/visual-library.blend` preserves twelve editable scenes. It does not replace the user's current Blender file. Run `python tools/validate-visual-assets.py` after rebuilding exports.

**ID:** Model dasar dan prompt Rodin tetap tersimpan. Rig, animasi dan geometri tambahan dibuat melalui Blender MCP; aset siap pakai disalin ke paket game. Lihat [penjelasan visual](../docs/visual-upgrade.md) untuk gambar dan batas cakupan.

From the repository root, with the Rodin server installed at the path configured in `.mcp.json`:

```sh
python tools/rodin-client.py Generate3DFromPrompt --arguments assets/sources/boat-prompt.json --output artifacts/rodin-boat-response.json
```

Generation may consume provider credits. Copy the resulting GLB into `assets/models/jukung.glb` after inspecting it. Response files stay in ignored `artifacts/` because they can contain temporary paths or download URLs.

Run `assets/sources/create_fish.py` inside Blender to rebuild the tuna. The script makes a new asset scene each run and exports only its selected objects.

## Kredit / Credits

Run `python assets/sources/generate_environment.py` to rebuild the original sky/audio. Direct float32 HDR loading is intentionally avoided because the tested ThreeNet adapter cannot filter its mipmaps; preserve the fixed exposure/intensity pairing. `python tools/audit-assets.py` updates the [asset hashes and model statistics](../docs/asset-audit.json), [dependency inventory](../docs/THIRD-PARTY-NOTICES.md), and available upstream notices in `docs/licenses/`.

Game: Ariana Mischa Fadhila, Syubadubin Studios. Rendering: [ThreeNet](https://github.com/DotNetVibeCoderz/Vibe_Graphics/tree/main/ThreeNet), Gravicode Studios, led by Kang Fadhil; MIT upstream. UI and supporting dependencies retain their respective licenses. Rodin-generated asset usage remains subject to the user's provider terms; this repository does not grant additional third-party asset rights.
