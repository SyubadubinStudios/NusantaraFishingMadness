using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Key = Avalonia.Input.Key;

namespace NusantaraFishing.Desktop;

public sealed partial class MainWindow
{
    private readonly DispatcherTimer settingsTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private bool settingsTimerRegistered;

    private void BuildSettingsPage()
    {
        Heading("Tampilan & kendali", "Nyaman bermain", "Pengaturan berlaku langsung dan disimpan lokal. Gamepad: stik kiri bergerak, A tarik, B naik perahu, X kemampuan, Y lompat.");
        sidebar.Children.Add(Text("Bahasa", 16, Brushes.White, true));
        var language = new ComboBox { ItemsSource = new[] { "Bahasa Indonesia", "English" }, SelectedIndex = settings.Language == "id" ? 0 : 1, HorizontalAlignment = HorizontalAlignment.Stretch };
        language.SelectionChanged += (_, _) =>
        {
            if (language.SelectedIndex < 0) return;
            settings.Language = language.SelectedIndex == 0 ? "id" : "en";
            ApplySettings(); ShowPage("Pengaturan");
        };
        sidebar.Children.Add(language);
        AddSlider("Ukuran teks", .85, 1.35, settings.TextScale, x => settings.TextScale = x);
        AddCheck("Kurangi gerakan lingkungan", settings.ReducedMotion, x => settings.ReducedMotion = x);
        AddCheck("Bisukan suara", settings.Muted, x => settings.Muted = x);
        AddSlider("Volume utama", 0, 1, settings.Volume, x => settings.Volume = (float)x);
        AddSlider("Volume musik", 0, 1, settings.MusicVolume, x => settings.MusicVolume = (float)x);
        AddSlider("Resolusi render", .5, 1, settings.RenderScale, x => settings.RenderScale = x);
        AddCheck("Bayangan dinamis",settings.Shadows,x=>settings.Shadows=x);
        sidebar.Children.Add(Button("Mode hemat GPU",()=>
        { settings.Shadows=false; settings.MsaaSamples=1; settings.RenderScale=.6; settings.FramesPerSecond=45; ApplySettings(); ShowPage("Pengaturan"); }));
        sidebar.Children.Add(Text("Batas FPS", 15, Brushes.White, true));
        var fpsOptions = new[] { 15, 30, 45, 60, 90, 120 };
        var fps = new ComboBox { ItemsSource = fpsOptions, SelectedItem = settings.FramesPerSecond, HorizontalAlignment = HorizontalAlignment.Stretch };
        fps.SelectionChanged += (_, _) => { if (fps.SelectedItem is int value) { settings.FramesPerSecond = value; ApplySettings(); } };
        sidebar.Children.Add(fps);
        sidebar.Children.Add(Text("Tombol keyboard", 18, Brushes.White, true));
        var keyOptions = new[] { Key.W, Key.A, Key.S, Key.D, Key.Space, Key.E, Key.F, Key.J, Key.Up, Key.Down, Key.Left, Key.Right, Key.Q, Key.R, Key.T, Key.G, Key.H, Key.K, Key.L, Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M, Key.Enter, Key.NumPad0, Key.NumPad1, Key.NumPad2 };
        var labels = new Dictionary<string, string>
        { ["forward"] = "Maju", ["back"] = "Mundur", ["left"] = "Kiri", ["right"] = "Kanan", ["cast"] = "Lempar / tarik", ["board"] = "Naik perahu", ["ability"] = "Kemampuan", ["jump"] = "Lompat" };
        foreach (var pair in labels)
        {
            sidebar.Children.Add(Text(pair.Value, 12, Muted));
            var combo = new ComboBox { ItemsSource = keyOptions, SelectedItem = settings.KeyFor(pair.Key), HorizontalAlignment = HorizontalAlignment.Stretch };
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is not Key key) return;
                if (settings.Keys.Any(x => x.Key != pair.Key && x.Value == key.ToString()))
                { saveStatus.Text = Localization.T("Tombol sudah digunakan aksi lain."); combo.SelectedItem = settings.KeyFor(pair.Key); return; }
                settings.Keys[pair.Key] = key.ToString(); keys.Clear(); ApplySettings();
            };
            sidebar.Children.Add(combo);
        }
        AddCheck("Laporan crash lokal (opsional)", settings.CrashReports, x => settings.CrashReports = x);
        sidebar.Children.Add(Text("Laporan disimpan di perangkat ini, tidak dikirim ke layanan mana pun.", 11, Muted));
        sidebar.Children.Add(Button("Simpan pengaturan", PersistSettings, true));
    }

    private void AddSlider(string title, double min, double max, double value, Action<double> apply)
    {
        sidebar.Children.Add(Text(title, 15, Brushes.White, true));
        var slider = new Slider { Minimum = min, Maximum = max, Value = value };
        slider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { apply(slider.Value); ApplySettings(); } };
        sidebar.Children.Add(slider);
    }
    private void AddCheck(string title, bool value, Action<bool> apply)
    {
        var check = new CheckBox { Content = Localization.T(title), IsChecked = value };
        check.IsCheckedChanged += (_, _) => { apply(check.IsChecked == true); ApplySettings(); };
        sidebar.Children.Add(check);
    }
    private void ApplySettings()
    {
        Localization.Configure(settings.Language, settings.TextScale);
        FontSize = 14 * settings.TextScale;
        viewport.RenderScale = settings.RenderScale;
        viewport.MaxFramesPerSecond = settings.FramesPerSecond;
        if(viewport.RendererOptions.Shadows!=settings.Shadows || viewport.RendererOptions.MsaaSamples!=settings.MsaaSamples)
            viewport.RendererOptions=viewport.RendererOptions with { Shadows=settings.Shadows,MsaaSamples=settings.MsaaSamples };
        if (world is not null) world.ReducedMotion = settings.ReducedMotion;
        CrashReporter.Enabled = settings.CrashReports;
        vm.Refresh();
        if (!settingsTimerRegistered)
        {
            settingsTimerRegistered = true;
            settingsTimer.Tick += (_, _) => { settingsTimer.Stop(); PersistSettings(); };
        }
        settingsTimer.Stop(); settingsTimer.Start();
    }
    private void PersistSettings()
    {
        if (snapshotMode || settingsReadOnly) return;
        settings.WaterAbsorption = waterClarity; settings.WaveStrength = waveStrength;
        try { settings.Save(settingsPath); saveStatus.Text = Localization.T("Pengaturan tersimpan."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { saveStatus.Text = "Settings: " + ex.Message; }
    }
}
