# Compte privilégié dont le mot de passe n'expire jamais

## Ce que l'audit a détecté
L'option « Le mot de passe n'expire jamais » est cochée sur un compte privilégié.

## Pourquoi c'est dangereux
Un mot de passe d'administrateur qui ne change jamais reste utilisable indéfiniment s'il a fuité un jour (hameçonnage, fichier de configuration, base de hashs volée). L'ANSSI classe ce point au niveau 1, le plus critique.

## Comment corriger
Décocher l'option et appliquer aux comptes privilégiés une politique de mot de passe dédiée (PSO) : longueur importante et renouvellement régulier.

## Pour aller plus loin
Le NIST (SP 800-63B) déconseille d'imposer un renouvellement périodique **aux utilisateurs**, car il pousse vers des mots de passe prévisibles. L'ANSSI, elle, exige l'expiration pour les comptes privilégiés. Les deux visions se rejoignent : mot de passe long et unique, renouvelé dès qu'une compromission est possible. Bon sujet de débat en cours !

Par défaut, le compte Administrator d'un domaine neuf a cette option : un AD tout juste installé n'est pas conforme.
