# Compte privilégié connecté sur une machine moins sensible

## Ce que l'audit a détecté
Un compte privilégié (ou d'un tier plus sensible) a une session ouverte sur un poste ou un serveur d'un tier moins sensible.

## Pourquoi c'est dangereux
Une session laisse les identifiants du compte dans la mémoire de la machine (LSASS) : tout administrateur local de cette machine peut les y récupérer, puis les rejouer. Le chemin est direct, d'un compte de poste de travail jusqu'au domaine : **Analyse > Chemins d'attaque** le montre (administrateur local, puis session). Le cloisonnement en tiers de l'ANSSI interdit à un administrateur du Tier 0 de se connecter ailleurs que sur le Tier 0.

## Comment corriger
Fermer la session, et empêcher qu'elle se rouvre : un compte d'administration dédié par tier, l'interdiction d'ouverture de session des comptes du Tier 0 sur les autres tiers (droits « Interdire l'ouverture de session » par GPO), et un silo d'authentification qui réserve ces comptes aux machines du Tier 0. Le groupe Protected Users limite aussi ce qui reste en mémoire.
