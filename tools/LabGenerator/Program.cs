// Génère les exercices fournis avec SMAD-X : SMAD-X.Core/Labs/*.smadx-lab.json
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;
using SMADX.Services.Labs;

var outDir = args[0];
Directory.CreateDirectory(outDir);
var reference = new DateTime(2026, 10, 22);
var data = new ADDataService();
var labs = new LabService();

ADObject Find(ADObject root, string name, ADObjectType? type = null) =>
    Flatten(root).First(o => o.Name == name && (type is null || o.Type == type));
IEnumerable<ADObject> Flatten(ADObject o) => new[] { o }.Concat(o.Children.SelectMany(Flatten));
ADObject Add(ADObject parent, ADObject child) { child.Parent = parent; parent.Children.Add(child); child.UpdateDistinguishedNameTree(); return child; }
ADObject User(ADObject parent, string name, string tier) => Add(parent, new ADObject(name, ADObjectType.User)
{ Tier = tier, Owner = "Domain Admins", PwdLastSet = reference.AddDays(-30), LastLogonTimestamp = reference.AddDays(-1), CreatedDate = reference.AddDays(-400) });

// Domaine neuf, dates figées à la date de référence
ADObject Fresh()
{
    TierConfigurationService.Instance.ResetToDefaults();
    var d = data.CreateFreshDomainStructure("contoso.local");
    foreach (var o in Flatten(d))
    {
        o.CreatedDate = reference.AddDays(-400);
        o.ModifiedDate = reference.AddDays(-400);
        if (o.PwdLastSet != null) o.PwdLastSet = reference.AddDays(-30);
        if (o.LastLogonTimestamp != null) o.LastLogonTimestamp = reference.AddDays(-1);
    }
    return d;
}

// Domaine durci : les constats d'un domaine neuf sont corrigés (c'est la solution de l'exercice 1)
ADObject Baseline()
{
    var d = Fresh();
    var admin = Find(d, "Administrator", ADObjectType.User);
    admin.AccountFlags = ADAccountFlags.NotDelegated;
    admin.MemberOf.Remove("Enterprise Admins");
    admin.MemberOf.Remove("Schema Admins");
    d.Tier = "Tier 0";
    var findings = new ADSecurityAuditService().Run(d, reference).Findings;
    if (findings.Count > 0) throw new Exception("Baseline non propre : " + string.Join(", ", findings.Select(f => f.Rule.Id + " " + f.Object.Name)));
    return d;
}

LabObjective Obj(string id, int points, string fr, string en, string hintFr, string hintEn, LabCheck check) => new()
{ Id = id, Points = points, Title = LocalizedText.Of(fr, en), Hint = LocalizedText.Of(hintFr, hintEn), Check = check };

void Save(string id, LabLevel level, int minutes, string titleFr, string titleEn, string stFr, string stEn, ADObject domain, params LabObjective[] objectives)
{
    var lab = new LabDefinition
    {
        Id = id, Level = level, DurationMinutes = minutes, ReferenceDate = reference,
        Title = LocalizedText.Of(titleFr, titleEn), Statement = LocalizedText.Of(stFr, stEn),
        Start = new ADRootDocument { Domain = domain, SitesTopology = data.CreateFreshTopology(domain.Name) },
        Objectives = objectives.ToList()
    };
    labs.SaveFile(lab, Path.Combine(outDir, id + LabService.FileExtension));
    Console.WriteLine($"{id}: {objectives.Length} objectifs, {lab.MaxPoints} points");
}

