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

    [Fact]
    public void AddItemAndPackage_MoneyAndLoad()
    {
        Assert.Equal("750 S", Page.MoneyText);
        Page.AddItemCommand.Execute(null);
        Pick("Lederrucksack");
        var backpack = Assert.Single(Page.Items);
        Assert.Equal("2 Stein · 34 S", backpack.WeightPriceText);
        Assert.Equal("716 S", Page.MoneyText);
        Assert.StartsWith("Getragen 2 Stein von ", Page.LoadText);

        backpack.IncreaseCommand.Execute(null);
        Assert.Equal("2 × 2 Stein · 34 S", Page.Items[0].WeightPriceText);
        Assert.Equal("682 S", Page.MoneyText);
        Page.Items[0].Paid = false;
        Assert.Equal("750 S", Page.MoneyText);

        // Paket: jede Zeile ein Eintrag, Waffen erscheinen bei den Waffen
        Page.AddPackageCommand.Execute(null);
        Pick("Abenteurerpaket");
        Assert.Contains(Page.Weapons, w => w.Name == "Kurzschwert");
        Assert.Contains(Page.Items, i => i.Name == "Kletterseil, 10 Schritt");
        Assert.Equal("174,5 S", Page.MoneyText);
    }

    [Fact]
    public void CustomItem_EditNameWeightPrice_AndBooking()
    {
        Page.AddCustomItemCommand.Execute(null);
        var item = Assert.Single(Page.Items);
        item.NameInput = "Familienerbstück";
        item = Page.Items[0];
        item.WeightInput = "0,5";
        Page.Items[0].PriceInput = "100";
        Assert.Equal("Familienerbstück", Page.Items[0].Name);
        Assert.Equal("0,5 Stein · 100 S", Page.Items[0].WeightPriceText);
        Assert.Equal("650 S", Page.MoneyText);

        Assert.False(Page.BookCommand.CanExecute(null));
        Page.BookingAmount = "−50,5";
        Page.BookingText = "Unterkunft";
        Page.BookCommand.Execute(null);
        Assert.Equal("599,5 S", Page.MoneyText);
        Assert.Equal("−50,5 S · Unterkunft", Assert.Single(Page.Bookings).Text);
        Assert.Equal("", Page.BookingAmount);
        Page.Bookings[0].RemoveCommand.Execute(null);
        Assert.Empty(Page.Bookings);
    }

    [Fact]
    public void Overloaded_ShowsLoadEncumbrance()
    {
        Page.AddCustomItemCommand.Execute(null);
        Page.Items[0].WeightInput = "100";
        Assert.NotNull(Page.LoadEffectText);
        Assert.Contains("Traglast", Page.EncumbranceText);
    }
}
