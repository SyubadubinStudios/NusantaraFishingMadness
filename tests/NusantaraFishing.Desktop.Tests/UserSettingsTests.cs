using NusantaraFishing.Desktop;

namespace NusantaraFishing.Desktop.Tests;

public sealed class UserSettingsTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string folder=Path.Combine(Path.GetTempPath(),"nfm-preferences-"+Guid.NewGuid().ToString("N"));
        public string PathName { get; }
        public Fixture() { Directory.CreateDirectory(folder); PathName=Path.Combine(folder,"settings.json"); }
        public void Dispose()
        {
            if(File.Exists(PathName)) File.Delete(PathName);
            if(File.Exists(PathName+".tmp")) File.Delete(PathName+".tmp");
            Directory.Delete(folder);
        }
    }

    [Fact]
    public void OlderSettings_KeepDefaultsForNewOptions()
    {
        using var file=new Fixture();
        File.WriteAllText(file.PathName,"{\"Language\":\"en\",\"RenderScale\":0.6}");
        var settings=UserSettings.Load(file.PathName);
        Assert.True(settings.Shadows);
        Assert.Equal("en",settings.Language);
        Assert.Equal(.6,settings.RenderScale);
    }

    [Fact]
    public void SaveReload_PreservesAccessibilityWaterAndInput()
    {
        using var file=new Fixture();
        var settings=new UserSettings { Language="en",TextScale=1.25,ReducedMotion=true,Muted=true,WaterAbsorption=.1f,WaveStrength=.08f };
        settings.Keys["cast"]="Enter";
        settings.Save(file.PathName);
        var loaded=UserSettings.Load(file.PathName);
        Assert.Equal("Enter",loaded.Keys["cast"]);
        Assert.True(loaded.ReducedMotion); Assert.True(loaded.Muted);
        Assert.Equal(1.25,loaded.TextScale);
        Assert.Equal(.1f,loaded.WaterAbsorption); Assert.Equal(.08f,loaded.WaveStrength);
        Assert.False(File.Exists(file.PathName+".tmp"));
    }

    [Theory]
    [InlineData("{\"Language\":\"invalid\"}")]
    [InlineData("{\"RenderScale\":0}")]
    [InlineData("{\"Keys\":{\"forward\":\"W\",\"left\":\"W\"}}")]
    public void InvalidPreferences_AreRejectedWithoutChangingOriginalFile(string json)
    {
        using var file=new Fixture();
        File.WriteAllText(file.PathName,json);
        Assert.Throws<InvalidDataException>(()=>UserSettings.Load(file.PathName));
        Assert.Equal(json,File.ReadAllText(file.PathName));
    }

}
