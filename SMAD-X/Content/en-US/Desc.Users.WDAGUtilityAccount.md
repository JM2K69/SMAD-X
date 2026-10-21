# 👤 WDAGUtilityAccount

System account for **Windows Defender Application Guard (WDAG)**.

## Role
- Used by the WDAG feature to isolate browsing sessions in a Hyper-V container
- Managed automatically by Windows — do not modify
- Disabled if WDAG is not in use

## ⚠️ Security
> 🟡 **System account — do not modify**

- Never enable or modify this account manually
- Its presence is normal in Windows 10/11 and Windows Server 2016+ domains