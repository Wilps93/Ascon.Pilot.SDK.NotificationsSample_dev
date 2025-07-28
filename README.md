# Ascon.Pilot.SDK.NotificationsSample Extension

## Overview
This is a .NET Framework 4.8.1 extension for Ascon.Pilot.SDK that provides notification functionality and integration with Rocket.Chat.

## Issues Fixed

### 1. Project Configuration Issues
- **Fixed**: Removed Cyrillic path references (`..\Новая папка\`) from project file
- **Fixed**: Updated Newtonsoft.Json to use NuGet package reference instead of local DLL
- **Added**: Configuration file (`appsettings.json`) to externalize hardcoded settings

### 2. Interface Implementation Issues
- **Fixed**: All `NotImplementedException` throws in interface implementations
- **Fixed**: IFileProvider methods now properly delegate to injected file provider
- **Fixed**: IObjectBuilder methods now properly delegate to injected modifier
- **Fixed**: ISignatureBuilder methods now return `this` for method chaining

### 3. Exception Handling Issues
- **Fixed**: Simplified complex exception handling with unnecessary casting
- **Fixed**: Removed redundant exception variable assignments
- **Improved**: Exception messages and logging

### 4. Code Quality Improvements
- **Fixed**: DataObject property no longer throws NotImplementedException
- **Improved**: Better null checking with null-conditional operators
- **Added**: Proper delegation pattern for interface implementations

## Configuration

The application now uses `appsettings.json` for configuration:

```json
{
  "RocketChat": {
    "BaseUrl": "http://192.168.10.180:3000",
    "AuthToken": "YxGV8XDD9dBIRuKLn6nNOv1JeoXHF_anND0s58oS4xR",
    "UserId": "PRGcw8PrY9YNyGfjo"
  },
  "BotSettings": {
    "BotDataGuid": "f6f831df-0e77-4060-98d2-4f45b114750c",
    "MaxMessageLength": 2000
  }
}
```

## Dependencies

- .NET Framework 4.8.1
- Ascon.Pilot.SDK (referenced from `..\Lib\Ascon.Pilot.SDK.dll`)
- Newtonsoft.Json 13.0.3 (NuGet package)
- System.ComponentModel.Composition
- System.Net.Http

## Building

1. Ensure the Ascon.Pilot.SDK.dll is available in the `..\Lib\` directory
2. Restore NuGet packages: `nuget restore`
3. Build the solution: `msbuild Ascon.Pilot.SDK.NotificationsSample.ext2.sln`

## Known Issues

1. **Missing Dependencies**: The Ascon.Pilot.SDK.dll must be manually placed in the `..\Lib\` directory
2. **Configuration Loading**: The appsettings.json configuration loading is not yet implemented in the code
3. **Resource Files**: Some resource references may need to be updated

## Recommendations

1. **Implement Configuration Loading**: Add proper configuration loading from appsettings.json
2. **Add Unit Tests**: The codebase would benefit from unit tests
3. **Refactor Large Class**: The Main class is very large and should be split into smaller, focused classes
4. **Add Logging**: Implement proper structured logging instead of Debug.WriteLine
5. **Security**: Move sensitive configuration (tokens, URLs) to secure configuration management

## Architecture

The extension implements multiple interfaces:
- `IDataPlugin`: Data processing functionality
- `IObjectCardHandler`: Object card handling
- `ISignatureModifier`: Signature modification
- `IFileProvider`: File operations
- `IMenu<ObjectsViewContext>`: Menu functionality
- `IHotKey<ObjectsViewContext>`: Hotkey support
- `IObserver<INotification>`: Notification handling

## Usage

This extension provides:
- Rocket.Chat integration for notifications
- Email notification support
- Object state management
- File operations
- Menu and hotkey functionality