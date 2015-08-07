#define _WIN32_WINNT _WIN32_WINNT_WIN2K
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#define CINTERFACE	/* use C interface mode to access vtable. */
#define INITGUID	/* KB130869 */
#include <Windows.h>
#include <crtdbg.h>						// for _ASSERTE
#include <Ks.h>							// for NANOSECONDS, KSCONVERT_PERFORMANCE_TIME
#include <Shlwapi.h>					// for PathAppend
#include <type_traits>					// for std::remove_pointer_t
#pragma comment(lib, "Shlwapi.lib")

#include <d3d9.h>
static_assert(sizeof IDirect3D9Vtbl == offsetof(IDirect3D9ExVtbl, GetAdapterModeCountEx), "old IDirect3D9Ex does not have IDirect3D9::RegisterSoftwareDevice().");

template<typename Interface>
using vtable_t = std::remove_pointer_t<decltype(Interface::lpVtbl)>;

HMODULE loadD3d9() {
	static HMODULE d3d9 = []() {
		TCHAR path[MAX_PATH];
		GetSystemDirectory(path, MAX_PATH);
		PathAppend(path, TEXT("d3d9.dll"));
		return LoadLibrary(path);
	}();
	_ASSERTE(d3d9);
	return d3d9;
}

template<typename Func>
inline Func* proc(const char* name) {
	auto func = reinterpret_cast<Func*>(GetProcAddress(loadD3d9(), name));
	_ASSERTE(func);
	return func;
}
#define PROC(NAME) proc<decltype(NAME)>(#NAME)

template<typename Func>
void hook(Func& target, Func& backup, Func hook) {
	if (target == backup)
		return;
	_ASSERTE(!backup);
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

void (STDMETHODCALLTYPE *Frame)(ULONGLONG, IDirect3DSurface9*) = nullptr;
D3DSURFACE_DESC desc;
auto frequency = []() {
	LARGE_INTEGER frequency;
	QueryPerformanceFrequency(&frequency);
	return static_cast<ULONGLONG>(frequency.QuadPart);
}();
unsigned fps;

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

	auto result = IDirect3DDevice9_EndScene_Orig(This);
	auto frame = Frame;
	if (frame) {
		IDirect3DSurface9* renderTarget;
		IDirect3DDevice9_GetRenderTarget(This, 0, &renderTarget);
		IDirect3DSurface9* offsecreen;
		IDirect3DDevice9_CreateOffscreenPlainSurface(This, desc.Width, desc.Height, desc.Format, D3DPOOL_SYSTEMMEM, &offsecreen, nullptr);
		IDirect3DDevice9_GetRenderTargetData(This, renderTarget, offsecreen);
		IDirect3DSurface9_Release(renderTarget);
		frame(ticks, offsecreen);
		IDirect3DSurface9_Release(offsecreen);
	}
	return result;
}
decltype(vtable_t<IDirect3DDevice9>::Release) IDirect3DDevice9_Release_Orig = nullptr;
ULONG STDMETHODCALLTYPE IDirect3DDevice9_Release_Hook(IDirect3DDevice9* This) {
	auto count = IDirect3DDevice9_Release_Orig(This);
	if (count == 1) {
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
		IDirect3DDevice9_AddRef(pDevice);
		HOOK(IDirect3DDevice9, pDevice, EndScene);
		HOOK(IDirect3DDevice9, pDevice, Release);
	}
	return result;
}
decltype(vtable_t<IDirect3D9>::Release) IDirect3D9_Release_Orig = nullptr;
ULONG STDMETHODCALLTYPE IDirect3D9_Release_Hook(IDirect3D9* This) {
	auto count = IDirect3D9_Release_Orig(This);
	if (count == 1) {
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
		IDirect3DDevice9_AddRef(pDevice);
		HOOK(IDirect3DDevice9, pDevice, EndScene);
		HOOK(IDirect3DDevice9, pDevice, Release);
	}
	return result;
}
decltype(vtable_t<IDirect3D9Ex>::Release) IDirect3D9Ex_Release_Orig = nullptr;
ULONG STDMETHODCALLTYPE IDirect3D9Ex_Release_Hook(IDirect3D9Ex* This) {
	auto count = IDirect3D9Ex_Release_Orig(This);
	if (count == 1) {
		UNHOOK(IDirect3D9, reinterpret_cast<IDirect3D9*>(This), CreateDevice);
		UNHOOK(IDirect3D9Ex, This, CreateDeviceEx);
		UNHOOK(IDirect3D9Ex, This, Release);
		count = IDirect3D9Ex_Release(This);
	}
	return count;
}

