using System;
using System.Reflection;
class RadarTests {
    static int Main() {
        var method=typeof(Projection).GetMethod("Radar");
        if(method==null)throw new Exception("FAIL: faltan coordenadas del radar relativas a la camara.");
        Check(method,new double[]{10000,0,0},0,110,10,true);
        Check(method,new double[]{0,10000,0},0,210,110,true);
        Check(method,new double[]{0,10000,0},90,110,10,true);
        Check(method,new double[]{20000,0,0},0,0,0,false);
        Console.WriteLine("PASS: radar delante, derecha, giro y limite de alcance.");return 0;
    }
    static void Check(MethodInfo method,double[] target,double yaw,double x,double y,bool expected) {
        var args=new object[]{target,new double[]{0,0,0},yaw,100.0,100.0,110.0,110.0,0.0,0.0};
        bool valid=(bool)method.Invoke(null,args);
        if(valid!=expected || (valid&&(Math.Abs((double)args[7]-x)>0.001||Math.Abs((double)args[8]-y)>0.001)))throw new Exception("FAIL: radar no respeta posicion, giro o alcance.");
    }
}
