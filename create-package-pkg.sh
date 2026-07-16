rm -r Output
mkdir -p Output

# Bundle the uninstall script into the app (macOS has no native uninstaller).
cp pkg/uninstall.sh publish/uninstall.sh
chmod 0755 publish/uninstall.sh

pkgbuild --root publish \
    --install-location /opt/func \
    --scripts pkg/scripts \
    --identifier func.app \
    Output/func_${1}_darwin-$2.pkg
