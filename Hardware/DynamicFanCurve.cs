namespace OmenMon.Hardware.Platform {
    public class FanCurvePoint {
        public float Temperature { get; set; }   // °C
        public byte FanSpeedCpu { get; set; }    // 0–100%
        public byte FanSpeedGpu { get; set; }    // 0–100%
    }
}

using System.Collections.Generic;
using System.Linq;

namespace OmenMon.Hardware.Platform {
    public class DynamicFanCurve {
        private List<FanCurvePoint> points = new List<FanCurvePoint>();

        public IReadOnlyList<FanCurvePoint> Points => points.AsReadOnly();

        public void AddPoint(float temp, byte cpu, byte gpu) {
            points.Add(new FanCurvePoint { Temperature = temp, FanSpeedCpu = cpu, FanSpeedGpu = gpu });
            points.Sort((a, b) => a.Temperature.CompareTo(b.Temperature));
        }

        public void RemovePoint(int index) {
            if (index >= 0 && index < points.Count)
                points.RemoveAt(index);
        }

        public void Clear() => points.Clear();

        // Interpolazione lineare
        public (byte cpu, byte gpu) GetFanSpeeds(float temperature) {
            if (points.Count == 0) return (0, 0);
            if (points.Count == 1) return (points[0].FanSpeedCpu, points[0].FanSpeedGpu);
            if (temperature <= points[0].Temperature) return (points[0].FanSpeedCpu, points[0].FanSpeedGpu);
            if (temperature >= points[^1].Temperature) return (points[^1].FanSpeedCpu, points[^1].FanSpeedGpu);

            for (int i = 0; i < points.Count - 1; i++) {
                if (temperature >= points[i].Temperature && temperature <= points[i + 1].Temperature) {
                    float t = (temperature - points[i].Temperature) / (points[i + 1].Temperature - points[i].Temperature);
                    byte cpu = (byte)(points[i].FanSpeedCpu + t * (points[i + 1].FanSpeedCpu - points[i].FanSpeedCpu));
                    byte gpu = (byte)(points[i].FanSpeedGpu + t * (points[i + 1].FanSpeedGpu - points[i].FanSpeedGpu));
                    return (cpu, gpu);
                }
            }
            return (0, 0);
        }
    }
}
