package com.example.mobileaudio.network

import android.annotation.SuppressLint
import android.content.Context
import android.net.wifi.WifiManager
import android.os.Build
import android.util.Log
import kotlinx.coroutines.*
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.InetSocketAddress
import java.nio.charset.Charset
import java.util.concurrent.ConcurrentHashMap

data class DiscoveredPc(
    val deviceName: String,
    val ipAddress: String,
    val audioPort: Int,
    val lastSeen: Long = System.currentTimeMillis()
)

class DiscoveryListener(private val context: Context) {
    companion object {
        private const val TAG = "DiscoveryListener"
        private const val DISCOVERY_PORT = 5001
        private const val DEVICE_TYPE = "MobileAudioPhone"
        private const val STALE_TIMEOUT_MS = 10000L // 10 seconds
    }

    private var socket: DatagramSocket? = null
    private var listenJob: Job? = null
    private var broadcastJob: Job? = null
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private var multicastLock: WifiManager.MulticastLock? = null

    private val discoveredPcs = ConcurrentHashMap<String, DiscoveredPc>()

    fun start(onDevicesUpdated: (List<DiscoveredPc>) -> Unit) {
        stop()

        // Acquire multicast lock for reliable broadcast reception
        val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
        multicastLock = wifiManager.createMulticastLock("MobileAudioDiscovery").apply {
            setReferenceCounted(false)
            acquire()
        }
        Log.d(TAG, "Multicast lock acquired")

        listenJob = scope.launch {
            try {
                socket = DatagramSocket(null).apply {
                    reuseAddress = true
                    bind(InetSocketAddress(DISCOVERY_PORT))
                    broadcast = true
                }
                Log.d(TAG, "Discovery socket bound to port $DISCOVERY_PORT")

                // Start broadcast loop
                broadcastJob = launch { broadcastLoop() }

                val buffer = ByteArray(512)
                while (isActive) {
                    try {
                        val packet = DatagramPacket(buffer, buffer.size)
                        socket?.receive(packet)
                        processPacket(packet, onDevicesUpdated)
                    } catch (e: Exception) {
                        if (!isActive) break
                    }
                }
            } catch (e: Exception) {
                Log.e(TAG, "Discovery error: ${e.message}")
                e.printStackTrace()
            }
        }
    }

    private fun broadcastLoop() {
        val deviceName = Build.MODEL ?: "Android Device"
        val localIp = getLocalIpAddress()
        val message = "HELLO|$DEVICE_TYPE|$deviceName|$localIp|5000"
        val bytes = message.toByteArray(Charset.forName("UTF-8"))

        while (scope.isActive) {
            try {
                val broadcastAddr = getBroadcastAddress()
                val packet = DatagramPacket(bytes, bytes.size, broadcastAddr, DISCOVERY_PORT)
                socket?.send(packet)
                Log.d(TAG, "Broadcast sent: $message")
            } catch (e: Exception) {
                Log.w(TAG, "Broadcast failed: ${e.message}")
            }

            // Clean stale devices
            val cutoff = System.currentTimeMillis() - STALE_TIMEOUT_MS
            val stale = discoveredPcs.filterValues { it.lastSeen < cutoff }.keys
            if (stale.isNotEmpty()) {
                stale.forEach { discoveredPcs.remove(it) }
                CoroutineScope(Dispatchers.Main).launch {
                    onDevicesUpdated?.invoke(discoveredPcs.values.toList().sortedBy { it.deviceName })
                }
            }

            Thread.sleep(2000)
        }
    }

    private var onDevicesUpdated: ((List<DiscoveredPc>) -> Unit)? = null

    private fun processPacket(packet: DatagramPacket, callback: (List<DiscoveredPc>) -> Unit) {
        onDevicesUpdated = callback
        val message = String(packet.data, 0, packet.length, Charset.forName("UTF-8")).trim()
        Log.d(TAG, "Received: $message from ${packet.address}")

        if (!message.startsWith("HELLO|")) return

        val parts = message.split("|")
        if (parts.size < 5) return

        val deviceType = parts[1]
        val deviceName = parts[2]
        val ip = parts[3]
        val port = parts[4].toIntOrNull() ?: 5000

        // Ignore other phones
        if (deviceType == DEVICE_TYPE) return

        val pc = DiscoveredPc(deviceName, ip, port)
        discoveredPcs[ip] = pc

        CoroutineScope(Dispatchers.Main).launch {
            callback(discoveredPcs.values.toList().sortedBy { it.deviceName })
        }
    }

    fun stop() {
        Log.d(TAG, "Stopping discovery")
        listenJob?.cancel()
        broadcastJob?.cancel()
        socket?.close()
        discoveredPcs.clear()
        multicastLock?.let {
            if (it.isHeld) it.release()
            Log.d(TAG, "Multicast lock released")
        }
        multicastLock = null
    }

    @SuppressLint("DefaultLocale")
    private fun getLocalIpAddress(): String {
        return try {
            val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
            val ipInt = wifiManager.connectionInfo.ipAddress
            String.format(
                "%d.%d.%d.%d",
                ipInt and 0xff,
                ipInt shr 8 and 0xff,
                ipInt shr 16 and 0xff,
                ipInt shr 24 and 0xff
            )
        } catch (e: Exception) {
            "127.0.0.1"
        }
    }

    private fun getBroadcastAddress(): InetAddress {
        return try {
            val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
            val dhcp = wifiManager.dhcpInfo
            val broadcast = (dhcp.ipAddress and dhcp.netmask) or (dhcp.netmask.inv())
            val quads = ByteArray(4)
            for (k in 0..3) {
                quads[k] = (broadcast shr k * 8 and 0xFF).toByte()
            }
            InetAddress.getByAddress(quads)
        } catch (e: Exception) {
            InetAddress.getByName("255.255.255.255")
        }
    }
}

