using System.Runtime.CompilerServices;
using System.Text;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class MemProcFsHeaderProbe {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
    [DllImport("kernel32.dll", CharSet=CharSet.Ansi, ExactSpelling=true, SetLastError=true)]
    static extern IntPtr GetProcAddress(IntPtr module, string name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate IntPtr InitDelegate(uint argc, [MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.LPStr, SizeParamIndex=0)] string[] argv);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void CloseDelegate(IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate bool PidDelegate(IntPtr context, [MarshalAs(UnmanagedType.LPStr)] string name, out uint pid);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate ulong BaseDelegate(IntPtr context, uint pid, [MarshalAs(UnmanagedType.LPStr)] string moduleName);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate bool ReadDelegate(IntPtr context, uint pid, ulong address, [Out] byte[] buffer, uint size, out uint read, ulong flags);
    static InitDelegate init;
    static CloseDelegate close;
    static PidDelegate findPid;
    static BaseDelegate findBase;
    static ReadDelegate read;

    static Delegate Export(IntPtr library, string name, Type type) {
        IntPtr address = GetProcAddress(library, name);
        if (address == IntPtr.Zero) throw new EntryPointNotFoundException(name);
        return Marshal.GetDelegateForFunctionPointer(address, type);
    }
    public static void LoadRuntime(string path) {
        IntPtr library = LoadLibraryEx(path, IntPtr.Zero, 8);
        if (library == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        init = (InitDelegate)Export(library, "VMMDLL_Initialize", typeof(InitDelegate));
        close = (CloseDelegate)Export(library, "VMMDLL_Close", typeof(CloseDelegate));
        findPid = (PidDelegate)Export(library, "VMMDLL_PidGetFromName", typeof(PidDelegate));
        findBase = (BaseDelegate)Export(library, "VMMDLL_ProcessGetModuleBaseU", typeof(BaseDelegate));
        read = (ReadDelegate)Export(library, "VMMDLL_MemReadEx", typeof(ReadDelegate));
    }
    public static IntPtr Begin(string device) {
        string[] args = {"-printf", "-device", device, "-disable-python", "-waitinitialize", "-printf", "-disable-symbolserver", "-logfile", System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "memprocfs-entidades.txt"), "-loglevel", "fflush,f:4"};
        return init((uint)args.Length, args);
    }
    public static uint FindGame(IntPtr context) {
        uint pid;
        return findPid(context, "PioneerGame.exe", out pid) ? pid : 0;
    }
    public static ulong GameBase(IntPtr context, uint pid) { return findBase(context, pid, "PioneerGame.exe"); }
    public static bool ReadHeader(IntPtr context, uint pid, ulong address, byte[] buffer, out uint bytesRead) {
        return read(context, pid, address, buffer, (uint)buffer.Length, out bytesRead, 1);
    }
    public static void End(IntPtr context) { if (context != IntPtr.Zero) close(context); }
}

public static class GNames
{
    private const ulong NamesOffset = 0x10B779F0;
    private const ulong KeyTableOffset = 0x10A1425C;
 
    private const uint HP = 0x01000193;
    private const ulong FnvPrime = 0x100000001B3UL;
    private const ulong FnvAdd = 0xDEBB31D42E781443UL;
 
    private const uint SlotHashAdd = 0xAE889C12u;
    private const ulong SlotXor = 0xE87D14C366730912UL;
 
    private const uint ShardHashAdd = 0xC3A0AE23u;
    private const ulong ChunkHashSeed = 0x10;
    private const ulong ChunkBlockBase = 0x20;
    private const uint ChunkOffsetMask = 0x1FFF00;
    private const ulong BlockAddLo = 0x1F53CB8D1AA510D5UL;
    private const ulong BlockAddHi = 0xE0ACCDCF9338EF2AUL;
 
    private const int KeystreamCount = 256;
    private const int KeystreamBase = 24;
    private const uint StringKeyBias = 0x8CB5;
 
    private static readonly ushort[] KeyTable = new ushort[256];
    private static bool _ready;
    private static readonly System.Collections.Generic.Dictionary<int,string> NameCache=new System.Collections.Generic.Dictionary<int,string>();
 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MemValid(ulong p) => p is > 0x10000UL and < 0x7FFFFFFFFFFFUL;
 
    private static void EnsureInit()
    {
        if (_ready) return;
        var baseAddr = Process.BaseAddress;
        if (baseAddr == 0) return;
 
        var buf = new byte[KeystreamCount * 2];
        Memory.ReadArray(baseAddr + KeyTableOffset, buf);
        for (var i = 0; i < KeystreamCount; i++)
            KeyTable[i] = BitConverter.ToUInt16(buf, i * 2);
        _ready = true;
    }
 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort KsSlot(uint k) => KeyTable[(int)((k & 0x3Fu) + KeystreamBase)];
 
    public static string GetActorName(ulong actor) => DecryptActorName(actor);
 
    public static int GetActorFNameId(ulong actor)
    {
        if (!MemValid(actor)) return 0;
        EnsureInit();
        if (!_ready) return 0;
        return ReadCompIndex(actor, PickNameSlot(actor));
    }
 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PickNameSlot(ulong actor)
    {
        var seed = actor + 0x10;
        var lo = (uint)seed;
        var hi = (uint)(seed >> 32);
 
        var hash = HP * BitOperations.RotateLeft(lo, 13) + SlotHashAdd;
        hash = HP * BitOperations.RotateLeft(hash, 29) + hi + SlotHashAdd;
        hash = HP * BitOperations.RotateLeft(hash, 13) + SlotHashAdd;
        hash = HP * (hash >> 3) + SlotHashAdd;
        return ((hash ^ (hash >> 16)) & 3u) ^ 2u;
    }
 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort Rol16(ushort x, int n) => (ushort)((x << n) | (x >> (16 - n)));
 
    private static int ReadCompIndex(ulong actor, uint slot)
    {
        var slotAddr = actor + 0x20 + 0x20UL * slot;
        if (!MemValid(slotAddr)) return 0;
 
        var enc = Memory.Read<ulong>(slotAddr);
        if (enc == 0) return 0;
 
        var dec = 0UL;
        for (var lane = 0; lane < 4; lane++)
        {
            var w = (ushort)(enc >> (lane * 16));
            var k = (ushort)(SlotXor >> (lane * 16));
            w = Rol16((ushort)(Rol16(w, 13) ^ k), 4);
            dec |= (ulong)w << (lane * 16);
        }
        return (int)(uint)BitOperations.RotateLeft(dec, 32);
    }
 
    private static string DecryptActorName(ulong actor)
    {
        if (!MemValid(actor)) return string.Empty;
        EnsureInit();
        if (!_ready) return string.Empty;
 
        var ci = GetActorFNameId(actor);
        if (ci <= 0) return string.Empty;
        string cached;if(NameCache.TryGetValue(ci,out cached))return cached;
 
        var namePtr = ResolveNamePtr(ci);
        if (!MemValid(namePtr)) return string.Empty;
 
        var header = Memory.Read<ushort>(namePtr);
        if (header == 0) return string.Empty;
 
        var isWide = (header & 1u) != 0;
        var length = (header & 0x3FE) + (header >> 15);
        if (length <= 0 || length > 1024) return string.Empty;
        string decoded=isWide ? DecodeWide(namePtr + 2, length >> 1) : DecodeNarrow(namePtr + 2, length);
        if(!string.IsNullOrEmpty(decoded)&&decoded.IndexOf('?')<0){if(NameCache.Count>=8192)NameCache.Clear();NameCache[ci]=decoded;}
        return decoded;
    }
 
    private static ulong ResolveNamePtr(int compIndex)
    {
        var ci = (uint)compIndex;
        var nameOffset = (ushort)ci;
        var chunk = Process.BaseAddress + NamesOffset + ((ci >> 8) & ChunkOffsetMask);
 
        ShardHash(chunk + ChunkHashSeed, out var bidx1, out var bidx2);
 
        var block1 = DecryptBlock(chunk + ChunkBlockBase + 32UL * bidx1);
        var block2 = DecryptBlock(chunk + ChunkBlockBase + 32UL * bidx2);
        if (block1 == 0 && block2 == 0) return 0;
 
        var fnv = FnvPrime * BitOperations.RotateLeft(block1, 57) + FnvAdd;
        fnv = FnvPrime * BitOperations.RotateLeft(fnv, 46) + FnvAdd;
        return block1 + (block2 ^ fnv) + 2UL * nameOffset;
    }
 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ShardHash(ulong seedAddr, out uint bidx1, out uint bidx2)
    {
        var lo = (uint)seedAddr;
        var hi = (uint)(seedAddr >> 32);
        var hash = HP * BitOperations.RotateLeft(lo, 24) + ShardHashAdd;
        hash = HP * BitOperations.RotateLeft(hash, 29) + hi + ShardHashAdd;
        hash = HP * (hash >> 8) + ShardHashAdd;
        hash = HP * (hash >> 3) + ShardHashAdd;
        bidx1 = (hash ^ (hash >> 16)) & 7u;
        bidx2 = (bidx1 + 1u) & 7u;
    }
 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong DecryptBlock(ulong address)
    {
        if (!MemValid(address)) return 0;
        var bytes = new byte[16];
        Memory.ReadArray(address, bytes);
        var raw = new ulong[] { BitConverter.ToUInt64(bytes, 0), BitConverter.ToUInt64(bytes, 8) };
        var x = BitOperations.RotateLeft(raw[0] + BlockAddLo, 53) ^ raw[1];
        return BitOperations.RotateLeft(x + BlockAddHi, 11) ^ raw[0];
    }
 
    private static string DecodeNarrow(ulong addr, int length)
    {
        var bytes = new byte[length];
        Memory.ReadArray(addr, bytes);
 
        var key = (uint)length + StringKeyBias;
        for (var i = 0; i < length; i++)
            bytes[i] ^= (byte)(KsSlot(key + (uint)i) >> 3);
 
        var sb = new StringBuilder(length);
        foreach (var b in bytes)
        {
            if (b == 0) break;
            sb.Append(b is >= 32 and <= 126 ? (char)b : '?');
        }
        return sb.ToString();
    }
 
    private static string DecodeWide(ulong addr, int length)
    {
        var raw = new byte[length * 2];
        Memory.ReadArray(addr, raw);
 
        var key = (uint)length + StringKeyBias;
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            var c = (ushort)(BitConverter.ToUInt16(raw, i * 2) ^ KsSlot(key + (uint)i));
            if (c == 0) break;
            sb.Append(c is >= 32 and <= 126 ? (char)c : '?');
        }
        return sb.ToString();
    }
}

