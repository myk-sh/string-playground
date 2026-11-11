using System;
using UnityEngine;

namespace StringLab
{
    [Serializable]
    public class StringSettings
    {
        public int material, room = 1;
        public float length = .65f, diameter = .33f, tension = 70f;
        public float position = .22f, strength = .65f, damping = 1f;
        public float temperature = 20f, humidity = 50f;
        public StringSettings Copy() => (StringSettings)MemberwiseClone();
    }

    // Homogeneous circular strings with fixed endpoints. Material losses and
    // environmental coefficients are illustrative, not measured instrument data.
    public static class StringPhysics
    {
        public struct Material
        {
            public string name;
            public double density, young, loss, thermal, moisture;
            public Material(string n, double rho, double e, double l, double a, double h)
            { name = n; density = rho; young = e; loss = l; thermal = a; moisture = h; }
        }
        public static readonly Material[] Materials = {
            new Material("Steel", 7850, 200e9, .38, 12e-6, 0),
            new Material("Nylon", 1140, 2.5e9, .95, 80e-6, 12e-6),
            new Material("Gut", 1300, 3e9, 1.25, 40e-6, 22e-6),
            new Material("Brass", 8500, 100e9, .65, 19e-6, 0)
        };
        public static double Area(StringSettings s) => Math.PI * Math.Pow(s.diameter * .001, 2) / 4;
        public static double EnvironmentOffset(StringSettings s)
        {
            var m = Materials[s.material];
            return -m.young * Area(s) * (m.thermal * (s.temperature - 20) + m.moisture * (s.humidity - 50));
        }
        public static double RawTension(StringSettings s) => s.tension + EnvironmentOffset(s);
        public static double Tension(StringSettings s) => Math.Max(.2, RawTension(s));
        public static double LinearDensity(StringSettings s) => Materials[s.material].density * Area(s);
        public static double Stiffness(StringSettings s)
        {
            double r = s.diameter * .0005;
            double inertia = Math.PI * Math.Pow(r, 4) / 4;
            return Math.PI * Math.PI * Materials[s.material].young * inertia / (Tension(s) * s.length * s.length);
        }
        public static double Frequency(StringSettings s, int n = 1) => n / (2 * s.length) * Math.Sqrt(Tension(s) / LinearDensity(s)) * Math.Sqrt(1 + Stiffness(s) * n * n);
        public static double Weight(StringSettings s, int n) => Math.Sin(Math.PI * n * s.position) / n * Math.Exp(-n * .035);
        public static double Decay(StringSettings s, int n)
        {
            double humidityLoss = s.material == 1 || s.material == 2 ? 1 + (s.humidity - 50) * .004 : 1;
            return Materials[s.material].loss * s.damping * humidityLoss * (1 + .14 * Math.Pow(n, 1.45));
        }
        public static string Note(double hz)
        {
            double midi = 69 + 12 * Math.Log(hz / 440, 2);
            int nearest = (int)Math.Round(midi);
            string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            return notes[((nearest % 12) + 12) % 12] + (nearest / 12 - 1) + "  " + ((midi - nearest) * 100).ToString("+0;-0;0") + " cents";
        }
        public const int SampleRate = 32000;
        public const int Modes = 48;
        public const float Duration = 8;
        // A modal bridge-velocity approximation. Oscillator recurrences avoid
        // transcendental functions per sample. Modes above Nyquist are omitted.
        public static float[] Render(StringSettings s)
        {
            var data = new float[(int)(SampleRate * Duration)];
            if (RawTension(s) <= 0) return data;
            double sum = 0;
            for (int n = 1; n <= Modes; n++) if (Frequency(s, n) < SampleRate * .45) sum += Math.Abs(Weight(s, n));
            double gain = .65 * s.strength / Math.Max(1, sum);
            for (int n = 1; n <= Modes; n++)
            {
                double frequency = Frequency(s, n);
                if (frequency >= SampleRate * .45) continue;
                double w = 2 * Math.PI * frequency / SampleRate;
                double a = 0, b = Math.Sin(w), coefficient = 2 * Math.Cos(w);
                double envelope = Weight(s, n) * gain;
                double falloff = Math.Exp(-Decay(s, n) / SampleRate);
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] += (float)(a * envelope);
                    double next = coefficient * b - a; a = b; b = next; envelope *= falloff;
                }
            }
            for (int i = 0; i < data.Length; i++)
            {
                // Short attack and release prevent clip-edge clicks.
                double fade = Math.Min(1, i / (SampleRate * .002)) * Math.Min(1, (data.Length - 1 - i) / (SampleRate * .04));
                data[i] *= (float)fade;
            }
            return data;
        }
    }
}
