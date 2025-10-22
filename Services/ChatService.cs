using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ascon.Pilot.SDK.Configuration;
using Ascon.Pilot.SDK.Services;

namespace Ascon.Pilot.SDK.Services
{
    /// <summary>
    /// Сервис для работы с Rocket.Chat
    /// </summary>
    public class ChatService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _authToken;
        private readonly string _userId;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор
        /// </summary>
        public ChatService()
        {
            _baseUrl = AppSettings.RocketChat.BaseUrl;
            _authToken = AppSettings.RocketChat.AuthToken;
            _userId = AppSettings.RocketChat.UserId;

            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", _authToken);
            _httpClient.DefaultRequestHeaders.Add("X-User-Id", _userId);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            LoggingService.LogInfo("ChatService initialized", nameof(ChatService));
        }

        /// <summary>
        /// Отправить сообщение в чат
        /// </summary>
        public async Task SendMessageAsync(string chatUserName, string messageText)
        {
            if (string.IsNullOrWhiteSpace(chatUserName))
            {
                LoggingService.LogWarning("Chat username is null or empty", nameof(SendMessageAsync));
                return;
            }

            if (string.IsNullOrWhiteSpace(messageText))
            {
                LoggingService.LogWarning("Message text is null or empty", nameof(SendMessageAsync));
                return;
            }

            try
            {
                LoggingService.LogInfo($"Sending message to {chatUserName}: {messageText}", nameof(SendMessageAsync));

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Создать или получить комнату чата
                var roomId = await CreateChatRoomAsync(chatUserName);
                
                // Отправить сообщение
                await SendMessageToRoomAsync(roomId, messageText);

                stopwatch.Stop();
                LoggingService.LogInfo($"Message sent successfully in {stopwatch.Elapsed.TotalMilliseconds:F2}ms", 
                    nameof(SendMessageAsync));
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to send message to {chatUserName}", ex, nameof(SendMessageAsync));
                throw;
            }
        }

        /// <summary>
        /// Создать комнату чата
        /// </summary>
        private async Task<string> CreateChatRoomAsync(string chatUserName)
        {
            try
            {
                var apiEndpoint = "/api/v1/im.create";
                var requestBody = JsonConvert.SerializeObject(new { username = chatUserName });

                var response = await ExecuteApiRequestAsync(apiEndpoint, requestBody);

                if (response?.success != true)
                {
                    var errorMessage = response?.error?.ToString() ?? "Unknown error";
                    LoggingService.LogError($"Failed to create chat room for {chatUserName}: {errorMessage}", 
                        null, nameof(CreateChatRoomAsync));
                    throw new Exception($"Failed to create chat room: {errorMessage}");
                }

                var roomId = response.room?._id?.ToString();
                if (string.IsNullOrEmpty(roomId))
                {
                    var errorMessage = response.room == null ? "Room field missing" : "Room ID missing";
                    LoggingService.LogError($"Room ID not available for {chatUserName}: {errorMessage}", 
                        null, nameof(CreateChatRoomAsync));
                    throw new Exception($"Failed to get room ID: {errorMessage}");
                }

                LoggingService.LogDebug($"Chat room created for {chatUserName}: {roomId}", nameof(CreateChatRoomAsync));
                return roomId;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to create chat room for {chatUserName}", ex, nameof(CreateChatRoomAsync));
                throw;
            }
        }

        /// <summary>
        /// Отправить сообщение в комнату
        /// </summary>
        private async Task SendMessageToRoomAsync(string roomId, string messageText)
        {
            try
            {
                var requestPayload = new
                {
                    message = new
                    {
                        rid = roomId,
                        msg = messageText
                    }
                };

                var requestBody = JsonConvert.SerializeObject(requestPayload);
                var response = await ExecuteApiRequestAsync("/api/v1/chat.sendMessage", requestBody);

                if (response?.success != true)
                {
                    var errorMessage = response?.error?.ToString() ?? "Unknown error";
                    LoggingService.LogError($"Failed to send message to room {roomId}: {errorMessage}", 
                        null, nameof(SendMessageToRoomAsync));
                    throw new Exception($"Failed to send message: {errorMessage}");
                }

                LoggingService.LogDebug($"Message sent to room {roomId}", nameof(SendMessageToRoomAsync));
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to send message to room {roomId}", ex, nameof(SendMessageToRoomAsync));
                throw;
            }
        }

        /// <summary>
        /// Выполнить API-запрос
        /// </summary>
        private async Task<dynamic> ExecuteApiRequestAsync(string apiEndpoint, string requestBody)
        {
            try
            {
                using (var requestContent = new StringContent(requestBody, Encoding.UTF8, "application/json"))
                {
                    var response = await _httpClient.PostAsync(apiEndpoint, requestContent);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    LoggingService.LogDebug($"API response for {apiEndpoint}: {responseContent}", nameof(ExecuteApiRequestAsync));

                    if (!response.IsSuccessStatusCode)
                    {
                        LoggingService.LogError($"HTTP error {response.StatusCode} for {apiEndpoint}: {responseContent}", 
                            null, nameof(ExecuteApiRequestAsync));
                        throw new Exception($"HTTP error {response.StatusCode}: {responseContent}");
                    }

                    var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    if (result == null)
                    {
                        LoggingService.LogError($"Failed to deserialize response for {apiEndpoint}", 
                            null, nameof(ExecuteApiRequestAsync));
                        throw new Exception("Failed to deserialize API response");
                    }

                    return result;
                }
            }
            catch (HttpRequestException ex)
            {
                LoggingService.LogError($"Rocket.Chat API error for {apiEndpoint}", ex, nameof(ExecuteApiRequestAsync));
                throw new Exception($"Rocket.Chat API error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Unexpected error for {apiEndpoint}", ex, nameof(ExecuteApiRequestAsync));
                throw;
            }
        }

        /// <summary>
        /// Обработать сообщение (убрать специальные символы, ограничить длину)
        /// </summary>
        private string ProcessMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            // Убрать специальные символы
            var processedMessage = message
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");

            // Ограничить длину сообщения
            if (processedMessage.Length > 2000)
            {
                processedMessage = processedMessage.Substring(0, 2000) + "...";
            }

            return processedMessage;
        }

        /// <summary>
        /// Получить информацию о пользователе
        /// </summary>
        public async Task<dynamic> GetUserInfoAsync(string username)
        {
            try
            {
                var apiEndpoint = $"/api/v1/users.info?username={Uri.EscapeDataString(username)}";
                var response = await _httpClient.GetAsync(apiEndpoint);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    LoggingService.LogWarning($"Failed to get user info for {username}: {responseContent}", 
                        nameof(GetUserInfoAsync));
                    return null;
                }

                return JsonConvert.DeserializeObject<dynamic>(responseContent);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to get user info for {username}", ex, nameof(GetUserInfoAsync));
                return null;
            }
        }

        /// <summary>
        /// Проверить подключение к Rocket.Chat
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/v1/info");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to test Rocket.Chat connection", ex, nameof(TestConnectionAsync));
                return false;
            }
        }

        /// <summary>
        /// Освободить ресурсы
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}