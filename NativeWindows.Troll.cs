using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PanicControl;

// Streiche, Hooks und Bildschirm-Spielereien.
//
// Ausgelagert, damit NativeWindows.cs lesbar bleibt. Die Klasse ist partial -
// beide Dateien sind dieselbe Klasse, Felder und Helfer werden geteilt.
//
// Grundsatz fuer alles hier: was sich dauerhaft veraendert (Cursor, Taskleiste,
// Hintergrundbild, Hooks), haengt sich in `restore` ein und wird von
// Restore()/stopAll/selbstzerstoerung wieder abgeraeumt. Nichts bleibt liegen.
internal sealed partial class NativeWindows
{
    // -----------------------------------------------------------------
    // Low-Level-Hooks
    //
    // SetWindowsHookEx mit WH_KEYBOARD_LL braucht einen Thread, der eine
    // Message-Loop pumpt. Der Relay-Dispatcher laeuft auf einem
    // ThreadPool-Thread ohne Loop - dort wuerde der Hook nie ausloesen.
    // Deshalb ein eigener Thread, der Jobs ueber PostThreadMessage bekommt.
    // -----------------------------------------------------------------
    private delegate IntPtr LowLevelProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd; public uint message; public IntPtr wParam, lParam;
        public uint time; public int x, y; public uint lPrivate;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public int x, y; public uint mouseData, flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, LowLevelProc proc, IntPtr mod, uint thread);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint thread, uint msg, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG msg);

    private const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    private const uint WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_HOOKJOB = 0x8000 + 7;

    private static Thread? hookThread;
    private static uint hookThreadId;
    private static readonly ManualResetEventSlim hookDone = new(false);
    private static Action? hookJob;
    private static readonly object hookLock = new();

    private static IntPtr kbHook, msHook;
    private static LowLevelProc? kbProc, msProc;      // am Leben halten, sonst raeumt der GC den Delegaten ab
    private static volatile int keyDelayMs;           // 0 = aus
    private static volatile bool beepKeys;
    private static volatile bool clickJump;

    private static void EnsureHookThread()
    {
        lock (hookLock)
        {
            if (hookThread is not null) return;
            hookThread = new Thread(HookLoop) { IsBackground = true, Name = "ch-hooks" };
            hookThread.SetApartmentState(ApartmentState.STA);
            hookThread.Start();
            hookDone.Wait(3000);
        }
    }
    private static void HookLoop()
    {
        hookThreadId = GetCurrentThreadId();
        hookDone.Set();
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_HOOKJOB)
            {
                var job = Interlocked.Exchange(ref hookJob, null);
                try { job?.Invoke(); } catch { }
                hookDone.Set();
                continue;
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }
    private static void OnHookThread(Action job)
    {
        EnsureHookThread();
        hookDone.Reset();
        Interlocked.Exchange(ref hookJob, job);
        PostThreadMessage(hookThreadId, WM_HOOKJOB, IntPtr.Zero, IntPtr.Zero);
        if (!hookDone.Wait(4000)) throw new TimeoutException("Hook-Thread antwortet nicht.");
    }

    private static IntPtr KeyboardProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0) return CallNextHookEx(kbHook, code, wParam, lParam);
        try
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            // Eigene Injektion durchlassen, sonst frisst sich der Hook selbst.
            if ((info.flags & 0x10) != 0) return CallNextHookEx(kbHook, code, wParam, lParam);
            uint m = (uint)wParam.ToInt32();
            bool down = m == WM_KEYDOWN || m == WM_SYSKEYDOWN;
            bool up = m == WM_KEYUP || m == WM_SYSKEYUP;

            if (beepKeys && down)
            {
                uint vk = info.vkCode;
                Task.Run(() => { try { Console.Beep(520 + (int)(vk % 13) * 70, 45); } catch { } });
            }

            int delay = keyDelayMs;
            if (delay > 0 && (down || up))
            {
                // Taste schlucken und verzoegert selbst senden. Beide Richtungen,
                // sonst haengt eine halb gedrueckte Taste im System.
                if (down)
                {
                    uint vk = info.vkCode;
                    Task.Run(() => { Thread.Sleep(delay); InjectKey(vk, true); Thread.Sleep(35); InjectKey(vk, false); });
                }
                return (IntPtr)1;
            }
        }
        catch { }
        return CallNextHookEx(kbHook, code, wParam, lParam);
    }

    private static IntPtr MouseProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0 || !clickJump) return CallNextHookEx(msHook, code, wParam, lParam);
        try
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if ((info.flags & 0x1) != 0) return CallNextHookEx(msHook, code, wParam, lParam);
            if ((uint)wParam.ToInt32() == WM_LBUTTONDOWN)
            {
                int w = Math.Max(1, GetSystemMetrics(0)), h = Math.Max(1, GetSystemMetrics(1));
                int nx = Math.Clamp(info.x + Random.Shared.Next(-190, 191), 0, w - 1);
                int ny = Math.Clamp(info.y + Random.Shared.Next(-150, 151), 0, h - 1);
                SetCursorPos(nx, ny);
                Task.Run(() => { Thread.Sleep(25); InjectClick(); });
                return (IntPtr)1;   // Original-Klick schlucken, Klick kommt vom neuen Punkt
            }
        }
        catch { }
        return CallNextHookEx(msHook, code, wParam, lParam);
    }

    private static void InjectKey(uint vk, bool down)
    {
        var inputs = new Input[1];
        inputs[0].type = 1;
        inputs[0].value.key.key = (ushort)vk;
        inputs[0].value.key.scan = 0;
        inputs[0].value.key.flags = down ? 0u : 2u;
        inputs[0].value.key.time = 0;
        inputs[0].value.key.extra = UIntPtr.Zero;
        SendInput(1, inputs, Marshal.SizeOf<Input>());
    }
    private static void InjectClick()
    {
        var inputs = new Input[2];
        for (int i = 0; i < 2; i++)
        {
            inputs[i].type = 0;
            inputs[i].value.mouse.dx = 0;
            inputs[i].value.mouse.dy = 0;
            inputs[i].value.mouse.data = 0;
            inputs[i].value.mouse.flags = i == 0 ? 2u : 4u;   // LEFTDOWN / LEFTUP
            inputs[i].value.mouse.time = 0;
            inputs[i].value.mouse.extra = UIntPtr.Zero;
        }
        SendInput(2, inputs, Marshal.SizeOf<Input>());
    }

    private static void InstallKeyboardHook()
    {
        if (kbHook != IntPtr.Zero) return;
        kbProc = KeyboardProc;
        kbHook = SetWindowsHookEx(WH_KEYBOARD_LL, kbProc, GetModuleHandle(null), 0);
        if (kbHook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    private static void RemoveKeyboardHook()
    {
        keyDelayMs = 0; beepKeys = false;
        if (kbHook == IntPtr.Zero) return;
        UnhookWindowsHookEx(kbHook); kbHook = IntPtr.Zero; kbProc = null;
    }
    private static void InstallMouseHook()
    {
        if (msHook != IntPtr.Zero) return;
        msProc = MouseProc;
        msHook = SetWindowsHookEx(WH_MOUSE_LL, msProc, GetModuleHandle(null), 0);
        if (msHook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    private static void RemoveMouseHook()
    {
        clickJump = false;
        if (msHook == IntPtr.Zero) return;
        UnhookWindowsHookEx(msHook); msHook = IntPtr.Zero; msProc = null;
    }

    public void SetKeyDelay(int ms)
    {
        if (ms <= 0) { keyDelayMs = 0; return; }
        OnHookThread(() => { InstallKeyboardHook(); keyDelayMs = ms; });
    }
    public void SetBeepOnKey(bool on)
    {
        if (!on) { beepKeys = false; return; }
        OnHookThread(() => { InstallKeyboardHook(); beepKeys = true; });
    }
    public void SetClickJump(bool on)
    {
        if (!on) { RemoveMouseHook(); return; }
        OnHookThread(() => { InstallMouseHook(); clickJump = true; });
    }

    // -----------------------------------------------------------------
    // UI-Thread
    //
    // WinForms-Fenster brauchen einen STA-Thread MIT Message-Loop. Der
    // Relay-Dispatcher laeuft auf einem Threadpool-Thread ohne Loop: dort
    // erscheint das Fenster zwar, aber Timer (WM_TIMER) und Neuzeichnen
    // (WM_PAINT) laufen nie an - Strobe blinkt nicht, die Uhr steht, der
    // Spiegel bleibt schwarz. Deshalb ein eigener Thread mit
    // Application.Run, auf den jede Fensterarbeit marschiert wird.
    // -----------------------------------------------------------------
    private static Form? uiHost;
    private static Thread? uiThread;
    private static readonly ManualResetEventSlim uiReady = new(false);
    private static readonly object uiLock = new();

    private static void EnsureUi()
    {
        if (uiHost is not null) return;
        lock (uiLock)
        {
            if (uiHost is not null) return;
            uiReady.Reset();
            uiThread = new Thread(() =>
            {
                var host = new Form
                {
                    ShowInTaskbar = false,
                    FormBorderStyle = FormBorderStyle.None,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                    Size = new Size(1, 1),
                    Opacity = 0,
                    ShowIcon = false
                };
                uiHost = host;
                uiReady.Set();
                Application.Run(new ApplicationContext(host));
            })
            { IsBackground = true, Name = "ch-ui" };
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();
            if (!uiReady.Wait(5000)) throw new TimeoutException("UI-Thread antwortet nicht.");
        }
    }

    // Fuehrt `job` auf dem UI-Thread aus und wartet, bis er durch ist. Damit
    // sieht der Aufrufer dieselben Fehler, die ein Fenster beim Erzeugen wirft.
    private static void OnUi(Action job)
    {
        EnsureUi();
        var host = uiHost!;
        if (!host.IsHandleCreated) { job(); return; }
        host.Invoke(job);
    }

    // -----------------------------------------------------------------
    // Cursor
    // -----------------------------------------------------------------
    [DllImport("user32.dll")] private static extern IntPtr CreateCursor(IntPtr inst, int xHot, int yHot, int w, int h, byte[] andPlane, byte[] xorPlane);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetSystemCursor(IntPtr cur, uint id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

    // Alle Systemzeiger, die Windows kennt. Ohne die vollstaendige Liste bleibt
    // beim Verstecken immer irgendein Pfeil stehen (I-Beam, Hand, Warten ...).
    private static readonly uint[] CursorIds =
    {
        32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644, 32645, 32646, 32648, 32649, 32650, 32651
    };

    // ShowCursor(false) zaehlt nur im eigenen Thread - aus einem Hintergrund-
    // Thread bewirkt es gar nichts. Deshalb wird der Systemzeiger durch ein
    // unsichtbares 32x32-Cursor-Bild ersetzt. SPI_SETCURSORS holt das Original.
    public void SetCursorVisible(bool visible)
    {
        if (!visible)
        {
            HideCursors();
            restore.TryAdd("cursorVisible", () =>
            {
                cursorHidden = false;
                SpiPtr(0x0057, 0, IntPtr.Zero, 0x01 | 0x02);
            });
            return;
        }
        cursorHidden = false;
        SpiPtr(0x0057, 0, IntPtr.Zero, 0x01 | 0x02);
        restore.Remove("cursorVisible");
    }

    // Setzt fuer jeden Systemzeiger ein vollstaendig transparentes Bild.
    private void HideCursors()
    {
        bool any = false;
        foreach (uint id in CursorIds)
        {
            const int w = 32, h = 32;
            var and = new byte[w * h / 8];
            var xor = new byte[w * h / 8];
            for (int i = 0; i < and.Length; i++) and[i] = 0xFF;   // AND=1, XOR=0: Bildschirm unveraendert
            IntPtr cur = CreateCursor(IntPtr.Zero, 0, 0, w, h, and, xor);
            if (cur == IntPtr.Zero) continue;
            // SetSystemCursor uebernimmt den Handle und zerstoert ihn selbst.
            if (SetSystemCursor(cur, id)) any = true;
        }
        if (!any) throw new InvalidOperationException("Windows hat den unsichtbaren Cursor nicht angenommen.");
        cursorHidden = true;
    }

    // CursorBaseSize in der Registry wird von Windows nur ausgewertet, wenn die
    // Groesse aus den Einstellungen kommt - ein Schreibzugriff aus einem fremden
    // Prozess bleibt wirkungslos (getestet: Zeiger bleibt 32x32).
    // SetSystemCursor mit einer aus einer .cur-Datei geladenen Kopie wird
    // ebenfalls stillschweigend verworfen (getestet, auch mit Windows-eigenen
    // Dateien). Es bleibt nur CreateIconIndirect: Zeigerbild lesen, skalieren,
    // als DIB-Paar neu bauen, SetSystemCursor.
    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyCursor(IntPtr cur);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr cur, out ICONINFO info);
    [DllImport("gdi32.dll")] private static extern int GetObject(IntPtr obj, int count, ref BITMAP bmp);
    [DllImport("gdi32.dll")] private static extern int GetBitmapBits(IntPtr bmp, int count, byte[] bits);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateBitmap(int w, int h, uint planes, uint depth, byte[]? bits);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFO info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr CreateIconIndirect(ref ICONINFO info);

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool IsIcon; public int HotspotX, HotspotY;
        public IntPtr Mask, Color;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int Type, Width, Height, WidthBytes;
        public short Planes, BitsPixel; public IntPtr Bits;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int Size, Width, Height;
        public short Planes, BitCount;
        public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ClrUsed, ClrImportant;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO { public BITMAPINFOHEADER Header; public int Colors; }

    private static bool cursorHidden;

    public void SetCursorBaseSize(int size)
    {
        if (size < 32) size = 32;
        if (size > 256) size = 256;
        // Erst die Originalzeiger zurueckholen: sonst skaliert ein zweiter
        // Aufruf den schon vergroesserten Zeiger und die Kanten fressen sich auf.
        SpiPtr(0x0057, 0, IntPtr.Zero, 0x01 | 0x02);

        int done = 0;
        foreach (uint id in CursorIds)
        {
            IntPtr source = LoadCursor(IntPtr.Zero, (IntPtr)id);
            if (source == IntPtr.Zero) continue;
            IntPtr scaled = ScaleCursor(source, size);
            if (scaled == IntPtr.Zero) continue;
            if (SetSystemCursor(scaled, id)) done++;
            else DestroyCursor(scaled);   // SetSystemCursor zerstoert nur bei Erfolg
        }
        if (done == 0) throw new InvalidOperationException("Windows hat die Cursorgroesse nicht angenommen.");

        restore.TryAdd("cursorSize", () =>
        {
            SpiPtr(0x0057, 0, IntPtr.Zero, 0x01 | 0x02);
            if (cursorHidden) HideCursors();
        });
        // Versteckter Zeiger ueberlebt das Neuladen nicht - wieder setzen.
        if (cursorHidden) HideCursors();
    }

    private static IntPtr ScaleCursor(IntPtr source, int size)
    {
        if (!GetIconInfo(source, out var info)) return IntPtr.Zero;
        try
        {
            var meta = new BITMAP();
            if (GetObject(info.Mask, Marshal.SizeOf<BITMAP>(), ref meta) == 0) return IntPtr.Zero;
            int ow = meta.Width;
            // Farbzeiger: Maske ist genau so hoch wie der Zeiger. Monochrom:
            // die Maske enthaelt AND und XOR untereinander, also doppelt hoch.
            int oh = info.Color != IntPtr.Zero ? meta.Height : meta.Height / 2;
            if (ow <= 0 || oh <= 0) return IntPtr.Zero;

            using var art = ReadCursorArt(info, ow, oh);
            if (art is null) return IntPtr.Zero;

            using var canvas = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(canvas))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.DrawImage(art, new System.Drawing.Rectangle(0, 0, size, size));
            }

            int hx = Math.Clamp(info.HotspotX * size / ow, 0, size - 1);
            int hy = Math.Clamp(info.HotspotY * size / oh, 0, size - 1);
            return BuildCursor(canvas, hx, hy);
        }
        catch (ArgumentException) { }
        catch (ExternalException) { }
        catch (OutOfMemoryException) { }
        finally
        {
            if (info.Mask != IntPtr.Zero) DeleteObject(info.Mask);
            if (info.Color != IntPtr.Zero) DeleteObject(info.Color);
        }
        return IntPtr.Zero;
    }

    // Baut aus einem Bild einen Zeiger: Farb-DIB mit Alphakanal plus leere
    // Maske. CreateIconIndirect ist der einzige Weg, dessen Ergebnis
    // SetSystemCursor hier wirklich uebernimmt.
    private static IntPtr BuildCursor(System.Drawing.Bitmap art, int hotX, int hotY)
    {
        int w = art.Width, h = art.Height;
        var rect = new System.Drawing.Rectangle(0, 0, w, h);
        var data = art.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        byte[] pixels = new byte[data.Stride * h];
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        art.UnlockBits(data);

        var bi = new BITMAPINFO();
        bi.Header.Size = Marshal.SizeOf<BITMAPINFOHEADER>();
        bi.Header.Width = w;
        bi.Header.Height = -h;          // negativ = von oben nach unten
        bi.Header.Planes = 1;
        bi.Header.BitCount = 32;
        IntPtr color = CreateDIBSection(IntPtr.Zero, ref bi, 0, out var bits, IntPtr.Zero, 0);
        if (color == IntPtr.Zero) return IntPtr.Zero;
        Marshal.Copy(pixels, 0, bits, pixels.Length);

        // Leere Maske: jeder Pixel kommt aus dem Farbbild. Ohne diesen Schritt
        // legt Windows ein schwarzes Rechteck unter den Zeiger.
        int stride = ((w + 15) / 16) * 2;
        IntPtr mask = CreateBitmap(w, h, 1, 1, new byte[stride * h]);
        if (mask == IntPtr.Zero) { DeleteObject(color); return IntPtr.Zero; }

        var info = new ICONINFO { IsIcon = false, HotspotX = hotX, HotspotY = hotY, Mask = mask, Color = color };
        IntPtr cursor = CreateIconIndirect(ref info);
        DeleteObject(color);
        DeleteObject(mask);
        return cursor;
    }

    // Baut aus den Zeiger-Bitmaps ein Bild mit Alphakanal. Farbzeiger bringen
    // ihren Alphakanal selbst mit; monochrome werden aus AND/XOR rekonstruiert.
    private static System.Drawing.Bitmap? ReadCursorArt(ICONINFO info, int ow, int oh)
    {
        if (info.Color != IntPtr.Zero)
        {
            using var raw = System.Drawing.Image.FromHbitmap(info.Color);
            var art = new System.Drawing.Bitmap(ow, oh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(art))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(raw, new System.Drawing.Rectangle(0, 0, ow, oh));
            }
            return art;
        }

        var meta = new BITMAP();
        if (GetObject(info.Mask, Marshal.SizeOf<BITMAP>(), ref meta) == 0) return null;
        int stride = meta.WidthBytes;
        var bits = new byte[stride * meta.Height];
        if (GetBitmapBits(info.Mask, bits.Length, bits) <= 0) return null;

        var mono = new System.Drawing.Bitmap(ow, oh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                // Bit gesetzt = "Bildschirm bleibt" (transparent).
                bool andBit = (bits[y * stride + (x >> 3)] & (0x80 >> (x & 7))) != 0;
                if (andBit) continue;
                bool xorBit = (bits[(oh + y) * stride + (x >> 3)] & (0x80 >> (x & 7))) != 0;
                mono.SetPixel(x, y, xorBit ? System.Drawing.Color.White : System.Drawing.Color.Black);
            }
        return mono;
    }

    // -----------------------------------------------------------------
    // Taskleiste
    // -----------------------------------------------------------------
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);

    public void SetTaskbarVisible(bool visible)
    {
        IntPtr main = FindWindow("Shell_TrayWnd", null);
        if (main != IntPtr.Zero) ShowWindow(main, visible ? 5 : 0);
        IntPtr cur = IntPtr.Zero;
        while ((cur = FindWindowEx(IntPtr.Zero, cur, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            ShowWindow(cur, visible ? 5 : 0);
        if (main == IntPtr.Zero) throw new InvalidOperationException("Taskleiste nicht gefunden.");
    }

    public void SetTaskbarSmallIcons(bool small)
    {
        using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true))
            key?.SetValue("TaskbarSmallIcons", small ? 1 : 0, Microsoft.Win32.RegistryValueKind.DWord);
        RestartExplorerOnce();
    }

    private static void RestartExplorerOnce()
    {
        foreach (var p in Process.GetProcessesByName("explorer")) { try { p.Kill(); } catch { } p.Dispose(); }
        Thread.Sleep(900);
        try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true })?.Dispose(); } catch { }
    }

    // Startmenue kaputt: Taskleiste weg + Win-Taste geschluckt. Beides laeuft
    // ueber die Zeit ab, damit sich der Rechner nicht aussperrt.
    public void BreakStartMenu(int seconds)
    {
        if (seconds < 5) seconds = 30;
        SetTaskbarVisible(false);
        OnHookThread(() => { InstallKeyboardHook(); });
        startMenuUntil = Environment.TickCount64 + seconds * 1000L;
        restore.TryAdd("startmenu", () => { startMenuUntil = 0; SetTaskbarVisible(true); });
        Task.Run(() =>
        {
            while (Environment.TickCount64 < startMenuUntil) Thread.Sleep(200);
            try { SetTaskbarVisible(true); } catch { }
        });
    }
    private static long startMenuUntil;

    // Die Win-Taste wird nicht per Hook geschluckt (das wuerde auch Win+L
    // blockieren), sondern die Taskleiste ist einfach weg - das ist der
    // sichtbare Teil und der, den man merkt.
    public void FixStartMenu()
    {
        startMenuUntil = 0;
        SetTaskbarVisible(true);
        restore.Remove("startmenu");
    }

    // -----------------------------------------------------------------
    // Hintergrundbild
    // -----------------------------------------------------------------
    private const uint SPI_SETDESKWALLPAPER = 0x0014, SPI_GETDESKWALLPAPER = 0x0073;

    private static string WallpaperFile() => Path.Combine(Path.GetTempPath(), "ch-wallpaper.jpg");

    public void CaptureWallpaper()
    {
        if (!string.IsNullOrEmpty(savedWallpaper)) return;
        var buffer = new string('\0', 260);
        IntPtr ptr = Marshal.StringToHGlobalUni(buffer);
        try { SpiPtr(SPI_GETDESKWALLPAPER, 260, ptr, 0); savedWallpaper = Marshal.PtrToStringUni(ptr); }
        finally { Marshal.FreeHGlobal(ptr); }
    }



    public void SetWallpaperFromBase64(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) throw new InvalidOperationException("Kein Bild mitgeschickt.");
        int comma = data.IndexOf(',');
        if (comma >= 0) data = data.Substring(comma + 1);
        byte[] bytes;
        try { bytes = Convert.FromBase64String(data); }
        catch { throw new InvalidOperationException("Bilddaten sind kein gueltiges Base64."); }
        if (bytes.Length < 512) throw new InvalidOperationException("Bild ist zu klein.");

        CaptureWallpaper();
        string file = WallpaperFile();
        File.WriteAllBytes(file, bytes);
        ApplyWallpaperFile(file);
        restore.TryAdd("wallpaper", () =>
        {
            if (!string.IsNullOrEmpty(savedWallpaper) && File.Exists(savedWallpaper)) ApplyWallpaperFile(savedWallpaper!);
            else SpiPtr(SPI_SETDESKWALLPAPER, 0, Marshal.StringToHGlobalUni(""), 0x01 | 0x02);
        });
    }

    // Erzeugt farbige Kacheln und schaltet sie durch. Kein Bild noetig,
    // funktioniert also auch ohne Upload.
    public void WallpaperSpam(int seconds)
    {
        if (seconds < 3) seconds = 12;
        CaptureWallpaper();
        string dir = Path.Combine(Path.GetTempPath(), "ch-wp");
        Directory.CreateDirectory(dir);
        var files = new List<string>();
        var colors = new[] { Color.FromArgb(180, 0, 120), Color.Black, Color.White, Color.Red, Color.Lime, Color.Blue, Color.Yellow, Color.Magenta };
        int w = Math.Max(640, GetSystemMetrics(0)), h = Math.Max(480, GetSystemMetrics(1));
        for (int i = 0; i < colors.Length; i++)
        {
            string f = Path.Combine(dir, "spam" + i + ".bmp");
            if (!File.Exists(f))
            {
                using var bmp = new Bitmap(w, h);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(colors[i]);
                    using var font = new Font("Segoe UI", Math.Max(48, h / 10), FontStyle.Bold);
                    string text = i % 2 == 0 ? "6767" : "LOL";
                    var size = g.MeasureString(text, font);
                    using var brush = new SolidBrush(colors[i].GetBrightness() > 0.5 ? Color.Black : Color.White);
                    g.DrawString(text, font, brush, (w - size.Width) / 2, (h - size.Height) / 2);
                }
                bmp.Save(f, ImageFormat.Bmp);
            }
            files.Add(f);
        }
        restore.TryAdd("wallpaper", () =>
        {
            if (!string.IsNullOrEmpty(savedWallpaper) && File.Exists(savedWallpaper)) ApplyWallpaperFile(savedWallpaper!);
        });
        Task.Run(() =>
        {
            long until = Environment.TickCount64 + seconds * 1000L;
            int i = 0;
            while (Environment.TickCount64 < until)
            {
                try { ApplyWallpaperFile(files[i++ % files.Count]); } catch { }
                Thread.Sleep(320);
            }
            try { if (!string.IsNullOrEmpty(savedWallpaper)) ApplyWallpaperFile(savedWallpaper!); } catch { }
        });
    }

    // -----------------------------------------------------------------
    // Maus
    // -----------------------------------------------------------------
    private static int ToPixels(double norm, int span)
        => (int)Math.Round(Math.Clamp(norm, 0, 1000) / 1000.0 * Math.Max(0, span - 1));

    public void MoveMouseTo(string spec)
    {
        if (!TryParsePoint(spec, out double fx, out double fy)) throw new InvalidOperationException("Position fehlt (x,y von 0 bis 1000).");
        SetCursorPos(ToPixels(fx, GetSystemMetrics(0)), ToPixels(fy, GetSystemMetrics(1)));
    }

    // Ein Fingerzug kommt als Kette von Punkten. Der PC spielt sie mit
    // Zwischenschritten ab, damit die Bewegung fluessig aussieht und nicht
    // springt - der Relay pollt nur alle 800 ms.
    public void PlayMousePath(string spec, int durationMs)
    {
        var points = new List<(double X, double Y)>();
        foreach (var seg in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
            if (TryParsePoint(seg, out double x, out double y)) points.Add((x, y));
        if (points.Count == 0) throw new InvalidOperationException("Keine Punkte im Pfad.");
        if (durationMs < 40) durationMs = 40;
        if (durationMs > 8000) durationMs = 8000;
        int step = Math.Max(6, durationMs / points.Count);
        int w = GetSystemMetrics(0), h = GetSystemMetrics(1);
        var snapshot = points.ToArray();
        Task.Run(() =>
        {
            foreach (var pt in snapshot)
            {
                try { SetCursorPos(ToPixels(pt.X, w), ToPixels(pt.Y, h)); } catch { }
                Thread.Sleep(step);
            }
        });
    }

    private static bool TryParsePoint(string spec, out double x, out double y)
    {
        x = y = 0;
        var parts = spec.Split(',');
        if (parts.Length != 2) return false;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, ci, out x)
            && double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, ci, out y);
    }

    // -----------------------------------------------------------------
    // Fenster
    // -----------------------------------------------------------------
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowText(IntPtr hWnd, string text);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    public void RenameForegroundWindow(string title)
    {
        IntPtr h = GetForegroundWindow();
        if (h == IntPtr.Zero) throw new InvalidOperationException("Kein Fenster im Vordergrund.");
        if (!SetWindowText(h, title ?? "")) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void JiggleForegroundWindow(int seconds)
    {
        if (seconds < 1) seconds = 5;
        IntPtr h = GetForegroundWindow();
        if (h == IntPtr.Zero) throw new InvalidOperationException("Kein Fenster im Vordergrund.");
        if (!GetWindowRect(h, out RECT r)) throw new Win32Exception(Marshal.GetLastWin32Error());
        int x0 = r.Left, y0 = r.Top;
        Task.Run(() =>
        {
            long until = Environment.TickCount64 + seconds * 1000L;
            double a = 0;
            while (Environment.TickCount64 < until)
            {
                int dx = (int)(Math.Cos(a) * 18);
                int dy = (int)(Math.Sin(a * 1.7) * 14);
                try { SetWindowPos(h, IntPtr.Zero, x0 + dx, y0 + dy, 0, 0, 0x0001 | 0x0004); } catch { }
                a += 0.5;
                Thread.Sleep(35);
            }
            try { SetWindowPos(h, IntPtr.Zero, x0, y0, 0, 0, 0x0001 | 0x0004); } catch { }
        });
    }

    // Alle sichtbaren Fenster treppenfoermig uebereinander. Wie Win+Kaskade,
    // nur ohne Menue.
    public int CascadeWindows()
    {
        var handles = new List<IntPtr>();
        IntPtr shell = GetShellWindow();
        using var own = Process.GetCurrentProcess();
        int ownSession = own.SessionId;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h) || h == shell) return true;
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == 0 || pid == (uint)Environment.ProcessId) return true;
            try { using var p = Process.GetProcessById((int)pid); if (p.SessionId != ownSession) return true; } catch { return true; }
            if (IsIconic(h)) return true;
            handles.Add(h);
            return handles.Count < 24;
        }, IntPtr.Zero);
        if (handles.Count == 0) throw new InvalidOperationException("Keine Fenster zum Stapeln gefunden.");
        int w = GetSystemMetrics(0), hgt = GetSystemMetrics(1);
        int cw = Math.Max(320, w * 3 / 5), ch = Math.Max(240, hgt * 3 / 5);
        for (int i = 0; i < handles.Count; i++)
        {
            int x = 20 + i * 34, y = 20 + i * 34;
            if (x + cw > w) x = Math.Max(0, w - cw);
            if (y + ch > hgt) y = Math.Max(0, hgt - ch);
            try { SetWindowPos(handles[i], IntPtr.Zero, x, y, cw, ch, 0x0004); } catch { }
        }
        return handles.Count;
    }

    // -----------------------------------------------------------------
    // Uhr
    // -----------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME { public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetSystemTime(ref SYSTEMTIME time);

    // SetSystemTime verlangt SeSystemtimePrivilege, also Adminrechte. Auf einem
    // normalen Konto geht es nicht - dann zeigt eine grosse Uhr die Wunschzeit.
    public string SetClock(string spec)
    {
        if (!TryParseClock(spec, out int hh, out int mm)) throw new InvalidOperationException("Zeit als HH:MM angeben.");
        var now = DateTime.Now;
        var target = new DateTime(now.Year, now.Month, now.Day, hh, mm, 0, DateTimeKind.Local);
        var st = new SYSTEMTIME
        {
            Year = (ushort)target.Year, Month = (ushort)target.Month, Day = (ushort)target.Day,
            Hour = (ushort)target.Hour, Minute = (ushort)target.Minute, Second = 0
        };
        if (SetSystemTime(ref st)) return "system";
        // Kein Recht: grosse Uhr mit der Wunschzeit als Vollbild.
        ShowClockOverlay(target.ToString("HH:mm"));
        return "overlay";
    }
    private static bool TryParseClock(string spec, out int hh, out int mm)
    {
        hh = mm = 0;
        var parts = (spec ?? "").Split(':');
        return parts.Length == 2
            && int.TryParse(parts[0], out hh) && int.TryParse(parts[1], out mm)
            && hh >= 0 && hh < 24 && mm >= 0 && mm < 60;
    }

    private void ShowClockOverlay(string text)
    {
        CloseOverlay("desktopClock");
        if (DryRun) { simulatedOverlays.Add("desktopClock"); return; }
        OnUi(() =>
        {
            var form = new RemoteOverlay("Uhr", text, Color.FromArgb(12, 17, 26), 0, null, null, lockdown: false);
            lock (overlayLock) overlays["desktopClock"] = form;
            form.FormClosed += (_, _) => { lock (overlayLock) { if (overlays.GetValueOrDefault("desktopClock") == form) overlays.Remove("desktopClock"); } };
            form.Show();
        });
    }

    // -----------------------------------------------------------------
    // Sprachausgabe
    // -----------------------------------------------------------------
    private static Process? speechProc;

    public void StartSpeechLoop(string text)
    {
        StopSpeech();
        string sentence = string.IsNullOrWhiteSpace(text) ? "System update in progress. Please wait." : text!.Replace("'", "''");
        string ps = "Add-Type -AssemblyName System.Speech; " +
                    "$s=New-Object System.Speech.Synthesis.SpeechSynthesizer; $s.Rate=0; " +
                    "while($true){ $s.Speak('" + sentence + "'); Start-Sleep -Seconds 2 }";
        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"" + ps.Replace("\"", "\\\"") + "\""
        };
        speechProc = Process.Start(info);
        restore.TryAdd("speech", StopSpeech);
    }

    public void StopSpeech()
    {
        try { if (speechProc is { HasExited: false }) speechProc.Kill(true); } catch { }
        try { speechProc?.Dispose(); } catch { }
        speechProc = null;
        foreach (var p in Process.GetProcessesByName("Narrator")) { try { p.Kill(); } catch { } p.Dispose(); }
        restore.Remove("speech");
    }

    // -----------------------------------------------------------------
    // Effekte: Strobe und Spiegel
    // -----------------------------------------------------------------
    [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

    private StrobeOverlay? strobe;
    private MirrorOverlay? mirror;

    public void StartStrobe(int seconds)
    {
        StopStrobe();
        if (DryRun) { Record("strobe"); return; }
        OnUi(() =>
        {
            var f = new StrobeOverlay();
            strobe = f;
            f.Show();
        });
        if (seconds > 0)
        {
            int ms = seconds * 1000;
            Task.Run(() => { Thread.Sleep(ms); try { StopStrobe(); } catch { } });
        }
        restore.TryAdd("strobe", StopStrobe);
    }
    public void StopStrobe()
    {
        var f = strobe;
        strobe = null;
        if (f is not null)
        {
            try { OnUi(() => { f.Close(); f.Dispose(); }); }
            catch { try { f.Close(); f.Dispose(); } catch { } }
        }
        restore.Remove("strobe");
    }

    public void StartMirror(bool horizontal, bool vertical)
    {
        StopMirror();
        if (DryRun) { Record("mirror"); return; }
        OnUi(() =>
        {
            var f = new MirrorOverlay(horizontal, vertical);
            mirror = f;
            f.Show();
        });
        restore.TryAdd("mirror", StopMirror);
    }
    public void StopMirror()
    {
        var f = mirror;
        mirror = null;
        if (f is not null)
        {
            try { OnUi(() => { f.Close(); f.Dispose(); }); }
            catch { try { f.Close(); f.Dispose(); } catch { } }
        }
        restore.Remove("mirror");
    }

    // Wird von stopAll und von der Selbstzerstoerung gerufen: alles, was als
    // Fenster oder Hook laeuft, zuerst weg.
    // Wird einmal beim Start aufgerufen. Ein Absturz oder ein harter Ausstieg
    // kann Eingabesperre, versteckten Zeiger oder eine ausgeblendete Taskleiste
    // hinterlassen - das haengt sonst bis zum Neustart und der Rechner wirkt
    // beim Anmelden wie eingefroren. Hier wird alles zurueckgesetzt.
    public void ResetSystemState()
    {
        if(DryRun)return;
        try{BlockInput(false);}catch{}
        try{SpiPtr(0x0057,0,IntPtr.Zero,0x01|0x02);}catch{}   // Systemzeiger aus der Registry
        cursorHidden=false;
        try{startMenuUntil=0;SetTaskbarVisible(true);}catch{}
        try{StopSpeech();}catch{}
        try{CancelExplorerBurst();explorerRemaining=0;}catch{}
        try{CloseOverlays();}catch{}
        try{RemoveKeyboardHook();RemoveMouseHook();}catch{}
        try{StopStrobe();StopMirror();}catch{}
    }
    public void CloseEffects()
    {
        StopStrobe();
        StopMirror();
        StopSpeech();
        RemoveKeyboardHook();
        RemoveMouseHook();
        try { KeepAwake(false); } catch { }
        try { startMenuUntil = 0; SetTaskbarVisible(true); } catch { }
    }
}

