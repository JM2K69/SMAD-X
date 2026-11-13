using SMADX.Services.Sources;

namespace SMADX.Tests;

/// <summary>
/// L'analyseur de la checklist ANSSI, sur une page qui imite son code minifié : les encadrés partagés appelés avec leur
/// seule langue (« Contexte : groupes privilégiés »), les noms de groupes donnés par leur SID, une valeur affichée en mot
/// (« Désactivé »), une pastille de droit, un masque d'accès, un texte rangé dans une variable, un lien interne au rapport.
/// Ce sont les cas qu'un premier analyseur laissait vides.
/// </summary>
public class AnssiChecklistTests
{
    private const string Page = """
        var Kxe={"S-1-5-32-544":{fr:`Administrateurs`,en:`Administrators`},"S-1-5-21-domain-501":{fr:`Invit\xe9`,en:`Guest`}};
        function gO({children:e}){return(0,I.jsx)(`div`,{style:{backgroundColor:`var(--row-info-bg)`},children:e})}
        function Y({SID:e,lang:t=`fr`}){let n=Kxe[e];return n?(0,I.jsx)(`span`,{children:n[t]}):(0,I.jsx)(`code`,{children:e})}
        function _O({lang:e,noContext:t}){return t?null:e===`en`?(0,I.jsxs)(gO,{children:[(0,I.jsx)(q,{level:2,children:`Context: privileged groups`}),(0,I.jsx)(`hr`,{}),(0,I.jsx)(`ul`,{children:(0,I.jsxs)(`li`,{children:[`\xab\xA0`,(0,I.jsx)(Y,{SID:`S-1-5-32-544`,lang:`en`}),`\xA0\xbb`]})})]}):(0,I.jsxs)(gO,{children:[(0,I.jsx)(q,{level:2,children:`Contexte\xA0: groupes privil\xe9gi\xe9s`}),(0,I.jsx)(`hr`,{}),(0,I.jsx)(`ul`,{children:(0,I.jsxs)(`li`,{children:[`\xab\xA0`,(0,I.jsx)(Y,{SID:`S-1-5-32-544`,lang:`fr`}),`\xA0\xbb`]})})]})}
        function uO({val:e,lang:t=`fr`}){let n=e!==`0`&&e!==0&&e!==!1&&e!=null;return(0,I.jsx)(`span`,{children:n?t===`fr`?`Activ\xe9`:`Enabled`:t===`fr`?`D\xe9sactiv\xe9`:`Disabled`})}
        var WSe=[[{mask:16,fr:`Propri\xe9t\xe9 de lecture`,en:`Read property`},{mask:1,fr:`Cr\xe9er un enfant`,en:`Create Child`}],[{mask:4,fr:`Liste`,en:`List`}]];
        function ok({accessMask:e,lang:t}){let[n,r]=(0,f.useState)(t);return(0,I.jsx)(`table`,{children:WSe.map(t=>t.map(t=>t[n]))})}
        var Uk={fr:(0,I.jsx)(`p`,{children:`Un texte partag\xe9.`}),en:(0,I.jsx)(`p`,{children:`A shared text.`})};
        var qSe={vuln_test:{title:{fr:(0,I.jsx)(I.Fragment,{children:`Point de test`}),en:(0,I.jsx)(I.Fragment,{children:`Test point`})},grades:[1,2],changelog:[{date:`2024-01-02`,type:`change`},{date:`2020-01-01`,type:`new`}],
        body:e=>{let t=e.attrs?.data??[];return{fr:(0,I.jsxs)(I.Fragment,{children:[(0,I.jsxs)(`p`,{children:[`Le compte `,(0,I.jsx)(Y,{SID:`S-1-5-21-domain-501`,lang:`fr`}),` est activ\xe9 ; l'h\xe9ritage de la DACL `,(0,I.jsx)(uO,{val:!1,lang:`fr`}),` est risqu\xe9.`]}),(0,I.jsx)(z,{caption:`Comptes`,columns:Zc,rows:t})]}),en:(0,I.jsx)(`p`,{children:[`The `,(0,I.jsx)(Y,{SID:`S-1-5-21-domain-501`,lang:`en`}),` account is enabled.`]})}},
        reco:e=>({fr:(0,I.jsxs)(I.Fragment,{children:[(0,I.jsxs)(`p`,{children:[`Le contr\xf4le `,(0,I.jsx)(yO,{quality:1,value:`WRITE_SPN`}),` est corrig\xe9, voir `,(0,I.jsx)(K,{href:`#/ad/controlgraph/`,children:`le graphe`}),`.`]}),(0,I.jsx)(ok,{lang:`fr`,accessMask:20}),Uk.fr,(0,I.jsx)(_O,{lang:`fr`})]}),en:(0,I.jsxs)(I.Fragment,{children:[(0,I.jsx)(`p`,{children:`Fix it.`}),Uk.en,(0,I.jsx)(_O,{lang:`en`,noContext:!0})]})})}};
        function oO({children:e}){return(0,I.jsx)(`code`,{style:{fontFamily:`monospace`},children:e})}
        function rB(e,t,n,r,i=Zc,o={}){return s=>{let c=s.attrs?.data??[],l=e=>o.clear?(0,I.jsx)(`div`,{children:e}):e;return{fr:(0,I.jsxs)(I.Fragment,{children:[n,l((0,I.jsx)(z,{caption:e,columns:i,rows:c}))]}),en:(0,I.jsxs)(I.Fragment,{children:[r]})}}}
        var lSe=`Import-Module .\\AdSyncConfig.psm1
        $credential = Get-Credential`,FO=()=>(0,I.jsx)(G_e,{"data-ad-code-block":!0,children:(0,I.jsx)(NO,{code:lSe,variant:`block`})});
        var Rq={vuln_helper:{title:{fr:(0,I.jsx)(I.Fragment,{children:`Point mod\xe8le`}),en:(0,I.jsx)(I.Fragment,{children:`Template point`})},grades:[2],changelog:[{date:`2021-01-01`,type:`new`}],
        body:rB(`Objets`,`Objects`,(0,I.jsx)(`p`,{children:`Texte du mod\xe8le.`}),(0,I.jsx)(`p`,{children:`Template text.`})),
        reco:e=>({fr:(0,I.jsxs)(I.Fragment,{children:[(0,I.jsxs)(`p`,{children:[`Utiliser `,(0,I.jsx)(oO,{children:(0,I.jsx)(NO,{code:`Set-X`})}),` :`]}),(0,I.jsx)(FO,{}),e.attrs?.graph&&(0,I.jsx)(`p`,{children:`Visualisation`})]}),en:(0,I.jsx)(`p`,{children:`Use it.`})})}};
        """;

