@echo off
echo ========================================
echo TOMS.moduls - Сборка и тестирование
echo ========================================

echo.
echo [1/4] Очистка предыдущей сборки...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"

echo.
echo [2/4] Восстановление пакетов NuGet...
nuget restore Ascon.Pilot.SDK.NotificationsSample.ext2.sln

echo.
echo [3/4] Сборка проекта...
msbuild Ascon.Pilot.SDK.NotificationsSample.ext2.sln /p:Configuration=Release /p:Platform="Any CPU" /verbosity:minimal

if %ERRORLEVEL% neq 0 (
    echo ❌ Ошибка сборки!
    pause
    exit /b 1
)

echo.
echo [4/4] Запуск тестов...
echo.

REM Создаем временный файл для запуска тестов
echo using System; > temp_test.cs
echo using System.Threading.Tasks; >> temp_test.cs
echo using Ascon.Pilot.SDK.Tests; >> temp_test.cs
echo class TestRunner { >> temp_test.cs
echo     static async Task Main() { >> temp_test.cs
echo         await ConfigurationTests.RunAllTestsAsync(); >> temp_test.cs
echo         ConfigurationTests.TestEncryption(); >> temp_test.cs
echo         ConfigurationTests.TestLogging(); >> temp_test.cs
echo         Console.WriteLine("Нажмите любую клавишу для выхода..."); >> temp_test.cs
echo         Console.ReadKey(); >> temp_test.cs
echo     } >> temp_test.cs
echo } >> temp_test.cs

REM Компилируем и запускаем тесты
csc.exe /reference:bin\Release\Ascon.Pilot.SDK.NotificationsSample.ext2.dll /reference:bin\Release\Newtonsoft.Json.dll /reference:System.dll /reference:System.Core.dll temp_test.cs /out:test_runner.exe

if exist "test_runner.exe" (
    test_runner.exe
    del test_runner.exe
)

del temp_test.cs

echo.
echo ========================================
echo ✅ Сборка завершена успешно!
echo ========================================
echo.
echo Файлы сборки:
echo - bin\Release\Ascon.Pilot.SDK.NotificationsSample.ext2.dll
echo - bin\Release\Ascon.Pilot.SDK.NotificationsSample.ext2.pdb
echo.
echo Логи:
echo - logs\pilot-module.log
echo.
pause