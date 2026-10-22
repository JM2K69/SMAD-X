# adminCount orphelin

## Ce que l'audit a détecté
Un compte a adminCount = 1 alors qu'il n'est plus membre d'aucun groupe protégé.

## Pourquoi c'est dangereux
SDProp ne remet jamais adminCount à 0. Un ancien administrateur garde donc ses permissions figées et l'**héritage désactivé** : les délégations posées sur son OU (par exemple la réinitialisation de mot de passe par le support) ne s'appliquent plus à lui. C'est aussi un indice : ce compte a été administrateur.

## Comment corriger
Remettre adminCount à 0, puis réactiver l'héritage des permissions sur l'objet.
