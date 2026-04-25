package com.example.mobileaudio.network

import kotlinx.coroutines.*
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.nio.charset.Charset

class DiscoveryListener {
    private var socket: DatagramSocket? = null
    private var job: Job? = null
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    fun start(onFound: (String, Int) -> Unit) {
        stop()
        job = scope.launch {
            try {
                socket = DatagramSocket(5001).apply { broadcast = true }
                val buffer = ByteArray(256)
                val broadcastAddress = InetAddress.getByName("255.255.255.255")
                val request = "DISCOVER_MOBILE_AUDIO".toByteArray(Charset.forName("UTF-8"))
                val requestPacket = DatagramPacket(request, request.size, broadcastAddress, 5001)
                socket?.send(requestPacket)

                while (isActive) {
                    try {
                        val packet = DatagramPacket(buffer, buffer.size)
                        socket?.receive(packet)
                        val response = String(packet.data, 0, packet.length, Charset.forName("UTF-8"))
                        if (response.startsWith("MOBILE_AUDIO_PC")) {
                            val parts = response.split("|")
                            if (parts.size >= 3) {
                                val port = parts[1].toIntOrNull() ?: 5000
                                val ip = parts[2]
                                withContext(Dispatchers.Main) { onFound(ip, port) }
                                break
                            }
                        }
                    } catch (_: Exception) {
                        if (!isActive) break
                    }
                }
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }
    }

    fun stop() {
        job?.cancel()
        socket?.close()
    }
}

