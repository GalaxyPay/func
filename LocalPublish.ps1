Remove-Item Output -Force -Recurse -ErrorAction Ignore
Remove-Item publish -Force -Recurse -ErrorAction Ignore

Set-Location AvmWinNode
dotnet publish -o ..\publish

Set-Location ..\NodeService
dotnet publish -o ..\publish\Services

Set-Location ..\webui
pnpm install
pnpm build

Set-Location ..
iscc AvmWinNode.iss