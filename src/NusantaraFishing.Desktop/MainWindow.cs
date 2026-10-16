using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NusantaraFishing.Core;
using ThreeNet;
using ThreeNet.Avalonia;
using Key = Avalonia.Input.Key;
using Vector = Avalonia.Vector;

namespace NusantaraFishing.Desktop;

public sealed partial class MainWindow : Window
{
    private static readonly IBrush Ink = Brush.Parse("#102E3B");
    private static readonly IBrush Panel = Brush.Parse("#193D49");
    private static readonly IBrush Muted = Brush.Parse("#9EB8BE");
    private static readonly IBrush Accent = Brush.Parse("#F1BB70");
    private readonly GameViewModel vm;
    private readonly SaveStore store;
    private readonly UserSettings settings;
    private readonly string settingsPath;
    private bool settingsReadOnly;
    private GameAudio? audio;
    private Gamepads? gamepads;
    private readonly TextBlock movementHint = Text("", 12, Brushes.White);
    private readonly TextBlock actionHint = Text("", 11, Muted);
    private readonly ThreeNetView viewport;
    private readonly StackPanel sidebar = new() { Spacing = 10, Margin = new Thickness(22) };
    private readonly TextBlock location = Text("", 30, Brushes.White, true);
    private readonly TextBlock conditions = Text("", 12, Muted);
    private readonly TextBlock fishName = Text("Siap untuk kejutan?", 19, Brushes.White, true);
    private readonly TextBlock directionText = Text("A / D  •  Ikuti arah ikan", 12, Muted);
    private readonly TextBlock saveStatus = Text("Tersimpan otomatis setiap 15 detik", 11, Muted);
    private readonly ProgressBar tension = new() { Minimum = 0, Maximum = 1, Height = 8, Foreground = Accent };
    private readonly ProgressBar progress = new() { Minimum = 0, Maximum = 1, Height = 8, Foreground = Brush.Parse("#65C5BA") };
    private readonly Button cast;
    private readonly HashSet<Key> keys = [];
    private readonly DispatcherTimer timer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private IslandWorld? world;
    private bool reeling;
    private bool reelPress;
    private double lastTime;
    private double lastSave;
    private double lastRenderedAt;
    private double lastUiRefresh;
    private string page = "Ekspedisi";
    private string? loadError;
    private bool rendererFailed;
    private bool snapshotStarted;
    private int frameCount;
    private float waterClarity = .075f;
    private float waveStrength = .035f;
    private readonly RuntimeBenchmark benchmark=new();
    private Bitmap? harborArt;
    private readonly SceneCacheCompatibility sceneCache = new();
    private bool benchmarkMode=>Program.Arguments.Contains("--benchmark");
    private bool legacyFrameLoop=>Program.Arguments.Contains("--legacy-frame-loop");
    private bool snapshotMode => Program.Arguments.Contains("--screenshot") || Program.Arguments.Contains("--nature-review") || Program.Arguments.Contains("--world-tour") || benchmarkMode || nativeNetworkMode;

