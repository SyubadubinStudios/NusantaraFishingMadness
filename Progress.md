# Development status / Status pengembangan

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.  
Created by Ariana Mischa Fadhila of Syubadubin Studios.

## Implemented and verified locally

## Kraken party requirement / Syarat party Kraken - 2026-10-17

- **EN:** Kraken now requires two ready online players in the same room and Raja Ampat, aboard boats with the existing equipment/catch requirements. Each player must explicitly challenge the boss. Combat pauses while fewer than two participants remain connected, including before a partner joins; the three-minute deadline continues. Local solo attempts and unsuitable/off-region/disconnected partners are rejected. Other regional bosses retain solo play. The UI explains the requirement in ID/EN.
- **ID:** Kraken kini memerlukan minimal dua pemain siap di ruang online dan Raja Ampat yang sama. Keduanya harus berada di perahu dan memilih tantang boss. Pertarungan berhenti sementara saat peserta terhubung kurang dari dua, tetapi batas waktu tiga menit tetap berjalan. Pemain solo, partner belum siap, berada di wilayah lain, atau terputus tidak memenuhi syarat. Boss regional lain tetap bisa dimainkan solo.

Validation: Release build zero warnings/errors; **82 tests passed** (75 Core, seven Desktop), including two/four-player Kraken victories, participation/disconnect/rejoin, timeout without loot, and reconnect/restart reward safety. Four real HTTP clients passed **25 checks with 100 ms injected client delay** in 29.5 seconds. Evidence: [network Kraken](docs/validation/network-kraken.json); repeat with `dotnet run --project tools/NusantaraFishing.Acceptance -c Release -- --kraken --delay-ms 100`. Native UI text appearance has not been recaptured for this gameplay change. Dedicated underwater controls, additional bespoke boss mechanics and production gates remain open.

## Environmental rain / Hujan lingkungan - 2026-10-17

- **EN:** Fixed precipitation appearing only around the player. The emitter had 110 short drops in a small area attached to the moored boat, and node scale multiplied the instance positions, collapsing them near the origin. Rain now uses 2,700 properly scaled near/distant drops across the active view; rain/storm rates create broader surface disturbance, and underground scenes suppress outdoor rain.
- **ID:** Hujan tidak lagi menumpuk di sekitar pemain. Sebelumnya hanya 110 tetes pendek mengikuti perahu, dan skala node ikut mengecilkan posisi instance. Kini ada 2.700 tetes dekat/jauh yang tersebar pada area pandang; hujan dan badai menambah riak permukaan, sedangkan area bawah tanah tidak menampilkan hujan luar.

Validation: Release build zero warnings/errors, 68 tests passed, visual assets remain valid. Native Raja Ampat review logged `RAIN_RENDER_OK` with 2,700 enabled instances and captured matching clear/rain camera views. See [rain environment validation](docs/validation/weather-rain.md).

## Visual polish / Pemolesan visual - 2026-10-17

- **EN:** Island heights now use irregular shoulders and saddles shared by rendering and climbing. Indexed terrain blends limestone, foliage and wet sand by slope and elevation, with screen-space erosion detail. The existing Rodin karst now has an isolated 1,799-triangle weathered derivative from Blender MCP. Cliff outcrops use a recessed stone backing. Waterfalls use curved sheets with warped stream texture, translucent edges, foam, mist and pool ripples. Shader creation is stable across region builds. Direct Raja Ampat and Terjun native captures are in [visual polish](docs/visual-polish.md).
- **ID:** Punggung dan cekungan pulau kini lebih alami dan tetap sama dengan medan yang bisa dipanjat. Batu, lumut, serta pasir basah menyatu mengikuti lereng. Model batu Rodin diringankan melalui Blender MCP tanpa mengubah aset asli. Air terjun memakai aliran bertekstur lebih bervariasi dengan tepi transparan, busa, kabut, dan riak.

Validation: Release build zero warnings/errors; 68 tests passed (61 Core, seven Desktop); 88 GLBs pass structural validation. Both custom WGSL shaders compiled and rendered in native direct-load reviews of Raja Ampat and Terjun. Low-power Intel UHD 620 benchmark: **25.8 FPS**, p95 **64.6 ms**, 1.58 GB working set, **62.3-second** first rendered frame. The previous same-condition review was 26.2 FPS, p95 49.0 ms; this pass does not claim a speed gain. Raw report: [visual polish low power](docs/performance/visual-polish-low-power.json).

