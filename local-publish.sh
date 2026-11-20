rm -r publish

cd FUNC
dotnet publish -o ../publish #-r linux-arm64

# macOS menu-bar helper (the .pkg requires it; Windows publishes it via LocalPublish.ps1).
if [ "$(uname)" = "Darwin" ]; then
  cd ../FUNC.Tray
  dotnet publish -o ../publish
fi

cd  ../webui
pnpm install
pnpm build

cd  ..
