using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SMADX.Models;
using SMADX.Services.AttackPaths;
using SMADX.Services.Audit;

namespace SMADX.Services.Labs
{
    /// <summary>Objectif proposé à l'auteur d'un exercice, avec la raison de la proposition.</summary>
    /// <param name="Recommended">Coché par défaut : les gardes ne le sont pas, c'est à l'auteur de juger ce qui est légitime.</param>
    public sealed record LabObjectiveSuggestion(LabObjective Objective, LocalizedText Reason, bool Recommended);

    /// <summary>
    /// Outils de l'auteur d'exercices (SMAD-X Studio et sa ligne de commande) : modèle d'énoncé, propositions
    /// d'objectifs à partir de l'audit et des chemins d'attaque, conversion des scénarios, description des
    /// vérifications. Tous les textes existent en français et en anglais.
    /// </summary>
    public static class LabAuthoring
    {
        private static LocalizationService Loc => LocalizationService.Instance;

        internal static string T(string culture, string fr, string en) => culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : fr;

        public static readonly string[] Cultures = ["fr-FR", "en-US"];

        // ─── Modèle d'énoncé ─────────────────────────────────────────────────────

        /// <summary>Énoncé à compléter : contexte, mission et outils, sans la réponse.</summary>
        public static string StatementTemplate(string culture, string title = "") => T(culture,
            $"# {(title.Length > 0 ? title : "Organisation : le problème en quelques mots")}\n\n" +
            "## Contexte\n" +
            "Qui est l'organisation (activité, taille), qui vous êtes (administrateur arrivé il y a un mois, prestataire, auditeur) et l'événement qui déclenche le travail (test d'intrusion, alerte, audit, passation).\n\n" +
            "## Votre mission\n" +
            "- Ce qu'il faut trouver et corriger.\n" +
            "- Les contraintes métier : ce qui doit continuer de fonctionner.\n\n" +
            "## Vos outils\n" +
            "- **Analyse > Chemins d'attaque** (`Ctrl+Shift+K`) : qui peut prendre le contrôle du domaine, et par quelles étapes.\n" +
            "- **Analyse > Audit de sécurité** (`Ctrl+Shift+A`) : les constats et leurs fiches.\n" +
            "- **Analyse > Avant / après** (`Ctrl+Shift+B`) : ce que vos modifications ont changé.\n" +
            "- **Vérifier**, dans cette fenêtre, corrige les objectifs ; **Indice** donne une piste par objectif.\n",
            $"# {(title.Length > 0 ? title : "Organisation: the problem in a few words")}\n\n" +
            "## Context\n" +
            "Who the organisation is (business, size), who you are (administrator who joined a month ago, contractor, auditor) and the event that triggers the work (penetration test, alert, audit, handover).\n\n" +
            "## Your mission\n" +
            "- What must be found and fixed.\n" +
            "- Business constraints: what must keep working.\n\n" +
            "## Your tools\n" +
            "- **Analyse > Attack paths** (`Ctrl+Shift+K`): who can take control of the domain, and through which steps.\n" +
            "- **Analyse > Security audit** (`Ctrl+Shift+A`): findings and their sheets.\n" +
            "- **Analyse > Before / after** (`Ctrl+Shift+B`): what your changes did.\n" +
            "- **Check**, in this window, grades the objectives; **Hint** gives a lead for each objective.\n");

        /// <summary>La section « Vos outils » du modèle d'énoncé, titre compris.</summary>
        public static string ToolsSection(string culture)
        {
            var heading = T(culture, "## Vos outils", "## Your tools");
            var template = StatementTemplate(culture);
            return template[template.IndexOf(heading, StringComparison.Ordinal)..];
        }

        // ─── Propositions d'objectifs ────────────────────────────────────────────

