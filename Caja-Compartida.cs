using System;
using System.Drawing;

static class PlayerBoxGeometry {
    static bool Valid(double[] values){if(values==null||values.Length<3)return false;foreach(double v in values)if(double.IsNaN(v)||double.IsInfinity(v))return false;return true;}
    static bool Project(double[] point,EspFrame frame,int width,int height,bool vertical,out double x,out double y) {
        return vertical?Projection.ProjectVertical(point,frame.Location,frame.Rotation,frame.Fov,width,height,out x,out y):Projection.Project(point,frame.Location,frame.Rotation,frame.Fov,width,height,out x,out y);
    }
    public static bool TryBox(PlayerPoint player,EspFrame frame,int width,int height,bool vertical,out RectangleF box) {
        box=RectangleF.Empty;
        if(player==null||frame==null||!frame.Valid||!Valid(player.Position)||!Valid(frame.Location)||!Valid(frame.Rotation)||width<1||height<1)return false;
        double x,y;if(!Project(player.Position,frame,width,height,vertical,out x,out y))return false;
        double x1,y1,x2,y2;
        double[] top={player.Position[0],player.Position[1],player.Position[2]+90};
        double[] foot={player.Position[0],player.Position[1],player.Position[2]-90};
        var target=player.Target;
        if(target!=null&&target.BonesValid&&Valid(target.Head)&&Valid(target.LeftFoot)&&Valid(target.RightFoot)){
            top=target.Head;foot=new[]{(target.LeftFoot[0]+target.RightFoot[0])/2,(target.LeftFoot[1]+target.RightFoot[1])/2,(target.LeftFoot[2]+target.RightFoot[2])/2};
        }
        if(!Project(top,frame,width,height,vertical,out x1,out y1)||!Project(foot,frame,width,height,vertical,out x2,out y2))return false;
        double h=Math.Abs(y2-y1),w=h*.45,cx=(x1+x2)/2;
        if(h<3||h>height*2||cx+w/2<0||cx-w/2>width||Math.Min(y1,y2)>height||Math.Max(y1,y2)<0)return false;
        box=new RectangleF((float)(cx-w/2),(float)Math.Min(y1,y2),(float)w,(float)h);return true;
    }
}
