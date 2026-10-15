# Rebuild and redeploy CardGames migrations, api, and web (existing volumes; no docker compose down -v).
# Usage: from repo root, after Docker Desktop is running: .\redeploy_code_to_docker.ps1

$ErrorActionPreference = "Stop"
$DeployRoot = Join-Path $PSScriptRoot "deploy"
Set-Location -LiteralPath $DeployRoot

function Assert-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "docker is not on PATH. Start Docker Desktop and wait until the engine is running."
    }

    & docker info --format "{{.ServerVersion}}" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker engine is not running. Start Docker Desktop and wait until it says Engine running."
    }
}

Assert-Docker

Write-Host "Rebuilding and recreating migrations, api, and web in $DeployRoot ..."
& docker compose up -d --build --force-recreate migrations api web
if ($LASTEXITCODE -ne 0) {
    throw "docker compose up failed with exit code $LASTEXITCODE."
}

& docker compose ps
