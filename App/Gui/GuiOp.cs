  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023-2024 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using OmenMon.External;
using OmenMon.Hardware.Bios;
using OmenMon.Hardware.Ec;
using OmenMon.Hardware.Platform;  // Necessario per DynamicFanController e DynamicFanCurve
using OmenMon.Library;

namespace OmenMon.AppGui {

    // Implements a backend for GUI mode operations
    public class GuiOp {

        // Riferimento al controller dinamico (se attivo)
        private DynamicFanController dynamicController;

        // Riferimento al controller attivo (può essere FanProgram o DynamicFanController)
        private object activeController;

        // Espone il controller attivo in lettura per altre classi (es. GuiTray)
        public object ActiveController => activeController;

        // Sensors class reference
        internal Platform Platform;

        // Fan program class reference (programmi statici)
        internal FanProgram Program;

        // Parent class reference
        private GuiTray Context;

        // Flag to indicate if running on full power
        public bool FullPower { get; private set; }

        // Constructs the operation-running class
        public GuiOp(GuiTray context) {

            // Initialize the parent class reference
            this.Context = context;

            // Initialize the BIOS and the Embedded Controller
            Hw.BiosInit();
            Hw.EcInit();

            // Initialize the hardware platform
            this.Platform = new Platform();

            // Initialize the fan program
            this.Program = new FanProgram(this.Platform, FanProgramCallback);

            // Set the full power flag
            this.FullPower = this.Platform.System.IsFullPower();

        }

        // Avvia la modalità dinamica con una curva specifica
        public void StartDynamicCurve(DynamicFanCurve curve, bool isAlternate = false) {
            // Ferma eventuali programmi attivi
            if (Program != null && Program.IsEnabled)
                Program.Terminate();
            if (dynamicController != null && dynamicController.IsEnabled)
                dynamicController.Stop();

            dynamicController = new DynamicFanController(Platform, curve, FanProgramCallback);
            dynamicController.Start(isAlternate);
            activeController = dynamicController;
        }

        // Ferma la modalità dinamica
        public void StopDynamicCurve() {
            if (dynamicController != null && dynamicController.IsEnabled) {
                dynamicController.Stop();
                activeController = null;
            }
        }

        // Shows the about dialog
        public static void About(string title = "", string text = "") {
            (new GuiFormAbout(title, text)).ShowDialog();
        }

        // Automatically applies the configuration on startup
        public void AutoConfig() {

            // Set whether the application should start automatically with Windows
            Hw.TaskSet(Config.TaskId.Gui, Config.AutoStartup);

            // Apply the default GPU power settings
            this.Platform.System.SetGpuPower(
                new BiosData.GpuPowerData(
                    (BiosData.GpuPowerLevel)
                        Enum.Parse(typeof(BiosData.GpuPowerLevel), Config.GpuPowerDefault)));

            // Avvia il controllo appropriato in base alla configurazione
            if (Config.UseDynamicFanCurve) {
                // Sceglie la curva in base allo stato di alimentazione
                var curve = this.FullPower ? Config.DynamicCurveAC : Config.DynamicCurveBattery;
                StartDynamicCurve(curve, !this.FullPower);
            } else {
                if (this.FullPower)
                    this.Program.Run(Config.FanProgramDefault);
                else
                    this.Program.Run(Config.FanProgramDefaultAlt, true);
            }

            // Update the main form, if visible
            if (Context.FormMain != null && Context.FormMain.Visible)
                Context.FormMain.UpdateFanCtl();

        }

        // Starts the automatic configuration in another thread
        // so as not to increase the application loading time
        public void AutoConfigRun() {

            Thread autoConfig = new Thread(this.AutoConfig);
            autoConfig.IsBackground = true;
            autoConfig.Start();

        }

        // Keeps updating the status as the fan program runs in the background
        public void FanProgramCallback(FanProgram.Severity severity, string message) {

            // Determina il nome del programma attivo (statico o dinamico)
            string progName = "";
            bool isAlt = false;

            if (activeController is FanProgram prog) {
                progName = prog.GetName();
                isAlt = prog.IsAlternate;
            } else if (activeController is DynamicFanController dyn) {
                progName = dyn.GetName();   // Assicurati che DynamicFanController abbia un metodo GetName()
                isAlt = dyn.IsAlternate;    // Assicurati che abbia una proprietà IsAlternate
            } else {
                progName = Program.GetName();
                isAlt = Program.IsAlternate;
            }

            // For important status updates only,
            // show a balloon tray notification
            if (severity == FanProgram.Severity.Important)
                Context.ShowBalloonTip(message);

            // Handle notice-severity messages
            else if (severity == FanProgram.Severity.Notice) {

                // Add a prefix if an alternate fan program
                string name = isAlt ?
                    Config.Locale.Get(Config.L_PROG + "Alt") + " " + progName
                    : progName;

                // If the main form is available, update the status there
                if (Context.FormMain != null && Context.FormMain.Visible)
                    Context.FormMain.UpdateSysMsg(
                        message.Replace(
                            Config.Locale.Get(Config.L_PROG + "SubMax"),
                            Conv.RTF_SUB1 + Config.Locale.Get(Config.L_PROG + "SubMax") + Conv.RTF_SUBSUP0)
                        + ": " + name);

                // Also put it in the tray icon tooltip
                Context.SetNotifyText(
                    Config.Locale.Get(Config.L_PROG) + ": " + name
                    + " @ " + DateTime.Now.ToString(Config.TimestampFormat)
                    + Environment.NewLine + message);

            }

            // Note: Verbose messages are silently ignored in the GUI mode
            // Run OmenMon -Prog <Name> from the command line to see them

        }

