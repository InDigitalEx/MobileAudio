package com.example.mobileaudio.ui.components

import androidx.compose.animation.core.*
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlin.random.Random

private val ColorLow = Color(0xFF00897B)
private val ColorHigh = Color(0xFF00E5FF)

/**
 * Animated audio visualizer with [barCount] bars.
 *
 * When [isPlaying] is `true`, bars animate randomly to simulate audio activity.
 * When `false`, bars collapse to a minimal idle state.
 */
@Composable
fun AudioVisualizer(
    isPlaying: Boolean,
    modifier: Modifier = Modifier,
    barCount: Int = 32
) {
    val bars = remember { List(barCount) { Animatable(0.1f) } }

    LaunchedEffect(isPlaying) {
        if (isPlaying) {
            while (true) {
                val targetValues = List(barCount) { Random.nextFloat() * 0.8f + 0.1f }
                val jobs = bars.mapIndexed { index, bar ->
                    launch {
                        bar.animateTo(
                            targetValue = targetValues[index],
                            animationSpec = tween(150, easing = FastOutSlowInEasing)
                        )
                    }
                }
                jobs.forEach { it.join() }
                delay(100)
            }
        } else {
            bars.forEach { bar ->
                launch {
                    bar.animateTo(0.1f, tween(300))
                }
            }
        }
    }

    Row(
        modifier = modifier
            .fillMaxWidth()
            .height(80.dp),
        horizontalArrangement = Arrangement.SpaceEvenly,
        verticalAlignment = Alignment.Bottom
    ) {
        bars.forEach { bar ->
            val height = bar.value
            val color = if (height > 0.6f) ColorHigh else ColorLow
            Box(
                modifier = Modifier
                    .width(4.dp)
                    .fillMaxHeight(fraction = height)
                    .clip(RoundedCornerShape(2.dp))
                    .background(color)
            )
        }
    }
}

