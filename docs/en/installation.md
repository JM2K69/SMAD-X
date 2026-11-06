# Installation

## Download

Each version of the SMA-X suite is published on the repository's [Releases](https://github.com/JM2K69/SMAD-X/releases/latest) page, with **one archive per application and per platform**:

| Platform | SMAD-X | SMAD-X Studio | SMAR-X |
|---|---|---|---|
| :fontawesome-brands-windows: Windows x64 | `SMAD-X-win-x64.zip` | `SMAD-X-Studio-win-x64.zip` | `SMAR-X-win-x64.zip` |
| :fontawesome-brands-linux: Linux x64 | `SMAD-X-linux-x64.zip` | `SMAD-X-Studio-linux-x64.zip` | `SMAR-X-linux-x64.zip` |
| :fontawesome-brands-apple: macOS Intel | `SMAD-X-osx-x64.zip` | `SMAD-X-Studio-osx-x64.zip` | `SMAR-X-osx-x64.zip` |
| :fontawesome-brands-apple: macOS Apple Silicon (M1 to M5) | `SMAD-X-osx-arm64.zip` | `SMAD-X-Studio-osx-arm64.zip` | `SMAR-X-osx-arm64.zip` |

- **SMAD-X**: the Active Directory simulator.
- **SMAD-X Studio**: [lab authoring](labs/studio.md), for trainers.
- **SMAR-X**: the [file access rights simulator](smar-x/index.md), Linux and Windows.

Download only the application you need: a student does not need Studio. Each archive holds **a single self-contained executable** and the licenses. The .NET runtime, graphics libraries and the application's content (sheets, labs and PDF report fonts for SMAD-X, sample servers for SMAR-X) are included. Nothing to install.

1. Unzip the archive anywhere.
2. Run the executable: `SMAD-X.exe`, `SMAD-X.Studio.exe` or `SMAR-X.exe` on Windows; `SMAD-X`, `SMAD-X.Studio` or `SMAR-X` on Linux and macOS.

=== "macOS"

    The downloaded file is quarantined by Gatekeeper. In a terminal, in the unzipped folder (here for SMAD-X; use `SMAD-X.Studio` or `SMAR-X` depending on the archive):

    ```bash
    chmod +x SMAD-X && xattr -d com.apple.quarantine SMAD-X
    ```

=== "Linux"

    Make the file executable (here for SMAD-X; use `SMAD-X.Studio` or `SMAR-X` depending on the archive):

    ```bash
    chmod +x SMAD-X
    ```

=== "Windows"

    SmartScreen may warn about an unsigned executable: **More info > Run anyway**.

On first launch, SMAD-X opens the `contoso.com` sample domain, with three AD sites; SMAR-X opens its sample servers, Linux (`srv-fic`) and Windows (`SRV-FIC`).

## Version and updates

The version is shown in **Help > About**. It is defined in a single place in the source code (`<Version>` in `Directory.Build.props`, shared by SMAD-X, SMAD-X Studio and SMAR-X), and each release matches a `vX.Y.Z` tag.

## Build from source

Prerequisite: the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/JM2K69/SMAD-X.git
cd SMAD-X
dotnet run --project SMAD-X/SMAD-X.csproj
```

To produce the self-contained executable, like the release:

```bash
dotnet publish SMAD-X/SMAD-X.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Same command with `SMAD-X.Studio/SMAD-X.Studio.csproj` for Studio and `SMAR-X/SMAR-X.csproj` for SMAR-X. Replace `win-x64` with `linux-x64`, `osx-x64` or `osx-arm64` as needed.

Before publishing a release, the CI runs each published executable, without a window: `SMAD-X --self-test` checks that the labs, sheets and fonts can be read inside the exe; `SMAR-X --self-test` builds the sample servers and checks the verdict of its four engines (Linux kernel, Samba, NTFS, SMB); Studio validates a lab. An exit code other than 0 fails the release.

### Tests

```bash
dotnet test SMAD-X.slnx
```

The PowerShell import script tests really run its functions: **PowerShell 7** (`pwsh`) must be installed, plus **Windows PowerShell 5.1** on Windows. A test fails if they are missing, rather than passing without checking anything.

## Error log

An unexpected error is shown in a window and written to the log folder:

| System | Folder |
|---|---|
| Windows | `%LOCALAPPDATA%\SMAD-X\logs` |
| Linux | `~/.local/share/SMAD-X/logs` |
| macOS | `~/Library/Application Support/SMAD-X/logs` |
