package com.example.mobileaudio

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.*
import androidx.core.view.WindowCompat
import com.example.mobileaudio.network.AudioReceiver
import com.example.mobileaudio.network.AudioStats
import com.example.mobileaudio.ui.screens.ConnectScreen
import com.example.mobileaudio.ui.screens.PlayerScreen
import com.example.mobileaudio.ui.theme.MobileAudioTheme

/**
 * Navigation destinations represented as a sealed class for type safety.
 */
private sealed class Screen {
    data object Connect : Screen()
    data class Player(val pcIp: String) : Screen()
}

class MainActivity : ComponentActivity() {

    private var audioReceiver: AudioReceiver? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        WindowCompat.setDecorFitsSystemWindows(window, false)

        setContent {
            MobileAudioTheme {
                var currentScreen by remember { mutableStateOf<Screen>(Screen.Connect) }
                var isConnected by remember { mutableStateOf(false) }
                var stats by remember { mutableStateOf(AudioStats.EMPTY) }
                var latencyMs by remember { mutableIntStateOf(30) }

                audioReceiver = remember {
                    AudioReceiver(
                        onConnected = { isConnected = true },
                        onDisconnected = { isConnected = false },
                        onStatsUpdate = { stats = it }
                    )
                }

                when (val screen = currentScreen) {
                    is Screen.Connect -> ConnectScreen(
                        onConnect = { ip ->
                            audioReceiver?.start(ip)
                            currentScreen = Screen.Player(ip)
                        }
                    )
                    is Screen.Player -> PlayerScreen(
                        pcIp = screen.pcIp,
                        isConnected = isConnected,
                        packetsReceived = stats.packetsReceived,
                        packetLossPercent = stats.lossPercent,
                        currentLatencyMs = latencyMs,
                        onLatencyChange = { newLatency ->
                            latencyMs = newLatency
                            audioReceiver?.setLatency(newLatency)
                        },
                        onDisconnect = {
                            audioReceiver?.stop()
                            currentScreen = Screen.Connect
                        }
                    )
                }
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        audioReceiver?.stop()
        audioReceiver = null
    }
}

