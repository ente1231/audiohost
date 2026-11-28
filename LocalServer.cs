using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace PanicControl;

internal sealed record LanAddress(IPAddress Ip,int Prefix,string Name)
{
    public override string ToString()=>$"{Name} · {Ip}";
    public static List<LanAddress> Discover()
    {
        var addresses=new List<LanAddress>();
        foreach(var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211))
            foreach(var a in nic.GetIPProperties().UnicastAddresses.Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork&&a.PrefixLength>=8&&Private(a.Address)))addresses.Add(new(a.Address,a.PrefixLength,nic.Name));
        return addresses;
    }
    public static bool Private(IPAddress ip){var b=ip.GetAddressBytes();return b.Length==4&&(b[0]==10||b[0]==192&&b[1]==168||b[0]==172&&b[1]>=16&&b[1]<=31);}
    public bool Allows(IPAddress? remote)
    {
        if(remote is null)return false;if(remote.IsIPv4MappedToIPv6)remote=remote.MapToIPv4();if(IPAddress.IsLoopback(remote))return true;
        if(!Private(remote))return false;var a=Ip.GetAddressBytes();var b=remote.GetAddressBytes();if(a.Length!=4||b.Length!=4)return false;
        uint x=0,y=0;for(int i=0;i<4;i++){x=(x<<8)|a[i];y=(y<<8)|b[i];}var mask=uint.MaxValue<<(32-Prefix);return (x&mask)==(y&mask);
    }
}
internal sealed class Peer
{
    public string Id {get;}=Guid.NewGuid().ToString("N");
    public string Token {get;}
    public string User {get;set;}="";
    public long LastSeen=Environment.TickCount64,MouseStart,MouseLast,MouseFrame;
    public bool MouseGesture;
    public long PreviewUntil,PreviewLast;
    public WebSocket Socket {get;}
    public CancellationTokenSource Cancel {get;}=new();
    private readonly Channel<byte[]> queue=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64){SingleReader=true,FullMode=BoundedChannelFullMode.Wait});
    public Peer(WebSocket socket,string token){Socket=socket;Token=token;}
    public void Send(object message){if(Cancel.IsCancellationRequested)return;if(!queue.Writer.TryWrite(JsonSerializer.SerializeToUtf8Bytes(message)))Close();}
    public void Close(){Cancel.Cancel();queue.Writer.TryComplete();Socket.Abort();}
    public async Task SendLoop()
    {
        try{await foreach(var data in queue.Reader.ReadAllAsync(Cancel.Token))await Socket.SendAsync(data,WebSocketMessageType.Text,true,Cancel.Token);}
        catch(Exception ex) when(ex is WebSocketException or OperationCanceledException or ObjectDisposedException){Close();}
    }
}
internal sealed class LocalServer
{
    private WebApplication? app;
    private X509Certificate2? activeCertificate;
    private readonly ControlState state;
    private readonly Func<Action,Task> dispatch;
    private readonly string dataPath;
    private readonly Dictionary<string,(int Count,long Window)> attempts=new();
    private readonly object loginGate=new();
    private int activeLogins;
    public string Fingerprint {get;private set;}="";
    public bool Running=>app is not null;
    public LocalServer(ControlState state,Func<Action,Task> dispatch,string dataPath){this.state=state;this.dispatch=dispatch;this.dataPath=dataPath;}
    public async Task Start(LanAddress lan,int port,bool testing=false)
    {
        if(app is not null)throw new InvalidOperationException("Server läuft bereits.");
        var certificate=LocalCertificate.Load(dataPath,lan.Ip);Fingerprint=certificate.GetCertHashString(HashAlgorithmName.SHA256);
        var builder=WebApplication.CreateSlimBuilder(new WebApplicationOptions{Args=Array.Empty<string>(),ContentRootPath=AppContext.BaseDirectory});builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k=>{k.Limits.MaxRequestBodySize=8192;k.Limits.MaxConcurrentConnections=32;k.Limits.RequestHeadersTimeout=TimeSpan.FromSeconds(10);k.Limits.KeepAliveTimeout=TimeSpan.FromSeconds(30);
            k.Listen(IPAddress.Loopback,port,o=>{o.Protocols=HttpProtocols.Http1;o.UseHttps(certificate);});
            if(!IPAddress.IsLoopback(lan.Ip))k.Listen(lan.Ip,port,o=>{o.Protocols=HttpProtocols.Http1;o.UseHttps(certificate);});});
        var server=builder.Build();server.UseWebSockets(new WebSocketOptions{KeepAliveInterval=TimeSpan.FromSeconds(15)});
        var hosts=new HashSet<string>(StringComparer.OrdinalIgnoreCase){lan.Ip.ToString(),"localhost","127.0.0.1"};
        // public hostnames are allowed only when they were deliberately configured:
        // quick tunnels end in trycloudflare.com, named tunnels are listed in allowed-hosts
        var tunnelHosts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var dir in new[]{AppContext.BaseDirectory,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","Windows","Caches","ClassroomData")})
        {
            try
            {
                var file=Path.Combine(dir,"allowed-hosts");
                if(!File.Exists(file))continue;
                foreach(var line in File.ReadAllLines(file))
                {
                    var value=line.Trim();
                    if(value.Length>0&&!value.StartsWith("#"))tunnelHosts.Add(value);
                }
            }
            catch{}
        }
        bool IsPublicHost(string host)=>host.EndsWith(".trycloudflare.com",StringComparison.OrdinalIgnoreCase)||tunnelHosts.Contains(host);
        server.Use(async(context,next)=>
        {
            context.Response.Headers["Cache-Control"]="no-store";
            context.Response.Headers["X-Content-Type-Options"]="nosniff";
            context.Response.Headers["X-Frame-Options"]="DENY";
            context.Response.Headers["Referrer-Policy"]="no-referrer";
            // style-src permits existing inline progress styles from the copied ESP UI.
            context.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' blob:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
            var requestedHost=context.Request.Host.Host;
            var localHost=hosts.Contains(requestedHost)&&context.Request.Host.Port==port;
            var publicHost=IsPublicHost(requestedHost);
            if(!lan.Allows(context.Connection.RemoteIpAddress)||(!localHost&&!publicHost)){context.Response.StatusCode=403;return;}
            var origin=context.Request.Headers.Origin.ToString();var expected="https://"+context.Request.Host;
            if(origin.Length>0&&!origin.Equals(expected,StringComparison.OrdinalIgnoreCase)){context.Response.StatusCode=403;return;}
            if((context.Request.Path.StartsWithSegments("/api")||context.Request.Path=="/ws")&&
                (context.Request.Path=="/ws"?origin!=expected:context.Request.Headers["X-Panic-Control"]!="1"||context.Request.Method=="POST"&&origin!=expected))
            {context.Response.StatusCode=403;return;}
            await next();
        });
        foreach(var entry in new[]{("/","index.html","text/html; charset=utf-8"),("/app.js","app.js","application/javascript; charset=utf-8"),("/app.css","app.css","text/css; charset=utf-8")})
            server.MapGet(entry.Item1,()=>Results.File(Path.Combine(AppContext.BaseDirectory,"web",entry.Item2),entry.Item3));
        server.MapPost("/api/login",async (HttpContext c)=>
        {
            var ip=c.Connection.RemoteIpAddress!.ToString();
            lock(loginGate)
            {
                var now=Environment.TickCount64;
                foreach(var key in attempts.Where(a=>now-a.Value.Window>60000).Select(a=>a.Key).ToArray())attempts.Remove(key);
                if(attempts.Count>=256||activeLogins>=2||attempts.TryGetValue(ip,out var prior)&&prior.Count>=5)return Results.Json(new{error="Zu viele Versuche. Bitte eine Minute warten."},statusCode:429);
                var item=attempts.GetValueOrDefault(ip,(Count:0,Window:now));attempts[ip]=(item.Count+1,item.Window);activeLogins++;
            }
            try
            {
                var body=await ReadJson(c);var name=ControlState.Text(body,"user",24,false);var password=ControlState.Text(body,"password",64,false);
                var token=await Task.Run(()=>state.Authenticate(name,password));if(token is null)return Results.Json(new{error="Benutzer oder Passwort falsch."},statusCode:401);
                c.Response.Cookies.Append("panic_session",token,new CookieOptions{HttpOnly=true,Secure=true,SameSite=SameSiteMode.Strict,MaxAge=TimeSpan.FromHours(24),Path="/",IsEssential=true});
                return Results.Json(new{ok=true});
            }
            catch(Exception ex) when(ex is JsonException or InvalidOperationException or Microsoft.AspNetCore.Http.BadHttpRequestException){return Results.Json(new{error="Ungültige Anmeldedaten."},statusCode:400);}
            finally{lock(loginGate)activeLogins--;}
        });
        server.MapPost("/api/logout",async (HttpContext c)=>{var token=c.Request.Cookies["panic_session"];await dispatch(()=>state.Logout(token));c.Response.Cookies.Delete("panic_session",new CookieOptions{Secure=true,SameSite=SameSiteMode.Strict,Path="/"});return Results.Json(new{ok=true});});
        server.MapGet("/api/screenshot",(Delegate)(async (HttpContext c)=>await Extra(c,"screenshotView",()=>
        {var raw=c.Request.Query["screen"].ToString();if(raw.Length==0)raw="0";if(!int.TryParse(raw,out var screen))throw new InvalidOperationException("Ungültiger Bildschirm.");return state.Native.Screenshot(screen);} )));
        server.MapGet("/api/screens",(Delegate)(async (HttpContext c)=>await Extra(c,"screenshotView",()=>state.Native.Screens(),false)));
        server.MapGet("/api/preview/screens",(Delegate)(async (HttpContext c)=>await Extra(c,"livePreview",()=>state.Native.Screens(),false)));
        server.MapGet("/api/preview",(Delegate)(async (HttpContext c)=>
        {
            IResult result=Results.Json(new{error="Bitte anmelden."},statusCode:401);
            await dispatch(()=>{lock(state.Gate){var token=c.Request.Cookies["panic_session"];if(state.Session(token) is null)return;
                try{if(!int.TryParse(c.Request.Query["screen"],out var screen))throw new InvalidOperationException("Ungültiger Bildschirm.");var lease=c.Request.Query["lease"].ToString();if(!Guid.TryParseExact(lease,"N",out _))throw new InvalidOperationException("Live-Vorschau zuerst starten.");result=Results.File(state.Preview(token!,screen,lease),"image/jpeg");}
                catch(ControlState.PreviewRateException){result=Results.Json(new{error="Maximal ein Live-Bild pro Sekunde."},statusCode:429);}
                catch(Exception ex) when(ex is InvalidOperationException or ExternalException){result=Results.Json(new{error=ex.Message},statusCode:403);}
            }});return result;
        }));
        server.MapGet("/api/windows",(Delegate)(async (HttpContext c)=>await Extra(c,"windowList",()=>state.Native.Windows())));
        server.MapGet("/api/clipboard",(Delegate)(async (HttpContext c)=>await Extra(c,"clipboardRead",()=>state.Native.ReadClipboard())));
        server.MapGet("/api/stats",(Delegate)(async (HttpContext c)=>await Extra(c,"pcStats",()=>state.Native.Stats())));
        if(testing&&state.Native.DryRun)
            server.MapGet("/api/test/snapshot",()=>Results.Json(state.Snapshot())); // Only in explicit local dry-run test mode.
        server.Map("/ws",async (HttpContext c)=>
        {
            var token=c.Request.Cookies["panic_session"];if(!c.WebSockets.IsWebSocketRequest||state.Session(token) is null||state.Paused){c.Response.StatusCode=401;return;}
            using var socket=await c.WebSockets.AcceptWebSocketAsync();var peer=new Peer(socket,token!);var sender=peer.SendLoop();
            try
            {
                await dispatch(()=>state.Attach(peer));var buffer=new byte[32768];long window=Environment.TickCount64;int messages=0,actions=0;
                while(socket.State==WebSocketState.Open&&!peer.Cancel.IsCancellationRequested)
                {
                    int count=0;WebSocketReceiveResult result;
                    do{result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer,count,buffer.Length-count),peer.Cancel.Token);if(result.MessageType==WebSocketMessageType.Close)return;
                        count+=result.Count;if(result.MessageType!=WebSocketMessageType.Text||count>=buffer.Length){peer.Close();return;}}
                    while(!result.EndOfMessage);
                    using var doc=JsonDocument.Parse(buffer.AsMemory(0,count),new JsonDocumentOptions{MaxDepth=8});if(doc.RootElement.ValueKind!=JsonValueKind.Object){peer.Close();return;}
                    if(Environment.TickCount64-window>=1000){window=Environment.TickCount64;messages=actions=0;}
                    var cmd=ControlState.Text(doc.RootElement,"cmd",24);if(++messages>80||cmd!="mouse"&&cmd!="heartbeat"&&++actions>12){peer.Send(new{toast="Bitte etwas langsamer bedienen.",type="error"});continue;}
                    var element=doc.RootElement.Clone();await dispatch(()=>state.Handle(peer,element));
                }
            }
            catch(Exception ex) when(ex is WebSocketException or OperationCanceledException or ObjectDisposedException or JsonException or InvalidOperationException){peer.Close();}
            finally{peer.Close();await sender;await dispatch(()=>state.Detach(peer));peer.Cancel.Dispose();}
        });
        try{await server.StartAsync();app=server;activeCertificate=certificate;state.Paused=false;}
        catch{await server.DisposeAsync();certificate.Dispose();throw;}
    }
    private async Task<IResult> Extra(HttpContext c,string action,Func<object> get,bool cooldown=true)
    {
        IResult result=Results.Json(new{error="Bitte anmelden."},statusCode:401);
        await dispatch(()=>
        {
            lock(state.Gate)
            {
                var a=state.Session(c.Request.Cookies["panic_session"]);if(a is null)return;
                try{if(state.Paused||!state.Allowed(a,action))throw new InvalidOperationException("Keine Berechtigung für diese Funktion.");if(cooldown&&!state.Begin(a,action)){result=Results.Json(new{error="Cooldown läuft noch."},statusCode:429);return;}var value=get();result=value is byte[] bytes?Results.File(bytes,"image/jpeg"):Results.Json(value);}
                catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception or ExternalException){result=Results.Json(new{error="Windows konnte die Aktion nicht ausführen: "+ex.Message},statusCode:403);}
            }
        });return result;
    }
    private static async Task<JsonElement> ReadJson(HttpContext c)
    {
        if(!c.Request.HasJsonContentType())throw new InvalidOperationException();
        using var doc=await JsonDocument.ParseAsync(c.Request.Body,new JsonDocumentOptions{MaxDepth=8},c.RequestAborted);return doc.RootElement.Clone();
    }
    public async Task Stop()
    {
        var old=app;app=null;
        try{await dispatch(()=>state.Pause());}
        finally{if(old is not null){using var deadline=new CancellationTokenSource(3000);try{await old.StopAsync(deadline.Token);}finally{await old.DisposeAsync();activeCertificate?.Dispose();activeCertificate=null;}}}
    }
}
internal static class LocalCertificate
{
    [StructLayout(LayoutKind.Sequential)] private struct Blob{public int size;public IntPtr data;}
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]private static extern bool CryptProtectData(ref Blob input,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true)]private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
    [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr pointer);
    private static byte[] Protect(byte[] bytes,bool decrypt)
    {
        var b=new Blob{size=bytes.Length,data=Marshal.AllocHGlobal(bytes.Length)};Marshal.Copy(bytes,0,b.data,bytes.Length);Blob output=default;
        try{bool ok=decrypt?CryptUnprotectData(ref b,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptProtectData(ref b,"Panic Control TLS",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());var result=new byte[output.size];Marshal.Copy(output.data,result,0,result.Length);return result;}
        finally{Marshal.Copy(new byte[bytes.Length],0,b.data,bytes.Length);Marshal.FreeHGlobal(b.data);if(output.data!=IntPtr.Zero)LocalFree(output.data);}
    }
    public static X509Certificate2 Load(string dataPath,IPAddress ip)
    {
        var file=Path.Combine(dataPath,"local-certificate.dpapi");
        // Schannel requires a temporary Windows key container. EphemeralKeySet
        // does not work for TLS on Windows. UserKeySet removes it on Dispose.
        if(File.Exists(file)){var raw=Protect(File.ReadAllBytes(file),true);try{return new X509Certificate2(raw,(string?)null,X509KeyStorageFlags.UserKeySet);}finally{CryptographicOperations.ZeroMemory(raw);}}
        using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=Panic Control Local",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature,false));
        var uses=new OidCollection{new Oid("1.3.6.1.5.5.7.3.1")};request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(uses,false));
        var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");san.AddIpAddress(IPAddress.Loopback);if(!IPAddress.IsLoopback(ip))san.AddIpAddress(ip);request.CertificateExtensions.Add(san.Build());
        using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(3));var data=cert.Export(X509ContentType.Pfx);
        try{File.WriteAllBytes(file,Protect(data,false));return new X509Certificate2(data,(string?)null,X509KeyStorageFlags.UserKeySet);}finally{CryptographicOperations.ZeroMemory(data);}
    }
}
