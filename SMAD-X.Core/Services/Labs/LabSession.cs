using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using SMADX.Models;

namespace SMADX.Services.Labs
{
    /// <summary>
    /// Un exercice en cours : le domaine de départ est préparé une fois, à la date du début, et sert de
    /// référence à la vérification, à « Recommencer » et à la correction de la copie dans Studio.
    /// Chaque session a son identifiant, tiré au hasard : les identifiants internes des objets du domaine
    /// de travail en sont dérivés, ce qui marque le travail de l'apprenant dès le départ (voir
    /// <see cref="SubmissionGrader"/>, qui repère ainsi une copie recopiée ou un domaine venu d'ailleurs).
    /// </summary>
    public sealed class LabSession
    {
        private static readonly LabService Labs = new();

        private LabSession(LabDefinition lab, DateTime startedOn, Guid sessionId, DateTime openedAt, int elapsedBefore)
        {
            Lab = lab;
            StartedOn = startedOn.Date;
            SessionId = sessionId;
            Start = Labs.PrepareStart(lab, StartedOn);
            OpenedAt = openedAt;
            ElapsedBefore = elapsedBefore;
        }

        /// <summary>Ouverture de cette séance de travail : le temps passé se compte à partir d'elle.</summary>
        public DateTime OpenedAt { get; }

        /// <summary>Minutes déjà passées lors des séances précédentes (copie reprise).</summary>
        public int ElapsedBefore { get; }

        /// <summary>Temps passé sur l'exercice, en minutes entamées, séances reprises comprises.</summary>
        public int ElapsedMinutes(DateTime now) => ElapsedBefore + (int)Math.Ceiling(Math.Max(0, (now - OpenedAt).TotalMinutes));

        public LabDefinition Lab { get; }

        /// <summary>Date de préparation du domaine de départ.</summary>
        public DateTime StartedOn { get; }

        /// <summary>Identifiant de la session ; Guid.Empty pour une copie faite avant les sessions.</summary>
        public Guid SessionId { get; }

        /// <summary>Domaine de départ préparé, référence de la comparaison « non modifié » (identifiants de l'exercice).</summary>
        public ADRootDocument Start { get; }

        public static LabSession Begin(LabDefinition lab, DateTime now, Guid? sessionId = null) => new(lab, now, sessionId ?? Guid.NewGuid(), now, 0);

        /// <summary>Reprend une copie enregistrée : même exercice, même date de départ, même session ; le temps passé continue.</summary>
        public static LabSession Resume(LabSubmission submission, DateTime? now = null) =>
            new(submission.Lab, submission.StartedOn, submission.SessionId, now ?? DateTime.Now, submission.ElapsedMinutes ?? 0);

        /// <summary>
        /// Nouvelle copie de travail du domaine de départ, aux mêmes dates que la référence et aux identifiants
        /// de la session : « Recommencer » redonne exactement le même domaine.
        /// </summary>
        public ADRootDocument NewWorkingCopy()
        {
            var working = Labs.PrepareStart(Lab, StartedOn);
            if (SessionId != Guid.Empty)
                foreach (var obj in Flatten(working.Domain))
                    obj.Id = SessionObjectId(SessionId, obj.Id);
            return working;
        }

        /// <summary>Identifiant d'un objet du domaine de départ dans la copie de travail d'une session.</summary>
        public static Guid SessionObjectId(Guid sessionId, Guid startId)
        {
            var bytes = SHA256.HashData([.. sessionId.ToByteArray(), .. startId.ToByteArray()])[..16];
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50); // version 5 (identifiant dérivé)
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variante RFC 4122
            return new Guid(bytes);
        }

        public LabSubmission ToSubmission(string student, ADRootDocument work, IEnumerable<string> hintsUsed, DateTime now,
                                          IReadOnlyDictionary<string, LabAnswer>? answers = null, string? studentId = null) => new()
        {
            Student = SafeText.Name(student),
            StudentId = string.IsNullOrWhiteSpace(studentId) ? null : SafeText.Name(studentId),
            LabId = Lab.Id,
            SessionId = SessionId,
            StartedOn = StartedOn,
            SavedAt = now,
            ElapsedMinutes = ElapsedMinutes(now),
            EngineVersion = EngineInfo.Version,
            RulesVersion = EngineInfo.RulesVersion,
            HintsUsed = hintsUsed.Distinct().ToList(),
            Answers = (answers ?? new Dictionary<string, LabAnswer>()).Where(a => !a.Value.IsEmpty).ToDictionary(a => a.Key, a => a.Value),
            Lab = Lab,
            Work = work
        };

        private static IEnumerable<ADObject> Flatten(ADObject root) => new[] { root }.Concat(root.Children.SelectMany(Flatten));
    }
}