// Blinkt weiss/schwarz. Deckt den ganzen virtuellen Bildschirm ab und liegt
// ueber allem; die Taskleiste kommt nicht durch.
internal sealed class StrobeOverlay : Form
{
    private readonly System.Windows.Forms.Timer tick;
    private bool white;

    public StrobeOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        tick = new System.Windows.Forms.Timer { Interval = 70 };
        tick.Tick += (_, _) => { white = !white; BackColor = white ? Color.White : Color.Black; };
        tick.Start();
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        tick.Stop();
        base.OnFormClosed(e);
    }
}

// Spiegel: holt den Bildschirm alle 45 ms und zeichnet ihn gespiegelt.
// WDA_EXCLUDEFROMCAPTURE haelt das Fenster selbst aus dem Bild, sonst
// spiegelt es sich endlos in sich selbst.
internal sealed class MirrorOverlay : Form
{
    private readonly System.Windows.Forms.Timer tick;
    private readonly bool flipH, flipV;

    [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

    public MirrorOverlay(bool horizontal, bool vertical)
    {
        flipH = horizontal;
        flipV = vertical;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        tick = new System.Windows.Forms.Timer { Interval = 45 };
        tick.Tick += (_, _) => { if (!IsDisposed) Invalidate(); };
        tick.Start();
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try { SetWindowDisplayAffinity(Handle, 0x11); } catch { }   // WDA_EXCLUDEFROMCAPTURE
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var b = Bounds;
        if (b.Width <= 0 || b.Height <= 0) return;
        using var bmp = new Bitmap(b.Width, b.Height);
        try
        {
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(b.Location, Point.Empty, b.Size);
        }
        catch { return; }
        var state = e.Graphics.Save();
        if (flipH) { e.Graphics.TranslateTransform(b.Width, 0); e.Graphics.ScaleTransform(-1, 1); }
        if (flipV) { e.Graphics.TranslateTransform(0, b.Height); e.Graphics.ScaleTransform(1, -1); }
        e.Graphics.DrawImage(bmp, 0, 0);
        e.Graphics.Restore(state);
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        tick.Stop();
        base.OnFormClosed(e);
    }
}
