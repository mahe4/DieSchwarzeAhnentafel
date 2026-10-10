using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Konflikt eines Protokolleintrags nach einer Korrektur der Erschaffung (PLAN.md Abschnitt 5.3).</summary>
public enum LogConflict
{
    None,
    /// <summary>Noch gültig, kostet aber anders als vor der Korrektur.</summary>
    CostChanged,
    /// <summary>Ausgangswert passt nicht mehr: verschieben (gleiche Anzahl Stufen) oder auf den Zielwert begrenzen.</summary>
    Shifted,
    /// <summary>Hinfällig (z. B. schon in der Erschaffung gekauft): löschen oder bewusst behalten.</summary>
    Invalid,
}

/// <param name="Ap">Kosten beim Abspielen (Differenz der ausgegebenen AP vor und nach dem Eintrag).</param>
/// <param name="CurrentValue">Wert bzw. Stufe vor dem Eintrag (bei Steigerungen und Stufenänderungen).</param>
/// <param name="PreviousAp">Kosten vor der Korrektur (nur bei <see cref="LogConflict.CostChanged"/>).</param>
public sealed record LogEntryState(LogEntry Entry, string Text, int Ap, LogConflict Conflict, string? ConflictText,
    int? CurrentValue, int? PreviousAp = null)
{
    /// <summary>Muss vor dem Übernehmen einer Korrektur aufgelöst werden.</summary>
    public bool NeedsResolution => Conflict is LogConflict.Shifted or LogConflict.Invalid && !Entry.Kept;
}

/// <param name="Working">Entscheidungen nach allen Einträgen (Grundlage für weitere Steigerungen).</param>
/// <param name="Final">Stand nach allen Einträgen.</param>
public sealed record AdvancementInfo(IReadOnlyList<LogEntryState> Entries, int AwardedAp, int ForfeitedAp,
    CreationLayer Working, CharacterState Final)
{
    public bool HasUnresolvedConflicts => Entries.Any(e => e.NeedsResolution);
}

/// <summary>Spielt das Steigerungsprotokoll auf einer Arbeitskopie der Erschaffung ab.</summary>
internal sealed class AdvancementReplay(CharacterCalculator calculator, RuleData rules)
{
    public CharacterState Run(Character character, CharacterState creationState)
    {
        var correcting = character.Phase == CharacterPhase.Creation;
        var info = Replay(character, character.Creation, character.Log, creationState);
        if (!correcting) return info.Final with { Advancement = info };

        // Korrektur: Kostenänderungen gegenüber dem Stand vor der Korrektur zeigen.
        if (character.CreationSnapshot is { } snapshot)
        {
            var before = Replay(character, snapshot.Creation, snapshot.Log,
                calculator.CalculateLayer(character, snapshot.Creation, false, 0, null));
            var previous = before.Entries.ToDictionary(e => e.Entry.Id, e => e.Ap);
            info = info with
            {
                Entries = info.Entries.Select(e => e.Conflict == LogConflict.None && previous.TryGetValue(e.Entry.Id, out var ap) && ap != e.Ap
                    ? e with { Conflict = LogConflict.CostChanged, PreviousAp = ap }
                    : e).ToList(),
            };
        }
        var conflicts = info.Entries.Where(e => e.NeedsResolution).Select(e => new Issue(IssueSeverity.Warning, "log.conflict",
            RuleScope.CreationOnly, $"Protokoll: {e.Text} – {e.ConflictText}", "log:" + e.Entry.Id));
        return creationState with { Advancement = info, Issues = [.. creationState.Issues, .. conflicts] };
    }

