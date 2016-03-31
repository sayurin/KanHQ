#pragma once
#include <mutex>						// for std::lock_guard, std::mutex
#include <crtdbg.h>						// for _ASSERTE, _RPTW0, _RPTWN
#include <Windows.h>
#include <Shlwapi.h>					// for PathAppend
#pragma comment(lib, "Shlwapi.lib")

BOOL APIENTRY DllMain(HINSTANCE, DWORD, LPVOID) {
	return TRUE;
}

static std::mutex loader;

static FARPROC proc(const char* name) {
	static HMODULE dll = nullptr;
	std::lock_guard<std::mutex> lock(loader);
	if (!dll) {
		TCHAR path[MAX_PATH];
		GetSystemDirectory(path, MAX_PATH);
		PathAppend(path, TEXT(DLLNAME));
		dll = LoadLibrary(path);
		_ASSERTE(dll);
	}
	auto func = GetProcAddress(dll, name);
	_ASSERTE(func);
	return func;
}

template<typename Func, int, typename ...Args>
static auto callproc(const char* name, Args... args) {
	static Func func = nullptr;
	if (!func)
		func = reinterpret_cast<Func>(proc(name));
	return func(args...);
}
#define CALLFUNCWITHPROCNAME(FUNC, PROC, ...) callproc<decltype(&FUNC), __COUNTER__>(PROC, __VA_ARGS__)
#define CALLFUNC(FUNC, ...) CALLFUNCWITHPROCNAME(FUNC, #FUNC, __VA_ARGS__)

#define FUNCTION(EXPORT, PROCNAME, RESULT, NAME, PARAMETERS, ...) RESULT NAME PARAMETERS {			\
	__pragma(comment(linker, "/EXPORT:" EXPORT));													\
	_RPTWN(_CRT_WARN, L"WININET: function called: %S\n", __FUNCTION__);								\
	return CALLFUNCWITHPROCNAME(NAME, PROCNAME, __VA_ARGS__);										\
}
#define FUNCTION_UNKNOWN(EXPORT, PROCNAME, RESULT, NAME, PARAMETERS, ...) RESULT NAME PARAMETERS {	\
	__pragma(comment(linker, "/EXPORT:" EXPORT));													\
	_RPTWN(_CRT_ERROR, L"WININET: unknown function called: %S\n", __FUNCTION__);					\
	return CALLFUNCWITHPROCNAME(NAME, PROCNAME, __VA_ARGS__);										\
}
#define FUNCTION_NAME(                   RESULT, NAME, PARAMETERS, ...)         FUNCTION(__FUNCTION__ "=" __FUNCDNAME__                        , __FUNCTION__             , RESULT, NAME, PARAMETERS, __VA_ARGS__)
#define FUNCTION_NAME_ORDINAL(  ORDINAL, RESULT, NAME, PARAMETERS, ...)         FUNCTION(__FUNCTION__ "=" __FUNCDNAME__ ",@" #ORDINAL          , __FUNCTION__             , RESULT, NAME, PARAMETERS, __VA_ARGS__)
#define FUNCTION_NONAME(        ORDINAL, RESULT, NAME, PARAMETERS, ...)         FUNCTION(__FUNCDNAME__                  ",@" #ORDINAL ",NONAME", MAKEINTRESOURCEA(ORDINAL), RESULT, NAME, PARAMETERS, __VA_ARGS__)
#define FUNCTION_UNKNOWN_NAME(           RESULT, NAME, PARAMETERS, ...) FUNCTION_UNKNOWN(__FUNCTION__ "=" __FUNCDNAME__                        , __FUNCTION__             , RESULT, NAME, PARAMETERS, __VA_ARGS__)
#define FUNCTION_UNKNOWN_NONAME(ORDINAL, RESULT, NAME, PARAMETERS, ...) FUNCTION_UNKNOWN(__FUNCDNAME__                  ",@" #ORDINAL ",NONAME", MAKEINTRESOURCEA(ORDINAL), RESULT, NAME, PARAMETERS, __VA_ARGS__)
