using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Persistence;

namespace DSACharacterCreator.Core.Rules;

/// <summary>Erschaffung abschließen, korrigieren und Steigerungen protokollieren (PLAN.md Abschnitt 5).</summary>
public static class Phases
{
    /// <summary>Was beim Abschluss passiert: verfallende AP (über 10 übrige) und bewusst akzeptierte Fehler.</summary>
    public sealed record FinishSummary(int Remaining, int Forfeited, int Errors, int Warnings, bool Overspent);

    public static FinishSummary Summarize(CharacterState state, RuleData rules)
    {
        var remaining = state.Ap.Remaining;
        // Fehler aus dem Protokoll (Konflikte) zählen nicht; die müssen vorher aufgelöst sein.
        var issues = state.Issues.Where(i => i.Code != "log.conflict").ToList();
        return new FinishSummary(remaining, Math.Max(0, remaining - rules.Creation.MaxLeftoverAp),
            issues.Count(i => i.Severity == IssueSeverity.Error), issues.Count(i => i.Severity == IssueSeverity.Warning),
            remaining < 0);
    }

    /// <summary>
    /// Erschaffung abschließen bzw. eine Korrektur übernehmen. Übrige AP über 10 verfallen (Heldenerschaffung, Schritt 11).
    /// </summary>
    public static void FinishCreation(Character character, CharacterState state, RuleData rules)
    {
        var summary = Summarize(state, rules);
        if (character.IsCorrectingCreation)
        {
            var finished = character.Log.OfType<CreationFinishedEntry>().FirstOrDefault();
            if (finished is null) character.Log.Insert(0, finished = new CreationFinishedEntry());
            finished.ForfeitedAp = summary.Forfeited;
            finished.AcceptedErrors = summary.Errors;
            character.Log.Add(new NoteEntry { Text = "Erschaffung korrigiert" });
            character.CreationSnapshot = null;
        }
        else
        {
            character.Log.Insert(0, new CreationFinishedEntry { ForfeitedAp = summary.Forfeited, AcceptedErrors = summary.Errors });
        }
        character.Phase = CharacterPhase.Advancement;
    }

    /// <summary>Zurück in die Erschaffung; die Sicherung erlaubt das Verwerfen der Korrektur.</summary>
    public static void StartCorrection(Character character)
    {
        if (character.Phase != CharacterPhase.Advancement) return;
        character.CreationSnapshot = new CreationSnapshot
        {
            Creation = character.Creation.Clone(),
            Log = CharacterSerializer.Clone(character.Log),
        };
        character.Phase = CharacterPhase.Creation;
    }

    public static void DiscardCorrection(Character character)
    {
        if (character.CreationSnapshot is not { } snapshot) return;
        character.Creation = snapshot.Creation;
        character.Log = snapshot.Log;
        character.CreationSnapshot = null;
        character.Phase = CharacterPhase.Advancement;
    }

    public static void AwardAp(Character character, int amount, string? note) =>
        character.Log.Add(new ApAwardEntry { Amount = amount, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() });

    public static void DeleteEntry(Character character, Guid id) => character.Log.RemoveAll(e => e.Id == id);

    /// <summary>Eintrag trotz Konflikt behalten (Hausregel, SL-Erlaubnis).</summary>
    public static void KeepEntry(Character character, Guid id)
    {
        if (character.Log.FirstOrDefault(e => e.Id == id) is { } entry) entry.Kept = true;
    }

    /// <summary>Wertverschiebung: gleiche Anzahl Stufen ab dem jetzigen Wert.</summary>
    public static void ShiftEntry(Character character, Guid id, int current)
    {
        switch (character.Log.FirstOrDefault(e => e.Id == id))
        {
            case RaiseEntry r:
                (r.From, r.To) = (current, current + r.To - r.From);
                break;
            case LevelEntry l:
                (l.From, l.To) = (current, Math.Max(1, current + l.To - l.From));
                break;
        }
    }