// ─── 1. Premier audit ─────────────────────────────────────────────────────────
Save("01-premier-audit", LabLevel.Beginner, 20,
    "Premier audit : un AD tout neuf est-il conforme ?",
    "First audit: is a brand-new AD compliant?",
    """
    # Premier audit

    Vous venez de promouvoir le premier contrôleur de domaine de **contoso.local**. Rien n'a encore été configuré : c'est l'état « sortie d'installation ».

    ## Votre mission
    1. Lancez l'audit (**Analyse > Audit de sécurité**, ou `Ctrl+Shift+A`) et lisez les fiches des constats.
    2. Corrigez chaque constat **sans retirer le compte Administrator du groupe Domain Admins**.
    3. Relancez l'audit, puis cliquez sur **Vérifier** dans cette fenêtre.

    ## Où agir
    - Options du compte : panneau de détails, section **Sécurité**.
    - Appartenance aux groupes : **Relations** (menu Affichage ou barre d'outils).

    > Un domaine neuf n'est pas conforme aux recommandations de l'ANSSI : c'est le premier réflexe à acquérir.
    """,
    """
    # First audit

    You have just promoted the first domain controller of **contoso.local**. Nothing has been configured yet: this is the out-of-the-box state.

    ## Your mission
    1. Run the audit (**Analyse > Security audit**, or `Ctrl+Shift+A`) and read the finding sheets.
    2. Fix every finding **without removing the Administrator account from Domain Admins**.
    3. Run the audit again, then click **Check** in this window.

    ## Where to act
    - Account options: details panel, **Security** section.
    - Group membership: **Relations** (View menu or toolbar).

    > A brand-new domain does not meet ANSSI recommendations: checking it is the first habit to build.
    """,
    Fresh(),
    Obj("o1", 3, "Le mot de passe du compte Administrator doit pouvoir expirer", "The Administrator password must be able to expire",
        "Section Sécurité de Administrator : option « Le mot de passe n'expire jamais ».", "Administrator's Security section: \"Password never expires\" option.",
        new LabCheck { Type = LabCheckType.RuleClear, RuleId = "PWD-01" }),
    Obj("o2", 2, "Enterprise Admins et Schema Admins doivent être vides", "Enterprise Admins and Schema Admins must be empty",
        "Relations > utilisateurs dans les groupes : retirez Administrator de ces deux groupes.", "Relations > users in groups: remove Administrator from both groups.",
        new LabCheck { Type = LabCheckType.RuleClear, RuleId = "PRIV-03" }),
    Obj("o3", 2, "Le compte Administrator ne doit pas pouvoir être délégué", "The Administrator account must not be delegable",
        "Cochez « Compte sensible, ne peut pas être délégué », ou ajoutez-le au groupe Protected Users.", "Check \"Account is sensitive and cannot be delegated\", or add it to Protected Users.",
        new LabCheck { Type = LabCheckType.RuleClear, RuleId = "DLG-04" }),
    Obj("o4", 1, "Administrator reste membre de Domain Admins", "Administrator stays a member of Domain Admins",
        "Ne retirez que les appartenances signalées par l'audit.", "Only remove the memberships the audit reports.",
        new LabCheck { Type = LabCheckType.MemberOf, Object = "Administrator", ObjectType = ADObjectType.User, Group = "Domain Admins" }),
    Obj("o5", 2, "L'audit ne remonte plus aucun constat (score 100)", "The audit reports no finding at all (score 100)",
        "Relancez l'audit : chaque règle levée fait remonter le score.", "Run the audit again: each cleared rule raises the score.",
        new LabCheck { Type = LabCheckType.ScoreAtLeast, Score = 100 }));

