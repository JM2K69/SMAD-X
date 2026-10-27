# Politique de mot de passe du domaine trop courte

## Ce que l'audit a détecté
La politique par défaut du domaine (Default Domain Policy) accepte des mots de passe de moins de 8 caractères. C'est la valeur d'un AD tout juste installé : 7 caractères.

## Pourquoi c'est dangereux
Tous les comptes sans PSO suivent cette politique. Un mot de passe de 7 caractères, même complexe, se casse hors ligne en quelques heures à partir d'une empreinte récupérée (Kerberoasting, sauvegarde de la base, NTLM).

## Comment corriger
Fixer la longueur minimale du domaine à 8 caractères au moins (sélectionnez le domaine, section « Politique de mot de passe »), et plutôt 12 ou plus. Les comptes qui demandent davantage reçoivent un PSO.

## Pour aller plus loin
Les référentiels ne fixent pas le même seuil. Le NIST (SP 800-63B) demande 8 caractères avec une authentification multifacteur et 15 sans, et déconseille d'imposer un changement périodique. CIS Controls 5.2 demande 8 avec MFA et 14 sans. L'ANSSI, pour les comptes privilégiés, demande au moins 8 caractères et un renouvellement au moins tous les 3 ans. Quel seuil retenir pour vos utilisateurs, et pourquoi ?
