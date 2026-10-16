using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NusantaraFishing.Core;

namespace NusantaraFishing.Desktop;

public sealed partial class MainWindow
{
    private readonly Dictionary<string, Bitmap> presentationArt = [];
    private Border loadingLayer = null!;
    private Grid creditsLayer = null!;
    private Image loadingImage = null!;
    private TextBlock loadingTitle = null!;
    private TextBlock loadingCaption = null!;
    private ScrollViewer creditsScroll = null!;
    private Button creditsPause = null!;
    private readonly DispatcherTimer creditsTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private bool creditsPaused;
    private bool presentingWorld;
    private bool loadingFramePending;
    private double creditTick;
    private bool BlocksGameInput => presentingWorld || loadingLayer.IsVisible || creditsLayer.IsVisible;

    private Bitmap? PresentationArt(string name)
    {
        if (presentationArt.TryGetValue(name, out var cached)) return cached;
        var path = Path.Combine(AppContext.BaseDirectory, "assets/art", name + ".jpg");
        if (!File.Exists(path)) return null;
        return presentationArt[name] = new Bitmap(path);
    }

    private void BuildPresentation(Grid root)
    {
        var loading = new Grid { ClipToBounds = true };
        loadingImage = new Image { Source = PresentationArt("launch-voyage"), Stretch = Stretch.UniformToFill };
        loading.Children.Add(loadingImage);
        loading.Children.Add(new Border { Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = [new(Color.Parse("#E509222B"), 0), new(Color.Parse("#A009222B"), .45), new(Color.Parse("#2009222B"), 1)]
        } });
        var copy = new StackPanel { Spacing = 18, MaxWidth = 560, Margin = new Thickness(64), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(Text("SYUBADUBIN STUDIOS  /  ORIGINAL ADVENTURE", 12, Accent, true));
        loadingTitle = Text("NUSANTARA\nFISHING MADNESS", 52, Brushes.White, true);
        loadingTitle.LineHeight = 58;
        copy.Children.Add(loadingTitle);
        loadingCaption = Text("Menyiapkan perjalanan Anda di perairan Nusantara.", 18, Brushes.White);
        copy.Children.Add(loadingCaption);
        copy.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 3, Width = 300, HorizontalAlignment = HorizontalAlignment.Left, Foreground = Accent, Margin = new Thickness(0, 20, 0, 0) });
        copy.Children.Add(Text("Memuat dunia dan menyiapkan grafis…", 12, Muted));
        loading.Children.Add(copy);
        loading.Children.Add(new TextBlock { Text = "Ariana Mischa Fadhila · Syubadubin Studios", Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(64, 30), VerticalAlignment = VerticalAlignment.Bottom });
        loadingLayer = new Border { Background = Ink, Child = loading, ZIndex = 100 };
        Grid.SetRowSpan(loadingLayer, 4); root.Children.Add(loadingLayer);

        creditsLayer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Background = Ink, IsVisible = false, ZIndex = 90, ClipToBounds = true };
        var art = new Image { Source = PresentationArt("launch-voyage"), Stretch = Stretch.UniformToFill, Opacity = .45 };
        Grid.SetColumnSpan(art, 2); creditsLayer.Children.Add(art);
        var shade = new Border { Background = Brush.Parse("#B009222B") };
        Grid.SetColumnSpan(shade, 2); creditsLayer.Children.Add(shade);
        var introduction = new StackPanel { Spacing = 24, Margin = new Thickness(60), VerticalAlignment = VerticalAlignment.Center };
        introduction.Children.Add(Text("DI BALIK PERJALANAN", 12, Accent, true));
        introduction.Children.Add(Text("NUSANTARA\nFISHING\nMADNESS", 50, Brushes.White, true));
        introduction.Children.Add(Text("Satu lemparan. Seribu cerita.", 20, Brushes.White));
        introduction.Children.Add(Text("Sebuah petualangan dari Syubadubin Studios", 14, Muted));
        introduction.Children.Add(Text("v" + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "preview"), 12, Accent));
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        creditsPause = Button("Jeda kredit", () => SetCreditsPaused(!creditsPaused));
        controls.Children.Add(creditsPause);
        controls.Children.Add(Button("Ulangi", () => { creditsScroll.Offset = new(0, 0); SetCreditsPaused(settings.ReducedMotion); }));
        introduction.Children.Add(controls);
        introduction.Children.Add(Button("Kembali ke ekspedisi", () => ShowPage("Ekspedisi"), true));
        creditsLayer.Children.Add(introduction);
        var roll = new StackPanel { Spacing = 38, Margin = new Thickness(35, 160, 55, 160) };
        void Credit(string role, string name, string? note = null)
        {
            var block = new StackPanel { Spacing = 9 };
            block.Children.Add(Text(role, 12, Accent, true));
            block.Children.Add(Text(name, 29, Brushes.White, true));
            if (note is not null) block.Children.Add(Text(note, 15, Muted));
            roll.Children.Add(block);
        }
        Credit("KREASI & PENGEMBANGAN", "Ariana Mischa Fadhila", "Syubadubin Studios");
        Credit("DUNIA & CERITA", "Nusantara Fishing Madness", "Terinspirasi perairan, kehidupan, dan imajinasi Indonesia.");
        Credit("RENDERER 3D", "ThreeNet / Vibe_Graphics", "Gravicode Studios · Kang Fadhil");
        Credit("APLIKASI & TEKNOLOGI", ".NET · Avalonia", "ReactiveUI · CommunityToolkit.Mvvm");
        Credit("ASET 3D & ILUSTRASI", "Rodin MCP", "Model dan ilustrasi generatif; optimasi, rig, serta animasi melalui Blender MCP.");
        Credit("MUSIK & SUASANA", "Suara perairan Nusantara", "Komposisi sintetis asli proyek, tema regional, boss, dan DJ.");
        Credit("SUMBER TERBUKA", "Terima kasih, para kontributor", "Lisensi dan atribusi dependensi disertakan dalam THIRD-PARTY-NOTICES.md.");
        Credit("UNTUK PARA PENJELAJAH", "Terima kasih sudah bermain", "Sampai jumpa di lemparan berikutnya.");
        creditsScroll = new ScrollViewer { Content = roll, VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Margin = new Thickness(0, 60, 30, 60) };
        creditsScroll.PointerWheelChanged += (_, _) => SetCreditsPaused(true);
        Grid.SetColumn(creditsScroll, 1); creditsLayer.Children.Add(creditsScroll);
        Grid.SetRowSpan(creditsLayer, 4); root.Children.Add(creditsLayer);
        creditsTimer.Tick += (_, _) =>
        {
            var now = clock.Elapsed.TotalSeconds;
            var dt = Math.Clamp(now - creditTick, 0, .1); creditTick = now;
            if (creditsPaused || !creditsLayer.IsVisible) return;
            var end = Math.Max(0, creditsScroll.Extent.Height - creditsScroll.Viewport.Height);
            creditsScroll.Offset = new(0, Math.Min(end, creditsScroll.Offset.Y + dt * 28));
            if (end > 0 && creditsScroll.Offset.Y >= end - 1) SetCreditsPaused(true);
        };
    }

    private void SetCreditsPaused(bool paused)
    {
        creditsPaused = paused;
        creditsPause.Content = Localization.T(paused ? "Lanjutkan kredit" : "Jeda kredit");
    }

    private void ShowCredits()
    {
        keys.Clear(); reeling = false;
        creditsLayer.IsVisible = true;
        creditsScroll.Offset = new(0, 0);
        SetCreditsPaused(settings.ReducedMotion);
        creditTick = clock.Elapsed.TotalSeconds;
        creditsTimer.Start();
    }

    private void ShowLoadingArt(bool startup)
    {
        var name = startup ? "launch-voyage" : vm.Game.World.Biome switch
        {
            Biome.Coast or Biome.Reef => "loading-reef",
            Biome.River or Biome.City or Biome.Pothole or Biome.Waterfall => "loading-river",
            _ => "loading-myth"
        };
        loadingImage.Source = PresentationArt(name);
        loadingTitle.Text = startup ? "NUSANTARA\nFISHING MADNESS" : vm.Game.Region.Name;
        loadingCaption.Text = Localization.T(startup ? "Menyiapkan perjalanan Anda di perairan Nusantara." : vm.Game.Region.Description);
        loadingLayer.IsVisible = true;
        keys.Clear(); reeling = false;
    }

    private async Task PresentWorldAsync(bool startup = false)
    {
        if (presentingWorld || isClosing) return;
        presentingWorld = true;
        ShowLoadingArt(startup);
        try
        {
            // Give Avalonia an opportunity to paint the illustration before native GPU setup.
            await Task.Delay(100, windowLifetime.Token);
            if (isClosing) return;
            if (Program.Arguments.Contains("--presentation-review")) CapturePresentation(startup ? "launch" : "loading-" + vm.Game.Region.Id);
            LoadWorld();
            loadingFramePending = !rendererFailed;
            if (rendererFailed) loadingLayer.IsVisible = false;
        }
        catch (OperationCanceledException) when (isClosing) { }
        finally { presentingWorld = false; }
    }

    private void DisposePresentation()
    {
        creditsTimer.Stop();
        foreach (var art in presentationArt.Values) art.Dispose();
    }

    private void CapturePresentation(string name)
    {
        var path = Path.GetFullPath("docs/images/presentation-" + name + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height), new Vector(96, 96));
        bitmap.Render(this);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    private async Task ReviewPresentation()
    {
        ShowPage("Tentang");
        await Task.Delay(900);
        if (creditsScroll.Offset.Y <= 0) throw new InvalidOperationException("Credits did not scroll.");
        CapturePresentation("credits");
        SetCreditsPaused(true);
        var offset = creditsScroll.Offset.Y;
        await Task.Delay(200);
        if (creditsScroll.Offset.Y != offset) throw new InvalidOperationException("Credits pause failed.");
        settings.ReducedMotion = true; ShowCredits();
        if (!creditsPaused) throw new InvalidOperationException("Reduced-motion credits must start paused.");
        settings.ReducedMotion = false;
        ShowPage("Ekspedisi");
        vm.Game.AbortFishing("Presentation review");
        vm.Game.Player.Experience = 1350; vm.Game.Player.Coins = 100000;
        foreach (var region in new[] { "ampat", "kalimantan", "kristal" })
        {
            if (!vm.Game.Travel(region)) throw new InvalidOperationException("Review travel failed.");
            await PresentWorldAsync();
            await Task.Delay(600);
            if (loadingLayer.IsVisible || rendererFailed) throw new InvalidOperationException("Loading overlay did not finish: " + region);
        }
        Console.WriteLine("PRESENTATION_OK: launch art, three regional loading illustrations, scrolling/pause/reduced-motion credits.");
    }
}
