// Génère les domaines d'exemple des chemins d'attaque : Samples/AttackPaths/{fr,en}/*.smad-x.json
// Chaque scénario existe en français et en anglais, chacun avec son propre nom de domaine.
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;

var outDir = args[0];
var reference = new DateTime(2026, 10, 22);
var data = new ADDataService();
var fr = true; // langue du scénario en cours de construction

string T(string french, string english) => fr ? french : english;

ADObject Find(ADObject root, string name, ADObjectType? type = null) =>
    Flatten(root).First(o => o.Name == name && (type is null || o.Type == type));
IEnumerable<ADObject> Flatten(ADObject o) => new[] { o }.Concat(o.Children.SelectMany(Flatten));
ADObject Add(ADObject parent, ADObject child) { child.Parent = parent; parent.Children.Add(child); child.UpdateDistinguishedNameTree(); return child; }

ADObject Ou(ADObject parent, string name, string tier) =>
    Add(parent, new ADObject(name, ADObjectType.OrganizationalUnit) { Tier = tier, Owner = "Domain Admins", CreatedDate = reference.AddDays(-400) });

ADObject User(ADObject parent, string name, string tier, string description) => Add(parent, new ADObject(name, ADObjectType.User)
{
    Tier = tier, Owner = "Domain Admins", Description = description,
    PwdLastSet = reference.AddDays(-30), LastLogonTimestamp = reference.AddDays(-1), CreatedDate = reference.AddDays(-400)
});

ADObject Group(ADObject parent, string name, string tier, string description) => Add(parent, new ADObject(name, ADObjectType.Group)
{
    Tier = tier, Owner = "Domain Admins", Description = description, CreatedDate = reference.AddDays(-400),
    GroupScope = ADGroupScope.Global, GroupCategory = ADGroupCategory.Security
});

ADObject Computer(ADObject parent, string name, string tier, string description) => Add(parent, new ADObject(name, ADObjectType.Computer)
{
    Tier = tier, Owner = "Domain Admins", Description = description,
    PwdLastSet = reference.AddDays(-20), LastLogonTimestamp = reference.AddDays(-1), CreatedDate = reference.AddDays(-400)
});

void Delegate(ADObject target, string trustee, TrusteeType type, string right, RightCategory category, string tier) =>
    target.Delegations.Add(new ADDelegation
    {
        TrusteeName = trustee, TrusteeType = type, TargetDN = target.DistinguishedName,
        Right = right, RightCategory = category, Tier = tier
    });

// Domaine neuf durci (constats d'un AD sortie d'installation corrigés), dates figées
ADObject Baseline(string domainName)
{
    TierConfigurationService.Instance.ResetToDefaults();
    var d = data.CreateFreshDomainStructure(domainName);
    foreach (var o in Flatten(d))
    {
        o.CreatedDate = reference.AddDays(-400);
        o.ModifiedDate = reference.AddDays(-400);
        if (o.PwdLastSet != null) o.PwdLastSet = reference.AddDays(o.Type == ADObjectType.Computer ? -20 : -30);
        if (o.LastLogonTimestamp != null) o.LastLogonTimestamp = reference.AddDays(-1);
    }
    var admin = Find(d, "Administrator", ADObjectType.User);
    admin.AccountFlags = ADAccountFlags.NotDelegated;
    admin.MemberOf.Remove("Enterprise Admins");
    admin.MemberOf.Remove("Schema Admins");
    d.Tier = "Tier 0";
    var findings = new ADSecurityAuditService().Run(d, reference).Findings;
    if (findings.Count > 0) throw new Exception("Domaine de départ non propre : " + string.Join(", ", findings.Select(f => f.Rule.Id + " " + f.Object.Name)));
    return d;
}

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

var index = new List<(string Id, string Lang, string Domain, string Title)>();

void Scenario(string id, string frDomain, string enDomain, Func<ADObject, (string Title, string Body)> build)
{
    foreach (var (french, domainName) in new[] { (true, frDomain), (false, enDomain) })
    {
        fr = french;
        var d = Baseline(domainName);
        var (title, body) = build(d);
        d.Description = $"# {title}\n\n{body.Trim()}\n";
        var lang = fr ? "fr" : "en";
        var dir = Path.Combine(outDir, lang);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{id}.smad-x.json");
        var document = new ADRootDocument { Version = ADRootDocument.CurrentVersion, Domain = d, SitesTopology = data.CreateFreshTopology(d.Name) };
        if (!data.SaveDocumentAsync(document, path).GetAwaiter().GetResult())
            throw new Exception("Échec de l'écriture de " + path);
        index.Add((id, lang, domainName, title));
        Console.WriteLine($"{lang}/{id} : {domainName}");
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
    intranet.AllowedToDelegateTo.Add($"cifs/DC01.{d.Name}");
    intranet.AllowedToDelegateTo.Add("cifs/DC01");
    var groups = Ou(d, T("Groupes", "Groups"), "Tier 2");
    var webTeam = Group(groups, T("GG-Webmestres", "GG-Web-Team"), "Tier 2", T("Équipe qui publie l'intranet.", "Team publishing the intranet."));
    Delegate(web, webTeam.Name, TrusteeType.Group, "GenericAll", RightCategory.FullControl, "Tier 1");
    User(groups, T("p.mercier", "d.wright"), "Tier 2", T("Webmestre.", "Webmaster.")).MemberOf.Add(webTeam.Name);

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

// ─── Sommaire ──────────────────────────────────────────────────────────────────
string Table(string lang) => string.Join("\n", index.Where(i => i.Lang == lang)
    .Select(i => $"| [`{i.Id}`]({lang}/{i.Id}.smad-x.json) | `{i.Domain}` | {i.Title[(i.Title.IndexOf(':') + 1)..].Trim()} |"));

File.WriteAllText(Path.Combine(outDir, "README.md"), $"""
    # Chemins d'attaque : domaines d'exemple / Attack paths: sample domains

    Fichiers générés par `dotnet run --project tools/SampleGenerator -- Samples/AttackPaths`. Ne les modifiez pas à la main.
    Generated by `dotnet run --project tools/SampleGenerator -- Samples/AttackPaths`. Do not edit them by hand.

    ## Français

    Ouvrez un fichier avec **Fichier > Ouvrir**, sélectionnez le domaine pour lire le scénario, puis **Analyse > Chemins d'attaque** (`Ctrl+Shift+K`).

    | Fichier | Domaine | Chemin |
    |---|---|---|
    {Table("fr")}

    ## English

    Open a file with **File > Open**, select the domain to read the scenario, then **Analyse > Attack paths** (`Ctrl+Shift+K`).

    | File | Domain | Path |
    |---|---|---|
    {Table("en")}

    """.Replace("\r\n", "\n"));
Console.WriteLine("README.md");
