using System.Text;
using Avalonia.Headless.XUnit;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Rules;

namespace DSACharacterCreator.UiTests;

public sealed class PdfExportTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose() => _app.Dispose();

    private static readonly string? ShotDir = Environment.GetEnvironmentVariable("DSA_SHOTS");

    private EditorViewModelFixture Magician()
    {
        _app.List.NewCommand.Execute(null);
        var editor = _app.Editor;
        editor.Name = "Alrik/Sohn des Alrik";
        editor.Basics.SpeciesOptions.Single(s => s.Name == "Mensch").IsSelected = true;
        Templates.ChangeCulture(editor.Character.Creation, "culture.mittelreicher", TestShell.Rules);
        Templates.ChangeProfession(editor.Character.Creation, "profession.gildenloser-magier-nach-agrimeton", TestShell.Rules);
        Templates.ChooseProfessionSpell(editor.Character.Creation, 0, "cantrip.feuerfinger");
        editor.Refresh();
        editor.ChangeEquipment(list =>
        {
            list.Add(new EquipmentEntry { Id = "weapon.dolch", Equipped = true });
            list.Add(new EquipmentEntry { Id = "armor.stoffruestung", Equipped = true });
            list.Add(new EquipmentEntry { Id = "item.lederrucksack" });
        });
        editor.Basics.PersonalFields.Single(f => f.Label == "Alter").Value = "21";
        editor.Basics.Notes.Value = "Sucht seinen Lehrmeister in Punin.\nSchuldet dem Wirt der Goldenen Gans 3 Silbertaler.";
        return new EditorViewModelFixture(editor);
    }

    private sealed record EditorViewModelFixture(ViewModels.EditorViewModel Editor);

    [AvaloniaFact]
    public async Task ExportPdf_SavesPdfNamedAfterHero()
    {
        var editor = Magician().Editor;

        // Auch ohne geöffnete Bogenseite: der Bogen wird für den Export gebaut.
        await editor.Sheet.ExportPdfCommand.ExecuteAsync(null);

        Assert.Equal("AlrikSohn des Alrik.pdf", _app.Dialogs.LastSavedName);
        var pdf = Assert.IsType<byte[]>(_app.Dialogs.LastSavedBytes);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.Contains("Gespeichert", editor.Sheet.ExportStatus);
        if (ShotDir is not null)
        {
            Directory.CreateDirectory(ShotDir);
            await File.WriteAllBytesAsync(Path.Combine(ShotDir, "bogen.pdf"), pdf);
        }
    }

    [AvaloniaFact]
    public async Task ExportPdf_LongSheet_BreaksAcrossPages()
    {
        var editor = Magician().Editor;
        var spells = TestShell.Rules.Spells.Where(s => s.Group == Core.Content.SpellGroup.Spell).Take(60).ToList();
        editor.Apply(c => c.Spells.AddRange(spells.Select(s => new SpellEntry { Id = s.Id, Value = 4 })));
        var items = TestShell.Rules.Items.Take(80).ToList();
        editor.ChangeEquipment(list => list.AddRange(items.Select(i => new EquipmentEntry { Id = i.Id })));
        editor.Basics.Notes.Value = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"Notiz {i}: " + string.Concat(Enumerable.Repeat("lange Zeile ", 20))));

        await editor.Sheet.ExportPdfCommand.ExecuteAsync(null);

        var pdf = Encoding.Latin1.GetString(Assert.IsType<byte[]>(_app.Dialogs.LastSavedBytes));
        var pages = System.Text.RegularExpressions.Regex.Count(pdf, @"/Type /Page\b(?!s)");
        Assert.InRange(pages, 6, 12);
        if (ShotDir is not null) await File.WriteAllBytesAsync(Path.Combine(ShotDir, "bogen-lang.pdf"), _app.Dialogs.LastSavedBytes!);
    }

    [AvaloniaFact]
    public async Task ExportSpellsPdf_SeparateFileWithRuleTexts()
    {
        var editor = Magician().Editor;
        Assert.True(editor.Sheet.HasSpells);

        await editor.Sheet.ExportSpellsPdfCommand.ExecuteAsync(null);

        Assert.Equal("AlrikSohn des Alrik – Zauber und Liturgien.pdf", _app.Dialogs.LastSavedName);
        var pdf = Assert.IsType<byte[]>(_app.Dialogs.LastSavedBytes);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        if (ShotDir is not null) await File.WriteAllBytesAsync(Path.Combine(ShotDir, "zauber.pdf"), pdf);
    }
}
