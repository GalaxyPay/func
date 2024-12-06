rm -r publish

cd FUNC
dotnet publish -o ../publish

cd  ../webui
pnpm install
pnpm build

cd  ..
