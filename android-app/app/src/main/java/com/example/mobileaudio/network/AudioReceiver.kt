package com.example.mobileaudio.network

import com.example.mobileaudio.audio.AudioPlayer
import kotlinx.coroutines.*
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetSocketAddress
import java.net.SocketTimeoutException
import java.nio.ByteBuffer

class AudioReceiver(
    private val onConnected: () -> Unit,
    private val onDisconnected: () -> Unit,
    private val onStatsUpdate: (Int, Int) -> Unit
) {
    private var socket: DatagramSocket? = null
    private var job: Job? = null
    private val audioPlayer = AudioPlayer()
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private var expectedSequence = 0u
    private var packetsReceived = 0
    private var packetsLost = 0

    fun start(pcIp: String, port: Int = 5000) {
        stop()
        job = scope.launch {
            try {
                socket = DatagramSocket(null).apply {
                    reuseAddress = true
                    bind(InetSocketAddress(port))
                    soTimeout = 3000
                }
                audioPlayer.start()
                withContext(Dispatchers.Main) { onConnected() }

                val buffer = ByteArray(4096)
                while (isActive) {
                    try {
                        val packet = DatagramPacket(buffer, buffer.size)
                        socket?.receive(packet)
                        processPacket(packet)
                    } catch (_: SocketTimeoutException) {
                        continue
                    }
                }
            } catch (e: Exception) {
                e.printStackTrace()
            } finally {
                withContext(Dispatchers.Main) { onDisconnected() }
                audioPlayer.stop()
                socket?.close()
            }
        }
    }

    private fun processPacket(packet: DatagramPacket) {
        val data = packet.data
        val length = packet.length
        if (length < 4) return

        val seq = ByteBuffer.wrap(data, 0, 4).int.toUInt()
        if (seq > expectedSequence && expectedSequence != 0u) {
            packetsLost += (seq - expectedSequence).toInt()
        }
        expectedSequence = seq + 1u
        packetsReceived++

        audioPlayer.write(data, 4, length - 4)

        if (packetsReceived % 100 == 0) {
            val total = packetsReceived + packetsLost
            val lossPercent = if (total > 0) (packetsLost * 100 / total) else 0
            CoroutineScope(Dispatchers.Main).launch {
                onStatsUpdate(packetsReceived, lossPercent)
            }
        }
    }

    fun stop() {
        job?.cancel()
        socket?.close()
        audioPlayer.stop()
        packetsReceived = 0
        packetsLost = 0
        expectedSequence = 0u
    }
}

