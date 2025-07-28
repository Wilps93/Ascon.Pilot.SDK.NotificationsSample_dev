using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Ascon.Pilot.SDK.Services;

namespace Ascon.Pilot.SDK.Validation
{
    /// <summary>
    /// Валидатор входных данных
    /// </summary>
    public static class InputValidator
    {
        private static readonly Regex EmailRegex = new Regex(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UrlRegex = new Regex(
            @"^https?://[^\s/$.?#].[^\s]*$", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex GuidRegex = new Regex(
            @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", 
            RegexOptions.Compiled);

        /// <summary>
        /// Результат валидации
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            public List<string> Warnings { get; set; } = new List<string>();

            public void AddError(string error)
            {
                Errors.Add(error);
                IsValid = false;
            }

            public void AddWarning(string warning)
            {
                Warnings.Add(warning);
            }
        }

        /// <summary>
        /// Валидировать email адрес
        /// </summary>
        public static ValidationResult ValidateEmail(string email)
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(email))
            {
                result.AddError("Email адрес не может быть пустым");
                return result;
            }

            if (email.Length > 254)
            {
                result.AddError("Email адрес слишком длинный (максимум 254 символа)");
            }

            if (!EmailRegex.IsMatch(email))
            {
                result.AddError("Неверный формат email адреса");
            }

            // Проверить на подозрительные символы
            if (email.Contains("..") || email.Contains("__") || email.Contains("--"))
            {
                result.AddWarning("Email адрес содержит подозрительные символы");
            }

            return result;
        }

