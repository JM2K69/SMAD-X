using System;
using System.Security.Cryptography;

namespace SMADX.Services.Labs
{
    /// <summary>
    /// Session de travail d'un apprenant : chaque session a son identifiant, tiré au hasard, dont sont dérivés
    /// les identifiants des objets de sa copie de travail. Le travail est ainsi marqué dès le départ, ce qui
    /// repère à la correction une copie recopiée ou venue d'une autre session.
    /// </summary>
    public static class WorkSession
    {
        /// <summary>Identifiant d'un objet du départ de l'exercice dans la copie de travail d'une session.</summary>
        public static Guid ObjectId(Guid sessionId, Guid startId)
        {
            var bytes = SHA256.HashData([.. sessionId.ToByteArray(), .. startId.ToByteArray()])[..16];
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50); // version 5 (identifiant dérivé)
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variante RFC 4122
            return new Guid(bytes);
        }
    }
}
