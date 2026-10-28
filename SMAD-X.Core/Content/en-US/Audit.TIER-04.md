# Privileged account logged on to a less sensitive machine

## What the audit found
A privileged account (or one from a more sensitive tier) has a session open on a workstation or server from a less sensitive tier.

## Why it is dangerous
A session leaves the account's credentials in the machine's memory (LSASS): any local administrator of that machine can retrieve them there, then replay them. The path is direct, from a workstation account up to the domain: **Analysis > Attack paths** shows it (local administrator, then session). ANSSI's tiering forbids a Tier 0 administrator from logging on anywhere but Tier 0.

## How to fix
Close the session, and keep it from opening again: a dedicated administration account per tier, denying logon to Tier 0 accounts on the other tiers ("Deny log on" rights through a GPO), and an authentication silo that restricts these accounts to Tier 0 machines. The Protected Users group also limits what stays in memory.
