using System;
using System.Reflection;
class EdgeIndicatorTests {
    static void Check(MethodInfo method,double[] position,double[] rotation,double wantX,double wantY) {
        object[] args={position,new double[]{0,0,0},rotation,1920,1080,60.0,0.0,0.0};
        if(!(bool)method.Invoke(null,args)||Math.Abs((double)args[6]-wantX)>.001||Math.Abs((double)args[7]-wantY)>.001)throw new Exception("FAIL: indicador de borde en direccion incorrecta.");
    }
    static int Main() {
        var method=typeof(Projection).GetMethod("EdgeMarker");
        if(method==null)throw new Exception("FAIL: faltan indicadores para jugadores fuera de pantalla.");
        Check(method,new double[]{10,100,0},new double[]{0,0,0},1860,540);
        Check(method,new double[]{10,-100,0},new double[]{0,0,0},60,540);
        Check(method,new double[]{10,0,100},new double[]{0,0,0},960,60);
        Check(method,new double[]{-100,0,0},new double[]{0,0,0},960,1020);
        Check(method,new double[]{-100,10,0},new double[]{0,90,0},1860,540);
        object[] bad={new double[]{double.NaN,0,0},new double[]{0,0,0},new double[]{0,0,0},1920,1080,60.0,0.0,0.0};
        if((bool)method.Invoke(null,bad))throw new Exception("FAIL: datos invalidos dibujan indicador.");
        Console.WriteLine("PASS: derecha, izquierda, arriba, detras, giro y rechazo de NaN.");return 0;
    }
}
