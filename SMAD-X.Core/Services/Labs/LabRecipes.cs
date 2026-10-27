using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SMADX.Models;
using SMADX.Services.Audit;
using static SMADX.Services.Labs.LabDomainBuilder;

namespace SMADX.Services.Labs
{
    /// <summary>
    /// Une recette injecte dans un domaine sain le manque de configuration que vérifie une règle d'audit
    /// (et donc le point de contrôle ANSSI qu'elle cite). Elle renvoie les objets qu'il ne faut pas supprimer
    /// pour corriger : ils deviennent des gardes.
    /// </summary>
    public sealed record LabRecipe(string RuleId, LocalizedText Situation, Func<LabDomainBuilder, ADObject, IReadOnlyList<ADObject>> Inject)
    {
        public AuditRule Rule => AuditRules.All.Single(r => r.Id == RuleId);

        /// <summary>Point de contrôle ANSSI cité par la règle, s'il y en a un.</summary>
        public AuditReference? Anssi => Rule.References.FirstOrDefault(r => r.Framework == AuditFramework.Anssi && !r.Id.StartsWith("ANSSI-PA"));
    }

    /// <summary>
    /// « Un exercice par recommandation » : pour chaque règle d'audit, une recette qui fabrique un domaine sain,
    /// peuplé, avec ce seul manque de configuration. SMAD-X Studio en tire un exercice prêt à relire.
    /// </summary>
    public static class LabRecipes
    {
        private static LocalizedText L(string fr, string en) => LocalizedText.Of(fr, en);

        // ─── Emplacements, créés au besoin ───────────────────────────────────────

        private static ADObject OuNamed(LabDomainBuilder b, ADObject d, string fr, string en, string tier) =>
            Flatten(d).FirstOrDefault(o => o.Type == ADObjectType.OrganizationalUnit && o.Name == b.T(fr, en)) ?? b.Ou(d, b.T(fr, en), tier);

        private static ADObject Services(LabDomainBuilder b, ADObject d) => OuNamed(b, d, "Comptes de service", "Service Accounts", "Tier 1");
        private static ADObject Servers(LabDomainBuilder b, ADObject d) => OuNamed(b, d, "Serveurs", "Servers", "Tier 1");
        private static ADObject AdminOu(LabDomainBuilder b, ADObject d) => OuNamed(b, d, "Administration", "Admin Accounts", "Tier 0");
        private static ADObject Technical(LabDomainBuilder b, ADObject d) => OuNamed(b, d, "Comptes techniques", "Technical Accounts", "Tier 2");

        private static ADObject Admin(LabDomainBuilder b, ADObject d, string name, bool protectedFromDelegation = true)
        {
            var admin = b.User(AdminOu(b, d), name, "Tier 0", b.T("Compte d'administration du domaine.", "Domain admin account."));
            admin.MemberOf.Add("Domain Admins");
            admin.AdminCount = true;
            if (protectedFromDelegation) admin.AccountFlags = ADAccountFlags.NotDelegated;
            return admin;
        }

        private static ADObject Server(LabDomainBuilder b, ADObject d, string name, string fr, string en) =>
            b.Computer(Servers(b, d), name, "Tier 1", b.T(fr, en));

        private static ADObject FirstStaff(ADObject d) =>
            Flatten(d).First(o => o.Type == ADObjectType.User && o.Parent?.Parent?.Type == ADObjectType.OrganizationalUnit && o.Tier == "Tier 2" && o.MemberOf.Count > 0);

        private static ADObject PsoContainer(ADObject d) => Flatten(d).First(ADValidationService.IsPasswordSettingsContainer);

        private static ADObject Dc(ADObject d) => Flatten(d).First(o => o.Type == ADObjectType.Computer && o.Parent?.Name == "Domain Controllers");

        // ─── Catalogue ───────────────────────────────────────────────────────────