    private static AnssiPoint Point() => new AnssiChecklist(Page).Find("vuln_test") ?? throw new InvalidOperationException("vuln_test introuvable");

    [Fact]
    public void TheTitleLevelsAndDates_AreRead()
    {
        var point = Point();
        Assert.Equal("Point de test", point.TitleFr);
        Assert.Equal("Test point", point.TitleEn);
        Assert.Equal([1, 2], point.Levels);
        Assert.Equal(("2020-01-01", "2024-01-02"), (point.Added, point.Updated));
    }

    [Fact]
    public void AGroupGivenByItsSid_IsNamed_AndAValueShownAsAWord_IsWritten()
    {
        var point = Point();
        Assert.Contains("Le compte *Invité* est activé ; l'héritage de la DACL Désactivé est risqué.", point.BodyFr);
        Assert.Contains("The *Guest* account is enabled.", point.BodyEn);
        Assert.Contains("*(Tableau des objets concernés, dans le rapport d'analyse.)*", point.BodyFr);
    }

    [Fact]
    public void ASharedBox_IsOpenedInTheSheetLanguage_AsAQuote()
    {
        var reco = Point().RecoFr;
        Assert.Contains("> #### Contexte : groupes privilégiés", reco);
        Assert.Contains("> - « *Administrateurs* »", reco);
        Assert.DoesNotContain("Context:", reco);
        // L'option noContext de l'appel anglais retire l'encadré, comme sur la page
        Assert.DoesNotContain("Context", Point().RecoEn);
    }

    [Fact]
    public void ARightBadge_AnAccessMask_ASharedText_AndAReportLink_AreRendered()
    {
        var point = Point();
        Assert.Contains("Le contrôle `WRITE_SPN` est corrigé, voir le graphe.", point.RecoFr);
        Assert.Contains("- Propriété de lecture\n- Liste", point.RecoFr);
        Assert.DoesNotContain("Créer un enfant", point.RecoFr);
        Assert.Contains("Un texte partagé.", point.RecoFr);
        Assert.Contains("A shared text.", point.RecoEn);
        Assert.DoesNotContain("…", point.RecoFr);
    }

    /// <summary>
    /// Le texte d'un point construit par une fonction modèle (« body:rB(`Objets`,`Objects`,texteFr,texteEn) »), du code
    /// passé en propriété (en ligne, ou en bloc depuis une variable), un composant sans propriété, et un texte que seul le
    /// rapport d'analyse montre (« e.attrs?.graph&&… ») : la checklist ne l'affiche pas, la fiche non plus.
    /// </summary>
    [Fact]
    public void ATemplateBody_CodeProperties_AParameterlessComponent_AndAReportOnlyText()
    {
        var point = new AnssiChecklist(Page).Find("vuln_helper") ?? throw new InvalidOperationException("vuln_helper introuvable");
        Assert.Equal("Point modèle", point.TitleFr);
        Assert.StartsWith("Texte du modèle.", point.BodyFr);
        Assert.Contains("*(Tableau des objets concernés, dans le rapport d'analyse.)*", point.BodyFr);
        Assert.Equal("Template text.", point.BodyEn);
        Assert.Contains("Utiliser `Set-X` :", point.RecoFr);
        Assert.Contains("```\nImport-Module .\\AdSyncConfig.psm1\n$credential = Get-Credential\n```", point.RecoFr);
        Assert.DoesNotContain("Visualisation", point.RecoFr);
    }
}