        /// <summary>Piste pour l'apprenant selon la technique d'entrée du chemin : elle oriente sans donner la correction.</summary>
        public static LocalizedText HintFor(AttackEdgeKind kind) => kind switch
        {
            AttackEdgeKind.MemberOf => LocalizedText.Of("Suivez les appartenances, y compris imbriquées.", "Follow memberships, including nested ones."),
            AttackEdgeKind.PrivilegedGroup => LocalizedText.Of("Quel groupe privilégié se trouve au bout du chemin ?", "Which privileged group lies at the end of the path?"),
            AttackEdgeKind.DomainController => LocalizedText.Of("Qui peut agir sur un contrôleur de domaine ?", "Who can act on a domain controller?"),
            AttackEdgeKind.Owner => LocalizedText.Of("Regardez les propriétaires des objets (section Sécurité).", "Look at object owners (Security section)."),
            AttackEdgeKind.FullControl => LocalizedText.Of("Regardez les délégations de contrôle total (fenêtre Délégations).", "Look at full control delegations (Delegations window)."),
            AttackEdgeKind.ResetPassword => LocalizedText.Of("Qui peut réinitialiser des mots de passe, et sur quels comptes ?", "Who can reset passwords, and on which accounts?"),
            AttackEdgeKind.WriteMembers => LocalizedText.Of("Qui peut modifier les membres des groupes ?", "Who can change group members?"),
            AttackEdgeKind.GpoLink => LocalizedText.Of("Regardez où les GPO sont liées, et qui peut les modifier.", "Look at where GPOs are linked, and who can edit them."),
            AttackEdgeKind.Kerberoasting => LocalizedText.Of("Un compte avec un SPN peut être kerberoasté par n'importe quel utilisateur.", "An account with an SPN can be kerberoasted by any user."),
            AttackEdgeKind.AsRepRoasting => LocalizedText.Of("Regardez les options Kerberos des comptes (section Sécurité).", "Look at the accounts' Kerberos options (Security section)."),
            AttackEdgeKind.EmptyPassword => LocalizedText.Of("Une option de compte accepte un mot de passe vide.", "One account option accepts an empty password."),
            AttackEdgeKind.UnconstrainedDelegation => LocalizedText.Of("Regardez les options de délégation des serveurs.", "Look at the servers' delegation options."),
            AttackEdgeKind.ConstrainedDelegationToDc => LocalizedText.Of("Vers quels services ce compte peut-il déléguer ?", "To which services can this account delegate?"),
            _ => LocalizedText.Of("Ouvrez Analyse > Chemins d'attaque.", "Open Analyse > Attack paths.")
        };

        private static string RuleTitle(AuditRule rule, string culture) =>
            Loc.Translate($"Audit.{rule.Id}", culture).Split('\n')[0].TrimStart('#', ' ').Trim();