        public static IReadOnlyList<LabRecipe> All { get; } = new List<LabRecipe>
        {
            new("KRB-01", L("Une application métier a été installée l'an dernier par un prestataire.", "A business application was installed last year by a contractor."), (b, d) =>
            {
                var svc = b.User(Services(b, d), "svc-app", "Tier 0", b.T("Compte de service de l'application métier.", "Business application service account."));
                svc.ServicePrincipalNames.Add($"HTTP/app.{d.Name}");
                svc.MemberOf.Add("Domain Admins");
                svc.AdminCount = true;
                svc.AccountFlags = ADAccountFlags.NotDelegated;
                return [svc];
            }),
            new("KRB-02", L("Une application métier a été installée l'an dernier par un prestataire.", "A business application was installed last year by a contractor."), (b, d) =>
            {
                var svc = b.User(Services(b, d), "svc-app", "Tier 1", b.T("Compte de service de l'application métier.", "Business application service account."));
                svc.ServicePrincipalNames.Add($"HTTP/app.{d.Name}");
                return [svc];
            }),
            new("KRB-03", L("Un administrateur utilise un vieil outil de supervision.", "An administrator uses an old monitoring tool."), (b, d) =>
            {
                var admin = Admin(b, d, "adm-supervision");
                admin.AccountFlags |= ADAccountFlags.DontRequirePreauth;
                return [admin];
            }),
            new("KRB-04", L("Un ancien scanner réseau est toujours en service.", "An old network scanner is still in use."), (b, d) =>
            {
                var scan = b.User(Technical(b, d), b.T("scan-reseau", "network-scan"), "Tier 2", b.T("Compte du scanner réseau.", "Network scanner account."));
                scan.AccountFlags = ADAccountFlags.DontRequirePreauth;
                return [scan];
            }),
            new("KRB-05", L("L'annuaire a été installé il y a plusieurs années et n'a jamais été revu.", "The directory was installed years ago and never reviewed."), (b, d) =>
            {
                var krbtgt = Find(d, "krbtgt", ADObjectType.User);
                krbtgt.PwdLastSet = b.Reference.AddDays(-900);
                return [krbtgt];
            }),
            new("DLG-01", L("Un prestataire a installé un serveur applicatif « pour que l'authentification fonctionne ».", "A contractor installed an application server \"so that authentication works\"."), (b, d) =>
            {
                var srv = Server(b, d, "SRV-APP01", "Serveur applicatif.", "Application server.");
                srv.AccountFlags = ADAccountFlags.TrustedForDelegation;
                return [srv];
            }),
            new("DLG-02", L("Un prestataire a installé un serveur applicatif qui accède aux partages des contrôleurs de domaine.", "A contractor installed an application server that reaches domain controller shares."), (b, d) =>
            {
                var srv = Server(b, d, "SRV-APP01", "Serveur applicatif.", "Application server.");
                srv.AccountFlags = ADAccountFlags.TrustedToAuthForDelegation;
                srv.AllowedToDelegateTo.Add($"cifs/{Dc(d).Name}.{d.Name}");
                return [srv];
            }),
            new("DLG-03", L("Un prestataire a installé un serveur applicatif qui accède aux partages des contrôleurs de domaine.", "A contractor installed an application server that reaches domain controller shares."), (b, d) =>
            {
                var srv = Server(b, d, "SRV-APP01", "Serveur applicatif.", "Application server.");
                srv.AllowedToDelegateTo.Add($"cifs/{Dc(d).Name}.{d.Name}");
                return [srv];
            }),
            new("DLG-04", L("Un nouvel administrateur du domaine vient d'arriver.", "A new domain administrator has just joined."), (b, d) =>
                [Admin(b, d, "adm-nouveau", protectedFromDelegation: false)]),
            new("PWD-01", L("L'administrateur se plaint de devoir changer son mot de passe.", "The administrator complains about having to change their password."), (b, d) =>
            {
                var admin = Find(d, "Administrator", ADObjectType.User);
                admin.AccountFlags |= ADAccountFlags.PasswordNeverExpires;
                return [admin];
            }),
            new("PWD-02", L("Un salarié se plaint de devoir changer son mot de passe.", "An employee complains about having to change their password."), (b, d) =>
            {
                var user = FirstStaff(d);
                user.AccountFlags |= ADAccountFlags.PasswordNeverExpires;
                return [user];
            }),
            new("PWD-03", L("Un compte a été créé en urgence pour un intervenant.", "An account was created in a hurry for a contractor."), (b, d) =>
            {
                var user = b.User(Technical(b, d), b.T("intervenant", "contractor"), "Tier 2", b.T("Compte d'un intervenant extérieur.", "External contractor account."));
                user.AccountFlags = ADAccountFlags.PasswordNotRequired;
                return [user];
            }),
            new("PWD-04", L("Un administrateur historique est toujours en poste.", "A long-standing administrator is still in office."), (b, d) =>
            {
                var admin = Admin(b, d, "adm-historique");
                admin.PwdLastSet = b.Reference.AddDays(-1200);
                return [admin];
            }),
            new("PWD-05", L("Un contrôleur de domaine a été restauré depuis une ancienne sauvegarde.", "A domain controller was restored from an old backup."), (b, d) =>
            {
                var dc = Dc(d);
                dc.PwdLastSet = b.Reference.AddDays(-120);
                return [dc];
            }),
            new("PWD-06", L("Un poste de travail a été retrouvé dans un placard.", "A workstation was found in a cupboard."), (b, d) =>
            {
                var pc = Flatten(d).First(o => o.Type == ADObjectType.Computer && o.Parent?.Name == b.T("Postes de travail", "Workstations"));
                pc.PwdLastSet = b.Reference.AddDays(-200);
                return [pc];
            }),
            new("ACC-03", L("Pour gagner du temps, les techniciens joignent les postes au domaine avec le compte de l'utilisateur.",
                             "To save time, technicians join workstations to the domain with the user's own account."), (b, d) =>
            {
                d.MachineAccountQuota = 10;
                return [];
            }),
            new("PWD-08", L("Le domaine a été installé il y a des années ; sa politique de mot de passe n'a jamais été revue.",
                             "The domain was installed years ago; its password policy has never been reviewed."), (b, d) =>
            {
                d.PSOMinPasswordLength = 7;
                // Les administrateurs ont déjà leur propre PSO : seule la politique du domaine est en cause
                var pso = Add(PsoContainer(d), new ADObject("PSO-Admins", ADObjectType.PasswordSettingsObject)
                {
                    Tier = "Tier 0", Description = b.T("Politique des comptes d'administration.", "Policy of the administration accounts."),
                    PSOPrecedence = 10, PSOMinPasswordLength = 16, PSOPasswordHistoryCount = 24, PSOComplexityEnabled = true,
                    PSOMaxPasswordAgeDays = 365, PSOMinPasswordAgeDays = 1, PSOLockoutThreshold = 5, PSOLockoutDurationMinutes = 30,
                    PSOLockoutObservationWindowMinutes = 30
                });
                pso.PSOAppliesTo.Add("Domain Admins");
                return [];
            }),
            new("PWD-09", L("Un prestataire a créé une politique de mot de passe « pratique » pour les administrateurs.",
                             "A contractor created a \"convenient\" password policy for the administrators."), (b, d) =>
            {
                var pso = Add(PsoContainer(d), new ADObject("PSO-Admins", ADObjectType.PasswordSettingsObject)
                {
                    Tier = "Tier 0", Description = b.T("Politique des comptes d'administration.", "Policy of the administration accounts."),
                    PSOPrecedence = 10, PSOMinPasswordLength = 6, PSOPasswordHistoryCount = 0, PSOComplexityEnabled = false,
                    PSOMaxPasswordAgeDays = 0, PSOMinPasswordAgeDays = 0, PSOLockoutThreshold = 0
                });
                pso.PSOAppliesTo.Add("Domain Admins");
                return [Find(d, "Administrator", ADObjectType.User)];
            }),
            new("PWD-07", L("Une application utilise un compte de service géré (gMSA).", "An application uses a group managed service account (gMSA)."), (b, d) =>
            {
                var gmsa = Add(Services(b, d), new ADObject("gmsa-app", ADObjectType.GMSA)
                {
                    Tier = "Tier 1", Owner = "Domain Admins", Description = b.T("Compte de service géré de l'application.", "Application managed service account."),
                    PwdLastSet = b.Reference.AddDays(-200), LastLogonTimestamp = b.Reference.AddDays(-1), CreatedDate = b.Reference.AddDays(-400)
                });
                return [gmsa];
            }),
            new("ACC-01", L("Un intervenant extérieur avait besoin d'un accès rapide.", "An external contractor needed quick access."), (b, d) =>
            {
                var guest = Find(d, "Guest", ADObjectType.User);
                guest.AccountFlags &= ~ADAccountFlags.AccountDisabled;
                return [guest];
            }),
            new("ACC-02", L("Plusieurs salariés ont quitté l'entreprise cette année.", "Several employees left the company this year."), (b, d) =>
            {
                FirstStaff(d).LastLogonTimestamp = b.Reference.AddDays(-200);
                return [];
            }),
            new("PRIV-01", L("Un administrateur a été ajouté en urgence pendant un incident.", "An administrator was added in a hurry during an incident."), (b, d) =>
            {
                var admin = Admin(b, d, "adm-urgence");
                admin.AdminCount = false;
                return [admin];
            }),
            new("PRIV-02", L("Un ancien administrateur a changé de poste.", "A former administrator changed jobs."), (b, d) =>
            {
                var user = FirstStaff(d);
                user.AdminCount = true;
                return [user];
            }),
            new("PRIV-03", L("Un compte d'administration devait « créer des comptes plus vite ».", "An admin account was meant to \"create accounts faster\"."), (b, d) =>
            {
                var admin = b.User(AdminOu(b, d), "adm-comptes", "Tier 0", b.T("Compte d'administration.", "Admin account."));
                admin.MemberOf.Add("Account Operators");
                admin.AdminCount = true;
                admin.AccountFlags = ADAccountFlags.NotDelegated;
                return [admin];
            }),
            new("PRIV-04", L("L'équipe réseau gère les enregistrements DNS.", "The network team manages DNS records."), (b, d) =>
            {
                var user = b.User(Technical(b, d), b.T("tech-reseau", "network-tech"), "Tier 1", b.T("Technicien réseau.", "Network technician."));
                user.MemberOf.Add("DnsAdmins");
                return [user];
            }),
            new("PRIV-05", L("Le classement en tiers a été fait rapidement, il y a longtemps.", "Tier classification was done quickly, a long time ago."), (b, d) =>
            {
                var group = Find(d, "Backup Operators", ADObjectType.Group);
                group.Tier = "Tier 1";
                return [group];
            }),
            new("ACL-01", L("Un technicien a aidé à préparer l'annuaire lors de son installation.", "A technician helped prepare the directory when it was installed."), (b, d) =>
            {
                var group = Find(d, "Domain Admins", ADObjectType.Group);
                group.Owner = FirstStaff(d).Name;
                return [group];
            }),
            new("TIER-01", L("Un utilisateur a été ajouté à un groupe d'administration des serveurs.", "A user was added to a server administration group."), (b, d) =>
            {
                var group = b.Group(Servers(b, d), b.T("GG-Admins-Serveurs", "GG-Server-Admins"), "Tier 1", b.T("Administrateurs des serveurs.", "Server administrators."));
                var user = FirstStaff(d);
                user.MemberOf.Add(group.Name);
                return [group, user];
            }),
            new("TIER-02", L("Une GPO de bureautique a été réutilisée ailleurs.", "An office GPO was reused elsewhere."), (b, d) =>
            {
                var gpo = Add(Find(d, "Policies", ADObjectType.Container), new ADObject(b.T("GPO-Bureautique", "GPO-Office"), ADObjectType.Policy)
                {
                    Tier = "Tier 2", Owner = "Domain Admins", CreatedDate = b.Reference.AddDays(-400),
                    Description = b.T("Paramètres de bureautique.", "Office settings.")
                });
                Find(d, "Domain Controllers", ADObjectType.OrganizationalUnit).LinkedGPOs.Add(gpo.Name);
                return [gpo];
            }),
            new("TIER-03", L("Le support a reçu des droits pour dépanner plus vite.", "The help desk was given rights to troubleshoot faster."), (b, d) =>
            {
                var support = b.Group(Technical(b, d), b.T("GG-Support", "GG-HelpDesk-Team"), "Tier 2", b.T("Équipe du support.", "Help desk team."));
                var admins = AdminOu(b, d);
                Admin(b, d, "adm-exploitation");
                Delegate(admins, support.Name, TrusteeType.Group, "Reset Password", RightCategory.PasswordReset, "Tier 0");
                return [support];
            }),
            new("GRP-01", L("Deux équipes ont fusionné leurs groupes.", "Two teams merged their groups."), (b, d) =>
            {
                var ou = Technical(b, d);
                var a = b.Group(ou, b.T("GG-Equipe-A", "GG-Team-A"), "Tier 2", b.T("Équipe A.", "Team A."));
                var c = b.Group(ou, b.T("GG-Equipe-B", "GG-Team-B"), "Tier 2", b.T("Équipe B.", "Team B."));
                a.MemberOf.Add(c.Name);
                c.MemberOf.Add(a.Name);
                return [a, c];
            }),
        };

