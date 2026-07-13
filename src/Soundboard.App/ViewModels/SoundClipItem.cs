using System.ComponentModel;
using System.Runtime.CompilerServices;
using Soundboard.Core.Soundboard;

namespace Soundboard.App.ViewModels;

public sealed class SoundClipItem : INotifyPropertyChanged
{
    private string _name;
    private string? _hotkey;
    private float _gain;

    public SoundClipItem(SoundClip clip)
    {
        Id = clip.Id;
        StoredFilePath = clip.StoredFilePath;
        _name = clip.Name;
        _hotkey = clip.Hotkey;
        _gain = clip.Gain;
    }

    public Guid Id { get; }
    public string StoredFilePath { get; }

    public string Name
    {
        get => _name;
        set { if (value != _name) { _name = value; OnPropertyChanged(); } }
    }

    public string? Hotkey
    {
        get => _hotkey;
        set { if (value != _hotkey) { _hotkey = value; OnPropertyChanged(); } }
    }

    public float Gain
    {
        get => _gain;
        set
        {
            var clamped = Math.Clamp(value, 0f, 2f);
            if (Math.Abs(clamped - _gain) <= 0.0001f)
                return;

            _gain = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumePercent));
            OnPropertyChanged(nameof(VolumeLabel));
        }
    }

    public double VolumePercent
    {
        get => Math.Round(_gain * 100d, 0);
        set => Gain = (float)Math.Clamp(value, 0d, 200d) / 100f;
    }

    public string VolumeLabel => $"{VolumePercent:0}%";

    public SoundClip ToModel() => new(Id, Name, StoredFilePath, Gain, Hotkey);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

