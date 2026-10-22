# Violation du cloisonnement : GPO d'un tier moins sensible

## Ce que l'audit a détecté
Une GPO classée dans un tier moins sensible est liée à une OU d'un tier plus sensible.

## Pourquoi c'est dangereux
Quiconque peut modifier la GPO peut exécuter du code (scripts, tâches planifiées) sur toutes les machines de l'OU. Si la GPO est administrée depuis un tier moins sensible, ce tier contrôle de fait le tier plus sensible.

## Comment corriger
Utiliser pour chaque tier des GPO administrées dans ce tier, ou reclasser la GPO et restreindre ses droits de modification aux administrateurs du tier le plus sensible.
