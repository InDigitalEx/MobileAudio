package com.example.mobileaudio.audio

import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioTrack
import android.util.Log

class AudioPlayer {
    companion object {
        private const val TAG = "AudioPlayer"
        private const val SAMPLE_RATE = 48000
        private const val BYTES_PER_FRAME = 4  // 2 channels * 2 bytes (16-bit)
    }

    private var audioTrack: AudioTrack? = null
    private var bufferSizeInFrames: Int = 0

    fun start() {
        if (audioTrack != null) return
        try {
            val minBuffer = AudioTrack.getMinBufferSize(SAMPLE_RATE, AudioFormat.CHANNEL_OUT_STEREO, AudioFormat.ENCODING_PCM_16BIT)
            // Use a small buffer: 20ms minimum, but at least what the system requires
            val desiredFrames = (SAMPLE_RATE * 20 / 1000).coerceAtLeast(minBuffer / BYTES_PER_FRAME)
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
     * Returns number of bytes that can be written without blocking.
     * Returns -1 if track is not initialized.
     */
    fun availableBufferSize(): Int {
        val track = audioTrack ?: return -1
        val playbackHead = track.playbackHeadPosition
        // This is an approximation; AudioTrack doesn't expose exact buffer fill level
        return bufferSizeInFrames * BYTES_PER_FRAME
    }

    fun write(data: ByteArray, offset: Int, length: Int): Int {
        val track = audioTrack ?: return -1
        return track.write(data, offset, length)
    }

    fun stop() {
        Log.d(TAG, "Stopping AudioTrack")
        try {
            audioTrack?.stop()
        } catch (_: Exception) {}
        audioTrack?.release()
        audioTrack = null
    }
}

