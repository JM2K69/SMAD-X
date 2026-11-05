using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Windows
{
    /// <summary>
    /// Une ACE de la DACL effective : explicite (<see cref="InheritedFrom"/> null) ou héritée d'un dossier parent.
    /// <see cref="Inheritance"/> est l'héritage tel qu'il se lit sur cet objet (InheritOnly : ne s'applique pas à lui).
    /// </summary>
    public sealed record EffectiveAce(AceType Type, string Trustee, NtfsRights Mask, AceInheritance Inheritance, string? InheritedFrom)
    {
        public bool Inherited => InheritedFrom != null;
        public bool InheritOnly => Inheritance.HasFlag(AceInheritance.InheritOnly);
    }

    /// <summary>
    /// La DACL effective d'un fichier ou d'un dossier, comme Windows la construit en propageant l'héritage :
    /// <list type="number">
    /// <item>les ACE explicites, les refus avant les autorisations (ordre canonique) ;</item>
    /// <item>puis, si l'héritage est activé, celles que le parent transmet : (CI) aux sous-dossiers, (OI) aux fichiers ;
    /// un sous-dossier garde une ACE (OI) seule en « héritage seulement » pour la passer à ses fichiers ; (NP) arrête
    /// la transmission aux enfants directs ; (IO) ne s'applique qu'à ce qui hérite ;</item>
    /// <item>CREATOR OWNER devient le propriétaire de l'enfant, et reste transmis en « héritage seulement ».</item>
    /// </list>
    /// Les ACE héritées du parent viennent avant celles du grand-parent : plus une ACE est proche, plus elle compte tôt.
    /// </summary>
    public static class NtfsInheritance
    {
        public static IReadOnlyList<EffectiveAce> Dacl(WindowsHost host, string path)
        {
            List<EffectiveAce>? acl = null;
            foreach (var (node, nodePath) in host.Chain(path))
                acl = acl == null || !node.InheritanceEnabled
                    ? Explicit(node)
                    : [.. Explicit(node), .. Inherit(acl, node, ParentPath(nodePath))];
            return acl ?? [];

            // L'origine d'une ACE héritée : le dossier où elle est explicite
            static string ParentPath(string p) => WindowsHost.Parent(p)!;
        }

        /// <summary>ACE explicites, refus d'abord ; sur un fichier, l'héritage n'a pas de sens et disparaît.</summary>
        public static List<EffectiveAce> Explicit(NtfsNode node) =>
            node.Aces.OrderBy(a => a.Type == AceType.Deny ? 0 : 1)
                .Select(a => new EffectiveAce(a.Type, a.Trustee, a.Mask,
                    node.IsDirectory ? a.Inheritance : a.Inheritance & AceInheritance.InheritOnly, null))
                .ToList();

        /// <summary>Ce que le parent (DACL <paramref name="parent"/>) transmet à l'enfant.</summary>
        public static IEnumerable<EffectiveAce> Inherit(IReadOnlyList<EffectiveAce> parent, NtfsNode child, string parentPath)
        {
            foreach (var ace in parent)
            {
                var flags = ace.Inheritance;
                var origin = ace.InheritedFrom ?? parentPath;
                var noPropagate = flags.HasFlag(AceInheritance.NoPropagate);
                var creatorOwner = ace.Trustee == WellKnown.CreatorOwner;
                if (child.IsDirectory)
                {
                    if (flags.HasFlag(AceInheritance.ContainerInherit))
                    {
                        var kept = noPropagate ? AceInheritance.None : flags & AceInheritance.ThisFolderSubfoldersAndFiles;
                        if (creatorOwner)
                        {
                            yield return ace with { Trustee = child.Owner, Inheritance = AceInheritance.None, InheritedFrom = origin };
                            if (!noPropagate)
                                yield return ace with { Inheritance = kept | AceInheritance.InheritOnly, InheritedFrom = origin };
                        }
                        else
                        {
                            yield return ace with { Inheritance = kept, InheritedFrom = origin };
                        }
                    }
                    else if (flags.HasFlag(AceInheritance.ObjectInherit) && !noPropagate)
                    {
                        yield return ace with { Inheritance = AceInheritance.ObjectInherit | AceInheritance.InheritOnly, InheritedFrom = origin };
                    }
                }
                else if (flags.HasFlag(AceInheritance.ObjectInherit))
                {
                    yield return ace with { Trustee = creatorOwner ? child.Owner : ace.Trustee, Inheritance = AceInheritance.None, InheritedFrom = origin };
                }
            }
        }
    }
}
