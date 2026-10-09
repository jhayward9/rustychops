namespace Rustychops;

public static class PitchDetector
{
    public static double? Detect(float[] samples, int sampleRate)
    {
        double energy = samples.Sum(value => (double)value * value);
        if (Math.Sqrt(energy / samples.Length) < .005) return null;
        int size = samples.Length / 2, min = sampleRate / 1200, max = Math.Min(size - 1, (int)Math.Ceiling(sampleRate / 60d));
        double[] difference = new double[max + 1];
        double total = 0;
        for (int lag = 1; lag <= max; lag++)
        {
            double sum = 0;
            for (int i = 0; i < size; i++) { double delta = samples[i] - samples[i + lag]; sum += delta * delta; }
            total += sum;
            difference[lag] = total > 0 ? sum * lag / total : 1;
        }
        for (int lag = min; lag < max - 1; lag++)
        {
            if (difference[lag] >= .15) continue;
            while (lag + 1 < max && difference[lag + 1] < difference[lag]) lag++;
            double before = difference[lag - 1], center = difference[lag], after = difference[lag + 1];
            double denominator = 2 * (2 * center - before - after);
            return sampleRate / (lag + (denominator == 0 ? 0 : (after - before) / denominator));
        }
        return null;
    }

    public static (string Name, double Cents) Describe(double frequency, double reference = 440)
    {
        int midi = (int)Math.Round(69 + 12 * Math.Log2(frequency / reference));
        string[] names = ["C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B"];
        return ($"{names[(midi % 12 + 12) % 12]}{midi / 12 - 1}", 1200 * Math.Log2(frequency / (reference * Math.Pow(2, (midi - 69) / 12d))));
    }
}
