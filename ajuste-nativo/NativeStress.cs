using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Diagnostics;
public static class NativeStress {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetProcAddress(IntPtr module,string name);
    [DllImport("kernel32.dll")]static extern uint SetErrorMode(uint mode);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Create(IntPtr config);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate bool Read(IntPtr context,ulong address,uint length,[Out] byte[] output);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Close(IntPtr context);
    static T Export<T>(IntPtr module,string name){return (T)(object)Marshal.GetDelegateForFunctionPointer(GetProcAddress(module,name),typeof(T));}
    static int errors;
    public static int Main(string[] args){
        SetErrorMode(0x8003);
        string dll=Path.GetFullPath(args[0]),file=Path.GetFullPath(args[1]);
        if(args.Length==3&&args[2]=="make"){
            byte[] block=new byte[65536];using(var f=File.Create(file))for(int page=0;page<512;page++){for(int i=0;i<block.Length;i++)block[i]=(byte)((page+i)%251);f.Write(block,0,block.Length);}Console.WriteLine("Synthetic file created: 32 MB");return 0;
        }
        if(new FileInfo(file).Length!=33554432)throw new Exception("Not the synthetic fixture");
        var module=LoadLibrary(dll);if(module==IntPtr.Zero)throw new Exception("DLL load failed");
        var create=Export<Create>(module,"LcCreate");var read=Export<Read>(module,"LcRead");var close=Export<Close>(module,"LcClose");
        IntPtr config=Marshal.AllocHGlobal(816);Marshal.Copy(new byte[816],0,config,816);Marshal.WriteInt32(config,unchecked((int)0xc0fd0002));
        byte[] name=Encoding.UTF8.GetBytes(file+"\0");if(name.Length>260)throw new Exception("Path too long");Marshal.Copy(name,0,IntPtr.Add(config,8),name.Length);Marshal.WriteInt64(config,536,33554431);
        IntPtr context=create(config);Marshal.FreeHGlobal(config);if(context==IntPtr.Zero)throw new Exception("LC initialization failed");
        Console.WriteLine("Fixture initialized. Parallel reads begin.");
        var start=new ManualResetEvent(false);var threads=new Thread[4];var clock=Stopwatch.StartNew();
        for(int t=0;t<threads.Length;t++){int index=t;threads[t]=new Thread(()=>{byte[] output=new byte[65536];start.WaitOne();for(int n=0;n<1500;n++){int page=1+(n*13+index*17)%510;if(!read(context,(ulong)page*65536,65536,output)){Interlocked.Increment(ref errors);continue;}for(int i=0;i<output.Length;i+=113)if(output[i]!=(byte)((page+i)%251)){Interlocked.Increment(ref errors);break;}}});threads[t].Start();}
        start.Set();foreach(var t in threads)t.Join();close(context);start.Dispose();
        Console.WriteLine("Reads=6000; validationErrors="+errors+"; seconds="+clock.Elapsed.TotalSeconds.ToString("F2"));return errors==0?0:2;
    }
}