        /// <summary>Nom d'organisation tiré du domaine : « atelier-boreal.local » → « Atelier Boreal ».</summary>
        public static string OrganisationName(string domainName) =>
            CultureInfo.InvariantCulture.TextInfo.ToTitleCase(domainName.Split('.')[0].Replace('-', ' '));

        /// <summary>
        /// Exercice prêt à relire : domaine sain et peuplé, le manque de configuration injecté, énoncé à compléter,
        /// objectifs proposés (dont la règle visée) et gardes. Les notes du formateur citent le point de contrôle
        /// et la correction attendue, tirée de la fiche de la règle.
        /// </summary>
        public static (LabDefinition Lab, string TeacherNotes) CreateLab(LabRecipe recipe, string culture, string domainName, DateTime reference) =>
            CreateLab([recipe], culture, domainName, reference);

        /// <summary>
        /// Recettes qui ne peuvent pas être combinées dans un même domaine : deux recettes créent un objet du même nom,
        /// ou une recette annule le manque d'une autre. Renvoie l'explication, ou null si la combinaison est possible.
        /// </summary>
        public static string? Incompatibility(IReadOnlyList<LabRecipe> recipes, string culture, DateTime reference)
        {
            try { Inject(recipes, culture, "verification.local", reference); return null; }
            catch (InvalidOperationException ex) { return ex.Message; }
        }

