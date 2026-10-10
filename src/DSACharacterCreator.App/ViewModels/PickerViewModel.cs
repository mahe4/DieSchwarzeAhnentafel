using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DSACharacterCreator.ViewModels;

/// <summary>Auswahlliste mit Suche für lange Listen (Kulturen, Professionen). Am Handy Vollbild, am Desktop Dialog.</summary>
public sealed partial class PickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<PickerItemViewModel> _items;
    private readonly Action<string> _onPick;
    private readonly Action _onClose;

    public PickerViewModel(string title, IEnumerable<PickerItemViewModel> items, string? selectedId,
        Action<string> onPick, Action onClose)
    {
        Title = title;
        _items = items.ToList();
        foreach (var item in _items)
        {
            item.IsCurrent = item.Id == selectedId;
            item.PickCommand = new RelayCommand(() => Pick(item.Id));
        }
        _onPick = onPick;
        _onClose = onClose;
        ApplyFilter();
    }

    public string Title { get; }

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
        Groups = matches.GroupBy(i => i.Group).OrderBy(g => g.Key, StringComparer.CurrentCulture)
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

public sealed class PickerItemViewModel(string id, string name, string group, string detail)
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public string Group { get; } = group;

    public string Detail { get; } = detail;

    public bool IsCurrent { get; set; }

    public IRelayCommand? PickCommand { get; set; }
}
