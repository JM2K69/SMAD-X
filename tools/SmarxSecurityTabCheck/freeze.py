# Convertit la sortie de security-tab.ps1 puis de edit-window.ps1, mises bout à bout, en fichier de fidélité de SMAR-X.
#   python -I freeze.py tous-les-releves.txt ../../SMAR-X.Tests/Fidelity/windows-security-tab.txt
# Les lignes EDIT (edit-window.ps1) donnent la DACL en SDDL : elles deviennent type,compte,portée,masque.
import re, sys
src, dst = sys.argv[1], sys.argv[2]
# Chaque relevé commence par un BOM (Add-Content -Encoding UTF8) : mis bout à bout, il y en a au milieu
lines = open(src, encoding='utf-8').read().replace('﻿', '').splitlines()
out = ['# Windows 11 Enterprise 10.0.26300 (en-US) : Explorateur > Propriétés > Sécurité, puis Avancé, lus par UI Automation et MSAA',
       '# (tools/SmarxSecurityTabCheck). C:\\SmarxUi\\F1 devient D:\\F1 ; les comptes perdent leur « (CLI\\…) ».',
       "# TAB|compte|colonne:ligne=Explicit (coche noire) ou Inherited (coche grise) ; ADV|compte|type|accès|hérité de|s'applique à"]


def path(p):
    return p.replace('\u202a', '').replace('C:\\SmarxUi\\', 'D:\\')


def who(n):
    return re.sub(r' \(CLI\\[^)]*\)$', '', n)


RIGHTS = {'CC': 0x1, 'DC': 0x2, 'LC': 0x4, 'SW': 0x8, 'RP': 0x10, 'WP': 0x20, 'DT': 0x40, 'LO': 0x80, 'CR': 0x100,
          'SD': 0x10000, 'RC': 0x20000, 'WD': 0x40000, 'WO': 0x80000, 'FA': 0x1F01FF, 'FR': 0x120089, 'FW': 0x120116, 'FX': 0x1200A0}
SIDS = {'BU': 'Users', 'SY': 'SYSTEM', 'BA': 'Administrators'}


def aces(sddl):
    out = []
    for type_, flags, rights, sid in re.findall(r'\((A|D);([A-Z]*);([^;]*);;;([A-Z]+)\)', sddl):
        mask = int(rights, 16) if rights.startswith('0x') else sum(RIGHTS[rights[i:i + 2]] for i in range(0, len(rights), 2))
        scope = ('OI' if 'OI' in flags else '') + ('CI' if 'CI' in flags else '')
        out.append(f"{'Allow' if type_ == 'A' else 'Deny'},{SIDS[sid]},{scope},{mask:08X}")
    return ';'.join(out)


edits = []
for l in lines:
    if l.startswith('OBJ|'):
        out.append('OBJ|' + path(l[4:]))
    elif l.startswith('ROWS|'):
        out.append('ROWS|' + l[5:].replace('  ', ' & '))
    elif l.startswith('TAB|'):
        _, name, marks = l.split('|', 2)
        items = []
        for m in [x.strip() for x in marks.split(';') if x.strip()]:
            k, v = m.split('=')
            col, row = k.split(' ', 1)
            items.append(f"{col}:{row}={'Explicit' if v.startswith('black') else 'Inherited'}")
        out.append(f"TAB|{who(name)}|{';'.join(items)}")
    elif l.startswith('ADV|'):
        f = l.split('|')
        if f[2] == '?':
            continue
        d = dict(kv.split(': ', 1) for kv in f[5].split('; '))
        out.append('|'.join(['ADV', who(f[4]), d['Type'], d['Access'], path(d['Inherited from']), d.get('Applies to', '')]))
    elif l.startswith('CASE|'):
        _, case, clicks, sddl = l.split('|', 3)
        boxes = ','.join(b.replace(' ', ':', 1) for b in clicks.split(','))
        edits.append(f"EDIT|{case}|{boxes}|{aces(sddl)}")
if edits:
    out += ['# Fenêtre « Permissions for » : un dossier protégé où Users a Modification explicite (puis SYSTEM et Administrators,',
            "# Contrôle total) ; on clique des cases, OK (et Oui à l'avertissement d'un refus), puis on relit la DACL.",
            '# EDIT|cas|cases cliquées|DACL écrite : type,compte,portée,masque'] + edits
open(dst, 'w', encoding='utf-8', newline='\n').write('\n'.join(out) + '\n')