        /// <summary>Domaine sain, puis chaque manque injecté ; vérifie que chaque règle visée est bien en échec.</summary>
        private static (LabDomainBuilder Builder, ADObject Domain, List<ADObject> Keep) Inject(IReadOnlyList<LabRecipe> recipes, string culture, string domainName, DateTime reference)
        {
            if (recipes.Count == 0) throw new ArgumentException("Au moins une recette.", nameof(recipes));
            if (recipes.Select(r => r.RuleId).Distinct().Count() != recipes.Count) throw new ArgumentException("Recette en double.", nameof(recipes));
            var builder = new LabDomainBuilder(culture, reference);
            var domain = builder.Healthy(domainName);
            string T(string fr, string en) => builder.T(fr, en);

            var keep = new List<ADObject>();
            var createdBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var recipe in recipes)
            {
                var before = Flatten(domain).ToHashSet(ReferenceEqualityComparer.Instance);
                var names = Flatten(domain).Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                keep.AddRange(recipe.Inject(builder, domain).Where(k => !keep.Contains(k)));
                foreach (var created in Flatten(domain).Where(o => !before.Contains(o)))
                {
                    if (names.Contains(created.Name))
                        throw new InvalidOperationException(createdBy.TryGetValue(created.Name, out var other)
                            ? T($"{other} et {recipe.RuleId} créent tous deux « {created.Name} » : choisissez l'une des deux.",
                                $"{other} and {recipe.RuleId} both create \"{created.Name}\": choose one of them.")
                            : T($"{recipe.RuleId} crée « {created.Name} », qui existe déjà.", $"{recipe.RuleId} creates \"{created.Name}\", which already exists."));
                    createdBy[created.Name] = recipe.RuleId;
                }
            }
            var failed = new ADSecurityAuditService().Run(domain, reference).FailedRules.Select(r => r.Id).ToHashSet();
            var missing = recipes.Where(r => !failed.Contains(r.RuleId)).Select(r => r.RuleId).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException(T($"Combinées, ces recettes ne font plus échouer {string.Join(", ", missing)} : retirez l'une d'elles.",
                                                      $"Combined, these recipes no longer fail {string.Join(", ", missing)}: remove one of them."));
            return (builder, domain, keep);
        }