        // Launches when the Omen key has been pressed
        public void KeyHandler(Gui.MessageParam lastParam) {

            // If Omen key is set
            // to toggle fan program 
            if (Config.KeyToggleFanProgram) {

                // Show the form on first press
                // if configured to do so and not already shown
                if (Config.KeyToggleFanProgramShowGuiFirst &&
                    (Context.FormMain == null || !Context.FormMain.Visible))
                    Context.ShowFormMain();

                else {

                    // Configured to cycle
                    // through all fan programs
                    if (Config.KeyToggleFanProgramCycleAll) {

                        // Default to the first fan program 
                        string next = Config.FanProgram.Keys[0];

                        // If a program is running,
                        // cycle to the next one, if exists
                        if (this.Program.IsEnabled)
                            try {
                                next = Config.FanProgram.Keys[
                                    Config.FanProgram.IndexOfKey(this.Program.GetName()) + 1];
                            } catch { }

                        // Run the next fan program
                        this.Program.Run(next);

                        // Configured to toggle
                        // default fan program on and off
                    } else {

                        // Terminate a program, if there is one running
                        if (this.Program.IsEnabled)
                            this.Program.Terminate();

                        // Run the default program, if no program running
                        else
                            this.Program.Run(Config.FanProgramDefault);

                    }

                    // Update the main form fan controls
                    // (if main form is being shown)
                    if (Context.FormMain != null && Context.FormMain.Visible)
                        Context.FormMain.UpdateFanCtl();

                    // Otherwise, show a balloon tip notification
                    // unless configured to toggle programs silently
                    else if (!Config.KeyToggleFanProgramSilent)
                        this.FanProgramCallback(
                            FanProgram.Severity.Important,
                            this.Program.IsEnabled ?
                                Config.Locale.Get(Config.L_PROG) + ": " + this.Program.GetName()
                                : Config.Locale.Get(Config.L_PROG + "End"));

                }

                // If Omen key action is set
                // to trigger a custom action
            } else if (Config.KeyCustomActionEnabled) {

                // Launch the action
                Process customAction = new Process();
                customAction.StartInfo.FileName = Config.KeyCustomActionExecCmd;
                customAction.StartInfo.Arguments = Config.KeyCustomActionExecArgs;
                customAction.StartInfo.UseShellExecute = false; // Required for environment change
                customAction.StartInfo.WindowStyle = Config.KeyCustomActionMinimized ?
                    ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal;
                customAction.Start();

            } else {

                // Just toggle the main form
                Context.ToggleFormMain();

            }

        }

        // Responds to power-mode status change events
        public void PowerChange() {

            // Only if automatic config is enabled and the power state actually changed
            if (Config.AutoConfig && this.FullPower != this.Platform.System.IsFullPower()) {

                // Toggle the power state
                this.FullPower = !this.FullPower;

                if (Config.UseDynamicFanCurve && activeController is DynamicFanController dynCtrl) {
                    // Cambia curva in base all'alimentazione
                    var newCurve = this.FullPower ? Config.DynamicCurveAC : Config.DynamicCurveBattery;
                    dynCtrl.SetCurve(newCurve);
                    dynCtrl.IsAlternate = !this.FullPower;
                } else if (!Config.UseDynamicFanCurve && this.Program.IsEnabled) {
                    if (this.FullPower)
                        this.Program.Run(Config.FanProgramDefault);
                    else
                        this.Program.Run(Config.FanProgramDefaultAlt, true);
                }

            }

            // Separately also update the main form, if it's visible
            if (Context.FormMain != null && Context.FormMain.Visible)
                Context.FormMain.UpdateSys();

        }

        // Responds to the system entering and resuming from low-power state events
        public uint SuspendResumeCallback(IntPtr context, uint type, IntPtr setting) {

            if (type == PowrProf.PBT_APMRESUMEAUTOMATIC) {
                // System is resuming from suspend
                if (activeController is FanProgram prog)
                    prog.Resume();
                else if (activeController is DynamicFanController dyn)
                    dyn.Resume();
                else
                    this.Program.Resume(); // fallback
            } else if (type == PowrProf.PBT_APMSUSPEND) {
                // System is about to be suspended
                if (activeController is FanProgram prog)
                    prog.Suspend();
                else if (activeController is DynamicFanController dyn)
                    dyn.Suspend();
                else
                    this.Program.Suspend(); // fallback
            }

            return 0;

        }

    }

}