public static class BitOperations {
    public static uint RotateLeft(uint n, int s) { return (n << s) | (n >> (32 - s)); }
    public static ulong RotateLeft(ulong n, int s) { return (n << s) | (n >> (64 - s)); }
}
public static class Process { public static ulong BaseAddress; }
public static class Memory {
    public static IntPtr Context;
    public static uint Pid;
    public static int FailedReads;
    public static bool Valid(ulong address) { return address > 0x10000 && address < 0x00007FFFFFFFFFFF; }
    public static void ReadArray(ulong address, byte[] bytes) {
        uint count=0;
        if (!Valid(address) || bytes.Length > 65536 || address + (ulong)bytes.Length >= 0x0000800000000000 || !MemProcFsHeaderProbe.ReadHeader(Context, Pid, address, bytes, out count) || count != bytes.Length) {
            FailedReads++; throw new System.IO.IOException("Lectura incompleta o direccion fuera de limites: 0x"+address.ToString("X")+", "+count+"/"+bytes.Length+" bytes.");
        }
    }
    public static byte[] Bytes(ulong address, int size) { byte[] bytes = new byte[size]; ReadArray(address, bytes); return bytes; }
    public static T Read<T>(ulong address) {
        object result;
        if (typeof(T) == typeof(ulong)) result = BitConverter.ToUInt64(Bytes(address, 8), 0);
        else if (typeof(T) == typeof(ushort)) result = BitConverter.ToUInt16(Bytes(address, 2), 0);
        else if (typeof(T) == typeof(int)) result = BitConverter.ToInt32(Bytes(address, 4), 0);
        else throw new NotSupportedException(typeof(T).Name);
        return (T)result;
    }
}
public static class EntityProgram {
    static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n) && Math.Abs(n) < 100000000; }
    static string Kind(string name) {
        if (name.StartsWith("C_Meerkat_C", StringComparison.Ordinal) || name.StartsWith("C_Tarragon_C", StringComparison.Ordinal)) return "machine";
        if (name.StartsWith("BP_NomadCache_FrozenTrail_WA_C", StringComparison.Ordinal)) return "cache";
        if (name.StartsWith("BP_PioneerCharacter_C", StringComparison.Ordinal) || name.IndexOf("PlayerCharacter", StringComparison.OrdinalIgnoreCase) >= 0) return "player_candidate";
        return "other";
    }
    static bool PlausibleName(string name) {
        if (string.IsNullOrEmpty(name) || name.Length > 1024 || name.IndexOf('?') >= 0) return false;
        foreach (char c in name) if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')) return false;
        return true;
    }
    static void ReadLocation(System.Collections.Generic.Dictionary<string,object> item, ulong actor) {
        try {
            ulong root = Memory.Read<ulong>(actor + 0x248);
            byte[] xyz = Memory.Bytes(root + 0x280, 24);
            double x=BitConverter.ToDouble(xyz,0), y=BitConverter.ToDouble(xyz,8), z=BitConverter.ToDouble(xyz,16);
            if (Finite(x) && Finite(y) && Finite(z) && (x!=0 || y!=0 || z!=0)) item["positionCandidate"] = new double[] {x,y,z};
        } catch (System.IO.IOException) { }
    }
    static void ReadCamera(System.Collections.Generic.Dictionary<string,object> item, ulong actor) {
        try {
            byte[] pov = Memory.Bytes(actor + 0xE10 + 0x10, 0x54);
            double[] loc = {BitConverter.ToDouble(pov,8),BitConverter.ToDouble(pov,16),BitConverter.ToDouble(pov,24)};
            double[] rot = {BitConverter.ToDouble(pov,0x30),BitConverter.ToDouble(pov,0x38),BitConverter.ToDouble(pov,0x40)};
            float fov = BitConverter.ToSingle(pov,0x50);
            bool valid = fov > 10 && fov < 170;
            foreach(double n in loc) valid &= Finite(n);
            foreach(double n in rot) valid &= Finite(n) && Math.Abs(n) <= 360;
            item["cameraPovPlausible"] = valid;
            if (valid) {item["cameraLocation"] = loc;item["cameraRotation"] = rot;item["cameraFov"] = fov;}
        } catch (System.IO.IOException) { item["cameraPovPlausible"] = false; }
    }
    static int Main(string[] args) {
        string folder = AppDomain.CurrentDomain.BaseDirectory;
        bool verify = args.Length == 1 && args[0] == "--verify";
        var result = new System.Collections.Generic.Dictionary<string,object>();
        result["timestampUtc"] = DateTime.UtcNow.ToString("o");
        result["memoryWritesPerformed"] = false;
        result["securitySettingsChanged"] = false;
        result["mouseInputSent"] = false;
        result["namesDecoded"] = 0;
        result["actorsInspected"] = 0;
        int exit = 1;
        IntPtr context = IntPtr.Zero;
        try {
            MemProcFsHeaderProbe.LoadRuntime(System.IO.Path.Combine(folder,"runtime","vmm.dll"));
            if (verify) { result["status"] = "exports_verified; acquisition_not_started"; exit=0; }
            else {
                if (!new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)) throw new Exception("Falta elevacion UAC.");
                if (!System.IO.File.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"DumpIt.dmp"))) throw new Exception("Falta DumpIt LIVEKD.");
                context = MemProcFsHeaderProbe.Begin("dumpit");
                if (context == IntPtr.Zero) throw new Exception("No se pudo iniciar MemProcFS.");
                Memory.Context = context; Memory.Pid = MemProcFsHeaderProbe.FindGame(context);
                if (Memory.Pid == 0) throw new Exception("No se encontro PioneerGame.exe.");
                Process.BaseAddress = MemProcFsHeaderProbe.GameBase(context,Memory.Pid);
                ulong world = Memory.Read<ulong>(Memory.Read<ulong>(Process.BaseAddress + 0x10A22A98));
                ulong level = Memory.Read<ulong>(world + 0x128);
                byte[] array = Memory.Bytes(level + 0x110,16);
                ulong data = BitConverter.ToUInt64(array,0);
                int num = BitConverter.ToInt32(array,8), max = BitConverter.ToInt32(array,12);
                if (!Memory.Valid(data) || num <= 0 || num > max || max > 100000) throw new Exception("Array de actores no coherente en este momento.");
                result["actorCount"] = num;
                int inspect = Math.Min(num,8192);
                byte[] pointers = Memory.Bytes(data,inspect*8);
                var actors = new System.Collections.Generic.List<object>();
                int decoded=0, nameFailures=0;
                for(int i=0;i<inspect;i++) {
                    ulong actor = BitConverter.ToUInt64(pointers,i*8);
                    if (!Memory.Valid(actor)) continue;
                    try {
                        string name=GNames.GetActorName(actor);
                        if (!PlausibleName(name)) {nameFailures++;continue;}
                        decoded++;
                        var item=new System.Collections.Generic.Dictionary<string,object>();
                        item["name"]=name; item["actorAddress"]="0x"+actor.ToString("X"); item["kind"]=Kind(name);
                        ReadLocation(item,actor);
                        if (name.StartsWith("BP_PioneerPlayerController_C",StringComparison.Ordinal)) {
                            try {
                                byte[] rot = Memory.Bytes(actor+0x430,24);
                                item["controlRotation"] = new double[] {BitConverter.ToDouble(rot,0),BitConverter.ToDouble(rot,8),BitConverter.ToDouble(rot,16)};
                                item["acknowledgedPawn"] = "0x"+Memory.Read<ulong>(actor+0x428).ToString("X");
                            } catch(System.IO.IOException) {}
                        }
                        if (name.StartsWith("BP_PioneerCharacter_C",StringComparison.Ordinal)) {
                            try { item["playerStatePointer"]="0x"+Memory.Read<ulong>(actor+0x3F0).ToString("X"); } catch(System.IO.IOException) {}
                        }
                        if (name.IndexOf("CameraManager",StringComparison.OrdinalIgnoreCase)>=0) ReadCamera(item,actor);
                        actors.Add(item);
                    } catch(System.IO.IOException) {nameFailures++;}
                }
                result["actorsInspected"]=inspect;
                result["namesDecoded"]=decoded;
                result["nameFailures"]=nameFailures;
                result["failedReads"]=Memory.FailedReads;
                result["actors"]=actors;
                result["status"]=decoded>0 ? "entity_names_read; labels_positions_and_camera_need_validation" : "actor_array_read; name_decryption_unverified";
                exit=decoded>0 ? 0 : 1;
            }
        } catch(Exception error) {result["status"]="probe_failed";result["error"]=error.Message;}
        finally {
            if(context!=IntPtr.Zero) MemProcFsHeaderProbe.End(context);
            string report = System.IO.Path.Combine(folder,verify ? "verificacion-entidades.json" : "resultado-entidades.json");
            System.IO.File.WriteAllText(report,new System.Web.Script.Serialization.JavaScriptSerializer {MaxJsonLength=16777216}.Serialize(result),new System.Text.UTF8Encoding(false));
            Console.WriteLine("Estado: "+result["status"]);
            Console.WriteLine("Resultado: "+report);
        }
        return exit;
    }
}
