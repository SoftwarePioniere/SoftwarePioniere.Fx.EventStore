$host.ui.RawUI.WindowTitle = 'build.. ' + (Get-Item -Path "." -Verbose).Name

dotnet cake --target=BuildTestPackLocalPush --configuration=Debug
#.\build.ps1 -Target BuildTestPackLocalPush -configuration Debug --verbosity=verbose

if ($LASTEXITCODE -ne 0) {
    # Write-Error "Build Error..."
    pause
}
