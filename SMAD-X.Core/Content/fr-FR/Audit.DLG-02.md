# Délégation contrainte avec transition de protocole vers un contrôleur de domaine

## Ce que l'audit a détecté
Un compte peut déléguer, **avec transition de protocole**, vers un service hébergé par un contrôleur de domaine.

## Pourquoi c'est dangereux
La transition de protocole (S4U2Self) permet au compte d'obtenir un ticket au nom de **n'importe quel utilisateur, sans que celui-ci se soit authentifié**. Combinée à une délégation vers un service d'un DC (par exemple `ldap/` ou `cifs/`), elle permet de se faire passer pour un administrateur du domaine auprès du DC : compromettre ce compte revient à compromettre le domaine.

## Comment corriger
- Supprimer la délégation vers les services des contrôleurs de domaine.
- Traiter le compte comme un compte de Tier 0 tant que la délégation existe.
