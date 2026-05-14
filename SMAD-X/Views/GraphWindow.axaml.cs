using Avalonia.Controls;
using Avalonia.Interactivity;
using SMADX.ViewModels;
using SMADX.Graph;

namespace SMADX.Views
{
    public partial class GraphWindow : Window
    {
        private readonly GraphCanvas _canvas = new();
        private GraphViewModel? _vm;

        public GraphWindow()
        {
            InitializeComponent();
        }

        public GraphWindow(GraphViewModel vm) : this()
        {
            _vm = vm;
            DataContext = vm;

            // Héberger le canvas dans le ContentControl
            var host = this.FindControl<ContentControl>("GraphHost")!;
            host.Content = _canvas;

            // Écouter les changements de filtre
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GraphViewModel.RefreshToken))
                    ReloadGraph();
            };

            // Écouter la sélection d'un nœud dans le canvas
            _canvas.NodeSelected += node =>
            {
                vm.SetSelectedNode(node);
            };

            ReloadGraph();
        }

        private void ReloadGraph()
        {
            if (_vm == null) return;
            var builder = new GraphBuilder();
            builder.Build(_vm.Root, _vm.BuildFilter());
            _canvas.LoadGraph(builder.Nodes, builder.Edges);
            _vm.UpdateStats(builder.Nodes.Count, builder.Edges.Count);
        }

        private void OnFitView(object? sender, RoutedEventArgs e) => _canvas.FitView();
        private void OnZoomIn(object? sender, RoutedEventArgs e)   => _canvas.ZoomIn();
        private void OnZoomOut(object? sender, RoutedEventArgs e)  => _canvas.ZoomOut();
        private void OnRelayout(object? sender, RoutedEventArgs e)
        {
            _canvas.ReLayout();
        }
        private void OnRefresh(object? sender, RoutedEventArgs e)  => ReloadGraph();
        private void OnClose(object? sender, RoutedEventArgs e)    => Close();
    }
}
