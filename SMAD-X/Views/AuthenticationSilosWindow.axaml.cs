using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SMADX.ViewModels;

namespace SMADX.Views
{
    /// <summary>Gestion des silos d'authentification du domaine (modale) ; renvoie true si l'on valide.</summary>
    public partial class AuthenticationSilosWindow : Window
    {
        public AuthenticationSilosWindow()
        {
            InitializeComponent();
        }

        private async void OnPickMembers(object? sender, RoutedEventArgs e) => await Pick(vm => vm.PickMembers(), (vm, names) => vm.AddMembers(names));

        private async void OnPickComputers(object? sender, RoutedEventArgs e) => await Pick(vm => vm.PickComputers(), (vm, names) => vm.AddComputers(names));

        /// <summary>Ouvre le choix d'objets dans l'annuaire, et ajoute au silo sélectionné ceux que l'on coche.</summary>
        private async Task Pick(Func<AuthenticationSilosViewModel, ObjectPickerViewModel> open, Action<AuthenticationSilosViewModel, IReadOnlyList<string>> add)
        {
            if (DataContext is not AuthenticationSilosViewModel { HasSelection: true } vm) return;
            var picker = open(vm);
            if (await new ObjectPickerWindow { DataContext = picker }.ShowDialog<bool?>(this) == true)
                add(vm, picker.SelectedNames);
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is AuthenticationSilosViewModel { Error: { } error })
            {
                ErrorTextBlock.Text = error;
                return;
            }
            Close(true);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
    }
}
