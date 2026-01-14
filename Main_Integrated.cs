#define DEBUG
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Ascon.Pilot.SDK;
using Ascon.Pilot.SDK.CreateObjectSample;
using Ascon.Pilot.SDK.HotKey;
using Ascon.Pilot.SDK.Menu;
using Ascon.Pilot.SDK.NotificationsSample;
using Ascon.Pilot.SDK.ObjectCard;
using Ascon.Pilot.SDK.Services;
using Ascon.Pilot.SDK.Configuration;
using Ascon.Pilot.SDK.Validation;
using Newtonsoft.Json;

[Export(typeof(IDataPlugin))]
[Export(typeof(IObjectCardHandler))]
[Export(typeof(ISignatureModifier))]
[Export(typeof(IFileProvider))]
[Export(typeof(IMenu<ObjectsViewContext>))]
public class Main : IFileProvider, IDisposable, ISignatureModifier, ISignatureBuilder, IDataPlugin, IObserver<INotification>, IObjectCardHandler, IObjectModifier, IObjectBuilder, IMenu<ObjectsViewContext>, IHotKey<ObjectsViewContext>
{
	internal class FileExtensionHelper
	{
		public const string XPS_ALIKE_REGEX_MASK = "\\.xps$|\\.dwfx$";

		public static bool IsXpsAlike(string filePath)
		{
			return IsFileMatchesExtensionMask(filePath, "\\.xps$|\\.dwfx$");
		}

		private static bool IsFileMatchesExtensionMask(string filePath, string mask)
		{
			string extension = Path.GetExtension(filePath);
			return !string.IsNullOrEmpty(extension) && Regex.IsMatch(extension, mask, RegexOptions.IgnoreCase);
		}
	}

	private readonly IObjectsRepository Repository;
	private readonly IObjectModifier _modifier;
	private readonly IFileProvider _fileProvider;
	private readonly IPilotStorageCommandController _PilotStorageCommandController;

	// Новые сервисы
	private readonly NotificationService _notificationService;
	private readonly ChatService _chatService;
	private readonly EmailService _emailService;
	private bool _disposed = false;

	// Сохраняем старые GUID для совместимости
	private Guid taskDelete = new Guid("abdbe49a-7094-4084-9673-eb5fb3f95262");
	private Guid taskNone = new Guid("d8ae8c3a-6f46-45d2-835b-563fe2b47acd");
	private Guid taskAssign = new Guid("dfa42efa-2748-4320-a6a2-594d1e24ead7");
	private Guid taskInProgress = new Guid("bf2f17dc-8b0c-4723-a7a1-e394e9330dc8");
	private Guid taskHasRemarks = new Guid("8e20c0ae-367e-4c07-b3fe-23d638c2a2c8");
	private Guid taskNoRemarks = new Guid("2f1e55d8-ea8d-41d7-be0a-0f986c4fc9e1");
	private Guid documentSigned = new Guid("a74e2bb3-b89c-466f-b994-b04b54dd9779");
	private Guid awaitigSigned = new Guid("9198ffdc-bbee-4aed-88b5-32a04c9a1ea3");
	private Guid state_assign = new Guid("dfa42efa-2748-4320-a6a2-594d1e24ead7");
	private Guid state_none = new Guid("d8ae8c3a-6f46-45d2-835b-563fe2b47acd");
	private Guid state_signed = new Guid("a74e2bb3-b89c-466f-b994-b04b54dd9779");
	private Guid state_revoked = new Guid("abdbe49a-7094-4084-9673-eb5fb3f95262");
	private Guid state_awaitingSignature = new Guid("9198ffdc-bbee-4aed-88b5-32a04c9a1ea3");
	private Guid state_hasRemarks = new Guid("8e20c0ae-367e-4c07-b3fe-23d638c2a2c8");
	private Guid state_delete = new Guid("8e20c0ae-367e-4c07-b3fe-23d638c2a2c8");
	private Guid state_workflowCompleted = new Guid("69042a68-29c1-494b-aecf-ce2857cb8098");
	private Guid notApproved = new Guid("ca50bfc9-0d90-4573-a9b6-9a28f48ec02c");
	private Guid state_sendToSignature = new Guid("a567f440-1005-4825-8cbe-bb1e1e18ad99");
	private Guid state_has_Remarks_to_po = new Guid("83eae38b-f842-4c22-9b00-b7f9ea4b7158");

