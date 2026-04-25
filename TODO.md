# MobileAudio Project TODO

## PC Application (C# WPF + NAudio)
- [x] Create solution and project files
- [x] Create App.xaml and App.xaml.cs
- [x] Create MainWindow.xaml and MainWindow.xaml.cs
- [x] Create AudioCapture.cs (NAudio WASAPI loopback)
- [x] Create UdpStreamer.cs (UDP audio streaming)
- [x] Create DiscoveryService.cs (auto-discovery broadcast)
- [x] Create AudioVisualizer user control
- [x] Create AudioSettings model
- [x] Integrate real audio levels into visualizer

## Android Application (Kotlin + Jetpack Compose)
- [x] Create project build files
- [x] Create AndroidManifest.xml
- [x] Create MainActivity.kt
- [x] Create AudioPlayer.kt
- [x] Create AudioReceiver.kt
- [x] Create DiscoveryListener.kt
- [x] Create UI screens (Connect, Player)
- [x] Create UI theme (Material3 dark)
- [x] Create AudioVisualizer component

## Documentation
- [x] Create README.md with build and run instructions

## Status: COMPLETE

## Installer (WiX v7)
- [x] Create WiX installer project (`pc-app/MobileAudioPC.Installer/Package.wxs`)
- [x] Create build script (`pc-app/build-installer.ps1`)
- [x] Build MSI successfully: `pc-app/MobileAudioPC.Installer/MobileAudioPC.msi`
- [x] Add installer project to solution (`MobileAudioPC.sln`)

