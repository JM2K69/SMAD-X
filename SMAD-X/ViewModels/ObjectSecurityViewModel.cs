using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using SMADX.Models;

namespace SMADX.ViewModels
{
    /// <summary>
    /// Section « Sécurité » du panneau de détails : expose les attributs de sécurité de l'objet
    /// sélectionné (drapeaux de compte, SPN, délégation, adminCount, portée de groupe, propriétaire)
    /// sous une forme liable en XAML. Chaque modification met à jour l'objet AD immédiatement.
    /// </summary>
    public sealed class ObjectSecurityViewModel : ObservableObject
    {
        private readonly ADObject _obj;

        public ObjectSecurityViewModel(ADObject obj) => _obj = obj;

        public bool IsAccount => _obj.IsAccount;
        public bool IsGroup => _obj.Type == ADObjectType.Group;
        public bool IsComputer => _obj.Type == ADObjectType.Computer;
        public bool HasSecurityAttributes => IsAccount || IsGroup;

        public static ADGroupScope[] GroupScopes { get; } = Enum.GetValues<ADGroupScope>();
        public static ADGroupCategory[] GroupCategories { get; } = Enum.GetValues<ADGroupCategory>();

        // ─── Drapeaux userAccountControl ──────────────────────────────────────────

        public bool AccountDisabled
        {
            get => Has(ADAccountFlags.AccountDisabled);
            set => SetFlag(ADAccountFlags.AccountDisabled, value);
        }

        public bool PasswordNotRequired
        {
            get => Has(ADAccountFlags.PasswordNotRequired);
            set => SetFlag(ADAccountFlags.PasswordNotRequired, value);
        }

        public bool PasswordNeverExpires
        {
            get => Has(ADAccountFlags.PasswordNeverExpires);
            set => SetFlag(ADAccountFlags.PasswordNeverExpires, value);
        }

        public bool DontRequirePreauth
        {
            get => Has(ADAccountFlags.DontRequirePreauth);
            set => SetFlag(ADAccountFlags.DontRequirePreauth, value);
        }

        public bool TrustedForDelegation
        {
            get => Has(ADAccountFlags.TrustedForDelegation);
            set => SetFlag(ADAccountFlags.TrustedForDelegation, value);
        }

        public bool TrustedToAuthForDelegation
        {
            get => Has(ADAccountFlags.TrustedToAuthForDelegation);
            set => SetFlag(ADAccountFlags.TrustedToAuthForDelegation, value);
        }

        public bool NotDelegated
        {
            get => Has(ADAccountFlags.NotDelegated);
            set => SetFlag(ADAccountFlags.NotDelegated, value);
        }

        private bool Has(ADAccountFlags flag) => _obj.AccountFlags.HasFlag(flag);

        private void SetFlag(ADAccountFlags flag, bool value, [CallerMemberName] string? property = null)
        {
            if (Has(flag) == value) return;
            _obj.AccountFlags = value ? _obj.AccountFlags | flag : _obj.AccountFlags & ~flag;
            Touch(property);
        }

        // ─── Listes (une valeur par ligne dans l'interface) ───────────────────────

        public string ServicePrincipalNamesText
        {
            get => string.Join(Environment.NewLine, _obj.ServicePrincipalNames);
            set => ReplaceLines(_obj.ServicePrincipalNames, value);
        }

        public string AllowedToDelegateToText
        {
            get => string.Join(Environment.NewLine, _obj.AllowedToDelegateTo);
            set => ReplaceLines(_obj.AllowedToDelegateTo, value);
        }

        /// <summary>Lignes non vides, sans espaces de bord ni doublons (SPN insensibles à la casse).</summary>
        private void ReplaceLines(ObservableCollection<string> target, string? text, [CallerMemberName] string? property = null)
        {
            var lines = (text ?? string.Empty)
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (lines.SequenceEqual(target)) return;

            target.Clear();
            foreach (var line in lines)
                target.Add(line);
            Touch(property);
        }

        // ─── Dates (lastLogonTimestamp, pwdLastSet) ───────────────────────────────

        public DateTime? LastLogonTimestamp
        {
            get => _obj.LastLogonTimestamp;
            set
            {
                if (_obj.LastLogonTimestamp == value) return;
                _obj.LastLogonTimestamp = value;
                Touch();
            }
        }

        public DateTime? PwdLastSet
        {
            get => _obj.PwdLastSet;
            set
            {
                if (_obj.PwdLastSet == value) return;
                _obj.PwdLastSet = value;
                Touch();
            }
        }

        // ─── Groupes, adminCount, propriétaire ────────────────────────────────────

        public bool AdminCount
        {
            get => _obj.AdminCount;
            set
            {
                if (_obj.AdminCount == value) return;
                _obj.AdminCount = value;
                Touch();
            }
        }

        public ADGroupScope GroupScope
        {
            get => _obj.GroupScope ?? ADGroupScope.Global;
            set
            {
                if (_obj.GroupScope == value) return;
                _obj.GroupScope = value;
                Touch();
            }
        }

        public ADGroupCategory GroupCategory
        {
            get => _obj.GroupCategory ?? ADGroupCategory.Security;
            set
            {
                if (_obj.GroupCategory == value) return;
                _obj.GroupCategory = value;
                Touch();
            }
        }

        public string Owner
        {
            get => _obj.Owner ?? string.Empty;
            set
            {
                var owner = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (_obj.Owner == owner) return;
                _obj.Owner = owner;
                Touch();
            }
        }

        private void Touch([CallerMemberName] string? property = null)
        {
            _obj.ModifiedDate = DateTime.Now;
            OnPropertyChanged(property);
        }
    }
}
