// Génère les scénarios de chemins d'attaque, en français et en anglais, chacun avec son propre domaine :
// - les domaines d'exemple Samples/AttackPaths/{fr,en}/*.smad-x.json ;
// - les exercices correspondants SMAD-X.Core/Labs/{chemin,path}-*.smadx-lab.json (menu Exercices > Chemins d'attaque).
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;
using SMADX.Services.AttackPaths;
using SMADX.Services.Labs;

var outDir = args[0];
var labDir = args[1];
var reference = new DateTime(2026, 10, 22);
var data = new ADDataService();
var fr = true; // langue du scénario en cours de construction

string T(string french, string english) => fr ? french : english;
string Culture() => fr ? "fr-FR" : "en-US";
// Fins de ligne LF partout : sinon les textes prendraient celles du fichier source (CRLF selon la configuration Git)
LocalizedText Text(string text) => new() { [Culture()] = text.ReplaceLineEndings("\n") };

// Domaine sain, peuplé et reproductible : LabDomainBuilder (SMAD-X.Core), partagé avec SMAD-X Studio
LabDomainBuilder builder = new("fr-FR", reference);
ADObject Find(ADObject root, string name, ADObjectType? type = null) => LabDomainBuilder.Find(root, name, type);
IEnumerable<ADObject> Flatten(ADObject o) => LabDomainBuilder.Flatten(o);
ADObject Add(ADObject parent, ADObject child) => LabDomainBuilder.Add(parent, child);
ADObject Ou(ADObject parent, string name, string tier) => builder.Ou(parent, name, tier);
ADObject User(ADObject parent, string name, string tier, string description) => builder.User(parent, name, tier, description);
ADObject Group(ADObject parent, string name, string tier, string description) => builder.Group(parent, name, tier, description);
ADObject Computer(ADObject parent, string name, string tier, string description) => builder.Computer(parent, name, tier, description);
void Delegate(ADObject target, string trustee, TrusteeType type, string right, RightCategory category, string tier) =>
    LabDomainBuilder.Delegate(target, trustee, type, right, category, tier);
ADObject Baseline(string domainName) => builder.Healthy(domainName);

// Partie commune des énoncés : comment observer le chemin dans l'application
string See(string source) => T(
    $"""
    ## Le voir dans SMAD-X
    1. **Analyse > Chemins d'attaque** (`Ctrl+Shift+K`).
    2. Sélectionnez la source **{source}** à gauche : les étapes du chemin s'affichent à droite.
    3. **Afficher dans le graphe** surligne le chemin en rouge dans le graphe des relations.
    4. Après correction, **Actualiser** doit faire disparaître la source, et **Analyse > Avant / après** (`Ctrl+Shift+B`) montre l'effet sur l'audit.
    """,
    $"""
    ## See it in SMAD-X
    1. **Analyse > Attack paths** (`Ctrl+Shift+K`).
    2. Select the source **{source}** on the left: the path steps appear on the right.
    3. **Show in graph** highlights the path in red in the relations graph.
    4. Once fixed, **Refresh** must remove the source, and **Analyse > Before / after** (`Ctrl+Shift+B`) shows the effect on the audit.
    """);

void AssignStableIds(ADRootDocument document) => LabDomainBuilder.AssignStableIds(document, reference);

var index = new List<(string Id, string Lang, string Domain, string Title)>();
LabSpec? currentLab = null;

// Appelé par chaque scénario avec les noms de ses objets : contexte réaliste, mission et objectifs corrigés
void Lab(string title, LabLevel level, int minutes, string context, string mission, params LabObjective[] objectives) =>
    currentLab = new LabSpec(title, level, minutes, context, mission, objectives);

// Titres et indices s'affichent en texte brut : pas de `code` Markdown
LabObjective Obj(string id, int points, string title, string hint, LabCheck check) =>
    new() { Id = id, Points = points, Title = Text(title.Replace("`", "")), Hint = Text(hint.Replace("`", "")), Check = check };

var anyUser = AttackPathService.AnyUserId;
string AnyUserLabel() => T("N'importe quel utilisateur du domaine", "Any domain user");

LabObjective CutPath(string source, string label, string hint) => Obj("o1", 4,
    T($"Le chemin depuis « {label} » est coupé", $"The path from \"{label}\" is cut"), hint,
    new LabCheck { Type = LabCheckType.NoAttackPath, Object = source });

LabObjective NoPathLeft(string hint) => Obj("o2", 2,
    T("Plus aucun chemin vers le contrôle du domaine hors Tier 0", "No path to domain control left outside Tier 0"), hint,
    new LabCheck { Type = LabCheckType.NoAttackPath });

LabObjective Exists(string id, string name, ADObjectType type, string title, string hint) =>
    Obj(id, 1, title, hint, new LabCheck { Type = LabCheckType.ObjectExists, Object = name, ObjectType = type });

LabObjective RuleClear(string id, int points, string ruleId, string title, string hint, string? obj = null) =>
    Obj(id, points, title, hint, new LabCheck { Type = LabCheckType.RuleClear, RuleId = ruleId, Object = obj });

string Statement(string title, string context, string mission) => T(
    $"""
    # {title}

    ## Contexte
    {context}

    ## Votre mission
    {mission}

    ## Vos outils
    - **Analyse > Chemins d'attaque** (`Ctrl+Shift+K`) : qui peut prendre le contrôle du domaine, et par quelles étapes. **Afficher dans le graphe** surligne le chemin.
    - **Analyse > Audit de sécurité** (`Ctrl+Shift+A`) : les constats et leurs fiches, avec les références ANSSI, NIST et MITRE.
    - **Analyse > Avant / après** (`Ctrl+Shift+B`) : ce que vos modifications ont changé.
    - **Vérifier**, dans cette fenêtre, corrige les objectifs ; **Indice** donne une piste par objectif.
    """,
    $"""
    # {title}

    ## Context
    {context}

    ## Your mission
    {mission}

    ## Your tools
    - **Analyse > Attack paths** (`Ctrl+Shift+K`): who can take control of the domain, and through which steps. **Show in graph** highlights the path.
    - **Analyse > Security audit** (`Ctrl+Shift+A`): findings and their sheets, with ANSSI, NIST and MITRE references.
    - **Analyse > Before / after** (`Ctrl+Shift+B`): what your changes did.
    - **Check**, in this window, grades the objectives; **Hint** gives a lead for each objective.
    """);

void Scenario(string id, string frDomain, string enDomain, Func<ADObject, (string Title, string Body)> build)
{
    foreach (var (french, domainName) in new[] { (true, frDomain), (false, enDomain) })
    {
        fr = french;
        builder = new LabDomainBuilder(fr ? "fr-FR" : "en-US", reference);
        // Descriptions des objets intégrés (Builtin, sites…) dans la langue du fichier, quelle que soit celle de la machine
        LocalizationService.Instance.SetLanguage(fr ? "fr-FR" : "en-US");
        var d = Baseline(domainName);
        currentLab = null;
        var (title, body) = build(d);
        var lab = currentLab ?? throw new Exception($"{id} : le scénario ne décrit pas son exercice.");
        d.Description = $"# {title}\n\n## {T("Contexte", "Context")}\n{lab.Context.Trim()}\n\n{body.Trim()}\n".ReplaceLineEndings("\n");
        var lang = fr ? "fr" : "en";
        var dir = Path.Combine(outDir, lang);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{id}.smad-x.json");
        var document = new ADRootDocument { Version = ADRootDocument.CurrentVersion, Domain = d, SitesTopology = data.CreateFreshTopology(d.Name) };
        AssignStableIds(document);
        if (!data.SaveDocumentAsync(document, path).GetAwaiter().GetResult())
            throw new Exception("Échec de l'écriture de " + path);
        index.Add((id, lang, domainName, title));
        Console.WriteLine($"{lang}/{id} : {domainName}");

        // Le domaine de départ de l'exercice ne porte que le contexte : la description de l'exemple contient
        // le chemin et la correction, que l'apprenant verrait en sélectionnant la racine du domaine
        var labStart = System.Text.Json.JsonSerializer.Deserialize<ADRootDocument>(
            System.Text.Json.JsonSerializer.Serialize(document, ADDataService.SerializerOptions), ADDataService.SerializerOptions)!;
        labStart.Domain.Description = $"# {lab.Title}\n\n{lab.Context.Trim()}\n".ReplaceLineEndings("\n");

        var labId = $"{T("chemin", "path")}-{id}";
        new LabService().SaveFile(new LabDefinition
        {
            Id = labId, Language = Culture(), Category = "attack-paths",
            Level = lab.Level, DurationMinutes = lab.Minutes, ReferenceDate = reference,
            Title = Text(lab.Title), Statement = Text(Statement(lab.Title, lab.Context.Trim(), lab.Mission.Trim())),
            Start = labStart, Objectives = lab.Objectives.ToList()
        }, Path.Combine(labDir, labId + LabService.FileExtension));
    }
}

// ─── 01. Kerberoasting d'un compte de service imbriqué dans Domain Admins ───────
Scenario("01-kerberoasting", "atelier-boreal.local", "northwind.local", d =>
{
    var app = T("ERP", "SharePoint");
    var ou = Ou(d, T("Comptes de service", "Service Accounts"), "Tier 1");
    var group = Group(ou, $"GG-{app}-Admins", "Tier 1", T(
        $"Administrateurs de l'application {app}. Imbriqué dans Domain Admins lors de l'installation.",
        $"{app} application administrators. Nested into Domain Admins during the installation."));
    group.MemberOf.Add("Domain Admins");
    group.AdminCount = true;
    var svc = User(ou, $"svc-{app.ToLowerInvariant()}", "Tier 1", T(
        $"Compte de service de {app}, mot de passe choisi par l'éditeur en 2019.",
        $"{app} service account, password chosen by the vendor in 2019."));
    svc.ServicePrincipalNames.Add($"HTTP/{app.ToLowerInvariant()}.{d.Name}");
    svc.ServicePrincipalNames.Add($"HTTP/{app.ToLowerInvariant()}");
    svc.MemberOf.Add(group.Name);
    svc.AdminCount = true;

    Lab(T("Atelier Boréal : le compte de service de l'ERP", "Northwind Traders: the SharePoint service account"), LabLevel.Beginner, 20,
        T($"""
        L'**Atelier Boréal** est une menuiserie industrielle de 120 salariés. Son ERP a été installé en 2019 par un intégrateur qui a depuis fermé ; personne ne sait vraiment quels droits il utilise.

        Vous venez d'arriver comme administrateur systèmes. L'assureur cyber de l'entreprise a fait réaliser un test d'intrusion : en moins d'une heure, l'auditeur est devenu administrateur du domaine **depuis un simple poste de l'atelier**, avec le compte d'un opérateur. Son rapport tient en une ligne : « compte de service exposé ».
        """, $"""
        **Northwind Traders** is a 300-employee food distributor. Its SharePoint intranet was set up in 2019 by a contractor who has since left; nobody really knows which rights it uses.

        You have just joined as systems administrator. The company's cyber insurer commissioned a penetration test: in less than an hour, the tester became domain administrator **from a plain warehouse workstation**, with an operator's account. The report fits in one line: "exposed service account".
        """),
        T($"""
        - Retrouvez le chemin utilisé par l'auditeur et coupez-le.
        - L'{app} doit continuer de fonctionner : le compte `{svc.Name}` reste en place.
        - À la fin, plus aucun compte hors Tier 0 ne doit pouvoir atteindre le domaine.
        """, $"""
        - Find the path the tester used and cut it.
        - {app} must keep running: the `{svc.Name}` account stays in place.
        - In the end, no account outside Tier 0 may reach the domain.
        """),
        CutPath(anyUser, AnyUserLabel(), T(
            $"Un compte avec un SPN peut être kerberoasté par n'importe qui. Regardez de quels groupes `{group.Name}` est lui-même membre.",
            $"An account with an SPN can be kerberoasted by anyone. Look at which groups `{group.Name}` itself belongs to.")),
        NoPathLeft(T("Actualisez **Chemins d'attaque** : la liste doit être vide.", "Refresh **Attack paths**: the list must be empty.")),
        RuleClear("o3", 2, "KRB-01", T("Plus aucun compte privilégié kerberoastable", "No privileged kerberoastable account left"),
            T("La fiche KRB-01 explique pourquoi un SPN sur un administrateur est critique.", "Rule sheet KRB-01 explains why an SPN on an administrator is critical.")),
        Exists("o4", svc.Name, ADObjectType.User, T($"Le compte de service `{svc.Name}` existe toujours", $"Service account `{svc.Name}` still exists"),
            T("Supprimer le compte arrêterait l'application : retirez-lui plutôt les droits qu'il n'a pas à avoir.", "Deleting the account would stop the application: remove the rights it should not have instead.")));

    return (T("Chemin d'attaque : Kerberoasting", "Attack path: Kerberoasting"), T(
        $"""
        Le compte de service `{svc.Name}` porte un **SPN**. N'importe quel utilisateur du domaine peut donc demander un ticket de service chiffré avec son mot de passe, puis le casser hors ligne, sans bruit sur le réseau.

        Ce compte n'est pas directement dans Domain Admins : il y arrive par le groupe imbriqué `{group.Name}`.

        ## Le chemin
        1. N'importe quel utilisateur → `{svc.Name}` : Kerberoasting (MITRE T1558.003).
        2. `{svc.Name}` → `{group.Name}` → `Domain Admins` : appartenance imbriquée.
        3. `Domain Admins` → contrôle du domaine.

        {See("N'importe quel utilisateur du domaine")}

        ## Le corriger
        - Sortez `{group.Name}` de Domain Admins et donnez au service uniquement les droits dont il a besoin.
        - Remplacez le compte par un **gMSA** (mot de passe de 120 caractères renouvelé automatiquement).
        """,
        $"""
        The `{svc.Name}` service account has an **SPN**. Any domain user can therefore request a service ticket encrypted with its password and crack it offline, without any network noise.

        The account is not directly in Domain Admins: it gets there through the nested group `{group.Name}`.

        ## The path
        1. Any domain user → `{svc.Name}`: Kerberoasting (MITRE T1558.003).
        2. `{svc.Name}` → `{group.Name}` → `Domain Admins`: nested membership.
        3. `Domain Admins` → domain control.

        {See("Any domain user")}

        ## Fix it
        - Remove `{group.Name}` from Domain Admins and give the service only the rights it needs.
        - Replace the account with a **gMSA** (120-character password rotated automatically).
        """));
});

