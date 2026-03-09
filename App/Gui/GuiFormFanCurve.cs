  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023-2024 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using System;
using System.Drawing;
using System.Windows.Forms;
using OmenMon.Hardware.Platform;
using OmenMon.Library;

namespace OmenMon.AppGui {

    /// <summary>
    /// Dialog window for editing dynamic fan curves (AC and battery).
    /// Allows graphical manipulation of temperature→fan speed points.
    /// </summary>
    public partial class GuiFormFanCurve : Form {

        // Private fields
        private DynamicFanCurve curveAC;                // Working copy of AC curve
        private DynamicFanCurve curveBattery;           // Working copy of battery curve
        private DynamicFanCurve currentCurve;           // Currently displayed curve (AC or battery)

        private TabControl tabControl;
        private PictureBox pictureBox;
        private Button btnSave;
        private Button btnCancel;
        private Button btnDefault;
        private Label lblStatus;

        // Interactive editing state
        private FanCurvePoint selectedPoint;             // The point being dragged (or null)
        private bool isDragging;                          // True while dragging a point
        private bool isDraggingCpu;                       // True if dragging CPU point, false for GPU
        private const float minTemp = 30f;                 // Minimum temperature on X axis
        private const float maxTemp = 100f;                // Maximum temperature on X axis
        private const float minSpeed = 0f;                 // Minimum fan speed on Y axis
        private const float maxSpeed = 100f;               // Maximum fan speed on Y axis
        private const int margin = 30;                      // Margin around the drawing area

        // Public properties to return the edited curves
        public DynamicFanCurve CurveAC { get; private set; }
        public DynamicFanCurve CurveBattery { get; private set; }

        /// <summary>
        /// Constructor. Takes the original curves and creates working copies.
        /// </summary>
        public GuiFormFanCurve(DynamicFanCurve ac, DynamicFanCurve battery) {
            InitializeComponent();
            // Create deep copies to avoid modifying original until Save
            curveAC = new DynamicFanCurve(ac.Points);
            curveBattery = new DynamicFanCurve(battery.Points);
            currentCurve = curveAC;
            CurveAC = ac;
            CurveBattery = battery;
            this.Text = "Modifica curva dinamica ventole";
        }

        /// <summary>
        /// Sets up all controls manually (without designer file).
        /// </summary>
        private void InitializeComponent() {
            this.tabControl = new TabControl();
            this.pictureBox = new PictureBox();
            this.btnSave = new Button();
            this.btnCancel = new Button();
            this.btnDefault = new Button();
            this.lblStatus = new Label();

            // TabControl
            tabControl.Dock = DockStyle.Top;
            tabControl.Height = 40;
            tabControl.TabPages.Add("AC (rete)");
            tabControl.TabPages.Add("Batteria");
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

            // PictureBox
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.BackColor = Color.White;
            pictureBox.Paint += PictureBox_Paint;
            pictureBox.MouseDown += PictureBox_MouseDown;
            pictureBox.MouseMove += PictureBox_MouseMove;
            pictureBox.MouseUp += PictureBox_MouseUp;

            // Buttons
            btnSave.Text = "Salva";
            btnSave.Location = new Point(10, 10);
            btnSave.Click += BtnSave_Click;

            btnCancel.Text = "Annulla";
            btnCancel.Location = new Point(100, 10);
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            btnDefault.Text = "Ripristina default";
            btnDefault.Location = new Point(190, 10);
            btnDefault.Click += BtnDefault_Click;

            lblStatus.Text = "Pronto";
            lblStatus.Location = new Point(300, 15);
            lblStatus.AutoSize = true;

            // Bottom panel for buttons
            Panel bottomPanel = new Panel { Height = 50, Dock = DockStyle.Bottom };
            bottomPanel.Controls.AddRange(new Control[] { btnSave, btnCancel, btnDefault, lblStatus });

            // Add controls to form
            this.Controls.Add(pictureBox);
            this.Controls.Add(tabControl);
            this.Controls.Add(bottomPanel);

            this.Size = new Size(800, 600);
            this.MinimumSize = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;
        }

        /// <summary>
        /// Switches between AC and battery curve when tab changes.
        /// </summary>
        private void TabControl_SelectedIndexChanged(object sender, EventArgs e) {
            currentCurve = tabControl.SelectedIndex == 0 ? curveAC : curveBattery;
            selectedPoint = null; // clear selection
            pictureBox.Invalidate();
        }

