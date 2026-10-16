using System.Numerics;
using System.Text.Json;
using Avalonia.Media.Imaging;
using NusantaraFishing.Core;

namespace NusantaraFishing.Desktop;

public sealed partial class MainWindow
{
    /// <summary>Opt-in native rendering checks. Runs on an isolated screenshot save.</summary>
    private async Task ReviewDesignContent(string folder)
    {
        var checks = new List<string>();
        vm.Game.AbortFishing("Content review");
        vm.Game.Player.Experience = 1350;
        vm.Game.Player.Coins = 100000;
        vm.Game.Player.RodLevel = 5;
        vm.Game.Player.TotalCaught = 12;
        vm.Game.Player.WorldHours = 12;
        foreach (var character in Catalog.Characters)
        {
            if (!vm.Execute("character", character.Id)) throw new InvalidOperationException("Character selection: " + character.Id);
            world!.ReviewCamera(new(vm.Game.Player.PositionX, 1.2f, vm.Game.Player.PositionZ), 6, 2.8f, .12f);
            await Task.Delay(350);
            Capture("character-" + character.Id);
            checks.Add("character:" + character.Id);
        }
        vm.Execute("character", "ujang");
        foreach (var species in Catalog.Species.Concat(Catalog.Regions.Select(Catalog.BossSpecies)))
        {
            var boss = species.Kind == CatchKind.Boss;
            vm.Game.ApplySnapshot(vm.Game.Snapshot() with
            {
                HookedId = species.Id, Phase = boss ? FishingPhase.Fighting : FishingPhase.Landed,
                Progress = .1, Tension = .2, BossCue = "", Message = species.Name
            });
            world!.ReviewCamera(boss ? new(0, 0, -6) : new(vm.Game.Player.PositionX + 1.8f, 1.4f, vm.Game.Player.PositionZ - 1), boss ? 20 : 5, .8f, .18f);
            await Task.Delay(120);
            viewport.RenderFrame();
            if (world.AssetWarning is not null) throw new InvalidOperationException(world.AssetWarning);
            if (species.Id is "batik" or "kepiting" or "keris" or "motor" or "paus-kubangan" or "boss-kubangan" or "raja") Capture("catch-" + species.Id);
            checks.Add("catch:" + species.Id);
        }
        vm.Game.AbortFishing("Content review");
        vm.Game.Player.Trophies.Add(new("daily:2026-10-15", 1, DateTimeOffset.UtcNow));
        vm.Game.Player.Exhibits.UnionWith(["keris", "motor", "batik"]);
        vm.Game.PlaceExhibit("keris", 100);
        vm.Game.PlaceExhibit("motor", 101);
        vm.Game.PlaceExhibit("batik", 0);
        world!.ReviewCamera(new(-18, 1.8f, -7), 17, .5f, .3f);
        await Task.Delay(300);
        Capture("museum");
        checks.Add("museum:species-and-trophy-models");
        if (!vm.Game.Travel("ampat")) throw new InvalidOperationException("Cannot reach snorkeling location.");
        LoadWorld();
        if (!vm.Execute("method", "Snorkel")) throw new InvalidOperationException("Snorkeling entry failed.");
        await Task.Delay(400);
        Capture("snorkel");
        checks.Add("snorkel:underwater-camera");
        if (!vm.Execute("method", "Rod")) throw new InvalidOperationException("Cannot return to boat.");
        foreach (var id in new[] { "dj", "boss-ampat" })
        {
            vm.Game.ApplySnapshot(vm.Game.Snapshot() with { HookedId = id, Phase = FishingPhase.Fighting, Progress = .1, Tension = .2 });
            await Task.Delay(300);
            var expected = id == "dj" ? "dj-theme" : "boss-theme";
            if (audio?.CurrentTrack != expected) throw new InvalidOperationException("Music transition failed: " + expected);
            checks.Add("audio:" + expected);
        }
        vm.Game.AbortFishing("Content review complete");
        await Task.Delay(300);
        if (audio?.CurrentTrack != "region-ampat") throw new InvalidOperationException("Regional music did not resume.");
        checks.Add("audio:region-ampat");
        vm.Game.Travel("jakarta"); LoadWorld();
        Traversal.SetPosition(vm.Game.Player, new(16, 32));
        if (vm.Game.ToggleBoarding()) throw new InvalidOperationException("Restricted pier admitted Ujang.");
        vm.Execute("character", "tono");
        if (!vm.Game.ToggleBoarding()) throw new InvalidOperationException("Restricted pier rejected Tono.");
        world!.ReviewCamera(new(16, 2, 28), 16, .7f, .3f);
        await Task.Delay(300);
        Capture("government-outpost");
        checks.Add("government-outpost:tono-access");
        vm.Game.Travel("kalimantan"); LoadWorld();
        world!.ReviewCamera(new(17, 2, -8), 17, -.9f, .3f);
        await Task.Delay(300);
        Capture("raft-house");
        checks.Add("kalimantan:raft-house");
        var report = Path.GetFullPath("docs/validation/design-runtime.json");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        File.WriteAllText(report, JsonSerializer.Serialize(new { Utc = DateTimeOffset.UtcNow, Checks = checks, AudioSamples = audio.MixedSamples, settings.RenderScale, settings.Shadows }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("CONTENT_REVIEW_OK: " + checks.Count + " native checks");

        void Capture(string name)
        {
            viewport.RenderFrame();
            if (rendererFailed || world?.AssetWarning is not null) throw new InvalidOperationException(world?.AssetWarning ?? "Rendering failed");
            using var frame = viewport.CaptureFrame();
            if (frame is null) throw new InvalidOperationException("No frame: " + name);
            frame.Save(Path.Combine(folder, "design-" + name + ".png"), PngBitmapEncoderOptions.Default);
        }
    }
}
