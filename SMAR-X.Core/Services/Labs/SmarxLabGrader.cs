using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SMADX.Models;
using SMADX.Services.Labs;
using SMARX.Models;
using SMARX.Services.Linux;
using SMARX.Services.Windows;

namespace SMARX.Services.Labs
{
    /// <summary>Résultat d'un objectif : la part atteinte (0 à 1) et une explication pour l'apprenant.</summary>
    public sealed record SmarxObjectiveResult(SmarxObjective Objective, double Fraction, string Feedback)
    {
        public bool Passed => Fraction >= 1;
        /// <summary>Annulé par une garde violée.</summary>
        public bool Annulled { get; init; }
        public double EarnedPoints => Objective.Guard ? 0 : Math.Round(Objective.Points * Fraction, 2);
    }

    public sealed record SmarxLabResult(IReadOnlyList<SmarxObjectiveResult> Objectives, IReadOnlyList<LabQuestionResult> Questions, int MaxPoints)
    {
        public double Score => Math.Round(Objectives.Sum(o => o.EarnedPoints) + Questions.Sum(q => q.EarnedPoints), 2);
        /// <summary>Tous les objectifs (gardes comprises) sont atteints.</summary>
        public bool Completed => Objectives.All(o => o.Passed);
    }

    /// <summary>
    /// Correction d'un exercice SMAR-X : chaque objectif est vérifié par les moteurs (les mêmes que le testeur et la
    /// matrice), sur les serveurs de l'apprenant. Une garde violée annule les objectifs qui portent sur le même
    /// compte ou le même chemin : on ne gagne pas de points en ouvrant tout.
    /// </summary>
    public static class SmarxLabGrader
    {
        private static readonly string[] BroadTrustees = [WellKnown.Everyone, WellKnown.AuthenticatedUsers, WellKnown.Users];
        private const NtfsRights WriteRights = NtfsRights.WriteData | NtfsRights.AppendData | NtfsRights.Delete
                                               | NtfsRights.DeleteSubdirectoriesAndFiles | NtfsRights.ChangePermissions | NtfsRights.TakeOwnership;

        public static SmarxLabResult Grade(SmarxLab lab, SmarxDocument work, IReadOnlyDictionary<string, LabAnswer>? answers, string culture)
        {
            var raw = lab.Objectives.Select(o => Evaluate(lab, o, work, culture)).ToList();

            // Gardes violées : les comptes et les chemins qu'elles protégeaient
            var accounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new List<string>();
            foreach (var guard in raw.Where(r => r.Objective.Guard && !r.Passed))
            {
                var check = guard.Objective.Check;
                if (check.Type == SmarxCheckType.AccountUnchanged)
                    accounts.UnionWith(ChangedAccounts(lab.Start, work, check));
                else
                    accounts.UnionWith(check.AccountsInvolved);
                paths.AddRange(check.PathsInvolved);
            }

            var objectives = raw.Select(r =>
            {
                if (r.Objective.Guard) return r;
                var check = r.Objective.Check;
                var touched = check.AccountsInvolved.Any(accounts.Contains)
                              || check.PathsInvolved.Any(p => paths.Any(g => Under(check.System, p, g)));
                if (!touched) return r;
                // Annulé, même s'il n'était pas atteint : la garde violée est la première chose à corriger
                var cancelled = T(culture, "Annulé : une garde n'est plus respectée (voir plus bas).", "Cancelled: a guard is no longer met (see below).");
                return r with { Fraction = 0, Annulled = true, Feedback = r.Passed || r.Fraction > 0 ? cancelled : $"{cancelled} {r.Feedback}" };
            }).ToList();

            var questions = lab.Questions.Select(q => LabQuestionGrader.Grade(q, answers?.GetValueOrDefault(q.Id))).ToList();
            return new SmarxLabResult(objectives, questions, lab.MaxPoints);
        }

