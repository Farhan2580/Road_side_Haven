using UnityEngine;

namespace RoadsideHaven
{
    /// <summary>
    /// Synthesises every game sound at start-up (the project ships no audio files): engines, tyres,
    /// impacts, horn, footsteps, birds, crickets, city hum, wind, water, doors and UI clicks.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int Rate = 44100;
        private static System.Random rng = new System.Random(1234);
        private static float Noise() => (float)rng.NextDouble() * 2f - 1f;

        private static AudioClip Make(string name, float[] data, bool loop)
        {
            if (loop) data = CrossfadeLoop(data, Rate / 8);
            Normalise(data, .85f);
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float[] CrossfadeLoop(float[] data, int fade)
        {
            fade = Mathf.Min(fade, data.Length / 4);
            var result = new float[data.Length - fade];
            System.Array.Copy(data, result, result.Length);
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; result[i] = data[i] * k + data[data.Length - fade + i] * (1 - k); }
            return result;
        }

        private static void Normalise(float[] data, float peak)
        {
            float max = 1e-5f;
            foreach (var v in data) max = Mathf.Max(max, Mathf.Abs(v));
            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        public static float Rms(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            double sum = 0; foreach (var v in data) sum += v * v;
            return Mathf.Sqrt((float)(sum / Mathf.Max(1, data.Length)));
        }

        /// <summary>Combustion engine idle loop at 50 Hz firing; pitch it up with AudioSource.pitch for revs.</summary>
        public static AudioClip Engine(int seed)
        {
            rng = new System.Random(seed);
            int n = Rate; var d = new float[n];
            float f0 = 48 + seed % 5 * 3, low = 0;
            var phase = new float[10]; for (int k = 0; k < 10; k++) phase[k] = (float)rng.NextDouble() * 6.28f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, s = 0;
                for (int k = 1; k <= 9; k++) s += Mathf.Sin(2 * Mathf.PI * f0 * k * t + phase[k]) / Mathf.Pow(k, .85f);
                // Firing pulses give the engine its throb.
                float pulse = Mathf.Pow(Mathf.Max(0, Mathf.Sin(2 * Mathf.PI * f0 * .5f * t)), 6);
                low += (Noise() - low) * .05f;
                d[i] = s * (.6f + .4f * pulse) + low * .8f;
            }
            return Make("Engine " + seed, d, true);
        }

