# Roadmap / Rencana Pengembangan

Tujuan / Goal: implement the complete vision in `solution-design.md` through independently verifiable milestones. A checked item means implemented in this repository, not merely described.

## 1. Fondasi yang dapat dimainkan / Playable foundation

- [x] .NET 10 solution, Avalonia desktop UI, ReactiveUI, CommunityToolkit, ThreeNet native rendering.
- [x] Core rules separated from rendering, deterministic tests, local save validation.
- [x] Rodin jukung; Blender tuna geometry, rig, swim animation, and export source.
- [x] Sailing, orbit camera, simple obstacle collision, transparent water, ripples, caustics, underwater scenery.
- [x] Fishing tension/direction, regional catch pools, weather/day cycle, character stat bonuses.
- [x] Inventory, sales, rod/boat stat upgrades, discovery collection, six quests, region unlocks.
- [x] Single-player regional boss encounters and exhibition donations/income.
- [x] Bilingual guides, screenshots, architecture notes, contributor guide.

## 2. Dunia dan kedalaman gameplay / World and gameplay depth

- [x] Replace shared island generator with authored biome landmark layouts: coast, river, flooded Jakarta, volcanic lakes, caves, mines, and endgame dimensions. All 14 layouts passed a native GPU tour.
- [x] Terrain chunk streaming, per-biome spawn ecology, currents, depth, bait, and line/reef collision.
- [x] Regional boss visual archetypes, attack telegraphs and recovery windows, ink/direction changes, geysers, regional quests, and deterministic combat checks. Further player balance testing remains part of milestone 4.
- [x] Playable on-foot characters, active abilities, bank traversal/jumping, secret-character level unlocks.
- [x] Unique boat meshes, museum/aquarium buildings, NPC visitors, exhibit placement.
- [x] Depth-aware refraction, varying seabed depth, original analytic HDR environment source with filterable runtime encoding, synthesized audio/music, accessibility and ID/EN UI localization.

Acceptance / Penerimaan: each region must have its own playable environment, meaningful mechanics, and validated navigation; catalog entries alone do not count as completed worlds.

### Dunia yang hidup / Living world (additional user requirement)

- [x] Dynamic sky, moving cloud meshes, fog/rain/lightning, sunrise/noon/sunset/night, stars and harbor lamps.
- [x] Passing fishing boats, jetski riders, surfers, floodwater drifters on life rings, harbor residents/vendors/anglers, exhibition visitors, seabirds, dolphins, river crocodiles/orangutans and cave bats.
- [x] Regional activity selection and daytime schedules; storm wakes/rain disturb the same water simulation. These are ambient NPC activities, not multiplayer avatars.
- [x] Native smoke verification of original audio, encoded HDR lighting, English settings/preferences and virtual controller path.

## 3. Multiplayer dan layanan / Multiplayer and services

- [x] Authoritative four-player co-op server, host/join UI, movement and fishing replication; native scene verifies three remote avatars.
- [x] Shared boss health and synchronized telegraphs, contribution ownership, deadline, reconnect/restart reward safety, version compatibility, real HTTP testing with 100 ms injected response delay.
- [x] Server-owned inventory/economy, atomic persistence and exclusive host storage ownership; local saves never grant online wealth.
- [x] 32-slot festival sessions, UTC daily/weekly/quarterly tournaments, leaderboards, one-time ended-period prizes, request/identity limits. Full 32-rendered-client performance remains untested.

Acceptance passed locally: four independent HTTP clients complete an encounter with consistent rewards; disconnect/reconnect and server restart do not duplicate items. See `docs/validation/network.json` and `docs/images/multiplayer.png`.

## 4. Produksi dan distribusi / Production and distribution

### Visual upgrade requested / Peningkatan visual lanjutan

- [x] Rodin PBR fisherman, Rina and karst; generated sand/teak textures and harbor art integrated in the game.
- [x] Blender 16-bone human rigs, idle/walk/fishing clips, skin-weight inspection and approximately 7k-triangle character exports.
- [x] Detailed palms, pier, exhibition pavilions, jetski, surfboard, gulls, dolphins, coral and reef fish; editable Blender library and generation scripts.
- [x] Sloped textured shore, grouped rock formations, quieter distant water detail, procedural sky clouds and atlas illustration.
- [x] Irregular shared island ridges, blended limestone/moss/wet sand, lightweight weathered karst and flowing transparent waterfalls; direct native region review captured.
- [x] Native character switching, fishing, audio and visual inspection; export validation checks embedded textures, isolated scenes, budgets and normalized weights.
- [x] Repeated-region cache regression, balanced/low-power profiles, refreshed Windows package with 741 verified files, isolated install/uninstall and packaged four-player native smoke.

