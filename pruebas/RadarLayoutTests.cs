using System;
using System.Drawing;
class RadarLayoutTests {
    static int Main() {
        var regular=HudLayout.Radar(1920,1080,110,230);
        if(regular!=new RectangleF(18,598,252,252))throw new Exception("FAIL: radar no queda arriba de la vida.");
        foreach(int width in new[]{320,1280,1920})foreach(int height in new[]{480,720,1080})foreach(int radius in new[]{60,110,140})foreach(int gap in new[]{120,230,450}) {
            var bounds=HudLayout.Radar(width,height,radius,gap);
            if(bounds.Left<0||bounds.Top<70||bounds.Right>width||bounds.Bottom>height-18)throw new Exception("FAIL: radar configurable sale del viewport.");
        }
        if(!HudLayout.Radar(100,100,140,450).IsEmpty)throw new Exception("FAIL: viewport minimo intenta dibujar fuera.");
        Console.WriteLine("PASS: radar arriba de vida y dentro de margenes con configuraciones extremas.");return 0;
    }
}