// ─── 02. AS-REP roasting d'un compte membre de Backup Operators ─────────────────
Scenario("02-asrep-roasting", "cabinet-lefevre.lan", "fabrikam.lan", d =>
{
    var ou = Ou(d, T("Utilisateurs", "Staff"), "Tier 2");
    var scan = User(ou, T("scan-copieur", "copier-scan"), "Tier 2", T(
        "Compte du copieur multifonction, utilisé pour déposer les numérisations sur le serveur de fichiers.",
        "Multifunction copier account, used to drop scans on the file server."));
    scan.AccountFlags = ADAccountFlags.DontRequirePreauth | ADAccountFlags.PasswordNeverExpires;
    scan.MemberOf.Add("Backup Operators");
    scan.AdminCount = true;

    Lab(T("Cabinet Lefèvre : le copieur qui en savait trop", "Fabrikam: the copier that knew too much"), LabLevel.Beginner, 15,
        T($"""
        Le **Cabinet Lefèvre** est un cabinet d'expertise comptable de 25 personnes. Son informatique est confiée à un prestataire qui passe une demi-journée par mois.

        Ce matin, l'EDR du prestataire a levé une alerte : des demandes Kerberos AS-REP en rafale, depuis le Wi-Fi invités de la salle d'attente, visant un seul compte. Le prestataire vous appelle : « Ce compte, c'est celui du copieur. Il est où exactement dans l'AD ? »
        """, $"""
        **Fabrikam** is a 40-person law firm. Its IT is outsourced to a provider who comes in half a day a month.

        This morning, the provider's EDR raised an alert: a burst of Kerberos AS-REP requests from the waiting-room guest Wi-Fi, all targeting a single account. The provider calls you: "That account is the copier's. Where exactly is it in AD?"
        """),
        T($"""
        - Comprenez pourquoi ce compte intéresse tant l'attaquant, et coupez le chemin.
        - Le copieur doit continuer de déposer les numérisations : ne supprimez pas `{scan.Name}`.
        """, $"""
        - Understand why the attacker is so interested in this account, and cut the path.
        - The copier must keep dropping scans: do not delete `{scan.Name}`.
        """),
        CutPath(anyUser, AnyUserLabel(), T(
            "Section **Sécurité** du compte : une option permet d'obtenir une réponse chiffrée avec son mot de passe sans le connaître.",
            "The account's **Security** section: one option lets anyone get a response encrypted with its password without knowing it.")),
        NoPathLeft(T($"Même avec la préauthentification réactivée, un compte de Tier 2 dans Backup Operators reste un chemin vers le domaine.",
            "Even with preauthentication back on, a Tier 2 account in Backup Operators is still a path to the domain.")),
        RuleClear("o3", 2, "KRB-03", T("Plus aucun compte privilégié sans préauthentification Kerberos", "No privileged account without Kerberos preauthentication"),
            T("Fiche KRB-03.", "Rule sheet KRB-03.")),
        Exists("o4", scan.Name, ADObjectType.User, T($"Le compte `{scan.Name}` existe toujours", $"Account `{scan.Name}` still exists"),
            T("Il suffit d'un droit d'écriture sur le partage des numérisations, pas d'un groupe d'opérateurs.", "A write permission on the scan share is enough, not an operators group.")));

    return (T("Chemin d'attaque : AS-REP roasting", "Attack path: AS-REP roasting"), T(
        $"""
        Le compte `{scan.Name}` est configuré **sans préauthentification Kerberos** (le vieux copieur ne la gérait pas). N'importe qui peut obtenir une réponse AS-REP chiffrée avec son mot de passe et la casser hors ligne.

        Pour pouvoir écrire partout, il a été ajouté à **Backup Operators** : ce groupe peut sauvegarder la base `NTDS.dit` d'un contrôleur de domaine, donc tous les hachages du domaine.

        ## Le chemin
        1. N'importe quel utilisateur → `{scan.Name}` : AS-REP roasting (MITRE T1558.004).
        2. `{scan.Name}` → `Backup Operators` : appartenance.
        3. `Backup Operators` → contrôle du domaine.

        {See("N'importe quel utilisateur du domaine")}

        ## Le corriger
        - Réactivez la préauthentification Kerberos (section **Sécurité** du compte).
        - Retirez le compte de Backup Operators : un droit d'écriture sur un partage suffit.
        """,
        $"""
        The `{scan.Name}` account is configured **without Kerberos preauthentication** (the old copier did not support it). Anyone can get an AS-REP response encrypted with its password and crack it offline.

        To be able to write anywhere, it was added to **Backup Operators**: this group can back up a domain controller's `NTDS.dit`, hence every hash of the domain.

        ## The path
        1. Any domain user → `{scan.Name}`: AS-REP roasting (MITRE T1558.004).
        2. `{scan.Name}` → `Backup Operators`: membership.
        3. `Backup Operators` → domain control.

        {See("Any domain user")}

        ## Fix it
        - Turn Kerberos preauthentication back on (the account's **Security** section).
        - Remove the account from Backup Operators: a write permission on one share is enough.
        """));
});

// ─── 03. Réinitialisation de mot de passe déléguée au support ───────────────────
Scenario("03-reset-password", "mediatheque-azur.local", "tailspintoys.local", d =>
{
    var support = Ou(d, "Support", "Tier 2");
    var supportGroup = Group(support, T("GG-Support", "GG-ServiceDesk"), "Tier 2", T("Techniciens du support de proximité.", "Service desk technicians."));
    User(support, T("tech-mdupont", "tech-jsmith"), "Tier 2", T("Technicien du support.", "Service desk technician.")).MemberOf.Add(supportGroup.Name);
    var groups = Ou(d, T("Groupes", "Groups"), "Tier 1");
    var ops = Group(groups, T("GG-Exploitation", "GG-Operations"), "Tier 1", T(
        "Équipe d'exploitation. Contrôle total sur le domaine « en attendant de déléguer finement ».",
        "Operations team. Full control over the domain \"until fine-grained delegation is done\"."));
    Delegate(d, ops.Name, TrusteeType.Group, "GenericAll", RightCategory.FullControl, "Tier 0");
    var users = Ou(d, T("Utilisateurs", "Staff"), "Tier 2");
    var target = User(users, T("a.roux", "m.brown"), "Tier 2", T(
        "Technicienne d'exploitation : son compte de travail quotidien est aussi son compte d'administration.",
        "Operations engineer: the daily account is also the admin account."));
    target.MemberOf.Add(ops.Name);
    Delegate(users, supportGroup.Name, TrusteeType.Group, "Reset Password", RightCategory.PasswordReset, "Tier 2");

    Lab(T("Médiathèque Azur : le support peut-il tout réinitialiser ?", "Tailspin Toys: can the service desk reset everything?"), LabLevel.Intermediate, 25,
        T($"""
        La **Médiathèque Azur** regroupe six médiathèques intercommunales et 90 agents. Le support de proximité, deux techniciens, réinitialise les mots de passe des agents qui ont oublié le leur.

        Lors d'un audit interne, un technicien du support a montré qu'il pouvait réinitialiser le mot de passe de `{target.Name}`, la technicienne d'exploitation. Le directeur des systèmes d'information vous demande si c'est grave, et de corriger sans priver le support de son outil de travail.
        """, $"""
        **Tailspin Toys** is a toy retailer with 15 stores and 400 staff. The two-person service desk resets the passwords of staff who forgot theirs.

        During an internal audit, a service desk technician showed they could reset the password of `{target.Name}`, the operations engineer. The IT director asks you whether this is serious, and to fix it without taking the service desk's daily tool away.
        """),
        T($"""
        - Évaluez jusqu'où mène cette réinitialisation, et coupez le chemin.
        - Le support doit garder la réinitialisation des mots de passe sur l'OU `{users.Name}`, et `{supportGroup.Name}` garde ses membres.
        """, $"""
        - Assess how far this password reset leads, and cut the path.
        - The service desk keeps password reset on the `{users.Name}` OU, and `{supportGroup.Name}` keeps its members.
        """),
        CutPath(supportGroup.Name, supportGroup.Name, T(
            $"Regardez les droits de `{ops.Name}` sur la racine du domaine (fenêtre **Délégations**).",
            $"Look at the rights of `{ops.Name}` on the domain root (**Delegations** window).")),
        NoPathLeft(T($"Déplacer `{target.Name}` ne suffit pas : `{ops.Name}` reste un chemin pour chacun de ses membres.",
            $"Moving `{target.Name}` is not enough: `{ops.Name}` is still a path for each of its members.")),
        Obj("o3", 1, T("Le support réinitialise toujours les mots de passe des utilisateurs", "The service desk can still reset user passwords"),
            T("Ne retirez pas la délégation du support : c'est son métier.", "Do not remove the service desk delegation: it is their job."),
            new LabCheck { Type = LabCheckType.DelegationExists, Trustee = supportGroup.Name, Target = users.Name, RightCategory = RightCategory.PasswordReset }),
        Obj("o4", 1, T($"`{T("tech-mdupont", "tech-jsmith")}` reste membre du support", $"`{T("tech-mdupont", "tech-jsmith")}` stays on the service desk"),
            T("Vider le groupe du support n'est pas une correction.", "Emptying the service desk group is not a fix."),
            new LabCheck { Type = LabCheckType.MemberOf, Object = T("tech-mdupont", "tech-jsmith"), Group = supportGroup.Name, Effective = false }));

    return (T("Chemin d'attaque : réinitialisation de mot de passe", "Attack path: password reset"), T(
        $"""
        Le support peut réinitialiser le mot de passe de tous les comptes de l'OU `{users.Name}`. C'est normal… sauf que cette OU contient aussi `{target.Name}`, membre de `{ops.Name}`, qui a le **contrôle total sur le domaine**.

        Comme ce compte n'est pas dans un groupe protégé, AdminSDHolder ne bloque pas l'héritage de la délégation.

        ## Le chemin
        1. `{supportGroup.Name}` → `{target.Name}` : réinitialisation du mot de passe déléguée sur l'OU.
        2. `{target.Name}` → `{ops.Name}` : appartenance.
        3. `{ops.Name}` → contrôle total sur le domaine.

        {See(supportGroup.Name)}

        ## Le corriger
        - Séparez le compte d'administration (`adm-…`, Tier 1) du compte bureautique, dans une OU où le support n'a aucun droit.
        - Remplacez le contrôle total de `{ops.Name}` par des délégations précises.
        """,
        $"""
        The service desk can reset the password of every account in the `{users.Name}` OU. That is expected… except this OU also contains `{target.Name}`, a member of `{ops.Name}`, which has **full control over the domain**.

        As this account is not in a protected group, AdminSDHolder does not block the inherited delegation.

        ## The path
        1. `{supportGroup.Name}` → `{target.Name}`: password reset delegated on the OU.
        2. `{target.Name}` → `{ops.Name}`: membership.
        3. `{ops.Name}` → full control over the domain.

        {See(supportGroup.Name)}

        ## Fix it
        - Split the admin account (`adm-…`, Tier 1) from the office account, in an OU where the service desk has no right.
        - Replace the full control of `{ops.Name}` with precise delegations.
        """));
});

