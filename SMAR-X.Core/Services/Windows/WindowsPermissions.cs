using System;
using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Windows
{
    /// <summary>Une identité du jeton ; « refus seulement » : elle compte pour les ACE Refuser, jamais pour les ACE Autoriser.</summary>
    public sealed record TokenSid(string Name, bool DenyOnly = false);

    /// <summary>
    /// Jeton d'accès d'une session : le compte, ses groupes, les identités prédéfinies (Everyone, Authenticated Users,
    /// INTERACTIVE pour une session locale, Users). Pour un administrateur, le contrôle de compte d'utilisateur (UAC)
    /// laisse Administrators en « refus seulement » tant que la session n'est pas élevée ; le compte Administrator intégré
    /// n'est pas filtré.
    /// </summary>
    public sealed record WindowsToken(WindowsUser User, IReadOnlyList<TokenSid> Sids, bool Elevated)
    {
        /// <summary>Privilège « Prendre possession de fichiers ou d'autres objets » : administrateurs, en session élevée.</summary>
        public bool CanTakeOwnership => Elevated;

        public static WindowsToken Local(WindowsUser user, bool elevated)
        {
            var admin = user.IsAdministrator;
            var effective = admin && (elevated || user.IsBuiltInAdministrator);
            var sids = new List<TokenSid>
            {
                new(user.Name), new(WellKnown.Everyone), new(WellKnown.AuthenticatedUsers), new(WellKnown.Interactive), new(WellKnown.Users)
            };
            sids.AddRange(user.Groups.Where(g => !string.Equals(g, WellKnown.Administrators, StringComparison.OrdinalIgnoreCase)
                                                 && !string.Equals(g, WellKnown.Users, StringComparison.OrdinalIgnoreCase))
                .Select(g => new TokenSid(g)));
            if (admin)
                sids.Add(new TokenSid(WellKnown.Administrators, DenyOnly: !effective));
            return new WindowsToken(user, sids, effective);
        }

        /// <summary>L'identité est dans le jeton ; pour une ACE Autoriser, elle doit y être active (pas « refus seulement »).</summary>
        public bool Has(string name, bool forAllow) =>
            Sids.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase) && (!forAllow || !s.DenyOnly));
    }

    public enum CheckStepKind
    {
        /// <summary>Le propriétaire lit et modifie toujours les autorisations (sauf ACE OWNER RIGHTS).</summary>
        Owner,
        /// <summary>Privilège « Prendre possession » d'un administrateur en session élevée.</summary>
        Privilege,
        /// <summary>ACE Autoriser qui s'applique au jeton.</summary>
        Allow,
        /// <summary>ACE Refuser qui s'applique au jeton et refuse un droit demandé, pas encore accordé.</summary>
        DenyBlocks,
        /// <summary>ACE Refuser qui s'applique au jeton, sans effet : ses droits sont déjà accordés, ou ne sont pas demandés.</summary>
        DenyNoEffect,
        /// <summary>Fin de la DACL : des droits demandés n'ont été accordés par aucune ACE.</summary>
        Missing
    }

    /// <summary>
    /// Une étape d'AccessCheck. <see cref="Rights"/> : droits accordés (Owner, Privilege, Allow), refusés (DenyBlocks) ou
    /// manquants (Missing). <see cref="Index"/> : rang de l'ACE dans la DACL effective (à partir de 1).
    /// </summary>
    public sealed record CheckStep(CheckStepKind Kind, NtfsRights Rights, EffectiveAce? Ace = null, int Index = 0);

    /// <summary>Résultat d'AccessCheck sur un objet : accordé ou non, et les étapes qui comptent, dans l'ordre.</summary>
    public sealed record AccessCheckResult(string Path, NtfsRights Desired, bool Granted, IReadOnlyList<CheckStep> Steps, string Owner);

    /// <summary>
    /// AccessCheck de Windows sur un fichier ou un dossier NTFS :
    /// <list type="number">
    /// <item>le propriétaire a d'office Autorisations de lecture et Modifier les autorisations, sauf si la DACL contient
    /// une ACE OWNER RIGHTS ; un administrateur en session élevée a le privilège Prendre possession ;</item>
    /// <item>les ACE se lisent dans l'ordre de la DACL : une ACE Refuser bloque si elle porte sur un droit demandé qui
    /// n'est pas encore accordé ; une ACE Autoriser accorde ses droits ; dès que tout est accordé, c'est autorisé ;</item>
    /// <item>à la fin de la DACL, ce qui n'a été accordé par personne est refusé.</item>
    /// </list>
    /// C'est pourquoi une ACE Autoriser explicite l'emporte sur un refus hérité : elle est lue avant.
    /// </summary>
    public static class WindowsPermissions
    {
        public const NtfsRights OwnerImplicit = NtfsRights.ReadPermissions | NtfsRights.ChangePermissions;

        public static AccessCheckResult Check(WindowsHost host, WindowsToken token, string path, NtfsRights desired)
        {
            var node = host.Find(path) ?? throw new ArgumentException($"{path} n'existe pas.", nameof(path));
            var dacl = NtfsInheritance.Dacl(host, path);
            var steps = new List<CheckStep>();
            var granted = NtfsRights.None;
            AccessCheckResult Done(bool ok) => new(path, desired, ok, steps, node.Owner);

            var isOwner = token.Has(node.Owner, forAllow: true);
            var ownerRights = dacl.Any(a => !a.InheritOnly && a.Trustee == WellKnown.OwnerRights);
            if (isOwner && !ownerRights && (desired & OwnerImplicit) != 0)
            {
                granted |= desired & OwnerImplicit;
                steps.Add(new CheckStep(CheckStepKind.Owner, desired & OwnerImplicit));
            }
            if (desired.HasFlag(NtfsRights.TakeOwnership) && token.CanTakeOwnership)
            {
                granted |= NtfsRights.TakeOwnership;
                steps.Add(new CheckStep(CheckStepKind.Privilege, NtfsRights.TakeOwnership));
            }
            if (granted == desired)
                return Done(true);

            for (var i = 0; i < dacl.Count; i++)
            {
                var ace = dacl[i];
                if (ace.InheritOnly) continue;
                var applies = ace.Trustee == WellKnown.OwnerRights ? isOwner : token.Has(ace.Trustee, forAllow: ace.Type == AceType.Allow);
                if (!applies) continue;

                if (ace.Type == AceType.Deny)
                {
                    var blocked = ace.Mask & desired & ~granted;
                    steps.Add(new CheckStep(blocked != 0 ? CheckStepKind.DenyBlocks : CheckStepKind.DenyNoEffect, blocked, ace, i + 1));
                    if (blocked != 0)
                        return Done(false);
                }
                else
                {
                    var added = ace.Mask & desired & ~granted;
                    if (added == 0) continue;
                    granted |= added;
                    steps.Add(new CheckStep(CheckStepKind.Allow, added, ace, i + 1));
                    if (granted == desired)
                        return Done(true);
                }
            }

            steps.Add(new CheckStep(CheckStepKind.Missing, desired & ~granted));
            return Done(false);
        }
    }
}
