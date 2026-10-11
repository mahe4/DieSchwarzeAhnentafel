using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.Core.Content;
using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.UiTests;

public sealed class SourcesTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose() => _app.Dispose();

    /// <summary>Ein Vorteil, der nur in einer einzigen Publikation außerhalb des Regelwerks steht.</summary>
    private static TraitDefinition ExclusiveAdvantage => TestShell.Rules.Advantages.First(a =>
        a.Publications.Select(p => p.Book).Distinct().Count() == 1 && a.Publications[0].Book != "grw");

    private SourceBookViewModel Book(SourcesViewModel sources, string id) => sources.Books.Single(b => b.Book.Id == id);

    [Fact]
    public void Basics_ShowsActiveSourcesAndOpensSelection()
    {
        _app.List.NewCommand.Execute(null);
        var basics = _app.Editor.Basics;

        Assert.StartsWith($"{TestShell.Rules.Books.Books.Count - 1} von {TestShell.Rules.Books.Books.Count}", basics.SourcesText);
        basics.OpenSourcesCommand.Execute(null);

        var sources = Assert.IsType<SourcesViewModel>(_app.Editor.Sources);
        Assert.True(Book(sources, "grw").IsEnabled);
        Assert.False(Book(sources, "wdv").IsEnabled);
        Assert.True(Book(sources, "wdv").IsAdult);
        Assert.Contains("Einträge", Book(sources, "grw").Detail);
    }

    [Fact]
    public void DisablingBook_KeepsChoiceAndWarns_UndoRestores()
    {
        _app.List.NewCommand.Execute(null);
        var advantage = ExclusiveAdvantage;
        var book = advantage.Publications[0].Book;
        _app.Editor.Mutate(() => _app.Editor.Character.Creation.Traits.Add(new TraitEntry { Id = advantage.Id }));
        _app.Editor.Basics.OpenSourcesCommand.Execute(null);
        var sources = _app.Editor.Sources!;
        Assert.Contains(advantage.Name, Book(sources, book).UsedText);

        Book(sources, book).IsEnabled = false;

        Assert.DoesNotContain(book, _app.Editor.Character.EnabledBooks);
        Assert.Contains(_app.Editor.Character.Creation.Traits, t => t.Id == advantage.Id);   // nicht gesperrt
        Assert.Contains(_app.Editor.State.Issues, i => i.Code == "source.disabled");
        Assert.True(Book(sources, book).IsWarning);
        Assert.Contains(advantage.Name, sources.WarningText);
        Assert.NotEmpty(_app.Editor.Basics.SourcesWarning);

        _app.Editor.UndoCommand.Execute(null);
        Assert.Contains(book, _app.Editor.Character.EnabledBooks);
    }

    [Fact]
    public void Presets_CoreRulesAndAllIncludingAdult()
    {
        _app.List.NewCommand.Execute(null);
        _app.Editor.Basics.OpenSourcesCommand.Execute(null);
        var sources = _app.Editor.Sources!;

        sources.UseCoreRulesCommand.Execute(null);
        Assert.Equal(["grw", "wiki"], _app.Editor.Character.EnabledBooks.Order());
        Assert.Equal($"2 von {sources.Books.Count} Publikationen aktiv", sources.SummaryText);

        sources.UseAllCommand.Execute(null);
        Assert.Contains("wdv", _app.Editor.Character.EnabledBooks);
        Assert.Contains("Ü18", _app.Editor.Basics.SourcesText);
    }

    [Fact]
    public void Search_FiltersBooks_BackClosesOverlay()
    {
        _app.List.NewCommand.Execute(null);
        _app.Editor.Basics.OpenSourcesCommand.Execute(null);
        var sources = _app.Editor.Sources!;

        sources.Search = "Rüstkammer";

        var books = sources.Rows.OfType<SourceBookViewModel>().ToList();
        Assert.NotEmpty(books);
        Assert.All(books, b => Assert.Contains("Rüstkammer", b.Name));
        Assert.True(_app.Shell.GoBack());
        Assert.Null(_app.Editor.Sources);
    }

    [Fact]
    public void DefaultSources_AreSavedAndUsedForNewHeroes()
    {
        _app.List.OpenSourcesCommand.Execute(null);
        var sources = _app.List.Sources!;
        Book(sources, "wdv").IsEnabled = true;
        Book(sources, "aventurisches-kompendium").IsEnabled = false;
        Assert.True(_app.Shell.GoBack());
        Assert.Null(_app.List.Sources);

        _app.List.NewCommand.Execute(null);

        Assert.Contains("wdv", _app.Editor.Character.EnabledBooks);
        Assert.DoesNotContain("aventurisches-kompendium", _app.Editor.Character.EnabledBooks);
        Assert.True(File.Exists(_app.Shell.Settings.Path));
    }
}
