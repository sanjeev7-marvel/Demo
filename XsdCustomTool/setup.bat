@echo off
REM Tries to run both possible locations. Even if one fails the script will still get the job done.

call "C:\Program Files (x86)\Microsoft Visual Studio 8\SDK\v2.0\Bin\sdkvars.bat"
call "C:\Program Files\Microsoft Visual Studio 8\SDK\v2.0\Bin\sdkvars.bat"

IF "%1"=="/u" (
	regasm /unregister XsdClassGenerator.dll
) ELSE (
	regasm /codebase XsdClassGenerator.dll
)
pause
