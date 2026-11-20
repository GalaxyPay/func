# The func.tray LaunchAgent points at /opt/func/FUNC.Tray; a package without it leaves
# launchd failing with EX_CONFIG (78) at every login.
if [ ! -x publish/FUNC.Tray ]; then
    echo "error: publish/FUNC.Tray is missing; publish FUNC.Tray first (cd FUNC.Tray && dotnet publish -o ../publish -r osx-<arch>)" >&2
    exit 1
fi

rm -rf Output
mkdir -p Output

# Bundle the uninstall script into the app (macOS has no native uninstaller).
cp pkg/uninstall.sh publish/uninstall.sh
chmod 0755 publish/uninstall.sh

pkgbuild --root publish \
    --install-location /opt/func \
    --scripts pkg/scripts \
    --identifier func.app \
    Output/func_${1}_darwin-$2.pkg
