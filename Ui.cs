using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

record AudioDevice(string Id, string Name)
{
    public override string ToString() => Name;
}

static class Palette
{
    public static readonly Color Background = Color.FromArgb(16, 18, 22);
    public static readonly Color Panel = Color.FromArgb(27, 30, 35);
    public static readonly Color Line = Color.FromArgb(54, 59, 65);
    public static readonly Color Text = Color.FromArgb(239, 241, 237);
    public static readonly Color Muted = Color.FromArgb(137, 144, 153);
    public static readonly Color Headset = Color.FromArgb(216, 243, 107);
    public static readonly Color Speakers = Color.FromArgb(245, 169, 110);
    public static readonly Color Accent = Headset;

    public static Label Label(string text, int x, int y, int w, int h, float size = 9,
        bool bold = false, Color? color = null)
    {
        return new Label {
            Text = text, Location = new Point(x, y), Size = new Size(w, h),
            ForeColor = color ?? Text, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
        };
    }

    public static Button Button(string text, int x, int y, int w, int h, bool accent = false)
    {
        var button = new Button {
            Text = text, Location = new Point(x, y), Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat, BackColor = accent ? Accent : Panel,
            ForeColor = accent ? Background : Text, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold), TabStop = true
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = accent ? Accent : Line;
        button.FlatAppearance.MouseOverBackColor = accent ? Color.FromArgb(232, 255, 137) : Color.FromArgb(44, 48, 53);
        return button;
    }
}

sealed class StatusForm : Form
{
    private readonly Label _state;
    private readonly Label _detail;
    private readonly Label _output;
    private readonly Label _last;
    private readonly Panel _indicator;
    private readonly Panel _modeBar;
    private readonly Button _speakers;
    private readonly Button _headset;

    public StatusForm(Action<bool> manualSwitch, Action openSettings)
    {
        Text = "Scape Switch";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(378, 351);
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Hide(); };
        Deactivate += (_, _) => Hide();
        _modeBar = new Panel { Location = new Point(0, 0), Size = new Size(3, 351), BackColor = Palette.Muted };
        Controls.Add(_modeBar);

        Controls.Add(Palette.Label("SCAPE SWITCH", 21, 16, 250, 25, 14, true));
        Controls.Add(Palette.Label("DOCK  /  AUDIO ROUTING", 22, 41, 250, 17, 7.5f, true, Palette.Muted));
        var settings = Palette.Button("SETTINGS", 275, 18, 81, 32);
        settings.Click += (_, _) => { Hide(); openSettings(); };
        Controls.Add(settings);
        Controls.Add(Line(70));

        Controls.Add(Palette.Label("HEADSET", 22, 83, 140, 17, 8, true, Palette.Muted));
        _indicator = new Panel { Location = new Point(23, 114), Size = new Size(10, 10), BackColor = Palette.Muted };
        Controls.Add(_indicator);
        _state = Palette.Label("Waiting for dongle", 42, 103, 300, 35, 19, true);
        Controls.Add(_state);
        _detail = Palette.Label("Looking for the Scape receiver", 22, 144, 330, 25, 9, false, Palette.Muted);
        Controls.Add(_detail);

        Controls.Add(Line(184));
        Controls.Add(Palette.Label("WINDOWS OUTPUT", 22, 196, 200, 18, 8, true, Palette.Muted));
        _output = Palette.Label("—", 22, 216, 334, 28, 10, true);
        Controls.Add(_output);

        _speakers = Palette.Button("SPEAKERS", 22, 260, 162, 42);
        _headset = Palette.Button("HEADSET", 194, 260, 162, 42);
        _speakers.Click += (_, _) => manualSwitch(true);
        _headset.Click += (_, _) => manualSwitch(false);
        Controls.Add(_speakers);
        Controls.Add(_headset);
        _last = Palette.Label("Ready", 22, 316, 334, 18, 8, false, Palette.Muted);
        Controls.Add(_last);
    }

    private static Panel Line(int y) => new() { Location = new Point(22, y), Size = new Size(334, 1), BackColor = Palette.Line };

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Palette.Line);
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    public void SetState(bool? docked, string status, string lastAction)
    {
        _state.Text = docked is null ? "Searching" : docked.Value ? "Docked" : "In use";
        _detail.Text = status;
        var modeColor = docked is null ? Palette.Muted : docked.Value ? Palette.Speakers : Palette.Headset;
        _indicator.BackColor = modeColor;
        _modeBar.BackColor = modeColor;
        _state.ForeColor = modeColor;
        _last.Text = lastAction;
        SetActiveButton(_speakers, docked == true, Palette.Speakers);
        SetActiveButton(_headset, docked == false, Palette.Headset);
        UpdateOutput();
    }

    private static void SetActiveButton(Button button, bool active, Color color)
    {
        button.BackColor = active ? color : Palette.Panel;
        button.ForeColor = active ? Palette.Background : Palette.Text;
        button.FlatAppearance.BorderColor = active ? color : Palette.Line;
    }

    public void UpdateOutput() => _output.Text = AudioSwitcher.GetDefaultOutput();

    public void ShowNear(Point anchor)
    {
        UpdateOutput();
        var area = Screen.FromPoint(anchor).WorkingArea;
        Location = new Point(
            Math.Clamp(anchor.X - Width / 2, area.Left + 8, area.Right - Width - 8),
            anchor.Y > area.Top + area.Height / 2 ? area.Bottom - Height - 8 : area.Top + 8);
        Show();
        Activate();
    }
}

