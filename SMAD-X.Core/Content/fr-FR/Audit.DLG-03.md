# Délégation contrainte vers un contrôleur de domaine

## Ce que l'audit a détecté
Un compte est autorisé à déléguer vers un service hébergé par un contrôleur de domaine.

## Pourquoi c'est dangereux
Le compte peut présenter au contrôleur de domaine les identités des utilisateurs qui s'authentifient auprès de lui. Si un administrateur du domaine s'y connecte, l'attaquant qui contrôle ce compte peut agir sur le DC en son nom.

## Comment corriger
Supprimer la délégation vers les services des DC, ou ne la conserver que pour un compte lui-même administré comme un compte de Tier 0.
