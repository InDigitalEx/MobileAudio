package com.example.mobileaudio.network

/**
 * Immutable snapshot of audio streaming statistics.
 */
data class AudioStats(
    val packetsReceived: Int = 0,
    val packetsLost: Int = 0,
    val latencyMs: Int = 0
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

