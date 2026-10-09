using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Eine Zeile mit Plus/Minus: Eigenschaft, Talent oder Kampftechnik.</summary>
public abstract partial class ValueRowViewModel : ObservableObject
{
    protected ValueRowViewModel(EditorViewModel editor, string id, string name)
    {
        Editor = editor;
        Id = id;
        Name = name;
    }

    protected EditorViewModel Editor { get; }

    public string Id { get; }

    public string Name { get; }

    /// <summary>Zweite Zeile, z. B. „MU/GE/KK · B“.</summary>
    public abstract string Detail { get; }

    /// <summary>Regeltext aus dem Wiki zum Aufklappen; <c>null</c> = keiner.</summary>
    public virtual string? Details => null;

    public bool HasDetails => Details is not null;

    [ObservableProperty]
    public partial bool ShowDetails { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IncreaseCommand), nameof(DecreaseCommand))]
    public partial int Value { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IncreaseCommand))]
    public partial int? Max { get; private set; }

    [ObservableProperty]
    public partial string MaxText { get; private set; } = "";

    [ObservableProperty]
    public partial string NextCostText { get; private set; } = "";

    [ObservableProperty]
    public partial string? IssueText { get; private set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    protected int Min { get; private set; }

    protected void SetValues(int value, int min, int? max, IEnumerable<Issue> issues)
    {
        Value = value;
        Min = min;
        Max = max;
        MaxText = max is { } m ? $"max. {m}" : "";
        var next = NextStepCost(value + 1);
        NextCostText = next is { } ap ? $"+1: {ap} AP" : "";
        var messages = issues.Select(i => i.Message).ToList();
        IssueText = messages.Count > 0 ? string.Join(Environment.NewLine, messages) : null;
        DecreaseCommand.NotifyCanExecuteChanged();
    }

    /// <summary>AP für die Steigerung auf <paramref name="value"/>; <c>null</c> außerhalb der Kostentabelle.</summary>
    protected abstract int? NextStepCost(int value);

    protected abstract void Store(int value);

    private bool CanIncrease() => (Max is null || Value < Max) && NextStepCost(Value + 1) is not null;

    [RelayCommand(CanExecute = nameof(CanIncrease))]
    private void Increase()
    {
        // RelayCommand.Execute prüft CanExecute nicht selbst.
        if (CanIncrease()) Store(Value + 1);
    }

    private bool CanDecrease() => Value > Min;

    [RelayCommand(CanExecute = nameof(CanDecrease))]
    private void Decrease()
    {
        if (CanDecrease()) Store(Value - 1);
    }

    [RelayCommand]
    private void ToggleDetails() => ShowDetails = !ShowDetails;
}
