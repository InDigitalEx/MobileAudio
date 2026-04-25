package com.example.mobileaudio.audio

import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioTrack

class AudioPlayer {
    private var audioTrack: AudioTrack? = null
    private val sampleRate = 48000
    private val channels = AudioFormat.CHANNEL_OUT_STEREO
    private val format = AudioFormat.ENCODING_PCM_16BIT
    private val bufferSize: Int

    init {
        val minBuffer = AudioTrack.getMinBufferSize(sampleRate, channels, format)
        bufferSize = minBuffer.coerceAtLeast(sampleRate * 2 * 2 * 20 / 1000) // 20ms min
    }

    fun start() {
        if (audioTrack != null) return
        audioTrack = AudioTrack.Builder()
            .setAudioAttributes(
                AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_MEDIA)
                    .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
                    .build()
            )
            .setAudioFormat(
                AudioFormat.Builder()
                    .setSampleRate(sampleRate)
                    .setEncoding(format)
                    .setChannelMask(channels)
                    .build()
            )
            .setBufferSizeInBytes(bufferSize)
            .setTransferMode(AudioTrack.MODE_STREAM)
            .build()
        audioTrack?.play()
    }

    fun write(data: ByteArray, offset: Int, length: Int) {
        audioTrack?.write(data, offset, length)
    }

    fun stop() {
        audioTrack?.stop()
        audioTrack?.release()
        audioTrack = null
    }
}

