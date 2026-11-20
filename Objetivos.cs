using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web.Script.Serialization;

enum TargetRelation { Unknown, Ally, Enemy }
enum TargetVisibility { Unknown, Clear, Occluded }
sealed class TargetData {
    public TargetRelation Relation;
    public TargetVisibility Visibility,ChestVisibility;
    public double VisibilityAgeMs=double.PositiveInfinity,VisibilityCapturedAt;
    public double ChestVisibilityAgeMs=double.PositiveInfinity,ChestVisibilityCapturedAt;
    public bool BonesValid;
    public double[] Head,Chest,Pelvis,LeftFoot,RightFoot;
}
// This is an explicit layout for a verified test build. No legacy offsets are
// guessed, and render timestamps are deliberately not a visibility source.
sealed class TargetReadProfile {
    public bool Enabled=false;
    public uint PeTimestamp=0,ImageSize=0;
    public int PawnStateOffset=-1,TeamIdOffset=-1,MeshOffset=-1,BoneArrayOffset=-1,ComponentTransformOffset=-1;
    public int HeadIndex=-1,ChestIndex=-1,PelvisIndex=-1,LeftFootIndex=-1,RightFootIndex=-1;
    public string BoneSpace="Component",VisibilitySource="";
    public int WorldTimeOffset=-1,VisibilityTimeOffset=-1,VisibilityObserverOffset=-1,HeadVisibilityOffset=-1,ChestVisibilityOffset=-1;
    public static TargetReadProfile Load(string path){try{var f=new FileInfo(path);if(!f.Exists||f.Length>16384)return new TargetReadProfile();return new JavaScriptSerializer().Deserialize<TargetReadProfile>(File.ReadAllText(path))??new TargetReadProfile();}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}catch(FormatException){}catch(OverflowException){}catch(Exception e)when(e.InnerException is FormatException||e.InnerException is OverflowException){}return new TargetReadProfile();}
}
static class TargetValidation {
    public static bool Finite(double v){return !double.IsNaN(v)&&!double.IsInfinity(v);}
    static bool Point(double[] p){if(p==null||p.Length!=3)return false;foreach(double n in p)if(!Finite(n)||Math.Abs(n)>=100000000)return false;return true;}
    static double Distance(double[] a,double[] b){double s=0;for(int i=0;i<3;i++)s+=(a[i]-b[i])*(a[i]-b[i]);return Math.Sqrt(s);}
    public static bool Offset(int n){return n>=0&&n<=65536;}
    public static bool TeamLayoutValid(TargetReadProfile p){return p!=null&&Offset(p.PawnStateOffset)&&Offset(p.TeamIdOffset);}
    public static bool ProfileValid(TargetReadProfile p){
        return p!=null&&p.Enabled&&p.PeTimestamp!=0&&p.ImageSize>=65536&&(TeamLayoutValid(p)||BoneLayoutValid(p));
    }
    public static bool BoneLayoutValid(TargetReadProfile p){
        if(p==null||p.BoneSpace!="Component"||!Offset(p.MeshOffset)||!Offset(p.BoneArrayOffset)||!Offset(p.ComponentTransformOffset))return false;
        var indices=new HashSet<int>();foreach(int index in new[]{p.HeadIndex,p.ChestIndex,p.PelvisIndex,p.LeftFootIndex,p.RightFootIndex})if(index<0||index>=512||!indices.Add(index))return false;return true;
    }
    public static bool TryBoneWorld(byte[] local,byte[] component,out double[] world){
        world=null;double[] position,origin;
        if(!Projection.TryWorldPosition(local,out position)||!Projection.TryWorldPosition(component,out origin))return false;
        double x=position[0]*BitConverter.ToDouble(component,64),y=position[1]*BitConverter.ToDouble(component,72),z=position[2]*BitConverter.ToDouble(component,80);
        double qx=BitConverter.ToDouble(component,0),qy=BitConverter.ToDouble(component,8),qz=BitConverter.ToDouble(component,16),qw=BitConverter.ToDouble(component,24);
        double tx=2*(qy*z-qz*y),ty=2*(qz*x-qx*z),tz=2*(qx*y-qy*x);
        var result=new[]{origin[0]+x+qw*tx+qy*tz-qz*ty,origin[1]+y+qw*ty+qz*tx-qx*tz,origin[2]+z+qw*tz+qx*ty-qy*tx};
        if(!Point(result))return false;world=result;return true;
    }
    public static bool CoherentPose(double[] origin,double[] head,double[] chest,double[] pelvis,double[] left,double[] right){
        foreach(var p in new[]{origin,head,chest,pelvis,left,right})if(!Point(p))return false;
        foreach(var p in new[]{head,chest,pelvis,left,right})if(Distance(origin,p)>300)return false;
        double height=(Distance(head,left)+Distance(head,right))/2;
        return height>=35&&height<=260&&Distance(head,chest)>=5&&Distance(head,chest)<=100&&Distance(chest,pelvis)>=5&&Distance(chest,pelvis)<=120&&Distance(left,right)<=140;
    }
}
// Session-only exclusions. Unknown metadata never implies an enemy or ally.
sealed class TeamFilter {
    readonly Dictionary<ulong,long> marked=new Dictionary<ulong,long>();
    ulong world,localPawn;
    public int Count {get{return marked.Count;}}
    public void Clear(){marked.Clear();}
    public TargetRelation Relation(PlayerPoint player){return player!=null&&marked.ContainsKey(player.Actor)?TargetRelation.Ally:player==null||player.Target==null?TargetRelation.Unknown:player.Target.Relation;}
    public bool Show(PlayerPoint player,bool hideAllies,bool hideUnknown){
        if(player==null)return false;var relation=Relation(player);
        return !(hideAllies&&relation==TargetRelation.Ally)&&!(hideUnknown&&relation==TargetRelation.Unknown);
    }
    public void Update(EspFrame frame,long now){
        if(frame!=null&&frame.Valid&&Memory.Valid(frame.World)&&Memory.Valid(frame.LocalPawn)){
            if(world!=frame.World||localPawn!=frame.LocalPawn){Clear();world=frame.World;localPawn=frame.LocalPawn;}
        }
        var expired=new List<ulong>();foreach(var item in marked)if(now<item.Value||now-item.Value>5*Stopwatch.Frequency)expired.Add(item.Key);
        foreach(var actor in expired)marked.Remove(actor);
        if(frame==null||!frame.IsFresh(now))return;
        foreach(var player in frame.Players)if(marked.ContainsKey(player.Actor))marked[player.Actor]=now;
    }
    public string ToggleAtCrosshair(EspFrame frame,int width,int height,bool vertical,int maxDistance){
        long now=Stopwatch.GetTimestamp();Update(frame,now);
        if(frame==null||!frame.IsFresh(now)||!Memory.Valid(frame.World)||!Memory.Valid(frame.LocalPawn))return "F6: esperando datos recientes";
        PlayerPoint selected=null;int matches=0;
        foreach(var player in frame.Players){
            System.Drawing.RectangleF box;
            if(!Memory.Valid(player.Actor)||!TargetValidation.Finite(player.Distance)||player.Distance<0||player.Distance>maxDistance||!PlayerBoxGeometry.TryBox(player,frame,width,height,vertical,out box)||!box.Contains(width/2f,height/2f))continue;
            selected=player;matches++;
        }
        if(matches!=1)return matches==0?"F6: coloca la mira sobre el companero":"F6: jugadores superpuestos; cambia la mira";
        if(marked.Remove(selected.Actor))return "Companero desmarcado";
        marked[selected.Actor]=now;return "Companero excluido del ESP";
    }
}
sealed class TargetDataReader {
    readonly TargetReadProfile profile;
    readonly Func<ulong,int,byte[]> read;
    bool active;
    public string Status {get;private set;}="Sin perfil de objetivos validado";
    public TargetDataReader(TargetReadProfile p,Func<ulong,int,byte[]> reader){profile=p;read=reader;}
    byte[] Block(ulong address,int size){if(!Memory.Valid(address)||size<1||size>65536||address+(ulong)size>=0x800000000000UL)throw new IOException("Direccion de objetivos invalida");byte[] b=read(address,size);if(b==null||b.Length!=size)throw new IOException("Lectura de objetivos incompleta");return b;}
    ulong Pointer(ulong address){return BitConverter.ToUInt64(Block(address,8),0);}
    double Time(ulong address){return BitConverter.ToDouble(Block(address,8),0);}
    public bool Bind(ulong imageBase){
        active=false;Status="Sin perfil de objetivos validado";if(!TargetValidation.ProfileValid(profile))return false;
        try{
            byte[] dos=Block(imageBase,64);int offset=BitConverter.ToInt32(dos,60);if(dos[0]!=77||dos[1]!=90||offset<64||offset>1048576)return false;
            byte[] pe=Block(imageBase+(uint)offset,84);
            active=BitConverter.ToUInt32(pe,0)==0x4550&&BitConverter.ToUInt16(pe,4)==0x8664&&BitConverter.ToUInt16(pe,24)==0x20b&&BitConverter.ToUInt32(pe,8)==profile.PeTimestamp&&BitConverter.ToUInt32(pe,80)==profile.ImageSize;
            Status=active?"Perfil coincide; datos sujetos a validacion":"Perfil no coincide con la compilacion";return active;
        }catch(IOException){Status="No se pudo comprobar la compilacion";return false;}
    }
    int? Team(ulong pawn){
        if(!TargetValidation.TeamLayoutValid(profile))return null;
        ulong state=Pointer(pawn+(uint)profile.PawnStateOffset);if(!Memory.Valid(state))return null;
        int id=BitConverter.ToInt32(Block(state+(uint)profile.TeamIdOffset,4),0);
        int after=BitConverter.ToInt32(Block(state+(uint)profile.TeamIdOffset,4),0);
        return id==after&&Pointer(pawn+(uint)profile.PawnStateOffset)==state&&id>=0&&id<=65535?(int?)id:null;
    }
    TargetVisibility Visibility(ulong actor,ulong world,ulong camera,int offset,out double sampleAge){
        sampleAge=double.PositiveInfinity;
        if(profile.VisibilitySource!="CameraRaycast"||!TargetValidation.Offset(profile.WorldTimeOffset)||!TargetValidation.Offset(profile.VisibilityTimeOffset)||!TargetValidation.Offset(profile.VisibilityObserverOffset)||!TargetValidation.Offset(offset))return TargetVisibility.Unknown;
        if(Pointer(actor+(uint)profile.VisibilityObserverOffset)!=camera)return TargetVisibility.Unknown;
        double now=Time(world+(uint)profile.WorldTimeOffset),stamp=Time(actor+(uint)profile.VisibilityTimeOffset);
        double age=now-stamp;if(!TargetValidation.Finite(now)||!TargetValidation.Finite(stamp)||stamp<=0||age<0||age>.08)return TargetVisibility.Unknown;
        byte value=Block(actor+(uint)offset,1)[0];
        if(Time(actor+(uint)profile.VisibilityTimeOffset)!=stamp||Pointer(actor+(uint)profile.VisibilityObserverOffset)!=camera)return TargetVisibility.Unknown;
        sampleAge=age*1000;return value==1?TargetVisibility.Clear:value==0?TargetVisibility.Occluded:TargetVisibility.Unknown;
    }
    public TargetData Read(ulong actor,ulong localPawn,ulong world,ulong camera,double[] origin){
        var result=new TargetData();if(!active||!Memory.Valid(actor)||!Memory.Valid(localPawn)||!Memory.Valid(world)||!Memory.Valid(camera))return result;
        try{int? own=Team(localPawn),other=Team(actor);if(own.HasValue&&other.HasValue)result.Relation=own==other?TargetRelation.Ally:TargetRelation.Enemy;}catch(IOException){}
        result.VisibilityCapturedAt=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        try{result.Visibility=Visibility(actor,world,camera,profile.HeadVisibilityOffset,out result.VisibilityAgeMs);}catch(IOException){result.Visibility=TargetVisibility.Unknown;result.VisibilityAgeMs=double.PositiveInfinity;}
        result.ChestVisibilityCapturedAt=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        try{result.ChestVisibility=Visibility(actor,world,camera,profile.ChestVisibilityOffset,out result.ChestVisibilityAgeMs);}catch(IOException){result.ChestVisibility=TargetVisibility.Unknown;result.ChestVisibilityAgeMs=double.PositiveInfinity;}
        try{
            if(!TargetValidation.BoneLayoutValid(profile))return result;
            ulong mesh=Pointer(actor+(uint)profile.MeshOffset);if(!Memory.Valid(mesh))return result;
            byte[] header=Block(mesh+(uint)profile.BoneArrayOffset,16);ulong array=BitConverter.ToUInt64(header,0);int count=BitConverter.ToInt32(header,8),capacity=BitConverter.ToInt32(header,12);
            if(!Memory.Valid(array)||count<5||count>512||capacity<count||capacity>512)return result;
            var indices=new[]{profile.HeadIndex,profile.ChestIndex,profile.PelvisIndex,profile.LeftFootIndex,profile.RightFootIndex};foreach(int index in indices)if(index>=count)return result;
            byte[] component=Block(mesh+(uint)profile.ComponentTransformOffset,96);
            var points=new double[5][];
            for(int i=0;i<5;i++)if(!TargetValidation.TryBoneWorld(Block(array+(ulong)indices[i]*96,96),component,out points[i]))return result;
            byte[] after=Block(mesh+(uint)profile.BoneArrayOffset,16);for(int i=0;i<16;i++)if(after[i]!=header[i])return result;
            if(Pointer(actor+(uint)profile.MeshOffset)!=mesh||!TargetValidation.CoherentPose(origin,points[0],points[1],points[2],points[3],points[4]))return result;
            result.Head=points[0];result.Chest=points[1];result.Pelvis=points[2];result.LeftFoot=points[3];result.RightFoot=points[4];result.BonesValid=true;
        }catch(IOException){}
        return result;
    }
}
