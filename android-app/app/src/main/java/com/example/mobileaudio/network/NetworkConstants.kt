package com.example.mobileaudio.network

/**
 * Centralized network and audio configuration constants.
 */
object NetworkConstants {
    const val UDP_PORT = 5000
    const val DISCOVERY_PORT = 5001

    const val DEVICE_TYPE_PC = "MobileAudioPC"
    const val DEVICE_TYPE_PHONE = "MobileAudioPhone"

    const val SAMPLE_RATE = 48_000
    const val CHANNELS = 2
    const val BITS_PER_SAMPLE = 16
    const val BYTES_PER_MS = SAMPLE_RATE * CHANNELS * (BITS_PER_SAMPLE / 8) / 1000

    const val PACKET_HEADER_BYTES = 4 // UInt32 sequence number
    const val SOCKET_TIMEOUT_MS = 3000
    const val BROADCAST_INTERVAL_MS = 2000L
    const val STALE_DEVICE_TIMEOUT_MS = 10_000L
    const val MAX_LATENCY_MS = 100
    const val SILENCE_INSERT_THRESHOLD = 20
}

