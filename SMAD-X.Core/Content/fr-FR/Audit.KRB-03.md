# Compte privilégié sans préauthentification Kerberos

## Ce que l'audit a détecté
L'option « Ne pas exiger la préauthentification Kerberos » (DONT_REQ_PREAUTH) est cochée sur un compte privilégié.

## Pourquoi c'est dangereux
Sans préauthentification, le contrôleur de domaine répond à une demande de TGT pour ce compte **sans que le demandeur prouve qu'il connaît le mot de passe**. La réponse contient une donnée chiffrée avec le mot de passe du compte : c'est l'**AS-REP roasting**. Pas besoin d'être authentifié dans le domaine pour la demander.

## Comment corriger
Décocher l'option. Elle n'existe que pour d'anciens clients Kerberos et n'a aucune raison d'être sur un compte d'administration.