        /// <summary>
        /// Objectifs proposés pour un domaine : couper chaque chemin hors Tier 0, n'en laisser aucun, lever les
        /// règles d'audit en échec ; et des gardes (objets du chemin à ne pas supprimer, délégations à garder),
        /// non cochées par défaut.
        /// </summary>
        public static IReadOnlyList<LabObjectiveSuggestion> Suggest(ADObject domain, DateTime now)
        {
            var suggestions = new List<LabObjectiveSuggestion>();
            var n = 0;
            string NextId() => $"o{++n}";

            var paths = new AttackPathService(domain, now).PathsOutsideTier0();
            foreach (var path in paths)
            {
                var source = path.Start.IsAnyUser ? AttackPathService.AnyUserId : path.Start.Label;
                var label = path.Start.IsAnyUser ? LocalizedText.Of("N'importe quel utilisateur du domaine", "Any domain user") : LocalizedText.Of(path.Start.Label, path.Start.Label);
                suggestions.Add(new LabObjectiveSuggestion(new LabObjective
                {
                    Id = NextId(), Points = path.Start.IsAnyUser ? 4 : 3,
                    Title = LocalizedText.Of($"Le chemin depuis « {label["fr-FR"]} » est coupé", $"The path from \"{label["en-US"]}\" is cut"),
                    Hint = HintFor(path.Steps[0].Kind),
                    Check = new LabCheck { Type = LabCheckType.NoAttackPath, Object = source }
                }, LocalizedText.Of($"Chemin en {path.Length} étape(s) vers le contrôle du domaine.", $"{path.Length}-step path to domain control."), true));
            }

            if (paths.Count > 0)
                suggestions.Add(new LabObjectiveSuggestion(new LabObjective
                {
                    Id = NextId(), Points = 2,
                    Title = LocalizedText.Of("Plus aucun chemin vers le contrôle du domaine hors Tier 0", "No path to domain control left outside Tier 0"),
                    Hint = LocalizedText.Of("Actualisez Chemins d'attaque : la liste doit être vide.", "Refresh Attack paths: the list must be empty."),
                    Check = new LabCheck { Type = LabCheckType.NoAttackPath }
                }, LocalizedText.Of("Montre qu'un seul maillon coupé ne suffit pas toujours.", "Shows that cutting one link is not always enough."), true));

            var report = new ADSecurityAuditService().Run(domain, now);
            foreach (var rule in report.FailedRules.OrderBy(r => r.Severity))
                suggestions.Add(new LabObjectiveSuggestion(new LabObjective
                {
                    Id = NextId(), Points = rule.Severity switch { AuditSeverity.Critical => 3, AuditSeverity.High => 2, _ => 1 },
                    Title = LocalizedText.Of($"Lever le constat {rule.Id} : {RuleTitle(rule, "fr-FR")}", $"Clear finding {rule.Id}: {RuleTitle(rule, "en-US")}"),
                    Hint = LocalizedText.Of($"Lisez la fiche {rule.Id} dans l'audit de sécurité.", $"Read rule sheet {rule.Id} in the security audit."),
                    Check = new LabCheck { Type = LabCheckType.RuleClear, RuleId = rule.Id }
                }, LocalizedText.Of($"Règle d'audit en échec ({rule.Severity}).", $"Failed audit rule ({rule.Severity})."), rule.Severity <= AuditSeverity.High));

            // Gardes : on ne corrige pas en supprimant un compte, un serveur ou un contrôleur de domaine du chemin
            var onPaths = paths.SelectMany(p => p.Steps.Select(s => s.To).Prepend(p.Start))
                .Select(node => node.Object).OfType<ADObject>()
                .Where(o => o.IsAccount && o.Type != ADObjectType.Domain)
                .Distinct().ToList();
            foreach (var obj in onPaths)
                suggestions.Add(new LabObjectiveSuggestion(new LabObjective
                {
                    Id = NextId(), Points = 1,
                    Title = LocalizedText.Of($"« {obj.Name} » existe toujours", $"\"{obj.Name}\" still exists"),
                    Hint = LocalizedText.Of("Supprimer l'objet n'est pas une correction.", "Deleting the object is not a fix."),
                    Check = new LabCheck { Type = LabCheckType.ObjectExists, Object = obj.Name, ObjectType = obj.Type }
                }, LocalizedText.Of("Garde : interdit de « corriger » en supprimant l'objet.", "Guard: forbids \"fixing\" by deleting the object."), false));

            // Gardes : délégations de mandataires présents sur un chemin, qui peuvent être légitimes
            var trustees = paths.SelectMany(p => p.Steps.Select(s => s.From)).Select(nd => nd.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var target in AllObjects(domain))
                foreach (var d in target.Delegations.Where(d => !d.IsInherited && d.RightCategory != RightCategory.FullControl && trustees.Contains(d.TrusteeName)))
                    suggestions.Add(new LabObjectiveSuggestion(new LabObjective
                    {
                        Id = NextId(), Points = 1,
                        Title = LocalizedText.Of($"« {d.TrusteeName} » garde sa délégation {d.RightCategory} sur « {target.Name} »", $"\"{d.TrusteeName}\" keeps its {d.RightCategory} delegation on \"{target.Name}\""),
                        Hint = LocalizedText.Of("Cette délégation est légitime : gardez-la.", "This delegation is legitimate: keep it."),
                        Check = new LabCheck { Type = LabCheckType.DelegationExists, Trustee = d.TrusteeName, Target = target.Name, RightCategory = d.RightCategory }
                    }, LocalizedText.Of("Garde, si cette délégation fait partie du métier.", "Guard, if this delegation is part of the job."), false));

            return suggestions;
        }

