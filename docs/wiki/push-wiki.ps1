# Mundofy MailMigrator - Wiki Publishing Script
param(
    [string]$WikiRepoUrl = "https://github.com/MundofyOfficial/Mundofy-MailMigrator.wiki.git"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$tempDir = Join-Path $env:TEMP ("wiki_" + [Guid]::NewGuid().ToString().Substring(0,8))

Write-Host "==> Checking GitHub Wiki repository..." -ForegroundColor Cyan
git clone $WikiRepoUrl $tempDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n[!] Notice: GitHub requires initializing the wiki once before Git push is enabled." -ForegroundColor Yellow
    Write-Host "Please open: https://github.com/MundofyOfficial/Mundofy-MailMigrator/wiki" -ForegroundColor White
    Write-Host "Click 'Create the first page' and click 'Save Page'." -ForegroundColor White
    Write-Host "Then run this script again: .\docs\wiki\push-wiki.ps1`n" -ForegroundColor Green
    exit 1
}

Write-Host "==> Copying wiki documentation pages..." -ForegroundColor Cyan
Copy-Item "$scriptDir\*.md" -Destination $tempDir -Force

Set-Location $tempDir
git add -A
git -c user.name="MundofyOfficial" -c user.email="info@mundofy.com" commit -m "Publish complete Mundofy MailMigrator wiki documentation"
git push origin master

Set-Location $scriptDir
Remove-Item -Recurse -Force $tempDir
Write-Host "`n[SUCCESS] Mundofy MailMigrator Wiki published successfully to GitHub!" -ForegroundColor Green