void STDMETHODCALLTYPE GetParameter(UINT* width, UINT* height, D3DFORMAT* format, unsigned* fps) {
	*width = desc.Width;
	*height = desc.Height;
	*format = desc.Format;
	*fps = ::fps;
}

void STDMETHODCALLTYPE Start(decltype(Frame) frame) {
	Frame = frame;
}

void STDMETHODCALLTYPE Stop() {
	Frame = nullptr;
}

BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID) {
	return TRUE;
}

extern "C"{
	int WINAPI D3DPERF_BeginEvent(D3DCOLOR col, LPCWSTR wszName) {
		static auto func = PROC(D3DPERF_BeginEvent);
		return func ? func(col, wszName) : -1;
	}
	int WINAPI D3DPERF_EndEvent() {
		static auto func = PROC(D3DPERF_EndEvent);
		return func ? func() : -1;
	}
	DWORD WINAPI D3DPERF_GetStatus() {
		static auto func = PROC(D3DPERF_GetStatus);
		return func ? func() : 0;
	}
	BOOL WINAPI D3DPERF_QueryRepeatFrame() {
		static auto func = PROC(D3DPERF_QueryRepeatFrame);
		return func ? func() : FALSE;
	}
	void WINAPI D3DPERF_SetMarker(D3DCOLOR col, LPCWSTR wszName) {
		static auto func = PROC(D3DPERF_SetMarker);
		if (func) func(col, wszName);
	}
	void WINAPI D3DPERF_SetOptions(DWORD dwOptions) {
		static auto func = PROC(D3DPERF_SetOptions);
		if (func) func(dwOptions);
	}
	void WINAPI D3DPERF_SetRegion(D3DCOLOR col, LPCWSTR wszName) {
		static auto func = PROC(D3DPERF_SetRegion);
		if (func) func(col, wszName);
	}
	int WINAPI DebugSetLevel() {
		static auto func = PROC(DebugSetLevel);
		return func ? func() : -1;
	}
	void WINAPI DebugSetMute() {
		static auto func = PROC(DebugSetMute);
		if (func) func();
	}
	int WINAPI Direct3D9EnableMaximizedWindowedModeShim() {
		static auto func = PROC(Direct3D9EnableMaximizedWindowedModeShim);
		return func ? func() : -1;
	}
	IDirect3D9* WINAPI Direct3DCreate9(UINT SDKVersion) {
		static auto func = PROC(Direct3DCreate9);
		if (!func)
			return nullptr;
		auto pD3D = func(SDKVersion);
		if (pD3D) {
			IDirect3D9_AddRef(pD3D);
			HOOK(IDirect3D9, pD3D, CreateDevice);
			HOOK(IDirect3D9, pD3D, Release);
		}
		return pD3D;
	}
	HRESULT WINAPI Direct3DCreate9Ex(UINT SDKVersion, IDirect3D9Ex** ppD3D) {
		static auto func = PROC(Direct3DCreate9Ex);
		if (!func)
			return D3DERR_NOTAVAILABLE;
		auto result = func(SDKVersion, ppD3D);
		if (result == S_OK) {
			auto pD3DEx = *ppD3D;
			IDirect3D9Ex_AddRef(pD3DEx);
			HOOK(IDirect3D9, reinterpret_cast<IDirect3D9*>(pD3DEx), CreateDevice);
			HOOK(IDirect3D9Ex, pD3DEx, CreateDeviceEx);
			HOOK(IDirect3D9Ex, pD3DEx, Release);
		}
		return result;
	}
}
struct IDirect3DShaderValidator9* __cdecl Direct3DShaderValidatorCreate9() {
	static auto func = PROC(Direct3DShaderValidatorCreate9);
	return func ? func() : nullptr;
}
void __cdecl PSGPError(class D3DFE_PROCESSVERTICES* a, enum PSGPERRORID b, unsigned int c) {
	static auto func = PROC(PSGPError);
	if (func) func(a, b, c);
}
void __cdecl PSGPSampleTexture(class D3DFE_PROCESSVERTICES* a, unsigned int b, float(*const c)[4], unsigned int d, float(*const e)[4]) {
	static auto func = PROC(PSGPSampleTexture);
	if (func) func(a, b, c, d, e);
}
void __cdecl Direct3D9ForceHybridEnumeration(unsigned int a) {
	static auto func = proc<decltype(Direct3D9ForceHybridEnumeration)>(MAKEINTRESOURCEA(16));
	if (func) func(a);
}
