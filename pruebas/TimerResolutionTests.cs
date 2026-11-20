using System;
using System.Diagnostics;
using System.Threading;
class TimerResolutionTests {
    static double Sample(){var watch=Stopwatch.StartNew();for(int i=0;i<20;i++)Thread.Sleep(17);return watch.Elapsed.TotalMilliseconds/20;}
    static int Main() {
        var type=typeof(EspReader).Assembly.GetType("TimerResolution");
        if(type==null)throw new Exception("FAIL: no existe control de precision del temporizador.");
        double baseline=Sample();
        var lease=(IDisposable)Activator.CreateInstance(type);double active;
        try{type.GetMethod("SetActive").Invoke(lease,new object[]{true});active=Sample();if(!(bool)type.GetProperty("Active").GetValue(lease,null))throw new Exception("FAIL: temporizador no activado.");}
        finally{lease.Dispose();}
        if((bool)type.GetProperty("Active").GetValue(lease,null))throw new Exception("FAIL: precision no liberada al cerrar.");
        double released=Sample();
        Console.WriteLine("Temporizador ms/espera: antes="+baseline.ToString("F2")+" activo="+active.ToString("F2")+" liberado="+released.ToString("F2"));
        if(active>=25)throw new Exception("FAIL: temporizador activo no resuelve esperas de 17 ms.");
        Console.WriteLine("PASS: temporizador nativo activado, medido y liberado sin adquisicion.");return 0;
    }
}
