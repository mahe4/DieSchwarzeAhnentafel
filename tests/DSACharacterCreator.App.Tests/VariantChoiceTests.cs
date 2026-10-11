using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DSACharacterCreator.ViewModels;
using DSACharacterCreator.Views;

namespace DSACharacterCreator.UiTests;

/// <summary>Variantenauswahl über die echte ComboBox (mit Rückfrage, wenn Werte angepasst wurden).</summary>
public sealed class VariantChoiceTests : IDisposable
{
    private readonly TestShell _app = new();

    public void Dispose() => _app.Dispose();

    private EditorViewModel Editor => _app.Editor;

    private static void Run()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private ComboBox VariantBox(Window window) => window.GetVisualDescendants().OfType<ComboBox>()
        .Single(c => c.DataContext == Editor.Basics.VariantChoice);

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectingVariant_InComboBox_KeepsSelection(bool adjusted)
    {
        _app.List.NewCommand.Execute(null);
        Editor.Basics.SpeciesOptions.Single(s => s.Name == "Zwerg").IsSelected = true;
        Editor.Basics.ChooseProfessionCommand.Execute(null);
        Editor.Picker!.Groups.SelectMany(g => g.Items).Single(i => i.Name == "Diener der Erdmutter").PickCommand!.Execute(null);
        if (adjusted) Editor.Skills.Groups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.zechen").IncreaseCommand.Execute(null);

        var window = new Window { Width = 392, Height = 800, Content = new MainView { DataContext = _app.Shell } };
        window.Show();
        Editor.SelectedSection = Editor.Sections.Single(s => s.Page == Editor.Basics);
        Editor.Basics.IsProfessionExpanded = true;
        Run();

        var box = VariantBox(window);
        box.SelectedItem = box.Items.Cast<ChoiceOption>().Single(o => o.Label.StartsWith("Gefährte des Erzes"));
        Run();
        if (adjusted)
        {
            Assert.NotNull(Editor.Confirmation);
            Editor.Confirmation!.ConfirmCommand.Execute(null);
            Run();
        }

        Assert.Equal("gefaehrte-des-erzes", Editor.Character.Creation.ProfessionVariant);
        box = VariantBox(window);
        Assert.Equal("gefaehrte-des-erzes", (box.SelectedItem as ChoiceOption)?.Id);
    }
}
