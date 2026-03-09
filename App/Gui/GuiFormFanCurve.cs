private void pictureBox_Paint(object sender, PaintEventArgs e) {
    var g = e.Graphics;
    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

    int width = pictureBox.ClientSize.Width;
    int height = pictureBox.ClientSize.Height;
    float minTemp = 30f, maxTemp = 100f; // o leggi da Config
    float minSpeed = 0f, maxSpeed = 100f;

    // Disegna griglia
    using (Pen gridPen = new Pen(Color.LightGray, 1)) {
        for (int i = 0; i <= 10; i++) {
            float x = i * width / 10f;
            float y = i * height / 10f;
            g.DrawLine(gridPen, x, 0, x, height);
            g.DrawLine(gridPen, 0, y, width, y);
        }
    }

    // Disegna assi
    using (Pen axisPen = new Pen(Color.Black, 2)) {
        g.DrawLine(axisPen, 0, height, width, height); // asse X
        g.DrawLine(axisPen, 0, 0, 0, height);          // asse Y
    }

    // Disegna linee della curva
    if (curve.Points.Count >= 2) {
        // Linea CPU (blu)
        using (Pen cpuPen = new Pen(Color.Blue, 2)) {
            for (int i = 0; i < curve.Points.Count - 1; i++) {
                var p1 = curve.Points[i];
                var p2 = curve.Points[i + 1];
                int x1 = (int)((p1.Temperature - minTemp) / (maxTemp - minTemp) * width);
                int y1 = (int)(height - (p1.FanSpeedCpu / maxSpeed) * height);
                int x2 = (int)((p2.Temperature - minTemp) / (maxTemp - minTemp) * width);
                int y2 = (int)(height - (p2.FanSpeedCpu / maxSpeed) * height);
                g.DrawLine(cpuPen, x1, y1, x2, y2);
            }
        }
        // Linea GPU (rossa)
        using (Pen gpuPen = new Pen(Color.Red, 2)) {
            for (int i = 0; i < curve.Points.Count - 1; i++) {
                var p1 = curve.Points[i];
                var p2 = curve.Points[i + 1];
                int x1 = (int)((p1.Temperature - minTemp) / (maxTemp - minTemp) * width);
                int y1 = (int)(height - (p1.FanSpeedGpu / maxSpeed) * height);
                int x2 = (int)((p2.Temperature - minTemp) / (maxTemp - minTemp) * width);
                int y2 = (int)(height - (p2.FanSpeedGpu / maxSpeed) * height);
                g.DrawLine(gpuPen, x1, y1, x2, y2);
            }
        }
    }

    // Disegna i punti (CPU blu, GPU rossi, oppure usa un colore unico)
    foreach (var p in curve.Points) {
        int x = (int)((p.Temperature - minTemp) / (maxTemp - minTemp) * width);
        int yCpu = (int)(height - (p.FanSpeedCpu / maxSpeed) * height);
        int yGpu = (int)(height - (p.FanSpeedGpu / maxSpeed) * height);

        using (Brush cpuBrush = new SolidBrush(Color.Blue))
            g.FillEllipse(cpuBrush, x - 4, yCpu - 4, 8, 8);
        using (Brush gpuBrush = new SolidBrush(Color.Red))
            g.FillEllipse(gpuBrush, x - 4, yGpu - 4, 8, 8);
    }

    // Opzionale: disegna punto di lavoro corrente (se controller attivo)
    if (showCurrentTemp && currentTemp > 0) {
        float currentSpeedCpu = curve.GetFanSpeeds(currentTemp).cpu;
        float currentSpeedGpu = curve.GetFanSpeeds(currentTemp).gpu;
        int xCurrent = (int)((currentTemp - minTemp) / (maxTemp - minTemp) * width);
        int yCurrentCpu = (int)(height - (currentSpeedCpu / maxSpeed) * height);
        int yCurrentGpu = (int)(height - (currentSpeedGpu / maxSpeed) * height);

        using (Pen pen = new Pen(Color.Green, 3))
            g.DrawEllipse(pen, xCurrent - 6, yCurrentCpu - 6, 12, 12);
        using (Pen pen = new Pen(Color.Green, 3))
            g.DrawEllipse(pen, xCurrent - 6, yCurrentGpu - 6, 12, 12);
    }
}
