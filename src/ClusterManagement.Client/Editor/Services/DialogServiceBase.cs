using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClusterManagement.Client.Editor.Services;

internal abstract class DialogServiceBase : INotifyPropertyChanged
{
    private bool _displayDialog;

    public bool DisplayDialog
    {
        get => _displayDialog;
        set => SetField(ref _displayDialog, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