// ─── 2. Kerberoasting et AS-REP roasting ─────────────────────────────────────
{
    var d = Baseline();
    var services = Add(d, new ADObject("Services", ADObjectType.OrganizationalUnit) { Tier = "Tier 1", Owner = "Domain Admins" });
    var sqlAdmins = Add(services, new ADObject("GG-SQL-Admins", ADObjectType.Group)
    { Tier = "Tier 1", GroupScope = ADGroupScope.Global, GroupCategory = ADGroupCategory.Security, AdminCount = true, Owner = "Domain Admins" });
    sqlAdmins.MemberOf.Add("Domain Admins");
    var svc = User(services, "svc-sql", "Tier 1");
    svc.ServicePrincipalNames.Add("MSSQLSvc/sql01.contoso.local:1433");
    svc.ServicePrincipalNames.Add("MSSQLSvc/sql01.contoso.local");
    svc.MemberOf.Add("GG-SQL-Admins");
    svc.AdminCount = true;
    svc.Description = "# svc-sql\n\nCompte de service de l'instance SQL Server de SQL01. Ajouté à Domain Admins « parce que l'installation échouait sinon ».";
    var users = Add(d, new ADObject("Utilisateurs", ADObjectType.OrganizationalUnit) { Tier = "Tier 2", Owner = "Domain Admins" });
    var scan = User(users, "legacy-scan", "Tier 2");
    scan.AccountFlags = ADAccountFlags.DontRequirePreauth;
    scan.Description = "# legacy-scan\n\nCompte d'un vieux scanner réseau, configuré sans préauthentification Kerberos il y a des années.";

    Save("02-kerberoasting", LabLevel.Intermediate, 30,
        "Kerberoasting et AS-REP roasting",
        "Kerberoasting and AS-REP roasting",
        """
        # Comptes de service exposés

        Lors d'un test d'intrusion, l'auditeur a obtenu en moins d'une heure le mot de passe d'un **administrateur du domaine**, sans jamais attaquer un serveur : il lui a suffi d'un compte utilisateur ordinaire.

        ## Votre mission
        - Identifiez les comptes vulnérables au **Kerberoasting** et à l'**AS-REP roasting** grâce à l'audit.
        - Supprimez le chemin qui fait de `svc-sql` un administrateur du domaine. Attention : il ne figure pas directement dans Domain Admins…
        - Le service SQL doit continuer de fonctionner : **ne supprimez pas** `svc-sql`.
        - À la fin, l'audit ne doit plus remonter aucun constat critique ou élevé.

        > Question pour aller plus loin : quel type de compte aurait évité le problème dès l'installation ?
        """,
        """
        # Exposed service accounts

        During a penetration test, the auditor obtained the password of a **domain administrator** in less than an hour, without ever attacking a server: an ordinary user account was enough.

        ## Your mission
        - Use the audit to identify the accounts exposed to **Kerberoasting** and **AS-REP roasting**.
        - Remove the path that makes `svc-sql` a domain administrator. Careful: it is not directly in Domain Admins…
        - The SQL service must keep running: **do not delete** `svc-sql`.
        - In the end, the audit must report no critical or high finding.

        > Going further: which kind of account would have avoided the problem from the start?
        """,
        d,
        Obj("o1", 3, "Plus aucun compte privilégié vulnérable au Kerberoasting", "No privileged account exposed to Kerberoasting any more",
            "La fiche KRB-01 explique pourquoi un SPN sur un administrateur est critique.", "Rule sheet KRB-01 explains why an SPN on an administrator is critical.",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "KRB-01" }),
        Obj("o2", 2, "svc-sql n'est plus administrateur du domaine, même par imbrication", "svc-sql is no longer a domain administrator, even through nesting",
            "Regardez de quels groupes GG-SQL-Admins est lui-même membre.", "Look at which groups GG-SQL-Admins itself belongs to.",
            new LabCheck { Type = LabCheckType.NotMemberOf, Object = "svc-sql", Group = "Domain Admins", Effective = true }),
        Obj("o3", 2, "Plus aucun compte sans préauthentification Kerberos", "No account without Kerberos preauthentication any more",
            "Section Sécurité de legacy-scan.", "legacy-scan's Security section.",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "KRB-04" }),
        Obj("o4", 1, "Le compte de service svc-sql existe toujours", "Service account svc-sql still exists",
            "Supprimer le compte arrêterait l'application.", "Deleting the account would stop the application.",
            new LabCheck { Type = LabCheckType.ObjectExists, Object = "svc-sql", ObjectType = ADObjectType.User }),
        Obj("o5", 2, "Plus aucun constat critique ou élevé", "No critical or high finding any more",
            "Un groupe de Tier 1 imbriqué dans Domain Admins est aussi une violation du cloisonnement (TIER-01).", "A Tier 1 group nested into Domain Admins is also a tiering violation (TIER-01).",
            new LabCheck { Type = LabCheckType.NoFindingAtOrAbove, Severity = AuditSeverity.High }));
}