// ─── 04. Modification des membres d'un groupe qui administre les DC ─────────────
Scenario("04-write-members", "transports-iris.local", "litware.local", d =>
{
    var groups = Ou(d, T("Groupes", "Groups"), "Tier 1");
    var dcOps = Group(groups, T("GG-Exploitation-DC", "GG-DC-Operations"), "Tier 1", T(
        "Prestataire chargé des contrôleurs de domaine : contrôle total sur la racine du domaine.",
        "Contractor in charge of the domain controllers: full control over the domain root."));
    Delegate(d, dcOps.Name, TrusteeType.Group, "GenericAll", RightCategory.FullControl, "Tier 0");
    var support = Ou(d, "Support", "Tier 2");
    var managers = Group(support, T("GG-Gestion-Groupes", "GG-Group-Managers"), "Tier 2", T(
        "Assistants qui gèrent les listes de diffusion et les groupes de partage.",
        "Assistants who manage distribution lists and share groups."));
    User(support, T("c.moreau", "e.johnson"), "Tier 2", T("Assistante de direction.", "Executive assistant.")).MemberOf.Add(managers.Name);
    Delegate(groups, managers.Name, TrusteeType.Group, "WriteProperty:member", RightCategory.AttributeWrite, "Tier 1");

    Lab(T("Transports Iris : les listes de diffusion", "Litware: the distribution lists"), LabLevel.Intermediate, 25,
        T($"""
        **Transports Iris** est un transporteur routier de 400 salariés répartis sur huit agences. Pour décharger l'informatique, les assistantes de direction gèrent elles-mêmes les listes de diffusion et les groupes de partage.

        Un prestataire administre les contrôleurs de domaine à distance. En préparant la fin de son contrat, vous découvrez que les droits des assistantes et ceux du prestataire se croisent dans la même OU.
        """, $"""
        **Litware** is a 250-employee software publisher. To relieve IT, executive assistants manage distribution lists and share groups themselves.

        A contractor administers the domain controllers remotely. While preparing the end of their contract, you find out that the assistants' rights and the contractor's rights meet in the same OU.
        """),
        T($"""
        - Montrez comment un compte d'assistante peut prendre le contrôle du domaine, puis coupez ce chemin.
        - Les assistantes gardent la gestion des groupes de l'OU `{groups.Name}`.
        """, $"""
        - Show how an assistant account can take control of the domain, then cut that path.
        - Assistants keep managing the groups of the `{groups.Name}` OU.
        """),
        CutPath(managers.Name, managers.Name, T(
            $"Quel groupe de l'OU `{groups.Name}` a des droits sur la racine du domaine ? Rangez-le ailleurs, ou retirez-lui ces droits.",
            $"Which group of the `{groups.Name}` OU has rights on the domain root? Move it elsewhere, or remove those rights.")),
        NoPathLeft(T($"Un contrôle total sur la racine du domaine reste un chemin pour les membres de `{dcOps.Name}`.",
            $"Full control on the domain root is still a path for the members of `{dcOps.Name}`.")),
        Obj("o3", 1, T("Les assistantes gèrent toujours les groupes", "Assistants still manage groups"),
            T("La délégation sur l'attribut `member` reste en place.", "The delegation on the `member` attribute stays."),
            new LabCheck { Type = LabCheckType.DelegationExists, Trustee = managers.Name, Target = groups.Name, RightCategory = RightCategory.AttributeWrite }),
        Obj("o4", 1, T($"`{T("c.moreau", "e.johnson")}` reste dans `{managers.Name}`", $"`{T("c.moreau", "e.johnson")}` stays in `{managers.Name}`"),
            T("Retirer l'assistante du groupe ne corrige pas la cause.", "Removing the assistant from the group does not fix the cause."),
            new LabCheck { Type = LabCheckType.MemberOf, Object = T("c.moreau", "e.johnson"), Group = managers.Name, Effective = false }));

    return (T("Chemin d'attaque : modification des membres d'un groupe", "Attack path: group membership change"), T(
        $"""
        `{managers.Name}` peut modifier les membres de **tous** les groupes de l'OU `{groups.Name}`, pour gérer les listes de diffusion. Or cette OU contient aussi `{dcOps.Name}`, qui a le contrôle total sur la racine du domaine, réplication comprise (DCSync).

        ## Le chemin
        1. `{managers.Name}` → `{dcOps.Name}` : droit d'écrire l'attribut `member`.
        2. `{dcOps.Name}` → contrôle total sur le domaine.

        {See(managers.Name)}

        > Pourquoi le prestataire n'a-t-il pas simplement le contrôle total sur l'OU Domain Controllers ? Parce qu'il ne servirait à rien : les comptes des DC sont protégés par **AdminSDHolder**, les délégations de l'OU ne s'y appliquent pas. D'où la délégation sur la racine, bien plus dangereuse.

        ## Le corriger
        - Rangez les groupes d'administration dans une OU distincte, sans délégation pour les assistants.
        - Supprimez le contrôle total de `{dcOps.Name}` : l'administration des DC relève du Tier 0.
        """,
        $"""
        `{managers.Name}` can change the members of **every** group in the `{groups.Name}` OU, to manage distribution lists. But this OU also contains `{dcOps.Name}`, which has full control over the domain root, replication included (DCSync).

        ## The path
        1. `{managers.Name}` → `{dcOps.Name}`: right to write the `member` attribute.
        2. `{dcOps.Name}` → full control over the domain.

        {See(managers.Name)}

        > Why doesn't the contractor simply have full control over the Domain Controllers OU? Because it would be useless: DC accounts are protected by **AdminSDHolder**, so OU delegations do not apply to them. Hence the delegation on the root, which is far more dangerous.

        ## Fix it
        - Keep admin groups in a separate OU, with no delegation for assistants.
        - Remove the full control of `{dcOps.Name}`: DC administration belongs to Tier 0.
        """));
});

// ─── 05. GPO liée à l'OU des administrateurs, possédée par un technicien ────────
Scenario("05-gpo", "clinique-des-pins.local", "woodgrovebank.local", d =>
{
    var users = Ou(d, T("Utilisateurs", "Staff"), "Tier 2");
    var tech = User(users, T("t.girard", "l.wilson"), "Tier 2", T(
        "Technicien poste de travail. A créé la GPO des fonds d'écran il y a deux ans.",
        "Desktop technician. Created the wallpaper GPO two years ago."));
    var admins = Ou(d, T("Administration", "Admin Accounts"), "Tier 0");
    var admin = User(admins, T("adm-lgarnier", "adm-owilson"), "Tier 0", T("Compte d'administration du domaine.", "Domain admin account."));
    admin.MemberOf.Add("Domain Admins");
    admin.AdminCount = true;
    admin.AccountFlags = ADAccountFlags.NotDelegated;
    var gpoName = T("GPO-Fond-Ecran", "GPO-Wallpaper");
    Add(Find(d, "Policies", ADObjectType.Container), new ADObject(gpoName, ADObjectType.Policy)
    {
        Tier = "Tier 2", Owner = tech.Name, CreatedDate = reference.AddDays(-700),
        Description = T("Fond d'écran de la clinique, liée « partout » pour aller plus vite.", "Company wallpaper, linked \"everywhere\" to save time.")
    });
    users.LinkedGPOs.Add(gpoName);
    admins.LinkedGPOs.Add(gpoName);

    Lab(T("Clinique des Pins : le fond d'écran", "Woodgrove Bank: the wallpaper"), LabLevel.Intermediate, 20,
        T($"""
        La **Clinique des Pins** est une clinique privée de 250 lits. Il y a deux ans, pour la nouvelle charte graphique, `{tech.Name}`, technicien poste de travail, a créé une GPO de fond d'écran et l'a liée « partout » pour aller plus vite.

        La clinique prépare un audit de sécurité avant sa certification. L'auditeur a prévenu : il regardera en priorité qui peut agir sur les sessions des administrateurs.
        """, $"""
        **Woodgrove Bank** is a regional bank with 30 branches. Two years ago, for the new brand identity, `{tech.Name}`, a desktop technician, created a wallpaper GPO and linked it "everywhere" to save time.

        The regulator's security audit is next month. The auditor warned: the first thing checked will be who can act on administrators' sessions.
        """),
        T($"""
        - Trouvez comment `{tech.Name}` peut exécuter du code dans la session de `{admin.Name}`, et coupez ce chemin.
        - Le fond d'écran reste déployé sur les postes des utilisateurs : ne supprimez pas `{gpoName}`.
        """, $"""
        - Find how `{tech.Name}` can run code in `{admin.Name}`'s session, and cut that path.
        - The wallpaper stays deployed on user workstations: do not delete `{gpoName}`.
        """),
        CutPath(tech.Name, tech.Name, T(
            $"Regardez où `{gpoName}` est liée, et à qui elle appartient (section **Sécurité**, propriétaire).",
            $"Look at where `{gpoName}` is linked, and who owns it (**Security** section, owner).")),
        NoPathLeft(T("Actualisez **Chemins d'attaque** : la liste doit être vide.", "Refresh **Attack paths**: the list must be empty.")),
        Exists("o3", gpoName, ADObjectType.Policy, T($"La GPO `{gpoName}` existe toujours", $"GPO `{gpoName}` still exists"),
            T("Supprimer la GPO priverait les utilisateurs du fond d'écran : retirez plutôt le lien en trop.", "Deleting the GPO would remove the users' wallpaper: remove the extra link instead.")),
        Obj("o4", 1, T($"`{admin.Name}` reste administrateur du domaine", $"`{admin.Name}` stays a domain administrator"),
            T("Le compte d'administration n'est pas le problème.", "The admin account is not the problem."),
            new LabCheck { Type = LabCheckType.MemberOf, Object = admin.Name, Group = "Domain Admins" }));

    return (T("Chemin d'attaque : GPO", "Attack path: GPO"), T(
        $"""
        `{tech.Name}` est propriétaire de la GPO `{gpoName}`, donc peut la modifier. Elle est liée à l'OU des utilisateurs… mais aussi à l'OU `{admins.Name}`. Une tâche planifiée ou un script de connexion ajouté à cette GPO s'exécutera dans la session de `{admin.Name}`.

        ## Le chemin
        1. `{tech.Name}` → `{gpoName}` : propriétaire.
        2. `{gpoName}` → `{admin.Name}` : GPO liée à son OU (exécution de code).
        3. `{admin.Name}` → `Domain Admins` → contrôle du domaine.

        {See(tech.Name)}

        ## Le corriger
        - Retirez le lien de `{gpoName}` sur l'OU `{admins.Name}`.
        - Donnez la propriété des GPO à Domain Admins, et déléguez leur modification au seul Tier correspondant.
        """,
        $"""
        `{tech.Name}` owns the `{gpoName}` GPO, so can edit it. It is linked to the staff OU… but also to the `{admins.Name}` OU. A scheduled task or logon script added to this GPO will run in `{admin.Name}`'s session.

        ## The path
        1. `{tech.Name}` → `{gpoName}`: owner.
        2. `{gpoName}` → `{admin.Name}`: GPO linked to its OU (code execution).
        3. `{admin.Name}` → `Domain Admins` → domain control.

        {See(tech.Name)}

        ## Fix it
        - Remove the `{gpoName}` link from the `{admins.Name}` OU.
        - Make Domain Admins the owner of GPOs, and delegate their editing to the matching tier only.
        """));
});

