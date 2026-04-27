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

/**
 * Immutable description of a discovered PC device.
 */
data class DiscoveredPc(
    val deviceName: String,
    val ipAddress: String,
    val audioPort: Int,
    val lastSeen: Long = System.currentTimeMillis()
)

/**
 * Listens for and broadcasts device-discovery messages on the local network.
 *
 * Must call [start] before receiving device updates and [stop] when done.
 */
class DiscoveryListener(private val context: Context) {

    companion object {
        private const val TAG = "DiscoveryListener"
    }

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var listenJob: Job? = null
    private var broadcastJob: Job? = null
    private var multicastLock: WifiManager.MulticastLock? = null

    private val discoveredPcs = mutableMapOf<String, DiscoveredPc>()
    private val lock = Any()

    /**
     * Starts discovery. [onDevicesUpdated] is called on the **Main** dispatcher
     * whenever the device list changes.
     */
    fun start(onDevicesUpdated: (List<DiscoveredPc>) -> Unit) {
        stop()

        acquireMulticastLock()

        listenJob = scope.launch {
            var socket: DatagramSocket? = null
            try {
                socket = DatagramSocket(null).apply {
                    reuseAddress = true
                    broadcast = true
                    bind(InetSocketAddress(NetworkConstants.DISCOVERY_PORT))
                }
                Log.d(TAG, "Discovery socket bound to port ${NetworkConstants.DISCOVERY_PORT}")

                broadcastJob = launch { broadcastLoop(socket, onDevicesUpdated) }
                listenLoop(socket, onDevicesUpdated)
            } catch (e: Exception) {
                if (e !is CancellationException) {
                    Log.e(TAG, "Discovery error: ${e.message}", e)
                }
            } finally {
                socket?.close()
            }
        }
    }

    /** Stops discovery and releases all resources. Safe to call multiple times. */
    fun stop() {
        Log.d(TAG, "Stopping discovery")
        scope.coroutineContext.cancelChildren()
        listenJob = null
        broadcastJob = null
        synchronized(lock) { discoveredPcs.clear() }
        releaseMulticastLock()
    }

    private fun listenLoop(
        socket: DatagramSocket,
        onDevicesUpdated: (List<DiscoveredPc>) -> Unit
    ) {
        val buffer = ByteArray(512)
        while (scope.isActive) {
            try {
                val packet = DatagramPacket(buffer, buffer.size)
                socket.receive(packet)
                processPacket(packet, onDevicesUpdated)
            } catch (_: CancellationException) {
                break
            } catch (e: Exception) {
                if (scope.isActive) {
                    Log.w(TAG, "Receive error: ${e.message}")
                }
            }
        }
    }

    private suspend fun broadcastLoop(
        socket: DatagramSocket,
        onDevicesUpdated: (List<DiscoveredPc>) -> Unit
    ) {
        val deviceName = Build.MODEL ?: "Android Device"
        val localIp = getLocalIpAddress()
        val message = "HELLO|${NetworkConstants.DEVICE_TYPE_PHONE}|$deviceName|$localIp|${NetworkConstants.UDP_PORT}"
        val bytes = message.toByteArray(Charset.forName("UTF-8"))

        while (scope.isActive) {
            try {
                val broadcastAddr = getBroadcastAddress()
                val packet = DatagramPacket(bytes, bytes.size, broadcastAddr, NetworkConstants.DISCOVERY_PORT)
                socket.send(packet)
            } catch (e: Exception) {
                Log.w(TAG, "Broadcast failed: ${e.message}")
            }

            cleanStaleDevices()?.let { devices ->
                withContext(Dispatchers.Main) { onDevicesUpdated(devices) }
            }

            delay(NetworkConstants.BROADCAST_INTERVAL_MS)
        }
    }

    private fun processPacket(
        packet: DatagramPacket,
        onDevicesUpdated: (List<DiscoveredPc>) -> Unit
    ) {
        val message = String(packet.data, 0, packet.length, Charset.forName("UTF-8")).trim()
        Log.d(TAG, "Received: $message from ${packet.address}")

        if (!message.startsWith("HELLO|")) return
        val parts = message.split("|")
        if (parts.size < 5) return

        val deviceType = parts[1]
        if (deviceType == NetworkConstants.DEVICE_TYPE_PHONE) return // Ignore other phones

        val deviceName = parts[2]
        val ip = parts[3]
        val port = parts[4].toIntOrNull() ?: NetworkConstants.UDP_PORT

        val updatedList = synchronized(lock) {
            discoveredPcs[ip] = DiscoveredPc(deviceName, ip, port)
            discoveredPcs.values.sortedBy { it.deviceName }
        }

        scope.launch(Dispatchers.Main) { onDevicesUpdated(updatedList) }
    }

    /** Removes stale devices and returns the updated list if anything was removed. */
    private fun cleanStaleDevices(): List<DiscoveredPc>? {
        val cutoff = System.currentTimeMillis() - NetworkConstants.STALE_DEVICE_TIMEOUT_MS
        synchronized(lock) {
            val staleKeys = discoveredPcs.filterValues { it.lastSeen < cutoff }.keys
            if (staleKeys.isEmpty()) return null
            staleKeys.forEach { discoveredPcs.remove(it) }
            return discoveredPcs.values.toList().sortedBy { it.deviceName }
        }
    }

    private fun acquireMulticastLock() {
        val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
        multicastLock = wifiManager?.createMulticastLock("MobileAudioDiscovery")?.apply {
            setReferenceCounted(false)
            acquire()
        }
        Log.d(TAG, "Multicast lock acquired")
    }

    private fun releaseMulticastLock() {
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
            Log.w(TAG, "Failed to get local IP: ${e.message}")
            "127.0.0.1"
        }
    }

    private fun getBroadcastAddress(): InetAddress {
        return try {
            val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
            val dhcp = wifiManager.dhcpInfo
            val broadcast = (dhcp.ipAddress and dhcp.netmask) or (dhcp.netmask.inv())
            val quads = ByteArray(4) { k -> (broadcast shr k * 8 and 0xFF).toByte() }
            InetAddress.getByAddress(quads)
        } catch (e: Exception) {
            Log.w(TAG, "Failed to get broadcast address: ${e.message}")
            InetAddress.getByName("255.255.255.255")
        }
    }
}

