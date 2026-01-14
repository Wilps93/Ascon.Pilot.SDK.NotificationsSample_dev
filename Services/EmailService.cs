using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Ascon.Pilot.SDK.Configuration;
using Ascon.Pilot.SDK.Services;

namespace Ascon.Pilot.SDK.Services
{
    /// <summary>
    /// Сервис для работы с email
    /// </summary>
    public class EmailService : IDisposable
    {
        private readonly SmtpClient _smtpClient;
        private readonly string _fromEmail;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор
        /// </summary>
        public EmailService()
        {
            _fromEmail = AppSettings.Smtp.Username;
            
            _smtpClient = new SmtpClient
            {
                Host = AppSettings.Smtp.Host,
                Port = AppSettings.Smtp.Port,
                EnableSsl = AppSettings.Smtp.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(AppSettings.Smtp.Username, AppSettings.Smtp.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            LoggingService.LogInfo("EmailService initialized", nameof(EmailService));
        }

        /// <summary>
        /// Отправить email уведомление
        /// </summary>
        public async Task SendEmailAsync(string recipientEmail, string subject, string messageBody, bool isHtml = true)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                LoggingService.LogWarning("Recipient email is null or empty", nameof(SendEmailAsync));
                return;
            }

            if (string.IsNullOrWhiteSpace(messageBody))
            {
                LoggingService.LogWarning("Message body is null or empty", nameof(SendEmailAsync));
                return;
            }

            try
            {
                LoggingService.LogInfo($"Sending email to {recipientEmail}: {subject}", nameof(SendEmailAsync));

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                using (var emailMessage = new MailMessage())
                {
                    emailMessage.From = new MailAddress(_fromEmail);
                    emailMessage.To.Add(new MailAddress(recipientEmail));
                    emailMessage.Subject = subject ?? "Уведомление от системы Pilot";
                    emailMessage.IsBodyHtml = isHtml;
                    emailMessage.Body = messageBody;

                    await _smtpClient.SendMailAsync(emailMessage);
                }

                stopwatch.Stop();
                LoggingService.LogInfo($"Email sent successfully in {stopwatch.Elapsed.TotalMilliseconds:F2}ms", 
                    nameof(SendEmailAsync));
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to send email to {recipientEmail}", ex, nameof(SendEmailAsync));
                throw;
            }
        }

        /// <summary>
        /// Отправить уведомление о задании
        /// </summary>
        public async Task SendTaskNotificationAsync(string recipientEmail, string taskName, string taskUrl, 
            string initiatorName, string projectName = null)
        {
            try
            {
                var subject = "Новое задание в системе Pilot";
                var messageBody = CreateTaskNotificationHtml(taskName, taskUrl, initiatorName, projectName);

                await SendEmailAsync(recipientEmail, subject, messageBody);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to send task notification to {recipientEmail}", ex, 
                    nameof(SendTaskNotificationAsync));
                throw;
            }
        }

