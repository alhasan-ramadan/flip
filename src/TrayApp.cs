using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Flip;

/// <summary>
/// Ein Taskleisten-Symbol: zeigt, ob gerade Lautsprecher oder Kopfhörer spielt. Linksklick wechselt,
/// das Menü wählt das Klangprofil für den Kopfhörer und legt die beiden Geräte fest.
/// </summary>
sealed class TrayApp : ApplicationContext
{
    readonly Settings settings = Settings.Load();
    readonly ContextMenuStrip menu = new();
    readonly NotifyIcon icon;
    string? renderedKey;

    public TrayApp()
    {
        if (settings.FillMissingDevices())
        {
            settings.Save();
            Log.Write($"Geräte: Lautsprecher = {settings.Speaker?.Name ?? "-"}, Kopfhörer = {settings.Headphones?.Name ?? "-"}");
        }
        if (!settings.FirstRunDone)
        {
            Autostart.Set(true);
            settings.FirstRunDone = true;
            settings.Save();
        }

        menu.Opening += (_, _) => BuildMenu();
        icon = new NotifyIcon { ContextMenuStrip = menu };
        icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                Show(Switcher.Toggle(settings));
        };

        // Windows meldet jeden Wechsel, auch aus den Soundeinstellungen oder wenn sich eine Bluetooth-Box verbindet.
        var ui = SynchronizationContext.Current;
        Audio.Changed += () => ui?.Post(_ => UpdateIcon(), null);
        try
        {
            Audio.Watch();
        }
        catch (COMException ex)
        {
            Log.Write($"Gerätewechsel werden nicht gemeldet: {ex.Message}");
        }
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        UpdateIcon();
        icon.Visible = true;
    }

    void Show(SwitchResult result)
    {
        if (result.Problem is not null)
            icon.ShowBalloonTip(5000, "FLIP", result.Problem, ToolTipIcon.Warning);
        UpdateIcon();
    }

    void UpdateIcon(bool force = false)
    {
        string? current = null;
        try
        {
            current = Audio.DefaultOutput();
        }
        catch (COMException ex)
        {
            Log.Write($"Standardgerät nicht lesbar: {ex.Message}");
        }
        var kind = current is null ? OutputKind.Other
            : current == settings.Headphones?.Id ? OutputKind.Headphones
            : current == settings.Speaker?.Id ? OutputKind.Speaker
            : OutputKind.Other;

        string key = kind.ToString();
        if (force || key != renderedKey)
        {
            var previous = icon.Icon;
            icon.Icon = IconRenderer.Render(kind);
            previous?.Dispose();
            renderedKey = key;
        }
        icon.Text = Truncate(Describe(kind, current), 127);
    }

    string Describe(OutputKind kind, string? current)
    {
        switch (kind)
        {
            case OutputKind.Headphones:
                string sound = EqProfiles.IsSetUp ? $"\nKlang: {EqProfiles.Active() ?? "aus"}" : "";
                return $"FLIP: Kopfhörer\n{settings.Headphones!.Name}{sound}\nKlick: zum Lautsprecher";
            case OutputKind.Speaker:
                return $"FLIP: Lautsprecher\n{settings.Speaker!.Name}\nKlick: zum Kopfhörer";
            default:
                string name = current is null ? "kein Ausgabegerät" : Audio.NameOf(current);
                return $"FLIP: {name}\nKlick: zum Kopfhörer";
        }
    }

    void BuildMenu()
    {
        foreach (var item in menu.Items.Cast<ToolStripItem>().ToList())
            item.Dispose();
        menu.Items.Clear();

        List<AudioDevice> outputs = [];
        string? current = null;
        try
        {
            outputs = Audio.ActiveOutputs();
            current = Audio.DefaultOutput();
        }
        catch (COMException ex)
        {
            Log.Write($"Geräte nicht lesbar: {ex.Message}");
        }

        AddOutput("Lautsprecher", settings.Speaker);
        AddOutput("Kopfhörer", settings.Headphones);

        if (EqProfiles.IsSetUp)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Klang am Kopfhörer") { Enabled = false });
            string? active = EqProfiles.Active();
            foreach (var profile in EqProfiles.List())
                AddProfile(profile, active);
            AddProfile(null, active);
        }

        menu.Items.Add(new ToolStripSeparator());
        var choose = new ToolStripMenuItem("Geräte festlegen");
        choose.DropDownItems.Add(ChooseMenu("Lautsprecher", outputs, settings.Speaker, d => settings.Speaker = d));
        choose.DropDownItems.Add(ChooseMenu("Kopfhörer", outputs, settings.Headphones, d => settings.Headphones = d));
        menu.Items.Add(choose);
        var autostart = new ToolStripMenuItem("Mit Windows starten") { Checked = Autostart.IsEnabled };
        autostart.Click += (_, _) => Autostart.Set(!Autostart.IsEnabled);
        menu.Items.Add(autostart);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => ExitThread());

        void AddOutput(string label, AudioDevice? device)
        {
            if (device is null)
            {
                menu.Items.Add(new ToolStripMenuItem($"{label}: nicht festgelegt") { Enabled = false });
                return;
            }
            bool connected = outputs.Any(d => d.Id == device.Id);
            var item = new ToolStripMenuItem($"{label}: {device.Name}" + (connected ? "" : " (nicht angeschlossen)"))
            {
                Checked = device.Id == current,
                Enabled = connected,
            };
            item.Click += (_, _) => Show(Switcher.SwitchTo(device));
            menu.Items.Add(item);
        }

        void AddProfile(string? profile, string? active)
        {
            var item = new ToolStripMenuItem(profile ?? "Aus")
            {
                Checked = string.Equals(profile, active, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += (_, _) => SetProfile(profile);
            menu.Items.Add(item);
        }
    }

    ToolStripMenuItem ChooseMenu(string label, List<AudioDevice> outputs, AudioDevice? chosen, Action<AudioDevice> set)
    {
        var item = new ToolStripMenuItem(label);
        foreach (var device in outputs.OrderBy(d => d.Name))
        {
            var entry = new ToolStripMenuItem(device.Name) { Checked = device.Id == chosen?.Id };
            entry.Click += (_, _) =>
            {
                set(device);
                settings.Save();
                Log.Write($"{label}: {device.Name}");
                UpdateIcon();
            };
            item.DropDownItems.Add(entry);
        }
        if (outputs.Count == 0)
            item.DropDownItems.Add(new ToolStripMenuItem("Kein aktives Gerät") { Enabled = false });
        return item;
    }

    void SetProfile(string? profile)
    {
        try
        {
            EqProfiles.SetActive(profile);
            Log.Write($"Klang: {profile ?? "aus"}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Klang nicht änderbar: {ex.Message}");
            icon.ShowBalloonTip(5000, "FLIP", $"Klangprofil nicht änderbar: {ex.Message}", ToolTipIcon.Warning);
        }
        UpdateIcon();
    }

    void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Hell/Dunkel der Taskleiste geändert: Symbol neu zeichnen.
        if (e.Category == UserPreferenceCategory.General)
            UpdateIcon(force: true);
    }

    static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    protected override void ExitThreadCore()
    {
        Audio.StopWatching();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        icon.Visible = false;
        icon.Icon?.Dispose();
        icon.Dispose();
        Log.Write("Beendet");
        base.ExitThreadCore();
    }
}
