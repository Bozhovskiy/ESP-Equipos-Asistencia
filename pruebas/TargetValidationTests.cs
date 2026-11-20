using System;
using System.Reflection;

public static class TargetValidationTests {
    static int failures;
    static Type TypeOf(string name){var t=typeof(TargetData).Assembly.GetType(name);if(t==null)throw new Exception("Falta "+name);return t;}
    static void Set(object o,string field,object value){var f=o.GetType().GetField(field);if(f==null)throw new Exception("Falta "+field);f.SetValue(o,value);}
    static object Call(string type,string method,params object[] args){try{return TypeOf(type).GetMethod(method).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static void Expect(bool v,string message){if(!v)throw new Exception(message);}
    static byte[] Transform(double x,double y,double z,double qz=0,double qw=1){var b=new byte[96];Array.Copy(BitConverter.GetBytes(qz),0,b,16,8);Array.Copy(BitConverter.GetBytes(qw),0,b,24,8);for(int i=0;i<3;i++){Array.Copy(BitConverter.GetBytes(new[]{x,y,z}[i]),0,b,32+i*8,8);Array.Copy(BitConverter.GetBytes(1.0),0,b,64+i*8,8);}return b;}
    static double[] World(byte[] local,byte[] component){object[] args={local,component,null};bool ok=(bool)Call("TargetValidation","TryBoneWorld",args);return ok?(double[])args[2]:null;}
    static object Profile(){var p=Activator.CreateInstance(TypeOf("TargetReadProfile"));Set(p,"Enabled",true);Set(p,"PeTimestamp",(uint)123);Set(p,"ImageSize",(uint)500000);return p;}
    public static int Main(){
        Check("Bone translation composes with component translation",()=>{var w=World(Transform(0,0,80),Transform(1000,20,30));Expect(w!=null&&w[0]==1000&&w[1]==20&&w[2]==110,"Wrong transform");});
        Check("Bone translation rotates with component quaternion",()=>{var w=World(Transform(10,0,0),Transform(1000,20,0,Math.Sqrt(.5),Math.Sqrt(.5)));Expect(w!=null&&Math.Abs(w[0]-1000)<.001&&Math.Abs(w[1]-30)<.001,"Rotation omitted");});
        Check("Malformed bone quaternion is rejected",()=>Expect(World(Transform(0,0,80,0,0),Transform(0,0,0))==null,"Malformed accepted"));
        Check("Nonfinite bone coordinate is rejected",()=>Expect(World(Transform(double.NaN,0,80),Transform(0,0,0))==null,"NaN accepted"));
        Check("Coherent pose accepted",()=>Expect((bool)Call("TargetValidation","CoherentPose",new double[]{1000,0,0},new double[]{1000,0,80},new double[]{1000,0,20},new double[]{1000,0,-10},new double[]{1000,-10,-90},new double[]{1000,10,-90}),"Pose rejected"));
        Check("Collapsed skeleton rejected",()=>Expect(!(bool)Call("TargetValidation","CoherentPose",new double[]{1000,0,0},new double[]{1000,0,0},new double[]{1000,0,0},new double[]{1000,0,0},new double[]{1000,0,0},new double[]{1000,0,0}),"Collapsed accepted"));
        Check("Oversized skeleton rejected",()=>Expect(!(bool)Call("TargetValidation","CoherentPose",new double[]{1000,0,0},new double[]{1000,0,8000},new double[]{1000,0,4000},new double[]{1000,0,1000},new double[]{1000,0,-90},new double[]{1000,0,-90}),"Oversized accepted"));
        Check("Malformed profile cannot enable reads",()=>Expect(!(bool)Call("TargetValidation","ProfileValid",Profile()),"Incomplete profile accepted"));
        Console.WriteLine("Fallos: "+failures+". Sin lectura del juego ni entrada real.");return failures==0?0:1;
    }
}
