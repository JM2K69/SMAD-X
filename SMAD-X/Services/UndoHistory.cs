using System.Collections.Generic;

namespace SMADX.Services
{
    /// <summary>
    /// Historique annuler/rétablir par instantanés : chaque état est le JSON complet du document.
    /// Un instantané identique au précédent n'est pas enregistré, si bien que l'on peut appeler
    /// <see cref="Record"/> sans risque après chaque action de l'utilisateur.
    /// </summary>
    public sealed class UndoHistory
    {
        private readonly List<string> _states = new();
        private int _index = -1;

        public UndoHistory(int capacity = 100) => Capacity = capacity;

        /// <summary>Nombre maximal d'états conservés (les plus anciens sont oubliés).</summary>
        public int Capacity { get; }

        public bool CanUndo => _index > 0;
        public bool CanRedo => _index >= 0 && _index < _states.Count - 1;

        /// <summary>État de référence courant (le dernier enregistré ou restauré).</summary>
        public string? Current => _index >= 0 ? _states[_index] : null;

        /// <summary>Repart de zéro avec un état initial (ouverture d'un fichier, nouveau domaine…).</summary>
        public void Reset(string state)
        {
            _states.Clear();
            _states.Add(state);
            _index = 0;
        }

        /// <summary>
        /// Enregistre un nouvel état s'il diffère du courant. Les états « rétablissables » sont alors
        /// abandonnés, comme dans tout éditeur. Renvoie vrai si un état a été ajouté.
        /// </summary>
        public bool Record(string state)
        {
            if (_index >= 0 && _states[_index] == state)
                return false;

            _states.RemoveRange(_index + 1, _states.Count - _index - 1);
            _states.Add(state);
            _index = _states.Count - 1;

            if (_states.Count > Capacity)
            {
                _states.RemoveAt(0);
                _index--;
            }
            return true;
        }

        /// <summary>État précédent, ou null s'il n'y en a pas.</summary>
        public string? Undo() => CanUndo ? _states[--_index] : null;

        /// <summary>État suivant, ou null s'il n'y en a pas.</summary>
        public string? Redo() => CanRedo ? _states[++_index] : null;
    }
}
