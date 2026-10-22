# Installation

## Télécharger

Chaque version est publiée sur la page [Releases](https://github.com/JM2K69/SMAD-X/releases/latest) du dépôt, avec un fichier par plateforme :

| Plateforme | Fichier |
|---|---|
| :fontawesome-brands-windows: Windows x64 | `SMAD-X-win-x64.zip` |
| :fontawesome-brands-linux: Linux x64 | `SMAD-X-linux-x64.zip` |
| :fontawesome-brands-apple: macOS Intel | `SMAD-X-osx-x64.zip` |
| :fontawesome-brands-apple: macOS Apple Silicon (M1 à M5) | `SMAD-X-osx-arm64.zip` |

L'archive contient **un seul exécutable**, autonome : le runtime .NET, les bibliothèques graphiques, les fiches pédagogiques, les exercices et les polices du rapport PDF sont inclus. Il n'y a rien à installer.

1. Décompressez l'archive où vous voulez.
2. Lancez `SMAD-X.exe` (Windows) ou `SMAD-X` (Linux, macOS).

=== "macOS"

    Le fichier téléchargé est mis en quarantaine par Gatekeeper. Dans un terminal, dans le dossier décompressé :

    ```bash
    chmod +x SMAD-X && xattr -d com.apple.quarantine SMAD-X
    ```

=== "Linux"

    Rendez le fichier exécutable :

    ```bash
    chmod +x SMAD-X
    ```

=== "Windows"

    SmartScreen peut afficher un avertissement pour un exécutable non signé : **Informations complémentaires > Exécuter quand même**.

Au premier lancement, SMAD-X ouvre le domaine d'exemple `contoso.com`, avec trois sites AD.

## Version et mises à jour

La version figure dans **Aide > À propos**. Elle est définie à un seul endroit du code source (`<Version>` dans `SMAD-X/SMAD-X.csproj`), et chaque release correspond à un tag `vX.Y.Z`.

## Construire depuis les sources

Prérequis : le [SDK .NET 10](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/JM2K69/SMAD-X.git
cd SMAD-X
dotnet run --project SMAD-X/SMAD-X.csproj
```

Pour produire l'exécutable autonome, comme la release :

```bash
dotnet publish SMAD-X/SMAD-X.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Remplacez `win-x64` par `linux-x64`, `osx-x64` ou `osx-arm64` selon la plateforme.

### Tests

```bash
dotnet test SMAD-X.slnx
```

Les tests de l'export PowerShell exécutent réellement les scripts produits : **PowerShell 7** (`pwsh`) doit être installé, ainsi que **Windows PowerShell 5.1** sous Windows. Un test échoue s'il ne les trouve pas, plutôt que de réussir sans rien vérifier.

## Journal des erreurs

Une erreur inattendue s'affiche dans une fenêtre et est écrite dans le dossier des journaux :

| Système | Dossier |
|---|---|
| Windows | `%LOCALAPPDATA%\SMAD-X\logs` |
| Linux | `~/.local/share/SMAD-X/logs` |
| macOS | `~/Library/Application Support/SMAD-X/logs` |
