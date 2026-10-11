using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DSACharacterCreator.Core.Characters;
using DSACharacterCreator.ViewModels;
using DSACharacterCreator.Views;

namespace DSACharacterCreator.UiTests;

/// <summary>Bereich „Tiergefährten“: Liste, geöffnetes Tier mit eigenen Tabs, Steigern und Erwerben im Protokoll des Tiers.</summary>
public sealed class CompanionPageTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose() => _app.Dispose();

    private static readonly string? ShotDir = Environment.GetEnvironmentVariable("DSA_SHOTS");

    private EditorViewModel Editor => _app.Editor;

    private void Pick(string name) =>
        Editor.Picker!.Groups.SelectMany(g => g.Items).First(i => i.Name == name).PickCommand!.Execute(null);

    private void Add(string name) =>
        Editor.Picker!.Groups.SelectMany(g => g.Items).First(i => i.Name == name).AddCommand!.Execute(null);

    /// <summary>Hexe mit Katze als Vertrauter und einem Hund als Begleiter, nach der Erschaffung mit AP-Gutschrift.</summary>
    internal static void BuildHeroWithAnimals(TestShell app)
    {
        app.List.NewCommand.Execute(null);
        var editor = app.Editor;
        editor.Name = "Hesindiane";
        editor.Apply(c =>
        {
            c.SpecialAbilities.Add(new TraitEntry { Id = "sf.tradition-hexen" });
            c.SpecialAbilities.Add(new TraitEntry { Id = "sf.vertrautenbindung" });
        });
        editor.Mutate(() =>
        {
            editor.Character.Phase = CharacterPhase.Advancement;
            editor.Character.Log.Add(new CreationFinishedEntry());
            editor.Character.Log.Add(new ApAwardEntry { Amount = 200, Note = "Die Wölfe von Firunen" });
        });

        editor.Companions.AddFamiliarCommand.Execute(null);
        editor.Picker!.Groups.SelectMany(g => g.Items).First(i => i.Id == "animal.scheunenkatze").PickCommand!.Execute(null);
        var cat = editor.ActiveCompanion!;
        cat.Basics.Name = "Mira";
        cat.Change(c => c.BoundAfter = null);
        cat.Values.Attributes.First(r => r.Id == "MU").IncreaseCommand.Execute(null);
        cat.FamiliarTricks.Groups[0].AddCommand.Execute(null);
        editor.Picker!.Groups.SelectMany(g => g.Items).First(i => i.Id == "familiar-trick.streicheleinheiten").AddCommand!.Execute(null);
        editor.ClosePicker();
        editor.CloseCompanion();

        editor.Companions.AddCompanionCommand.Execute(null);
        editor.Picker!.Groups.SelectMany(g => g.Items).First(i => i.Id == "animal.bornlaender").PickCommand!.Execute(null);
        var dog = editor.ActiveCompanion!;
        dog.Basics.Name = "Brandt";
        dog.Change(c => c.BoundAfter = null);
        dog.Log.AwardAmount = "20";
        dog.Log.AwardNote = "Startausbildung";
        dog.Log.AwardCommand.Execute(null);
        dog.Training.Groups[0].AddCommand.Execute(null);
        editor.Picker!.Groups.SelectMany(g => g.Items).First(i => i.Id == "animal-training.wachtier").AddCommand!.Execute(null);
        editor.ClosePicker();
        dog.Skills.Loyalty[0].IncreaseCommand.Execute(null);
        editor.CloseCompanion();
    }

    private static Window Show(MainViewModel shell, double width, double height = 860)
    {
        var window = new Window { Width = width, Height = height, Content = new MainView { DataContext = shell } };
        window.Show();
        Settle();
        return window;
    }

    private static void Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static void Shot(Window window, string name)
    {
        if (ShotDir is null) return;
        Directory.CreateDirectory(ShotDir);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.CaptureRenderedFrame()!.Save(Path.Combine(ShotDir, $"companion-{name.Replace("/", "")}.png"), new PngBitmapEncoderOptions());
    }

    [AvaloniaFact]
    public void List_FamiliarFirst_OpenReplacesNavigation_BackReturns()
    {
        BuildHeroWithAnimals(_app);
        var editor = Editor;
        editor.SelectedSection = editor.Sections.First(s => s.Page == editor.Companions);
        var window = Show(_app.Shell, 1400);
        Shot(window, "liste-breit");

        var cards = editor.Companions.Cards;
        Assert.Equal(["Mira", "Brandt"], cards.Select(c => c.Name));
        Assert.True(cards[0].IsFamiliar);
        // 200 AP vom Helden → 50 AP für jedes Tier; Brandt hat dazu 20 Bonus-AP
        Assert.Contains("50 AP", cards[0].ApDetailText);
        Assert.Contains("70 AP", cards[1].ApDetailText);

        cards[0].OpenCommand.Execute(null);
        Settle();
        Assert.NotNull(editor.ActiveCompanion);
        Assert.Equal("Ergebnis", editor.NavSelectedSection!.Title);
        Assert.Same(editor.ActiveCompanion!.Sections, editor.NavSections);
        Assert.Contains(editor.NavSections, s => s.Title == "Vertrautentricks");
        Shot(window, "ergebnis-breit");
        foreach (var section in editor.ActiveCompanion.Sections)
        {
            editor.NavSelectedSection = section;
            Settle();
            Shot(window, "breit-" + section.ShortTitle.Replace(".", ""));
        }

        editor.BackCommand.Execute(null);
        Settle();
        Assert.Null(editor.ActiveCompanion);
        Assert.Same(editor.Sections, editor.NavSections);
        Assert.Same(editor.Companions, editor.SelectedSection!.Page);
    }

    [AvaloniaFact]
    public void Phone_ShowsCompanionTabsAtTheBottom()
    {
        BuildHeroWithAnimals(_app);
        var editor = Editor;
        editor.SelectedSection = editor.Sections.First(s => s.Page == editor.Companions);
        var window = Show(_app.Shell, 392, 812);
        Shot(window, "liste-handy");
        editor.Companions.Cards[1].OpenCommand.Execute(null);
        Settle();
        Shot(window, "ergebnis-handy");
        var nav = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "Nav");
        Assert.Equal(8, nav.ItemCount); // Begleiter ohne Vertrautentricks
        Assert.Equal(editor.ActiveCompanion!.ApText, editor.HeaderApText);
        foreach (var section in editor.ActiveCompanion.Sections)
        {
            editor.NavSelectedSection = section;
            Settle();
            Shot(window, "handy-" + section.ShortTitle.Replace(".", ""));
        }
        Assert.True(_app.Shell.GoBack());
        Assert.Null(editor.ActiveCompanion);
    }

    [AvaloniaFact]
    public void Values_RaiseAndLower_LoggedInTheCompanionLog()
    {
        BuildHeroWithAnimals(_app);
        var editor = Editor;
        editor.OpenCompanion(editor.Character.Companions[0].Id);
        var cat = editor.ActiveCompanion!;
        var mu = cat.Values.Attributes.First(r => r.Id == "MU");
        Assert.Equal(14, mu.Value); // 12 + 1 (Vertrauter) + 1 (gesteigert)

        // Nach einem anderen Eintrag (Vertrautentrick) beginnt eine neue Steigerung, direkt folgende werden zusammengefasst
        mu.IncreaseCommand.Execute(null);
        cat.Values.Attributes.First(r => r.Id == "MU").IncreaseCommand.Execute(null);
        Assert.Equal(16, cat.Values.Attributes.First(r => r.Id == "MU").Value);
        var raises = cat.Companion!.Log.OfType<RaiseEntry>().ToList();
        Assert.Equal(2, raises.Count);
        Assert.Equal(2, raises[1].To - raises[1].From);

        // Senken nimmt die letzten eigenen Steigerungen zurück, bis zum Wert der Tierart mit Boni
        for (var i = 0; i < 3; i++) cat.Values.Attributes.First(r => r.Id == "MU").DecreaseCommand.Execute(null);
        Assert.Empty(cat.Companion!.Log.OfType<RaiseEntry>());
        Assert.Equal(13, cat.Values.Attributes.First(r => r.Id == "MU").Value);
        Assert.False(cat.Values.Attributes.First(r => r.Id == "MU").DecreaseCommand.CanExecute(null));

        editor.UndoCommand.Execute(null);
        Assert.Equal(14, editor.ActiveCompanion!.Values.Attributes.First(r => r.Id == "MU").Value);
    }

    [AvaloniaFact]
    public void Loyalty_CostsHeroAp_TrainingAddsTricks()
    {
        BuildHeroWithAnimals(_app);
        var editor = Editor;
        Assert.Equal(2, editor.State.Ap.Companions); // Loyalität 4 → 5 kostet 2 AP (B) – der Held bezahlt
        editor.OpenCompanion(editor.Character.Companions[1].Id);
        var dog = editor.ActiveCompanion!;
        Assert.Equal(5, dog.Skills.Loyalty[0].Value);
        var tricks = dog.Training.Groups[1].Rows.Select(r => r.Title).ToList();
        Assert.Contains("Wache", tricks);
        Assert.Contains(dog.Training.Groups[0].Rows, r => r.Title == "Wachtier" && r.CanRemove);
        Assert.Contains("Startausbildung", dog.Log.Entries.Select(e => e.Text).Last());
    }

    [AvaloniaFact]
    public void Remove_And_Undo()
    {
        BuildHeroWithAnimals(_app);
        var editor = Editor;
        var id = editor.Character.Companions[1].Id;
        editor.OpenCompanion(id);
        editor.ActiveCompanion!.Basics.DeleteCommand.Execute(null);
        editor.Confirmation!.ConfirmCommand.Execute(null);
        Assert.Null(editor.ActiveCompanion);
        Assert.Single(editor.Companions.Cards);

        editor.UndoCommand.Execute(null);
        Assert.Equal(2, editor.Companions.Cards.Count);
    }

    [AvaloniaFact]
    public async Task Pdf_HasAPagePerLivingCompanion()
    {
        BuildHeroWithAnimals(_app);
        var editor = Editor;

        await editor.Sheet.ExportPdfCommand.ExecuteAsync(null);
        var bytes = Assert.IsType<byte[]>(_app.Dialogs.LastSavedBytes);
        var pages = System.Text.RegularExpressions.Regex.Count(System.Text.Encoding.Latin1.GetString(bytes), @"/Type /Page\b(?!s)");
        if (ShotDir is not null)
        {
            Directory.CreateDirectory(ShotDir);
            await File.WriteAllBytesAsync(Path.Combine(ShotDir, "tiere.pdf"), bytes);
        }

        // Verstorbene Tiere fehlen im PDF: eine Seite weniger
        editor.Mutate(() => editor.Character.Companions[1].Deceased = true);
        await editor.Sheet.ExportPdfCommand.ExecuteAsync(null);
        var fewer = System.Text.RegularExpressions.Regex.Count(System.Text.Encoding.Latin1.GetString(_app.Dialogs.LastSavedBytes!),
            @"/Type /Page\b(?!s)");
        Assert.Equal(pages - 1, fewer);
    }
}