    private AdvancementInfo Replay(Character character, CreationLayer layer, IReadOnlyList<LogEntry> log, CharacterState creationState)
    {
        // Unveränderte Einträge vom Anfang des Protokolls aus dem Zwischenspeicher übernehmen; gerechnet wird erst ab der
        // ersten Änderung (beim normalen Steigern nur der letzte Eintrag).
        var key = ReplayCache.Key(character, layer);
        var cached = calculator.Cache.Get(key);
        var reuse = 0;
        while (reuse < log.Count && reuse < cached.Count && cached[reuse].Fingerprint == log[reuse].Fingerprint()) reuse++;

        var entries = new List<LogEntryState>();
        var steps = new List<ReplayStep>();
        CreationLayer working;
        CharacterState before;
        int bonus, awarded, forfeited;
        if (reuse > 0)
        {
            for (var i = 0; i < reuse; i++)
            {
                steps.Add(cached[i]);
                entries.Add(cached[i].State with { Entry = log[i] });
            }
            // Nur die Entscheidungen werden gemerkt (vollständige Zwischenstände belasten den Speicher): einmal neu berechnen
            var last = cached[reuse - 1];
            (working, bonus, awarded, forfeited) = (last.Working.Clone(), last.Bonus, last.Awarded, last.Forfeited);
            before = calculator.CalculateLayer(character, working, false, bonus, creationState);
        }
        else
        {
            working = layer.Clone();
            (bonus, awarded, forfeited) = (0, 0, 0);
            before = calculator.CalculateLayer(character, working, false, bonus, creationState);
        }

        foreach (var entry in log.Skip(reuse))
        {
            var conflict = LogConflict.None;
            string? conflictText = null;
            int? current = null;
            string text;
            void Conflict(LogConflict kind, string message) => (conflict, conflictText) = (kind, message);

            switch (entry)
            {
                case CreationFinishedEntry f:
                    bonus -= f.ForfeitedAp;
                    forfeited += f.ForfeitedAp;
                    text = "Erschaffung abgeschlossen" + (f.ForfeitedAp > 0 ? $", {f.ForfeitedAp} übrige AP verfallen" : "") +
                           (f.AcceptedErrors > 0 ? $" (trotz {f.AcceptedErrors} Fehler{(f.AcceptedErrors == 1 ? "" : "n")})" : "");
                    break;
                case ApAwardEntry a:
                    bonus += a.Amount;
                    awarded += a.Amount;
                    text = $"{a.Amount:+0;−0} AP" + (string.IsNullOrWhiteSpace(a.Note) ? "" : $" – {a.Note}");
                    break;
                case RaiseEntry r:
                    text = $"{RaiseName(r)}: {r.From} → {r.To}";
                    current = Value(before, r);
                    if (current is not { } now)
                    {
                        Conflict(LogConflict.Invalid, "Ziel ist in den Regeldaten nicht vorhanden.");
                        break;
                    }
                    var target = r.To;
                    if (now != r.From && !r.Kept)
                    {
                        Conflict(LogConflict.Shifted, $"Ausgangswert ist jetzt {now} statt {r.From}.");
                        target = now + (r.To - r.From); // Vorschau: gleiche Anzahl Stufen
                    }
                    if (target > now) Raise(working, r, now, target);
                    break;
                case AcquireEntry a:
                    var definition = Find(a.Kind, a.Entry.Id);
                    text = $"{KindName(a.Kind)} {Label(definition?.Name ?? a.Entry.Id, a.Entry)} erworben";
                    if (definition is null)
                    {
                        Conflict(LogConflict.Invalid, "ist in den Regeldaten nicht vorhanden.");
                        break;
                    }
                    var list = a.Kind == EntryKind.SpecialAbility ? working.SpecialAbilities : working.Traits;
                    if (!TraitRules.AllowsRepeatedOption(definition) && list.Any(e => e.Id == a.Entry.Id && e.Option == a.Entry.Option &&
                            e.Specification == a.Entry.Specification) && !a.Kept)
                    {
                        Conflict(LogConflict.Invalid, "ist bereits vorhanden (z. B. schon in der Erschaffung gewählt).");
                        break;
                    }
                    var acquired = a.Entry.Clone();
                    acquired.LogEntryId = a.Id;
                    acquired.Key = a.Id;
                    list.Add(acquired);
                    break;
                case LevelEntry l:
                    var levelDef = Find(l.Kind, l.EntryId);
                    text = $"{Label(levelDef?.Name ?? l.EntryId, null, l.Option)}: Stufe {TraitRules.Roman(l.From)} → {TraitRules.Roman(l.To)}";
                    var owned = (l.Kind == EntryKind.SpecialAbility ? working.SpecialAbilities : working.Traits)
                        .FirstOrDefault(e => e.Id == l.EntryId && e.Option == l.Option && e.LogEntryId is null);
                    if (owned is null || levelDef is null)
                    {
                        Conflict(LogConflict.Invalid, "Der Eintrag ist nicht mehr vorhanden.");
                        break;
                    }
                    current = TraitRules.Level(levelDef, owned);
                    var level = l.To;
                    if (current != l.From && !l.Kept)
                    {
                        Conflict(LogConflict.Shifted, $"Stufe ist jetzt {TraitRules.Roman(current.Value)} statt {TraitRules.Roman(l.From)}.");
                        level = Math.Max(1, current.Value + (l.To - l.From));
                    }
                    owned.Level = level;
                    break;
                case RemoveEntry rm:
                    var removeDef = Find(rm.Kind, rm.EntryId);
                    text = $"{KindName(rm.Kind)} {Label(removeDef?.Name ?? rm.EntryId, null, rm.Option)} " +
                           (rm.Kind == EntryKind.Disadvantage ? "abgebaut" : "abgelegt");
                    var removeList = rm.Kind == EntryKind.SpecialAbility ? working.SpecialAbilities : working.Traits;
                    var index = removeList.FindIndex(e => e.Id == rm.EntryId && e.Option == rm.Option && e.LogEntryId is null);
                    if (index < 0) Conflict(LogConflict.Invalid, "Der Eintrag ist nicht mehr vorhanden.");
                    else removeList.RemoveAt(index);
                    break;
                case LearnEntry learn:
                    var spell = rules.FindSpell(learn.SpellId);
                    text = $"{SpellKind(spell)} {spell?.Name ?? learn.SpellId} aktiviert";
                    if (spell is null)
                    {
                        Conflict(LogConflict.Invalid, "ist in den Regeldaten nicht vorhanden.");
                        break;
                    }
                    if (working.Spells.Any(e => e.Id == learn.SpellId) && !learn.Kept)
                    {
                        Conflict(LogConflict.Invalid, "ist bereits vorhanden (z. B. schon in der Erschaffung aktiviert).");
                        break;
                    }
                    working.Spells.Add(new SpellEntry
                    {
                        Id = learn.SpellId, Value = spell.HasValue ? 0 : null, LogEntryId = learn.Id, Key = learn.Id,
                    });
                    break;
                case NoteEntry n:
                    text = n.Text;
                    break;
                default:
                    text = entry.GetType().Name;
                    break;
            }

            var after = entry is NoteEntry ? before : calculator.CalculateLayer(character, working, false, bonus, creationState);
            var state = new LogEntryState(entry, text, after.Ap.Spent - before.Ap.Spent, conflict, conflictText, current);
            entries.Add(state);
            steps.Add(new ReplayStep(entry.Fingerprint(), state, working.Clone(), bonus, awarded, forfeited));
            before = after;
        }
        calculator.Cache.Set(key, steps);
        return new AdvancementInfo(entries, awarded, forfeited, working, before);
    }

