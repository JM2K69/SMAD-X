using System.Collections.ObjectModel;

namespace SMADX.Models
{
    /// <summary>
    /// Silo de stratégies d'authentification (msDS-AuthNPolicySilo) et la stratégie qu'il applique à ses comptes
    /// utilisateurs : un compte du silo ne s'authentifie que depuis les ordinateurs du silo, ou ceux que la stratégie
    /// autorise en plus. En mode audit, rien n'est refusé : les refus sont seulement journalisés.
    /// </summary>
    public sealed class ADAuthenticationSilo
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>Le silo et sa stratégie sont en mode « appliquer » ; sinon, en mode audit.</summary>
        public bool Enforced { get; set; }

        /// <summary>
        /// Comptes, ordinateurs et comptes de service du silo. Un groupe y met ses membres : AD veut chaque compte, comme
        /// le fait le script de déploiement du modèle de Microsoft à partir de ses groupes d'ordinateurs.
        /// </summary>
        public ObservableCollection<string> Members { get; set; } = new();

        /// <summary>Ordinateurs, ou groupes d'ordinateurs, depuis lesquels les comptes du silo s'authentifient aussi.</summary>
        public ObservableCollection<string> AllowedFrom { get; set; } = new();

        public ADAuthenticationSilo Copy() => new()
        {
            Name = Name, Description = Description, Enforced = Enforced,
            Members = new ObservableCollection<string>(Members), AllowedFrom = new ObservableCollection<string>(AllowedFrom)
        };
    }
}
