using System;

namespace Manimal.QuestBriefingAPI
{
    // Short, band-limited squelch bursts. Generated locally; no recording edits or assets needed.
    internal static class RadioCueSamples
    {
        internal const int SampleRate = 44100;

        internal static float[] Create(bool opening)
        {
            var samples = new float[(int)(SampleRate * (opening ? 0.20 : 0.14))];
            var random = new Random(opening ? 1701 : 1702);
            double low = 0, high = 0;
            double lowAlpha = 1 - Math.Exp(-2 * Math.PI * 3200 / SampleRate);
            double highAlpha = 1 - Math.Exp(-2 * Math.PI * 500 / SampleRate);
            for (int i = 0; i < samples.Length; i++)
            {
                double t = (double)i / SampleRate;
                double noise = random.NextDouble() * 2 - 1;
                low += lowAlpha * (noise - low);
                high += highAlpha * (low - high);
                double envelope = Math.Min(1, t / 0.002)
                    * Math.Min(1, (samples.Length - 1 - i) / (SampleRate * 0.025));
                double burst = (low - high) * 0.8 * Math.Exp(-t * (opening ? 8 : 15));
                double click = 0.3 * Math.Sin(2 * Math.PI * (opening ? 1450 : 950) * t)
                    * Math.Exp(-t * 180);
                samples[i] = (float)((burst + click) * envelope);
            }
            return samples;
        }
    }
}