// ─── 3. Délégation au support ─────────────────────────────────────────────────
{
    var d = Baseline();
    var users = Add(d, new ADObject("Utilisateurs", ADObjectType.OrganizationalUnit) { Tier = "Tier 2", Owner = "Domain Admins" });
    User(users, "alice", "Tier 2");
    User(users, "bob", "Tier 2");
    var support = Add(d, new ADObject("Support", ADObjectType.OrganizationalUnit) { Tier = "Tier 2", Owner = "Domain Admins" });
    var helpdesk = Add(support, new ADObject("GG-Helpdesk", ADObjectType.Group)
    { Tier = "Tier 2", GroupScope = ADGroupScope.Global, GroupCategory = ADGroupCategory.Security, Owner = "Domain Admins" });
    User(support, "tech1", "Tier 2").MemberOf.Add("GG-Helpdesk");
    users.Delegations.Add(new ADDelegation { TrusteeName = "GG-Helpdesk", TrusteeType = TrusteeType.Group, TargetDN = users.DistinguishedName,
        Right = "GenericAll", RightCategory = RightCategory.FullControl, Tier = "Tier 2" });
    d.Delegations.Add(new ADDelegation { TrusteeName = "GG-Helpdesk", TrusteeType = TrusteeType.Group, TargetDN = d.DistinguishedName,
        Right = "ResetPassword", RightCategory = RightCategory.PasswordReset, Tier = "Tier 0" });

    Save("03-delegation-support", LabLevel.Intermediate, 25,
        "Délégation au support : le moindre privilège",
        "Delegating to the help desk: least privilege",
        """
        # Le support a trop de droits

        Pour « gagner du temps », l'équipe support (groupe `GG-Helpdesk`) a reçu le **contrôle total** sur l'OU `Utilisateurs`, et le droit de réinitialiser les mots de passe… à la racine du domaine, donc aussi ceux des administrateurs.

        ## Votre mission
        Le support doit pouvoir faire son travail, **et rien de plus** :
        - réinitialiser les mots de passe des comptes de l'OU `Utilisateurs` ;
        - déverrouiller ces comptes ;
        - aucun autre droit, nulle part.

        ## Où agir
        **Analyse > Délégations** (`Ctrl+D`) : sélectionnez une OU à gauche, puis ajoutez ou retirez des délégations.

        > Pourquoi la réinitialisation de mot de passe à la racine du domaine est-elle un chemin d'attaque vers le Tier 0 ?
        """,
        """
        # The help desk has too many rights

        To "save time", the support team (group `GG-Helpdesk`) was given **full control** over the `Utilisateurs` OU, and the right to reset passwords… at the domain root, which includes the administrators' passwords.

        ## Your mission
        The help desk must be able to do its job, **and nothing more**:
        - reset the passwords of the accounts in the `Utilisateurs` OU;
        - unlock those accounts;
        - no other right, anywhere.

        ## Where to act
        **Analyse > Delegations** (`Ctrl+D`): select an OU on the left, then add or remove delegations.

        > Why is password reset at the domain root an attack path to Tier 0?
        """,
        d,
        Obj("o1", 2, "GG-Helpdesk peut réinitialiser les mots de passe de l'OU Utilisateurs", "GG-Helpdesk can reset passwords in the Utilisateurs OU",
            "Délégations : sélectionnez l'OU Utilisateurs, mandataire GG-Helpdesk, catégorie PasswordReset.", "Delegations: select the Utilisateurs OU, trustee GG-Helpdesk, category PasswordReset.",
            new LabCheck { Type = LabCheckType.DelegationExists, Trustee = "GG-Helpdesk", Target = "Utilisateurs", RightCategory = RightCategory.PasswordReset }),
        Obj("o2", 1, "GG-Helpdesk peut déverrouiller les comptes de l'OU Utilisateurs", "GG-Helpdesk can unlock accounts in the Utilisateurs OU",
            "Même OU, catégorie AccountUnlock.", "Same OU, category AccountUnlock.",
            new LabCheck { Type = LabCheckType.DelegationExists, Trustee = "GG-Helpdesk", Target = "Utilisateurs", RightCategory = RightCategory.AccountUnlock }),
        Obj("o3", 3, "GG-Helpdesk n'a aucun autre droit", "GG-Helpdesk has no other right",
            "Le contrôle total (FullControl) doit disparaître.", "Full control (FullControl) must go.",
            new LabCheck { Type = LabCheckType.DelegationOnly, Trustee = "GG-Helpdesk", AllowedCategories = new() { RightCategory.PasswordReset, RightCategory.AccountUnlock } }),
        Obj("o4", 2, "Plus aucune délégation d'un tier moins sensible sur le Tier 0", "No delegation from a less sensitive tier on Tier 0 any more",
            "Regardez les délégations posées sur la racine du domaine.", "Look at the delegations set on the domain root.",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "TIER-03" }));
}