// ─── 06. Délégation Kerberos non contrainte sur un serveur d'impression ─────────
Scenario("06-unconstrained-delegation", "imprimerie-orion.lan", "wingtiptoys.lan", d =>
{
    var servers = Ou(d, T("Serveurs", "Servers"), "Tier 1");
    var print = Computer(servers, T("SRV-IMPRESSION", "PRINT01"), "Tier 1", T("Serveur d'impression.", "Print server."));
    print.AccountFlags = ADAccountFlags.TrustedForDelegation;
    var groups = Ou(d, T("Groupes", "Groups"), "Tier 1");
    var serverAdmins = Group(groups, T("GG-Admins-Serveurs", "GG-Server-Admins"), "Tier 1", T("Administrateurs des serveurs membres.", "Member server administrators."));
    Delegate(servers, serverAdmins.Name, TrusteeType.Group, "GenericAll", RightCategory.FullControl, "Tier 1");
    User(groups, T("adm-slambert", "adm-rdavies"), "Tier 1", T("Administrateur des serveurs.", "Server administrator.")).MemberOf.Add(serverAdmins.Name);

    Lab(T("Imprimerie Orion : le serveur d'impression", "Wingtip Toys: the print server"), LabLevel.Intermediate, 20,
        T($"""
        L'**Imprimerie Orion** emploie 60 personnes et tourne en trois-huit. `{print.Name}` pilote les traceurs grand format ; il a été installé il y a dix ans avec une option « pour que l'impression fonctionne depuis les applications web ».

        Le CERT-FR vient de publier un bulletin sur les attaques par coercition d'authentification (« PrinterBug »). Votre responsable vous demande si l'imprimerie est concernée.
        """, $"""
        **Wingtip Toys** employs 200 people across two factories. `{print.Name}` drives the label printers; it was installed ten years ago with an option "so that printing works from web applications".

        A national CERT has just published a bulletin about authentication coercion attacks ("PrinterBug"). Your manager asks whether the company is affected.
        """),
        T($"""
        - Montrez qu'un administrateur des serveurs (Tier 1) peut atteindre le Tier 0 par `{print.Name}`, et coupez ce chemin.
        - Le serveur d'impression reste en service, et les administrateurs des serveurs gardent la main dessus.
        """, $"""
        - Show that a server administrator (Tier 1) can reach Tier 0 through `{print.Name}`, and cut that path.
        - The print server stays in service, and server administrators keep managing it.
        """),
        CutPath(serverAdmins.Name, serverAdmins.Name, T(
            $"Section **Sécurité** de `{print.Name}` : une option fait que tout compte qui s'y authentifie y laisse son TGT.",
            $"`{print.Name}`'s **Security** section: one option makes every account that authenticates to it leave its TGT there.")),
        NoPathLeft(T("Actualisez **Chemins d'attaque** : la liste doit être vide.", "Refresh **Attack paths**: the list must be empty.")),
        RuleClear("o3", 2, "DLG-01", T("Plus aucune délégation non contrainte hors des contrôleurs de domaine", "No unconstrained delegation outside domain controllers"),
            T("Fiche DLG-01.", "Rule sheet DLG-01.")),
        Exists("o4", print.Name, ADObjectType.Computer, T($"`{print.Name}` existe toujours", $"`{print.Name}` still exists"),
            T("L'atelier a besoin de ses traceurs.", "The factory needs its printers.")));

    return (T("Chemin d'attaque : délégation non contrainte", "Attack path: unconstrained delegation"), T(
        $"""
        `{print.Name}` est approuvé pour la **délégation Kerberos non contrainte** : tout compte qui s'y authentifie y laisse son TGT. Un attaquant qui contrôle ce serveur force un contrôleur de domaine à s'y connecter (service Spouleur, « PrinterBug ») et récupère le TGT du DC.

        Les administrateurs de serveurs (Tier 1) contrôlent ce serveur : ils peuvent donc atteindre le Tier 0.

        ## Le chemin
        1. `{serverAdmins.Name}` → `{print.Name}` : contrôle total sur l'OU `{servers.Name}`.
        2. `{print.Name}` → contrôle du domaine : délégation non contrainte.

        {See(serverAdmins.Name)}

        ## Le corriger
        - Passez en délégation **contrainte** vers les seuls services nécessaires, ou retirez la délégation.
        - Ajoutez les comptes sensibles à **Protected Users** et désactivez le Spouleur sur les DC.
        """,
        $"""
        `{print.Name}` is trusted for **unconstrained Kerberos delegation**: every account that authenticates to it leaves its TGT there. An attacker who controls this server coerces a domain controller into connecting (Print Spooler, "PrinterBug") and grabs the DC's TGT.

        Server administrators (Tier 1) control this server: they can therefore reach Tier 0.

        ## The path
        1. `{serverAdmins.Name}` → `{print.Name}`: full control over the `{servers.Name}` OU.
        2. `{print.Name}` → domain control: unconstrained delegation.

        {See(serverAdmins.Name)}

        ## Fix it
        - Switch to **constrained** delegation to the required services only, or remove delegation.
        - Add sensitive accounts to **Protected Users** and disable the Spooler on DCs.
        """));
});

// ─── 07. Délégation contrainte vers un service du contrôleur de domaine ─────────
Scenario("07-constrained-delegation", "college-les-tilleuls.local", "adventureworks.local", d =>
{
    var web = Ou(d, T("Serveurs Web", "Web Servers"), "Tier 1");
    var intranet = Computer(web, T("SRV-INTRANET", "WEB01"), "Tier 1", T("Serveur de l'intranet.", "Intranet web server."));
    intranet.AccountFlags = ADAccountFlags.TrustedToAuthForDelegation;
    intranet.AllowedToDelegateTo.Add($"cifs/DC01.{d.Name}");
    intranet.AllowedToDelegateTo.Add("cifs/DC01");
    var groups = Ou(d, T("Groupes", "Groups"), "Tier 2");
    var webTeam = Group(groups, T("GG-Webmestres", "GG-Web-Team"), "Tier 2", T("Équipe qui publie l'intranet.", "Team publishing the intranet."));
    Delegate(web, webTeam.Name, TrusteeType.Group, "GenericAll", RightCategory.FullControl, "Tier 1");
    User(groups, T("p.mercier", "d.wright"), "Tier 2", T("Webmestre.", "Webmaster.")).MemberOf.Add(webTeam.Name);

    Lab(T("Collège Les Tilleuls : l'intranet", "Adventure Works: the intranet"), LabLevel.Intermediate, 25,
        T($"""
        Le **Collège Les Tilleuls** accueille 650 élèves. Son intranet tourne sur `{intranet.Name}`, installé par un prestataire pour « afficher les documents du serveur de fichiers » ; deux professeurs documentalistes, membres de `{webTeam.Name}`, le mettent à jour.

        Le référent numérique du rectorat signale que ce type d'intranet est une cible fréquente. Vous êtes l'administrateur réseau du collège (une demi-journée par semaine).
        """, $"""
        **Adventure Works Cycles** builds bicycles and employs 500 people. Its intranet runs on `{intranet.Name}`, installed by a contractor to "show documents from the file server"; the web team, members of `{webTeam.Name}`, keeps it up to date.

        The group's security officer warns that this kind of intranet is a frequent target. You are the network administrator.
        """),
        T($"""
        - Montrez comment un webmestre peut ouvrir une session d'administrateur sur un contrôleur de domaine, puis coupez ce chemin.
        - L'intranet reste en service.
        """, $"""
        - Show how a webmaster can open an administrator session on a domain controller, then cut that path.
        - The intranet stays in service.
        """),
        CutPath(webTeam.Name, webTeam.Name, T(
            $"Section **Sécurité** de `{intranet.Name}` : vers quels services peut-il déléguer ?",
            $"`{intranet.Name}`'s **Security** section: to which services can it delegate?")),
        NoPathLeft(T("Actualisez **Chemins d'attaque** : la liste doit être vide.", "Refresh **Attack paths**: the list must be empty.")),
        RuleClear("o3", 2, "DLG-02", T("Plus de délégation avec transition de protocole vers un contrôleur de domaine", "No delegation with protocol transition to a domain controller"),
            T("Fiche DLG-02.", "Rule sheet DLG-02.")),
        Exists("o4", intranet.Name, ADObjectType.Computer, T($"`{intranet.Name}` existe toujours", $"`{intranet.Name}` still exists"),
            T("Les élèves ont besoin de l'intranet.", "Staff need the intranet.")));

    return (T("Chemin d'attaque : délégation contrainte vers un DC", "Attack path: constrained delegation to a DC"), T(
        $"""
        `{intranet.Name}` peut se faire passer pour n'importe quel utilisateur auprès du service `cifs` de **DC01** (délégation contrainte avec transition de protocole). Qui contrôle ce serveur ouvre une session sur le contrôleur de domaine en tant qu'administrateur.

        ## Le chemin
        1. `{webTeam.Name}` → `{intranet.Name}` : contrôle total sur l'OU `{web.Name}`.
        2. `{intranet.Name}` → `DC01` : délégation contrainte vers `cifs/DC01`.
        3. `DC01` → contrôle du domaine.

        {See(webTeam.Name)}

        ## Le corriger
        - Limitez la délégation aux services dont l'intranet a vraiment besoin, jamais vers un DC.
        - Réservez l'administration du serveur au Tier 1 et retirez le contrôle total des webmestres.
        """,
        $"""
        `{intranet.Name}` can impersonate any user to the `cifs` service of **DC01** (constrained delegation with protocol transition). Whoever controls this server logs on to the domain controller as an administrator.

        ## The path
        1. `{webTeam.Name}` → `{intranet.Name}`: full control over the `{web.Name}` OU.
        2. `{intranet.Name}` → `DC01`: constrained delegation to `cifs/DC01`.
        3. `DC01` → domain control.

        {See(webTeam.Name)}

        ## Fix it
        - Limit delegation to the services the intranet really needs, never to a DC.
        - Keep server administration in Tier 1 and remove the webmasters' full control.
        """));
});

