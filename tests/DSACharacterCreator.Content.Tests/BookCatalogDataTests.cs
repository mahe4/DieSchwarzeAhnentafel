using DSACharacterCreator.Content;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.Content.Tests;

public class BookCatalogDataTests
{
    private static BookCatalog Load()
    {
        using var stream = EmbeddedContent.Open("books.json");
        return BookCatalog.Load(stream);
    }

    [Fact]
    public void BooksJson_IsEmbedded()
    {
        Assert.Contains("books.json", EmbeddedContent.List());
    }

    [Fact]
    public void BooksJson_LoadsAndContainsCoreRules()
    {
        var catalog = Load();

        Assert.NotNull(catalog.Find("grw"));
    }

    [Fact]
    public void Books_HaveRequiredFields()
    {
        foreach (var book in Load().Books)
        {
            Assert.False(string.IsNullOrWhiteSpace(book.Id));
            Assert.False(string.IsNullOrWhiteSpace(book.Name), $"Buch '{book.Id}' ohne Namen.");
            // Die Pseudo-Quelle „wiki“ (Wiki-Eintrag ohne Quellenangabe) hat keine Wiki-Schreibweise.
            if (book.Id != "wiki") Assert.NotEmpty(book.WikiNames);
        }
    }

    [Fact]
    public void WegeDerVereinigungen_IsMarkedAdult()
    {
        Assert.True(Load().Find("wdv")?.Adult);
    }
}

public class RuleDataTests
{
    private static readonly RuleData Data = RuleData.Load(EmbeddedContent.Open, EmbeddedContent.List());

    [Fact]
    public void LoadsAndValidates()
    {
        Assert.Equal(8, Data.Attributes.Count);
        Assert.Equal(7, Data.ExperienceLevels.Count);
        Assert.Equal(59, Data.Skills.Count);
        Assert.Equal(21, Data.CombatTechniques.Count);
    }

    [Fact]
    public void EveryDataFile_IsWellFormedJson()
    {
        foreach (var path in EmbeddedContent.List())
        {
            using var stream = EmbeddedContent.Open(path);
            using var doc = System.Text.Json.JsonDocument.Parse(stream);
        }
    }

    [Fact]
    public void CoreRulebook_HasFourteenCombatTechniques()
    {
        var core = new SourceSelection(["grw"]);
        Assert.Equal(14, Data.CombatTechniques.Count(c => core.IsAvailable(c.Publications)));
        Assert.Equal(59, Data.Skills.Count(s => core.IsAvailable(s.Publications)));
    }
}

public class OriginDataTests
{
    private static readonly RuleData Data = RuleData.Load(EmbeddedContent.Open, EmbeddedContent.List());

    [Fact]
    public void SpeciesMatchWikiOverview()
    {
        // Heldenerschaffung › Schritt 3, Übersichtstabelle
        var zwerg = Data.FindSpecies("species.zwerg")!;
        Assert.Equal((61, 8, -4, -4, 6), (zwerg.Ap, zwerg.LifeBase, zwerg.SpiritBase, zwerg.ToughnessBase, zwerg.SpeedBase));
        var elf = Data.FindSpecies("species.elf")!;
        Assert.Equal((18, 2, -4, -6, 8), (elf.Ap, elf.LifeBase, elf.SpiritBase, elf.ToughnessBase, elf.SpeedBase));
        Assert.Equal(10, Data.Species.Count);
    }

    /// <summary>Der Paketpreis entspricht den Talentkosten von 0 an – Gegenprobe für den Import.</summary>
    [Fact]
    public void CulturePackagePrices_MatchSkillCosts()
    {
        // Abweichungen, die so im Wiki stehen.
        var knownDifferences = new HashSet<string> { "culture.zahori" };
        foreach (var culture in Data.Cultures.Where(c => c.Package is not null && !knownDifferences.Contains(c.Id)))
        {
            var cost = culture.Package!.Skills.Sum(v =>
                Data.Costs.Cost(Data.Skills.Single(s => s.Id == v.Skill).ImprovementCost, 0, v.Value));
            Assert.True(cost == culture.Package.Ap, $"{culture.Name}: Paket {culture.Package.Ap} AP, Talente {cost} AP");
        }
    }

    [Fact]
    public void MostProfessionsAreFullyStructured()
    {
        Assert.True(Data.Professions.Count >= 290);
        Assert.True(Data.Professions.Count(p => p.Unparsed is { Count: > 0 }) <= 10);
        var streuner = Data.FindProfession("profession.streuner")!;
        Assert.Equal(212, streuner.Ap);
        Assert.Contains(streuner.Skills, s => s.Skill == "skill.gassenwissen" && s.Value == 7);
        Assert.Contains(streuner.CombatTechniqueChoices!, c => c.Options.Contains("ct.wurfwaffen") && c.Value == 10);
        Assert.Contains(streuner.Variants!, v => v.Name == "Einbrecher/in" && v.Ap == 221);
    }
}
