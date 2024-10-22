@echo off
setlocal
if [%1]==[] goto default
set CONFIG=%1
goto dobuild
:default
set CONfIG=Debug
:dobuild
msbuild codegen.sln -t:Build -p:Configuration=%CONFIG%
