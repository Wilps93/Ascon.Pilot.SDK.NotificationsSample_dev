# Инструкции по развертыванию TOMS.moduls

## 🚀 Быстрый старт

### 1. Подготовка среды

```bash
# Проверка требований
- .NET Framework 4.8.1
- Visual Studio 2019+ или MSBuild
- Доступ к Ascon.Pilot.SDK
- Newtonsoft.Json 13.0.0+
```

### 2. Сборка проекта

```bash
# Запуск скрипта сборки
build.bat

# Или ручная сборка
msbuild Ascon.Pilot.SDK.NotificationsSample.ext2.sln /p:Configuration=Release
```

### 3. Настройка конфигурации

Отредактируйте `App.config`:

```xml
<appSettings>
  <!-- Rocket.Chat -->
  <add key="RocketChat:BaseUrl" value="http://your-server:3000" />
  <add key="RocketChat:AuthToken" value="your-token" />
  <add key="RocketChat:UserId" value="your-user-id" />
  
  <!-- SMTP -->
  <add key="Smtp:Host" value="mail.your-domain.com" />
  <add key="Smtp:Port" value="587" />
  <add key="Smtp:Username" value="your-email@domain.com" />
  <add key="Smtp:Password" value="your-password" />
</appSettings>
```

### 4. Развертывание

```bash
# Копирование файлов
copy bin\Release\Ascon.Pilot.SDK.NotificationsSample.ext2.dll "C:\Program Files\Ascon\Pilot\Extensions\"
copy App.config "C:\Program Files\Ascon\Pilot\Extensions\"

# Создание папки для логов
mkdir "C:\Program Files\Ascon\Pilot\Extensions\logs"
```

## 🔧 Детальная настройка

### Настройка Rocket.Chat

1. **Получение токена доступа:**
   ```bash
   curl -H "Content-Type: application/json" \
        -d '{"user":"your-username","password":"your-password"}' \
        http://your-rocket-chat-server/api/v1/login
   ```

2. **Получение User ID:**
   ```bash
   curl -H "X-Auth-Token: your-token" \
        -H "X-User-Id: your-user-id" \
        http://your-rocket-chat-server/api/v1/me
   ```

### Настройка SMTP

1. **Проверка SMTP-сервера:**
   ```bash
   telnet mail.your-domain.com 587
   ```

2. **Тест отправки:**
   ```csharp
   var emailService = new EmailService();
   await emailService.TestConnectionAsync();
   ```

### Настройка безопасности

1. **Шифрование чувствительных данных:**
   ```csharp
   var encrypted = AppSettings.EncryptString("your-password");
   // Добавить в App.config
   ```

2. **Настройка прав доступа:**
   ```bash
   # Права на папку логов
   icacls "C:\Program Files\Ascon\Pilot\Extensions\logs" /grant "NETWORK SERVICE":(OI)(CI)F
   ```

## 📊 Мониторинг

### Проверка работоспособности

```csharp
// Тест конфигурации
var configResult = InputValidator.ValidateConfiguration();
Console.WriteLine(InputValidator.GetValidationSummary(configResult));

// Тест подключений
using (var chatService = new ChatService())
{
    var isConnected = await chatService.TestConnectionAsync();
    Console.WriteLine($"Rocket.Chat: {(isConnected ? "OK" : "FAIL")}");
}

using (var emailService = new EmailService())
{
    var isConnected = await emailService.TestConnectionAsync();
    Console.WriteLine($"SMTP: {(isConnected ? "OK" : "FAIL")}");
}
```

### Просмотр логов

```bash
# Просмотр текущего лога
type "C:\Program Files\Ascon\Pilot\Extensions\logs\pilot-module.log"

# Поиск ошибок
findstr "ERROR" "C:\Program Files\Ascon\Pilot\Extensions\logs\pilot-module.log"

# Мониторинг в реальном времени
powershell "Get-Content 'C:\Program Files\Ascon\Pilot\Extensions\logs\pilot-module.log' -Wait"
```

## 🔄 Обновление

### Процедура обновления

