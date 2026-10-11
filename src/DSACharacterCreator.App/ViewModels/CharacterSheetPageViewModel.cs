using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Rules;
using DSACharacterCreator.Core.Sheet;
using DSACharacterCreator.Services;

namespace DSACharacterCreator.ViewModels;

/// <summary>
/// Charakterbogen: Gesamtansicht des Helden zum Lesen (Grundlage auch für den PDF-Export). Wird nur aufgebaut, solange
/// die Seite angezeigt wird; Änderungen auf anderen Seiten bauen ihn beim nächsten Öffnen neu.
/// </summary>
public sealed partial class CharacterSheetPageViewModel(EditorViewModel editor) : ViewModelBase
{
    private CharacterState? _state;
    private bool _dirty = true;
    private bool _active;

    [ObservableProperty]
    public partial CharacterSheet? Sheet { get; private set; }

    /// <summary>„Spezies: Mensch · Kultur: Mittelreich …“ als Zeilen.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SheetField> Header { get; private set; } = [];

    /// <summary>Vorteile, Nachteile, Sonderfertigkeiten, Sprachen, Schriften: je Gruppe eine kommagetrennte Zeile.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SheetTextGroup> TextGroups { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<SheetSkillGroup> SkillGroups { get; private set; } = [];

    /// <summary>Talentgruppen auf zwei Spalten verteilt (breite Ansicht), möglichst gleich lang.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SheetSkillGroup> SkillColumnLeft { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<SheetSkillGroup> SkillColumnRight { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<SheetSpellGroup> SpellGroups { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasWeapons { get; private set; }

    [ObservableProperty]
    public partial bool HasArmors { get; private set; }

    [ObservableProperty]
    public partial bool HasItems { get; private set; }

    /// <summary>Rückmeldung zum PDF-Export; <c>null</c> = keine.</summary>
    [ObservableProperty]
    public partial string? ExportStatus { get; private set; }

    private bool _exporting;

    private bool CanExportPdf() => !_exporting;

    /// <summary>Bogen als PDF speichern (Desktop: Dateidialog, Android: Speicherort über das System wählen).</summary>
    [RelayCommand(CanExecute = nameof(CanExportPdf))]
    private async Task ExportPdf()
    {
        if (_state is null) return;
        _exporting = true;
        ExportPdfCommand.NotifyCanExecuteChanged();
        try
        {
            if (_dirty || Sheet is null) Build();
            var sheet = Sheet!;
            ExportStatus = "PDF wird erstellt …";
            var pdf = await Task.Run(() => PdfExport.Create(sheet));
            var fileName = string.Concat(sheet.Name.Split(Path.GetInvalidFileNameChars())) + ".pdf";
            var saved = await editor.FileDialogs.SaveBytesAsync("Charakterbogen als PDF speichern", fileName, "PDF-Dokument", ".pdf", pdf);
            ExportStatus = saved ? $"Gespeichert: {fileName}" : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ExportStatus = "PDF-Export fehlgeschlagen: " + ex.Message;
        }
        finally
        {
            _exporting = false;
            ExportPdfCommand.NotifyCanExecuteChanged();
        }
    }

    public void Update(CharacterState state)
    {
        _state = state;
        _dirty = true;
        if (_active) Build();
    }

    /// <summary>Seite wird angezeigt bzw. verlassen.</summary>
    public void SetActive(bool active)
    {
        _active = active;
        if (active && _dirty) Build();
    }

    /// <summary>Gruppen in Reihenfolge auf zwei Spalten: die linke endet dort, wo sie etwa die Hälfte der Zeilen hat.</summary>
    private static (IReadOnlyList<SheetSkillGroup>, IReadOnlyList<SheetSkillGroup>) Balance(IReadOnlyList<SheetSkillGroup> groups)
    {
        var total = groups.Sum(g => g.Items.Count + 2);
        var best = 0;
        var bestDiff = int.MaxValue;
        for (int split = 0, left = 0; split <= groups.Count; split++)
        {
            var diff = Math.Abs(total - 2 * left);
            if (diff < bestDiff) (best, bestDiff) = (split, diff);
            if (split < groups.Count) left += groups[split].Items.Count + 2;
        }
        return (groups.Take(best).ToList(), groups.Skip(best).ToList());
    }

    private void Build()
    {
        if (_state is null) return;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var sheet = CharacterSheet.Create(editor.Character, _state, editor.Rules);
        Header = sheet.Header;
        var groups = new List<SheetTextGroup>
        {
            new("Vorteile", sheet.Advantages),
            new("Nachteile", sheet.Disadvantages),
        };
        groups.AddRange(sheet.SpecialAbilities.Select(g => new SheetTextGroup(g.Title, g.Items)));
        groups.Add(new SheetTextGroup("Sprachen", sheet.Languages));
        groups.Add(new SheetTextGroup("Schriften", sheet.Scripts));
        TextGroups = groups;
        SkillGroups = sheet.Skills.Select(g => new SheetSkillGroup(g.Title, g.Items)).ToList();
        (SkillColumnLeft, SkillColumnRight) = Balance(SkillGroups);
        SpellGroups = sheet.Spells.Select(g => new SheetSpellGroup(g.Title, g.Items)).ToList();
        HasWeapons = sheet.Weapons.Count > 0;
        HasArmors = sheet.Armors.Count > 0;
        HasItems = sheet.Items.Count > 0;
        Sheet = sheet;
        _dirty = false;
        Services.Timing.Log("Charakterbogen", watch);
    }
}

/// <param name="Text">„Begabung (Klettern), Hohe Lebenskraft III“; „–“ ohne Einträge.</param>
public sealed record SheetTextGroup(string Title, string Text)
{
    public SheetTextGroup(string title, IReadOnlyList<string> items) : this(title, items.Count > 0 ? string.Join(", ", items) : "–")
    {
    }
}

public sealed record SheetSkillGroup(string Title, IReadOnlyList<SheetSkill> Items);

public sealed record SheetSpellGroup(string Title, IReadOnlyList<SheetSpell> Items);
