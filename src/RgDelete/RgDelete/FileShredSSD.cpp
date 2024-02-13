#include "stdafx.h"
#include <ShellAPI.h>

#include "DiskUtil.h"
#include "FileShredSSD.h"
#include <VersionHelpers.h>

bool FileShredSSD::DoErasingSSD(__in LPWSTR path, __inout BOOL *triedTrimFlag)
{
	//This SSD erase doesn't support below WIN7
	if (IsWin7OrLater())
	{
		if (IsDrvSSD(path))
		{
			DoEnableTrim(triedTrimFlag);
			SetFileZero(path);
			return TRUE;
		}
	}

	return FALSE;
}

bool FileShredSSD::IsDrvSSD(__in LPWSTR path)
{
	TCHAR PhysicalDriveName[MAX_PATH] = { 0 };
	vector<int> num = DiskUtil::GetExtentsFromPath(path);

	wsprintf(PhysicalDriveName, _T("\\\\.\\PhysicalDrive%d"), num[0]);

	return (DiskUtil::HasNoSeekPenalty(PhysicalDriveName) == S_OK) ? TRUE : FALSE;
}

bool FileShredSSD::DoEnableTrim(__inout BOOL *triedTrimFlag)
{
	//Do this action only one time for the action by check the flag.
	if (*triedTrimFlag == TRUE)
		return FALSE;

	if (!IsTrimEnabled())
	{
		if (IDYES == MessageBox(g_hMain, LD_SSD_TRIM_EANBLE_MSG, LD_MSG_BOX_TITLE, MB_YESNO))
		{
			if (SetTrimEnable())
				MessageBox(g_hMain, LD_SSD_TRIM_SUCCESSFUL_MSG, LD_MSG_BOX_TITLE, MB_OK);
			else
				MessageBox(g_hMain, LD_SSD_TRIM_FAILED_MSG, LD_MSG_BOX_TITLE, MB_OK);
		}
	}
	*triedTrimFlag = TRUE;

	return TRUE;
}

bool FileShredSSD::IsTrimEnabled()
{
	TCHAR szParam[1024] = { 0 };
	TCHAR szTemp[MAX_PATH] = { 0 };
	TCHAR szOuput[MAX_PATH] = { 0 };
	TCHAR szGuid[40] = { 0 };
	GUID guid = { 0 };

	CoCreateGuid(&guid);
	wsprintf(szGuid, _T("{%08X-%04X-%04X-%02X%02X-%02X%02X%02X%02X%02X%02X}")
		, guid.Data1, guid.Data2, guid.Data3, guid.Data4[0], guid.Data4[1]
		, guid.Data4[2], guid.Data4[3], guid.Data4[4], guid.Data4[5]
		, guid.Data4[6], guid.Data4[7]);

	GetTempPath(MAX_PATH, szTemp);

	//Set file to ouput
	wsprintf(szOuput, _T("%s%s.tmp"), szTemp, szGuid);
	wsprintf(szParam, _T("/C fsutil behavior query disabledeletenotify > %s"), szOuput);

	//scheduled delete
	SHELLEXECUTEINFO ShExecInfo = { 0 };
	ShExecInfo.cbSize = sizeof(SHELLEXECUTEINFO);
	ShExecInfo.fMask = SEE_MASK_NOCLOSEPROCESS;
	ShExecInfo.hwnd = NULL;
	ShExecInfo.lpVerb = NULL;
	ShExecInfo.lpFile = _T("cmd");
	ShExecInfo.lpParameters = szParam;
	ShExecInfo.lpDirectory = NULL;
	ShExecInfo.nShow = SW_HIDE;
	ShExecInfo.hInstApp = NULL;
	ShellExecuteEx(&ShExecInfo);

	// Wait until child process exits.
	WaitForSingleObject(ShExecInfo.hProcess, INFINITE);

	//read file to get trim option
	FILE * hFile = NULL;
	CHAR optionName[MAX_PATH] = { 0 };
	INT optionValue = 0;

	hFile = _tfopen(szOuput, L"r");
	if (hFile == NULL)
		return FALSE;

	fscanf(hFile, "%s = %d\n", optionName, &optionValue);

	//delete file
	fclose(hFile);
	DeleteFile(szOuput);

	//CString debugMsg;
	//debugMsg.Format(L"name %s, value %d", optionName, optionValue);
	//MessageBox(g_hMain, debugMsg, LD_MSG_BOX_TITLE, MB_YESNO);

	//DisableDeleteNotify = 0 : Trim enabled,  DisableDeleteNotify = 1 : trim disalbed
	return (optionValue == 0) ? TRUE : FALSE;
}