        /// <summary>
        /// Валидировать URL
        /// </summary>
        public static ValidationResult ValidateUrl(string url)
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(url))
            {
                result.AddError("URL не может быть пустым");
                return result;
            }

            if (url.Length > 2048)
            {
                result.AddError("URL слишком длинный (максимум 2048 символов)");
            }

            if (!UrlRegex.IsMatch(url))
            {
                result.AddError("Неверный формат URL");
            }

            return result;
        }

        /// <summary>
        /// Валидировать GUID
        /// </summary>
        public static ValidationResult ValidateGuid(string guid)
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(guid))
            {
                result.AddError("GUID не может быть пустым");
                return result;
            }

            if (!GuidRegex.IsMatch(guid))
            {
                result.AddError("Неверный формат GUID");
            }

            return result;
        }

        /// <summary>
        /// Валидировать имя пользователя
        /// </summary>
        public static ValidationResult ValidateUsername(string username)
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(username))
            {
                result.AddError("Имя пользователя не может быть пустым");
                return result;
            }

            if (username.Length < 3)
            {
                result.AddError("Имя пользователя слишком короткое (минимум 3 символа)");
            }

            if (username.Length > 50)
            {
                result.AddError("Имя пользователя слишком длинное (максимум 50 символов)");
            }

            // Проверить на допустимые символы
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9._-]+$"))
            {
                result.AddError("Имя пользователя содержит недопустимые символы");
            }

            return result;
        }

        /// <summary>
        /// Валидировать сообщение чата
        /// </summary>
        public static ValidationResult ValidateChatMessage(string message)
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(message))
            {
                result.AddError("Сообщение не может быть пустым");
                return result;
            }

            if (message.Length > 2000)
            {
                result.AddError("Сообщение слишком длинное (максимум 2000 символов)");
            }

            // Проверить на потенциально опасные символы
            if (message.Contains("<script>") || message.Contains("javascript:"))
            {
                result.AddError("Сообщение содержит недопустимые символы");
            }

            return result;
        }

        /// <summary>
        /// Валидировать ID объекта
        /// </summary>
        public static ValidationResult ValidateObjectId(Guid objectId)
        {
            var result = new ValidationResult { IsValid = true };

            if (objectId == Guid.Empty)
            {
                result.AddError("ID объекта не может быть пустым");
            }

            return result;
        }

        /// <summary>
        /// Валидировать массив целых чисел
        /// </summary>
        public static ValidationResult ValidateIntArray(int[] array, string fieldName = "Array")
        {
            var result = new ValidationResult { IsValid = true };

            if (array == null)
            {
                result.AddError($"{fieldName} не может быть null");
                return result;
            }

            if (array.Length == 0)
            {
                result.AddWarning($"{fieldName} пустой");
            }

            if (array.Length > 1000)
            {
                result.AddError($"{fieldName} слишком большой (максимум 1000 элементов)");
            }

            // Проверить на отрицательные значения
            if (array.Any(x => x < 0))
            {
                result.AddWarning($"{fieldName} содержит отрицательные значения");
            }

            return result;
        }

        /// <summary>
        /// Валидировать строку
        /// </summary>
        public static ValidationResult ValidateString(string value, string fieldName, int maxLength = 1000)
        {
            var result = new ValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(value))
            {
                result.AddError($"{fieldName} не может быть пустым");
                return result;
            }

            if (value.Length > maxLength)
            {
                result.AddError($"{fieldName} слишком длинный (максимум {maxLength} символов)");
            }

            return result;
        }

        /// <summary>
        /// Валидировать дату
        /// </summary>
        public static ValidationResult ValidateDate(DateTime date, string fieldName = "Date")
        {
            var result = new ValidationResult { IsValid = true };

            if (date == DateTime.MinValue)
            {
                result.AddError($"{fieldName} не может быть минимальной датой");
            }

            if (date > DateTime.Now.AddYears(10))
            {
                result.AddWarning($"{fieldName} слишком далеко в будущем");
            }

            if (date < DateTime.Now.AddYears(-100))
            {
                result.AddWarning($"{fieldName} слишком далеко в прошлом");
            }

            return result;
        }

        /// <summary>
        /// Валидировать конфигурацию
        /// </summary>
        public static ValidationResult ValidateConfiguration()
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                // Проверить настройки Rocket.Chat
                var rocketChatUrl = Configuration.AppSettings.RocketChat.BaseUrl;
                var rocketChatToken = Configuration.AppSettings.RocketChat.AuthToken;
                var rocketChatUserId = Configuration.AppSettings.RocketChat.UserId;

                var urlResult = ValidateUrl(rocketChatUrl);
                if (!urlResult.IsValid)
                {
                    result.Errors.AddRange(urlResult.Errors.Select(e => $"Rocket.Chat URL: {e}"));
                }

                if (string.IsNullOrWhiteSpace(rocketChatToken))
                {
                    result.AddError("Rocket.Chat токен не настроен");
                }

                if (string.IsNullOrWhiteSpace(rocketChatUserId))
                {
                    result.AddError("Rocket.Chat User ID не настроен");
                }

                // Проверить настройки SMTP
                var smtpHost = Configuration.AppSettings.Smtp.Host;
                var smtpPort = Configuration.AppSettings.Smtp.Port;
                var smtpUsername = Configuration.AppSettings.Smtp.Username;
                var smtpPassword = Configuration.AppSettings.Smtp.Password;

                if (string.IsNullOrWhiteSpace(smtpHost))
                {
                    result.AddError("SMTP хост не настроен");
                }

                if (smtpPort <= 0 || smtpPort > 65535)
                {
                    result.AddError("SMTP порт неверный");
                }

                var emailResult = ValidateEmail(smtpUsername);
                if (!emailResult.IsValid)
                {
                    result.Errors.AddRange(emailResult.Errors.Select(e => $"SMTP Username: {e}"));
                }

                if (string.IsNullOrWhiteSpace(smtpPassword))
                {
                    result.AddError("SMTP пароль не настроен");
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Ошибка при валидации конфигурации: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Получить сводку ошибок валидации
        /// </summary>
        public static string GetValidationSummary(ValidationResult result)
        {
            if (result.IsValid && result.Warnings.Count == 0)
            {
                return "Валидация прошла успешно";
            }

            var summary = new List<string>();

            if (!result.IsValid)
            {
                summary.Add("Ошибки:");
                summary.AddRange(result.Errors.Select(e => $"  - {e}"));
            }

            if (result.Warnings.Count > 0)
            {
                summary.Add("Предупреждения:");
                summary.AddRange(result.Warnings.Select(w => $"  - {w}"));
            }

            return string.Join(Environment.NewLine, summary);
        }
    }
}