        /// <summary>
        /// Paints the curve, grid, axes, points and selection highlight.
        /// </summary>
        private void PictureBox_Paint(object sender, PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int width = pictureBox.ClientSize.Width;
            int height = pictureBox.ClientSize.Height;

            int graphWidth = width - 2 * margin;
            int graphHeight = height - 2 * margin;

            if (graphWidth <= 0 || graphHeight <= 0) return;

            // Helper to convert world coordinates to pixel coordinates
            PointF WorldToPixel(float temp, float speed) {
                float x = margin + (temp - minTemp) / (maxTemp - minTemp) * graphWidth;
                float y = height - margin - (speed / maxSpeed) * graphHeight;
                return new PointF(x, y);
            }

            // Draw white background
            g.Clear(Color.White);

            // Draw grid (light gray lines)
            using (Pen gridPen = new Pen(Color.LightGray, 1)) {
                // Vertical lines (temperature)
                for (int i = 0; i <= 10; i++) {
                    float temp = minTemp + i * (maxTemp - minTemp) / 10;
                    var p = WorldToPixel(temp, 0);
                    g.DrawLine(gridPen, p.X, margin, p.X, height - margin);
                }
                // Horizontal lines (speed)
                for (int i = 0; i <= 10; i++) {
                    float speed = i * 10;
                    var p = WorldToPixel(minTemp, speed);
                    g.DrawLine(gridPen, margin, p.Y, width - margin, p.Y);
                }
            }

            // Draw axes (black lines)
            using (Pen axisPen = new Pen(Color.Black, 2)) {
                var origin = WorldToPixel(minTemp, 0);
                var maxX = WorldToPixel(maxTemp, 0);
                var maxY = WorldToPixel(minTemp, maxSpeed);
                g.DrawLine(axisPen, origin.X, origin.Y, maxX.X, origin.Y); // X axis
                g.DrawLine(axisPen, origin.X, origin.Y, origin.X, maxY.Y); // Y axis
            }

            // Draw the CPU and GPU lines if at least two points exist
            if (currentCurve.Points.Count >= 2) {
                // CPU line (blue)
                using (Pen cpuPen = new Pen(Color.Blue, 2)) {
                    for (int i = 0; i < currentCurve.Points.Count - 1; i++) {
                        var p1 = currentCurve.Points[i];
                        var p2 = currentCurve.Points[i + 1];
                        var pt1 = WorldToPixel(p1.Temperature, p1.FanSpeedCpu);
                        var pt2 = WorldToPixel(p2.Temperature, p2.FanSpeedCpu);
                        g.DrawLine(cpuPen, pt1, pt2);
                    }
                }
                // GPU line (red)
                using (Pen gpuPen = new Pen(Color.Red, 2)) {
                    for (int i = 0; i < currentCurve.Points.Count - 1; i++) {
                        var p1 = currentCurve.Points[i];
                        var p2 = currentCurve.Points[i + 1];
                        var pt1 = WorldToPixel(p1.Temperature, p1.FanSpeedGpu);
                        var pt2 = WorldToPixel(p2.Temperature, p2.FanSpeedGpu);
                        g.DrawLine(gpuPen, pt1, pt2);
                    }
                }
            }

            // Draw all points (CPU blue, GPU red)
            foreach (var p in currentCurve.Points) {
                var ptCpu = WorldToPixel(p.Temperature, p.FanSpeedCpu);
                var ptGpu = WorldToPixel(p.Temperature, p.FanSpeedGpu);

                using (Brush cpuBrush = new SolidBrush(Color.Blue))
                    g.FillEllipse(cpuBrush, ptCpu.X - 4, ptCpu.Y - 4, 8, 8);
                using (Brush gpuBrush = new SolidBrush(Color.Red))
                    g.FillEllipse(gpuBrush, ptGpu.X - 4, ptGpu.Y - 4, 8, 8);

                // Highlight selected point with a black ring
                if (p == selectedPoint) {
                    using (Pen selPen = new Pen(Color.Black, 2)) {
                        g.DrawEllipse(selPen, ptCpu.X - 6, ptCpu.Y - 6, 12, 12);
                        g.DrawEllipse(selPen, ptGpu.X - 6, ptGpu.Y - 6, 12, 12);
                    }
                }
            }

            // (Optional) Draw current operating point if needed
            // if (showCurrentTemp) { ... }
        }

        /// <summary>
        /// Converts pixel coordinates to world (temperature, speed).
        /// </summary>
        private (float temp, float speed) PixelToWorld(PointF pixel) {
            int graphWidth = pictureBox.ClientSize.Width - 2 * margin;
            int graphHeight = pictureBox.ClientSize.Height - 2 * margin;
            float temp = minTemp + (pixel.X - margin) / graphWidth * (maxTemp - minTemp);
            float speed = maxSpeed - (pixel.Y - margin) / graphHeight * maxSpeed;
            return (temp, speed);
        }

