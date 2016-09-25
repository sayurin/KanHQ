#define _WIN32_WINNT _WIN32_WINNT_WIN2K
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#define _HAS_EXCEPTIONS 0
#define CINTERFACE	/* use C interface mode to access vtable. */
#define INITGUID	/* KB130869 */
#include <Windows.h>
#include <crtdbg.h>						// for _ASSERTE, _CRT_WARN, _RPTFWN
#include <Ks.h>							// for NANOSECONDS, KSCONVERT_PERFORMANCE_TIME
#include <Shlwapi.h>					// for PathAppend
#include <atomic>
#include <type_traits>					// for std::remove_pointer_t
#pragma comment(lib, "Shlwapi.lib")

#define DLLNAME "d3d9.dll"
#include "../callproc.h"

#include <d3d9.h>
static_assert(sizeof IDirect3D9Vtbl == offsetof(IDirect3D9ExVtbl, GetAdapterModeCountEx), "old IDirect3D9Ex does not have IDirect3D9::RegisterSoftwareDevice().");

template<typename Interface>
using vtable_t = std::remove_pointer_t<decltype(Interface::lpVtbl)>;

template<typename Func>
void hook(Func& target, Func& backup, Func hook) {
	if (target == hook)
		return;
	_ASSERTE(!backup || backup == target);
	MEMORY_BASIC_INFORMATION info;
	auto size = VirtualQuery(&target, &info, sizeof MEMORY_BASIC_INFORMATION);
	_ASSERTE(0 < size);
	auto protect = info.Protect & ~0xFF | (info.Protect & 0x0F ? PAGE_READWRITE : PAGE_EXECUTE_READWRITE);
	auto result = VirtualProtect(&target, sizeof(Func), protect, &protect);
	_ASSERTE(result);
	backup = target;
	target = hook;
}
#define HOOK(INTERFACE, PTR, MEMBER) hook<decltype(vtable_t<INTERFACE>::MEMBER)>(PTR->lpVtbl->MEMBER, INTERFACE ## _ ## MEMBER ## _Orig, INTERFACE ## _ ## MEMBER ## _Hook)

template<typename Func>
void unhook(Func& target, Func& backup, Func hook) {
	_ASSERTE(target == hook);
	target = backup;
	backup = nullptr;
}
#define UNHOOK(INTERFACE, PTR, MEMBER) unhook<decltype(vtable_t<INTERFACE>::MEMBER)>(PTR->lpVtbl->MEMBER, INTERFACE ## _ ## MEMBER ## _Orig, INTERFACE ## _ ## MEMBER ## _Hook)

typedef void (STDMETHODCALLTYPE *FramePtr)(ULONGLONG, void*);
std::atomic<FramePtr> Frame = nullptr;
D3DSURFACE_DESC desc;
auto frequency = []() {
	LARGE_INTEGER frequency;
	QueryPerformanceFrequency(&frequency);
	return static_cast<ULONGLONG>(frequency.QuadPart);
}();
unsigned fps;

void check(const TCHAR* method, HRESULT result) {
	if (FAILED(result)) {
		TCHAR buffer[1024];
		wsprintf(buffer, TEXT("%s() failed: %08X\n"), method, result);
		OutputDebugString(buffer);
	}
}

decltype(vtable_t<IDirect3DDevice9>::EndScene) IDirect3DDevice9_EndScene_Orig = nullptr;
HRESULT STDMETHODCALLTYPE IDirect3DDevice9_EndScene_Hook(IDirect3DDevice9* This) {
	static unsigned count = 0;
	auto ticks = []() {
		LARGE_INTEGER value;
		QueryPerformanceCounter(&value);
		return KSCONVERT_PERFORMANCE_TIME(frequency, value);
	}();
	static auto start = [This, &ticks]() {
		IDirect3DSurface9* renderTarget;
		IDirect3DDevice9_GetRenderTarget(This, 0, &renderTarget);
		IDirect3DSurface9_GetDesc(renderTarget, &desc);
		IDirect3DSurface9_Release(renderTarget);

		// quick hack, first 'ticks - start' is 1.
		return ticks++;
	}();
	fps = static_cast<unsigned>(static_cast<ULONGLONG>(++count) * NANOSECONDS / (ticks - start));

	FramePtr frame = Frame;
	if (frame) {
		IDirect3DSurface9* offsecreen;
		check(TEXT("CreateOffscreenPlainSurface"), IDirect3DDevice9_CreateOffscreenPlainSurface(This, desc.Width, desc.Height, desc.Format, D3DPOOL_SYSTEMMEM, &offsecreen, nullptr));
		{
			IDirect3DSurface9* target;
			check(TEXT("GetRenderTarget"), IDirect3DDevice9_GetRenderTarget(This, 0, &target));
#if 0
			if (desc.MultiSampleType != D3DMULTISAMPLE_NONE) {
				IDirect3DSurface9* resolved;
				check(TEXT("CreateRenderTarget"), IDirect3DDevice9_CreateRenderTarget(This, desc.Width, desc.Height, desc.Format, D3DMULTISAMPLE_NONE, 0, false, &resolved, nullptr));
				check(TEXT("StretchRect"), IDirect3DDevice9_StretchRect(This, target, nullptr, resolved, nullptr, D3DTEXF_NONE));
				IDirect3DSurface9_Release(target);
				target = resolved;
			}
#endif
			check(TEXT("GetRenderTargetData"), IDirect3DDevice9_GetRenderTargetData(This, target, offsecreen));
			IDirect3DSurface9_Release(target);
		}
		frame(ticks, offsecreen);
		IDirect3DSurface9_Release(offsecreen);
	}
	return IDirect3DDevice9_EndScene_Orig(This);
}

