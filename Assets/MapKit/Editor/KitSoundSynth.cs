using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// The sounds the kit's examples use, made here rather than shipped: the kit carries no sound files, and
    /// every map the examples make gets its own copies to change. Short, mono, 22050 Hz, written as 16-bit
    /// WAV beside the map and imported like any sound an author would bring.
    ///
    /// Each one is a few lines of arithmetic over time: tones with envelopes, filtered noise, a phase that
    /// sweeps. Seeded, so the same example always gets the same files.
    /// </summary>
    internal static class KitSoundSynth
    {
        public const int Rate = 22050;

        const double Tau = Math.PI * 2d;

        /// <summary>Writes the samples as a WAV file in the folder and imports it. A clip that loops is
        /// kept as PCM: compressed audio can open a gap where the end meets the start.</summary>
        public static AudioClip Save(string folder, string name, float[] samples, bool loops = false)
        {
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.wav";
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                int count = samples.Length;
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + count * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);   // PCM
                writer.Write((short)1);   // mono
                writer.Write(Rate);
                writer.Write(Rate * 2);   // bytes a second
                writer.Write((short)2);   // bytes a sample
                writer.Write((short)16);  // bits a sample
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(count * 2);
                foreach (float sample in samples)
                    writer.Write((short)Mathf.Clamp(sample * 32767f, -32768f, 32767f));
            }

            AssetDatabase.ImportAsset(path);
            if (loops && AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ------------------------------------------------------------------ building blocks

        static float[] New(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        static float Sine(double cycles) => (float)Math.Sin(cycles * Tau);

        static float Square(double cycles, double duty = 0.5d) => cycles - Math.Floor(cycles) < duty ? 1f : -1f;

        static float Triangle(double cycles)
        {
            double f = cycles + 0.25d - Math.Floor(cycles + 0.25d);
            return (float)(1d - 4d * Math.Abs(f - 0.5d));
        }

        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        /// <summary>The coefficient of a one-pole low-pass at this cutoff.</summary>
        static float LowPass(float cutoff) => 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);

        /// <summary>A quick rise at the start and a quick fall at the end, so no clip starts or stops with a
        /// click.</summary>
        static float Edges(float time, float length, float attack = 0.005f, float release = 0.02f) =>
            Mathf.Min(1f, time / attack) * Mathf.Clamp01((length - time) / release);

        /// <summary>Scales the clip so its loudest sample sits at this level.</summary>
        static float[] Peak(float[] samples, float level)
        {
            float max = 0f;
            foreach (float s in samples)
                max = Mathf.Max(max, Mathf.Abs(s));
            if (max > 1e-5f)
            {
                float scale = level / max;
                for (int i = 0; i < samples.Length; i++)
                    samples[i] *= scale;
            }
            return samples;
        }

        /// <summary>A struck bell: a tone and two overtones, dying away.</summary>
        static void Bell(float[] into, float start, float frequency, float amplitude, float decay)
        {
            int first = Mathf.Max(0, Mathf.RoundToInt(start * Rate));
            for (int i = first; i < into.Length; i++)
            {
                float t = (i - first) / (float)Rate;
                float envelope = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * decay);
                if (envelope < 1e-4f && t > 0.1f)
                    break;
                into[i] += amplitude * envelope * (Sine(frequency * t) + 0.45f * Mathf.Exp(-t * 3f) * Sine(frequency * 2f * t)
                    + 0.2f * Sine(frequency * 3f * t));
            }
        }

        // ------------------------------------------------------------------ the sounds

        /// <summary>A motor's hum: 1.5 s, spinning up as it starts and fading as it stops.</summary>
        public static float[] MotorHum()
        {
            const float seconds = 1.5f;
            float[] samples = New(seconds);
            var random = new System.Random(7);
            double phase = 0d;
            float rumble = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)Rate;
                double frequency = 70d + 25d * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, time / (seconds * 0.25f)));
                phase += frequency / Rate;

                float tone = Sine(phase) + 0.5f * Sine(phase * 2d) + 0.25f * Sine(phase * 3d) + 0.12f * Sine(phase * 5d);
                rumble = rumble * 0.92f + ((float)random.NextDouble() * 2f - 1f) * 0.08f;
                float envelope = Mathf.Min(1f, time / 0.08f) * Mathf.Min(1f, (seconds - time) / 0.25f);
                samples[i] = (tone * 0.2f + rumble * 0.8f) * envelope;
            }
            return samples;
        }

        /// <summary>A lift arriving at a floor.</summary>
        public static float[] Ding()
        {
            float[] samples = New(1.1f);
            Bell(samples, 0f, 1318.5f, 1f, 4.5f);
            return Peak(samples, 0.6f);
        }

        /// <summary>No: two rough low tones beating against each other.</summary>
        public static float[] Buzz()
        {
            const float seconds = 0.35f;
            float[] samples = New(seconds);
            float soft = 0f, a = LowPass(2500f);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                float raw = 0.5f * Square(110d * t) + 0.5f * Square(116.5d * t);
                soft += a * (raw - soft);
                samples[i] = soft * Edges(t, seconds, 0.01f, 0.04f);
            }
            return Peak(samples, 0.5f);
        }

        /// <summary>Yes: two quick rising notes.</summary>
        public static float[] Chime()
        {
            float[] samples = New(0.7f);
            Bell(samples, 0f, 659.25f, 0.8f, 7f);
            Bell(samples, 0.11f, 987.77f, 1f, 5f);
            return Peak(samples, 0.55f);
        }

        /// <summary>Done: a major arpeggio, its top note held.</summary>
        public static float[] Fanfare()
        {
            const float seconds = 1.2f;
            float[] samples = New(seconds);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.11f;
                float decay = n == notes.Length - 1 ? 2.2f : 7f;
                int first = Mathf.RoundToInt(start * Rate);
                for (int i = first; i < samples.Length; i++)
                {
                    float t = (i - first) / (float)Rate;
                    float envelope = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * decay) * Edges(i / (float)Rate, seconds);
                    samples[i] += envelope * (Triangle(notes[n] * t) + 0.25f * Square(notes[n] * t, 0.25d));
                }
            }
            return Peak(samples, 0.55f);
        }

        /// <summary>The hunt is on: a tone sweeping up and down, twice.</summary>
        public static float[] Siren()
        {
            const float seconds = 2.4f;
            float[] samples = New(seconds);
            double phase = 0d;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                double frequency = 600d + 350d * (0.5d - 0.5d * Math.Cos(Tau * t / 1.2d));
                phase += frequency / Rate;
                samples[i] = (Sine(phase) + 0.2f * Square(phase)) * Edges(t, seconds, 0.05f, 0.2f);
            }
            return Peak(samples, 0.55f);
        }

        /// <summary>A rush of air: noise through a filter that opens and closes.</summary>
        public static float[] Whoosh()
        {
            const float seconds = 0.7f;
            float[] samples = New(seconds);
            var random = new System.Random(11);
            float filtered = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / seconds;
                float cutoff = 400f + 2600f * Mathf.Sin(Mathf.PI * u);
                filtered += LowPass(cutoff) * (((float)random.NextDouble() * 2f - 1f) - filtered);
                float envelope = Mathf.Sin(Mathf.PI * u);
                samples[i] = filtered * envelope * envelope;
            }
            return Peak(samples, 0.6f);
        }

        /// <summary>A firework: a thump, a burst, a crackle.</summary>
        public static float[] Pop()
        {
            const float seconds = 0.7f;
            float[] samples = New(seconds);
            var random = new System.Random(13);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                float crackle = random.NextDouble() > 0.97d ? noise : 0f;
                samples[i] = (0.6f * Sine(70d * t) * Mathf.Exp(-t * 12f) + 0.5f * noise * Mathf.Exp(-t * 40f)
                    + 0.5f * crackle * Mathf.Exp(-t * 4f)) * Edges(t, seconds, 0.002f, 0.05f);
            }
            return Peak(samples, 0.7f);
        }

        /// <summary>A generator's hum that loops without a seam: every part repeats a whole number of
        /// times in its two seconds.</summary>
        public static float[] GeneratorHum()
        {
            const float seconds = 2f;
            float[] samples = new float[Mathf.RoundToInt(seconds * Rate)];
            for (int i = 0; i < samples.Length; i++)
            {
                double t = i / (double)Rate;
                float tone = Sine(60d * t) + 0.5f * Sine(120d * t) + 0.35f * Sine(180d * t) + 0.15f * Sine(240d * t);
                samples[i] = tone * (1f + 0.15f * Sine(0.5d * t));
            }
            return Peak(samples, 0.4f);
        }

        /// <summary>Four seconds of chiptune at 120 beats a minute: a melody in eighths over a bass in
        /// quarters. The jukebox plays A, then B, then A again, each starting the next when it ends.</summary>
        public static float[] Music(bool second)
        {
            int[] melody = second
                ? new[] { 79, 81, 83, 84, 83, 81, 79, 76, 77, 79, 81, 79, 77, 76, 74, 0 }
                : new[] { 76, 79, 84, 79, 81, 79, 76, 74, 72, 74, 76, 79, 76, 74, 72, 0 };
            int[] bass = second ? new[] { 43, 43, 48, 48, 41, 41, 43, 43 } : new[] { 48, 48, 45, 45, 41, 41, 43, 43 };

            const float seconds = 4f, eighth = 0.25f, quarter = 0.5f;
            float[] samples = new float[Mathf.RoundToInt(seconds * Rate)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;

                int m = Mathf.Min(melody.Length - 1, (int)(t / eighth));
                float mt = t - m * eighth;
                float lead = melody[m] == 0 ? 0f
                    : Square(Midi(melody[m]) * mt, 0.25d) * Edges(mt, eighth, 0.005f, 0.03f) * (0.4f + 0.6f * Mathf.Exp(-mt * 6f));

                int b = Mathf.Min(bass.Length - 1, (int)(t / quarter));
                float bt = t - b * quarter;
                float low = Triangle(Midi(bass[b]) * bt) * Edges(bt, quarter, 0.005f, 0.03f);

                samples[i] = 0.35f * lead + 0.6f * low;
            }
            return Peak(samples, 0.5f);
        }

        /// <summary>An alarm: two tones taking turns.</summary>
        public static float[] Alarm()
        {
            const float seconds = 1.2f;
            float[] samples = New(seconds);
            float soft = 0f, a = LowPass(3000f);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                float frequency = ((int)(t / 0.15f) & 1) == 0 ? 880f : 660f;
                soft += a * (Square(frequency * t) - soft);
                samples[i] = soft * Edges(t, seconds);
            }
            return Peak(samples, 0.5f);
        }

        /// <summary>Something falling into a pit: a heavy splash and a few bubbles.</summary>
        public static float[] Splash()
        {
            const float seconds = 0.9f;
            float[] samples = New(seconds);
            var random = new System.Random(17);
            float filtered = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                filtered += LowPass(300f + 1200f * Mathf.Exp(-t * 5f)) * (((float)random.NextDouble() * 2f - 1f) - filtered);
                float sample = filtered * Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 4f);
                foreach (float bubble in new[] { 0.15f, 0.3f, 0.45f })
                {
                    float bt = t - bubble;
                    if (bt >= 0f && bt < 0.06f)
                        sample += 0.15f * Sine((400f + 8000f * bt) * bt);
                }
                samples[i] = sample * Edges(t, seconds);
            }
            return Peak(samples, 0.6f);
        }

        /// <summary>A heavy door settling shut.</summary>
        public static float[] Clunk()
        {
            const float seconds = 0.3f;
            float[] samples = New(seconds);
            var random = new System.Random(19);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                samples[i] = (0.7f * Sine(65d * t) * Mathf.Exp(-t * 18f)
                    + 0.3f * ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 60f)) * Edges(t, seconds, 0.002f);
            }
            return Peak(samples, 0.6f);
        }

        /// <summary>A switch.</summary>
        public static float[] Click()
        {
            const float seconds = 0.08f;
            float[] samples = New(seconds);
            var random = new System.Random(23);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                samples[i] = (0.5f * ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 120f)
                    + 0.3f * Sine(2200d * t) * Mathf.Exp(-t * 80f)) * Edges(t, seconds, 0.001f, 0.01f);
            }
            return Peak(samples, 0.45f);
        }

        /// <summary>A weight hitting the floor.</summary>
        public static float[] Slam()
        {
            const float seconds = 0.7f;
            float[] samples = New(seconds);
            var random = new System.Random(29);
            float filtered = 0f, a = LowPass(900f);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                filtered += a * (((float)random.NextDouble() * 2f - 1f) - filtered);
                samples[i] = (0.8f * Sine(48d * t) * Mathf.Exp(-t * 8f) + 0.6f * filtered * Mathf.Exp(-t * 10f)) * Edges(t, seconds, 0.002f);
            }
            return Peak(samples, 0.75f);
        }

        /// <summary>A vent breathing fire: low noise swelling, flickering, dying.</summary>
        public static float[] Roar()
        {
            const float seconds = 1.6f;
            float[] samples = New(seconds);
            var random = new System.Random(31);
            float low = 0f, high = 0f, flicker = 0f;
            float aLow = LowPass(600f), aHigh = LowPass(2500f), aFlicker = LowPass(8f);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += aLow * (noise - low);
                high += aHigh * (noise - high);
                flicker += aFlicker * (((float)random.NextDouble() * 2f - 1f) - flicker);
                float envelope = Mathf.Min(1f, t / 0.08f) * Mathf.Clamp01((seconds - t) / 0.4f);
                samples[i] = (low + 0.3f * high) * envelope * (1f + 2f * flicker);
            }
            return Peak(samples, 0.65f);
        }

        /// <summary>Something too hot touched: a hiss.</summary>
        public static float[] Sizzle()
        {
            const float seconds = 0.45f;
            float[] samples = New(seconds);
            var random = new System.Random(37);
            float low = 0f, a = LowPass(3000f);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += a * (noise - low);
                samples[i] = (noise - low) * Mathf.Exp(-t * 6f) * Edges(t, seconds, 0.003f);
            }
            return Peak(samples, 0.45f);
        }

        /// <summary>A public address chime: ding, dong.</summary>
        public static float[] DingDong()
        {
            float[] samples = New(1.5f);
            Bell(samples, 0f, 783.99f, 1f, 3f);
            Bell(samples, 0.55f, 659.25f, 1f, 3f);
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= Edges(i / (float)Rate, 1.5f, 0.004f, 0.1f);
            return Peak(samples, 0.6f);
        }
    }
}
