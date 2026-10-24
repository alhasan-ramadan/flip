using System.Runtime.InteropServices;

namespace Flip;

/// <summary>Ein Wiedergabegerät, benannt wie in den Soundeinstellungen.</summary>
sealed record AudioDevice(string Id, string Name);

/// <summary>Wiedergabegeräte auflisten, das Standardgerät lesen und setzen, Wechsel melden (Windows Core Audio).</summary>
static class Audio
{
    const int Render = 0, StateActive = 1;
    public const int RoleConsole = 0, RoleCommunications = 2;
    // Konsole (Spiele, System), Multimedia (Musik, Video), Kommunikation (Discord, Teams): alle drei mitnehmen,
    // damit Programme auf „Standard“ dem Wechsel folgen.
    static readonly int[] AllRoles = [0, 1, 2];
    static readonly PropertyKey FriendlyName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    static readonly PropertyKey FormFactor = new(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"), 0);
    const int DigitalAudioDisplayDevice = 9;

    /// <summary>Feuert auf einem Hintergrund-Thread, wenn sich Standardgerät oder Geräteliste ändert.</summary>
    public static event Action? Changed;
    static IMMDeviceEnumerator? watched;
    static NotificationClient? client;

    static IMMDeviceEnumerator Enumerator() => (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();

    public static List<AudioDevice> ActiveOutputs()
    {
        Check(Enumerator().EnumAudioEndpoints(Render, StateActive, out var collection));
        Check(collection.GetCount(out int count));
        var list = new List<AudioDevice>(count);
        for (int i = 0; i < count; i++)
        {
            Check(collection.Item(i, out var device));
            list.Add(Describe(device));
        }
        return list;
    }

    /// <summary>ID des Standardgeräts für eine Rolle, null wenn es keins gibt.</summary>
    public static string? DefaultOutput(int role = RoleConsole)
    {
        if (Enumerator().GetDefaultAudioEndpoint(Render, role, out var device) != 0)
            return null;
        Check(device.GetId(out string id));
        return id;
    }

    public static bool IsActive(string id) =>
        Enumerator().GetDevice(id, out var device) == 0 && device.GetState(out int state) == 0 && state == StateActive;

    public static string NameOf(string id) =>
        Enumerator().GetDevice(id, out var device) == 0 ? Describe(device).Name : id;

    /// <summary>Ton über HDMI oder DisplayPort, also ein Monitor oder Fernseher.</summary>
    public static bool IsDisplayAudio(string id)
    {
        if (Enumerator().GetDevice(id, out var device) != 0 || device.OpenPropertyStore(0, out var store) != 0)
            return false;
        var key = FormFactor;
        if (store.GetValue(ref key, out var value) != 0)
            return false;
        const ushort VT_UI4 = 19;
        bool display = value.Type == VT_UI4 && value.Number == DigitalAudioDisplayDevice;
        PropVariantClear(ref value);
        return display;
    }

    public static void SetDefaultOutput(string id)
    {
        var policy = (IPolicyConfig)new PolicyConfigClass();
        foreach (int role in AllRoles)
            Check(policy.SetDefaultEndpoint(id, role));
    }

    public static void Watch()
    {
        watched = Enumerator();
        client = new NotificationClient();
        Check(watched.RegisterEndpointNotificationCallback(client));
    }

    public static void StopWatching()
    {
        if (watched is not null && client is not null)
            watched.UnregisterEndpointNotificationCallback(client);
        watched = null;
        client = null;
    }

    static AudioDevice Describe(IMMDevice device)
    {
        Check(device.GetId(out string id));
        Check(device.OpenPropertyStore(0, out var store));
        var key = FriendlyName;
        Check(store.GetValue(ref key, out var value));
        try
        {
            const ushort VT_LPWSTR = 31;
            return new AudioDevice(id, value.Type == VT_LPWSTR ? Marshal.PtrToStringUni(value.Pointer) ?? id : id);
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    static void Check(int hr)
    {
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);
    }

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(ref PropVariant value);

    [ComVisible(true)]
    sealed class NotificationClient : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string id, int newState) => Changed?.Invoke();
        public void OnDeviceAdded(string id) => Changed?.Invoke();
        public void OnDeviceRemoved(string id) => Changed?.Invoke();
        public void OnDefaultDeviceChanged(int flow, int role, string? id)
        {
            if (flow == Render)
                Changed?.Invoke();
        }
        public void OnPropertyValueChanged(string id, PropertyKey key) { }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey(Guid formatId, int propertyId)
    {
        public Guid FormatId = formatId;
        public int PropertyId = propertyId;
    }

    // PROPVARIANT, wie es unter 64 Bit im Speicher liegt; gebraucht werden nur Text-Zeiger und Zahl.
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
        [FieldOffset(8)] public uint Number;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorClass { }

    // Nicht dokumentiert, aber seit Windows 7 der Weg, über den auch die Soundeinstellungen das Standardgerät setzen.
    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    class PolicyConfigClass { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int Item(int index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetAt(int index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }

    [ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMNotificationClient
    {
        void OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, int newState);
        void OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
        void OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
        void OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? id);
        void OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
    }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        // Die ersten zehn Methoden werden nicht gebraucht; sie stehen nur da, damit SetDefaultEndpoint an der richtigen Stelle liegt.
        [PreserveSig] int GetMixFormat(IntPtr a, IntPtr b);
        [PreserveSig] int GetDeviceFormat(IntPtr a, int b, IntPtr c);
        [PreserveSig] int ResetDeviceFormat(IntPtr a);
        [PreserveSig] int SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
        [PreserveSig] int GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetProcessingPeriod(IntPtr a, IntPtr b);
        [PreserveSig] int GetShareMode(IntPtr a, IntPtr b);
        [PreserveSig] int SetShareMode(IntPtr a, IntPtr b);
        [PreserveSig] int GetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
    }
}
