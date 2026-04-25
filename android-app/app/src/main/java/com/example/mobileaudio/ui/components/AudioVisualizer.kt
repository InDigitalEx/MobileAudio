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
import kotlin.random.Random

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
                bars.forEach { bar ->
                    launch {
                        bar.animateTo(
                            targetValue = Random.nextFloat() * 0.8f + 0.1f,
                            animationSpec = tween(150, easing = FastOutSlowInEasing)
                        )
                    }
                }
                delay(100)
            }
        } else {
            bars.forEach { bar ->
                bar.animateTo(0.1f, tween(300))
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
            Box(
                modifier = Modifier
                    .width(4.dp)
                    .fillMaxHeight(bar.value)
                    .clip(RoundedCornerShape(2.dp))
                    .background(
                        if (bar.value > 0.6f) Color(0xFF00E5FF)
                        else Color(0xFF00897B)
                    )
            )
        }
    }
}

