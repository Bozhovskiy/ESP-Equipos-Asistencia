using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

public sealed class EspSettings {
    public bool Enabled=true,Boxes=true,Distances=true,Radar=true,Status=true;
    public bool PracticeCalibration=false;
    public bool EdgeIndicators=true;
    public bool HideAllies=true,HideUnknownTeams=false;
    public bool Skeleton=true;
    public bool RadarNorthUp=false,RadarAutoZoom=true,RadarLabels=true,RadarAltitude=true,RadarBorder=true,RadarHeadingTick=true;
    public int TextSize=12,MaxDistance=1000,RadarRange=300;
    public int RefreshHz=60,BoxThickness=2,RadarRadius=110,RadarBottomGap=230;
    public string BoxColor="Azul";
    public string BoxStyle="Esquinas";
    public string FovAxis="Horizontal";
    public Color Color {get {switch(BoxColor){case "Verde":return Color.FromArgb(114,231,196);case "Blanco":return Color.FromArgb(233,244,252);case "Amarillo":return Color.FromArgb(255,202,116);case "Violeta":return Color.FromArgb(231,172,255);default:return Color.FromArgb(100,201,255);}}}
    public static EspSettings Load() {
        try {
            var serializer=new JavaScriptSerializer();
            var profile=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"configuracion-esp.json")));
            var value=serializer.ConvertToType<EspSettings>(profile);
            if(value==null)return new EspSettings();
            value.TextSize=Math.Max(6,Math.Min(20,value.TextSize));value.MaxDistance=Math.Max(10,Math.Min(2000,value.MaxDistance));value.RadarRange=Math.Max(10,Math.Min(1000,value.RadarRange));
            value.RefreshHz=Math.Max(20,Math.Min(60,value.RefreshHz));value.BoxThickness=Math.Max(1,Math.Min(4,value.BoxThickness));value.RadarRadius=Math.Max(60,Math.Min(140,value.RadarRadius));value.RadarBottomGap=Math.Max(120,Math.Min(450,value.RadarBottomGap));
            if(value.BoxStyle!="Completa"&&value.BoxStyle!="Laterales")value.BoxStyle="Esquinas";
            if(value.FovAxis!="Vertical")value.FovAxis="Horizontal";
            return value;
        }catch{return new EspSettings();}
    }
    public void Save() {
        try {
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"configuracion-esp.json");
            // Only explicit user options: no runtime or game data are persisted.
            var value=new {Enabled,Boxes,Distances,Radar,Status,TextSize,MaxDistance,RadarRange,BoxColor,BoxStyle,PracticeCalibration,FovAxis,EdgeIndicators,RefreshHz,BoxThickness,RadarRadius,RadarBottomGap,Skeleton,HideAllies,HideUnknownTeams,RadarNorthUp,RadarAutoZoom,RadarLabels,RadarAltitude,RadarBorder,RadarHeadingTick};
            File.WriteAllText(path,new JavaScriptSerializer().Serialize(value));
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }
}

