# Élément du modèle de cloisonnement absent

## Ce que l'audit a détecté
Le domaine suit un modèle de cloisonnement publié (Microsoft Active Directory Tier Model ou HardenAD), mais l'un de ses éléments manque : une OU, un groupe, un compte d'administration, une GPO, le lien d'une GPO, un paramètre de GPO (un compte qu'elle ne refuse plus, un administrateur local qu'elle ne met plus) ou une délégation que le modèle pose.

## Pourquoi c'est dangereux
Un modèle de cloisonnement forme un tout : les OU rangent chaque objet dans son tier, les groupes et les délégations donnent à chaque équipe les seuls droits de son tier, et les GPO de restriction empêchent un compte d'administration de se connecter sur une machine moins sensible. Un élément retiré ou jamais déployé ouvre une brèche que le reste du modèle ne compense pas : une GPO de restriction déliée, ou un groupe retiré de ses refus, et un administrateur du Tier 0 peut de nouveau laisser ses identifiants sur un poste de travail.

## Comment corriger
Rétablir l'élément tel que le modèle le définit, ou rejouer le déploiement du modèle (dans SMAD-X : **Configuration > Appliquer un modèle de cloisonnement...**, qui n'ajoute que ce qui manque). Si l'écart est volontaire, le documenter, et vérifier dans **Analyse > Chemins d'attaque** qu'il n'ouvre aucun chemin.

## Pour aller plus loin
Les deux modèles fournissent un outil d'audit de leur propre déploiement (Audit-TierModel.ps1 pour Microsoft, le Documentator de HardenAD). Pourquoi un modèle de sécurité doit-il pouvoir détecter qu'il a été modifié après son déploiement ?
