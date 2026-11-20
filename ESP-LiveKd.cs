using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

sealed class PlayerPoint {
    public ulong Actor;
    public double[] Position;
    public double Distance;
    public double Health=double.NaN,MaxHealth=double.NaN,Armor=double.NaN,MaxArmor=double.NaN;
    public TargetData Target=new TargetData();
}
sealed class TimerResolution:IDisposable {
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
    public bool Active {get;private set;}
    public void SetActive(bool wanted){if(wanted==Active)return;if(wanted)Active=timeBeginPeriod(1)==0;else{timeEndPeriod(1);Active=false;}}
    public void Dispose(){SetActive(false);}
}
static class HudLayout {
    public static RectangleF Radar(int width,int height,int radius,int gap) {
        float size=Math.Min(2*(Math.Max(60,Math.Min(140,radius))+16),Math.Min(width-36,height-140));
        if(size<100)return RectangleF.Empty;
        float y=Math.Max(70,Math.Min(height-gap-size,height-size-18));
        return new RectangleF(18,y,size,size);
    }
}
static class CameraDiscovery {
    static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value)&&Math.Abs(value)<100000000;}
    static byte[] Block(Func<ulong,int,byte[]> read,ulong address,int size){byte[] bytes=read(address,size);if(bytes==null||bytes.Length<size)throw new System.IO.IOException("Partial camera read");return bytes;}
    public static ulong FindUnique(IEnumerable<ulong> actors,ulong controller,Func<ulong,int,byte[]> read,Func<bool> stopping) {
        if(!Memory.Valid(controller))return 0;
        ulong found=0;var seen=new HashSet<ulong>();var clock=Stopwatch.StartNew();
        foreach(ulong actor in actors) {
            if(stopping()||clock.ElapsedMilliseconds>250||seen.Count>=8192)return 0;
            if(!Memory.Valid(actor)||!seen.Add(actor))continue;
            try {
                if(BitConverter.ToUInt64(Block(read,actor+0x400,8),0)!=controller)continue;
                byte[] pov=Block(read,actor+0xE00,0x64);
                float fov=BitConverter.ToSingle(pov,0x60);if(!(fov>10&&fov<170))continue;
                ulong root=BitConverter.ToUInt64(Block(read,actor+0x248,8),0);if(!Memory.Valid(root))continue;
                byte[] origin=Block(read,root+0x280,24);
                bool valid=true;double delta=0;
                for(int i=0;i<3;i++) {
                    double location=BitConverter.ToDouble(pov,0x10+i*8),relative=BitConverter.ToDouble(origin,i*8),rotation=BitConverter.ToDouble(pov,0x38+i*8);
                    valid&=Finite(location)&&Finite(relative)&&Finite(rotation)&&Math.Abs(rotation)<=360;
                    delta+=Math.Abs(location-relative);
                }
                if(!valid||delta>100)continue;
                if(found!=0)return 0;found=actor;
            }catch(System.IO.IOException){}
        }
        return found;
    }
}
sealed class EspFrame {
    public ulong World,LocalPawn;
    public string TargetDataStatus="No validated target profile";
    public bool Valid;
    public long CapturedAt=Stopwatch.GetTimestamp();
    public DateTime AcquiredUtc=DateTime.UtcNow;
    public bool IsFresh(long now) {long elapsed=now-CapturedAt;return Valid&&elapsed>=0&&elapsed<=Stopwatch.Frequency/2;}
    public string Status="Waiting for data";
    public double[] Location,Rotation;
    public float Fov;
    public float AspectRatio,DesiredFov;
    public double[] CameraRootLocation,CameraRootRotation;
    public List<PlayerPoint> Players=new List<PlayerPoint>();
}
// Uses the same layout already used by Inventory and the local FOV lookup.
// Each array header is rechecked before its pointers can affect discovery.
static class SceneDiscovery {
    static byte[] Block(Func<ulong,int,byte[]> read,ulong address,int size){
        if(!Memory.Valid(address))throw new System.IO.IOException("Invalid scene address");
        var bytes=read(address,size);if(bytes==null||bytes.Length!=size)throw new System.IO.IOException("Incomplete scene read");return bytes;
    }
    static ulong Pointer(Func<ulong,int,byte[]> read,ulong address){return BitConverter.ToUInt64(Block(read,address,8),0);}
    static int Int32(Func<ulong,int,byte[]> read,ulong address){return BitConverter.ToInt32(Block(read,address,4),0);}
    static List<ulong> Pointers(Func<ulong,int,byte[]> read,ulong address,int limit,bool truncate=false){
        var header=Block(read,address,16);ulong data=BitConverter.ToUInt64(header,0);
        int count=BitConverter.ToInt32(header,8),capacity=BitConverter.ToInt32(header,12);
        if(count<0||capacity<count||capacity>100000||!truncate&&count>limit||count>0&&!Memory.Valid(data))throw new System.IO.IOException("Array de escena incoherente");
        int take=Math.Min(count,limit);var bytes=take==0?new byte[0]:Block(read,data,take*8);
        var after=Block(read,address,16);for(int i=0;i<16;i++)if(header[i]!=after[i])throw new System.IO.IOException("Array de escena cambio durante lectura");
        var result=new List<ulong>();for(int i=0;i<take;i++){ulong pointer=BitConverter.ToUInt64(bytes,i*8);if(Memory.Valid(pointer))result.Add(pointer);}return result;
    }
    public static List<ulong> CollectActors(ulong world,Func<ulong,int,byte[]> read,Func<bool> stopping){
        var actors=new List<ulong>();if(stopping())return actors;
        var levels=new List<ulong>();var seenLevels=new HashSet<ulong>();
        try {
            byte[] header=Block(read,world+0x328,16);ulong data=BitConverter.ToUInt64(header,0);int count=BitConverter.ToInt32(header,8),capacity=BitConverter.ToInt32(header,12);
            if(Memory.Valid(data)&&count>0&&count<=8&&capacity>=count&&capacity<=8)for(int i=0;i<count;i++){
                ulong collection=data+(ulong)i*0x78;
                ulong persistent=Pointer(read,collection+0x20);if(Memory.Valid(persistent)&&seenLevels.Add(persistent))levels.Add(persistent);
                ulong sparse=collection+0x28,sparseData=Pointer(read,sparse);int sparseCount=Int32(read,sparse+8);
                if(!Memory.Valid(sparseData)||sparseCount<=0||sparseCount>128)continue;
                byte[] bits=Block(read,sparse+0x10,((sparseCount+31)/32)*4);
                for(int n=0;n<sparseCount;n++)if((bits[n>>3]&(1<<(n&7)))!=0){ulong level=Pointer(read,sparseData+(ulong)n*0x10);if(Memory.Valid(level)&&seenLevels.Add(level))levels.Add(level);}
            }
        } catch(System.IO.IOException) {}
        if(levels.Count==0)try{ulong main=Reverse64(Pointer(read,world+0x170)^0xBFDE9CA800000000UL);if(Memory.Valid(main)&&seenLevels.Add(main))levels.Add(main);}catch(System.IO.IOException){}
        if(levels.Count==0)try{ulong legacy=Reverse64(Pointer(read,world+0x140)^0x7DD776A600000000UL);if(Memory.Valid(legacy)&&seenLevels.Add(legacy))levels.Add(legacy);}catch(System.IO.IOException){}
        if(levels.Count==0)throw new System.IO.IOException("Waiting for persistent level");
        var seen=new HashSet<ulong>();
        foreach(ulong level in levels){
            if(stopping())return new List<ulong>();
            try{foreach(ulong actor in Pointers(read,level+0x110,8192,true)){
                if(stopping())return new List<ulong>();if(seen.Add(actor))actors.Add(actor);if(actors.Count>=8192)return actors;
            }}catch(System.IO.IOException){}
        }
        return actors;
    }
    public static ulong FindLocalController(ulong world,List<ulong> actors,List<ulong> named,Func<ulong,int,byte[]> read){
        var present=new HashSet<ulong>(actors);var localControllers=new HashSet<ulong>();
        try {
            ulong localPlayer=Pointer(read,Process.BaseAddress+0x109D3010);
            ulong directController=Pointer(read,localPlayer+0xA0);
            if(Memory.Valid(localPlayer)&&Memory.Valid(directController))return directController;
        } catch(System.IO.IOException) {}
        bool localChainAvailable=false;
        try{
            ulong instance=Pointer(read,world+0x398);if(!Memory.Valid(instance))throw new System.IO.IOException("Game instance unavailable");
            var locals=Pointers(read,instance+0xf8,4);localChainAvailable=true;
            var owners=new List<ulong>();
            foreach(ulong local in locals){
                ulong owner=Pointer(read,local+0xa0);
                if(owner!=Pointer(read,local+0xa0))return 0;
                owners.Add(owner);
                if(present.Contains(owner))localControllers.Add(owner);
            }
            if(Pointer(read,world+0x398)!=instance)return 0;
            var after=Pointers(read,instance+0xf8,4);if(after.Count!=locals.Count)return 0;
            for(int i=0;i<locals.Count;i++)if(after[i]!=locals[i]||Pointer(read,locals[i]+0xa0)!=owners[i])return 0;
            if(localControllers.Count==1)foreach(ulong value in localControllers)return value;
            return 0;
        }catch(System.IO.IOException){
            if(localChainAvailable)return 0;
            var fallback=new HashSet<ulong>();foreach(ulong value in named)if(present.Contains(value))fallback.Add(value);
            if(fallback.Count==1)foreach(ulong value in fallback)return value;
            return 0;
        }
    }
    static ulong Reverse64(ulong v){return ((v&0xFFUL)<<56)|((v&0xFF00UL)<<40)|((v&0xFF0000UL)<<24)|((v&0xFF000000UL)<<8)|((v>>8)&0xFF000000UL)|((v>>24)&0xFF0000UL)|((v>>40)&0xFF00UL)|(v>>56);}
}
sealed class EspReader {
    readonly TargetDataReader targetReader=new TargetDataReader(TargetReadProfile.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"perfil-objetivos.json")),Memory.Bytes);
    public volatile bool Stop;
    public volatile bool Active=true;
    public volatile int ReadHz=60;
    public long ReadCount;
    public double ReadMilliseconds,DiscoveryMilliseconds;
    public volatile EspFrame Frame=new EspFrame();
    public volatile string Discovery="Not scanned";
    public volatile string LastError="";
    public int AxisConstraint=-1;
    ulong world,camera,controller,localPawn;
    readonly List<ulong> players=new List<ulong>();
    readonly HashSet<ulong> states=new HashSet<ulong>();
    static bool Finite(double d) {return !double.IsNaN(d)&&!double.IsInfinity(d)&&Math.Abs(d)<100000000;}
    static bool ValidStat(double value,double maximum){return Finite(value)&&Finite(maximum)&&value>=0&&maximum>0&&maximum<=10000&&value<=maximum+1;}
    static double[] Triple(byte[] b,int o) {return new double[]{BitConverter.ToDouble(b,o),BitConverter.ToDouble(b,o+8),BitConverter.ToDouble(b,o+16)};}
    static ulong Chain(ulong first,params ulong[] offsets){ulong value=Memory.Read<ulong>(Process.BaseAddress+first);foreach(ulong offset in offsets){if(!Memory.Valid(value))return 0;value=Memory.Read<ulong>(value+offset);}return Memory.Valid(value)?value:0;}
    static ulong ResolveWorld(){
        ulong value=Chain(0x109D3010,0xA0,0x248,0x1B8);if(Memory.Valid(value))return value;
        value=Chain(0x108FEEC0,0xA0,0x250,0x160);if(Memory.Valid(value))return value;
        value=Chain(0x109BB1C0,0xA0,0x248,0x148);if(Memory.Valid(value))return value;
        ulong engine=Memory.Read<ulong>(Process.BaseAddress+0x104673E8);if(!Memory.Valid(engine))return 0;
        for(ulong offset=8;offset<0x1800;offset+=8){try{ulong viewport=Memory.Read<ulong>(engine+offset);if(!Memory.Valid(viewport))continue;value=Memory.Read<ulong>(viewport+0x140);if(Memory.Valid(value))return value;}catch(System.IO.IOException){}}
        return 0;
    }
    void Refresh() {
        targetReader.Bind(Process.BaseAddress);
        ulong nextWorld=ResolveWorld();
        if(!Memory.Valid(nextWorld)) throw new Exception("Waiting for world");
        world=nextWorld; camera=controller=localPawn=0;AxisConstraint=-1; players.Clear();states.Clear();
        var candidates=new List<ulong>();
        var actors=SceneDiscovery.CollectActors(world,Memory.Bytes,()=>Stop);
        var cameras=new List<ulong>();var controllers=new List<ulong>();
        foreach(ulong actor in actors) {
            if(Stop)return;
            try {
                string name=GNames.GetActorName(actor);
                if(name=="PlayerCameraManager") cameras.Add(actor);
                else if(name.StartsWith("BP_PioneerPlayerController_C",StringComparison.Ordinal)) controllers.Add(actor);
                else if(name.StartsWith("BP_PioneerPlayerState_C",StringComparison.Ordinal)) states.Add(actor);
                else if(name.StartsWith("BP_PioneerCharacter_C",StringComparison.Ordinal)) candidates.Add(actor);
            } catch(System.IO.IOException) {}
        }
        Discovery="Actors="+actors.Count+" (loaded levels); cameras="+cameras.Count+"; controllers="+controllers.Count+"; characters="+candidates.Count+"; states="+states.Count;
        controller=SceneDiscovery.FindLocalController(world,actors,controllers,Memory.Bytes);
        Discovery+="; local controller="+(controller!=0?"1":"0");
        if(controller==0) {ProcessDiagnosticRequest();return;}
        camera=CameraDiscovery.FindUnique(actors,controller,Memory.Bytes,()=>Stop);
        Discovery+="; validated camera="+(camera!=0?"1":"0")+(cameras.Count==0&&camera!=0?" (owner)":"")+(camera==0?" (controller fallback)":"");
        if(Memory.Valid(controller)) localPawn=Memory.Read<ulong>(controller+0x408);
        try {
            byte[] collections=Memory.Bytes(world+0x328,16);ulong collectionData=BitConverter.ToUInt64(collections,0);int collectionCount=BitConverter.ToInt32(collections,8);
            if(Memory.Valid(collectionData)&&collectionCount>0&&collectionCount<=8)for(int c=0;c<collectionCount;c++){
                ulong gameState=Memory.Read<ulong>(collectionData+(ulong)c*0x78+8);if(!Memory.Valid(gameState))continue;
                foreach(ulong state in PointerArray(gameState+0x428,512)){
                    states.Add(state);ulong pawn=Memory.Read<ulong>(state+0x440);
                    if(Memory.Valid(pawn)&&pawn!=localPawn&&!players.Contains(pawn))players.Add(pawn);
                }
            }
        } catch(System.IO.IOException) {}
        foreach(ulong actor in candidates) {
            if(Stop) return;
            if(actor==localPawn) continue;
            try {if(states.Contains(Memory.Read<ulong>(actor+0x3F0))) players.Add(actor);} catch(System.IO.IOException) {}
        }
        AxisConstraint=-1;
        try {
            ulong instance=Memory.Read<ulong>(world+0x398);
            foreach(ulong local in PointerArray(instance+0xF8,4)) {
                if(Memory.Read<ulong>(local+0xA0)==controller) {
                    int axis=Memory.Bytes(local+0x228,1)[0];
                    if(axis<=2)AxisConstraint=axis;
                }
            }
        }catch(System.IO.IOException){}
        ProcessDiagnosticRequest();
    }
    void ProcessDiagnosticRequest() {
        string request=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"calibrar-practica.request");
        if(System.IO.File.Exists(request)) {
            try {System.IO.File.Delete(request);Inventory();}
            catch(Exception error) {
                LastError=error.ToString();
                try {System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostico-escena.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new {timestampUtc=DateTime.UtcNow.ToString("o"),status="failed",error=error.ToString()}));}
                catch(System.IO.IOException){}catch(UnauthorizedAccessException){}
            }
        }
    }
    static List<ulong> PointerArray(ulong address,int limit) {
        byte[] header=Memory.Bytes(address,16);
        int count=BitConverter.ToInt32(header,8),max=BitConverter.ToInt32(header,12);
        if(count<0||count>max||max>100000||count>limit)throw new System.IO.IOException("Array fuera del limite de diagnostico.");
        var values=new List<ulong>();if(count==0)return values;
        byte[] bytes=Memory.Bytes(BitConverter.ToUInt64(header,0),count*8);
        for(int i=0;i<count;i++){ulong value=BitConverter.ToUInt64(bytes,i*8);if(Memory.Valid(value))values.Add(value);}
        return values;
    }
    void Inventory() {
        var clock=Stopwatch.StartNew();
        var cameraPovs=new List<object>();
        string cameraScanError="";
        try {
            double[] reference=Triple(Memory.Bytes(camera+0xE00,24),0);
            for(int chunk=0;chunk<0x4800&&!Stop&&clock.ElapsedMilliseconds<=1200;chunk+=0x400) {
              byte[] bytes;
              try{bytes=Memory.Bytes(camera+(ulong)chunk,0x500);}catch(System.IO.IOException error){cameraScanError=error.Message;continue;}
              for(int locationOffset=8;locationOffset<0x408&&locationOffset+0x98<bytes.Length;locationOffset+=8) {
                double[] location=Triple(bytes,locationOffset);double delta=0;for(int k=0;k<3;k++)delta+=Math.Abs(location[k]-reference[k]);
                if(!Finite(delta)||delta>100)continue;
                int povOffset=locationOffset-8;float fov=BitConverter.ToSingle(bytes,povOffset+0x50);
                if(fov<=10||fov>=170)continue;
                cameraPovs.Add(new {offset="0x"+(chunk+povOffset).ToString("X"),location,rotation=Triple(bytes,povOffset+0x30),fov,desiredFov=BitConverter.ToSingle(bytes,povOffset+0x64),aspect=BitConverter.ToSingle(bytes,povOffset+0x94),delta});
                if(cameraPovs.Count>=16)break;
              }
              if(cameraPovs.Count>=16)break;
            }
        }catch(System.IO.IOException error){cameraScanError=error.Message;}
        var transforms=new List<object>();
        foreach(ulong actor in new[]{camera,localPawn})if(!Stop&&clock.ElapsedMilliseconds<=1200&&Memory.Valid(actor))try{transforms.Add(InspectTransform(actor,true));}catch(System.IO.IOException){}
        var levels=new HashSet<ulong>{Memory.Valid(world)?SceneDiscoveryPersistentLevel(world):0};
        string levelsError="";
        try {foreach(ulong level in PointerArray(world+0x288,128))levels.Add(level);}catch(System.IO.IOException error){levelsError=error.Message;}
        var items=new List<object>();var seen=new HashSet<ulong>();
        int failed=0;bool limited=false;
        foreach(ulong level in levels) {
            if(Stop||clock.ElapsedMilliseconds>1200){limited=true;break;}
            try {
                foreach(ulong actor in PointerArray(level+0x110,8192)) {
                    if(Stop||clock.ElapsedMilliseconds>1200||seen.Count>=2048){limited=true;break;}
                    if(!seen.Add(actor))continue;
                    try {
                        string name="",nameError="";
                        try{name=GNames.GetActorName(actor);}catch(System.IO.IOException error){nameError=error.Message;failed++;}
                        bool cameraOwnerMatch=false,povPlausible=false;
                        double[] povLocation=null,povRotation=null;float povFov=0;
                        if(Memory.Valid(controller))try {
                            cameraOwnerMatch=Memory.Read<ulong>(actor+0x438)==controller;
                            if(cameraOwnerMatch) {
                                byte[] pov=Memory.Bytes(actor+0xDE0,0x54);
                                povLocation=Triple(pov,8);povRotation=Triple(pov,0x30);povFov=BitConverter.ToSingle(pov,0x50);
                                povPlausible=povFov>10&&povFov<170;
                                foreach(double d in povLocation)povPlausible&=Finite(d);
                                foreach(double d in povRotation)povPlausible&=Finite(d)&&Math.Abs(d)<=360;
                            }
                        }catch(System.IO.IOException){}
                        ulong root=0,parent=0;double[] position=null;
                        try {root=Memory.Read<ulong>(actor+0x248);if(Memory.Valid(root)){parent=Memory.Read<ulong>(root+0x1F0);position=Triple(Memory.Bytes(root+0x280,24),0);}}
                        catch(System.IO.IOException){failed++;}
                        items.Add(new {actor=actor.ToString("X"),name,nameError,root=root.ToString("X"),parent=parent.ToString("X"),relativeLocation=position,cameraOwnerMatch,povPlausible,povLocation,povRotation,povFov});
                    }catch(System.IO.IOException){failed++;}
                }
            }catch(System.IO.IOException){failed++;}
        }
        var report=new {timestampUtc=DateTime.UtcNow.ToString("o"),levels=levels.Count,levelsError,limited,failed,elapsedMs=clock.ElapsedMilliseconds,axisConstraint=AxisConstraint,cameraScanError,cameraPovs,transforms,actors=items};
        System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostico-escena.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(report));
    }
    static ulong SceneDiscoveryPersistentLevel(ulong value){ulong v=Memory.Read<ulong>(value+0x118)^0x2BFF037800000000UL;return ((v&0xFFUL)<<56)|((v&0xFF00UL)<<40)|((v&0xFF0000UL)<<24)|((v&0xFF000000UL)<<8)|((v>>8)&0xFF000000UL)|((v>>24)&0xFF0000UL)|((v>>40)&0xFF00UL)|(v>>56);}
    object InspectTransform(ulong actor,bool requireMatch) {
        ulong root=Memory.Read<ulong>(actor+0x248);
        byte[] block=Memory.Bytes(root,0x700);double[] relative=Triple(block,0x280);
        var candidates=new List<object>();
        for(int offset=0x80;offset+0x60<=block.Length;offset+=8) {
            double q0=BitConverter.ToDouble(block,offset),q1=BitConverter.ToDouble(block,offset+8),q2=BitConverter.ToDouble(block,offset+16),q3=BitConverter.ToDouble(block,offset+24);
            double norm=q0*q0+q1*q1+q2*q2+q3*q3;
            if(!Finite(norm)||Math.Abs(norm-1)>.001)continue;
            double[] translation=Triple(block,offset+0x20),scale=Triple(block,offset+0x40);
            bool good=true;for(int k=0;k<3;k++)good&=Finite(translation[k])&&Finite(scale[k])&&Math.Abs(scale[k])>.001&&Math.Abs(scale[k])<100;
            if(!good)continue;
            double delta=0;for(int k=0;k<3;k++)delta+=Math.Abs(translation[k]-relative[k]);
            if(requireMatch&&delta>.01)continue;
            if(!requireMatch&&Math.Abs(translation[0])+Math.Abs(translation[1])+Math.Abs(translation[2])<10000)continue;
            candidates.Add(new {offset="0x"+offset.ToString("X"),translation,scale,quaternion=new[]{q0,q1,q2,q3},delta});
            if(candidates.Count>=16)break;
        }
        return new {actor="0x"+actor.ToString("X"),root="0x"+root.ToString("X"),relative,candidates};
    }
    EspFrame ReadFrame() {
        var frame=new EspFrame();
        frame.TargetDataStatus=targetReader.Status;
        if(!Memory.Valid(controller)) {frame.Status="Waiting for local controller";return frame;}
        localPawn=Memory.Read<ulong>(controller+0x408);
        if(!Memory.Valid(localPawn)) {frame.Status="Waiting for local character";return frame;}
        frame.World=world;frame.LocalPawn=localPawn;
        if(Memory.Valid(camera)){
            byte[] pov=Memory.Bytes(camera+0xDF0+0x10,0xB0);
            frame.Location=Triple(pov,0x10);frame.Rotation=Triple(pov,0x38);frame.Fov=BitConverter.ToSingle(pov,0x60);
            frame.AspectRatio=BitConverter.ToSingle(pov,0x94);frame.DesiredFov=BitConverter.ToSingle(pov,0x64);
        } else {
            ulong localRoot=Memory.Read<ulong>(localPawn+0x248);double[] origin;
            if(!Memory.Valid(localRoot)||!Projection.TryWorldPosition(Memory.Bytes(localRoot+0x390,0x60),out origin)){frame.Status="Waiting for local transform";return frame;}
            frame.Location=new[]{origin[0],origin[1],origin[2]+70.0};frame.Rotation=Triple(Memory.Bytes(controller+0x448,24),0);frame.Fov=90;frame.DesiredFov=90;frame.AspectRatio=0;
        }
        if(!(frame.Fov>10 && frame.Fov<170)) {frame.Status="Camara sin FOV valido";return frame;}
        foreach(double d in frame.Location) if(!Finite(d)) {frame.Status="Camara sin posicion valida";return frame;}
        foreach(double d in frame.Rotation) if(!Finite(d)||Math.Abs(d)>360) {frame.Status="Camara sin rotacion valida";return frame;}
        if(Memory.Valid(camera))try {ulong root=Memory.Read<ulong>(camera+0x248);byte[] transform=Memory.Bytes(root+0x280,48);frame.CameraRootLocation=Triple(transform,0);frame.CameraRootRotation=Triple(transform,24);}catch(System.IO.IOException){}
        foreach(ulong actor in players) {
            if(Stop) return new EspFrame{Status="Cerrando"};
            if(actor==localPawn) continue;
            try {
                ulong root=Memory.Read<ulong>(actor+0x248);
                double[] pos;
                if(!Projection.TryWorldPosition(Memory.Bytes(root+0x390,0x60),out pos))continue;
                double dx=pos[0]-frame.Location[0],dy=pos[1]-frame.Location[1],dz=pos[2]-frame.Location[2];
                double distance=Math.Sqrt(dx*dx+dy*dy+dz*dz)/100;
                if(distance<2||distance>2000) continue;
                var player=new PlayerPoint{Actor=actor,Position=pos,Distance=distance,Target=targetReader.Read(actor,localPawn,world,Memory.Valid(camera)?camera:controller,pos)};
                try {
                    ulong healthComponent=Memory.Read<ulong>(actor+0xD98);
                    if(Memory.Valid(healthComponent)) {
                        double health=BitConverter.ToDouble(Memory.Bytes(healthComponent+0x708,8),0),maxHealth=BitConverter.ToDouble(Memory.Bytes(healthComponent+0x380,8),0);
                        double armor=BitConverter.ToDouble(Memory.Bytes(healthComponent+0x1D0,8),0),maxArmor=BitConverter.ToDouble(Memory.Bytes(healthComponent+0x1D8,8),0);
                        if(ValidStat(health,maxHealth)){player.Health=health;player.MaxHealth=maxHealth;}
                        if(ValidStat(armor,maxArmor)){player.Armor=armor;player.MaxArmor=maxArmor;}
                    }
                } catch(System.IO.IOException) {}
                frame.Players.Add(player);
            } catch(System.IO.IOException) {}
        }
        frame.Valid=true; frame.Status="ESP: "+frame.Players.Count+" players read | F10: close";
        return frame;
    }
    public void Run() {
        DateTime refresh=DateTime.MinValue;
        bool retryDiscovery=true;
        while(!Stop) {
            if(retryDiscovery&&DateTime.UtcNow<refresh) {Thread.Sleep(33);continue;}
            var cycle=Stopwatch.StartNew();
            try {
                // Keep the last timestamped frame while discovering. IsFresh still
                // expires it normally; discovery errors publish an invalid frame.
                if(DateTime.UtcNow>=refresh) {var discoveryClock=Stopwatch.StartNew();Refresh();DiscoveryMilliseconds=discoveryClock.Elapsed.TotalMilliseconds;refresh=DateTime.UtcNow.AddMilliseconds(RediscoveryDelay(true));retryDiscovery=false;}
                var readClock=Stopwatch.StartNew();EspFrame next=ReadFrame();ReadMilliseconds=readClock.Elapsed.TotalMilliseconds;Frame=next;
                if(next.Valid){Interlocked.Increment(ref ReadCount);LastError="";}
                else {refresh=DateTime.UtcNow.AddMilliseconds(RediscoveryDelay(false));retryDiscovery=true;}
            } catch(Exception error) {LastError=error.ToString();Frame=new EspFrame{Status=error.Message};refresh=DateTime.UtcNow.AddMilliseconds(250);retryDiscovery=true;}
            Thread.Sleep(Active?RemainingDelay(ReadHz,cycle.Elapsed.TotalMilliseconds):250);
        }
    }
    public static int RemainingDelay(int hz,double elapsed){return Math.Max(1,(int)Math.Ceiling(1000.0/Math.Max(20,Math.Min(60,hz))-elapsed));}
    public static int RediscoveryDelay(bool valid){return valid?3000:250;}
}
sealed class EspWindow:Form {
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct PointNative {public int X,Y;}
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hwnd,ref PointNative point);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd,int id,uint flags,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd,int id);
    readonly EspReader reader;
    readonly TimerResolution timerResolution=new TimerResolution();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    readonly DateTime began=DateTime.UtcNow;
    readonly IntPtr gameWindow;
    readonly EspSettings settings=EspSettings.Load();
    EspMenu menu;
    Font overlayFont;
    Font detailFont;
    DateTime nextReport=DateTime.MinValue;
    bool gameFocused;
    bool insertRegistered;
    bool allyKeyRegistered,clearKeyRegistered;
    readonly TeamFilter teamFilter=new TeamFilter();
    string teamMessage="F6: mark teammate | F7: clear marks";
    int drawn;
    EspFrame paintedFrame;
    Rectangle paintedBounds;
    bool paintedFresh;
    long reportReadCount;
    long reportTick=Stopwatch.GetTimestamp();
    double actualReadHz;
    readonly List<RectangleF> occupiedCards=new List<RectangleF>();
    EspFrame displayFrame;
    public EspWindow(EspReader r,IntPtr hwnd) {
        reader=r;gameWindow=hwnd;
        reader.ReadHz=settings.RefreshHz;
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;
        BackColor=Color.Magenta;TransparencyKey=Color.Magenta;DoubleBuffered=true;
        UpdateFont();
        menu=new EspMenu(settings,()=>{UpdateFont();Invalidate();},()=>Close());
        timer.Interval=16;timer.Tick+=Tick;timer.Start();
        IntPtr ensured=Handle;
    }
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get {var p=base.CreateParams;p.ExStyle|=0x00080000|0x20|0x80|0x08000000;return p;}}
    protected override void OnHandleCreated(EventArgs e) {base.OnHandleCreated(e);if(!RegisterHotKey(Handle,1,0x4000,0x79)) throw new Exception("Could not register F10 as the close hotkey.");}
    protected override void OnHandleDestroyed(EventArgs e) {UnregisterHotKey(Handle,1);if(insertRegistered)UnregisterHotKey(Handle,2);if(allyKeyRegistered)UnregisterHotKey(Handle,3);if(clearKeyRegistered)UnregisterHotKey(Handle,4);insertRegistered=allyKeyRegistered=clearKeyRegistered=false;base.OnHandleDestroyed(e);}
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x0312) {
            if(m.WParam.ToInt32()==1) {Close();return;}
            if((m.WParam.ToInt32()==3||m.WParam.ToInt32()==4)&&menu!=null&&!menu.Visible&&GetForegroundWindow()==gameWindow){
                if(m.WParam.ToInt32()==3)teamMessage=teamFilter.ToggleAtCrosshair(reader.Frame,ClientSize.Width,ClientSize.Height,settings.FovAxis=="Vertical",settings.MaxDistance);
                else {teamFilter.Clear();teamMessage="Teammate marks cleared";}
                Invalidate();return;
            }
            if(m.WParam.ToInt32()==2&&menu!=null) {
                if(menu.Visible)menu.Hide();
                else if(GetForegroundWindow()==gameWindow){Rectangle area=Screen.FromHandle(gameWindow).WorkingArea;menu.Size=new Size(Math.Min(400,Math.Max(220,area.Width-40)),Math.Min(745,Math.Max(300,area.Height-40)));menu.Location=new Point(Math.Max(area.Left,Math.Min(Left+(Width-menu.Width)/2,area.Right-menu.Width)),Math.Max(area.Top,Math.Min(Top+(Height-menu.Height)/2,area.Bottom-menu.Height)));menu.Show();menu.Activate();}
                return;
            }
        }
        base.WndProc(ref m);
    }
    void UpdateFont(){if(overlayFont!=null)overlayFont.Dispose();if(detailFont!=null)detailFont.Dispose();overlayFont=new Font("Segoe UI",Math.Max(3f,settings.TextSize/2f),FontStyle.Bold,GraphicsUnit.Point);detailFont=new Font("Segoe UI",Math.Max(3f,(settings.TextSize-1)/2f),FontStyle.Regular,GraphicsUnit.Point);}
    void Tick(object sender,EventArgs e) {
        string stopRequest=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cerrar-esp.request");
        if(System.IO.File.Exists(stopRequest)){try{System.IO.File.Delete(stopRequest);}catch(System.IO.IOException){}catch(UnauthorizedAccessException){}Close();return;}
        Rect rect;
        if(DateTime.UtcNow-began>TimeSpan.FromHours(1)||!GetClientRect(gameWindow,out rect)) {Close();return;}
        PointNative origin=new PointNative();ClientToScreen(gameWindow,ref origin);
        int w=rect.Right-rect.Left,h=rect.Bottom-rect.Top;
        gameFocused=GetForegroundWindow()==gameWindow;
        timerResolution.SetActive(gameFocused&&settings.Enabled);
        reader.Active=gameFocused&&settings.Enabled;
        reader.ReadHz=settings.RefreshHz;
        timer.Interval=gameFocused?16:250;
        if(Visible!=gameFocused)Visible=gameFocused;
        bool wantsInsert=gameFocused||GetForegroundWindow()==menu.Handle;
        if(wantsInsert&&!insertRegistered)insertRegistered=RegisterHotKey(Handle,2,0x4000,0x2E);
        else if(!wantsInsert&&insertRegistered){UnregisterHotKey(Handle,2);insertRegistered=false;}
        if(gameFocused){
            if(!allyKeyRegistered)allyKeyRegistered=RegisterHotKey(Handle,3,0x4000,0x75);
            if(!clearKeyRegistered)clearKeyRegistered=RegisterHotKey(Handle,4,0x4000,0x76);
        }else{
            if(allyKeyRegistered){UnregisterHotKey(Handle,3);allyKeyRegistered=false;}
            if(clearKeyRegistered){UnregisterHotKey(Handle,4);clearKeyRegistered=false;}
        }
        if(w>0&&h>0) Bounds=new Rectangle(origin.X,origin.Y,w,h);
        displayFrame=reader.Frame;
        int previousMarks=teamFilter.Count;
        teamFilter.Update(displayFrame,Stopwatch.GetTimestamp());
        if(previousMarks>teamFilter.Count)teamMessage="Marcas actualizadas: cambio de escena o actores ausentes";
        if(gameFocused&&(!ReferenceEquals(paintedFrame,displayFrame)||paintedBounds!=Bounds||paintedFresh!=displayFrame.IsFresh(Stopwatch.GetTimestamp())))Invalidate();
        if(DateTime.UtcNow>=nextReport) {
            var f=displayFrame;
            long reportNow=Stopwatch.GetTimestamp(),reads=Interlocked.Read(ref reader.ReadCount);double seconds=(reportNow-reportTick)/(double)Stopwatch.Frequency;
            actualReadHz=seconds>0?(reads-reportReadCount)/seconds:0;reportTick=reportNow;reportReadCount=reads;
            var result=new {timestampUtc=DateTime.UtcNow.ToString("o"),acquiredUtc=f.AcquiredUtc.ToString("o"),frameAgeMs=(Stopwatch.GetTimestamp()-f.CapturedAt)*1000.0/Stopwatch.Frequency,dataFresh=f.IsFresh(Stopwatch.GetTimestamp()),backend="livekd",status=f.Status,discovery=reader.Discovery,lastError=reader.LastError,cameraValid=f.Valid,playersRead=f.Players.Count,boxesLastFrame=drawn,gameFocused=gameFocused,viewportWidth=w,viewportHeight=h,overlayEnabled=settings.Enabled,menuOpen=menu.Visible,radarEnabled=settings.Radar,axisConstraint=reader.AxisConstraint,cameraLocation=f.Location,cameraRotation=f.Rotation,cameraRootLocation=f.CameraRootLocation,cameraRootRotation=f.CameraRootRotation,fov=f.Fov,fovAxis=settings.FovAxis,aspectRatio=f.AspectRatio,desiredFov=f.DesiredFov,playerSamples=f.Players.GetRange(0,Math.Min(f.Players.Count,8)),targetReadHz=settings.RefreshHz,actualReadHz,readMilliseconds=reader.ReadMilliseconds,discoveryMilliseconds=reader.DiscoveryMilliseconds,memoryWritesPerformed=false,targetDataStatus=f.TargetDataStatus,manualTeammates=teamFilter.Count,hideAllies=settings.HideAllies,hideUnknownTeams=settings.HideUnknownTeams,teammateKeyAvailable=allyKeyRegistered,clearTeammateKeyAvailable=clearKeyRegistered,teamMessage,securitySettingsChanged=false};
            try {
                string report=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"estado-esp.json");
                string temporary=report+".tmp";
                System.IO.File.WriteAllText(temporary,new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result));
                if(System.IO.File.Exists(report)) System.IO.File.Replace(temporary,report,null);else System.IO.File.Move(temporary,report);
            } catch(System.IO.IOException) {} catch(UnauthorizedAccessException) {}
            nextReport=DateTime.UtcNow.AddSeconds(1);
        }
    }
    protected override void OnFormClosing(FormClosingEventArgs e) {
        timer.Stop();
        base.OnFormClosing(e);
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e); drawn=0;occupiedCards.Clear();
        if(!gameFocused) return;
        e.Graphics.TextRenderingHint=TextRenderingHint.SingleBitPerPixelGridFit;
        EspFrame frame=displayFrame??reader.Frame;
        paintedFrame=frame;paintedBounds=Bounds;
        paintedFresh=frame.IsFresh(Stopwatch.GetTimestamp());
        float labelY=20;
        if(settings.Status){var statusBounds=DrawLabel(e.Graphics,(frame.Valid&&!paintedFresh?"Waiting for fresh data":frame.Status)+" | "+frame.TargetDataStatus+" | "+actualReadHz.ToString("F0")+" Hz | DELETE: menu",20,20);occupiedCards.Add(statusBounds);labelY=statusBounds.Bottom+8;}
        if(settings.Status)occupiedCards.Add(DrawLabel(e.Graphics,teamMessage+" | marcados: "+teamFilter.Count+(!allyKeyRegistered||!clearKeyRegistered?" | Tecla ocupada":""),20,labelY));
        if(!settings.Enabled)return;
        if(!frame.IsFresh(Stopwatch.GetTimestamp())) return;
        if(settings.Radar){DrawRadar(e.Graphics,frame);occupiedCards.Add(RadarBounds());}
        using(var outline=new Pen(Color.FromArgb(5,15,26),settings.BoxThickness+2f)) using(var pen=new Pen(settings.Color,settings.BoxThickness)) {
            foreach(PlayerPoint player in frame.Players) {
                if(!teamFilter.Show(player,settings.HideAllies,settings.HideUnknownTeams))continue;
                if(player.Distance>settings.MaxDistance)continue;
                RectangleF box;
                if(!PlayerBoxGeometry.TryBox(player,frame,ClientSize.Width,ClientSize.Height,settings.FovAxis=="Vertical",out box)) {
                    continue;
                }
                if(settings.Skeleton&&player.Target.BonesValid)DrawPose(e.Graphics,pen,player.Target,frame);
                string stats=PlayerStats(player);
                if(settings.Distances)stats+=(stats.Length>0?" | ":"")+player.Distance.ToString("F0")+" m";
                if(stats.Length>0)DrawCard(e.Graphics,stats,box);
                drawn++;
            }
        }
    }
    static string VisibilityLabel(TargetVisibility value){return value==TargetVisibility.Clear?"head LOS":value==TargetVisibility.Occluded?"head occluded":"LOS ?";}
    static bool ValidStat(double value,double maximum){return !double.IsNaN(value)&&!double.IsInfinity(value)&&!double.IsNaN(maximum)&&!double.IsInfinity(maximum)&&value>=0&&maximum>0&&maximum<=10000&&value<=maximum+1;}
    static string PlayerStats(PlayerPoint player) {
        var parts=new List<string>();
        if(ValidStat(player.Health,player.MaxHealth))parts.Add("HP "+player.Health.ToString("F0")+"/"+player.MaxHealth.ToString("F0"));
        if(ValidStat(player.Armor,player.MaxArmor))parts.Add("Armor "+player.Armor.ToString("F0")+"/"+player.MaxArmor.ToString("F0"));
        return string.Join(" | ",parts.ToArray());
    }
    void DrawPose(Graphics g,Pen pen,TargetData t,EspFrame frame){
        var points=new[]{t.Head,t.Chest,t.Pelvis,t.LeftFoot,t.RightFoot};int[] from={0,1,2,2},to={1,2,3,4};
        for(int i=0;i<from.Length;i++){
            double x1,y1,x2,y2;if(!ProjectPoint(points[from[i]],frame,out x1,out y1)||!ProjectPoint(points[to[i]],frame,out x2,out y2))continue;
            if(x1<0||x1>ClientSize.Width||x2<0||x2>ClientSize.Width||y1<0||y1>ClientSize.Height||y2<0||y2>ClientSize.Height)continue;
            g.DrawLine(pen,(float)x1,(float)y1,(float)x2,(float)y2);
        }
    }
    bool ProjectPoint(double[] point,EspFrame frame,out double x,out double y) {
        if(settings.FovAxis=="Vertical")return Projection.ProjectVertical(point,frame.Location,frame.Rotation,frame.Fov,ClientSize.Width,ClientSize.Height,out x,out y);
        return Projection.Project(point,frame.Location,frame.Rotation,frame.Fov,ClientSize.Width,ClientSize.Height,out x,out y);
    }
    RectangleF DrawLabel(Graphics graphics,string text,float x,float y) {
        SizeF size=graphics.MeasureString(text,overlayFont);
        x=Math.Max(0,Math.Min(x,ClientSize.Width-size.Width-10));y=Math.Max(0,Math.Min(y,ClientSize.Height-size.Height-8));
        using(var background=new SolidBrush(Color.FromArgb(18,22,29)))graphics.FillRectangle(background,x-3,y-2,size.Width+8,size.Height+4);
        graphics.DrawString(text,overlayFont,Brushes.White,x,y);
        return new RectangleF(x-3,y-2,size.Width+8,size.Height+4);
    }
    void DrawBox(Graphics graphics,Pen pen,RectangleF b) {
        if(settings.BoxStyle=="Completa"){graphics.DrawRectangle(pen,b.X,b.Y,b.Width,b.Height);return;}
        float a=Math.Min(b.Width*.32f,25),t=Math.Min(b.Height*.22f,29);
        if(settings.BoxStyle=="Laterales")t=b.Height;
        foreach(float x in new[]{b.Left,b.Right})foreach(float y in new[]{b.Top,b.Bottom}) {
            graphics.DrawLine(pen,x,y,x+(x==b.Left?a:-a),y);
            graphics.DrawLine(pen,x,y,x,y+(y==b.Top?t:-t));
        }
    }
    void DrawCard(Graphics graphics,string text,RectangleF box) {
        SizeF textSize=graphics.MeasureString(text,overlayFont);
        float w=textSize.Width+8,h=textSize.Height+4;
        if(w>ClientSize.Width-6||h>ClientSize.Height-6)return;
        float cx=box.Left+box.Width/2;
        var options=new[]{new PointF(cx-w/2,box.Top-h-10),new PointF(cx-w/2,box.Bottom+12),new PointF(box.Left-w-12,box.Top),new PointF(box.Right+12,box.Top),new PointF(cx-w/2,box.Top-2*h-24)};
        RectangleF chosen=RectangleF.Empty;
        foreach(var p in options) {
            var r=new RectangleF(Math.Max(3,Math.Min(p.X,ClientSize.Width-w-3)),Math.Max(3,Math.Min(p.Y,ClientSize.Height-h-3)),w,h);
            bool collision=false;
            foreach(var occupied in occupiedCards){var padded=occupied;padded.Inflate(7,7);if(padded.IntersectsWith(r)){collision=true;break;}}
            if(!collision){chosen=r;break;}
        }
        if(chosen.IsEmpty)return;
        occupiedCards.Add(chosen);
        using(var background=new SolidBrush(Color.FromArgb(5,15,26)))graphics.FillRectangle(background,chosen);
        graphics.DrawString(text,overlayFont,Brushes.White,chosen.X+4,chosen.Y+2);
    }
    void DrawEdgeIndicator(Graphics graphics,PlayerPoint player,EspFrame frame) {
        double x,y;
        if(!Projection.EdgeMarker(player.Position,frame.Location,frame.Rotation,ClientSize.Width,ClientSize.Height,60,out x,out y))return;
        double angle=Math.Atan2(y-ClientSize.Height/2.0,x-ClientSize.Width/2.0);
        float ux=(float)Math.Cos(angle),uy=(float)Math.Sin(angle),cx=(float)x,cy=(float)y;
        var arrow=new[]{new PointF(cx+ux*10,cy+uy*10),new PointF(cx-ux*8-uy*6,cy-uy*8+ux*6),new PointF(cx-ux*4,cy-uy*4),new PointF(cx-ux*8+uy*6,cy-uy*8-ux*6)};
        foreach(var occupied in occupiedCards)if(occupied.IntersectsWith(new RectangleF(cx-12,cy-12,24,24)))return;
        using(var brush=new SolidBrush(Color.FromArgb(255,104,126)))graphics.FillPolygon(brush,arrow);
        using(var dark=new Pen(Color.FromArgb(5,15,26),1))graphics.DrawPolygon(dark,arrow);
        occupiedCards.Add(new RectangleF(cx-12,cy-12,24,24));
        if(!settings.Distances)return;
        string text=player.Distance.ToString("F0")+" m";
        SizeF size=graphics.MeasureString(text,detailFont);
        float separation=22+Math.Abs(ux)*(size.Width/2+3)+Math.Abs(uy)*(size.Height/2+2);
        float tx=Math.Max(5,Math.Min(cx-ux*separation-size.Width/2,ClientSize.Width-size.Width-5));
        float ty=Math.Max(5,Math.Min(cy-uy*separation-size.Height/2,ClientSize.Height-size.Height-5));
        var label=new RectangleF(tx-3,ty-2,size.Width+6,size.Height+4);
        foreach(var occupied in occupiedCards)if(occupied.IntersectsWith(label))return;
        occupiedCards.Add(label);
        using(var background=new SolidBrush(Color.FromArgb(5,15,26)))graphics.FillRectangle(background,label);
        using(var brush=new SolidBrush(Color.FromArgb(255,104,126)))graphics.DrawString(text,detailFont,brush,tx,ty);
    }
    void DrawRadar(Graphics graphics,EspFrame frame) {
        var panel=RadarBounds();if(panel.IsEmpty)return;
        float radius=panel.Width/2-16,cx=panel.Left+panel.Width/2,cy=panel.Top+panel.Height/2;
        int radarRange=settings.RadarRange;
        if(settings.RadarAutoZoom){bool near=false;foreach(var p in frame.Players)if(p.Distance<=100){near=true;break;}radarRange=near?100:200;}
        using(var background=new SolidBrush(Color.FromArgb(7,20,33)))graphics.FillRectangle(background,panel);
        if(settings.RadarBorder)using(var border=new Pen(Color.FromArgb(74,97,120))) {
            graphics.DrawRectangle(border,panel.X,panel.Y,panel.Width,panel.Height);
            for(int i=1;i<=3;i++){float r=radius*i/3;graphics.DrawEllipse(border,cx-r,cy-r,2*r,2*r);}
            graphics.DrawLine(border,cx-radius,cy,cx+radius,cy);graphics.DrawLine(border,cx,cy-radius,cx,cy+radius);
        }
        if(settings.RadarHeadingTick)graphics.FillPolygon(Brushes.White,new PointF[]{new PointF(cx,cy-7),new PointF(cx-5,cy+5),new PointF(cx+5,cy+5)});else graphics.FillEllipse(Brushes.White,cx-3,cy-3,6,6);
        using(var point=new SolidBrush(settings.Color))foreach(var player in frame.Players) {
            if(!teamFilter.Show(player,settings.HideAllies,settings.HideUnknownTeams))continue;
            double x,y;
            double yaw=settings.RadarNorthUp?0:frame.Rotation[1];
            if(player.Distance<=settings.MaxDistance&&Projection.Radar(player.Position,frame.Location,yaw,radarRange,radius,cx,cy,out x,out y)){
                graphics.FillEllipse(point,(float)x-4,(float)y-4,8,8);
                if(settings.RadarLabels){string label=player.Distance.ToString("F0")+"m";double dz=(player.Position[2]-frame.Location[2])/100.0;if(settings.RadarAltitude&&Math.Abs(dz)>=1)label+=" "+(dz>0?"▲":"▼")+Math.Abs(dz).ToString("F0")+"m";graphics.DrawString(label,detailFont,point,(float)x+5,(float)y-5);}
                else if(settings.RadarAltitude){double dz=(player.Position[2]-frame.Location[2])/100.0;if(Math.Abs(dz)>=1)graphics.DrawString((dz>0?"▲":"▼")+Math.Abs(dz).ToString("F0"),detailFont,point,(float)x+5,(float)y-5);}
            }
        }
        string title="RADAR · "+radarRange+" m · "+(settings.RadarNorthUp?"NORTH UP":"PLAYER UP");
        SizeF titleSize=graphics.MeasureString(title,detailFont);
        using(var muted=new SolidBrush(Color.FromArgb(164,192,211)))graphics.DrawString(title,detailFont,muted,cx-titleSize.Width/2,panel.Y+3);
    }
    RectangleF RadarBounds(){return HudLayout.Radar(ClientSize.Width,ClientSize.Height,settings.RadarRadius,settings.RadarBottomGap);}
    protected override void Dispose(bool disposing) {if(disposing){timer.Dispose();timerResolution.Dispose();if(menu!=null)menu.Dispose();if(overlayFont!=null)overlayFont.Dispose();if(detailFont!=null)detailFont.Dispose();}base.Dispose(disposing);}
}
public static class EspProgram {
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [STAThread] public static int Main(string[] args) {
        IntPtr context=IntPtr.Zero; EspReader reader=null;Thread thread=null;
        string folder=AppDomain.CurrentDomain.BaseDirectory;
        try {
            MemProcFsHeaderProbe.LoadRuntime(System.IO.Path.Combine(folder,"runtime","vmm.dll"));
            if(args.Length==1&&args[0]=="--verify") {Console.WriteLine("Exports ESP verificados; adquisicion no iniciada.");return 0;}
            if(!System.IO.File.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"livekd.dmp"))) throw new Exception("Falta LiveKd; iniciar mediante el lanzador.");
            context=MemProcFsHeaderProbe.Begin("livekd");if(context==IntPtr.Zero)throw new Exception("Could not initialize the reader.");
            Memory.Context=context;Memory.Pid=MemProcFsHeaderProbe.FindGame(context);
            if(Memory.Pid==0)throw new Exception("Juego no encontrado.");
            Process.BaseAddress=MemProcFsHeaderProbe.GameBase(context,Memory.Pid);
            IntPtr hwnd=System.Diagnostics.Process.GetProcessById((int)Memory.Pid).MainWindowHandle;
            if(hwnd==IntPtr.Zero)throw new Exception("Ventana del juego no encontrada.");
            SetProcessDPIAware(); Application.EnableVisualStyles();
            reader=new EspReader();thread=new Thread(reader.Run){IsBackground=true};thread.Start();
            using(var window=new EspWindow(reader,hwnd)) Application.Run(window);
            return 0;
        } catch(Exception error) {System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"error-esp.txt"),error.ToString());Console.WriteLine(error.Message);return 1;}
        finally {
            if(reader!=null)reader.Stop=true;
            if(thread!=null&&!thread.Join(3000)) {
                // End only this LiveKd-launched child; its acquisition parent
                // observes termination and can release LIVEKD. Never close a
                // context while the native reader still uses it.
                Environment.Exit(3);
            }
            if(context!=IntPtr.Zero)MemProcFsHeaderProbe.End(context);
        }
    }
}
