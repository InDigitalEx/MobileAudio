package com.example.mobileaudio.audio

import com.example.mobileaudio.network.AudioStats
import com.example.mobileaudio.network.NetworkConstants
import java.util.ArrayDeque

/**
 * Thread-safe jitter buffer for incoming audio packets.
 *
 * Maintains a queue of audio byte arrays and enforces latency bounds.
 */
internal class JitterBuffer {

    private val queue = ArrayDeque<ByteArray>()

    val sizeBytes: Int
        get() = synchronized(queue) { queue.sumOf { it.size } }

    val isEmpty: Boolean
        get() = synchronized(queue) { queue.isEmpty() }

    fun clear() = synchronized(queue) { queue.clear() }

    /**
     * Adds [data] to the queue, dropping oldest packets if the total size
     * would exceed [maxBytes].
     */
    fun offer(data: ByteArray, maxBytes: Int) {
        synchronized(queue) {
            while (queue.isNotEmpty() && sizeBytes + data.size > maxBytes) {
                queue.pollFirst()
            }
            queue.addLast(data)
        }
    }

    /** Removes and returns the oldest packet, or `null` if empty. */
    fun poll(): ByteArray? = synchronized(queue) { queue.pollFirst() }

    /** Computes an [AudioStats] snapshot. */
    fun toStats(packetsReceived: Int, packetsLost: Int): AudioStats {
        return AudioStats(
            packetsReceived = packetsReceived,
            packetsLost = packetsLost,
            latencyMs = sizeBytes / NetworkConstants.BYTES_PER_MS
        )
    }
}

