using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
public static class SceneDiscoveryTests {
    const ulong World=0x100000,MainLevel=0x110000,Stream=0x120000,ActorArray=0x130000,StreamArray=0x140000,Levels=0x150000,Instance=0x160000,Locals=0x170000,Local=0x180000,Own=0x190000,Remote=0x1a0000;
    static int failures;
    sealed class Fixture {
        readonly Dictionary<ulong,byte> memory=new Dictionary<ulong,byte>();
        public Func<ulong,int,byte[]> Source;
        public Fixture(){Put(World+0x128,BitConverter.GetBytes(MainLevel));Array(World+0x470,Levels,new[]{Stream});Array(MainLevel+0x110,ActorArray,new[]{Own});Array(Stream+0x110,StreamArray,new[]{Own,Remote});Put(World+0x4a8,BitConverter.GetBytes(Instance));Array(Instance+0xf8,Locals,new[]{Local});Put(Local+0xa0,BitConverter.GetBytes(Own));Source=Read;}
        public void Put(ulong p,byte[] b){for(int i=0;i<b.Length;i++)memory[p+(ulong)i]=b[i];}
        public void Array(ulong header,ulong data,ulong[] pointers){var b=new byte[16];System.Array.Copy(BitConverter.GetBytes(data),b,8);System.Array.Copy(BitConverter.GetBytes(pointers.Length),0,b,8,4);System.Array.Copy(BitConverter.GetBytes(pointers.Length),0,b,12,4);Put(header,b);for(int i=0;i<pointers.Length;i++)Put(data+(ulong)i*8,BitConverter.GetBytes(pointers[i]));}
        byte[] Read(ulong p,int n){var b=new byte[n];for(int i=0;i<n;i++)if(!memory.TryGetValue(p+(ulong)i,out b[i]))throw new IOException("Synthetic partial read");return b;}
    }
    static Type Discovery(){var t=typeof(EspFrame).Assembly.GetType("SceneDiscovery");if(t==null)throw new Exception("No hay busqueda en niveles cargados");return t;}
    static object Call(string name,params object[] args){try{return Discovery().GetMethod(name).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static List<ulong> Actors(Fixture f,Func<bool> stop=null){return (List<ulong>)Call("CollectActors",World,f.Source,stop??(()=>false));}
    static ulong Controller(Fixture f,params ulong[] named){return (ulong)Call("FindLocalController",World,Actors(f),new List<ulong>(named),f.Source);}
    static void Expect(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    public static int Main(){
        Check("Actor in a loaded sublevel participates in discovery",()=>{var a=Actors(new Fixture());Expect(a.Contains(Own)&&a.Contains(Remote)&&a.Count==2,"Streaming actor missing or duplicated");});
        Check("Missing loaded-level array preserves main level",()=>{var f=new Fixture();var original=f.Source;f.Source=(p,n)=>{if(p==World+0x470)throw new IOException();return original(p,n);};Expect(Actors(f).Count==1,"Main level lost");});
        Check("Invalid actor pointers are discarded",()=>{var f=new Fixture();f.Array(Stream+0x110,StreamArray,new[]{0UL,3UL,Remote});Expect(Actors(f).Count==2,"Invalid actor retained");});
        Check("Changing actor header rejects that level",()=>{var f=new Fixture();var original=f.Source;int reads=0;f.Source=(p,n)=>{var b=original(p,n);if(p==Stream+0x110&&++reads>1)b[8]=1;return b;};var a=Actors(f);Expect(a.Count==1&&a[0]==Own,"Torn actor list accepted");});
        Check("Malformed streaming level does not discard main actors",()=>{var f=new Fixture();f.Put(Stream+0x118,BitConverter.GetBytes(-1));Expect(Actors(f).Count==1,"Broken level accepted or main lost");});
        Check("Cancellation publishes no partial actor list",()=>Expect(Actors(new Fixture(),()=>true).Count==0,"Partial cancellation accepted"));
        Check("Authoritative local controller selected among multiple names",()=>Expect(Controller(new Fixture(),Own,Remote)==Own,"Remote or ambiguous controller used"));
        Check("Controller can be identified without actor name",()=>Expect(Controller(new Fixture())==Own,"Name absence blocked local controller"));
        Check("Multiple local controllers stay ambiguous",()=>{var f=new Fixture();f.Array(Instance+0xf8,Locals,new[]{Local,Local+0x1000});f.Put(Local+0x1000+0xa0,BitConverter.GetBytes(Remote));Expect(Controller(f,Own,Remote)==0,"Ambiguous local controller guessed");});
        Check("Missing local array retains unique named fallback",()=>{var f=new Fixture();var original=f.Source;f.Source=(p,n)=>{if(p==World+0x4a8)throw new IOException();return original(p,n);};Expect(Controller(f,Own)==Own,"Unique fallback lost");});
        Check("Fallback rejects multiple named controllers",()=>{var f=new Fixture();var original=f.Source;f.Source=(p,n)=>{if(p==World+0x4a8)throw new IOException();return original(p,n);};Expect(Controller(f,Own,Remote)==0,"Fallback guesses remote controller");});
        Check("Local pointer must be present in discovered actors",()=>{var f=new Fixture();f.Put(Local+0xa0,BitConverter.GetBytes(0x900000UL));Expect(Controller(f)==0,"Foreign pointer used");});
        Check("Changing game instance during owner read rejects old controller",()=>{var f=new Fixture();var original=f.Source;f.Source=(p,n)=>{var b=original(p,n);if(p==Local+0xa0)f.Put(World+0x4a8,BitConverter.GetBytes(Instance+0x1000));return b;};Expect(Controller(f,Own)==0,"Removed game instance accepted");});
        Check("Changing local player list during owner read rejects old controller",()=>{var f=new Fixture();var original=f.Source;bool changed=false;f.Source=(p,n)=>{var b=original(p,n);if(p==Local+0xa0&&!changed){changed=true;f.Array(Instance+0xf8,Locals+0x1000,new[]{Local+0x2000});f.Put(Local+0x2000+0xa0,BitConverter.GetBytes(Remote));}return b;};Expect(Controller(f,Own)==0,"Removed local player accepted");});
        Check("Incomplete known local chain never falls back to remote name",()=>{var f=new Fixture();var original=f.Source;f.Source=(p,n)=>{if(p==Local+0xa0)throw new IOException();return original(p,n);};Expect(Controller(f,Remote)==0,"Partial authoritative sample fell back to remote name");});
        Console.WriteLine("Fallos: "+failures+". Memoria sintetica, sin lectura del juego.");return failures==0?0:1;
    }
}
