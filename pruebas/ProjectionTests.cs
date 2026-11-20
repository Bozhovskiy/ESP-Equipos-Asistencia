using System;
class ProjectionTests {
    static int Main() {
        double x,y;
        var zero=new double[]{0,0,0};
        if(!Projection.Project(new double[]{10,0,0},zero,zero,90,1920,1080,out x,out y) || Math.Abs(x-960)>0.001 || Math.Abs(y-540)>0.001) throw new Exception("FAIL: delante debe proyectar al centro.");
        if(Projection.Project(new double[]{-10,0,0},zero,zero,90,1920,1080,out x,out y)) throw new Exception("FAIL: detras debe rechazarse.");
        if(!Projection.Project(new double[]{0,10,0},zero,new double[]{0,90,0},90,1920,1080,out x,out y) || Math.Abs(x-960)>0.001) throw new Exception("FAIL: debe respetar el giro de camara.");
        if(Projection.Project(new double[]{10,0,0},zero,zero,0,1920,1080,out x,out y)) throw new Exception("FAIL: FOV cero debe rechazarse.");
        if(!Projection.Project(new double[]{10,5,0},zero,zero,90,1920,1080,out x,out y) || x<=960) throw new Exception("FAIL: derecha debe proyectar a la derecha.");
        Console.WriteLine("PASS: centro, profundidad, giro, FOV invalido y derecha."); return 0;
    }
}
