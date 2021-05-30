﻿/*
 * Copyright(c) 2021 Daniel Kuschny
 * Licensed under the MPL-2.0 license. See https://github.com/CoderLine/alphaTab/blob/develop/LICENSE for full license information.
 */

using System;

namespace BardMusicPlayer.Siren.AlphaTab.Audio.Synth.Util
{
    internal static class SynthHelper
    {
        public static float Timecents2Secs(float timecents) => (float) Math.Pow(2f, timecents / 1200f);

        public static double Timecents2Secs(double timecents) => Math.Pow(2.0, timecents / 1200.0);

        public static float DecibelsToGain(float db) => db > -100f ? (float) Math.Pow(10.0f, db * 0.05f) : 0;

        public static float GainToDecibels(float gain) => gain <= .00001f ? -100f : (float) (20.0 * Math.Log10(gain));

        public static float Cents2Hertz(float cents) => 8.176f * (float) Math.Pow(2.0f, cents / 1200.0f);

        public static byte ClampB(byte value, byte min, byte max)
        {
            if (value <= min) return min;

            if (value >= max) return max;

            return value;
        }

        public static double ClampD(double value, double min, double max)
        {
            if (value <= min) return min;

            if (value >= max) return max;

            return value;
        }

        public static float ClampF(float value, float min, float max)
        {
            if (value <= min) return min;

            if (value >= max) return max;

            return value;
        }
    }
}