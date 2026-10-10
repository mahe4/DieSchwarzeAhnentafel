using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Grundlagen: Erfahrungsgrad, Spezies, Kultur und Profession.</summary>
public sealed partial class BasicsPageViewModel : ViewModelBase
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private readonly EditorViewModel _editor;

    public BasicsPageViewModel(EditorViewModel editor)
    {
        _editor = editor;
        ExperienceLevels = editor.Rules.ExperienceLevels.Select(l => new ExperienceLevelOptionViewModel(editor, l)).ToList();
    }

    private RuleData Rules => _editor.Rules;

    private PackageCosts Costs => _costs ??= new PackageCosts(Rules);

    private PackageCosts? _costs;

    public IReadOnlyList<ExperienceLevelOptionViewModel> ExperienceLevels { get; }

    [ObservableProperty]
    public partial string ExperienceLevelText { get; private set; } = "";

    // ---- Spezies ----

    [ObservableProperty]
    public partial IReadOnlyList<SpeciesOptionViewModel> SpeciesOptions { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ChoiceViewModel> SpeciesChoices { get; private set; } = [];

    [ObservableProperty]
    public partial string? SpeciesDetails { get; private set; }

    // ---- Kultur ----

    [ObservableProperty]
    public partial string CultureName { get; private set; } = "";

    [ObservableProperty]
    public partial bool HasCulture { get; private set; }

    [ObservableProperty]
    public partial bool HasCulturePackage { get; private set; }

    [ObservableProperty]
    public partial string CulturePackageText { get; private set; } = "";

    [ObservableProperty]
    public partial string? CultureDetails { get; private set; }

    private bool _culturePackage;

    public bool CulturePackage
    {
        get => _culturePackage;
        set
        {
            if (_culturePackage == value) return;
            _editor.Apply(c => c.CulturePackage = value);
        }
    }

    // ---- Profession ----

    [ObservableProperty]
    public partial string ProfessionName { get; private set; } = "";

    [ObservableProperty]
    public partial bool HasProfession { get; private set; }

    [ObservableProperty]
    public partial string ProfessionSummary { get; private set; } = "";

    [ObservableProperty]
    public partial ChoiceViewModel? VariantChoice { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<ChoiceViewModel> CombatChoices { get; private set; } = [];

    [ObservableProperty]
    public partial string? ProfessionDetails { get; private set; }

    public void Update(CharacterState state)
    {
        foreach (var option in ExperienceLevels) option.SetSelected(option.Level.Id == state.ExperienceLevel.Id);
        ExperienceLevelText = $"{state.ExperienceLevel.Name} ({state.ExperienceLevel.Ap.ToString("N0", German)} AP)";

        var creation = _editor.Character.Creation;
        var sources = SourceSelection.ForCharacter(_editor.Character, Rules.Books);
        var origin = state.Origin;

        // Spezies: nur verfügbare (oder die gewählte) anzeigen
        SpeciesOptions = Rules.Species
            .Where(s => sources.IsAvailable(s.Publications) || s.Id == origin.Species?.Id)
            .OrderBy(s => s.Name, StringComparer.CurrentCulture)
            .Select(s => new SpeciesOptionViewModel(_editor, s, s.Id == origin.Species?.Id))
            .ToList();
        SpeciesChoices = origin.Species is { } species ? SpeciesChoiceList(species, creation.SpeciesAttributeChoices) : [];
        SpeciesDetails = origin.Species is { } sp ? SpeciesText(sp) : null;

        // Kultur
        HasCulture = origin.Culture is not null;
        CultureName = origin.Culture?.Name ?? "Keine Kultur gewählt";
        HasCulturePackage = origin.Culture?.Package is not null;
        _culturePackage = creation.CulturePackage;
        OnPropertyChanged(nameof(CulturePackage));
        CulturePackageText = origin.Culture?.Package is { } package
            ? $"{package.Name} als Vorlage übernehmen (Talente im Wert von {Costs.CultureCost(package)} AP): " +
              string.Join(", ", package.Skills.Select(v => $"{SkillName(v.Skill)} +{v.Value}"))
            : "";
        CultureDetails = origin.Culture is { } culture ? CultureText(culture) : null;

        // Profession
        var profession = origin.Profession;
        HasProfession = profession is not null;
        ProfessionName = profession?.Name ?? "Keine Profession gewählt";
        ProfessionSummary = profession is null || state.ProfessionCost is not { } pc
            ? ""
            : $"{profession.Group} · Vorlage im Wert von {pc.Total} AP (Talente {pc.Skills} · Kampftechniken {pc.CombatTechniques}" +
              (pc.Unmodeled > 0 ? $" · Sonderfertigkeiten, Sprachen, Zauber u. Ä. vorläufig {pc.Unmodeled} laut Wiki" : "") +
              "). Alle Werte lassen sich danach frei anpassen.";
        VariantChoice = profession?.Variants is { Count: > 0 } variants
            ? new ChoiceViewModel(
                profession.VariantRequired ? "Variante (Pflicht)" : "Variante",
                [new ChoiceOption("", $"Keine Variante ({Costs.ProfessionCost(profession, null, null).Total} AP)"),
                 .. variants.Select(v => new ChoiceOption(v.Id, $"{v.Name} ({Costs.ProfessionCost(profession, v, null).Total} AP)"))],
                creation.ProfessionVariant ?? "",
                id => _editor.Apply(c => c.ProfessionVariant = id.Length == 0 ? null : id))
            : null;
        CombatChoices = profession?.CombatTechniqueChoices is { } choices
            ? choices.Select((choice, i) => new ChoiceViewModel(
                $"Kampftechnik auf {choice.Value}",
                choice.Options.Select(o => new ChoiceOption(o, CombatName(o))).ToList(),
                creation.ProfessionCombatTechniqueChoices.ElementAtOrDefault(i),
                id => _editor.Apply(c => SetAt(c.ProfessionCombatTechniqueChoices, i, id)))).ToList()
            : [];
        ProfessionDetails = profession is null ? null : ProfessionText(profession, origin.Variant);
    }

    private IReadOnlyList<ChoiceViewModel> SpeciesChoiceList(SpeciesDefinition species, List<string?> chosen)
    {
        var result = new List<ChoiceViewModel>();
        var index = 0;
        foreach (var change in species.AttributeChanges.Where(c => c.IsChoice))
        {
            var i = index++;
            result.Add(new ChoiceViewModel(
                $"Eigenschaft {change.Value:+0;−0} ({(change.Options.Count > 3 ? "beliebig" : string.Join(" oder ", change.Options))})",
                change.Options.Select(o => new ChoiceOption(o, AttributeName(o))).ToList(),
                chosen.ElementAtOrDefault(i),
                id => _editor.Apply(c => SetAt(c.SpeciesAttributeChoices, i, id))));
        }
        return result;
    }

    private static void SetAt(List<string?> list, int index, string value)
    {
        while (list.Count <= index) list.Add(null);
        list[index] = value;
    }

    private string AttributeName(string id) => Rules.Attributes.First(a => a.Id == id).Name;

    private string SkillName(string id) => Rules.Skills.First(s => s.Id == id).Name;

    private string CombatName(string id) => Rules.CombatTechniques.First(c => c.Id == id).Name;

    private static string Lines(params (string Label, string? Text)[] parts) =>
        string.Join(Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p.Text))
            .Select(p => $"{p.Label}: {p.Text!.Replace("\n", " ")}"));

    private static string SpeciesText(SpeciesDefinition s) => Lines(
        ("Eigenschaftsänderungen", s.AttributeChangesText),
        ("Automatische Vorteile", s.AutomaticAdvantagesText),
        ("Automatische Nachteile", s.AutomaticDisadvantagesText),
        ("Verbrauchte AP für Vor-/Nachteile", s.AdvantageApText),
        ("Übliche Kulturen", s.CommonCulturesText),
        ("Dringend empfohlen", s.RecommendedText),
        ("Typische Vorteile", s.TypicalAdvantagesText),
        ("Typische Nachteile", s.TypicalDisadvantagesText),
        ("Untypische Vorteile", s.UntypicalAdvantagesText),
        ("Untypische Nachteile", s.UntypicalDisadvantagesText),
        ("Anmerkung", s.Note));

    private static string CultureText(CultureDefinition c) => Lines(
        ("Sprache", c.LanguageText),
        ("Schrift", c.ScriptText),
        ("Ortskenntnis", c.LocalKnowledgeText),
        ("Sozialstatus", c.SocialStatusText),
        ("Übliche Professionen", c.CommonProfessionsText),
        ("Typische Vorteile", c.TypicalAdvantagesText),
        ("Typische Nachteile", c.TypicalDisadvantagesText),
        ("Untypische Vorteile", c.UntypicalAdvantagesText),
        ("Untypische Nachteile", c.UntypicalDisadvantagesText),
        ("Typische Talente", c.TypicalSkillsText),
        ("Untypische Talente", c.UntypicalSkillsText));

    private static string ProfessionText(ProfessionDefinition p, ProfessionVariant? variant) => Lines(
        ("Voraussetzungen", p.PrerequisitesText),
        ("Sonderfertigkeiten", p.SpecialAbilitiesText),
        ("Zauber", p.SpellsText),
        ("Liturgien", p.LiturgiesText),
        ("Stil", p.StylesText),
        ("Variante", variant?.Text),
        ("Empfohlene Vorteile", p.RecommendedAdvantagesText),
        ("Empfohlene Nachteile", p.RecommendedDisadvantagesText),
        ("Ungeeignete Vorteile", p.UnsuitableAdvantagesText),
        ("Ungeeignete Nachteile", p.UnsuitableDisadvantagesText),
        ("Anmerkung", p.Note),
        ("Von Hand übernehmen", p.Unparsed is { Count: > 0 } u ? string.Join("; ", u) : null));

    [RelayCommand]
    private void ChooseCulture()
    {
        var sources = SourceSelection.ForCharacter(_editor.Character, Rules.Books);
        _editor.OpenPicker("Kultur wählen",
            Rules.Cultures.Where(c => sources.IsAvailable(c.Publications)).Select(c => new PickerItemViewModel(
                c.Id, c.Name, c.Group, c.Package is { } p ? $"Kulturpaket {Costs.CultureCost(p)} AP" : "ohne Kulturpaket")),
            _editor.Character.Creation.Culture,
            id => _editor.Apply(c =>
            {
                c.Culture = id;
                c.CulturePackage = true;
            }));
    }

    [RelayCommand]
    private void ClearCulture() => _editor.Apply(c => c.Culture = null);

    [RelayCommand]
    private void ChooseProfession()
    {
        var sources = SourceSelection.ForCharacter(_editor.Character, Rules.Books);
        _editor.OpenPicker("Profession wählen",
            Rules.Professions.Where(p => sources.IsAvailable(p.Publications)).Select(p => new PickerItemViewModel(
                p.Id, p.Name, p.Group,
                $"{Costs.ProfessionCost(p, null, null).Total} AP" + (p.Variants is { Count: > 0 } v ? $" · {v.Count} {(v.Count == 1 ? "Variante" : "Varianten")}" : ""))),
            _editor.Character.Creation.Profession,
            id => _editor.Apply(c =>
            {
                c.Profession = id;
                c.ProfessionVariant = null;
                c.ProfessionCombatTechniqueChoices.Clear();
            }));
    }

    [RelayCommand]
    private void ClearProfession() => _editor.Apply(c =>
    {
        c.Profession = null;
        c.ProfessionVariant = null;
        c.ProfessionCombatTechniqueChoices.Clear();
    });
}

