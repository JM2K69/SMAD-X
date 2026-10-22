# Membre non privilégié du groupe DnsAdmins

## Ce que l'audit a détecté
Un compte qui n'est pas administrateur du domaine est membre du groupe **DnsAdmins**.

## Pourquoi c'est dangereux
Les membres de DnsAdmins peuvent faire charger une DLL arbitraire par le service DNS, qui tourne en SYSTEM sur les contrôleurs de domaine. Être membre de DnsAdmins équivaut donc à pouvoir prendre le contrôle des DC.

## Comment corriger
Retirer le compte du groupe et traiter DnsAdmins comme un groupe de Tier 0.
