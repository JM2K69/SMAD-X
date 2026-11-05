using System;

namespace SMARX.Models
{
    /// <summary>Droits Unix : lecture, écriture, exécution (pour un dossier : lister, modifier son contenu, le traverser).</summary>
    [Flags]
    public enum Perm
    {
        None = 0,
        Execute = 1,
        Write = 2,
        Read = 4,
        All = Read | Write | Execute
    }

    public static class Perms
    {
        /// <summary>« rwx », « r-x », « --- ».</summary>
        public static string Format(Perm perm) =>
            $"{(perm.HasFlag(Perm.Read) ? 'r' : '-')}{(perm.HasFlag(Perm.Write) ? 'w' : '-')}{(perm.HasFlag(Perm.Execute) ? 'x' : '-')}";

        /// <summary>Lit « rwx », « r-x », « rw » ou « --- », comme setfacl.</summary>
        /// <exception cref="FormatException">Autre chose que r, w, x et -.</exception>
        public static Perm Parse(string? text)
        {
            var perm = Perm.None;
            foreach (var c in text ?? string.Empty)
                perm |= c switch
                {
                    'r' => Perm.Read,
                    'w' => Perm.Write,
                    'x' => Perm.Execute,
                    '-' => Perm.None,
                    _ => throw new FormatException($"Droits « {text} » : seuls r, w, x et - sont permis.")
                };
            return perm;
        }

        /// <summary>Tous les droits demandés sont accordés.</summary>
        public static bool Covers(this Perm granted, Perm needed) => (granted & needed) == needed;
    }
}