public sealed class SpeciesOptionViewModel(EditorViewModel editor, SpeciesDefinition species, bool selected)
{
    public string Name => species.Name;

    public string Summary =>
        $"{species.Ap} AP · LeP {species.LifeBase} · SK {species.SpiritBase} · ZK {species.ToughnessBase} · GS {species.SpeedBase} · {species.AttributeChangesText}";

    /// <summary>RadioButton-Auswahl; setzt die Spezies und verwirft frühere Eigenschaftswahlen.</summary>
    public bool IsSelected
    {
        get => selected;
        set
        {
            if (!value || selected) return;
            editor.Apply(c =>
            {
                c.Species = species.Id;
                c.SpeciesAttributeChoices.Clear();
            });
        }
    }
}

public sealed record ChoiceOption(string Id, string Label);

/// <summary>Auswahl per ComboBox (Eigenschaftsänderung, Variante, Kampftechnik).</summary>
public sealed class ChoiceViewModel(string label, IReadOnlyList<ChoiceOption> options, string? selectedId, Action<string> onSelect)
{
    public string Label { get; } = label;

    public IReadOnlyList<ChoiceOption> Options { get; } = options;

    public bool IsOpen => Selected is null;

    public ChoiceOption? Selected
    {
        get => Options.FirstOrDefault(o => o.Id == selectedId);
        set
        {
            if (value is null || value.Id == selectedId) return;
            onSelect(value.Id);
        }
    }
}

public sealed partial class ExperienceLevelOptionViewModel(EditorViewModel editor, ExperienceLevel level) : ObservableObject
{
    private bool _isSelected;

    public ExperienceLevel Level => level;

    public string Name => level.Name;

    public string ApText => level.Ap.ToString("N0", CultureInfo.GetCultureInfo("de-DE")) + " AP";

    public string LimitsText =>
        $"Eigenschaft max. {level.MaxAttribute} · Summe max. {level.MaxAttributeTotal} · Fertigkeit max. {level.MaxSkill} · " +
        $"Kampftechnik max. {level.MaxCombatTechnique} · Zauber/Liturgien max. {level.MaxSpellsAndLiturgies} (davon fremd {level.MaxForeignSpells})";

    /// <summary>Auswahl über RadioButton; setzt den Erfahrungsgrad des Helden.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!value || _isSelected) return;
            editor.Apply(c => c.ExperienceLevel = level.Id);
        }
    }

    internal void SetSelected(bool selected)
    {
        if (_isSelected == selected) return;
        _isSelected = selected;
        OnPropertyChanged(nameof(IsSelected));
    }
}