int IDirect3DDevice9_HookCount = 0;
decltype(vtable_t<IDirect3DDevice9>::Release) IDirect3DDevice9_Release_Orig = nullptr;
ULONG STDMETHODCALLTYPE IDirect3DDevice9_Release_Hook(IDirect3DDevice9* This) {
	auto count = IDirect3DDevice9_Release_Orig(This);
	if (count == 1 && --IDirect3DDevice9_HookCount == 0) {
		UNHOOK(IDirect3DDevice9, This, EndScene);
		UNHOOK(IDirect3DDevice9, This, Release);
		count = IDirect3DDevice9_Release(This);
	}
	return count;
}

decltype(vtable_t<IDirect3D9>::CreateDevice) IDirect3D9_CreateDevice_Orig = nullptr;
HRESULT STDMETHODCALLTYPE IDirect3D9_CreateDevice_Hook(IDirect3D9* This, UINT Adapter, D3DDEVTYPE DeviceType, HWND hFocusWindow, DWORD BehaviorFlags, D3DPRESENT_PARAMETERS* pPresentationParameters, IDirect3DDevice9** ppReturnedDeviceInterface) {
	auto result = IDirect3D9_CreateDevice_Orig(This, Adapter, DeviceType, hFocusWindow, BehaviorFlags, pPresentationParameters, ppReturnedDeviceInterface);
	if (result == S_OK) {
		auto pDevice = *ppReturnedDeviceInterface;
		IDirect3DDevice9_HookCount++;
		IDirect3DDevice9_AddRef(pDevice);
		HOOK(IDirect3DDevice9, pDevice, EndScene);
		HOOK(IDirect3DDevice9, pDevice, Release);
	}
	return result;
}

int IDirect3D9_HookCount = 0;
decltype(vtable_t<IDirect3D9>::Release) IDirect3D9_Release_Orig = nullptr;
ULONG STDMETHODCALLTYPE IDirect3D9_Release_Hook(IDirect3D9* This) {
	auto count = IDirect3D9_Release_Orig(This);
	if (count == 1 && --IDirect3D9_HookCount == 0) {
		UNHOOK(IDirect3D9, This, CreateDevice);
		UNHOOK(IDirect3D9, This, Release);
		count = IDirect3D9_Release(This);
	}
	return count;
}

decltype(vtable_t<IDirect3D9Ex>::CreateDeviceEx) IDirect3D9Ex_CreateDeviceEx_Orig = nullptr;
HRESULT STDMETHODCALLTYPE IDirect3D9Ex_CreateDeviceEx_Hook(IDirect3D9Ex* This, UINT Adapter, D3DDEVTYPE DeviceType, HWND hFocusWindow, DWORD BehaviorFlags, D3DPRESENT_PARAMETERS* pPresentationParameters, D3DDISPLAYMODEEX* pFullscreenDisplayMode, IDirect3DDevice9Ex** ppReturnedDeviceInterface) {
	auto result = IDirect3D9Ex_CreateDeviceEx_Orig(This, Adapter, DeviceType, hFocusWindow, BehaviorFlags, pPresentationParameters, pFullscreenDisplayMode, ppReturnedDeviceInterface);
	if (result == S_OK) {
		auto pDevice = reinterpret_cast<IDirect3DDevice9*>(*ppReturnedDeviceInterface);
		IDirect3DDevice9_HookCount++;
		IDirect3DDevice9_AddRef(pDevice);
		HOOK(IDirect3DDevice9, pDevice, EndScene);
		HOOK(IDirect3DDevice9, pDevice, Release);
	}
	return result;
}

