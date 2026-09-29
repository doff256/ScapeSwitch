using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HidSharp;
using WindowsMediaController;

// ── Console on demand ─────────────────────────────────────────────────────────

static class ConsoleHelper
{
    [DllImport("kernel32.dll")] static extern bool AllocConsole();
    [DllImport("kernel32.dll")] static extern bool FreeConsole();

    private static bool _visible = false;

    public static void Show()
    {
        if (_visible) return;
        AllocConsole();
        // Reopen stdout so Console.WriteLine actually works after AllocConsole
        var sw = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(sw);
        Console.Title = "FractalDockSwitch — Log";
        _visible = true;
        Log.Info("Console opened all log output will appear here");
    }

    public static void Hide()
    {
        if (!_visible) return;
        FreeConsole();
        _visible = false;
    }

    public static void Toggle() { if (_visible) Hide(); else Show(); }
    public static bool IsVisible => _visible;
}

// ── Logging ───────────────────────────────────────────────────────────────────

static class Log
{
    public static void Info(string msg)  => Write("INFO ", msg, ConsoleColor.Cyan);
    public static void Ok(string msg)    => Write("OK   ", msg, ConsoleColor.Green);
    public static void Warn(string msg)  => Write("WARN ", msg, ConsoleColor.Yellow);
    public static void Error(string msg) => Write("ERR  ", msg, ConsoleColor.Red);
    public static void Data(string msg)  => Write("DATA ", msg, ConsoleColor.Gray);

    private static void Write(string level, string msg, ConsoleColor color)
    {
        if (!ConsoleHelper.IsVisible) return;
        try
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
            Console.ForegroundColor = color;
            Console.Write(level);
            Console.ResetColor();
            Console.WriteLine(msg);
        }
        catch { }
    }
}

// ── Native Audio Device Switcher ──────────────────────────────────────────────

static class AudioSwitcher
{
    private static IMMDeviceEnumerator Enumerator() => (IMMDeviceEnumerator)Activator.CreateInstance(
        Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!)!;

    private static string NameOf(IMMDevice device)
    {
        device.OpenPropertyStore(0, out var store);
        var key = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
        store.GetValue(ref key, out var pv);
        return pv.GetString();
    }

    public static System.Collections.Generic.List<AudioDevice> GetOutputDevices()
    {
        var result = new System.Collections.Generic.List<AudioDevice>();
        try
        {
            var mmde = Enumerator();
            mmde.EnumAudioEndpoints(0, 1, out var col);
            col.GetCount(out int count);
            for (int i = 0; i < count; i++)
            {
                try
                {
                    col.Item(i, out var dev);
                    dev.GetId(out string id);
                    result.Add(new AudioDevice(id, NameOf(dev)));
                }
                catch (Exception ex) { Log.Warn($"Audio: skipped endpoint {i} ({ex.Message})"); }
            }
        }
        catch (Exception ex) { Log.Error($"Audio: enumeration failed ({ex.Message})"); }
        return result;
    }

    public static string GetDefaultOutput()
    {
        try
        {
            Enumerator().GetDefaultAudioEndpoint(0, 1, out var device);
            return NameOf(device);
        }
        catch { return "No default output"; }
    }

    public static bool SetDefault(string nameContains, string? preferredId = null)
    {
        Log.Info($"Audio: looking for \"{nameContains}\"");
        try
        {
            var endpoints = GetOutputDevices();
            var target = endpoints.FirstOrDefault(d => !string.IsNullOrEmpty(preferredId) && d.Id == preferredId)
                ?? endpoints.FirstOrDefault(d => d.Name.Equals(nameContains, StringComparison.OrdinalIgnoreCase))
                ?? endpoints.FirstOrDefault(d => !string.IsNullOrWhiteSpace(nameContains) && d.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                Log.Warn($"Audio: no match for \"{nameContains}\"");
                return false;
            }
            var policy = (IPolicyConfig)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9"))!)!;
            policy.SetDefaultEndpoint(target.Id, 0);
            policy.SetDefaultEndpoint(target.Id, 1);
            policy.SetDefaultEndpoint(target.Id, 2);
            Log.Ok($"Audio: switched to \"{target.Name}\"");
            return true;
        }
        catch (Exception ex) { Log.Error($"Audio: {ex.Message}"); return false; }
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(int dataFlow, int stateMask, [MarshalAs(UnmanagedType.Interface)] out IMMDeviceCollection devices);
        void GetDefaultAudioEndpoint(int dataFlow, int role, [MarshalAs(UnmanagedType.Interface)] out IMMDevice device);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.Interface)] out IMMDevice device);
        void _u1(); void _u2();
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        void GetCount(out int count);
        void Item(int index, [MarshalAs(UnmanagedType.Interface)] out IMMDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        void Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IntPtr ppv);
        void OpenPropertyStore(int stgmAccess, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetState(out int state);
    }
    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        void GetCount(out int count);
        void GetAt(int index, out PROPERTYKEY key);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        void Commit();
    }
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        void _u1(); void _u2(); void _u3(); void _u4(); void _u5();
        void _u6(); void _u7(); void _u8(); void _u9(); void _u10();
        void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string device, int role);
        void _u11();
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct PROPERTYKEY { public Guid fmtid; public int pid; }
    [StructLayout(LayoutKind.Explicit)]
    public struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr ptrVal;
        public string GetString() => vt == 31 ? Marshal.PtrToStringUni(ptrVal) ?? "" : "";
    }
}