### Design alignment pass / Penyesuaian desain

- [x] Eleven distinct Rodin character meshes with Blender rigs; 58 catch/boss exports, including 33 variants from ten Rodin anatomy sources.
- [x] Five missing catch entries, discovery-gated quests, Dugong achievement and persistent tournament trophy displays.
- [x] Rina barter, Bambang research, Ojan hand catching/tree climbing, Tono restricted-pier access, Raja Ampat swimming/snorkeling/spear mode.
- [x] Octopus camouflage/ink, Phantom visibility, Ninja reflexes, crab counter window, rocket dash and additional creature attack cues.
- [x] Fourteen regional music loops, boss/DJ themes and crossfades; raft houses, government outposts and guards.
- [ ] Refine 25 procedural catch exports, remaining simple boats/scenery and shared regional architecture; polish cloth weights and individual tentacle animation.
- [ ] Give snorkeling/spearfishing dedicated approach/aim/throw interactions; finish boss-specific mechanics including Kraken party requirements and deeper NPC interactions.

Catalog coverage is not full design fidelity. See [design alignment](docs/design-alignment.md) for the implemented behavior and remaining gaps. Earlier visual-package checks above describe the previous preview; current package evidence belongs in `Progress.md`.

### Rasa air dan pulau hidup / Water feel and living islands

- [x] Hull-footprint boat collision in Core (shared with the server): piers, pavilions, buildings, island beaches, cliffs, crystals, vents, signs and waterfalls are solid; bridges, arches and submerged reefs stay passable. Boat heading is persisted and replicated.
- [x] Distance-based V wakes for the player, remote peers, ambient craft, crocodiles, dolphins, monitor lizards, hooked fish and bosses; frame-rate independent.
- [x] Buoyant pitch/roll/heave from swell and simulated ripples, scaled by wave setting, weather, basin and reduced motion; hulls lean into turns and trim at speed.
- [x] Island hills with jungle floor, limestone cliffs and beaches; wind-blown palms, banana plants, undergrowth, rainforest trees and seagrass via per-frame instance batches.
- [x] Rodin undergrowth, banana plant and macaque; Blender-rigged macaque Walk/Idle; monitor lizards that bask and swim; villagers on banks; island and cliff cascades with flowing ribbons, churning foam, ripples and mist.
- [ ] Wind-driven vertex bending of individual fronds (currently whole-plant bending), terrain-aware wildlife avoidance and interactions with the player.

### Release work / Pekerjaan rilis

- [x] Initial Intel UHD 620/DX12 profile: allocations, readbacks, first-load time, draw calls, frame percentiles and asset hashes/budgets; reports in `docs/performance/`.
- [x] Static instancing and coordinated frame scheduling; measured 23.85 FPS balanced and 30.76 FPS low-power on the local adapter.
- [ ] Extend measurements to representative CPUs/GPUs and all regions/long sessions; resolve slow first GPU initialization (46-53 seconds locally with the detailed art).
- [x] Windows self-contained desktop/server packaging, SHA-256 manifest, reversible user-level installer/uninstaller, save migration and isolated installation verification.
- [x] Windows/Linux/macOS CI definitions for build, unit and real HTTP acceptance tests. Remote CI execution is not yet evidenced.
- [ ] Cross-platform native runtime tests on Windows, Linux, macOS; clean-machine installation; trusted signing, macOS bundle/notarization and Linux desktop installer.
- [x] Native virtual controller input, remappable keys, reduced motion, scalable text, audio/graphics preferences and ID/EN interface.
- [ ] Broader independent playtesting, physical controllers, balance and usability validation.
- [x] Asset/dependency inventory with original sources and upstream notices, opt-in local crash reporting, documented release checklist.
- [ ] Owner confirmation of Rodin redistribution terms, repository-wide license selection and trusted code-signing credentials.

Acceptance: reproducible signed packages and documented performance measurements for supported hardware. Local unsigned packages and a measured single-adapter baseline exist; signing, cross-platform runtime evidence and broader playtesting remain release gates. See `docs/release.md`. Do not mark those gates complete without their external evidence.
