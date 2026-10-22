# Password not required

## What the audit found
The PASSWD_NOTREQD flag is set on an enabled account.

## Why it is dangerous
The account can be given an **empty password**, regardless of the domain password policy. An attacker who enumerates accounts can try to log on without a password.

## How to fix
Remove the flag (`Set-ADAccountControl -PasswordNotRequired $false`), then set a compliant password. The flag is not visible in the graphical console: only an audit reveals it.