// ─── 4. Cloisonnement en tiers ────────────────────────────────────────────────
{
    var d = Baseline();
    var postes = Add(d, new ADObject("Postes", ADObjectType.OrganizationalUnit) { Tier = "Tier 2", Owner = "Domain Admins" });
    var jdupont = User(postes, "jdupont", "Tier 2");
    jdupont.MemberOf.Add("Domain Admins");
    jdupont.MemberOf.Add("Account Operators");
    jdupont.AdminCount = true;
    jdupont.AccountFlags = ADAccountFlags.NotDelegated;
    jdupont.Description = "# jdupont\n\nTechnicien poste de travail. Son compte de tous les jours a été ajouté à Domain Admins « temporairement », il y a deux ans.";
    var policies = Find(d, "Policies", ADObjectType.Container);
    Add(policies, new ADObject("GPO-Postes", ADObjectType.Policy) { Tier = "Tier 2", Owner = "Domain Admins", Description = "# GPO-Postes\n\nParamètres des postes de travail, administrée par l'équipe poste." });
    Find(d, "Domain Controllers", ADObjectType.OrganizationalUnit).LinkedGPOs.Add("GPO-Postes");
    Find(d, "Read-only Domain Controllers", ADObjectType.Group).Tier = "Tier 1";

    Save("04-cloisonnement", LabLevel.Advanced, 40,
        "Cloisonnement en tiers",
        "Tiering",
        """
        # Le Tier 0 fuit

        Le modèle en tiers (ANSSI-PA-099, modèle d'accès Microsoft) repose sur une règle : **un tier ne doit jamais pouvoir contrôler un tier plus sensible**. Dans ce domaine, plusieurs chemins mènent du Tier 2 (postes de travail) au Tier 0 (contrôleurs de domaine).

        ## Votre mission
        - Retrouvez, avec l'audit, chaque violation du cloisonnement et corrigez-la.
        - `jdupont` doit garder son compte : il en a besoin pour son travail sur les postes.
        - Ne modifiez pas la GPO **Default Domain Controllers Policy**.

        > Pour chaque correction, demandez-vous : faut-il retirer le lien, ou reclasser l'objet dans le bon tier ?
        """,
        """
        # Tier 0 is leaking

        The tiering model (ANSSI-PA-099, Microsoft enterprise access model) relies on one rule: **a tier must never be able to control a more sensitive tier**. In this domain, several paths lead from Tier 2 (workstations) to Tier 0 (domain controllers).

        ## Your mission
        - Use the audit to find every tiering violation and fix it.
        - `jdupont` must keep his account: he needs it for his workstation work.
        - Do not modify the **Default Domain Controllers Policy** GPO.

        > For each fix, ask yourself: should the link be removed, or should the object be reclassified into the right tier?
        """,
        d,
        Obj("o1", 3, "Aucun compte de Tier 2 n'appartient à un groupe de Tier 0", "No Tier 2 account belongs to a Tier 0 group",
            "Relations : de quels groupes jdupont est-il membre ?", "Relations: which groups is jdupont a member of?",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "TIER-01" }),
        Obj("o2", 2, "Aucune GPO de Tier 2 n'est liée à une OU de Tier 0", "No Tier 2 GPO is linked to a Tier 0 OU",
            "Relations > GPO : regardez les liens de l'OU Domain Controllers.", "Relations > GPO: look at the links of the Domain Controllers OU.",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "TIER-02" }),
        Obj("o3", 1, "Tous les groupes protégés sont classés en Tier 0", "Every protected group is classified as Tier 0",
            "Un groupe de contrôleurs de domaine, même en lecture seule, relève du Tier 0.", "A domain controller group, even read-only, belongs to Tier 0.",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "PRIV-05" }),
        Obj("o4", 2, "Les groupes Operators sont vides", "The Operators groups are empty",
            "Account Operators donne des chemins connus vers l'administration du domaine.", "Account Operators gives known paths to domain administration.",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "PRIV-03" }),
        Obj("o5", 1, "Le compte jdupont existe toujours", "Account jdupont still exists",
            "Corrigez ses appartenances, ne supprimez pas son compte.", "Fix his memberships, do not delete his account.",
            new LabCheck { Type = LabCheckType.ObjectExists, Object = "jdupont", ObjectType = ADObjectType.User }),
        Obj("o6", 1, "La GPO Default Domain Controllers Policy est inchangée", "The Default Domain Controllers Policy GPO is unchanged",
            "Cette GPO n'est pas en cause : laissez-la telle quelle.", "This GPO is not the problem: leave it as it is.",
            new LabCheck { Type = LabCheckType.NotModified, Object = "Default Domain Controllers Policy", ObjectType = ADObjectType.Policy }));
}

