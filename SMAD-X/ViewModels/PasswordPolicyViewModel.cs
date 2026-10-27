using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SMADX.Models;

namespace SMADX.ViewModels
{
    /// <summary>
    /// Section « Politique de mot de passe » du panneau de détails : la politique par défaut du domaine (Default
    /// Domain Policy) ou celle d'un PSO, éditable. Les valeurs sont celles d'AD ; une durée de 0 signifie « jamais ».
    /// NumericUpDown travaille en decimal : null efface la valeur.
    /// </summary>
    public sealed class PasswordPolicyViewModel : ObservableObject
    {
        private readonly ADObject _obj;

        public PasswordPolicyViewModel(ADObject obj) => _obj = obj;

        /// <summary>Un PSO a une priorité ; le domaine n'en a pas.</summary>
        public bool IsPso => _obj.Type == ADObjectType.PasswordSettingsObject;

        /// <summary>Le domaine porte aussi le quota d'ordinateurs joignables par tout utilisateur.</summary>
        public bool IsDomain => _obj.Type == ADObjectType.Domain;

        public decimal? MachineAccountQuota { get => _obj.MachineAccountQuota; set => Set(v => _obj.MachineAccountQuota = v, value); }

        public decimal? Precedence { get => _obj.PSOPrecedence; set => Set(v => _obj.PSOPrecedence = v, value); }
        public decimal? MinPasswordLength { get => _obj.PSOMinPasswordLength; set => Set(v => _obj.PSOMinPasswordLength = v, value); }
        public decimal? PasswordHistoryCount { get => _obj.PSOPasswordHistoryCount; set => Set(v => _obj.PSOPasswordHistoryCount = v, value); }
        public decimal? MaxPasswordAgeDays { get => _obj.PSOMaxPasswordAgeDays; set => Set(v => _obj.PSOMaxPasswordAgeDays = v, value); }
        public decimal? MinPasswordAgeDays { get => _obj.PSOMinPasswordAgeDays; set => Set(v => _obj.PSOMinPasswordAgeDays = v, value); }
        public decimal? LockoutThreshold { get => _obj.PSOLockoutThreshold; set => Set(v => _obj.PSOLockoutThreshold = v, value); }
        public decimal? LockoutDurationMinutes { get => _obj.PSOLockoutDurationMinutes; set => Set(v => _obj.PSOLockoutDurationMinutes = v, value); }
        public decimal? LockoutObservationWindowMinutes { get => _obj.PSOLockoutObservationWindowMinutes; set => Set(v => _obj.PSOLockoutObservationWindowMinutes = v, value); }

        public bool ComplexityEnabled
        {
            get => _obj.PSOComplexityEnabled ?? false;
            set
            {
                _obj.PSOComplexityEnabled = value;
                _obj.ModifiedDate = DateTime.Now;
                OnPropertyChanged();
            }
        }

        private void Set(Action<int?> assign, decimal? value, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
        {
            assign(value is { } v ? (int)Math.Max(0, Math.Round(v)) : null);
            _obj.ModifiedDate = DateTime.Now;
            OnPropertyChanged(property);
        }
    }
}
