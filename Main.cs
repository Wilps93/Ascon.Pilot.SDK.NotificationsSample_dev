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
using Newtonsoft.Json;

[Export(typeof(IDataPlugin))]
[Export(typeof(IObjectCardHandler))]
[Export(typeof(ISignatureModifier))]
[Export(typeof(IFileProvider))]
[Export(typeof(IMenu<ObjectsViewContext>))]
public class Main : IFileProvider, IDisposable, ISignatureModifier, ISignatureBuilder, IDataPlugin, IObserver<INotification>, IObjectCardHandler, IObjectModifier, IObjectBuilder, IMenu<ObjectsViewContext>, IHotKey<ObjectsViewContext>
{
	public static class RocketChatSettings
	{
		public const string BaseUrl = "http://192.168.10.180:3000";

		public const string AuthToken = "YxGV8XDD9dBIRuKLn6nNOv1JeoXHF_anND0s58oS4xR";

		public const string UserId = "PRGcw8PrY9YNyGfjo";
	}

	public static class PilotLogger
	{
		public static void Log(string message, string methodName, bool isVerbose, TimeSpan? elapsed = null)
		{
			string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {methodName}: {message}";
			if (elapsed.HasValue)
			{
				log += $", Elapsed: {elapsed.Value.TotalMilliseconds:F3}ms";
			}
			Debug.WriteLine(log);
		}

		public static void LogError(string methodName, bool isVerbose, Exception ex, string message = null)
		{
			string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR {methodName}: {message ?? ex.Message}";
			if (ex != null)
			{
				log += $"\nException: {ex}";
			}
			Debug.WriteLine(log);
		}

		public static void LogLoad(string objectId, string methodName, bool isVerbose, TimeSpan? elapsed = null)
		{
			string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {methodName}: Loading object {objectId}";
			if (elapsed.HasValue)
			{
				log += $", Elapsed: {elapsed.Value.TotalMilliseconds:F3}ms";
			}
			Debug.WriteLine(log);
		}
	}

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

	private static readonly HttpClient _httpClient;

	private readonly IObjectsRepository Repository;

	private readonly IObjectModifier _modifier;

	private readonly IFileProvider _fileProvider;

	private readonly IPilotStorageCommandController _PilotStorageCommandController;

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

	private string[] depName = new string[15]
	{
		"АР", "АСУТП", "ВК", "ГИП", "НК", "ОВ", "ОГПиТ", "ОГТС", "ОИЭ", "ОТП",
		"СО", "ССиОПС", "СЭО", "ТО", "ЭТО"
	};

	private int[] depNum = new int[15]
	{
		104, 105, 26, 7, 7, 27, 103, 99, 126, 10,
		15, 129, 13, 16, 11
	};

	private int[] depBoss = new int[15]
	{
		22, 106, 29, 19, 19, 32, 21, 100, 127, 37,
		58, 47, 56, 69, 88
	};

	public string[][] user = new string[300][];

	public string IdTaskContextLast;

	public Ascon.Pilot.SDK.IDataObject TaskContextLast;

	public Ascon.Pilot.SDK.IDataObject currentStadia;

