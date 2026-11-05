# Installation

## Télécharger

Chaque version est publiée sur la page [Releases](https://github.com/JM2K69/SMAD-X/releases/latest) du dépôt, avec **une archive par application et par plateforme** :

| Plateforme | SMAD-X | SMAD-X Studio | SMAR-X |
|---|---|---|---|
| :fontawesome-brands-windows: Windows x64 | `SMAD-X-win-x64.zip` | `SMAD-X-Studio-win-x64.zip` | `SMAR-X-win-x64.zip` |
| :fontawesome-brands-linux: Linux x64 | `SMAD-X-linux-x64.zip` | `SMAD-X-Studio-linux-x64.zip` | `SMAR-X-linux-x64.zip` |
| :fontawesome-brands-apple: macOS Intel | `SMAD-X-osx-x64.zip` | `SMAD-X-Studio-osx-x64.zip` | `SMAR-X-osx-x64.zip` |
| :fontawesome-brands-apple: macOS Apple Silicon (M1 à M5) | `SMAD-X-osx-arm64.zip` | `SMAD-X-Studio-osx-arm64.zip` | `SMAR-X-osx-arm64.zip` |

- **SMAD-X** : le simulateur Active Directory.
- **SMAD-X Studio** : la [création d'exercices](exercices/studio.md), pour les formateurs.
- **SMAR-X** : le [simulateur des droits d'accès aux fichiers](smar-x/index.md), Linux et Windows.

On ne télécharge que l'application dont on a besoin : un élève n'a pas besoin de Studio. Chaque archive contient **un seul exécutable** autonome et les licences. Le runtime .NET, les bibliothèques graphiques et le contenu de l'application (fiches, exercices et polices du rapport PDF pour SMAD-X, serveurs d'exemple pour SMAR-X) sont inclus. Il n'y a rien à installer.

1. Décompressez l'archive où vous voulez.
2. Lancez l'exécutable : `SMAD-X.exe`, `SMAD-X.Studio.exe` ou `SMAR-X.exe` sous Windows ; `SMAD-X`, `SMAD-X.Studio` ou `SMAR-X` sous Linux et macOS.

=== "macOS"

    Le fichier téléchargé est mis en quarantaine par Gatekeeper. Dans un terminal, dans le dossier décompressé (ici pour SMAD-X ; remplacez par `SMAD-X.Studio` ou `SMAR-X` selon l'archive) :

    ```bash
    chmod +x SMAD-X && xattr -d com.apple.quarantine SMAD-X
    ```

=== "Linux"

    Rendez le fichier exécutable (ici pour SMAD-X ; remplacez par `SMAD-X.Studio` ou `SMAR-X` selon l'archive) :

    ```bash
    chmod +x SMAD-X
    ```

=== "Windows"

    SmartScreen peut afficher un avertissement pour un exécutable non signé : **Informations complémentaires > Exécuter quand même**.

Au premier lancement, SMAD-X ouvre le domaine d'exemple `contoso.com`, avec trois sites AD ; SMAR-X ouvre ses serveurs d'exemple, Linux (`srv-fic`) et Windows (`SRV-FIC`).

## Version et mises à jour

La version figure dans **Aide > À propos**. Elle est définie à un seul endroit du code source (`<Version>` dans `Directory.Build.props`, commun à SMAD-X, SMAD-X Studio et SMAR-X), et chaque release correspond à un tag `vX.Y.Z`.

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

Même commande avec `SMAD-X.Studio/SMAD-X.Studio.csproj` pour Studio et `SMAR-X/SMAR-X.csproj` pour SMAR-X. Remplacez `win-x64` par `linux-x64`, `osx-x64` ou `osx-arm64` selon la plateforme.

Avant de publier une release, la CI lance chaque exécutable publié, sans fenêtre : `SMAD-X --self-test` vérifie que les exercices, les fiches et les polices se lisent dans l'exe ; `SMAR-X --self-test` construit les serveurs d'exemple et vérifie le verdict de ses quatre moteurs (noyau Linux, Samba, NTFS, SMB) ; Studio valide un exercice. Un code de sortie autre que 0 fait échouer la release.

### Tests

```bash
dotnet test SMAD-X.slnx
```

Les tests du script d'import PowerShell exécutent réellement ses fonctions : **PowerShell 7** (`pwsh`) doit être installé, ainsi que **Windows PowerShell 5.1** sous Windows. Un test échoue s'il ne les trouve pas, plutôt que de réussir sans rien vérifier.

## Journal des erreurs

Une erreur inattendue s'affiche dans une fenêtre et est écrite dans le dossier des journaux :

| Système | Dossier |
|---|---|
| Windows | `%LOCALAPPDATA%\SMAD-X\logs` |
| Linux | `~/.local/share/SMAD-X/logs` |
| macOS | `~/Library/Application Support/SMAD-X/logs` |
