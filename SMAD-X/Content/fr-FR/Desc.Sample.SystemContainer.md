# ⚙️ Container System

Container système d'Active Directory contenant les objets de configuration et les paramètres système.

## Contenu typique
- `Password Settings Container` : Objets PSO (Fine-Grained Password Policies)
- `Policies` : GPOs du domaine (stockage objet AD, hors SYSVOL)
- `WMI Filters` : Filtres WMI pour le ciblage des GPOs
- `AdminSDHolder` : Modèle de sécurité pour les objets protégés

## ⚠️ Sécurité
> 🔴 **Tier 0 — Ne pas modifier manuellement**

- **AdminSDHolder** : surveiller les modifications de ses ACLs — tout objet y ressemblant sera propagé aux groupes protégés (Domain Admins, etc.) toutes les 60 minutes par le processus SDProp
- Ne jamais supprimer ou déplacer les objets système de ce container