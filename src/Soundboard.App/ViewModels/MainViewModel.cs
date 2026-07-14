using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using Soundboard.App.Utils;
using Soundboard.Core.Audio;
using Soundboard.Core.Hotkeys;
using Soundboard.Core.Persistence;
using Soundboard.Core.Soundboard;

namespace Soundboard.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AudioDeviceService _devices = new();
    private readonly AudioEngine _engine = new();
    private readonly ClipMonitorEngine _monitorEngine = new();
    private readonly SoundLibraryService _library = new();
    private readonly SoundRepository _soundRepo = new();
    private readonly SettingsRepository _settingsRepo = new();
    private readonly GlobalHotkeyManager _hotkeys = new();

    private SoundPlayerService _player;
    private readonly Dictionary<Guid, int> _soundHotkeyIds = new();

    private string? _selectedCaptureDeviceId;
    private string? _selectedRenderDeviceId;
    private string? _selectedMonitorDeviceId;
    private bool _hearSoundsLocally = true;
    private string _statusText = "Stopped";
    private string _outputWarningText = string.Empty;
    private string _discordHintText = string.Empty;
    private bool _hasVirtualCable;
    private bool _showOutputWarning;
    private bool _showInstallVirtualCableButton;
    private SoundClipItem? _hotkeyCaptureTarget;

    public MainViewModel()
    {
        _player = new SoundPlayerService(_engine, _monitorEngine);

        LoadSounds();
        RefreshDevices();
        LoadSettings();

        RefreshDevicesCommand = new RelayCommand(RefreshDevices);
        StartEngineCommand = new RelayCommand(StartEngine);
        StopEngineCommand = new RelayCommand(StopEngine);
        ImportSoundCommand = new RelayCommand(ImportSound);
        PlaySoundCommand = new RelayCommand<SoundClipItem>(PlaySound);
        DeleteSoundCommand = new RelayCommand<SoundClipItem>(DeleteSound);
        BindHotkeyCommand = new RelayCommand<SoundClipItem>(BeginHotkeyCapture);
        ClearHotkeyCommand = new RelayCommand<SoundClipItem>(ClearHotkey);
        CancelHotkeyCaptureCommand = new RelayCommand(CancelHotkeyCapture, () => IsCapturingHotkey);
        InstallVirtualCableCommand = new RelayCommand(OpenVirtualCableDownloadPage);

        _engine.StateChanged += s => StatusText = s.ToString();
    }

    public ObservableCollection<AudioDeviceInfo> CaptureDevices { get; } = new();
    public ObservableCollection<AudioDeviceInfo> RenderDevices { get; } = new();
    public ObservableCollection<SoundClipItem> Sounds { get; } = new();

    public string? SelectedCaptureDeviceId
    {
        get => _selectedCaptureDeviceId;
        set { if (value != _selectedCaptureDeviceId) { _selectedCaptureDeviceId = value; OnPropertyChanged(); } }
    }

    public string? SelectedRenderDeviceId
    {
        get => _selectedRenderDeviceId;
        set
        {
            if (value == _selectedRenderDeviceId)
                return;

            _selectedRenderDeviceId = value;
            OnPropertyChanged();
            UpdateOutputGuidance();
        }
    }

    public string? SelectedMonitorDeviceId
    {
        get => _selectedMonitorDeviceId;
        set
        {
            if (value == _selectedMonitorDeviceId)
                return;

            _selectedMonitorDeviceId = value;
            OnPropertyChanged();
            ApplyMonitorOutput();
        }
    }

    public bool HearSoundsLocally
    {
        get => _hearSoundsLocally;
        set
        {
            if (value == _hearSoundsLocally)
                return;

            _hearSoundsLocally = value;
            OnPropertyChanged();
            ApplyMonitorOutput();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set { if (value != _statusText) { _statusText = value; OnPropertyChanged(); } }
    }

    public string OutputWarningText
    {
        get => _outputWarningText;
        private set { if (value != _outputWarningText) { _outputWarningText = value; OnPropertyChanged(); } }
    }

    public string DiscordHintText
    {
        get => _discordHintText;
        private set { if (value != _discordHintText) { _discordHintText = value; OnPropertyChanged(); } }
    }

    public bool HasVirtualCable
    {
        get => _hasVirtualCable;
        private set { if (value != _hasVirtualCable) { _hasVirtualCable = value; OnPropertyChanged(); } }
    }

    public bool ShowOutputWarning
    {
        get => _showOutputWarning;
        private set { if (value != _showOutputWarning) { _showOutputWarning = value; OnPropertyChanged(); } }
    }

    public bool ShowInstallVirtualCableButton
    {
        get => _showInstallVirtualCableButton;
        private set { if (value != _showInstallVirtualCableButton) { _showInstallVirtualCableButton = value; OnPropertyChanged(); } }
    }

    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand StartEngineCommand { get; }
    public RelayCommand StopEngineCommand { get; }
    public RelayCommand ImportSoundCommand { get; }
    public RelayCommand<SoundClipItem> PlaySoundCommand { get; }
    public RelayCommand<SoundClipItem> DeleteSoundCommand { get; }
    public RelayCommand<SoundClipItem> BindHotkeyCommand { get; }
    public RelayCommand<SoundClipItem> ClearHotkeyCommand { get; }
    public RelayCommand CancelHotkeyCaptureCommand { get; }

    public bool IsCapturingHotkey => _hotkeyCaptureTarget is not null;

    public string HotkeyCapturePrompt =>
        _hotkeyCaptureTarget is null
            ? string.Empty
            : $"Listening for hotkey: {_hotkeyCaptureTarget.Name} — press a key combo now (Esc to cancel)";

    public Guid? HotkeyCaptureTargetId => _hotkeyCaptureTarget?.Id;
    public RelayCommand InstallVirtualCableCommand { get; }

    public void AttachWindowHandle(IntPtr hwnd)
    {
        _hotkeys.AttachWindowHandle(hwnd);
        RebindAllHotkeys();
    }

    public bool TryHandleWindowMessage(int msg, IntPtr wParam)
        => _hotkeys.TryHandleWindowMessage(msg, wParam);

    private void RefreshDevices()
    {
        CaptureDevices.Clear();
        foreach (var d in _devices.ListCaptureDevices()) CaptureDevices.Add(d);

        RenderDevices.Clear();
        var renderDevices = _devices.ListRenderDevices()
            .OrderByDescending(d => VirtualCableHelper.IsVirtualCableRenderDevice(d.FriendlyName))
            .ThenBy(d => d.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var d in renderDevices) RenderDevices.Add(d);

        HasVirtualCable = renderDevices.Any(d => VirtualCableHelper.IsVirtualCableRenderDevice(d.FriendlyName));

        var savedCapture = _settingsRepo.Get("audio.captureDeviceId");
        var savedRender = _settingsRepo.Get("audio.renderDeviceId");
        var preferredVirtual = VirtualCableHelper.FindPreferredRenderDevice(renderDevices);

        SelectedCaptureDeviceId = ResolveDeviceId(
            CaptureDevices,
            savedCapture,
            _devices.TryGetDefaultCaptureDeviceId());

        // Never default to speakers — route into a virtual cable when available.
        var renderId = ResolveDeviceId(RenderDevices, savedRender, preferredVirtual?.Id);
        if (renderId is not null
            && RenderDevices.FirstOrDefault(d => d.Id == renderId) is { } chosen
            && VirtualCableHelper.IsLikelySpeaker(chosen.FriendlyName)
            && preferredVirtual is not null)
        {
            renderId = preferredVirtual.Id;
        }

        SelectedRenderDeviceId = renderId;
        UpdateOutputGuidance();

        var savedMonitor = _settingsRepo.Get("audio.monitorDeviceId");
        SelectedMonitorDeviceId = ResolveDeviceId(
            RenderDevices,
            _selectedMonitorDeviceId ?? savedMonitor,
            _devices.TryGetDefaultRenderDeviceId());
    }

    private void LoadSettings()
    {
        HearSoundsLocally = _settingsRepo.Get("audio.hearLocally") != "false";
    }

    private static string? ResolveDeviceId(
        IEnumerable<AudioDeviceInfo> devices,
        string? savedId,
        string? fallbackId)
    {
        var list = devices.ToList();
        if (!string.IsNullOrWhiteSpace(savedId) && list.Any(d => d.Id == savedId))
            return savedId;

        if (!string.IsNullOrWhiteSpace(fallbackId) && list.Any(d => d.Id == fallbackId))
            return fallbackId;

        return list.FirstOrDefault()?.Id;
    }

    private void UpdateOutputGuidance()
    {
        var render = RenderDevices.FirstOrDefault(d => d.Id == SelectedRenderDeviceId);
        var capture = VirtualCableHelper.FindMatchingCaptureDevice(CaptureDevices, render);

        DiscordHintText = VirtualCableHelper.GetDiscordSetupHint(render, capture);

        if (render is null)
        {
            OutputWarningText = "Select an output device.";
            ShowOutputWarning = true;
            ShowInstallVirtualCableButton = !HasVirtualCable;
            return;
        }

        if (VirtualCableHelper.IsVirtualCableRenderDevice(render.FriendlyName))
        {
            OutputWarningText = string.Empty;
            ShowOutputWarning = false;
            ShowInstallVirtualCableButton = false;
            return;
        }

        if (VirtualCableHelper.IsLikelySpeaker(render.FriendlyName))
        {
            OutputWarningText = HasVirtualCable
                ? $"Output is set to speakers ({render.FriendlyName}). Pick a virtual cable device like CABLE Input instead."
                : $"Output is set to speakers ({render.FriendlyName}). Install VB-CABLE first, then pick CABLE Input here.";
            ShowOutputWarning = true;
            ShowInstallVirtualCableButton = !HasVirtualCable;
            return;
        }

        OutputWarningText = "This output may not create a virtual mic for Discord. Prefer CABLE Input / VoiceMeeter Input.";
        ShowOutputWarning = true;
        ShowInstallVirtualCableButton = !HasVirtualCable;
    }

    private void LoadSounds()
    {
        Sounds.Clear();
        foreach (var s in _soundRepo.GetAll())
            AddSoundItem(new SoundClipItem(s));
    }

    private void AddSoundItem(SoundClipItem item)
    {
        item.PropertyChanged += OnSoundItemPropertyChanged;
        Sounds.Add(item);
    }

    private void OnSoundItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SoundClipItem item)
            return;

        if (e.PropertyName is nameof(SoundClipItem.Gain) or nameof(SoundClipItem.Name))
            _soundRepo.Upsert(item.ToModel());
    }

    private void PersistSettings()
    {
        if (!string.IsNullOrWhiteSpace(SelectedCaptureDeviceId))
            _settingsRepo.Set("audio.captureDeviceId", SelectedCaptureDeviceId);
        if (!string.IsNullOrWhiteSpace(SelectedRenderDeviceId))
            _settingsRepo.Set("audio.renderDeviceId", SelectedRenderDeviceId);
        if (!string.IsNullOrWhiteSpace(SelectedMonitorDeviceId))
            _settingsRepo.Set("audio.monitorDeviceId", SelectedMonitorDeviceId);
        _settingsRepo.Set("audio.hearLocally", HearSoundsLocally ? "true" : "false");
    }

    private void ApplyMonitorOutput()
    {
        if (!string.IsNullOrWhiteSpace(SelectedMonitorDeviceId))
            _settingsRepo.Set("audio.monitorDeviceId", SelectedMonitorDeviceId);
        _settingsRepo.Set("audio.hearLocally", HearSoundsLocally ? "true" : "false");

        if (_engine.State != AudioEngineState.Running || _engine.MixWaveFormat is null)
            return;

        _monitorEngine.Stop();

        if (HearSoundsLocally && !string.IsNullOrWhiteSpace(SelectedMonitorDeviceId))
        {
            try
            {
                _monitorEngine.Start(SelectedMonitorDeviceId, _engine.MixWaveFormat);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed to switch local monitor device");
            }
        }
    }

    private void StartEngine()
    {
        if (string.IsNullOrWhiteSpace(SelectedCaptureDeviceId) || string.IsNullOrWhiteSpace(SelectedRenderDeviceId))
        {
            MessageBox.Show("Select both an input microphone and an output device.", "Soundboard");
            return;
        }

        var render = RenderDevices.FirstOrDefault(d => d.Id == SelectedRenderDeviceId);
        if (render is not null && !VirtualCableHelper.IsVirtualCableRenderDevice(render.FriendlyName))
        {
            var message = HasVirtualCable
                ? "Output is not a virtual cable device, so Discord cannot use this as a microphone.\n\nSelect a device like 'CABLE Input' or 'VoiceMeeter Input', then Start again."
                : "No virtual cable is installed on this PC.\n\nInstall VB-CABLE (free), reboot if needed, click Refresh devices, select 'CABLE Input' as Output, then Start.\n\nOpen the VB-CABLE download page now?";

            if (!HasVirtualCable)
            {
                var install = MessageBox.Show(message, "Virtual cable required", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (install == MessageBoxResult.Yes)
                    OpenVirtualCableDownloadPage();
                return;
            }

            MessageBox.Show(message, "Wrong output device", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            PersistSettings();
            _engine.Start(new AudioEngineConfig(SelectedCaptureDeviceId, SelectedRenderDeviceId));
            ApplyMonitorOutput();

            StatusText = "Running";
        }
        catch (Exception ex)
        {
            _monitorEngine.Stop();
            _engine.Stop();
            MessageBox.Show(ex.Message, "Failed to start audio engine");
            StatusText = "Faulted";
        }
    }

    private void StopEngine()
    {
        _monitorEngine.Stop();
        _engine.Stop();
        StatusText = "Stopped";
    }

    private void ImportSound()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import sound",
            Filter = "Audio files|*.wav;*.mp3;*.aac;*.m4a;*.wma;*.flac;*.ogg|All files|*.*",
            Multiselect = false
        };

        if (dlg.ShowDialog() != true)
            return;

        try
        {
            var clip = _library.ImportFromFile(dlg.FileName);
            _soundRepo.Upsert(clip);
            AddSoundItem(new SoundClipItem(clip));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Import failed");
        }
    }

    private void PlaySound(SoundClipItem item)
    {
        if (_engine.State != AudioEngineState.Running)
        {
            MessageBox.Show("Click Start first — the audio engine must be running to play sounds.", "Soundboard");
            return;
        }

        try
        {
            _player.Play(item.ToModel());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Playback failed");
        }
    }

    private void DeleteSound(SoundClipItem item)
    {
        if (MessageBox.Show($"Delete '{item.Name}'?", "Soundboard", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        try
        {
            UnbindHotkey(item);
            item.PropertyChanged -= OnSoundItemPropertyChanged;
            _soundRepo.Delete(item.Id);
            _library.DeleteClip(item.ToModel());
            Sounds.Remove(item);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Delete failed");
        }
    }

    private void BeginHotkeyCapture(SoundClipItem item)
    {
        _hotkeyCaptureTarget = item;
        NotifyHotkeyCaptureChanged();
    }

    public void CancelHotkeyCapture()
    {
        if (_hotkeyCaptureTarget is null)
            return;

        _hotkeyCaptureTarget = null;
        NotifyHotkeyCaptureChanged();
    }

    public void CompleteHotkeyCapture(Hotkey hotkey)
    {
        var item = _hotkeyCaptureTarget;
        if (item is null)
            return;

        _hotkeyCaptureTarget = null;
        NotifyHotkeyCaptureChanged();

        var hotkeyText = hotkey.ToString();
        var duplicate = Sounds.FirstOrDefault(s =>
            s.Id != item.Id
            && !string.IsNullOrWhiteSpace(s.Hotkey)
            && string.Equals(s.Hotkey, hotkeyText, StringComparison.OrdinalIgnoreCase));

        if (duplicate is not null)
        {
            MessageBox.Show(
                $"Hotkey {hotkeyText} is already bound to '{duplicate.Name}'.",
                "Hotkey conflict",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        item.Hotkey = hotkeyText;
        ApplyHotkeyBinding(item);
        StatusText = $"Bound {hotkeyText} to {item.Name}";
    }

    private void ClearHotkey(SoundClipItem item)
    {
        CancelHotkeyCapture();
        UnbindHotkey(item);
        item.Hotkey = null;
        _soundRepo.Upsert(item.ToModel());
        StatusText = $"Cleared hotkey for {item.Name}";
    }

    private void NotifyHotkeyCaptureChanged()
    {
        OnPropertyChanged(nameof(IsCapturingHotkey));
        OnPropertyChanged(nameof(HotkeyCapturePrompt));
        OnPropertyChanged(nameof(HotkeyCaptureTargetId));
        CancelHotkeyCaptureCommand.NotifyCanExecuteChanged();
    }

    private void ApplyHotkeyBinding(SoundClipItem item)
    {
        try
        {
            _soundRepo.Upsert(item.ToModel());
            UnbindHotkey(item);

            if (string.IsNullOrWhiteSpace(item.Hotkey))
                return;

            if (!Hotkey.TryParse(item.Hotkey, out var hk))
            {
                MessageBox.Show("Invalid hotkey format. Example: Ctrl+Alt+F1", "Hotkey");
                return;
            }

            var id = _hotkeys.Register(hk, () => PlaySound(item));
            _soundHotkeyIds[item.Id] = id;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Hotkey bind failed");
        }
    }

    private void BindHotkey(SoundClipItem item)
    {
        ApplyHotkeyBinding(item);
    }

    private void RebindAllHotkeys()
    {
        foreach (var s in Sounds)
        {
            if (!string.IsNullOrWhiteSpace(s.Hotkey))
                BindHotkey(s);
        }
    }

    private void UnbindHotkey(SoundClipItem item)
    {
        if (_soundHotkeyIds.TryGetValue(item.Id, out var id))
        {
            _hotkeys.Unregister(id);
            _soundHotkeyIds.Remove(item.Id);
        }
    }

    public void OpenVirtualCableDownloadPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://vb-audio.com/Cable/",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not open browser");
        }
    }

    public void Dispose()
    {
        foreach (var id in _soundHotkeyIds.Values.ToList())
            _hotkeys.Unregister(id);
        _soundHotkeyIds.Clear();

        _hotkeys.Dispose();
        _monitorEngine.Dispose();
        _engine.Dispose();
        _devices.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