        /// <summary>
        /// Отправить уведомление об изменении состояния
        /// </summary>
        public async Task SendStateChangeNotificationAsync(string recipientEmail, string taskName, string taskUrl, 
            string executorName, string newState, string projectName = null)
        {
            try
            {
                var subject = "Изменение состояния задания в системе Pilot";
                var messageBody = CreateStateChangeNotificationHtml(taskName, taskUrl, executorName, newState, projectName);

                await SendEmailAsync(recipientEmail, subject, messageBody);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to send state change notification to {recipientEmail}", ex, 
                    nameof(SendStateChangeNotificationAsync));
                throw;
            }
        }

        /// <summary>
        /// Создать HTML для уведомления о задании
        /// </summary>
        private string CreateTaskNotificationHtml(string taskName, string taskUrl, string initiatorName, string projectName)
        {
            var projectInfo = !string.IsNullOrEmpty(projectName) ? $"<p><strong>Проект:</strong> {projectName}</p>" : "";

            return $@"
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .container {{ max-width: 600px; margin: 0 auto; }}
                        .header {{ background-color: #f8f9fa; padding: 20px; border-radius: 5px; }}
                        .content {{ padding: 20px; }}
                        .button {{ display: inline-block; padding: 10px 20px; background-color: #007bff; 
                                   color: white; text-decoration: none; border-radius: 5px; }}
                        .footer {{ margin-top: 20px; padding-top: 20px; border-top: 1px solid #dee2e6; 
                                   font-size: 12px; color: #6c757d; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Новое задание</h2>
                        </div>
                        <div class='content'>
                            <p><strong>Задание:</strong> {taskName}</p>
                            <p><strong>Инициатор:</strong> {initiatorName}</p>
                            {projectInfo}
                            <p>
                                <a href='{taskUrl}' class='button'>Открыть задание</a>
                            </p>
                        </div>
                        <div class='footer'>
                            <p>Это автоматическое уведомление от системы Pilot.</p>
                            <p>Если у вас есть вопросы, обратитесь к администратору системы.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        /// <summary>
        /// Создать HTML для уведомления об изменении состояния
        /// </summary>
        private string CreateStateChangeNotificationHtml(string taskName, string taskUrl, string executorName, 
            string newState, string projectName)
        {
            var projectInfo = !string.IsNullOrEmpty(projectName) ? $"<p><strong>Проект:</strong> {projectName}</p>" : "";
            var stateText = GetStateText(newState);

            return $@"
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .container {{ max-width: 600px; margin: 0 auto; }}
                        .header {{ background-color: #f8f9fa; padding: 20px; border-radius: 5px; }}
                        .content {{ padding: 20px; }}
                        .state {{ padding: 10px; background-color: #e9ecef; border-radius: 5px; margin: 10px 0; }}
                        .button {{ display: inline-block; padding: 10px 20px; background-color: #007bff; 
                                   color: white; text-decoration: none; border-radius: 5px; }}
                        .footer {{ margin-top: 20px; padding-top: 20px; border-top: 1px solid #dee2e6; 
                                   font-size: 12px; color: #6c757d; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Изменение состояния задания</h2>
                        </div>
                        <div class='content'>
                            <p><strong>Задание:</strong> {taskName}</p>
                            <p><strong>Исполнитель:</strong> {executorName}</p>
                            {projectInfo}
                            <div class='state'>
                                <strong>Новое состояние:</strong> {stateText}
                            </div>
                            <p>
                                <a href='{taskUrl}' class='button'>Открыть задание</a>
                            </p>
                        </div>
                        <div class='footer'>
                            <p>Это автоматическое уведомление от системы Pilot.</p>
                            <p>Если у вас есть вопросы, обратитесь к администратору системы.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        /// <summary>
        /// Получить текстовое описание состояния
        /// </summary>
        private string GetStateText(string state)
        {
            return state switch
            {
                "bf2f17dc-8b0c-4723-a7a1-e394e9330dc8" => "В работе",
                "a0068698-a2c3-4b5f-8504-e0d590bf49c8" => "На проверке",
                "dfa42efa-2748-4320-a6a2-594d1e24ead7" => "Назначено",
                "149a520c-523a-404a-a1ac-07973c6b23bd" => "Принято",
                "abdbe49a-7094-4084-9673-eb5fb3f95262" => "Отозвано",
                "8e20c0ae-367e-4c07-b3fe-23d638c2a2c8" => "Есть замечания",
                "2f1e55d8-ea8d-41d7-be0a-0f986c4fc9e1" => "Без замечаний",
                _ => "Неизвестное состояние"
            };
        }

        /// <summary>
        /// Проверить подключение к SMTP-серверу
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // Попробуем отправить тестовое письмо на тот же адрес
                await SendEmailAsync(_fromEmail, "Test Connection", "This is a test email to verify SMTP connection.");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to test SMTP connection", ex, nameof(TestConnectionAsync));
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
                _smtpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}