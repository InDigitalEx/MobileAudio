package com.example.mobileaudio.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.mobileaudio.ui.components.AudioVisualizer
import com.example.mobileaudio.ui.theme.Teal40

private const val LATENCY_MIN = 10f
private const val LATENCY_MAX = 80f
private const val LATENCY_STEP = 10f

@Composable
fun PlayerScreen(
    pcIp: String,
    isConnected: Boolean,
    packetsReceived: Int,
    packetLossPercent: Int,
    currentLatencyMs: Int,
    onLatencyChange: (Int) -> Unit,
    onDisconnect: () -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = "MobileAudio",
            style = MaterialTheme.typography.headlineLarge,
            color = Teal40
        )

        Spacer(modifier = Modifier.height(8.dp))

        Text(
            text = "Воспроизведение с $pcIp",
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.7f)
        )

        Spacer(modifier = Modifier.height(32.dp))

        Card(
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f),
            shape = RoundedCornerShape(20.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center
            ) {
                AudioVisualizer(isPlaying = isConnected)

                Spacer(modifier = Modifier.height(32.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceEvenly
                ) {
                    StatCard("Статус", if (isConnected) "Подключено" else "Ожидание...")
                    StatCard("Пакетов", packetsReceived.toString())
                    StatCard("Потери", "$packetLossPercent%")
                }

                Spacer(modifier = Modifier.height(24.dp))

                LatencyControl(
                    currentLatencyMs = currentLatencyMs,
                    onLatencyChange = onLatencyChange
                )
            }
        }

        Spacer(modifier = Modifier.height(24.dp))

        Button(
            onClick = onDisconnect,
            modifier = Modifier
                .fillMaxWidth()
                .height(56.dp),
            shape = RoundedCornerShape(28.dp),
            colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
        ) {
            Text("ОТКЛЮЧИТЬСЯ", style = MaterialTheme.typography.labelLarge)
        }
    }
}

@Composable
private fun LatencyControl(
    currentLatencyMs: Int,
    onLatencyChange: (Int) -> Unit
) {
    Column(
        modifier = Modifier.fillMaxWidth(),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = "Задержка буфера: ${currentLatencyMs}ms",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.8f)
        )

        // Steps: 10,20,30,40,50,60,70,80 -> 7 intervals -> steps = 6
        val stepsCount = ((LATENCY_MAX - LATENCY_MIN) / LATENCY_STEP).toInt() - 1

        Slider(
            value = currentLatencyMs.toFloat(),
            onValueChange = { onLatencyChange(it.toInt()) },
            valueRange = LATENCY_MIN..LATENCY_MAX,
            steps = stepsCount,
            modifier = Modifier.fillMaxWidth(),
            colors = SliderDefaults.colors(
                thumbColor = Teal40,
                activeTrackColor = Teal40
            )
        )

        Text(
            text = latencyDescription(currentLatencyMs),
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.5f)
        )
    }
}

private fun latencyDescription(ms: Int): String = when {
    ms <= 20 -> "Низкая (могут быть щелчки)"
    ms <= 40 -> "Сбалансированная"
    else -> "Стабильная (больше задержка)"
}

@Composable
private fun StatCard(label: String, value: String) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Text(
            text = value,
            style = MaterialTheme.typography.headlineMedium,
            color = Teal40
        )
        Text(
            text = label,
            style = MaterialTheme.typography.labelLarge,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.6f)
        )
    }
}

