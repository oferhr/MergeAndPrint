---
name: out
description: Build MergeAndPrint in Release (win-x64) and zip it into out/MergeAndPrint-<version>.zip, ready to send to the client. Use when asked to "build a release", "make the zip", "package for the client" or run /out.
---

# /out — build and zip a MergeAndPrint release

Builds the app in Release for Windows x64 and zips it into the `out` folder at the repo root,
named after the version in `Properties/AssemblyInfo.cs` (`AssemblyFileVersion`).

## Steps

Run from the repo root (`C:\Projects\Ofer\MergeAndPrint`).

1. **Check before building**
   - Read the version: `grep AssemblyFileVersion Properties/AssemblyInfo.cs`.
   - Confirm `licenses.json` exists in the repo root. Without it the zip ships with no license keys
     and the client's prints and merged PDFs get evaluation watermarks (IronPdf and Spire).
     If it is missing, stop and tell the user.
   - Note the current branch and whether the tree is clean (`git status --short`), and mention it
     in the report: a zip built from uncommitted changes doesn't match any commit.
   - If `out/MergeAndPrint-<version>.zip` already exists, say so; it will be replaced.

2. **Publish** to a fresh folder in the session scratchpad (never inside the repo):

   ```bash
   dotnet publish -c Release -r win-x64 --self-contained false -o "<scratchpad>/publish"
   ```

   Delete that folder first if it exists. Use `win-x64` only: a plain Release build carries
   Linux/macOS/Android native files and is ~600 MB instead of ~345 MB. If the user says the
   client machine is 32-bit, use `-r win-x86` instead.

3. **Zip** with PowerShell into `out/` (create `out` if missing):

   ```powershell
   Compress-Archive -Path "<scratchpad>\publish\*" -DestinationPath "C:\Projects\Ofer\MergeAndPrint\out\MergeAndPrint-<version>.zip" -CompressionLevel Optimal
   ```

4. **Verify** by opening the zip (`System.IO.Compression.ZipFile.OpenRead`):
   - `MergeAndPrint.exe` is present and its `FileVersion` equals the version from step 1.
   - `licenses.json`, `Spire.Pdf.dll` and `IronPdf.dll` are present.
   - `config.json` is **not** present: unzipping over an install must keep the client's settings.
     If it is there, rebuild without it.
   - Report size and file count.
   - Run `git status --short` and confirm the zip does not appear (`out/` is in `.gitignore`).

## Report to the user

- Full path of the zip, its size, and the version.
- The branch/commit it was built from and whether there were uncommitted changes.
- A reminder that the zip contains the license keys (`licenses.json`): send it privately,
  never commit it or upload it to a public link.
- `config.json` is not included, so the client's settings survive the update.
