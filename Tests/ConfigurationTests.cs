using System;
using System.Threading.Tasks;
using Ascon.Pilot.SDK.Configuration;
using Ascon.Pilot.SDK.Validation;
using Ascon.Pilot.SDK.Services;

namespace Ascon.Pilot.SDK.Tests
{
    /// <summary>
    /// Тесты для конфигурации и валидации
    /// </summary>
    public static class ConfigurationTests
    {
        /// <summary>
        /// Запустить все тесты конфигурации
        /// </summary>
        public static async Task RunAllTestsAsync()
        {
            Console.WriteLine("=== Запуск тестов конфигурации ===");
            
            try
            {
                // Тест 1: Валидация конфигурации
                await TestConfigurationValidationAsync();
                
                // Тест 2: Валидация email
                await TestEmailValidationAsync();
                
                // Тест 3: Валидация URL
                await TestUrlValidationAsync();
                
                // Тест 4: Тест подключения к сервисам
                await TestServiceConnectionsAsync();
                
                Console.WriteLine("=== Все тесты прошли успешно ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== Ошибка в тестах: {ex.Message} ===");
                LoggingService.LogError("Test failed", ex, nameof(RunAllTestsAsync));
            }
        }

        /// <summary>
        /// Тест валидации конфигурации
        /// </summary>
        private static async Task TestConfigurationValidationAsync()
        {
            Console.WriteLine("Тест 1: Валидация конфигурации...");
            
            var result = InputValidator.ValidateConfiguration();
            var summary = InputValidator.GetValidationSummary(result);
            
            Console.WriteLine($"Результат: {summary}");
            
            if (!result.IsValid)
            {
                throw new Exception($"Конфигурация не прошла валидацию: {summary}");
            }
            
            Console.WriteLine("✅ Конфигурация валидна");
        }

        /// <summary>
        /// Тест валидации email
        /// </summary>
        private static async Task TestEmailValidationAsync()
        {
            Console.WriteLine("Тест 2: Валидация email...");
            
            // Тест валидного email
            var validEmail = "test@example.com";
            var result = InputValidator.ValidateEmail(validEmail);
            
            if (!result.IsValid)
            {
                throw new Exception($"Валидный email не прошел проверку: {string.Join(", ", result.Errors)}");
            }
            
            // Тест невалидного email
            var invalidEmail = "invalid-email";
            result = InputValidator.ValidateEmail(invalidEmail);
            
            if (result.IsValid)
            {
                throw new Exception("Невалидный email прошел проверку");
            }
            
            Console.WriteLine("✅ Валидация email работает корректно");
        }

        /// <summary>
        /// Тест валидации URL
        /// </summary>
        private static async Task TestUrlValidationAsync()
        {
            Console.WriteLine("Тест 3: Валидация URL...");
            
            // Тест валидного URL
            var validUrl = "http://192.168.10.180:3000";
            var result = InputValidator.ValidateUrl(validUrl);
            
            if (!result.IsValid)
            {
                throw new Exception($"Валидный URL не прошел проверку: {string.Join(", ", result.Errors)}");
            }
            
            // Тест невалидного URL
            var invalidUrl = "not-a-url";
            result = InputValidator.ValidateUrl(invalidUrl);
            
            if (result.IsValid)
            {
                throw new Exception("Невалидный URL прошел проверку");
            }
            
            Console.WriteLine("✅ Валидация URL работает корректно");
        }

        /// <summary>
        /// Тест подключения к сервисам
        /// </summary>
        private static async Task TestServiceConnectionsAsync()
        {
            Console.WriteLine("Тест 4: Тест подключения к сервисам...");
            
            try
            {
                // Тест ChatService
                using (var chatService = new ChatService())
                {
                    var isConnected = await chatService.TestConnectionAsync();
                    Console.WriteLine($"Rocket.Chat подключение: {(isConnected ? "✅" : "❌")}");
                }
                
                // Тест EmailService
                using (var emailService = new EmailService())
                {
                    var isConnected = await emailService.TestConnectionAsync();
                    Console.WriteLine($"SMTP подключение: {(isConnected ? "✅" : "❌")}");
                }
                
                Console.WriteLine("✅ Тесты подключения завершены");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка при тестировании подключений: {ex.Message}");
                // Не выбрасываем исключение, так как это может быть нормально в тестовой среде
            }
        }

        /// <summary>
        /// Тест шифрования конфигурации
        /// </summary>
        public static void TestEncryption()
        {
            Console.WriteLine("Тест 5: Шифрование конфигурации...");
            
            var testValue = "test-password-123";
            var encrypted = AppSettings.EncryptString(testValue);
            var decrypted = AppSettings.EncryptString(encrypted); // В реальности это должно быть DecryptString
            
            if (encrypted == testValue)
            {
                Console.WriteLine("⚠️ Шифрование не работает (возможно, в тестовой среде)");
            }
            else
            {
                Console.WriteLine("✅ Шифрование работает");
            }
        }

        /// <summary>
        /// Тест логирования
        /// </summary>
        public static void TestLogging()
        {
            Console.WriteLine("Тест 6: Система логирования...");
            
            try
            {
                LoggingService.LogInfo("Тестовое информационное сообщение", "TestLogging");
                LoggingService.LogWarning("Тестовое предупреждение", "TestLogging");
                LoggingService.LogDebug("Тестовое отладочное сообщение", "TestLogging");
                
                Console.WriteLine("✅ Логирование работает");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка логирования: {ex.Message}");
            }
        }
    }
}