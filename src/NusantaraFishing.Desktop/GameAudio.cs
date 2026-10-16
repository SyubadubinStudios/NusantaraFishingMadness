using NusantaraFishing.Core;
using ThreeNet;

namespace NusantaraFishing.Desktop;

/// <summary>Small owned audio graph. Offline mode exercises the same native mixer during smoke runs.</summary>
public sealed class GameAudio : IDisposable
{
    private readonly AudioEngine engine;
    private readonly Dictionary<string, AudioClip> clips = [];
    private readonly SoundInstance waves;
    private readonly SoundInstance rain;
    private readonly SoundInstance night;
    private readonly SoundInstance birds;
    private readonly Dictionary<string, SoundInstance> music = [];
    private readonly Dictionary<string, float> musicGains = [];
    public string CurrentTrack { get; private set; } = "";
    private FishingPhase lastPhase;
    private double lastAbility;
    private readonly bool offline;
    private readonly float[] offlineBuffer = new float[2048];
    public int MixedSamples { get; private set; }
    public float Volume { get; set; } = .35f;
    public float MusicVolume { get; set; } = .25f;
    public bool Muted { get; set; }

    public GameAudio(bool offline)
    {
        this.offline = offline;
        engine = offline ? AudioEngine.CreateOffline(22050) : AudioEngine.Open();
        try
        {
            foreach (var name in new[] { "water", "rain", "night", "birds", "island-theme", "cast", "bite", "catch", "escape", "ability", "ui" })
                clips[name] = engine.LoadClip(Path.Combine(AppContext.BaseDirectory, "assets/audio", name + ".wav"));
            waves = engine.Play(clips["water"], SoundOptions.Flat(.6f, true));
            rain = engine.Play(clips["rain"], SoundOptions.Flat(0, true));
            night = engine.Play(clips["night"], SoundOptions.Flat(0, true));
            birds = engine.Play(clips["birds"], SoundOptions.Flat(.35f, true));
            // Music clips are decoded on demand; only audible crossfade voices keep playing.
        }
        catch { engine.Dispose(); throw; }
    }
    public void Click() => engine.Play(clips["ui"], SoundOptions.Flat(.15f));
    public void Update(GameSession game, IslandWorld? world, float dt)
    {
        engine.MasterGain = Muted ? 0 : Volume;
        var isNight = game.Player.WorldHours is < 6 or >= 19;
        var wet = game.Weather is Weather.Rain or Weather.Storm or Weather.Lightning;
        waves.Update(x => x with { Gain = game.World.Underground ? .25f : .6f });
        rain.Update(x => x with { Gain = wet ? .6f : 0 });
        night.Update(x => x with { Gain = isNight && !game.World.Underground ? .5f : 0 });
        birds.Update(x => x with { Gain = !isNight && !game.World.Underground && !wet ? .25f : 0 });
        CurrentTrack = MusicSelection.For(game);
        if (!music.ContainsKey(CurrentTrack))
        {
            if (!clips.TryGetValue(CurrentTrack, out var clip))
                clips[CurrentTrack] = clip = engine.LoadClip(Path.Combine(AppContext.BaseDirectory, "assets/audio", CurrentTrack + ".wav"));
            music[CurrentTrack] = engine.Play(clip, SoundOptions.Flat(0, true));
            musicGains[CurrentTrack] = 0;
        }
        foreach (var track in music.Keys.ToArray())
        {
            var desired = track == CurrentTrack ? MusicVolume : 0;
            var gain = musicGains[track] + Math.Clamp(desired - musicGains[track], -dt * .2f, dt * .2f);
            musicGains[track] = gain;
            music[track].Update(x => x with { Gain = gain });
            if (gain <= 0 && track != CurrentTrack)
            { music[track].Stop(); music.Remove(track); musicGains.Remove(track); }
        }
        if (lastPhase != game.Phase)
        {
            var sound = game.Phase switch
            { FishingPhase.Waiting => "cast", FishingPhase.Fighting => "bite", FishingPhase.Landed => "catch", FishingPhase.Escaped => "escape", _ => null };
            if (sound is not null) engine.Play(clips[sound], SoundOptions.Flat(.55f));
        }
        if (game.AbilitySeconds > lastAbility + .5) engine.Play(clips["ability"], SoundOptions.Flat(.4f));
        lastAbility = game.AbilitySeconds;
        lastPhase = game.Phase;
        if (world is not null) engine.Update(world.Scene, world.Camera, dt);
        if (offline) MixedSamples += engine.Render(offlineBuffer);
    }
    public void Dispose() => engine.Dispose();
}
