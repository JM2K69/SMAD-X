# Tiering model element missing

## What the audit found
The domain follows a published tiering model (Microsoft Active Directory Tier Model or HardenAD), but one of its elements is missing: an OU, a group, an administration account, a GPO, a GPO link or a delegation that the model sets.

## Why it is dangerous
A tiering model works as a whole: the OUs place each object in its tier, the groups and delegations give each team only the rights of its tier, and the restriction GPOs stop an administration account from logging on to a less sensitive machine. An element removed or never deployed opens a gap that the rest of the model does not make up for: unlink a restriction GPO, and a Tier 0 administrator can once again leave their credentials on a workstation.

## How to fix
Restore the element as the model defines it, or replay the model's deployment (in SMAD-X: **Settings > Apply a tiering model...**, which only adds what is missing). If the deviation is deliberate, document it, and check in **Analysis > Attack paths** that it opens no path.

## Going further
Both models provide a tool to audit their own deployment (Audit-TierModel.ps1 for Microsoft, the HardenAD Documentator). Why must a security model be able to detect that it was changed after its deployment?