bool FileShredSSD::SetTrimEnable()
{
	TCHAR szParam[1024] = { 0 };
	wsprintf(szParam, _T("behavior set disabledeletenotify 0"), szParam);

	SHELLEXECUTEINFO ShExecInfo = { 0 };
	ShExecInfo.cbSize = sizeof(SHELLEXECUTEINFO);
	ShExecInfo.fMask = SEE_MASK_NOCLOSEPROCESS;
	ShExecInfo.hwnd = NULL;
	ShExecInfo.lpVerb = _T("runas");
	ShExecInfo.lpFile = _T("fsutil");
	ShExecInfo.lpParameters = szParam;
	ShExecInfo.lpDirectory = NULL;
	ShExecInfo.nShow = SW_HIDE;
	ShExecInfo.hInstApp = NULL;
	ShellExecuteEx(&ShExecInfo);

	WaitForSingleObject(ShExecInfo.hProcess, INFINITE);

	return IsTrimEnabled() ? TRUE : FALSE;
}

bool FileShredSSD::SetFileZero(TCHAR filename[])
{
	TCHAR szParam[1024] = { 0 };
	LARGE_INTEGER liFileSize;

	// Overwrite file
	HANDLE hFile = CreateFile(filename, GENERIC_READ | GENERIC_WRITE, 0, NULL, OPEN_EXISTING, FILE_FLAG_WRITE_THROUGH, NULL);
	if (hFile == INVALID_HANDLE_VALUE)
		return FALSE;

	//get file size and close handle
	GetFileSizeEx(hFile, &liFileSize);
	CloseHandle(hFile);

	wsprintf(szParam, _T("file setzerodata offset=\"0\" length=\"%I64d\" \"%s\""), liFileSize.QuadPart, filename);

	//Set command and parameters
	SHELLEXECUTEINFO ShExecInfo = { 0 };
	ShExecInfo.cbSize = sizeof(SHELLEXECUTEINFO);
	ShExecInfo.fMask = SEE_MASK_NOCLOSEPROCESS;
	ShExecInfo.hwnd = NULL;
	ShExecInfo.lpVerb = NULL;
	ShExecInfo.lpFile = _T("fsutil");
	ShExecInfo.lpParameters = szParam;
	ShExecInfo.lpDirectory = NULL;
	ShExecInfo.nShow = SW_HIDE;
	ShExecInfo.hInstApp = NULL;
	ShellExecuteEx(&ShExecInfo);

	// Wait until child process exits.
	WaitForSingleObject(ShExecInfo.hProcess, INFINITE);

	return TRUE;
}

bool FileShredSSD::IsWin7OrLater()
{
	return IsWindowsVersionOrGreater(6, 1, 0);
}

bool FileShredSSD::IsWindowsVersionOrGreater(WORD wMajorVersion, WORD wMinorVersion, WORD wServicePackMajor)
{
	OSVERSIONINFOEXW osvi = { sizeof(osvi), 0, 0, 0, 0, { 0 }, 0, 0 };
	DWORDLONG const dwlConditionMask = VerSetConditionMask(
		VerSetConditionMask(
		VerSetConditionMask(
		0, VER_MAJORVERSION, VER_GREATER_EQUAL),
		VER_MINORVERSION, VER_GREATER_EQUAL),
		VER_SERVICEPACKMAJOR, VER_GREATER_EQUAL);

	osvi.dwMajorVersion = wMajorVersion;
	osvi.dwMinorVersion = wMinorVersion;
	osvi.wServicePackMajor = wServicePackMajor;

	return VerifyVersionInfoW(&osvi, VER_MAJORVERSION |
		VER_MINORVERSION | VER_SERVICEPACKMAJOR, dwlConditionMask) != FALSE;
}
