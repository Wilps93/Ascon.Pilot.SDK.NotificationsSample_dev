using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ascon.Pilot.SDK;
using Ascon.Pilot.SDK.Services;

namespace Ascon.Pilot.SDK.Services
{
    /// <summary>
    /// Сервис для работы с уведомлениями
    /// </summary>
    public class NotificationService : IDisposable
    {
        private readonly IObjectsRepository _repository;
        private readonly IObjectModifier _modifier;
        private readonly ChatService _chatService;
        private readonly EmailService _emailService;
        private readonly Dictionary<int, string[]> _userCache;
        private bool _disposed = false;

        /// <summary>
        /// Конструктор
        /// </summary>
        public NotificationService(IObjectsRepository repository, IObjectModifier modifier)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _modifier = modifier ?? throw new ArgumentNullException(nameof(modifier));
            _chatService = new ChatService();
            _emailService = new EmailService();
            _userCache = new Dictionary<int, string[]>();
            
            LoggingService.LogInfo("NotificationService initialized", nameof(NotificationService));
        }

        /// <summary>
        /// Обработать уведомление
        /// </summary>
        public async Task ProcessNotificationAsync(INotification notification)
        {
            if (notification == null)
            {
                LoggingService.LogWarning("Received null notification", nameof(ProcessNotificationAsync));
                return;
            }

            try
            {
                LoggingService.LogInfo($"Processing notification: {notification.ChangeKind} for object {notification.ObjectId}", 
                    nameof(ProcessNotificationAsync));

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Обновить кэш пользователей
                await UpdateUserCacheAsync();

                // Обработать в зависимости от типа уведомления
                switch (notification.ChangeKind)
                {
                    case NotificationKind.ObjectCreated:
                        await HandleObjectCreatedAsync(notification);
                        break;
                    case NotificationKind.ObjectAttributeChanged:
                        await HandleObjectAttributeChangedAsync(notification);
                        break;
                    case NotificationKind.ObjectSignatureChanged:
                        await HandleObjectSignatureChangedAsync(notification);
                        break;
                    case NotificationKind.ObjectDeleted:
                        await HandleObjectDeletedAsync(notification);
                        break;
                    default:
                        LoggingService.LogDebug($"Unhandled notification type: {notification.ChangeKind}", 
                            nameof(ProcessNotificationAsync));
                        break;
                }

                stopwatch.Stop();
                LoggingService.LogInfo($"Notification processed in {stopwatch.Elapsed.TotalMilliseconds:F2}ms", 
                    nameof(ProcessNotificationAsync));
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to process notification", ex, nameof(ProcessNotificationAsync));
                throw;
            }
        }

