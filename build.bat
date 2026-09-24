@echo off
setlocal EnableDelayedExpansion

set "PLUGIN_DIR=E:\Games for Steam\steamapps\common\Nine Sols\BepInEx\plugins\GlassSol"
set "PROJECT=%~dp0NineSolsNohit.csproj"
set "OUT=%~dp0bin\Release\GlassSol.dll"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

if not exist "!VSWHERE!" goto MissingVswhere

set "MSBUILD="
for /f "usebackq tokens=*" %%i in (`"!VSWHERE!" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%i"

if not defined MSBUILD goto MissingMsbuild

echo Building Release...
"!MSBUILD!" "!PROJECT!" /nologo /v:m /p:Configuration=Release
if errorlevel 1 goto BuildFailed

if not exist "!OUT!" goto MissingOutput

if not exist "!PLUGIN_DIR!" mkdir "!PLUGIN_DIR!"

copy /Y "!OUT!" "!PLUGIN_DIR!\GlassSol.dll" >nul
if errorlevel 1 goto CopyFailed

echo Copied to !PLUGIN_DIR!\GlassSol.dll
exit /b 0

:MissingVswhere
echo vswhere not found: !VSWHERE!
exit /b 1

:MissingMsbuild
echo MSBuild not found.
exit /b 1

:BuildFailed
echo Build failed.
exit /b 1

:MissingOutput
echo Output not found: !OUT!
exit /b 1

:CopyFailed
echo Copy failed.
exit /b 1
