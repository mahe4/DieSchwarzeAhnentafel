using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.ViewModels;

/// <summary>
/// Phase des Helden (PLAN.md Abschnitt 5): Erschaffung abschließen, AP gutschreiben, Steigerungsprotokoll,
/// Erschaffung korrigieren mit Konfliktauflösung.
/// </summary>
public sealed partial class PhaseViewModel(EditorViewModel editor) : ViewModelBase
{
    private CharacterState? _state;

    private Character Character => editor.Character;

    [ObservableProperty]
    public partial bool IsCreation { get; private set; }

    [ObservableProperty]
    public partial bool IsCorrecting { get; private set; }

    [ObservableProperty]
    public partial bool IsAdvancement { get; private set; }

    [ObservableProperty]
    public partial string RemainingText { get; private set; } = "";

    /// <summary>Protokoll, neueste Einträge zuerst.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<LogRowViewModel> Log { get; private set; } = [];

    [ObservableProperty]
    public partial bool HasLog { get; private set; }

    [ObservableProperty]
    public partial string? ConflictText { get; private set; }

    [ObservableProperty]
    public partial string AwardAmount { get; set; } = "";

    [ObservableProperty]
    public partial string AwardNote { get; set; } = "";

    public void Update(CharacterState state)
    {
        _state = state;
        IsCorrecting = Character.IsCorrectingCreation;
        IsCreation = Character.Phase == CharacterPhase.Creation && !IsCorrecting;
        IsAdvancement = Character.Phase == CharacterPhase.Advancement;
        RemainingText = $"{state.Ap.Remaining} AP verfügbar";
        var entries = state.Advancement?.Entries ?? [];
        Log = entries.Reverse().Select(e => new LogRowViewModel(this, e)).ToList();
        HasLog = Log.Count > 0;
        var open = entries.Count(e => e.NeedsResolution);
        ConflictText = open switch
        {
            0 => null,
            1 => "1 Protokolleintrag passt nicht mehr zur korrigierten Erschaffung. Bitte auflösen.",
            _ => $"{open} Protokolleinträge passen nicht mehr zur korrigierten Erschaffung. Bitte auflösen.",
        };
        FinishCommand.NotifyCanExecuteChanged();
    }

    private bool CanFinish() => _state?.Advancement?.HasUnresolvedConflicts != true;

    /// <summary>Erschaffung abschließen bzw. Korrektur übernehmen (Heldenerschaffung, Schritt 11).</summary>
    [RelayCommand(CanExecute = nameof(CanFinish))]
    private void Finish()
    {
        if (_state is not { } state || !CanFinish()) return;
        var summary = Phases.Summarize(state, editor.Rules);
        var max = editor.Rules.Creation.MaxLeftoverAp;
        var lines = new List<string>();
        if (summary.Overspent)
            lines.Add($"Das AP-Konto ist um {-summary.Remaining} AP überzogen. Ein Minus lässt sich nicht mitnehmen.");
        else if (summary.Forfeited > 0)
            lines.Add($"Übrig sind {summary.Remaining} AP. Höchstens {max} AP dürfen zurückbehalten werden, {summary.Forfeited} AP verfallen.");
        else
            lines.Add($"Übrig sind {summary.Remaining} AP; sie bleiben für spätere Steigerungen erhalten.");
        if (summary.Errors > 0 || summary.Warnings > 0)
            lines.Add($"Offen sind noch {summary.Errors} Fehler und {summary.Warnings} Warnungen (siehe Hinweise)." +
                      (summary.Errors > 0 ? " Trotzdem abschließen nur als Hausregel oder mit Erlaubnis der Spielleitung." : ""));
        lines.Add(IsCorrecting
            ? "Die korrigierte Erschaffung gilt danach, das Steigerungsprotokoll wird darauf weiter abgespielt."
            : "Danach werden Änderungen als Steigerungen protokolliert. Über „Erschaffung korrigieren“ lässt sich die Erschaffung später noch ändern.");
        var title = IsCorrecting ? "Korrektur übernehmen" : "Erschaffung abschließen";
        var confirm = summary.Errors > 0 || summary.Overspent ? "Trotzdem abschließen" : IsCorrecting ? "Übernehmen" : "Abschließen";
        editor.Confirm(title, string.Join(Environment.NewLine + Environment.NewLine, lines), confirm,
            () => editor.Mutate(() => Phases.FinishCreation(Character, state, editor.Rules)));
    }