// ── Config ────────────────────────────────────────────────────────────────────

class Config
{
    public string SpeakerDeviceName   { get; set; } = "Speakers/Headphones (Realtek(R) Audio)";
    public string HeadphoneDeviceName { get; set; } = "Headset Earphone (Fractal Scape Dongle)";
    public string? SpeakerDeviceId { get; set; }
    public string? HeadphoneDeviceId { get; set; }
    public bool   PauseOnSwitch       { get; set; } = true;
    public bool   SyncOnLaunch        { get; set; } = false;

    private static readonly string FilePath =
        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dockconfig.json");

    public static Config Load()
    {
        try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath)) ?? new Config(); }
        catch { }
        return new Config();
    }

    public void Save()
    {
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}

// ── Tray App ──────────────────────────────────────────────────────────────────

class TrayApp : ApplicationContext
{
    private const int  FractalVendorId  = 14012;
    private const int  FractalProductId = 1;
    private const uint TargetUsagePage  = 0xFF00;
    private const int  DebounceMs       = 600;
    private const int  RetryMs          = 2000;

    private static readonly byte[] PollCommand = { 0x11, 0x21 };

    private readonly NotifyIcon              _tray  = new();
    private readonly Control                 _dispatcher = new();
    private readonly StatusForm              _statusForm;
    private SettingsForm?                    _settingsForm;
    private          Config                  _cfg   = Config.Load();
    private readonly CancellationTokenSource _cts   = new();
    private readonly MediaManager            _media = new();

    private ToolStripMenuItem _statusItem  = new();
    private ToolStripMenuItem _stateItem   = new();
    private ToolStripMenuItem _consoleItem = new();

    private bool? _rawDocked   = null;
    private bool? _actedDocked = null;
    private readonly object _dockLock = new();
    private CancellationTokenSource? _debounceCts = null;
    private string _lastAction = "Ready";

