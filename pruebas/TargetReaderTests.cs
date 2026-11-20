using System;
using System.Collections.Generic;
using System.IO;
public static class TargetReaderTests {
    const ulong Image=0x100000,Actor=0x20000,Local=0x21000,OtherState=0x30000,OwnState=0x31000,Mesh=0x40000,Bones=0x50000,World=0x60000,Camera=0x70000;
    static int failures;
    static void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static void Expect(bool v,string message){if(!v)throw new Exception(message);}
    sealed class Fixture {
        public readonly Dictionary<ulong,byte> Bytes=new Dictionary<ulong,byte>();
        public readonly TargetReadProfile Profile=new TargetReadProfile{Enabled=true,PeTimestamp=123,ImageSize=500000,PawnStateOffset=40,TeamIdOffset=8,MeshOffset=48,BoneArrayOffset=100,ComponentTransformOffset=200,HeadIndex=0,ChestIndex=1,PelvisIndex=2,LeftFootIndex=3,RightFootIndex=4,VisibilitySource="CameraRaycast",WorldTimeOffset=32,VisibilityObserverOffset=64,VisibilityTimeOffset=72,HeadVisibilityOffset=80,ChestVisibilityOffset=81};
        public Func<ulong,int,byte[]> Source;
        public Fixture(){
            var dos=new byte[64];dos[0]=77;dos[1]=90;Array.Copy(BitConverter.GetBytes(128),0,dos,60,4);Put(Image,dos);
            var pe=new byte[84];Array.Copy(BitConverter.GetBytes(0x4550u),0,pe,0,4);Array.Copy(BitConverter.GetBytes((ushort)0x8664),0,pe,4,2);Array.Copy(BitConverter.GetBytes(123u),0,pe,8,4);Array.Copy(BitConverter.GetBytes((ushort)0x20b),0,pe,24,2);Array.Copy(BitConverter.GetBytes(500000u),0,pe,80,4);Put(Image+128,pe);
            Put(Actor+40,BitConverter.GetBytes(OtherState));Put(Local+40,BitConverter.GetBytes(OwnState));Put(OtherState+8,BitConverter.GetBytes(2));Put(OwnState+8,BitConverter.GetBytes(1));
            Put(Actor+48,BitConverter.GetBytes(Mesh));var header=new byte[16];Array.Copy(BitConverter.GetBytes(Bones),header,8);Array.Copy(BitConverter.GetBytes(5),0,header,8,4);Array.Copy(BitConverter.GetBytes(5),0,header,12,4);Put(Mesh+100,header);
            Put(Mesh+200,Transform(1000,0,0));Put(Bones,Transform(0,0,80));Put(Bones+96,Transform(0,0,20));Put(Bones+192,Transform(0,0,-10));Put(Bones+288,Transform(0,-10,-90));Put(Bones+384,Transform(0,10,-90));
            Put(World+32,BitConverter.GetBytes(10.1));Put(Actor+72,BitConverter.GetBytes(10.1));Put(Actor+64,BitConverter.GetBytes(Camera));Put(Actor+80,new byte[]{1,1});Source=Read;
        }
        public void Put(ulong p,byte[] bytes){for(int i=0;i<bytes.Length;i++)Bytes[p+(ulong)i]=bytes[i];}
        public byte[] Read(ulong p,int size){var b=new byte[size];for(int i=0;i<size;i++)if(!Bytes.TryGetValue(p+(ulong)i,out b[i]))throw new IOException("Synthetic partial read");return b;}
        public TargetDataReader Reader(){var r=new TargetDataReader(Profile,Source);Expect(r.Bind(Image),"Valid fixture not bound");return r;}
        public TargetData Data(){return Reader().Read(Actor,Local,World,Camera,new[]{1000.0,0,0});}
    }
    static byte[] Transform(double x,double y,double z){var b=new byte[96];Array.Copy(BitConverter.GetBytes(1.0),0,b,24,8);for(int i=0;i<3;i++){Array.Copy(BitConverter.GetBytes(new[]{x,y,z}[i]),0,b,32+i*8,8);Array.Copy(BitConverter.GetBytes(1.0),0,b,64+i*8,8);}return b;}
    public static int Main(){
        Check("Team classification works without a bone layout",()=>{var f=new Fixture();f.Profile.MeshOffset=-1;f.Profile.HeadIndex=-1;var r=new TargetDataReader(f.Profile,f.Source);Expect(r.Bind(Image),"Team-only profile rejected because bones unavailable");var d=r.Read(Actor,Local,World,Camera,new[]{1000.0,0,0});Expect(d.Relation==TargetRelation.Enemy&&!d.BonesValid,"Independent team data unavailable");});
        Check("Changing team during sample remains unknown",()=>{var f=new Fixture();int seen=0;f.Source=(p,n)=>{var b=f.Read(p,n);if(p==OtherState+8&&++seen>1)return BitConverter.GetBytes(1);return b;};Expect(f.Data().Relation==TargetRelation.Unknown,"Torn team read classified as enemy");});
        Check("Verified synthetic build reads enemy, visibility and world bones",()=>{var d=new Fixture().Data();Expect(d.Relation==TargetRelation.Enemy&&d.Visibility==TargetVisibility.Clear&&d.BonesValid&&d.Head[0]==1000&&d.Head[2]==80,"Metadata missing");});
        Check("Build mismatch never enables metadata",()=>{var f=new Fixture();f.Profile.PeTimestamp++;var r=new TargetDataReader(f.Profile,f.Source);Expect(!r.Bind(Image)&&!r.Read(Actor,Local,World,Camera,new[]{1000.0,0,0}).BonesValid,"Wrong build accepted");});
        Check("Disabled profile does not read memory",()=>{var f=new Fixture();f.Profile.Enabled=false;int calls=0;var r=new TargetDataReader(f.Profile,(a,n)=>{calls++;return f.Read(a,n);});Expect(!r.Bind(Image)&&calls==0,"Disabled reads memory");});
        Check("Failed rebind revokes previously enabled reader",()=>{var f=new Fixture();var r=f.Reader();f.Put(Image+136,BitConverter.GetBytes(456u));Expect(!r.Bind(Image)&&r.Read(Actor,Local,World,Camera,new[]{1000.0,0,0}).Relation==TargetRelation.Unknown,"Old binding retained");});
        Check("Equal authoritative team ids classify ally",()=>{var f=new Fixture();f.Put(OtherState+8,BitConverter.GetBytes(1));Expect(f.Data().Relation==TargetRelation.Ally,"Ally missed");});
        Check("Invalid team id remains unknown",()=>{var f=new Fixture();f.Put(OtherState+8,BitConverter.GetBytes(-1));Expect(f.Data().Relation==TargetRelation.Unknown,"Invalid enemy inferred");});
        Check("Visibility belongs to local camera",()=>{var f=new Fixture();f.Put(Actor+64,BitConverter.GetBytes(Camera+100));Expect(f.Data().Visibility==TargetVisibility.Unknown,"Other observer accepted");});
        Check("Stale raycast remains unknown",()=>{var f=new Fixture();f.Put(Actor+72,BitConverter.GetBytes(9.9));Expect(f.Data().Visibility==TargetVisibility.Unknown,"Stale raycast accepted");});
        Check("Future raycast remains unknown",()=>{var f=new Fixture();f.Put(Actor+72,BitConverter.GetBytes(10.2));Expect(f.Data().Visibility==TargetVisibility.Unknown,"Future raycast accepted");});
        Check("Render flag never substitutes for raycast",()=>{var f=new Fixture();f.Profile.VisibilitySource="LastRenderTime";Expect(f.Data().Visibility==TargetVisibility.Unknown,"Render implies line of sight");});
        Check("Raycast reports head occluded and chest clear separately",()=>{var f=new Fixture();f.Put(Actor+80,new byte[]{0,1});var d=f.Data();Expect(d.Visibility==TargetVisibility.Occluded&&d.ChestVisibility==TargetVisibility.Clear,"Per-bone visibility lost");});
        Check("Out of range bone index rejects pose",()=>{var f=new Fixture();f.Profile.HeadIndex=10;Expect(!f.Data().BonesValid,"Invalid bone accepted");});
        Check("Capacity mismatch rejects pose",()=>{var f=new Fixture();f.Put(Mesh+112,BitConverter.GetBytes(4));Expect(!f.Data().BonesValid,"Bad array accepted");});
        Check("Partial bone read rejects pose",()=>{var f=new Fixture();f.Bytes.Remove(Bones+24);Expect(!f.Data().BonesValid,"Partial accepted");});
        Check("Pose header changing during read rejects pose",()=>{var f=new Fixture();int seen=0;f.Source=(p,n)=>{var b=f.Read(p,n);if(p==Mesh+100&&++seen>1)b[8]=4;return b;};Expect(!f.Data().BonesValid,"Torn array accepted");});
        Check("Out of range bone index rejects pose",()=>{var f=new Fixture();f.Profile.HeadIndex=10;Expect(!f.Data().BonesValid,"Invalid bone accepted");});
        Check("Chest IO failure does not discard head observation",()=>{var f=new Fixture();f.Bytes.Remove(Actor+81);var d=f.Data();Expect(d.Visibility==TargetVisibility.Clear&&d.ChestVisibility==TargetVisibility.Unknown,"Head erased by chest error");});
        Console.WriteLine("Fallos: "+failures+". Memoria sintetica; sin entrada real.");return failures==0?0:1;
    }
}
