rm -r Output

PKG=Output/func_${1}_linux-$2

mkdir -p $PKG/lib/systemd/system
mkdir -p $PKG/opt/func
mkdir -p $PKG/usr/share/func

cp deb/func.service $PKG/lib/systemd/system/
cp -r publish/* $PKG/opt/func

mkdir $PKG/DEBIAN
cp deb/postinst $PKG/DEBIAN

echo "Package: func" > $PKG/DEBIAN/control
echo "Version: $1" >> $PKG/DEBIAN/control
echo "Section: base" >> $PKG/DEBIAN/control
echo "Priority: optional" >> $PKG/DEBIAN/control
echo "Architecture: $2" >> $PKG/DEBIAN/control
echo "Maintainer: Andy Funk <acfunk@gmail.com>" >> $PKG/DEBIAN/control
echo "Description: Algorand Node Manager" >> $PKG/DEBIAN/control

chmod 0755 $PKG/DEBIAN/postinst

dpkg-deb --build $PKG
