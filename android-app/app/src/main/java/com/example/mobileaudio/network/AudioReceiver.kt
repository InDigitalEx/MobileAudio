package com.example.mobileaudio.network

import android.util.Log
import com.example.mobileaudio.audio.AudioPlayer
import com.example.mobileaudio.audio.JitterBuffer
import kotlinx.coroutines.*
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetSocketAddress
import java.net.SocketTimeoutException
import java.nio.ByteBuffer
import java.util.ArrayDeque
import kotlin.coroutines.coroutineContext

/**
 * Receives audio over UDP, manages a jitter buffer, and plays back via [AudioPlayer].
 *
 * @param onConnected Invoked on the main thread when streaming begins.
 * @param onDisconnected Invoked on the main thread when streaming ends.
 * @param onStatsUpdate Invoked on the main thread with periodic [AudioStats].
 * @param targetLatencyMs Desired buffer latency in milliseconds (10–100).
 */
class AudioReceiver(
    private val onConnected: () -> Unit = {},
    private val onDisconnected: () -> Unit = {},
    private val onStatsUpdate: (AudioStats) -> Unit = {},
    targetLatencyMs: Int = 30
) {

    companion object {
        private const val TAG = "AudioReceiver"
        private const val RECEIVE_BUFFER_SIZE = 4096
        private const val PLAYBACK_DELAY_MS = 5L
        private const val STATS_INTERVAL_PACKETS = 100
    }

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var receiveJob: Job? = null
    private var playJob: Job? = null

    private val audioPlayer = AudioPlayer()
    private val jitterBuffer = JitterBuffer()

    private val silenceFrame: ByteArray by lazy {
        ByteArray(NetworkConstants.BYTES_PER_MS * 3) // ~3ms silence
    }

    @Volatile
    private var isPlaying = false

    @Volatile
    private var expectedSequence = 0u

    private var packetsReceived = 0
    private var packetsLost = 0

    @Volatile
    var latencyMs: Int = targetLatencyMs.coerceIn(10, NetworkConstants.MAX_LATENCY_MS)
        private set

    /** Rolling window of instantaneous buffer latencies for averaging. */
    private val latencyHistory = ArrayDeque<Int>()
    private val latencyHistoryMaxSize = 50 // ~5s of samples at 100 packets/sec

    private val targetJitterBytes: Int
        get() = latencyMs * NetworkConstants.BYTES_PER_MS

    private val maxJitterBytes: Int
        get() = NetworkConstants.MAX_LATENCY_MS * NetworkConstants.BYTES_PER_MS

    /** Updates the target latency. Takes effect on the next buffer pre-fill. */
    fun setLatency(ms: Int) {
        latencyMs = ms.coerceIn(10, NetworkConstants.MAX_LATENCY_MS)
        Log.d(TAG, "Latency set to ${latencyMs}ms")
    }

    /** Starts receiving audio from [pcIp]:[port]. */
    fun start(pcIp: String, port: Int = NetworkConstants.UDP_PORT) {
        if (receiveJob?.isActive == true) {
            Log.w(TAG, "Already running, ignoring start() call")
            return
        }

        resetState()
        Log.d(TAG, "Starting receiver on port $port")

        receiveJob = scope.launch {
            var socket: DatagramSocket? = null
            try {
                socket = DatagramSocket(null).apply {
                    reuseAddress = true
                    bind(InetSocketAddress(port))
                    soTimeout = NetworkConstants.SOCKET_TIMEOUT_MS
                }
                Log.d(TAG, "Socket bound to port $port")

                audioPlayer.start()
                withContext(Dispatchers.Main) { onConnected() }

                playJob = launch { playbackLoop() }

                val buffer = ByteArray(RECEIVE_BUFFER_SIZE)
                var timeoutCount = 0

                while (isActive) {
                    try {
                        val packet = DatagramPacket(buffer, buffer.size)
                        socket.receive(packet)
                        processPacket(packet)
                        timeoutCount = 0
                    } catch (_: SocketTimeoutException) {
                        timeoutCount++
                        if (timeoutCount % 10 == 0) {
                            Log.d(TAG, "No packets for ${timeoutCount * 3}s")
                        }
                    }
                }
            } catch (e: Exception) {
                if (e !is CancellationException) {
                    Log.e(TAG, "Receiver error: ${e.message}", e)
                }
            } finally {
                socket?.close()
                withContext(NonCancellable + Dispatchers.Main) { onDisconnected() }
                audioPlayer.stop()
            }
        }
    }

    /** Stops receiving and releases all resources. Safe to call multiple times. */
    fun stop() {
        Log.d(TAG, "Stopping receiver. Packets: $packetsReceived, Lost: $packetsLost")
        scope.coroutineContext.cancelChildren()
        receiveJob = null
        playJob = null
        audioPlayer.stop()
        jitterBuffer.clear()
        resetState()
    }

    private fun resetState() {
        packetsReceived = 0
        packetsLost = 0
        expectedSequence = 0u
        isPlaying = false
        latencyHistory.clear()
    }

    private fun processPacket(packet: DatagramPacket) {
        val data = packet.data
        val length = packet.length
        if (length < NetworkConstants.PACKET_HEADER_BYTES) {
            Log.w(TAG, "Packet too short: $length bytes")
            return
        }

        val seq = ByteBuffer.wrap(data, 0, NetworkConstants.PACKET_HEADER_BYTES).int.toUInt()
        if (seq > expectedSequence && expectedSequence != 0u) {
            packetsLost += (seq - expectedSequence).toInt()
        }
        expectedSequence = seq + 1u
        packetsReceived++

        val audioData = data.copyOfRange(NetworkConstants.PACKET_HEADER_BYTES, length)
        jitterBuffer.offer(audioData, maxJitterBytes)

        // Track instantaneous latency for rolling average
        val instantLatency = jitterBuffer.sizeBytes / NetworkConstants.BYTES_PER_MS
        latencyHistory.addLast(instantLatency)
        if (latencyHistory.size > latencyHistoryMaxSize) {
            latencyHistory.removeFirst()
        }

        if (packetsReceived <= 5 || packetsReceived % STATS_INTERVAL_PACKETS == 0) {
            val avgLatency = if (latencyHistory.isNotEmpty()) {
                latencyHistory.sum() / latencyHistory.size
            } else 0

            val stats = AudioStats(
                packetsReceived = packetsReceived,
                packetsLost = packetsLost,
                latencyMs = instantLatency,
                averageLatencyMs = avgLatency
            )
            Log.d(TAG, "Packet #$packetsReceived seq=$seq size=${audioData.size} " +
                    "latency=${stats.latencyMs}ms avg=${stats.averageLatencyMs}ms loss=${stats.lossPercent}%")
            scope.launch(Dispatchers.Main) { onStatsUpdate(stats) }
        }
    }

    private suspend fun playbackLoop() {
        Log.d(TAG, "Playback loop started")
        var playCount = 0
        var consecutiveEmpty = 0

        while (coroutineContext.isActive) {
            val data = synchronized(jitterBuffer) {
                val bufferSize = jitterBuffer.sizeBytes
                if (!isPlaying) {
                    if (bufferSize < targetJitterBytes) return@synchronized null
                    isPlaying = true
                    consecutiveEmpty = 0
                    Log.d(TAG, "Started playing, buffer=${bufferSize / NetworkConstants.BYTES_PER_MS}ms")
                }

                if (jitterBuffer.isEmpty) {
                    consecutiveEmpty++
                    if (consecutiveEmpty >= NetworkConstants.SILENCE_INSERT_THRESHOLD) {
                        isPlaying = false
                        consecutiveEmpty = 0
                        Log.d(TAG, "Too many underruns, resetting playback")
                        return@synchronized null
                    }
                    return@synchronized silenceFrame.copyOf()
                }
                consecutiveEmpty = 0
                jitterBuffer.poll()
            }

            if (data == null) {
                delay(PLAYBACK_DELAY_MS)
                continue
            }

            var offset = 0
            while (offset < data.size) {
                val written = audioPlayer.write(data, offset, data.size - offset)
                when {
                    written > 0 -> offset += written
                    written < 0 -> {
                        Log.e(TAG, "AudioTrack write error: $written")
                        break
                    }
                    else -> delay(1) // Buffer full, yield
                }
            }

            playCount++
            if (playCount % STATS_INTERVAL_PACKETS == 0) {
                val latency = jitterBuffer.sizeBytes / NetworkConstants.BYTES_PER_MS
                Log.d(TAG, "Played $playCount chunks, latency=${latency}ms")
            }
        }
        Log.d(TAG, "Playback loop ended")
    }
}
