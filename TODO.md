# MobileAudio Project TODO

## PC Application (C# WPF + NAudio)
- [x] Create solution and project files
- [x] Create App.xaml and App.xaml.cs
- [x] Create MainWindow.xaml and MainWindow.xaml.cs
- [x] Create AudioCapture.cs (WASAPI loopback, resampler, Console logging)
- [x] Create UdpStreamer.cs (UDP streaming, Console logging)
- [x] Create DiscoveryService.cs (auto-discovery broadcast)
- [x] Create AudioVisualizer user control
- [x] Create AudioSettings model
- [x] Build passes
- [x] Added in-app logger (LogTextBox in UI)

## Android Application (Kotlin + Jetpack Compose)
- [x] Create project build files
- [x] Create AndroidManifest.xml
- [x] Create MainActivity.kt
- [x] Create AudioPlayer.kt (PERFORMANCE_MODE_LOW_LATENCY, Log.d)
- [x] Create AudioReceiver.kt (jitter buffer, playback loop, Log.d)
- [x] Create DiscoveryListener.kt (two-way broadcast + multicast lock)
- [x] Create UI screens (Connect with device cards, Player)
- [x] Create UI theme (Material3 dark)
- [x] Create AudioVisualizer component

## Device Discovery (completed)
- [x] Two-way discovery: PC broadcasts itself, Android broadcasts itself
- [x] PC shows list of found phones with friendly names (hostname)
- [x] Android shows list of found PCs with friendly names (device model)
- [x] Click/tap on discovered device auto-fills IP and connects
- [x] Auto-cleanup of stale devices (10s TTL)
- [x] Manual IP entry as fallback
- [x] Android: WifiManager.MulticastLock for reliable broadcast reception

## Documentation
- [x] Create README.md with build and run instructions

## Diagnostics (completed)
- [x] Added Console.WriteLine logging to PC app
- [x] Added Log.d logging to Android app
- [x] Added in-app logger UI to PC app
- [x] Fixed: MTU issue (DontFragment + 10ms frames = dropped packets)
- [x] Fixed: Frame timing drift (PC sent faster than real-time)
- [x] Fixed: Android jitter buffer growing unbounded
- [x] Fixed: AudioTrack buffer accumulation causing 5s lag
- [x] Reduced frame size from 10ms to 5ms for better MTU compatibility
