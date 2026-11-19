#!/bin/sh
# Builds AutoDarkModeX.exe and the installer AutoDarkModeX-Setup-<version>.exe.
# Needs: Mono C# compiler (mcs) with Windows Forms, and NSIS (makensis).
# The version lives in the VERSION file (e.g. 1.1.7) - bump it before every release.
set -e
cd "$(dirname "$0")"
VER=$(tr -d ' \r\n' < VERSION)
# four-part form for Windows file properties, e.g. 1.1 -> 1.1.0.0
VER4=$(echo "$VER.0.0.0" | cut -d. -f1-4)
printf 'namespace AutoDarkMode { static class AppVersion { public const string Text = "%s"; public const string Full = "%s"; } }\n' "$VER" "$VER4" > src/Version.cs
mcs -target:winexe -out:AutoDarkModeX.exe -codepage:utf8 -win32icon:assets/app.ico -resource:assets/icon_256.png,icon.png -optimize+ \
  -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.dll -r:System.Web.Extensions.dll \
  src/Version.cs src/Sun.cs src/Core.cs src/Controls.cs src/Main.cs src/About.cs src/AssemblyInfo.cs
makensis -DAPPVER="$VER" -DAPPVER4="$VER4" installer.nsi
echo "Built AutoDarkModeX-Setup-$VER.exe"
