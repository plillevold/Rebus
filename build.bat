@echo off
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe scripts\build.proj /t:build;createNugetPackage /v:m /p:Version=%1