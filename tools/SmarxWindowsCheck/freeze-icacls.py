"""Fige la sortie d'icacls-display.ps1 dans SMAR-X.Tests/Fidelity/icacls-display.txt.

    python tools/SmarxWindowsCheck/freeze-icacls.py icacls-display.out > SMAR-X.Tests/Fidelity/icacls-display.txt

Garde chaque affichage d'une autorisation (16 383 masques). Pour les refus, vérifie d'abord la règle sur les 16 383
masques (« (DENY) » suivi de l'affichage de l'autorisation, sauf Contrôle total, affiché « (N) ») puis n'en garde qu'un
sur 64. Pour les drapeaux, écarte les ACE marquées ID, que .NET a écrites sans ce drapeau (icacls ne les montre donc
pas héritées), et l'ACE IO sans OI ni CI, qu'icacls n'affiche pas et que SMAR-X ne crée pas.
"""
import re
import sys

allow, deny, flags, header = {}, {}, [], []
for line in open(sys.argv[1], encoding='utf-8', errors='replace').read().splitlines():
    if line.startswith('#'):
        header.append(line)
    elif m := re.match(r'MASK (A|D) 0x([0-9A-F]+) (.*)$', line):
        (allow if m.group(1) == 'A' else deny)[int(m.group(2), 16)] = m.group(3).strip()
    elif m := re.match(r'FLAGS (A|D) \[([A-Z]*)\] 0x([0-9A-F]+) ?(.*)$', line):
        flags.append((m.group(1), m.group(2), int(m.group(3), 16), m.group(4).strip()))

assert len(allow) == len(deny) == 16383, (len(allow), len(deny))
for mask, shown in deny.items():
    expected = '(N)' if mask == 0x1F01FF else '(DENY)' + allow[mask]
    assert shown == expected, (hex(mask), shown, expected)

out = header + [
    '# A <masque> <affichage> : chaque combinaison des 14 droits spéciaux, autorisée',
    '# D <masque> <affichage> : refusée ; vérifié pour les 16 383 masques, un sur 64 gardé ici',
    '# FLAGS <A|D> <drapeaux SDDL> <masque> <affichage>',
]
out += [f'A {mask:X} {shown}' for mask, shown in sorted(allow.items())]
out += [f'D {mask:X} {shown}' for mask, shown in sorted(deny.items()) if mask % 64 == 1 or mask == 0x1F01FF]
for kind, set_, mask, shown in flags:
    if 'ID' in set_ or (('IO' in set_) and 'OI' not in set_ and 'CI' not in set_):
        continue
    out.append(f'FLAGS {kind} {set_ or "-"} {mask:X} {shown}')
sys.stdout.reconfigure(encoding='utf-8', newline=chr(10))
sys.stdout.write('\n'.join(out) + '\n')
