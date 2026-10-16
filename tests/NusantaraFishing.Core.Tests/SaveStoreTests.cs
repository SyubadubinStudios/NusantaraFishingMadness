using NusantaraFishing.Core;

namespace NusantaraFishing.Core.Tests;

public sealed class SaveStoreTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "nusantara-tests-" + Guid.NewGuid());
    private SaveStore Store => new(Path.Combine(folder, "save.json"));

    [Fact]
    public void Save_RoundTripsProgressAndInventory_AndReplacesPreviousSave()
    {
        var state = new PlayerState { Coins = 987, RodLevel = 3, Experience = 400 };
        state.Inventory.Add(new("lele", 1.2, 65, DateTimeOffset.UtcNow, "selatan"));
        state.Discovered.Add("lele");
        Store.Save(state);
        var loaded = Store.Load();
        Assert.Equal(987, loaded.Coins);
        Assert.Single(loaded.Inventory);
        Assert.Contains("lele", loaded.Discovered);
        loaded.Coins = 123;
        Store.Save(loaded);
        Assert.Equal(123, Store.Load().Coins);
        Assert.False(File.Exists(Store.Path + ".tmp"));
    }

    [Fact]
    public void Load_MissingSave_ReturnsNewPlayer()
    { Assert.Equal(300, Store.Load().Coins); }

    [Fact]
    public void Load_VersionOneSave_MigratesWithoutLosingProgress()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Store.Path, "{\"Version\":1,\"Coins\":850,\"Experience\":300,\"Discovered\":[\"lele\"]}");
        var migrated = Store.Load();
        Assert.Equal(2, migrated.Version);
        Assert.Equal(850, migrated.Coins);
        Assert.Contains("lele", migrated.Discovered);
        Assert.Equal(5, migrated.PositionZ);
        Assert.Equal("worm", migrated.BaitId);
        Store.Save(migrated);
        Assert.Equal(2, Store.Load().Version);
    }

    [Theory]
    [InlineData("{\"Version\":99}")]
    [InlineData("{\"BoatIndex\":100}")]
    [InlineData("{\"Coins\":-5}")]
    [InlineData("{\"Discovered\":[\"unknown-fish\"]}")]
    [InlineData("{\"Inventory\":null}")]
    public void Load_InvalidSave_ThrowsWithoutOverwritingOriginal(string json)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Store.Path, json);
        Assert.Throws<InvalidDataException>(() => Store.Load());
        Assert.Equal(json, File.ReadAllText(Store.Path));
    }

    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
}
