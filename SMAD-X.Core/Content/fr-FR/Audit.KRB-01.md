# Compte privilégié vulnérable au Kerberoasting

## Ce que l'audit a détecté
Un compte **utilisateur** membre (directement ou par imbrication) d'un groupe privilégié porte un ou plusieurs SPN (servicePrincipalName).

## Pourquoi c'est dangereux
N'importe quel utilisateur du domaine peut demander un ticket de service (TGS) pour un SPN. Ce ticket est chiffré avec une clé dérivée du **mot de passe du compte de service** : l'attaquant l'emporte hors ligne et tente de le casser par force brute, sans aucune alerte de verrouillage. Si le mot de passe cède, l'attaquant devient administrateur.

## Comment corriger
- Retirer le compte des groupes privilégiés : un compte de service n'a presque jamais besoin d'être administrateur du domaine.
- Remplacer le compte par un **gMSA** (mot de passe aléatoire de 120 caractères, renouvelé automatiquement).
- À défaut : mot de passe long (plus de 25 caractères) et chiffrement AES uniquement.

## Pour aller plus loin
L'ANSSI tolère un compte privilégié avec SPN seulement dans des configurations précises (point `warning_spn_priv`). Les comptes `krbtgt` (SPN `kadmin/changepw`) et les gMSA ne sont pas concernés.
