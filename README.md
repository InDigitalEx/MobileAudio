# MobileAudio

Потоковая передача системного аудио с компьютера на Android-устройство по локальной сети с минимальной задержкой.

## Архитектура

- **PC-приложение** (C# WPF + NAudio) — захват системного аудио через WASAPI loopback и отправка по UDP
- **Android-приложение** (Kotlin + Jetpack Compose) — приём UDP-потока и воспроизведение через AudioTrack
- **Протокол** — UDP, PCM 16-bit 48 kHz stereo, фреймы по 10 мс
- **Автообнаружение** — broadcast UDP на порту 5001

## Структура проекта

```
MobileAudio/
├── pc-app/                    # C# WPF приложение
│   ├── MobileAudio.sln
│   ├── build-installer.ps1    # Скрипт сборки установщика
│   ├── clean.ps1              # Скрипт очистки артефактов сборки
│   ├── MobileAudio/
│   │   ├── MobileAudio.csproj
│   │   ├── App.xaml
│   │   ├── MainWindow.xaml
│   │   ├── Audio/
│   │   │   ├── AudioCapture.cs
│   │   │   ├── UdpStreamer.cs
│   │   │   └── DiscoveryService.cs
│   │   ├── UI/
│   │   │   └── AudioVisualizer.xaml
│   │   └── Models/
│   │       └── AudioSettings.cs
│   └── Setup/
│       ├── setup.iss          # Скрипт Inno Setup
│       └── setup-icon.ico
├── android-app/               # Kotlin Android приложение
│   ├── build.gradle.kts
│   ├── settings.gradle.kts
│   └── app/
│       ├── build.gradle.kts
│       └── src/main/
│           ├── AndroidManifest.xml
│           ├── java/com/example/mobileaudio/
│           │   ├── MainActivity.kt
│           │   ├── audio/AudioPlayer.kt
│           │   ├── network/AudioReceiver.kt
│           │   ├── network/DiscoveryListener.kt
│           │   ├── ui/screens/ConnectScreen.kt
│           │   ├── ui/screens/PlayerScreen.kt
│           │   ├── ui/components/AudioVisualizer.kt
│           │   └── ui/theme/
│           └── res/values/
└── README.md
```

## Сборка и запуск PC-приложения

### Требования
- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 (или VS Code + C# Dev Kit)

### Сборка через Visual Studio

1. Откройте решение:
   ```
   pc-app/MobileAudio.sln
   ```

2. Восстановите NuGet-пакеты (автоматически при сборке):
   - NAudio 2.2.1
   - MaterialDesignThemes 5.0.0
   - MaterialDesignColors 3.0.0

3. Соберите и запустите проект (F5 или Ctrl+F5).

### Сборка через командную строку
```powershell
cd pc-app/MobileAudio
dotnet restore
dotnet build
dotnet run
```

## Очистка артефактов сборки

```powershell
cd pc-app
.\clean.ps1
```

Удаляет папки `bin/`, `obj/`, `publish/` и скомпилированный установщик.

## Сборка установщика (Inno Setup)

### Требования
- Установленный [Inno Setup 6](https://jrsoftware.org/isdl.php)

### Шаги
```powershell
cd pc-app
.\build-installer.ps1
```

Результат: `pc-app/Setup/Output/MobileAudio-Setup.exe`

Установщик содержит self-contained сборку приложения (включая .NET 8 Runtime) и работает на Windows 10/11 x64 без дополнительных зависимостей.

### Примечание: размер Release

Release-сборка создаётся как `self-contained` — в папку `publish` копируется весь .NET Runtime (~30–50 МБ, сотни файлов). Это ожидаемо и необходимо для автономной работы установщика.

Если нужна миниатюрная версия (требует .NET 8 Desktop Runtime на ПК пользователя):
```powershell
dotnet publish MobileAudio\MobileAudio.csproj -c Release -r win-x64 --self-contained false
```

## Сборка и запуск Android-приложения

### Требования
- Android Studio Hedgehog (2023.1.1) или новее
- Android SDK 34
- JDK 17
- Устройство Android 8.0+ (API 26+)

### Шаги

1. Откройте проект в Android Studio:
   ```
   android-app/
   ```

2. Дождитесь синхронизации Gradle.

3. Подключите Android-устройство по USB (включите отладку по USB) или используйте эмулятор.

4. Нажмите **Run** (Shift+F10).

### Альтернатива через командную строку
```bash
cd android-app
./gradlew assembleDebug
adb install app/build/outputs/apk/debug/app-debug.apk
```

## Использование

1. **Запустите PC-приложение** на компьютере.
   - Приложение покажет ваш локальный IP-адрес.
   - Служба автообнаружения запускается автоматически.

2. **Запустите Android-приложение** на телефоне.

3. **Подключитесь** одним из способов:
   - Введите IP компьютера вручную и нажмите "ПОДКЛЮЧИТЬСЯ"
   - Нажмите "АВТООБНАРУЖЕНИЕ" — приложение найдёт компьютер автоматически

4. **Начните стриминг** на PC:
   - Введите IP телефона в поле "IP телефона"
   - Нажмите "СТАРТ"

5. **Звук** с компьютера теперь воспроизводится на телефоне.

## Настройка брандмауэра Windows

Если соединение не устанавливается, разрешите UDP-порты:

```powershell
# PowerShell от имени администратора
New-NetFirewallRule -DisplayName "MobileAudio" -Direction Inbound -Protocol UDP -LocalPort 5000,5001 -Action Allow
```

## Параметры аудио

| Параметр | Значение |
|----------|----------|
| Частота дискретизации | 48 кГц |
| Каналы | Stereo (2) |
| Битность | 16-bit |
| Формат | PCM |
| Размер фрейма | 10 мс |
| Порт аудио | 5000 |
| Порт обнаружения | 5001 |

## Поддержка 24-bit и других форматов

PC-приложение автоматически конвертирует любой системный формат аудио (16-bit, 24-bit, 32-bit float, любая частота дискретизации и количество каналов) в стандартизированный 16-bit PCM 48 kHz stereo перед отправкой на телефон.

## Оптимизация задержки

- Используйте 5 GHz Wi-Fi вместо 2.4 GHz
- Разместите устройства ближе к роутеру
- Закройте другие сетевые приложения
- На Android используйте режим "Игры" или "Производительность"

## Лицензия

MIT License

