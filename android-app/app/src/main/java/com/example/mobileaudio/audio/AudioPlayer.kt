package com.example.mobileaudio.audio

import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioTrack
import android.util.Log
import com.example.mobileaudio.network.NetworkConstants

/**
 * Low-latency audio playback using [AudioTrack] in streaming mode.
 *
 * Call [start] before writing audio data, and [stop] to release resources.
 */
class AudioPlayer {

    companion object {
        private const val TAG = "AudioPlayer"
        private const val BUFFER_DURATION_MS = 20
    }

    private var audioTrack: AudioTrack? = null

    val isActive: Boolean
        get() = audioTrack?.playState == AudioTrack.PLAYSTATE_PLAYING

    /**
     * Initializes and starts the [AudioTrack] if not already active.
     */
    fun start() {
        if (isActive) return

        val encoding = AudioFormat.ENCODING_PCM_16BIT
        val channelMask = AudioFormat.CHANNEL_OUT_STEREO
        val bytesPerFrame = NetworkConstants.CHANNELS * (NetworkConstants.BITS_PER_SAMPLE / 8)

        val minBufferBytes = AudioTrack.getMinBufferSize(
            NetworkConstants.SAMPLE_RATE, channelMask, encoding
        )
        val desiredFrames = (NetworkConstants.SAMPLE_RATE * BUFFER_DURATION_MS / 1000)
            .coerceAtLeast(minBufferBytes / bytesPerFrame)
        val bufferBytes = desiredFrames * bytesPerFrame

        audioTrack = try {
            AudioTrack.Builder()
                .setAudioAttributes(
                    AudioAttributes.Builder()
                        .setUsage(AudioAttributes.USAGE_MEDIA)
                        .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
                        .build()
                )
                .setAudioFormat(
                    AudioFormat.Builder()
                        .setSampleRate(NetworkConstants.SAMPLE_RATE)
                        .setEncoding(encoding)
                        .setChannelMask(channelMask)
                        .build()
                )
                .setBufferSizeInBytes(bufferBytes)
                .setTransferMode(AudioTrack.MODE_STREAM)
                .setPerformanceMode(AudioTrack.PERFORMANCE_MODE_LOW_LATENCY)
                .build()
                .also { it.play() }
        } catch (e: Exception) {
            Log.e(TAG, "Failed to create AudioTrack: ${e.message}")
            null
        }
    }

    /**
     * Writes PCM data to the active [AudioTrack].
     *
     * @return the total number of bytes written, or `-1` if the track is not initialized.
     */
    fun write(data: ByteArray, offset: Int = 0, length: Int = data.size): Int {
        val track = audioTrack ?: return -1
        return track.write(data, offset, length)
    }

    /**
     * Stops playback and releases the underlying [AudioTrack]. Safe to call multiple times.
     */
    fun stop() {
        val track = audioTrack ?: return
        audioTrack = null
        try {
            track.stop()
        } catch (_: Exception) {
            // Ignore — track may already be in an invalid state.
        }
        track.release()
    }
}

