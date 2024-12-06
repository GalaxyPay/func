Remove-Item Output -Force -Recurse -ErrorAction Ignore
Remove-Item publish -Force -Recurse -ErrorAction Ignore

Set-Location FUNC
dotnet publish -o ..\publish

Set-Location ..\NodeService
dotnet publish -o ..\publish\Services

Set-Location ..\RetiService
dotnet publish -o ..\publish\Services

Set-Location ..\webui
pnpm install
pnpm build

Set-Location ..
iscc FUNC.iss