// injector.cpp - einmaliger Aktivierer.
//
// Laedt AudioAgent.dll in einen laufenden explorer.exe. Braucht kein Admin:
// explorer laeuft in derselben Sitzung mit denselben Rechten.
//
// Wird EINMAL gebraucht, damit die DLL sofort im laufenden Explorer haengt.
// Ab der naechsten Anmeldung laedt explorer sie von allein (COM-Hijack).
//
// Build: cl /std:c++17 /W4 /O2 /Fe:injector.exe injector.cpp
//        (keine DLL, eigenes Image - liegt nicht auf dem Zielrechner)

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <tlhelp32.h>
#include <cstdio>
#include <cstring>
#include <cwchar>

static DWORD FindProcess(const wchar_t* name)
{
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE) return 0;
    PROCESSENTRY32W entry{};
    entry.dwSize = sizeof(entry);
    DWORD pid = 0;
    if (Process32FirstW(snap, &entry))
    {
        do
        {
            if (_wcsicmp(entry.szExeFile, name) == 0)
            {
                // nur der Explorer der eigenen Sitzung
                DWORD session = 0;
                if (ProcessIdToSessionId(entry.th32ProcessID, &session) &&
                    session == WTSGetActiveConsoleSessionId())
                {
                    pid = entry.th32ProcessID;
                    break;
                }
            }
        } while (Process32NextW(snap, &entry));
    }
    CloseHandle(snap);
    return pid;
}

int wmain(int argc, wchar_t** argv)
{
    if (argc < 2)
    {
        wprintf(L"injector.exe <pfad\\AudioAgent.dll>\n");
        return 2;
    }

    wchar_t full[MAX_PATH]{};
    if (!GetFullPathNameW(argv[1], MAX_PATH, full, nullptr))
    {
        wprintf(L"pfad ungueltig\n");
        return 2;
    }
    if (GetFileAttributesW(full) == INVALID_FILE_ATTRIBUTES)
    {
        wprintf(L"nicht gefunden: %s\n", full);
        return 2;
    }

    DWORD pid = FindProcess(L"explorer.exe");
    if (!pid)
    {
        wprintf(L"explorer.exe nicht gefunden\n");
        return 3;
    }

    HANDLE proc = OpenProcess(PROCESS_VM_WRITE | PROCESS_VM_OPERATION |
                              PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION,
                              FALSE, pid);
    if (!proc)
    {
        wprintf(L"OpenProcess fehlgeschlagen (%lu)\n", GetLastError());
        return 4;
    }

    SIZE_T bytes = (wcslen(full) + 1) * sizeof(wchar_t);
    void* remote = VirtualAllocEx(proc, nullptr, bytes, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (!remote)
    {
        wprintf(L"VirtualAllocEx fehlgeschlagen (%lu)\n", GetLastError());
        CloseHandle(proc);
        return 5;
    }

    if (!WriteProcessMemory(proc, remote, full, bytes, nullptr))
    {
        wprintf(L"WriteProcessMemory fehlgeschlagen (%lu)\n", GetLastError());
        VirtualFreeEx(proc, remote, 0, MEM_RELEASE);
        CloseHandle(proc);
        return 6;
    }

    HMODULE kernel = GetModuleHandleW(L"kernel32.dll");
    auto loadLibrary = (LPTHREAD_START_ROUTINE)GetProcAddress(kernel, "LoadLibraryW");
    HANDLE thread = CreateRemoteThread(proc, nullptr, 0, loadLibrary, remote, 0, nullptr);
    if (!thread)
    {
        wprintf(L"CreateRemoteThread fehlgeschlagen (%lu)\n", GetLastError());
        VirtualFreeEx(proc, remote, 0, MEM_RELEASE);
        CloseHandle(proc);
        return 7;
    }

    WaitForSingleObject(thread, 10000);
    CloseHandle(thread);
    VirtualFreeEx(proc, remote, 0, MEM_RELEASE);
    CloseHandle(proc);

    wprintf(L"geladen in explorer.exe (pid %lu): %s\n", pid, full);
    return 0;
}
