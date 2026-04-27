package com.example.mobileaudio.network

/**
 * Immutable snapshot of audio streaming statistics.
 *
 * @property packetsReceived Total packets successfully received.
 * @property packetsLost Total packets detected as lost (by sequence gaps).
 * @property latencyMs Instantaneous jitter-buffer fill level in milliseconds.
 * @property averageLatencyMs Rolling average of [latencyMs] over recent samples.
 */
data class AudioStats(
    val packetsReceived: Int = 0,
    val packetsLost: Int = 0,
    val latencyMs: Int = 0,
    val averageLatencyMs: Int = 0
) {
    val lossPercent: Int
        get() {
            val total = packetsReceived + packetsLost
            return if (total > 0) (packetsLost * 100 / total) else 0
        }

    companion object {
        val EMPTY = AudioStats()
    }
}