sealed class SettingsForm : Form
{
    private readonly Config _cfg;
    private readonly Action _onSave;
    private readonly ComboBox _speaker;
    private readonly ComboBox _headset;
    private readonly CheckBox _pause;
    private readonly CheckBox _sync;

    public SettingsForm(Config cfg, Action onSave)
    {
        _cfg = cfg;
        _onSave = onSave;
        Text = "Scape Switch · Settings";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(422, 361);
        BackColor = Palette.Background;
        ForeColor = Palette.Text;

        Controls.Add(Palette.Label("AUDIO ROUTING", 23, 17, 350, 26, 14, true));
        Controls.Add(Palette.Label("Choose the output used in each headset state.", 24, 46, 370, 22, 9, false, Palette.Muted));
        Controls.Add(Palette.Label("WHEN DOCKED  /  SPEAKERS", 24, 85, 350, 17, 8, true, Palette.Speakers));
        _speaker = DevicePicker(24, 109);
        Controls.Add(_speaker);
        Controls.Add(Palette.Label("WHEN IN USE  /  HEADSET", 24, 161, 350, 17, 8, true, Palette.Headset));
        _headset = DevicePicker(24, 185);
        Controls.Add(_headset);

        var devices = AudioSwitcher.GetOutputDevices();
        FillPicker(_speaker, devices, cfg.SpeakerDeviceId, cfg.SpeakerDeviceName);
        FillPicker(_headset, devices, cfg.HeadphoneDeviceId, cfg.HeadphoneDeviceName);

        _pause = Check("Pause media when the headset changes state", 24, 243, cfg.PauseOnSwitch);
        _sync = Check("Set the matching output when the app starts", 24, 271, cfg.SyncOnLaunch);
        Controls.Add(_pause);
        Controls.Add(_sync);

        var save = Palette.Button("SAVE SETTINGS", 24, 312, 374, 35, true);
        save.Click += (_, _) => Save();
        Controls.Add(save);
    }

    private static ComboBox DevicePicker(int x, int y)
    {
        return new ComboBox {
            Location = new Point(x, y), Size = new Size(374, 32),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Palette.Panel, ForeColor = Palette.Text,
            Font = new Font("Segoe UI", 9), FlatStyle = FlatStyle.Flat,
            IntegralHeight = false, DropDownHeight = 240
        };
    }

    private static CheckBox Check(string text, int x, int y, bool value)
    {
        return new CheckBox {
            Text = text, Location = new Point(x, y), Size = new Size(374, 24), Checked = value,
            ForeColor = Palette.Text, BackColor = Palette.Background,
            Font = new Font("Segoe UI", 9)
        };
    }

    private static void FillPicker(ComboBox picker, List<AudioDevice> devices, string? id, string name)
    {
        foreach (var device in devices) picker.Items.Add(device);
        int selected = devices.FindIndex(d => !string.IsNullOrEmpty(id) && d.Id == id);
        if (selected < 0) selected = devices.FindIndex(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (selected < 0)
        {
            picker.Items.Add(new AudioDevice(id ?? "", name + " (not connected)"));
            selected = picker.Items.Count - 1;
        }
        picker.SelectedIndex = selected;
    }

    private void Save()
    {
        if (_speaker.SelectedItem is not AudioDevice speakers || _headset.SelectedItem is not AudioDevice headset) return;
        _cfg.SpeakerDeviceName = speakers.Name.Replace(" (not connected)", "");
        _cfg.HeadphoneDeviceName = headset.Name.Replace(" (not connected)", "");
        _cfg.SpeakerDeviceId = string.IsNullOrEmpty(speakers.Id) ? null : speakers.Id;
        _cfg.HeadphoneDeviceId = string.IsNullOrEmpty(headset.Id) ? null : headset.Id;
        _cfg.PauseOnSwitch = _pause.Checked;
        _cfg.SyncOnLaunch = _sync.Checked;
        try
        {
            _cfg.Save();
            _onSave();
            Close();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save settings", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
