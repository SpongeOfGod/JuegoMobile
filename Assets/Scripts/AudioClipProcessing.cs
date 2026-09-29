using System;
using UnityEngine;

namespace Gluttony
{
    internal static class AudioClipProcessing
    {
        public static AudioClip NormalizeVolume(AudioClip clip, float targetRms, float peakCeiling)
        {
            if (clip == null)
                return clip;
            if (clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0))
                return clip;
            int windowFrames = Mathf.Max(1, clip.frequency / 50);
            float loudestRms = 0f;
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            for (int start = 0; start < clip.samples; start += windowFrames)
                loudestRms = Mathf.Max(loudestRms, CalculateRms(data, clip.channels, start, Mathf.Min(windowFrames, clip.samples - start)));
            if (loudestRms < 0.00001f)
                return clip;
            double energy = 0.0;
            int activeFrames = 0;
            for (int start = 0; start < clip.samples; start += windowFrames)
            {
                int length = Mathf.Min(windowFrames, clip.samples - start);
                float rms = CalculateRms(data, clip.channels, start, length);
                if (rms < loudestRms * 0.1f)
                    continue;
                energy += rms * rms * length;
                activeFrames += length;
            }
            float activeRms = (float)Math.Sqrt(energy / Math.Max(1, activeFrames));
            float gain = Mathf.Min(targetRms / Mathf.Max(activeRms, 0.00001f), peakCeiling / Mathf.Max(peak, 0.00001f));
            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;
            var copy = AudioClip.Create(clip.name + "_balanced", clip.samples, clip.channels, clip.frequency, false);
            copy.SetData(data, 0);
            return copy;
        }

        public static AudioClip SmoothEnding(AudioClip clip, float fadeSeconds)
        {
            if (clip.loadState != AudioDataLoadState.Loaded)
                clip.LoadAudioData();
            if (clip.loadState != AudioDataLoadState.Loaded)
                return clip;
            int channels = clip.channels;
            int frames = clip.samples;
            int sampleRate = clip.frequency;
            var source = new float[frames * channels];
            if (!clip.GetData(source, 0))
                return clip;

            int windowFrames = Mathf.Max(1, sampleRate / 50);
            int edgeFrames = Mathf.Max(1, sampleRate / 500);
            float loudestRms = 0.0001f;
            for (int start = 0; start + windowFrames <= frames; start += windowFrames)
                loudestRms = Mathf.Max(loudestRms, CalculateRms(source, channels, start, windowFrames));
            int last = Mathf.Min(windowFrames, frames);
            float endingRms = CalculateRms(source, channels, frames - last, last);
            bool hasAbruptEnding = frames > edgeFrames && CalculateRms(source, channels, frames - edgeFrames, edgeFrames) > endingRms * 0.6f;
            bool needsTail = hasAbruptEnding && frames > windowFrames * 4 && endingRms / loudestRms > 0.05f;

            int grainFrames = sampleRate / 25;
            int hopFrames = grainFrames / 2;
            int tailFrames = needsTail ? Mathf.RoundToInt(sampleRate * 0.35f) : 0;
            int totalFrames = frames + tailFrames;
            var data = new float[totalFrames * channels];
            System.Array.Copy(source, data, source.Length);

            if (needsTail)
            {
                var random = new System.Random(frames);
                int firstGrainFrame = Mathf.Max(0, frames - Mathf.RoundToInt(sampleRate * 0.08f) - grainFrames);
                int lastGrainFrame = frames - grainFrames;
                for (int frame = frames - hopFrames; frame < frames; frame++)
                {
                    float windowGain = 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (frame - (frames - hopFrames)) / hopFrames);
                    for (int channel = 0; channel < channels; channel++)
                        data[frame * channels + channel] *= windowGain;
                }
                for (int destinationFrame = frames - hopFrames, grainIndex = 0; destinationFrame < totalFrames; destinationFrame += hopFrames, grainIndex++)
                {
                    int sourceFrame = firstGrainFrame + random.Next(Mathf.Max(1, lastGrainFrame - firstGrainFrame));
                    float gain = Mathf.Exp(-grainIndex * hopFrames / (sampleRate * 0.09f));
                    for (int grainFrame = 0; grainFrame < grainFrames && destinationFrame + grainFrame < totalFrames; grainFrame++)
                    {
                        if (destinationFrame + grainFrame < frames - hopFrames)
                            continue;
                        float windowGain = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * grainFrame / grainFrames);
                        for (int channel = 0; channel < channels; channel++)
                            data[(destinationFrame + grainFrame) * channels + channel] += source[(sourceFrame + grainFrame) * channels + channel] * windowGain * gain;
                    }
                }
            }

            int fadeInFrames = Mathf.Max(1, Mathf.Min(totalFrames / 4, sampleRate / 1000));
            int fadeOutFrames = hasAbruptEnding ? Mathf.Max(1, Mathf.Min(totalFrames / 3, Mathf.RoundToInt(sampleRate * fadeSeconds))) : 0;
            for (int frame = 0; frame < totalFrames; frame++)
            {
                float gain = 1f;
                if (frame < fadeInFrames)
                    gain = frame / (float)fadeInFrames;
                int remainingFrames = totalFrames - 1 - frame;
                if (remainingFrames < fadeOutFrames)
                    gain *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * remainingFrames / fadeOutFrames);
                for (int channel = 0; channel < channels; channel++)
                    data[frame * channels + channel] *= gain;
            }
            var copy = AudioClip.Create(clip.name, totalFrames, channels, sampleRate, false);
            copy.SetData(data, 0);
            return copy;
        }

        private static float CalculateRms(float[] data, int channels, int start, int length)
        {
            double sum = 0;
            for (int frame = start; frame < start + length; frame++)
                for (int channel = 0; channel < channels; channel++)
                {
                    float sample = data[frame * channels + channel];
                    sum += sample * sample;
                }
            return (float)Math.Sqrt(sum / (length * channels));
        }
    }
}
