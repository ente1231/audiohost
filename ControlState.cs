using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PanicControl;

internal sealed record ActionInfo(string Id,int Category);
internal sealed class Account
{
    public string Name {get;set;}="";
    public string Salt {get;set;}="";
    public string Hash {get;set;}="";
    public string Permissions {get;set;}=new('0',32);
    public bool CooldownEnabled {get;set;}=true;
}
internal sealed class Settings
{
    public int PermissionCount {get;set;}=90;
    public List<Account> Accounts {get;set;}=new();
    public int[] Cooldowns {get;set;}={2,5,15};
    public string Launcher {get;set;}="";
}
internal sealed class ControlState
{
    public const int PasswordIterations=210000;
    public readonly object Gate=new();
    public Settings Settings {get;private set;}
    public ActionInfo[] Actions {get;}
    // permission bitmap width follows the action list, not a fixed 128 bits
    public int PermBytes=>(Actions.Length+7)/8;
    public NativeWindows Native {get;}
    public bool Paused {get;set;}
    public event Action? Changed;
    // fires after the uninstall script is scheduled; the host closes the app here
    public event Action? SelfDestructRequested;
    private readonly string settingsPath;
    private readonly Dictionary<string,(string User,long Expires)> sessions=new();
    private readonly Dictionary<(string User,string Action),long> deadlines=new();
    private readonly Dictionary<string,string> effects=new();
    private readonly Dictionary<string,long> lastEffect=new();
    private readonly List<Peer> peers=new();
    private string? explorerOwner;
    private readonly Dictionary<string,string> overlayOwners=new();
    private readonly Dictionary<int,(long At,byte[] Bytes)> previewFrames=new();
    private int drift=3,jitter=6,bungeeStep,labDx=5,labDy=5;
    private double angle;
    private long nextBeep=Environment.TickCount64+180000;
    private static long Now=>Environment.TickCount64;
    private static readonly HashSet<string> ToggleIds=new(StringComparer.Ordinal){"driftLeft","driftRight","driftUp","driftDown","jitter","teleport","circle","autoClick","bungee","labyrinth","scrollWheel","boomerang","windowDance","beep"};
    public ControlState(string dataPath,bool dryRun)
    {
        Directory.CreateDirectory(dataPath);settingsPath=Path.Combine(dataPath,"accounts.json");
        Actions=JsonSerializer.Deserialize<ActionInfo[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"actions.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Aktionsliste fehlt.");
        Settings=File.Exists(settingsPath)?JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath))??throw new InvalidDataException("Kontodatei beschädigt."):new Settings();
        if(Settings.PermissionCount<90||Settings.Cooldowns.Length!=3||Settings.Cooldowns.Any(x=>x<0||x>3600)||Settings.Accounts.Count>12||Settings.Accounts.Any(a=>!ValidName(a.Name))||Settings.Accounts.Select(a=>a.Name).Distinct().Count()!=Settings.Accounts.Count)
            throw new InvalidDataException("Kontodatei beschädigt; bitte aus Sicherung wiederherstellen.");
        foreach(var a in Settings.Accounts) { if(Convert.FromBase64String(a.Salt).Length!=16||Convert.FromBase64String(a.Hash).Length!=32)throw new InvalidDataException("Passwortdaten beschädigt."); }
        MigratePermissions();
        if(Settings.Accounts.Any(a=>!ValidPermissions(a.Permissions)))throw new InvalidDataException("Kontodatei beschädigt; bitte aus Sicherung wiederherstellen.");
        if(Settings.Accounts.Count>0&&!Settings.Accounts.Any(a=>Allowed(a,"admin")))throw new InvalidDataException("Kein Administrator in der Kontodatei.");
        Native=new NativeWindows(dryRun);
    }
    // Das Rechtfeld ist ein Bitfeld, dessen Breite die Zahl der Aktionen ist.
    // Waechst die Aktionsliste ueber eine Byte-Grenze (187 -> 217), passt ein
    // gespeicherter Blob nicht mehr in die neue Breite und die Kontodatei waere
    // unlesbar. Deshalb hier auffuellen: bestehende Bits bleiben an ihrem
    // Platz, Administratoren bekommen die neu hinzugekommenen Rechte.
    private void MigratePermissions()
    {
        var oldCount=Math.Clamp(Settings.PermissionCount,0,Actions.Length);
        if(Settings.PermissionCount==Actions.Length&&Settings.Accounts.All(a=>a.Permissions.Length==PermBytes*2))return;
        var adminIndex=Array.FindIndex(Actions,a=>a.Id=="admin");
        foreach(var a in Settings.Accounts)
        {
            if(!Regex.IsMatch(a.Permissions,"^[0-9a-fA-F]+$"))throw new InvalidDataException("Kontodatei beschädigt; bitte aus Sicherung wiederherstellen.");
            var old=Convert.FromHexString(a.Permissions.Length%2==0?a.Permissions:"0"+a.Permissions);
            var bits=new byte[PermBytes];
            Array.Copy(old,bits,Math.Min(old.Length,PermBytes));
            // Alte Blobs sind schmaler: die Bit-Position von "admin" bleibt
            // innerhalb der alten Breite, deshalb ist die Pruefung hier sicher.
            if(adminIndex>=0&&(bits[adminIndex/8]&(1<<(adminIndex%8)))!=0)
                for(int i=oldCount;i<Actions.Length;i++)bits[i/8]|=(byte)(1<<(i%8));
            a.Permissions=Convert.ToHexString(bits).ToLowerInvariant();
        }
        Settings.PermissionCount=Actions.Length;Save();
    }
    public Account? User(string name)=>Settings.Accounts.FirstOrDefault(a=>a.Name==name);

    // single entry point for both the phone UI path and the relay path
    public void RequestSelfDestruct()
    {
        Native.SelfDestruct();
        SelfDestructRequested?.Invoke();
    }
    public bool Allowed(Account account,string action)
    {
        if(action=="ping"||action=="heartbeat")return true;
        var index=Array.FindIndex(Actions,a=>a.Id==action);if(index<0)return false;
        var b=Convert.FromHexString(account.Permissions);return (b[index/8]&(1<<(index%8)))!=0;
    }
    public string PermissionsFor(params string[] ids)
    {
        var bytes=new byte[PermBytes];foreach(var id in ids){int i=Array.FindIndex(Actions,a=>a.Id==id);if(i<0)throw new ArgumentException(id);bytes[i/8]|=(byte)(1<<(i%8));}return Convert.ToHexString(bytes).ToLowerInvariant();
    }
    private static bool ValidName(string s)=>Regex.IsMatch(s,"^[A-Za-z0-9_-]{3,24}$");
    private bool ValidPermissions(string s)=>s.Length==PermBytes*2&&Regex.IsMatch(s,"^[0-9a-fA-F]+$")&&Convert.FromHexString(s).SelectMany(b=>Enumerable.Range(0,8).Select(i=>(b>>i)&1)).Skip(Actions.Length).All(x=>x==0);
    private static bool ValidPassword(string s)=>s.Length is >=8 and <=64;
    private static void SetPassword(Account a,string password)
    {
        if(!ValidPassword(password))throw new InvalidOperationException("Passwort: 8 bis 64 Zeichen.");
        var salt=RandomNumberGenerator.GetBytes(16);a.Salt=Convert.ToBase64String(salt);
        a.Hash=Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password,salt,PasswordIterations,HashAlgorithmName.SHA256,32));
    }
    public void SetupAdmin(string name,string password)
    {
        lock(Gate){if(Settings.Accounts.Count>0)throw new InvalidOperationException("Administrator existiert bereits.");if(!ValidName(name))throw new InvalidOperationException("Name: 3–24 Zeichen (Buchstaben, Zahlen, _ oder -).");
        var a=new Account{Name=name,Permissions=PermissionsFor(Actions.Select(x=>x.Id).ToArray())};SetPassword(a,password);Settings.Accounts.Add(a);Save();}
    }
    public string? Authenticate(string name,string password)
    {
        lock(Gate)
        {
            var a=User(name);var salt=a is null?new byte[16]:Convert.FromBase64String(a.Salt);var expected=a is null?new byte[32]:Convert.FromBase64String(a.Hash);
            var actual=Rfc2898DeriveBytes.Pbkdf2(password,salt,PasswordIterations,HashAlgorithmName.SHA256,32);
            if(a is null||!CryptographicOperations.FixedTimeEquals(expected,actual))return null;
            foreach(var key in sessions.Where(x=>x.Value.Expires<Now).Select(x=>x.Key).ToArray())sessions.Remove(key);
            if(sessions.Count>=64)sessions.Remove(sessions.Keys.First());
            var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));sessions.Add(token,(name,Now+24*60*60*1000));return token;
        }
    }
    public Account? Session(string? token)
    {
        lock(Gate){return token is not null&&sessions.TryGetValue(token,out var s)&&s.Expires>Now?User(s.User):null;}
    }
    public void Logout(string? token) {lock(Gate){if(token is not null)sessions.Remove(token);foreach(var p in peers.Where(p=>p.Token==token).ToArray()){p.Close();Detach(p);}}}
    public void Save()
    {
        var temp=settingsPath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(Settings,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,settingsPath,true);Changed?.Invoke();
    }
    public int Connections {get {lock(Gate)return peers.Count;}}
    public int LivePreviews {get{lock(Gate)return peers.Count(p=>p.PreviewUntil>Now);}}
    public byte[] Preview(string token,int screen,string? peerId=null)
    {
        lock(Gate)
        {
            var user=Session(token);var p=peers.FirstOrDefault(p=>p.Token==token&&p.PreviewUntil>Now&&(peerId is null||p.Id==peerId));
            if(Paused||user is null||!Allowed(user,"livePreview")||p is null)throw new InvalidOperationException("Live-Vorschau zuerst starten.");
            if(Now-p.PreviewLast<900)throw new PreviewRateException();
            p.PreviewLast=Now;p.LastSeen=Now;p.PreviewUntil=Now+8000;
            if(previewFrames.TryGetValue(screen,out var cached)&&Now-cached.At<900)return cached.Bytes;
            var bytes=Native.Screenshot(screen,960);previewFrames[screen]=(Now,bytes);return bytes;
        }
    }
    internal sealed class PreviewRateException : Exception { }
    public void Attach(Peer p)
    {
        lock(Gate){var a=Session(p.Token);if(a is null||Paused)throw new InvalidOperationException("Bitte neu anmelden.");if(peers.Count>=16)throw new InvalidOperationException("Zu viele Verbindungen.");peers.Add(p);p.User=a.Name;
        p.Send(new{ev="loginOk",user=a.Name,admin=Allowed(a,"admin"),items=Actions.Select(a=>a.Id),pbytes=PermBytes,perms=a.Permissions,cooldowns=Cooldowns(a),cooldownSeconds=Settings.Cooldowns,effects=ToggleIds.ToDictionary(x=>x,x=>effects.ContainsKey(x)),filterRecovery=false});Online();Changed?.Invoke();}
    }
    public void Detach(Peer p)
    {
        lock(Gate){if(!peers.Remove(p))return;p.MouseGesture=false;p.PreviewUntil=0;if(LivePreviews==0)previewFrames.Clear();
        if(!peers.Any(x=>x.User==p.User))StopOwned(p.User,null);
        if(peers.Count==0)Reset();Online();Changed?.Invoke();}
    }
    private void Broadcast(object message) {foreach(var p in peers.ToArray())p.Send(message);}
    private void ToUser(string name,object message) {foreach(var p in peers.Where(p=>p.User==name).ToArray())p.Send(message);}
    private void Online()=>Broadcast(new{ev="online",users=peers.Select(p=>p.User).Distinct()});
    private void UserList(Peer peer)=>peer.Send(new{ev="userList",items=Actions.Select(a=>a.Id),pbytes=PermBytes,users=Settings.Accounts.Select(a=>new{n=a.Name,p=a.Permissions,a=Allowed(a,"admin"),cd=a.CooldownEnabled})});
    private void AdminLists(){foreach(var p in peers.ToArray()){var a=Session(p.Token);if(a is not null&&Allowed(a,"admin"))UserList(p);}}
    private Dictionary<string,long> Cooldowns(Account a)=>!a.CooldownEnabled?new():deadlines.Where(x=>x.Key.User==a.Name&&x.Value>Now).ToDictionary(x=>x.Key.Action,x=>Math.Max(0,x.Value-Now));
    public bool Begin(Account a,string action,Peer? peer=null)
    {
        if(Paused)throw new InvalidOperationException("Fernzugriff ist am PC pausiert.");
        if(!Allowed(a,action))throw new InvalidOperationException("Keine Berechtigung für diese Funktion.");
        var info=Actions.FirstOrDefault(x=>x.Id==action);if(info is null||info.Category==0||!a.CooldownEnabled)return true;
        var key=(a.Name,action);if(deadlines.TryGetValue(key,out var until)&&until>Now){ToUser(a.Name,new{ev="cooldown",action,ms=until-Now});return false;}
        var ms=Settings.Cooldowns[info.Category-1]*1000;if(ms>0){deadlines[key]=Now+ms;ToUser(a.Name,new{ev="cooldown",action,ms});}return true;
    }
    private void ClearCooldown(string user,string action)=>deadlines.Remove((user,action));
    public void Handle(Peer peer,JsonElement msg)
    {
        lock(Gate)
        {
            var a=Session(peer.Token);if(a is null){peer.Send(new{ev="resumeFail"});peer.Close();return;}
            peer.LastSeen=Now;
            try
            {
                var cmd=Text(msg,"cmd",24);
                if(cmd=="heartbeat")return;
                if(cmd=="preview")
                {
                    var enabled=Boolean(msg,"value");
                    if(enabled&&peer.PreviewUntil<=Now){if(LivePreviews>=4)throw new InvalidOperationException("Maximal vier Live-Vorschauen gleichzeitig.");if(!Begin(a,"livePreview",peer))return;}
                    if(enabled&&!Allowed(a,"livePreview"))throw new InvalidOperationException("Keine Berechtigung für die Live-Vorschau.");
                    peer.PreviewUntil=enabled?Now+8000:0;peer.PreviewLast=0;peer.Send(new{ev="previewState",active=enabled,lease=enabled?peer.Id:""});if(LivePreviews==0)previewFrames.Clear();return;
                }
                if(cmd=="admin"){if(!Allowed(a,"admin"))throw new InvalidOperationException("Keine Admin-Berechtigung.");Admin(peer,msg);return;}
                if(Paused)throw new InvalidOperationException("Fernzugriff pausiert.");
                if(cmd=="mouse"){Mouse(peer,a,msg);return;}
                if(cmd=="slider")
                {
                    var name=Text(msg,"name",20);var val=Integer(msg,"value",1,name=="jitter"?20:10);
                    if(name=="drift"&&new[]{"driftLeft","driftRight","driftUp","driftDown"}.Any(id=>Allowed(a,id)))drift=val;
                    else if(name=="jitter"&&Allowed(a,"jitter"))jitter=val;
                    else throw new InvalidOperationException("Keine Berechtigung für diesen Regler.");return;
                }
                if(cmd=="toggle")
                {
                    var name=Text(msg,"name",24);var enabled=Boolean(msg,"value");
                    if(!ToggleIds.Contains(name)||!Allowed(a,name))throw new InvalidOperationException("Keine Berechtigung oder unbekannter Effekt.");
                    if(enabled&&!effects.ContainsKey(name)&&!Begin(a,name,peer))return;
                    if(name=="windowDance"&&enabled&&!effects.ContainsKey(name))Native.SetDance(true);
                    if(enabled){if(!effects.ContainsKey(name))effects[name]=a.Name;}else {effects.Remove(name);if(name=="windowDance")Native.SetDance(false);}
                    Broadcast(new{ev="effectState",name,value=enabled});return;
                }
                var id=cmd=="action"?Text(msg,"action",24):cmd;
                string? text=null;
                if(id is "typeText" or "iSeeYou" or "browserSearch" or "showMessage")text=Text(msg,"text",id=="showMessage"?240:512,false);
                if(id=="clipboardWrite")text=Text(msg,"text",4096);
                if(id=="screenColor"){text=Text(msg,"color",8,false);if(text is not ("red" or "green" or "blue" or "white" or "black"))throw new InvalidOperationException("Ungültige Testfarbe.");}
                var seconds=id=="countdown"?Integer(msg,"seconds",1,600):60;
                string? window=id is "windowFocus" or "windowMinimize" or "windowMaximize" or "windowRestore"?Text(msg,"window",32,false):null;
                if(id=="admin"||id=="mouseMove"||ToggleIds.Contains(id)||id is "screenshotView" or "pcStats" or "windowList" or "clipboardRead" or "livePreview")throw new InvalidOperationException("Ungültige Aktion.");
                if(id=="ctrlAltDel")throw new InvalidOperationException("Strg+Alt+Entf muss am PC selbst gedrückt werden.");
                if(id=="launchApp"&&(string.IsNullOrEmpty(Settings.Launcher)||!File.Exists(Settings.Launcher)))throw new InvalidOperationException("Erst im PC-Fenster eine Anwendung auswählen.");
                if(!Begin(a,id,peer))return;
                try{if(id=="stopAll")Reset();else if(id=="selfDestruct")RequestSelfDestruct();else if(window is not null)Native.WindowAction(id,window);else if(id=="clipboardWrite")Native.WriteClipboard(text!);else {Native.Execute(id,text,Settings.Launcher,seconds);if(id=="winELoop")explorerOwner=a.Name;if(NativeWindows.OverlayActions.Contains(id))overlayOwners[id]=a.Name;if(id=="closeOverlays")overlayOwners.Clear();}}
                catch{ClearCooldown(a.Name,id);ToUser(a.Name,new{ev="cooldownState",cooldowns=Cooldowns(a)});throw;}
                peer.Send(new{toast=id=="winELoop"?"Explorer-Flut gestartet · 10 × Win+E":id=="ping"?"Pong! Verbindung OK":id=="shutdown"?"Herunterfahren in 15 Sekunden":id=="filterOff"?"Filter Keys ausgeschaltet":"Aktion ausgeführt",type="success"});Changed?.Invoke();
            }
            catch(Exception ex) when(ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception or InvalidDataException or System.Runtime.InteropServices.ExternalException)
            {peer.Send(new{toast=ex is System.Runtime.InteropServices.ExternalException?"Windows konnte die Aktion nicht ausführen. Die Zwischenablage kann kurz durch eine andere Anwendung belegt sein.":ex.Message,type="error"});}
        }
    }
    private void Mouse(Peer p,Account a,JsonElement msg)
    {
        var phase=Text(msg,"phase",8);if(phase=="end"){p.MouseGesture=false;return;}
        if(!Allowed(a,"mouseMove")){p.MouseGesture=false;throw new InvalidOperationException("Keine Maus-Berechtigung.");}
        if(p.MouseGesture&&(Now-p.MouseLast>1500||Now-p.MouseStart>=30000))p.MouseGesture=false;
        if(phase=="start")
        {
            if(!p.MouseGesture&&!Begin(a,"mouseMove",p)){p.Send(new{ev="mouseGesture",active=false});return;}
            if(!p.MouseGesture)p.MouseStart=Now;p.MouseLast=Now;p.MouseFrame=Now-34;p.MouseGesture=true;p.Send(new{ev="mouseGesture",active=true});return;
        }
        if(phase!="move")throw new InvalidOperationException("Ungültige Mausphase.");
        if(!p.MouseGesture){p.Send(new{ev="mouseGesture",active=false});return;}
        var dx=Integer(msg,"dx",-127,127);var dy=Integer(msg,"dy",-127,127);if(Now-p.MouseFrame<25)return;
        p.MouseLast=p.MouseFrame=Now;if(dx!=0||dy!=0)Native.Move(dx,dy);
    }
    private void Admin(Peer p,JsonElement msg)
    {
        var op=Text(msg,"op",24);if(op=="list"){UserList(p);p.Send(new{ev="cooldownConfig",seconds=Settings.Cooldowns});return;}
        if(op=="setCooldowns")
        {
            if(!msg.TryGetProperty("seconds",out var values)||values.ValueKind!=JsonValueKind.Array||values.GetArrayLength()!=3)throw new InvalidOperationException("Drei Zeiten erforderlich.");
            var times=values.EnumerateArray().Select(x=>x.TryGetInt32(out int n)&&n>=0&&n<=3600?n:throw new InvalidOperationException("Cooldown: 0–3600 Sekunden.")).ToArray();
            Settings.Cooldowns=times;foreach(var key in deadlines.Keys.ToArray()){var cat=Actions.First(a=>a.Id==key.Action).Category;if(cat>0&&times[cat-1]==0)deadlines.Remove(key);}
            Save();Broadcast(new{ev="cooldownConfig",seconds=times});foreach(var peer in peers){var u=User(peer.User);if(u is not null)peer.Send(new{ev="cooldownState",cooldowns=Cooldowns(u)});}return;
        }
        var name=Text(msg,"user",24);var account=User(name);
        if(op=="add")
        {
            if(!ValidName(name)||account is not null||Settings.Accounts.Count>=12)throw new InvalidOperationException("Name ungültig, bereits vergeben oder 12 Accounts erreicht.");
            var perms=Text(msg,"perms",PermBytes*2);if(!ValidPermissions(perms))throw new InvalidOperationException("Ungültige Rechte.");
            var n=new Account{Name=name,Permissions=perms};SetPassword(n,Text(msg,"pw",64));Settings.Accounts.Add(n);Save();AdminLists();return;
        }
        if(account is null)throw new InvalidOperationException("Benutzer nicht gefunden.");
        if(op=="setUserCooldown")
        {
            account.CooldownEnabled=Boolean(msg,"enabled");if(!account.CooldownEnabled)foreach(var key in deadlines.Keys.Where(k=>k.User==name).ToArray())deadlines.Remove(key);
            Save();ToUser(name,new{ev="cooldownState",cooldowns=Cooldowns(account)});AdminLists();return;
        }
        if(op=="setPerms")
        {
            var perms=Text(msg,"perms",PermBytes*2);if(!ValidPermissions(perms))throw new InvalidOperationException("Ungültige Rechte.");
            if(Allowed(account,"admin")&&!Allowed(new Account{Permissions=perms},"admin")&&Settings.Accounts.Count(a=>Allowed(a,"admin"))==1)throw new InvalidOperationException("Mindestens ein Admin muss bleiben.");
            if(perms!=account.Permissions){account.Permissions=perms;StopOwned(name,account);foreach(var peer in peers.Where(x=>x.User==name))peer.MouseGesture=false;Save();ToUser(name,new{ev="permissionsChanged"});}AdminLists();return;
        }
        if(op=="del")
        {
            if(Allowed(account,"admin")&&Settings.Accounts.Count(a=>Allowed(a,"admin"))==1)throw new InvalidOperationException("Letzten Admin kann man nicht löschen.");
            Invalidate(name);Settings.Accounts.Remove(account);foreach(var key in deadlines.Keys.Where(k=>k.User==name).ToArray())deadlines.Remove(key);Save();AdminLists();return;
        }
        if(op=="setPass"){SetPassword(account,Text(msg,"pw",64));Invalidate(name);Save();AdminLists();return;}
        throw new InvalidOperationException("Unbekannte Admin-Aktion.");
    }
    private void Invalidate(string name)
    {
        foreach(var key in sessions.Where(s=>s.Value.User==name).Select(s=>s.Key).ToArray())sessions.Remove(key);
        StopOwned(name,null);foreach(var p in peers.Where(p=>p.User==name).ToArray()){p.Send(new{ev="resumeFail"});p.Close();Detach(p);}
    }
    private void StopOwned(string name,Account? current)
    {
        foreach(var id in overlayOwners.Where(x=>x.Value==name&&(current is null||!Allowed(current,x.Key))).Select(x=>x.Key).ToArray()){Native.CloseOverlay(id);overlayOwners.Remove(id);}
        foreach(var p in peers.Where(p=>p.User==name&&(current is null||!Allowed(current,"livePreview")))){p.PreviewUntil=0;p.Send(new{ev="previewState",active=false});}if(LivePreviews==0)previewFrames.Clear();
        if(explorerOwner==name&&(current is null||!Allowed(current,"winELoop"))){Native.CancelExplorerBurst();explorerOwner=null;}
        foreach(var id in effects.Where(e=>e.Value==name&&(current is null||!Allowed(current,e.Key))).Select(e=>e.Key).ToArray())
        {effects.Remove(id);if(id=="windowDance")Native.SetDance(false);Broadcast(new{ev="effectState",name=id,value=false});}
    }
    public void Reset()
    {
        lock(Gate){explorerOwner=null;overlayOwners.Clear();previewFrames.Clear();foreach(var id in effects.Keys.ToArray())Broadcast(new{ev="effectState",name=id,value=false});effects.Clear();
        foreach(var p in peers){p.MouseGesture=false;p.PreviewUntil=0;p.Send(new{ev="previewState",active=false});p.Send(new{ev="mouseGesture",active=false});}
        try{Native.Restore();}catch(Exception ex){Broadcast(new{toast=ex.Message,type="error"});throw;}Changed?.Invoke();}
    }
    public void Pause()
    {
        lock(Gate){Paused=true;Reset();sessions.Clear();foreach(var p in peers.ToArray()){p.Close();Detach(p);}Changed?.Invoke();}
    }
    public void Tick()
    {
        lock(Gate)
        {
            foreach(var key in deadlines.Where(x=>x.Value<=Now).Select(x=>x.Key).ToArray())deadlines.Remove(key);
            foreach(var p in peers.ToArray())
            {
                if(Session(p.Token) is null||Now-p.LastSeen>45000){p.Close();Detach(p);continue;}
                if(p.MouseGesture&&(Now-p.MouseLast>1500||Now-p.MouseStart>30000)){p.MouseGesture=false;p.Send(new{ev="mouseGesture",active=false});}
                if(p.PreviewUntil>0&&p.PreviewUntil<=Now){p.PreviewUntil=0;p.Send(new{ev="previewState",active=false});}
            }
            if(LivePreviews==0)previewFrames.Clear();
            foreach(var id in overlayOwners.Keys.Except(Native.ActiveOverlays).ToArray())overlayOwners.Remove(id);
            if(Paused)return;Native.PresentationTick(Now);
            if(effects.Count==0&&!Native.ExplorerActive)return;
            try
            {
                Native.CheckDesktop();Native.ExplorerTick(Now);if(!Native.ExplorerActive)explorerOwner=null;Native.DanceTick(Now);int dx=0,dy=0;
                if(effects.ContainsKey("driftLeft"))dx-=drift;if(effects.ContainsKey("driftRight"))dx+=drift;
                if(effects.ContainsKey("driftUp"))dy-=drift;if(effects.ContainsKey("driftDown"))dy+=drift;if(dx!=0||dy!=0)Native.Move(dx,dy);
                foreach(var id in effects.Keys.ToArray())
                {
                    var interval=id switch{"jitter"=>60,"teleport"=>3000,"autoClick"=>5000,"labyrinth"=>60,"scrollWheel"=>200,"boomerang"=>700,"windowDance"=>160,"beep"=>180000,_=>30};
                    if(lastEffect.TryGetValue(id,out var t)&&Now-t<interval)continue;lastEffect[id]=Now;
                    switch(id)
                    {
                        case "jitter":Native.Move(Random.Shared.Next(-jitter,jitter+1),Random.Shared.Next(-jitter,jitter+1));break;
                        case "teleport":Native.Move(Random.Shared.Next(-200,201),Random.Shared.Next(-200,201));break;
                        case "circle":angle+=0.15;Native.Move((int)(Math.Cos(angle)*5),(int)(Math.Sin(angle)*5));break;
                        case "autoClick":Native.Click();break;
                        case "bungee":bungeeStep++;Native.Move(bungeeStep%30==0?45:-2,bungeeStep%30==0?28:-1);break;
                        case "labyrinth":if(Random.Shared.Next(8)==0)labDx=-labDx;if(Random.Shared.Next(8)==0)labDy=-labDy;Native.Move(labDx,labDy);break;
                        case "scrollWheel":Native.Scroll(Random.Shared.Next(2)==0?3:-3);break;
                        case "boomerang":int x=Random.Shared.Next(-100,101),y=Random.Shared.Next(-100,101);Native.Move(x,y);Native.Move(-x/2,-y/2);break;
                        case "windowDance":break; // Native timer repeats arrows while holding Win.
                        case "beep":if(Now>=nextBeep){Native.Beep();nextBeep=Now+Random.Shared.Next(180000,420001);}break;
                    }
                }
            }
            catch{Reset();Broadcast(new{toast="Effekte gestoppt: Windows nimmt keine Eingaben an.",type="error"});}
        }
    }
    public object Snapshot()=>new{paused=Paused,connections=Connections,previews=LivePreviews,overlays=Native.ActiveOverlays,effects=effects.Keys.ToArray(),simulated=Native.Simulated};
    public static string Text(JsonElement e,string property,int max,bool empty=true)
    {
        if(!e.TryGetProperty(property,out var v)||v.ValueKind!=JsonValueKind.String)throw new InvalidOperationException("Ungültiges Feld: "+property);
        var s=v.GetString()!;if(s.Length>max||(!empty&&s.Length==0)||s.Contains('\0'))throw new InvalidOperationException("Ungültige Länge: "+property);return s;
    }
    public static bool Boolean(JsonElement e,string name)=>e.TryGetProperty(name,out var v)&&v.ValueKind is JsonValueKind.True or JsonValueKind.False?v.GetBoolean():throw new InvalidOperationException("Boolescher Wert erforderlich.");
    public static int Integer(JsonElement e,string name,int min,int max)=>e.TryGetProperty(name,out var v)&&v.TryGetInt32(out int n)&&n>=min&&n<=max?n:throw new InvalidOperationException("Ungültiger Zahlenwert.");
}
