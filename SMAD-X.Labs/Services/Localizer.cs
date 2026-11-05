using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace SMADX.Services
{
    /// <summary>
    /// Traductions FR/EN d'une application : les textes du socle commun (questions, copies), ceux que
    /// l'application ajoute (<see cref="AddTranslations"/>) et ses fiches Markdown (<see cref="LoadMarkdownContent"/>).
    /// Chaque application en dérive (LocalizationService pour SMAD-X) ; le socle parle la langue de la dernière
    /// créée (<see cref="Current"/>), celle de l'interface.
    /// </summary>
    public class Localizer : INotifyPropertyChanged
    {
        private static Localizer? _current;
        private CultureInfo _currentCulture;
        private readonly Dictionary<string, Dictionary<string, string>> _translations = new();

        /// <summary>Traductions de l'application ; sans application (outil, test), celles du socle seul.</summary>
        public static Localizer Current => _current ??= new Localizer();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected Localizer()
        {
            _currentCulture = CultureInfo.CurrentUICulture;
            AddTranslations("fr-FR", SharedFrench);
            AddTranslations("en-US", SharedEnglish);
            _current = this;
        }

        public CultureInfo CurrentCulture
        {
            get => _currentCulture;
            set
            {
                if (_currentCulture != value)
                {
                    _currentCulture = value;
                    CultureInfo.CurrentUICulture = value;

                    // Notifier TOUTES les propriétés individuellement pour forcer le rafraîchissement
                    OnPropertyChanged(nameof(CurrentCulture));

                    // Notifier toutes les clés de traduction
                    foreach (var key in _translations["fr-FR"].Keys)
                    {
                        OnPropertyChanged($"[{key}]");
                    }
                }
            }
        }

        public string this[string key] => Translate(key, _currentCulture.Name);

        /// <summary>Traduction dans une culture donnée (repli sur le français), sans changer la langue courante.</summary>
        public string Translate(string key, string cultureName)
        {
            if (_translations.TryGetValue(cultureName, out var cultureDictionary))
            {
                if (cultureDictionary.TryGetValue(key, out var translation))
                {
                    return translation;
                }
            }

            // Fallback sur français si la clé n'existe pas
            if (_translations.TryGetValue("fr-FR", out var frenchDictionary))
            {
                if (frenchDictionary.TryGetValue(key, out var translation))
                {
                    return translation;
                }
            }

            return $"[{key}]"; // Clé manquante
        }

        /// <summary>Langues disponibles (fr-FR, en-US).</summary>
        public IEnumerable<string> Cultures => _translations.Keys;

        /// <summary>Clés traduites dans une culture (vide si elle n'existe pas) : sert à vérifier que fr-FR et en-US ont les mêmes.</summary>
        public IReadOnlyCollection<string> KeysOf(string cultureName) =>
            _translations.TryGetValue(cultureName, out var dictionary) ? dictionary.Keys : Array.Empty<string>();

        private Dictionary<string, string>? _descriptionKeys;

        /// <summary>
        /// Clé « Desc.* » dont le texte, dans l'une des langues, est <paramref name="text"/> : null pour une
        /// description écrite à la main, qui ne doit pas être remplacée au changement de langue.
        /// </summary>
        public string? FindDescriptionKey(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (_descriptionKeys == null)
            {
                _descriptionKeys = new Dictionary<string, string>();
                foreach (var dictionary in _translations.Values)
                    foreach (var (key, value) in dictionary)
                        if (key.StartsWith("Desc.", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(value))
                            _descriptionKeys.TryAdd(Normalize(value), key);
            }
            return _descriptionKeys.GetValueOrDefault(Normalize(text));
        }

        /// <summary>Même texte aux fins de ligne et aux blancs d'extrémité près.</summary>
        public static bool SameText(string? a, string? b) => Normalize(a ?? string.Empty) == Normalize(b ?? string.Empty);

        private static string Normalize(string text) => text.ReplaceLineEndings("\n").Trim();

        /// <summary>Ajoute les textes d'une culture ; une clé déjà présente prend le nouveau texte.</summary>
        protected void AddTranslations(string culture, IReadOnlyDictionary<string, string> entries)
        {
            if (!_translations.TryGetValue(culture, out var dictionary))
                _translations[culture] = dictionary = new Dictionary<string, string>();
            foreach (var (key, value) in entries)
                dictionary[key] = value;
        }

        /// <summary>Préfixe des descriptions Markdown embarquées (voir Content/ et le .csproj de l'application).</summary>
        private const string ContentPrefix = "Content/";

        /// <summary>
        /// Charge les descriptions pédagogiques (clés Desc.*) depuis les fichiers Content/{culture}/{clé}.md.
        /// Ils sont embarqués dans <paramref name="assembly"/> ; un dossier Content/ placé à côté de l'exécutable
        /// les remplace fichier par fichier, ce qui permet à un enseignant d'adapter le contenu sans recompiler.
        /// </summary>
        protected void LoadMarkdownContent(Assembly assembly)
        {
            foreach (var resource in assembly.GetManifestResourceNames())
            {
                var path = resource.Replace('\\', '/');
                if (!path.StartsWith(ContentPrefix, StringComparison.Ordinal) || !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new StreamReader(stream);
                AddMarkdown(path[ContentPrefix.Length..], reader.ReadToEnd());
            }

            var overrideDir = Path.Combine(AppContext.BaseDirectory, "Content");
            if (!Directory.Exists(overrideDir))
                return;

            foreach (var file in Directory.EnumerateFiles(overrideDir, "*.md", SearchOption.AllDirectories))
            {
                try
                {
                    AddMarkdown(Path.GetRelativePath(overrideDir, file).Replace('\\', '/'), File.ReadAllText(file));
                }
                catch (IOException)
                {
                    // Fichier illisible : on garde la version embarquée
                }
            }
        }

        /// <summary>Enregistre "fr-FR/Desc.Users.md" sous la clé Desc.Users de la culture fr-FR.</summary>
        private void AddMarkdown(string relativePath, string content)
        {
            var slash = relativePath.IndexOf('/');
            if (slash <= 0)
                return;

            var culture = relativePath[..slash];
            var key = Path.GetFileNameWithoutExtension(relativePath[(slash + 1)..]);
            if (!_translations.TryGetValue(culture, out var dictionary))
                _translations[culture] = dictionary = new Dictionary<string, string>();

            // Les fichiers peuvent avoir été extraits en CRLF par git : le rendu attend des \n
            dictionary[key] = content.Replace("\r\n", "\n");
        }

        public void SetLanguage(string cultureName)
        {
            CurrentCulture = new CultureInfo(cultureName);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Textes du socle : la correction des questions
        private static readonly Dictionary<string, string> SharedFrench = new()
        {
            ["Lab.Feedback.NoAnswer"] = "Pas de réponse.",
            ["Lab.Feedback.AnswerSaved"] = "Réponse enregistrée : le formateur la corrigera.",
            ["Lab.Feedback.RightAnswer"] = "Bonne réponse.",
            ["Lab.Feedback.WrongAnswer"] = "Réponse inexacte.",
            ["Lab.Feedback.PartialAnswer"] = "Réponse incomplète : une partie des points.",
            ["Lab.Feedback.TextNotRecognised"] = "Réponse différente des réponses attendues : le formateur la relira.",
            ["Lab.Feedback.Marked"] = "Noté par le formateur.",
        };

        private static readonly Dictionary<string, string> SharedEnglish = new()
        {
            ["Lab.Feedback.NoAnswer"] = "No answer.",
            ["Lab.Feedback.AnswerSaved"] = "Answer saved: the trainer will grade it.",
            ["Lab.Feedback.RightAnswer"] = "Right answer.",
            ["Lab.Feedback.WrongAnswer"] = "Wrong answer.",
            ["Lab.Feedback.PartialAnswer"] = "Incomplete answer: part of the points.",
            ["Lab.Feedback.TextNotRecognised"] = "Answer different from the expected ones: the trainer will read it.",
            ["Lab.Feedback.Marked"] = "Graded by the trainer.",
        };
    }
}
