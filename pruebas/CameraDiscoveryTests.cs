using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
class CameraDiscoveryTests {
    static readonly Dictionary<ulong,byte[]> blocks=new Dictionary<ulong,byte[]>();
    static byte[] Read(ulong address,int size) {byte[] result;if(!blocks.TryGetValue(address,out result))throw new IOException("unavailable");return result;}
    static void Double(byte[] bytes,int offset,double value){Array.Copy(BitConverter.GetBytes(value),0,bytes,offset,8);}
    static void Camera(ulong actor,ulong controller,ulong root) {
        blocks[actor+0x428]=BitConverter.GetBytes(controller);blocks[actor+0x248]=BitConverter.GetBytes(root);
        var pov=new byte[0x54];Double(pov,8,199258.09);Double(pov,16,-97134.93);Double(pov,24,151785.02);Double(pov,0x30,-9.44);Double(pov,0x38,-154.53);Array.Copy(BitConverter.GetBytes(45f),0,pov,0x50,4);blocks[actor+0xE20]=pov;
        var transform=new byte[48];Double(transform,0,199258.09);Double(transform,8,-97134.93);Double(transform,16,151785.02);blocks[root+0x280]=transform;
    }
    static int Main() {
        var type=typeof(EspReader).Assembly.GetType("CameraDiscovery");
        if(type==null)throw new Exception("FAIL: camara inaccesible cuando no se puede leer su nombre.");
        var method=type.GetMethod("FindUnique");
        ulong controller=0x265EE1590,actor=0x1669D9250,root=0x17BE766D0,other=0x200000;
        Camera(actor,controller,root);
        Func<ulong,int,byte[]> read=Read;Func<bool> stop=()=>false;
        Func<ulong[],ulong> find=actors=>(ulong)method.Invoke(null,new object[]{actors,controller,read,stop});
        if(find(new[]{other,actor,actor})!=actor)throw new Exception("FAIL: camara valida sin nombre no recuperada.");
        blocks[actor+0x428]=BitConverter.GetBytes(controller+8);if(find(new[]{actor})!=0)throw new Exception("FAIL: propietario incorrecto aceptado.");
        Camera(actor,controller,root);Double(blocks[actor+0xE20],8,double.NaN);if(find(new[]{actor})!=0)throw new Exception("FAIL: POV NaN aceptado.");
        Camera(actor,controller,root);blocks[actor+0xE20]=new byte[12];if(find(new[]{actor})!=0)throw new Exception("FAIL: POV parcial aceptado.");
        Camera(actor,controller,root);Double(blocks[root+0x280],0,0);if(find(new[]{actor})!=0)throw new Exception("FAIL: raiz distinta al POV aceptada.");
        Camera(actor,controller,root);Camera(other,controller,0x300000);if(find(new[]{actor,other})!=0)throw new Exception("FAIL: camara ambigua aceptada.");
        if((ulong)method.Invoke(null,new object[]{new[]{actor},controller,read,new Func<bool>(()=>true)})!=0)throw new Exception("FAIL: se ignora cierre solicitado.");
        Console.WriteLine("PASS: camara sin nombre; propietario, POV, raiz, ambiguedad y cierre validados.");return 0;
    }
}
