package com.example.mobileaudio

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.*
import androidx.core.view.WindowCompat
import com.example.mobileaudio.network.AudioReceiver
import com.example.mobileaudio.ui.screens.ConnectScreen
import com.example.mobileaudio.ui.screens.PlayerScreen
import com.example.mobileaudio.ui.theme.MobileAudioTheme

class MainActivity : ComponentActivity() {
    private lateinit var audioReceiver: AudioReceiver

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        WindowCompat.setDecorFitsSystemWindows(window, false)

        audioReceiver = AudioReceiver(
            onConnected = {},
            onDisconnected = {},
            onStatsUpdate = { _, _ -> }
        )

        setContent {
            MobileAudioTheme {
                var currentScreen by remember { mutableStateOf("connect") }
                var pcIp by remember { mutableStateOf("") }
                var isConnected by remember { mutableStateOf(false) }
                var packetsReceived by remember { mutableIntStateOf(0) }
                var packetLossPercent by remember { mutableIntStateOf(0) }

                audioReceiver = remember {
                    AudioReceiver(
                        onConnected = { isConnected = true },
                        onDisconnected = { isConnected = false },
                        onStatsUpdate = { received, loss ->
                            packetsReceived = received
                            packetLossPercent = loss
                        }
                    )
                }

                when (currentScreen) {
                    "connect" -> ConnectScreen(
                        onConnect = { ip ->
                            pcIp = ip
                            audioReceiver.start(ip)
                            currentScreen = "player"
                        }
                    )
                    "player" -> PlayerScreen(
                        pcIp = pcIp,
                        isConnected = isConnected,
                        packetsReceived = packetsReceived,
                        packetLossPercent = packetLossPercent,
                        onDisconnect = {
                            audioReceiver.stop()
                            currentScreen = "connect"
                        }
                    )
                }
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        if (::audioReceiver.isInitialized) {
            audioReceiver.stop()
        }
    }
}

