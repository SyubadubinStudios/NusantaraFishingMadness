# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Nusantara Fishing Madness 3D: an Indonesian open-world fishing game on .NET 10 (SDK pinned in `global.json`), Avalonia 12, ReactiveUI, CommunityToolkit.Mvvm and ThreeNet 0.12 (NuGet `ThreeNet.Avalonia`; `external/Vibe_Graphics` is an ignored reference clone, not a build input). `AGENTS.md` predates the code and still says no source exists; its style, documentation and attribution rules still apply.

## Commands

The solution file is `NusantaraFishingMadness.slnx`. Package lock files are enforced (`RestorePackagesWithLockFile`), so after changing a `PackageReference` run a plain `dotnet restore` to update `packages.lock.json`; CI restores with `--locked-mode`.

```sh
dotnet restore NusantaraFishingMadness.slnx --locked-mode
dotnet build NusantaraFishingMadness.slnx -c Release --no-restore
dotnet test NusantaraFishingMadness.slnx -c Release --no-build
dotnet test tests/NusantaraFishing.Core.Tests --filter "FullyQualifiedName~GameSessionTests"   # single class/test
dotnet run --project src/NusantaraFishing.Desktop -c Release --no-build            # append -- --low-power on integrated GPUs
dotnet run --project src/NusantaraFishing.Server                                  # authoritative co-op server
dotnet run --project tools/NusantaraFishing.Acceptance -c Release --no-build -- --delay-ms 100   # real server + 4 HTTP clients
python tools/validate-visual-assets.py   # CI runs this; also run after changing runtime models
python tools/audit-assets.py             # refreshes docs/asset-audit.json and THIRD-PARTY-NOTICES
powershell -NoProfile -File tools/publish.ps1 [-Runtime win-x64|linux-x64|osx-arm64|osx-x64]
```

Tests use xUnit. Desktop validation modes (need an interactive GPU desktop, use isolated profiles): `--nature-review` (drives the boat into the pier and frames islands/wildlife/waterfalls into `artifacts/nature-review/`), `--world-tour`, `--native-network-test`, `--benchmark`, `--visual-review`, `--content-review`, `--presentation-review`, `--screenshot`, `--startup-profile`, plus diagnostics flags `--legacy-frame-loop` and `--no-batching`. Headless CI (`.github/workflows/build.yml`) covers build, unit and HTTP acceptance only, not GPU rendering, so report rendering changes as validated only after running a native mode.

GLB and `.blend` files are in Git LFS (`git lfs pull`). `artifacts/` is ignored scratch output for logs, packages, benchmarks and Rodin responses.

## Architecture

Three projects, with dependencies pointing only toward Core:

- **`NusantaraFishing.Core`**: has no UI or GPU dependencies. It owns all game rules: `Catalog` (species, regions, characters, boats), `Worlds`/`WorldDefinition` (14 authored biome landmark layouts and the shared depth function used for navigability, ecology and water optics), `GameSession` (fishing state machine, bosses, economy, quests and exhibitions, split into partials), `Traversal`, `PlayerState`, and `SaveStore` (v2 saves with v1 migration, range/catalog validation and atomic replace; invalid saves are never overwritten). `Networking/` holds the protocol, `CoopService` (20 Hz authoritative simulation under one lock, ordered commands, shared boss encounters, reward ledger), `CoopClient` and persistence.
- **`NusantaraFishing.Desktop`**: Avalonia app built in code, without XAML. `MainWindow.*.cs` partials handle UI, settings, network and the review modes. `IslandWorld.*.cs` partials build the ThreeNet scene (environment, characters, catches, exhibitions, ambient life, network peers, instance batching). `TerrainStreamer` keeps residency at 49 tiles. `WaterOptics`, `GameAudio` and `UserSettings` live alongside them. The desktop runs either a local `GameSession` or server snapshots via `CoopClient`, never both for the same progress. Disconnecting restores the prior local snapshot.
- **`NusantaraFishing.Server`**: ASP.NET Core host for `CoopService` (`SimulationWorker`). It accepts only inputs and actions, never imported local wealth. It holds an exclusive lock on its data directory.

Things that are not obvious from reading a single file:

- **Frame loop**: an 8 ms UI tick schedules both the simulation and the rendering deadlines, and the view's automatic rendering is disabled. Rendering reads GPU pixels back into Avalonia bitmaps, so FPS includes transfer costs. Measure before and after performance changes and record results under `docs/performance/`.
- **`SceneCacheCompatibility`** works around ThreeNet 0.12 caching GPU resources by scene-local IDs. It renders an empty scene before a region swap and bumps instance and water-shader versions. Don't remove it unless an upstream fix has been verified.
- **Boat collision and island shape live in Core**: `BoatHull`, `PlayerState.BoatHeading`, `WorldDefinition.HullFits` and `Landmark.BlocksSailing`/`IslandSurface` are shared by local play, the server and rendering. When you change how a landmark is drawn, update its waterline footprint in `BlocksSailing` too.
- **Water feel and wind** (`WaterMotion.cs`, `IslandWorld.Nature.cs`): wakes are emitted per distance travelled, not per frame. Hulls float on an analytic swell plus the ripple field. Vegetation registered with `Sway(...)` must be added before `BatchSwayingVegetation()` runs; it is pulled out of static batching into per-frame instance batches.
- **HDR sky**: it is shipped as a fixed-exposure PNG plus an inverse intensity because the ThreeNet adapter can't filter float32 HDR mipmaps. Keep the exposure and intensity values paired.
- **Localization**: Indonesian is the source language and the default (`Language = "id"`). UI strings are written in Indonesian in code and registered through `Localization.Register`/`T()`. `Locales.en.json` (an embedded resource) maps Indonesian text, including sentence fragments of 4+ characters for dynamic labels, to English. When you add or change a UI string, add its English entry.
- **Assets**: the Desktop csproj links `assets/{models,textures,audio,art}` into the output. Editable sources (Blender scripts, `.blend` files, Rodin prompts and originals) live in `assets/sources/`, separate from the runtime exports. Blender scripts run through the Blender MCP or inside Blender; source generators that are missing an original call Rodin, which costs provider credits. See `assets/README.md`.

## Project documents

`solution-design.md` is the game vision, and `NusantaraFishingMadness.txt` lists the delivery requirements. Keep `PLAN.md` (roadmap; an item is checked only when it is implemented, not just described) and `Progress.md` (validation record) up to date. User docs are bilingual (`docs/en`, `docs/id`, and paired ID/EN sections in README and docs). Keep the attribution "Ariana Mischa Fadhila, Syubadubin Studios" in docs and in the app. Further detail is in `docs/architecture.md`, `docs/networking.md` and `docs/release.md`.
