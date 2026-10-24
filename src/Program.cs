using System.Runtime.InteropServices;
using System.Text;

namespace Flip;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0)
            return RunCommand(args);

        using var mutex = new Mutex(true, @"Local\Flip", out bool firstInstance);
        if (!firstInstance)
            return 0;

        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Log.Write($"Fehler: {e.Exception}");
        Log.Write($"Start {Environment.ProcessPath}");
        Application.Run(new TrayApp());
        return 0;
    }

    /// <summary>Für Verknüpfungen und Tastenkürzel: --toggle, --profile Name|off, --dump.</summary>
    static int RunCommand(string[] args)
    {
        using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
        var settings = Settings.Load();
        settings.FillMissingDevices();
        try
        {
            switch (args[0])
            {
                case "--toggle":
                    var result = Switcher.Toggle(settings);
                    output.WriteLine(result.Problem ?? $"Jetzt: {result.Device!.Name}");
                    return result.Problem is null ? 0 : 1;

                case "--profile" when args.Length > 1:
                    string? profile = args[1].Equals("off", StringComparison.OrdinalIgnoreCase) ? null
                        : EqProfiles.List().FirstOrDefault(p => p.Equals(args[1], StringComparison.OrdinalIgnoreCase));
                    if (profile is null && !args[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                    {
                        output.WriteLine($"Kein Profil „{args[1]}“. Vorhanden: {string.Join(", ", EqProfiles.List())}");
                        return 1;
                    }
                    EqProfiles.SetActive(profile);
                    Log.Write($"Klang: {profile ?? "aus"}");
                    output.WriteLine($"Klang: {profile ?? "aus"}");
                    return 0;

                case "--dump":
                    Dump(output, settings);
                    return 0;
                default:
                    output.WriteLine("Aufruf: Flip.exe [--toggle | --profile <Name|off> | --dump]");
                    return 2;
            }
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"Fehler: {ex.Message}");
            return 1;
        }
    }

    static void Dump(StreamWriter output, Settings settings)
    {
        string? current = Audio.DefaultOutput(), communication = Audio.DefaultOutput(Audio.RoleCommunications);
        foreach (var device in Audio.ActiveOutputs())
        {
            var marks = new List<string>();
            if (device.Id == current) marks.Add("Standard");
            if (device.Id == communication) marks.Add("Kommunikation");
            if (device.Id == settings.Speaker?.Id) marks.Add("FLIP-Lautsprecher");
            if (device.Id == settings.Headphones?.Id) marks.Add("FLIP-Kopfhörer");
            output.WriteLine($"{device.Name} [{string.Join(", ", marks)}] {device.Id}");
        }
        output.WriteLine(EqProfiles.IsSetUp
            ? $"Klangprofile: {string.Join(", ", EqProfiles.List())}; aktiv: {EqProfiles.Active() ?? "aus"}"
            : "Klangprofile: Equalizer APO oder Ordner config\\flip fehlt");
    }
}
