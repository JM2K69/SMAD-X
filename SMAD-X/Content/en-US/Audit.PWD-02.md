# Password never expires

## What the audit found
The "Password never expires" option is checked on an enabled account.

## Why it is dangerous
The password escapes the domain's password renewal policy. This is common on service accounts, which are often forgotten and whose passwords end up circulating in scripts or documentation.

## How to fix
- Uncheck the option and subject the account to the domain policy.
- For a service account: prefer a **gMSA**, whose password changes automatically.

## Going further
NIST (SP 800-63B) no longer recommends forced periodic password changes for users, provided that long, uncompromised passwords are required and a change is forced in the event of a leak. ANSSI keeps this checkpoint: compare the two approaches.