    public MainWindow()
    {
        StartupDiagnostics.Mark("window-constructor");
        Title = "Nusantara Fishing Madness 3D • Syubadubin Studios";
        Width = 1440; Height = 900; MinWidth = 1080; MinHeight = 720;
        Background = Ink;
        FontFamily = new FontFamily("Segoe UI");
        Foreground = Brushes.White;
        var savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SyubadubinStudios", "NusantaraFishingMadness", "save.json");
        settingsPath = Path.Combine(Path.GetDirectoryName(savePath)!, "settings.json");
        settings = new();
        if (!snapshotMode)
        {
            try { settings = UserSettings.Load(settingsPath); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
            { settingsReadOnly = true; saveStatus.Text = "Pengaturan tidak valid: " + ex.Message; }
        }
        Localization.Configure(settings.Language, settings.TextScale);
        if(Program.Arguments.Contains("--low-power"))
        { settings.Shadows=false; settings.MsaaSamples=1; settings.RenderScale=.6; settings.FramesPerSecond=45; }
        waterClarity = settings.WaterAbsorption; waveStrength = settings.WaveStrength;
        CrashReporter.Configure(Path.Combine(Path.GetDirectoryName(savePath)!, "crash-reports"), settings.CrashReports);
        // Screenshot mode never reads or overwrites the player's save.
        store = new SaveStore(snapshotMode ? Path.Combine(Path.GetTempPath(), "nusantara-screenshot-save.json") : savePath);
        PlayerState player = new();
        if (snapshotMode) player.WorldHours = 9.5;
        if (Program.Arguments.Contains("--nature-review"))
        {
            var region = Program.Arguments.FirstOrDefault(x => x.StartsWith("--nature-region=", StringComparison.Ordinal))?.Split('=', 2)[1];
            if (region is not null && Catalog.Regions.Any(x => x.Id == region))
            { player.RegionId = region; player.Experience = 1350; }
        }
        if (!snapshotMode)
        {
            try { player = store.Load(); }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException or UnauthorizedAccessException)
            { loadError = "Save tidak bisa dibaca. File asli dipertahankan. " + ex.Message; }
        }
        vm = new(new GameSession(player));
        DataContext = vm;
        var root = new Grid { RowDefinitions = new RowDefinitions("90,62,*,36") };
        Content = root;
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(30, 15) };
        var brand = new StackPanel { Spacing = 1 };
        brand.Children.Add(Text("NUSANTARA", 25, Brushes.White, true));
        brand.Children.Add(Text("F I S H I N G   M A D N E S S   /   3 D", 10, Accent));
        header.Children.Add(brand);
        var stats = Text("", 16, Brushes.White, true);
        stats.VerticalAlignment = VerticalAlignment.Center;
        stats.Bind(TextBlock.TextProperty, new Binding(nameof(GameViewModel.Summary)));
        Grid.SetColumn(stats, 1); header.Children.Add(stats); root.Children.Add(header);
        var navigation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(24, 4) };
        foreach (var name in new[] { "Ekspedisi", "Peta nusantara", "Koleksi", "Misi", "Bengkel", "Karakter", "Perairan", "Pengaturan", "Multiplayer", "Tentang" })
        {
            var tab = Button(name, () => ShowPage(name));
            tab.Background = Brushes.Transparent;
            navigation.Children.Add(tab);
        }
        var navScroll = new ScrollViewer { Content = navigation, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(navScroll, 1); root.Children.Add(navScroll);
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,334"), Margin = new Thickness(20, 0, 20, 12) };
        Grid.SetRow(body, 2); root.Children.Add(body);
        viewport = new ThreeNetView
        {
            IsRendering=legacyFrameLoop,
            RenderScale = settings.RenderScale,
            MaxFramesPerSecond = settings.FramesPerSecond,
            RendererOptions = RendererOptions.Default with { BgraOutput = true, VSync = false, Shadows = settings.Shadows,MsaaSamples=settings.MsaaSamples,ShadowMapSize = 1024, ShadowDistance = 55, Bloom = false, Exposure = .72f }
        };
        viewport.RenderFailed += (_, error) => { rendererFailed = true; saveStatus.Text = "Renderer gagal: " + error; };
        viewport.RendererCreated+=(_,_)=>StartupDiagnostics.Mark("renderer-created");
        viewport.Frame+=(_,frame)=>
        {
            if(benchmarkMode) benchmark.Record(viewport,frame);
            if (loadingFramePending) { loadingFramePending = false; loadingLayer.IsVisible = false; }
        };
        var viewArea = new Grid { ClipToBounds = true };
        viewArea.Children.Add(viewport);
        var titlePanel = new StackPanel { Margin = new Thickness(16), Spacing = 5, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
        titlePanel.Children.Add(Text("CATATAN EKSPEDISI   /   NUSANTARA", 10, Brush.Parse("#F4E3B4"), true));
        titlePanel.Children.Add(location);
        titlePanel.Children.Add(conditions);
        viewArea.Children.Add(new Border
        {
            Background = Brush.Parse("#CD102E3B"),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(22),
            Width = 400,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = titlePanel
        });
        var controlPanel = new StackPanel { Spacing = 6, Width = 550 };
        controlPanel.Children.Add(movementHint);
        controlPanel.Children.Add(actionHint);
        viewArea.Children.Add(new Border { Background = Brush.Parse("#D9102E3B"), CornerRadius = new CornerRadius(8), Padding = new Thickness(18, 12), Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left, Child = controlPanel });
        var liveStatus = Text("", 13, Brushes.White);
        liveStatus.Bind(TextBlock.TextProperty, new Binding(nameof(GameViewModel.Status)));
        viewArea.Children.Add(new Border
        {
            Background = Brush.Parse("#E6102E3B"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(24, 0, 24, 110),
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Child = liveStatus
        });
        body.Children.Add(new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, Child = viewArea });
        var side = new Border { Background = Panel, CornerRadius = new CornerRadius(12), Margin = new Thickness(12, 0, 0, 0), Child = new ScrollViewer { Content = sidebar, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } };
        Grid.SetColumn(side, 1); body.Children.Add(side);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(24, 0, 24, 10) };
        footer.Children.Add(Text("Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios", 10, Muted));
        Grid.SetColumn(saveStatus, 1); footer.Children.Add(saveStatus); Grid.SetRow(footer, 3); root.Children.Add(footer);
        cast = Button("Lempar kail  ↗", () =>
        {
            if (reelPress) { reelPress = false; return; }
            vm.CastCommand.Execute(null);
        }, true);
        cast.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (vm.Game.Phase != FishingPhase.Fighting) { reelPress = false; return; }
            reelPress = true; reeling = true;
        }, RoutingStrategies.Tunnel);
        cast.AddHandler(PointerReleasedEvent, (_, _) => reeling = false, RoutingStrategies.Tunnel);
        cast.PointerCaptureLost += (_, _) => reeling = false;
        // Routed keyboard handlers keep controls usable even after a navigation button has focus.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => { keys.Clear(); reeling = false; };
        vm.StateChanged += Refresh;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(legacyFrameLoop?33:8) };
        timer.Tick += Tick;
        BuildPresentation(root);
        Opened += async (_, _) =>
        {
            StartupDiagnostics.Mark("window-opened");
            await PresentWorldAsync(true);
            if (isClosing) return;
            ShowPage("Ekspedisi"); timer.Start();
            StartupDiagnostics.Mark("world-and-ui-ready");
            try { audio = new GameAudio(snapshotMode); } catch (Exception ex) when (ex is ThreeNetException or IOException) { saveStatus.Text = "Audio: " + ex.Message; }
            try { gamepads = new Gamepads(); } catch (ThreeNetException) { gamepads = null; }
            StartupDiagnostics.Mark("audio-and-input-ready");
            if (loadError is not null) saveStatus.Text = loadError;
        };
        Closed += (_, _) => { timer.Stop(); settingsTimer.Stop(); Save(); PersistSettings(); CloseNetwork(); audio?.Dispose(); gamepads?.Dispose(); world?.Dispose(); harborArt?.Dispose(); DisposePresentation(); };
    }

    private static TextBlock Text(string text, double size, IBrush brush, bool bold = false) => Localization.Register(new TextBlock
    {
        Text = text,
        FontSize = size,
        Foreground = brush,
        FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
        TextWrapping = TextWrapping.Wrap
    }, text, size);
    private static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(14, 10),
            CornerRadius = new CornerRadius(6),
            Background = primary ? Accent : Brush.Parse("#28505B"),
            Foreground = primary ? Ink : Brushes.White,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        button.Click += (_, _) => action();
        return Localization.Register(button, text);
    }
    private void Heading(string eyebrow, string title, string description)
    {
        sidebar.Children.Add(Text(eyebrow.ToUpperInvariant(), 10, Accent, true));
        sidebar.Children.Add(Text(title, 23, Brushes.White, true));
        sidebar.Children.Add(Text(description, 12, Muted));
        sidebar.Children.Add(new Separator { Background = Brush.Parse("#35545D"), Height = 1, Margin = new Thickness(0, 4) });
    }
    private void ShowPage(string name)
    {
        page = name;
        creditsLayer.IsVisible = false; creditsTimer.Stop();
        if (name == "Tentang") { ShowCredits(); return; }
        sidebar.Children.Clear();
        switch (name)
        {
            case "Ekspedisi":
                Heading("Petualangan dimulai", "Satu lemparan.\nSeribu kemungkinan.", "Tidak ada yang tahu apa yang akan muncul dari air berikutnya.");
                sidebar.Children.Add(Text("PEMANCING", 10, Muted));
                sidebar.Children.Add(Text(vm.Game.Character.Name, 20, Brushes.White, true));
                sidebar.Children.Add(Text(vm.Game.Character.Bonus, 12, Muted));
                sidebar.Children.Add(fishName);
                sidebar.Children.Add(Text("TEGANGAN TALI", 10, Muted)); sidebar.Children.Add(tension);
                sidebar.Children.Add(Text("JARAK TANGKAPAN", 10, Muted)); sidebar.Children.Add(progress);
                sidebar.Children.Add(directionText);
                sidebar.Children.Add(cast);
                sidebar.Children.Add(Button("Kemampuan aktif  [F]", () => vm.Execute("ability")));
                sidebar.Children.Add(Button("Naik / turun perahu  [E]", () => vm.Execute("board")));
                if (vm.Game.Region.Id == "ampat" || vm.Game.Character.Id == "ojan")
                {
                    sidebar.Children.Add(Text("Cara menangkap", 13, Muted));
                    sidebar.Children.Add(Button("Gunakan joran", () => vm.Execute("method", "Rod")));
                    if (vm.Game.Region.Id == "ampat")
                    {
                        sidebar.Children.Add(Button("Snorkeling • dokumentasi satwa", () => vm.Execute("method", "Snorkel")));
                        sidebar.Children.Add(Button("Spearfishing • gunakan tombak", () => vm.Execute("method", "Spear")));
                    }
                    if (vm.Game.Character.Id == "ojan")
                    {
                        sidebar.Children.Add(Button("Tangkap dengan tangan di tepian", () => vm.Execute("method", "Hand")));
                        sidebar.Children.Add(Button("Panjat / turun pohon", () => vm.Execute("climb")));
                    }
                }
                sidebar.Children.Add(Button("Tantang boss regional", () => vm.BossCommand.Execute(null)));
                sidebar.Children.Add(Text("Boss: joran Lv. 3 + 10 tangkapan", 11, Muted));
                sidebar.Children.Add(Text(vm.Game.World.Mechanic, 12, Muted));
                if (vm.Game.World.HasGovernmentPier)
                    sidebar.Children.Add(Text("Pos pemerintah di timur dermaga (16, 28): Pak Tono memiliki izin masuk.", 12, Muted));
                var status = Text("", 13, Brushes.White);
                status.Bind(TextBlock.TextProperty, new Binding(nameof(GameViewModel.Status)));
                sidebar.Children.Add(new Border { Background = Ink, CornerRadius = new CornerRadius(7), Padding = new Thickness(14), Child = status });
                break;
            case "Peta nusantara":
                Heading("Atlas perjalanan", "Pilih perairan", "Wilayah terbuka mengikuti level. Biaya berlaku setiap perjalanan.");
                var artPath = Path.Combine(AppContext.BaseDirectory, "assets/art/harbor-keyart.png");
                if (File.Exists(artPath))
                {
                    harborArt ??= new Bitmap(artPath);
                    sidebar.Children.Add(new Image { Source = harborArt, Height = 150, Stretch = Stretch.UniformToFill });
                }
                foreach (var region in Catalog.Regions)
                {
                    var current = region.Id == vm.Game.Player.RegionId;
                    sidebar.Children.Add(Text(region.Name, 17, Brush.Parse(region.Color), true));
                    sidebar.Children.Add(Text(region.Description, 12, Muted));
                    sidebar.Children.Add(Button(current ? "Anda di sini" : $"Lv. {region.Level}  •  {region.TravelCost} koin  →", async () =>
                    {
                        if(vm.Execute("travel",region.Id)) await PresentWorldAsync();
                        ShowPage("Peta nusantara");
                    }));
                }
                break;
            case "Koleksi":
                Heading("Jurnal penemuan", "Cerita dari air", $"{vm.Game.Player.Inventory.Count}/200 di tas • {vm.Game.Player.Exhibits.Count} pameran • +{vm.Game.Player.Exhibits.Count * 3} koin/menit aktif.");
                sidebar.Children.Add(Button("Jual semua isi tas", () => { vm.SellCommand.Execute(null); ShowPage(page); }, true));
                if (vm.Game.Player.Discovered.Contains("dugong")) sidebar.Children.Add(Text("Sahabat Dugong • pencapaian terbuka", 14, Accent, true));
                foreach (var trophy in vm.Game.Player.Trophies.TakeLast(4))
                    sidebar.Children.Add(Text($"Piala {trophy.Period} • Peringkat {trophy.Rank} • Dipajang di museum", 13, Accent));
                foreach (var id in vm.Game.Player.Discovered.OrderBy(x => x))
                {
                    var species = Catalog.FindSpecies(id);
                    var count = vm.Game.Player.Inventory.Count(x => x.SpeciesId == id);
                    sidebar.Children.Add(Text(species.Name, 16, Brushes.White, true));
                    sidebar.Children.Add(Text($"{species.Rarity} • {count} di tas • {species.Kind}", 11, Muted));
                    if (vm.Game.Player.Exhibits.Contains(id)) sidebar.Children.Add(Text("Dipamerkan • +3 koin/menit", 11, Accent));
                    else if (count > 0) sidebar.Children.Add(Button("Sumbangkan ke pameran", () => { vm.Execute("donate",id); ShowPage(page); }));
                    if (vm.Game.Player.Exhibits.Contains(id))
                    {
                        var museum = species.Kind is CatchKind.Artifact or CatchKind.Junk or CatchKind.Boss;
                        var offset = museum ? 100 : 0;
                        var capacity = (museum ? vm.Game.Player.MuseumLevel : vm.Game.Player.AquariumLevel) * 4;
                        var slots = new ComboBox { ItemsSource = Enumerable.Range(0, capacity).Select(i => $"Slot {i + 1}").ToArray(), SelectedIndex = vm.Game.Player.ExhibitSlots.GetValueOrDefault(id, offset) - offset, HorizontalAlignment = HorizontalAlignment.Stretch };
                        sidebar.Children.Add(slots);
                        sidebar.Children.Add(Button("Tempatkan di " + (museum ? "museum" : "aquarium"), () =>
                        { vm.Execute("place",id,offset+slots.SelectedIndex); ShowPage(page); }));
                    }
                }
                if (vm.Game.Player.Discovered.Count == 0) sidebar.Children.Add(Text("Belum ada penemuan. Kembali ke Ekspedisi dan lempar kail pertama Anda.", 14, Muted));
                sidebar.Children.Add(Text("Satwa konservasi dicatat dan dilepas kembali. Artefak & benda unik masuk museum; makhluk lainnya masuk aquarium.", 12, Muted));
                break;
            case "Misi":
                Heading("Buku tugas", "Jejak petualangan", "Ambil hadiah setelah target terpenuhi.");
                foreach (var quest in Quests.All.Where(x => Quests.Visible(x, vm.Game.Player)))
                {
                    var value = Quests.Progress(quest, vm.Game.Player);
                    sidebar.Children.Add(Text(quest.Title, 17, Brushes.White, true));
                    sidebar.Children.Add(Text(quest.Description, 12, Muted));
                    var claimed = vm.Game.Player.ClaimedQuests.Contains(quest.Id);
                    var claim = Button(claimed ? "Hadiah diambil" : $"{Math.Min(value, quest.Target)}/{quest.Target} • Ambil {quest.Reward} koin", () => { vm.Execute("quest",quest.Id); ShowPage(page); });
                    claim.IsEnabled = !claimed && value >= quest.Target; sidebar.Children.Add(claim);
                }
                break;
            case "Bengkel":
                Heading("Dermaga nelayan", "Siap lebih jauh", "Perkuat joran untuk menghadapi boss dan buka perjalanan berikutnya.");
                if (vm.Game.Character.Id == "rina")
                {
                    sidebar.Children.Add(Text("Barter Rina", 19, Brushes.White, true));
                    sidebar.Children.Add(Button("3 sandal → Keris Kuno", () => { vm.Execute("barter", "keris"); ShowPage(page); }));
                    sidebar.Children.Add(Button("2 kulkas → Peta Rahasia", () => { vm.Execute("barter", "peta"); ShowPage(page); }));
                }
                sidebar.Children.Add(Text($"Joran level {vm.Game.Player.RodLevel}", 20, Brushes.White, true));
                sidebar.Children.Add(Text("Setiap upgrade memperkuat tali dan mempercepat tarikan.", 13, Muted));
                var upgrade = Button(vm.Game.Player.RodLevel >= 10 ? "Joran maksimum" : $"Upgrade • {vm.Game.UpgradeCost} koin", () => { vm.UpgradeCommand.Execute(null); ShowPage(page); }, true);
                upgrade.IsEnabled = vm.Game.Player.RodLevel < 10; sidebar.Children.Add(upgrade);
                sidebar.Children.Add(Text(Catalog.Boats[vm.Game.Player.BoatIndex].Name, 20, Brushes.White, true));
                sidebar.Children.Add(Text("Perahu meningkatkan kekuatan tarikan dan kecepatan berlayar. Setiap jenis memiliki bentuk sendiri.", 13, Muted));
                if (vm.Game.Player.BoatIndex < Catalog.Boats.Length - 1)
                {
                    var boat = Catalog.Boats[vm.Game.Player.BoatIndex + 1];
                    sidebar.Children.Add(Button($"{boat.Name} • {(int)(boat.Cost * (1 - vm.Game.Character.Discount))} koin", () => { vm.BoatCommand.Execute(null); ShowPage(page); }));
                }
                sidebar.Children.Add(Text(vm.Game.Message, 13, Accent));
                sidebar.Children.Add(Text("Umpan & kedalaman", 19, Brushes.White, true));
                foreach (var bait in WorldDefinition.Baits)
                {
                    sidebar.Children.Add(Text(bait.Description, 12, Muted));
                    sidebar.Children.Add(Button((vm.Game.Player.BaitId == bait.Id ? "✓ " : "") + bait.Name + $" • {bait.Cost}/lempar", () =>
                    { vm.Execute("bait",bait.Id); ShowPage(page); }));
                }
                sidebar.Children.Add(Text($"Aquarium Lv. {vm.Game.Player.AquariumLevel} • Museum Lv. {vm.Game.Player.MuseumLevel}", 17, Brushes.White, true));
                sidebar.Children.Add(Button($"Perluas aquarium • {vm.Game.Player.AquariumLevel * 400} koin", () =>
                { vm.Execute("aquarium"); ShowPage(page); }));
                sidebar.Children.Add(Button($"Perluas museum • {vm.Game.Player.MuseumLevel * 400} koin", () =>
                { vm.Execute("museum"); ShowPage(page); }));
                sidebar.Children.Add(Text($"Pengunjung disambut: {vm.Game.Player.VisitorsWelcomed}. Tempatkan koleksi lalu kunjungi gedung di dermaga dengan berjalan kaki.", 12, Muted));
                break;
            case "Karakter":
                Heading("Rekan ekspedisi", "Pilih pemancing", "Ganti karakter saat tidak sedang memancing. Progres tetap tersimpan.");
                foreach (var character in Catalog.Characters)
                {
                    sidebar.Children.Add(Text(character.Name, 19, Brushes.White, true));
                    sidebar.Children.Add(Text(character.Role + " • " + character.Bonus, 12, Muted));
                    var unlocked = vm.Game.Player.Level >= GameSession.UnlockLevel(character.Id);
                    var choose = Button(!unlocked ? $"Terbuka di Lv. {GameSession.UnlockLevel(character.Id)}" : vm.Game.Character.Id == character.Id ? "Sedang dimainkan" : "Pilih karakter", () => { vm.Execute("character",character.Id); ShowPage(page); });
                    choose.IsEnabled = unlocked;
                    sidebar.Children.Add(choose);
                }
                break;
            case "Perairan":
                Heading("Lihat lebih dalam", "Air & kehidupan", "Gelombang, pantulan, dan riak tetap terlihat saat Anda mengamati kehidupan di bawah air.");
                sidebar.Children.Add(Text("Kejernihan air", 17, Brushes.White, true));
                sidebar.Children.Add(Text("Kiri: lebih jernih. Kanan: lebih keruh. Air terlihat lebih gelap dari sudut mendatar.", 12, Muted));
                var clarity = new Slider { Minimum = .025, Maximum = .45, Value = waterClarity };
                clarity.PropertyChanged += (_, e) =>
                {
                    if (e.Property != Slider.ValueProperty) return;
                    waterClarity = (float)clarity.Value;
                    if (world is not null) world.WaterClarity = waterClarity;
                    PersistSettings();
                };
                sidebar.Children.Add(clarity);
                sidebar.Children.Add(Text("Kekuatan gelombang kecil", 17, Brushes.White, true));
                var waves = new Slider { Minimum = .02, Maximum = .2, Value = waveStrength };
                waves.PropertyChanged += (_, e) =>
                {
                    if (e.Property != Slider.ValueProperty) return;
                    waveStrength = (float)waves.Value;
                    if (world is not null) world.WaveStrength = waveStrength;
                    PersistSettings();
                };
                sidebar.Children.Add(waves);
                sidebar.Children.Add(Text("Di bawah permukaan", 20, Brushes.White, true));
                sidebar.Children.Add(Text("Dasar berpasir, batu, karang, lamun, dan gerombolan ikan. Riak mengikuti gerak perahu, sambaran kail, dan hujan. Cahaya di dasar air mengikuti simulasi gelombang yang sama.", 13, Muted));
                sidebar.Children.Add(Button("Air tropis jernih", () =>
                {
                    clarity.Value = .055; waves.Value = .055;
                }, true));
                break;
            case "Pengaturan":
                BuildSettingsPage();
                break;
            case "Multiplayer":
                BuildNetworkPage();
                break;
        }
        Refresh();
    }

    private void Refresh()
    {
        var game = vm.Game;
        location.Text = game.Region.Name;
        conditions.Text = $"{(int)game.Player.WorldHours:00}:{(int)(game.Player.WorldHours % 1 * 60):00}   /   {WeatherName(game.Weather)}   /   {game.Depth:N1} m   /   {(game.Player.OnFoot ? "BERJALAN" : Catalog.Boats[game.Player.BoatIndex].Name)}";
        tension.Value = game.Tension; progress.Value = game.Progress;
        tension.Foreground = game.Tension > .75 ? Brush.Parse("#EE7965") : Accent;
        fishName.Text = Localization.T(game.Phase switch
        {
            FishingPhase.Fighting => game.AbilityActive && game.Character.Id == "bambang" ? game.Hooked!.Name : "Ada yang menyambar!",
            FishingPhase.Landed => game.Hooked?.Name ?? "Tertangkap!",
            FishingPhase.Waiting => "Menunggu sambaran…",
            FishingPhase.Escaped => "Coba sekali lagi.",
            _ => "Siap untuk kejutan?"
        });
        directionText.Text = game.Phase == FishingPhase.Fighting ? (game.FishDirection < -.25 ? "← Tahan A untuk mengikuti ikan" : game.FishDirection > .25 ? "Tahan D untuk mengikuti ikan →" : "Arah ikan di tengah • lepas A / D") : "Jaga tegangan di bawah batas merah.";
        cast.IsEnabled = game.Phase != FishingPhase.Waiting;
        cast.Content = Localization.T(game.Phase == FishingPhase.Fighting ? "Tahan untuk tarik" : game.Phase == FishingPhase.Waiting ? "Menunggu sambaran…" : "Lempar kail  ↗");
        movementHint.Text = Localization.T($"{settings.Keys["forward"]} {settings.Keys["left"]} {settings.Keys["back"]} {settings.Keys["right"]}  Berlayar  •  Drag Kamera  •  Scroll Zoom");
        actionHint.Text = Localization.T($"{settings.Keys["cast"]} Lempar / tarik  •  {settings.Keys["board"]} Naik / turun  •  {settings.Keys["ability"]} Kemampuan  •  {settings.Keys["jump"]} Lompat");
        conditions.Text = Localization.T(conditions.Text ?? "");
        directionText.Text = Localization.T(directionText.Text ?? "");
    }
    private static string WeatherName(Weather weather) => weather switch
    { Weather.Sunny => "CERAH", Weather.Rain => "HUJAN", Weather.Storm => "BADAI", Weather.Fog => "KABUT", _ => "PETIR" };
    private void LoadWorld()
    {
        world?.Dispose(); world = null;
        try
        {
            StartupDiagnostics.Mark("world-construction-start");
            SceneCacheCompatibility.FlushResources(viewport.Renderer);
            world = new IslandWorld(viewport, vm.Game, sceneCache) { WaterClarity = waterClarity, WaveStrength = waveStrength, ReducedMotion = settings.ReducedMotion };
            if (roomView is not null) { world.SetPeers(roomView.Players, roomView.PlayerId); world.SetSharedBoss(roomView.Boss); }
            StartupDiagnostics.Mark("world-construction-end");
            viewport.IsRendering = legacyFrameLoop; rendererFailed = false;
        }
        catch (Exception ex) when (ex is ThreeNetException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { rendererFailed = true; saveStatus.Text = "3D tidak tersedia: " + ex.Message; }
    }
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (BlocksGameInput)
        {
            keys.Clear();
            if (creditsLayer.IsVisible && e.Key == Key.Escape) ShowPage("Ekspedisi");
            e.Handled = true; return;
        }
        if (FocusManager?.GetFocusedElement() is Visual focused &&
            focused.GetSelfAndVisualAncestors().Any(x => x is TextBox or ComboBox))
        { keys.Clear(); return; }
        var action = settings.Keys.FirstOrDefault(x => x.Value == e.Key.ToString()).Key;
        if (action is "board" or "ability" or "jump")
        {
            if (!keys.Add(e.Key)) return;
            vm.Execute(action);
            e.Handled = true; return;
        }
        if (action is null) return;
        var first = keys.Add(e.Key);
        if (action == "cast" && first && !vm.Game.Busy) vm.CastCommand.Execute(null);
        e.Handled = true;
    }
    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        keys.Remove(e.Key);
        if (e.Key == settings.KeyFor("cast") && FocusManager?.GetFocusedElement() is not TextBox) e.Handled = true;
    }
    private void Tick(object? sender, EventArgs e)
    {
        var now = clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - lastTime, 0, .1); lastTime = now;
        if (presentingWorld) return;
        var direction = (keys.Contains(settings.KeyFor("right")) ? 1f : 0) - (keys.Contains(settings.KeyFor("left")) ? 1 : 0);
        var padMove = Vector2.Zero;
        var padReel = false;
        if (gamepads is not null && !BlocksGameInput)
        {
            gamepads.Update();
            var pad = gamepads.Connected.FirstOrDefault();
            if (pad.IsConnected)
            {
                padMove = new(pad.LeftStick.X, -pad.LeftStick.Y);
                if (Math.Abs(pad.LeftStick.X) > .2f) direction = pad.LeftStick.X;
                padReel = pad.IsDown(GamepadButton.South);
                if (pad.WasPressed(GamepadButton.South) && !vm.Game.Busy) vm.CastCommand.Execute(null);
                if (pad.WasPressed(GamepadButton.East)) vm.Execute("board");
                if (pad.WasPressed(GamepadButton.West)) vm.Execute("ability");
                if (pad.WasPressed(GamepadButton.North)) vm.Execute("jump");
                world?.AdjustCamera(pad.RightStick * (float)dt, (pad.IsDown(GamepadButton.RightBumper) ? 1 : 0) - (pad.IsDown(GamepadButton.LeftBumper) ? 1 : 0));
            }
        }
        var phase = vm.Game.Phase;
        var reelInput=reeling || keys.Contains(settings.KeyFor("cast")) || padReel;
        if(network is null) vm.Game.Tick(dt,reelInput,direction);
        var move = new Vector2((keys.Contains(settings.KeyFor("right")) ? 1 : 0) - (keys.Contains(settings.KeyFor("left")) ? 1 : 0), (keys.Contains(settings.KeyFor("back")) ? 1 : 0) - (keys.Contains(settings.KeyFor("forward")) ? 1 : 0)) + padMove;
        if(move.LengthSquared()>1) move=Vector2.Normalize(move);
        if(network is null) vm.Game.Move((float)dt,move);
        else PollNetwork(new(move.X,move.Y,reelInput,Math.Clamp(direction,-1,1)),now);
        // Schedule from the previous frame's start, rather than waiting an entire interval after readback.
        // This keeps the configured cap while avoiding ThreeNetView's render-time + timer-time cadence.
        if(legacyFrameLoop && !rendererFailed && !creditsLayer.IsVisible) world?.Update((float)dt,(float)now,move);
        if(!legacyFrameLoop && !rendererFailed && !creditsLayer.IsVisible && now-lastRenderedAt>=1.0/settings.FramesPerSecond)
        {
            var renderDt=Math.Clamp(now-lastRenderedAt,0,.1);
            lastRenderedAt=now;
            world?.Update((float)renderDt,(float)now,move);
            if(frameCount==0) StartupDiagnostics.Mark("first-frame-start");
            viewport.RenderFrame();
            if(frameCount==0) StartupDiagnostics.Mark("first-frame-readback");
        }
        if (audio is not null)
        {
            audio.Volume = settings.Volume; audio.MusicVolume = settings.MusicVolume; audio.Muted = settings.Muted;
            audio.Update(vm.Game, rendererFailed ? null : world, (float)dt);
        }
        frameCount++;
        if (now-lastUiRefresh>.12 || phase != vm.Game.Phase) { vm.Refresh(); lastUiRefresh=now; }
        if (now - lastSave > 15) { Save(); lastSave = now; }
        if(benchmarkMode && (benchmark.Ready || now>180) && !snapshotStarted)
        {
            snapshotStarted=true;
            try { Console.WriteLine("BENCHMARK_OK: "+benchmark.Write(viewport,world!,Path.GetFullPath(Program.Arguments.Contains("--low-power")?"artifacts/benchmarks/low-power.json":Program.Arguments.Contains("--no-batching")?"artifacts/benchmarks/baseline.json":"artifacts/benchmarks/optimized.json"))); }
            catch(Exception ex) { Console.Error.WriteLine("BENCHMARK_FAILED: "+ex); Environment.ExitCode=1; }
            Close();
        }
        if(nativeNetworkMode && now>6 && !snapshotStarted && viewport.Renderer is not null)
        { snapshotStarted=true; _=RunNativeNetworkSmoke(); }
        if (snapshotMode && !benchmarkMode && !nativeNetworkMode && now > 6 && !snapshotStarted)
        {
            snapshotStarted = true;
            Dispatcher.UIThread.Post(CaptureScreenshots);
        }
    }
    private void Save()
    {
        if (loadError is not null || snapshotMode || network is not null) return;
        try { store.Save(vm.Game.Player); saveStatus.Text = "Tersimpan • " + DateTime.Now.ToString("HH:mm:ss"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { saveStatus.Text = "Gagal menyimpan: " + ex.Message; }
    }
    private async void CaptureScreenshots()
    {
        try
        {
            if (rendererFailed || viewport.Renderer is null) throw new InvalidOperationException("No rendered 3D frame.");
            // A standalone nature review keeps its incidental captures out of the documentation images.
            var reviewOnly = Program.Arguments.Contains("--nature-review") && !Program.Arguments.Contains("--screenshot");
            var folder = Path.GetFullPath(reviewOnly ? "artifacts/nature-review" : "docs/images"); Directory.CreateDirectory(folder);
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height), new Vector(96, 96)))
            { bitmap.Render(this); bitmap.Save(Path.Combine(folder, "expedition.png"), PngBitmapEncoderOptions.Default); }
            using (var frame = viewport.CaptureFrame()) frame?.Save(Path.Combine(folder, "world.png"), PngBitmapEncoderOptions.Default);
            void Screenshot(string filename)
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height), new Vector(96, 96));
                bitmap.Render(this); bitmap.Save(Path.Combine(folder, filename), PngBitmapEncoderOptions.Default);
            }
            ShowPage("Peta nusantara");
            await Task.Delay(400);
            Screenshot("atlas.png");
            ShowPage("Perairan");
            await Task.Delay(400);
            Screenshot("water.png");
            foreach (var tab in new[] { "Karakter", "Bengkel", "Misi", "Koleksi", "Pengaturan" })
            {
                ShowPage(tab);
                await Task.Delay(60);
            }
            settings.Language = "en";
            ApplySettings(); ShowPage("Pengaturan");
            await Task.Delay(350);
            Screenshot("settings-en.png");
            if (Localization.T("Lempar kail  ↗") != "Cast your line  ↗") throw new InvalidOperationException("Locale resource not applied.");
            settings.Language = "id"; ApplySettings();
            ShowPage("Ekspedisi");
            cast.RaiseEvent(new RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
            if (vm.Game.Phase != FishingPhase.Waiting) throw new InvalidOperationException("Smoke: cast button failed.");
            for (var i = 0; i < 50 && vm.Game.Phase == FishingPhase.Waiting; i++) vm.Game.Tick(.1);
            vm.Refresh();
            await Task.Delay(400);
            Screenshot("fishing.png");
            for (var i = 0; i < 1800 && vm.Game.Busy; i++)
                vm.Game.Tick(.05, vm.Game.Tension < .65, Math.Sign(vm.Game.FishDirection) * .8);
            if (vm.Game.Phase != FishingPhase.Landed || vm.Game.Player.TotalCaught != 1)
                throw new InvalidOperationException("Smoke: fishing did not land a catch.");
            if (world?.AssetWarning is not null) throw new InvalidOperationException("Asset import: " + world.AssetWarning);
            if (audio is null || audio.MixedSamples <= 0) throw new InvalidOperationException("Offline audio mixer did not produce samples.");
            Console.WriteLine($"AUDIO_OK: {audio.MixedSamples} samples mixed from original clips.");
            if (Program.Arguments.Contains("--visual-review"))
            {
                world!.ReviewCamera(new(-12, 2, -6), 24, .5f, .2f);
                await Task.Delay(700);
                using (var frame = viewport.CaptureFrame()) frame?.Save(Path.Combine(folder, "visual-harbor.png"), PngBitmapEncoderOptions.Default);
                foreach (var character in new[] { "ujang", "rina" })
                {
                    vm.Game.Player.CharacterId = character;
                    world.ReviewCamera(new(vm.Game.Player.PositionX, 1.2f, vm.Game.Player.PositionZ), 7, 2.8f, .12f);
                    await Task.Delay(700);
                    using var frame = viewport.CaptureFrame();
                    frame?.Save(Path.Combine(folder, "visual-" + character + ".png"), PngBitmapEncoderOptions.Default);
                    if (world.AssetWarning is not null) throw new InvalidOperationException(world.AssetWarning);
                }
                world.ReviewCamera(null);
            }
            if (Program.Arguments.Contains("--world-tour"))
            {
                vm.Game.Player.Experience = 1350;
                vm.Game.Player.Coins = 100000;
                foreach (var region in Catalog.Regions)
                {
                    if (!vm.Game.Travel(region.Id)) throw new InvalidOperationException("World tour travel failed: " + region.Id);
                    LoadWorld(); ShowPage("Ekspedisi");
                    await Task.Delay(600);
                    viewport.RenderFrame();
                    if (rendererFailed || world is null || world.ResidentTerrainChunks == 0 || world.AmbientActorCount == 0)
                        throw new InvalidOperationException("World tour render failed: " + region.Id);
                    Screenshot("biome-" + region.Id + ".png");
                    Console.WriteLine($"WORLD_OK: {region.Id}, {world.Scene.NodeCount} nodes, {world.ResidentTerrainChunks} terrain tiles, {world.AmbientActorCount} ambient actors");
                }
                vm.Game.Travel("selatan"); LoadWorld();
                foreach (var hour in new[] { 6, 12, 18, 0 })
                {
                    vm.Game.Player.WorldHours = hour;
                    await Task.Delay(400);
                    Screenshot("time-" + hour + ".png");
                }
            }
            if (Program.Arguments.Contains("--content-review")) await ReviewDesignContent(folder);
            if (Program.Arguments.Contains("--nature-review")) await ReviewNature();
            if (Program.Arguments.Contains("--presentation-review")) await ReviewPresentation();
            Console.WriteLine($"SMOKE_OK: {world!.Scene.NodeCount} nodes, {world.Scene.Animations.Count} animation clips, all tabs, fishing landed.");
            Console.WriteLine("SCREENSHOT_OK • " + folder);
            Close();
        }
        catch (Exception ex) { Console.Error.WriteLine("SCREENSHOT_FAILED: " + ex); Environment.ExitCode = 1; Close(); }
    }
}
