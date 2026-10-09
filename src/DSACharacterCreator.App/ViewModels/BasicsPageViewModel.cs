using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>Grundlagen: Erfahrungsgrad (Spezies, Kultur, Profession folgen mit Meilenstein 3).</summary>
public sealed class BasicsPageViewModel : ViewModelBase
{
    public BasicsPageViewModel(EditorViewModel editor)
    {
        ExperienceLevels = editor.Rules.ExperienceLevels.Select(l => new ExperienceLevelOptionViewModel(editor, l)).ToList();
    }

    public IReadOnlyList<ExperienceLevelOptionViewModel> ExperienceLevels { get; }

    public void Update(CharacterState state)
    {
        foreach (var option in ExperienceLevels) option.SetSelected(option.Level.Id == state.ExperienceLevel.Id);
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