1. **Резервное копирование:**
   ```bash
   copy "C:\Program Files\Ascon\Pilot\Extensions\Ascon.Pilot.SDK.NotificationsSample.ext2.dll" backup\
   copy "C:\Program Files\Ascon\Pilot\Extensions\App.config" backup\
   ```

2. **Остановка служб:**
   ```bash
   net stop "Ascon Pilot Service"
   ```

3. **Замена файлов:**
   ```bash
   copy bin\Release\Ascon.Pilot.SDK.NotificationsSample.ext2.dll "C:\Program Files\Ascon\Pilot\Extensions\"
   ```

4. **Запуск служб:**
   ```bash
   net start "Ascon Pilot Service"
   ```

5. **Проверка работоспособности:**
   ```bash
   # Проверка логов
   findstr "INFO.*initialized" "C:\Program Files\Ascon\Pilot\Extensions\logs\pilot-module.log"
   ```

## 🛠️ Устранение неполадок

### Частые проблемы

#### 1. Ошибка подключения к Rocket.Chat
```
ERROR: Rocket.Chat API error: Connection refused
```

**Решение:**
- Проверить доступность сервера: `ping your-rocket-chat-server`
- Проверить токен и User ID
- Проверить настройки брандмауэра

#### 2. Ошибка SMTP
```
ERROR: Failed to send email: Authentication failed
```

**Решение:**
- Проверить логин/пароль SMTP
- Проверить настройки SSL/TLS
- Проверить порт (587 для STARTTLS, 465 для SSL)

#### 3. Ошибка доступа к файлам
```
ERROR: Access to the path 'logs' is denied
```

**Решение:**
```bash
# Назначить права
icacls "C:\Program Files\Ascon\Pilot\Extensions\logs" /grant "NETWORK SERVICE":(OI)(CI)F
```

#### 4. Ошибка валидации
```
ERROR: Configuration validation failed
```

**Решение:**
- Проверить формат App.config
- Проверить обязательные параметры
- Запустить тесты: `ConfigurationTests.RunAllTestsAsync()`

### Диагностика

#### Проверка конфигурации
```csharp
var result = InputValidator.ValidateConfiguration();
foreach (var error in result.Errors)
{
    Console.WriteLine($"Config Error: {error}");
}
```

#### Проверка подключений
```csharp
// Тест HTTP-подключений
using (var client = new HttpClient())
{
    var response = await client.GetAsync("http://your-rocket-chat-server/api/v1/info");
    Console.WriteLine($"Rocket.Chat Status: {response.StatusCode}");
}
```

#### Проверка логов
```bash
# Последние 50 строк лога
powershell "Get-Content 'logs\pilot-module.log' | Select-Object -Last 50"

# Поиск ошибок за последний час
powershell "Get-Content 'logs\pilot-module.log' | Where-Object { $_ -match 'ERROR' -and $_ -match (Get-Date).ToString('yyyy-MM-dd HH') }"
```

## 📋 Чек-лист развертывания

- [ ] .NET Framework 4.8.1 установлен
- [ ] Ascon.Pilot.SDK доступен
- [ ] Newtonsoft.Json 13.0.0+ установлен
- [ ] Проект успешно собирается
- [ ] App.config настроен
- [ ] Rocket.Chat доступен и настроен
- [ ] SMTP-сервер доступен и настроен
- [ ] Папка logs создана с правами записи
- [ ] Тесты проходят успешно
- [ ] Логирование работает
- [ ] Модуль загружается в Pilot
- [ ] Уведомления отправляются

## 📞 Поддержка

### Контакты для поддержки:
- **Email:** support@tomsmineral.ru
- **Телефон:** +7 (XXX) XXX-XX-XX
- **Документация:** [Внутренняя Wiki]

### Полезные команды:
```bash
# Проверка версии .NET
dotnet --version

# Проверка сборки
msbuild /version

# Проверка служб
sc query "Ascon Pilot Service"

# Очистка логов
del /q "C:\Program Files\Ascon\Pilot\Extensions\logs\*.log"
```