# MobileAudio

Система для стриминга системного аудио с компьютера на Android-устройство через локальную сеть с минимальной задержкой.

## Архитектура

- **PC-приложение** (C# WPF + NAudio) — захват системного аудио через WASAPI loopback и отправка по UDP
- **Android-приложение** (Kotlin + Jetpack Compose) — приём UDP-потока и воспроизведение через AudioTrack
- **Протокол** — UDP, PCM 16-bit 48kHz stereo, фреймы по 10 мс
- **Автообнаружение** — broadcast UDP на порту 5001

## Структура проекта

```
MobileAudio/
├── pc-app/              # C# WPF приложение
│   ├── MobileAudioPC.sln
│   └── MobileAudioPC/
│       ├── MobileAudioPC.csproj
│       ├── App.xaml
│       ├── MainWindow.xaml
│       ├── Audio/
│       │   ├── AudioCapture.cs
│       │   ├── UdpStreamer.cs
│       │   └── DiscoveryService.cs
│       ├── UI/
│       │   └── AudioVisualizer.xaml
│       └── Models/
│           └── AudioSettings.cs
├── android-app/         # Kotlin Android приложение
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

### Шаги

1. Откройте решение в Visual Studio:
   ```
   pc-app/MobileAudioPC.sln
   ```

2. Восстановите NuGet-пакеты (автоматически при сборке):
   - NAudio 2.2.1
   - MaterialDesignThemes 5.0.0
   - MaterialDesignColors 3.0.0

3. Соберите и запустите проект (F5 или Ctrl+F5).

### Альтернатива через командную строку
```bash
cd pc-app/MobileAudioPC
dotnet restore
dotnet build
dotnet run
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
| Размер фрейма | 10 мс (1920 байт) |
| Порт аудио | 5000 |
| Порт обнаружения | 5001 |

## Поддержка 24-bit и других форматов

PC-приложение автоматически конвертирует любой системный формат аудио (16-bit, 24-bit, 32-bit float, 32-bit int, любая частота дискретизации) в стандартизированный 16-bit PCM 48kHz stereo перед отправкой на телефон. Конвертация выполняется через `MediaFoundationResampler` из NAudio с максимальным качеством (`ResamplerQuality = 60`).

Таким образом, независимо от настроек звуковой карты или формата вывода приложений на ПК, Android-устройство всегда получает корректный поток.

## Оптимизация задержки

- Используйте 5GHz Wi-Fi вместо 2.4GHz
- Разместите устройства ближе к роутеру
- Закройте другие сетевые приложения
- На Android используйте режим "Игры" или "Производительность"

## Лицензия

MIT License

