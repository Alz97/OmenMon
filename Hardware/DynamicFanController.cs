using System;
using OmenMon.Hardware.Bios;
using OmenMon.Library;

namespace OmenMon.Hardware.Platform {
    public class DynamicFanController {
        private Platform platform;
        private DynamicFanCurve curve;
        private Action<FanProgram.Severity, string> callback;

        public bool IsEnabled { get; private set; }
        public bool IsSuspended { get; private set; }
        public bool IsAlternate { get; private set; }

        private BiosData.FanMode? lastFanMode;
        private BiosData.GpuPowerData? lastGpuPower;

        public DynamicFanController(Platform platform, DynamicFanCurve curve,
                                     Action<FanProgram.Severity, string> callback) {
            this.platform = platform;
            this.curve = curve;
            this.callback = callback;
        }

        public string GetName() => "Curva Dinamica";

        public void Start(bool isAlternate = false) {
            if (IsEnabled) return;

            if (Config.FanLevelNeedManual)
                platform.Fans.SetManual(true);

            IsAlternate = isAlternate;
            IsEnabled = true;
            IsSuspended = false;

            lastFanMode = platform.Fans.GetMode();
            lastGpuPower = platform.System.GetGpuPower();

            // Estendi il countdown iniziale se configurato
            if (Config.FanCountdownExtendInterval > 0)
                platform.Fans.SetCountdown(Config.FanCountdownExtendInterval);

            callback?.Invoke(FanProgram.Severity.Notice, "Controllo dinamico avviato");
        }

        public void Stop() {
            if (!IsEnabled) return;

            platform.Fans.SetLevels(new byte[] { byte.MaxValue, byte.MaxValue });
            if (Config.FanLevelNeedManual)
                platform.Fans.SetManual(false);

            if (lastFanMode.HasValue)
                platform.Fans.SetMode(lastFanMode.Value);
            if (lastGpuPower.HasValue)
                platform.System.SetGpuPower(lastGpuPower.Value);

            IsEnabled = false;
            IsSuspended = false;

            callback?.Invoke(FanProgram.Severity.Notice, "Controllo dinamico terminato");
        }

        public void Suspend() {
            if (!IsEnabled || IsSuspended) return;
            IsSuspended = true;

            platform.Fans.SetLevels(new byte[] { byte.MaxValue, byte.MaxValue });
            if (Config.FanLevelNeedManual)
                platform.Fans.SetManual(false);
            platform.Fans.SetMode(lastFanMode ?? BiosData.FanMode.Default);

            callback?.Invoke(FanProgram.Severity.Notice, "Controllo dinamico sospeso");
        }

        public void Resume() {
            if (!IsEnabled || !IsSuspended) return;
            IsSuspended = false;

            if (Config.FanLevelNeedManual)
                platform.Fans.SetManual(true);

            // Estendi il countdown dopo la ripresa
            if (Config.FanCountdownExtendInterval > 0)
                platform.Fans.SetCountdown(Config.FanCountdownExtendInterval);

            Update();

            callback?.Invoke(FanProgram.Severity.Notice, "Controllo dinamico ripristinato");
        }

        public void Update() {
            if (!IsEnabled || IsSuspended) return;

            byte maxTemp = platform.GetMaxTemperature(true);
            var (cpu, gpu) = curve.GetFanSpeeds(maxTemp);

            platform.Fans.SetLevels(new byte[] { cpu, gpu });

            // Estendi il countdown per evitare che l'EC riprenda il controllo automatico
            if (Config.FanCountdownExtendInterval > 0)
                platform.Fans.SetCountdown(Config.FanCountdownExtendInterval);

            callback?.Invoke(FanProgram.Severity.Verbose,
                $"Temp: {maxTemp}°C -> Ventole: CPU={cpu}%, GPU={gpu}%");
        }

        // Permette di cambiare curva a caldo (es. quando si passa da AC a batteria)
        public void SetCurve(DynamicFanCurve newCurve) {
            curve = newCurve;
        }
    }
}
