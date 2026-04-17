@echo off
set "nowPath=%~dp0"

::delete specify file(*.pdb,*.vshost.*)
for /r "%nowPath%" %%i in (*.pdb,*.vshost.*) do (del %%i)

::delete specify folder(obj,bin)
for /r "%nowPath%" %%i in (obj,bin) do (IF EXIST %%i RD /s /q %%i)

echo OK
pause