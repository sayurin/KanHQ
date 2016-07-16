#define _USING_V110_SDK71_				// for xp
#define _WINX32_						// don't import WinInet.h and Winineti.h
#define WIN32_LEAN_AND_MEAN				// avoid Winsock
#define _HAS_EXCEPTIONS 0
#include <algorithm>					// for std::copy_n
#include <memory>						// for std::make_unique, std::unique_ptr
#include <mutex>						// for std::lock_guard, std::mutex
#include <string>						// for std::wstring
#include <unordered_map>				// for std::unordered_map
#include <vector>						// for std::vector
#include <crtdbg.h>						// for _ASSERTE, _RPTW0, _RPTWN
#include <Windows.h>
#include <WinSock2.h>					// for SOCKET
#include <WinInet.h>
#include <Winineti.h>

#define DLLNAME "WININET.dll"
#include "../callproc.h"

static inline void append(std::vector<unsigned char>& buffer, const void* src, std::size_t count) {
	buffer.resize(size(buffer) + count);
	std::copy_n(reinterpret_cast<const unsigned char*>(src), count, begin(buffer) + size(buffer) - count);
}

class char2wchar {
	std::unique_ptr<wchar_t[]> buffer;
public:
	char2wchar(const char* src) {
		if (!src)
			return;
		auto length = MultiByteToWideChar(CP_THREAD_ACP, 0, src, -1, nullptr, 0);
		buffer = std::make_unique<wchar_t[]>(length);
		auto result = MultiByteToWideChar(CP_THREAD_ACP, 0, src, -1, buffer.get(), length);
		_ASSERTE(result == length);
	}
	operator const wchar_t*() const {
		return buffer ? buffer.get() : nullptr;
	}
};


static bool (CALLBACK* OnRequest)(const wchar_t* path) = nullptr;
static void (CALLBACK* OnResponse)(const wchar_t* path, const unsigned char* request, unsigned requestSize, const unsigned char* response, unsigned responseSize) = nullptr;
static std::mutex mutex;
struct session {
	std::wstring path;
	std::vector<unsigned char> request;
	std::vector<unsigned char> response;
	session(const wchar_t* path) : path(path) {}
};
static std::unordered_map<HINTERNET, session> sessions;

void STDAPICALLTYPE SetCallback(decltype(OnRequest) onRequest, decltype(OnResponse) onResponse) {
	__pragma(comment(linker, "/EXPORT:" __FUNCTION__ "=" __FUNCDNAME__));
	OnRequest = onRequest;
	OnResponse = onResponse;
}

static void addSession(HINTERNET file, const wchar_t* path) {
	if (file && OnRequest && OnRequest(path)) {
		std::lock_guard<std::mutex> lock(mutex);
		sessions.emplace(file, path);
		_RPTWN(_CRT_WARN, L"WININET: active sessions = %d\n", sessions.size());
	}
}

static void addRequest(BOOL result, HINTERNET hRequest, LPVOID lpOptional, DWORD dwOptionalLength) {
	auto lastError = GetLastError();
	if ((result || lastError == ERROR_IO_PENDING) && lpOptional && 0 < dwOptionalLength) {
		std::lock_guard<std::mutex> lock(mutex);
		auto itor = sessions.find(hRequest);
		if (itor != sessions.end())
			append(itor->second.request, lpOptional, dwOptionalLength);
	}
	SetLastError(lastError);
}

static void callOnResponse(const decltype(sessions)::iterator& itor) {
	if (OnResponse) {
		auto& request = itor->second.request;
		auto& response = itor->second.response;
		OnResponse(itor->second.path.c_str(), request.data(), unsigned(request.size()), response.data(), unsigned(response.size()));
	}
	sessions.erase(itor);
	_RPTWN(_CRT_WARN, L"WININET: remain sessions = %d\n", sessions.size());
}


INTERNETAPI_(HINTERNET) HttpOpenRequestA(_In_ HINTERNET hConnect, _In_opt_ LPCSTR lpszVerb, _In_opt_ LPCSTR lpszObjectName, _In_opt_ LPCSTR lpszVersion, _In_opt_ LPCSTR lpszReferrer, _In_opt_z_ LPCSTR FAR * lplpszAcceptTypes, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext) {
	__pragma(comment(linker, "/EXPORT:" __FUNCTION__ "=" __FUNCDNAME__));

	_RPTWN(_CRT_WARN, L"WININET: HttpOpenRequestA(%S %S)\n", lpszVerb, lpszObjectName);
	auto file = CALLFUNC(HttpOpenRequestA, hConnect, lpszVerb, lpszObjectName, lpszVersion, lpszReferrer, lplpszAcceptTypes, dwFlags, dwContext);
	auto lastError = GetLastError();
	addSession(file, char2wchar(lpszObjectName));
	SetLastError(lastError);
	return file;
}

INTERNETAPI_(HINTERNET) HttpOpenRequestW(_In_ HINTERNET hConnect, _In_opt_ LPCWSTR lpszVerb, _In_opt_ LPCWSTR lpszObjectName, _In_opt_ LPCWSTR lpszVersion, _In_opt_ LPCWSTR lpszReferrer, _In_opt_z_ LPCWSTR FAR * lplpszAcceptTypes, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext) {
	__pragma(comment(linker, "/EXPORT:" __FUNCTION__ "=" __FUNCDNAME__));

	_RPTWN(_CRT_WARN, L"WININET: HttpOpenRequestW(%s %s)\n", lpszVerb, lpszObjectName);
	auto file = CALLFUNC(HttpOpenRequestW, hConnect, lpszVerb, lpszObjectName, lpszVersion, lpszReferrer, lplpszAcceptTypes, dwFlags, dwContext);
	auto lastError = GetLastError();
	addSession(file, lpszObjectName);
	SetLastError(lastError);
	return file;
}

BOOLAPI HttpSendRequestA(_In_ HINTERNET hRequest, _In_reads_opt_(dwHeadersLength) LPCSTR lpszHeaders, _In_ DWORD dwHeadersLength, _In_reads_bytes_opt_(dwOptionalLength) LPVOID lpOptional, _In_ DWORD dwOptionalLength) {
	__pragma(comment(linker, "/EXPORT:" __FUNCTION__ "=" __FUNCDNAME__));

	_RPTWN(_CRT_WARN, L"WININET: HttpSendRequestA(opt=%d)\n", dwOptionalLength);
	auto result = CALLFUNC(HttpSendRequestA, hRequest, lpszHeaders, dwHeadersLength, lpOptional, dwOptionalLength);
	addRequest(result, hRequest, lpOptional, dwOptionalLength);
	return result;
}

BOOLAPI HttpSendRequestW(_In_ HINTERNET hRequest, _In_reads_opt_(dwHeadersLength) LPCWSTR lpszHeaders, _In_ DWORD dwHeadersLength, _In_reads_bytes_opt_(dwOptionalLength) LPVOID lpOptional, _In_ DWORD dwOptionalLength) {
	__pragma(comment(linker, "/EXPORT:" __FUNCTION__ "=" __FUNCDNAME__));

	_RPTWN(_CRT_WARN, L"WININET: HttpSendRequestW(opt=%d)\n", dwOptionalLength);
	auto result = CALLFUNC(HttpSendRequestW, hRequest, lpszHeaders, dwHeadersLength, lpOptional, dwOptionalLength);
	addRequest(result, hRequest, lpOptional, dwOptionalLength);
	return result;
}

BOOLAPI InternetReadFile(_In_ HINTERNET hFile, _Out_writes_bytes_(dwNumberOfBytesToRead) __out_data_source(NETWORK) LPVOID lpBuffer, _In_ DWORD dwNumberOfBytesToRead, _Out_ LPDWORD lpdwNumberOfBytesRead) {
	__pragma(comment(linker, "/EXPORT:" __FUNCTION__ "=" __FUNCDNAME__));

	auto result = CALLFUNC(InternetReadFile, hFile, lpBuffer, dwNumberOfBytesToRead, lpdwNumberOfBytesRead);
	auto lastError = GetLastError();
	{
		std::lock_guard<std::mutex> lock(mutex);
		auto itor = sessions.find(hFile);
		if (itor != sessions.end()) {
			_RPTWN(_CRT_WARN, L"WININET: InternetReadFile(%d => %d, %s, %d) => %d, %d\n", dwNumberOfBytesToRead, *lpdwNumberOfBytesRead, itor->second.path.c_str(), itor->second.response.size(), result, lastError);
			if (result) {
				auto read = *lpdwNumberOfBytesRead;
				if (0 < read)
					append(itor->second.response, lpBuffer, read);
				else
					callOnResponse(itor);
			} else
				sessions.erase(itor);
		} else
			_RPTWN(_CRT_WARN, L"WININET: InternetReadFile(%d => %d) %d, %d\n", dwNumberOfBytesToRead, *lpdwNumberOfBytesRead, result, lastError);
	}
	SetLastError(lastError);
	return result;
}

BOOLAPI InternetQueryDataAvailable(_In_ HINTERNET hFile, _Out_opt_ __out_data_source(NETWORK) LPDWORD lpdwNumberOfBytesAvailable, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext) {
	__pragma(comment(linker, "/EXPORT:" __FUNCTION__ "=" __FUNCDNAME__));

	auto result = CALLFUNC(InternetQueryDataAvailable, hFile, lpdwNumberOfBytesAvailable, dwFlags, dwContext);
	auto lastError = GetLastError();
	if (result && *lpdwNumberOfBytesAvailable == 0) {
		std::lock_guard<std::mutex> lock(mutex);
		auto itor = sessions.find(hFile);
		if (itor != sessions.end()) {
			_RPTWN(_CRT_WARN, L"WININET: InternetQueryDataAvailable(%d, %s) => %d, %d\n", *lpdwNumberOfBytesAvailable, itor->second.path.c_str(), result, lastError);
			callOnResponse(itor);
		} else
			_RPTWN(_CRT_WARN, L"WININET: InternetQueryDataAvailable(%d) => %d, %d\n", *lpdwNumberOfBytesAvailable, result, lastError);
	}
	SetLastError(lastError);
	return result;
}

BOOLAPI InternetCloseHandle(_In_ HINTERNET hInternet) {
	__pragma(comment(linker, "/EXPORT:" __FUNCTION__ "=" __FUNCDNAME__));

	{
		std::lock_guard<std::mutex> lock(mutex);
		auto itor = sessions.find(hInternet);
		if (itor != sessions.end()) {
			_RPTWN(_CRT_WARN, L"WININET: InternetCloseHandle(%s)\n", itor->second.path.c_str());
			callOnResponse(itor);
		} else
			_RPTW0(_CRT_WARN, L"WININET: InternetCloseHandle()\n");
	}
	return CALLFUNC(InternetCloseHandle, hInternet);
}