        /// <summary>Renumérote les objectifs o1, o2… dans l'ordre.</summary>
        public static void Renumber(IList<LabObjective> objectives)
        {
            for (var i = 0; i < objectives.Count; i++)
                objectives[i].Id = $"o{i + 1}";
        }

        // ─── Conversion d'un scénario ────────────────────────────────────────────

        /// <summary>Sections Markdown de niveau 2 d'un texte : titre de section → contenu (le titre de niveau 1 sous la clé « # »).</summary>
        public static Dictionary<string, string> Sections(string markdown)
        {
            var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string current = "";
            var sb = new StringBuilder();
            foreach (var raw in markdown.ReplaceLineEndings("\n").Split('\n'))
            {
                if (raw.StartsWith("# ") && !sections.ContainsKey("#"))
                {
                    sections["#"] = raw[2..].Trim();
                    continue;
                }
                if (raw.StartsWith("## "))
                {
                    sections[current] = sb.ToString().Trim();
                    sb.Clear();
                    current = raw[3..].Trim();
                    continue;
                }
                sb.AppendLine(raw);
            }
            sections[current] = sb.ToString().Trim();
            return sections;
        }

        private static readonly string[] ContextHeadings = ["Contexte", "Context"];
        private static readonly string[] SolutionHeadings = ["Le chemin", "The path", "Le corriger", "Fix it"];

