using System;
public static class Projection {
    public static bool EdgeMarker(double[] p,double[] c,double[] r,int width,int height,double margin,out double x,out double y) {
        x=y=0;
        if(p==null||c==null||r==null||p.Length<3||c.Length<3||r.Length<3||margin<0||width<=2*margin||height<=2*margin)return false;
        double pitch=r[0]*Math.PI/180,yaw=r[1]*Math.PI/180,roll=r[2]*Math.PI/180;
        double sp=Math.Sin(pitch),cp=Math.Cos(pitch),sy=Math.Sin(yaw),cy=Math.Cos(yaw),sr=Math.Sin(roll),cr=Math.Cos(roll);
        double dx=p[0]-c[0],dy=p[1]-c[1],dz=p[2]-c[2];
        double forward=dx*cp*cy+dy*cp*sy+dz*sp;
        double right=dx*(sr*sp*cy-cr*sy)+dy*(sr*sp*sy+cr*cy)-dz*sr*cp;
        double up=dx*(-cr*sp*cy-sr*sy)+dy*(-cr*sp*sy+sr*cy)+dz*cr*cp;
        if(forward<=1)up=-Math.Abs(up)-Math.Abs(forward);
        double vx=right,vy=-up;
        if(double.IsNaN(vx)||double.IsNaN(vy)||double.IsInfinity(vx)||double.IsInfinity(vy)||Math.Abs(vx)+Math.Abs(vy)<.001)return false;
        double scale=Math.Min(Math.Abs(vx)<.001?double.PositiveInfinity:(width/2.0-margin)/Math.Abs(vx),Math.Abs(vy)<.001?double.PositiveInfinity:(height/2.0-margin)/Math.Abs(vy));
        x=width/2.0+vx*scale;y=height/2.0+vy*scale;
        return true;
    }
    public static bool TryWorldPosition(byte[] transform,out double[] position) {
        position=null;if(transform==null||transform.Length<0x60)return false;
        double norm=0;
        for(int i=0;i<4;i++){double q=BitConverter.ToDouble(transform,i*8);norm+=q*q;}
        if(double.IsNaN(norm)||double.IsInfinity(norm)||Math.Abs(norm-1)>.001)return false;
        var result=new double[3];
        for(int i=0;i<3;i++) {
            result[i]=BitConverter.ToDouble(transform,0x20+i*8);
            double scale=BitConverter.ToDouble(transform,0x40+i*8);
            if(double.IsNaN(result[i])||double.IsInfinity(result[i])||Math.Abs(result[i])>=100000000||double.IsNaN(scale)||double.IsInfinity(scale)||Math.Abs(scale)<.001||Math.Abs(scale)>=100)return false;
        }
        position=result;return true;
    }
    public static bool Radar(double[] p,double[] c,double yaw,double range,double radius,double centerX,double centerY,out double x,out double y) {
        x=y=0;
        if(range<=0||radius<=0||p==null||c==null||p.Length<2||c.Length<2) return false;
        double dx=(p[0]-c[0])/100,dy=(p[1]-c[1])/100;
        if(double.IsNaN(dx)||double.IsNaN(dy)||double.IsInfinity(dx)||double.IsInfinity(dy)||dx*dx+dy*dy>range*range) return false;
        double angle=yaw*Math.PI/180;
        double forward=dx*Math.Cos(angle)+dy*Math.Sin(angle),right=-dx*Math.Sin(angle)+dy*Math.Cos(angle);
        x=centerX+right/range*radius;y=centerY-forward/range*radius;
        return !double.IsNaN(x)&&!double.IsNaN(y)&&!double.IsInfinity(x)&&!double.IsInfinity(y);
    }
    public static bool Project(double[] p,double[] c,double[] r,double fov,int width,int height,out double x,out double y) {
        return ProjectCore(p,c,r,fov,width,height,false,out x,out y);
    }
    public static bool ProjectVertical(double[] p,double[] c,double[] r,double fov,int width,int height,out double x,out double y) {
        return ProjectCore(p,c,r,fov,width,height,true,out x,out y);
    }
    static bool ProjectCore(double[] p,double[] c,double[] r,double fov,int width,int height,bool vertical,out double x,out double y) {
        x=y=0;
        if(fov<=10 || fov>=170 || width<=0 || height<=0) return false;
        double pitch=r[0]*Math.PI/180, yaw=r[1]*Math.PI/180, roll=r[2]*Math.PI/180;
        double sp=Math.Sin(pitch),cp=Math.Cos(pitch),sy=Math.Sin(yaw),cy=Math.Cos(yaw),sr=Math.Sin(roll),cr=Math.Cos(roll);
        double dx=p[0]-c[0],dy=p[1]-c[1],dz=p[2]-c[2];
        double forward=dx*cp*cy+dy*cp*sy+dz*sp;
        if(forward<=1) return false;
        double right=dx*(sr*sp*cy-cr*sy)+dy*(sr*sp*sy+cr*cy)+dz*(-sr*cp);
        double up=dx*(-cr*sp*cy-sr*sy)+dy*(-cr*sp*sy+sr*cy)+dz*cr*cp;
        double focal=(vertical?height:width)/(2*Math.Tan(fov*Math.PI/360));
        x=width/2.0+right*focal/forward; y=height/2.0-up*focal/forward;
        return !double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y);
    }
}