        public static AudioClip Skid()
        {
            int n = Rate * 2; var d = new float[n]; float bp = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float x = Noise(); lp += (x - lp) * .35f; bp = lp - bp * .2f;
                float squeal = Mathf.Sin(2 * Mathf.PI * (1750 + 120 * Mathf.Sin(2 * Mathf.PI * 6 * t)) * t);
                d[i] = squeal * .55f + bp * .45f;
            }
            return Make("Tyre skid", d, true);
        }

        public static AudioClip Impact()
        {
            int n = Rate / 2; var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate; lp += (Noise() - lp) * .3f;
                d[i] = Mathf.Sin(2 * Mathf.PI * 68 * t) * Mathf.Exp(-t * 11) + lp * Mathf.Exp(-t * 22) * 1.3f + Noise() * Mathf.Exp(-t * 60) * .5f;
            }
            return Make("Impact", d, false);
        }

        public static AudioClip Horn()
        {
            int n = Rate; var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float a = Mathf.Sin(2 * Mathf.PI * 400 * t), b = Mathf.Sin(2 * Mathf.PI * 500 * t);
                // Soft-clipped twin tone like a car horn.
                d[i] = (float)System.Math.Tanh((a + b) * 1.8f);
            }
            return Make("Horn", d, true);
        }

        public static AudioClip Footstep(bool grass, int seed)
        {
            rng = new System.Random(seed + (grass ? 100 : 200));
            int n = (int)(Rate * (grass ? .22f : .14f)); var d = new float[n]; float lp = 0, hp = 0, prev = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, x = Noise();
                lp += (x - lp) * (grass ? .18f : .5f); hp = x - prev; prev = x;
                if (grass) d[i] = (lp * Mathf.Exp(-t * 24) + hp * .25f * Mathf.Exp(-t * 18) * (rng.NextDouble() < .3 ? 1 : .4f)) * Mathf.Min(1, t * 300);
                else d[i] = hp * Mathf.Exp(-t * 140) * .9f + Mathf.Sin(2 * Mathf.PI * (110 + seed * 7) * t) * Mathf.Exp(-t * 45) * .8f + lp * Mathf.Exp(-t * 60) * .4f;
            }
            return Make((grass ? "Grass" : "Hard") + " step " + seed, d, false);
        }

        public static AudioClip Birds()
        {
            rng = new System.Random(77);
            int n = Rate * 9; var d = new float[n];
            int cursor = 0;
            while (cursor < n)
            {
                cursor += (int)(Rate * (.15f + (float)rng.NextDouble() * .9f));
                int notes = 2 + rng.Next(6); float f = 2400 + (float)rng.NextDouble() * 2400, sweep = ((float)rng.NextDouble() - .4f) * 2600, vol = .3f + (float)rng.NextDouble() * .7f;
                for (int k = 0; k < notes && cursor < n; k++)
                {
                    int len = (int)(Rate * (.04f + (float)rng.NextDouble() * .09f)); float ph = 0;
                    for (int i = 0; i < len && cursor + i < n; i++)
                    {
                        float u = i / (float)len; float freq = f + sweep * u + 300 * Mathf.Sin(u * 40);
                        ph += 2 * Mathf.PI * freq / Rate;
                        d[cursor + i] += Mathf.Sin(ph) * Mathf.Sin(u * Mathf.PI) * vol;
                    }
                    cursor += len + (int)(Rate * .03f * rng.NextDouble());
                }
            }
            return Make("Birds", d, true);
        }

        public static AudioClip Crickets()
        {
            rng = new System.Random(31);
            int n = Rate * 4; var d = new float[n];
            for (int c = 0; c < 4; c++)
            {
                float carrier = 4100 + c * 260, rate = 1.6f + c * .45f, vol = 1f / (1 + c * .6f), offset = (float)rng.NextDouble();
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float chirp = Mathf.Repeat(t * rate + offset, 1f);
                    // A chirp is three or four rapid pulses.
                    float burst = chirp < .18f ? Mathf.Max(0, Mathf.Sin(chirp / .18f * Mathf.PI * 4)) : 0;
                    d[i] += Mathf.Sin(2 * Mathf.PI * carrier * t) * burst * vol;
                }
            }
            return Make("Crickets", d, true);
        }

        public static AudioClip CityHum()
        {
            rng = new System.Random(9);
            int n = Rate * 7; var d = new float[n]; float brown = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                brown = Mathf.Clamp(brown + Noise() * .04f, -1, 1); lp += (brown - lp) * .08f;
                float drone = Mathf.Sin(2 * Mathf.PI * 92 * t + Mathf.Sin(t * .7f) * 3) * .25f + Mathf.Sin(2 * Mathf.PI * 61 * t) * .18f;
                float pass = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * .9f)), 4) * .5f;
                d[i] = lp * 2.2f + drone * (.5f + pass) + Noise() * .03f;
            }
            return Make("City hum", d, true);
        }

        public static AudioClip Wind()
        {
            rng = new System.Random(5);
            int n = Rate * 8; var d = new float[n]; float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float gust = .5f + .5f * Mathf.Sin(t * .8f) * Mathf.Sin(t * .37f + 1);
                lp += (Noise() - lp) * (.02f + gust * .06f); lp2 += (lp - lp2) * .2f;
                d[i] = lp2 * (.4f + gust);
            }
            return Make("Wind", d, true);
        }

        public static AudioClip Water()
        {
            rng = new System.Random(3);
            int n = Rate * 5; var d = new float[n]; float lp = 0, env = 0;
            for (int i = 0; i < n; i++)
            {
                lp += (Noise() - lp) * .25f;
                if (rng.NextDouble() < .0015) env = .6f + (float)rng.NextDouble() * .8f;
                env *= .9993f;
                d[i] = lp * (.35f + env) + (lp - d[Mathf.Max(0, i - 1)]) * .15f;
            }
            return Make("Water", d, true);
        }

        public static AudioClip DoorSwoosh()
        {
            int n = (int)(Rate * .7f); var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = t / .7f;
                lp += (Noise() - lp) * Mathf.Lerp(.04f, .3f, Mathf.Sin(u * Mathf.PI));
                d[i] = lp * Mathf.Sin(u * Mathf.PI) + Mathf.Sin(2 * Mathf.PI * 180 * t) * Mathf.Exp(-t * 30) * .3f;
            }
            return Make("Door swoosh", d, false);
        }

        public static AudioClip Click()
        {
            int n = (int)(Rate * .06f); var d = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; d[i] = Mathf.Sin(2 * Mathf.PI * 1900 * t) * Mathf.Exp(-t * 90) + Noise() * Mathf.Exp(-t * 300) * .3f; }
            return Make("UI click", d, false);
        }

        public static AudioClip Waves()
        {
            rng = new System.Random(21);
            int n = Rate * 10; var d = new float[n]; float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // Two overlapping swells per loop: build, break (bright), then wash back.
                float swell = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI / 5f)), 3) + .5f * Mathf.Pow(Mathf.Max(0, Mathf.Sin((t + 2.3f) * Mathf.PI / 5f)), 4);
                lp += (Noise() - lp) * (.05f + swell * .25f); lp2 += (lp - lp2) * .3f;
                d[i] = lp2 * (.25f + swell);
            }
            return Make("Sea waves", d, true);
        }

        public static AudioClip Gull(int seed)
        {
            rng = new System.Random(300 + seed);
            int n = (int)(Rate * .9f); var d = new float[n]; float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, call = Mathf.Repeat(t, .3f) / .3f;
                float f = Mathf.Lerp(1900, 900, call) + 180 * Mathf.Sin(t * 70);
                ph += 2 * Mathf.PI * f / Rate;
                float env = Mathf.Sin(Mathf.Min(1, call * 1.3f) * Mathf.PI) * (t < .9f ? 1 : 0);
                d[i] = ((float)System.Math.Tanh(Mathf.Sin(ph) * 3) * .7f + Noise() * .15f) * env;
            }
            return Make("Gull " + seed, d, false);
        }

        public static AudioClip Crowd()
        {
            rng = new System.Random(41);
            int n = Rate * 6; var d = new float[n];
            for (int v = 0; v < 14; v++)
            {
                float f = 280 + (float)rng.NextDouble() * 520, rate = 2 + (float)rng.NextDouble() * 4, off = (float)rng.NextDouble() * 6; float lp = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    lp += (Noise() - lp) * .15f;
                    float syllable = Mathf.Max(0, Mathf.Sin((t + off) * rate * Mathf.PI));
                    d[i] += (Mathf.Sin(2 * Mathf.PI * f * t + lp * 4) * .5f + lp) * syllable * .2f;
                }
            }
            return Make("Crowd murmur", d, true);
        }

        public static AudioClip Cheer()
        {
            rng = new System.Random(42);
            int n = Rate * 3; var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate; lp += (Noise() - lp) * .45f;
                float env = Mathf.Min(1, t * 2.5f) * Mathf.Exp(-Mathf.Max(0, t - .8f) * 1.4f);
                d[i] = (lp + Mathf.Sin(2 * Mathf.PI * (500 + 80 * Mathf.Sin(t * 9)) * t) * .2f) * env;
            }
            return Make("Crowd cheer", d, false);
        }

        public static AudioClip Whistle()
        {
            int n = (int)(Rate * .8f); var d = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; float trill = .5f + .5f * Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * 28)); d[i] = Mathf.Sin(2 * Mathf.PI * (2700 + trill * 250) * t) * Mathf.Min(1, t * 40) * (t < .7f ? 1 : Mathf.Max(0, 1 - (t - .7f) * 10)); }
            return Make("Referee whistle", d, false);
        }

        public static AudioClip Moo()
        {
            int n = (int)(Rate * 1.6f); var d = new float[n]; float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = t / 1.6f;
                float f = 105 + 45 * Mathf.Sin(u * Mathf.PI) - u * 25; ph += 2 * Mathf.PI * f / Rate;
                float s = 0; for (int k = 1; k <= 8; k++) s += Mathf.Sin(ph * k) / k * (k == 3 || k == 4 ? 1.8f : 1);
                d[i] = s * Mathf.Sin(u * Mathf.PI);
            }
            return Make("Cow moo", d, false);
        }

        public static AudioClip Rooster()
        {
            int n = (int)(Rate * 1.8f); var d = new float[n]; float ph = 0;
            float[] notes = { 620, 760, 900, 880, 700 }; float[] lens = { .18f, .22f, .55f, .45f, .3f };
            float start = 0;
            for (int k = 0; k < notes.Length; k++)
            {
                int a = (int)(start * Rate), b = Mathf.Min(n, (int)((start + lens[k]) * Rate));
                for (int i = a; i < b; i++) { float u = (i - a) / (float)(b - a); ph += 2 * Mathf.PI * notes[k] * (1 + .03f * Mathf.Sin(u * 30)) / Rate; d[i] = (float)System.Math.Tanh(Mathf.Sin(ph) * 2.5f + Mathf.Sin(ph * 2) * .6f) * Mathf.Sin(u * Mathf.PI); }
                start += lens[k];
            }
            return Make("Rooster", d, false);
        }

        public static AudioClip Bark(int seed)
        {
            rng = new System.Random(500 + seed);
            int n = (int)(Rate * .7f); var d = new float[n];
            for (int b = 0; b < 2; b++)
            {
                int a = (int)(b * .32f * Rate); float ph = 0, f0 = 420 + seed * 40;
                for (int i = 0; i < Rate * .16f && a + i < n; i++) { float u = i / (Rate * .16f); ph += 2 * Mathf.PI * f0 * (1 - u * .4f) / Rate; d[a + i] += ((float)System.Math.Tanh(Mathf.Sin(ph) * 4) + Noise() * .4f) * Mathf.Sin(u * Mathf.PI); }
            }
            return Make("Dog bark " + seed, d, false);
        }

        public static AudioClip Siren()
        {
            int n = Rate * 4; var d = new float[n]; float ph = 0;
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; float f = 750 + 450 * (.5f + .5f * Mathf.Sin(t * Mathf.PI * .9f)); ph += 2 * Mathf.PI * f / Rate; d[i] = (float)System.Math.Tanh(Mathf.Sin(ph) * 2) * Mathf.Min(1, t * 2) * Mathf.Min(1, (4 - t) * 2); }
            return Make("Distant siren", d, false);
        }

        public static AudioClip JetRumble()
        {
            rng = new System.Random(61);
            int n = Rate * 5; var d = new float[n]; float lp = 0, brown = 0;
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; brown = Mathf.Clamp(brown + Noise() * .05f, -1, 1); lp += (Noise() - lp) * .3f; d[i] = brown * 1.6f + lp * .35f + Mathf.Sin(2 * Mathf.PI * 2900 * t) * .03f; }
            return Make("Jet rumble", d, true);
        }

        public static AudioClip HelicopterChop()
        {
            rng = new System.Random(62);
            int n = Rate * 2; var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate; lp += (Noise() - lp) * .2f;
                float blade = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI * 2 * 17)), 10);
                d[i] = lp * (.25f + blade * 1.6f) + Mathf.Sin(2 * Mathf.PI * 34 * t) * blade * .6f + Mathf.Sin(2 * Mathf.PI * 1100 * t) * .04f;
            }
            return Make("Helicopter", d, true);
        }

        /// <summary>Gentle eight-second lo-fi loop for shop interiors.</summary>
        public static AudioClip ShopMusic()
        {
            int n = Rate * 8; var d = new float[n];
            float[][] chords = { new[] { 261.6f, 329.6f, 392f }, new[] { 220f, 261.6f, 329.6f }, new[] { 174.6f, 220f, 261.6f }, new[] { 196f, 246.9f, 293.7f } };
            rng = new System.Random(71);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate; int bar = (int)(t / 2) % 4; float inBar = Mathf.Repeat(t, 2);
                float pad = 0; foreach (var f in chords[bar]) pad += Mathf.Sin(2 * Mathf.PI * f * t) * .3f + Mathf.Sin(2 * Mathf.PI * f * 2 * t) * .05f;
                pad *= .6f + .4f * Mathf.Exp(-inBar * 1.5f);
                float beat = Mathf.Repeat(t, .5f);
                float kick = Mathf.Sin(2 * Mathf.PI * (60 + 60 * Mathf.Exp(-beat * 30)) * beat) * Mathf.Exp(-beat * 14) * (Mathf.Repeat(t, 1) < .5f ? 1 : .0f);
                float hat = Noise() * Mathf.Exp(-Mathf.Repeat(t + .25f, .5f) * 60) * .25f;
                float melody = Mathf.Sin(2 * Mathf.PI * chords[bar][(int)(inBar * 2) % 3] * 2 * t) * Mathf.Exp(-Mathf.Repeat(inBar, .5f) * 5) * .25f;
                d[i] = pad * .5f + kick * .7f + hat + melody;
            }
            return Make("Shop music", d, true);
        }

        public static AudioClip RoomTone()
        {
            rng = new System.Random(72);
            int n = Rate * 4; var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; lp += (Noise() - lp) * .03f; d[i] = lp * .8f + Mathf.Sin(2 * Mathf.PI * 50 * t) * .08f + Mathf.Sin(2 * Mathf.PI * 120 * t) * .04f; }
            return Make("Room tone", d, true);
        }

        public static AudioClip Sparks()
        {
            int n = (int)(Rate * .35f); var d = new float[n];
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; d[i] = Noise() * Mathf.Exp(-t * 14) * (rng.NextDouble() < .3 ? 1 : .3f); }
            return Make("Metal scrape", d, false);
        }

        public static AudioClip Splash()
        {
            int n = (int)(Rate * .5f); var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; lp += (Noise() - lp) * .4f; d[i] = lp * Mathf.Exp(-t * 9) + Noise() * Mathf.Exp(-t * 40) * .4f; }
            return Make("Splash", d, false);
        }
    }
}
