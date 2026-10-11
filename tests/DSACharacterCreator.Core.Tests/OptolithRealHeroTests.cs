using System.Text.Json.Nodes;
using DSACharacterCreator.Content;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.Core.Optolith;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.Core.Tests;

/// <summary>
/// Importiert echte Optolith-Helden aus dem Ordner in <c>DSA_OPTOLITH</c> (ohne die Variable passiert nichts) und prüft,
/// dass Eigenschaften, Talente, Kampftechniken und Zauber dem Optolith-Stand entsprechen.
/// </summary>
public class OptolithRealHeroTests(ITestOutputHelper output)
{
    [Fact]
    public void ImportedValues_MatchOptolith()
    {
        if (Environment.GetEnvironmentVariable("DSA_OPTOLITH") is not { Length: > 0 } dir) return;
        var rules = TestRules.Data;
        using var stream = EmbeddedContent.Open(OptolithMapping.ResourcePath);
        var map = OptolithMapping.Load(stream);
        var failures = new List<string>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            var json = File.ReadAllText(file);
            if (!OptolithImport.IsOptolithHero(json)) continue;
            var result = OptolithImport.Read(json, rules, map, SourceSelection.Default(rules.Books));
            var state = new CharacterCalculator(rules).Calculate(result.Character);
            var hero = JsonNode.Parse(json)!;
            output.WriteLine($"== {Path.GetFileName(file)}: {result.Character.Phase}, AP {state.Ap.Total}/{state.Ap.Spent}");
            foreach (var note in result.Notes) output.WriteLine("   " + note);

            // Rundweg: Export und erneuter Import ergeben dieselben Werte und AP
            var export = OptolithExport.Write(result.Character, rules, map);
            foreach (var note in export.Notes) output.WriteLine("   Export: " + note);
            var again = OptolithImport.Read(export.Json, rules, map, SourceSelection.Default(rules.Books));
            var againState = new CharacterCalculator(rules).Calculate(again.Character);
            if (againState.Ap.Spent != state.Ap.Spent || againState.Ap.Total != state.Ap.Total)
                failures.Add($"{Path.GetFileName(file)}: Rundweg AP {againState.Ap.Total}/{againState.Ap.Spent} statt {state.Ap.Total}/{state.Ap.Spent}");
            string Entries(CharacterState s) => string.Join("; ", s.Traits.Concat(s.SpecialAbilities)
                .Select(t => $"{t.Definition.Id}|{t.Entry.Option}|{t.Entry.Specification}|{t.Level}|{t.Ap}").Order());
            if (Entries(againState) != Entries(state))
                failures.Add($"{Path.GetFileName(file)}: Rundweg Einträge\n  {Entries(state)}\n  {Entries(againState)}");
            if (string.Join(",", againState.Spells.Select(x => x.Definition.Id + x.Value).Order()) !=
                string.Join(",", state.Spells.Select(x => x.Definition.Id + x.Value).Order()))
                failures.Add($"{Path.GetFileName(file)}: Rundweg Zauber");
            if (againState.Equipment.Money != state.Equipment.Money || again.Character.Equipment.Count != result.Character.Equipment.Count)
                failures.Add($"{Path.GetFileName(file)}: Rundweg Ausrüstung/Geld");
            foreach (var issue in state.Issues.Where(i => i.Severity == IssueSeverity.Error))
                output.WriteLine("   Fehler: " + issue.Message);

            void Check(string what, int expected, int? actual)
            {
                if (expected != actual) failures.Add($"{Path.GetFileName(file)}: {what} {actual} statt {expected}");
            }

            foreach (var v in hero["attr"]!["values"]!.AsArray())
            {
                var (id, value) = v is JsonArray a ? ((string)a[0]!, (int)a[1]!) : ((string)v!["id"]!, (int)v["value"]!);
                Check(id, value, state.Attribute(map.Attributes[id]));
            }
            foreach (var (key, value) in hero["talents"]!.AsObject())
                Check(key, (int)value!, state.Skills.First(s => s.Definition.Id == map.Skills[key]).Value);
            foreach (var ct in state.CombatTechniques)
            {
                var key = map.CombatTechniques.First(p => p.Value == ct.Definition.Id).Key;
                Check(key, (int?)hero["ct"]![key] ?? 6, ct.Value);
            }
            foreach (var group in new[] { "spells", "liturgies" })
                foreach (var (key, value) in hero[group]!.AsObject())
                    if (map.Spells.TryGetValue(key, out var id))
                        Check(key, (int)value!, state.Spells.FirstOrDefault(s => s.Definition.Id == id)?.Value);
        }
        foreach (var f in failures) output.WriteLine(f);
        Assert.Empty(failures);
    }
}
