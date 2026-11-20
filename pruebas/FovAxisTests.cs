using System;
using System.Reflection;
class FovAxisTests {
    static int Main() {
        var method=typeof(Projection).GetMethod("ProjectVertical");
        if(method==null)throw new Exception("FAIL: no admite FOV vertical para calibrar.");
        object[] args={new double[]{1000,1000,1000},new double[]{0,0,0},new double[]{0,0,0},90.0,1920,1080,0.0,0.0};
        if(!(bool)method.Invoke(null,args)||Math.Abs((double)args[6]-1500)>.001||Math.Abs((double)args[7])>.001)throw new Exception("FAIL: objetivo a 45 grados no coincide con borde vertical.");
        args=new object[]{new double[]{1000,0,1000},new double[]{0,0,0},new double[]{45,0,0},90.0,1920,1080,0.0,0.0};
        if(!(bool)method.Invoke(null,args)||Math.Abs((double)args[6]-960)>.001||Math.Abs((double)args[7]-540)>.001)throw new Exception("FAIL: inclinacion hacia objetivo no lo centra.");
        Console.WriteLine("PASS: FOV vertical y camara inclinada con coordenadas esperadas.");return 0;
    }
}
