# Measured performance / Kinerja terukur

Measured locally on Windows x64, .NET 10.0.12, Intel UHD Graphics 620, DirectX 12 driver 31.0.101.2127, eight logical CPU threads. Each sample records at least fifteen steady seconds after five warmup frames, coastal scenery, 23 ambient actors and 49 terrain chunks. Values include GPU readback and Avalonia scheduling. Raw reports are retained here; they are single-machine measurements, not platform-wide guarantees.

The following table records the **earlier primitive-heavy art baseline**, before the visual upgrade.

| Configuration | Actual FPS | Frame p95 | Mean draw calls | First frame | Working set |
|---|---:|---:|---:|---:|---:|
| Original timer, unbatched | 15.46 | 70.54 ms | 395 | 41.73 s | 1.03 GB |
| Batched, coordinated pacing | 23.85 | 51.14 ms | 161 | 36.12 s | 1.03 GB |
| Low power: 60%, no shadows, MSAA 1 | 30.76 | 39.06 ms | 161 | 38.72 s | 776 MB |

Static instancing replaces 329 individual props with 18 batches. Resident scene nodes drop from 683 to 372. Start-to-start frame pacing avoids adding a full timer interval after every render. The low-power preset reduces readback to approximately 0.79 MB/frame and measured capture/bitmap time to 2.58 ms, versus 1.40 MB/frame and 4.49 ms originally. Managed allocation rates remain approximately 171-305 KB/s across these runs; this work does not claim zero allocation.

First-load shader/native initialization remains slow (36-42 seconds). This is an unresolved release concern. Average 30.8 FPS in one preset does not establish a guaranteed 30 FPS minimum. Broader CPU/GPU coverage, prolonged sessions, all-region performance and physical controller testing remain required.

## Reproduce / Reproduksi

```sh
dotnet run --project src/NusantaraFishing.Desktop -c Release -- --benchmark --no-batching --legacy-frame-loop
dotnet run --project src/NusantaraFishing.Desktop -c Release --no-build -- --benchmark
dotnet run --project src/NusantaraFishing.Desktop -c Release --no-build -- --benchmark --low-power
python tools/audit-assets.py
```

Reports go to ignored `artifacts/benchmarks/`. The new models/textures/audio/art total **35,157,535 bytes**; the original 16 MB review target is exceeded by the requested PBR assets and is explicitly superseded for this art pass. `docs/asset-audit.json` records hashes and model statistics. Terrain residency remains bounded to 49 chunks. The first unoptimized visual pass measured 16.32 FPS, 367k rendered triangles and 1.29 GB working set; `visual-before-lod.json` preserves that result. Small underwater rocks now share simplified geometry; final measurements are recorded separately.

The startup investigation retained DirectX 12. One Vulkan attempt initialized quickly, but three subsequent attempts crashed inside native renderer creation on this driver. The raw Vulkan timing file is diagnostic evidence, not a supported backend recommendation.

## Visual pass / Aset baru

The balanced profile after shared mesh simplification measured **19.74 FPS**, p95 **57.55 ms**, approximately **284k rendered triangles / 129 draw calls**, **1.29 GB working set**, and **50.09 seconds to the first frame**. Raw data: [visual-balanced.json](visual-balanced.json). This improves on the initial new-art pass (16.32 FPS), while remaining slower than the earlier simpler-art baseline. The art upgrade does not establish a 30 FPS guarantee on Intel UHD 620.

**ID:** Aset baru lebih berat daripada bentuk sederhana sebelumnya. Batu bawah air dan lambung perahu memakai geometri yang disederhanakan serta dibagi antar-instance. Profil seimbang mencapai sekitar 20 FPS pada perangkat ini; waktu inisialisasi GPU masih menjadi pekerjaan rilis.

The same final scene in **low-power mode measured 29.6 FPS**, p95 **38.3 ms**. See [visual-low-power.json](visual-low-power.json). Prefer this preset on the tested integrated GPU; it trades resolution and shadows for responsiveness. These are averages from a short coastal run, not guaranteed frame rates across fourteen regions.

The original analytic RGBE sky is retained for authoring. Runtime uses its fixed-exposure sRGB encoding with inverse environment intensity, because direct float32 HDR mipmapping panics on this adapter when ThreeNet's float filtering capability is unavailable. Do not restore direct HDR loading without a capability-safe engine path.

## Design alignment checkpoint - 2026-10-16

With 116 runtime assets (121,692,721 bytes), a short coastal low-power run measured **29.71 FPS**, p95 **40.20 ms**, **128.6 mean draw calls**, 1.43 GB working set and **39.03 seconds** to the first frame. Raw evidence: [design-low-power.json](design-low-power.json). The final aisle positioning adjustment was made after this capture; no performance claim depends on that small placement change. This run does not benchmark every character, boss, region or a long multiplayer session. The new art remains above the original asset budget.

**ID:** Pengukuran singkat mode hemat GPU dengan aset terbaru mencapai sekitar 29,7 FPS pada Intel UHD 620. Hasil ini bukan jaminan 30 FPS di semua wilayah atau perangkat. Waktu startup serta kebutuhan memori tetap perlu ditingkatkan.
