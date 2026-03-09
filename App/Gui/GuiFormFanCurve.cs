private void pictureBox_Paint(object sender, PaintEventArgs e) {
    var g = e.Graphics;
    // Disegna griglia, assi, ecc.
    // Disegna i punti della curva
    foreach (var p in curve.Points) {
        // Converti coordinate temperatura/velocità in pixel
        int x = (int)((p.Temperature - minTemp) / (maxTemp - minTemp) * width);
        int y = (int)(height - (p.FanSpeedCpu / 100.0 * height)); // per CPU, analogo per GPU con colore diverso
        g.FillEllipse(Brushes.Red, x - 3, y - 3, 6, 6);
    }
}