        /// <summary>
        /// Exercice tiré d'un domaine de scénario (comme ceux de Samples/AttackPaths) : la section Contexte de la
        /// description du domaine devient l'énoncé, « Le chemin » et « Le corriger » partent dans les notes du
        /// formateur et sont retirés du domaine donné à l'apprenant. Les objectifs proposés et cochés sont ajoutés.
        /// </summary>
        public static (LabDefinition Lab, string TeacherNotes) ConvertScenario(ADRootDocument document, string culture, string id, DateTime referenceDate)
        {
            var start = JsonSerializer.Deserialize<ADRootDocument>(JsonSerializer.Serialize(document, ADDataService.SerializerOptions), ADDataService.SerializerOptions)!;
            ADDataService.RestoreParentReferences(start.Domain, null);

            var sections = Sections(start.Domain.Description ?? "");
            var title = sections.TryGetValue("#", out var h1) && h1.Length > 0 ? h1 : start.Domain.Name;
            var context = ContextHeadings.Select(h => sections.GetValueOrDefault(h)).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                          ?? sections.GetValueOrDefault("") ?? "";

            var notes = new StringBuilder();
            foreach (var heading in SolutionHeadings.Where(h => !string.IsNullOrWhiteSpace(sections.GetValueOrDefault(h))))
                notes.AppendLine($"## {heading}").AppendLine().AppendLine(sections[heading]).AppendLine();

            // L'apprenant ne doit trouver la solution nulle part dans le domaine
            start.Domain.Description = $"# {title}\n\n{context}\n";
            foreach (var obj in AllObjects(start.Domain).Where(o => SolutionHeadings.Any(h => (o.Description ?? "").Contains("## " + h))))
                obj.Description = string.Join("\n\n", Sections(obj.Description ?? "")
                    .Where(kv => kv.Key != "#" && !SolutionHeadings.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                    .Select(kv => kv.Key.Length == 0 ? kv.Value : $"## {kv.Key}\n{kv.Value}")).Trim();

            var statement = T(culture,
                $"# {title}\n\n## Contexte\n{context}\n\n## Votre mission\n- Trouvez qui peut prendre le contrôle du domaine, et coupez ces chemins.\n- Ce qui fonctionne aujourd'hui doit continuer de fonctionner.\n\n",
                $"# {title}\n\n## Context\n{context}\n\n## Your mission\n- Find who can take control of the domain, and cut those paths.\n- What works today must keep working.\n\n") + ToolsSection(culture);

            var objectives = Suggest(start.Domain, referenceDate).Where(s => s.Recommended).Select(s => s.Objective).ToList();
            Renumber(objectives);
            var lab = new LabDefinition
            {
                Id = id, Language = culture, Level = LabLevel.Intermediate, DurationMinutes = 25, ReferenceDate = referenceDate.Date,
                Title = new LocalizedText { [culture] = title },
                Statement = new LocalizedText { [culture] = statement.ReplaceLineEndings("\n") },
                Start = start, Objectives = objectives
            };
            foreach (var o in objectives)
            {
                o.Title = new LocalizedText { [culture] = o.Title.Resolve(culture) };
                o.Hint = new LocalizedText { [culture] = o.Hint.Resolve(culture) };
            }
            return (lab, notes.ToString().Trim());
        }

        // ─── Description lisible d'une vérification ──────────────────────────────

        public static string Describe(LabCheck check, string culture)
        {
            string Obj() => check.Object ?? "?";
            return check.Type switch
            {
                LabCheckType.RuleClear => T(culture, $"plus de constat {check.RuleId}{(check.Object != null ? $" sur « {check.Object} »" : "")}", $"no {check.RuleId} finding{(check.Object != null ? $" on \"{check.Object}\"" : "")}"),
                LabCheckType.NoFindingAtOrAbove => T(culture, $"plus de constat de gravité {check.Severity} ou plus", $"no finding of severity {check.Severity} or worse"),
                LabCheckType.ScoreAtLeast => T(culture, $"score d'audit ≥ {check.Score}", $"audit score ≥ {check.Score}"),
                LabCheckType.NoAttackPath when check.Object is null => T(culture, "aucun chemin vers le domaine hors Tier 0", "no path to the domain outside Tier 0"),
                LabCheckType.NoAttackPath when check.Object == AttackPathService.AnyUserId => T(culture, "aucun chemin depuis n'importe quel utilisateur", "no path from any domain user"),
                LabCheckType.NoAttackPath => T(culture, $"aucun chemin depuis « {Obj()} »", $"no path from \"{Obj()}\""),
                LabCheckType.ObjectExists => T(culture, $"« {Obj()} » existe", $"\"{Obj()}\" exists"),
                LabCheckType.ObjectAbsent => T(culture, $"« {Obj()} » n'existe plus", $"\"{Obj()}\" no longer exists"),
                LabCheckType.MemberOf => T(culture, $"« {Obj()} » membre de « {check.Group} »", $"\"{Obj()}\" member of \"{check.Group}\""),
                LabCheckType.NotMemberOf => T(culture, $"« {Obj()} » n'est plus membre de « {check.Group} »", $"\"{Obj()}\" no longer member of \"{check.Group}\""),
                LabCheckType.HasFlag => T(culture, $"option {check.Flag} activée sur « {Obj()} »", $"option {check.Flag} set on \"{Obj()}\""),
                LabCheckType.NotHasFlag => T(culture, $"option {check.Flag} désactivée sur « {Obj()} »", $"option {check.Flag} cleared on \"{Obj()}\""),
                LabCheckType.TierIs => T(culture, $"« {Obj()} » classé en {check.Tier}", $"\"{Obj()}\" classified as {check.Tier}"),
                LabCheckType.DelegationExists => T(culture, $"« {check.Trustee} » garde {check.RightCategory} sur « {check.Target} »", $"\"{check.Trustee}\" keeps {check.RightCategory} on \"{check.Target}\""),
                LabCheckType.DelegationOnly => T(culture, $"« {check.Trustee} » n'a que {string.Join(", ", check.AllowedCategories ?? [])}", $"\"{check.Trustee}\" only has {string.Join(", ", check.AllowedCategories ?? [])}"),
                LabCheckType.NotModified => T(culture, $"« {Obj()} » inchangé", $"\"{Obj()}\" unchanged"),
                _ => check.Type.ToString()
            };
        }

        /// <summary>Objectif de garde : déjà atteint au départ, il refuse une solution brutale.</summary>
        public static bool IsGuard(LabCheck check) =>
            check.Type is LabCheckType.ObjectExists or LabCheckType.MemberOf or LabCheckType.DelegationExists or LabCheckType.NotModified or LabCheckType.TierIs or LabCheckType.HasFlag;

        internal static IEnumerable<ADObject> AllObjects(ADObject root) => new[] { root }.Concat(root.Children.SelectMany(AllObjects));
    }
}