// ─── 08. Propriétaire d'un compte de contrôleur de domaine ──────────────────────
Scenario("08-owner", "agence-meridien.local", "proseware.local", d =>
{
    var users = Ou(d, T("Utilisateurs", "Staff"), "Tier 2");
    var tech = User(users, T("j.petit", "a.davies"), "Tier 2", T(
        "Ancien administrateur, aujourd'hui chef de projet. A préparé le compte de DC02 en 2021.",
        "Former administrator, now project manager. Pre-created the DC02 account in 2021."));
    var dc2 = Computer(Find(d, "Domain Controllers", ADObjectType.OrganizationalUnit), "DC02", "Tier 0", T("Second contrôleur de domaine.", "Second domain controller."));
    dc2.AccountFlags = ADAccountFlags.TrustedForDelegation;
    dc2.Owner = tech.Name;

    Lab(T("Agence Méridien : l'héritage de l'ancien administrateur", "Proseware: the former administrator's legacy"), LabLevel.Beginner, 15,
        T($"""
        L'**Agence Méridien** est une agence de communication de 35 personnes. En 2021, l'administrateur de l'époque, `{tech.Name}`, a préparé le compte de **DC02** avant de le promouvoir. Il est depuis devenu chef de projet et n'a plus aucun rôle informatique.

        Vous reprenez l'administration de l'AD. Avant de signer la passation, vous vérifiez que l'ancien administrateur n'a plus aucun accès privilégié, direct ou indirect.
        """, $"""
        **Proseware** is a 35-person marketing agency. In 2021, the administrator at the time, `{tech.Name}`, pre-created the **DC02** account before promoting it. They have since become a project manager with no IT role.

        You are taking over AD administration. Before signing the handover, you check that the former administrator has no privileged access left, direct or indirect.
        """),
        T($"""
        - Trouvez l'accès qu'a gardé `{tech.Name}` et retirez-le.
        - DC02 reste un contrôleur de domaine, et `{tech.Name}` garde son compte de chef de projet.
        """, $"""
        - Find the access `{tech.Name}` kept and remove it.
        - DC02 stays a domain controller, and `{tech.Name}` keeps their project manager account.
        """),
        CutPath(tech.Name, tech.Name, T(
            "Section **Sécurité** de DC02 : qui en est le propriétaire ?", "DC02's **Security** section: who owns it?")),
        NoPathLeft(T("Actualisez **Chemins d'attaque** : la liste doit être vide.", "Refresh **Attack paths**: the list must be empty.")),
        RuleClear("o3", 1, "ACL-01", T("DC02 a un propriétaire administratif", "DC02 has an administrative owner"),
            T("Fiche ACL-01 : le propriétaire d'un objet de Tier 0 doit être Domain Admins.", "Rule sheet ACL-01: the owner of a Tier 0 object must be Domain Admins."), "DC02"),
        Exists("o4", "DC02", ADObjectType.Computer, T("DC02 existe toujours", "DC02 still exists"),
            T("Supprimer un contrôleur de domaine n'est pas une correction.", "Deleting a domain controller is not a fix.")),
        Exists("o5", tech.Name, ADObjectType.User, T($"`{tech.Name}` a toujours son compte", $"`{tech.Name}` still has an account"),
            T("Le chef de projet a besoin de son compte bureautique.", "The project manager needs their office account.")));

    return (T("Chemin d'attaque : propriétaire d'un objet", "Attack path: object owner"), T(
        $"""
        Le compte ordinateur de **DC02** a été créé à l'avance par `{tech.Name}`, qui en est resté **propriétaire**. Un propriétaire peut toujours réécrire les permissions de l'objet, par exemple pour configurer une délégation basée sur les ressources (RBCD) et ouvrir une session sur DC02.

        ## Le chemin
        1. `{tech.Name}` → `DC02` : propriétaire de l'objet.
        2. `DC02` → contrôle du domaine.

        {See(tech.Name)}

        ## Le corriger
        - Donnez la propriété de DC02 à **Domain Admins** (champ propriétaire, section **Sécurité**).
        - Vérifiez le propriétaire de tous les objets Tier 0 : c'est le point ANSSI `vuln_owner`.
        """,
        $"""
        The **DC02** computer account was pre-created by `{tech.Name}`, who is still its **owner**. An owner can always rewrite the object's permissions, for example to set up resource-based constrained delegation (RBCD) and log on to DC02.

        ## The path
        1. `{tech.Name}` → `DC02`: object owner.
        2. `DC02` → domain control.

        {See(tech.Name)}

        ## Fix it
        - Make **Domain Admins** the owner of DC02 (owner field, **Security** section).
        - Check the owner of every Tier 0 object: this is ANSSI point `vuln_owner`.
        """));
});

// ─── 09. Contrôle total pour les utilisateurs authentifiés ──────────────────────
Scenario("09-full-control", "fromagerie-alpage.local", "lucernepublishing.local", d =>
{
    var groups = Ou(d, T("Groupes", "Groups"), "Tier 1");
    var backupAdmins = Group(groups, T("GG-Sauvegarde-Admins", "GG-Backup-Admins"), "Tier 1", T(
        "Logiciel de sauvegarde : contrôle total sur le domaine demandé par l'éditeur.",
        "Backup software: full control over the domain required by the vendor."));
    Delegate(d, backupAdmins.Name, TrusteeType.Group, "GenericAll", RightCategory.FullControl, "Tier 0");
    var services = Ou(d, T("Comptes de service", "Service Accounts"), "Tier 1");
    var svc = User(services, T("svc-sauvegarde", "svc-backup"), "Tier 1", T("Compte du logiciel de sauvegarde.", "Backup software account."));
    svc.MemberOf.Add(backupAdmins.Name);
    Delegate(services, "Authenticated Users", TrusteeType.Group, "GenericAll", RightCategory.FullControl, "Tier 1");

    Lab(T("Fromagerie de l'Alpage : l'assistant d'installation", "Lucerne Publishing: the setup wizard"), LabLevel.Intermediate, 25,
        T($"""
        La **Fromagerie de l'Alpage** est une coopérative de 90 salariés. Le nouveau logiciel de sauvegarde a été installé un vendredi soir ; l'assistant échouait, et le prestataire a « débloqué » la situation en donnant des droits à **Authenticated Users**.

        Depuis, tout fonctionne et personne n'y a retouché. Le commissaire aux comptes demande pour la première fois un état des droits sur l'annuaire.
        """, $"""
        **Lucerne Publishing** is a 150-employee publisher. The new backup software was installed on a Friday night; the wizard kept failing, and the contractor "unblocked" it by granting rights to **Authenticated Users**.

        Everything has worked since and nobody touched it again. The statutory auditor is asking, for the first time, for a review of directory permissions.
        """),
        T($"""
        - Montrez ce que n'importe quel salarié peut faire avec ces droits, et coupez ce chemin.
        - Les sauvegardes continuent de tourner : `{svc.Name}` reste en place.
        """, $"""
        - Show what any employee can do with these rights, and cut that path.
        - Backups keep running: `{svc.Name}` stays in place.
        """),
        CutPath(anyUser, AnyUserLabel(), T(
            $"Fenêtre **Délégations** : quels droits a Authenticated Users sur l'OU `{services.Name}` ?",
            $"**Delegations** window: which rights does Authenticated Users have on the `{services.Name}` OU?")),
        NoPathLeft(T($"`{backupAdmins.Name}` a encore le contrôle total du domaine : son compte de service reste un chemin.",
            $"`{backupAdmins.Name}` still has full control of the domain: its service account is still a path.")),
        Exists("o3", svc.Name, ADObjectType.User, T($"Le compte `{svc.Name}` existe toujours", $"Account `{svc.Name}` still exists"),
            T("Sans lui, plus de sauvegarde.", "Without it, no more backups.")));

    return (T("Chemin d'attaque : contrôle total pour tous", "Attack path: full control for everyone"), T(
        $"""
        Pendant l'installation du logiciel de sauvegarde, **Authenticated Users** a reçu le contrôle total sur l'OU `{services.Name}`, « pour que l'assistant fonctionne ». Tout utilisateur peut donc réinitialiser le mot de passe de `{svc.Name}`, dont le groupe contrôle tout le domaine.

        ## Le chemin
        1. N'importe quel utilisateur → `{svc.Name}` : contrôle total hérité de l'OU.
        2. `{svc.Name}` → `{backupAdmins.Name}` : appartenance.
        3. `{backupAdmins.Name}` → contrôle total sur le domaine.

        {See("N'importe quel utilisateur du domaine")}

        ## Le corriger
        - Retirez la délégation d'Authenticated Users sur l'OU.
        - Remplacez le contrôle total du domaine par les seuls droits de sauvegarde, sur un **gMSA**.
        """,
        $"""
        While installing the backup software, **Authenticated Users** was given full control over the `{services.Name}` OU, "so that the wizard works". Any user can therefore reset the password of `{svc.Name}`, whose group controls the whole domain.

        ## The path
        1. Any domain user → `{svc.Name}`: full control inherited from the OU.
        2. `{svc.Name}` → `{backupAdmins.Name}`: membership.
        3. `{backupAdmins.Name}` → full control over the domain.

        {See("Any domain user")}

        ## Fix it
        - Remove the Authenticated Users delegation from the OU.
        - Replace full control over the domain with backup rights only, on a **gMSA**.
        """));
});

// ─── 10. Chaîne : Kerberoasting, réinitialisation, DnsAdmins ────────────────────
Scenario("10-attack-chain", "mairie-saint-aubin.local", "relecloud.local", d =>
{
    var services = Ou(d, T("Comptes de service", "Service Accounts"), "Tier 1");
    var svc = User(services, T("svc-planning", "svc-timesheet"), "Tier 1", T(
        "Application de planning : réinitialise les mots de passe des agents à leur arrivée.",
        "Timesheet application: resets new staff passwords on arrival."));
    svc.ServicePrincipalNames.Add($"HTTP/{T("planning", "timesheet")}.{d.Name}");
    var it = Ou(d, T("Informatique", "IT Staff"), "Tier 1");
    var admin = User(it, T("it-nfaure", "it-swalker"), "Tier 1", T("Administrateur réseau.", "Network administrator."));
    admin.MemberOf.Add("DnsAdmins");
    Delegate(it, svc.Name, TrusteeType.User, "Reset Password", RightCategory.PasswordReset, "Tier 1");

    Lab(T("Mairie de Saint-Aubin : trois petites erreurs", "Relecloud: three small mistakes"), LabLevel.Advanced, 35,
        T($"""
        La **mairie de Saint-Aubin** (12 000 habitants, 180 agents) a vu une commune voisine paralysée trois semaines par un rançongiciel. Le maire veut savoir si la même chose peut arriver ici.

        Vous êtes le seul informaticien de la mairie. Aucun réglage ne vous paraît alarmant : un logiciel de planning, un administrateur réseau, quelques délégations posées au fil des années.
        """, $"""
        **Relecloud** is a 600-employee logistics company. A competitor was crippled for three weeks by ransomware, and the board wants to know whether the same could happen here.

        You are the IT manager. No setting looks alarming: a timesheet application, a network administrator, a few delegations set over the years.
        """),
        T($"""
        - Reconstituez la chaîne complète qui mène au domaine.
        - Coupez-la, puis assurez-vous qu'aucun des comptes de la chaîne n'en est plus un point de départ.
        - Le logiciel de planning et le compte de l'administrateur réseau restent en place.
        """, $"""
        - Rebuild the full chain that leads to the domain.
        - Cut it, then make sure none of the chain's accounts is a starting point any more.
        - The timesheet application and the network administrator's account stay in place.
        """),
        CutPath(anyUser, AnyUserLabel(), T(
            "Un seul maillon suffit pour couper ce chemin-là : le SPN, la délégation, ou l'appartenance à DnsAdmins.",
            "One link is enough to cut this particular path: the SPN, the delegation, or the DnsAdmins membership.")),
        NoPathLeft(T($"Les comptes de la chaîne sont eux-mêmes des sources : tant que `{admin.Name}` est dans DnsAdmins, il reste un chemin.",
            $"The chain's accounts are sources themselves: as long as `{admin.Name}` is in DnsAdmins, a path remains.")),
        RuleClear("o3", 2, "PRIV-04", T("DnsAdmins ne contient plus de compte non privilégié", "DnsAdmins no longer contains a non-privileged account"),
            T("Fiche PRIV-04.", "Rule sheet PRIV-04.")),
        Exists("o4", svc.Name, ADObjectType.User, T($"Le compte `{svc.Name}` existe toujours", $"Account `{svc.Name}` still exists"),
            T("Le service RH en a besoin chaque lundi.", "HR needs it every Monday.")),
        Exists("o5", admin.Name, ADObjectType.User, T($"Le compte `{admin.Name}` existe toujours", $"Account `{admin.Name}` still exists"),
            T("L'administrateur réseau a besoin de son compte.", "The network administrator needs their account.")));

    return (T("Chemin d'attaque : une chaîne en trois rebonds", "Attack path: a three-hop chain"), T(
        $"""
        Aucun réglage n'est catastrophique à lui seul. Mis bout à bout, ils mènent pourtant au domaine :

        - `{svc.Name}` a un SPN : il est kerberoastable.
        - Il peut réinitialiser les mots de passe de l'OU `{it.Name}`, délégation posée « pour l'arrivée des agents ».
        - `{admin.Name}` est membre de **DnsAdmins**, qui peut faire charger une DLL au service DNS des contrôleurs de domaine. DnsAdmins n'est pas protégé par AdminSDHolder : la délégation s'applique.

        ## Le chemin
        1. N'importe quel utilisateur → `{svc.Name}` : Kerberoasting.
        2. `{svc.Name}` → `{admin.Name}` : réinitialisation du mot de passe.
        3. `{admin.Name}` → `DnsAdmins` → contrôle du domaine.

        {See("N'importe quel utilisateur du domaine")}

        ## Le corriger
        Couper **un seul** maillon suffit à supprimer ce chemin. Essayez chacun, en vérifiant avec **Actualiser** :
        - le compte de service en gMSA ;
        - la délégation limitée à l'OU des agents ;
        - DnsAdmins vidé (administration DNS depuis le Tier 0).
        """,
        $"""
        No single setting is a disaster on its own. Put end to end, they still lead to the domain:

        - `{svc.Name}` has an SPN: it is kerberoastable.
        - It can reset passwords in the `{it.Name}` OU, a delegation set "for staff onboarding".
        - `{admin.Name}` is a member of **DnsAdmins**, which can make the domain controllers' DNS service load a DLL. DnsAdmins is not protected by AdminSDHolder: the delegation applies.

        ## The path
        1. Any domain user → `{svc.Name}`: Kerberoasting.
        2. `{svc.Name}` → `{admin.Name}`: password reset.
        3. `{admin.Name}` → `DnsAdmins` → domain control.

        {See("Any domain user")}

        ## Fix it
        Breaking **one** link is enough to remove this path. Try each one, checking with **Refresh**:
        - the service account as a gMSA;
        - the delegation restricted to the staff OU;
        - DnsAdmins emptied (DNS administered from Tier 0).
        """));
});

