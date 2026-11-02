using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;

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

        private static LocalizationService Loc => LocalizationService.Instance;

        public bool IsAccount => _obj.IsAccount;
        public bool IsGroup => _obj.Type == ADObjectType.Group;
        public bool IsComputer => _obj.Type == ADObjectType.Computer;
        public bool IsPolicy => _obj.Type == ADObjectType.Policy;
        public bool HasSecurityAttributes => IsAccount || IsGroup || IsPolicy;
        public bool HasAdminCount => IsAccount || IsGroup;

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

        // ─── Ordinateur : LAPS, administrateurs locaux, sessions ───────────────────

        /// <summary>LAPS gère le mot de passe de l'administrateur local ; l'activer fixe une expiration dans 30 jours.</summary>
        public bool Laps
        {
            get => _obj.LapsPasswordExpiration != null;
            set
            {
                if (Laps == value) return;
                _obj.LapsPasswordExpiration = value ? DateTime.Now.Date.AddDays(Services.Labs.LabDomainBuilder.LapsRenewalDays) : null;
                Touch();
                OnPropertyChanged(nameof(LapsPasswordExpiration));
            }
        }

        public DateTime? LapsPasswordExpiration
        {
            get => _obj.LapsPasswordExpiration;
            set
            {
                if (_obj.LapsPasswordExpiration == value) return;
                _obj.LapsPasswordExpiration = value;
                Touch();
                OnPropertyChanged(nameof(Laps));
            }
        }

        /// <summary>Comptes et groupes membres du groupe local Administrateurs (absent de l'annuaire : posé à la main).</summary>
        public string LocalAdminsText
        {
            get => string.Join(Environment.NewLine, _obj.LocalAdmins ?? []);
            set => ReplaceLines(_obj.LocalAdmins ??= new(), value);
        }

        /// <summary>Comptes qui ont une session ouverte sur la machine : leurs identifiants y sont en mémoire.</summary>
        public string SessionsText
        {
            get => string.Join(Environment.NewLine, _obj.Sessions ?? []);
            set => ReplaceLines(_obj.Sessions ??= new(), value);
        }

        /// <summary>Contexte du domaine de l'objet, pour les effets des GPO ; calculé à la sélection.</summary>
        private AuditContext? Context =>
            _context ??= Root(_obj) is { Type: ADObjectType.Domain } domain ? new AuditContext(domain, DateTime.Now) : null;
        private AuditContext? _context;

        private static ADObject Root(ADObject o) => o.Parent == null ? o : Root(o.Parent);

        /// <summary>Administrateurs locaux que les GPO imposent à la machine, avec la GPO de chacun.</summary>
        public string GpoLocalAdminsEffect => IsComputer && Context is { } c
            ? string.Join(Environment.NewLine, c.Policies.LocalAdministrators(_obj).Where(a => a.Gpo != null)
                .Select(a => string.Format(Loc["Security.ByGpo"], a.Name, a.Gpo!.Name)))
            : string.Empty;

        /// <summary>Sessions saisies que les GPO ou les silos interdisent sur la machine : elles n'exposent aucun identifiant.</summary>
        public string BlockedSessionsEffect => IsComputer && Context is { } c
            ? string.Join(Environment.NewLine, (_obj.Sessions ?? []).Select(name => (name, account: c.Principal(name)))
                .Where(x => x.account is { IsAccount: true })
                .Select(x => (x.name, causes: c.Policies.LogonDeniedBy(x.account!, _obj).Select(g => $"GPO {g.Name}")
                    .Concat(c.Silos.LogonDeniedBy(x.account!, _obj) is { } silo ? [$"silo {silo.Name}"] : []).ToList()))
                .Where(x => x.causes.Count > 0)
                .Select(x => string.Format(Loc["Security.BlockedBy"], x.name, string.Join(", ", x.causes))))
            : string.Empty;

        /// <summary>Silo d'authentification du compte et son mode ; vide s'il n'est dans aucun.</summary>
        public string SiloEffect => IsAccount && Context?.Silos.SiloOf(_obj) is { } silo
            ? string.Format(Loc[silo.Enforced ? "Security.SiloEnforced" : "Security.SiloAudit"], silo.Name)
            : string.Empty;

        public bool HasSiloEffect => SiloEffect.Length > 0;

        public bool HasGpoLocalAdminsEffect => GpoLocalAdminsEffect.Length > 0;
        public bool HasBlockedSessionsEffect => BlockedSessionsEffect.Length > 0;

        // ─── GPO : paramètres qui changent les chemins d'attaque ──────────────────

        /// <summary>« Interdire l'ouverture d'une session locale » ; vide = paramètre non défini.</summary>
        public string GpoDenyInteractiveLogonText
        {
            get => string.Join(Environment.NewLine, _obj.GpoDenyInteractiveLogon ?? []);
            set => _obj.GpoDenyInteractiveLogon = SetOptionalLines(_obj.GpoDenyInteractiveLogon, value);
        }

        /// <summary>« Interdire l'ouverture de session par les services Bureau à distance » ; vide = paramètre non défini.</summary>
        public string GpoDenyRemoteInteractiveLogonText
        {
            get => string.Join(Environment.NewLine, _obj.GpoDenyRemoteInteractiveLogon ?? []);
            set => _obj.GpoDenyRemoteInteractiveLogon = SetOptionalLines(_obj.GpoDenyRemoteInteractiveLogon, value);
        }

        /// <summary>Membres que la GPO met dans le groupe local Administrateurs ; vide = paramètre non défini.</summary>
        public string GpoLocalAdminsText
        {
            get => string.Join(Environment.NewLine, _obj.GpoLocalAdmins ?? []);
            set => _obj.GpoLocalAdmins = SetOptionalLines(_obj.GpoLocalAdmins, value);
        }

        public bool GpoReplaceLocalAdmins
        {
            get => _obj.GpoReplaceLocalAdmins;
            set
            {
                if (_obj.GpoReplaceLocalAdmins == value) return;
                _obj.GpoReplaceLocalAdmins = value;
                Touch();
            }
        }

        /// <summary>Requêtes du filtre WMI d'une GPO importée, d'où SMAD-X a tiré le filtre.</summary>
        public string GpoWmiQuery => _obj.GpoWmiQuery ?? string.Empty;
        public bool HasGpoWmiQuery => GpoWmiQuery.Length > 0;

        /// <summary>Choix du filtre WMI : vide (tous les ordinateurs), Servers ou Workstations.</summary>
        public static string[] GpoComputerFilters { get; } = [string.Empty, nameof(Models.GpoComputerFilter.Servers), nameof(Models.GpoComputerFilter.Workstations)];

        public string GpoComputerFilterChoice
        {
            get => _obj.GpoComputerFilter?.ToString() ?? string.Empty;
            set
            {
                Models.GpoComputerFilter? filter = Enum.TryParse<Models.GpoComputerFilter>(value, out var parsed) ? parsed : null;
                if (_obj.GpoComputerFilter == filter) return;
                _obj.GpoComputerFilter = filter;
                Touch();
            }
        }

        /// <summary>Liste d'un paramètre de GPO : null quand elle est vide, le paramètre n'étant alors pas défini.</summary>
        private ObservableCollection<string>? SetOptionalLines(ObservableCollection<string>? current, string? text, [CallerMemberName] string? property = null)
        {
            var target = current ?? new ObservableCollection<string>();
            ReplaceLines(target, text, property);
            return target.Count == 0 ? null : target;
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