int IDirect3D9Ex_HookCount = 0;
decltype(vtable_t<IDirect3D9Ex>::Release) IDirect3D9Ex_Release_Orig = nullptr;
ULONG STDMETHODCALLTYPE IDirect3D9Ex_Release_Hook(IDirect3D9Ex* This) {
	auto count = IDirect3D9Ex_Release_Orig(This);
	if (count == 1 && --IDirect3D9Ex_HookCount == 0) {
		UNHOOK(IDirect3D9, reinterpret_cast<IDirect3D9*>(This), CreateDevice);
		UNHOOK(IDirect3D9Ex, This, CreateDeviceEx);
		UNHOOK(IDirect3D9Ex, This, Release);
		count = IDirect3D9Ex_Release(This);
	}
	return count;
}

void STDMETHODCALLTYPE GetParameter(UINT* width, UINT* height, GUID* subtype, unsigned* fps) {
	DLLEXPORT;
	*width = desc.Width;
	*height = desc.Height;
	// https://msdn.microsoft.com/en-us/library/aa370819(v=vs.85).aspx#Creating_Subtype_GUIDs_from_FOURCCs_and_D3DFORMAT_Values
	*subtype = { unsigned long(desc.Format), 0x0000, 0x0010, { 0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71 } };
	*fps = ::fps;
	_RPTFWN(_CRT_WARN, TEXT("GetParameter: format=%u, type=%u, usage=%u, pool=%u, sampletype=%u, samplequality=%u, width=%u, height=%u, fps = %u\n"),
		desc.Format, desc.Type, desc.Usage, desc.Pool, desc.MultiSampleType, desc.MultiSampleQuality, desc.Width, desc.Height, ::fps);
}

void STDMETHODCALLTYPE Start(FramePtr frame) {
	DLLEXPORT;
	Frame = frame;
}

void STDMETHODCALLTYPE Stop() {
	DLLEXPORT;
	Frame = nullptr;
}

extern "C"{
	IDirect3D9* WINAPI Direct3DCreate9(UINT SDKVersion) {
		DLLEXPORT;
		auto pD3D = CALLFUNC(Direct3DCreate9, SDKVersion);
		if (pD3D) {
			IDirect3D9_HookCount++;
			IDirect3D9_AddRef(pD3D);
			HOOK(IDirect3D9, pD3D, CreateDevice);
			HOOK(IDirect3D9, pD3D, Release);
		}
		return pD3D;
	}
	HRESULT WINAPI Direct3DCreate9Ex(UINT SDKVersion, IDirect3D9Ex** ppD3D) {
		DLLEXPORT;
		auto result = CALLFUNC(Direct3DCreate9Ex, SDKVersion, ppD3D);
		if (result == S_OK) {
			auto pD3DEx = *ppD3D;
			IDirect3D9Ex_HookCount++;
			IDirect3D9Ex_AddRef(pD3DEx);
			HOOK(IDirect3D9, reinterpret_cast<IDirect3D9*>(pD3DEx), CreateDevice);
			HOOK(IDirect3D9Ex, pD3DEx, CreateDeviceEx);
			HOOK(IDirect3D9Ex, pD3DEx, Release);
		}
		return result;
	}
FUNCTION_NAME(int WINAPI, D3DPERF_BeginEvent, (D3DCOLOR col, LPCWSTR wszName), col, wszName)
FUNCTION_NAME(int WINAPI, D3DPERF_EndEvent, ())
FUNCTION_NAME(DWORD WINAPI, D3DPERF_GetStatus, ())
FUNCTION_NAME(BOOL WINAPI, D3DPERF_QueryRepeatFrame, ())
FUNCTION_NAME(void WINAPI, D3DPERF_SetMarker, (D3DCOLOR col, LPCWSTR wszName), col, wszName)
FUNCTION_NAME(void WINAPI, D3DPERF_SetOptions, (DWORD dwOptions), dwOptions)
FUNCTION_NAME(void WINAPI, D3DPERF_SetRegion, (D3DCOLOR col, LPCWSTR wszName), col, wszName)
FUNCTION_NAME(int __cdecl, DebugSetLevel, ())
FUNCTION_NAME(void __cdecl, DebugSetMute, ())
FUNCTION_NAME(int WINAPI, Direct3D9EnableMaximizedWindowedModeShim, (int a), a)
}
FUNCTION_NAME(struct IDirect3DShaderValidator9* WINAPI, Direct3DShaderValidatorCreate9, ())
FUNCTION_NAME(void WINAPI, PSGPError, (class D3DFE_PROCESSVERTICES* a, enum PSGPERRORID b, unsigned int c), a, b, c)
FUNCTION_NAME(void WINAPI, PSGPSampleTexture, (class D3DFE_PROCESSVERTICES* a, unsigned int b, float(*const c)[4], unsigned int d, float(*const e)[4]), a, b, c, d, e)
FUNCTION_NONAME(16, void WINAPI, Direct3D9ForceHybridEnumeration, (unsigned int a), a)
