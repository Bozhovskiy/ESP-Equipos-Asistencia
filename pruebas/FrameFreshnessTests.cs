using System;
using System.Diagnostics;
using System.Reflection;
class FrameFreshnessTests {
    static int Main() {
        var check=typeof(EspFrame).GetMethod("IsFresh");
        if(check==null)throw new Exception("FAIL: el renderer necesita rechazar datos de mas de 500 ms.");
        var frame=new EspFrame {Valid=true};
        var timestamp=typeof(EspFrame).GetField("CapturedAt");
        long acquired=(long)timestamp.GetValue(frame);
        Func<long,bool> fresh=now=>(bool)check.Invoke(frame,new object[]{now});
        if(!fresh(acquired))throw new Exception("FAIL: frame recien capturado debe poder dibujarse.");
        if(fresh(acquired+Stopwatch.Frequency))throw new Exception("FAIL: frame antiguo debe dejar de dibujarse.");
        if(fresh(acquired-1))throw new Exception("FAIL: timestamp futuro debe rechazarse.");
        frame.Valid=false;
        if(fresh(acquired))throw new Exception("FAIL: frame invalido no debe dibujarse.");
        Console.WriteLine("PASS: edad de datos, datos invalidos y tiempo monotono.");return 0;
    }
}
