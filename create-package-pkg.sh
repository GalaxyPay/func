rm -r Output
mkdir -p Output

pkgbuild --root publish \
    --install-location /opt/func \
    --scripts pkg/scripts \
    --identifier func.app \
    Output/func_2.2.3_darwin-$1.pkg
