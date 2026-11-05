namespace SMADX.Models
{
    /// <summary>État du sceau d'une copie, constaté à la lecture du fichier.</summary>
    public enum SubmissionSeal
    {
        /// <summary>Pas de sceau : la copie n'a pas été enregistrée par le simulateur.</summary>
        Missing,
        Valid,
        /// <summary>Le contenu a changé depuis l'enregistrement par le simulateur.</summary>
        Broken
    }
}