// ─── 11. Mot de passe non obligatoire sur un compte d'Account Operators ─────────
Scenario("11-empty-password", "librairie-papyrus.lan", "humongousinsurance.lan", d =>
{
    var users = Ou(d, T("Utilisateurs", "Staff"), "Tier 2");
    var temp = User(users, T("stagiaire-compta", "temp-accounting"), "Tier 2", T(
        "Compte créé pour un stagiaire en 2022, ajouté à Account Operators pour créer des comptes.",
        "Account created for an intern in 2022, added to Account Operators to create accounts."));
    temp.AccountFlags = ADAccountFlags.PasswordNotRequired;
    temp.MemberOf.Add("Account Operators");
    temp.AdminCount = true;

    Lab(T("Librairie Papyrus : le compte du stagiaire", "Humongous Insurance: the intern's account"), LabLevel.Beginner, 15,
        T($"""
        La **Librairie Papyrus** compte quatre magasins et 30 libraires. En 2022, un stagiaire en comptabilité devait créer des comptes pour les vendeurs saisonniers ; on lui a fait un compte « vite fait », et il est reparti.

        En faisant le ménage avant l'arrivée des saisonniers de Noël, vous tombez sur ce compte toujours actif.
        """, $"""
        **Humongous Insurance** is a 120-employee broker. In 2022, an accounting intern had to create accounts for seasonal staff; they were given a "quick" account, then left.

        While cleaning up before the Christmas seasonal staff arrive, you come across this account, still enabled.
        """),
        T($"""
        - Évaluez le risque que représente `{temp.Name}` et supprimez le chemin.
        - Account Operators ne doit plus servir à créer des comptes : ce n'est pas son rôle.
        """, $"""
        - Assess the risk `{temp.Name}` represents and remove the path.
        - Account Operators must no longer be used to create accounts: that is not its purpose.
        """),
        CutPath(anyUser, AnyUserLabel(), T(
            $"Section **Sécurité** de `{temp.Name}` : une option accepte un mot de passe vide.",
            $"`{temp.Name}`'s **Security** section: one option accepts an empty password.")),
        NoPathLeft(T("Même désactivé ou protégé par un mot de passe, un compte de Tier 2 dans Account Operators reste un chemin.",
            "Even disabled or with a password, a Tier 2 account in Account Operators is still a path.")),
        RuleClear("o3", 2, "PWD-03", T("Plus aucun compte actif sans mot de passe obligatoire", "No enabled account without a required password"),
            T("Fiche PWD-03.", "Rule sheet PWD-03.")),
        RuleClear("o4", 1, "PRIV-03", T("Account Operators est vide", "Account Operators is empty"),
            T("L'ANSSI recommande que les groupes d'opérateurs restent vides ; déléguez plutôt la création de comptes sur une OU.", "ANSSI recommends keeping operator groups empty; delegate account creation on an OU instead."), "Account Operators"));

    return (T("Chemin d'attaque : mot de passe non obligatoire", "Attack path: password not required"), T(
        $"""
        `{temp.Name}` porte l'option **PASSWD_NOTREQD** : la stratégie de mot de passe ne s'applique pas, et un mot de passe vide est accepté. Un simple essai de connexion sans mot de passe suffit.

        Le compte est membre d'**Account Operators**, qui peut modifier la plupart des comptes et groupes du domaine et se connecter aux contrôleurs de domaine.

        ## Le chemin
        1. N'importe quel utilisateur → `{temp.Name}` : mot de passe vide possible.
        2. `{temp.Name}` → `Account Operators` → contrôle du domaine.

        {See("N'importe quel utilisateur du domaine")}

        ## Le corriger
        - Décochez « Mot de passe non requis » et désactivez le compte s'il ne sert plus.
        - Videz Account Operators : l'ANSSI recommande que les groupes d'opérateurs restent vides.
        """,
        $"""
        `{temp.Name}` has the **PASSWD_NOTREQD** flag: the password policy does not apply and an empty password is accepted. A single logon attempt without a password is enough.

        The account is a member of **Account Operators**, which can modify most domain accounts and groups and log on to domain controllers.

        ## The path
        1. Any domain user → `{temp.Name}`: an empty password is possible.
        2. `{temp.Name}` → `Account Operators` → domain control.

        {See("Any domain user")}

        ## Fix it
        - Clear "Password not required" and disable the account if it is no longer used.
        - Empty Account Operators: ANSSI recommends keeping operator groups empty.
        """));
});

// ─── 12. Groupes imbriqués jusqu'à DnsAdmins ────────────────────────────────────
Scenario("12-nested-groups", "cooperative-vignoble.local", "fourthcoffee.local", d =>
{
    var groups = Ou(d, T("Groupes", "Groups"), "Tier 1");
    var dns = Group(groups, T("GG-Admins-DNS", "GG-DNS-Admins"), "Tier 1", T("Administrateurs DNS.", "DNS administrators."));
    dns.MemberOf.Add("DnsAdmins");
    var network = Group(groups, T("GG-Reseau", "GG-Network-Team"), "Tier 1", T("Équipe réseau.", "Network team."));
    network.MemberOf.Add(dns.Name);
    var users = Ou(d, T("Utilisateurs", "Staff"), "Tier 2");
    var user = User(users, T("e.chevalier", "k.walker"), "Tier 2", T("Technicien réseau, compte bureautique.", "Network technician, office account."));
    user.MemberOf.Add(network.Name);

    Lab(T("Coopérative du Vignoble : qui est vraiment admin DNS ?", "Fourth Coffee: who really is a DNS admin?"), LabLevel.Beginner, 15,
        T($"""
        La **Coopérative du Vignoble** réunit 50 salariés et une centaine de saisonniers pendant les vendanges. L'équipe réseau, deux personnes, a été ajoutée « au groupe DNS » il y a des années, pour gérer les enregistrements des nouveaux sites.

        `{user.Name}`, technicien réseau, utilise son compte bureautique pour tout. Vous préparez la revue annuelle des droits.
        """, $"""
        **Fourth Coffee** runs 30 coffee shops. The two-person network team was added "to the DNS group" years ago, to manage records for new shops.

        `{user.Name}`, a network technician, uses their office account for everything. You are preparing the yearly access review.
        """),
        T($"""
        - Trouvez par quel chemin `{user.Name}` atteint le contrôle du domaine, et coupez-le.
        - `{user.Name}` reste dans l'équipe réseau (`{network.Name}`).
        """, $"""
        - Find the path through which `{user.Name}` reaches domain control, and cut it.
        - `{user.Name}` stays in the network team (`{network.Name}`).
        """),
        CutPath(user.Name, user.Name, T(
            $"Suivez les appartenances de `{network.Name}`, puis de `{dns.Name}`.", $"Follow the memberships of `{network.Name}`, then `{dns.Name}`.")),
        NoPathLeft(T("Actualisez **Chemins d'attaque** : la liste doit être vide.", "Refresh **Attack paths**: the list must be empty.")),
        RuleClear("o3", 2, "PRIV-04", T("DnsAdmins ne contient plus de compte non privilégié", "DnsAdmins no longer contains a non-privileged account"),
            T("Fiche PRIV-04.", "Rule sheet PRIV-04.")),
        Obj("o4", 1, T($"`{user.Name}` reste dans `{network.Name}`", $"`{user.Name}` stays in `{network.Name}`"),
            T("Le problème n'est pas l'équipe réseau, mais ce que son groupe hérite.", "The problem is not the network team, but what its group inherits."),
            new LabCheck { Type = LabCheckType.MemberOf, Object = user.Name, Group = network.Name, Effective = false }));

    return (T("Chemin d'attaque : groupes imbriqués", "Attack path: nested groups"), T(
        $"""
        `{user.Name}` ne voit dans ses propriétés que le groupe `{network.Name}`. Par imbrication, il est pourtant membre de **DnsAdmins**, et donc à une DLL près du contrôle du domaine.

        ## Le chemin
        1. `{user.Name}` → `{network.Name}` → `{dns.Name}` → `DnsAdmins` : appartenances imbriquées.
        2. `DnsAdmins` → contrôle du domaine (chargement d'une DLL par le service DNS des DC).

        {See(user.Name)}

        ## Le corriger
        - Sortez `{dns.Name}` de DnsAdmins : administrez le DNS depuis des comptes Tier 0 dédiés.
        - Évitez les imbrications profondes : elles cachent les droits réels.
        """,
        $"""
        `{user.Name}` only sees the `{network.Name}` group in its properties. Through nesting, it is however a member of **DnsAdmins**, one DLL away from domain control.

        ## The path
        1. `{user.Name}` → `{network.Name}` → `{dns.Name}` → `DnsAdmins`: nested memberships.
        2. `DnsAdmins` → domain control (DLL loaded by the DCs' DNS service).

        {See(user.Name)}

        ## Fix it
        - Remove `{dns.Name}` from DnsAdmins: administer DNS from dedicated Tier 0 accounts.
        - Avoid deep nesting: it hides the real rights.
        """));
});

