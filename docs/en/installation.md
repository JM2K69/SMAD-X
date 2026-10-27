# Installation

## Download

Each version is published on the repository's [Releases](https://github.com/JM2K69/SMAD-X/releases/latest) page, with one file per platform:

| Platform | File |
|---|---|
| :fontawesome-brands-windows: Windows x64 | `SMAD-X-win-x64.zip` |
| :fontawesome-brands-linux: Linux x64 | `SMAD-X-linux-x64.zip` |
| :fontawesome-brands-apple: macOS Intel | `SMAD-X-osx-x64.zip` |
| :fontawesome-brands-apple: macOS Apple Silicon (M1 to M5) | `SMAD-X-osx-arm64.zip` |

The archive holds **two self-contained executables**: **SMAD-X**, the simulator, and **SMAD-X Studio**, [lab authoring](labs/studio.md) for trainers. The .NET runtime, graphics libraries, teaching sheets, labs and the PDF report fonts are included. Nothing to install.

1. Unzip the archive anywhere.
2. Run `SMAD-X.exe` (Windows) or `SMAD-X` (Linux, macOS); `SMAD-X.Studio` the same way.

=== "macOS"

    The downloaded file is quarantined by Gatekeeper. In a terminal, in the unzipped folder:

    ```bash
    chmod +x SMAD-X SMAD-X.Studio && xattr -d com.apple.quarantine SMAD-X SMAD-X.Studio
    ```

=== "Linux"

    Make the file executable:

    ```bash
    chmod +x SMAD-X SMAD-X.Studio
    ```

=== "Windows"

    SmartScreen may warn about an unsigned executable: **More info > Run anyway**.

On first launch, SMAD-X opens the `contoso.com` sample domain, with three AD sites.

## Version and updates

The version is shown in **Help > About**. It is defined in a single place in the source code (`<Version>` in `Directory.Build.props`, shared by SMAD-X and SMAD-X Studio), and each release matches a `vX.Y.Z` tag.

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

Same command with `SMAD-X.Studio/SMAD-X.Studio.csproj` for Studio. Replace `win-x64` with `linux-x64`, `osx-x64` or `osx-arm64` as needed.

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
