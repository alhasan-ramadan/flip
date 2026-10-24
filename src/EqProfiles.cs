using Microsoft.Win32;

namespace Flip;

/// <summary>
/// Klangprofile für Equalizer APO. Jede .txt-Datei in config\flip ist ein Profil. FLIP schreibt nur
/// config\flip-aktiv.txt, die das gewählte Profil einbindet; für welches Gerät das gilt, legt die
/// config.txt fest (siehe README).
/// </summary>
static class EqProfiles
{
    const string ProfileFolder = "flip";
    const string ActiveFile = "flip-aktiv.txt";

    /// <summary>Konfigurationsordner von Equalizer APO, null wenn es nicht installiert ist.</summary>
    static string? ConfigFolder()
    {
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(@"SOFTWARE\EqualizerAPO");
        string folder = key?.GetValue("ConfigPath") as string
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EqualizerAPO", "config");
        return Directory.Exists(folder) ? folder : null;
    }

    public static bool IsSetUp => ConfigFolder() is { } folder && Directory.Exists(Path.Combine(folder, ProfileFolder));

    public static List<string> List()
    {
        if (ConfigFolder() is not { } folder || !Directory.Exists(Path.Combine(folder, ProfileFolder)))
            return [];
        return Directory.GetFiles(Path.Combine(folder, ProfileFolder), "*.txt")
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Name des eingebundenen Profils, null = aus.</summary>
    public static string? Active()
    {
        if (ConfigFolder() is not { } folder || !File.Exists(Path.Combine(folder, ActiveFile)))
            return null;
        foreach (var line in File.ReadLines(Path.Combine(folder, ActiveFile)))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Include:", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(trimmed["Include:".Length..].Trim());
        }
        return null;
    }

    /// <summary>Bindet ein Profil ein (null = aus). Equalizer APO merkt die Änderung und lädt sofort neu.</summary>
    public static void SetActive(string? profile)
    {
        string folder = ConfigFolder() ?? throw new IOException("Equalizer APO ist nicht installiert.");
        string content = profile is null
            ? "# Von FLIP geschrieben: Klangprofil aus.\n"
            : $"# Von FLIP geschrieben: aktives Klangprofil.\nInclude: {ProfileFolder}\\{profile}.txt\n";
        File.WriteAllText(Path.Combine(folder, ActiveFile), content);
    }
}