        public static SmarxObjectiveResult Evaluate(SmarxLab lab, SmarxObjective objective, SmarxDocument work, string culture)
        {
            var c = objective.Check;
            try
            {
                return c.Type switch
                {
                    SmarxCheckType.AccessAllowed => Access(objective, work, culture, expected: true),
                    SmarxCheckType.AccessDenied => Access(objective, work, culture, expected: false),
                    SmarxCheckType.ExactAccess => Exact(objective, work, culture),
                    SmarxCheckType.CreatedAllows => Created(objective, work, culture),
                    SmarxCheckType.Hygiene => Hygiene(objective, work, culture),
                    SmarxCheckType.AccountUnchanged => AccountUnchanged(lab, objective, work, culture),
                    SmarxCheckType.PathUnchanged => PathUnchanged(lab, objective, work, culture),
                    _ => new SmarxObjectiveResult(objective, 0, "?")
                };
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return new SmarxObjectiveResult(objective, 0, T(culture, $"Vérification impossible : {ex.Message}", $"Cannot check: {ex.Message}"));
            }
        }

        // ─── Accès ───────────────────────────────────────────────────────────────

        /// <summary>L'accès est-il permis ? Null avec une raison quand il ne peut pas être évalué (compte, chemin ou partage absent).</summary>
        public static bool? Allowed(SmarxDocument doc, SmarxSystem system, string account, string operation, string path, string? share, string culture, out string target, out string? problem)
        {
            problem = null;
            target = path;
            if (system == SmarxSystem.Linux)
            {
                var host = doc.Linux.FirstOrDefault() ?? throw new InvalidOperationException(T(culture, "pas de serveur Linux", "no Linux server"));
                var op = Enum.Parse<LinuxOperation>(operation);
                var user = host.Users.FirstOrDefault(u => u.Name == account);
                if (user == null) { problem = T(culture, $"le compte {account} n'existe pas", $"account {account} does not exist"); return null; }
                if (host.Find(path) == null) { problem = T(culture, $"{path} n'existe pas", $"{path} does not exist"); return false; }
                if (share == null) return LinuxAccess.Check(host, user, path, op).Allowed;
                var samba = host.Shares.FirstOrDefault(s => s.Name == share);
                if (samba == null) { problem = T(culture, $"le partage {share} n'existe pas", $"share {share} does not exist"); return false; }
                if (!samba.Contains(path)) { problem = T(culture, $"{path} n'est pas dans le partage {share}", $"{path} is not in share {share}"); return false; }
                target = samba.Unc(host.Name, path);
                return SambaAccess.Check(host, user, samba, path, op).Allowed;
            }
            else
            {
                var host = doc.Windows.FirstOrDefault() ?? throw new InvalidOperationException(T(culture, "pas de serveur Windows", "no Windows server"));
                var op = Enum.Parse<WindowsOperation>(operation);
                var user = host.User(account);
                if (user == null) { problem = T(culture, $"le compte {account} n'existe pas", $"account {account} does not exist"); return null; }
                if (host.Find(path) == null) { problem = T(culture, $"{path} n'existe pas", $"{path} does not exist"); return false; }
                if (share == null) return WindowsAccess.Check(host, user, path, op).Allowed;
                var smb = host.Share(share);
                if (smb == null) { problem = T(culture, $"le partage {share} n'existe pas", $"share {share} does not exist"); return false; }
                if (!smb.Contains(path)) { problem = T(culture, $"{path} n'est pas dans le partage {share}", $"{path} is not in share {share}"); return false; }
                target = smb.Unc(host.Name, path);
                return SmbAccess.Check(host, user, smb, path, op).Allowed;
            }
        }

        public static string OperationLabel(SmarxSystem system, string operation, string culture)
        {
            var label = system == SmarxSystem.Linux
                ? LinuxExplainer.Operation(Enum.Parse<LinuxOperation>(operation), culture)
                : WindowsExplainer.Operation(Enum.Parse<WindowsOperation>(operation), culture);
            return label.Length > 0 ? char.ToLowerInvariant(label[0]) + label[1..] : label;
        }

        private static SmarxObjectiveResult Access(SmarxObjective objective, SmarxDocument work, string culture, bool expected)
        {
            var c = objective.Check;
            var allowed = Allowed(work, c.System, c.Account!, c.Operation!, c.Path!, c.Share, culture, out var target, out var problem);
            var what = $"{c.Account} → {OperationLabel(c.System, c.Operation!, culture)} {target}";
            if (allowed == null) return new SmarxObjectiveResult(objective, 0, problem!);
            var ok = allowed == expected;
            var feedback = allowed.Value
                ? T(culture, $"{what} : autorisé.", $"{what}: allowed.")
                : T(culture, $"{what} : refusé.", $"{what}: denied.") + (problem != null ? $" ({problem})" : "");
            return new SmarxObjectiveResult(objective, ok ? 1 : 0, feedback);
        }

