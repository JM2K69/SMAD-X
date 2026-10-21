# Container Policies (SYSVOL)

Ce container représente le dossier `SYSVOL\Policies` sur les contrôleurs de domaine.

Chaque GPO possède un sous-dossier identifié par son **GUID** dans :
```
\\<domaine>\SYSVOL\<domaine>\Policies\{GUID}
```