using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.UiTests;

/// <summary>Ausrüstung (Meilenstein 7a).</summary>
public sealed class EquipmentPageTests : IDisposable
{
    private readonly TestShell _app = new();

    public EquipmentPageTests()
    {
        _app.List.NewCommand.Execute(null);
    }

    public void Dispose() => _app.Dispose();

    private EditorViewModel Editor => _app.Editor;

    private EquipmentPageViewModel Page => Editor.Equipment;

    private void Pick(string name) =>
        Editor.Picker!.Groups.SelectMany(g => g.Items).First(i => i.Name == name).PickCommand!.Execute(null);

    [Fact]
    public void AddWeaponAndArmor_ShowsValuesAndEncumbrance()
    {
        Page.AddWeaponCommand.Execute(null);
        Pick("Langschwert");
        var sword = Assert.Single(Page.Weapons);
        Assert.Contains("AT ", sword.ValuesText);
        Assert.Contains("PA ", sword.ValuesText);
        Assert.Contains("TP 1W6+4", sword.ValuesText);

        Page.AddArmorCommand.Execute(null);
        Pick("Kettenrüstung");
        Assert.True(Assert.Single(Page.Armors).Worn); // erste Rüstung wird gleich angelegt
        Assert.Equal("Belastung II (Rüstung II)", Page.EncumbranceText);
        Assert.Contains("AT −2", Page.EffectsText);
        Assert.Contains("RS 4", Editor.Summary.DerivedText);

        // zweite Rüstung anlegen: die erste wird abgelegt
        Page.AddArmorCommand.Execute(null);
        Pick("Lederrüstung");
        Assert.False(Page.Armors[1].Worn);
        Page.Armors[1].Worn = true;
        Assert.False(Page.Armors[0].Worn);
        Assert.Equal("Belastung I (Rüstung I)", Page.EncumbranceText);
        Assert.Contains("zusätzlich −1 GS, −1 INI", Page.EffectsText);
    }

    [Fact]
    public void Encumbrance_ShownOnSkillsWithEncumbrance()
    {
        Page.AddArmorCommand.Execute(null);
        Pick("Plattenrüstung");
        var climbing = Editor.Skills.Groups.SelectMany(g => g.Rows).Single(r => r.Id == "skill.klettern");
        Assert.Contains("−3 durch Belastung", climbing.Detail);
    }

    [Fact]
    public void RemoveWeapon_AndUndo()
    {
        Page.AddWeaponCommand.Execute(null);
        Pick("Dolch");
        Page.Weapons[0].RemoveCommand.Execute(null);
        Assert.Empty(Page.Weapons);
        Editor.UndoCommand.Execute(null);
        Assert.Single(Page.Weapons);
    }
}