        /// <summary>Les accès nécessaires, « compte opération chemin » (le chemin peut contenir des espaces).</summary>
        public static HashSet<(string Account, string Operation, string Path)> Needs(SmarxCheck check) =>
            (check.Needs ?? []).Select(n => n.Split(' ', 3, StringSplitOptions.TrimEntries))
                .Where(p => p.Length == 3)
                .Select(p => (p[0], p[1], p[2]))
                .ToHashSet();

        private static SmarxObjectiveResult Exact(SmarxObjective objective, SmarxDocument work, string culture)
        {
            var c = objective.Check;
            var needs = Needs(c);
            int total = 0, right = 0;
            var wrong = new List<string>();
            foreach (var account in c.Accounts ?? [])
            foreach (var path in c.Paths ?? [])
            foreach (var operation in c.Operations ?? [])
            {
                if (!Applies(work, c.System, operation, path)) continue;
                total++;
                var needed = needs.Contains((account, operation, path));
                var allowed = Allowed(work, c.System, account, operation, path, c.Share, culture, out var target, out _) == true;
                if (allowed == needed) { right++; continue; }
                var what = $"{account} → {OperationLabel(c.System, operation, culture)} {target}";
                wrong.Add(needed
                    ? T(culture, $"{what} : refusé, alors que c'est nécessaire", $"{what}: denied, though it is needed")
                    : T(culture, $"{what} : autorisé, alors que ce n'est pas nécessaire", $"{what}: allowed, though it is not needed"));
            }
            if (total == 0) return new SmarxObjectiveResult(objective, 0, T(culture, "Rien à vérifier.", "Nothing to check."));
            var fraction = objective.Partial ? (double)right / total : right == total ? 1 : 0;
            var feedback = wrong.Count == 0
                ? T(culture, $"Les {total} accès vérifiés sont exactement ceux qui sont nécessaires.", $"The {total} accesses checked are exactly the ones needed.")
                : T(culture, $"{right} accès justes sur {total}. ", $"{right} of {total} accesses right. ") + string.Join(T(culture, " ; ", "; "), wrong.Take(4)) + (wrong.Count > 4 ? T(culture, " ; …", "; …") : ".");
            return new SmarxObjectiveResult(objective, fraction, feedback);
        }

        /// <summary>L'opération a un sens pour cet objet (lister un dossier, lire un fichier) ; un chemin absent compte.</summary>
        private static bool Applies(SmarxDocument doc, SmarxSystem system, string operation, string path)
        {
            if (system == SmarxSystem.Linux)
            {
                var host = doc.Linux.FirstOrDefault();
                var node = host?.Find(path);
                return node == null || LinuxAccess.Applies(Enum.Parse<LinuxOperation>(operation), node, path);
            }
            var whost = doc.Windows.FirstOrDefault();
            var wnode = whost?.Find(path);
            return wnode == null || WindowsAccess.Applies(Enum.Parse<WindowsOperation>(operation), wnode, path);
        }

        // ─── Création : ce que reçoit un nouveau fichier ─────────────────────────

        private static SmarxObjectiveResult Created(SmarxObjective objective, SmarxDocument work, string culture)
        {
            var c = objective.Check;
            var copy = Clone(work);
            const string Name = "nouveau-fichier.txt";
            string path;
            if (c.System == SmarxSystem.Linux)
            {
                var host = copy.Linux.First();
                var user = host.Users.FirstOrDefault(u => u.Name == c.Account) ?? throw new ArgumentException(c.Account);
                var created = LinuxCreation.Create(host, user, c.Path!, Name, LinuxNodeKind.File);
                if (created.Node == null)
                    return new SmarxObjectiveResult(objective, 0, T(culture, $"{c.Account} ne peut pas créer dans {c.Path}.", $"{c.Account} cannot create in {c.Path}."));
                host.Find(c.Path!)!.Children.Add(created.Node);
                path = LinuxHost.Combine(c.Path!, Name);
            }
            else
            {
                var host = copy.Windows.First();
                var user = host.User(c.Account!) ?? throw new ArgumentException(c.Account);
                var created = WindowsCreation.Create(host, user, c.Path!, Name, NtfsNodeKind.File);
                if (created.Node == null)
                    return new SmarxObjectiveResult(objective, 0, T(culture, $"{c.Account} ne peut pas créer dans {c.Path}.", $"{c.Account} cannot create in {c.Path}."));
                path = created.Path;
            }
            var allowed = Allowed(copy, c.System, c.Other!, c.Operation!, path, c.Share, culture, out _, out _) == true;
            var what = T(culture, $"un fichier créé par {c.Account} dans {c.Path}", $"a file created by {c.Account} in {c.Path}");
            var op = OperationLabel(c.System, c.Operation!, culture);
            return new SmarxObjectiveResult(objective, allowed ? 1 : 0, allowed
                ? T(culture, $"{c.Other} peut {op} {what}.", $"{c.Other} can {op} {what}.")
                : T(culture, $"{c.Other} ne peut pas {op} {what}.", $"{c.Other} cannot {op} {what}."));
        }

