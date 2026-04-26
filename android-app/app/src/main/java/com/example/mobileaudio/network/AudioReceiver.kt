package com.example.mobileaudio.network

import android.util.Log
import com.example.mobileaudio.audio.AudioPlayer
import kotlinx.coroutines.*
import kotlin.coroutines.coroutineContext
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetSocketAddress
import java.net.SocketTimeoutException
import java.nio.ByteBuffer
import java.util.concurrent.ConcurrentLinkedQueue

class AudioReceiver(
    private val onConnected: () -> Unit,
    private val onDisconnected: () -> Unit,
    private val onStatsUpdate: (Int, Int) -> Unit
) {
    companion object {
        private const val TAG = "AudioReceiver"
        // 48kHz stereo 16bit = 192000 bytes/sec
        // 5ms frame = 960 bytes
        private const val BYTES_PER_MS = 192
        private const val TARGET_LATENCY_MS = 30      // start playing after 30ms
        private const val MAX_LATENCY_MS = 80         // drop packets if latency exceeds 80ms
        private const val TARGET_JITTER_BYTES = TARGET_LATENCY_MS * BYTES_PER_MS  // 5760
        private const val MAX_JITTER_BYTES = MAX_LATENCY_MS * BYTES_PER_MS        // 15360
    }

    private var socket: DatagramSocket? = null
    private var receiveJob: Job? = null
    private var playJob: Job? = null
    private val audioPlayer = AudioPlayer()
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    private val jitterBuffer = ConcurrentLinkedQueue<ByteArray>()
    private var expectedSequence = 0u
    private var packetsReceived = 0
    private var packetsLost = 0
    private var bytesWritten = 0
    private var isPlaying = false

    fun start(pcIp: String, port: Int = 5000) {
        Log.d(TAG, "Starting receiver on port $port")
        stop()

        receiveJob = scope.launch {
            try {
                socket = DatagramSocket(null).apply {
                    reuseAddress = true
                    bind(InetSocketAddress(port))
                    soTimeout = 3000
                }
                Log.d(TAG, "Socket bound to port $port")

                audioPlayer.start()

                withContext(Dispatchers.Main) { onConnected() }

                // Start playback coroutine
                playJob = launch { playbackLoop() }

                val buffer = ByteArray(4096)
                var timeoutCount = 0
                while (isActive) {
                    try {
                        val packet = DatagramPacket(buffer, buffer.size)
                        socket?.receive(packet)
                        processPacket(packet)
                        timeoutCount = 0
                    } catch (_: SocketTimeoutException) {
                        timeoutCount++
                        if (timeoutCount % 10 == 0) {
                            Log.d(TAG, "No packets received for ${timeoutCount * 3} seconds. Check that PC app is streaming.")
                        }
                        continue
                    }
                }
            } catch (e: Exception) {
                Log.e(TAG, "Receiver error: ${e.message}")
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
        if (length < 4) {
            Log.w(TAG, "Packet too short: $length bytes")
            return
        }

        val seq = ByteBuffer.wrap(data, 0, 4).int.toUInt()
        if (seq > expectedSequence && expectedSequence != 0u) {
            packetsLost += (seq - expectedSequence).toInt()
        }
        expectedSequence = seq + 1u
        packetsReceived++

        val audioData = data.copyOfRange(4, length)

        synchronized(jitterBuffer) {
            var currentSize = jitterBuffer.sumOf { it.size }

            // Hard latency cap: drop oldest packets if we're over max
            while (currentSize + audioData.size > MAX_JITTER_BYTES && jitterBuffer.isNotEmpty()) {
                val dropped = jitterBuffer.poll()
                if (dropped != null) currentSize -= dropped.size
            }

            jitterBuffer.offer(audioData)
            currentSize += audioData.size

            // Log stats
            if (packetsReceived <= 5 || packetsReceived % 100 == 0) {
                val latencyMs = currentSize / BYTES_PER_MS
                val total = packetsReceived + packetsLost
                val lossPercent = if (total > 0) (packetsLost * 100 / total) else 0
                Log.d(TAG, "Packet #$packetsReceived, seq=$seq, size=${audioData.size}, " +
                        "latency=${latencyMs}ms, loss=$lossPercent%")
                CoroutineScope(Dispatchers.Main).launch {
                    onStatsUpdate(packetsReceived, lossPercent)
                }
            }
        }
    }

    private suspend fun playbackLoop() {
        Log.d(TAG, "Playback loop started")
        var playCount = 0
        var consecutiveEmpty = 0

        while (coroutineContext.isActive) {
            val data = synchronized(jitterBuffer) {
                val bufferSize = jitterBuffer.sumOf { it.size }

                if (!isPlaying) {
                    // Wait until we have enough data to start
                    if (bufferSize < TARGET_JITTER_BYTES) {
                        return@synchronized null
                    }
                    isPlaying = true
                    Log.d(TAG, "Started playing, buffer=${bufferSize / BYTES_PER_MS}ms")
                }

                // If buffer is critically low, we have an underrun
                if (bufferSize == 0) {
                    isPlaying = false
                    consecutiveEmpty++
                    return@synchronized null
                }
                consecutiveEmpty = 0

                jitterBuffer.poll()
            }

            if (data != null) {
                // Write all data, handle partial writes
                var offset = 0
                while (offset < data.size) {
                    val written = audioPlayer.write(data, offset, data.size - offset)
                    if (written > 0) {
                        offset += written
                        bytesWritten += written
                    } else if (written < 0) {
                        Log.e(TAG, "AudioTrack write error: $written")
                        break
                    }
                    // If written == 0, AudioTrack buffer is full; yield and retry
                    if (written == 0) {
                        delay(1)
                    }
                }

                playCount++
                if (playCount % 100 == 0) {
                    val latency = synchronized(jitterBuffer) { jitterBuffer.sumOf { it.size } / BYTES_PER_MS }
                    Log.d(TAG, "Played $playCount chunks, latency=${latency}ms")
                }
            } else {
                // Buffer underrun - wait a bit
                if (consecutiveEmpty > 10) {
                    // Reset playback state if we keep underrunning
                    isPlaying = false
                    consecutiveEmpty = 0
                }
                delay(5)
            }
        }
        Log.d(TAG, "Playback loop ended")
    }

    fun stop() {
        Log.d(TAG, "Stopping receiver. Packets: $packetsReceived, Bytes written: $bytesWritten")
        receiveJob?.cancel()
        playJob?.cancel()
        socket?.close()
        audioPlayer.stop()
        synchronized(jitterBuffer) {
            jitterBuffer.clear()
        }
        packetsReceived = 0
        packetsLost = 0
        bytesWritten = 0
        expectedSequence = 0u
        isPlaying = false
    }
}

