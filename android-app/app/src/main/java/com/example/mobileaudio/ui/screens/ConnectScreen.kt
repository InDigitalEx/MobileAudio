package com.example.mobileaudio.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.example.mobileaudio.network.DiscoveryListener
import com.example.mobileaudio.ui.theme.Teal40
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ConnectScreen(
    onConnect: (String) -> Unit
) {
    var ipAddress by remember { mutableStateOf("") }
    var isSearching by remember { mutableStateOf(false) }
    var discoveredIp by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()
    val discoveryListener = remember { DiscoveryListener() }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
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

        Spacer(modifier = Modifier.height(48.dp))

        OutlinedTextField(
            value = ipAddress,
            onValueChange = { ipAddress = it },
            label = { Text("IP-адрес компьютера") },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
            singleLine = true,
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(12.dp)
        )

        Spacer(modifier = Modifier.height(16.dp))

        Button(
            onClick = {
                if (ipAddress.isNotBlank()) {
                    onConnect(ipAddress)
                }
            },
            modifier = Modifier
                .fillMaxWidth()
                .height(56.dp),
            shape = RoundedCornerShape(28.dp),
            colors = ButtonDefaults.buttonColors(containerColor = Teal40)
        ) {
            Text("ПОДКЛЮЧИТЬСЯ", style = MaterialTheme.typography.labelLarge)
        }

        Spacer(modifier = Modifier.height(24.dp))

        Text(
            text = "или",
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onBackground.copy(alpha = 0.5f)
        )

        Spacer(modifier = Modifier.height(24.dp))

        Button(
            onClick = {
                isSearching = true
                discoveredIp = null
                discoveryListener.start { ip, _ ->
                    discoveredIp = ip
                    isSearching = false
                }
                scope.launch {
                    delay(5000)
                    if (isSearching) {
                        isSearching = false
                        discoveryListener.stop()
                    }
                }
            },
            modifier = Modifier
                .fillMaxWidth()
                .height(56.dp),
            shape = RoundedCornerShape(28.dp),
            colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
            enabled = !isSearching
        ) {
            if (isSearching) {
                CircularProgressIndicator(
                    modifier = Modifier.size(24.dp),
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            } else {
                Text("АВТООБНАРУЖЕНИЕ", style = MaterialTheme.typography.labelLarge)
            }
        }

        discoveredIp?.let {
            Spacer(modifier = Modifier.height(16.dp))
            Card(
                onClick = { onConnect(it) },
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)
            ) {
                Text(
                    text = "Найден: $it",
                    modifier = Modifier.padding(16.dp),
                    color = MaterialTheme.colorScheme.onPrimaryContainer
                )
            }
        }
    }
}

