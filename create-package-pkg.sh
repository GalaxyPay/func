rm -r Output
mkdir -p Output

pkgbuild --root publish \
    --install-location /opt/func \
    --scripts pkg/scripts \
    --identifier func.app \
    Output/func_3.4.0_darwin-$1.pkg
