# Policies Container (SYSVOL)

This container represents the `SYSVOL\Policies` folder on domain controllers.

Each GPO has a subfolder identified by its **GUID** under:
```
\\<domain>\SYSVOL\<domain>\Policies\{GUID}
```