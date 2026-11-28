using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PanicControl;

internal static class SelfTests
{
    public static void Run(string folder)
    {
        Directory.CreateDirectory(folder);
        void Check(bool value,string msg){if(!value)throw new Exception(msg);}
        var data=Path.Combine(folder,"isolated-"+Guid.NewGuid().ToString("N"));var state=new ControlState(data,true);
        state.SetupAdmin("testadmin","Strong-Test-Password");
        var token=state.Authenticate("testadmin","Strong-Test-Password");Check(token is not null,"Admin login");
        Check(state.Authenticate("testadmin","wrong")==null,"Bad password rejected");
        Check(!File.ReadAllText(Path.Combine(data,"accounts.json")).Contains("Strong-Test-Password"),"No cleartext password on disk");
        var admin=state.User("testadmin")!;Check(state.Actions.Length>=124,"Original + extra permissions");Check(state.Actions.Select(a=>a.Id).Distinct().Count()==state.Actions.Length,"Permission ids unique");Check(state.PermBytes*8>=state.Actions.Length,"Permission bitmap wide enough");
        Check(state.Begin(admin,"capsToggle"),"First button allowed");Check(!state.Begin(admin,"capsToggle"),"Second same button locked");Check(state.Begin(admin,"mediaMute"),"Other button independent");Check(state.Begin(admin,"filterOff"),"Recovery exempt");Check(state.Begin(admin,"filterOff"),"Recovery remains available");
        var peer=new Peer(WebSocket.CreateFromStream(new MemoryStream(),true,null,TimeSpan.Zero),token!);state.Attach(peer);
        void Command(object o){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(o));state.Handle(peer,doc.RootElement);}
        Command(new{cmd="admin",op="add",user="guest",pw="Guest-Password",perms=state.PermissionsFor("capsToggle","jitter","mouseMove")});
        var guest=state.User("guest")!;Check(state.Begin(guest,"capsToggle"),"Per-account isolation");
        var guestToken=state.Authenticate("guest","Guest-Password")!;var g=new Peer(WebSocket.CreateFromStream(new MemoryStream(),true,null,TimeSpan.Zero),guestToken);state.Attach(g);
        void Guest(object o){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(o));state.Handle(g,doc.RootElement);}
        Guest(new{cmd="action",action="shutdown"});Check(!state.Native.Simulated.Contains("shutdown"),"Unauthorized action blocked");
        Guest(new{cmd="toggle",name="jitter",value=true});Check(JsonSerializer.Serialize(state.Snapshot()).Contains("jitter"),"Effect enabled");
        Command(new{cmd="admin",op="setPerms",user="guest",perms=state.PermissionsFor("capsToggle")});
        Check(!JsonSerializer.Serialize(state.Snapshot()).Contains("jitter"),"Revocation stops owned effect");
        Guest(new{cmd="mouse",phase="start"});Check(!g.MouseGesture,"Revoked touchpad permission checked dynamically");
        Command(new{cmd="admin",op="setUserCooldown",user="guest",enabled=false});Check(state.Begin(guest,"capsToggle")&&state.Begin(guest,"capsToggle"),"Cooldown opt-out");
        Command(new{cmd="admin",op="setPerms",user="testadmin",perms=state.PermissionsFor("capsToggle")});Check(state.Allowed(admin,"admin"),"Last admin protected");
        Command(new{cmd="admin",op="setPass",user="guest",pw="Another-Password"});Check(state.Session(guestToken)==null,"Password change invalidates all sessions");
        var restored=new ControlState(data,true);Check(restored.Authenticate("guest","Another-Password") is not null,"Accounts persist");Check(!restored.User("guest")!.CooldownEnabled,"Policy persists");
        Command(new{cmd="preview",value=true});Check(state.LivePreviews==1,"Live preview starts explicitly");
        Check(state.Preview(token!,1).Length>0,"Live preview returns selected monitor");
        try{state.Preview(token!,1);throw new Exception("Preview rate not limited");}catch(ControlState.PreviewRateException){}
        Command(new{cmd="preview",value=false});Check(state.LivePreviews==0,"Preview stop ignores running cooldown");
        try{state.Preview(token!,1);throw new Exception("Stopped preview still exposed");}catch(InvalidOperationException){}
        Command(new{cmd="admin",op="setUserCooldown",user="testadmin",enabled=false});
        Command(new{cmd="preview",value=true});peer.PreviewUntil=Environment.TickCount64-1;state.Tick();Check(state.LivePreviews==0,"Abandoned preview lease expires");
        Command(new{cmd="preview",value=true});
        Command(new{cmd="admin",op="setPerms",user="testadmin",perms=state.PermissionsFor(state.Actions.Where(a=>a.Id!="livePreview").Select(a=>a.Id).ToArray())});Check(state.LivePreviews==0,"Live permission revocation stops lease immediately");
        try{state.Preview(token!,0,peer.Id);throw new Exception("Revoked preview exposed");}catch(InvalidOperationException){}
        Command(new{cmd="admin",op="setPerms",user="testadmin",perms=state.PermissionsFor(state.Actions.Select(a=>a.Id).ToArray())});
        Command(new{cmd="action",action="showMessage",text="Test"});Check(state.Native.ActiveOverlays.Contains("showMessage"),"Message overlay routed");
        Command(new{cmd="action",action="countdown",seconds=0});Check(!state.Native.ActiveOverlays.Contains("countdown"),"Invalid countdown rejected");
        Command(new{cmd="action",action="countdown",seconds=601});Check(!state.Native.ActiveOverlays.Contains("countdown"),"Countdown maximum checked");
        long countdownNow=10000;var countdownEnd=countdownNow+2000;
        using(var countdown=new RemoteOverlay("Countdown",NativeWindows.CountdownText(countdownEnd,countdownNow),Color.Navy,7,()=>NativeWindows.CountdownText(countdownEnd,countdownNow),()=>countdownNow))
        {
            using var preview=countdown.RenderPreview();Check(countdown.DisplayText=="00:02","Countdown starts with requested duration");
            countdownNow=11999;Check(countdown.RefreshDisplay()&&countdown.DisplayText=="00:01","Countdown never finishes early");
            countdownNow=12000;Check(countdown.RefreshDisplay()&&countdown.DisplayText=="Zeit abgelaufen!","Countdown displays completion");
            countdownNow=16999;Check(countdown.RefreshDisplay(),"Completion stays visible for five seconds");
            countdownNow=17000;Check(!countdown.RefreshDisplay()&&countdown.IsDisposed,"Countdown actually closes its window after completion");
        }
        Check(NativeWindows.CountdownText(600000,0)=="10:00","Maximum duration formats correctly");
        var capacity=new ControlState(Path.Combine(folder,"capacity-"+Guid.NewGuid().ToString("N")),true);capacity.SetupAdmin("capacity","Capacity-Test-Password");capacity.User("capacity")!.CooldownEnabled=false;
        var capacityToken=capacity.Authenticate("capacity","Capacity-Test-Password")!;var connections=new List<Peer>();
        for(int i=0;i<16;i++){var client=new Peer(WebSocket.CreateFromStream(new MemoryStream(),true,null,TimeSpan.Zero),capacityToken);capacity.Attach(client);connections.Add(client);}
        Check(capacity.Connections==16,"Sixteen connections accepted");
        var overflow=new Peer(WebSocket.CreateFromStream(new MemoryStream(),true,null,TimeSpan.Zero),capacityToken);
        try{capacity.Attach(overflow);throw new Exception("Seventeenth connection accepted");}catch(InvalidOperationException){}finally{overflow.Close();}
        foreach(var client in connections.Take(5)){using var previewCommand=JsonDocument.Parse("{\"cmd\":\"preview\",\"value\":true}");capacity.Handle(client,previewCommand.RootElement);}
        Check(capacity.LivePreviews==4&&connections[4].PreviewUntil==0,"Four live previews accepted; fifth rejected");capacity.Pause();Check(capacity.Connections==0&&capacity.LivePreviews==0,"Capacity cleanup");
        Command(new{cmd="action",action="screenColor",color="arbitrary"});Check(!state.Native.ActiveOverlays.Contains("screenColor"),"Invalid color rejected");
        Command(new{cmd="action",action="closeOverlays"});Check(state.Native.ActiveOverlays.Count==0,"Overlay recovery closes all");
        Command(new{cmd="action",action="screenBlank"});Check(state.Native.ActiveOverlays.Contains("screenBlank"),"Blank overlay routed");
        Command(new{cmd="admin",op="setPerms",user="testadmin",perms=state.PermissionsFor(state.Actions.Where(a=>a.Id!="screenBlank").Select(a=>a.Id).ToArray())});Check(!state.Native.ActiveOverlays.Contains("screenBlank"),"Rights revocation closes owned overlay");
        using(var full=new RemoteOverlay("Test","Fullscreen",Color.Navy))
        {Check(full.TopMost&&full.FormBorderStyle==FormBorderStyle.None&&full.Bounds==Screen.PrimaryScreen!.Bounds,"Shared overlays are topmost borderless full-screen");using var picture=full.RenderPreview();picture.Save(Path.Combine(folder,"fullscreen-layout.png"));}
        var network=new LanAddress(IPAddress.Parse("192.168.1.20"),24,"test");Check(network.Allows(IPAddress.Parse("192.168.1.99")),"Same subnet allowed");Check(network.Allows(IPAddress.Loopback),"Loopback allowed");Check(!network.Allows(IPAddress.Parse("192.168.2.9")),"Other subnet blocked");Check(!network.Allows(IPAddress.Parse("8.8.8.8")),"Public IP blocked");
        Check(Marshal.SizeOf<NativeWindows.Input>()==(Environment.Is64BitProcess?40:28),"Correct native INPUT layout");
        NativeWindows.ProbeSettings();
        var input=new NativeWindows(true);input.SetDance(true);
        for(long tick=0;tick<500;tick+=34)input.DanceTick(tick);
        Check(input.Simulated.Count(x=>x is "key:25:down" or "key:27:down")>=4,"Dance repeats arrows");
        Check(input.Simulated.Count(x=>x=="key:5B:down")==1,"Win stays held between arrow pulses");
        input.Hotkey(NativeWindows.Ctrl,0x53);input.DanceTick(600);input.SetDance(false);
        foreach(var key in new[]{"5B","25","27"})Check(input.Simulated.Count(x=>x==$"key:{key}:down")==input.Simulated.Count(x=>x==$"key:{key}:up"),"Dance releases all held keys after other shortcuts and stop");
        var count=input.Simulated.Count;input.DanceTick(900);Check(input.Simulated.Count==count,"Dance stops immediately");
        var explorer=new NativeWindows(true);explorer.StartExplorerBurst();explorer.ExplorerTick(0);explorer.ExplorerTick(349);
        Check(explorer.Simulated.Count(x=>x=="key:45:down")==1,"Explorer burst is paced");
        for(long tick=350;tick<=3150;tick+=350)explorer.ExplorerTick(tick);
        Check(!explorer.ExplorerActive&&explorer.Simulated.Count(x=>x=="key:45:down")==10,"Explorer sends exactly ten Win+E shortcuts");
        explorer.StartExplorerBurst();explorer.CancelExplorerBurst();count=explorer.Simulated.Count;explorer.ExplorerTick(5000);Check(explorer.Simulated.Count==count,"Explorer burst cancellation");
        Command(new{cmd="toggle",name="windowDance",value=true});state.Tick();Command(new{cmd="action",action="winELoop"});state.Tick();
        state.Detach(peer);Check(!state.Native.ExplorerActive,"Last disconnect cancels Explorer burst");
        Check(state.Native.Simulated.Count(x=>x=="key:5B:down")==state.Native.Simulated.Count(x=>x=="key:5B:up"),"Disconnect releases dance modifier");
        Check(input.Screens() is not null&&input.Screenshot(1).Length>0,"Secondary screenshot supported");
        try{input.Screenshot(-1);throw new Exception("Invalid screen accepted");}catch(InvalidOperationException){}
        try{input.WindowAction("windowFocus","forged-handle");throw new Exception("Forged window accepted");}catch(InvalidOperationException){}
        // Upgrade an actual persisted older account file. Admins gain appended bits;
        // guest grants and every existing bit retain their old meaning.
        restored.Settings.PermissionCount=90;restored.User("testadmin")!.Permissions=restored.PermissionsFor(restored.Actions.Take(90).Select(x=>x.Id).ToArray());restored.Save();
        var upgraded=new ControlState(data,true);Check(upgraded.Allowed(upgraded.User("testadmin")!,"clipboardRead"),"Admin receives appended permissions on upgrade");
        Check(upgraded.Allowed(upgraded.User("testadmin")!,"livePreview")&&upgraded.Allowed(upgraded.User("testadmin")!,"openSettings"),"Newest appended permissions migrate");
        Check(!upgraded.Allowed(upgraded.User("guest")!,"clipboardRead"),"Upgrade never grants guest extra permissions");
        upgraded.Settings.PermissionCount=100;upgraded.User("testadmin")!.Permissions=upgraded.PermissionsFor(upgraded.Actions.Take(100).Where(a=>a.Id!="clipboardRead").Select(a=>a.Id).ToArray());upgraded.Save();
        var newest=new ControlState(data,true);Check(!newest.Allowed(newest.User("testadmin")!,"clipboardRead")&&newest.Allowed(newest.User("testadmin")!,"livePreview"),"100-right upgrade preserves deliberately removed existing grants");
        // Jede Aktion, die die Fernbedienung anbietet, muss der PC kennen.
        // Die Liste ist absichtlich hier und nicht im Relay: sie ist der
        // Gegenpart zum Katalog und faellt auf, wenn eine Seite driftet.
        string[] catalog = {
            "altF4","altTabSpam","beepOff","beepOnKey","blackout","browserBack",
            "browserFullscreen","browserHardRefresh","browserNewTab","browserRefresh","browserSearch","browserZoomIn",
            "browserZoomReset","cancelShutdown","capsSpam","capsToggle","clickJump","clickJumpOff",
            "clipboardClear","clockFix","clockWrong","closeOverlays","closeTab","corruptor",
            "countdown","cursorBig","cursorGiant","cursorNormal","desktop","desktopClock",
            "desktopClose","desktopNext","desktopPrev","doubleSpace","downloads","emojiPanel",
            "explorerLoop","fakeBsod","fakeRansom","fakeUpdate","filterKeys","filterOff",
            "freezeInput","gameBar","gfxReset","hideCursor","highContrast","history",
            "iconsHide","iconsShow","iSeeYou","keepAwake","keepAwakeOff","keyDelayOff",
            "keyDelayOn","killExplorer","launchApp","layoutSwap","lock","magnifier",
            "magnifierClose","magnifierInvert","mediaMute","mediaNext","mediaPlayPause","mediaPrev",
            "mediaStop","minimizeAll","mirrorH","mirrorHV","mirrorOff","mirrorV",
            "monitorOff","monitorOn","mouseDouble","mouseKeys","mouseKeysOff","mouseLeft",
            "mouseMiddle","mouseRight","mouseShake","mouseSpeedMin","mouseSpeedNormal","mouseWarpCorner",
            "narrator","numLockParty","openCalculator","openCmd","openControlPanel","openDeviceManager",
            "openDownloadsFolder","openEventViewer","openExplorer","openNotepad","openPowerShell","openRegedit",
            "openSettings","openTempFolder","osk","overwrite","pasteClipboard","peekDesktop",
            "ping","printDialog","printScreen","randomKey","releaseKeys","reopenTab",
            "restartExplorer","restoreAll","rickroll","rotate","save","screenBlank",
            "screenColor","screensaverNow","scrollDown","scrollUp","secondScreenBack","selectAllSpace",
            "selfDestruct","setAbout","setApps","setBluetooth","setDateTime","setDisplay",
            "setNetwork","setNotifications","setPower","setPrinters","setPrivacy","setSound",
            "setTaskbar","setUpdate","showCursor","showMessage","shutdown","sleep",
            "slowType","snippingTool","speechLoop","speechStop","startMenuBreak","startMenuFix",
            "stickyKeys","stickyOff","stopAll","strobe","strobeOff","swapButtons",
            "swapButtonsOff","tabNext","tabPrev","tabSpam","taskbarAutoHide","taskbarHide",
            "taskbarRestore","taskbarShow","taskbarTiny","taskbarTinyOff","taskMgr","taskView",
            "titleRename","typeSpam","typeText","undo","unfreezeInput","update",
            "virtDesktop","volDown","volumeMax","volumeParty","volUp","wallpaperForce",
            "wallpaperRestore","wallpaperSpam","wallpaperTroll","windowCascade","windowJiggle","windowMonitorNext",
            "windowSnapLeft","windowSnapRight","winELoop","winHold","winPSecond","winRelease",
            "xboxRecord","ytRestart","zoomOut"
        };
        Check(catalog.Distinct().Count()==catalog.Length,"Remote catalog has no duplicates");
        var audit=new NativeWindows(true);
        var unknown=new List<string>();
        foreach(string action in catalog)
        {
            try{audit.Execute(action,"12:00",null,5);}
            catch(InvalidOperationException ex) when(ex.Message=="Unbekannte Aktion."){unknown.Add(action);}
        }
        Check(unknown.Count==0,"Remote catalog fully implemented; missing: "+string.Join(", ",unknown));
        // Der Trockenlauf darf nichts am Rechner hinterlassen haben.
        audit.Restore();
        Check(audit.ActiveOverlays.Count==0&&!audit.HasPresentations,"Dry run leaves no window or effect behind");
        state.Pause();Check(state.Session(token)==null&&state.Connections==0,"Local stop disconnects and invalidates sessions");
        File.WriteAllText(Path.Combine(folder,"self-tests.txt"),"PASS: authentication, hashes, persistence, permissions, revocation, cooldowns, settings probe, arrow repeat and key release, paced Explorer burst, disconnect cleanup, selected-monitor screenshots, forged windows rejected, permission migration, fullscreen configuration and offscreen render, overlay cleanup and revocation, countdown timing/completion/window-close and bounds, color validation, 16 connection capacity and overflow, 4 preview capacity and overflow, live-preview start/stop, expiry/revocation and frame rate, every remote action implemented, dry run leaves nothing behind, local stop. No real input sent; layout rendered offscreen without focus.\n");
    }
}