        // ─── Hygiène ─────────────────────────────────────────────────────────────

        private static SmarxObjectiveResult Hygiene(SmarxObjective objective, SmarxDocument work, string culture)
        {
            var c = objective.Check;
            var offenders = new List<string>();
            switch (c.Rule)
            {
                case SmarxHygieneRule.NoWorldWritable:
                case SmarxHygieneRule.NoSetuid:
                {
                    var host = work.Linux.First();
                    foreach (var (node, path) in Walk(host.Find(c.Path ?? "/"), c.Path ?? "/"))
                    {
                        if (c.Rule == SmarxHygieneRule.NoWorldWritable && node.OtherPerm.HasFlag(Perm.Write) && !(node.IsDirectory && node.Sticky))
                            offenders.Add(path);
                        if (c.Rule == SmarxHygieneRule.NoSetuid && !node.IsDirectory && (node.SetUid || node.SetGid))
                            offenders.Add(path);
                    }
                    break;
                }
                case SmarxHygieneRule.NoBroadWrite:
                {
                    var host = work.Windows.First();
                    foreach (var (_, path) in Walk(host.Find(c.Path ?? "D:"), c.Path ?? "D:"))
                        if (NtfsInheritance.Dacl(host, path).Any(a => a.Type == AceType.Allow && !a.InheritOnly
                                                                     && BroadTrustees.Contains(a.Trustee, StringComparer.OrdinalIgnoreCase)
                                                                     && (a.Mask & WriteRights) != 0))
                            offenders.Add(path);
                    break;
                }
                case SmarxHygieneRule.ShareLeastPrivilege:
                {
                    var host = work.Windows.First();
                    var shares = c.Share != null
                        ? host.Shares.Where(s => string.Equals(s.Name, c.Share, StringComparison.OrdinalIgnoreCase)).ToList()
                        : host.Shares.Where(s => c.Path == null || Under(SmarxSystem.Windows, s.Path, c.Path)).ToList();
                    if (c.Share != null && shares.Count == 0)
                        return new SmarxObjectiveResult(objective, 0, T(culture, $"Le partage {c.Share} n'existe pas.", $"Share {c.Share} does not exist."));
                    foreach (var share in shares)
                    foreach (var ace in share.Permissions.Where(a => a.Type == AceType.Allow))
                    {
                        var broad = BroadTrustees.Contains(ace.Trustee, StringComparer.OrdinalIgnoreCase) && ace.Right >= ShareRight.Change;
                        var full = ace.Right == ShareRight.FullControl && !string.Equals(ace.Trustee, WellKnown.Administrators, StringComparison.OrdinalIgnoreCase);
                        if (broad || full) offenders.Add($"{share.Name} : {ace.Trustee} {ace.Right}");
                    }
                    break;
                }
            }
            return new SmarxObjectiveResult(objective, offenders.Count == 0 ? 1 : 0, offenders.Count == 0
                ? T(culture, "Règle respectée.", "Rule met.")
                : T(culture, "Règle non respectée : ", "Rule not met: ") + string.Join(", ", offenders.Take(5)) + (offenders.Count > 5 ? ", …" : "."));
        }