// ─── 13. DCSync délégué à un outil de synchronisation ──────────────────────────
Scenario("13-dcsync", "cabinet-delorme.local", "alpineskihouse.local", d =>
{
    var services = Ou(d, T("Comptes de service", "Service Accounts"), "Tier 1");
    var svc = User(services, "svc-sync", "Tier 1", T(
        "Outil de synchronisation des mots de passe vers la messagerie en ligne (prestataire, 2023).",
        "Password synchronisation tool for the online mailbox service (contractor, 2023)."));
    // La console web de l'outil tourne sous ce compte : il porte un SPN
    svc.ServicePrincipalNames.Add($"HTTP/sync.{d.Name}");
    Delegate(d, svc.Name, TrusteeType.User, "DS-Replication-Get-Changes", RightCategory.Other, "Tier 0");
    Delegate(d, svc.Name, TrusteeType.User, "DS-Replication-Get-Changes-All", RightCategory.Replication, "Tier 0");

    Lab(T("Cabinet Delorme : la synchronisation oubliée", "Alpine Ski House: the forgotten synchronisation"), LabLevel.Intermediate, 20,
        T($"""
        Le **Cabinet Delorme** est un cabinet d'expertise comptable de 60 personnes. En 2023, un prestataire a installé un outil qui synchronisait les mots de passe de l'annuaire vers une messagerie en ligne. Le cabinet est revenu l'an dernier à un serveur de messagerie interne, mais l'outil n'a jamais été désinstallé : le prestataire doit passer le mois prochain.

        Un client, une banque, exige un état des comptes capables de lire les secrets de l'annuaire avant de renouveler son contrat.
        """, $"""
        **Alpine Ski House** is a ski resort operator with 60 permanent staff. In 2023, a contractor installed a tool that synchronised directory passwords to an online mailbox service. The company moved back to an on-premises mail server last year, but the tool was never uninstalled: the contractor is coming next month.

        A key partner, a bank, requires a list of the accounts able to read directory secrets before renewing its contract.
        """),
        T($"""
        - Montrez ce que n'importe quel salarié peut obtenir à partir de `{svc.Name}`, et coupez ce chemin.
        - Le compte `{svc.Name}` reste en place : le prestataire en a besoin pour désinstaller l'outil.
        """, $"""
        - Show what any employee can get starting from `{svc.Name}`, and cut that path.
        - The `{svc.Name}` account stays: the contractor needs it to uninstall the tool.
        """),
        CutPath(anyUser, AnyUserLabel(), T(
            $"Le compte `{svc.Name}` a un SPN ; fenêtre **Délégations**, racine du domaine : quels droits a-t-il ?",
            $"The `{svc.Name}` account has an SPN; **Delegations** window, domain root: which rights does it have?")),
        NoPathLeft(T($"Retirer le SPN ne suffit pas : `{svc.Name}` lui-même mène encore au domaine.",
            $"Removing the SPN is not enough: `{svc.Name}` itself still leads to the domain.")),
        RuleClear("o3", 2, "ACL-03", T("La racine du domaine n'accorde plus la réplication à un compte ordinaire", "The domain root no longer grants replication to an ordinary account"),
            T("Fiche ACL-03 : seuls les contrôleurs de domaine répliquent les secrets.", "Rule sheet ACL-03: only domain controllers replicate secrets."), d.Name),
        Exists("o4", svc.Name, ADObjectType.User, T($"Le compte `{svc.Name}` existe toujours", $"Account `{svc.Name}` still exists"),
            T("Le prestataire en a besoin pour désinstaller l'outil : retirez ses droits, pas le compte.", "The contractor needs it to uninstall the tool: remove its rights, not the account.")));

    return (T("Chemin d'attaque : DCSync délégué", "Attack path: delegated DCSync"), T(
        $"""
        Pour lire les mots de passe, l'outil de synchronisation a reçu avec le compte `{svc.Name}` les deux droits de **réplication** sur la racine du domaine (`DS-Replication-Get-Changes` et `DS-Replication-Get-Changes-All`) : ceux d'un contrôleur de domaine. Comme le compte porte un SPN, n'importe quel utilisateur obtient un ticket chiffré avec son mot de passe, et le casse hors ligne.

        ## Le chemin
        1. N'importe quel utilisateur → `{svc.Name}` : Kerberoasting.
        2. `{svc.Name}` → contrôle du domaine : DCSync, l'extraction des secrets de tous les comptes, krbtgt compris.

        {See("N'importe quel utilisateur du domaine")}

        ## Le corriger
        - Retirez les deux droits de réplication de `{svc.Name}` sur la racine du domaine (fenêtre **Délégations**).
        - Un outil qui en a vraiment besoin (Entra Connect, par exemple) se protège comme un contrôleur de domaine : compte du Tier 0, gMSA ou mot de passe long, pas de SPN inutile. C'est le point ANSSI `vuln_permissions_naming_context`.
        """,
        $"""
        To read passwords, the synchronisation tool's `{svc.Name}` account was given both **replication** rights on the domain root (`DS-Replication-Get-Changes` and `DS-Replication-Get-Changes-All`): those of a domain controller. As the account has an SPN, any user gets a ticket encrypted with its password, and cracks it offline.

        ## The path
        1. Any domain user → `{svc.Name}`: Kerberoasting.
        2. `{svc.Name}` → domain control: DCSync, extracting the secrets of every account, krbtgt included.

        {See("Any domain user")}

        ## Fix it
        - Remove both replication rights of `{svc.Name}` on the domain root (**Delegations** window).
        - A tool that really needs them (Entra Connect, for example) is protected like a domain controller: Tier 0 account, gMSA or long password, no needless SPN. This is ANSSI point `vuln_permissions_naming_context`.
        """));
});

// ─── 14. Shadow Credentials sur le contrôleur de domaine ───────────────────────
Scenario("14-shadow-credentials", "laboratoire-helios.local", "treyresearch.local", d =>
{
    var it = Ou(d, T("Informatique", "IT"), "Tier 1");
    var pilot = Group(it, T("GG-Pilote-Hello", "GG-Hello-Pilot"), "Tier 1", T(
        "Projet pilote Windows Hello Entreprise, mené par un prestataire.", "Windows Hello for Business pilot project, run by a contractor."));
    var tech = User(it, T("t.garnier", "m.clarke"), "Tier 1", T("Technicien du prestataire, membre du projet pilote.", "Contractor technician, member of the pilot project."));
    tech.MemberOf.Add(pilot.Name);
    Delegate(d, pilot.Name, TrusteeType.Group, "WriteProperty:msDS-KeyCredentialLink", RightCategory.WriteKeyCredentialLink, "Tier 0");
    // Le serveur d'administration : les administrateurs du domaine s'y connectent, à leur place dans le Tier 0
    var tier0 = Ou(d, T("Serveurs Tier 0", "Tier 0 Servers"), "Tier 0");
    var jump = Computer(tier0, "SRV-ADMIN01", "Tier 0", T("Serveur d'administration : les outils d'administration du domaine.", "Administration server: the domain administration tools."));
    var admins = Ou(d, T("Administration", "Admin Accounts"), "Tier 0");
    var admin = User(admins, T("adm-rlefort", "adm-mclark"), "Tier 0", T("Compte d'administration du domaine.", "Domain admin account."));
    admin.MemberOf.Add("Domain Admins");
    admin.AdminCount = true;
    admin.AccountFlags = ADAccountFlags.NotDelegated;
    jump.Sessions = new() { admin.Name };

    Lab(T("Laboratoire Hélios : le pilote Windows Hello", "Trey Research: the Windows Hello pilot"), LabLevel.Advanced, 25,
        T($"""
        Le **Laboratoire Hélios** est un laboratoire d'analyses médicales de 120 personnes. Pour se connecter sans mot de passe, la direction a lancé un projet pilote **Windows Hello Entreprise**, confié à un prestataire. Pour « gagner du temps », son technicien `{tech.Name}` a donné un droit à son groupe sur tout le domaine.

        Le pilote est terminé et sera généralisé. Avant cela, le responsable de la sécurité veut savoir ce que ce droit permet réellement.
        """, $"""
        **Trey Research** is a 120-person research lab. To log on without passwords, management started a **Windows Hello for Business** pilot, run by a contractor. To "save time", their technician `{tech.Name}` gave their group a right on the whole domain.

        The pilot is over and will be rolled out. Before that, the security officer wants to know what this right really allows.
        """),
        T($"""
        - Montrez ce que `{tech.Name}` peut faire avec le droit de son groupe, et coupez ce chemin.
        - Windows Hello doit continuer de fonctionner : le droit prévu pour cela, celui de **Key Admins**, reste en place.
        """, $"""
        - Show what `{tech.Name}` can do with their group's right, and cut that path.
        - Windows Hello must keep working: the right meant for it, that of **Key Admins**, stays in place.
        """),
        CutPath(tech.Name, tech.Name, T(
            "Fenêtre **Délégations**, racine du domaine : qui peut écrire msDS-KeyCredentialLink ? Le droit est hérité par les comptes qui ne sont pas protégés par AdminSDHolder.",
            "**Delegations** window, domain root: who can write msDS-KeyCredentialLink? The right is inherited by the accounts AdminSDHolder does not protect.")),
        NoPathLeft(T("Actualisez **Chemins d'attaque** : la liste doit être vide.", "Refresh **Attack paths**: the list must be empty.")),
        RuleClear("o3", 2, "ACL-03", T("La racine du domaine n'accorde msDS-KeyCredentialLink qu'aux groupes prévus", "The domain root grants msDS-KeyCredentialLink only to the intended groups"),
            T("Fiche ACL-03 : sur un domaine neuf, seuls Key Admins et Enterprise Key Admins ont ce droit.", "Rule sheet ACL-03: on a fresh domain, only Key Admins and Enterprise Key Admins have this right."), d.Name),
        Obj("o4", 1, T("Key Admins garde son droit sur msDS-KeyCredentialLink", "Key Admins keeps its right on msDS-KeyCredentialLink"),
            T("C'est le droit par défaut qui fait fonctionner Windows Hello : ne le retirez pas.", "It is the default right that makes Windows Hello work: do not remove it."),
            new LabCheck { Type = LabCheckType.DelegationExists, Trustee = "Key Admins", Target = d.Name, RightCategory = RightCategory.WriteKeyCredentialLink }),
        Exists("o5", tech.Name, ADObjectType.User, T($"Le compte `{tech.Name}` existe toujours", $"Account `{tech.Name}` still exists"),
            T("Le technicien généralisera le déploiement : retirez le droit de son groupe, pas son compte.", "The technician will roll out the deployment: remove their group's right, not their account.")),
        Obj("o6", 1, T($"`{admin.Name}` reste administrateur du domaine", $"`{admin.Name}` stays a domain administrator"),
            T("Le compte d'administration est à sa place sur le serveur d'administration : le problème est ailleurs.", "The admin account belongs on the administration server: the problem is elsewhere."),
            new LabCheck { Type = LabCheckType.MemberOf, Object = admin.Name, Group = "Domain Admins" }));

    return (T("Chemin d'attaque : Shadow Credentials", "Attack path: Shadow Credentials"), T(
        $"""
        Pour le pilote **Windows Hello Entreprise**, le prestataire a donné à son groupe `{pilot.Name}` le droit d'écrire `msDS-KeyCredentialLink` sur la racine du domaine, au lieu de passer par le groupe **Key Admins** prévu pour cela. Le droit est hérité par tous les comptes qu'AdminSDHolder ne protège pas : pas les contrôleurs de domaine ni les administrateurs, mais le compte d'ordinateur de **{jump.Name}**, le serveur d'administration. En y ajoutant sa propre clé, un membre du groupe s'authentifie comme ce serveur (Shadow Credentials), en devient administrateur, et y récupère les identifiants de `{admin.Name}`, qui y a une session.

        ## Le chemin
        1. `{tech.Name}` → `{pilot.Name}` : appartenance.
        2. `{pilot.Name}` → `{jump.Name}` : écriture de msDS-KeyCredentialLink, héritée de la racine du domaine.
        3. `{jump.Name}` → `{admin.Name}` : session ouverte, identifiants en mémoire.
        4. `{admin.Name}` → Domain Admins → contrôle du domaine.

        {See(tech.Name)}

        ## Le corriger
        - Retirez le droit de `{pilot.Name}` sur la racine du domaine ; laissez celui de **Key Admins**, présent sur tout domaine neuf.
        - Windows Hello se déploie avec Key Admins (ou Enterprise Key Admins), dont l'appartenance se surveille comme celle de Domain Admins. C'est le point ANSSI `vuln_permissions_naming_context`.
        """,
        $"""
        For the **Windows Hello for Business** pilot, the contractor gave their group `{pilot.Name}` the right to write `msDS-KeyCredentialLink` on the domain root, instead of using the **Key Admins** group meant for it. The right is inherited by every account AdminSDHolder does not protect: not the domain controllers nor the administrators, but the computer account of **{jump.Name}**, the administration server. By adding their own key there, a group member authenticates as that server (Shadow Credentials), becomes its administrator, and retrieves the credentials of `{admin.Name}`, who has a session there.

        ## The path
        1. `{tech.Name}` → `{pilot.Name}`: membership.
        2. `{pilot.Name}` → `{jump.Name}`: write msDS-KeyCredentialLink, inherited from the domain root.
        3. `{jump.Name}` → `{admin.Name}`: open session, credentials in memory.
        4. `{admin.Name}` → Domain Admins → domain control.

        {See(tech.Name)}

        ## Fix it
        - Remove the right of `{pilot.Name}` on the domain root; keep that of **Key Admins**, present on every fresh domain.
        - Windows Hello is deployed with Key Admins (or Enterprise Key Admins), whose membership is monitored like that of Domain Admins. This is ANSSI point `vuln_permissions_naming_context`.
        """));
});

