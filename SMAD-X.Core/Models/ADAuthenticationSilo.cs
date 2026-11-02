using System.Collections.ObjectModel;

namespace SMADX.Models
{
    /// <summary>
    /// Silo de stratégies d'authentification (msDS-AuthNPolicySilo) et la stratégie qu'il applique à ses comptes : il
    /// associe des identités (utilisateurs, groupes, comptes de service) aux ordinateurs depuis lesquels elles peuvent
    /// s'authentifier. En mode audit, rien n'est refusé : les refus sont seulement journalisés.
    /// </summary>
    public sealed class ADAuthenticationSilo
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>Le silo et sa stratégie sont en mode « appliquer » ; sinon, en mode audit.</summary>
        public bool Enforced { get; set; }

        /// <summary>
        /// Identités du silo : utilisateurs, comptes de service, ou groupes qui y mettent leurs membres (AD veut chaque compte,
        /// comme le fait le script de déploiement du modèle de Microsoft à partir de ses groupes).
        /// </summary>
        public ObservableCollection<string> Members { get; set; } = new();

        /// <summary>
        /// Ordinateurs, ou groupes d'ordinateurs, depuis lesquels les identités du silo s'authentifient : ceux du silo et ceux
        /// que sa stratégie autorise. Everyone : la stratégie ne restreint pas l'origine de l'authentification.
        /// </summary>
        public ObservableCollection<string> Computers { get; set; } = new();

        public ADAuthenticationSilo Copy() => new()
        {
            Name = Name, Description = Description, Enforced = Enforced,
            Members = new ObservableCollection<string>(Members), Computers = new ObservableCollection<string>(Computers)
        };
    }
}
