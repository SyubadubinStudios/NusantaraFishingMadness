using System.Text.Json;
using NusantaraFishing.Core;

namespace NusantaraFishing.Desktop.Tests;

public sealed class ContentAssetTests
{
    [Fact]
    public void EveryCatchAndRegionalBoss_HasEmbeddedGlbGeometry()
    {
        foreach (var id in Catalog.Species.Select(x => x.Id).Concat(Catalog.Regions.Select(x => "boss-" + x.Id)))
        {
            var path = Path.Combine(AppContext.BaseDirectory, "assets", "models", "catch-" + id + ".glb");
            Assert.True(File.Exists(path), path);
            using var reader = new BinaryReader(File.OpenRead(path));
            Assert.Equal(0x46546C67u, reader.ReadUInt32());
            Assert.Equal(2u, reader.ReadUInt32());
            Assert.Equal(reader.BaseStream.Length, reader.ReadUInt32());
            var count = reader.ReadInt32();
            Assert.Equal(0x4E4F534Au, reader.ReadUInt32());
            using var json = JsonDocument.Parse(reader.ReadBytes(count));
            Assert.NotEmpty(json.RootElement.GetProperty("meshes").EnumerateArray());
            Assert.Single(json.RootElement.GetProperty("scenes").EnumerateArray());
        }
    }

    [Fact]
    public void EverySelectedMusicTrack_HasNonSilentPcmLoop()
    {
        foreach (var name in MusicSelection.Tracks)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "assets", "audio", name + ".wav");
            using var reader = new BinaryReader(File.OpenRead(path));
            Assert.Equal("RIFF", new string(reader.ReadChars(4)));
            reader.BaseStream.Position = 22;
            Assert.Equal(1, reader.ReadInt16());
            Assert.Equal(22050, reader.ReadInt32());
            reader.BaseStream.Position = 44;
            var samples = new List<short>();
            while (reader.BaseStream.Position < reader.BaseStream.Length) samples.Add(reader.ReadInt16());
            Assert.True(samples.Count > 22050 * 10, name);
            Assert.True(samples.Max(x => Math.Abs((int)x)) > 1000, name);
            Assert.InRange(Math.Abs(samples[0] - samples[^1]), 0, 1200);
        }
    }
}
