CFS Network Launcher v1.4.0
============================

GitHub automatikus frissítés:
Repository:
https://github.com/energie1024/bebep

Latest Release API:
https://api.github.com/repos/energie1024/bebep/releases/latest

A launcher a GitHub Release-ből a következő assetet keresi:
CFSNetworkLauncher.exe

Verziózás:
- v1.4.0 = első auto-update képes launcher
- v1.5.0, v1.6.0, stb. = új kiadások

A GitHub Release tag legyen például:
v1.4.0

Az asset pontos neve:
CFSNetworkLauncher.exe

Fontos:
A forráskódban a LauncherUpdater.cs tartalmazza az automatikus frissítést.
A FRISSÍTÉS gomb ellenőrzi a legújabb GitHub Release-t.
Ha új verzió van, letölti az EXE-t, bezárja a régi launchert, lecseréli és újraindítja.

Build:
A build.bat futtatásával készül a self-contained win-x64 EXE.