public sealed class EspMenu:Form {
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr w,IntPtr l);
    readonly EspSettings settings;
    readonly Action changed,closeEsp;
    readonly Color accent=Color.FromArgb(85,215,255);
    readonly Font menuFont=new Font("Segoe UI",5.5f,FontStyle.Regular);
    int cursorY=83;
    public EspMenu(EspSettings value,Action update,Action close) {
        settings=value;changed=update;closeEsp=close;
        Text="ESP · Ajustes de superposicion";Size=new Size(400,745);FormBorderStyle=FormBorderStyle.None;
        AutoScroll=true;AutoScrollMinSize=new Size(380,745);
        BackColor=Color.FromArgb(21,25,33);ForeColor=Color.White;Font=menuFont;TopMost=true;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
        var header=new Panel{Location=new Point(0,0),Size=new Size(400,65),BackColor=Color.FromArgb(30,37,49)};
        header.MouseDown+=(sender,e)=>{if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};
        var title=new Label{Text="ESP  /  AJUSTES",Location=new Point(20,20),AutoSize=true,ForeColor=accent};
        header.Controls.Add(title);
        var hide=new Button{Text="×",Location=new Point(352,12),Size=new Size(34,36),FlatStyle=FlatStyle.Flat,BackColor=header.BackColor,ForeColor=Color.White};hide.FlatAppearance.BorderSize=0;hide.Click+=(sender,e)=>Hide();header.Controls.Add(hide);Controls.Add(header);
        Toggle("ESP activado",settings.Enabled,v=>settings.Enabled=v);
        Toggle("Hide confirmed or marked teammates",settings.HideAllies,v=>settings.HideAllies=v);
        Toggle("Also hide unknown teams",settings.HideUnknownTeams,v=>settings.HideUnknownTeams=v);
        Controls.Add(new Label{Text="F6: mark/unmark teammate under crosshair\nF7: clear marks for this session",Location=new Point(20,cursorY),Size=new Size(360,40),ForeColor=accent,Font=new Font("Segoe UI",4.5f)});cursorY+=46;
        Toggle("Postura de huesos validados",settings.Skeleton,v=>settings.Skeleton=v);
        Toggle("Distances",settings.Distances,v=>settings.Distances=v);
        Toggle("Radar · left side, above health",settings.Radar,v=>settings.Radar=v);
        Toggle("Radar north-up (off = player-up)",settings.RadarNorthUp,v=>settings.RadarNorthUp=v);
        Toggle("Radar automatic 100/200 m zoom",settings.RadarAutoZoom,v=>settings.RadarAutoZoom=v);
        Toggle("Radar player distance labels",settings.RadarLabels,v=>settings.RadarLabels=v);
        Toggle("Radar altitude indicators",settings.RadarAltitude,v=>settings.RadarAltitude=v);
        Toggle("Radar border and range rings",settings.RadarBorder,v=>settings.RadarBorder=v);
        Toggle("Radar heading tick",settings.RadarHeadingTick,v=>settings.RadarHeadingTick=v);
        Toggle("Mostrar estado",settings.Status,v=>settings.Status=v);
        Number("Tamano de letra",settings.TextSize,6,20,1,v=>settings.TextSize=v);
        Number("Actualizacion (Hz)",settings.RefreshHz,20,60,5,v=>settings.RefreshHz=v);
        Number("Maximum distance (m)",settings.MaxDistance,10,2000,10,v=>settings.MaxDistance=v);
        Number("Alcance del radar (m)",settings.RadarRange,10,1000,10,v=>settings.RadarRange=v);
        Number("Tamano del radar",settings.RadarRadius,60,140,5,v=>settings.RadarRadius=v);
        Number("Altura del radar",settings.RadarBottomGap,120,450,10,v=>settings.RadarBottomGap=v);
        var colorLabel=new Label{Text="Player color",Location=new Point(20,cursorY+4),AutoSize=true};Controls.Add(colorLabel);
        var colors=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(245,cursorY),Size=new Size(135,30),BackColor=Color.FromArgb(35,43,55),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        colors.Items.AddRange(new object[]{"Azul","Verde","Blanco","Amarillo","Violeta"});colors.SelectedItem=settings.BoxColor;if(colors.SelectedIndex<0)colors.SelectedIndex=0;
        colors.SelectedIndexChanged+=(sender,e)=>{settings.BoxColor=(string)colors.SelectedItem;Commit();};Controls.Add(colors);cursorY+=40;
        Controls.Add(new Label{Text="Eje del FOV",Location=new Point(20,cursorY+4),AutoSize=true});
        var axes=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(245,cursorY),Size=new Size(135,30),BackColor=colors.BackColor,ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        axes.Items.AddRange(new object[]{"Horizontal","Vertical"});axes.SelectedItem=settings.FovAxis;axes.SelectedIndexChanged+=(sender,e)=>{settings.FovAxis=(string)axes.SelectedItem;Commit();};Controls.Add(axes);cursorY+=40;
        var quit=new Button{Text="Cerrar ESP  ·  F10",Location=new Point(20,cursorY+55),Size=new Size(360,36),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(35,43,55),ForeColor=Color.White};quit.FlatAppearance.BorderColor=Color.FromArgb(62,76,96);quit.Click+=(sender,e)=>closeEsp();Controls.Add(quit);
        Controls.Add(new Label{Text="DELETE opens or closes this menu",Location=new Point(20,cursorY+105),AutoSize=true,ForeColor=accent,Font=new Font("Segoe UI",4.5f)});
        AutoScrollMinSize=new Size(380,cursorY+150);
    }
    void Toggle(string text,bool initial,Action<bool> set) {
        var box=new CheckBox{Text=text,Checked=initial,Location=new Point(20,cursorY),Size=new Size(360,28),FlatStyle=FlatStyle.Flat};
        box.CheckedChanged+=(sender,e)=>{set(box.Checked);Commit();};Controls.Add(box);cursorY+=34;
    }
    void Number(string text,int initial,int min,int max,int step,Action<int> set) {
        Controls.Add(new Label{Text=text,Location=new Point(20,cursorY+4),AutoSize=true});
        var number=new NumericUpDown{Minimum=min,Maximum=max,Increment=step,Value=initial,Location=new Point(275,cursorY),Size=new Size(105,30),BackColor=Color.FromArgb(35,43,55),ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle};
        number.ValueChanged+=(sender,e)=>{set((int)number.Value);Commit();};Controls.Add(number);cursorY+=38;
    }
    void Commit(){settings.Save();changed();}
    protected override void OnFormClosing(FormClosingEventArgs e){if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}base.OnFormClosing(e);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(accent))e.Graphics.DrawRectangle(pen,0,0,ClientSize.Width-1,ClientSize.Height-1);}
    protected override void Dispose(bool disposing){if(disposing)menuFont.Dispose();base.Dispose(disposing);}
}
