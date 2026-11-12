Remove-Item Output -Force -Recurse -ErrorAction Ignore
Remove-Item publish -Force -Recurse -ErrorAction Ignore

Set-Location FUNC
dotnet publish -o ..\publish

Set-Location ..\NodeService
dotnet publish -o ..\publish\Services

Set-Location ..\RetiService
dotnet publish -o ..\publish\Services

Set-Location ..\ValarService
dotnet publish -o ..\publish\Services

Set-Location ..\webui
pnpm install
pnpm build

Set-Location ..
$version = (Get-Content webui\package.json -Raw | ConvertFrom-Json).version
iscc FUNC.iss /DMyAppVersion=$version