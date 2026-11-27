using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Threading;
using SMAGX.Controls;
using SMAGX.ViewModels;

namespace SMAGX.Views;

/// <summary>Le terminal : la sortie (couleurs de Git), l'invite et la saisie, avec l'historique et Ctrl+L.</summary>
public partial class TerminalView : UserControl
{
    private StationViewModel? _vm;

    public TerminalView()
    {
        InitializeComponent();
        InputBox.AddHandler(KeyDownEvent, OnTab, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => Attach(DataContext as StationViewModel);
        AttachedToVisualTree += (_, _) => InputBox.Focus();
        PointerReleased += (_, _) => { if (string.IsNullOrEmpty(Output.SelectedText)) InputBox.Focus(); };
    }

    private void Attach(StationViewModel? vm)
    {
        if (_vm is not null)
        {
            _vm.Blocks.CollectionChanged -= OnBlocksChanged;
            _vm.PropertyChanged -= OnViewModelChanged;
        }
        _vm = vm;
        if (vm is null) return;
        vm.Blocks.CollectionChanged += OnBlocksChanged;
        vm.PropertyChanged += OnViewModelChanged;
        Render();
        // Un autre poste choisi : on tape dans son terminal
        Dispatcher.UIThread.Post(() => InputBox.Focus());
    }

    private void OnBlocksChanged(object? sender, NotifyCollectionChangedEventArgs e) => Render();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StationViewModel.Prompt)) RenderPrompt();
        if (e.PropertyName is nameof(StationViewModel.Pending) or nameof(StationViewModel.Answering)) Render();
        if ((e.PropertyName == nameof(StationViewModel.Busy) && _vm is { Busy: false })
            || (e.PropertyName == nameof(StationViewModel.Answering) && _vm is { Answering: true }))
            Dispatcher.UIThread.Post(() => InputBox.Focus());
    }

    private void Render()
    {
        if (_vm is null) return;
        var inlines = new InlineCollection();
        foreach (var block in _vm.Blocks.TakeLast(400))
        {
            AnsiText.AppendPrompt(inlines, block.Prompt);
            AnsiText.Append(inlines, block.Command + "\n");
            AnsiText.Append(inlines, block.Output);
        }
        // La commande qui attend une réponse : sa sortie, question comprise ; la saisie, dessous, lui répond
        if (_vm.Pending is { } pending)
        {
            AnsiText.AppendPrompt(inlines, pending.Prompt);
            AnsiText.Append(inlines, pending.Command + "\n");
            AnsiText.Append(inlines, pending.Output);
        }
        Output.Inlines = inlines;
        RenderPrompt();
        Dispatcher.UIThread.Post(() => Scroller.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void RenderPrompt()
    {
        if (_vm is null) return;
        var inlines = new InlineCollection();
        // Pendant une question, pas d'invite : ce qui est tapé est la réponse
        if (_vm.Pending is null) AnsiText.AppendPrompt(inlines, _vm.Prompt);
        PromptText.Inlines = inlines;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null) return;
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                await _vm.RunAsync();
                break;
            case Key.C when e.KeyModifiers.HasFlag(KeyModifiers.Control) && _vm.Answering:
                e.Handled = true;
                _vm.Answer(null);
                break;
            case Key.Up when !_vm.Answering:
                e.Handled = true;
                _vm.History(-1);
                InputBox.CaretIndex = _vm.Input.Length;
                break;
            case Key.Down when !_vm.Answering:
                e.Handled = true;
                _vm.History(+1);
                InputBox.CaretIndex = _vm.Input.Length;
                break;
            case Key.L when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                e.Handled = true;
                _vm.Clear();
                break;
        }
    }

    // Tabulation : la complétion, avant que la touche ne fasse passer le focus au contrôle suivant
    private void OnTab(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || _vm is null) return;
        e.Handled = true;
        if (_vm.Answering || e.KeyModifiers != KeyModifiers.None) return;
        var caret = _vm.Complete(InputBox.CaretIndex);
        InputBox.Text = _vm.Input;
        InputBox.CaretIndex = caret;
    }
}
