using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Bereich „Tiergefährten“: Liste der Vertrauten und Begleiter des Helden.</summary>
public sealed partial class CompanionsPageViewModel(EditorViewModel editor) : PageViewModel
{
    public ObservableCollection<CompanionCardViewModel> Cards { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    public void Update(IReadOnlyList<CompanionState> states)
    {
        // Vertraute zuerst, dann Begleiter, verstorbene am Ende
        var ordered = states
            .OrderBy(s => s.Companion.Deceased)
            .ThenBy(s => s.Companion.Kind != CompanionKind.Familiar)
            .ToList();
        if (Cards.Count != ordered.Count || !Cards.Zip(ordered).All(p => p.First.Id == p.Second.Companion.Id))
        {
            Cards.Clear();
            foreach (var s in ordered) Cards.Add(new CompanionCardViewModel(editor, s.Companion.Id));
        }
        foreach (var (card, s) in Cards.Zip(ordered)) card.Update(s);
        IsEmpty = Cards.Count == 0;
    }

    [RelayCommand]
    private void AddFamiliar() => editor.AddCompanion(CompanionKind.Familiar);

    [RelayCommand]
    private void AddCompanion() => editor.AddCompanion(CompanionKind.Companion);
}

/// <summary>Karte eines Tiers in der Liste.</summary>
public sealed partial class CompanionCardViewModel(EditorViewModel editor, Guid id) : ObservableObject
{
    public Guid Id { get; } = id;

    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    [ObservableProperty]
    public partial string SpeciesText { get; private set; } = "";

    [ObservableProperty]
    public partial string ValuesText { get; private set; } = "";

    [ObservableProperty]
    public partial string ApText { get; private set; } = "";

    [ObservableProperty]
    public partial string ApDetailText { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsFamiliar { get; private set; }

    [ObservableProperty]
    public partial bool IsDeceased { get; private set; }

    [ObservableProperty]
    public partial bool HasWarnings { get; private set; }

    public void Update(CompanionState s)
    {
        var c = s.Companion;
        IsFamiliar = c.Kind == CompanionKind.Familiar;
        IsDeceased = c.Deceased;
        Name = c.Name.Length > 0 ? c.Name : s.Animal?.Name ?? "Neues Tier";
        SpeciesText = (s.Animal?.Name ?? "Tierart wählen") +
                      (c.FamiliarChoice is { } choice ? $" · {choice}" : "") +
                      (c.Deceased ? " · verstorben" + (c.DeceasedNote is { Length: > 0 } n ? $" ({n})" : "") : "");
        ValuesText = CompanionTexts.ShortValues(s);
        ApText = $"{s.Ap.Remaining} AP frei";
        ApDetailText = $"{s.Ap.Spent} von {s.Ap.Total} AP ausgegeben";
        HasWarnings = s.Issues.Any(i => i.Severity >= IssueSeverity.Warning);
    }

    [RelayCommand]
    private void Open() => editor.OpenCompanion(Id);
}
