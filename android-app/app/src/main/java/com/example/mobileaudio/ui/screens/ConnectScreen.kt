package com.example.mobileaudio.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.example.mobileaudio.network.DiscoveredPc
import com.example.mobileaudio.network.DiscoveryListener
import com.example.mobileaudio.ui.theme.Teal40

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ConnectScreen(
    onConnect: (String) -> Unit
) {
    val context = LocalContext.current
    var ipAddress by remember { mutableStateOf("") }
    var discoveredPcs by remember { mutableStateOf<List<DiscoveredPc>>(emptyList()) }
    var isSearching by remember { mutableStateOf(true) }
    val discoveryListener = remember { DiscoveryListener(context) }

    DisposableEffect(Unit) {
        discoveryListener.start { devices ->
            discoveredPcs = devices
            isSearching = devices.isEmpty()
        }
        onDispose { discoveryListener.stop() }
    }

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
            text = "Подключение к компьютеру",
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.7f)
        )

        Spacer(modifier = Modifier.height(32.dp))

        Text(
            text = "Найденные компьютеры",
            style = MaterialTheme.typography.titleMedium,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.9f),
            modifier = Modifier.fillMaxWidth()
        )

        Spacer(modifier = Modifier.height(8.dp))

        Card(
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(12.dp)
            ) {
                when {
                    isSearching && discoveredPcs.isEmpty() -> SearchingIndicator()
                    discoveredPcs.isEmpty() -> EmptyDevicesMessage()
                    else -> DevicesList(devices = discoveredPcs, onConnect = onConnect)
                }
            }
        }

        Spacer(modifier = Modifier.height(24.dp))

        Text(
            text = "или введите IP вручную",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.5f)
        )

        Spacer(modifier = Modifier.height(12.dp))

        OutlinedTextField(
            value = ipAddress,
            onValueChange = { ipAddress = it },
            label = { Text("IP-адрес компьютера") },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
            singleLine = true,
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(12.dp)
        )

        Spacer(modifier = Modifier.height(12.dp))

        Button(
            onClick = { if (ipAddress.isNotBlank()) onConnect(ipAddress) },
            modifier = Modifier
                .fillMaxWidth()
                .height(52.dp),
            shape = RoundedCornerShape(26.dp),
            colors = ButtonDefaults.buttonColors(containerColor = Teal40),
            enabled = ipAddress.isNotBlank()
        ) {
            Text("ПОДКЛЮЧИТЬСЯ", style = MaterialTheme.typography.labelLarge)
        }
    }
}

@Composable
private fun SearchingIndicator() {
    Column(
        modifier = Modifier.fillMaxSize(),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        CircularProgressIndicator(
            modifier = Modifier.size(40.dp),
            color = Teal40,
            strokeWidth = 3.dp
        )
        Spacer(modifier = Modifier.height(12.dp))
        Text(
            text = "Поиск устройств в сети...",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.5f)
        )
    }
}

@Composable
private fun EmptyDevicesMessage() {
    Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Text(
            text = "Устройства не найдены.\nУбедитесь, что PC-приложение запущено и оба устройства в одной сети.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.5f),
            textAlign = TextAlign.Center
        )
    }
}

@Composable
private fun DevicesList(
    devices: List<DiscoveredPc>,
    onConnect: (String) -> Unit
) {
    Column(
        modifier = Modifier.fillMaxSize(),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        devices.forEach { pc ->
            DiscoveredPcCard(pc = pc, onClick = { onConnect(pc.ipAddress) })
        }
    }
}

@Composable
private fun DiscoveredPcCard(
    pc: DiscoveredPc,
    onClick: () -> Unit
) {
    Card(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.3f)
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Column {
                Text(
                    text = pc.deviceName,
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onPrimaryContainer
                )
                Text(
                    text = pc.ipAddress,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onPrimaryContainer.copy(alpha = 0.7f)
                )
            }
            Icon(
                imageVector = Icons.Default.PlayArrow,
                contentDescription = "Подключиться",
                tint = Teal40,
                modifier = Modifier.size(28.dp)
            )
        }
    }
}

