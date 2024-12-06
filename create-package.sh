rm -r Output

PKG=Output/func_2.0.0_amd64

mkdir -p $PKG/lib/systemd/system
mkdir -p $PKG/opt/func
mkdir -p $PKG/usr/share/func

cp deb/func.service $PKG/lib/systemd/system/
cp -r publish/* $PKG/opt/func

mkdir $PKG/DEBIAN
cp deb/control deb/postinst $PKG/DEBIAN

chmod 0755 $PKG/DEBIAN/postinst

dpkg-deb --build $PKG
