using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Stonks.Client.Desktop.ViewModels;

public sealed class QaTurnViewModel : INotifyPropertyChanged
{
    private string answer = "";
    private bool isAnswering = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Question { get; }

    public string Answer
    {
        get => answer;
        set => SetField(ref answer, value);
    }

    public bool IsAnswering
    {
        get => isAnswering;
        set => SetField(ref isAnswering, value);
    }

    public QaTurnViewModel(string question) => Question = question;

    public void AppendAnswerChunk(string chunk) => Answer += chunk;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
