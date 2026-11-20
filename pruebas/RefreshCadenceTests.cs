using System;
class RefreshCadenceTests {
    static int Main() {
        var method=typeof(EspReader).GetMethod("RemainingDelay");
        if(method==null)throw new Exception("FAIL: la espera no descuenta el tiempo de lectura.");
        if((int)method.Invoke(null,new object[]{60,10.0})!=7)throw new Exception("FAIL: lectura de 10 ms agrega una pausa completa.");
        if((int)method.Invoke(null,new object[]{60,25.0})!=1)throw new Exception("FAIL: lectura lenta crea un bucle sin pausa.");
        if((int)method.Invoke(null,new object[]{20,10.0})!=40)throw new Exception("FAIL: tasa reducida no se respeta.");
        Console.WriteLine("PASS: cadencia descuenta IO y mantiene pausa ante lecturas lentas.");return 0;
    }
}
