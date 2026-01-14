using System;
using System.IO;
using Newtonsoft.Json;

namespace Ascon.Pilot.SDK.NotificationsSample
{
    public class ConfigurationHelper
    {
        private static AppSettings _settings;

        public static AppSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    LoadSettings();
                }
                return _settings;
            }
        }

        private static void LoadSettings()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json);
                }
                else
                {
                    // Use default settings if file doesn't exist
                    _settings = new AppSettings
                    {
                        RocketChat = new RocketChatSettings
                        {
                            BaseUrl = "http://192.168.10.180:3000",
                            AuthToken = "YxGV8XDD9dBIRuKLn6nNOv1JeoXHF_anND0s58oS4xR",
                            UserId = "PRGcw8PrY9YNyGfjo"
                        },
                        BotSettings = new BotSettings
                        {
                            BotDataGuid = "f6f831df-0e77-4060-98d2-4f45b114750c",
                            MaxMessageLength = 2000
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading configuration: {ex.Message}");
                // Use default settings on error
                _settings = new AppSettings();
            }
        }
    }

    public class AppSettings
    {
        public RocketChatSettings RocketChat { get; set; } = new RocketChatSettings();
        public BotSettings BotSettings { get; set; } = new BotSettings();
    }

    public class RocketChatSettings
    {
        public string BaseUrl { get; set; } = "http://192.168.10.180:3000";
        public string AuthToken { get; set; } = "YxGV8XDD9dBIRuKLn6nNOv1JeoXHF_anND0s58oS4xR";
        public string UserId { get; set; } = "PRGcw8PrY9YNyGfjo";
    }

    public class BotSettings
    {
        public string BotDataGuid { get; set; } = "f6f831df-0e77-4060-98d2-4f45b114750c";
        public int MaxMessageLength { get; set; } = 2000;
    }
}