using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSACharacterCreator.Core.Content;

namespace DSACharacterCreator.ViewModels;

/// <summary>
/// Quellenauswahl: welche Publikationen gelten. Je Held (Änderung sofort am Helden) oder als Standard für neue Helden
/// (Einstellungen). Ü18-Werke sind gekennzeichnet und nie automatisch aktiv. Abwählen sperrt nichts: Gewähltes aus
/// abgewählten Quellen bleibt erhalten und wird als Warnung gemeldet.
/// </summary>
public sealed partial class SourcesViewModel : ObservableObject
{
    private const string CoreGroup = "Grundregeln";
    private const string OtherGroup = "Weitere Publikationen";
    private const string AdultGroup = "Ab 18 Jahren";

    private readonly BookCatalog _catalog;
    private readonly Action<SourceSelection> _onChange;
    private readonly Action _onClose;
    private readonly Func<SourceSelection, IReadOnlyDictionary<string, IReadOnlyList<string>>>? _dependents;
    private readonly IReadOnlyList<SourceBookViewModel> _books;
    private readonly Dictionary<string, PickerHeaderViewModel> _headers = [];
    private SourceSelection _selection;

    /// <param name="dependents">Je Buch die gewählten Inhalte des Helden, die daran hängen (nur bei der Auswahl je Held).</param>
    public SourcesViewModel(string title, string intro, BookCatalog catalog, IReadOnlyList<BookUsage> usage,
        SourceSelection selection, Action<SourceSelection> onChange, Action onClose,
        Func<SourceSelection, IReadOnlyDictionary<string, IReadOnlyList<string>>>? dependents = null)
    {
        Title = title;
        Intro = intro;
        _catalog = catalog;
        _selection = selection;
        _onChange = onChange;
        _onClose = onClose;
        _dependents = dependents;
        _books = usage
            .Select(u => new SourceBookViewModel(this, u, GroupOf(u.Book)))
            .OrderBy(b => b.Group switch { CoreGroup => 0, OtherGroup => 1, _ => 2 })
            .ThenBy(b => b.Book.Id == "grw" ? 0 : 1)
            .ThenBy(b => b.Name, StringComparer.CurrentCulture)
            .ToList();
        Refresh();
    }

    public string Title { get; }

    public string Intro { get; }

    public SourceSelection Selection => _selection;

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    partial void OnSearchChanged(string value) => ApplyFilter();

    /// <summary>Gruppenüberschriften und Bücher als flache Liste für die virtualisierte Anzeige.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<object> Rows { get; private set; } = [];

    /// <summary>„214 von 221 Publikationen aktiv“.</summary>
    [ObservableProperty]
    public partial string SummaryText { get; private set; } = "";

    /// <summary>Gewählte Inhalte aus nicht aktiven Quellen; leer, wenn alles verfügbar ist.</summary>
    [ObservableProperty]
    public partial string WarningText { get; private set; } = "";

    public IReadOnlyList<SourceBookViewModel> Books => _books;

    private static string GroupOf(Book book) =>
        book.Adult ? AdultGroup : book.Id is "grw" or "wiki" ? CoreGroup : OtherGroup;

    internal void Toggle(SourceBookViewModel book, bool enabled)
    {
        if (_selection.IsEnabled(book.Book.Id) == enabled) return;
        Apply(_selection.With(book.Book.Id, enabled));
    }

    private void Apply(SourceSelection selection)
    {
        if (selection.SetEquals(_selection)) return;
        _selection = selection;
        _onChange(selection);
        Refresh();
    }

    [RelayCommand]
    private void UseDefault() => Apply(SourceSelection.Default(_catalog));

    [RelayCommand]
    private void UseCoreRules() => Apply(SourceSelection.CoreRules(_catalog));

    [RelayCommand]
    private void UseAll() => Apply(SourceSelection.All(_catalog));

    [RelayCommand]
    private void Close() => _onClose();

    private void Refresh()
    {
        var dependents = _dependents?.Invoke(_selection);
        var missing = new HashSet<string>(StringComparer.CurrentCulture);
        foreach (var book in _books)
        {
            var enabled = _selection.IsEnabled(book.Book.Id);
            var names = dependents?.GetValueOrDefault(book.Book.Id) ?? [];
            book.Update(enabled, names);
            if (!enabled) missing.UnionWith(names);
        }
        SummaryText = $"{_books.Count(b => b.IsEnabled)} von {_books.Count} Publikationen aktiv";
        WarningText = missing.Count == 0
            ? ""
            : $"⚠ Aus keiner aktiven Quelle: {string.Join(", ", missing.Order(StringComparer.CurrentCulture))}. "
              + "Diese Inhalte bleiben gewählt, gelten aber als Warnung.";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var term = Search.Trim();
        Rows = _books
            .Where(b => term.Length == 0 || b.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
            .GroupBy(b => b.Group)
            .SelectMany(g => g.Cast<object>().Prepend(HeaderFor(g.Key)))
            .ToList();
    }

    private PickerHeaderViewModel HeaderFor(string title) =>
        _headers.TryGetValue(title, out var header) ? header : _headers[title] = new PickerHeaderViewModel(title);
}

/// <summary>Ein Buch in der Quellenauswahl.</summary>
public sealed partial class SourceBookViewModel(SourcesViewModel owner, BookUsage usage, string group) : ObservableObject
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public Book Book => usage.Book;

    public string Name => usage.Book.Name;

    public string Group { get; } = group;

    public bool IsAdult => usage.Book.Adult;

    /// <summary>„312 Einträge, davon 120 nur hier“.</summary>
    public string Detail => usage.Entries switch
    {
        0 => "keine erfassten Einträge",
        var n => (n == 1 ? "1 Eintrag" : $"{n.ToString("N0", German)} Einträge")
                 + (usage.Exclusive > 0 ? $", davon {usage.Exclusive.ToString("N0", German)} nur hier" : ""),
    };

    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value)) return;
            owner.Toggle(this, value);
        }
    }

    /// <summary>Gewählte Inhalte des Helden, die an diesem Buch hängen; leer ohne.</summary>
    [ObservableProperty]
    public partial string UsedText { get; private set; } = "";

    /// <summary>Gewähltes, das wegen dieses abgewählten Buchs aus keiner aktiven Quelle stammt.</summary>
    [ObservableProperty]
    public partial bool IsWarning { get; private set; }

    internal void Update(bool enabled, IReadOnlyList<string> dependents)
    {
        if (_isEnabled != enabled)
        {
            _isEnabled = enabled;
            OnPropertyChanged(nameof(IsEnabled));
        }
        IsWarning = !enabled && dependents.Count > 0;
        UsedText = dependents.Count == 0 ? ""
            : (enabled ? "Nur hieraus gewählt: " : "⚠ Gewählt, Quelle nicht aktiv: ") + string.Join(", ", dependents);
    }
}
