using System;
using System.Reflection;
using System.Diagnostics;
public static class TeamFilterTests {
    static int failures;
    static object Filter(){var t=typeof(EspFrame).Assembly.GetType("TeamFilter");if(t==null)throw new Exception("Falta filtro compartido de companeros");return Activator.CreateInstance(t);}
    static object Call(object o,string name,params object[] args){try{return o.GetType().GetMethod(name).Invoke(o,args);}catch(TargetInvocationException e){throw e.InnerException;}}
    static void Expect(bool v,string m){if(!v)throw new Exception(m);}
    static void Check(string n,Action test){try{test();Console.WriteLine("PASS "+n);}catch(Exception e){failures++;Console.WriteLine("FAIL "+n+": "+e.Message);}}
    static PlayerPoint Player(ulong actor=0x20000){return new PlayerPoint{Actor=actor,Position=new[]{1000.0,0,0},Distance=10};}
    static EspFrame Frame(){var f=new EspFrame{Valid=true,Location=new[]{0.0,0,0},Rotation=new[]{0.0,0,0},Fov=90};typeof(EspFrame).GetField("World").SetValue(f,(ulong)0x60000);typeof(EspFrame).GetField("LocalPawn").SetValue(f,(ulong)0x70000);f.Players.Add(Player());return f;}
    static string Toggle(object f,EspFrame frame){return (string)Call(f,"ToggleAtCrosshair",frame,1920,1080,false,400);}
    static bool Show(object f,PlayerPoint p,bool unknown=false){return (bool)Call(f,"Show",p,true,unknown);}
    public static int Main(){
        Check("Confirmed teammate excluded from shared display filter",()=>{var p=Player();p.Target.Relation=TargetRelation.Ally;Expect(!Show(Filter(),p),"Ally displayed");});
        Check("Unknown team remains labelled unknown",()=>{var f=Filter();var p=Player();Expect((TargetRelation)Call(f,"Relation",p)==TargetRelation.Unknown&&Show(f,p),"Unknown promoted to enemy");});
        Check("Strict display suppresses unknown",()=>Expect(!Show(Filter(),Player(),true),"Unknown displayed in strict mode"));
        Check("Manual teammate exclusion also overrides enemy target data",()=>{var f=Filter();var frame=Frame();Toggle(f,frame);frame.Players[0].Target.Relation=TargetRelation.Enemy;Expect(!Show(f,frame.Players[0])&&(TargetRelation)Call(f,"Relation",frame.Players[0])==TargetRelation.Ally,"Manual ally not excluded");});
        Check("Same hotkey reverses manual mark",()=>{var f=Filter();var frame=Frame();Toggle(f,frame);Toggle(f,frame);Expect(Show(f,frame.Players[0]),"Unmark failed");});
        Check("Overlapping players are not guessed",()=>{var f=Filter();var frame=Frame();frame.Players.Add(Player(0x30000));Toggle(f,frame);Expect(Show(f,frame.Players[0])&&Show(f,frame.Players[1]),"Ambiguous teammate guessed");});
        Check("Stale frame cannot mark teammate",()=>{var f=Filter();var frame=Frame();frame.CapturedAt=Stopwatch.GetTimestamp()-Stopwatch.Frequency;Toggle(f,frame);Expect(Show(f,frame.Players[0]),"Stale marked");});
        Check("Player outside crosshair cannot be marked",()=>{var f=Filter();var frame=Frame();frame.Players[0].Position[1]=500;Toggle(f,frame);Expect(Show(f,frame.Players[0]),"Unrelated player marked");});
        Check("Changing world clears marks",()=>{var f=Filter();var frame=Frame();Toggle(f,frame);typeof(EspFrame).GetField("World").SetValue(frame,(ulong)0x80000);Call(f,"Update",frame,Stopwatch.GetTimestamp());Expect(Show(f,frame.Players[0]),"Old match marks retained");});
        Check("Changing local pawn clears marks",()=>{var f=Filter();var frame=Frame();Toggle(f,frame);typeof(EspFrame).GetField("LocalPawn").SetValue(frame,(ulong)0x80000);Call(f,"Update",frame,Stopwatch.GetTimestamp());Expect(Show(f,frame.Players[0]),"Respawn marks retained");});
        Check("Absent actors expire before address reuse",()=>{var f=Filter();var frame=Frame();Toggle(f,frame);var p=frame.Players[0];frame.Players.Clear();Call(f,"Update",frame,Stopwatch.GetTimestamp()+6*Stopwatch.Frequency);Expect(Show(f,p),"Expired address retained");});
        Check("Clear removes manual exclusions",()=>{var f=Filter();var frame=Frame();Toggle(f,frame);Call(f,"Clear");Expect(Show(f,frame.Players[0]),"Clear failed");});
        Console.WriteLine("Fallos: "+failures+". Sin lectura del juego ni entrada real.");return failures==0?0:1;
    }
}