        /// <summary>
        /// Обработать создание объекта
        /// </summary>
        private async Task HandleObjectCreatedAsync(INotification notification)
        {
            try
            {
                var objectLoader = new ObjectLoader(_repository);
                var obj = await objectLoader.Load(notification.ObjectId, 0L);

                if (obj == null)
                {
                    LoggingService.LogWarning($"Failed to load object {notification.ObjectId}", 
                        nameof(HandleObjectCreatedAsync));
                    return;
                }

                // Обработать в зависимости от типа объекта
                switch (notification.TypeId)
                {
                    case 20: // task
                        await HandleTaskCreatedAsync(obj, notification);
                        break;
                    case 25: // workflow
                        await HandleWorkflowCreatedAsync(obj, notification);
                        break;
                    case 28: // project
                        await HandleProjectCreatedAsync(obj, notification);
                        break;
                    default:
                        LoggingService.LogDebug($"Unhandled object type: {notification.TypeId}", 
                            nameof(HandleObjectCreatedAsync));
                        break;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to handle object creation", ex, nameof(HandleObjectCreatedAsync));
            }
        }

        /// <summary>
        /// Обработать создание задания
        /// </summary>
        private async Task HandleTaskCreatedAsync(IDataObject task, INotification notification)
        {
            try
            {
                // Получить информацию об исполнителе и инициаторе
                var executorId = GetExecutorId(task);
                var initiatorId = GetInitiatorId(task);

                if (executorId.HasValue && _userCache.ContainsKey(executorId.Value))
                {
                    var executorNick = _userCache[executorId.Value][0];
                    var initiatorNick = initiatorId.HasValue && _userCache.ContainsKey(initiatorId.Value) 
                        ? _userCache[initiatorId.Value][0] 
                        : "Unknown";

                    var message = $":assigned: @{initiatorNick} отправил(а) вам задание: [{task.DisplayName}](piloturi://{task.Id})";
                    
                    await _chatService.SendMessageAsync(executorNick, message);
                    LoggingService.LogInfo($"Task notification sent to {executorNick}", nameof(HandleTaskCreatedAsync));
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to handle task creation", ex, nameof(HandleTaskCreatedAsync));
            }
        }

        /// <summary>
        /// Обработать создание процесса
        /// </summary>
        private async Task HandleWorkflowCreatedAsync(IDataObject workflow, INotification notification)
        {
            try
            {
                LoggingService.LogInfo($"Workflow created: {workflow.DisplayName}", nameof(HandleWorkflowCreatedAsync));
                
                // Установить начальное состояние
                _modifier.EditById(workflow.Id).SetAttribute("state", 
                    new Guid("11748395-9a9f-48cd-92ef-7a9d9f776ecd"));
                _modifier.Apply();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to handle workflow creation", ex, nameof(HandleWorkflowCreatedAsync));
            }
        }

        /// <summary>
        /// Обработать создание проекта
        /// </summary>
        private async Task HandleProjectCreatedAsync(IDataObject project, INotification notification)
        {
            try
            {
                LoggingService.LogInfo($"Project created: {project.DisplayName}", nameof(HandleProjectCreatedAsync));
                
                // Сделать проект публичным
                _modifier.EditById(project.Id).MakePublic();
                _modifier.Apply();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to handle project creation", ex, nameof(HandleProjectCreatedAsync));
            }
        }

        /// <summary>
        /// Обработать изменение атрибутов объекта
        /// </summary>
        private async Task HandleObjectAttributeChangedAsync(INotification notification)
        {
            try
            {
                var objectLoader = new ObjectLoader(_repository);
                var obj = await objectLoader.Load(notification.ObjectId, 0L);

                if (obj == null) return;

                // Обработать изменения состояния
                if (obj.Attributes.ContainsKey("state"))
                {
                    await HandleStateChangeAsync(obj, notification);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to handle attribute change", ex, nameof(HandleObjectAttributeChangedAsync));
            }
        }

        /// <summary>
        /// Обработать изменение состояния
        /// </summary>
        private async Task HandleStateChangeAsync(IDataObject obj, INotification notification)
        {
            try
            {
                var state = (Guid)obj.Attributes["state"];
                var executorId = GetExecutorId(obj);
                var initiatorId = GetInitiatorId(obj);

                if (!executorId.HasValue || !initiatorId.HasValue) return;

                var executorNick = _userCache.ContainsKey(executorId.Value) ? _userCache[executorId.Value][0] : "Unknown";
                var initiatorNick = _userCache.ContainsKey(initiatorId.Value) ? _userCache[initiatorId.Value][0] : "Unknown";

                string message = null;

                // Определить сообщение в зависимости от состояния
                switch (state.ToString())
                {
                    case "bf2f17dc-8b0c-4723-a7a1-e394e9330dc8": // InProgress
                        message = $":inprogress: @{executorNick} приступил(а) к выполнению задания [{obj.DisplayName}](piloturi://{obj.Id})";
                        break;
                    case "a0068698-a2c3-4b5f-8504-e0d590bf49c8": // OnValidation
                        message = $":noremarks: @{executorNick} завершил(а) задание [{obj.DisplayName}](piloturi://{obj.Id}) и оно ожидает вашей проверки";
                        break;
                    case "dfa42efa-2748-4320-a6a2-594d1e24ead7": // Assigned
                        message = $":assigned: @{initiatorNick} вернул(а) вам задание [{obj.DisplayName}](piloturi://{obj.Id})";
                        break;
                }

                if (!string.IsNullOrEmpty(message))
                {
                    await _chatService.SendMessageAsync(initiatorNick, message);
                    LoggingService.LogInfo($"State change notification sent", nameof(HandleStateChangeAsync));
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to handle state change", ex, nameof(HandleStateChangeAsync));
            }
        }

        /// <summary>
        /// Обработать изменение подписи
        /// </summary>
        private async Task HandleObjectSignatureChangedAsync(INotification notification)
        {
            try
            {
                LoggingService.LogInfo($"Signature changed for object {notification.ObjectId}", 
                    nameof(HandleObjectSignatureChangedAsync));
                
                // Логика обработки изменения подписи
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to handle signature change", ex, nameof(HandleObjectSignatureChangedAsync));
            }
        }

        /// <summary>
        /// Обработать удаление объекта
        /// </summary>
        private async Task HandleObjectDeletedAsync(INotification notification)
        {
            try
            {
                LoggingService.LogInfo($"Object deleted: {notification.ObjectId}", nameof(HandleObjectDeletedAsync));
                
                // Логика обработки удаления объекта
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to handle object deletion", ex, nameof(HandleObjectDeletedAsync));
            }
        }

        /// <summary>
        /// Обновить кэш пользователей
        /// </summary>
        private async Task UpdateUserCacheAsync()
        {
            try
            {
                var objectLoader = new ObjectLoader(_repository);
                var userFolder = await objectLoader.Load(new Guid("d3496120-943a-4378-9641-e25787f74898"), 0L);
                
                if (userFolder?.Children != null)
                {
                    foreach (var userGuid in userFolder.Children)
                    {
                        var userFile = await objectLoader.Load(userGuid, 0L);
                        if (userFile?.Attributes != null)
                        {
                            var userId = (int)(long)userFile.Attributes["userId"];
                            var userNick = userFile.Attributes["userNick"]?.ToString() ?? "Unknown";
                            var userName = userFile.Attributes["UserName"]?.ToString() ?? "Unknown";
                            
                            _userCache[userId] = new[] { userNick, userName };
                        }
                    }
                    
                    LoggingService.LogDebug($"User cache updated: {_userCache.Count} users", nameof(UpdateUserCacheAsync));
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to update user cache", ex, nameof(UpdateUserCacheAsync));
            }
        }

        /// <summary>
        /// Получить ID исполнителя
        /// </summary>
        private int? GetExecutorId(IDataObject obj)
        {
            if (obj?.Attributes?.ContainsKey("executor") == true)
            {
                var executors = obj.Attributes["executor"] as int[];
                return executors?.FirstOrDefault();
            }
            return null;
        }

        /// <summary>
        /// Получить ID инициатора
        /// </summary>
        private int? GetInitiatorId(IDataObject obj)
        {
            if (obj?.Attributes?.ContainsKey("initiator") == true)
            {
                var initiators = obj.Attributes["initiator"] as int[];
                return initiators?.FirstOrDefault();
            }
            return null;
        }

        /// <summary>
        /// Освободить ресурсы
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _chatService?.Dispose();
                _emailService?.Dispose();
                _disposed = true;
            }
        }
    }
}