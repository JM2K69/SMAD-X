# Ordinateur sans LAPS

## Ce que l'audit a détecté
Le mot de passe du compte administrateur local de cet ordinateur n'est pas géré par LAPS : aucune date d'expiration de mot de passe LAPS n'est enregistrée dans l'annuaire.

## Pourquoi c'est dangereux
Sans LAPS, le mot de passe de l'administrateur local est souvent le même sur toutes les machines, posé par l'image de déploiement. Qui le récupère sur un poste (ou son empreinte) l'utilise sur tous les autres : c'est le mouvement latéral le plus courant (pass-the-hash), jusqu'à une machine où un administrateur a une session.

## Comment corriger
Déployer Windows LAPS par GPO sur les postes et les serveurs : chaque machine reçoit un mot de passe unique, aléatoire et renouvelé, stocké dans l'annuaire. Limiter ensuite sa lecture à un groupe d'administration du tier de la machine : un droit de lecture trop large est lui-même un chemin d'attaque.
