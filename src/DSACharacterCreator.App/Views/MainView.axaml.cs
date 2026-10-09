using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DSACharacterCreator.Services;
using DSACharacterCreator.ViewModels;

namespace DSACharacterCreator.Views;

public partial class MainView : UserControl
{
    private TopLevel? _topLevel;

    public MainView()
    {
        InitializeComponent();
    }

    // Android-Zurück-Taste (und Maus-Zurück-Taste am Desktop) navigiert innerhalb der App.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null) _topLevel.BackRequested += OnBackRequested;
        StorageFileDialogs.Host = _topLevel;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null) _topLevel.BackRequested -= OnBackRequested;
        if (StorageFileDialogs.Host == _topLevel) StorageFileDialogs.Host = null;
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.GoBack()) e.Handled = true;
    }
}