	// Сохраняем старые массивы для совместимости
	private string[] depName = new string[15] { "Генеральный директор", "Технический директор", "Главный инженер", "Начальник ПТО", "Начальник АСУП", "Начальник ОГТ", "Начальник ОК", "Начальник ОМТС", "Начальник ОГЭ", "Начальник ОТБ", "Начальник ОЭС", "Начальник ОХД", "Начальник АХО", "Начальник ОБ", "Начальник ОТиЗ" };
	private int[] depNum = new int[15] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
	private int[] depBoss = new int[15] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
	public string[][] user = new string[300][];
	public string IdTaskContextLast;
	public Ascon.Pilot.SDK.IDataObject TaskContextLast;
	public Ascon.Pilot.SDK.IDataObject currentStadia;
	public int[] TypesForFolderCopy = new int[13] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
	public int[] TypesForFolderTask = new int[9] { 33, 34, 35, 36, 38, 39, 40, 67, 70 };
	private Ascon.Pilot.SDK.IDataObject selectedObjectTask;
	private Ascon.Pilot.SDK.IDataObject inProject;
	private Ascon.Pilot.SDK.IDataObject inStadia;
	private DateTime timePastProjectCreate = DateTime.Now;
	public List<Guid> dataSelectedGuids = new List<Guid>();
	public int countSelectedDoc = 0;
	private List<Ascon.Pilot.SDK.IDataObject> SelectedDoc = new List<Ascon.Pilot.SDK.IDataObject>();
	private static readonly Dictionary<string, Guid> DocumentStates;

	public Ascon.Pilot.SDK.IDataObject DataObject
	{
		get
		{
			return Repository.GetObject(DataObject.Id);
		}
		set
		{
			DataObject = value;
		}
	}

	static Main()
	{
		DocumentStates = new Dictionary<string, Guid>
		{
			{ "taskDelete", new Guid("abdbe49a-7094-4084-9673-eb5fb3f95262") },
			{ "taskNone", new Guid("d8ae8c3a-6f46-45d2-835b-563fe2b47acd") },
			{ "taskAssign", new Guid("dfa42efa-2748-4320-a6a2-594d1e24ead7") },
			{ "taskInProgress", new Guid("bf2f17dc-8b0c-4723-a7a1-e394e9330dc8") },
			{ "taskHasRemarks", new Guid("8e20c0ae-367e-4c07-b3fe-23d638c2a2c8") },
			{ "taskNoRemarks", new Guid("2f1e55d8-ea8d-41d7-be0a-0f986c4fc9e1") },
			{ "documentSigned", new Guid("a74e2bb3-b89c-466f-b994-b04b54dd9779") },
			{ "awaitigSigned", new Guid("9198ffdc-bbee-4aed-88b5-32a04c9a1ea3") }
		};
	}