    private TraitDefinition? Find(EntryKind kind, string id) =>
        kind == EntryKind.SpecialAbility ? rules.FindSpecialAbility(id) : rules.FindTrait(id);

    private string RaiseName(RaiseEntry r) => r.Kind switch
    {
        RaiseTarget.Attribute => rules.Attributes.FirstOrDefault(a => a.Id == r.Target)?.Name ?? r.Target,
        RaiseTarget.Skill => rules.FindSkill(r.Target)?.Name ?? r.Target,
        RaiseTarget.CombatTechnique => rules.FindCombatTechnique(r.Target)?.Name ?? r.Target,
        RaiseTarget.Spell => rules.FindSpell(r.Target)?.Name ?? r.Target,
        _ => r.Target switch
        {
            Energies.LifePoints => "Zugekaufte LeP",
            Energies.Astral => "Zugekaufte AsP",
            Energies.Karma => "Zugekaufte KaP",
            Energies.AstralRepurchased => "Zurückgekaufte AsP",
            Energies.KarmaRepurchased => "Zurückgekaufte KaP",
            Energies.AstralLost => "Permanent verlorene AsP",
            Energies.KarmaLost => "Permanent verlorene KaP",
            _ => r.Target,
        },
    };

    private static int? Value(CharacterState state, RaiseEntry r) => r.Kind switch
    {
        RaiseTarget.Attribute => state.Attributes.FirstOrDefault(a => a.Definition.Id == r.Target)?.Value,
        RaiseTarget.Skill => state.Skills.FirstOrDefault(a => a.Definition.Id == r.Target)?.Value,
        RaiseTarget.CombatTechnique => state.CombatTechniques.FirstOrDefault(a => a.Definition.Id == r.Target)?.Value,
        RaiseTarget.Spell => state.Spells.FirstOrDefault(a => a.Definition.Id == r.Target)?.Value,
        _ => state.Energies.FirstOrDefault(e => e.Id == r.Target) is { } e ? e.Value - e.Fixed : null,
    };

