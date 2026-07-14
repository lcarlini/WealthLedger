@echo off
start "BACK" cmd /k "cd /d C:\LocalApps\WealthLedger && dotnet .\WealthLedger.WebApp.dll --urls http://localhost:5000"
start "FRONT" cmd /k "cd /d C:\git\WealthLedger\Source\Run\SPA && (npm run start)"