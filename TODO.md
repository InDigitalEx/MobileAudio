# Refactoring TODO

## Android App
- [x] Create NetworkConstants.kt
- [x] Create AudioStats.kt
- [x] Refactor AudioPlayer.kt
- [x] Refactor AudioReceiver.kt (extract JitterBuffer, fix coroutines)
- [x] Refactor DiscoveryListener.kt (delay instead of Thread.sleep)
- [x] Refactor MainActivity.kt (remove double init, use sealed class for screens)
- [x] Refactor ConnectScreen.kt (remove unused imports, simplify logic)
- [x] Refactor PlayerScreen.kt (fix slider steps)
- [x] Refactor AudioVisualizer.kt (replace 32 coroutines with InfiniteTransition)
- [x] Cleanup Theme.kt (remove unused DarkCard, respect darkTheme)
- [x] Cleanup Color.kt (remove unused DarkCard)

## PC App
- [x] Create NetworkHelper.cs
- [x] Create BigEndianExtensions.cs
- [x] Make AudioSettings.cs immutable (sealed record)
- [x] Refactor AudioCapture.cs (fix resampling, reuse arrays, fix timing with Stopwatch)
- [x] Refactor UdpStreamer.cs (fix dispose, use big-endian extensions)
- [x] Refactor DiscoveryService.cs (async receive, proper cleanup, immutable device record)
- [x] Refactor MainWindow.xaml.cs (extract UI creation, remove duplicate IP helper)
- [x] Refactor AudioVisualizer.xaml.cs (fix nullability, division safety)

## Verification
- [ ] Verify Android build
- [ ] Verify PC build

