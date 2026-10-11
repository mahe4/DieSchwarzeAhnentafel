using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DSACharacterCreator.ViewModels;

/// <summary>
/// Auswahlliste mit Suche für lange Listen. Am Handy Vollbild, am Desktop Dialog.
/// Einfachauswahl (Kultur, Profession): ein Klick wählt und schließt. Sammelauswahl (Vorteile, Sonderfertigkeiten,
/// Zauber, Ausrüstung …): ein Klick klappt die Beschreibung auf, „Hinzufügen“ fügt hinzu und die Liste bleibt offen.
/// </summary>
public sealed partial class PickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<PickerItemViewModel> _items;
    private readonly Action<string> _onPick;
    private readonly Action _onClose;
    private readonly bool _sortGroups;

    /// <param name="sortGroups">Gruppen alphabetisch; sonst in der Reihenfolge der Einträge (z. B. eigene Tradition zuerst).</param>
    /// <param name="multiple">Sammelauswahl: Einträge aufklappen und mit „Hinzufügen“ übernehmen, ohne zu schließen.</param>
    public PickerViewModel(string title, IEnumerable<PickerItemViewModel> items, string? selectedId,
        Action<string> onPick, Action onClose, bool sortGroups = true, bool multiple = false)
    {
        Title = title;
        IsMultiple = multiple;
        _sortGroups = sortGroups;
        _items = items.ToList();
        foreach (var item in _items)
        {
            item.IsCurrent = item.Id == selectedId;
            item.IsMultiple = multiple;
            item.PickCommand = multiple ? new RelayCommand(item.ToggleExpanded) : new RelayCommand(() => Pick(item.Id));
            item.AddCommand = new RelayCommand(() => Add(item), () => item.CanAdd);
        }
        _onPick = onPick;
        _onClose = onClose;
        ApplyFilter();
    }

    public string Title { get; }

    public bool IsMultiple { get; }

    /// <summary>„3 hinzugefügt“; leer, solange nichts hinzugefügt wurde.</summary>
    [ObservableProperty]
    public partial string AddedText { get; private set; } = "";

    private int _added;

    private void Add(PickerItemViewModel item)
    {
        if (!item.CanAdd) return;
        _onPick(item.Id);
        item.MarkAdded();
        _added++;
        AddedText = $"{_added} hinzugefügt";
    }

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    partial void OnSearchChanged(string value) => ApplyFilter();

    /// <summary>Gefilterte Einträge nach Gruppen (für Tests und Logik).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PickerGroupViewModel> Groups { get; private set; } = [];

    /// <summary>
    /// Flache Liste aus Gruppenüberschriften und Einträgen für die virtualisierte Anzeige: Nur sichtbare Zeilen werden
    /// als Steuerelemente erzeugt (am Handy sonst ruckelig bei ~300 Professionen).
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<object> Rows { get; private set; } = [];

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    private void ApplyFilter()
    {
        var term = Search.Trim();
        var matches = _items.Where(i => term.Length == 0
                                        || i.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                                        || i.Group.Contains(term, StringComparison.CurrentCultureIgnoreCase))
            .ToList();
        var groups = matches.GroupBy(i => i.Group);
        Groups = (_sortGroups ? groups.OrderBy(g => g.Key, StringComparer.CurrentCulture) : groups)
            .Select(g => new PickerGroupViewModel(g.Key, g.OrderBy(i => i.Name, StringComparer.CurrentCulture).ToList()))
            .ToList();
        CountText = $"{matches.Count} von {_items.Count}";
        Rows = Groups.SelectMany(g => g.Items.Cast<object>().Prepend(HeaderFor(g.Title))).ToList();
    }

    private readonly Dictionary<string, PickerHeaderViewModel> _headers = [];

    private PickerHeaderViewModel HeaderFor(string title) =>
        _headers.TryGetValue(title, out var header) ? header : _headers[title] = new PickerHeaderViewModel(title);

    private void Pick(string id)
    {
        _onPick(id);
        _onClose();
    }

    [RelayCommand]
    private void Close() => _onClose();
}

public sealed record PickerGroupViewModel(string Title, IReadOnlyList<PickerItemViewModel> Items);

public sealed record PickerHeaderViewModel(string Title);

/// <param name="description">Beschreibung zum Aufklappen (wird erst beim Aufklappen erzeugt).</param>
/// <param name="unique">Nur einmal hinzufügbar (z. B. Zauber); danach ist „Hinzufügen“ gesperrt.</param>
public sealed partial class PickerItemViewModel(string id, string name, string group, string detail,
    Func<string?>? description = null, bool unique = false) : ObservableObject
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public string Group { get; } = group;

    public string Detail { get; } = detail;

    public bool IsCurrent { get; set; }

    public bool IsMultiple { get; set; }

    public IRelayCommand? PickCommand { get; set; }

    public IRelayCommand? AddCommand { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; private set; }

    /// <summary>Beschreibung (Regeltext …); <c>null</c>, solange zugeklappt oder ohne Beschreibung.</summary>
    [ObservableProperty]
    public partial string? Description { get; private set; }

    [ObservableProperty]
    public partial int AddedCount { get; private set; }

    /// <summary>„✓“ bzw. „✓ 2×“ nach dem Hinzufügen.</summary>
    public string? AddedText => AddedCount switch { 0 => null, 1 => "✓", var n => $"✓ {n}×" };

    public bool CanAdd => !unique || AddedCount == 0;

    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
        if (IsExpanded) Description ??= description?.Invoke() ?? "Keine Beschreibung vorhanden.";
    }

    public void MarkAdded()
    {
        AddedCount++;
        OnPropertyChanged(nameof(AddedText));
        OnPropertyChanged(nameof(CanAdd));
        AddCommand?.NotifyCanExecuteChanged();
    }
}