// ─── 15. Délégation basée sur la ressource vers le serveur de sauvegarde ───────
Scenario("15-rbcd", "lycee-jean-moulin.local", "wideworldimporters.local", d =>
{
    var servers = Ou(d, T("Serveurs", "Servers"), "Tier 1");
    var techs = Group(servers, T("GG-Techniciens-Serveurs", "GG-Server-Technicians"), "Tier 1", T(
        "Techniciens qui installent et dépannent les serveurs.", "Technicians who install and troubleshoot servers."));
    var tech = User(servers, T("n.morel", "j.walker"), "Tier 1", T("Technicien serveurs.", "Server technician."));
    tech.MemberOf.Add(techs.Name);
    // Leur métier : joindre les nouveaux serveurs au domaine
    Delegate(servers, techs.Name, TrusteeType.Group, "CreateChild:computer", RightCategory.ComputerManagement, "Tier 1");
    var tier0 = Ou(d, T("Serveurs Tier 0", "Tier 0 Servers"), "Tier 0");
    var backup = Computer(tier0, T("SRV-SAUV01", "SRV-BACKUP01"), "Tier 0", T(
        "Serveur de sauvegarde des contrôleurs de domaine. Installé par les techniciens serveurs.",
        "Domain controller backup server. Installed by the server technicians."));
    var admins = Ou(d, T("Administration", "Admin Accounts"), "Tier 0");
    var admin = User(admins, T("adm-sauvegarde", "adm-backup"), "Tier 0", T("Administrateur du domaine chargé des sauvegardes.", "Domain administrator in charge of backups."));
    admin.MemberOf.Add("Domain Admins");
    admin.AdminCount = true;
    admin.AccountFlags = ADAccountFlags.NotDelegated;
    // L'administrateur des sauvegardes travaille sur le serveur : à sa place, dans le Tier 0
    backup.Sessions = new() { admin.Name };
    // Le technicien qui a joint le serveur a gardé la main sur sa délégation basée sur la ressource
    Delegate(backup, techs.Name, TrusteeType.Group, "WriteProperty:msDS-AllowedToActOnBehalfOfOtherIdentity", RightCategory.WriteAllowedToAct, "Tier 0");

    Lab(T("Lycée Jean-Moulin : le serveur de sauvegarde", "Wide World Importers: the backup server"), LabLevel.Advanced, 30,
        T($"""
        Le **lycée Jean-Moulin** accueille 1 400 élèves ; son informatique est gérée par une petite équipe de techniciens. L'an dernier, les techniciens serveurs ont installé **{backup.Name}**, le serveur qui sauvegarde les contrôleurs de domaine. L'administrateur `{admin.Name}` s'y connecte chaque semaine pour vérifier les sauvegardes.

        La région, qui finance le lycée, impose un audit des droits d'administration avant la rentrée.
        """, $"""
        **Wide World Importers** is an import company with 400 employees; its IT is run by a small team of technicians. Last year, the server technicians installed **{backup.Name}**, the server that backs up the domain controllers. The administrator `{admin.Name}` logs on to it every week to check the backups.

        The group's insurer requires an audit of administration rights before renewing the cyber-insurance policy.
        """),
        T($"""
        - Montrez comment `{tech.Name}` peut prendre la main sur **{backup.Name}**, et ce que cela lui donne ; coupez ce chemin.
        - Les techniciens continuent de joindre des serveurs au domaine, et `{admin.Name}` reste administrateur du domaine.
        """, $"""
        - Show how `{tech.Name}` can take over **{backup.Name}**, and what this gives them; cut that path.
        - The technicians keep joining servers to the domain, and `{admin.Name}` stays a domain administrator.
        """),
        CutPath(tech.Name, tech.Name, T(
            $"Fenêtre **Délégations** : quels droits a `{techs.Name}` sur le compte d'ordinateur de {backup.Name} ? Puis, section **Sécurité** du serveur : qui y a une session ?",
            $"**Delegations** window: which rights does `{techs.Name}` have on the {backup.Name} computer account? Then, the server's **Security** section: who has a session there?")),
        NoPathLeft(T("Actualisez **Chemins d'attaque** : la liste doit être vide.", "Refresh **Attack paths**: the list must be empty.")),
        RuleClear("o3", 2, "TIER-03", T($"Le Tier 1 n'a plus de droit de contrôle sur {backup.Name}", $"Tier 1 no longer has a control right on {backup.Name}"),
            T("Fiche TIER-03 : un groupe du Tier 1 ne doit pas contrôler une machine du Tier 0.", "Rule sheet TIER-03: a Tier 1 group must not control a Tier 0 machine."), backup.Name),
        Exists("o4", backup.Name, ADObjectType.Computer, T($"Le serveur {backup.Name} existe toujours", $"Server {backup.Name} still exists"),
            T("Sans lui, plus de sauvegarde des contrôleurs de domaine.", "Without it, the domain controllers are no longer backed up.")),
        Obj("o5", 1, T($"`{admin.Name}` reste administrateur du domaine", $"`{admin.Name}` stays a domain administrator"),
            T("Le compte d'administration est à sa place sur un serveur du Tier 0 : le problème est ailleurs.", "The admin account belongs on a Tier 0 server: the problem is elsewhere."),
            new LabCheck { Type = LabCheckType.MemberOf, Object = admin.Name, Group = "Domain Admins" }),
        Obj("o6", 1, T("Les techniciens joignent toujours des serveurs au domaine", "Technicians can still join servers to the domain"),
            T("La délégation de création des comptes d'ordinateur sur l'OU des serveurs reste en place.", "The computer creation delegation on the servers OU stays."),
            new LabCheck { Type = LabCheckType.DelegationExists, Trustee = techs.Name, Target = servers.Name, RightCategory = RightCategory.ComputerManagement }));

    return (T("Chemin d'attaque : délégation basée sur la ressource (RBCD)", "Attack path: resource-based constrained delegation (RBCD)"), T(
        $"""
        En joignant **{backup.Name}** au domaine, les techniciens serveurs ont gardé le droit d'écrire `msDS-AllowedToActOnBehalfOfOtherIdentity` sur son compte d'ordinateur. Avec un compte d'ordinateur qu'il contrôle, un membre de `{techs.Name}` y inscrit ce compte, puis obtient vers le serveur un ticket au nom de n'importe quel utilisateur, administrateur compris : il en devient administrateur local (RBCD). Or `{admin.Name}`, administrateur du domaine, y a une session ouverte.

        ## Le chemin
        1. `{tech.Name}` → `{techs.Name}` : appartenance.
        2. `{techs.Name}` → `{backup.Name}` : écriture de la délégation basée sur la ressource.
        3. `{backup.Name}` → `{admin.Name}` : session ouverte, identifiants en mémoire.
        4. `{admin.Name}` → Domain Admins → contrôle du domaine.

        {See(tech.Name)}

        ## Le corriger
        - Retirez le droit de `{techs.Name}` sur le compte d'ordinateur de {backup.Name} (fenêtre **Délégations**) : une machine du Tier 0 ne se délègue qu'au Tier 0.
        - Gardez leur délégation de création de comptes d'ordinateur sur l'OU des serveurs : c'est leur métier. Fermer la session de `{admin.Name}` ne corrige rien : il reviendra la semaine prochaine.
        """,
        $"""
        When joining **{backup.Name}** to the domain, the server technicians kept the right to write `msDS-AllowedToActOnBehalfOfOtherIdentity` on its computer account. With a computer account they control, a member of `{techs.Name}` adds that account there, then gets a ticket to the server in the name of any user, administrators included: they become its local administrator (RBCD). And `{admin.Name}`, a domain administrator, has a session open there.

        ## The path
        1. `{tech.Name}` → `{techs.Name}`: membership.
        2. `{techs.Name}` → `{backup.Name}`: write the resource-based delegation.
        3. `{backup.Name}` → `{admin.Name}`: open session, credentials in memory.
        4. `{admin.Name}` → Domain Admins → domain control.

        {See(tech.Name)}

        ## Fix it
        - Remove the right of `{techs.Name}` on the {backup.Name} computer account (**Delegations** window): a Tier 0 machine is delegated to Tier 0 only.
        - Keep their computer account creation delegation on the servers OU: it is their job. Closing `{admin.Name}`'s session fixes nothing: they will be back next week.
        """));
});

// ─── Sommaire ──────────────────────────────────────────────────────────────────
string Table(string lang) => string.Join("\n", index.Where(i => i.Lang == lang)
    .Select(i => $"| [`{i.Id}`]({lang}/{i.Id}.smad-x.json) | `{i.Domain}` | {i.Title[(i.Title.IndexOf(':') + 1)..].Trim()} |"));

File.WriteAllText(Path.Combine(outDir, "README.md"), $"""
    # Chemins d'attaque : domaines d'exemple / Attack paths: sample domains

    Fichiers générés par `dotnet run --project tools/SampleGenerator -- Samples/AttackPaths SMAD-X.Core/Labs`. Ne les modifiez pas à la main.
    Generated by `dotnet run --project tools/SampleGenerator -- Samples/AttackPaths SMAD-X.Core/Labs`. Do not edit them by hand.

    ## Français

    > **Matériel du formateur.** Chaque domaine décrit son chemin d'attaque et sa correction : ce sont les corrigés des exercices « Chemins d'attaque » de SMAD-X. Ne les donnez pas aux apprenants avant l'exercice.

    Ouvrez un fichier avec **Fichier > Ouvrir**, sélectionnez le domaine pour lire le scénario, puis **Analyse > Chemins d'attaque** (`Ctrl+Shift+K`).

    | Fichier | Domaine | Chemin |
    |---|---|---|
    {Table("fr")}

    ## English

    > **Trainer material.** Each domain describes its attack path and its fix: these are the answer keys of the SMAD-X "Attack paths" labs. Do not give them to learners before the lab.

    Open a file with **File > Open**, select the domain to read the scenario, then **Analyse > Attack paths** (`Ctrl+Shift+K`).

    | File | Domain | Path |
    |---|---|---|
    {Table("en")}

    """.Replace("\r\n", "\n"));
Console.WriteLine("README.md");

/// <summary>Exercice tiré d'un scénario : titre réaliste, contexte, mission et objectifs corrigés.</summary>
record LabSpec(string Title, LabLevel Level, int Minutes, string Context, string Mission, LabObjective[] Objectives);