// ─── 5. Hygiène des comptes ───────────────────────────────────────────────────
{
    var d = Baseline();
    Find(d, "Guest", ADObjectType.User).AccountFlags &= ~ADAccountFlags.AccountDisabled;
    Find(d, "Guest", ADObjectType.User).LastLogonTimestamp = reference.AddDays(-2);
    Find(d, "krbtgt", ADObjectType.User).PwdLastSet = reference.AddDays(-800);
    var users = Add(d, new ADObject("Utilisateurs", ADObjectType.OrganizationalUnit) { Tier = "Tier 2", Owner = "Domain Admins" });
    User(users, "stagiaire-2025", "Tier 2").LastLogonTimestamp = reference.AddDays(-300);
    User(users, "jmartin", "Tier 2").AccountFlags = ADAccountFlags.PasswordNotRequired;
    var postes = Add(d, new ADObject("Postes", ADObjectType.OrganizationalUnit) { Tier = "Tier 2", Owner = "Domain Admins" });
    var pc = Add(postes, new ADObject("PC-COMPTA-07", ADObjectType.Computer)
    { Tier = "Tier 2", Owner = "Domain Admins", CreatedDate = reference.AddDays(-900), PwdLastSet = reference.AddDays(-200), LastLogonTimestamp = reference.AddDays(-200) });
    pc.ServicePrincipalNames.Add("HOST/PC-COMPTA-07");

    Save("05-hygiene-comptes", LabLevel.Beginner, 20,
        "Hygiène des comptes",
        "Account hygiene",
        """
        # Ménage de printemps

        Ce domaine fonctionne depuis plus de deux ans sans que personne ne fasse de revue des comptes. L'audit va vous montrer ce que l'on trouve dans presque tous les AD en production.

        ## Votre mission
        Corrigez les constats sur :
        - le compte **Invité** ;
        - le compte **krbtgt** (dans le simulateur, renouveler le mot de passe revient à mettre la date de « mot de passe changé le » à aujourd'hui — en vrai, il faut le faire **deux fois**) ;
        - les **comptes dormants** et les **ordinateurs** qui ne se connectent plus (désactivez-les : on ne supprime jamais sans période d'observation) ;
        - le compte qui accepte un **mot de passe vide**.
        """,
        """
        # Spring cleaning

        This domain has been running for more than two years without anyone reviewing its accounts. The audit will show you what is found in almost every production AD.

        ## Your mission
        Fix the findings on:
        - the **Guest** account;
        - the **krbtgt** account (in the simulator, renewing the password means setting "password last set" to today — in real life it must be done **twice**);
        - **dormant accounts** and **computers** that no longer log on (disable them: never delete without an observation period);
        - the account that accepts an **empty password**.
        """,
        d,
        Obj("o1", 2, "Le compte Invité est désactivé", "The Guest account is disabled",
            "Section Sécurité de Guest.", "Guest's Security section.",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "ACC-01" }),
        Obj("o2", 3, "Le mot de passe de krbtgt a été renouvelé", "The krbtgt password has been renewed",
            "Section Sécurité de krbtgt : date « mot de passe changé le ».", "krbtgt's Security section: \"password last set\" date.",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "KRB-05" }),
        Obj("o3", 1, "Plus aucun compte utilisateur dormant actif", "No enabled dormant user account any more",
            "Un compte désactivé n'est plus considéré comme dormant.", "A disabled account is no longer considered dormant.",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "ACC-02" }),
        Obj("o4", 1, "Plus aucun ordinateur obsolète actif", "No enabled stale computer any more",
            "Un ordinateur qui ne renouvelle plus son mot de passe depuis 90 jours est probablement sorti du parc.", "A computer that has not renewed its password for 90 days has probably left the fleet.",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "PWD-06" }),
        Obj("o5", 2, "Plus aucun compte ne peut avoir un mot de passe vide", "No account can have an empty password any more",
            "Option « Mot de passe non requis » de jmartin.", "jmartin's \"Password not required\" option.",
            new LabCheck { Type = LabCheckType.RuleClear, RuleId = "PWD-03" }));
}

// Contrôle : chaque exercice se relit
foreach (var f in Directory.GetFiles(outDir, "*" + LabService.FileExtension))
    Console.WriteLine($"relu : {Path.GetFileName(f)} — {labs.LoadFile(f).Objectives.Count} objectifs, {new FileInfo(f).Length / 1024} Ko");
