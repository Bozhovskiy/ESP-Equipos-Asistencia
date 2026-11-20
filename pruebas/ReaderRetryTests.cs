using System;
using System.Threading;
using System.Diagnostics;
class ReaderRetryTests {
    static int Main() {
        // Sin cargar runtime: cada intento real de lectura falla antes de adquirir memoria.
        var reader=new EspReader();
        var worker=new Thread(reader.Run) {IsBackground=true};
        worker.Start();int replacements=0;EspFrame previous=reader.Frame;
        var watch=Stopwatch.StartNew();
        try {
            while(watch.ElapsedMilliseconds<200) {
                var next=reader.Frame;
                if(!Object.ReferenceEquals(previous,next)){previous=next;replacements++;}
                Thread.Sleep(5);
            }
        }finally {reader.Stop=true;if(!worker.Join(1000))throw new Exception("FAIL: lector no termina.");}
        if(replacements==0)throw new Exception("FAIL: no se comprobo fallo real de lectura.");
        if(replacements>2)throw new Exception("FAIL: lector fallido reintenta sin pausa, frames="+replacements);
        Console.WriteLine("PASS: lectura fallida espera antes de repetir y termina limpiamente.");return 0;
    }
}
