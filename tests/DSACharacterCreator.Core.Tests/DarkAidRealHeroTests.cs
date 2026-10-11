using System.Text.Json.Nodes;
using DSACharacterCreator.Content;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.DarkAid;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

/// <summary>
/// Importiert echte Dark-Aid-Helden aus dem Ordner in <c>DSA_DARKAID</c> (ohne die Variable passiert nichts) und prüft,
/// dass Eigenschaften, Talente, Kampftechniken und Zauber dem Dark-Aid-Stand entsprechen und der Rundweg Export → Import
/// dieselben Werte ergibt.
/// </summary>
public class DarkAidRealHeroTests(ITestOutputHelper output)
{
    [Fact]
    public void ImportedValues_MatchDarkAid()
    {
        if (Environment.GetEnvironmentVariable("DSA_DARKAID") is not { Length: > 0 } dir) return;
        var rules = TestRules.Data;
        using var stream = EmbeddedContent.Open(DarkAidMapping.ResourcePath);
        var map = DarkAidMapping.Load(stream);
        var failures = new List<string>();
        foreach (var file in Directory.EnumerateFiles(dir, "*" + DarkAidImport.FileExtension).Order())
        {
            var json = File.ReadAllText(file);
            if (!DarkAidImport.IsDarkAidHero(json)) continue;
            var name = Path.GetFileName(file);
            var result = DarkAidImport.Read(json, rules, map, SourceSelection.Default(rules.Books));
            var state = new CharacterCalculator(rules).Calculate(result.Character);
            var hero = JsonNode.Parse(json)!;
            output.WriteLine($"== {name}: {result.Character.Phase}, AP {state.Ap.Total}/{state.Ap.Spent}");
            var ap = state.Ap;
            output.WriteLine($"   Spezies {ap.Species}, Eigenschaften {ap.Attributes}, Talente {ap.Skills}, Kampftechniken {ap.CombatTechniques}, " +
                             $"Vorteile {ap.Advantages}, Nachteile {ap.Disadvantages}, SF {ap.SpecialAbilities}, Energien {ap.Energies}, " +
                             $"Zauber {ap.Spells}, Liturgien {ap.Liturgies}");
            foreach (var t in state.Traits.Concat(state.SpecialAbilities).Where(t => t.Ap is null))
                output.WriteLine($"   Ohne AP-Wert: {t.Definition.Name} {t.Entry.Option} {t.Entry.Specification}");
            foreach (var note in result.Notes) output.WriteLine("   " + note);
            foreach (var issue in state.Issues.Where(i => i.Severity == IssueSeverity.Error))
                output.WriteLine("   Fehler: " + issue.Message);

            // Rundweg: Export und erneuter Import ergeben dieselben Werte und AP
            var export = DarkAidExport.Write(result.Character, rules, map);
            foreach (var note in export.Notes) output.WriteLine("   Export: " + note);
            var again = DarkAidImport.Read(export.Json, rules, map, SourceSelection.Default(rules.Books));
            var againState = new CharacterCalculator(rules).Calculate(again.Character);
            if (againState.Ap.Spent != state.Ap.Spent || againState.Ap.Total != state.Ap.Total)
                failures.Add($"{name}: Rundweg AP {againState.Ap.Total}/{againState.Ap.Spent} statt {state.Ap.Total}/{state.Ap.Spent}");
            string Entries(CharacterState s) => string.Join("; ", s.Traits.Concat(s.SpecialAbilities)
                .Select(t => $"{t.Definition.Id}|{t.Entry.Option}|{t.Entry.Specification}|{t.Level}|{t.Ap}").Order());
            if (Entries(againState) != Entries(state))
                failures.Add($"{name}: Rundweg Einträge\n  {Entries(state)}\n  {Entries(againState)}");
            string Spells(CharacterState s) => string.Join(",", s.Spells
                .Select(x => x.Definition.Id + x.Value + "+" + string.Join("+", x.Enhancements.Select(e => e.Definition.Id).Order())).Order());
            if (Spells(againState) != Spells(state)) failures.Add($"{name}: Rundweg Zauber\n  {Spells(state)}\n  {Spells(againState)}");
            // Eigene Gegenstände kennt Dark Aid nicht
            if (againState.Equipment.Money != state.Equipment.Money ||
                again.Character.Equipment.Count(e => e.Id != "") != result.Character.Equipment.Count(e => e.Id != ""))
                failures.Add($"{name}: Rundweg Ausrüstung/Geld");

            void Check(string what, int expected, int? actual)
            {
                if (expected != actual) failures.Add($"{name}: {what} {actual} statt {expected}");
            }

            foreach (var v in hero["attributes"]!.AsArray())
                Check((string)v!["id"]!, (int)v["level"]!, state.Attribute(map.Attributes[(string)v["id"]!]));
            foreach (var s in hero["skills"]!.AsArray())
                Check((string)s!["id"]!, (int)s["level"]!, state.Skills.First(x => x.Definition.Id == map.Skills[(string)s["id"]!]).Value);
            foreach (var ct in state.CombatTechniques)
            {
                if (map.CombatTechniques.FirstOrDefault(p => p.Value == ct.Definition.Id).Key is not { } key) continue;
                var level = hero["combattechniques"]?.AsArray().FirstOrDefault(c => (string?)c?["id"] == key)?["level"];
                Check(key, (int?)level ?? 6, ct.Value);
            }
            foreach (var group in new[] { "spells", "chants" })
                foreach (var s in hero[group]?.AsArray() ?? [])
                    if (map.Spells.TryGetValue((string)s!["id"]!, out var id) && s["level"] is { } level)
                        Check((string)s["id"]!, (int)level, state.Spells.FirstOrDefault(x => x.Definition.Id == id)?.Value);
        }
        foreach (var f in failures) output.WriteLine(f);
        Assert.Empty(failures);
    }
}
