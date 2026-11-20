using System;
using System.Reflection;
class WorldPositionTests {
    static void Put(byte[] bytes,int offset,double value){Array.Copy(BitConverter.GetBytes(value),0,bytes,offset,8);}
    static int Main() {
        var method=typeof(Projection).GetMethod("TryWorldPosition");
        if(method==null)throw new Exception("FAIL: falta lector de origen mundial para componentes unidos.");
        byte[] block=new byte[0x60];Put(block,24,1);Put(block,0x20,197561.626636);Put(block,0x28,-97010.83770478329);Put(block,0x30,151643.89891814478);
        Put(block,0x40,1);Put(block,0x48,1);Put(block,0x50,1);
        object[] args={block,null};
        if(!(bool)method.Invoke(null,args))throw new Exception("FAIL: transformacion valida rechazada.");
        var position=(double[])args[1];
        if(position[0]!=197561.626636||position[1]!=-97010.83770478329||position[2]!=151643.89891814478)throw new Exception("FAIL: posicion mundial mal descodificada.");
        Put(block,24,0);args=new object[]{block,null};if((bool)method.Invoke(null,args))throw new Exception("FAIL: quaternion no valido aceptado.");
        Put(block,24,1);Put(block,0x30,double.NaN);args=new object[]{block,null};if((bool)method.Invoke(null,args))throw new Exception("FAIL: posicion NaN aceptada.");
        args=new object[]{new byte[12],null};if((bool)method.Invoke(null,args))throw new Exception("FAIL: lectura parcial aceptada.");
        Console.WriteLine("PASS: origen mundial de componente unido, rechazo de quaternion, NaN y bloque parcial.");return 0;
    }
}
