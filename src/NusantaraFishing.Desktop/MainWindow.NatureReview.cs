using System.Numerics;
using Avalonia.Media.Imaging;
using NusantaraFishing.Core;

namespace NusantaraFishing.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// --nature-review: drives the boat through open water and into the pier, then frames islands,
    /// wildlife, banks and cascades. Captures go to artifacts/nature-review, never to docs.
    /// </summary>
    private async Task ReviewNature()
    {
        var folder = Path.GetFullPath("artifacts/nature-review");
        Directory.CreateDirectory(folder);
        vm.Game.Player.Experience = 1350;
        vm.Game.Player.Coins = 100000;
        async Task Shot(string name, Vector3 target, float distance, float yaw, float pitch, int delay = 900)
        {
            world!.ReviewCamera(target, distance, yaw, pitch);
            await Task.Delay(delay);
            using var frame = viewport.CaptureFrame();
            frame?.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
        }
        async Task Drive(Vector2 input, float seconds)
        {
            for (var t = 0f; t < seconds; t += 1 / 60f)
            {
                vm.Game.Move(1 / 60f, input);
                await Task.Delay(16);
            }
        }
        async Task Visit(string region)
        {
            if (!vm.Game.Travel(region)) throw new InvalidOperationException("Nature review travel failed: " + region);
            LoadWorld(); ShowPage("Ekspedisi");
            vm.Game.Player.WorldHours = 10;
            await Task.Delay(900);
            Console.WriteLine($"NATURE_WORLD: {region}, {world!.SwayingInstances} swaying instances, {world.WildlifeCount} wildlife, {world.Scene.NodeCount} nodes");
        }

        // Direct entry distinguishes a scene-build problem from a cross-region GPU cache problem.
        if (Program.Arguments.Any(x => x.StartsWith("--nature-region=", StringComparison.Ordinal)))
        {
            if (vm.Game.Region.Id == "terjun")
            {
                await Shot("waterfall-direct", new(0, 9, -26), 34, 0, .12f);
                await Shot("waterfall-plunge-direct", new(0, 2, -26), 14, .4f, .2f);
            }
            else
            {
                var island = vm.Game.World.Landmarks.FirstOrDefault(x => x.Kind == LandmarkKind.Island);
                await Shot("island-direct", island is null ? new(0, 2, 0) : new(island.X, island.Height * .5f, island.Z), 32, .3f, .25f);
                if (Program.Arguments.Contains("--rain-review"))
                {
                    var target = new Vector3(12, 8, -26);
                    await Shot("rain-weather-clear-reference", target, 45, .2f, .18f);
                    vm.Game.SynchronizeEnvironment(10, Weather.Rain);
                    await Shot("rain-island-direct", target, 45, .2f, .18f);
                    world!.ReviewCamera(null);
                    await Task.Delay(900);
                    using var playerView = viewport.CaptureFrame();
                    playerView?.Save(Path.Combine(folder, "rain-player-view.png"), PngBitmapEncoderOptions.Default);
                }
            }
            Console.WriteLine("NATURE_DIRECT_OK: " + vm.Game.Region.Id);
            return;
        }

        await Visit("selatan");
        if (Program.Arguments.Contains("--rain-review"))
        {
            vm.Game.SynchronizeEnvironment(10, Weather.Rain);
            await Shot("rain-coast-surroundings", new(-8, 3, -10), 42, .2f, .24f);
        }
        // Open-water wake and buoyancy.
        await Drive(new(1, 0), 1.2f);
        var boat = Traversal.Position(vm.Game.Player);
        world!.ReviewCamera(new(boat.X, .5f, boat.Y), 11, 2.2f, .32f);
        await Drive(new(1, -.3f), 1.4f);
        await Shot("boat-wake", new(vm.Game.Player.PositionX, .5f, vm.Game.Player.PositionZ), 11, 2.2f, .32f, 100);
        var stern = Traversal.Position(vm.Game.Player) - Traversal.Forward(vm.Game.Player.BoatHeading) * 4;
        Console.WriteLine($"WAKE_PROBE: behind boat {world!.RippleNear(stern, 3):N3}, calm water {world.RippleNear(new(40, 40), 3):N3}");
        // Ram the fishing pier: the hull must stop outside it.
        vm.Game.Player.PositionX = vm.Game.Player.BoatX = 0;
        vm.Game.Player.PositionZ = vm.Game.Player.BoatZ = 0;
        await Drive(new(-1, 0), 4);
        var hull = BoatHull.For(vm.Game.Player.BoatIndex);
        var bow = Traversal.Position(vm.Game.Player) + Traversal.Forward(vm.Game.Player.BoatHeading) * hull.HalfLength;
        Console.WriteLine($"PIER_COLLISION: boat {Traversal.Position(vm.Game.Player)}, bow {bow}, clear={vm.Game.World.CanSail(bow, 0)}");
        if (!vm.Game.World.CanSail(bow, 0)) throw new InvalidOperationException("Boat bow entered the pier.");
        await Shot("pier-collision", new(-5, 1, 0), 12, 1.2f, .45f);
        await Shot("coast-bank", new(-30, 3, -18), 22, .3f, .25f);
        await Shot("coast-island", new(40, 6, -25), 30, .5f, .2f);

        await Visit("ampat");
        if (Program.Arguments.Contains("--rain-review"))
            await Shot("rain-ampat-surroundings", new(12, 8, -26), 45, .2f, .18f);
        await Shot("ampat-islands", new(12, 8, -26), 45, .2f, .18f);
        await Shot("ampat-crown", new(12, 17, -28), 13, .7f, .45f);
        foreach (var (kind, position) in world!.WildlifePositions().DistinctBy(x => x.Kind))
            await Shot("wildlife-" + kind, position + new Vector3(0, .3f, 0), kind == "villager" ? 5 : 3.2f, .9f, .25f);
        await Shot("ampat-cascade", world.CascadeTargets().FirstOrDefault(new Vector3(0, 5, -20)), 22, .2f, .15f);

        await Visit("terjun");
        await Shot("waterfall", new(0, 9, -26), 34, 0, .12f);
        await Shot("waterfall-plunge", new(0, 2, -26), 14, .4f, .2f);

        await Visit("kalimantan");
        await Shot("river-bank", new(-24, 4, 0), 34, -.9f, .3f);
        vm.Game.Player.Experience = 1350;
        vm.Game.Travel("selatan"); LoadWorld();
        world!.ReviewCamera(null);
        Console.WriteLine("NATURE_REVIEW_OK • " + folder);
    }
}
