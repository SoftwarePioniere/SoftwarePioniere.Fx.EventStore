$host.ui.RawUI.WindowTitle = 'build.. ' + (Get-Item -Path "." -Verbose).Name

.\build.ps1 -Target BuildTestPackLocalPush -configuration Debug

if ($LASTEXITCODE -ne 0) {
    # Write-Error "Build Error..."
    pause
}