- .NET 10 native Avalonia/ThreeNet desktop, independent core and ASP.NET Core authoritative server.
- Fourteen authored biome layouts and bounded terrain streaming; all regions completed a GPU tour.
- Transparent depth-aware water, refraction/caustics, visible underwater life, wakes and rain ripples.
- Day cycle, clouds, fog, weather/lightning, night stars/lamps; region-specific boats, surfers, jetskis, drifters, working residents, exhibition visitors and wildlife.
- Eleven character profiles, abilities, shore walking/jumping, six boat visuals, regional boss combat, reef hazards, bait/depth ecology, twenty-four quests and walkable exhibitions.
- Original Rodin/Blender assets, analytic environment lighting, synthesized music/ambience/effects, ID/EN interface, persistent accessibility/graphics/audio/remapped keys, native controller input.
- Four-player co-op, server-owned economy, shared HP/telegraphs/deadline/rewards, reconnect/restart safety, 32-slot festivals and UTC tournaments.
- Atomic v2 local saves with v1 migration; separate server persistence and opt-in local crash reports.

## Water feel and living islands / Rasa air dan pulau hidup - 2026-10-16

Boats now collide by their full hull rather than a 0.7 m point: piers, harbor pavilions, raft houses, buildings, island beaches, cliffs, crystals, vents, signs and waterfalls are solid, while bridges, arches and submerged reefs remain passable. The rule lives in Core, so the authoritative server enforces it too; bow heading is saved and replicated. Every boat pitches, rolls and heaves with a weather- and basin-scaled swell plus the simulated ripples, and leaves a distance-based V wake. Jetskis, surfers, drifters, crocodiles, surfacing dolphins, monitor lizards, hooked fish and bosses ripple the surface. Steep ripples now whiten as foam.

Islands are forested limestone hills sharing one height field with Core climbing. Palms, banana plants, undergrowth, rainforest trees and seagrass bend in the wind through per-frame instance batches (about 170-410 swaying instances per region in the review). Rodin supplied three new originals (undergrowth, banana plant, macaque); Blender reduced them to 2,600-4,200 triangles and rigged the macaque with Walk/Idle clips. Macaques, monitor lizards and villagers populate islands and banks. The Air Terjun cliff and the tallest tropical island have cascades with moving ribbons, churning foam, pool ripples and mist.

Validation: Release build **zero warnings/errors**; **67 tests passed** (60 core including eight new hull-collision tests, seven desktop). All **87 GLBs** passed structural/rig validation, including the new macaque rig and plant budgets. Four-client HTTP acceptance passed **19 checks at 100 ms injected latency** with the extended `PeerView`. The native `--nature-review` run drove the boat into the pier (bow stopped at x = -4.95, outside the pier at -5.08) and captured coast, Raja Ampat, Air Terjun and Kalimantan scenes. The asset audit records **129 runtime assets, 128,754,051 bytes**. A numeric probe in that run found the wake only about three times the calm-water ripple height (0.033 vs 0.011 field units) because the bow lift and stern dents cancelled on the 1.33 m simulation grid; the drops now push the same way at roughly double strength. That final wake tuning has **not yet been re-captured natively**: a further GPU run was stopped by the host because system memory ran low. No new frame-rate measurement was taken for this pass either; the instance-batched vegetation adds roughly 170-410 instances and their per-frame matrix uploads, so `--benchmark --low-power` should be re-run before quoting performance. Captures: [islands](docs/images/nature-coast-island.png), [Raja Ampat](docs/images/nature-ampat-islands.png), [waterfall](docs/images/nature-waterfall.png), [bank](docs/images/nature-coast-bank.png), [pier collision](docs/images/nature-pier-collision.png), [macaque](docs/images/nature-wildlife-macaque.png).

**ID:** Perahu kini bertabrakan dengan seluruh lambungnya, bergoyang mengikuti alun dan meninggalkan riak V. Pulau menjadi bukit kapur berhutan dengan tumbuhan yang bergoyang tertiup angin, monyet, biawak, warga dan air terjun berkabut. Batasan: tumbuhan membengkok sebagai satu kesatuan (belum per pelepah), dan satwa mengikuti rute tetap tanpa menghindari pemain.

## Design alignment / Penyesuaian desain - 2026-10-16

Eleven characters now use distinct Rodin meshes with Blender rigs. All 44 catalog entries and fourteen regional bosses have runtime models. Ten generated creature anatomy sources support 33 variants; 25 catch exports remain procedural. Elongated Rodin creatures have four-bone Swim clips. Kalimantan raft houses, restricted government outposts and trophy displays are integrated. Visual inspection corrected bank overlap around the raft houses, moved aquarium specimens into their tanks, kept visitors in the front aisle and removed local head obstruction from the underwater camera.

Rina barter, Bambang research, Tono pier permission, Ojan hand catching/tree climbing, Raja Ampat snorkeling/spear mode, discovery-gated quests and additional creature behavior now have gameplay implementations. Tournament trophies persist with server-owned rewards. Protocol 2 replicates swimming/climbing/jump height. Fourteen regional music loops and boss/DJ themes crossfade during play.

Validation: Release build **zero warnings/errors**; **59 tests passed** (52 core, seven desktop). All **84 GLBs** passed structural/rig validation. The native fourteen-region tour and **76 content checks** passed, including all eleven characters, all 58 catch/boss models, music transitions, underwater entry, museum, government access and raft houses. The offline audio mixer produced samples during that review; its exact count is recorded in the report. Four-client HTTP acceptance passed **19 checks at 100 ms injected latency**. Evidence: [design runtime](docs/validation/design-runtime.json), [model validation](docs/validation/visual-assets.json), [network](docs/validation/network.json).