        private static IEnumerable<(LinuxNode Node, string Path)> Walk(LinuxNode? node, string path)
        {
            if (node == null) yield break;
            yield return (node, path);
            foreach (var child in node.Children)
            foreach (var item in Walk(child, LinuxHost.Combine(path, child.Name)))
                yield return item;
        }

        private static IEnumerable<(NtfsNode Node, string Path)> Walk(NtfsNode? node, string path)
        {
            if (node == null) yield break;
            yield return (node, path);
            foreach (var child in node.Children)
            foreach (var item in Walk(child, WindowsHost.Combine(path, child.Name)))
                yield return item;
        }

        // ─── Gardes ──────────────────────────────────────────────────────────────

        /// <summary>Les comptes changés depuis le départ (groupes, groupe principal, umask), ajoutés ou supprimés.</summary>
        public static IReadOnlyList<string> ChangedAccounts(SmarxDocument start, SmarxDocument work, SmarxCheck check)
        {
            Dictionary<string, string> Accounts(SmarxDocument doc) => check.System == SmarxSystem.Linux
                ? (doc.Linux.FirstOrDefault()?.Users ?? []).ToDictionary(u => u.Name, u => $"{u.Uid}|{u.PrimaryGroup}|{string.Join(',', u.Groups.Order())}|{u.Umask}")
                : (doc.Windows.FirstOrDefault()?.Users ?? []).ToDictionary(u => u.Name, u => string.Join(',', u.Groups.Order(StringComparer.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase);
            var before = Accounts(start);
            var after = Accounts(work);
            return before.Keys.Union(after.Keys)
                .Where(name => check.Account is null or "*" || string.Equals(name, check.Account, StringComparison.OrdinalIgnoreCase))
                .Where(name => before.GetValueOrDefault(name) != after.GetValueOrDefault(name))
                .Order()
                .ToList();
        }

        private static SmarxObjectiveResult AccountUnchanged(SmarxLab lab, SmarxObjective objective, SmarxDocument work, string culture)
        {
            var changed = ChangedAccounts(lab.Start, work, objective.Check);
            return new SmarxObjectiveResult(objective, changed.Count == 0 ? 1 : 0, changed.Count == 0
                ? T(culture, "Les comptes n'ont pas changé.", "The accounts are unchanged.")
                : T(culture, $"Comptes modifiés : {string.Join(", ", changed)}.", $"Changed accounts: {string.Join(", ", changed)}."));
        }

        private static SmarxObjectiveResult PathUnchanged(SmarxLab lab, SmarxObjective objective, SmarxDocument work, string culture)
        {
            var c = objective.Check;
            var same = Rights(lab.Start, c) == Rights(work, c);
            return new SmarxObjectiveResult(objective, same ? 1 : 0, same
                ? T(culture, $"Les droits de {c.Path} n'ont pas changé.", $"The permissions of {c.Path} are unchanged.")
                : T(culture, $"Les droits de {c.Path} ont changé.", $"The permissions of {c.Path} have changed."));
        }

        /// <summary>Propriétaire et droits d'un chemin, sans ses enfants.</summary>
        private static string Rights(SmarxDocument doc, SmarxCheck check)
        {
            if (check.System == SmarxSystem.Linux)
            {
                var node = doc.Linux.FirstOrDefault()?.Find(check.Path!);
                return node == null ? "" : JsonSerializer.Serialize(new { node.Owner, node.Group, node.ModeBits, node.Acl, node.DefaultAcl });
            }
            var wnode = doc.Windows.FirstOrDefault()?.Find(check.Path!);
            return wnode == null ? "" : JsonSerializer.Serialize(new { wnode.Owner, wnode.InheritanceEnabled, wnode.Aces }, SmarxDocument.SerializerOptions);
        }

        // ─── Outils ──────────────────────────────────────────────────────────────

        public static SmarxDocument Clone(SmarxDocument doc) => SmarxDocument.Parse(doc.Serialize());

        /// <summary>Le chemin est le dossier lui-même ou dedans.</summary>
        public static bool Under(SmarxSystem system, string path, string folder)
        {
            if (system == SmarxSystem.Linux)
                return path == folder || folder == "/" || path.StartsWith(folder.TrimEnd('/') + "/", StringComparison.Ordinal);
            return string.Equals(path.TrimEnd('\\'), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(folder.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static string T(string culture, string fr, string en) => culture == "fr-FR" ? fr : en;
    }
}