	[ImportingConstructor]
	[Obsolete]
	public Main(IObjectsRepository repository, IObjectModifier modifier, IPilotStorageCommandController PilotStorageCommand, IFileProvider fileProvider)
	{
		Repository = repository ?? throw new ArgumentNullException(nameof(repository));
		_modifier = modifier ?? throw new ArgumentNullException(nameof(modifier));
		_fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
		_PilotStorageCommandController = PilotStorageCommand ?? throw new ArgumentNullException(nameof(PilotStorageCommand));

		// Инициализация новых сервисов
		try
		{
			// Валидация конфигурации при запуске
			var configResult = InputValidator.ValidateConfiguration();
			if (!configResult.IsValid)
			{
				LoggingService.LogCritical($"Configuration validation failed: {InputValidator.GetValidationSummary(configResult)}", 
					null, nameof(Main));
				throw new InvalidOperationException("Configuration validation failed");
			}

			// Инициализация сервисов
			_chatService = new ChatService();
			_emailService = new EmailService();
			_notificationService = new NotificationService(repository, modifier);

			LoggingService.LogInfo("Main module initialized successfully", nameof(Main));

			// Тест подключений
			_ = Task.Run(async () =>
			{
				try
				{
					var chatConnected = await _chatService.TestConnectionAsync();
					var emailConnected = await _emailService.TestConnectionAsync();

					LoggingService.LogInfo($"Service connections - Chat: {(chatConnected ? "OK" : "FAIL")}, Email: {(emailConnected ? "OK" : "FAIL")}", 
						nameof(Main));
				}
				catch (Exception ex)
				{
					LoggingService.LogError("Failed to test service connections", ex, nameof(Main));
				}
			});
		}
		catch (Exception ex)
		{
			LoggingService.LogCritical("Failed to initialize Main module", ex, nameof(Main));
			throw;
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_notificationService?.Dispose();
			_chatService?.Dispose();
			_emailService?.Dispose();
			_disposed = true;
		}
	}

	// Заменяем старые методы на новые сервисы
	public async Task SendChatNotificationAsync(string chatName, string messageText, [CallerLineNumber] int sourceLineNumber = 0)
	{
		try
		{
			// Валидация входных данных
			var validationResult = InputValidator.ValidateChatMessage(messageText);
			if (!validationResult.IsValid)
			{
				LoggingService.LogError($"Chat message validation failed: {string.Join(", ", validationResult.Errors)}", 
					null, nameof(SendChatNotificationAsync));
				return;
			}

			await _chatService.SendMessageAsync(chatName, messageText);
		}
		catch (Exception ex)
		{
			LoggingService.LogError($"Failed to send chat notification to {chatName}", ex, nameof(SendChatNotificationAsync));
		}
	}

	public void SendEmailNotification(string recipientEmail, string messageBody)
	{
		try
		{
			// Валидация email
			var emailValidation = InputValidator.ValidateEmail(recipientEmail);
			if (!emailValidation.IsValid)
			{
				LoggingService.LogError($"Email validation failed: {string.Join(", ", emailValidation.Errors)}", 
					null, nameof(SendEmailNotification));
				return;
			}

			// Асинхронная отправка email
			_ = Task.Run(async () =>
			{
				try
				{
					await _emailService.SendEmailAsync(recipientEmail, "Уведомление от системы Pilot", messageBody);
				}
				catch (Exception ex)
				{
					LoggingService.LogError($"Failed to send email to {recipientEmail}", ex, nameof(SendEmailNotification));
				}
			});
		}
		catch (Exception ex)
		{
			LoggingService.LogError($"Failed to initiate email sending to {recipientEmail}", ex, nameof(SendEmailNotification));
		}
	}

	// Заменяем старый OnNext на новый сервис
	[Obsolete]
	public async void OnNext(INotification value)
	{
		try
		{
			// Используем новый NotificationService
			await _notificationService.ProcessNotificationAsync(value);
		}
		catch (Exception ex)
		{
			LoggingService.LogError("Failed to process notification in OnNext", ex, nameof(OnNext));
		}
	}

	public void OnError(Exception error)
	{
		LoggingService.LogError("Notification error occurred", error, nameof(OnError));
	}

	public void OnCompleted()
	{
		LoggingService.LogInfo("Notification stream completed", nameof(OnCompleted));
	}

	// Остальные методы остаются без изменений для совместимости
	// ... (все остальные методы из оригинального Main.cs)
}