using System.Globalization;

namespace FUNC.Health
{
    public static class Prometheus
    {
        // Metric values by name from Prometheus text format, as algod's /metrics serves
        // it. Labels are dropped; samples that differ only by label are added together.
        public static Dictionary<string, double> Parse(string text)
        {
            Dictionary<string, double> metrics = [];
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int nameEnd = line.IndexOfAny(['{', ' ']);
                if (nameEnd <= 0) continue;
                // The value follows the labels, which may themselves contain spaces.
                int valueStart = line[nameEnd] == '{' ? line.LastIndexOf('}') + 1 : nameEnd;
                if (valueStart <= 0) continue;
                string[] rest = line[valueStart..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (rest.Length == 0 || !double.TryParse(rest[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) continue;
                if (double.IsNaN(value) || double.IsInfinity(value)) continue;
                string name = line[..nameEnd];
                metrics[name] = metrics.GetValueOrDefault(name) + value;
            }
            return metrics;
        }
    }

    // Turns ever-growing counters into the amount added since the previous sample.
    public sealed class CounterDeltas
    {
        private readonly Dictionary<string, double> _previous = [];

        // Zero for the first sample. A counter that went down was reset by a node
        // restart, so its whole value is new.
        public double Next(string name, double current)
        {
            bool seen = _previous.TryGetValue(name, out double previous);
            _previous[name] = current;
            if (!seen) return 0;
            return current >= previous ? current - previous : current;
        }
    }
}
