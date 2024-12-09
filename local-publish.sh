rm -r publish

cd FUNC
dotnet publish -o ../publish #-r linux-arm64

cd  ../webui
pnpm install
pnpm build

cd  ..