    private static void Raise(CreationLayer working, RaiseEntry r, int current, int target)
    {
        switch (r.Kind)
        {
            case RaiseTarget.Attribute:
                working.Attributes[r.Target] = target;
                break;
            case RaiseTarget.Skill:
                working.SkillAdjustments[r.Target] = working.SkillAdjustments.GetValueOrDefault(r.Target) + target - current;
                break;
            case RaiseTarget.CombatTechnique:
                working.CombatTechniqueAdjustments[r.Target] =
                    working.CombatTechniqueAdjustments.GetValueOrDefault(r.Target) + target - current;
                break;
            case RaiseTarget.Spell:
                if (working.Spells.FirstOrDefault(e => e.Id == r.Target) is { } spell) spell.Value = target;
                break;
            default:
                working.BoughtEnergies[r.Target] = target;
                break;
        }
    }

    private static string SpellKind(SpellDefinition? def) => def?.Group switch
    {
        SpellGroup.Ritual => "Ritual",
        SpellGroup.Cantrip => "Zaubertrick",
        SpellGroup.MagicalAction => def.Category,
        SpellGroup.Liturgy => "Liturgie",
        SpellGroup.Ceremony => "Zeremonie",
        SpellGroup.Blessing => "Segnung",
        _ => "Zauber",
    };

    private static string KindName(EntryKind kind) => kind switch
    {
        EntryKind.Advantage => "Vorteil",
        EntryKind.Disadvantage => "Nachteil",
        _ => "Sonderfertigkeit",
    };

    private string Label(string name, TraitEntry? entry, string? option = null)
    {
        option ??= entry?.Option;
        var optionName = option is null ? null
            : rules.FindSkill(option)?.Name ?? rules.FindCombatTechnique(option)?.Name ??
              rules.FindLanguage(option)?.Name ?? rules.FindScript(option)?.Name ?? option;
        var level = entry?.Level is { } l && l > 1 ? " " + TraitRules.Roman(l) : "";
        return name + level + (optionName is null ? "" : $" ({optionName})") +
               (entry?.Specification is { Length: > 0 } spec ? $" ({spec})" : "");
    }
}

/// <summary>Stand nach einem Protokolleintrag (für den Zwischenspeicher).</summary>
internal sealed record ReplayStep(string Fingerprint, LogEntryState State, CreationLayer Working,
    int Bonus, int Awarded, int Forfeited);

/// <summary>
/// Zwischenspeicher fürs Abspielen des Protokolls, je Ausgangslage (Erschaffungsschicht, Quellen, Phase). Die
/// Erschaffungsschicht geht auch als Objekt ein: Nach Rückgängig oder Laden haben die Einträge neue Kennungen
/// (<see cref="TraitEntry.Key"/>), dann wird neu gerechnet.
/// </summary>
internal sealed class ReplayCache
{
    private const int MaxKeys = 3; // Steigerung, Korrektur (aktuell und Sicherung)
    private readonly List<(string Key, List<ReplayStep> Steps)> _entries = [];

    public static string Key(Character character, CreationLayer layer) =>
        $"{character.Id}|{character.Phase}|{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(layer)}|" +
        $"{string.Join(",", character.EnabledBooks)}|{string.Join(",", character.KnownBooks)}|" +
        Persistence.CharacterSerializer.Fingerprint(layer);

    public IReadOnlyList<ReplayStep> Get(string key) => _entries.FirstOrDefault(e => e.Key == key).Steps ?? [];

    public void Set(string key, List<ReplayStep> steps)
    {
        _entries.RemoveAll(e => e.Key == key);
        _entries.Insert(0, (key, steps));
        if (_entries.Count > MaxKeys) _entries.RemoveAt(_entries.Count - 1);
    }
}