The updated inventory contains **116 runtime files, 121,692,721 bytes**, including 84 GLBs and 16 additional music loops. This exceeds the previous asset budget. A short coastal low-power run measured 29.71 FPS, p95 40.20 ms, 1.43 GB working set and a 39.03-second first frame on Intel UHD 620; see docs/performance/design-low-power.json. Wider hardware and longer sessions remain untested. The previous preview package below does not include this alignment pass.

**ID:** Sebelas karakter memiliki mesh berbeda dan seluruh entri tangkapan/boss sudah punya model. Fitur baru serta musik sudah terhubung ke game. Ini belum berarti semua detail desain selesai: 25 model masih prosedural, beberapa varian berbagi anatomi, animasi tentakel/pakaian serta interaksi tombak/NPC perlu dipoles. Rincian terbuka ada di [design-alignment.md](docs/design-alignment.md).

## Earlier evidence / Bukti tahap sebelumnya

Earlier foundation checkpoint: **45 xUnit tests passed** (40 core, five desktop preferences), including shared-clock expiry, reward isolation and invalid-settings preservation. Real HTTP acceptance: **19 checks passed**, four clients, **100 ms injected response latency**, shared boss victory, reconnect, restart and one reward per contributor. Raw result: [network report](docs/validation/network.json).

Native multiplayer smoke verified four peers/three remote avatars, routed movement, virtual gamepad and local/online isolation. Settings-English capture and offline audio mixer passed. Region tour and four time-of-day captures are in [screenshots](docs/images/).

Earlier simple-art Intel UHD 620 DX12 measurements: original 15.46 FPS / 395 draws; coordinated batching 23.85 FPS / 161 draws; low-power 30.76 FPS / p95 39.06 ms, first frame 36-42 seconds. New-art measurements appear below. Detailed limitations and raw data: [performance](docs/performance/README.md).

The earlier visual upgrade asset audit inventoried **30 runtime files, 35,157,535 bytes**, fourteen GLBs and 57 restored packages. Provider redistribution terms and game license remain owner decisions. The earlier preview package `artifacts/packages/win-x64-20261015-195458.zip` predates this art pass; it passed 715 checksum checks and isolated installation/uninstallation. That executable also passed native four-player hosting/joining, typing-field isolation and local/online restoration. Earlier Linux/macOS portable packages were cross-published but have not been run on those operating systems.

## Visual work / Peningkatan visual — 2026-10-16

Rodin generated two PBR human meshes, karst, sand/teak textures and harbor key art. Blender produced optimized sixteen-bone rigs with Idle/Walk/Fishing clips and twelve editable asset scenes. Timber pavilions/pier, coconut fronds, watercraft, birds, dolphins, coral and reef fish are integrated. The renderer uses the art directly; screenshot examples and exact source paths are in [visual-upgrade.md](docs/visual-upgrade.md).

Export checks passed for fourteen GLBs, including character weights and three animation clips. The expanded scene exposed a ThreeNet 0.12 cross-scene GPU cache collision during the native world tour; a compatibility adapter prunes stale resources and differentiates instance/shader versions. A second complete fourteen-region tour returned to the coast with correct meshes and textures, including the four time-of-day captures. After simplifying submerged rocks and boat hulls, final measurements reached **19.74 FPS balanced / 29.6 FPS low power**, with p95 **57.55 / 38.3 ms** on Intel UHD 620. Initial GPU startup remains approximately **46–50 seconds**; see `docs/performance/visual-*.json`.

The earlier visual-pass Windows package is `artifacts/packages/win-x64-20261016-041750.zip` (179,297,822 bytes). **741 hashes**, isolated installation/uninstallation and preservation of unknown files passed. The packaged executable passed four-player host/join, three rendered remote avatars, text-field isolation, virtual controller movement and local/online separation. See [package verification](docs/validation/package-visual.json). It is an unsigned preview, not a signed production release.

## Remaining release gates

The main systems in milestones 1-3 are implemented, with remaining design-fidelity work explicitly tracked in PLAN.md. Milestone 4 is not a completed public release. Linux/macOS graphics/audio/input runtime tests, broader CPUs/GPUs, physical controllers, longer independent playtests/balance, clean-machine installs and trusted signatures remain unverified. CI jobs exist but have not been executed on a remote repository in this workspace.

The ambient drifter has no rescue quest. Festival capacity is service-tested, not a 32-rendered-client hardware benchmark. No hosted matchmaking, account recovery or public moderation infrastructure is supplied. Slow initial GPU compilation remains a release concern.

Follow [PLAN.md](PLAN.md) and the [release checklist](docs/release.md); unchecked items require actual evidence and must not be represented as complete.