    // Track previous icon to dispose it and avoid GDI handle leak
    private Icon? _currentIcon = null;
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);

    public TrayApp()
    {
        if (!File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dockconfig.json")))
            _cfg.Save();

        _ = _dispatcher.Handle; // Create a UI-thread handle before the HID worker starts.
        _statusForm = new StatusForm(ManualSwitch, OpenSettings);
        _ = Task.Run(async () => {
            try { await _media.StartAsync(); }
            catch (Exception ex) { Log.Warn($"Media: controller unavailable ({ex.Message})"); }
        });

        _tray.ContextMenuStrip = BuildMenu();
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) OpenStatus(); };
        SetTrayState(null, "Looking for the Scape receiver");
        _tray.Visible = true;

        new Thread(() => HidLoop(_cts.Token)) { IsBackground = true, Name = "HidMonitor" }.Start();
    }

    // Small status glyphs remain legible at the Windows tray's 16-pixel size.

    private Icon BuildIcon(bool? docked)
    {
        using var bmp = new System.Drawing.Bitmap(32, 32);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.Transparent);
        g.SmoothingMode    = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        using var background = new System.Drawing.SolidBrush(Palette.Background);
        var color = docked is null ? Palette.Muted : docked.Value ? Palette.Speakers : Palette.Headset;
        using var brush = new System.Drawing.SolidBrush(color);
        using var pen = new System.Drawing.Pen(color, 2.8f) {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        g.FillRectangle(background, 0, 0, 32, 32);
        if (docked == null)
        {
            g.DrawEllipse(pen, 10, 10, 12, 12);
            g.FillEllipse(brush, 14, 14, 4, 4);
        }
        else if (docked.Value)
        {
            // A filled loudspeaker and two short sound waves stay distinct at 16 px.
            g.FillRectangle(brush, 3, 13, 6, 7);
            g.FillPolygon(brush, new[] {
                new System.Drawing.Point(8, 13), new System.Drawing.Point(16, 7),
                new System.Drawing.Point(16, 25), new System.Drawing.Point(8, 20)
            });
            g.DrawArc(pen, 12, 9, 12, 14, -58, 116);
            g.DrawArc(pen, 13, 5, 17, 22, -58, 116);
        }
        else
        {
            // Wide headband and solid ear cups, with no letters or tiny detail.
            g.DrawArc(pen, 6, 5, 20, 20, 180, 180);
            g.FillRectangle(brush, 5, 16, 5, 9);
            g.FillRectangle(brush, 22, 16, 5, 9);
        }
        var handle = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    private ContextMenuStrip BuildMenu()
    {
        _statusItem      = new ToolStripMenuItem("Status: starting…") { Enabled = false };
        _statusItem.Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold);
        _stateItem       = new ToolStripMenuItem("Headphones: unknown") { Enabled = false };
        _consoleItem     = new ToolStripMenuItem("Show log console", null, (s, e) => ToggleConsole());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_stateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_consoleItem);
        menu.Items.Add("Open panel", null, (s, e) => OpenStatus());
        menu.Items.Add("Settings", null, (s, e) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit",     null, (s, e) => ExitApp());
        return menu;
    }

    private void ToggleConsole()
    {
        ConsoleHelper.Toggle();
        _consoleItem.Text = ConsoleHelper.IsVisible ? "Hide log console" : "Show log console";
    }

    private void SetTrayState(bool? docked, string status)
    {
        if (status.StartsWith("Status: ", StringComparison.Ordinal)) status = status[8..];
        RunOnUi(() =>
        {
            _statusItem.Text = "Status: " + status;
            _stateItem.Text = docked is null ? "Headset: unknown" : docked.Value ? "Headset: docked" : "Headset: in use";
            _tray.Text = docked is null ? "Scape Switch — searching" : docked.Value ? "Scape Switch — docked" : "Scape Switch — in use";
            var old = _currentIcon;
            _currentIcon = BuildIcon(docked);
            _tray.Icon = _currentIcon;
            old?.Dispose();
            _statusForm.SetState(docked, status, _lastAction);
        });
    }

    private void RunOnUi(Action action)
    {
        if (_cts.IsCancellationRequested || _dispatcher.IsDisposed) return;
        try
        {
            if (_dispatcher.InvokeRequired) _dispatcher.BeginInvoke(action);
            else action();
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    private void OpenStatus()
    {
        if (_statusForm.Visible) _statusForm.Hide();
        else _statusForm.ShowNear(Cursor.Position);
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false }) { _settingsForm.Activate(); return; }
        _settingsForm = new SettingsForm(_cfg, () => _cfg = Config.Load());
        _settingsForm.Show();
    }

    private void ManualSwitch(bool docked)
    {
        var cfg = _cfg;
        var success = AudioSwitcher.SetDefault(
            docked ? cfg.SpeakerDeviceName : cfg.HeadphoneDeviceName,
            docked ? cfg.SpeakerDeviceId : cfg.HeadphoneDeviceId);
        _lastAction = success ? $"Manually switched to {(docked ? "speakers" : "headset")}" : "Output unavailable — check settings";
        _statusForm.SetState(_actedDocked, (_statusItem.Text ?? "").Replace("Status: ", ""), _lastAction);
    }

    private void ExitApp()
    {
        _cts.Cancel();
        _debounceCts?.Cancel();
        _tray.Visible = false;
        _tray.Dispose();
        _currentIcon?.Dispose();
        _statusForm.Dispose();
        _settingsForm?.Close();
        _dispatcher.Dispose();
        ConsoleHelper.Hide();
        ExitThread();
    }

    // ── HID loop ─────────────────────────────────────────────────────────────

    private void HidLoop(CancellationToken ct)
    {
        HidDevice? device = null;
        HidStream? stream = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (stream == null)
                {
                    SetTrayState(null, "Status: searching…");
                    var all = DeviceList.Local.GetHidDevices(FractalVendorId, FractalProductId).ToList();

                    device = all.FirstOrDefault(d =>
                    {
                        try { return d.GetReportDescriptor().DeviceItems.Any(item =>
                                item.Usages.GetAllValues().Any(u => (u >> 16) == TargetUsagePage)); }
                        catch { return false; }
                    }) ?? all.OrderByDescending(d => d.GetMaxInputReportLength()).FirstOrDefault();

                    if (device == null)
                    {
                        Log.Warn("HID: dongle not found — retrying…");
                        SetTrayState(null, "Status: dongle not found");
                        Thread.Sleep(RetryMs);
                        continue;
                    }

                    if (!device.TryOpen(out stream))
                    {
                        Log.Warn("HID: dongle busy — retrying…");
                        SetTrayState(null, "Status: dongle busy");
                        device = null;
                        Thread.Sleep(RetryMs);
                        continue;
                    }

                    stream.ReadTimeout = 2000;

                    try
                    {
                        var outputBuf = new byte[device.GetMaxOutputReportLength()];
                        outputBuf[0] = 2;
                        outputBuf[1] = PollCommand[0];
                        outputBuf[2] = PollCommand[1];
                        stream.Write(outputBuf);
                        Log.Ok("HID: stream opened, wake command sent — listening…");
                        SetTrayState(_actedDocked, "Status: monitoring");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"HID: wake command failed ({ex.Message})");
                    }
                }

                var buf = new byte[Math.Max(device!.GetMaxInputReportLength(), 64)];
                stream.Read(buf);

                if (buf[0] != 2 || buf[1] != 0x11) continue;

                bool isDocked = buf[4] == 0;
                Log.Data($"Heartbeat: docked={isDocked}  raw=[{string.Join(",", buf.Take(6))}]");

                lock (_dockLock)
                {
                    if (_rawDocked == null)
                    {
                        _rawDocked   = isDocked;
                        _actedDocked = isDocked;
                        Log.Info($"Initial state: {(isDocked ? "DOCKED" : "ACTIVE")}");
                        SetTrayState(isDocked, "Status: monitoring");
                        if (_cfg.SyncOnLaunch)
                        {
                            var cfg = _cfg;
                            var synced = AudioSwitcher.SetDefault(
                                isDocked ? cfg.SpeakerDeviceName : cfg.HeadphoneDeviceName,
                                isDocked ? cfg.SpeakerDeviceId : cfg.HeadphoneDeviceId);
                            _lastAction = synced ? "Output synced on launch" : "Could not sync output — check settings";
                            SetTrayState(isDocked, "Status: monitoring");
                        }
                        continue;
                    }

                    if (isDocked == _rawDocked) continue;
                    _rawDocked = isDocked;
                }

                ScheduleDockAction(isDocked);
            }
            catch (TimeoutException)
            {
                try
                {
                    if (stream != null && device != null)
                    {
                        var outputBuf = new byte[device.GetMaxOutputReportLength()];
                        outputBuf[0] = 2;
                        outputBuf[1] = PollCommand[0];
                        outputBuf[2] = PollCommand[1];
                        stream.Write(outputBuf);
                        Log.Data("HID: keepalive sent");
                    }
                }
                catch
                {
                    stream?.Close();
                    stream = null;
                    device = null;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"HID: stream lost ({ex.GetType().Name}) — reconnecting…");
                SetTrayState(null, "Status: reconnecting…");
                stream?.Close();
                stream = null;
                device = null;
                Thread.Sleep(RetryMs);
            }
        }

        stream?.Close();
        Log.Info("HID loop exited");
    }

    private void ScheduleDockAction(bool isDocked)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceMs, token);

                bool shouldAct;
                lock (_dockLock)
                {
                    shouldAct = _actedDocked != isDocked;
                    if (shouldAct) _actedDocked = isDocked;
                }

                if (!shouldAct) return;

                Log.Ok($"Confirmed: {(isDocked ? "DOCKED → speakers" : "UNDOCKED → headphones")}");
                await OnDockChanged(isDocked);
                SetTrayState(isDocked, "Status: monitoring");
            }
            catch (TaskCanceledException) { }
            catch (Exception ex) { Log.Error($"Dock action: {ex.Message}"); }
        }, CancellationToken.None);
    }

    private async Task OnDockChanged(bool isDocked)
    {
        if (_cfg.PauseOnSwitch)
        {
            try
            {
                var session = _media.GetFocusedSession();
                if (session != null) { await session.ControlSession.TryPauseAsync(); Log.Ok("Media paused"); }
                else Log.Warn("Media: no active session");
            }
            catch (Exception ex) { Log.Error($"Media: {ex.Message}"); }
        }

        var cfg = _cfg;
        string target = isDocked ? cfg.SpeakerDeviceName : cfg.HeadphoneDeviceName;
        var switched = AudioSwitcher.SetDefault(target, isDocked ? cfg.SpeakerDeviceId : cfg.HeadphoneDeviceId);
        _lastAction = switched
            ? $"Switched to {(isDocked ? "speakers" : "headset")} · {DateTime.Now:HH:mm}"
            : "Output unavailable — check settings";
    }
}

// ── Entry Point ───────────────────────────────────────────────────────────────

static class Program
{
    [STAThread]
    static void Main()
    {
        using var singleInstance = new Mutex(true, @"Local\ScapeSwitch.TrayApp", out var firstInstance);
        if (!firstInstance) return;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApp());
    }
}
