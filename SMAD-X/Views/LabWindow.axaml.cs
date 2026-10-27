using System;
using Avalonia.Controls;
using Avalonia.Threading;
using SMADX.ViewModels;

namespace SMADX.Views
{
    /// <summary>Fenêtre « Exercice » (non modale), liée à un LabViewModel ; rafraîchit le temps passé.</summary>
    public partial class LabWindow : Window
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };

        public LabWindow()
        {
            InitializeComponent();
            _timer.Tick += (_, _) => (DataContext as LabViewModel)?.Tick();
            Opened += (_, _) => _timer.Start();
            Closed += (_, _) => _timer.Stop();
        }
    }
}