	public int[] TypesForFolderCopy = new int[13]
	{
		28, 29, 33, 34, 35, 36, 38, 39, 40, 52,
		67, 70, 79
	};

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
			throw new NotImplementedException();
		}
	}

	static Main()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Expected O, but got Unknown
		_httpClient = new HttpClient();
		DocumentStates = new Dictionary<string, Guid>
		{
			{
				"Empty",
				new Guid("d8ae8c3a-6f46-45d2-835b-563fe2b47acd")
			},
			{
				"AwaitAnswered",
				new Guid("f9f77c9f-b221-463f-92e9-0ffb41f8a325")
			},
			{
				"Answered",
				new Guid("da3f641b-6b82-439d-ac93-2e3a70a25d2f")
			}
		};
		_httpClient.BaseAddress = new Uri("http://192.168.10.180:3000");
		((HttpHeaders)_httpClient.DefaultRequestHeaders).Add("X-Auth-Token", "YxGV8XDD9dBIRuKLn6nNOv1JeoXHF_anND0s58oS4xR".Trim());
		((HttpHeaders)_httpClient.DefaultRequestHeaders).Add("X-User-Id", "PRGcw8PrY9YNyGfjo".Trim());
		_httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
	}

	[ImportingConstructor]
	[Obsolete]
	public Main(IObjectsRepository repository, IObjectModifier modifier, IPilotStorageCommandController PilotStorageCommand, IFileProvider fileProvider)
	{
		Repository = repository;
		_fileProvider = fileProvider;
		_modifier = modifier;
		_PilotStorageCommandController = PilotStorageCommand;
		repository.SubscribeNotification(NotificationKind.ObjectCreated).Subscribe(this);
		repository.SubscribeNotification(NotificationKind.ObjectAttributeChanged).Subscribe(this);
		repository.SubscribeNotification(NotificationKind.ObjectSignatureChanged).Subscribe(this);
		repository.SubscribeNotification(NotificationKind.StorageObjectCreated).Subscribe(this);
		repository.SubscribeNotification(NotificationKind.ObjectFileChanged).Subscribe(this);
		repository.SubscribeNotification(NotificationKind.TaskCreated).Subscribe(this);
		user[6] = new string[2] { "Suslov", "Админ" };
		user[71] = new string[2] { "Dokuchaev_ts", "Докучаев Тимофей Сергеевич" };
		user[69] = new string[2] { "Kirin", "Кирин Дмитрий Владимирович" };
		user[83] = new string[2] { "Cherkashin", "Черкашин Андрей Александрович" };
		user[43] = new string[2] { "Molokov", "Молоков Владислав Васильевич" };
		user[37] = new string[2] { "Sidorov", "Сидоров Александр Сергеевич" };
		user[39] = new string[2] { "Razboinikov", "Разбойников Роман Геннадьевич" };
		user[32] = new string[2] { "Abramenko", "Абраменко Дмитрий Николаевич" };
		user[68] = new string[2] { "barhatova", "Бархатова Надежда Викторовна" };
		user[117] = new string[2] { "Bolohoev", "Болохоев Альберт Николаевич" };
		user[34] = new string[2] { "Bolohoev_alex", "Болохоев Алексей Николаевич" };
		user[47] = new string[2] { "Byrgazov", "Быргазов Алексей Викторович" };
		user[35] = new string[2] { "Gushanskiy", "Гушанский Владимир Борисович" };
		user[57] = new string[2] { "bobrovnikova", "Бобровникова Любовь Игоревна" };
		user[45] = new string[2] { "Erin", "Ерин Вадим Николаевич" };
		user[25] = new string[2] { "Zhdanova", "Жданова Мария Владимировна" };
		user[66] = new string[2] { "Ivanova", "Иванова Марина Петровна" };
		user[29] = new string[2] { "Karpova", "Карпова Инна Петровна" };
		user[60] = new string[2] { "Katelevskaya", "Кателевская Анастасия Андреевна" };
		user[33] = new string[2] { "Kopytova", "Копытова Светлана Владимировна" };
		user[58] = new string[2] { "Kostousov_De", "Костоусов Дмитрий Евгеньевич" };
		user[64] = new string[2] { "Kostousov_Eb", "Костоусов Евгений Борисович" };
		user[20] = new string[2] { "Kunts", "Кунц Дарья Геннадьевна" };
		user[31] = new string[2] { "Matveeva", "Матвеева Людмила Валерьевна" };
		user[21] = new string[2] { "Morozov", "Морозов Игорь Андреевич" };
		user[49] = new string[2] { "Novopashin", "Новопашин Олег Игоревич" };
		user[22] = new string[2] { "Pavlova", "Павлова Евгения Олеговна" };
		user[55] = new string[2] { "Petryakova", "Петрякова Ольга Викторовна" };
		user[51] = new string[2] { "Аntsiferov", "Анциферов Иван Дмитриевич" };
		user[44] = new string[2] { "Svirbutovich", "Свирбутович-Артюхов Максим Витальевич" };
		user[36] = new string[2] { "Semakina", "Семакина Дарья Дмитриевна" };
		user[65] = new string[2] { "bikineeva", "Бикинеева Ангелина Дмитриевна" };
		user[66] = new string[2] { "dragunova", "Драгунова Анна Валерьевна" };
		user[30] = new string[2] { "Stavtseva", "Ставцева Алина Сергеевна" };
		user[41] = new string[2] { "Stafeev", "Стафеев Вадим Вадимович" };
		user[116] = new string[2] { "Tebyakin", "Тебякин Андрей Сергеевич" };
		user[79] = new string[2] { "Tokhtobin", "Тохтобин Денис Валерьевич" };
		user[101] = new string[2] { "Fedotov", "Федотов Никита Олегович" };
		user[28] = new string[2] { "Fedotova", "Федотова Анастасия Александровна" };
		user[52] = new string[2] { "Hamaza", "Хамаза Евгений Алексеевич" };
		user[56] = new string[2] { "Cherednik", "Чередник Диана Дмитриевна" };
		user[24] = new string[2] { "Cherepanova", "Черепанова Юлия Александровна" };
		user[19] = new string[2] { "Chernyy", "Черный Константин Геннадьевич" };
		user[61] = new string[2] { "Chigrinskaya", "Чигринская Лариса Сергеевна" };
		user[80] = new string[2] { "Shelkunov", "Шелкунов Юрий Анатольевич" };
		user[59] = new string[2] { "Shubin", "Шубин Степан Александрович" };
		user[86] = new string[2] { "Tihomirova", "Тихомирова Александра Владимировна" };
		user[46] = new string[2] { "Yusupova", "Юсупова Елена Викторовна" };
		user[17] = new string[2] { "kibirev", "Кибирев Андрей Викторович" };
		user[87] = new string[2] { "sUsloV", "test" };
		user[89] = new string[2] { "val_p38", "Пьянкова Валентина Павловна" };
		user[90] = new string[2] { "butorov", "Буторов Илья Павлович" };
		user[91] = new string[2] { "fedorova", "Фёдорова Юлия Викторовна" };
		user[92] = new string[2] { "Oluhov", "Олухов Михаил Вячеславович" };
		user[93] = new string[2] { "sUslov", "test2" };
		user[94] = new string[2] { "suSlov", "test3" };
		user[97] = new string[2] { "Shishkin_au", "Шишкин Александр Юрьевич" };
		user[40] = new string[2] { "Kabanov", "Кабанов Александр Олегович" };
		user[88] = new string[2] { "komarov", "Комаров Роман Андреевич" };
		user[84] = new string[2] { "Lipetskiy", "Липецкий Николай Игоревич" };
		user[53] = new string[2] { "malkov", "Малков Андрей Анатольевич" };
		user[102] = new string[2] { "Vinogorov", "Виногоров Дмитрий Сергеевич" };
		user[100] = new string[2] { "Sigitov", "Сигитов Владимир Александрович" };
		user[70] = new string[2] { "Vishnyakov", "Вишняков Дмитрий Олегович" };
		user[106] = new string[2] { "Shitikov", "Шитиков Сергей Николаевич" };
		user[107] = new string[2] { "Zaitsev", "Зайцев Анатолий Юрьевич" };
		user[108] = new string[2] { "vlasevsky", "Власевский Артемий Александрович" };
		user[111] = new string[2] { "koscheeva", "Кощеева Татьяна Владимировна" };
		user[114] = new string[2] { "Prokoptsev", "Прокопцев Алексей Александрович" };
		user[118] = new string[2] { "Bonoeva", "Боноева Ксения Алексеевна" };
		user[125] = new string[2] { "Shablonin", "Шаблонин Иван Андреевич" };
		user[115] = new string[2] { "karpeev", "Карпеев Павел Петрович" };
		user[120] = new string[2] { "sUslov", "test4" };
		user[122] = new string[2] { "sUslov", "test5" };
		user[127] = new string[2] { "Tolstyh", "Толстых Элеонора Александровна" };
		user[100] = new string[2] { "nikolaev", "Николаев Сергей Анатольевич" };
		user[132] = new string[2] { "Sabirova", "Сабирова Эльвира Раиловна" };
		user[133] = new string[2] { "protopopov", "Протопопов Александр Сергеевич" };
		user[136] = new string[2] { "pankina", "Панкина Ксения Георгиевна" };
		user[137] = new string[2] { "tarkov", "Тарков Юрий Михайлович" };
		user[138] = new string[2] { "kolugina", "Колугина Галина Владимировна" };
		user[139] = new string[2] { "gulkov", "Гульков Павел Павлович" };
		user[67] = new string[2] { "sokolova", "Соколова Маргарита Сречкоевна" };
		user[142] = new string[2] { "otrubenko", "Отрубенко Светлана Леонидовна" };
		user[145] = new string[2] { "vershok", "Вершок Денис Юрьевич" };
		AddUser();
	}

	public void Dispose()
	{
	}

	public string GetRedirectAddress(string NotificationType)
	{
		Guid databaseId = Repository.GetDatabaseId();
		if (Dns.GetHostName() == "Dokuchaevsv")
		{
			if (NotificationType == "email")
			{
				return "Dokuchaev@tomsmineral.ru";
			}
			if (NotificationType == "chat")
			{
				return "Dokuchaev_ts";
			}
		}
		return null;
	}

	public async Task CreateStorageLinkFromGuids(Guid FirstObjectId, Guid SecondObjectId)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		Ascon.Pilot.SDK.IDataObject firstObject = await loader.Load(FirstObjectId, 0L);
		Ascon.Pilot.SDK.IDataObject secondObject = await loader.Load(SecondObjectId, 0L);
		Guid relationId = Guid.NewGuid();
		string relationName = "LinkDocumentToFile";
		ObjectRelationType relationType = ObjectRelationType.SourceFiles;
		Relation relation1 = new Relation
		{
			Id = relationId,
			Type = relationType,
			Name = relationName,
			TargetId = firstObject.Id
		};
		Relation relation2 = new Relation
		{
			Id = relationId,
			Type = relationType,
			Name = relationName,
			TargetId = secondObject.Id
		};
		_modifier.CreateLink(relation1, relation2);
		_modifier.Apply();
	}

	public async Task CreateCustomRelationFromGuids(Guid FirstObjectId, Guid SecondObjectId)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		Ascon.Pilot.SDK.IDataObject firstObject = await loader.Load(FirstObjectId, 0L);
		Ascon.Pilot.SDK.IDataObject secondObject = await loader.Load(SecondObjectId, 0L);
		Guid relationId = Guid.NewGuid();
		string relationName = "RelationTaskSomeObj";
		ObjectRelationType relationType = ObjectRelationType.Custom;
		Relation relation1 = new Relation
		{
			Id = relationId,
			Type = relationType,
			Name = relationName,
			TargetId = firstObject.Id
		};
		Relation relation2 = new Relation
		{
			Id = relationId,
			Type = relationType,
			Name = relationName,
			TargetId = secondObject.Id
		};
		_modifier.CreateLink(relation1, relation2);
		_modifier.Apply();
	}

	public async Task CreateTaskAttachmentLinkFromGuids(Guid FirstObjectId, Guid SecondObjectId)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		Ascon.Pilot.SDK.IDataObject firstObject = await loader.Load(FirstObjectId, 0L);
		Ascon.Pilot.SDK.IDataObject secondObject = await loader.Load(SecondObjectId, 0L);
		Guid relationId = Guid.NewGuid();
		string relationName = "TaskAttachments";
		ObjectRelationType relationType = ObjectRelationType.TaskAttachments;
		Relation relation1 = new Relation
		{
			Id = relationId,
			Type = relationType,
			Name = relationName,
			TargetId = firstObject.Id
		};
		Relation relation2 = new Relation
		{
			Id = relationId,
			Type = relationType,
			Name = relationName,
			TargetId = secondObject.Id
		};
		_modifier.CreateLink(relation1, relation2);
		_modifier.Apply();
	}

	public static async Task<dynamic> ExecuteRocketChatApiRequestAsync(string apiEndpoint, string requestBody)
	{
		try
		{
			StringContent requestContent = new StringContent(requestBody, Encoding.UTF8, "application/json");
			try
			{
				HttpResponseMessage response = await _httpClient.PostAsync(apiEndpoint, (HttpContent)requestContent);
				try
				{
					string responseContent = await response.Content.ReadAsStringAsync();
					PilotLogger.Log("API response for " + apiEndpoint + ": " + responseContent, "ExecuteRocketChatApiRequestAsync", isVerbose: true);
					if (!response.IsSuccessStatusCode)
					{
						PilotLogger.LogError("ExecuteRocketChatApiRequestAsync", isVerbose: true, null, $"HTTP error {response.StatusCode} for {apiEndpoint}: {responseContent}");
						throw new Exception($"HTTP error {response.StatusCode}: {responseContent}");
					}
					dynamic result = JsonConvert.DeserializeObject<object>(responseContent);
					if (result == null)
					{
						PilotLogger.LogError("ExecuteRocketChatApiRequestAsync", isVerbose: true, null, "Failed to deserialize response for " + apiEndpoint);
						throw new Exception("Не удалось десериализовать ответ API");
					}
					return result;
				}
				finally
				{
					((IDisposable)response)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)requestContent)?.Dispose();
			}
		}
		catch (HttpRequestException ex)
		{
			HttpRequestException ex2 = ex;
			HttpRequestException ex3 = ex2;
			HttpRequestException ex4 = ex3;
			HttpRequestException ex5 = ex4;
			PilotLogger.LogError("ExecuteRocketChatApiRequestAsync", isVerbose: true, (Exception)(object)ex5, "Rocket.Chat API error for " + apiEndpoint);
			throw new Exception("Ошибка API Rocket.Chat: " + ((Exception)(object)ex5).Message, (Exception)(object)ex5);
		}
	}

	public async Task SendChatNotificationAsync(string chatName, string messageText, [CallerLineNumber] int sourceLineNumber = 0)
	{
		string redirectChatUser = GetRedirectAddress("chat");
		if (redirectChatUser != null)
		{
			chatName = redirectChatUser;
		}
		if (Dns.GetHostName() == "Dokuchaevse" && redirectChatUser == null)
		{
			MessageBox.Show(messageText, chatName);
			return;
		}
		try
		{
			ObjectLoader loader = new ObjectLoader(Repository);
			Ascon.Pilot.SDK.IDataObject botData = await loader.Load(new Guid("f6f831df-0e77-4060-98d2-4f45b114750c"), 0L);
			if (botData == null)
			{
				PilotLogger.LogError("SendChatNotificationAsync", isVerbose: true, null, "Не удалось загрузить объект botData по GUID f6f831df-0e77-4060-98d2-4f45b114750c");
				throw new Exception("Не удалось загрузить данные бота");
			}
			string userNick = ((!botData.Attributes.ContainsKey("userNick")) ? "UnknownNick" : botData.Attributes["userNick"]?.ToString());
			string userName = ((!botData.Attributes.ContainsKey("UserName")) ? "UnknownName" : botData.Attributes["UserName"]?.ToString());
			PilotLogger.Log("botData Attributes: " + string.Join(", ", botData.Attributes.Select((KeyValuePair<string, object> kvp) => $"{kvp.Key}: {kvp.Value}")), "SendChatNotificationAsync", isVerbose: true);
			Console.WriteLine("userNick: " + userNick + ", userName: " + userName);
			Console.WriteLine("Original message: " + messageText);
			byte[] utf8Bytes = Encoding.UTF8.GetBytes(messageText);
			string processedMessage = Encoding.UTF8.GetString(utf8Bytes);
			if (processedMessage.Length > 2000)
			{
				processedMessage = processedMessage.Substring(0, 2000);
			}
			processedMessage = ProcessMessage(processedMessage);
			Console.WriteLine("Processed message: " + processedMessage);
			await SendMessageAsync(await CreateChatRoomAsync(chatName), processedMessage);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Не удалось отправить уведомление " + chatName + ": " + ex.Message, "Ошибка");
			throw;
		}
	}

	public string ProcessMessage(string message)
	{
		return message;
	}

	public async Task<string> CreateChatRoomAsync(string chatUserName)
	{
		if (string.IsNullOrWhiteSpace(chatUserName))
		{
			throw new ArgumentNullException("chatUserName", "Имя пользователя чата не может быть пустым.");
		}
		string apiEndpoint = "/api/v1/im.create";
		string requestBody = JsonConvert.SerializeObject(new
		{
			username = chatUserName
		});
		dynamic apiResponse = await ExecuteRocketChatApiRequestAsync(apiEndpoint, requestBody);
		if (apiResponse == null || apiResponse.success == null || !(bool)apiResponse.success)
		{
			string errorMessage = apiResponse?.error?.ToString() ?? "Неизвестная ошибка или пустой ответ";
			PilotLogger.LogError("CreateChatRoomAsync", isVerbose: true, null, "Не удалось создать комнату чата для " + chatUserName + ": " + errorMessage);
			throw new Exception("Не удалось создать чат: " + errorMessage);
		}
		string roomId = apiResponse.room?._id?.ToString();
		if (string.IsNullOrEmpty(roomId))
		{
			string errorMessage2 = ((apiResponse.room == null) ? "Поле room отсутствует в ответе" : "Поле _id отсутствует в room");
			PilotLogger.LogError("CreateChatRoomAsync", isVerbose: true, null, "ID комнаты недоступно для " + chatUserName + ": " + errorMessage2);
			throw new Exception("Не удалось получить ID комнаты чата: " + errorMessage2);
		}
		return roomId;
	}

	public async Task SendMessageAsync(string roomId, string messageText)
	{
		if (string.IsNullOrWhiteSpace(roomId))
		{
			throw new ArgumentNullException("roomId", "ID комнаты не может быть пустым.");
		}
		if (string.IsNullOrWhiteSpace(messageText))
		{
			throw new ArgumentNullException("messageText", "Текст сообщения не может быть пустым.");
		}
		var requestPayload = new
		{
			message = new
			{
				rid = roomId,
				msg = messageText
			}
		};
		string requestBody = JsonConvert.SerializeObject(requestPayload);
		dynamic apiResponse = await ExecuteRocketChatApiRequestAsync("/api/v1/chat.sendMessage", requestBody);
		if (!(bool)apiResponse.success)
		{
			string apiErrorMessage = apiResponse.error?.ToString() ?? "Неизвестная ошибка";
			PilotLogger.LogError("SendMessageAsync", isVerbose: true, null, "Ошибка отправки сообщения: " + apiErrorMessage);
			throw new Exception("Ошибка API Rocket.Chat: " + apiErrorMessage);
		}
	}

	public void SendEmailNotification(string recipientEmail, string messageBody)
	{
		string redirectEmailAddress = GetRedirectAddress("email");
		if (redirectEmailAddress != null)
		{
			recipientEmail = redirectEmailAddress;
		}
		try
		{
			MailMessage emailMessage = new MailMessage();
			SmtpClient smtpClient = new SmtpClient();
			try
			{
				emailMessage.From = new MailAddress("pilot-ice@tomsmineral.ru");
				emailMessage.To.Add(new MailAddress(recipientEmail));
				emailMessage.Subject = "Рассылка уведомлений";
				emailMessage.IsBodyHtml = true;
				emailMessage.Body = messageBody;
				smtpClient.Port = 587;
				smtpClient.Host = "mail.tomsmineral.ru";
				smtpClient.EnableSsl = true;
				smtpClient.UseDefaultCredentials = false;
				smtpClient.Credentials = new NetworkCredential("pilot-ice@tomsmineral.ru", "Sxk8uRfcWaxz7");
				smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
				smtpClient.Send(emailMessage);
			}
			finally
			{
				emailMessage.Dispose();
				smtpClient.Dispose();
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Ошибка отправки письма: " + ex.Message, "Ошибка");
			throw;
		}
	}

	private int getBossNum(string nameDep)
	{
		return depBoss[Array.IndexOf(depName, nameDep)];
	}

	public void RemoveVirtualRequests(Ascon.Pilot.SDK.IDataObject dataObject)
	{
		IFile currentFile = dataObject.ActualFileSnapshot.Files.FirstOrDefault((IFile f) => FileExtensionHelper.IsXpsAlike(f.Name));
		if (currentFile != null)
		{
			ISignatureModifier signatureBuilder = _modifier.Edit(dataObject).SetSignatures((IFile p) => FileExtensionHelper.IsXpsAlike(p.Name));
			signatureBuilder.Remove((ISignature s) => s.IsAdditional() && string.IsNullOrEmpty(s.Sign));
			_modifier.Apply();
		}
	}

	public async Task AddUser()
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		foreach (Guid userGuid in (await loader.Load(new Guid("d3496120-943a-4378-9641-e25787f74898"), 0L)).Children)
		{
			Ascon.Pilot.SDK.IDataObject userFile = await loader.Load(userGuid, 0L);
			int userId = (int)(long)userFile.Attributes["userId"];
			string userNick = userFile.Attributes["userNick"]?.ToString();
			string userName = userFile.Attributes["UserName"]?.ToString();
			user[userId] = new string[2] { userNick, userName };
		}
	}

	[Obsolete]
	public async void OnNext(INotification value)
	{
		AddUser();
		_ = value.NotificationName;
		value.ChangesetId();
		if ((DateTime.Now - timePastProjectCreate).TotalSeconds < 5.0)
		{
			return;
		}
		if (value.ChangeKind == NotificationKind.ObjectCreated && value.TypeId == 28)
		{
			timePastProjectCreate = DateTime.Now;
		}
		IEnumerable<IPerson> people = Repository.GetPeople();
		IEnumerable<IOrganisationUnit> orgStruc = Repository.GetOrganisationUnits();
		ObjectLoader loader = new ObjectLoader(Repository);
		Repository.GetCurrentPerson();
		Repository.GetDatabaseId();
		Repository.GetTypes();
		Ascon.Pilot.SDK.IDataObject obj = await loader.Load(value.ObjectId, 0L);
		if (user[94].Length < 1)
		{
			Ascon.Pilot.SDK.IDataObject folderUser = await loader.Load(new Guid("d3496120-943a-4378-9641-e25787f74898"), 0L);
			_ = (int)folderUser.Attributes["userId"];
			_ = (string)folderUser.Attributes["userNick"];
			_ = (string)folderUser.Attributes["UserName"];
		}
		Thread.Sleep(1);
		int curentUserPosition = Repository.GetCurrentPerson().MainPosition.Position;
		Repository.GetTypes();
		Thread.Sleep(1);
		string initiatorName = "";
		string initiatorNick = "";
		int executorId = 0;
		string executorName = "";
		string executorNick = "";
		string executorMail = "";
		orgStruc.ElementAt(108).Person();
		try
		{
			if (obj.Attributes.ContainsKey("initiator"))
			{
				int initiatorId = ((int[])obj.Attributes["initiator"]).First();
				initiatorName = user[int.Parse(initiatorId.ToString())][1];
				initiatorNick = user[int.Parse(initiatorId.ToString())][0];
				int person = orgStruc.ElementAt(initiatorId).Person();
				people.ElementAt(person - 1).Email();
			}
			if (obj.Attributes.ContainsKey("executor"))
			{
				executorId = ((int[])obj.Attributes["executor"]).First();
				executorName = user[int.Parse(executorId.ToString())][1];
				executorNick = user[int.Parse(executorId.ToString())][0];
				int person2 = orgStruc.ElementAt(executorId).Person();
				executorMail = people.ElementAt(person2 - 1).Email();
			}
		}
		catch (Exception)
		{
		}
		string ProjectDisplayName = "";
		if (value.TypeId == 25)
		{
			ObjectLoader objectLoader = loader;
			ObjectLoader objectLoader2 = loader;
			ObjectLoader objectLoader3 = objectLoader;
			ObjectLoader objectLoader4 = objectLoader2;
			await objectLoader3.Load((await objectLoader4.Load((await loader.Load(value.ObjectId, 0L)).ParentId, 0L)).ParentId, 0L);
		}
		if (this.inProject != null)
		{
			ProjectDisplayName = " В проекте: " + this.inProject.DisplayName;
		}
		if (value.ChangeKind == NotificationKind.ObjectAttributeChanged && (value.TypeId == 34 || value.TypeId == 35 || value.TypeId == 79 || value.TypeId == 38 || value.TypeId == 20))
		{
			int idCurrentUser = Repository.GetCurrentPerson().MainPosition.Position;
			try
			{
				if (!obj.Attributes.ContainsKey("responsible") || obj.Attributes["responsible"] == null)
				{
					MessageBox.Show("Не удалось добавить/удалить ответствнных за ведение документации");
				}
				else
				{
					for (int i = 0; i < obj.Access2.Count; i++)
					{
						if (obj.Access2.ElementAt(i).Access.IsInherited || ((int[])obj.Attributes["responsible"]).Contains(obj.Access2.ElementAt(i).OrgUnitId))
						{
							continue;
						}
						if (obj.Access2.ElementAt(i).RecordOwner != idCurrentUser && idCurrentUser != 6)
						{
							_ = obj.Access2.ElementAt(i).OrgUnitId;
							_ = obj.Access2.ElementAt(i).RecordOwner;
							obj = await loader.Load(value.ObjectId, 0L);
							List<int> responsibleFromAccess = new List<int>();
							foreach (IAccessRecord item in obj.Access2)
							{
								if (!item.Access.IsInherited)
								{
									responsibleFromAccess.Add(item.OrgUnitId);
								}
							}
							_modifier.EditById(obj.Id).SetAttribute("responsible", responsibleFromAccess.ToArray());
							_modifier.Apply();
							continue;
						}
						_modifier.EditById(obj.Id).RemoveAccessRights(obj.Access2.ElementAt(i).OrgUnitId);
						foreach (IRelation objRelColect in obj.Relations)
						{
							Ascon.Pilot.SDK.IDataObject objRel = await loader.Load(objRelColect.TargetId, 0L);
							if ((objRel.Type.Id == 2) | (objRel.Type.Id == 55))
							{
								_modifier.EditById(objRel.Id).RemoveAccessRights(obj.Access2.ElementAt(i).OrgUnitId);
							}
						}
						_modifier.Apply();
						SendChatNotificationAsync(user[obj.Access2.ElementAt(i).OrgUnitId][0], " Теперь вы не ответственны за ведение документации:[" + value.Title + "](http://192.168.10.5:5545/url?id=" + value.ObjectId.ToString() + ")", 1090);
					}
				}
			}
			catch
			{
			}
			try
			{
				int[] array = (int[])obj.Attributes["responsible"];
				int[] array2 = array;
				int[] array3 = array2;
				foreach (int userId in array3)
				{
					bool newUser = true;
					for (int k = 0; k < obj.Access2.Count; k++)
					{
						if (obj.Access2[k].OrgUnitId == userId)
						{
							newUser = false;
						}
					}
					if (!newUser)
					{
						continue;
					}
					_modifier.EditById(obj.Id).SetAccessRights(userId, AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
					foreach (IRelation objRelColect2 in obj.Relations)
					{
						Ascon.Pilot.SDK.IDataObject objRel2 = await loader.Load(objRelColect2.TargetId, 0L);
						if ((objRel2.Type.Id == 2) | (objRel2.Type.Id == 55))
						{
							_modifier.EditById(objRel2.Id).SetAccessRights(userId, AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
						}
					}
					_modifier.Apply();
					AddUser();
					SendChatNotificationAsync(user[userId][0], ":responsible: @" + user[idCurrentUser][0] + " назначил(а) вас ответственным за ведение документации:[" + value.Title + "](http://192.168.10.5:5545/url?id=" + value.ObjectId.ToString() + ")", 1122);
				}
			}
			catch (ArgumentException)
			{
				MessageBox.Show("Не удалось назначить ответственных");
				throw;
			}
		}
		if ((value.ChangeKind == NotificationKind.ObjectCreated) & (value.TypeId == 74))
		{
			ObjectLoader objectLoader5 = loader;
			ObjectLoader objectLoader6 = loader;
			ObjectLoader objectLoader7 = objectLoader5;
			ObjectLoader objectLoader8 = objectLoader6;
			Ascon.Pilot.SDK.IDataObject parentObj2 = await objectLoader7.Load((await objectLoader8.Load((await loader.Load(value.ObjectId, 0L)).ParentId, 0L)).ParentId, 0L);
			try
			{
				CreateCustomRelationFromGuids(value.ObjectId, parentObj2.Relations.First().TargetId);
			}
			catch
			{
			}
		}
		if ((value.ChangeKind == NotificationKind.ObjectCreated) & (value.TypeId == 29))
		{
			_modifier.EditById(value.ObjectId).SetAccessRights(((int[])obj.Attributes["GIP"]).First(), AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
			_modifier.Apply();
		}
		if ((value.ChangeKind == NotificationKind.ObjectCreated) & (value.TypeId == 28))
		{
			try
			{
				_modifier.EditById(value.ObjectId).MakePublic();
				_modifier.EditById(value.ObjectId).SetAccessRights(((int[])obj.Attributes["responsible_for_the_contract"]).First(), AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
				_modifier.EditById(value.ObjectId).SetAccessRights(((int[])obj.Attributes["Person_in_charge"]).First(), AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
				_modifier.Apply();
			}
			catch (Exception)
			{
				MessageBox.Show("не удалось назначить права ГИПу");
				throw;
			}
		}
		if ((value.ChangeKind == NotificationKind.ObjectCreated) & TypesForFolderCopy.Contains(value.TypeId))
		{
			Ascon.Pilot.SDK.IDataObject parentFolder = await loader.Load(obj.ParentId, 0L);
			string nameInStroage = obj.DisplayName;
			if (nameInStroage.Length > 50)
			{
				nameInStroage = obj.DisplayName.Substring(0, 50);
			}
			nameInStroage = nameInStroage.Replace("\\", ".").Replace("/", ".").Replace(":", ".")
				.Replace("*", ".")
				.Replace(",", ".")
				.Replace("?", ".")
				.Replace("\"", ".")
				.Replace("<", ".")
				.Replace(">", ".")
				.Replace("|", ".");
			while (nameInStroage[nameInStroage.Length - 1] == '.')
			{
				nameInStroage = nameInStroage.Substring(0, nameInStroage.Length - 1);
			}
			if (parentFolder.Type.Id != 28)
			{
				if (parentFolder.RelatedSourceFiles.Count() > 0)
				{
					Ascon.Pilot.SDK.IDataObject folderOnStorage = await loader.Load(parentFolder.RelatedSourceFiles.First(), 0L);
					IObjectBuilder newobj = _modifier.Create(folderOnStorage.Id, Repository.GetType(2)).SetAttribute("Title 4C281306-E329-423A-AF45-7B39EC30273F", nameInStroage);
					_modifier.Apply();
					CreateStorageLinkFromGuids(newobj.DataObject.Id, obj.Id);
				}
			}
			else
			{
				await loader.Load(obj.Id, 0L);
				IObjectBuilder newobj2 = _modifier.Create(obj.ParentId, Repository.GetType(2)).SetParent(obj.ParentId).SetAttribute("Title 4C281306-E329-423A-AF45-7B39EC30273F", nameInStroage);
				CreateStorageLinkFromGuids(newobj2.DataObject.Id, obj.Id);
				_modifier.Apply();
			}
		}
		if ((value.ChangeKind == NotificationKind.ObjectCreated) & TypesForFolderTask.Contains(value.TypeId))
		{
			int Stadia = 33;
			int folder_zadanie_sm_otd = 55;
			if (value.TypeId == 34)
			{
			}
			string[] stringSeparators = new string[1] { "; " };
			string[] ArNewFolder = value.Title.Replace("\"", "").Split(stringSeparators, StringSplitOptions.None);
			if (ArNewFolder.Length > 1)
			{
				Ascon.Pilot.SDK.IDataObject respon0 = await loader.Load(value.ObjectId, 0L);
				for (int l = 0; l < ArNewFolder.Length; l++)
				{
					Guid GuidRef = ((value.TypeId == 38) ? new Guid("134fe8cb-2bce-43ef-8538-fcd0b966fd1d") : new Guid("c3aa6d6e-ee80-461b-9707-87ee8e42636b"));
					foreach (Guid GuidsFodler in (await loader.Load(GuidRef, 0L)).Children)
					{
						foreach (Guid guidItemFolder in (await loader.Load(GuidsFodler, 0L)).Children)
						{
							Ascon.Pilot.SDK.IDataObject Sprav = await loader.Load(guidItemFolder, 0L);
							if ((Sprav.Attributes.ContainsKey("name") && (string)Sprav.Attributes["name"] == ArNewFolder[l]) | (Sprav.Attributes.ContainsKey("nameRazd") && (string)Sprav.Attributes["nameRazd"] == ArNewFolder[l]))
							{
								IObjectBuilder newObj = _modifier.Create(obj.ParentId, obj.Type);
								if (Sprav.Attributes.ContainsKey("Serial_number"))
								{
									newObj.SetAttribute("Serial_number", (string)Sprav.Attributes["Serial_number"]);
								}
								if (Sprav.Attributes.ContainsKey("number"))
								{
									newObj.SetAttribute("number", (string)Sprav.Attributes["number"]);
								}
								if (Sprav.Attributes.ContainsKey("number_book"))
								{
									newObj.SetAttribute("code", (string)Sprav.Attributes["number_book"]);
								}
								if (Sprav.Attributes.ContainsKey("code"))
								{
									newObj.SetAttribute("code", (string)Sprav.Attributes["code"]);
								}
								if (Sprav.Attributes.ContainsKey("name"))
								{
									newObj.SetAttribute("name", (string)Sprav.Attributes["name"]);
								}
								if (Sprav.Attributes.ContainsKey("nameRazd"))
								{
									newObj.SetAttribute("nameRazd", (string)Sprav.Attributes["nameRazd"]);
								}
								newObj.SetAttribute("responsible", (int[])respon0.Attributes["responsible"]);
								if (l == 0)
								{
									_modifier.Apply();
								}
							}
						}
					}
				}
				_modifier.DeleteById(value.ObjectId);
				_modifier.Apply();
			}
			if (value.TypeId == 33)
			{
				IObjectBuilder newObj2 = _modifier.Create(value.ObjectId, Repository.GetType(55));
				newObj2.SetAttribute("name", "Папка заданий смежного отдела");
				CreateCustomRelationFromGuids(obj.Id, newObj2.DataObject.Id);
				_modifier.Apply();
			}
			if (value.TypeId != 33 && Repository.GetType(value.TypeId).Children.Count != 0)
			{
				List<Ascon.Pilot.SDK.IDataObject> path = new List<Ascon.Pilot.SDK.IDataObject>();
				Ascon.Pilot.SDK.IDataObject parentObj3 = obj;
				path.Add(obj);
				do
				{
					parentObj3 = await loader.Load(parentObj3.ParentId, 0L);
					path.Add(parentObj3);
				}
				while (parentObj3.Type.Id != 33);
				path.Reverse();
				bool flagFind = false;
				do
				{
					foreach (Guid item2 in (await loader.Load(path[0].Id, 0L)).Children)
					{
						if ((await loader.Load(item2, 0L)).Type.Id == 55)
						{
							flagFind = true;
							break;
						}
					}
					if (flagFind)
					{
						break;
					}
					Thread.Sleep(10);
				}
				while (!flagFind);
			}
			new List<int>();
			Ascon.Pilot.SDK.IDataObject taskFolderTemplate = await loader.Load(new Guid("d59079b7-30f5-4cf8-a73f-fa2c63c498e0"), 0L);
			Ascon.Pilot.SDK.IDataObject newFolder = await loader.Load(value.ObjectId, 0L);
			Ascon.Pilot.SDK.IDataObject parentFolder2 = await loader.Load(newFolder.ParentId, 0L);
			if (value.TypeId == 35 || value.TypeId == 34 || value.TypeId == 38 || value.TypeId == 39)
			{
				string[] stringSeparator = new string[1] { "; " };
				string[] TitleArray = value.Title.Split(stringSeparator, StringSplitOptions.None);
				if (TitleArray.Length > 1)
				{
					_modifier.EditById(value.ObjectId).SetAttribute("name", TitleArray[0]);
				}
				if (parentFolder2.Type.Id == 33)
				{
					Guid taskFolder = new Guid("00000000-0000-0000-0000-000000000000");
					foreach (KeyValuePair<Guid, int> taskF in parentFolder2.TypesByChildren)
					{
						if (taskF.Value == 55)
						{
							taskFolder = taskF.Key;
							break;
						}
					}
					if (taskFolder == new Guid("00000000-0000-0000-0000-000000000000"))
					{
						IObjectBuilder newObj3 = _modifier.Create(parentFolder2.Id, taskFolderTemplate.Type);
						newObj3.SetAttribute("name", "Папка заданий смежного отдела");
						taskFolder = newObj3.DataObject.Id;
						_modifier.Apply();
					}
					IObjectBuilder newObjFolder = _modifier.Create(taskFolder, taskFolderTemplate.Type);
					CreateCustomRelationFromGuids(value.ObjectId, newObjFolder.DataObject.Id);
					newObjFolder.SetAttribute("name", value.Title);
					_modifier.Apply();
					try
					{
						if (newFolder.Attributes.ContainsKey("responsible"))
						{
							int[] array4 = (int[])newFolder.Attributes["responsible"];
							int[] array5 = array4;
							int[] array6 = array5;
							foreach (int userId2 in array6)
							{
								_modifier.EditById(newFolder.Id).SetAccessRights(userId2, AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
								_modifier.EditById(newObjFolder.DataObject.Id).SetAccessRights(userId2, AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
								_modifier.Apply();
							}
						}
					}
					catch
					{
						MessageBox.Show("Не удалось назначить ответственных для папки: " + newFolder.Id.ToString());
					}
				}
				else
				{
					if (!newFolder.Attributes.ContainsKey("responsible") || ((int[])newFolder.Attributes["responsible"]).Count() == 0)
					{
						return;
					}
					int[] array7 = (int[])newFolder.Attributes["responsible"];
					int[] array8 = array7;
					int[] array9 = array8;
					foreach (int userId3 in array9)
					{
						try
						{
							_modifier.EditById(newFolder.Id).SetAccessRights(userId3, AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
							_modifier.Apply();
						}
						catch (Exception)
						{
							throw;
						}
					}
				}
			}
			if (value.TypeId != 79)
			{
				try
				{
					if (value.Title.Contains(";"))
					{
						return;
					}
					List<Ascon.Pilot.SDK.IDataObject> treeFolder = new List<Ascon.Pilot.SDK.IDataObject>();
					Ascon.Pilot.SDK.IDataObject curentfolder = await loader.Load(value.ObjectId, 0L);
					do
					{
						treeFolder.Add(curentfolder);
						curentfolder = await loader.Load(curentfolder.ParentId, 0L);
					}
					while (curentfolder.Type.Id != Stadia);
					treeFolder.Add(curentfolder);
					treeFolder.Reverse();
					Ascon.Pilot.SDK.IDataObject[] treeFolderArray = treeFolder.ToArray();
					Ascon.Pilot.SDK.IDataObject folderStadia = curentfolder;
					Ascon.Pilot.SDK.IDataObject curentFolderTask = curentfolder;
					try
					{
						foreach (Guid checkFolderTask in folderStadia.Children)
						{
							Ascon.Pilot.SDK.IDataObject obj5 = await loader.Load(checkFolderTask, 0L);
							if (obj5.Type.Id == folder_zadanie_sm_otd)
							{
								curentFolderTask = obj5;
								break;
							}
						}
					}
					catch
					{
						MessageBox.Show("В папке стадия нет папки задание");
						return;
					}
					try
					{
						for (int num = 1; num < treeFolderArray.Length; num++)
						{
							if (curentFolderTask.Children.Count == 0)
							{
								if (!TypesForFolderTask.Contains(treeFolderArray[num].Type.Id))
								{
									continue;
								}
								IObjectBuilder newObj4 = _modifier.Create(curentFolderTask, curentFolderTask.Type);
								newObj4.SetAttribute("name", treeFolderArray[num].DisplayName.ToString());
								_modifier.Apply();
								CreateCustomRelationFromGuids(newObj4.DataObject.Id, treeFolderArray[num].Id);
								curentFolderTask = await loader.Load(newObj4.DataObject.Id, 0L);
							}
							foreach (Guid guidChild in curentFolderTask.Children)
							{
								Ascon.Pilot.SDK.IDataObject child = await loader.Load(guidChild, 0L);
								if (child.Attributes["name"].ToString() == treeFolderArray[num].DisplayName)
								{
									curentFolderTask = child;
									break;
								}
								if (guidChild == curentFolderTask.Children.Last() && TypesForFolderTask.Contains(treeFolderArray[num].Type.Id))
								{
									IObjectBuilder newObj5 = _modifier.Create(curentFolderTask, curentFolderTask.Type);
									newObj5.SetAttribute("name", treeFolderArray[num].DisplayName.ToString());
									int[] array10 = (int[])newFolder.Attributes["responsible"];
									int[] array11 = array10;
									int[] array12 = array11;
									foreach (int userId4 in array12)
									{
										_modifier.EditById(newObj5.DataObject.Id).SetAccessRights(userId4, AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
									}
									_modifier.Apply();
									CreateCustomRelationFromGuids(newObj5.DataObject.Id, treeFolderArray[num].Id);
								}
							}
						}
					}
					catch
					{
					}
				}
				catch
				{
				}
			}
		}
		if ((value.ChangeKind == NotificationKind.ObjectDeleted) & TypesForFolderTask.Contains(value.TypeId))
		{
			try
			{
				if (obj.Relations.Count != 0)
				{
					_modifier.DeleteById(obj.Relations.First().TargetId);
					_modifier.Apply();
				}
			}
			catch
			{
			}
		}
		if (value.ChangeKind == NotificationKind.ObjectSignatureChanged)
		{
			foreach (IRelation rel in (await loader.Load(value.ObjectId, 0L)).Relations)
			{
				Ascon.Pilot.SDK.IDataObject task = await loader.Load(rel.TargetId, 0L);
				if (task.Attributes.ContainsKey("executor") && ((int[])task.Attributes["executor"]).Contains(curentUserPosition) && task.Attributes.ContainsKey("state") && (Guid)task.Attributes["state"] == state_awaitingSignature)
				{
					if (!task.Attributes.ContainsKey("current_sub") && task.Attributes["current_sub"] == null)
					{
						_modifier.EditById(task.Id).SetAttribute("current_sub", 0);
					}
					if (!task.Attributes.ContainsKey("need_sub") && task.Attributes["need_sub"] == null)
					{
						_modifier.EditById(task.Id).SetAttribute("need_sub", 0);
					}
					_modifier.Apply();
					_modifier.EditById(task.Id).SetAttribute("current_sub", (long)task.Attributes["current_sub"] + 1);
					_modifier.Apply();
					task = await loader.Load(rel.TargetId, 0L);
					if ((long)task.Attributes["need_sub"] == (long)task.Attributes["current_sub"])
					{
						_modifier.EditById(task.Id).SetAttribute("state", state_signed);
					}
					_modifier.Apply();
				}
			}
		}
		if (value.ChangeKind == NotificationKind.ObjectCreated)
		{
			Ascon.Pilot.SDK.IDataObject ttask = await loader.Load(value.ObjectId, 0L);
			if (ttask.Type.Name == "task_agreement_doc")
			{
				ObjectLoader objectLoader9 = loader;
				ObjectLoader objectLoader10 = objectLoader9;
				Ascon.Pilot.SDK.IDataObject twork = await objectLoader10.Load((await loader.Load(ttask.ParentId, 0L)).ParentId, 0L);
				if (ttask.ParentId == twork.Children.First())
				{
					_modifier.EditById(twork.Id).SetAttribute("state", new Guid("11748395-9a9f-48cd-92ef-7a9d9f776ecd"));
					_modifier.EditById(value.ObjectId).SetAttribute("state", state_awaitingSignature);
					_modifier.Apply();
				}
			}
			if (value.TypeId == 106)
			{
			}
			if (value.TypeId == 28)
			{
				return;
			}
			await loader.Load(value.ObjectId, 0L);
			if (value.TypeId == 97)
			{
				_modifier.EditById(value.ObjectId).SetAttribute("state", new Guid("11748395-9a9f-48cd-92ef-7a9d9f776ecd"));
				_modifier.Apply();
			}
			if (value.TypeId == 99)
			{
				_modifier.EditById(value.ObjectId).SetAttribute("state", new Guid("dfa42efa-2748-4320-a6a2-594d1e24ead7"));
				_modifier.Apply();
				SendChatNotificationAsync(executorNick, initiatorName + " отправил(а) вам задание гипа: [" + value.Title + "](piloturi://" + value.ObjectId.ToString() + ")", 1576);
				if (executorId == 32)
				{
					SendChatNotificationAsync("zelenskiy", initiatorName + " отправил(а) вам задание гипа: [" + value.Title + "](piloturi://" + value.ObjectId.ToString() + ")", 1579);
				}
			}
			if (value.TypeId == 20)
			{
				Thread.Sleep(10);
				_modifier.EditById(value.ObjectId).SetAttribute("state", new Guid("dfa42efa-2748-4320-a6a2-594d1e24ead7"));
				Ascon.Pilot.SDK.IDataObject parent = await loader.Load(value.ObjectId, 0L);
				if (parent.Type.Id == 25)
				{
					ObjectLoader objectLoader11 = loader;
					ObjectLoader objectLoader12 = objectLoader11;
					Ascon.Pilot.SDK.IDataObject work = await objectLoader12.Load((await loader.Load(parent.ParentId, 0L)).ParentId, 0L);
					_modifier.EditById(work.Id).SetAccessRights(executorId, AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
				}
				_modifier.EditById(value.ObjectId).SetAccessRights(executorId, AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
				_modifier.Apply();
				SendChatNotificationAsync(executorNick, ":assigned: @" + initiatorNick + " отправил(а) вам задание: [" + obj.DisplayName + "](piloturi://" + obj.Id.ToString() + ") . " + ProjectDisplayName, 1596);
				SendEmailNotification(executorMail, initiatorName + " отправил(а) вам задание: <a href='piloturi://" + obj.Id.ToString() + "'>" + obj.DisplayName + "  " + ProjectDisplayName + "</a>");
				try
				{
					ObjectLoader objectLoader13 = loader;
					ObjectLoader objectLoader14 = loader;
					ObjectLoader objectLoader15 = objectLoader13;
					ObjectLoader objectLoader16 = objectLoader14;
					Ascon.Pilot.SDK.IDataObject folderDoc = await objectLoader15.Load((await objectLoader16.Load((await loader.Load(value.ObjectId, 0L)).Relations.First().TargetId, 0L)).Relations.First().TargetId, 0L);
					int[] oldRespUsers = (int[])folderDoc.Attributes["responsible"];
					int[] newRespUsers = new int[oldRespUsers.Length + 1];
					oldRespUsers.CopyTo(newRespUsers, 0);
					newRespUsers[oldRespUsers.Length] = executorId;
					Ascon.Pilot.SDK.IDataObject parentTask = await loader.Load(obj.ParentId, 0L);
					_modifier.EditById(folderDoc.Id).SetAttribute("responsible", newRespUsers.Distinct().ToArray());
					_modifier.EditById(obj.Id).SetAccessRights(((int[])obj.Attributes["executor"]).First(), AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
					_modifier.EditById(obj.Id).SetAccessRights(((int[])parentTask.Attributes["initiator"]).First(), AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
					_modifier.Apply();
				}
				catch
				{
				}
			}
			if (value.TypeId == 95)
			{
				Ascon.Pilot.SDK.IDataObject task2 = await loader.Load(value.ObjectId, 0L);
				ObjectLoader objectLoader17 = loader;
				ObjectLoader objectLoader18 = objectLoader17;
				if ((await objectLoader18.Load((await loader.Load(task2.ParentId, 0L)).ParentId, 0L)).Children.First() == task2.ParentId)
				{
					_modifier.EditById(task2.Id).SetAttribute("state", state_awaitingSignature);
					_modifier.Apply();
				}
			}
			if (value.TypeId == 93)
			{
				_modifier.EditById(value.ObjectId).SetAttribute("state", new Guid("11748395-9a9f-48cd-92ef-7a9d9f776ecd"));
				_modifier.Apply();
			}
			if (value.TypeId == 84 || value.TypeId == 23 || value.TypeId == 87)
			{
				Ascon.Pilot.SDK.IDataObject agreement = await loader.Load(value.ObjectId, value.ChangesetId());
				_modifier.EditById(agreement.Id).SetAttribute("state", new Guid("11748395-9a9f-48cd-92ef-7a9d9f776ecd"));
				int[] initiator = new int[1] { Repository.GetCurrentPerson().MainPosition.Position };
				_modifier.EditById(value.ObjectId).SetAttribute("initiator", initiator);
				_modifier.Apply();
				if (value.TypeId == 27)
				{
					_modifier.EditById(value.ObjectId).SetAttribute("state", new Guid("dfa42efa-2748-4320-a6a2-594d1e24ead7"));
					_modifier.Apply();
				}
			}
			if (value.TypeId == 85)
			{
				Ascon.Pilot.SDK.IDataObject task3 = await loader.Load(value.ObjectId, value.ChangesetId());
				Ascon.Pilot.SDK.IDataObject order = await loader.Load(task3.ParentId, 0L);
				if ((await loader.Load(order.ParentId, 0L)).Children.First() == order.Id)
				{
					_modifier.EditById(task3.Id).SetAttribute("state", new Guid("dfa42efa-2748-4320-a6a2-594d1e24ead7"));
					_modifier.Apply();
				}
			}
			if (value.TypeId == 21)
			{
				try
				{
					Ascon.Pilot.SDK.IDataObject tehTask = await loader.Load(value.ObjectId, value.ChangesetId());
					ObjectLoader objectLoader19 = loader;
					ObjectLoader objectLoader20 = objectLoader19;
					await objectLoader20.Load((await loader.Load(tehTask.Children.First(), 0L)).Children.First(), 0L);
					_modifier.EditById(tehTask.Id).SetAttribute("executives", new int[3] { 19, 87, 28 });
					_modifier.EditById(tehTask.Id).SetAccessRights(28, AccessLevel.View, DateTime.MaxValue, isInheritable: true);
					_modifier.Apply();
					_ = ":assigned: @" + initiatorNick + " создал(а) процесс: [" + tehTask.DisplayName + "](piloturi://" + tehTask.Id.ToString() + ")." + ProjectDisplayName;
				}
				catch (Exception)
				{
					throw;
				}
			}
			if (value.TypeId == 74)
			{
				try
				{
					ObjectLoader objectLoader21 = loader;
					ObjectLoader objectLoader22 = objectLoader21;
					CreateCustomRelationFromGuids(SecondObjectId: (await objectLoader22.Load((await loader.Load(obj.ParentId, 0L)).ParentId, 0L)).Relations.First().TargetId, FirstObjectId: obj.Id);
				}
				catch
				{
					throw;
				}
			}
			if (value.TypeId == 26)
			{
				return;
			}
			try
			{
				if (value.TypeId == 25)
				{
					Ascon.Pilot.SDK.IDataObject thisTask = await loader.Load(value.ObjectId, 0L);
					if ((await loader.Load(thisTask.ParentId, 0L)).Attributes["order"].ToString() == "1")
					{
						string title1 = thisTask.DisplayName;
						string text1 = ":assigned: @" + initiatorNick + " отправил(а) вам задание [" + title1 + "](piloturi://" + thisTask.Id.ToString() + "). " + ProjectDisplayName;
						SendChatNotificationAsync(executorNick, text1, 1763);
					}
					_ = ":assigned: @" + initiatorNick + " назначил вас исполнителем задания " + value.Title.ToString() + " piloturi://" + value.ObjectId.ToString();
					Ascon.Pilot.SDK.IDataObject obj12 = await loader.Load(obj.ParentId, value.ChangesetId());
					Ascon.Pilot.SDK.IDataObject obj13 = await loader.Load(obj12.ParentId, value.ChangesetId());
					ObjectLoader objectLoader23 = loader;
					ObjectLoader objectLoader24 = objectLoader23;
					Ascon.Pilot.SDK.IDataObject task_approval = await objectLoader24.Load(new Guid((await loader.Load(new Guid(obj13.Children[0].ToString()), 0L)).Children.First().ToString()), 0L);
					ObjectLoader objectLoader25 = loader;
					ObjectLoader objectLoader26 = objectLoader25;
					await objectLoader26.Load(new Guid((await loader.Load(new Guid(obj13.Children[0].ToString()), 0L)).Children.First().ToString()), 0L);
					Ascon.Pilot.SDK.IDataObject task4 = await loader.Load(obj.Relations.First().TargetId, 0L);
					if (obj13.Children.Count == 4)
					{
						ObjectLoader objectLoader27 = loader;
						ObjectLoader objectLoader28 = objectLoader27;
						Ascon.Pilot.SDK.IDataObject obj14 = await objectLoader28.Load((await loader.Load(obj13.Children[3], 0L)).Children[0], 0L);
						_modifier.EditById(task4.Id).SetAccessRights(((int[])obj14.Attributes["executor"]).First(), AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
						_modifier.Apply();
					}
					if (task_approval.ParentId == obj13.Children[0])
					{
						_modifier.EditById(task_approval.Id).SetAttribute("state", new Guid("dfa42efa-2748-4320-a6a2-594d1e24ead7"));
						_modifier.Apply();
						_modifier.EditById(obj13.Id).SetAttribute("state", new Guid("11748395-9a9f-48cd-92ef-7a9d9f776ecd"));
						_modifier.Apply();
					}
					if (!obj13.Attributes.ContainsKey("auditors"))
					{
						_modifier.EditById(obj13.Id).SetAttribute("auditors", new int[1] { ((int[])task_approval.Attributes["executor"]).First() });
						_modifier.Apply();
					}
					obj13 = await loader.Load(obj12.ParentId, value.ChangesetId());
					int[] auditors = (int[])obj13.Attributes["auditors"];
					int[] newAuditors = new int[auditors.Length + 1];
					auditors.CopyTo(newAuditors, 0);
					int[] actualFor = new int[4]
					{
						newAuditors[newAuditors.Length - 1] = ((int[])task_approval.Attributes["executor"]).First(),
						0,
						0,
						0
					};
					_modifier.EditById(obj13.Id).SetAttribute("auditors", newAuditors);
					_modifier.Apply();
					if (obj13.Children.Count == 4)
					{
						for (int num3 = 1; num3 < obj13.Children.Count; num3++)
						{
							int[] array13 = actualFor;
							int num4 = num3;
							ObjectLoader objectLoader29 = loader;
							int[] array14 = array13;
							int num5 = num4;
							ObjectLoader objectLoader30 = objectLoader29;
							array14[num5] = ((int[])(await objectLoader30.Load((await loader.Load(obj13.Children[num3], 0L)).Children[0], 0L)).Attributes["executor"]).First();
						}
						_modifier.EditById(obj13.Id).SetAttribute("actualFor", actualFor);
						_modifier.Apply();
					}
					int[] newInitiator = new int[1] { 87 };
					for (int num6 = 1; num6 < obj13.Children.Count(); num6++)
					{
						Ascon.Pilot.SDK.IDataObject Order = await loader.Load(new Guid(obj13.Children[num6].ToString()), 0L);
						int[] array15 = newInitiator;
						ObjectLoader objectLoader31 = loader;
						int[] array16 = array15;
						ObjectLoader objectLoader32 = objectLoader31;
						array16[0] = ((int[])(await objectLoader32.Load(new Guid((await loader.Load(new Guid(obj13.Children[num6 - 1].ToString()), 0L)).Children.First().ToString()), 0L)).Attributes["executor"]).First();
						if (num6 == 3)
						{
							task4 = await loader.Load(new Guid(Order.Children.First().ToString()), 0L);
							_modifier.EditById(task4.Id).SetAccessRights(((int[])obj.Attributes["executor"]).First(), AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
							_modifier.Apply();
						}
						foreach (Guid ii in Order.Children)
						{
							task4 = await loader.Load(ii, 0L);
							_modifier.EditById(task4.Id).SetAttribute("initiator", newInitiator);
							_modifier.Apply();
							do
							{
								Thread.Sleep(10);
							}
							while (((int[])(await loader.Load(task4.Id, 0L)).Attributes["initiator"]).First() != newInitiator.First());
						}
					}
					return;
				}
			}
			catch (Exception)
			{
			}
			if (value.TypeId == 27)
			{
				_modifier.EditById(value.ObjectId).SetAttribute("state", taskAssign);
				_modifier.Apply();
				SendChatNotificationAsync(executorNick, ":inprogress: @" + initiatorNick + " отправила(а) на ознакомление вам документ [" + obj.DisplayName + "](piloturi://" + obj.Id.ToString() + "). " + ProjectDisplayName, 1854);
			}
		}
		if (value.ChangeKind == NotificationKind.ObjectAttributeChanged)
		{
			bool flag = value.TypeId == 108;
			bool flag2 = flag;
			if (flag2)
			{
				flag2 = (Guid)(await loader.Load(value.ObjectId, 0L)).Attributes["state"] == state_awaitingSignature;
			}
			if (flag2)
			{
				await loader.Load(new Guid("1ccea07e-4712-4119-aaff-37748e1eb31e"), 0L);
			}
			if (value.TypeId == 74)
			{
				string storage_Task_template = Repository.GetStoragePath(new Guid("6e926d2b-40-49ec-b788-79315896dbf4"));
				if (storage_Task_template == null)
				{
					Repository.Mount(new Guid("6e926d2b-4081-49ec-b788-79315896dbf4"));
					storage_Task_template = Repository.GetStoragePath(new Guid("6e926d2b-4081-49ec-b788-79315896dbf4"));
				}
				string storache_New_Task = Repository.GetStoragePath(value.ObjectId) + "\\Задание.docx";
				if (!File.Exists(storache_New_Task))
				{
					storage_Task_template += "\\Задание.docx";
					File.Copy(storage_Task_template, storache_New_Task);
				}
			}
			if (value.TypeId == 54)
			{
				foreach (Guid childInFolder in (await loader.Load(obj.ParentId, 0L)).Children)
				{
					if ((await loader.Load(childInFolder, 0L)).Type.Name == "task")
					{
						return;
					}
				}
			}
			if (value.TypeId == 23)
			{
				Ascon.Pilot.SDK.IDataObject psdWorflow = await loader.Load(value.ObjectId, 0L);
				if ((Guid)psdWorflow.Attributes["state"] == state_delete)
				{
					foreach (Guid guidStage in psdWorflow.Children)
					{
						ObjectLoader objectLoader33 = loader;
						ObjectLoader objectLoader34 = objectLoader33;
						Ascon.Pilot.SDK.IDataObject cTask = await objectLoader34.Load((await loader.Load(guidStage, 0L)).Children.First(), 0L);
						_modifier.EditById(cTask.Id).SetAttribute("state", state_delete);
					}
					_modifier.Apply();
				}
			}
			Thread.Sleep(10);
			if (value.TypeId == 95)
			{
				Ascon.Pilot.SDK.IDataObject task5 = await loader.Load(value.ObjectId, value.ChangesetId());
				ObjectLoader objectLoader35 = loader;
				ObjectLoader objectLoader36 = objectLoader35;
				Ascon.Pilot.SDK.IDataObject workflow = await objectLoader36.Load((await loader.Load(task5.ParentId, 0L)).ParentId, 0L);
				string text2 = task5.Attributes["state"].ToString();
				string text3 = text2;
				if (text3 == "a74e2bb3-b89c-466f-b994-b04b54dd9779")
				{
					if (task5.ParentId == workflow.Children[0])
					{
						Ascon.Pilot.SDK.IDataObject task6 = await loader.Load(workflow.Children[1], 0L);
						_modifier.EditById(task6.Id).SetAttribute("state", state_awaitingSignature);
						_modifier.Apply();
					}
					if (task5.ParentId == workflow.Children[workflow.Children.Count - 1])
					{
						await loader.Load(workflow.Children[1], 0L);
						_modifier.EditById(workflow.Id).SetAttribute("state", state_awaitingSignature);
						_modifier.Apply();
					}
				}
			}
			if (value.TypeId == 93)
			{
				MessageBox.Show((await loader.Load(value.ObjectId, 0L)).Attributes["state"].ToString(), "workflow");
			}
			if (value.TypeId == 20)
			{
				switch (obj.Attributes["state"].ToString())
				{
				case "bf2f17dc-8b0c-4723-a7a1-e394e9330dc8":
					SendChatNotificationAsync(initiatorNick, ":inprogress: @" + executorNick + " приступил(а) к выполнению задания [" + obj.DisplayName + "](piloturi://" + obj.Id.ToString() + "). " + ProjectDisplayName, 1960);
					break;
				case "a0068698-a2c3-4b5f-8504-e0d590bf49c8":
					SendChatNotificationAsync(initiatorNick, ":noremarks: @" + executorNick + " завершила(а) задание [" + obj.DisplayName + "](piloturi://" + obj.Id.ToString() + ") и оно ожидает вашей проверки." + ProjectDisplayName, 1963);
					break;
				case "dfa42efa-2748-4320-a6a2-594d1e24ead7":
					SendChatNotificationAsync(executorNick, ":assigned: @" + initiatorNick + " вернул(а) вам задание [" + obj.DisplayName + "](piloturi://" + obj.Id.ToString() + ")." + ProjectDisplayName, 1966);
					break;
				case "149a520c-523a-404a-a1ac-07973c6b23bd":
					SendChatNotificationAsync(executorNick, ":noremarks: @" + initiatorNick + " принял(а) задание [" + obj.DisplayName + "](piloturi://" + obj.Id.ToString() + ")." + ProjectDisplayName, 1969);
					break;
				case "abdbe49a-7094-4084-9673-eb5fb3f9526":
					SendChatNotificationAsync(executorNick, ":revoked: @" + initiatorNick + " отозавал(а) задание [" + obj.DisplayName + "](piloturi://" + obj.Id.ToString() + ") оно больше не актуально." + ProjectDisplayName, 1972);
					break;
				case "46ac47c0-d1b5-4759-86a5-2030ac9cc586":
					SendChatNotificationAsync(initiatorNick, ":noremarks: @" + executorNick + " выполнил(а) задание [" + obj.DisplayName + "](piloturi://" + obj.Id.ToString() + ") с замечаниями." + ProjectDisplayName, 1975);
					break;
				}
			}
			if (value.TypeId == 87)
			{
				Ascon.Pilot.SDK.IDataObject workflowFamiliar = await loader.Load(value.ObjectId, 0L);
				if (workflowFamiliar.Attributes["state"].ToString() == "abdbe49a-7094-4084-9673-eb5fb3f95262")
				{
					foreach (Guid familiar in (await loader.Load(workflowFamiliar.Children.First(), 0L)).Children)
					{
						_modifier.EditById(familiar).SetAttribute("state", new Guid("abdbe49a-7094-4084-9673-eb5fb3f95262"));
					}
					_modifier.Apply();
				}
			}
			if (value.TypeId == 27)
			{
				ObjectLoader objectLoader37 = loader;
				ObjectLoader objectLoader38 = objectLoader37;
				Ascon.Pilot.SDK.IDataObject stage = await objectLoader38.Load((await loader.Load(value.ObjectId, 0L)).ParentId, 0L);
				bool AllFamiliar = true;
				foreach (Guid children in stage.Children)
				{
					if ((await loader.Load(children, 0L)).Attributes["state"].ToString() != "c1354e0d-19db-46b1-93fd-c6e5fd059f1c")
					{
						AllFamiliar = false;
					}
				}
				if (AllFamiliar)
				{
					_modifier.EditById(stage.ParentId).SetAttribute("state", new Guid("69042a68-29c1-494b-aecf-ce2857cb8098"));
					_modifier.Apply();
				}
			}
			if (value.TypeId == 85)
			{
				Ascon.Pilot.SDK.IDataObject task7 = await loader.Load(value.ObjectId, value.ChangesetId());
				if (task7.Attributes["state"].ToString() == "dfa42efa-2748-4320-a6a2-594d1e24ead7")
				{
					return;
				}
				Ascon.Pilot.SDK.IDataObject order2 = await loader.Load(task7.ParentId, 0L);
				Ascon.Pilot.SDK.IDataObject agreement2 = await loader.Load(order2.ParentId, 0L);
				int orderId = agreement2.Children.IndexOf(order2.Id);
				switch (task7.Attributes["state"].ToString())
				{
				case "e69243ab-e1a9-4539-ade4-28af447eb634":
					if (agreement2.Children.Last() != order2.Id)
					{
						ObjectLoader objectLoader41 = loader;
						ObjectLoader objectLoader42 = objectLoader41;
						Ascon.Pilot.SDK.IDataObject nextTask2 = await objectLoader42.Load((await loader.Load(agreement2.Children[orderId + 1], 0L)).Children.First(), 0L);
						_modifier.EditById(nextTask2.Id).SetAttribute("state", new Guid("dfa42efa-2748-4320-a6a2-594d1e24ead7"));
						_modifier.Apply();
						string text6 = ":noremarks: @" + executorNick + " согласовал(а) документ: [" + task7.DisplayName + "](piloturi://" + agreement2.Id.ToString() + ")." + ProjectDisplayName;
						SendChatNotificationAsync(initiatorNick, text6, 2027);
					}
					else
					{
						_modifier.EditById(agreement2.Id).SetAttribute("state", new Guid("69042a68-29c1-494b-aecf-ce2857cb8098"));
						_modifier.Apply();
						if (agreement2.Children.Count > 0)
						{
							string text7 = ":noremarks: @" + initiatorNick + " завершил(а) последний этап согласования документа: [" + task7.DisplayName + "](piloturi://" + agreement2.Id.ToString() + ")" + ProjectDisplayName;
							SendChatNotificationAsync(initiatorNick, text7, 2036);
						}
					}
					break;
				case "bf2f17dc-8b0c-4723-a7a1-e394e9330dc8":
					SendChatNotificationAsync(initiatorNick, ":inprogress: @" + executorNick + " приступил(а) к ознакомлению документа: [" + task7.DisplayName + "](piloturi://" + agreement2.Id.ToString() + ") " + ProjectDisplayName, 2041);
					break;
				case "dfa42efa-2748-4320-a6a2-594d1e24ead7":
					SendChatNotificationAsync(executorNick, ":assigned: @" + initiatorNick + " отправил(а) вам документ на согласование: [" + task7.DisplayName + "](piloturi://" + task7.Id.ToString() + ") " + ProjectDisplayName, 2044);
					break;
				case "ca50bfc9-0d90-4573-a9b6-9a28f48ec02c":
					SendChatNotificationAsync(initiatorNick, ":notapproved: @" + executorNick + " отправил(а) вам на доработку документ [" + task7.DisplayName + "](piloturi://" + agreement2.Id.ToString() + ") " + ProjectDisplayName, 2047);
					break;
				case "abdbe49a-7094-4084-9673-eb5fb3f95262":
					_modifier.EditById(agreement2.Id).SetAttribute("state", new Guid("abdbe49a-7094-4084-9673-eb5fb3f95262"));
					_modifier.Apply();
					break;
				case "2f1e55d8-ea8d-41d7-be0a-0f986c4fc9e1":
					if (agreement2.Children.Last() != order2.Id)
					{
						ObjectLoader objectLoader39 = loader;
						ObjectLoader objectLoader40 = objectLoader39;
						Ascon.Pilot.SDK.IDataObject nextTask = await objectLoader40.Load((await loader.Load(agreement2.Children[orderId + 1], 0L)).Children.First(), 0L);
						_modifier.EditById(nextTask.Id).SetAttribute("state", new Guid("dfa42efa-2748-4320-a6a2-594d1e24ead7"));
						_modifier.Apply();
						string text4 = ":noremarks: @" + initiatorNick + " согласовал(а) ПСД документ: [" + task7.DisplayName + "](piloturi://" + agreement2.Id.ToString() + ") " + ProjectDisplayName;
						SendChatNotificationAsync(initiatorNick, text4, 2061);
						break;
					}
					_modifier.EditById(agreement2.Id).SetAttribute("state", new Guid("69042a68-29c1-494b-aecf-ce2857cb8098"));
					_modifier.Apply();
					if (agreement2.Children.Count > 0)
					{
						(new int[1])[0] = 1;
						_modifier.EditById(agreement2.Id).SetAttribute("actualFor", "");
						_modifier.Apply();
						string text5 = ":noremarks: @" + initiatorNick + " завершил(а) последний этап согласования псд документа: [" + task7.DisplayName + "](piloturi://" + agreement2.Id.ToString() + ") " + ProjectDisplayName;
						SendChatNotificationAsync(initiatorNick, text5, 2073);
					}
					break;
				}
			}
			if (value.TypeId == 84)
			{
				Ascon.Pilot.SDK.IDataObject agreement3 = await loader.Load(value.ObjectId, value.ChangesetId());
				if (agreement3.Attributes["state"].ToString() == "abdbe49a-7094-4084-9673-eb5fb3f95262")
				{
					foreach (Guid agreementChildren in agreement3.Children)
					{
						foreach (Guid orderChildren in (await loader.Load(agreementChildren, 0L)).Children)
						{
							Ascon.Pilot.SDK.IDataObject task8 = await loader.Load(orderChildren, 0L);
							_modifier.EditById(task8.Id).SetAttribute("state", new Guid("abdbe49a-7094-4084-9673-eb5fb3f95262"));
							_modifier.Apply();
						}
					}
				}
			}
			if (value.TypeId == 27)
			{
				ObjectLoader objectLoader43 = loader;
				ObjectLoader objectLoader44 = objectLoader43;
				Ascon.Pilot.SDK.IDataObject stage2 = await objectLoader44.Load((await loader.Load(value.ObjectId, 0L)).ParentId, 0L);
				foreach (Guid chekTask in stage2.Children)
				{
					if ((await loader.Load(chekTask, 0L)).Attributes["state"].ToString() != "d8ae8c3a-6f46-45d2-835b-563fe2b47acd")
					{
						return;
					}
				}
				_modifier.EditById(stage2.ParentId).SetAttribute("state", new Guid("d8ae8c3a-6f46-45d2-835b-563fe2b47acd"));
				_modifier.EditById(stage2.ParentId).SetAttribute("actualFor", "");
				_modifier.Apply();
			}
			if (value.TypeId == 25)
			{
				ObjectLoader objectLoader45 = loader;
				ObjectLoader objectLoader46 = loader;
				ObjectLoader objectLoader47 = objectLoader45;
				ObjectLoader objectLoader48 = objectLoader46;
				Ascon.Pilot.SDK.IDataObject folderDoc2 = await objectLoader47.Load((await objectLoader48.Load((await loader.Load(value.ObjectId, 0L)).Relations.First().TargetId, 0L)).Relations.First().TargetId, 0L);
				int[] oldRespUsers2 = new int[0];
				if (folderDoc2.Attributes.ContainsKey("responsible"))
				{
					oldRespUsers2 = (int[])folderDoc2.Attributes["responsible"];
				}
				int[] newRespUsers2 = new int[oldRespUsers2.Length + 1];
				oldRespUsers2.CopyTo(newRespUsers2, 0);
				newRespUsers2[oldRespUsers2.Length] = executorId;
				_modifier.EditById(folderDoc2.Id).SetAttribute("responsible", newRespUsers2.Distinct().ToArray());
				_modifier.Apply();
			}
			if (value.TypeId == 26)
			{
				Ascon.Pilot.SDK.IDataObject task_psd = obj;
				Ascon.Pilot.SDK.IDataObject doc_psd = await loader.Load(task_psd.Relations.First().TargetId, 0L);
				Ascon.Pilot.SDK.IDataObject stage_psd = await loader.Load(task_psd.ParentId, 0L);
				Ascon.Pilot.SDK.IDataObject workflow_psd = await loader.Load(stage_psd.ParentId, 0L);
				_ = workflow_psd.Children.Count;
				long long_order = (long)stage_psd.Attributes["order"];
				int stage_order = (int)long_order;
				Thread.Sleep(10);
				Guid state_task_psd = (Guid)task_psd.Attributes["state"];
				int initiatorId2 = ((int[])task_psd.Attributes["initiator"]).First();
				executorId = Repository.GetCurrentPerson().MainPosition.Position;
				initiatorNick = user[initiatorId2][0];
				executorNick = user[executorId][0];
				initiatorName = user[initiatorId2][1];
				executorName = user[executorId][1];
				ObjectLoader objectLoader49 = loader;
				ObjectLoader objectLoader50 = objectLoader49;
				Ascon.Pilot.SDK.IDataObject currentOrder = await objectLoader50.Load((await loader.Load(value.ObjectId, value.ChangesetId())).ParentId, value.ChangesetId());
				if (state_task_psd == state_hasRemarks)
				{
					Ascon.Pilot.SDK.IDataObject TechTask = await loader.Load(currentOrder.ParentId, value.ChangesetId());
					Form form = new Form();
					form.Text = "Описание замечания";
					form.Icon = new Icon(SystemIcons.Question, 40, 40);
					TextBox textBox1 = new TextBox();
					TextBox textBox2 = new TextBox();
					textBox1.Multiline = true;
					textBox1.ScrollBars = ScrollBars.Vertical;
					textBox1.WordWrap = true;
					textBox1.Size = new Size(980, 40);
					textBox1.Location = new Point(0, 520);
					textBox2.Multiline = true;
					textBox2.ScrollBars = ScrollBars.Vertical;
					textBox2.WordWrap = true;
					textBox2.Size = new Size(960, 520);
					textBox2.ReadOnly = true;
					value.GetActionString();
					form.Size = new Size(960, 660);
					form.MaximizeBox = false;
					form.FormBorderStyle = FormBorderStyle.FixedDialog;
					form.Controls.Add(textBox1);
					form.Controls.Add(textBox2);
					form.TopMost = true;
					form.ControlBox = false;
					Button button = new Button
					{
						Text = "Ok",
						Size = new Size(960, 40),
						Location = new Point(0, 560)
					};
					form.Controls.Add(button);
					_ = textBox1.Text;
					button.Click += delegate
					{
						if (textBox1.Text.Length > 1)
						{
							IPerson currentPerson = Repository.GetCurrentPerson();
							_modifier.EditById(TechTask.Id).SetAttribute("reason", textBox2.Text + Environment.NewLine + executorName + "->" + initiatorName + ": " + textBox1.Text);
							_modifier.Apply();
							form.Close();
						}
						else
						{
							MessageBox.Show("Причиина не заполнена");
						}
					};
					textBox1.Text = "";
					if (TechTask.Attributes.ContainsKey("reason"))
					{
						textBox2.Text = TechTask.Attributes["reason"].ToString();
					}
					if (!(currentOrder.Attributes["order"].ToString() == "1"))
					{
						form.ShowDialog();
					}
					foreach (Guid stageId in workflow_psd.Children)
					{
						string text8 = ":hasremarks:  @" + executorNick + " выдал(а) замечания по заданию [" + workflow_psd.DisplayName + "](piloturi://" + workflow_psd.Id.ToString() + ")." + ((textBox1.Text.Length > 1) ? " Причина: " : " ") + textBox1.Text + " " + ProjectDisplayName;
						Ascon.Pilot.SDK.IDataObject dataStage = await loader.Load(stageId, 0L);
						Ascon.Pilot.SDK.IDataObject task_psd_onstage = await loader.Load(dataStage.Children.First(), 0L);
						if ((Guid)task_psd_onstage.Attributes["state"] != state_none)
						{
							if (Convert.ToInt32(dataStage.Attributes["order"]) == 1)
							{
								text8 += " Необходимо отозвать текущее задание и переотправить документы на подпись с исправлениями";
								_modifier.EditById(dataStage.Children.First()).SetAttribute("state", state_hasRemarks);
							}
							else
							{
								_modifier.EditById(dataStage.Children.First()).SetAttribute("state", state_none);
							}
							int[] array17 = (int[])task_psd_onstage.Attributes["initiator"];
							int[] array18 = array17;
							int[] array19 = array18;
							foreach (int item3 in array19)
							{
								SendChatNotificationAsync(user[item3][0], text8, 2223);
							}
						}
					}
					_modifier.Apply();
				}
				if (state_task_psd == state_signed)
				{
					bool allSigned = true;
					foreach (Guid GuidTask in stage_psd.Children)
					{
						if ((Guid)(await loader.Load(GuidTask, 0L)).Attributes["state"] != state_signed)
						{
							allSigned = false;
							break;
						}
					}
					if (allSigned)
					{
						if (workflow_psd.Children.Count == stage_order)
						{
							_modifier.EditById(workflow_psd.Id).SetAttribute("state", state_workflowCompleted);
						}
						else
						{
							foreach (Guid Gtask in (await loader.Load(workflow_psd.Children[stage_order], 0L)).Children)
							{
								_modifier.EditById(Gtask).SetAttribute("state", state_awaitingSignature);
							}
						}
						_modifier.Apply();
					}
				}
				if (state_task_psd == state_awaitingSignature)
				{
					int[] array20 = (int[])task_psd.Attributes["executor"];
					int[] array21 = array20;
					int[] array22 = array21;
					foreach (int idexecs in array22)
					{
						Thread.Sleep(200);
						if (Convert.ToInt32(task_psd.Attributes["current_sub"]) == 0)
						{
							SendChatNotificationAsync(user[idexecs][0], "@" + initiatorNick + " отправил(а) вам документ(ы) на подпись [" + task_psd.DisplayName + "](piloturi://" + task_psd.Id.ToString() + ")", 2265);
							if (stage_order == 4)
							{
								SendChatNotificationAsync("kirin", "задание [" + workflow_psd.DisplayName + "](piloturi://" + workflow_psd.Id.ToString() + ") дошло до НК.", 2268);
							}
							Thread.Sleep(200);
						}
					}
				}
				if (state_task_psd == state_sendToSignature)
				{
					if ((long)task_psd.Attributes["current_sub"] == (long)task_psd.Attributes["need_sub"])
					{
						_modifier.EditById(task_psd.Id).SetAttribute("state", state_signed);
						_modifier.Apply();
					}
					else
					{
						_modifier.EditById(task_psd.Id).SetAttribute("state", state_awaitingSignature);
						_modifier.Apply();
						MessageBox.Show("не все/ё подписали");
					}
				}
				foreach (IRelation rel2 in task_psd.Relations)
				{
					if ((await loader.Load(rel2.TargetId, 0L)).Type.Id != 54)
					{
					}
				}
				if (state_task_psd == state_revoked)
				{
					_modifier.EditById(workflow_psd.Id).SetAttribute("state", state_revoked);
					_modifier.Apply();
					foreach (ISignature item4 in doc_psd.Files.First().Signatures)
					{
						if (item4.Sign != null)
						{
							item4.Sign.Remove(0);
						}
					}
					int initiatorWorkflowId = ((int[])workflow_psd.Attributes["initiator"]).First();
					_ = user[initiatorWorkflowId][0];
				}
				if (value.ChangeKind == NotificationKind.ObjectCreated)
				{
				}
				if (state_task_psd == notApproved)
				{
					Ascon.Pilot.SDK.IDataObject stage_approval = await loader.Load(task_psd.ParentId, 0L);
					int order3 = Convert.ToInt32(stage_approval.Attributes["order"]);
					Ascon.Pilot.SDK.IDataObject workflow_approval = await loader.Load(stage_approval.ParentId, 0L);
					Ascon.Pilot.SDK.IDataObject[] arTaskPsd = new Ascon.Pilot.SDK.IDataObject[6];
					foreach (Guid guidOder in workflow_approval.Children)
					{
						Ascon.Pilot.SDK.IDataObject ordern = await loader.Load(guidOder, 0L);
						Ascon.Pilot.SDK.IDataObject[] array23 = arTaskPsd;
						int num9 = Convert.ToInt32(ordern.Attributes["order"]);
						Ascon.Pilot.SDK.IDataObject[] array24 = array23;
						int num10 = num9;
						Ascon.Pilot.SDK.IDataObject[] array25 = array24;
						int num11 = num10;
						array25[num11] = await loader.Load(ordern.Children.First(), 0L);
					}
					switch (order3)
					{
					case 1:
						_modifier.EditById(arTaskPsd[1].Id).SetAttribute("state", state_awaitingSignature);
						MessageBox.Show("Нет предыдущего этапа");
						break;
					case 2:
						_modifier.EditById(arTaskPsd[1].Id).SetAttribute("state", state_delete);
						_modifier.EditById(arTaskPsd[2].Id).SetAttribute("state", state_none);
						break;
					case 3:
						_modifier.EditById(arTaskPsd[1].Id).SetAttribute("state", state_hasRemarks);
						_modifier.EditById(arTaskPsd[2].Id).SetAttribute("state", state_delete);
						_modifier.EditById(arTaskPsd[3].Id).SetAttribute("state", state_none);
						break;
					case 4:
						_modifier.EditById(arTaskPsd[3].Id).SetAttribute("state", state_delete);
						_modifier.Apply();
						_modifier.EditById(arTaskPsd[4].Id).SetAttribute("state", state_none);
						_modifier.Apply();
						break;
					}
					_modifier.Apply();
				}
				if (!(state_task_psd == state_has_Remarks_to_po))
				{
				}
			}
			if (value.TypeId == 25 || value.TypeId == 85 || value.TypeId == 108)
			{
				Ascon.Pilot.SDK.IDataObject currentTask = await loader.Load(value.ObjectId, value.ChangesetId());
				try
				{
					Ascon.Pilot.SDK.IDataObject inProject = await loader.Load(currentTask.Relations.First().TargetId, 0L);
					do
					{
						inProject = await loader.Load(inProject.ParentId, 0L);
					}
					while (inProject.Type.Name != "project");
					ProjectDisplayName = inProject.DisplayName;
				}
				catch (Exception)
				{
					MessageBox.Show("к заданию не прикреплён документ");
					throw;
				}
				Ascon.Pilot.SDK.IDataObject currentOrder2 = await loader.Load(currentTask.ParentId, value.ChangesetId());
				Ascon.Pilot.SDK.IDataObject currentWorkflow2 = await loader.Load(currentOrder2.ParentId, 0L);
				Ascon.Pilot.SDK.IDataObject TechTask2 = await loader.Load(currentOrder2.ParentId, value.ChangesetId());
				currentTask.Attributes["state"].ToString();
				string title2 = value.Title.ToString();
				int stageNumber = -1;
				for (int i2 = 0; i2 < TechTask2.Children.Count; i2++)
				{
					if (TechTask2.Children[i2] == currentOrder2.Id)
					{
						stageNumber = i2;
						break;
					}
				}
				if (stageNumber == -1)
				{
					MessageBox.Show("не удалось получить номер этапа");
				}
				Ascon.Pilot.SDK.IDataObject predTask = null;
				if (stageNumber > 0)
				{
					ObjectLoader objectLoader51 = loader;
					ObjectLoader objectLoader52 = objectLoader51;
					predTask = await objectLoader52.Load(new Guid((await loader.Load(new Guid(TechTask2.Children[stageNumber - 1].ToString()), 0L)).Children[0].ToString()), 0L);
				}
				if (stageNumber - 1 > 0)
				{
					ObjectLoader objectLoader53 = loader;
					ObjectLoader objectLoader54 = objectLoader53;
					await objectLoader54.Load(new Guid((await loader.Load(new Guid(TechTask2.Children[stageNumber - 2].ToString()), 0L)).Children[0].ToString()), 0L);
				}
				Ascon.Pilot.SDK.IDataObject nextTask3 = null;
				if (stageNumber < TechTask2.Children.Count - 1)
				{
					ObjectLoader objectLoader55 = loader;
					ObjectLoader objectLoader56 = objectLoader55;
					nextTask3 = await objectLoader56.Load(new Guid((await loader.Load(new Guid(TechTask2.Children[stageNumber + 1].ToString()), 0L)).Children[0].ToString()), 0L);
				}
				try
				{
				}
				catch
				{
				}
				switch (currentTask.Attributes["state"].ToString())
				{
				case "2f1e55d8-ea8d-41d7-be0a-0f986c4fc9e1":
				{
					if (nextTask3 != null)
					{
						_modifier.EditById(nextTask3.Id).SetAttribute("state", taskAssign);
						_modifier.Apply();
					}
					else
					{
						_modifier.EditById(TechTask2.Id).SetAttribute("state", new Guid("69042a68-29c1-494b-aecf-ce2857cb8098"));
						_modifier.Apply();
						int[] auNew2 = null;
						_modifier.EditById(TechTask2.Id).SetAttribute("auditors", auNew2);
						_modifier.EditById(TechTask2.Id).SetAttribute("actualFor", auNew2);
						_modifier.Apply();
					}
					string text13 = ":noremarks: @" + executorNick + " не имеет замечаний по заданию [" + title2 + "](piloturi://" + value.ObjectId.ToString() + ")." + ProjectDisplayName;
					if (nextTask3 != null)
					{
						int nextexecutorId = ((int[])obj.Attributes["executor"]).First();
						string nextexecutorNick = user[int.Parse(nextexecutorId.ToString())][0];
						text13 = text13 + ". Теперь [оно](piloturi://" + nextTask3.Id.ToString() + "). на согласовании у @" + nextexecutorNick;
					}
					else
					{
						string iniciatorText = ":workflowcompleted: Процесс технического задания [" + title2 + "](piloturi://" + TechTask2.Id.ToString() + "). " + ProjectDisplayName + " полностью завершён";
						int firstiniciatorId = ((int[])(await loader.Load((await loader.Load(currentWorkflow2.Children.First(), 0L)).Children.First(), 0L)).Attributes["initiator"]).First();
						string firstiniciatorNick = user[int.Parse(firstiniciatorId.ToString())][0];
						SendChatNotificationAsync(firstiniciatorNick, iniciatorText, 2457);
					}
					string to5 = initiatorNick;
					SendChatNotificationAsync(to5, text13, 2460);
					break;
				}
				case "8e20c0ae-367e-4c07-b3fe-23d638c2a2c8":
				{
					Form form2 = new Form();
					form2.Text = "Описание замечания";
					form2.Icon = new Icon(SystemIcons.Question, 40, 40);
					TextBox textBox3 = new TextBox();
					TextBox textBox4 = new TextBox();
					textBox3.Multiline = true;
					textBox3.ScrollBars = ScrollBars.Vertical;
					textBox3.WordWrap = true;
					textBox3.Size = new Size(980, 40);
					textBox3.Location = new Point(0, 520);
					textBox4.Multiline = true;
					textBox4.ScrollBars = ScrollBars.Vertical;
					textBox4.WordWrap = true;
					textBox4.Size = new Size(960, 520);
					textBox4.ReadOnly = true;
					value.GetActionString();
					form2.Size = new Size(960, 660);
					form2.MaximizeBox = false;
					form2.FormBorderStyle = FormBorderStyle.FixedDialog;
					form2.Controls.Add(textBox3);
					form2.Controls.Add(textBox4);
					form2.TopMost = true;
					form2.ControlBox = false;
					Button button2 = new Button
					{
						Text = "Ok",
						Size = new Size(960, 40),
						Location = new Point(0, 560)
					};
					form2.Controls.Add(button2);
					if (TechTask2.Attributes.ContainsKey("reason"))
					{
						textBox3.Text = TechTask2.Attributes["reason"].ToString();
					}
					_ = textBox3.Text;
					button2.Click += delegate
					{
						if (textBox3.Text.Length > 1)
						{
							IPerson currentPerson = Repository.GetCurrentPerson();
							_modifier.EditById(TechTask2.Id).SetAttribute("reason", textBox4.Text + Environment.NewLine + executorName + "->" + initiatorName + ": " + textBox3.Text);
							_modifier.Apply();
							form2.Close();
						}
						else
						{
							MessageBox.Show("Причиина не заполнена");
						}
					};
					textBox3.Text = "";
					if (TechTask2.Attributes.ContainsKey("reason"))
					{
						textBox4.Text = TechTask2.Attributes["reason"].ToString();
					}
					form2.ShowDialog();
					string text12 = ":hasremarks:  @" + executorNick + " выдал(а) замечания по заданию [" + title2 + "](piloturi://" + value.ObjectId.ToString() + ")." + ((textBox3.Text.Length > 1) ? " Причина: " : " ") + textBox3.Text + " " + ProjectDisplayName;
					string to4 = initiatorNick;
					SendChatNotificationAsync(to4, text12, 2523);
					break;
				}
				case "bf2f17dc-8b0c-4723-a7a1-e394e9330dc8":
				{
					string text10 = ":inprogress: @" + executorNick + " приступил(а) к выполнению задания [" + title2 + "](piloturi://" + value.ObjectId.ToString() + ")." + ProjectDisplayName;
					string to2 = initiatorNick;
					SendChatNotificationAsync(to2, text10, 2529);
					break;
				}
				case "abdbe49a-7094-4084-9673-eb5fb3f95262":
				{
					_modifier.EditById(TechTask2.Id).SetAttribute("state", state_delete);
					_modifier.Apply();
					int[] auNew = null;
					_modifier.EditById(TechTask2.Id).SetAttribute("auditors", auNew);
					string text9 = ":revoked: @" + initiatorNick + " отозвал(а) задание [" + title2 + "](piloturi://" + value.ObjectId.ToString() + ")." + ProjectDisplayName;
					string to = executorNick;
					SendChatNotificationAsync(to, text9, 2538);
					break;
				}
				case "8fecae19-1be9-4752-8b18-e379afbc3508":
					_ = ":unableToComplete: @" + executorNick + " не может выполнить задание [" + title2 + "](piloturi://" + value.ObjectId.ToString() + "). " + ProjectDisplayName;
					break;
				case "a0068698-a2c3-4b5f-8504-e0d590bf49c8":
					_ = ":onvalidation: @" + executorNick + " завершил(а) задание и оно ожидает вашей проверки [" + title2 + "](piloturi://" + value.ObjectId.ToString() + ")." + ProjectDisplayName;
					break;
				case "149a520c-523a-404a-a1ac-07973c6b23bd":
					_ = ":noremarks: @" + executorNick + " подтвердил(а) выполнение задания [" + title2 + "](piloturi://" + value.ObjectId.ToString() + ")." + ProjectDisplayName;
					break;
				case "dfa42efa-2748-4320-a6a2-594d1e24ead7":
				{
					string text14 = ":assigned: @" + initiatorNick + " отправил(а) вам задание [" + title2 + "](piloturi://" + value.ObjectId.ToString() + "). " + ProjectDisplayName;
					string to6 = executorNick;
					SendChatNotificationAsync(to6, text14, 2556);
					break;
				}
				case "ca50bfc9-0d90-4573-a9b6-9a28f48ec02c":
					if (stageNumber != 0)
					{
						_modifier.EditById(currentTask.Id).SetAttribute("state", state_none);
						_modifier.Apply();
						_modifier.EditById(predTask.Id).SetAttribute("state", taskHasRemarks);
						_modifier.EditById(predTask.Id).SetAttribute("Reason", "");
						_modifier.Apply();
						int idto = ((int[])predTask.Attributes["initiator"]).First();
						string to3 = user[idto][0];
						string title3 = predTask.Attributes["description"].ToString();
						string id = TechTask2.Id.ToString();
						string text11 = ":notapproved: @" + initiatorNick + " вернул(а) на доработку вам задание [" + title3 + "](piloturi://" + id + ")." + ProjectDisplayName;
						SendChatNotificationAsync(to3, text11, 2573);
					}
					else
					{
						MessageBox.Show("Некому отправить на доработку. Вы инициатор тех. процесса");
						_modifier.EditById(currentTask.Id).SetAttribute("state", taskHasRemarks);
						_modifier.Apply();
					}
					break;
				case "c1354e0d-19db-46b1-93fd-c6e5fd059f1c":
					if (nextTask3 != null)
					{
						_modifier.EditById(nextTask3.Id).SetAttribute("state", taskAssign);
						_modifier.Apply();
					}
					else
					{
						_modifier.EditById(TechTask2.Id).SetAttribute("state", new Guid("69042a68-29c1-494b-aecf-ce2857cb8098"));
						_modifier.Apply();
					}
					break;
				}
				try
				{
				}
				catch (Exception)
				{
				}
			}
			if (value.TypeId != 35)
			{
			}
		}
		if (value.ChangeKind != NotificationKind.ObjectSignatureChanged)
		{
			return;
		}
		foreach (IRelation tasks in obj.Relations)
		{
			Ascon.Pilot.SDK.IDataObject RelTask = await loader.Load(tasks.TargetId, 0L);
			if (!RelTask.Attributes.ContainsKey("state") || !((Guid)RelTask.Attributes["state"] == state_awaitingSignature) || ((int[])RelTask.Attributes["executor"]).First() != executorId)
			{
				continue;
			}
			foreach (IRelation docs in RelTask.Relations)
			{
				await loader.Load(docs.TargetId, 0L);
			}
		}
	}

	public void OnError(Exception error)
	{
		MessageBox.Show("notifacation error");
	}

	public void OnCompleted()
	{
		MessageBox.Show("notifacation completed");
	}

	public bool Handle(IAttributeModifier modifier, ObjectCardContext context)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		IEnumerable<IType> v = Repository.GetTypes();
		string[] ar = new string[120];
		if (inStadia != null && inStadia.Attributes.ContainsKey("number_stage"))
		{
			modifier.SetValue("Object", (string)inStadia.Attributes["number_stage"]);
		}
		if (inProject != null)
		{
			modifier.SetValue("Stage", (string)inProject.Attributes["Stage"]);
		}
		foreach (IType item in v)
		{
			if (!item.IsDeleted)
			{
				ar[item.Id] = item.Name.ToString() + ";" + item.Title.ToString();
			}
		}
		IEnumerable<IOrganisationUnit> orgStruc = Repository.GetOrganisationUnits();
		int person = orgStruc.ElementAt(108).Person();
		IEnumerable<IPerson> people = Repository.GetPeople();
		if (context.EditiedObject == null)
		{
			if (context.Type.Name == "workflow_giptask")
			{
				modifier.SetValue("description", selectedObjectTask.Attributes["name_work"].ToString());
			}
			Ascon.Pilot.SDK.IDataObject objParent = context.Parent;
			if (context.Type.Name == "task_")
			{
				string dep_take_task = "СО; ТО";
				dep_take_task = dep_take_task.Replace(" ", "");
				string[] ar_dep_take_task = dep_take_task.Split(';');
				int[] newEx2 = new int[1] { 87 };
				string[] array = ar_dep_take_task;
				string[] array2 = array;
				string[] array3 = array2;
				foreach (string name in array3)
				{
					int depId = Array.IndexOf(depName, name);
				}
			}
			if (context.Type.Name == "task_GIP")
			{
				string dep_take_task2 = selectedObjectTask.Attributes["dep_take_task"].ToString();
				dep_take_task2 = dep_take_task2.Replace(" ", "");
				string[] ar_dep_take_task2 = dep_take_task2.Split(';');
				int[] executor = new int[ar_dep_take_task2.Length];
				for (int j = 0; j < ar_dep_take_task2.Length; j++)
				{
					int depId2 = Array.IndexOf(depName, ar_dep_take_task2[j]);
					if (depId2 == -1)
					{
						MessageBox.Show("Не удалось найти начальника " + ar_dep_take_task2[j]);
						depId2 = 6;
					}
					executor[j] = depBoss[depId2];
				}
				modifier.SetValue("executor", executor);
			}
			if (context.Type.Name == "task_approval")
			{
				int[] newEx3 = new int[1] { 87 };
				string[] t = new string[1] { "test" };
				modifier.SetValue("executor", t.ToString());
			}
			int CurentUserId = Repository.GetCurrentPerson().Positions.First().Position;
			if (context.Type.Name == "folderTask")
			{
				try
				{
					int[] array4 = depNum;
					int[] array5 = array4;
					int[] array6 = array5;
					foreach (int depF in array6)
					{
						IOrganisationUnit dep = orgStruc.ElementAt(depF);
						if (dep.Children.Contains(CurentUserId))
						{
							modifier.SetValue("otd", depName[Array.IndexOf(depNum, depF)]);
							break;
						}
					}
					IPerson cp = Repository.GetCurrentPerson();
				}
				catch
				{
				}
			}
			if (context.Type.Name == "task" || context.Type.Name == "taskPSD")
			{
				try
				{
					modifier.SetValue("dep_give_task", objParent.Attributes["otd"].ToString());
					modifier.SetValue("dep_take_task", objParent.Attributes["Leading_department"].ToString());
					modifier.SetValue("number", objParent.Attributes["NumberFolderTask"].ToString());
					modifier.SetValue("NameFile", "NameFile");
					modifier.SetValue("developed", Repository.GetCurrentPerson().DisplayName);
					string otd = objParent.Attributes["otd"].ToString();
					string Leading_department = objParent.Attributes["Leading_department"].ToString();
					if (depName.Contains(objParent.Attributes["otd"].ToString()))
					{
						modifier.SetValue("DepHead", user[depBoss[Array.IndexOf(depName, otd)]][1]);
					}
					else
					{
						MessageBox.Show("Не найден отдел " + otd + " указанный в папке задания");
					}
					if (depName.Contains(objParent.Attributes["Leading_department"].ToString()))
					{
						modifier.SetValue("accepted", user[depBoss[Array.IndexOf(depName, Leading_department)]][1]);
					}
					else
					{
						MessageBox.Show("Не найден отдел " + Leading_department + " указанный в папке задания");
					}
				}
				catch (Exception)
				{
					throw;
				}
			}
			if (context.Type.Name == "task_approval")
			{
				DateTime date1 = DateTime.Now;
				modifier.SetValue("deadlineDate", date1.AddDays(2.0));
				modifier.SetValue("deadlineDate", date1.AddDays(2.0));
				modifier.SetValue("dateOfAssignment", date1.AddDays(2.0));
				modifier.SetValue("dateOfAssignment", date1.AddDays(2.0));
				modifier.SetValue("deadlineDate(1)", date1.AddDays(2.0));
				modifier.SetValue("deadlineDate(1)", date1.AddDays(2.0));
			}
			if (context.Type.Name == "workflow_common")
			{
				MessageBox.Show("Прецесс ТЗ теперь нужно создавать через контекстное меню Отправить задание");
				try
				{
					IObjectsRepository repo = Repository;
					modifier.SetValue("Prof", selectedObjectTask.Attributes["dep_give_task"].ToString());
					modifier.SetValue("task_select", selectedObjectTask.Attributes["dep_take_task"].ToString());
					modifier.SetValue("description", selectedObjectTask.Attributes["content"].ToString());
					modifier.SetValue("project_name", inProject.Attributes["project_name"].ToString());
					modifier.SetValue("Stage", inProject.Attributes["Stage"].ToString());
					selectedObjectTask = null;
				}
				catch
				{
				}
			}
			if (context.Type.Name == "workflow_approval")
			{
				MessageBox.Show("Отправлять ПСД на согласование нужно через контекстное меню Отправить ПСД на согласование");
			}
			if (context.Type.Name == "task_GIPdoc")
			{
				try
				{
				}
				catch (Exception)
				{
					throw;
				}
			}
		}
		else
		{
			int[] developed = new int[1] { 6 };
			try
			{
				if (context.Type.Name == "project")
				{
					string newGipStringId = ((int[])context.AttributesValues["responsible_for_the_contract"]).First().ToString();
					int newGipId = int.Parse(newGipStringId);
					_modifier.Edit(context.EditiedObject).SetAttribute("gipName", user[newGipId][1]);
					_modifier.Apply();
				}
			}
			catch
			{
			}
		}
		return true;
	}

	private async Task<Ascon.Pilot.SDK.IDataObject> GetProjectByIdAsync(Guid objectId)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		_ = (Ascon.Pilot.SDK.IDataObject)loader.Load(objectId, 0L);
		Ascon.Pilot.SDK.IDataObject obj = await loader.Load(objectId, 0L);
		while (obj.Type.Name != "project")
		{
			if (obj.Type.Name == "Root_object_type")
			{
				MessageBox.Show("не удалось найти проект");
				return obj;
			}
			obj = (Ascon.Pilot.SDK.IDataObject)loader.Load(obj.ParentId, 0L);
		}
		return obj;
	}

	private async Task<Ascon.Pilot.SDK.IDataObject> GetObjectByIdAsync(Guid objectId)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		return await loader.Load(objectId, 0L);
	}

	private async void getProjectById(Guid objectId)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		Ascon.Pilot.SDK.IDataObject obj = await loader.Load(objectId, 0L);
		while (obj.Type.Name != "project")
		{
			if (obj.Type.Name == "Root_object_type")
			{
			}
			obj = await loader.Load(obj.ParentId, 0L);
		}
		inProject = obj;
	}

	public bool OnValueChanged(IAttribute sender, AttributeValueChangedEventArgs args, IAttributeModifier modifier)
	{
		int[] t = new int[2] { 87, 6 };
		if (sender.Name == "answer_to")
		{
			modifier.SetValue("state", new Guid("da3f641b-6b82-439d-ac93-2e3a70a25d2f"));
		}
		if (sender.Name == "project" || args.Context.Type.Id == 41)
		{
		}
		if (sender.Name == "description")
		{
		}
		if (sender.Name == "task_approval")
		{
			int[] newEx = new int[1] { 87 };
			modifier.SetValue("initiator", newEx);
		}
		return true;
	}

	public IObjectBuilder Create(Ascon.Pilot.SDK.IDataObject parent, IType type)
	{
		return _modifier.Create(parent, type);
	}

	public IObjectBuilder Create(Guid id, Ascon.Pilot.SDK.IDataObject parent, IType type)
	{
		return _modifier.Create(id, parent, type);
	}

	public IObjectBuilder CreateById(Guid id, Guid parentId, IType type)
	{
		return _modifier.CreateById(id, parentId, type);
	}

	public IObjectBuilder Edit(Ascon.Pilot.SDK.IDataObject @object)
	{
		return _modifier.Edit(@object);
	}

	public IObjectBuilder EditById(Guid objectId)
	{
		return _modifier.EditById(objectId);
	}

	public void Move(Ascon.Pilot.SDK.IDataObject @object, Ascon.Pilot.SDK.IDataObject newParent)
	{
		_modifier.Move(@object, newParent);
	}

	public void MoveById(Guid objectId, Guid newParentId)
	{
		_modifier.MoveById(objectId, newParentId);
	}

	public void Delete(Ascon.Pilot.SDK.IDataObject @object)
	{
		_modifier.Delete(@object);
	}

	public void DeleteById(Guid objectId)
	{
		_modifier.DeleteById(objectId);
	}

	public void ChangeState(Ascon.Pilot.SDK.IDataObject @object, ObjectState state)
	{
		_modifier.ChangeState(@object, state);
	}

	public IObjectBuilder Restore(Guid objectId, Guid parentId)
	{
		return _modifier.Restore(objectId, parentId);
	}

	public void DeletePermanently(Guid objectId)
	{
		_modifier.DeletePermanently(objectId);
	}

	public IObjectBuilder RestorePermanentlyDeletedObject(Guid id, Guid parentId, IType type)
	{
		return _modifier.RestorePermanentlyDeletedObject(id, parentId, type);
	}

	public void Apply()
	{
		_modifier.Apply();
	}

	public void Clear()
	{
		_modifier.Clear();
	}

	public void CreateLink(IRelation relation1, IRelation relation2)
	{
		_modifier.CreateLink(relation1, relation2);
	}

	public void RemoveLink(Ascon.Pilot.SDK.IDataObject obj, IRelation relation)
	{
		_modifier.RemoveLink(obj, relation);
	}

	public IObjectBuilder Create(Guid parent, IType type)
	{
		return _modifier.Create(parent, type);
	}

	public async Task<Ascon.Pilot.SDK.IDataObject> getObjectById(Guid objectId)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		return await loader.Load(objectId, 0L);
	}

	public async void getProjectGuid(Guid guid)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		await loader.Load(guid, 0L);
	}

	public async void getStadiaByIdAsync(Guid objectId)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		ObjectLoader objectLoader = loader;
		ObjectLoader objectLoader2 = objectLoader;
		Ascon.Pilot.SDK.IDataObject parentObj = await objectLoader2.Load((await loader.Load(objectId, 0L)).ParentId, 0L);
		do
		{
			if (parentObj.Type.Name == "Root_object_type")
			{
			}
			parentObj = await loader.Load(parentObj.ParentId, 0L);
		}
		while (parentObj.Type.Name != "Stadia");
		inStadia = parentObj;
		inProject = inStadia;
		do
		{
			if (parentObj.Type.Name == "Root_object_type")
			{
			}
			parentObj = await loader.Load(parentObj.ParentId, 0L);
		}
		while (parentObj.Type.Name != "project");
	}

	public void Build(IMenuBuilder builder, ObjectsViewContext context)
	{
		byte[] copyStorageIcon = new byte[4078]
		{
			60, 63, 120, 109, 108, 32, 118, 101, 114, 115,
			105, 111, 110, 61, 34, 49, 46, 48, 34, 32,
			101, 110, 99, 111, 100, 105, 110, 103, 61, 34,
			85, 84, 70, 45, 56, 34, 63, 62, 10, 60,
			33, 68, 79, 67, 84, 89, 80, 69, 32, 115,
			118, 103, 32, 80, 85, 66, 76, 73, 67, 32,
			34, 45, 47, 47, 87, 51, 67, 47, 47, 68,
			84, 68, 32, 83, 86, 71, 32, 49, 46, 49,
			47, 47, 69, 78, 34, 32, 34, 104, 116, 116,
			112, 58, 47, 47, 119, 119, 119, 46, 119, 51,
			46, 111, 114, 103, 47, 71, 114, 97, 112, 104,
			105, 99, 115, 47, 83, 86, 71, 47, 49, 46,
			49, 47, 68, 84, 68, 47, 115, 118, 103, 49,
			49, 46, 100, 116, 100, 34, 62, 10, 60, 115,
			118, 103, 32, 120, 109, 108, 110, 115, 61, 34,
			104, 116, 116, 112, 58, 47, 47, 119, 119, 119,
			46, 119, 51, 46, 111, 114, 103, 47, 50, 48,
			48, 48, 47, 115, 118, 103, 34, 32, 118, 101,
			114, 115, 105, 111, 110, 61, 34, 49, 46, 49,
			34, 32, 119, 105, 100, 116, 104, 61, 34, 49,
			50, 56, 112, 120, 34, 32, 104, 101, 105, 103,
			104, 116, 61, 34, 49, 50, 56, 112, 120, 34,
			32, 115, 116, 121, 108, 101, 61, 34, 115, 104,
			97, 112, 101, 45, 114, 101, 110, 100, 101, 114,
			105, 110, 103, 58, 103, 101, 111, 109, 101, 116,
			114, 105, 99, 80, 114, 101, 99, 105, 115, 105,
			111, 110, 59, 32, 116, 101, 120, 116, 45, 114,
			101, 110, 100, 101, 114, 105, 110, 103, 58, 103,
			101, 111, 109, 101, 116, 114, 105, 99, 80, 114,
			101, 99, 105, 115, 105, 111, 110, 59, 32, 105,
			109, 97, 103, 101, 45, 114, 101, 110, 100, 101,
			114, 105, 110, 103, 58, 111, 112, 116, 105, 109,
			105, 122, 101, 81, 117, 97, 108, 105, 116, 121,
			59, 32, 102, 105, 108, 108, 45, 114, 117, 108,
			101, 58, 101, 118, 101, 110, 111, 100, 100, 59,
			32, 99, 108, 105, 112, 45, 114, 117, 108, 101,
			58, 101, 118, 101, 110, 111, 100, 100, 34, 32,
			120, 109, 108, 110, 115, 58, 120, 108, 105, 110,
			107, 61, 34, 104, 116, 116, 112, 58, 47, 47,
			119, 119, 119, 46, 119, 51, 46, 111, 114, 103,
			47, 49, 57, 57, 57, 47, 120, 108, 105, 110,
			107, 34, 62, 10, 60, 103, 62, 60, 112, 97,
			116, 104, 32, 115, 116, 121, 108, 101, 61, 34,
			111, 112, 97, 99, 105, 116, 121, 58, 48, 46,
			57, 49, 49, 34, 32, 102, 105, 108, 108, 61,
			34, 35, 48, 55, 48, 53, 48, 49, 34, 32,
			100, 61, 34, 77, 32, 49, 50, 55, 46, 53,
			44, 55, 57, 46, 53, 32, 67, 32, 49, 50,
			55, 46, 53, 44, 56, 53, 46, 53, 32, 49,
			50, 55, 46, 53, 44, 57, 49, 46, 53, 32,
			49, 50, 55, 46, 53, 44, 57, 55, 46, 53,
			67, 32, 49, 49, 55, 46, 51, 56, 53, 44,
			49, 50, 48, 46, 51, 56, 32, 49, 48, 48,
			46, 55, 49, 57, 44, 49, 50, 54, 46, 56,
			56, 32, 55, 55, 46, 53, 44, 49, 49, 55,
			67, 32, 55, 49, 46, 55, 51, 49, 44, 49,
			49, 51, 46, 50, 51, 50, 32, 54, 55, 46,
			51, 57, 55, 55, 44, 49, 48, 56, 46, 50,
			51, 50, 32, 54, 52, 46, 53, 44, 49, 48,
			50, 67, 32, 52, 52, 46, 53, 44, 49, 48,
			49, 46, 54, 54, 55, 32, 50, 52, 46, 53,
			44, 49, 48, 49, 46, 51, 51, 51, 32, 52,
			46, 53, 44, 49, 48, 49, 67, 32, 50, 46,
			54, 55, 54, 57, 52, 44, 49, 48, 48, 46,
			48, 48, 51, 32, 49, 46, 48, 49, 48, 50,
			55, 44, 57, 56, 46, 56, 51, 54, 49, 32,
			45, 48, 46, 53, 44, 57, 55, 46, 53, 67,
			32, 45, 48, 46, 53, 44, 54, 56, 46, 49,
			54, 54, 55, 32, 45, 48, 46, 53, 44, 51,
			56, 46, 56, 51, 51, 51, 32, 45, 48, 46,
			53, 44, 57, 46, 53, 67, 32, 49, 46, 48,
			49, 48, 50, 55, 44, 56, 46, 49, 54, 51,
			57, 52, 32, 50, 46, 54, 55, 54, 57, 52,
			44, 54, 46, 57, 57, 55, 50, 56, 32, 52,
			46, 53, 44, 54, 67, 32, 49, 51, 46, 53,
			44, 53, 46, 51, 51, 51, 51, 51, 32, 50,
			50, 46, 53, 44, 53, 46, 51, 51, 51, 51,
			51, 32, 51, 49, 46, 53, 44, 54, 67, 32,
			51, 53, 46, 57, 49, 51, 53, 44, 56, 46,
			52, 56, 52, 52, 57, 32, 51, 56, 46, 57,
			49, 51, 53, 44, 49, 50, 46, 49, 53, 49,
			50, 32, 52, 48, 46, 53, 44, 49, 55, 67,
			32, 53, 57, 46, 49, 54, 54, 55, 44, 49,
			55, 46, 51, 51, 51, 51, 32, 55, 55, 46,
			56, 51, 51, 51, 44, 49, 55, 46, 54, 54,
			54, 55, 32, 57, 54, 46, 53, 44, 49, 56,
			67, 32, 57, 57, 46, 48, 53, 55, 51, 44,
			49, 56, 46, 54, 56, 53, 51, 32, 49, 48,
			48, 46, 56, 57, 49, 44, 50, 48, 46, 49,
			56, 53, 51, 32, 49, 48, 50, 44, 50, 50,
			46, 53, 67, 32, 49, 48, 51, 46, 49, 57,
			57, 44, 50, 53, 46, 55, 50, 53, 54, 32,
			49, 48, 51, 46, 54, 57, 57, 44, 50, 57,
			46, 48, 53, 57, 32, 49, 48, 51, 46, 53,
			44, 51, 50, 46, 53, 67, 32, 49, 48, 57,
			46, 48, 51, 49, 44, 51, 50, 46, 49, 55,
			54, 55, 32, 49, 49, 50, 46, 56, 54, 52,
			44, 51, 52, 46, 53, 49, 32, 49, 49, 53,
			44, 51, 57, 46, 53, 67, 32, 49, 49, 53,
			46, 51, 51, 51, 44, 52, 55, 46, 49, 54,
			54, 55, 32, 49, 49, 53, 46, 54, 54, 55,
			44, 53, 52, 46, 56, 51, 51, 51, 32, 49,
			49, 54, 44, 54, 50, 46, 53, 67, 32, 49,
			50, 49, 46, 51, 54, 54, 44, 54, 55, 46,
			50, 54, 51, 57, 32, 49, 50, 53, 46, 50,
			44, 55, 50, 46, 57, 51, 48, 53, 32, 49,
			50, 55, 46, 53, 44, 55, 57, 46, 53, 32,
			90, 34, 47, 62, 60, 47, 103, 62, 10, 60,
			103, 62, 60, 112, 97, 116, 104, 32, 115, 116,
			121, 108, 101, 61, 34, 111, 112, 97, 99, 105,
			116, 121, 58, 49, 34, 32, 102, 105, 108, 108,
			61, 34, 35, 102, 54, 100, 50, 56, 57, 34,
			32, 100, 61, 34, 77, 32, 55, 51, 46, 53,
			44, 51, 54, 46, 53, 32, 67, 32, 55, 49,
			46, 48, 53, 48, 52, 44, 51, 54, 46, 50,
			57, 56, 32, 54, 56, 46, 55, 49, 55, 44,
			51, 54, 46, 54, 51, 49, 52, 32, 54, 54,
			46, 53, 44, 51, 55, 46, 53, 67, 32, 54,
			54, 46, 55, 55, 55, 56, 44, 51, 53, 46,
			54, 53, 51, 54, 32, 54, 54, 46, 49, 49,
			49, 50, 44, 51, 52, 46, 51, 50, 48, 50,
			32, 54, 52, 46, 53, 44, 51, 51, 46, 53,
			67, 32, 54, 51, 46, 48, 56, 49, 53, 44,
			51, 52, 46, 48, 48, 52, 55, 32, 54, 50,
			46, 52, 49, 52, 56, 44, 51, 53, 46, 48,
			48, 52, 55, 32, 54, 50, 46, 53, 44, 51,
			54, 46, 53, 67, 32, 54, 48, 46, 49, 54,
			54, 55, 44, 51, 54, 46, 53, 32, 53, 55,
			46, 56, 51, 51, 51, 44, 51, 54, 46, 53,
			32, 53, 53, 46, 53, 44, 51, 54, 46, 53,
			67, 32, 53, 53, 46, 53, 56, 53, 56, 44,
			51, 53, 46, 53, 48, 52, 49, 32, 53, 53,
			46, 50, 53, 50, 52, 44, 51, 52, 46, 54,
			55, 48, 56, 32, 53, 52, 46, 53, 44, 51,
			52, 67, 32, 52, 51, 46, 49, 54, 54, 55,
			44, 51, 51, 46, 51, 51, 51, 51, 32, 51,
			49, 46, 56, 51, 51, 51, 44, 51, 51, 46,
			51, 51, 51, 51, 32, 50, 48, 46, 53, 44,
			51, 52, 67, 32, 49, 57, 46, 48, 54, 57,
			50, 44, 51, 52, 46, 52, 54, 53, 50, 32,
			49, 55, 46, 57, 48, 50, 53, 44, 51, 53,
			46, 50, 57, 56, 54, 32, 49, 55, 44, 51,
			54, 46, 53, 67, 32, 49, 53, 46, 54, 57,
			49, 52, 44, 53, 53, 46, 52, 49, 48, 49,
			32, 49, 53, 46, 48, 50, 52, 55, 44, 55,
			52, 46, 52, 49, 48, 49, 32, 49, 53, 44,
			57, 51, 46, 53, 67, 32, 49, 49, 46, 57,
			57, 54, 53, 44, 57, 55, 32, 56, 46, 54,
			54, 51, 49, 52, 44, 57, 55, 46, 51, 51,
			51, 51, 32, 53, 44, 57, 52, 46, 53, 67,
			32, 52, 46, 51, 51, 51, 51, 51, 44, 54,
			55, 46, 49, 54, 54, 55, 32, 52, 46, 51,
			51, 51, 51, 51, 44, 51, 57, 46, 56, 51,
			51, 51, 32, 53, 44, 49, 50, 46, 53, 67,
			32, 53, 46, 53, 44, 49, 50, 32, 54, 44,
			49, 49, 46, 53, 32, 54, 46, 53, 44, 49,
			49, 67, 32, 49, 52, 46, 49, 54, 54, 55,
			44, 49, 48, 46, 51, 51, 51, 51, 32, 50,
			49, 46, 56, 51, 51, 51, 44, 49, 48, 46,
			51, 51, 51, 51, 32, 50, 57, 46, 53, 44,
			49, 49, 67, 32, 51, 50, 46, 54, 55, 52,
			54, 44, 49, 51, 46, 56, 50, 50, 57, 32,
			51, 53, 46, 48, 48, 55, 57, 44, 49, 55,
			46, 51, 50, 50, 57, 32, 51, 54, 46, 53,
			44, 50, 49, 46, 53, 67, 32, 53, 53, 46,
			56, 50, 52, 55, 44, 50, 50, 46, 51, 51,
			51, 32, 55, 53, 46, 49, 53, 56, 49, 44,
			50, 50, 46, 56, 51, 51, 32, 57, 52, 46,
			53, 44, 50, 51, 67, 32, 57, 53, 46, 52,
			49, 54, 50, 44, 50, 51, 46, 51, 55, 52,
			50, 32, 57, 54, 46, 50, 52, 57, 54, 44,
			50, 51, 46, 56, 55, 52, 50, 32, 57, 55,
			44, 50, 52, 46, 53, 67, 32, 57, 55, 46,
			52, 57, 56, 44, 50, 55, 46, 52, 56, 49,
			54, 32, 57, 55, 46, 54, 54, 52, 54, 44,
			51, 48, 46, 52, 56, 49, 54, 32, 57, 55,
			46, 53, 44, 51, 51, 46, 53, 67, 32, 56,
			57, 46, 56, 50, 54, 49, 44, 51, 51, 46,
			51, 51, 51, 54, 32, 56, 50, 46, 49, 53,
			57, 52, 44, 51, 51, 46, 53, 48, 48, 51,
			32, 55, 52, 46, 53, 44, 51, 52, 67, 32,
			55, 51, 46, 55, 52, 55, 54, 44, 51, 52,
			46, 54, 55, 48, 56, 32, 55, 51, 46, 52,
			49, 52, 50, 44, 51, 53, 46, 53, 48, 52,
			49, 32, 55, 51, 46, 53, 44, 51, 54, 46,
			53, 32, 90, 34, 47, 62, 60, 47, 103, 62,
			10, 60, 103, 62, 60, 112, 97, 116, 104, 32,
			115, 116, 121, 108, 101, 61, 34, 111, 112, 97,
			99, 105, 116, 121, 58, 49, 34, 32, 102, 105,
			108, 108, 61, 34, 35, 49, 56, 49, 50, 48,
			55, 34, 32, 100, 61, 34, 77, 32, 54, 54,
			46, 53, 44, 51, 55, 46, 53, 32, 67, 32,
			54, 52, 46, 55, 51, 54, 55, 44, 51, 56,
			46, 57, 53, 51, 56, 32, 54, 51, 46, 52,
			48, 51, 51, 44, 51, 56, 46, 54, 50, 48,
			52, 32, 54, 50, 46, 53, 44, 51, 54, 46,
			53, 67, 32, 54, 50, 46, 52, 49, 52, 56,
			44, 51, 53, 46, 48, 48, 52, 55, 32, 54,
			51, 46, 48, 56, 49, 53, 44, 51, 52, 46,
			48, 48, 52, 55, 32, 54, 52, 46, 53, 44,
			51, 51, 46, 53, 67, 32, 54, 54, 46, 49,
			49, 49, 50, 44, 51, 52, 46, 51, 50, 48,
			50, 32, 54, 54, 46, 55, 55, 55, 56, 44,
			51, 53, 46, 54, 53, 51, 54, 32, 54, 54,
			46, 53, 44, 51, 55, 46, 53, 32, 90, 34,
			47, 62, 60, 47, 103, 62, 10, 60, 103, 62,
			60, 112, 97, 116, 104, 32, 115, 116, 121, 108,
			101, 61, 34, 111, 112, 97, 99, 105, 116, 121,
			58, 49, 34, 32, 102, 105, 108, 108, 61, 34,
			35, 102, 100, 98, 102, 52, 51, 34, 32, 100,
			61, 34, 77, 32, 53, 53, 46, 53, 44, 51,
			54, 46, 53, 32, 67, 32, 53, 55, 46, 56,
			51, 51, 51, 44, 51, 54, 46, 53, 32, 54,
			48, 46, 49, 54, 54, 55, 44, 51, 54, 46,
			53, 32, 54, 50, 46, 53, 44, 51, 54, 46,
			53, 67, 32, 54, 51, 46, 52, 48, 51, 51,
			44, 51, 56, 46, 54, 50, 48, 52, 32, 54,
			52, 46, 55, 51, 54, 55, 44, 51, 56, 46,
			57, 53, 51, 56, 32, 54, 54, 46, 53, 44,
			51, 55, 46, 53, 67, 32, 54, 56, 46, 55,
			49, 55, 44, 51, 54, 46, 54, 51, 49, 52,
			32, 55, 49, 46, 48, 53, 48, 52, 44, 51,
			54, 46, 50, 57, 56, 32, 55, 51, 46, 53,
			44, 51, 54, 46, 53, 67, 32, 55, 51, 46,
			54, 49, 48, 55, 44, 51, 55, 46, 49, 49,
			55, 52, 32, 55, 51, 46, 57, 52, 52, 44,
			51, 55, 46, 54, 49, 55, 52, 32, 55, 52,
			46, 53, 44, 51, 56, 67, 32, 56, 53, 46,
			56, 51, 51, 51, 44, 51, 56, 46, 51, 51,
			51, 51, 32, 57, 55, 46, 49, 54, 54, 55,
			44, 51, 56, 46, 54, 54, 54, 55, 32, 49,
			48, 56, 46, 53, 44, 51, 57, 67, 32, 49,
			48, 57, 46, 56, 49, 49, 44, 52, 53, 46,
			55, 55, 54, 53, 32, 49, 48, 57, 46, 56,
			49, 49, 44, 53, 50, 46, 54, 48, 57, 57,
			32, 49, 48, 56, 46, 53, 44, 53, 57, 46,
			53, 67, 32, 56, 56, 46, 49, 52, 57, 55,
			44, 53, 50, 46, 55, 53, 53, 53, 32, 55,
			51, 46, 51, 49, 54, 52, 44, 53, 57, 46,
			48, 56, 56, 56, 32, 54, 52, 44, 55, 56,
			46, 53, 67, 32, 54, 51, 46, 48, 55, 55,
			53, 44, 56, 50, 46, 55, 57, 48, 49, 32,
			54, 50, 46, 53, 55, 55, 53, 44, 56, 55,
			46, 49, 50, 51, 52, 32, 54, 50, 46, 53,
			44, 57, 49, 46, 53, 67, 32, 54, 50, 46,
			53, 44, 57, 50, 46, 56, 51, 51, 51, 32,
			54, 50, 46, 53, 44, 57, 52, 46, 49, 54,
			54, 55, 32, 54, 50, 46, 53, 44, 57, 53,
			46, 53, 67, 32, 52, 55, 46, 57, 57, 48,
			51, 44, 57, 53, 46, 56, 50, 57, 51, 32,
			51, 51, 46, 54, 53, 54, 57, 44, 57, 53,
			46, 52, 57, 53, 57, 32, 49, 57, 46, 53,
			44, 57, 52, 46, 53, 67, 32, 49, 57, 46,
			49, 55, 57, 57, 44, 55, 54, 46, 49, 49,
			57, 57, 32, 49, 57, 46, 53, 49, 51, 50,
			44, 53, 55, 46, 55, 56, 54, 54, 32, 50,
			48, 46, 53, 44, 51, 57, 46, 53, 67, 32,
			51, 49, 46, 56, 49, 56, 55, 44, 51, 56,
			46, 54, 54, 55, 53, 32, 52, 51, 46, 49,
			53, 50, 44, 51, 56, 46, 49, 54, 55, 53,
			32, 53, 52, 46, 53, 44, 51, 56, 67, 32,
			53, 53, 46, 48, 53, 54, 44, 51, 55, 46,
			54, 49, 55, 52, 32, 53, 53, 46, 51, 56,
			57, 51, 44, 51, 55, 46, 49, 49, 55, 52,
			32, 53, 53, 46, 53, 44, 51, 54, 46, 53,
			32, 90, 34, 47, 62, 60, 47, 103, 62, 10,
			60, 103, 62, 60, 112, 97, 116, 104, 32, 115,
			116, 121, 108, 101, 61, 34, 111, 112, 97, 99,
			105, 116, 121, 58, 49, 34, 32, 102, 105, 108,
			108, 61, 34, 35, 99, 51, 100, 99, 53, 52,
			34, 32, 100, 61, 34, 77, 32, 57, 48, 46,
			53, 44, 54, 48, 46, 53, 32, 67, 32, 49,
			49, 51, 46, 56, 52, 51, 44, 54, 48, 46,
			54, 55, 52, 32, 49, 50, 52, 46, 51, 52,
			51, 44, 55, 50, 46, 51, 52, 48, 55, 32,
			49, 50, 50, 44, 57, 53, 46, 53, 67, 32,
			49, 49, 52, 46, 50, 51, 55, 44, 49, 49,
			52, 46, 53, 51, 52, 32, 49, 48, 48, 46,
			55, 51, 55, 44, 49, 50, 48, 46, 51, 54,
			55, 32, 56, 49, 46, 53, 44, 49, 49, 51,
			67, 32, 54, 54, 46, 53, 57, 56, 52, 44,
			49, 48, 50, 46, 51, 50, 52, 32, 54, 51,
			46, 52, 51, 49, 56, 44, 56, 56, 46, 56,
			50, 52, 50, 32, 55, 50, 44, 55, 50, 46,
			53, 67, 32, 55, 55, 46, 48, 54, 53, 56,
			44, 54, 54, 46, 53, 54, 52, 49, 32, 56,
			51, 46, 50, 51, 50, 52, 44, 54, 50, 46,
			53, 54, 52, 49, 32, 57, 48, 46, 53, 44,
			54, 48, 46, 53, 32, 90, 34, 47, 62, 60,
			47, 103, 62, 10, 60, 103, 62, 60, 112, 97,
			116, 104, 32, 115, 116, 121, 108, 101, 61, 34,
			111, 112, 97, 99, 105, 116, 121, 58, 49, 34,
			32, 102, 105, 108, 108, 61, 34, 35, 48, 57,
			48, 97, 48, 54, 34, 32, 100, 61, 34, 77,
			32, 56, 55, 46, 53, 44, 55, 53, 46, 53,
			32, 67, 32, 57, 57, 46, 51, 55, 52, 54,
			44, 55, 51, 46, 51, 57, 54, 56, 32, 49,
			48, 56, 46, 56, 55, 53, 44, 55, 55, 46,
			48, 54, 51, 52, 32, 49, 49, 54, 44, 56,
			54, 46, 53, 67, 32, 49, 49, 54, 46, 48,
			52, 53, 44, 57, 48, 46, 50, 52, 57, 54,
			32, 49, 49, 52, 46, 53, 52, 53, 44, 57,
			51, 46, 52, 49, 54, 50, 32, 49, 49, 49,
			46, 53, 44, 57, 54, 67, 32, 49, 48, 49,
			46, 56, 56, 49, 44, 49, 48, 50, 46, 52,
			52, 57, 32, 57, 49, 46, 56, 56, 48, 55,
			44, 49, 48, 51, 46, 49, 49, 54, 32, 56,
			49, 46, 53, 44, 57, 56, 67, 32, 55, 56,
			46, 51, 51, 51, 51, 44, 57, 54, 46, 49,
			54, 54, 55, 32, 55, 53, 46, 56, 51, 51,
			51, 44, 57, 51, 46, 54, 54, 54, 55, 32,
			55, 52, 44, 57, 48, 46, 53, 67, 32, 55,
			51, 46, 57, 53, 52, 55, 44, 56, 54, 46,
			55, 53, 48, 52, 32, 55, 53, 46, 52, 53,
			52, 55, 44, 56, 51, 46, 53, 56, 51, 56,
			32, 55, 56, 46, 53, 44, 56, 49, 67, 32,
			56, 49, 46, 53, 54, 51, 56, 44, 55, 57,
			46, 49, 52, 49, 50, 32, 56, 52, 46, 53,
			54, 51, 56, 44, 55, 55, 46, 51, 48, 55,
			57, 32, 56, 55, 46, 53, 44, 55, 53, 46,
			53, 32, 90, 34, 47, 62, 60, 47, 103, 62,
			10, 60, 103, 62, 60, 112, 97, 116, 104, 32,
			115, 116, 121, 108, 101, 61, 34, 111, 112, 97,
			99, 105, 116, 121, 58, 49, 34, 32, 102, 105,
			108, 108, 61, 34, 35, 55, 101, 56, 56, 97,
			53, 34, 32, 100, 61, 34, 77, 32, 57, 50,
			46, 53, 44, 56, 48, 46, 53, 32, 67, 32,
			49, 48, 48, 46, 50, 48, 49, 44, 56, 48,
			46, 51, 54, 50, 50, 32, 49, 48, 51, 46,
			51, 54, 55, 44, 56, 52, 46, 48, 50, 56,
			57, 32, 49, 48, 50, 44, 57, 49, 46, 53,
			67, 32, 57, 55, 46, 51, 51, 51, 51, 44,
			57, 56, 46, 49, 54, 54, 55, 32, 57, 50,
			46, 54, 54, 54, 55, 44, 57, 56, 46, 49,
			54, 54, 55, 32, 56, 56, 44, 57, 49, 46,
			53, 67, 32, 56, 54, 46, 55, 55, 53, 56,
			44, 56, 54, 46, 54, 50, 54, 32, 56, 56,
			46, 50, 55, 53, 56, 44, 56, 50, 46, 57,
			53, 57, 51, 32, 57, 50, 46, 53, 44, 56,
			48, 46, 53, 32, 90, 34, 47, 62, 60, 47,
			103, 62, 10, 60, 103, 62, 60, 112, 97, 116,
			104, 32, 115, 116, 121, 108, 101, 61, 34, 111,
			112, 97, 99, 105, 116, 121, 58, 49, 34, 32,
			102, 105, 108, 108, 61, 34, 35, 101, 51, 101,
			51, 101, 51, 34, 32, 100, 61, 34, 77, 32,
			56, 50, 46, 53, 44, 56, 51, 46, 53, 32,
			67, 32, 56, 50, 46, 52, 50, 50, 52, 44,
			56, 54, 46, 54, 55, 55, 51, 32, 56, 50,
			46, 53, 56, 57, 49, 44, 57, 48, 46, 48,
			49, 48, 55, 32, 56, 51, 44, 57, 51, 46,
			53, 67, 32, 56, 49, 46, 51, 49, 52, 57,
			44, 57, 49, 46, 57, 56, 50, 53, 32, 55,
			57, 46, 56, 49, 52, 57, 44, 57, 48, 46,
			51, 49, 53, 57, 32, 55, 56, 46, 53, 44,
			56, 56, 46, 53, 67, 32, 55, 57, 46, 56,
			53, 54, 57, 44, 56, 54, 46, 56, 49, 52,
			52, 32, 56, 49, 46, 49, 57, 48, 50, 44,
			56, 53, 46, 49, 52, 55, 55, 32, 56, 50,
			46, 53, 44, 56, 51, 46, 53, 32, 90, 34,
			47, 62, 60, 47, 103, 62, 10, 60, 103, 62,
			60, 112, 97, 116, 104, 32, 115, 116, 121, 108,
			101, 61, 34, 111, 112, 97, 99, 105, 116, 121,
			58, 49, 34, 32, 102, 105, 108, 108, 61, 34,
			35, 100, 100, 100, 100, 100, 100, 34, 32, 100,
			61, 34, 77, 32, 49, 48, 54, 46, 53, 44,
			56, 51, 46, 53, 32, 67, 32, 49, 48, 56,
			46, 52, 55, 57, 44, 56, 52, 46, 56, 49,
			50, 49, 32, 49, 49, 48, 46, 49, 52, 53,
			44, 56, 54, 46, 52, 55, 56, 56, 32, 49,
			49, 49, 46, 53, 44, 56, 56, 46, 53, 67,
			32, 49, 49, 48, 46, 49, 54, 55, 44, 57,
			48, 46, 49, 54, 54, 55, 32, 49, 48, 56,
			46, 56, 51, 51, 44, 57, 49, 46, 56, 51,
			51, 51, 32, 49, 48, 55, 46, 53, 44, 57,
			51, 46, 53, 67, 32, 49, 48, 55, 46, 55,
			55, 50, 44, 57, 48, 46, 50, 51, 53, 57,
			32, 49, 48, 55, 46, 52, 51, 57, 44, 56,
			54, 46, 57, 48, 50, 54, 32, 49, 48, 54,
			46, 53, 44, 56, 51, 46, 53, 32, 90, 34,
			47, 62, 60, 47, 103, 62, 10, 60, 103, 62,
			60, 112, 97, 116, 104, 32, 115, 116, 121, 108,
			101, 61, 34, 111, 112, 97, 99, 105, 116, 121,
			58, 49, 34, 32, 102, 105, 108, 108, 61, 34,
			35, 98, 55, 56, 57, 50, 101, 34, 32, 100,
			61, 34, 77, 32, 54, 50, 46, 53, 44, 57,
			49, 46, 53, 32, 67, 32, 54, 51, 46, 52,
			53, 49, 49, 44, 57, 50, 46, 57, 49, 56,
			55, 32, 54, 51, 46, 55, 56, 52, 53, 44,
			57, 52, 46, 53, 56, 53, 51, 32, 54, 51,
			46, 53, 44, 57, 54, 46, 53, 67, 32, 52,
			56, 46, 52, 56, 53, 50, 44, 57, 54, 46,
			56, 51, 50, 55, 32, 51, 51, 46, 52, 56,
			53, 50, 44, 57, 54, 46, 52, 57, 57, 51,
			32, 49, 56, 46, 53, 44, 57, 53, 46, 53,
			67, 32, 49, 56, 46, 54, 50, 51, 54, 44,
			57, 52, 46, 56, 57, 51, 51, 32, 49, 56,
			46, 57, 53, 54, 57, 44, 57, 52, 46, 53,
			54, 32, 49, 57, 46, 53, 44, 57, 52, 46,
			53, 67, 32, 51, 51, 46, 54, 53, 54, 57,
			44, 57, 53, 46, 52, 57, 53, 57, 32, 52,
			55, 46, 57, 57, 48, 51, 44, 57, 53, 46,
			56, 50, 57, 51, 32, 54, 50, 46, 53, 44,
			57, 53, 46, 53, 67, 32, 54, 50, 46, 53,
			44, 57, 52, 46, 49, 54, 54, 55, 32, 54,
			50, 46, 53, 44, 57, 50, 46, 56, 51, 51,
			51, 32, 54, 50, 46, 53, 44, 57, 49, 46,
			53, 32, 90, 34, 47, 62, 60, 47, 103, 62,
			10, 60, 47, 115, 118, 103, 62, 10
		};
		byte[] copyIcon = new byte[1836]
		{
			60, 63, 120, 109, 108, 32, 118, 101, 114, 115,
			105, 111, 110, 61, 34, 49, 46, 48, 34, 32,
			101, 110, 99, 111, 100, 105, 110, 103, 61, 34,
			85, 84, 70, 45, 56, 34, 63, 62, 13, 10,
			60, 33, 68, 79, 67, 84, 89, 80, 69, 32,
			115, 118, 103, 32, 80, 85, 66, 76, 73, 67,
			32, 34, 45, 47, 47, 87, 51, 67, 47, 47,
			68, 84, 68, 32, 83, 86, 71, 32, 49, 46,
			49, 47, 47, 69, 78, 34, 32, 34, 104, 116,
			116, 112, 58, 47, 47, 119, 119, 119, 46, 119,
			51, 46, 111, 114, 103, 47, 71, 114, 97, 112,
			104, 105, 99, 115, 47, 83, 86, 71, 47, 49,
			46, 49, 47, 68, 84, 68, 47, 115, 118, 103,
			49, 49, 46, 100, 116, 100, 34, 62, 13, 10,
			60, 33, 45, 45, 32, 67, 114, 101, 97, 116,
			111, 114, 58, 32, 67, 111, 114, 101, 108, 68,
			82, 65, 87, 32, 50, 48, 50, 48, 32, 40,
			54, 52, 45, 66, 105, 116, 41, 32, 45, 45,
			62, 13, 10, 60, 115, 118, 103, 32, 120, 109,
			108, 110, 115, 61, 34, 104, 116, 116, 112, 58,
			47, 47, 119, 119, 119, 46, 119, 51, 46, 111,
			114, 103, 47, 50, 48, 48, 48, 47, 115, 118,
			103, 34, 32, 120, 109, 108, 58, 115, 112, 97,
			99, 101, 61, 34, 112, 114, 101, 115, 101, 114,
			118, 101, 34, 32, 119, 105, 100, 116, 104, 61,
			34, 50, 56, 46, 55, 48, 55, 51, 109, 109,
			34, 32, 104, 101, 105, 103, 104, 116, 61, 34,
			50, 56, 46, 55, 48, 55, 51, 109, 109, 34,
			32, 118, 101, 114, 115, 105, 111, 110, 61, 34,
			49, 46, 49, 34, 32, 115, 116, 121, 108, 101,
			61, 34, 115, 104, 97, 112, 101, 45, 114, 101,
			110, 100, 101, 114, 105, 110, 103, 58, 103, 101,
			111, 109, 101, 116, 114, 105, 99, 80, 114, 101,
			99, 105, 115, 105, 111, 110, 59, 32, 116, 101,
			120, 116, 45, 114, 101, 110, 100, 101, 114, 105,
			110, 103, 58, 103, 101, 111, 109, 101, 116, 114,
			105, 99, 80, 114, 101, 99, 105, 115, 105, 111,
			110, 59, 32, 105, 109, 97, 103, 101, 45, 114,
			101, 110, 100, 101, 114, 105, 110, 103, 58, 111,
			112, 116, 105, 109, 105, 122, 101, 81, 117, 97,
			108, 105, 116, 121, 59, 32, 102, 105, 108, 108,
			45, 114, 117, 108, 101, 58, 101, 118, 101, 110,
			111, 100, 100, 59, 32, 99, 108, 105, 112, 45,
			114, 117, 108, 101, 58, 101, 118, 101, 110, 111,
			100, 100, 34, 13, 10, 118, 105, 101, 119, 66,
			111, 120, 61, 34, 48, 32, 48, 32, 49, 50,
			55, 49, 46, 49, 54, 32, 49, 50, 55, 49,
			46, 49, 54, 34, 13, 10, 32, 120, 109, 108,
			110, 115, 58, 120, 108, 105, 110, 107, 61, 34,
			104, 116, 116, 112, 58, 47, 47, 119, 119, 119,
			46, 119, 51, 46, 111, 114, 103, 47, 49, 57,
			57, 57, 47, 120, 108, 105, 110, 107, 34, 13,
			10, 32, 120, 109, 108, 110, 115, 58, 120, 111,
			100, 109, 61, 34, 104, 116, 116, 112, 58, 47,
			47, 119, 119, 119, 46, 99, 111, 114, 101, 108,
			46, 99, 111, 109, 47, 99, 111, 114, 101, 108,
			100, 114, 97, 119, 47, 111, 100, 109, 47, 50,
			48, 48, 51, 34, 62, 13, 10, 32, 60, 100,
			101, 102, 115, 62, 13, 10, 32, 32, 60, 115,
			116, 121, 108, 101, 32, 116, 121, 112, 101, 61,
			34, 116, 101, 120, 116, 47, 99, 115, 115, 34,
			62, 13, 10, 32, 32, 32, 60, 33, 91, 67,
			68, 65, 84, 65, 91, 13, 10, 32, 32, 32,
			32, 46, 102, 105, 108, 48, 32, 123, 102, 105,
			108, 108, 58, 98, 108, 97, 99, 107, 59, 102,
			105, 108, 108, 45, 114, 117, 108, 101, 58, 110,
			111, 110, 122, 101, 114, 111, 125, 13, 10, 32,
			32, 32, 93, 93, 62, 13, 10, 32, 32, 60,
			47, 115, 116, 121, 108, 101, 62, 13, 10, 32,
			60, 47, 100, 101, 102, 115, 62, 13, 10, 32,
			60, 103, 32, 105, 100, 61, 34, 208, 161, 208,
			187, 208, 190, 208, 185, 95, 120, 48, 48, 50,
			48, 95, 49, 34, 62, 13, 10, 32, 32, 60,
			109, 101, 116, 97, 100, 97, 116, 97, 32, 105,
			100, 61, 34, 67, 111, 114, 101, 108, 67, 111,
			114, 112, 73, 68, 95, 48, 67, 111, 114, 101,
			108, 45, 76, 97, 121, 101, 114, 34, 47, 62,
			13, 10, 32, 32, 60, 112, 97, 116, 104, 32,
			99, 108, 97, 115, 115, 61, 34, 102, 105, 108,
			48, 34, 32, 100, 61, 34, 77, 49, 49, 56,
			48, 46, 51, 54, 32, 49, 56, 49, 46, 54,
			99, 49, 50, 46, 55, 55, 44, 48, 32, 50,
			52, 46, 53, 57, 44, 50, 46, 51, 54, 32,
			51, 53, 46, 52, 54, 44, 55, 46, 48, 57,
			32, 49, 48, 46, 56, 56, 44, 52, 46, 55,
			51, 32, 50, 48, 46, 51, 52, 44, 49, 49,
			46, 51, 53, 32, 50, 56, 46, 51, 56, 44,
			49, 57, 46, 56, 54, 32, 56, 46, 53, 50,
			44, 56, 46, 48, 52, 32, 49, 53, 46, 49,
			51, 44, 49, 55, 46, 53, 32, 49, 57, 46,
			56, 55, 44, 50, 56, 46, 51, 55, 32, 52,
			46, 55, 50, 44, 49, 48, 46, 56, 56, 32,
			55, 46, 48, 57, 44, 50, 50, 46, 55, 32,
			55, 46, 48, 57, 44, 51, 53, 46, 52, 55,
			108, 48, 32, 57, 57, 56, 46, 55, 54, 32,
			45, 57, 55, 50, 46, 53, 50, 32, 48, 32,
			45, 49, 49, 55, 46, 48, 53, 32, 45, 49,
			49, 55, 46, 55, 53, 32, 48, 32, 45, 49,
			53, 52, 46, 54, 51, 32, 45, 54, 52, 46,
			53, 53, 32, 48, 32, 45, 49, 49, 55, 46,
			48, 52, 32, 45, 49, 49, 55, 46, 55, 53,
			32, 48, 32, 45, 55, 57, 48, 46, 50, 50,
			99, 48, 44, 45, 49, 50, 46, 55, 55, 32,
			50, 46, 51, 54, 44, 45, 50, 52, 46, 53,
			57, 32, 55, 46, 48, 57, 44, 45, 51, 53,
			46, 52, 55, 32, 52, 46, 55, 51, 44, 45,
			49, 48, 46, 56, 56, 32, 49, 49, 46, 49,
			49, 44, 45, 50, 48, 46, 51, 52, 32, 49,
			57, 46, 49, 53, 44, 45, 50, 56, 46, 51,
			55, 32, 56, 46, 53, 49, 44, 45, 56, 46,
			53, 50, 32, 49, 56, 46, 50, 49, 44, 45,
			49, 53, 46, 49, 51, 32, 50, 57, 46, 48,
			56, 44, 45, 49, 57, 46, 56, 54, 32, 49,
			48, 46, 56, 56, 44, 45, 52, 46, 55, 51,
			32, 50, 50, 46, 55, 44, 45, 55, 46, 48,
			57, 32, 51, 53, 46, 52, 55, 44, 45, 55,
			46, 48, 57, 108, 56, 49, 55, 46, 49, 55,
			32, 48, 99, 49, 50, 46, 55, 55, 44, 48,
			32, 50, 52, 46, 53, 57, 44, 50, 46, 51,
			54, 32, 51, 53, 46, 52, 55, 44, 55, 46,
			48, 57, 32, 49, 48, 46, 56, 56, 44, 52,
			46, 55, 51, 32, 50, 48, 46, 51, 51, 44,
			49, 49, 46, 51, 53, 32, 50, 56, 46, 51,
			55, 44, 49, 57, 46, 56, 54, 32, 56, 46,
			53, 49, 44, 56, 46, 48, 52, 32, 49, 53,
			46, 49, 51, 44, 49, 55, 46, 53, 32, 49,
			57, 46, 56, 54, 44, 50, 56, 46, 51, 55,
			32, 52, 46, 55, 51, 44, 49, 48, 46, 56,
			56, 32, 55, 46, 48, 57, 44, 50, 50, 46,
			55, 32, 55, 46, 48, 57, 44, 51, 53, 46,
			52, 55, 108, 48, 32, 57, 48, 46, 56, 32,
			49, 56, 49, 46, 53, 57, 32, 48, 122, 109,
			45, 55, 50, 54, 46, 51, 55, 32, 52, 53,
			51, 46, 57, 56, 108, 53, 52, 52, 46, 55,
			56, 32, 48, 32, 48, 32, 45, 51, 54, 51,
			46, 49, 57, 32, 45, 53, 52, 52, 46, 55,
			56, 32, 48, 32, 48, 32, 51, 54, 51, 46,
			49, 57, 122, 109, 45, 50, 55, 50, 46, 51,
			57, 32, 50, 55, 50, 46, 51, 57, 108, 48,
			32, 45, 54, 51, 53, 46, 53, 56, 99, 48,
			44, 45, 49, 50, 46, 55, 55, 32, 50, 46,
			51, 54, 44, 45, 50, 52, 46, 53, 57, 32,
			55, 46, 48, 57, 44, 45, 51, 53, 46, 52,
			55, 32, 52, 46, 55, 51, 44, 45, 49, 48,
			46, 56, 56, 32, 49, 49, 46, 49, 49, 44,
			45, 50, 48, 46, 51, 51, 32, 49, 57, 46,
			49, 53, 44, 45, 50, 56, 46, 51, 55, 32,
			56, 46, 53, 50, 44, 45, 56, 46, 53, 49,
			32, 49, 56, 46, 50, 49, 44, 45, 49, 53,
			46, 49, 51, 32, 50, 57, 46, 48, 57, 44,
			45, 49, 57, 46, 56, 54, 32, 49, 48, 46,
			56, 56, 44, 45, 52, 46, 55, 51, 32, 50,
			50, 46, 55, 44, 45, 55, 46, 48, 57, 32,
			51, 53, 46, 52, 54, 44, 45, 55, 46, 48,
			57, 108, 54, 51, 53, 46, 53, 56, 32, 48,
			32, 48, 32, 45, 57, 48, 46, 56, 32, 45,
			56, 49, 55, 46, 49, 55, 32, 48, 32, 48,
			32, 55, 53, 50, 46, 54, 50, 32, 54, 52,
			46, 53, 53, 32, 54, 52, 46, 53, 53, 32,
			50, 54, 46, 50, 52, 32, 48, 122, 109, 55,
			50, 54, 46, 51, 56, 32, 48, 108, 45, 52,
			53, 51, 46, 57, 56, 32, 48, 32, 48, 32,
			50, 55, 50, 46, 51, 57, 32, 57, 48, 46,
			56, 32, 48, 32, 48, 32, 45, 49, 56, 49,
			46, 53, 57, 32, 57, 48, 46, 56, 32, 48,
			32, 48, 32, 49, 56, 49, 46, 53, 57, 32,
			50, 55, 50, 46, 51, 57, 32, 48, 32, 48,
			32, 45, 50, 55, 50, 46, 51, 57, 122, 109,
			50, 55, 50, 46, 51, 57, 32, 45, 54, 51,
			53, 46, 53, 56, 108, 45, 57, 48, 46, 56,
			32, 48, 32, 48, 32, 52, 53, 51, 46, 57,
			56, 32, 45, 55, 50, 54, 46, 51, 55, 32,
			48, 32, 48, 32, 45, 52, 53, 51, 46, 57,
			56, 32, 45, 57, 48, 46, 56, 32, 48, 32,
			48, 32, 56, 52, 51, 46, 52, 50, 32, 54,
			52, 46, 53, 53, 32, 54, 52, 46, 53, 53,
			32, 50, 54, 46, 50, 53, 32, 48, 32, 48,
			32, 45, 51, 54, 51, 46, 49, 57, 32, 54,
			51, 53, 46, 53, 56, 32, 48, 32, 48, 32,
			51, 54, 51, 46, 49, 57, 32, 49, 56, 49,
			46, 53, 57, 32, 48, 32, 48, 32, 45, 57,
			48, 55, 46, 57, 55, 122, 34, 47, 62, 13,
			10, 32, 60, 47, 103, 62, 13, 10, 60, 47,
			115, 118, 103, 62, 13, 10
		};
		byte[] pasteIcon = new byte[3624]
		{
			255, 254, 60, 0, 63, 0, 120, 0, 109, 0,
			108, 0, 32, 0, 118, 0, 101, 0, 114, 0,
			115, 0, 105, 0, 111, 0, 110, 0, 61, 0,
			34, 0, 49, 0, 46, 0, 48, 0, 34, 0,
			32, 0, 101, 0, 110, 0, 99, 0, 111, 0,
			100, 0, 105, 0, 110, 0, 103, 0, 61, 0,
			34, 0, 85, 0, 84, 0, 70, 0, 45, 0,
			49, 0, 54, 0, 34, 0, 63, 0, 62, 0,
			13, 0, 10, 0, 60, 0, 33, 0, 68, 0,
			79, 0, 67, 0, 84, 0, 89, 0, 80, 0,
			69, 0, 32, 0, 115, 0, 118, 0, 103, 0,
			32, 0, 80, 0, 85, 0, 66, 0, 76, 0,
			73, 0, 67, 0, 32, 0, 34, 0, 45, 0,
			47, 0, 47, 0, 87, 0, 51, 0, 67, 0,
			47, 0, 47, 0, 68, 0, 84, 0, 68, 0,
			32, 0, 83, 0, 86, 0, 71, 0, 32, 0,
			49, 0, 46, 0, 49, 0, 47, 0, 47, 0,
			69, 0, 78, 0, 34, 0, 32, 0, 34, 0,
			104, 0, 116, 0, 116, 0, 112, 0, 58, 0,
			47, 0, 47, 0, 119, 0, 119, 0, 119, 0,
			46, 0, 119, 0, 51, 0, 46, 0, 111, 0,
			114, 0, 103, 0, 47, 0, 71, 0, 114, 0,
			97, 0, 112, 0, 104, 0, 105, 0, 99, 0,
			115, 0, 47, 0, 83, 0, 86, 0, 71, 0,
			47, 0, 49, 0, 46, 0, 49, 0, 47, 0,
			68, 0, 84, 0, 68, 0, 47, 0, 115, 0,
			118, 0, 103, 0, 49, 0, 49, 0, 46, 0,
			100, 0, 116, 0, 100, 0, 34, 0, 62, 0,
			13, 0, 10, 0, 60, 0, 33, 0, 45, 0,
			45, 0, 32, 0, 67, 0, 114, 0, 101, 0,
			97, 0, 116, 0, 111, 0, 114, 0, 58, 0,
			32, 0, 67, 0, 111, 0, 114, 0, 101, 0,
			108, 0, 68, 0, 82, 0, 65, 0, 87, 0,
			32, 0, 50, 0, 48, 0, 50, 0, 48, 0,
			32, 0, 40, 0, 54, 0, 52, 0, 45, 0,
			66, 0, 105, 0, 116, 0, 41, 0, 32, 0,
			45, 0, 45, 0, 62, 0, 13, 0, 10, 0,
			60, 0, 115, 0, 118, 0, 103, 0, 32, 0,
			120, 0, 109, 0, 108, 0, 110, 0, 115, 0,
			61, 0, 34, 0, 104, 0, 116, 0, 116, 0,
			112, 0, 58, 0, 47, 0, 47, 0, 119, 0,
			119, 0, 119, 0, 46, 0, 119, 0, 51, 0,
			46, 0, 111, 0, 114, 0, 103, 0, 47, 0,
			50, 0, 48, 0, 48, 0, 48, 0, 47, 0,
			115, 0, 118, 0, 103, 0, 34, 0, 32, 0,
			120, 0, 109, 0, 108, 0, 58, 0, 115, 0,
			112, 0, 97, 0, 99, 0, 101, 0, 61, 0,
			34, 0, 112, 0, 114, 0, 101, 0, 115, 0,
			101, 0, 114, 0, 118, 0, 101, 0, 34, 0,
			32, 0, 119, 0, 105, 0, 100, 0, 116, 0,
			104, 0, 61, 0, 34, 0, 50, 0, 56, 0,
			46, 0, 55, 0, 48, 0, 55, 0, 51, 0,
			109, 0, 109, 0, 34, 0, 32, 0, 104, 0,
			101, 0, 105, 0, 103, 0, 104, 0, 116, 0,
			61, 0, 34, 0, 51, 0, 50, 0, 46, 0,
			56, 0, 48, 0, 56, 0, 52, 0, 109, 0,
			109, 0, 34, 0, 32, 0, 118, 0, 101, 0,
			114, 0, 115, 0, 105, 0, 111, 0, 110, 0,
			61, 0, 34, 0, 49, 0, 46, 0, 49, 0,
			34, 0, 32, 0, 115, 0, 116, 0, 121, 0,
			108, 0, 101, 0, 61, 0, 34, 0, 115, 0,
			104, 0, 97, 0, 112, 0, 101, 0, 45, 0,
			114, 0, 101, 0, 110, 0, 100, 0, 101, 0,
			114, 0, 105, 0, 110, 0, 103, 0, 58, 0,
			103, 0, 101, 0, 111, 0, 109, 0, 101, 0,
			116, 0, 114, 0, 105, 0, 99, 0, 80, 0,
			114, 0, 101, 0, 99, 0, 105, 0, 115, 0,
			105, 0, 111, 0, 110, 0, 59, 0, 32, 0,
			116, 0, 101, 0, 120, 0, 116, 0, 45, 0,
			114, 0, 101, 0, 110, 0, 100, 0, 101, 0,
			114, 0, 105, 0, 110, 0, 103, 0, 58, 0,
			103, 0, 101, 0, 111, 0, 109, 0, 101, 0,
			116, 0, 114, 0, 105, 0, 99, 0, 80, 0,
			114, 0, 101, 0, 99, 0, 105, 0, 115, 0,
			105, 0, 111, 0, 110, 0, 59, 0, 32, 0,
			105, 0, 109, 0, 97, 0, 103, 0, 101, 0,
			45, 0, 114, 0, 101, 0, 110, 0, 100, 0,
			101, 0, 114, 0, 105, 0, 110, 0, 103, 0,
			58, 0, 111, 0, 112, 0, 116, 0, 105, 0,
			109, 0, 105, 0, 122, 0, 101, 0, 81, 0,
			117, 0, 97, 0, 108, 0, 105, 0, 116, 0,
			121, 0, 59, 0, 32, 0, 102, 0, 105, 0,
			108, 0, 108, 0, 45, 0, 114, 0, 117, 0,
			108, 0, 101, 0, 58, 0, 101, 0, 118, 0,
			101, 0, 110, 0, 111, 0, 100, 0, 100, 0,
			59, 0, 32, 0, 99, 0, 108, 0, 105, 0,
			112, 0, 45, 0, 114, 0, 117, 0, 108, 0,
			101, 0, 58, 0, 101, 0, 118, 0, 101, 0,
			110, 0, 111, 0, 100, 0, 100, 0, 34, 0,
			13, 0, 10, 0, 118, 0, 105, 0, 101, 0,
			119, 0, 66, 0, 111, 0, 120, 0, 61, 0,
			34, 0, 48, 0, 32, 0, 48, 0, 32, 0,
			49, 0, 53, 0, 48, 0, 52, 0, 46, 0,
			57, 0, 52, 0, 32, 0, 49, 0, 55, 0,
			49, 0, 57, 0, 46, 0, 57, 0, 52, 0,
			34, 0, 13, 0, 10, 0, 32, 0, 120, 0,
			109, 0, 108, 0, 110, 0, 115, 0, 58, 0,
			120, 0, 108, 0, 105, 0, 110, 0, 107, 0,
			61, 0, 34, 0, 104, 0, 116, 0, 116, 0,
			112, 0, 58, 0, 47, 0, 47, 0, 119, 0,
			119, 0, 119, 0, 46, 0, 119, 0, 51, 0,
			46, 0, 111, 0, 114, 0, 103, 0, 47, 0,
			49, 0, 57, 0, 57, 0, 57, 0, 47, 0,
			120, 0, 108, 0, 105, 0, 110, 0, 107, 0,
			34, 0, 13, 0, 10, 0, 32, 0, 120, 0,
			109, 0, 108, 0, 110, 0, 115, 0, 58, 0,
			120, 0, 111, 0, 100, 0, 109, 0, 61, 0,
			34, 0, 104, 0, 116, 0, 116, 0, 112, 0,
			58, 0, 47, 0, 47, 0, 119, 0, 119, 0,
			119, 0, 46, 0, 99, 0, 111, 0, 114, 0,
			101, 0, 108, 0, 46, 0, 99, 0, 111, 0,
			109, 0, 47, 0, 99, 0, 111, 0, 114, 0,
			101, 0, 108, 0, 100, 0, 114, 0, 97, 0,
			119, 0, 47, 0, 111, 0, 100, 0, 109, 0,
			47, 0, 50, 0, 48, 0, 48, 0, 51, 0,
			34, 0, 62, 0, 13, 0, 10, 0, 32, 0,
			60, 0, 100, 0, 101, 0, 102, 0, 115, 0,
			62, 0, 13, 0, 10, 0, 32, 0, 32, 0,
			60, 0, 115, 0, 116, 0, 121, 0, 108, 0,
			101, 0, 32, 0, 116, 0, 121, 0, 112, 0,
			101, 0, 61, 0, 34, 0, 116, 0, 101, 0,
			120, 0, 116, 0, 47, 0, 99, 0, 115, 0,
			115, 0, 34, 0, 62, 0, 13, 0, 10, 0,
			32, 0, 32, 0, 32, 0, 60, 0, 33, 0,
			91, 0, 67, 0, 68, 0, 65, 0, 84, 0,
			65, 0, 91, 0, 13, 0, 10, 0, 32, 0,
			32, 0, 32, 0, 32, 0, 46, 0, 102, 0,
			105, 0, 108, 0, 48, 0, 32, 0, 123, 0,
			102, 0, 105, 0, 108, 0, 108, 0, 58, 0,
			98, 0, 108, 0, 97, 0, 99, 0, 107, 0,
			59, 0, 102, 0, 105, 0, 108, 0, 108, 0,
			45, 0, 114, 0, 117, 0, 108, 0, 101, 0,
			58, 0, 110, 0, 111, 0, 110, 0, 122, 0,
			101, 0, 114, 0, 111, 0, 125, 0, 13, 0,
			10, 0, 32, 0, 32, 0, 32, 0, 93, 0,
			93, 0, 62, 0, 13, 0, 10, 0, 32, 0,
			32, 0, 60, 0, 47, 0, 115, 0, 116, 0,
			121, 0, 108, 0, 101, 0, 62, 0, 13, 0,
			10, 0, 32, 0, 60, 0, 47, 0, 100, 0,
			101, 0, 102, 0, 115, 0, 62, 0, 13, 0,
			10, 0, 32, 0, 60, 0, 103, 0, 32, 0,
			105, 0, 100, 0, 61, 0, 34, 0, 33, 4,
			62, 4, 52, 4, 53, 4, 64, 4, 54, 4,
			56, 4, 60, 4, 62, 4, 53, 4, 95, 0,
			120, 0, 48, 0, 48, 0, 50, 0, 48, 0,
			95, 0, 80, 0, 111, 0, 119, 0, 101, 0,
			114, 0, 67, 0, 108, 0, 105, 0, 112, 0,
			34, 0, 62, 0, 13, 0, 10, 0, 32, 0,
			32, 0, 60, 0, 109, 0, 101, 0, 116, 0,
			97, 0, 100, 0, 97, 0, 116, 0, 97, 0,
			32, 0, 105, 0, 100, 0, 61, 0, 34, 0,
			67, 0, 111, 0, 114, 0, 101, 0, 108, 0,
			67, 0, 111, 0, 114, 0, 112, 0, 73, 0,
			68, 0, 95, 0, 48, 0, 67, 0, 111, 0,
			114, 0, 101, 0, 108, 0, 45, 0, 76, 0,
			97, 0, 121, 0, 101, 0, 114, 0, 34, 0,
			47, 0, 62, 0, 13, 0, 10, 0, 32, 0,
			32, 0, 60, 0, 112, 0, 97, 0, 116, 0,
			104, 0, 32, 0, 99, 0, 108, 0, 97, 0,
			115, 0, 115, 0, 61, 0, 34, 0, 102, 0,
			105, 0, 108, 0, 48, 0, 34, 0, 32, 0,
			100, 0, 61, 0, 34, 0, 77, 0, 49, 0,
			53, 0, 48, 0, 52, 0, 46, 0, 57, 0,
			52, 0, 32, 0, 54, 0, 52, 0, 52, 0,
			46, 0, 57, 0, 55, 0, 108, 0, 48, 0,
			32, 0, 49, 0, 48, 0, 55, 0, 52, 0,
			46, 0, 57, 0, 54, 0, 32, 0, 45, 0,
			56, 0, 53, 0, 57, 0, 46, 0, 57, 0,
			55, 0, 32, 0, 48, 0, 32, 0, 48, 0,
			32, 0, 45, 0, 49, 0, 48, 0, 55, 0,
			46, 0, 53, 0, 32, 0, 45, 0, 54, 0,
			52, 0, 52, 0, 46, 0, 57, 0, 55, 0,
			32, 0, 48, 0, 32, 0, 48, 0, 32, 0,
			45, 0, 49, 0, 51, 0, 57, 0, 55, 0,
			46, 0, 52, 0, 52, 0, 32, 0, 52, 0,
			50, 0, 57, 0, 46, 0, 57, 0, 56, 0,
			32, 0, 48, 0, 99, 0, 48, 0, 44, 0,
			45, 0, 50, 0, 57, 0, 46, 0, 49, 0,
			50, 0, 32, 0, 53, 0, 46, 0, 54, 0,
			44, 0, 45, 0, 53, 0, 54, 0, 46, 0,
			56, 0, 51, 0, 32, 0, 49, 0, 54, 0,
			46, 0, 56, 0, 44, 0, 45, 0, 56, 0,
			51, 0, 46, 0, 49, 0, 52, 0, 32, 0,
			49, 0, 49, 0, 46, 0, 55, 0, 54, 0,
			44, 0, 45, 0, 50, 0, 54, 0, 46, 0,
			51, 0, 49, 0, 32, 0, 50, 0, 55, 0,
			46, 0, 49, 0, 54, 0, 44, 0, 45, 0,
			52, 0, 56, 0, 46, 0, 57, 0, 57, 0,
			32, 0, 52, 0, 54, 0, 46, 0, 49, 0,
			57, 0, 44, 0, 45, 0, 54, 0, 56, 0,
			46, 0, 48, 0, 50, 0, 32, 0, 49, 0,
			57, 0, 46, 0, 54, 0, 44, 0, 45, 0,
			49, 0, 57, 0, 46, 0, 54, 0, 32, 0,
			52, 0, 50, 0, 46, 0, 50, 0, 55, 0,
			44, 0, 45, 0, 51, 0, 52, 0, 46, 0,
			57, 0, 57, 0, 32, 0, 54, 0, 56, 0,
			46, 0, 48, 0, 50, 0, 44, 0, 45, 0,
			52, 0, 54, 0, 46, 0, 49, 0, 57, 0,
			32, 0, 50, 0, 54, 0, 46, 0, 51, 0,
			49, 0, 44, 0, 45, 0, 49, 0, 49, 0,
			46, 0, 55, 0, 53, 0, 32, 0, 53, 0,
			52, 0, 46, 0, 51, 0, 49, 0, 44, 0,
			45, 0, 49, 0, 55, 0, 46, 0, 54, 0,
			52, 0, 32, 0, 56, 0, 51, 0, 46, 0,
			57, 0, 56, 0, 44, 0, 45, 0, 49, 0,
			55, 0, 46, 0, 54, 0, 52, 0, 32, 0,
			50, 0, 57, 0, 46, 0, 49, 0, 50, 0,
			44, 0, 48, 0, 32, 0, 53, 0, 54, 0,
			46, 0, 56, 0, 51, 0, 44, 0, 53, 0,
			46, 0, 56, 0, 56, 0, 32, 0, 56, 0,
			51, 0, 46, 0, 49, 0, 52, 0, 44, 0,
			49, 0, 55, 0, 46, 0, 54, 0, 52, 0,
			32, 0, 50, 0, 54, 0, 46, 0, 51, 0,
			49, 0, 44, 0, 49, 0, 49, 0, 46, 0,
			50, 0, 32, 0, 52, 0, 56, 0, 46, 0,
			57, 0, 57, 0, 44, 0, 50, 0, 54, 0,
			46, 0, 53, 0, 57, 0, 32, 0, 54, 0,
			56, 0, 46, 0, 48, 0, 50, 0, 44, 0,
			52, 0, 54, 0, 46, 0, 49, 0, 57, 0,
			32, 0, 49, 0, 57, 0, 46, 0, 54, 0,
			44, 0, 49, 0, 57, 0, 46, 0, 48, 0,
			52, 0, 32, 0, 51, 0, 52, 0, 46, 0,
			57, 0, 57, 0, 44, 0, 52, 0, 49, 0,
			46, 0, 55, 0, 49, 0, 32, 0, 52, 0,
			54, 0, 46, 0, 49, 0, 57, 0, 44, 0,
			54, 0, 56, 0, 46, 0, 48, 0, 50, 0,
			32, 0, 49, 0, 49, 0, 46, 0, 55, 0,
			53, 0, 44, 0, 50, 0, 54, 0, 46, 0,
			51, 0, 50, 0, 32, 0, 49, 0, 55, 0,
			46, 0, 54, 0, 52, 0, 44, 0, 53, 0,
			52, 0, 46, 0, 48, 0, 51, 0, 32, 0,
			49, 0, 55, 0, 46, 0, 54, 0, 52, 0,
			44, 0, 56, 0, 51, 0, 46, 0, 49, 0,
			52, 0, 108, 0, 52, 0, 50, 0, 57, 0,
			46, 0, 57, 0, 56, 0, 32, 0, 48, 0,
			32, 0, 48, 0, 32, 0, 52, 0, 50, 0,
			57, 0, 46, 0, 57, 0, 56, 0, 32, 0,
			50, 0, 49, 0, 52, 0, 46, 0, 57, 0,
			57, 0, 32, 0, 48, 0, 122, 0, 109, 0,
			45, 0, 49, 0, 49, 0, 56, 0, 50, 0,
			46, 0, 52, 0, 53, 0, 32, 0, 45, 0,
			51, 0, 50, 0, 50, 0, 46, 0, 52, 0,
			56, 0, 108, 0, 48, 0, 32, 0, 49, 0,
			48, 0, 55, 0, 46, 0, 52, 0, 57, 0,
			32, 0, 54, 0, 52, 0, 52, 0, 46, 0,
			57, 0, 55, 0, 32, 0, 48, 0, 32, 0,
			48, 0, 32, 0, 45, 0, 49, 0, 48, 0,
			55, 0, 46, 0, 52, 0, 57, 0, 32, 0,
			45, 0, 50, 0, 49, 0, 52, 0, 46, 0,
			57, 0, 57, 0, 32, 0, 48, 0, 99, 0,
			48, 0, 44, 0, 45, 0, 49, 0, 52, 0,
			32, 0, 48, 0, 46, 0, 50, 0, 56, 0,
			44, 0, 45, 0, 50, 0, 56, 0, 46, 0,
			56, 0, 51, 0, 32, 0, 48, 0, 46, 0,
			56, 0, 52, 0, 44, 0, 45, 0, 52, 0,
			52, 0, 46, 0, 53, 0, 49, 0, 32, 0,
			48, 0, 46, 0, 53, 0, 54, 0, 44, 0,
			45, 0, 49, 0, 54, 0, 46, 0, 50, 0,
			52, 0, 32, 0, 48, 0, 46, 0, 50, 0,
			56, 0, 44, 0, 45, 0, 51, 0, 50, 0,
			46, 0, 49, 0, 57, 0, 32, 0, 45, 0,
			48, 0, 46, 0, 56, 0, 52, 0, 44, 0,
			45, 0, 52, 0, 55, 0, 46, 0, 56, 0,
			55, 0, 32, 0, 45, 0, 49, 0, 46, 0,
			49, 0, 50, 0, 44, 0, 45, 0, 49, 0,
			54, 0, 46, 0, 50, 0, 52, 0, 32, 0,
			45, 0, 51, 0, 46, 0, 54, 0, 52, 0,
			44, 0, 45, 0, 51, 0, 49, 0, 46, 0,
			54, 0, 51, 0, 32, 0, 45, 0, 55, 0,
			46, 0, 53, 0, 54, 0, 44, 0, 45, 0,
			52, 0, 54, 0, 46, 0, 49, 0, 57, 0,
			32, 0, 45, 0, 51, 0, 46, 0, 51, 0,
			54, 0, 44, 0, 45, 0, 49, 0, 53, 0,
			46, 0, 49, 0, 49, 0, 32, 0, 45, 0,
			56, 0, 46, 0, 57, 0, 54, 0, 44, 0,
			45, 0, 50, 0, 56, 0, 46, 0, 50, 0,
			55, 0, 32, 0, 45, 0, 49, 0, 54, 0,
			46, 0, 56, 0, 44, 0, 45, 0, 51, 0,
			57, 0, 46, 0, 52, 0, 55, 0, 32, 0,
			45, 0, 55, 0, 46, 0, 56, 0, 52, 0,
			44, 0, 45, 0, 49, 0, 49, 0, 46, 0,
			50, 0, 32, 0, 45, 0, 49, 0, 56, 0,
			46, 0, 52, 0, 55, 0, 44, 0, 45, 0,
			50, 0, 48, 0, 46, 0, 49, 0, 54, 0,
			32, 0, 45, 0, 51, 0, 49, 0, 46, 0,
			57, 0, 50, 0, 44, 0, 45, 0, 50, 0,
			54, 0, 46, 0, 56, 0, 55, 0, 32, 0,
			45, 0, 49, 0, 51, 0, 46, 0, 52, 0,
			52, 0, 44, 0, 45, 0, 54, 0, 46, 0,
			55, 0, 50, 0, 32, 0, 45, 0, 51, 0,
			48, 0, 46, 0, 53, 0, 49, 0, 44, 0,
			45, 0, 49, 0, 48, 0, 46, 0, 48, 0,
			56, 0, 32, 0, 45, 0, 53, 0, 49, 0,
			46, 0, 50, 0, 51, 0, 44, 0, 45, 0,
			49, 0, 48, 0, 46, 0, 48, 0, 56, 0,
			32, 0, 45, 0, 51, 0, 49, 0, 46, 0,
			51, 0, 53, 0, 44, 0, 48, 0, 32, 0,
			45, 0, 53, 0, 52, 0, 46, 0, 53, 0,
			56, 0, 44, 0, 55, 0, 46, 0, 50, 0,
			56, 0, 32, 0, 45, 0, 54, 0, 57, 0,
			46, 0, 55, 0, 44, 0, 50, 0, 49, 0,
			46, 0, 56, 0, 51, 0, 32, 0, 45, 0,
			49, 0, 52, 0, 46, 0, 53, 0, 54, 0,
			44, 0, 49, 0, 52, 0, 46, 0, 53, 0,
			54, 0, 32, 0, 45, 0, 50, 0, 52, 0,
			46, 0, 57, 0, 49, 0, 44, 0, 51, 0,
			50, 0, 46, 0, 55, 0, 53, 0, 32, 0,
			45, 0, 51, 0, 49, 0, 46, 0, 48, 0,
			55, 0, 44, 0, 53, 0, 52, 0, 46, 0,
			53, 0, 57, 0, 32, 0, 45, 0, 53, 0,
			46, 0, 54, 0, 44, 0, 50, 0, 49, 0,
			46, 0, 50, 0, 56, 0, 32, 0, 45, 0,
			56, 0, 46, 0, 49, 0, 50, 0, 44, 0,
			52, 0, 52, 0, 46, 0, 53, 0, 49, 0,
			32, 0, 45, 0, 55, 0, 46, 0, 53, 0,
			54, 0, 44, 0, 54, 0, 57, 0, 46, 0,
			55, 0, 49, 0, 32, 0, 48, 0, 46, 0,
			53, 0, 54, 0, 44, 0, 50, 0, 53, 0,
			46, 0, 49, 0, 57, 0, 32, 0, 48, 0,
			46, 0, 56, 0, 52, 0, 44, 0, 52, 0,
			56, 0, 46, 0, 49, 0, 53, 0, 32, 0,
			48, 0, 46, 0, 56, 0, 52, 0, 44, 0,
			54, 0, 56, 0, 46, 0, 56, 0, 54, 0,
			108, 0, 45, 0, 50, 0, 49, 0, 52, 0,
			46, 0, 57, 0, 57, 0, 32, 0, 48, 0,
			122, 0, 109, 0, 51, 0, 50, 0, 50, 0,
			46, 0, 52, 0, 56, 0, 32, 0, 49, 0,
			49, 0, 56, 0, 50, 0, 46, 0, 52, 0,
			53, 0, 108, 0, 48, 0, 32, 0, 45, 0,
			56, 0, 53, 0, 57, 0, 46, 0, 57, 0,
			55, 0, 32, 0, 53, 0, 51, 0, 55, 0,
			46, 0, 52, 0, 56, 0, 32, 0, 48, 0,
			32, 0, 48, 0, 32, 0, 45, 0, 51, 0,
			50, 0, 50, 0, 46, 0, 52, 0, 56, 0,
			32, 0, 45, 0, 49, 0, 48, 0, 55, 0,
			46, 0, 52, 0, 57, 0, 32, 0, 48, 0,
			32, 0, 48, 0, 32, 0, 50, 0, 49, 0,
			52, 0, 46, 0, 57, 0, 57, 0, 32, 0,
			45, 0, 56, 0, 53, 0, 57, 0, 46, 0,
			57, 0, 54, 0, 32, 0, 48, 0, 32, 0,
			48, 0, 32, 0, 45, 0, 50, 0, 49, 0,
			52, 0, 46, 0, 57, 0, 57, 0, 32, 0,
			45, 0, 49, 0, 48, 0, 55, 0, 46, 0,
			53, 0, 32, 0, 48, 0, 32, 0, 48, 0,
			32, 0, 49, 0, 49, 0, 56, 0, 50, 0,
			46, 0, 52, 0, 53, 0, 32, 0, 53, 0,
			51, 0, 55, 0, 46, 0, 52, 0, 56, 0,
			32, 0, 48, 0, 122, 0, 109, 0, 55, 0,
			53, 0, 50, 0, 46, 0, 52, 0, 55, 0,
			32, 0, 45, 0, 55, 0, 53, 0, 50, 0,
			46, 0, 52, 0, 55, 0, 108, 0, 45, 0,
			54, 0, 52, 0, 52, 0, 46, 0, 57, 0,
			55, 0, 32, 0, 48, 0, 32, 0, 48, 0,
			32, 0, 56, 0, 53, 0, 57, 0, 46, 0,
			57, 0, 54, 0, 32, 0, 54, 0, 52, 0,
			52, 0, 46, 0, 57, 0, 55, 0, 32, 0,
			48, 0, 32, 0, 48, 0, 32, 0, 45, 0,
			56, 0, 53, 0, 57, 0, 46, 0, 57, 0,
			54, 0, 122, 0, 34, 0, 47, 0, 62, 0,
			13, 0, 10, 0, 32, 0, 60, 0, 47, 0,
			103, 0, 62, 0, 13, 0, 10, 0, 60, 0,
			47, 0, 115, 0, 118, 0, 103, 0, 62, 0,
			13, 0, 10, 0
		};
		byte[] psdIcon = new byte[2231]
		{
			60, 63, 120, 109, 108, 32, 118, 101, 114, 115,
			105, 111, 110, 61, 34, 49, 46, 48, 34, 32,
			101, 110, 99, 111, 100, 105, 110, 103, 61, 34,
			85, 84, 70, 45, 56, 34, 63, 62, 10, 60,
			115, 118, 103, 32, 119, 105, 100, 116, 104, 61,
			34, 49, 101, 51, 112, 116, 34, 32, 104, 101,
			105, 103, 104, 116, 61, 34, 49, 101, 51, 112,
			116, 34, 32, 118, 101, 114, 115, 105, 111, 110,
			61, 34, 49, 46, 48, 34, 32, 118, 105, 101,
			119, 66, 111, 120, 61, 34, 48, 32, 48, 32,
			49, 101, 51, 32, 49, 101, 51, 34, 32, 120,
			109, 108, 110, 115, 61, 34, 104, 116, 116, 112,
			58, 47, 47, 119, 119, 119, 46, 119, 51, 46,
			111, 114, 103, 47, 50, 48, 48, 48, 47, 115,
			118, 103, 34, 62, 10, 32, 60, 103, 32, 116,
			114, 97, 110, 115, 102, 111, 114, 109, 61, 34,
			109, 97, 116, 114, 105, 120, 40, 49, 32, 48,
			32, 48, 32, 49, 32, 45, 50, 48, 32, 49,
			50, 53, 41, 34, 62, 10, 32, 32, 60, 112,
			97, 116, 104, 32, 100, 61, 34, 109, 50, 53,
			50, 32, 53, 55, 99, 45, 55, 32, 50, 45,
			49, 50, 32, 53, 45, 49, 52, 32, 49, 49,
			45, 48, 46, 54, 32, 50, 45, 53, 32, 49,
			54, 45, 49, 48, 32, 51, 51, 45, 53, 32,
			49, 55, 45, 57, 32, 51, 48, 45, 57, 32,
			51, 49, 45, 48, 46, 50, 32, 48, 46, 53,
			45, 56, 45, 49, 49, 45, 49, 55, 45, 50,
			53, 45, 57, 45, 49, 52, 45, 49, 55, 45,
			50, 55, 45, 49, 56, 45, 50, 57, 45, 51,
			45, 51, 45, 49, 49, 45, 55, 45, 49, 54,
			45, 55, 45, 53, 32, 48, 45, 56, 32, 49,
			45, 49, 53, 32, 55, 45, 49, 55, 32, 49,
			51, 45, 51, 48, 32, 51, 48, 45, 51, 56,
			32, 53, 48, 45, 51, 32, 57, 45, 56, 55,
			32, 50, 57, 54, 45, 56, 57, 32, 51, 48,
			51, 45, 51, 32, 49, 52, 45, 49, 32, 51,
			53, 32, 51, 32, 52, 57, 32, 57, 32, 51,
			48, 32, 50, 55, 32, 53, 49, 32, 54, 48,
			32, 54, 57, 108, 53, 32, 51, 45, 49, 49,
			32, 51, 56, 99, 45, 49, 48, 32, 51, 52,
			45, 49, 49, 32, 51, 56, 45, 49, 49, 32,
			52, 50, 32, 48, 46, 52, 32, 53, 32, 50,
			32, 57, 32, 54, 32, 49, 51, 32, 52, 32,
			51, 32, 54, 32, 52, 32, 49, 51, 32, 52,
			32, 54, 32, 48, 32, 49, 48, 45, 50, 32,
			49, 53, 45, 53, 32, 53, 45, 52, 32, 54,
			45, 53, 32, 49, 56, 45, 52, 52, 108, 49,
			49, 45, 51, 54, 32, 49, 51, 45, 48, 46,
			52, 99, 56, 45, 48, 46, 51, 32, 49, 54,
			45, 48, 46, 56, 32, 49, 57, 45, 50, 32,
			49, 55, 45, 51, 32, 51, 53, 45, 49, 48,
			32, 52, 55, 45, 49, 55, 32, 50, 48, 45,
			49, 51, 32, 51, 55, 45, 51, 50, 32, 52,
			53, 45, 53, 51, 32, 49, 45, 51, 32, 49,
			55, 45, 53, 53, 32, 51, 53, 45, 49, 49,
			54, 32, 51, 54, 45, 49, 50, 49, 32, 51,
			53, 45, 49, 49, 53, 32, 51, 49, 45, 49,
			50, 50, 45, 50, 45, 53, 45, 56, 45, 57,
			45, 49, 52, 45, 49, 48, 45, 57, 45, 51,
			45, 55, 45, 52, 45, 53, 51, 32, 50, 49,
			45, 50, 51, 32, 49, 50, 45, 52, 49, 32,
			50, 50, 45, 52, 49, 32, 50, 49, 45, 48,
			46, 49, 45, 48, 46, 53, 32, 50, 45, 57,
			32, 54, 45, 49, 57, 108, 54, 45, 49, 56,
			32, 52, 56, 45, 50, 53, 99, 54, 53, 45,
			51, 52, 32, 54, 55, 45, 51, 53, 32, 54,
			57, 45, 51, 55, 32, 49, 45, 49, 32, 51,
			45, 51, 32, 52, 45, 53, 32, 50, 45, 51,
			32, 50, 45, 52, 32, 50, 45, 56, 45, 48,
			46, 50, 45, 49, 48, 45, 52, 45, 50, 56,
			45, 57, 45, 52, 49, 45, 54, 45, 49, 54,
			45, 49, 51, 45, 50, 56, 45, 50, 52, 45,
			52, 48, 45, 49, 52, 45, 49, 54, 45, 51,
			55, 45, 50, 57, 45, 53, 56, 45, 51, 52,
			45, 53, 45, 49, 45, 54, 45, 49, 45, 49,
			49, 32, 48, 46, 48, 55, 122, 109, 50, 48,
			32, 52, 52, 99, 56, 32, 53, 32, 49, 56,
			32, 49, 53, 32, 50, 53, 32, 50, 52, 32,
			53, 32, 56, 32, 49, 49, 32, 50, 53, 32,
			49, 49, 32, 51, 50, 108, 45, 48, 46, 48,
			55, 32, 51, 45, 51, 51, 32, 49, 56, 99,
			45, 49, 56, 32, 49, 48, 45, 51, 52, 32,
			49, 56, 45, 51, 52, 32, 49, 56, 45, 48,
			46, 50, 45, 48, 46, 50, 32, 48, 46, 51,
			45, 50, 32, 49, 45, 52, 32, 48, 46, 56,
			45, 50, 32, 50, 45, 54, 32, 50, 45, 56,
			32, 48, 46, 51, 45, 50, 32, 54, 45, 50,
			49, 32, 49, 50, 45, 52, 50, 32, 54, 45,
			50, 49, 32, 49, 49, 45, 51, 57, 32, 49,
			50, 45, 52, 49, 32, 48, 46, 52, 45, 49,
			32, 49, 45, 50, 32, 49, 45, 50, 32, 48,
			46, 52, 32, 48, 32, 50, 32, 48, 46, 55,
			32, 51, 32, 50, 122, 109, 45, 56, 57, 32,
			53, 50, 99, 49, 48, 32, 49, 55, 32, 49,
			57, 32, 51, 49, 32, 49, 57, 32, 51, 50,
			32, 48, 32, 48, 46, 56, 45, 50, 55, 32,
			57, 52, 45, 50, 56, 32, 57, 52, 45, 48,
			46, 48, 55, 32, 48, 46, 48, 55, 45, 57,
			45, 49, 53, 45, 50, 49, 45, 51, 52, 108,
			45, 50, 49, 45, 51, 52, 32, 49, 48, 45,
			51, 51, 99, 54, 45, 49, 56, 32, 49, 48,
			45, 51, 52, 32, 49, 48, 45, 51, 52, 32,
			48, 46, 49, 45, 52, 32, 49, 48, 45, 50,
			50, 32, 49, 50, 45, 50, 49, 32, 48, 46,
			50, 32, 48, 46, 51, 32, 57, 32, 49, 52,
			32, 49, 57, 32, 51, 49, 122, 109, 45, 52,
			52, 32, 49, 52, 51, 99, 49, 49, 32, 49,
			56, 32, 50, 48, 32, 51, 52, 32, 50, 48,
			32, 51, 52, 45, 48, 46, 52, 32, 52, 45,
			53, 51, 32, 49, 56, 51, 45, 53, 52, 32,
			49, 56, 52, 45, 49, 32, 48, 46, 57, 45,
			49, 48, 45, 53, 45, 49, 54, 45, 49, 49,
			45, 55, 45, 55, 45, 49, 49, 45, 49, 50,
			45, 49, 54, 45, 50, 49, 45, 55, 45, 49,
			51, 45, 49, 48, 45, 51, 48, 45, 56, 45,
			52, 51, 32, 48, 46, 54, 45, 52, 32, 53,
			49, 45, 49, 55, 55, 32, 53, 50, 45, 49,
			55, 56, 32, 48, 46, 50, 45, 48, 46, 51,
			32, 48, 46, 54, 45, 48, 46, 51, 32, 48,
			46, 57, 32, 48, 32, 48, 46, 51, 32, 48,
			46, 51, 32, 49, 48, 32, 49, 53, 32, 50,
			49, 32, 51, 52, 122, 109, 49, 51, 52, 32,
			53, 99, 45, 48, 46, 49, 32, 48, 46, 53,
			45, 52, 32, 49, 51, 45, 56, 32, 50, 56,
			45, 52, 32, 49, 53, 45, 56, 32, 50, 55,
			45, 56, 32, 50, 56, 45, 48, 46, 52, 32,
			49, 45, 55, 51, 32, 52, 48, 45, 55, 52,
			32, 51, 57, 45, 48, 46, 51, 45, 48, 46,
			51, 32, 49, 53, 45, 53, 53, 32, 49, 54,
			45, 53, 54, 32, 48, 46, 53, 45, 48, 46,
			56, 32, 55, 50, 45, 51, 57, 32, 55, 51,
			45, 51, 57, 32, 48, 46, 51, 45, 48, 46,
			48, 55, 32, 48, 46, 52, 32, 48, 46, 51,
			32, 48, 46, 50, 32, 48, 46, 55, 122, 109,
			45, 52, 50, 32, 49, 52, 48, 99, 45, 49,
			49, 32, 51, 53, 45, 49, 50, 32, 51, 55,
			45, 49, 54, 32, 52, 53, 45, 56, 32, 49,
			52, 45, 49, 57, 32, 50, 52, 45, 51, 55,
			32, 51, 51, 45, 52, 32, 50, 45, 57, 32,
			52, 45, 49, 50, 32, 52, 45, 53, 32, 49,
			45, 50, 50, 32, 51, 45, 50, 51, 32, 50,
			45, 48, 46, 51, 45, 48, 46, 51, 32, 50,
			51, 45, 55, 55, 32, 50, 52, 45, 55, 56,
			32, 48, 46, 54, 45, 48, 46, 55, 32, 55,
			51, 45, 51, 57, 32, 55, 51, 45, 51, 56,
			32, 48, 46, 49, 32, 48, 46, 49, 45, 52,
			32, 49, 53, 45, 49, 48, 32, 51, 50, 122,
			34, 47, 62, 10, 32, 32, 60, 112, 97, 116,
			104, 32, 100, 61, 34, 109, 53, 49, 49, 32,
			57, 48, 99, 45, 51, 54, 32, 50, 45, 55,
			56, 32, 49, 48, 45, 49, 48, 56, 32, 50,
			48, 108, 45, 53, 32, 50, 118, 54, 53, 104,
			50, 99, 48, 46, 57, 32, 48, 32, 53, 45,
			50, 32, 57, 45, 52, 32, 49, 51, 45, 55,
			32, 51, 55, 45, 49, 54, 32, 53, 52, 45,
			50, 49, 32, 50, 57, 45, 56, 32, 54, 54,
			45, 49, 48, 32, 57, 49, 45, 54, 32, 50,
			57, 32, 54, 32, 53, 49, 32, 50, 48, 32,
			53, 57, 32, 52, 48, 32, 52, 32, 49, 48,
			32, 53, 32, 49, 54, 32, 53, 32, 50, 57,
			32, 48, 32, 49, 56, 45, 50, 32, 50, 57,
			45, 57, 32, 52, 49, 45, 49, 48, 32, 49,
			57, 45, 51, 50, 32, 52, 48, 45, 54, 48,
			32, 53, 55, 45, 56, 32, 53, 45, 51, 53,
			32, 50, 48, 45, 53, 53, 32, 51, 48, 108,
			45, 57, 32, 53, 45, 48, 46, 48, 55, 32,
			52, 57, 118, 52, 57, 104, 54, 50, 118, 45,
			51, 54, 108, 48, 46, 48, 55, 45, 51, 54,
			32, 49, 51, 45, 55, 99, 49, 53, 45, 56,
			32, 51, 57, 45, 50, 51, 32, 52, 57, 45,
			51, 48, 32, 57, 45, 54, 32, 50, 49, 45,
			49, 54, 32, 51, 48, 45, 50, 51, 32, 50,
			49, 45, 49, 57, 32, 51, 55, 45, 52, 52,
			32, 52, 52, 45, 54, 55, 32, 56, 45, 50,
			57, 32, 54, 45, 54, 49, 45, 53, 45, 56,
			53, 45, 49, 54, 45, 51, 52, 45, 53, 50,
			45, 53, 57, 45, 57, 55, 45, 54, 56, 45,
			49, 53, 45, 51, 45, 50, 56, 45, 52, 45,
			52, 56, 45, 53, 45, 49, 48, 45, 48, 46,
			49, 45, 50, 48, 45, 48, 46, 49, 45, 50,
			50, 32, 48, 122, 34, 47, 62, 10, 32, 32,
			60, 112, 97, 116, 104, 32, 100, 61, 34, 109,
			52, 56, 50, 32, 53, 52, 50, 118, 51, 52,
			104, 55, 48, 118, 45, 54, 55, 104, 45, 55,
			48, 122, 34, 47, 62, 10, 32, 32, 60, 112,
			97, 116, 104, 32, 100, 61, 34, 109, 56, 50,
			56, 32, 57, 53, 99, 45, 51, 54, 32, 50,
			45, 55, 56, 32, 49, 48, 45, 49, 48, 56,
			32, 50, 48, 108, 45, 53, 32, 50, 118, 54,
			53, 104, 50, 99, 48, 46, 57, 32, 48, 32,
			53, 45, 50, 32, 57, 45, 52, 32, 49, 51,
			45, 55, 32, 51, 55, 45, 49, 54, 32, 53,
			52, 45, 50, 49, 32, 50, 57, 45, 56, 32,
			54, 54, 45, 49, 48, 32, 57, 49, 45, 54,
			32, 50, 57, 32, 54, 32, 53, 49, 32, 50,
			48, 32, 53, 57, 32, 52, 48, 32, 52, 32,
			49, 48, 32, 53, 32, 49, 54, 32, 53, 32,
			50, 57, 32, 48, 32, 49, 56, 45, 50, 32,
			50, 57, 45, 57, 32, 52, 49, 45, 49, 48,
			32, 49, 57, 45, 51, 50, 32, 52, 48, 45,
			54, 48, 32, 53, 55, 45, 56, 32, 53, 45,
			51, 53, 32, 50, 48, 45, 53, 53, 32, 51,
			48, 108, 45, 57, 32, 53, 45, 48, 46, 48,
			55, 32, 52, 57, 118, 52, 57, 104, 54, 50,
			118, 45, 51, 54, 108, 48, 46, 48, 55, 45,
			51, 54, 32, 49, 51, 45, 55, 99, 49, 53,
			45, 56, 32, 51, 57, 45, 50, 51, 32, 52,
			57, 45, 51, 48, 32, 57, 45, 54, 32, 50,
			49, 45, 49, 54, 32, 51, 48, 45, 50, 51,
			32, 50, 49, 45, 49, 57, 32, 51, 55, 45,
			52, 52, 32, 52, 52, 45, 54, 55, 32, 56,
			45, 50, 57, 32, 54, 45, 54, 49, 45, 53,
			45, 56, 53, 45, 49, 54, 45, 51, 52, 45,
			53, 50, 45, 53, 57, 45, 57, 55, 45, 54,
			56, 45, 49, 53, 45, 51, 45, 50, 56, 45,
			52, 45, 52, 56, 45, 53, 45, 49, 48, 45,
			48, 46, 49, 45, 50, 48, 45, 48, 46, 49,
			45, 50, 50, 32, 48, 122, 34, 47, 62, 10,
			32, 32, 60, 112, 97, 116, 104, 32, 100, 61,
			34, 109, 56, 48, 48, 32, 53, 52, 55, 118,
			51, 52, 104, 55, 48, 118, 45, 54, 55, 104,
			45, 55, 48, 122, 34, 47, 62, 10, 32, 60,
			47, 103, 62, 10, 60, 47, 115, 118, 103, 62,
			10
		};
		AddUser();
		Ascon.Pilot.SDK.IDataObject selectedObject = context.SelectedObjects.FirstOrDefault();
		if (selectedObject == null)
		{
			return;
		}
		getStadiaByIdAsync(selectedObject.Id);
		getProjectById(selectedObject.Id);
		builder.RemoveItem("miShowSharingSettings");
		List<Ascon.Pilot.SDK.IDataObject> objects = context.SelectedObjects.ToList();
		List<string> itemNames = builder.ItemNames.ToList();
		int insertIndex = itemNames.IndexOf("miShowSharingSettings") + 1;
		builder.AddSeparator(insertIndex);
		if (Repository.GetStoragePath(new Guid("6e926d2b-4081-49ec-b788-79315896dbf4")) == null)
		{
			Repository.Mount(new Guid("6e926d2b-4081-49ec-b788-79315896dbf4"));
		}
		if (selectedObject.Type.Id == 74 || selectedObject.Type.Id == 96)
		{
			selectedObjectTask = selectedObject;
		}
		string currentHostName = Dns.GetHostName();
		int currentPersonPosition = Repository.GetCurrentPerson().MainPosition.Position;
		int[] authorizedPositions = new int[12]
		{
			17, 18, 19, 20, 79, 80, 115, 116, 82, 93,
			81, 3
		};
		switch (currentHostName)
		{
		default:
			if (!authorizedPositions.Contains(currentPersonPosition))
			{
				break;
			}
			goto case "suslovkv";
		case "suslovkv":
		case "kirin":
		case "cherkashin":
		{
			bool isNotProject = true;
			foreach (Ascon.Pilot.SDK.IDataObject doc in objects)
			{
				if (doc.Type.Id == 28 || doc.Type.Id == 29)
				{
					isNotProject = false;
					break;
				}
			}
			if (objects.Any() && isNotProject)
			{
				int num = insertIndex + 1;
				insertIndex = num;
				builder.AddItem("CONTEXT_MENU_copy_folder2", num).WithHeader("Копировать структуру").WithIcon(copyIcon);
			}
			if (objects.Count == 1 && dataSelectedGuids.Any())
			{
				int num2 = insertIndex + 1;
				insertIndex = num2;
				builder.AddItem("CONTEXT_MENU_paste_folder2", num2).WithHeader("Вставить структуру").WithIcon(pasteIcon);
			}
			break;
		}
		}
		if (objects.Count == 1)
		{
			bool hasSourceFiles = false;
			foreach (IRelation relation in selectedObject.Relations)
			{
				if (relation.Type == ObjectRelationType.SourceFiles)
				{
					hasSourceFiles = true;
					break;
				}
			}
			if (selectedObject.Relations.Any())
			{
				int num3 = insertIndex + 1;
				insertIndex = num3;
				builder.AddItem("CONTEXT_MENU_SHOW_FILE", num3).WithHeader("Показать файлы в текущей папке").WithIsEnabled(hasSourceFiles)
					.WithIcon(copyStorageIcon);
			}
		}
		if (selectedObject.Type.Id == 96 && authorizedPositions.Contains(Repository.GetCurrentPerson().Id))
		{
			int num4 = insertIndex + 1;
			insertIndex = num4;
			builder.AddItem("CONTEXT_MENU_GIP_TASK", num4).WithHeader("Отправить задание ГИПа");
		}
		foreach (Ascon.Pilot.SDK.IDataObject doc2 in objects)
		{
			if (doc2.Type.Id == 74)
			{
				int num5 = insertIndex + 1;
				insertIndex = num5;
				builder.AddItem("CONTEXT_MENU_TASK2", num5).WithHeader("Отправить задание");
				break;
			}
		}
		if (selectedObject.Type.Id == 74 && objects.Count == 1)
		{
			int num6 = insertIndex + 1;
			insertIndex = num6;
			builder.AddItem("CONTEXT_MENU_TASK_COPY", num6).WithHeader("Новое ТЗ на основе этого");
		}
		bool allDocsAreDocuments = true;
		bool allDocsAreXps = true;
		foreach (Ascon.Pilot.SDK.IDataObject doc3 in objects)
		{
			if (doc3.Type.Name != "document")
			{
				allDocsAreDocuments = false;
			}
			IFile currentFile = doc3.ActualFileSnapshot.Files.FirstOrDefault((IFile f) => FileExtensionHelper.IsXpsAlike(f.Name));
			if (currentFile == null)
			{
				allDocsAreXps = false;
			}
		}
		if (allDocsAreDocuments)
		{
			int num7 = insertIndex + 1;
			insertIndex = num7;
			builder.AddItem("CONTEXT_MENU_DOC_SIGNATURE", num7).WithHeader(allDocsAreXps ? "Отправить ПСД на согласование" : "Отправить ПСД на согласование(НЕ XPS)").WithIsEnabled(allDocsAreXps)
				.WithIcon(psdIcon);
		}
	}

	public async Task<string> GetObjGuidByType(string nameParent, Guid whereChild)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		Ascon.Pilot.SDK.IDataObject objParent = await loader.Load(whereChild, 0L);
		do
		{
			objParent = await loader.Load(objParent.ParentId, 0L);
		}
		while (objParent.Type.Name != nameParent);
		return objParent.Id.ToString();
	}

	public void CreateFolderStorage(Guid whenDir0, Guid whatDir0)
	{
	}

	public async void pasteStructure(Guid whatDir0, Guid whenDir0)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		Ascon.Pilot.SDK.IDataObject whatDir1 = await loader.Load(whatDir0, 0L);
		if (whatDir1.Type.Children.Count() == 0 || whatDir1.Type.Id == 55)
		{
			return;
		}
		Ascon.Pilot.SDK.IDataObject whenDir1 = await loader.Load(whenDir0, 0L);
		IObjectBuilder newobj = _modifier.Create(whenDir1.Id, whatDir1.Type);
		foreach (KeyValuePair<string, object> whatAtr in whatDir1.Attributes)
		{
			if (whatAtr.Key == "responsible" || whatAtr.Key == "responsible_for_the_contract" || whatAtr.Key == "Person_in_charge")
			{
				_modifier.EditById(newobj.DataObject.Id).SetAttribute(whatAtr.Key, (int[])whatAtr.Value);
			}
			else if (whatAtr.Key == "date")
			{
				_modifier.EditById(newobj.DataObject.Id).SetAttribute(whatAtr.Key, (DateTime)whatAtr.Value);
			}
			else if (whatAtr.Key == "state")
			{
				_modifier.EditById(newobj.DataObject.Id).SetAttribute(whatAtr.Key, (Guid)whatAtr.Value);
			}
			else
			{
				_modifier.EditById(newobj.DataObject.Id).SetAttribute(whatAtr.Key, (string)whatAtr.Value);
			}
		}
		_modifier.Apply();
		foreach (Guid childFodler in whatDir1.Children)
		{
			pasteStructure(childFodler, newobj.DataObject.Id);
		}
	}

	public async void createPrj(Guid whenDir0, Guid whatDir0, bool copyfolder)
	{
		ObjectLoader loader = new ObjectLoader(Repository);
		Guid whenDir1 = whenDir0;
		if (copyfolder)
		{
			Ascon.Pilot.SDK.IDataObject whatFistFolder = await loader.Load(whatDir0, 0L);
			IObjectBuilder newobj = _modifier.Create(whenDir1, whatFistFolder.Type);
			foreach (KeyValuePair<string, object> whatAtr in whatFistFolder.Attributes)
			{
				if (whatAtr.Key == "responsible")
				{
					_modifier.EditById(newobj.DataObject.Id).SetAttribute(whatAtr.Key, (int[])whatAtr.Value);
				}
				else if (whatAtr.Key == "date")
				{
					_modifier.EditById(newobj.DataObject.Id).SetAttribute(whatAtr.Key, (DateTime)whatAtr.Value);
				}
				else if (whatAtr.Key == "state")
				{
					_modifier.EditById(newobj.DataObject.Id).SetAttribute(whatAtr.Key, (Guid)whatAtr.Value);
				}
				else
				{
					_modifier.EditById(newobj.DataObject.Id).SetAttribute(whatAtr.Key, (string)whatAtr.Value);
				}
			}
			if (newobj.DataObject.Type.Name == "task")
			{
				Ascon.Pilot.SDK.IDataObject whenDirObject = await loader.Load(whenDir1, 0L);
				await loader.Load(whatDir0, 0L);
				createPrj(whenDir1, new Guid("30097640-deb7-4447-a087-9dcb3f297a93"), copyfolder: false);
				int countTasks = 0;
				foreach (KeyValuePair<Guid, int> typesByChild3 in whenDirObject.TypesByChildren)
				{
					if (typesByChild3.Value.ToString() == "74")
					{
						countTasks++;
					}
				}
				string newNumber = newobj.DataObject.Attributes["number"].ToString().Split('/')[0] + "/" + countTasks;
				_modifier.EditById(newobj.DataObject.Id).SetAttribute("number", newNumber);
			}
			_modifier.Apply();
			whenDir1 = newobj.DataObject.Id;
			createPrj(whenDir1, whatDir0, copyfolder: false);
			return;
		}
		Ascon.Pilot.SDK.IDataObject objWhat = await loader.Load(whatDir0, 0L);
		if (objWhat.Children.Count == 0)
		{
			return;
		}
		foreach (Guid childGuid in objWhat.Children)
		{
			Ascon.Pilot.SDK.IDataObject childData = await loader.Load(childGuid, 0L);
			if ((childData.Type.Id == 55) | (childData.Type.Children.Count <= 0))
			{
				continue;
			}
			IObjectBuilder newobj2 = _modifier.Create(whenDir1, childData.Type);
			foreach (KeyValuePair<string, object> whatAtr2 in childData.Attributes)
			{
				if (whatAtr2.Key == "responsible")
				{
					_modifier.EditById(newobj2.DataObject.Id).SetAttribute(whatAtr2.Key, (int[])whatAtr2.Value);
				}
				else
				{
					_modifier.EditById(newobj2.DataObject.Id).SetAttribute(whatAtr2.Key, (string)whatAtr2.Value);
				}
			}
			_modifier.Apply();
			createPrj(newobj2.DataObject.Id, childGuid, copyfolder: false);
		}
	}

	public async void OnMenuItemClick(string name, ObjectsViewContext context)
	{
		_ = Thread.CurrentThread.ManagedThreadId;
		ObjectLoader loader = new ObjectLoader(Repository);
		Ascon.Pilot.SDK.IDataObject Project = await loader.Load(context.SelectedObjects.First().Id, 0L);
		Thread.Sleep(10);
		if (name == "CONTEXT_MENU_DOC_SIGNATURE")
		{
			if (Repository.GetCurrentPerson().ActualName == "admin")
			{
				MessageBox.Show("admin!");
			}
			countSelectedDoc = context.SelectedObjects.Count();
			Form form1 = new Form();
			form1.Text = "Принимающие отделы";
			form1.Icon = new Icon(SystemIcons.Question, 40, 40);
			form1.Size = new Size(620, 470);
			form1.MaximizeBox = false;
			form1.FormBorderStyle = FormBorderStyle.FixedDialog;
			form1.TopMost = true;
			form1.ControlBox = false;
			form1.FormBorderStyle = FormBorderStyle.SizableToolWindow;
			Button buttonOk = new Button
			{
				Text = "Отправить",
				Size = new Size(300, 80),
				Location = new Point(0, 330)
			};
			Button buttonCancel = new Button
			{
				Text = "Отмена",
				Size = new Size(300, 80),
				Location = new Point(300, 330)
			};
			ComboBox typeAgrement = new ComboBox();
			foreach (Guid gChild in (await loader.Load(new Guid("3024e077-361b-4aba-8cec-646f69870d05"), 0L)).Children)
			{
				Ascon.Pilot.SDK.IDataObject child = await loader.Load(gChild, 0L);
				typeAgrement.Items.Add((string)child.Attributes["title"]);
			}
			TextBox description = new TextBox();
			description.Text = "Описание";
			description.Multiline = true;
			description.Size = new Size(500, 300);
			description.ScrollBars = ScrollBars.Vertical;
			description.WordWrap = true;
			description.Location = new Point(100, 0);
			typeAgrement.Location = new Point(0, 300);
			typeAgrement.Size = new Size(600, 21);
			typeAgrement.Name = "Тип согласования";
			typeAgrement.DropDownStyle = ComboBoxStyle.DropDownList;
			CheckBox[] arCheckbox = new CheckBox[15];
			for (int i = 0; i < 15; i++)
			{
				arCheckbox[i] = new CheckBox();
				arCheckbox[i].Text = depName[i];
				arCheckbox[i].Location = new Point(0, i * 20);
				form1.Controls.Add(arCheckbox[i]);
			}
			ToolTip toolTip1 = new ToolTip();
			toolTip1.AutoPopDelay = 5000;
			toolTip1.InitialDelay = 100;
			toolTip1.ReshowDelay = 100;
			toolTip1.ShowAlways = true;
			toolTip1.SetToolTip(arCheckbox[4], "Мимо начальноков отдела. Принимающий отдел должен быть указан");
			form1.Controls.Add(buttonOk);
			form1.Controls.Add(buttonCancel);
			form1.Controls.Add(typeAgrement);
			form1.Controls.Add(description);
			description.Click += delegate
			{
				if (description.Text == "Описание")
				{
					description.Text = "";
				}
			};
			if (Dns.GetHostName() == "suslovkv")
			{
				description.Text = "text";
				arCheckbox[0].Checked = true;
				arCheckbox[1].Checked = true;
				typeAgrement.SelectedIndex = 0;
			}
			buttonCancel.Click += delegate
			{
				form1.Close();
			};
			buttonOk.Click += delegate
			{
				bool flag = false;
				CheckBox[] array11 = arCheckbox;
				CheckBox[] array12 = array11;
				CheckBox[] array13 = array12;
				foreach (CheckBox checkBox in array13)
				{
					if (!(checkBox.Text == "НК") && checkBox.Checked)
					{
						flag = true;
						break;
					}
				}
				if (flag & (description.Text != "") & (typeAgrement.Text != ""))
				{
					foreach (Ascon.Pilot.SDK.IDataObject current in context.SelectedObjects)
					{
						RemoveVirtualRequests(current);
						foreach (IRelation current2 in current.Relations)
						{
							_modifier.RemoveLink(current, current2);
						}
						_modifier.Apply();
					}
					IObjectBuilder objectBuilder = _modifier.Create(new Guid("00000000-0000-0000-0000-000000000000"), Repository.GetType(23));
					objectBuilder.SetAttribute("description", description.Text);
					objectBuilder.SetAttribute("title", typeAgrement.Text);
					string displayName = inProject.DisplayName;
					try
					{
						objectBuilder.SetAttribute("project_name", displayName);
					}
					catch (Exception)
					{
						MessageBox.Show("Не удалось заполнить имя проекта");
						throw;
					}
					List<int> list = new List<int>();
					CheckBox[] array14 = arCheckbox;
					CheckBox[] array15 = array14;
					CheckBox[] array16 = array15;
					foreach (CheckBox checkBox2 in array16)
					{
						if (checkBox2.Checked)
						{
							flag = true;
							list.Add(depBoss[Array.IndexOf(depName, checkBox2.Text)]);
						}
					}
					IObjectBuilder objectBuilder2 = _modifier.Create(objectBuilder.DataObject.Id, Repository.GetType(24));
					IObjectBuilder objectBuilder3 = _modifier.Create(objectBuilder.DataObject.Id, Repository.GetType(24));
					IObjectBuilder objectBuilder4 = _modifier.Create(objectBuilder.DataObject.Id, Repository.GetType(24));
					IObjectBuilder objectBuilder5 = _modifier.Create(objectBuilder.DataObject.Id, Repository.GetType(24));
					IObjectBuilder objectBuilder6 = _modifier.Create(objectBuilder.DataObject.Id, Repository.GetType(24));
					objectBuilder2.SetAttribute("order", 1);
					objectBuilder3.SetAttribute("order", 2);
					objectBuilder4.SetAttribute("order", 3);
					objectBuilder5.SetAttribute("order", 4);
					objectBuilder6.SetAttribute("order", 5);
					IObjectBuilder objectBuilder7 = _modifier.Create(objectBuilder2.DataObject.Id, Repository.GetType(26));
					IObjectBuilder objectBuilder8 = _modifier.Create(objectBuilder3.DataObject.Id, Repository.GetType(26));
					IObjectBuilder objectBuilder9 = _modifier.Create(objectBuilder4.DataObject.Id, Repository.GetType(26));
					IObjectBuilder objectBuilder10 = _modifier.Create(objectBuilder5.DataObject.Id, Repository.GetType(26));
					IObjectBuilder objectBuilder11 = _modifier.Create(objectBuilder6.DataObject.Id, Repository.GetType(26));
					objectBuilder7.SetAttribute("title", description.Text);
					objectBuilder8.SetAttribute("title", description.Text);
					objectBuilder9.SetAttribute("title", description.Text);
					objectBuilder10.SetAttribute("title", description.Text);
					objectBuilder11.SetAttribute("title", description.Text);
					if (arCheckbox[4].Checked)
					{
						objectBuilder7.SetAttribute("need_sub", 0);
					}
					else
					{
						objectBuilder7.SetAttribute("need_sub", countSelectedDoc);
					}
					objectBuilder8.SetAttribute("need_sub", countSelectedDoc * list.Count);
					objectBuilder9.SetAttribute("need_sub", countSelectedDoc);
					objectBuilder10.SetAttribute("need_sub", countSelectedDoc * 2);
					objectBuilder11.SetAttribute("need_sub", countSelectedDoc);
					objectBuilder7.SetAttribute("current_sub", 0);
					objectBuilder8.SetAttribute("current_sub", 0);
					objectBuilder9.SetAttribute("current_sub", 0);
					objectBuilder10.SetAttribute("current_sub", 0);
					objectBuilder11.SetAttribute("current_sub", 0);
					int[] array17 = new int[1] { Repository.GetCurrentPerson().Positions.First().Position };
					objectBuilder7.SetAttribute("initiator", array17);
					int[] array18 = new int[1] { 71 };
					IEnumerable<IPerson> people2 = Repository.GetPeople();
					IPerson currentPerson = Repository.GetCurrentPerson();
					IEnumerable<IOrganisationUnit> organisationUnits = Repository.GetOrganisationUnits();
					int index = currentPerson.AllOrgUnits()[1];
					IOrganisationUnit organisationUnit = organisationUnits.ElementAt(index);
					int[] array19 = new int[1];
					foreach (int current3 in organisationUnit.Children)
					{
						if (organisationUnits.ElementAt(current3).IsChief)
						{
							array19[0] = current3;
						}
					}
					objectBuilder7.SetAttribute("executor", array19);
					int[] array20 = list.ToArray();
					objectBuilder8.SetAttribute("executor", array20);
					int[] array21 = new int[1] { ((int[])inProject.Attributes["responsible_for_the_contract"]).First() };
					objectBuilder9.SetAttribute("executor", array21);
					objectBuilder9.SetAttribute("executor_3stage", array21);
					int[] array22 = new int[2] { 70, 116 };
					objectBuilder10.SetAttribute("executor", array22);
					objectBuilder11.SetAttribute("executor", array21);
					objectBuilder.SetAccessRights(array19.First(), AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
					int[] array23 = array20;
					int[] array24 = array23;
					int[] array25 = array24;
					foreach (int positionId in array25)
					{
						objectBuilder.SetAccessRights(positionId, AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
					}
					objectBuilder.SetAccessRights(array21.First(), AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
					objectBuilder.SetAccessRights(array22.First(), AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
					objectBuilder.SetAccessRights(70, AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
					objectBuilder.SetAccessRights(116, AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
					int[] array26 = new int[2] { 70, 116 };
					objectBuilder8.SetAttribute("initiator", array19);
					objectBuilder9.SetAttribute("initiator", array20);
					objectBuilder10.SetAttribute("initiator", array21);
					objectBuilder11.SetAttribute("initiator", array26);
					objectBuilder7.SetAttribute("project_name", displayName);
					objectBuilder8.SetAttribute("project_name", displayName);
					objectBuilder9.SetAttribute("project_name", displayName);
					objectBuilder10.SetAttribute("project_name", displayName);
					objectBuilder11.SetAttribute("project_name", displayName);
					objectBuilder7.SetAttribute("state", state_none);
					objectBuilder8.SetAttribute("state", state_none);
					objectBuilder9.SetAttribute("state", state_none);
					objectBuilder10.SetAttribute("state", state_none);
					objectBuilder11.SetAttribute("state", state_none);
					foreach (Ascon.Pilot.SDK.IDataObject current4 in context.SelectedObjects)
					{
						CreateTaskAttachmentLinkFromGuids(objectBuilder7.DataObject.Id, current4.Id);
						CreateTaskAttachmentLinkFromGuids(objectBuilder8.DataObject.Id, current4.Id);
						CreateTaskAttachmentLinkFromGuids(objectBuilder9.DataObject.Id, current4.Id);
						CreateTaskAttachmentLinkFromGuids(objectBuilder10.DataObject.Id, current4.Id);
						CreateTaskAttachmentLinkFromGuids(objectBuilder11.DataObject.Id, current4.Id);
					}
					if (arCheckbox[4].Checked)
					{
						objectBuilder10.SetAttribute("state", state_awaitingSignature);
						int[] array27 = array26;
						int[] array28 = array27;
						int[] array29 = array28;
						foreach (int num2 in array29)
						{
							SendChatNotificationAsync(user[num2][0], "@" + user[array17[0]][0] + " *повторно* отправил(а) вам документ(ы) на подпись [" + description.Text + "](piloturi://" + objectBuilder10.DataObject.Id.ToString() + ")", 3495);
						}
						SendChatNotificationAsync("kirin", "задание [" + description.Text + "](piloturi://" + objectBuilder.DataObject.Id.ToString() + ") *повторно* дошло до НК.", 3497);
					}
					else
					{
						SendChatNotificationAsync(user[array19[0]][0], "@" + user[array17[0]][0] + " отправил(а) вам документ(ы) на подпись [" + description.Text + "](piloturi://" + objectBuilder7.DataObject.Id.ToString() + ")", 3501);
						objectBuilder7.SetAttribute("state", state_awaitingSignature);
					}
					_modifier.Apply();
					form1.Close();
				}
				else
				{
					MessageBox.Show("Не всё поля заполнены");
				}
			};
			form1.Show();
		}
		if (name == "CONTEXT_MENU_TASK_COPY")
		{
			createPrj(context.SelectedObjects.First().ParentId, context.SelectedObjects.First().Id, copyfolder: true);
		}
		if (name == "CONTEXT_MENU_copy_folder2")
		{
			List<Guid> whatDirs = new List<Guid>();
			foreach (Ascon.Pilot.SDK.IDataObject selectItem in context.SelectedObjects)
			{
				whatDirs.Add(selectItem.Id);
			}
			dataSelectedGuids = whatDirs;
		}
		if (name == "CONTEXT_MENU_paste_folder2")
		{
			foreach (Guid what in dataSelectedGuids)
			{
				pasteStructure(what, context.SelectedObjects.First().Id);
			}
		}
		if (name == "CONTEXT_MENU_copy_folder")
		{
			List<Guid> whatDirs2 = new List<Guid>();
			foreach (Ascon.Pilot.SDK.IDataObject selectItem2 in context.SelectedObjects)
			{
				whatDirs2.Add(selectItem2.Id);
			}
			dataSelectedGuids = whatDirs2;
		}
		if (name == "CONTEXT_MENU_SHOW_FILE")
		{
			foreach (IRelation item in context.SelectedObjects.First().Relations)
			{
				if (item.Type == ObjectRelationType.SourceFiles)
				{
					Process.Start("explorer.exe", "piloturi://" + item.TargetId.ToString());
					break;
				}
			}
		}
		if (name == "CONTEXT_MENU_paste_folder")
		{
			if (dataSelectedGuids.Count() == 0)
			{
				MessageBox.Show("Нечего вставлять");
			}
			else
			{
				List<Guid>.Enumerator enumerator6 = dataSelectedGuids.GetEnumerator();
				try
				{
					while (enumerator6.MoveNext())
					{
						createPrj(whatDir0: enumerator6.Current, whenDir0: context.SelectedObjects.First().Id, copyfolder: true);
					}
				}
				finally
				{
					enumerator6.Dispose();
				}
			}
		}
		if (name == "PROJECT_CREATE")
		{
			Form form2 = new Form();
			form2.Text = "Выбор шаблона проекта";
			form2.Icon = new Icon(SystemIcons.Question, 40, 40);
			ListBox listBox1 = new ListBox();
			listBox1.Items.Add("qq1");
			listBox1.Items.Add("qq2");
			listBox1.Size = new Size(640, 320);
			form2.Size = new Size(640, 440);
			form2.MaximizeBox = false;
			form2.FormBorderStyle = FormBorderStyle.FixedDialog;
			form2.Controls.Add(listBox1);
			form2.TopMost = true;
			form2.ControlBox = false;
			Button button = new Button
			{
				Text = "Ok",
				Size = new Size(640, 80),
				Location = new Point(0, 320)
			};
			form2.Controls.Add(button);
			button.Click += delegate
			{
				if (listBox1.SelectedIndex != -1)
				{
					form2.Close();
				}
				else
				{
					MessageBox.Show("Причиина не заполнена");
				}
			};
			form2.ShowDialog();
		}
		while (Project.Type.Name != "project")
		{
			if (Project.Type.Name == "Root_object_type")
			{
			}
			Project = await loader.Load(Project.ParentId, 0L);
		}
		if (name == "CONTEXT_MENU_GIP_TASK")
		{
			Ascon.Pilot.SDK.IDataObject simpleWorkflowGIP = await loader.Load(new Guid("7c2c55af-6ac9-482a-b9b6-35c774498ff2"), 0L);
			Ascon.Pilot.SDK.IDataObject simpleStageGIP = await loader.Load(new Guid("96ae21af-08a7-4e57-8837-a6264fd03834"), 0L);
			Ascon.Pilot.SDK.IDataObject simpleTaskGIP = await loader.Load(new Guid("0472f7ca-fbf8-4a41-b99e-f92f2338bb46"), 0L);
			Ascon.Pilot.SDK.IDataObject selectedObject = context.SelectedObjects.First();
			IObjectBuilder newWorkflow = _modifier.Create(new Guid("00000000-0000-0000-0000-000000000000"), simpleWorkflowGIP.Type);
			IObjectBuilder newStage = _modifier.Create(newWorkflow.DataObject.Id, simpleStageGIP.Type);
			int[] newInitiator = new int[1] { Repository.GetCurrentPerson().Positions.First().Position };
			string new_description = context.SelectedObjects.First().Attributes["name_work"].ToString();
			newWorkflow.SetAttribute("initiator", newInitiator);
			newWorkflow.SetAttribute("description", new_description);
			string dep_take_task = selectedObject.Attributes["dep_take_task"].ToString();
			dep_take_task = dep_take_task.Replace(" ", "");
			string[] ar_dep_take_task = dep_take_task.Split(';');
			_ = new int[ar_dep_take_task.Length];
			_ = new Guid[ar_dep_take_task.Length];
			for (int i2 = 0; i2 < ar_dep_take_task.Length; i2++)
			{
				int depId = Array.IndexOf(depName, ar_dep_take_task[i2]);
				if (depId == -1)
				{
					MessageBox.Show("Не удалось найти начальника " + ar_dep_take_task[i2]);
					depId = 6;
				}
				int[] newexecutor = new int[1] { depBoss[depId] };
				IObjectBuilder newTask = _modifier.Create(newStage.DataObject.Id, simpleTaskGIP.Type);
				newTask.SetAttribute("initiator", newInitiator);
				newTask.SetAttribute("executor", newexecutor);
				newTask.SetAttribute("description", new_description);
				newTask.SetAccessRights(depBoss[depId], AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
				CreateTaskAttachmentLinkFromGuids(newTask.DataObject.Id, selectedObject.Id);
			}
			_modifier.Apply();
		}
		if (name == "CONTEXT_MENU_TASK2")
		{
			Ascon.Pilot.SDK.IDataObject docTask = await loader.Load(context.SelectedObjects.First().Id, 0L);
			IObjectBuilder newWorkflow2 = _modifier.Create(new Guid("00000000-0000-0000-0000-000000000000"), Repository.GetType(21));
			newWorkflow2.SetAttribute("project_name", inProject.Attributes["project_name"].ToString());
			newWorkflow2.SetAttribute("title", docTask.Attributes["Type_of_task"].ToString());
			newWorkflow2.SetAttribute("description", docTask.Attributes["content"].ToString());
			newWorkflow2.SetAttribute("Stage", docTask.Attributes["Stage"].ToString());
			newWorkflow2.SetAttribute("Prof", docTask.Attributes["dep_give_task"].ToString());
			newWorkflow2.SetAttribute("task_select", docTask.Attributes["dep_take_task"].ToString());
			IObjectBuilder newStage2 = _modifier.Create(newWorkflow2.DataObject.Id, Repository.GetType(22));
			IObjectBuilder newStage3 = _modifier.Create(newWorkflow2.DataObject.Id, Repository.GetType(22));
			IObjectBuilder newStage4 = _modifier.Create(newWorkflow2.DataObject.Id, Repository.GetType(22));
			IObjectBuilder newStage5 = _modifier.Create(newWorkflow2.DataObject.Id, Repository.GetType(22));
			newStage2.SetAttribute("order", 1);
			newStage3.SetAttribute("order", 2);
			newStage4.SetAttribute("order", 3);
			newStage5.SetAttribute("order", 4);
			string developed1 = docTask.Attributes["developed"].ToString();
			string checked2 = docTask.Attributes["checked"].ToString();
			string DepHead3 = docTask.Attributes["DepHead"].ToString();
			string gip4 = Project.Attributes["gipName"].ToString();
			string accepted5 = docTask.Attributes["accepted"].ToString();
			string[] userString = new string[200];
			for (int i3 = 0; i3 < user.Length; i3++)
			{
				if (user[i3] != null)
				{
					userString[i3] = user[i3][1];
				}
			}
			int developed11 = Array.IndexOf(userString, developed1);
			int checked22 = Array.IndexOf(userString, checked2);
			int DepHead33 = Array.IndexOf(userString, DepHead3);
			int gip44 = Array.IndexOf(userString, gip4);
			int accepted55 = Array.IndexOf(userString, accepted5);
			newWorkflow2.SetAccessRights(developed11, AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
			newWorkflow2.SetAccessRights(checked22, AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
			newWorkflow2.SetAccessRights(DepHead33, AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
			newWorkflow2.SetAccessRights(gip44, AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
			newWorkflow2.SetAccessRights(accepted55, AccessLevel.ViewEditAgrement, DateTime.MaxValue, isInheritable: true);
			IObjectBuilder newTask2 = _modifier.Create(newStage2.DataObject.Id, Repository.GetType(25));
			IObjectBuilder newTask3 = _modifier.Create(newStage3.DataObject.Id, Repository.GetType(25));
			IObjectBuilder newTask4 = _modifier.Create(newStage4.DataObject.Id, Repository.GetType(25));
			IObjectBuilder newTask5 = _modifier.Create(newStage5.DataObject.Id, Repository.GetType(25));
			await loader.Load(new Guid("28c43037-dd91-45ab-85fb-009eb67a5306"), 0L);
			int[] developed111 = new int[1] { developed11 };
			int[] checked222 = new int[1] { checked22 };
			int[] DepHead333 = new int[1] { DepHead33 };
			int[] gip444 = new int[1] { gip44 };
			int[] accepted555 = new int[1] { accepted55 };
			newTask2.SetAttribute("initiator", developed111);
			newTask2.SetAttribute("executor", checked222);
			newTask2.SetAttribute("state", state_assign);
			newTask2.SetAttribute("title", docTask.Attributes["Type_of_task"].ToString());
			newTask2.SetAttribute("description", docTask.Attributes["content"].ToString());
			CreateTaskAttachmentLinkFromGuids(newTask2.DataObject.Id, docTask.Id);
			newTask3.SetAttribute("initiator", checked222);
			newTask3.SetAttribute("executor", DepHead333);
			newTask3.SetAttribute("title", docTask.Attributes["Type_of_task"].ToString());
			newTask3.SetAttribute("description", docTask.Attributes["content"].ToString());
			CreateTaskAttachmentLinkFromGuids(newTask3.DataObject.Id, docTask.Id);
			newTask4.SetAttribute("initiator", DepHead333);
			newTask4.SetAttribute("executor", gip444);
			newTask4.SetAttribute("title", docTask.Attributes["Type_of_task"].ToString());
			newTask4.SetAttribute("description", docTask.Attributes["content"].ToString());
			CreateTaskAttachmentLinkFromGuids(newTask4.DataObject.Id, docTask.Id);
			newTask5.SetAttribute("initiator", gip444);
			newTask5.SetAttribute("executor", accepted555);
			newTask5.SetAttribute("title", docTask.Attributes["Type_of_task"].ToString());
			newTask5.SetAttribute("description", docTask.Attributes["content"].ToString());
			CreateTaskAttachmentLinkFromGuids(newTask5.DataObject.Id, docTask.Id);
			_modifier.Apply();
			MessageBox.Show("ТЗ создано");
		}
		if (!(name == "CONTEXT_MENU_TASK"))
		{
			return;
		}
		int currentperson = Repository.GetCurrentPerson().Positions.First().Position;
		Ascon.Pilot.SDK.IDataObject docTask2 = await loader.Load(context.SelectedObjects.First().Id, 0L);
		foreach (Ascon.Pilot.SDK.IDataObject d in context.SelectedObjects)
		{
			if (d.Type.Id == 74)
			{
				docTask2 = d;
				break;
			}
		}
		inProject = docTask2;
		do
		{
			inProject = await loader.Load(inProject.ParentId, 0L);
		}
		while (inProject.Type.Id != 28);
		IObjectBuilder newWorkflow3 = _modifier.Create(new Guid("00000000-0000-0000-0000-000000000000"), Repository.GetType(21));
		newWorkflow3.SetAttribute("project_name", inProject.Attributes["project_name"].ToString());
		newWorkflow3.SetAttribute("state", new Guid("11748395-9a9f-48cd-92ef-7a9d9f776ecd"));
		newWorkflow3.SetAttribute("title", docTask2.Attributes["Type_of_task"].ToString());
		newWorkflow3.SetAttribute("description", docTask2.Attributes["content"].ToString());
		newWorkflow3.SetAttribute("Stage", docTask2.Attributes["Stage"].ToString());
		newWorkflow3.SetAttribute("Prof", docTask2.Attributes["dep_give_task"].ToString());
		newWorkflow3.SetAttribute("task_select", docTask2.Attributes["dep_take_task"].ToString());
		int[] WorkflowInitiator = new int[1] { Repository.GetCurrentPerson().Positions.First().Position };
		newWorkflow3.SetAttribute("actualFor", WorkflowInitiator);
		newWorkflow3.SetAttribute("initiator", WorkflowInitiator);
		string checkedString = docTask2.Attributes["checked"].ToString();
		int[] checkedId = new int[1];
		for (int i4 = 0; i4 < user.Length; i4++)
		{
			if (user[i4] != null && user[i4][1] == checkedString)
			{
				checkedId[0] = i4;
			}
		}
		Guid GuidFolderRelDoc = Guid.NewGuid();
		foreach (IRelation item2 in docTask2.Relations)
		{
			if (item2.Type == ObjectRelationType.Custom)
			{
				GuidFolderRelDoc = item2.TargetId;
			}
		}
		for (int nStage = 1; nStage <= 4; nStage++)
		{
			IObjectBuilder newStage6 = _modifier.Create(newWorkflow3.DataObject.Id, Repository.GetType(22));
			IObjectBuilder newTask6 = _modifier.Create(newStage6.DataObject.Id, Repository.GetType(25));
			CreateTaskAttachmentLinkFromGuids(newTask6.DataObject.Id, docTask2.Id);
			newStage6.SetAttribute("order", nStage);
			newStage6.SetAttribute("state", new Guid("d8ae8c3a-6f46-45d2-835b-563fe2b47acd"));
			newTask6.SetAttribute("description", docTask2.Attributes["content"].ToString());
			newTask6.SetAttribute("dateOfAssignment", newTask6.DataObject.Created);
			DateTime dateTime2d = DateTime.Now.AddDays(2.0);
			newTask6.SetAttribute("deadlineDate", dateTime2d);
			newTask6.SetAttribute("title", docTask2.Attributes["Type_of_task"].ToString());
			int[] GIP = (int[])Project.Attributes["responsible_for_the_contract"];
			Ascon.Pilot.SDK.IDataObject folderDoc = await loader.Load(GuidFolderRelDoc, 0L);
			foreach (IRelation rel in folderDoc.Relations)
			{
				if (rel.Type == ObjectRelationType.SourceFiles)
				{
					break;
				}
			}
			int[] oldOtvets = (int[])folderDoc.Attributes["responsible"];
			int[] newOtvet = new int[oldOtvets.Length];
			oldOtvets.CopyTo(newOtvet, 0);
			newOtvet[newOtvet.Length - 1] = checkedId[0];
			try
			{
				if (currentperson != newOtvet[0])
				{
					_modifier.EditById(GuidFolderRelDoc).SetAttribute("responsible", newOtvet);
					_modifier.Apply();
				}
			}
			catch (Exception)
			{
				MessageBox.Show("нет прав на папку" + GuidFolderRelDoc.ToString());
				throw;
			}
			int[] boss_give_task = new int[1];
			int[] boss_take_task = new int[1];
			try
			{
				int[] array = depBoss;
				object[] array2 = depName;
				object[] array3 = array2;
				object[] array4 = array3;
				boss_give_task[0] = array[Array.IndexOf(array4, docTask2.Attributes["dep_give_task"])];
				newTask6.SetAccessRights(boss_give_task[0], AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
				newWorkflow3.SetAccessRights(boss_give_task[0], AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
			}
			catch (Exception)
			{
				MessageBox.Show("Не найден начальник выдающего отдела");
			}
			try
			{
				int[] array5 = depBoss;
				object[] array2 = depName;
				object[] array6 = array2;
				object[] array7 = array6;
				boss_take_task[0] = array5[Array.IndexOf(array7, docTask2.Attributes["dep_take_task"])];
				newTask6.SetAccessRights(boss_take_task[0], AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
				newWorkflow3.SetAccessRights(boss_take_task[0], AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
			}
			catch (Exception)
			{
				MessageBox.Show("Не найден начальник принимающего отдела");
			}
			newTask6.SetAccessRights(checkedId[0], AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
			newTask6.SetAccessRights(GIP[0], AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
			newWorkflow3.SetAccessRights(checkedId[0], AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
			newWorkflow3.SetAccessRights(GIP[0], AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
			newWorkflow3.SetAttribute("actualFor", checkedId);
			int[] init = new int[1] { Repository.GetCurrentPerson().Positions.First().Position };
			newWorkflow3.SetAttribute("actualFor", init);
			switch (nStage)
			{
			case 1:
			{
				newTask6.SetAttribute("state", taskAssign);
				newTask6.SetAttribute("state", taskAssign);
				newTask6.SetAttribute("initiator", init);
				newTask6.SetAttribute("executor", checkedId);
				newTask6.SetAttribute("actualFor", checkedId);
				_ = user[checkedId[0]][0];
				_ = user[init[0]][0];
				Ascon.Pilot.SDK.IDataObject folder = await loader.Load(selectedObjectTask.Relations.First().TargetId, 0L);
				if (!folder.Attributes.ContainsKey("responsible") || folder.Attributes["responsible"] == null)
				{
					MessageBox.Show("Не удалось добавить/удалить ответствнных за ведение документации");
				}
				else
				{
					for (int i5 = 0; i5 < folder.Access2.Count; i5++)
					{
						if (!((int[])folder.Attributes["responsible"]).Contains(folder.Access2.ElementAt(i5).OrgUnitId))
						{
							int OrgUnitIdDel = folder.Access2.ElementAt(i5).OrgUnitId;
							if (OrgUnitIdDel == currentperson)
							{
							}
						}
					}
				}
				try
				{
					int[] array8 = (int[])folder.Attributes["responsible"];
					int[] array9 = array8;
					int[] array10 = array9;
					foreach (int userId in array10)
					{
						_modifier.EditById(folder.Id).SetAccessRights(userId, AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
						_modifier.EditById(folder.Relations.First().TargetId).SetAccessRights(userId, AccessLevel.Full, DateTime.MaxValue, isInheritable: true);
						_modifier.Apply();
					}
				}
				catch
				{
					MessageBox.Show("Не удалось назначить ответственных");
				}
				break;
			}
			case 2:
				newTask6.SetAttribute("initiator", checkedId);
				newTask6.SetAttribute("executor", boss_give_task);
				break;
			case 3:
				newTask6.SetAttribute("initiator", boss_give_task);
				newTask6.SetAttribute("executor", GIP);
				break;
			case 4:
			{
				newTask6.SetAttribute("initiator", GIP);
				newTask6.SetAttribute("executor", boss_take_task);
				_ = "@" + newWorkflow3.DataObject.Creator.ActualName + " создал(а) процесс технического задания: [" + newWorkflow3.DataObject.DisplayName + "](piloturi://" + newWorkflow3.DataObject.Id.ToString() + "). Состояние задания может измениться.";
				_ = "http://192.168.10.5:5545/url?id=" + newWorkflow3.DataObject.Id.ToString() + " " + newWorkflow3.DataObject.Creator.DisplayName + " создал(а) процесс технического задания: " + newWorkflow3.DataObject.DisplayName + ". Состояние задания может измениться.";
				string textEmail = newWorkflow3.DataObject.Creator.DisplayName + " создал(а) процесс технического задания:  <a href='piloturi://" + newWorkflow3.DataObject.Id.ToString() + "'>" + newWorkflow3.DataObject.DisplayName + "</a>. Состояние задания может измениться.";
				IEnumerable<IOrganisationUnit> orgStruc = Repository.GetOrganisationUnits();
				orgStruc.ElementAt(108).Person();
				IEnumerable<IPerson> people = Repository.GetPeople();
				Repository.GetCurrentPerson();
				foreach (IPerson p in people)
				{
					if (p.AllOrgUnits().Count != 0 && p.AllOrgUnits().First() == boss_take_task[0])
					{
						SendEmailNotification(p.Email(), textEmail);
					}
				}
				break;
			}
			}
		}
		MessageBox.Show("ТЗ создано");
	}

	public void AssignHotKeys(IHotKeyCollection hotKeyCollection)
	{
	}

	public void OnHotKeyPressed(string commandId, ObjectsViewContext context)
	{
	}

	public bool Exists(Guid fileId)
	{
		throw new NotImplementedException();
	}

	public bool IsFull(Guid fileId)
	{
		throw new NotImplementedException();
	}

	public Stream OpenRead(IFile file)
	{
		throw new NotImplementedException();
	}

	public ISignatureBuilder Add(Guid id)
	{
		throw new NotImplementedException();
	}

	public ISignatureBuilder WithDatabaseId(Guid databaseId)
	{
		throw new NotImplementedException();
	}

	public ISignatureBuilder WithPositionId(int positionId)
	{
		throw new NotImplementedException();
	}

	public ISignatureBuilder WithRole(string role)
	{
		throw new NotImplementedException();
	}

	public ISignatureBuilder WithSign(string sign)
	{
		throw new NotImplementedException();
	}

	public ISignatureBuilder WithRequestSigner(string requestSigner)
	{
		throw new NotImplementedException();
	}

	public ISignatureBuilder WithIsAdditional(bool value)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetAttribute(string name, string value)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetAttribute(string name, int value)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetAttribute(string name, double value)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetAttribute(string name, DateTime value)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetAttribute(string name, decimal value)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetAttribute(string name, long value)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetAttribute(string name, Guid value)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetAttribute(string name, int[] value)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder RemoveAttribute(string name)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder AddOrReplaceFile(string name, Stream stream, IFile file, DateTime creationTime, DateTime lastAccessTime, DateTime lastWriteTime)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder AddFile(string path)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder AddFile(string name, Stream stream, DateTime creationTime, DateTime lastAccessTime, DateTime lastWriteTime)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetAccessRights(int positionId, AccessLevel level, DateTime validThrough, bool isInheritable)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder RemoveAccessRights(int positionId)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder MakeSecret()
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder MakePublic()
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder AddSourceFileRelation(Guid relatedObjectId)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder RemoveSourceFileRelation(Guid relatedObjectId)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder AddTaskInitiatorAttachmentRelation(Guid relatedObjectId)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder RemoveTaskInitiatorAttachmentRelation(Guid relatedObjectId)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder AddTaskMessageAttachmentRelation(Guid relatedObjectId)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder RemoveTaskMessageAttachmentRelation(Guid relatedObjectId)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder CreateFileSnapshot(string reason)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder MakeSnapshotActual(string reason, IFilesSnapshot snapshot)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder AddSubscriber(int personId)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder RemoveSubscriber(int personId)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetIsDeleted(bool isDeleted)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetIsInRecycleBin(bool isInRecycleBin)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetParent(Guid parentId)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetType(IType type)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SetCreator(int creatorId)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder Lock()
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder Unlock()
	{
		throw new NotImplementedException();
	}

	public ISignatureModifier SetSignatures(Predicate<IFile> selectFilesPredicate)
	{
		throw new NotImplementedException();
	}

	public IObjectBuilder SaveHistoryItem()
	{
		throw new NotImplementedException();
	}

	public long GetFileSizeOnDisk(Guid fileId)
	{
		throw new NotImplementedException();
	}

	public void DeleteLocalFile(Guid fileId)
	{
		throw new NotImplementedException();
	}

	public ISignatureModifier Remove(Predicate<ISignature> findSignature)
	{
		throw new NotImplementedException();
	}
}
