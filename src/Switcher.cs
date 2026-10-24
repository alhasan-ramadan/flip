using System.Runtime.InteropServices;

namespace Flip;

/// <summary>Ergebnis eines Wechsels: das neue Gerät oder warum es nicht ging.</summary>
sealed record SwitchResult(AudioDevice? Device, string? Problem);

static class Switcher
{
    /// <summary>Spielt gerade der Kopfhörer, geht es zum Lautsprecher, sonst (auch von einem dritten Gerät) zum Kopfhörer.</summary>
    public static SwitchResult Toggle(Settings settings)
    {
        if (settings.Speaker is null || settings.Headphones is null)
            return new(null, "Lautsprecher und Kopfhörer sind noch nicht festgelegt (Menü › Geräte festlegen).");
        try
        {
            var target = Audio.DefaultOutput() == settings.Headphones.Id ? settings.Speaker : settings.Headphones;
            return SwitchTo(target);
        }
        catch (COMException ex)
        {
            Log.Write($"Wechsel fehlgeschlagen: {ex.Message}");
            return new(null, $"Wechsel fehlgeschlagen: {ex.Message}");
        }
    }

    public static SwitchResult SwitchTo(AudioDevice target)
    {
        try
        {
            if (!Audio.IsActive(target.Id))
                return new(null, $"{target.Name} ist gerade nicht angeschlossen.");
            Audio.SetDefaultOutput(target.Id);
            Log.Write($"Ausgabe: {target.Name}");
            return new(target, null);
        }
        catch (COMException ex)
        {
            Log.Write($"Wechsel fehlgeschlagen: {ex.Message}");
            return new(null, $"Wechsel fehlgeschlagen: {ex.Message}");
        }
    }
}
