package com.example.mobileaudio.audio

import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioTrack
import android.util.Log

/**
 * Low-latency PCM audio playback using [AudioTrack] in streaming mode.
 */
class AudioPlayer {
    companion object {
        private const val TAG = "AudioPlayer"
        const val SAMPLE_RATE = 48000
        const val BYTES_PER_FRAME = 4  // 2 channels * 2 bytes (16-bit)
    }

    private var audioTrack: AudioTrack? = null
    private var bufferSizeInFrames: Int = 0

    /**
     * Creates and starts the [AudioTrack]. Uses a ~10 ms buffer
     * (tight but stable on modern devices with [PERFORMANCE_MODE_LOW_LATENCY]).
     */
    fun start() {
        if (audioTrack != null) return
        try {
            val minBuffer = AudioTrack.getMinBufferSize(
                SAMPLE_RATE,
                AudioFormat.CHANNEL_OUT_STEREO,
                AudioFormat.ENCODING_PCM_16BIT
            )

            // ~10 ms, but never below the platform minimum
            val desiredFrames = (SAMPLE_RATE * 10 / 1000).coerceAtLeast(minBuffer / BYTES_PER_FRAME)
            bufferSizeInFrames = desiredFrames

            audioTrack = AudioTrack.Builder()
                .setAudioAttributes(
                    AudioAttributes.Builder()
                        .setUsage(AudioAttributes.USAGE_MEDIA)
                        .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
                        .build()
                )
                .setAudioFormat(
                    AudioFormat.Builder()
                        .setSampleRate(SAMPLE_RATE)
                        .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                        .setChannelMask(AudioFormat.CHANNEL_OUT_STEREO)
                        .build()
                )
                .setBufferSizeInBytes(desiredFrames * BYTES_PER_FRAME)
                .setTransferMode(AudioTrack.MODE_STREAM)
                .setPerformanceMode(AudioTrack.PERFORMANCE_MODE_LOW_LATENCY)
                .build()

            audioTrack?.play()
            Log.d(TAG, "AudioTrack started, buffer=${desiredFrames} frames (${desiredFrames * 1000 / SAMPLE_RATE}ms)")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to start AudioTrack: ${e.message}")
        }
    }

    /**
     * Returns the number of bytes that can be written without blocking,
     * or -1 if the track is not initialized.
     */
    fun availableBufferSize(): Int {
        val track = audioTrack ?: return -1
        // AudioTrack doesn't expose exact fill level; return total buffer size as conservative estimate.
        return bufferSizeInFrames * BYTES_PER_FRAME
    }

    /** Writes [length] bytes from [data] starting at [offset]. Returns bytes written or error code. */
    fun write(data: ByteArray, offset: Int, length: Int): Int {
        val track = audioTrack ?: return -1
        return track.write(data, offset, length)
    }

    /** Stops playback and releases resources. Safe to call multiple times. */
    fun stop() {
        Log.d(TAG, "Stopping AudioTrack")
        try {
            audioTrack?.stop()
        } catch (_: Exception) {
            // Ignore
        }
        audioTrack?.release()
        audioTrack = null
    }
}