        /// <summary>
        /// Exercice qui combine un ou plusieurs manques de configuration dans le même domaine : un objectif par règle visée,
        /// les objectifs recommandés par l'audit et les chemins d'attaque, et les gardes de chaque recette.
        /// </summary>
        /// <exception cref="InvalidOperationException">Les recettes ne peuvent pas être combinées (voir <see cref="Incompatibility"/>).</exception>
        public static (LabDefinition Lab, string TeacherNotes) CreateLab(IReadOnlyList<LabRecipe> recipes, string culture, string domainName, DateTime reference)
        {
            var (builder, domain, keep) = Inject(recipes, culture, domainName, reference);
            var document = builder.Document(domain);
            var org = OrganisationName(domainName);
            string T(string fr, string en) => builder.T(fr, en);
            var several = recipes.Count > 1;
            var situations = string.Join(" ", recipes.Select(r => r.Situation.Resolve(culture)).Distinct());

            var context = T(
                $"**{org}** compte environ 80 salariés. {situations}\n\nLe responsable de la sécurité vous demande de vérifier que l'annuaire respecte les recommandations de l'ANSSI avant l'audit annuel.",
                $"**{org}** has about 80 employees. {situations}\n\nThe security officer asks you to check that the directory follows ANSSI recommendations before the yearly audit.");
            var mission = several
                ? T("- Trouvez les configurations qui ne sont pas conformes et corrigez-les : il y en a plusieurs.\n- Ne supprimez pas de compte pour « corriger » : ce qui fonctionne doit continuer de fonctionner.",
                    "- Find the configurations that are not compliant and fix them: there are several.\n- Do not delete accounts to \"fix\" things: what works must keep working.")
                : T("- Trouvez la configuration qui n'est pas conforme et corrigez-la.\n- Ne supprimez pas de compte pour « corriger » : ce qui fonctionne doit continuer de fonctionner.",
                    "- Find the configuration that is not compliant and fix it.\n- Do not delete accounts to \"fix\" things: what works must keep working.");
            var title = T($"{org} : la revue de conformité", $"{org}: the compliance review");
            domain.Description = $"# {org}\n\n{context}\n";

            var statement = T(
                $"# {title}\n\n## Contexte\n{context}\n\n## Votre mission\n{mission}\n\n",
                $"# {title}\n\n## Context\n{context}\n\n## Your mission\n{mission}\n\n") + LabAuthoring.ToolsSection(culture);

            var targeted = recipes.Select(r => r.RuleId).ToHashSet();
            var objectives = LabAuthoring.Suggest(domain, reference)
                .Where(s => s.Recommended || s.Objective.Check.RuleId is { } id && targeted.Contains(id))
                .Select(s => s.Objective)
                .Where(o => o.Check.Type != LabCheckType.ObjectExists)
                .ToList();
            foreach (var obj in keep.Where(k => objectives.All(o => !(o.Check.Type == LabCheckType.ObjectExists && o.Check.Object == k.Name))))
                objectives.Add(new LabObjective
                {
                    Points = 0, Guard = true,
                    Title = LocalizedText.Of($"« {obj.Name} » existe toujours", $"\"{obj.Name}\" still exists"),
                    Hint = LocalizedText.Of("Supprimer l'objet n'est pas une correction.", "Deleting the object is not a fix."),
                    Check = new LabCheck { Type = LabCheckType.ObjectExists, Object = obj.Name, ObjectType = obj.Type }
                });
            LabAuthoring.Renumber(objectives);
            foreach (var o in objectives)
            {
                o.Title = new LocalizedText { [culture] = o.Title.Resolve(culture) };
                o.Hint = new LocalizedText { [culture] = o.Hint.Resolve(culture) };
            }

            var level = recipes.Count switch
            {
                1 => recipes[0].Rule.Severity <= AuditSeverity.High ? LabLevel.Beginner : LabLevel.Intermediate,
                <= 3 => LabLevel.Intermediate,
                _ => LabLevel.Advanced
            };
            var lab = new LabDefinition
            {
                Id = "anssi-" + string.Join("-", recipes.Select(r => r.RuleId.ToLowerInvariant())), Language = culture, Level = level,
                DurationMinutes = 15 + 10 * (recipes.Count - 1), ReferenceDate = reference.Date,
                Title = new LocalizedText { [culture] = title },
                Statement = new LocalizedText { [culture] = statement.ReplaceLineEndings("\n") },
                Start = document, Objectives = objectives
            };

            var loc = LocalizationService.Instance;
            var notes = new StringBuilder();
            foreach (var recipe in recipes)
            {
                var sheet = LabAuthoring.Sections(loc.Translate($"Audit.{recipe.RuleId}", culture));
                var fix = sheet.FirstOrDefault(kv => kv.Key is "Comment corriger" or "How to fix").Value ?? "";
                if (several) notes.AppendLine($"## {recipe.RuleId} · {sheet.GetValueOrDefault("#")}").AppendLine();
                notes.AppendLine(T($"**Manque de configuration injecté** : {recipe.RuleId}, {sheet.GetValueOrDefault("#")}", $"**Injected misconfiguration**: {recipe.RuleId}, {sheet.GetValueOrDefault("#")}")).AppendLine();
                foreach (var r in recipe.Rule.References)
                    notes.AppendLine($"- {r.FrameworkLabel} · {r.Id} : [{r.Title}]({r.Url})");
                notes.AppendLine().AppendLine(T("**Correction attendue** (fiche de la règle) :", "**Expected fix** (rule sheet):")).AppendLine().AppendLine(fix).AppendLine();
            }
            return (lab, notes.ToString().Trim().ReplaceLineEndings("\n"));
        }
    }
}