    [RelayCommand]
    private void StartCorrection() => editor.Confirm("Erschaffung korrigieren",
        "Die Erschaffung wird wieder bearbeitbar, mit ihren Grenzen. Die Steigerungen bleiben erhalten und werden danach " +
        "weiter abgespielt; passt ein Eintrag nicht mehr, wird er angezeigt. „Korrektur verwerfen“ stellt den jetzigen Stand wieder her.",
        "Korrigieren", () => editor.Mutate(() => Phases.StartCorrection(Character)));

    [RelayCommand]
    private void DiscardCorrection() => editor.Confirm("Korrektur verwerfen",
        "Alle Änderungen seit „Erschaffung korrigieren“ gehen verloren.", "Verwerfen",
        () => editor.Mutate(() => Phases.DiscardCorrection(Character)));

    /// <summary>AP-Gutschrift, z. B. nach einem Abenteuer.</summary>
    [RelayCommand]
    private void Award()
    {
        if (!int.TryParse(AwardAmount.Replace("–", "-").Trim(), out var amount) || amount == 0) return;
        var note = AwardNote;
        editor.Mutate(() => Phases.AwardAp(Character, amount, note));
        AwardAmount = "";
        AwardNote = "";
    }

    internal void Resolve(Action<Character> action) => editor.Mutate(() => action(Character));
}

/// <summary>Ein Protokolleintrag mit Kosten und gegebenenfalls Konflikt samt Lösungen.</summary>
public sealed partial class LogRowViewModel(PhaseViewModel phase, LogEntryState state)
{
    public string Text => state.Text;

    public string DateText => state.Entry.Date.ToLocalTime().ToString("dd.MM.yyyy");

    public string ApText => state.Entry switch
    {
        ApAwardEntry a => $"{a.Amount:+0;−0} AP",
        CreationFinishedEntry f => f.ForfeitedAp > 0 ? $"−{f.ForfeitedAp} AP" : "",
        _ => (state.Ap switch { > 0 => $"{state.Ap} AP", < 0 => $"+{-state.Ap} AP", _ => "" }) +
             (state.PreviousAp is { } before ? $" (vorher {before} AP)" : ""),
    };

    public string? ConflictText => state.Conflict switch
    {
        LogConflict.CostChanged => "Kostet nach der Korrektur anders.",
        LogConflict.None => null,
        _ => state.Entry.Kept ? $"{state.ConflictText} (bewusst behalten)" : state.ConflictText,
    };

    public bool HasConflict => ConflictText is not null;

    public bool NeedsResolution => state.NeedsResolution;

    public bool CanShift => NeedsResolution && state.Conflict == LogConflict.Shifted;

    public bool CanDelete => NeedsResolution || state.Entry is ApAwardEntry;

    public bool HasActions => NeedsResolution || CanDelete;

    [RelayCommand]
    private void Shift() => phase.Resolve(c => Phases.ShiftEntry(c, state.Entry.Id, state.CurrentValue ?? 0));

    [RelayCommand]
    private void Cap() => phase.Resolve(c => Phases.CapEntry(c, state.Entry.Id, state.CurrentValue ?? 0));

    [RelayCommand]
    private void Keep() => phase.Resolve(c => Phases.KeepEntry(c, state.Entry.Id));

    [RelayCommand]
    private void Delete() => phase.Resolve(c => Phases.DeleteEntry(c, state.Entry.Id));
}
