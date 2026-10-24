using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;

namespace Flip;

/// <summary>Einstellungen in %LOCALAPPDATA%\Flip\settings.json.</summary>
sealed class Settings
{
    public static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flip");
    static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public AudioDevice? Speaker { get; set; }
    public AudioDevice? Headphones { get; set; }

    /// <summary>Beim ersten Start wird der Autostart einmal eingeschaltet.</summary>
    public bool FirstRunDone { get; set; }

    public static Settings Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new() : new();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            Log.Write($"Einstellungen nicht lesbar: {ex.Message}");
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Ergänzt fehlende Geräte: Kopfhörer = das Gerät für Kommunikation (Discord, Teams), Lautsprecher =
    /// das Standardgerät, falls es ein anderes ist, sonst das nächste. Monitor-Ton über HDMI kommt nur
    /// in Frage, wenn es nichts anderes gibt. Im Menü änderbar.
    /// </summary>
    public bool FillMissingDevices()
    {
        if (Speaker is not null && Headphones is not null)
            return false;
        try
        {
            var outputs = Audio.ActiveOutputs();
            var candidates = outputs.Where(d => !Audio.IsDisplayAudio(d.Id)).ToList();
            if (candidates.Count == 0)
                candidates = outputs;
            AudioDevice? Find(string? id) => candidates.FirstOrDefault(d => d.Id == id);
            var (speaker, headphones) = (Speaker, Headphones);

            if (Headphones is null)
            {
                var communication = Find(Audio.DefaultOutput(Audio.RoleCommunications));
                Headphones = communication is not null && communication.Id != Speaker?.Id
                    ? communication
                    : candidates.FirstOrDefault(d => d.Id != Speaker?.Id);
            }
            if (Speaker is null)
            {
                var console = Find(Audio.DefaultOutput());
                Speaker = console is not null && console.Id != Headphones?.Id
                    ? console
                    : candidates.FirstOrDefault(d => d.Id != Headphones?.Id);
            }
            return Speaker != speaker || Headphones != headphones;
        }
        catch (COMException ex)
        {
            Log.Write($"Geräte nicht lesbar: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Autostart als Aufgabe „Flip“ in der Aufgabenplanung: bei der Anmeldung, nur für den eigenen
/// Benutzer, ohne Admin-Rechte. Gleicher Weg wie bei JUICE, weil Windows 11 Build 26300 den
/// Run-Schlüssel beim Anmelden übersprungen hat.
/// </summary>
static class Autostart
{
    const string TaskName = "Flip";
    const int CreateOrUpdate = 6, InteractiveToken = 3;

    static string ExePath => Environment.ProcessPath!;

    public static bool IsEnabled
    {
        get
        {
            try
            {
                dynamic task = TaskFolder().GetTask(TaskName);
                return (bool)task.Enabled && ((string)task.Xml).Contains(SecurityElement.Escape(ExePath));
            }
            catch (Exception ex) when (ex is FileNotFoundException or COMException)
            {
                return false;
            }
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            if (enabled)
                TaskFolder().RegisterTask(TaskName, TaskXml(), CreateOrUpdate, null, null, InteractiveToken, null);
            else
                TaskFolder().DeleteTask(TaskName, 0);
            Log.Write($"Autostart {(enabled ? "an" : "aus")}: {ExePath}");
        }
        catch (Exception ex) when (ex is FileNotFoundException or COMException)
        {
            Log.Write($"Autostart {(enabled ? "an" : "aus")} fehlgeschlagen: {ex.Message}");
        }
    }

    static dynamic TaskFolder()
    {
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", throwOnError: true)!)!;
        service.Connect();
        return service.GetFolder(@"\");
    }

    // 10 s Verzögerung, damit die Taskleiste steht, bevor das Symbol kommt. Priorität 7 ist der
    // Standard der Aufgabenplanung (unter normal), für ein Tool im Hintergrund passend.
    static string TaskXml()
    {
        string user = SecurityElement.Escape($@"{Environment.UserDomainName}\{Environment.UserName}");
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>FLIP: Umschalter zwischen Lautsprecher und Kopfhörer in der Taskleiste.</Description></RegistrationInfo>
              <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId><Delay>PT10S</Delay></LogonTrigger></Triggers>
              <Principals><Principal id="Author"><UserId>{user}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author"><Exec><Command>{SecurityElement.Escape(ExePath)}</Command></Exec></Actions>
            </Task>
            """;
    }
}

static class Log
{
    static readonly string FilePath = Path.Combine(Settings.Folder, "flip.log");
    const long MaxBytes = 256 * 1024;

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Settings.Folder);
            var info = new FileInfo(FilePath);
            if (info.Exists && info.Length > MaxBytes)
            {
                var lines = File.ReadAllLines(FilePath);
                File.WriteAllLines(FilePath, lines[(lines.Length / 2)..]);
            }
            File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Protokoll ist nur zur Fehlersuche da.
        }
    }
}
