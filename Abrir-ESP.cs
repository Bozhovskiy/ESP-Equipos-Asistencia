using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
public static class AbrirEsp {
    [STAThread] public static int Main(string[] args) {
        string folder=AppDomain.CurrentDomain.BaseDirectory;
        string script=Path.Combine(folder,"Probar-LiveKd.ps1");
        if(args.Length==1 && args[0]=="--verify") {
            if(!File.Exists(script)) return 1;
            Console.WriteLine("Launcher found; acquisition not started.");return 0;
        }
        try {
            if(!File.Exists(script))throw new FileNotFoundException("Keep this executable with the complete release folder.",script);
            var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell\\v1.0\\powershell.exe"));
            start.Arguments="-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -Mode ESP";
            start.WorkingDirectory=folder;start.UseShellExecute=true;start.Verb="runas";start.WindowStyle=ProcessWindowStyle.Hidden;
            System.Diagnostics.Process.Start(start);return 0;
        }catch(Exception error){MessageBox.Show(error.Message,"ESP",MessageBoxButtons.OK,MessageBoxIcon.Information);return 1;}
    }
}
