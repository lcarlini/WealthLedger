Set WshShell = CreateObject("WScript.Shell")
WshShell.CurrentDirectory = "C:\LocalApps\WealthLedger"
WshShell.Run "dotnet .\WealthLedger.WebApp.dll --urls http://localhost:5000", 1
Set WshShell = Nothing
