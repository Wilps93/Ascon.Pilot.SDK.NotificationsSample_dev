using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Ascon.Pilot.SDK.Configuration
{
    /// <summary>
    /// Безопасная конфигурация приложения
    /// </summary>
    public static class AppSettings
    {
        private static readonly string EncryptionKey = "TOMS_Pilot_2025_Key";

        /// <summary>
        /// Настройки Rocket.Chat
        /// </summary>
        public static class RocketChat
        {
            public static string BaseUrl => GetSecureSetting("RocketChat:BaseUrl", "http://192.168.10.180:3000");
            public static string AuthToken => GetSecureSetting("RocketChat:AuthToken", "YxGV8XDD9dBIRuKLn6nNOv1JeoXHF_anND0s58oS4xR");
            public static string UserId => GetSecureSetting("RocketChat:UserId", "PRGcw8PrY9YNyGfjo");
        }

        /// <summary>
        /// Настройки SMTP
        /// </summary>
        public static class Smtp
        {
            public static string Host => GetSecureSetting("Smtp:Host", "mail.tomsmineral.ru");
            public static int Port => int.Parse(GetSecureSetting("Smtp:Port", "587"));
            public static string Username => GetSecureSetting("Smtp:Username", "pilot-ice@tomsmineral.ru");
            public static string Password => GetSecureSetting("Smtp:Password", "Sxk8uRfcWaxz7");
            public static bool EnableSsl => bool.Parse(GetSecureSetting("Smtp:EnableSsl", "true"));
        }

        /// <summary>
        /// Настройки приложения
        /// </summary>
        public static class Application
        {
            public static string CompanyName => GetSetting("Application:CompanyName", "НИИПИ \"ТОМС\"");
            public static string ProductName => GetSetting("Application:ProductName", "TOMS.moduls");
            public static string Version => GetSetting("Application:Version", "10.7.25.0");
        }

        /// <summary>
        /// Получить настройку из конфигурации
        /// </summary>
        private static string GetSetting(string key, string defaultValue)
        {
            try
            {
                return ConfigurationManager.AppSettings[key] ?? defaultValue;
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Получить зашифрованную настройку из конфигурации
        /// </summary>
        private static string GetSecureSetting(string key, string defaultValue)
        {
            try
            {
                var encryptedValue = ConfigurationManager.AppSettings[key];
                if (string.IsNullOrEmpty(encryptedValue))
                    return defaultValue;

                return DecryptString(encryptedValue);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Зашифровать строку
        /// </summary>
        public static string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(EncryptionKey.PadRight(32).Substring(0, 32));
                    aes.IV = new byte[16];

                    using (var encryptor = aes.CreateEncryptor())
                    using (var msEncrypt = new System.IO.MemoryStream())
                    {
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (var swEncrypt = new System.IO.StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }

                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
            catch (Exception)
            {
                return plainText;
            }
        }

        /// <summary>
        /// Расшифровать строку
        /// </summary>
        private static string DecryptString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = Encoding.UTF8.GetBytes(EncryptionKey.PadRight(32).Substring(0, 32));
                    aes.IV = new byte[16];

                    using (var decryptor = aes.CreateDecryptor())
                    using (var msDecrypt = new System.IO.MemoryStream(Convert.FromBase64String(cipherText)))
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var srDecrypt = new System.IO.StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch (Exception)
            {
                return cipherText;
            }
        }
    }
}