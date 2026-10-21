# 👥 Domain Admins Group (Admin OU)

Local instance of the **Domain Admins** group in the Admin OU.

## Role
- Groups named administrator accounts holding Domain Admins rights
- Used to delegate domain administration to identified individuals

## ⚠️ Security
> 🔴 **Tier 0 — Ultra-privileged group**

- Keep membership at the **absolute minimum** (ideally 2–3 break-glass accounts)
- Audit members monthly
- Any addition must go through a change management process
- Monitor Event ID 4728 (member added) and 4729 (member removed)