using Avalonia.Media;
using NusantaraFishing.Core;

namespace NusantaraFishing.Desktop;

public sealed partial class MainWindow
{
    private void RefreshDiveHud()
    {
        var game = vm.Game;
        var underwater = game.Player.InWater;
        tensionCaption.IsVisible = tension.IsVisible = !underwater;
        progressCaption.Text = Localization.T(!underwater ? "JARAK TANGKAPAN" :
            game.Player.CatchingMethod == CatchingMethod.Snorkel ? "FOKUS DOKUMENTASI" : "KESIAPAN TOMBAK");
        diveReticle.IsVisible = underwater;
        if (!underwater) return;
        var maxRange = game.Player.CatchingMethod == CatchingMethod.Snorkel ? 4 : 6;
        diveReticle.Foreground = game.Busy && game.DiveAligned && game.DiveClear && game.DiveRange <= maxRange ? Brush.Parse("#65C5BA") : Brushes.White;
        fishName.Text = game.Hooked?.Name ?? Localization.T("Cari kehidupan bawah air");
        directionText.Text = game.Busy
            ? Localization.T($"Jarak target: {game.DiveRange:N1} m • Batas bidik: {maxRange} m")
            : Localization.T("Cari target, lalu berenang mendekat. Satwa konservasi hanya didokumentasikan.");
        cast.IsEnabled = game.Dive?.Stage != DiveStage.Projectile;
        cast.Content = Localization.T(!game.Busy ? "Cari target bawah air" : game.Player.CatchingMethod == CatchingMethod.Snorkel
            ? "Tahan untuk dokumentasi" : "Tahan bidik / lepas lempar");
        movementHint.Text = Localization.T($"{settings.Keys["forward"]} / {settings.Keys["back"]} Berenang maju / mundur • {settings.Keys["left"]} / {settings.Keys["right"]} Geser / putar saat membidik");
        actionHint.Text = Localization.T($"{settings.Keys["cast"]} Cari / tahan bidik • Lepas untuk lempar • {settings.Keys["board"]} Kembali ke perahu");
    }
}