    /// <summary>Wertverschiebung: auf den bisherigen Zielwert begrenzen; ist er schon erreicht, entfällt der Eintrag.</summary>
    public static void CapEntry(Character character, Guid id, int current)
    {
        switch (character.Log.FirstOrDefault(e => e.Id == id))
        {
            case RaiseEntry r when r.To > current:
                r.From = current;
                break;
            case LevelEntry l when l.To != current:
                l.From = current;
                break;
            case { } entry:
                character.Log.Remove(entry);
                break;
        }
    }

    /// <summary>Ändern sich Vor- oder Nachteile? Das ist nach der Erschaffung nur mit Erlaubnis der Spielleitung möglich.</summary>
    public static bool NeedsGmPermission(CreationLayer before, CreationLayer after)
    {
        var old = before.Traits.ToDictionary(t => t.Key);
        return after.Traits.Count != before.Traits.Count ||
               after.Traits.Any(t => !old.TryGetValue(t.Key, out var o) || o.Level != t.Level);
    }

    /// <summary>
    /// Überträgt eine Änderung an der Arbeitskopie (<see cref="AdvancementInfo.Working"/>) als Einträge ins Protokoll.
    /// Senken nimmt nur eigene Steigerungen zurück; Einträge aus der Erschaffung lassen sich nicht zurückgeben, außer
    /// Nachteile (abbauen).
    /// </summary>
    public static void Record(Character character, CharacterState state, CreationLayer after, RuleData rules)
    {
        var info = state.Advancement ?? throw new InvalidOperationException("Kein Protokoll vorhanden.");
        var before = info.Working;
        var log = character.Log;
        var start = rules.Creation.AttributeStartValue;

        foreach (var def in rules.Attributes)
            Value(RaiseTarget.Attribute, def.Id, before.Attributes.GetValueOrDefault(def.Id, start),
                after.Attributes.GetValueOrDefault(def.Id, start));
        foreach (var skill in state.Skills)
        {
            var delta = after.SkillAdjustments.GetValueOrDefault(skill.Definition.Id) - before.SkillAdjustments.GetValueOrDefault(skill.Definition.Id);
            if (delta != 0) Value(RaiseTarget.Skill, skill.Definition.Id, skill.Value, skill.Value + delta);
        }
        foreach (var ct in state.CombatTechniques)
        {
            var delta = after.CombatTechniqueAdjustments.GetValueOrDefault(ct.Definition.Id) -
                        before.CombatTechniqueAdjustments.GetValueOrDefault(ct.Definition.Id);
            if (delta != 0) Value(RaiseTarget.CombatTechnique, ct.Definition.Id, ct.Value, ct.Value + delta);
        }
        foreach (var id in new[] { Energies.LifePoints, Energies.Astral, Energies.Karma, Energies.AstralRepurchased,
                     Energies.KarmaRepurchased, Energies.AstralLost, Energies.KarmaLost })
            Value(RaiseTarget.Energy, id, before.BoughtEnergies.GetValueOrDefault(id), after.BoughtEnergies.GetValueOrDefault(id));

        // Zauber: aktivieren, steigern; selbst aktivierte lassen sich wieder entfernen (Einträge aus der Erschaffung nicht)
        var oldSpells = before.Spells.ToDictionary(e => e.Key);
        foreach (var removed in before.Spells.Where(e => after.Spells.All(n => n.Key != e.Key)))
            if (removed.LogEntryId is { } learnId)
                log.RemoveAll(e => e.Id == learnId || e is RaiseEntry { Kind: RaiseTarget.Spell } r && r.Target == removed.Id ||
                                   e is LearnEntry { Enhancement: not null } l && l.SpellId == removed.Id);
        foreach (var spell in after.Spells)
        {
            if (!oldSpells.TryGetValue(spell.Key, out var previous))
            {
                log.Add(new LearnEntry { SpellId = spell.Id });
                if (spell.Value is > 0 and var v) log.Add(new RaiseEntry { Kind = RaiseTarget.Spell, Target = spell.Id, From = 0, To = v });
                Enhancements(spell, []);
                continue;
            }
            if (spell.Value is { } to && previous.Value is { } from && to != from) Value(RaiseTarget.Spell, spell.Id, from, to);
            Enhancements(spell, previous.Enhancements);
        }

        // Zauber-/Liturgieerweiterungen: erlernen; selbst erlernte lassen sich wieder entfernen (die der Erschaffung nicht)
        void Enhancements(SpellEntry spell, List<string> old)
        {
            foreach (var removed in old.Except(spell.Enhancements))
                log.RemoveAll(e => e is LearnEntry l && l.SpellId == spell.Id && l.Enhancement == removed);
            foreach (var added in spell.Enhancements.Except(old))
                log.Add(new LearnEntry { SpellId = spell.Id, Enhancement = added });
        }

        Entries(before.Traits, after.Traits, false);
        Entries(before.SpecialAbilities, after.SpecialAbilities, true);

        void Value(RaiseTarget kind, string id, int from, int to)
        {
            if (to > from)
            {
                if (log.LastOrDefault() is RaiseEntry last && last.Kind == kind && last.Target == id && last.To == from) last.To = to;
                else log.Add(new RaiseEntry { Kind = kind, Target = id, From = from, To = to });
                return;
            }
            // Senken: jüngste eigene Steigerungen zurücknehmen, nie unter den Stand der Erschaffung
            var remaining = from;
            while (remaining > to &&
                   log.LastOrDefault(e => e is RaiseEntry r && r.Kind == kind && r.Target == id) is RaiseEntry raise)
            {
                if (raise.From >= to)
                {
                    log.Remove(raise);
                    remaining = raise.From;
                }
                else
                {
                    raise.To = to;
                    remaining = to;
                }
            }
        }

        void Entries(List<TraitEntry> old, List<TraitEntry> now, bool specialAbility)
        {
            EntryKind Kind(TraitEntry e) => specialAbility ? EntryKind.SpecialAbility
                : rules.FindTrait(e.Id)?.IsDisadvantage == true ? EntryKind.Disadvantage : EntryKind.Advantage;
            var oldByKey = old.ToDictionary(e => e.Key);
            var nowKeys = now.Select(e => e.Key).ToHashSet();

            foreach (var removed in old.Where(e => !nowKeys.Contains(e.Key)))
            {
                if (removed.LogEntryId is { } logId) log.RemoveAll(e => e.Id == logId);
                else if (Kind(removed) == EntryKind.Disadvantage)
                    log.Add(new RemoveEntry { Kind = EntryKind.Disadvantage, EntryId = removed.Id, Option = removed.Option });
            }
            foreach (var entry in now)
            {
                if (!oldByKey.TryGetValue(entry.Key, out var previous))
                {
                    var stored = entry.Clone();
                    stored.LogEntryId = null;
                    stored.Source = null;
                    log.Add(new AcquireEntry { Kind = Kind(entry), Entry = stored });
                    continue;
                }
                if (entry.LogEntryId is { } id)
                {
                    if (log.FirstOrDefault(e => e.Id == id) is AcquireEntry acquire)
                    {
                        var stored = entry.Clone();
                        stored.LogEntryId = null;
                        acquire.Entry = stored;
                    }
                    continue;
                }
                // Eintrag aus der Erschaffung: nur die Stufe lässt sich ändern
                int from = previous.Level ?? 1, to = entry.Level ?? 1;
                if (from == to) continue;
                var kind = Kind(entry);
                if (log.LastOrDefault() is LevelEntry last && last.Kind == kind && last.EntryId == entry.Id &&
                    last.Option == entry.Option && last.To == from)
                {
                    last.To = to;
                    if (last.To == last.From) log.Remove(last);
                }
                else log.Add(new LevelEntry { Kind = kind, EntryId = entry.Id, Option = entry.Option, From = from, To = to });
            }
        }
    }
}