#pragma region "proxy"
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheCheckManifest, (_In_opt_ PCWSTR pwszMasterUrl, _In_ PCWSTR pwszManifestUrl, _In_reads_bytes_(dwManifestDataSize) const BYTE *pbManifestData, _In_ DWORD dwManifestDataSize, _In_reads_bytes_(dwManifestResponseHeadersSize) const BYTE *pbManifestResponseHeaders, _In_ DWORD dwManifestResponseHeadersSize, _Out_ APP_CACHE_STATE *peState, _Out_ APP_CACHE_HANDLE *phNewAppCache), pwszMasterUrl, pwszManifestUrl, pbManifestData, dwManifestDataSize, pbManifestResponseHeaders, dwManifestResponseHeadersSize, peState, phNewAppCache)
FUNCTION_NAME(INTERNETAPI_(VOID), AppCacheCloseHandle, (_In_ APP_CACHE_HANDLE hAppCache), hAppCache)
FUNCTION_UNKNOWN_NAME(INTERNETAPI_(DWORD), AppCacheCreateAndCommitFile, (_In_ APP_CACHE_HANDLE hAppCache, _In_ PCWSTR pwszSourceFilePath, _In_ PCWSTR pwszUrl, _In_reads_bytes_(dwResponseHeadersSize) const BYTE *pbResponseHeaders, _In_ DWORD dwResponseHeadersSize), hAppCache, pwszSourceFilePath, pwszUrl, pbResponseHeaders, dwResponseHeadersSize)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheDeleteGroup, (_In_ PCWSTR pwszManifestUrl), pwszManifestUrl)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheDeleteIEGroup, (_In_ PCWSTR pwszManifestUrl), pwszManifestUrl)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheDuplicateHandle, (_In_ APP_CACHE_HANDLE hAppCache, _Outptr_ APP_CACHE_HANDLE *phDuplicatedAppCache), hAppCache, phDuplicatedAppCache)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheFinalize, (_In_ APP_CACHE_HANDLE hAppCache, _In_reads_bytes_(dwManifestDataSize) const BYTE *pbManifestData, _In_ DWORD dwManifestDataSize, _Out_ APP_CACHE_FINALIZE_STATE *peState), hAppCache, pbManifestData, dwManifestDataSize, peState)
FUNCTION_NAME(INTERNETAPI_(VOID), AppCacheFreeDownloadList, (_Inout_ APP_CACHE_DOWNLOAD_LIST *pDownloadList), pDownloadList)
FUNCTION_NAME(INTERNETAPI_(VOID), AppCacheFreeGroupList, (_Inout_ APP_CACHE_GROUP_LIST *pAppCacheGroupList), pAppCacheGroupList)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheFreeIESpace, (_In_ FILETIME ftCutOff), ftCutOff)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheFreeSpace, (_In_ FILETIME ftCutOff), ftCutOff)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheGetDownloadList, (_In_ APP_CACHE_HANDLE hAppCache, _Out_ APP_CACHE_DOWNLOAD_LIST *pDownloadList), hAppCache, pDownloadList)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheGetFallbackUrl, (_In_ APP_CACHE_HANDLE hAppCache, _In_ PCWSTR pwszUrl, _Outptr_result_z_ PWSTR *ppwszFallbackUrl), hAppCache, pwszUrl, ppwszFallbackUrl)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheGetGroupList, (_Out_ APP_CACHE_GROUP_LIST *pAppCacheGroupList), pAppCacheGroupList)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheGetIEGroupList, (_Out_ APP_CACHE_GROUP_LIST *pAppCacheGroupList), pAppCacheGroupList)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheGetInfo, (_In_ APP_CACHE_HANDLE hAppCache, _Out_ APP_CACHE_GROUP_INFO *pAppCacheInfo), hAppCache, pAppCacheInfo)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheGetManifestUrl, (_In_ APP_CACHE_HANDLE hAppCache, _Outptr_result_z_ PWSTR *ppwszManifestUrl), hAppCache, ppwszManifestUrl)
FUNCTION_NAME(INTERNETAPI_(DWORD), AppCacheLookup, (_In_ PCWSTR pwszUrl, _In_ DWORD dwFlags, _Out_ APP_CACHE_HANDLE *phAppCache), pwszUrl, dwFlags, phAppCache)
FUNCTION_NAME(BOOLAPI, CommitUrlCacheEntryA, (_In_ LPCSTR lpszUrlName, _In_opt_ LPCSTR lpszLocalFileName, _In_ FILETIME ExpireTime, _In_ FILETIME LastModifiedTime, _In_ DWORD CacheEntryType, _In_reads_opt_(cchHeaderInfo) LPBYTE lpHeaderInfo, _In_ DWORD cchHeaderInfo, _Reserved_ LPCSTR lpszFileExtension, _In_opt_ LPCSTR lpszOriginalUrl), lpszUrlName, lpszLocalFileName, ExpireTime, LastModifiedTime, CacheEntryType, lpHeaderInfo, cchHeaderInfo, lpszFileExtension, lpszOriginalUrl)
FUNCTION_NAME(URLCACHEAPI_(DWORD), CommitUrlCacheEntryBinaryBlob, (_In_ PCWSTR pwszUrlName, _In_ DWORD dwType, _In_ FILETIME ftExpireTime, _In_ FILETIME ftModifiedTime, _In_reads_opt_(cbBlob) const BYTE *pbBlob, _In_ DWORD cbBlob), pwszUrlName, dwType, ftExpireTime, ftModifiedTime, pbBlob, cbBlob)
FUNCTION_NAME(BOOLAPI, CommitUrlCacheEntryW, (_In_ LPCWSTR lpszUrlName, _In_opt_ LPCWSTR lpszLocalFileName, _In_ FILETIME ExpireTime, _In_ FILETIME LastModifiedTime, _In_ DWORD CacheEntryType, _In_reads_opt_(cchHeaderInfo) LPWSTR lpszHeaderInfo, _In_ DWORD cchHeaderInfo, _Reserved_ LPCWSTR lpszFileExtension, _In_opt_ LPCWSTR lpszOriginalUrl), lpszUrlName, lpszLocalFileName, ExpireTime, LastModifiedTime, CacheEntryType, lpszHeaderInfo, cchHeaderInfo, lpszFileExtension, lpszOriginalUrl)
FUNCTION_NAME(BOOLAPI, CreateMD5SSOHash, (_In_ PWSTR pszChallengeInfo, _In_ PWSTR pwszRealm, _In_ PWSTR pwszTarget, _Out_ PBYTE pbHexHash), pszChallengeInfo, pwszRealm, pwszTarget, pbHexHash)
FUNCTION_NAME(BOOLAPI, CreateUrlCacheContainerA, (_In_ LPCSTR Name, _In_ LPCSTR lpCachePrefix, _In_opt_ LPCSTR lpszCachePath, _In_ DWORD KBCacheLimit, _In_ DWORD dwContainerType, _In_ DWORD dwOptions, _Reserved_ LPVOID pvBuffer, _Reserved_ LPDWORD cbBuffer), Name, lpCachePrefix, lpszCachePath, KBCacheLimit, dwContainerType, dwOptions, pvBuffer, cbBuffer)
FUNCTION_NAME(BOOLAPI, CreateUrlCacheContainerW, (_In_ LPCWSTR Name, _In_ LPCWSTR lpCachePrefix, _In_opt_ LPCWSTR lpszCachePath, _In_ DWORD KBCacheLimit, _In_ DWORD dwContainerType, _In_ DWORD dwOptions, _Reserved_ LPVOID pvBuffer, _Reserved_ LPDWORD cbBuffer), Name, lpCachePrefix, lpszCachePath, KBCacheLimit, dwContainerType, dwOptions, pvBuffer, cbBuffer)
FUNCTION_NAME(BOOLAPI, CreateUrlCacheEntryA, (_In_ LPCSTR lpszUrlName, _In_ DWORD dwExpectedFileSize, _In_opt_ LPCSTR lpszFileExtension, _Inout_updates_(MAX_PATH) LPSTR lpszFileName, _In_ DWORD dwReserved), lpszUrlName, dwExpectedFileSize, lpszFileExtension, lpszFileName, dwReserved)
FUNCTION_NAME(BOOLAPI, CreateUrlCacheEntryExW, (_In_ LPCWSTR lpszUrlName, _In_ DWORD dwExpectedFileSize, _In_opt_ LPCWSTR lpszFileExtension, _Inout_updates_(MAX_PATH) LPWSTR lpszFileName, _In_ DWORD dwReserved, _In_ BOOL fPreserveIncomingFileName), lpszUrlName, dwExpectedFileSize, lpszFileExtension, lpszFileName, dwReserved, fPreserveIncomingFileName)
FUNCTION_NAME(BOOLAPI, CreateUrlCacheEntryW, (_In_ LPCWSTR lpszUrlName, _In_ DWORD dwExpectedFileSize, _In_opt_ LPCWSTR lpszFileExtension, _Inout_updates_(MAX_PATH) LPWSTR lpszFileName, _In_ DWORD dwReserved), lpszUrlName, dwExpectedFileSize, lpszFileExtension, lpszFileName, dwReserved)
FUNCTION_NAME(INTERNETAPI_(GROUPID), CreateUrlCacheGroup, (_In_ DWORD dwFlags, _Reserved_ LPVOID lpReserved), dwFlags, lpReserved)
FUNCTION_NAME(INTERNETAPI_(DWORD), DeleteIE3Cache, (_In_ HWND hwnd, _In_ HINSTANCE hinst, _In_ LPSTR lpszCmd, _In_ int nCmdShow), hwnd, hinst, lpszCmd, nCmdShow)
FUNCTION_NAME(BOOLAPI, DeleteUrlCacheContainerA, (_In_ LPCSTR Name, _In_ DWORD dwOptions), Name, dwOptions)
FUNCTION_NAME(BOOLAPI, DeleteUrlCacheContainerW, (_In_ LPCWSTR Name, _In_ DWORD dwOptions), Name, dwOptions)
#undef DeleteUrlCacheEntry
FUNCTION_NAME(BOOLAPI, DeleteUrlCacheEntry, (_In_ LPCSTR lpszUrlName), lpszUrlName)
FUNCTION_NAME(BOOLAPI, DeleteUrlCacheEntryA, (_In_ LPCSTR lpszUrlName), lpszUrlName)
FUNCTION_NAME(BOOLAPI, DeleteUrlCacheEntryW, (_In_ LPCWSTR lpszUrlName), lpszUrlName)
FUNCTION_NAME(BOOLAPI, DeleteUrlCacheGroup, (_In_ GROUPID GroupId, _In_ DWORD dwFlags, _Reserved_ LPVOID lpReserved), GroupId, dwFlags, lpReserved)
FUNCTION_NAME(BOOLAPI, DeleteWpadCacheForNetworks, (_In_ WPAD_CACHE_DELETE wpadCacheDelete), wpadCacheDelete)
FUNCTION_NAME(BOOLAPI, DetectAutoProxyUrl, (_Out_writes_(cchAutoProxyUrl) PSTR pszAutoProxyUrl, _In_ DWORD cchAutoProxyUrl, _In_ DWORD dwDetectFlags), pszAutoProxyUrl, cchAutoProxyUrl, dwDetectFlags)
FUNCTION_NAME_ORDINAL(106, void CALLBACK, DispatchAPICall, (HWND hwnd, HINSTANCE hinst, LPSTR lpszCmdLine, int nCmdShow), hwnd, hinst, lpszCmdLine, nCmdShow)
FUNCTION_NAME(STDAPI, DllCanUnloadNow, (void), )
FUNCTION_NAME(STDAPI, DllGetClassObject, (_In_ REFCLSID rclsid, _In_ REFIID riid, _Outptr_ LPVOID FAR* ppv), rclsid, riid, ppv)
FUNCTION_NAME(STDAPI, DllInstall, (BOOL bInstall, _In_opt_ PCWSTR pszCmdLine), bInstall, pszCmdLine)
FUNCTION_NAME(STDAPI, DllRegisterServer, (void), )
FUNCTION_NAME(STDAPI, DllUnregisterServer, (void), )
FUNCTION_NAME(BOOLAPI, FindCloseUrlCache, (_In_ HANDLE hEnumHandle), hEnumHandle)
FUNCTION_NAME(INTERNETAPI_(HANDLE), FindFirstUrlCacheContainerA, (_Inout_ LPDWORD pdwModified, _Out_writes_bytes_(*lpcbContainerInfo) LPINTERNET_CACHE_CONTAINER_INFOA lpContainerInfo, _Inout_ LPDWORD lpcbContainerInfo, _In_ DWORD dwOptions), pdwModified, lpContainerInfo, lpcbContainerInfo, dwOptions)
FUNCTION_NAME(INTERNETAPI_(HANDLE), FindFirstUrlCacheContainerW, (_Inout_ LPDWORD pdwModified, _Out_writes_bytes_(*lpcbContainerInfo) LPINTERNET_CACHE_CONTAINER_INFOW lpContainerInfo, _Inout_ LPDWORD lpcbContainerInfo, _In_ DWORD dwOptions), pdwModified, lpContainerInfo, lpcbContainerInfo, dwOptions)
FUNCTION_NAME(INTERNETAPI_(HANDLE), FindFirstUrlCacheEntryA, (_In_opt_ LPCSTR lpszUrlSearchPattern, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOA lpFirstCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo), lpszUrlSearchPattern, lpFirstCacheEntryInfo, lpcbCacheEntryInfo)
FUNCTION_NAME(INTERNETAPI_(HANDLE), FindFirstUrlCacheEntryExA, (_In_opt_ LPCSTR lpszUrlSearchPattern, _In_ DWORD dwFlags, _In_ DWORD dwFilter, _In_ GROUPID GroupId, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOA lpFirstCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo, _Reserved_ LPVOID lpGroupAttributes, _Reserved_ LPDWORD lpcbGroupAttributes, _Reserved_ LPVOID lpReserved), lpszUrlSearchPattern, dwFlags, dwFilter, GroupId, lpFirstCacheEntryInfo, lpcbCacheEntryInfo, lpGroupAttributes, lpcbGroupAttributes, lpReserved)
FUNCTION_NAME(INTERNETAPI_(HANDLE), FindFirstUrlCacheEntryExW, (_In_opt_ LPCWSTR lpszUrlSearchPattern, _In_ DWORD dwFlags, _In_ DWORD dwFilter, _In_ GROUPID GroupId, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOW lpFirstCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo, _Reserved_ LPVOID lpGroupAttributes, _Reserved_ LPDWORD lpcbGroupAttributes, _Reserved_ LPVOID lpReserved), lpszUrlSearchPattern, dwFlags, dwFilter, GroupId, lpFirstCacheEntryInfo, lpcbCacheEntryInfo, lpGroupAttributes, lpcbGroupAttributes, lpReserved)
FUNCTION_NAME(INTERNETAPI_(HANDLE), FindFirstUrlCacheEntryW, (_In_opt_ LPCWSTR lpszUrlSearchPattern, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOW lpFirstCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo), lpszUrlSearchPattern, lpFirstCacheEntryInfo, lpcbCacheEntryInfo)
FUNCTION_NAME(URLCACHEAPI_(HANDLE), FindFirstUrlCacheGroup, (_In_ DWORD dwFlags, _In_ DWORD dwFilter, _Reserved_ LPVOID lpSearchCondition, _Reserved_ DWORD dwSearchCondition, _Out_ GROUPID* lpGroupId, _Reserved_ LPVOID lpReserved), dwFlags, dwFilter, lpSearchCondition, dwSearchCondition, lpGroupId, lpReserved)
FUNCTION_NAME(BOOLAPI, FindNextUrlCacheContainerA, (_In_ HANDLE hEnumHandle, _Out_writes_bytes_(*lpcbContainerInfo) LPINTERNET_CACHE_CONTAINER_INFOA lpContainerInfo, _Inout_ LPDWORD lpcbContainerInfo), hEnumHandle, lpContainerInfo, lpcbContainerInfo)
FUNCTION_NAME(BOOLAPI, FindNextUrlCacheContainerW, (_In_ HANDLE hEnumHandle, _Out_writes_bytes_(*lpcbContainerInfo) LPINTERNET_CACHE_CONTAINER_INFOW lpContainerInfo, _Inout_ LPDWORD lpcbContainerInfo), hEnumHandle, lpContainerInfo, lpcbContainerInfo)
FUNCTION_NAME(BOOLAPI, FindNextUrlCacheEntryA, (_In_ HANDLE hEnumHandle, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOA lpNextCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo), hEnumHandle, lpNextCacheEntryInfo, lpcbCacheEntryInfo)
FUNCTION_NAME(BOOLAPI, FindNextUrlCacheEntryExA, (_In_ HANDLE hEnumHandle, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOA lpNextCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo, _Reserved_ LPVOID lpGroupAttributes, _Reserved_ LPDWORD lpcbGroupAttributes, _Reserved_ LPVOID lpReserved), hEnumHandle, lpNextCacheEntryInfo, lpcbCacheEntryInfo, lpGroupAttributes, lpcbGroupAttributes, lpReserved)
FUNCTION_NAME(BOOLAPI, FindNextUrlCacheEntryExW, (_In_ HANDLE hEnumHandle, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOW lpNextCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo, _Reserved_ LPVOID lpGroupAttributes, _Reserved_ LPDWORD lpcbGroupAttributes, _Reserved_ LPVOID lpReserved), hEnumHandle, lpNextCacheEntryInfo, lpcbCacheEntryInfo, lpGroupAttributes, lpcbGroupAttributes, lpReserved)
FUNCTION_NAME(BOOLAPI, FindNextUrlCacheEntryW, (_In_ HANDLE hEnumHandle, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOW lpNextCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo), hEnumHandle, lpNextCacheEntryInfo, lpcbCacheEntryInfo)
FUNCTION_NAME(BOOLAPI, FindNextUrlCacheGroup, (_In_ HANDLE hFind, _Out_ GROUPID* lpGroupId, _Reserved_ LPVOID lpReserved), hFind, lpGroupId, lpReserved)
FUNCTION_UNKNOWN_NAME(INTERNETAPI_(DWORD), ForceNexusLookup, (void), )
FUNCTION_UNKNOWN_NAME(INTERNETAPI_(DWORD), ForceNexusLookupExW, (DWORD arg0, DWORD arg1, DWORD arg2, DWORD arg3, DWORD arg4), arg0, arg1, arg2, arg3, arg4)
FUNCTION_NAME(BOOLAPI, FreeUrlCacheSpaceA, (_In_opt_ LPCSTR lpszCachePath, _In_ DWORD dwSize, _In_ DWORD dwFilter), lpszCachePath, dwSize, dwFilter)
FUNCTION_NAME(BOOLAPI, FreeUrlCacheSpaceW, (_In_opt_ LPCWSTR lpszCachePath, _In_ DWORD dwSize, _In_ DWORD dwFilter), lpszCachePath, dwSize, dwFilter)
FUNCTION_NAME(BOOLAPI, FtpCommandA, (_In_ HINTERNET hConnect, _In_ BOOL fExpectResponse, _In_ DWORD dwFlags, _In_ LPCSTR lpszCommand, _In_opt_ DWORD_PTR dwContext, _Out_opt_ HINTERNET *phFtpCommand), hConnect, fExpectResponse, dwFlags, lpszCommand, dwContext, phFtpCommand)
FUNCTION_NAME(BOOLAPI, FtpCommandW, (_In_ HINTERNET hConnect, _In_ BOOL fExpectResponse, _In_ DWORD dwFlags, _In_ LPCWSTR lpszCommand, _In_opt_ DWORD_PTR dwContext, _Out_opt_ HINTERNET *phFtpCommand), hConnect, fExpectResponse, dwFlags, lpszCommand, dwContext, phFtpCommand)
FUNCTION_NAME(BOOLAPI, FtpCreateDirectoryA, (_In_ HINTERNET hConnect, _In_ LPCSTR lpszDirectory), hConnect, lpszDirectory)
FUNCTION_NAME(BOOLAPI, FtpCreateDirectoryW, (_In_ HINTERNET hConnect, _In_ LPCWSTR lpszDirectory), hConnect, lpszDirectory)
FUNCTION_NAME(BOOLAPI, FtpDeleteFileA, (_In_ HINTERNET hConnect, _In_ LPCSTR lpszFileName), hConnect, lpszFileName)
FUNCTION_NAME(BOOLAPI, FtpDeleteFileW, (_In_ HINTERNET hConnect, _In_ LPCWSTR lpszFileName), hConnect, lpszFileName)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), FtpFindFirstFileA, (_In_ HINTERNET hConnect, _In_opt_ LPCSTR lpszSearchFile, _Out_opt_ LPWIN32_FIND_DATAA lpFindFileData, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszSearchFile, lpFindFileData, dwFlags, dwContext)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), FtpFindFirstFileW, (_In_ HINTERNET hConnect, _In_opt_ LPCWSTR lpszSearchFile, _Out_opt_ LPWIN32_FIND_DATAW lpFindFileData, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszSearchFile, lpFindFileData, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, FtpGetCurrentDirectoryA, (_In_ HINTERNET hConnect, _Out_writes_(*lpdwCurrentDirectory) LPSTR lpszCurrentDirectory, _Inout_ LPDWORD lpdwCurrentDirectory), hConnect, lpszCurrentDirectory, lpdwCurrentDirectory)
FUNCTION_NAME(BOOLAPI, FtpGetCurrentDirectoryW, (_In_ HINTERNET hConnect, _Out_writes_(*lpdwCurrentDirectory) LPWSTR lpszCurrentDirectory, _Inout_ LPDWORD lpdwCurrentDirectory), hConnect, lpszCurrentDirectory, lpdwCurrentDirectory)
FUNCTION_NAME(BOOLAPI, FtpGetFileA, (_In_ HINTERNET hConnect, _In_ LPCSTR lpszRemoteFile, _In_ LPCSTR lpszNewFile, _In_ BOOL fFailIfExists, _In_ DWORD dwFlagsAndAttributes, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszRemoteFile, lpszNewFile, fFailIfExists, dwFlagsAndAttributes, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, FtpGetFileEx, (_In_ HINTERNET hFtpSession, _In_ LPCSTR lpszRemoteFile, _In_ LPCWSTR lpszNewFile, _In_ BOOL fFailIfExists, _In_ DWORD dwFlagsAndAttributes, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hFtpSession, lpszRemoteFile, lpszNewFile, fFailIfExists, dwFlagsAndAttributes, dwFlags, dwContext)
FUNCTION_NAME(INTERNETAPI_(DWORD), FtpGetFileSize, (_In_ HINTERNET hFile, _Out_opt_ LPDWORD lpdwFileSizeHigh), hFile, lpdwFileSizeHigh)
FUNCTION_NAME(BOOLAPI, FtpGetFileW, (_In_ HINTERNET hConnect, _In_ LPCWSTR lpszRemoteFile, _In_ LPCWSTR lpszNewFile, _In_ BOOL fFailIfExists, _In_ DWORD dwFlagsAndAttributes, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszRemoteFile, lpszNewFile, fFailIfExists, dwFlagsAndAttributes, dwFlags, dwContext)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), FtpOpenFileA, (_In_ HINTERNET hConnect, _In_ LPCSTR lpszFileName, _In_ DWORD dwAccess, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszFileName, dwAccess, dwFlags, dwContext)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), FtpOpenFileW, (_In_ HINTERNET hConnect, _In_ LPCWSTR lpszFileName, _In_ DWORD dwAccess, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszFileName, dwAccess, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, FtpPutFileA, (_In_ HINTERNET hConnect, _In_ LPCSTR lpszLocalFile, _In_ LPCSTR lpszNewRemoteFile, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszLocalFile, lpszNewRemoteFile, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, FtpPutFileEx, (_In_ HINTERNET hFtpSession, _In_ LPCWSTR lpszLocalFile, _In_ LPCSTR lpszNewRemoteFile, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hFtpSession, lpszLocalFile, lpszNewRemoteFile, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, FtpPutFileW, (_In_ HINTERNET hConnect, _In_ LPCWSTR lpszLocalFile, _In_ LPCWSTR lpszNewRemoteFile, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszLocalFile, lpszNewRemoteFile, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, FtpRemoveDirectoryA, (_In_ HINTERNET hConnect, _In_ LPCSTR lpszDirectory), hConnect, lpszDirectory)
FUNCTION_NAME(BOOLAPI, FtpRemoveDirectoryW, (_In_ HINTERNET hConnect, _In_ LPCWSTR lpszDirectory), hConnect, lpszDirectory)
FUNCTION_NAME(BOOLAPI, FtpRenameFileA, (_In_ HINTERNET hConnect, _In_ LPCSTR lpszExisting, _In_ LPCSTR lpszNew), hConnect, lpszExisting, lpszNew)
FUNCTION_NAME(BOOLAPI, FtpRenameFileW, (_In_ HINTERNET hConnect, _In_ LPCWSTR lpszExisting, _In_ LPCWSTR lpszNew), hConnect, lpszExisting, lpszNew)
FUNCTION_NAME(BOOLAPI, FtpSetCurrentDirectoryA, (_In_ HINTERNET hConnect, _In_ LPCSTR lpszDirectory), hConnect, lpszDirectory)
FUNCTION_NAME(BOOLAPI, FtpSetCurrentDirectoryW, (_In_ HINTERNET hConnect, _In_ LPCWSTR lpszDirectory), hConnect, lpszDirectory)
FUNCTION_UNKNOWN_NAME(INTERNETAPI_(DWORD), GetProxyDllInfo, (DWORD arg0, DWORD arg1), arg0, arg1)
FUNCTION_NAME(BOOLAPI, GetUrlCacheConfigInfoA, (_Inout_ LPINTERNET_CACHE_CONFIG_INFOA lpCacheConfigInfo, _Reserved_ LPDWORD lpcbCacheConfigInfo, _In_ DWORD dwFieldControl), lpCacheConfigInfo, lpcbCacheConfigInfo, dwFieldControl)
FUNCTION_NAME(BOOLAPI, GetUrlCacheConfigInfoW, (_Inout_ LPINTERNET_CACHE_CONFIG_INFOW lpCacheConfigInfo, _Reserved_ LPDWORD lpcbCacheConfigInfo, _In_ DWORD dwFieldControl), lpCacheConfigInfo, lpcbCacheConfigInfo, dwFieldControl)
FUNCTION_NAME(URLCACHEAPI_(DWORD), GetUrlCacheEntryBinaryBlob, (_In_ PCWSTR pwszUrlName, _Out_ DWORD *dwType, _Out_ FILETIME *pftExpireTime, _Out_ FILETIME *pftAccessTime, _Out_ FILETIME *pftModifiedTime, _Outptr_result_buffer_all_maybenull_(*pcbBlob) BYTE **ppbBlob, _Out_ DWORD *pcbBlob), pwszUrlName, dwType, pftExpireTime, pftAccessTime, pftModifiedTime, ppbBlob, pcbBlob)
FUNCTION_NAME(BOOLAPI, GetUrlCacheEntryInfoA, (_In_ LPCSTR lpszUrlName, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOA lpCacheEntryInfo, _Inout_opt_ LPDWORD lpcbCacheEntryInfo), lpszUrlName, lpCacheEntryInfo, lpcbCacheEntryInfo)
FUNCTION_NAME(BOOLAPI, GetUrlCacheEntryInfoExA, (_In_ LPCSTR lpszUrl, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOA lpCacheEntryInfo, _Inout_opt_ LPDWORD lpcbCacheEntryInfo, _Reserved_ LPSTR lpszRedirectUrl, _Reserved_ LPDWORD lpcbRedirectUrl, _Reserved_ LPVOID lpReserved, _In_ DWORD dwFlags), lpszUrl, lpCacheEntryInfo, lpcbCacheEntryInfo, lpszRedirectUrl, lpcbRedirectUrl, lpReserved, dwFlags)
FUNCTION_NAME(BOOLAPI, GetUrlCacheEntryInfoExW, (_In_ LPCWSTR lpszUrl, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOW lpCacheEntryInfo, _Inout_opt_ LPDWORD lpcbCacheEntryInfo, _Reserved_ LPWSTR lpszRedirectUrl, _Reserved_ LPDWORD lpcbRedirectUrl, _Reserved_ LPVOID lpReserved, _In_ DWORD dwFlags), lpszUrl, lpCacheEntryInfo, lpcbCacheEntryInfo, lpszRedirectUrl, lpcbRedirectUrl, lpReserved, dwFlags)
FUNCTION_NAME(BOOLAPI, GetUrlCacheEntryInfoW, (_In_ LPCWSTR lpszUrlName, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOW lpCacheEntryInfo, _Inout_opt_ LPDWORD lpcbCacheEntryInfo), lpszUrlName, lpCacheEntryInfo, lpcbCacheEntryInfo)
FUNCTION_NAME(BOOLAPI, GetUrlCacheGroupAttributeA, (_In_ GROUPID gid, _Reserved_ DWORD dwFlags, _In_ DWORD dwAttributes, _Out_writes_bytes_(*lpcbGroupInfo) LPINTERNET_CACHE_GROUP_INFOA lpGroupInfo, _Inout_ LPDWORD lpcbGroupInfo, _Reserved_ LPVOID lpReserved), gid, dwFlags, dwAttributes, lpGroupInfo, lpcbGroupInfo, lpReserved)
FUNCTION_NAME(BOOLAPI, GetUrlCacheGroupAttributeW, (_In_ GROUPID gid, _Reserved_ DWORD dwFlags, _In_ DWORD dwAttributes, _Out_writes_bytes_(*lpcbGroupInfo) LPINTERNET_CACHE_GROUP_INFOW lpGroupInfo, _Inout_ LPDWORD lpcbGroupInfo, _Reserved_ LPVOID lpReserved), gid, dwFlags, dwAttributes, lpGroupInfo, lpcbGroupInfo, lpReserved)
FUNCTION_NAME(BOOLAPI, GetUrlCacheHeaderData, (_In_ DWORD nIdx, _Out_ LPDWORD lpdwData), nIdx, lpdwData)
FUNCTION_NAME(BOOLAPI, GopherCreateLocatorA, (_In_ LPCSTR lpszHost, _In_ INTERNET_PORT nServerPort, _In_opt_ LPCSTR lpszDisplayString, _In_opt_ LPCSTR lpszSelectorString, _In_ DWORD dwGopherType, _Out_writes_opt_(*lpdwBufferLength) LPSTR lpszLocator, _Inout_ LPDWORD lpdwBufferLength), lpszHost, nServerPort, lpszDisplayString, lpszSelectorString, dwGopherType, lpszLocator, lpdwBufferLength)
FUNCTION_NAME(BOOLAPI, GopherCreateLocatorW, (_In_ LPCWSTR lpszHost, _In_ INTERNET_PORT nServerPort, _In_opt_ LPCWSTR lpszDisplayString, _In_opt_ LPCWSTR lpszSelectorString, _In_ DWORD dwGopherType, _Out_writes_opt_(*lpdwBufferLength) LPWSTR lpszLocator, _Inout_ LPDWORD lpdwBufferLength), lpszHost, nServerPort, lpszDisplayString, lpszSelectorString, dwGopherType, lpszLocator, lpdwBufferLength)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), GopherFindFirstFileA, (_In_ HINTERNET hConnect, _In_opt_ LPCSTR lpszLocator, _In_opt_ LPCSTR lpszSearchString, _Out_opt_ LPGOPHER_FIND_DATAA lpFindData, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszLocator, lpszSearchString, lpFindData, dwFlags, dwContext)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), GopherFindFirstFileW, (_In_ HINTERNET hConnect, _In_opt_ LPCWSTR lpszLocator, _In_opt_ LPCWSTR lpszSearchString, _Out_opt_ LPGOPHER_FIND_DATAW lpFindData, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszLocator, lpszSearchString, lpFindData, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, GopherGetAttributeA, (_In_ HINTERNET hConnect, _In_ LPCSTR lpszLocator, _In_opt_ LPCSTR lpszAttributeName, _At_((LPSTR)lpBuffer, _Out_writes_dwBufferLength) LPBYTE lpBuffer, _In_ DWORD dwBufferLength, _Out_ LPDWORD lpdwCharactersReturned, _In_opt_ GOPHER_ATTRIBUTE_ENUMERATOR lpfnEnumerator, _In_opt_ DWORD_PTR dwContext), hConnect, lpszLocator, lpszAttributeName, lpBuffer, dwBufferLength, lpdwCharactersReturned, lpfnEnumerator, dwContext)
FUNCTION_NAME(BOOLAPI, GopherGetAttributeW, (_In_ HINTERNET hConnect, _In_ LPCWSTR lpszLocator, _In_opt_ LPCWSTR lpszAttributeName, _At_((LPWSTR)lpBuffer, _Out_writes_dwBufferLength) LPBYTE lpBuffer, _In_ DWORD dwBufferLength, _Out_ LPDWORD lpdwCharactersReturned, _In_opt_ GOPHER_ATTRIBUTE_ENUMERATOR lpfnEnumerator, _In_opt_ DWORD_PTR dwContext), hConnect, lpszLocator, lpszAttributeName, lpBuffer, dwBufferLength, lpdwCharactersReturned, lpfnEnumerator, dwContext)
FUNCTION_NAME(BOOLAPI, GopherGetLocatorTypeA, (_In_ LPCSTR lpszLocator, _Out_ LPDWORD lpdwGopherType), lpszLocator, lpdwGopherType)
FUNCTION_NAME(BOOLAPI, GopherGetLocatorTypeW, (_In_ LPCWSTR lpszLocator, _Out_ LPDWORD lpdwGopherType), lpszLocator, lpdwGopherType)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), GopherOpenFileA, (_In_ HINTERNET hConnect, _In_ LPCSTR lpszLocator, _In_opt_ LPCSTR lpszView, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszLocator, lpszView, dwFlags, dwContext)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), GopherOpenFileW, (_In_ HINTERNET hConnect, _In_ LPCWSTR lpszLocator, _In_opt_ LPCWSTR lpszView, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hConnect, lpszLocator, lpszView, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, HttpAddRequestHeadersA, (_In_ HINTERNET hRequest, _When_(dwHeadersLength == (DWORD)-1, _In_z_) _When_(dwHeadersLength != (DWORD)-1, _In_reads_dwHeadersLength) LPCSTR lpszHeaders, _In_ DWORD dwHeadersLength, _In_ DWORD dwModifiers), hRequest, lpszHeaders, dwHeadersLength, dwModifiers)
FUNCTION_NAME(BOOLAPI, HttpAddRequestHeadersW, (_In_ HINTERNET hRequest, _When_(dwHeadersLength == (DWORD)-1, _In_z_) _When_(dwHeadersLength != (DWORD)-1, _In_reads_dwHeadersLength) LPCWSTR lpszHeaders, _In_ DWORD dwHeadersLength, _In_ DWORD dwModifiers), hRequest, lpszHeaders, dwHeadersLength, dwModifiers)
#undef HttpCheckDavCompliance
FUNCTION_NAME(BOOLAPI, HttpCheckDavCompliance, (_In_ LPCSTR lpszUrl, _In_ LPCSTR lpszComplianceToken, _Inout_ LPBOOL lpfFound, _In_ HWND hWnd, _In_ LPVOID lpvReserved), lpszUrl, lpszComplianceToken, lpfFound, hWnd, lpvReserved)
FUNCTION_NAME(INTERNETAPI_(VOID), HttpCloseDependencyHandle, (_In_ HTTP_DEPENDENCY_HANDLE hDependencyHandle), hDependencyHandle)
FUNCTION_NAME(INTERNETAPI_(DWORD), HttpDuplicateDependencyHandle, (_In_ HTTP_DEPENDENCY_HANDLE hDependencyHandle, _Outptr_ HTTP_DEPENDENCY_HANDLE *phDuplicatedDependencyHandle), hDependencyHandle, phDuplicatedDependencyHandle)
FUNCTION_NAME(BOOLAPI, HttpEndRequestA, (_In_ HINTERNET hRequest, _Out_opt_ LPINTERNET_BUFFERSA lpBuffersOut, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hRequest, lpBuffersOut, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, HttpEndRequestW, (_In_ HINTERNET hRequest, _Out_opt_ LPINTERNET_BUFFERSW lpBuffersOut, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hRequest, lpBuffersOut, dwFlags, dwContext)
FUNCTION_NAME(INTERNETAPI_(DWORD), HttpGetServerCredentials, (_In_ PWSTR pwszUrl, _Outptr_result_z_ PWSTR *ppwszUserName, _Outptr_result_z_ PWSTR *ppwszPassword), pwszUrl, ppwszUserName, ppwszPassword)
FUNCTION_NAME(INTERNETAPI_(DWORD), HttpGetTunnelSocket, (_In_ HINTERNET hRequest, _Out_ SOCKET *pSocket, _Outptr_result_buffer_all_maybenull_(*pdwDataLength) PBYTE *ppbData, _Out_ PDWORD pdwDataLength), hRequest, pSocket, ppbData, pdwDataLength)
FUNCTION_UNKNOWN_NAME(INTERNETAPI_(DWORD), HttpIsHostHstsEnabled, (_In_z_ PCWSTR pcwszUrl, _Out_ PBOOL pfIsHsts), pcwszUrl, pfIsHsts)
FUNCTION_NAME(INTERNETAPI_(DWORD), HttpOpenDependencyHandle, (_In_ HINTERNET hRequestHandle, _In_ BOOL fBackground, _Outptr_ HTTP_DEPENDENCY_HANDLE *phDependencyHandle), hRequestHandle, fBackground, phDependencyHandle)
//HttpOpenRequestA
//HttpOpenRequestW
FUNCTION_NAME(INTERNETAPI_(VOID), HttpPushClose, (_In_ HTTP_PUSH_WAIT_HANDLE hWait), hWait)
FUNCTION_NAME(INTERNETAPI_(DWORD), HttpPushEnable, (_In_ HINTERNET hRequest, _In_ HTTP_PUSH_TRANSPORT_SETTING *pTransportSetting, _Out_ HTTP_PUSH_WAIT_HANDLE *phWait), hRequest, pTransportSetting, phWait)
FUNCTION_NAME(INTERNETAPI_(DWORD), HttpPushWait, (_In_ HTTP_PUSH_WAIT_HANDLE hWait, _In_ HTTP_PUSH_WAIT_TYPE eType, _Out_opt_ HTTP_PUSH_NOTIFICATION_STATUS *pNotificationStatus), hWait, eType, pNotificationStatus)
FUNCTION_NAME(BOOLAPI, HttpQueryInfoA, (_In_ HINTERNET hRequest, _In_ DWORD dwInfoLevel, _Inout_updates_bytes_to_opt_(*lpdwBufferLength, *lpdwBufferLength) __out_data_source(NETWORK) LPVOID lpBuffer, _Inout_ LPDWORD lpdwBufferLength, _Inout_opt_ LPDWORD lpdwIndex), hRequest, dwInfoLevel, lpBuffer, lpdwBufferLength, lpdwIndex)
FUNCTION_NAME(BOOLAPI, HttpQueryInfoW, (_In_ HINTERNET hRequest, _In_ DWORD dwInfoLevel, _Inout_updates_bytes_to_opt_(*lpdwBufferLength, *lpdwBufferLength) __out_data_source(NETWORK) LPVOID lpBuffer, _Inout_ LPDWORD lpdwBufferLength, _Inout_opt_ LPDWORD lpdwIndex), hRequest, dwInfoLevel, lpBuffer, lpdwBufferLength, lpdwIndex)
//HttpSendRequestA
FUNCTION_NAME(BOOLAPI, HttpSendRequestExA, (_In_ HINTERNET hRequest, _In_opt_ LPINTERNET_BUFFERSA lpBuffersIn, _Out_opt_ LPINTERNET_BUFFERSA lpBuffersOut, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hRequest, lpBuffersIn, lpBuffersOut, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, HttpSendRequestExW, (_In_ HINTERNET hRequest, _In_opt_ LPINTERNET_BUFFERSW lpBuffersIn, _Out_opt_ LPINTERNET_BUFFERSW lpBuffersOut, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hRequest, lpBuffersIn, lpBuffersOut, dwFlags, dwContext)
//HttpSendRequestW
FUNCTION_NAME(BOOLAPI, HttpWebSocketClose, (_In_ HINTERNET hWebSocket, _In_ USHORT usStatus, _In_reads_bytes_opt_(dwReasonLength) PVOID pvReason, _In_range_(0, HTTP_WEB_SOCKET_MAX_CLOSE_REASON_LENGTH) DWORD dwReasonLength), hWebSocket, usStatus, pvReason, dwReasonLength)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), HttpWebSocketCompleteUpgrade, (_In_ HINTERNET hRequest, _In_ DWORD_PTR dwContext), hRequest, dwContext)
FUNCTION_NAME(BOOLAPI, HttpWebSocketQueryCloseStatus, (_In_ HINTERNET hWebSocket, _Out_ USHORT *pusStatus, _Out_writes_bytes_to_opt_(dwReasonLength, *pdwReasonLengthConsumed) PVOID pvReason, _In_range_(0, HTTP_WEB_SOCKET_MAX_CLOSE_REASON_LENGTH) DWORD dwReasonLength, _Out_range_(0, HTTP_WEB_SOCKET_MAX_CLOSE_REASON_LENGTH) DWORD *pdwReasonLengthConsumed), hWebSocket, pusStatus, pvReason, dwReasonLength, pdwReasonLengthConsumed)
FUNCTION_NAME(BOOLAPI, HttpWebSocketReceive, (_In_ HINTERNET hWebSocket, _Out_writes_bytes_to_(dwBufferLength, *pdwBytesRead) PVOID pvBuffer, _In_ DWORD dwBufferLength, _Out_range_(0, dwBufferLength) DWORD *pdwBytesRead, _Out_ HTTP_WEB_SOCKET_BUFFER_TYPE *pBufferType), hWebSocket, pvBuffer, dwBufferLength, pdwBytesRead, pBufferType)
FUNCTION_NAME(BOOLAPI, HttpWebSocketSend, (_In_ HINTERNET hWebSocket, _In_ HTTP_WEB_SOCKET_BUFFER_TYPE BufferType, _In_reads_bytes_opt_(dwBufferLength) PVOID pvBuffer, _In_ DWORD dwBufferLength), hWebSocket, BufferType, pvBuffer, dwBufferLength)
FUNCTION_NAME(BOOLAPI, HttpWebSocketShutdown, (_In_ HINTERNET hWebSocket, _In_ USHORT usStatus, _In_reads_bytes_opt_(dwReasonLength) PVOID pvReason, _In_range_(0, HTTP_WEB_SOCKET_MAX_CLOSE_REASON_LENGTH) DWORD dwReasonLength), hWebSocket, usStatus, pvReason, dwReasonLength)
FUNCTION_NAME(BOOLAPI, IncrementUrlCacheHeaderData, (_In_ DWORD nIdx, _Out_ LPDWORD lpdwData), nIdx, lpdwData)
FUNCTION_NAME(BOOLAPI, InternetAlgIdToStringA, (_In_ ALG_ID ai, _Out_writes_(*lpdwstrLength) LPSTR lpstr, _Inout_ LPDWORD lpdwstrLength, _Reserved_ DWORD dwReserved), ai, lpstr, lpdwstrLength, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetAlgIdToStringW, (_In_ ALG_ID ai, _Out_writes_(*lpdwstrLength) LPWSTR lpstr, _Inout_ LPDWORD lpdwstrLength, _Reserved_ DWORD dwReserved), ai, lpstr, lpdwstrLength, dwReserved)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetAttemptConnect, (_In_ DWORD dwReserved), dwReserved)
FUNCTION_NAME(BOOLAPI, InternetAutodial, (_In_ DWORD dwFlags, _In_opt_ HWND hwndParent), dwFlags, hwndParent)
FUNCTION_UNKNOWN_NAME(INTERNETAPI_(DWORD), InternetAutodialCallback, (DWORD arg0, DWORD arg1), arg0, arg1)
FUNCTION_NAME(BOOLAPI, InternetAutodialHangup, (_Reserved_ DWORD dwReserved), dwReserved)
FUNCTION_NAME(BOOLAPI, InternetCanonicalizeUrlA, (_In_ LPCSTR lpszUrl, _Out_writes_(*lpdwBufferLength) LPSTR lpszBuffer, _Inout_ LPDWORD lpdwBufferLength, _In_ DWORD dwFlags), lpszUrl, lpszBuffer, lpdwBufferLength, dwFlags)
FUNCTION_NAME(BOOLAPI, InternetCanonicalizeUrlW, (_In_ LPCWSTR lpszUrl, _Out_writes_(*lpdwBufferLength) LPWSTR lpszBuffer, _Inout_ LPDWORD lpdwBufferLength, _In_ DWORD dwFlags), lpszUrl, lpszBuffer, lpdwBufferLength, dwFlags)
FUNCTION_NAME(BOOLAPI, InternetCheckConnectionA, (_In_ LPCSTR lpszUrl, _In_ DWORD dwFlags, _In_ DWORD dwReserved), lpszUrl, dwFlags, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetCheckConnectionW, (_In_ LPCWSTR lpszUrl, _In_ DWORD dwFlags, _In_ DWORD dwReserved), lpszUrl, dwFlags, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetClearAllPerSiteCookieDecisions, (void), )
//InternetCloseHandle
FUNCTION_NAME(BOOLAPI, InternetCombineUrlA, (_In_ LPCSTR lpszBaseUrl, _In_ LPCSTR lpszRelativeUrl, _Out_writes_(*lpdwBufferLength) LPSTR lpszBuffer, _Inout_ LPDWORD lpdwBufferLength, _In_ DWORD dwFlags), lpszBaseUrl, lpszRelativeUrl, lpszBuffer, lpdwBufferLength, dwFlags)
FUNCTION_NAME(BOOLAPI, InternetCombineUrlW, (_In_ LPCWSTR lpszBaseUrl, _In_ LPCWSTR lpszRelativeUrl, _Out_writes_(*lpdwBufferLength) LPWSTR lpszBuffer, _Inout_ LPDWORD lpdwBufferLength, _In_ DWORD dwFlags), lpszBaseUrl, lpszRelativeUrl, lpszBuffer, lpdwBufferLength, dwFlags)
#undef InternetConfirmZoneCrossing
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetConfirmZoneCrossing, (_In_ HWND hWnd, _In_ LPSTR szUrlPrev, _In_ LPSTR szUrlNew, _In_ BOOL bPost), hWnd, szUrlPrev, szUrlNew, bPost)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetConfirmZoneCrossingA, (_In_ HWND hWnd, _In_ LPSTR szUrlPrev, _In_ LPSTR szUrlNew, _In_ BOOL bPost), hWnd, szUrlPrev, szUrlNew, bPost)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetConfirmZoneCrossingW, (_In_ HWND hWnd, _In_ LPWSTR szUrlPrev, _In_ LPWSTR szUrlNew, _In_ BOOL bPost), hWnd, szUrlPrev, szUrlNew, bPost)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), InternetConnectA, (_In_ HINTERNET hInternet, _In_ LPCSTR lpszServerName, _In_ INTERNET_PORT nServerPort, _In_opt_ LPCSTR lpszUserName, _In_opt_ LPCSTR lpszPassword, _In_ DWORD dwService, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hInternet, lpszServerName, nServerPort, lpszUserName, lpszPassword, dwService, dwFlags, dwContext)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), InternetConnectW, (_In_ HINTERNET hInternet, _In_ LPCWSTR lpszServerName, _In_ INTERNET_PORT nServerPort, _In_opt_ LPCWSTR lpszUserName, _In_opt_ LPCWSTR lpszPassword, _In_ DWORD dwService, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hInternet, lpszServerName, nServerPort, lpszUserName, lpszPassword, dwService, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, InternetCrackUrlA, (_In_reads_(dwUrlLength) LPCSTR lpszUrl, _In_ DWORD dwUrlLength, _In_ DWORD dwFlags, _Inout_ LPURL_COMPONENTSA lpUrlComponents), lpszUrl, dwUrlLength, dwFlags, lpUrlComponents)
FUNCTION_NAME(BOOLAPI, InternetCrackUrlW, (_In_reads_(dwUrlLength) LPCWSTR lpszUrl, _In_ DWORD dwUrlLength, _In_ DWORD dwFlags, _Inout_ LPURL_COMPONENTSW lpUrlComponents), lpszUrl, dwUrlLength, dwFlags, lpUrlComponents)
FUNCTION_NAME(BOOLAPI, InternetCreateUrlA, (_In_ LPURL_COMPONENTSA lpUrlComponents, _In_ DWORD dwFlags, _Out_writes_opt_(*lpdwUrlLength) LPSTR lpszUrl, _Inout_ LPDWORD lpdwUrlLength), lpUrlComponents, dwFlags, lpszUrl, lpdwUrlLength)
FUNCTION_NAME(BOOLAPI, InternetCreateUrlW, (_In_ LPURL_COMPONENTSW lpUrlComponents, _In_ DWORD dwFlags, _Out_writes_opt_(*lpdwUrlLength) LPWSTR lpszUrl, _Inout_ LPDWORD lpdwUrlLength), lpUrlComponents, dwFlags, lpszUrl, lpdwUrlLength)
#undef InternetDial
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetDial, (_In_ HWND hwndParent, _In_opt_ LPSTR lpszConnectoid, _In_ DWORD dwFlags, _Out_ DWORD_PTR *lpdwConnection, _Reserved_ DWORD dwReserved), hwndParent, lpszConnectoid, dwFlags, lpdwConnection, dwReserved)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetDialA, (_In_ HWND hwndParent, _In_opt_ LPSTR lpszConnectoid, _In_ DWORD dwFlags, _Out_ DWORD_PTR *lpdwConnection, _Reserved_ DWORD dwReserved), hwndParent, lpszConnectoid, dwFlags, lpdwConnection, dwReserved)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetDialW, (_In_ HWND hwndParent, _In_opt_ LPWSTR lpszConnectoid, _In_ DWORD dwFlags, _Out_ DWORD_PTR *lpdwConnection, _Reserved_ DWORD dwReserved), hwndParent, lpszConnectoid, dwFlags, lpdwConnection, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetEnumPerSiteCookieDecisionA, (_Out_writes_to_(*pcSiteNameSize, *pcSiteNameSize) LPSTR pszSiteName, _Inout_ unsigned long *pcSiteNameSize, _Out_ unsigned long *pdwDecision, _In_ unsigned long dwIndex), pszSiteName, pcSiteNameSize, pdwDecision, dwIndex)
FUNCTION_NAME(BOOLAPI, InternetEnumPerSiteCookieDecisionW, (_Out_writes_to_(*pcSiteNameSize, *pcSiteNameSize) LPWSTR pszSiteName, _Inout_ unsigned long *pcSiteNameSize, _Out_ unsigned long *pdwDecision, _In_ unsigned long dwIndex), pszSiteName, pcSiteNameSize, pdwDecision, dwIndex)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetErrorDlg, (_In_ HWND hWnd, _Inout_opt_ HINTERNET hRequest, _In_ DWORD dwError, _In_ DWORD dwFlags, _Inout_opt_ LPVOID * lppvData), hWnd, hRequest, dwError, dwFlags, lppvData)
FUNCTION_NAME(BOOLAPI, InternetFindNextFileA, (_In_ HINTERNET hFind, _Out_ LPVOID lpvFindData), hFind, lpvFindData)
FUNCTION_NAME(BOOLAPI, InternetFindNextFileW, (_In_ HINTERNET hFind, _Out_ LPVOID lpvFindData), hFind, lpvFindData)
FUNCTION_NAME(BOOLAPI, InternetFortezzaCommand, (_In_ DWORD dwCommand, _In_ HWND hwnd, _Reserved_ DWORD_PTR dwReserved), dwCommand, hwnd, dwReserved)
FUNCTION_NAME(INTERNETAPI_(VOID), InternetFreeCookies, (_Inout_opt_ INTERNET_COOKIE2 *pCookies, _In_ DWORD dwCookieCount), pCookies, dwCookieCount)
FUNCTION_NAME(INTERNETAPI_(VOID), InternetFreeProxyInfoList, (_Inout_ WININET_PROXY_INFO_LIST *pProxyInfoList), pProxyInfoList)
FUNCTION_UNKNOWN_NAME(BOOLAPI, InternetGetCertByURL, (_In_ LPSTR lpszURL, _Inout_updates_bytes_(dwcbCertText) LPSTR lpszCertText, _Inout_ DWORD dwcbCertText), lpszURL, lpszCertText, dwcbCertText)
FUNCTION_UNKNOWN_NAME(BOOLAPI, InternetGetCertByURLA, (_In_ LPSTR lpszURL, _Inout_updates_bytes_(dwcbCertText) LPSTR lpszCertText, _Inout_ DWORD dwcbCertText), lpszURL, lpszCertText, dwcbCertText)
FUNCTION_NAME(BOOLAPI, InternetGetConnectedState, (_Out_ LPDWORD lpdwFlags, _Reserved_ DWORD dwReserved), lpdwFlags, dwReserved)
#undef InternetGetConnectedStateEx
FUNCTION_NAME(BOOLAPI, InternetGetConnectedStateEx, (_Out_opt_ LPDWORD lpdwFlags, _Out_writes_opt_(cchNameLen) LPSTR lpszConnectionName, _In_ DWORD cchNameLen, _Reserved_ DWORD dwReserved), lpdwFlags, lpszConnectionName, cchNameLen, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetGetConnectedStateExA, (_Out_opt_ LPDWORD lpdwFlags, _Out_writes_opt_(cchNameLen) LPSTR lpszConnectionName, _In_ DWORD cchNameLen, _Reserved_ DWORD dwReserved), lpdwFlags, lpszConnectionName, cchNameLen, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetGetConnectedStateExW, (_Out_opt_ LPDWORD lpdwFlags, _Out_writes_opt_(cchNameLen) LPWSTR lpszConnectionName, _In_ DWORD cchNameLen, _Reserved_ DWORD dwReserved), lpdwFlags, lpszConnectionName, cchNameLen, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetGetCookieA, (_In_ LPCSTR lpszUrl, _In_opt_ LPCSTR lpszCookieName, _Out_writes_opt_(*lpdwSize) LPSTR lpszCookieData, _Inout_ LPDWORD lpdwSize), lpszUrl, lpszCookieName, lpszCookieData, lpdwSize)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetGetCookieEx2, (_In_ PCWSTR pcwszUrl, _In_opt_ PCWSTR pcwszCookieName, _In_ DWORD dwFlags, _Outptr_ INTERNET_COOKIE2 **ppCookies, _Out_ PDWORD pdwCookieCount), pcwszUrl, pcwszCookieName, dwFlags, ppCookies, pdwCookieCount)
FUNCTION_NAME(BOOLAPI, InternetGetCookieExA, (_In_ LPCSTR lpszUrl, _In_opt_ LPCSTR lpszCookieName, _In_reads_opt_(*lpdwSize) LPSTR lpszCookieData, _Inout_ LPDWORD lpdwSize, _In_ DWORD dwFlags, _Reserved_ LPVOID lpReserved), lpszUrl, lpszCookieName, lpszCookieData, lpdwSize, dwFlags, lpReserved)
FUNCTION_NAME(BOOLAPI, InternetGetCookieExW, (_In_ LPCWSTR lpszUrl, _In_opt_ LPCWSTR lpszCookieName, _In_reads_opt_(*lpdwSize) LPWSTR lpszCookieData, _Inout_ LPDWORD lpdwSize, _In_ DWORD dwFlags, _Reserved_ LPVOID lpReserved), lpszUrl, lpszCookieName, lpszCookieData, lpdwSize, dwFlags, lpReserved)
FUNCTION_NAME(BOOLAPI, InternetGetCookieW, (_In_ LPCWSTR lpszUrl, _In_opt_ LPCWSTR lpszCookieName, _Out_writes_opt_(*lpdwSize) LPWSTR lpszCookieData, _Inout_ LPDWORD lpdwSize), lpszUrl, lpszCookieName, lpszCookieData, lpdwSize)
FUNCTION_NAME(BOOLAPI, InternetGetLastResponseInfoA, (_Out_ LPDWORD lpdwError, _Out_writes_opt_(*lpdwBufferLength) LPSTR lpszBuffer, _Inout_ LPDWORD lpdwBufferLength), lpdwError, lpszBuffer, lpdwBufferLength)
FUNCTION_NAME(BOOLAPI, InternetGetLastResponseInfoW, (_Out_ LPDWORD lpdwError, _Out_writes_opt_(*lpdwBufferLength) LPWSTR lpszBuffer, _Inout_ LPDWORD lpdwBufferLength), lpdwError, lpszBuffer, lpdwBufferLength)
FUNCTION_NAME(BOOLAPI, InternetGetPerSiteCookieDecisionA, (_In_ LPCSTR pchHostName, _Out_ unsigned long* pResult), pchHostName, pResult)
FUNCTION_NAME(BOOLAPI, InternetGetPerSiteCookieDecisionW, (_In_ LPCWSTR pchHostName, _Out_ unsigned long* pResult), pchHostName, pResult)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetGetProxyForUrl, (_In_ HINTERNET hInternet, _In_ PCWSTR pcwszUrl, _Out_ WININET_PROXY_INFO_LIST *pProxyInfoList), hInternet, pcwszUrl, pProxyInfoList)
#undef InternetGetSecurityInfoByURL
FUNCTION_NAME(BOOLAPI, InternetGetSecurityInfoByURL, (_In_ LPSTR lpszURL, _Out_ PCCERT_CHAIN_CONTEXT * ppCertChain, _Out_ DWORD *pdwSecureFlags), lpszURL, ppCertChain, pdwSecureFlags)
FUNCTION_NAME(BOOLAPI, InternetGetSecurityInfoByURLA, (_In_ LPSTR lpszURL, _Out_ PCCERT_CHAIN_CONTEXT * ppCertChain, _Out_ DWORD *pdwSecureFlags), lpszURL, ppCertChain, pdwSecureFlags)
FUNCTION_NAME(BOOLAPI, InternetGetSecurityInfoByURLW, (_In_ LPCWSTR lpszURL, _Out_ PCCERT_CHAIN_CONTEXT * ppCertChain, _Out_ DWORD *pdwSecureFlags), lpszURL, ppCertChain, pdwSecureFlags)
#undef InternetGoOnline
FUNCTION_NAME(BOOLAPI, InternetGoOnline, (_In_opt_ LPCSTR lpszURL, _In_ HWND hwndParent, _In_ DWORD dwFlags), lpszURL, hwndParent, dwFlags)
FUNCTION_NAME(BOOLAPI, InternetGoOnlineA, (_In_opt_ LPCSTR lpszURL, _In_ HWND hwndParent, _In_ DWORD dwFlags), lpszURL, hwndParent, dwFlags)
FUNCTION_NAME(BOOLAPI, InternetGoOnlineW, (_In_opt_ LPCWSTR lpszURL, _In_ HWND hwndParent, _In_ DWORD dwFlags), lpszURL, hwndParent, dwFlags)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetHangUp, (_In_ DWORD_PTR dwConnection, _Reserved_ DWORD dwReserved), dwConnection, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetInitializeAutoProxyDll, (_In_ DWORD dwReserved), dwReserved)
FUNCTION_NAME(BOOLAPI, InternetLockRequestFile, (_In_ HINTERNET hInternet, _Out_ HANDLE * lphLockRequestInfo), hInternet, lphLockRequestInfo)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), InternetOpenA, (_In_opt_ LPCSTR lpszAgent, _In_ DWORD dwAccessType, _In_opt_ LPCSTR lpszProxy, _In_opt_ LPCSTR lpszProxyBypass, _In_ DWORD dwFlags), lpszAgent, dwAccessType, lpszProxy, lpszProxyBypass, dwFlags)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), InternetOpenUrlA, (_In_ HINTERNET hInternet, _In_ LPCSTR lpszUrl, _In_reads_opt_(dwHeadersLength) LPCSTR lpszHeaders, _In_ DWORD dwHeadersLength, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hInternet, lpszUrl, lpszHeaders, dwHeadersLength, dwFlags, dwContext)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), InternetOpenUrlW, (_In_ HINTERNET hInternet, _In_ LPCWSTR lpszUrl, _In_reads_opt_(dwHeadersLength) LPCWSTR lpszHeaders, _In_ DWORD dwHeadersLength, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hInternet, lpszUrl, lpszHeaders, dwHeadersLength, dwFlags, dwContext)
FUNCTION_NAME(INTERNETAPI_(HINTERNET), InternetOpenW, (_In_opt_ LPCWSTR lpszAgent, _In_ DWORD dwAccessType, _In_opt_ LPCWSTR lpszProxy, _In_opt_ LPCWSTR lpszProxyBypass, _In_ DWORD dwFlags), lpszAgent, dwAccessType, lpszProxy, lpszProxyBypass, dwFlags)
//InternetQueryDataAvailable
FUNCTION_NAME(BOOLAPI, InternetQueryFortezzaStatus, (_Out_ DWORD *pdwStatus, _Reserved_ DWORD_PTR dwReserved), pdwStatus, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetQueryOptionA, (_In_opt_ HINTERNET hInternet, _In_ DWORD dwOption, _Out_writes_bytes_to_opt_(*lpdwBufferLength, *lpdwBufferLength) __out_data_source(NETWORK) LPVOID lpBuffer, _Inout_ LPDWORD lpdwBufferLength), hInternet, dwOption, lpBuffer, lpdwBufferLength)
FUNCTION_NAME(BOOLAPI, InternetQueryOptionW, (_In_opt_ HINTERNET hInternet, _In_ DWORD dwOption, _Out_writes_bytes_to_opt_(*lpdwBufferLength, *lpdwBufferLength) __out_data_source(NETWORK) LPVOID lpBuffer, _Inout_ LPDWORD lpdwBufferLength), hInternet, dwOption, lpBuffer, lpdwBufferLength)
//InternetReadFile
FUNCTION_NAME(BOOLAPI, InternetReadFileExA, (_In_ HINTERNET hFile, _Out_ __out_data_source(NETWORK) LPINTERNET_BUFFERSA lpBuffersOut, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hFile, lpBuffersOut, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, InternetReadFileExW, (_In_ HINTERNET hFile, _Out_ __out_data_source(NETWORK) LPINTERNET_BUFFERSW lpBuffersOut, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hFile, lpBuffersOut, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, InternetSecurityProtocolToStringA, (_In_ DWORD dwProtocol, _Out_writes_to_opt_(*lpdwstrLength, *lpdwstrLength) LPSTR lpstr, _Inout_ LPDWORD lpdwstrLength, _Reserved_ DWORD dwReserved), dwProtocol, lpstr, lpdwstrLength, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetSecurityProtocolToStringW, (_In_ DWORD dwProtocol, _Out_writes_to_opt_(*lpdwstrLength, *lpdwstrLength) LPWSTR lpstr, _Inout_ LPDWORD lpdwstrLength, _Reserved_ DWORD dwReserved), dwProtocol, lpstr, lpdwstrLength, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetSetCookieA, (_In_ LPCSTR lpszUrl, _In_opt_ LPCSTR lpszCookieName, _In_ LPCSTR lpszCookieData), lpszUrl, lpszCookieName, lpszCookieData)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetSetCookieEx2, (_In_ PCWSTR pcwszUrl, _In_ const INTERNET_COOKIE2 *pCookie, _In_opt_ PCWSTR pcwszP3PPolicy, _In_ DWORD dwFlags, _Out_ PDWORD pdwCookieState), pcwszUrl, pCookie, pcwszP3PPolicy, dwFlags, pdwCookieState)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetSetCookieExA, (_In_ LPCSTR lpszUrl, _In_opt_ LPCSTR lpszCookieName, _In_ LPCSTR lpszCookieData, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwReserved), lpszUrl, lpszCookieName, lpszCookieData, dwFlags, dwReserved)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetSetCookieExW, (_In_ LPCWSTR lpszUrl, _In_opt_ LPCWSTR lpszCookieName, _In_ LPCWSTR lpszCookieData, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwReserved), lpszUrl, lpszCookieName, lpszCookieData, dwFlags, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetSetCookieW, (_In_ LPCWSTR lpszUrl, _In_opt_ LPCWSTR lpszCookieName, _In_ LPCWSTR lpszCookieData), lpszUrl, lpszCookieName, lpszCookieData)
#undef InternetSetDialState
FUNCTION_NAME(BOOLAPI, InternetSetDialState, (_In_opt_ LPCSTR lpszConnectoid, _In_ DWORD dwState, _Reserved_ DWORD dwReserved), lpszConnectoid, dwState, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetSetDialStateA, (_In_opt_ LPCSTR lpszConnectoid, _In_ DWORD dwState, _Reserved_ DWORD dwReserved), lpszConnectoid, dwState, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetSetDialStateW, (_In_opt_ LPCWSTR lpszConnectoid, _In_ DWORD dwState, _Reserved_ DWORD dwReserved), lpszConnectoid, dwState, dwReserved)
FUNCTION_NAME(INTERNETAPI_(DWORD), InternetSetFilePointer, (_In_ HINTERNET hFile, _In_ LONG lDistanceToMove, _Inout_opt_ PLONG lpDistanceToMoveHigh, _In_ DWORD dwMoveMethod, _Reserved_ DWORD_PTR dwContext), hFile, lDistanceToMove, lpDistanceToMoveHigh, dwMoveMethod, dwContext)
FUNCTION_NAME(BOOLAPI, InternetSetOptionA, (_In_opt_ HINTERNET hInternet, _In_ DWORD dwOption, _In_opt_ LPVOID lpBuffer, _In_ DWORD dwBufferLength), hInternet, dwOption, lpBuffer, dwBufferLength)
FUNCTION_NAME(BOOLAPI, InternetSetOptionExA, (_In_opt_ HINTERNET hInternet, _In_ DWORD dwOption, _In_opt_ LPVOID lpBuffer, _In_ DWORD dwBufferLength, _In_ DWORD dwFlags), hInternet, dwOption, lpBuffer, dwBufferLength, dwFlags)
FUNCTION_NAME(BOOLAPI, InternetSetOptionExW, (_In_opt_ HINTERNET hInternet, _In_ DWORD dwOption, _In_opt_ LPVOID lpBuffer, _In_ DWORD dwBufferLength, _In_ DWORD dwFlags), hInternet, dwOption, lpBuffer, dwBufferLength, dwFlags)
FUNCTION_NAME(BOOLAPI, InternetSetOptionW, (_In_opt_ HINTERNET hInternet, _In_ DWORD dwOption, _In_opt_ LPVOID lpBuffer, _In_ DWORD dwBufferLength), hInternet, dwOption, lpBuffer, dwBufferLength)
FUNCTION_NAME(BOOLAPI, InternetSetPerSiteCookieDecisionA, (_In_ LPCSTR pchHostName, _In_ DWORD dwDecision), pchHostName, dwDecision)
FUNCTION_NAME(BOOLAPI, InternetSetPerSiteCookieDecisionW, (_In_ LPCWSTR pchHostName, _In_ DWORD dwDecision), pchHostName, dwDecision)
#undef InternetSetStatusCallback
FUNCTION_NAME(INTERNETAPI_(INTERNET_STATUS_CALLBACK), InternetSetStatusCallback, (_In_ HINTERNET hInternet, _In_opt_ INTERNET_STATUS_CALLBACK lpfnInternetCallback), hInternet, lpfnInternetCallback)
FUNCTION_NAME(INTERNETAPI_(INTERNET_STATUS_CALLBACK), InternetSetStatusCallbackA, (_In_ HINTERNET hInternet, _In_opt_ INTERNET_STATUS_CALLBACK lpfnInternetCallback), hInternet, lpfnInternetCallback)
FUNCTION_NAME(INTERNETAPI_(INTERNET_STATUS_CALLBACK), InternetSetStatusCallbackW, (_In_ HINTERNET hInternet, _In_opt_ INTERNET_STATUS_CALLBACK lpfnInternetCallback), hInternet, lpfnInternetCallback)
#undef InternetShowSecurityInfoByURL
FUNCTION_NAME(BOOLAPI, InternetShowSecurityInfoByURL, (_In_ LPSTR lpszURL, _In_ HWND hwndParent), lpszURL, hwndParent)
FUNCTION_NAME(BOOLAPI, InternetShowSecurityInfoByURLA, (_In_ LPSTR lpszURL, _In_ HWND hwndParent), lpszURL, hwndParent)
FUNCTION_NAME(BOOLAPI, InternetShowSecurityInfoByURLW, (_In_ LPCWSTR lpszURL, _In_ HWND hwndParent), lpszURL, hwndParent)
#undef InternetTimeFromSystemTime
FUNCTION_NAME(BOOLAPI, InternetTimeFromSystemTime, (_In_ CONST SYSTEMTIME *pst, _In_ DWORD dwRFC, _Out_writes_bytes_(cbTime) LPSTR lpszTime, _In_ DWORD cbTime), pst, dwRFC, lpszTime, cbTime)
FUNCTION_NAME(BOOLAPI, InternetTimeFromSystemTimeA, (_In_ CONST SYSTEMTIME *pst, _In_ DWORD dwRFC, _Out_writes_bytes_(cbTime) LPSTR lpszTime, _In_ DWORD cbTime), pst, dwRFC, lpszTime, cbTime)
FUNCTION_NAME(BOOLAPI, InternetTimeFromSystemTimeW, (_In_ CONST SYSTEMTIME *pst, _In_ DWORD dwRFC, _Out_writes_bytes_(cbTime) LPWSTR lpszTime, _In_ DWORD cbTime), pst, dwRFC, lpszTime, cbTime)
#undef InternetTimeToSystemTime
FUNCTION_NAME(BOOLAPI, InternetTimeToSystemTime, (_In_ LPCSTR lpszTime, _Out_ SYSTEMTIME *pst, _Reserved_ DWORD dwReserved), lpszTime, pst, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetTimeToSystemTimeA, (_In_ LPCSTR lpszTime, _Out_ SYSTEMTIME *pst, _Reserved_ DWORD dwReserved), lpszTime, pst, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetTimeToSystemTimeW, (_In_ LPCWSTR lpszTime, _Out_ SYSTEMTIME *pst, _Reserved_ DWORD dwReserved), lpszTime, pst, dwReserved)
FUNCTION_NAME(BOOLAPI, InternetUnlockRequestFile, (_Inout_ HANDLE hLockRequestInfo), hLockRequestInfo)
FUNCTION_NAME(BOOLAPI, InternetWriteFile, (_In_ HINTERNET hFile, _In_reads_bytes_(dwNumberOfBytesToWrite) LPCVOID lpBuffer, _In_ DWORD dwNumberOfBytesToWrite, _Out_ LPDWORD lpdwNumberOfBytesWritten), hFile, lpBuffer, dwNumberOfBytesToWrite, lpdwNumberOfBytesWritten)
FUNCTION_NAME(BOOLAPI, InternetWriteFileExA, (_In_ HINTERNET hFile, _In_ LPINTERNET_BUFFERSA lpBuffersIn, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hFile, lpBuffersIn, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, InternetWriteFileExW, (_In_ HINTERNET hFile, _In_ LPINTERNET_BUFFERSW lpBuffersIn, _In_ DWORD dwFlags, _In_opt_ DWORD_PTR dwContext), hFile, lpBuffersIn, dwFlags, dwContext)
FUNCTION_NAME(BOOLAPI, IsHostInProxyBypassList, (_In_ INTERNET_SCHEME tScheme, _In_reads_(cchHost) LPCSTR lpszHost, _In_ DWORD cchHost), tScheme, lpszHost, cchHost)
FUNCTION_NAME(BOOLAPI, IsUrlCacheEntryExpiredA, (_In_ LPCSTR lpszUrlName, _In_ DWORD dwFlags, _Inout_ FILETIME* pftLastModified), lpszUrlName, dwFlags, pftLastModified)
FUNCTION_NAME(BOOLAPI, IsUrlCacheEntryExpiredW, (_In_ LPCWSTR lpszUrlName, _In_ DWORD dwFlags, _Inout_ FILETIME* pftLastModified), lpszUrlName, dwFlags, pftLastModified)
FUNCTION_NAME(BOOLAPI, LoadUrlCacheContent, (void), )
FUNCTION_NAME(INTERNETAPI_(DWORD), ParseX509EncodedCertificateForListBoxEntry, (_In_reads_bytes_(cbCert) LPBYTE lpCert, _In_ DWORD cbCert, _Out_writes_opt_(*lpdwListBoxEntry) LPSTR lpszListBoxEntry, _Inout_ LPDWORD lpdwListBoxEntry), lpCert, cbCert, lpszListBoxEntry, lpdwListBoxEntry)
FUNCTION_NAME(INTERNETAPI_(DWORD), PrivacyGetZonePreferenceW, (_In_ DWORD dwZone, _In_ DWORD dwType, _Out_opt_ LPDWORD pdwTemplate, _Out_writes_opt_(*pdwBufferLength) LPWSTR pszBuffer, _Inout_opt_ LPDWORD pdwBufferLength), dwZone, dwType, pdwTemplate, pszBuffer, pdwBufferLength)
FUNCTION_NAME(INTERNETAPI_(DWORD), PrivacySetZonePreferenceW, (_In_ DWORD dwZone, _In_ DWORD dwType, _In_ DWORD dwTemplate, _In_opt_ LPCWSTR pszPreference), dwZone, dwType, dwTemplate, pszPreference)
FUNCTION_NAME(BOOLAPI, ReadUrlCacheEntryStream, (_In_ HANDLE hUrlCacheStream, _In_ DWORD dwLocation, _Out_writes_bytes_(*lpdwLen) __out_data_source(NETWORK) LPVOID lpBuffer, _Inout_ LPDWORD lpdwLen, _Reserved_ DWORD Reserved), hUrlCacheStream, dwLocation, lpBuffer, lpdwLen, Reserved)
FUNCTION_NAME(BOOLAPI, ReadUrlCacheEntryStreamEx, (_In_ HANDLE hUrlCacheStream, _In_ DWORDLONG qwLocation, _Out_writes_bytes_(*lpdwLen) __out_data_source(NETWORK) LPVOID lpBuffer, _Inout_ LPDWORD lpdwLen), hUrlCacheStream, qwLocation, lpBuffer, lpdwLen)
FUNCTION_NAME(BOOLAPI, RegisterUrlCacheNotification, (_In_opt_ HWND hWnd, _In_ UINT uMsg, _In_ GROUPID gid, _In_ DWORD dwOpsFilter, _In_ DWORD dwReserved), hWnd, uMsg, gid, dwOpsFilter, dwReserved)
FUNCTION_NAME(BOOLAPI, ResumeSuspendedDownload, (_In_ HINTERNET hRequest, _In_ DWORD dwResultCode), hRequest, dwResultCode)
FUNCTION_NAME(BOOLAPI, RetrieveUrlCacheEntryFileA, (_In_ LPCSTR lpszUrlName, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOA lpCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo, _Reserved_ DWORD dwReserved), lpszUrlName, lpCacheEntryInfo, lpcbCacheEntryInfo, dwReserved)
FUNCTION_NAME(BOOLAPI, RetrieveUrlCacheEntryFileW, (_In_ LPCWSTR lpszUrlName, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOW lpCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo, _Reserved_ DWORD dwReserved), lpszUrlName, lpCacheEntryInfo, lpcbCacheEntryInfo, dwReserved)
FUNCTION_NAME(INTERNETAPI_(HANDLE), RetrieveUrlCacheEntryStreamA, (_In_ LPCSTR lpszUrlName, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOA lpCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo, _In_ BOOL fRandomRead, _Reserved_ DWORD dwReserved), lpszUrlName, lpCacheEntryInfo, lpcbCacheEntryInfo, fRandomRead, dwReserved)
FUNCTION_NAME(INTERNETAPI_(HANDLE), RetrieveUrlCacheEntryStreamW, (_In_ LPCWSTR lpszUrlName, _Inout_updates_bytes_opt_(*lpcbCacheEntryInfo) LPINTERNET_CACHE_ENTRY_INFOW lpCacheEntryInfo, _Inout_ LPDWORD lpcbCacheEntryInfo, _In_ BOOL fRandomRead, _Reserved_ DWORD dwReserved), lpszUrlName, lpCacheEntryInfo, lpcbCacheEntryInfo, fRandomRead, dwReserved)
FUNCTION_NAME(INTERNETAPI_(DWORD), RunOnceUrlCache, (_In_ HWND hwnd, _In_ HINSTANCE hinst, _In_ LPSTR lpszCmd, _In_ int nCmdShow), hwnd, hinst, lpszCmd, nCmdShow)
FUNCTION_NAME(BOOLAPI, SetUrlCacheConfigInfoA, (_In_ LPINTERNET_CACHE_CONFIG_INFOA lpCacheConfigInfo, _In_ DWORD dwFieldControl), lpCacheConfigInfo, dwFieldControl)
FUNCTION_NAME(BOOLAPI, SetUrlCacheConfigInfoW, (_In_ LPINTERNET_CACHE_CONFIG_INFOW lpCacheConfigInfo, _In_ DWORD dwFieldControl), lpCacheConfigInfo, dwFieldControl)
#undef SetUrlCacheEntryGroup
FUNCTION_NAME(BOOLAPI, SetUrlCacheEntryGroup, (_In_ LPCSTR lpszUrlName, _In_ DWORD dwFlags, _In_ GROUPID GroupId, _Reserved_ LPBYTE pbGroupAttributes, _Reserved_ DWORD cbGroupAttributes, _Reserved_ LPVOID lpReserved), lpszUrlName, dwFlags, GroupId, pbGroupAttributes, cbGroupAttributes, lpReserved)
FUNCTION_NAME(BOOLAPI, SetUrlCacheEntryGroupA, (_In_ LPCSTR lpszUrlName, _In_ DWORD dwFlags, _In_ GROUPID GroupId, _Reserved_ LPBYTE pbGroupAttributes, _Reserved_ DWORD cbGroupAttributes, _Reserved_ LPVOID lpReserved), lpszUrlName, dwFlags, GroupId, pbGroupAttributes, cbGroupAttributes, lpReserved)
FUNCTION_NAME(BOOLAPI, SetUrlCacheEntryGroupW, (_In_ LPCWSTR lpszUrlName, _In_ DWORD dwFlags, _In_ GROUPID GroupId, _Reserved_ LPBYTE pbGroupAttributes, _Reserved_ DWORD cbGroupAttributes, _Reserved_ LPVOID lpReserved), lpszUrlName, dwFlags, GroupId, pbGroupAttributes, cbGroupAttributes, lpReserved)
FUNCTION_NAME(BOOLAPI, SetUrlCacheEntryInfoA, (_In_ LPCSTR lpszUrlName, _In_ LPINTERNET_CACHE_ENTRY_INFOA lpCacheEntryInfo, _In_ DWORD dwFieldControl), lpszUrlName, lpCacheEntryInfo, dwFieldControl)
FUNCTION_NAME(BOOLAPI, SetUrlCacheEntryInfoW, (_In_ LPCWSTR lpszUrlName, _In_ LPINTERNET_CACHE_ENTRY_INFOW lpCacheEntryInfo, _In_ DWORD dwFieldControl), lpszUrlName, lpCacheEntryInfo, dwFieldControl)
FUNCTION_NAME(BOOLAPI, SetUrlCacheGroupAttributeA, (_In_ GROUPID gid, _Reserved_ DWORD dwFlags, _In_ DWORD dwAttributes, _In_ LPINTERNET_CACHE_GROUP_INFOA lpGroupInfo, _Reserved_ LPVOID lpReserved), gid, dwFlags, dwAttributes, lpGroupInfo, lpReserved)
FUNCTION_NAME(BOOLAPI, SetUrlCacheGroupAttributeW, (_In_ GROUPID gid, _Reserved_ DWORD dwFlags, _In_ DWORD dwAttributes, _In_ LPINTERNET_CACHE_GROUP_INFOW lpGroupInfo, _Reserved_ LPVOID lpReserved), gid, dwFlags, dwAttributes, lpGroupInfo, lpReserved)
FUNCTION_NAME(BOOLAPI, SetUrlCacheHeaderData, (_In_ DWORD nIdx, _In_ DWORD dwData), nIdx, dwData)
FUNCTION_UNKNOWN_NAME(INTERNETAPI_(void), ShowCertificate, (void), )
FUNCTION_NAME(INTERNETAPI_(DWORD), ShowClientAuthCerts, (_In_ HWND hWndParent), hWndParent)
FUNCTION_NAME(INTERNETAPI_(DWORD), ShowSecurityInfo, (_In_ HWND hWndParent, _In_ LPINTERNET_SECURITY_INFO pSecurityInfo), hWndParent, pSecurityInfo)
FUNCTION_NAME(INTERNETAPI_(DWORD), ShowX509EncodedCertificate, (_In_ HWND hWndParent, _In_reads_bytes_(cbCert) LPBYTE lpCert, _In_ DWORD cbCert), hWndParent, lpCert, cbCert)
#undef UnlockUrlCacheEntryFile
FUNCTION_NAME(BOOLAPI, UnlockUrlCacheEntryFile, (_In_ LPCSTR lpszUrlName, _Reserved_ DWORD dwReserved), lpszUrlName, dwReserved)
FUNCTION_NAME(BOOLAPI, UnlockUrlCacheEntryFileA, (_In_ LPCSTR lpszUrlName, _Reserved_ DWORD dwReserved), lpszUrlName, dwReserved)
FUNCTION_NAME(BOOLAPI, UnlockUrlCacheEntryFileW, (_In_ LPCWSTR lpszUrlName, _Reserved_ DWORD dwReserved), lpszUrlName, dwReserved)
FUNCTION_NAME(BOOLAPI, UnlockUrlCacheEntryStream, (_In_ HANDLE hUrlCacheStream, _Reserved_ DWORD Reserved), hUrlCacheStream, Reserved)
FUNCTION_NAME(BOOLAPI, UpdateUrlCacheContentPath, (_In_ LPCSTR szNewPath), szNewPath)
FUNCTION_NAME(URLCACHEAPI, UrlCacheCheckEntriesExist, (_In_reads_(cEntries) PCWSTR *rgpwszUrls, _In_ DWORD cEntries, _Out_writes_(cEntries) BOOL *rgfExist), rgpwszUrls, cEntries, rgfExist)
FUNCTION_NAME(URLCACHEAPI_(VOID), UrlCacheCloseEntryHandle, (_In_ URLCACHE_HANDLE hEntryFile), hEntryFile)
FUNCTION_NAME(URLCACHEAPI, UrlCacheContainerSetEntryMaximumAge, (_In_z_ const WCHAR *pwszPrefix, _In_ DWORD dwEntryMaxAge), pwszPrefix, dwEntryMaxAge)
FUNCTION_NAME(URLCACHEAPI, UrlCacheCreateContainer, (_In_z_ const WCHAR *pwszName, _In_z_ const WCHAR *pwszPrefix, _In_z_ const WCHAR *pwszDirectory, _In_ ULONGLONG ullLimit, _In_ DWORD dwOptions), pwszName, pwszPrefix, pwszDirectory, ullLimit, dwOptions)
FUNCTION_NAME(URLCACHEAPI, UrlCacheFindFirstEntry, (_In_opt_z_ const WCHAR *pwszPrefix, _In_ DWORD dwFlags, _In_ DWORD dwFilter, _In_ GROUPID GroupId, _Out_ PURLCACHE_ENTRY_INFO pCacheEntryInfo, _Out_ HANDLE *phFind), pwszPrefix, dwFlags, dwFilter, GroupId, pCacheEntryInfo, phFind)
FUNCTION_NAME(URLCACHEAPI, UrlCacheFindNextEntry, (_In_ HANDLE hFind, _Out_ PURLCACHE_ENTRY_INFO pCacheEntryInfo), hFind, pCacheEntryInfo)
FUNCTION_NAME(URLCACHEAPI_(VOID), UrlCacheFreeEntryInfo, (_Inout_ PURLCACHE_ENTRY_INFO pCacheEntryInfo), pCacheEntryInfo)
FUNCTION_UNKNOWN_NAME(DWORD, UrlCacheFreeGlobalSpace, (_In_ ULONGLONG ullTargetSize, _In_ DWORD dwFilter), ullTargetSize, dwFilter)
FUNCTION_NAME(URLCACHEAPI, UrlCacheGetContentPaths, (_Outptr_result_buffer_(*pcDirectories) PWSTR **pppwszDirectories, _Out_ DWORD *pcDirectories), pppwszDirectories, pcDirectories)
FUNCTION_NAME(URLCACHEAPI, UrlCacheGetEntryInfo, (_In_opt_ APP_CACHE_HANDLE hAppCache, _In_ PCWSTR pcwszUrl, _Out_opt_ PURLCACHE_ENTRY_INFO pCacheEntryInfo), hAppCache, pcwszUrl, pCacheEntryInfo)
FUNCTION_UNKNOWN_NAME(DWORD, UrlCacheGetGlobalCacheSize, (_In_ DWORD dwFilter, _Out_ PULONGLONG pullSize, _Out_ PULONGLONG pullLimit), dwFilter, pullSize, pullLimit)
FUNCTION_NAME(URLCACHEAPI, UrlCacheGetGlobalLimit, (_In_ URL_CACHE_LIMIT_TYPE limitType, _Out_ ULONGLONG *pullLimit), limitType, pullLimit)
FUNCTION_NAME(URLCACHEAPI, UrlCacheReadEntryStream, (_In_ URLCACHE_HANDLE hUrlCacheStream, _In_ ULONGLONG ullLocation, _Inout_ PVOID pBuffer, _In_ DWORD dwBufferLen, _Out_ PDWORD pdwBufferLen), hUrlCacheStream, ullLocation, pBuffer, dwBufferLen, pdwBufferLen)
FUNCTION_NAME(URLCACHEAPI, UrlCacheReloadSettings, (void), )
FUNCTION_NAME(URLCACHEAPI, UrlCacheRetrieveEntryFile, (_In_opt_ APP_CACHE_HANDLE hAppCache, _In_ PCWSTR pcwszUrl, _Out_ PURLCACHE_ENTRY_INFO pCacheEntryInfo, _Out_ URLCACHE_HANDLE *phEntryFile), hAppCache, pcwszUrl, pCacheEntryInfo, phEntryFile)
FUNCTION_NAME(URLCACHEAPI, UrlCacheRetrieveEntryStream, (_In_opt_ APP_CACHE_HANDLE hAppCache, _In_ PCWSTR pcwszUrl, _In_ BOOL fRandomRead, _Out_ PURLCACHE_ENTRY_INFO pCacheEntryInfo, _Out_ URLCACHE_HANDLE *phEntryStream), hAppCache, pcwszUrl, fRandomRead, pCacheEntryInfo, phEntryStream)
FUNCTION_UNKNOWN_NAME(INTERNETAPI_(DWORD), UrlCacheServer, (void), )
FUNCTION_NAME(URLCACHEAPI, UrlCacheSetGlobalLimit, (_In_ URL_CACHE_LIMIT_TYPE limitType, _In_ ULONGLONG ullLimit), limitType, ullLimit)
FUNCTION_NAME(URLCACHEAPI, UrlCacheUpdateEntryExtraData, (_In_opt_ APP_CACHE_HANDLE hAppCache, _In_ PCWSTR pcwszUrl, _In_reads_bytes_(cbExtraData) const BYTE *pbExtraData, _In_ DWORD cbExtraData), hAppCache, pcwszUrl, pbExtraData, cbExtraData)
FUNCTION_UNKNOWN_NAME(INTERNETAPI_(DWORD), UrlZonesDetach, (void), )
#ifdef _M_IX86 /* at x86, first '_' is removed. so add extra '_'. */
#define _GetFileExtensionFromUrl __GetFileExtensionFromUrl
#endif
FUNCTION_NAME(INTERNETAPI_(DWORD), _GetFileExtensionFromUrl, (_In_ LPSTR lpszUrl, _In_ DWORD dwFlags, _Inout_updates_bytes_(*pcchExt) LPSTR lpszExt, _Inout_ DWORD *pcchExt), lpszUrl, dwFlags, lpszExt, pcchExt)
FUNCTION_NONAME(101, BOOL, DoConnectoidsExist, (void), )
FUNCTION_NONAME(102, BOOLAPI, GetDiskInfoA, (_In_ PCSTR pszPath, _Out_opt_ PDWORD pdwClusterSize, _Out_opt_ PDWORDLONG pdlAvail, _Out_opt_ PDWORDLONG pdlTotal), pszPath, pdwClusterSize, pdlAvail, pdlTotal)
FUNCTION_NONAME(103, BOOL, PerformOperationOverUrlCacheA, (_In_opt_ PCSTR pszUrlSearchPattern, _In_ DWORD dwFlags, _In_ DWORD dwFilter, _In_ GROUPID GroupId, _Reserved_ PVOID pReserved1, _Reserved_ PDWORD pdwReserved2, _Reserved_ PVOID pReserved3, _In_ CACHE_OPERATOR op, _Inout_ PVOID pOperatorData), pszUrlSearchPattern, dwFlags, dwFilter, GroupId, pReserved1, pdwReserved2, pReserved3, op, pOperatorData)
FUNCTION_NONAME(104, BOOLAPI, HttpCheckDavComplianceA, (_In_ LPCSTR lpszUrl, _In_ LPCSTR lpszComplianceToken, _Inout_ LPBOOL lpfFound, _In_ HWND hWnd, _In_ LPVOID lpvReserved), lpszUrl, lpszComplianceToken, lpfFound, hWnd, lpvReserved)
FUNCTION_NONAME(105, BOOLAPI, HttpCheckDavComplianceW, (_In_ LPCWSTR lpszUrl, _In_ LPCWSTR lpszComplianceToken, _Inout_ LPBOOL lpfFound, _In_ HWND hWnd, _In_ LPVOID lpvReserved), lpszUrl, lpszComplianceToken, lpfFound, hWnd, lpvReserved)
FUNCTION_NONAME(108, BOOLAPI, ImportCookieFileA, (_In_ LPCSTR szFilename), szFilename)
FUNCTION_NONAME(109, BOOLAPI, ExportCookieFileA, (_In_ LPCSTR szFilename, _In_ BOOL fAppend), szFilename, fAppend)
FUNCTION_NONAME(110, BOOLAPI, ImportCookieFileW, (_In_ LPCWSTR szFilename), szFilename)
FUNCTION_NONAME(111, BOOLAPI, ExportCookieFileW, (_In_ LPCWSTR szFilename, _In_ BOOL fAppend), szFilename, fAppend)
FUNCTION_UNKNOWN_NONAME(112, INTERNETAPI_(int), CHttp2Stream_ConnLimitExempted, (void), )
FUNCTION_NONAME(116, BOOLAPI, IsDomainLegalCookieDomainA, (_In_ LPCSTR pchDomain, _In_ LPCSTR pchFullDomain), pchDomain, pchFullDomain)
FUNCTION_NONAME(117, BOOLAPI, IsDomainLegalCookieDomainW, (_In_ LPCWSTR pchDomain, _In_ LPCWSTR pchFullDomain), pchDomain, pchFullDomain)
FUNCTION_NONAME(118, INTERNETAPI_(int), FindP3PPolicySymbol, (_In_ const char *pszSymbol), pszSymbol)
FUNCTION_UNKNOWN_NONAME(120, INTERNETAPI_(void), MapResourceToPolicy, (void), )
FUNCTION_UNKNOWN_NONAME(121, INTERNETAPI_(void), GetP3PPolicy, (void), )
FUNCTION_UNKNOWN_NONAME(122, INTERNETAPI_(void), FreeP3PObject, (void), )
FUNCTION_UNKNOWN_NONAME(123, INTERNETAPI_(void), GetP3PRequestStatus, (void), )
FUNCTION_NONAME(346, INTERNETAPI_(DWORD), InternalInternetGetCookie, (_In_ LPCSTR lpszUrl, _Out_writes_(*lpdwDataSize) LPSTR lpszCookieData, _Inout_ DWORD *lpdwDataSize), lpszUrl, lpszCookieData, lpdwDataSize)
FUNCTION_NONAME(401, BOOLAPI, ReadGuidsForConnectedNetworks, (_Out_opt_ DWORD *pcNetworks, _Out_opt_ PWSTR **pppwszNetworkGuids, _Out_opt_ BSTR **pppbstrNetworkNames, _Out_opt_ PWSTR **pppwszGWMacs, _Out_opt_ DWORD *pcGatewayMacs, _Out_opt_ DWORD *pdwFlags), pcNetworks, pppwszNetworkGuids, pppbstrNetworkNames, pppwszGWMacs, pcGatewayMacs, pdwFlags)
FUNCTION_UNKNOWN_NONAME(402, INTERNETAPI_(DWORD), InternetAutoProxyGetProxyForUrl, (void), )
FUNCTION_UNKNOWN_NONAME(403, INTERNETAPI_(DWORD), InternetAutoProxyOnSendRequestComplete, (void), )
FUNCTION_UNKNOWN_NONAME(410, INTERNETAPI_(DWORD), GetCacheServerConnection, (void), )
FUNCTION_UNKNOWN_NONAME(411, INTERNETAPI_(DWORD), CreateCacheServerRpcBinding, (void), )
FUNCTION_UNKNOWN_NONAME(413, INTERNETAPI_(DWORD), SetGlobalJetParameters, (void), )
FUNCTION_NONAME(420, INTERNETAPI_(DWORD), IsLanConnection, (DWORD arg0), arg0)
FUNCTION_UNKNOWN_NONAME(421, INTERNETAPI_(DWORD), IsDialUpConnection, (void), )
FUNCTION_UNKNOWN_NONAME(422, INTERNETAPI_(DWORD), RegisterForNetworkChangeNotification, (void), )
FUNCTION_UNKNOWN_NONAME(423, INTERNETAPI_(DWORD), UnRegisterNetworkChangeNotification, (void), )
#pragma endregion