        /// <summary>
        /// Checks if the mouse is near a point (CPU or GPU) and returns that point,
        /// also setting a flag indicating which component (CPU or GPU) was hit.
        /// </summary>
        private FanCurvePoint HitTest(Point mousePos, out bool cpuHit) {
            cpuHit = false;
            foreach (var p in currentCurve.Points) {
                // Compute pixel positions
                int graphWidth = pictureBox.ClientSize.Width - 2 * margin;
                int graphHeight = pictureBox.ClientSize.Height - 2 * margin;
                float x = margin + (p.Temperature - minTemp) / (maxTemp - minTemp) * graphWidth;
                float yCpu = pictureBox.ClientSize.Height - margin - (p.FanSpeedCpu / maxSpeed) * graphHeight;
                float yGpu = pictureBox.ClientSize.Height - margin - (p.FanSpeedGpu / maxSpeed) * graphHeight;

                if (Math.Abs(mousePos.X - x) < 8 && Math.Abs(mousePos.Y - yCpu) < 8) {
                    cpuHit = true;
                    return p;
                }
                if (Math.Abs(mousePos.X - x) < 8 && Math.Abs(mousePos.Y - yGpu) < 8) {
                    cpuHit = false;
                    return p;
                }
            }
            return null;
        }

        /// <summary>
        /// Mouse down: select point or prepare to add new point.
        /// </summary>
        private void PictureBox_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                // Check if we hit a point
                bool cpuHit;
                var hitPoint = HitTest(e.Location, out cpuHit);
                if (hitPoint != null) {
                    // Start dragging that point
                    selectedPoint = hitPoint;
                    isDragging = true;
                    isDraggingCpu = cpuHit;
                    pictureBox.Invalidate();
                } else {
                    // No point hit: prepare to add a new point (will be added on MouseUp if no drag)
                    selectedPoint = null;
                    isDragging = false;
                }
            } else if (e.Button == MouseButtons.Right) {
                // Right click: remove point if hit
                bool cpuHit;
                var hitPoint = HitTest(e.Location, out cpuHit);
                if (hitPoint != null) {
                    currentCurve.RemovePoint(currentCurve.Points.IndexOf(hitPoint));
                    selectedPoint = null;
                    pictureBox.Invalidate();
                }
            }
        }

        /// <summary>
        /// Mouse move: drag selected point.
        /// </summary>
        private void PictureBox_MouseMove(object sender, MouseEventArgs e) {
            if (isDragging && selectedPoint != null && e.Button == MouseButtons.Left) {
                // Convert mouse position to world coordinates
                var (temp, speed) = PixelToWorld(e.Location);
                // Clamp values
                temp = Math.Max(minTemp, Math.Min(maxTemp, temp));
                speed = Math.Max(minSpeed, Math.Min(maxSpeed, speed));

                // Update the appropriate component (CPU or GPU)
                if (isDraggingCpu) {
                    selectedPoint.FanSpeedCpu = (byte)Math.Round(speed);
                } else {
                    selectedPoint.FanSpeedGpu = (byte)Math.Round(speed);
                }
                // Temperature remains unchanged (only speed changes on drag)
                // Force points to remain sorted (temperature unchanged, so order stays)
                pictureBox.Invalidate();
            }
        }

        /// <summary>
        /// Mouse up: if not dragging and left click on empty area, add a new point.
        /// </summary>
        private void PictureBox_MouseUp(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                if (!isDragging && selectedPoint == null) {
                    // Add a new point at clicked position
                    var (temp, speed) = PixelToWorld(e.Location);
                    temp = Math.Max(minTemp, Math.Min(maxTemp, temp));
                    speed = Math.Max(minSpeed, Math.Min(maxSpeed, speed));
                    byte cpuSpeed = (byte)Math.Round(speed);
                    byte gpuSpeed = (byte)Math.Round(speed); // start with same value
                    currentCurve.AddPoint(temp, cpuSpeed, gpuSpeed);
                }
                isDragging = false;
                pictureBox.Invalidate();
            }
        }

        /// <summary>
        /// Save the edited curves back to the originals.
        /// </summary>
        private void BtnSave_Click(object sender, EventArgs e) {
            CurveAC.SetPoints(curveAC.Points);
            CurveBattery.SetPoints(curveBattery.Points);
            this.DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// Restore default curves (hardcoded or from Config).
        /// </summary>
        private void BtnDefault_Click(object sender, EventArgs e) {
            // Example default curves (you may load from Config or define constants)
            curveAC.Clear();
            curveAC.AddPoint(30, 20, 20);
            curveAC.AddPoint(50, 30, 30);
            curveAC.AddPoint(70, 50, 45);
            curveAC.AddPoint(90, 80, 70);

            curveBattery.Clear();
            curveBattery.AddPoint(30, 15, 15);
            curveBattery.AddPoint(50, 25, 25);
            curveBattery.AddPoint(70, 40, 35);
            curveBattery.AddPoint(90, 60, 50);

            currentCurve = (tabControl.SelectedIndex == 0) ? curveAC : curveBattery;
            pictureBox.Invalidate();
        }
    }
}
