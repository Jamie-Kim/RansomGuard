#include "stdafx.h"
#include <ShlObj.h>
#include <ShellAPI.h>
#include <winreg.h>  

#include "RgDelete.h"
#include "FileShred.h"
#include "FileShredEx.h"
#include "FileShredSSD.h"
#include "RgDeleteCmd.h"


int RgDeleteCmd::CmdActions()
{
	LPWSTR path = NULL, time = NULL, date = NULL, regular = NULL, del_method = NULL, security = NULL;
	LPWSTR trashBin, taskName = NULL, dirErase = NULL, delTasks = NULL;
	LPWSTR default_dir_erase = NULL, default_time = NULL, default_method = NULL;
	LPWSTR *szArgList = NULL;
	LPWSTR th_day = NULL;	//trash bin day


	int argCount = 0;
	bool bDirErase = TRUE;
	TCHAR programDataPath[MAX_PATH] = { 0 };

	int de = 0;
	int t = 0;
	int em = 0;

	//get commpand parsing
	szArgList = CommandLineToArgvW(GetCommandLine(), &argCount);
	if (szArgList == NULL)
	{
		MessageBox(NULL, LD_MSG_BOX_ERROR_PARAMS, LD_MSG_BOX_TITLE, MB_OK);
		return -1;
	}

	//get param's values
	path = GetArg(szArgList, _T("/p"), argCount);  //path
	del_method = GetArg(szArgList, _T("/m"), argCount); //erase method
	date = GetArg(szArgList, _T("/d"), argCount); //date
	time = GetArg(szArgList, _T("/t"), argCount); //time
	regular = GetArg(szArgList, _T("/r"), argCount); //regular erase
	security = GetArg(szArgList, _T("/s"), argCount); //security key
	taskName = GetArg(szArgList, _T("/tn"), argCount); //task name
	trashBin = GetArg(szArgList, _T("/th"), argCount); //trash bin erase
	delTasks = GetArg(szArgList, _T("/dt"), argCount); //delete Tasks
	th_day = GetArg(szArgList, _T("/td"), argCount); //trash bin day

	if (security == NULL || _tcscmp(_T("RgDelete"), security))
	{
		MessageBox(NULL, LD_MSG_BOX_ERROR_START, LD_MSG_BOX_TITLE, MB_OK);
		return -1;
	}

	//do actions
	if (delTasks != NULL)
	{
		if (trashBin != NULL)
		{
			DeletTbAllTasks();
		}
		else
		{
			DeletAllTasks();
		}
	}
	else if (path != NULL && del_method != NULL)
	{
		//Scheduled erase including regular deletion
		if (time != NULL && taskName == NULL)
			FileShredEx::DoScheDelete(path, del_method, date, NULL, time, dirErase, regular, FALSE);
		//Direct Delete for Regular sche erasae 
		else if (date != NULL && time != NULL && taskName != NULL)
			FileShredEx::DoDirectDeleteRegular(path, del_method, taskName, regular, date, time, dirErase, bDirErase);
		//Direct Delete
		else if (date == NULL && time == NULL && regular == NULL && trashBin == NULL)
			FileShredEx::DoDirectDelete(path, del_method, taskName, bDirErase);
		//Direct Delete For Trash Bin
		else if (date == NULL && time == NULL && regular == NULL && trashBin != NULL)
			FileShredEx::DoDirectDelete(path, del_method, taskName, bDirErase);

		//don't show UI
		return -1;
	}
	else if (trashBin != NULL && del_method != NULL)
	{
		//Scheduled erase for trash bin
		if (time != NULL)
			FileShredEx::DoDirectDeleteTbRegular(del_method, taskName, regular, time, th_day);
		else
			FileShredEx::DoTrashBinDelete(del_method, th_day);

		//don't show UI
		return -1;
	}

	//UI option init, if reg data to init values were not found , then use the pram data
	BOOL isRegValue = RegistryGetOptions(&g_defaultSettings[DEFAULT_DIR_ERASE]
		, &g_defaultSettings[DEFAULT_TIME_METHORD]
		, &g_defaultSettings[DEFAULT_WRITE_METHORD]);

	if (!isRegValue){
		//parse default settings to run UI
		if (default_dir_erase != NULL)
			g_defaultSettings[DEFAULT_DIR_ERASE] = _ttoi(default_dir_erase);

		if (default_time != NULL)
			g_defaultSettings[DEFAULT_TIME_METHORD] = _ttoi(default_time);

		if (default_method != NULL)
			g_defaultSettings[DEFAULT_WRITE_METHORD] = _ttoi(default_method);
	}

	//free parameter data
	LocalFree(szArgList);

	//show UI dialog
	return 1;
}

LPWSTR RgDeleteCmd::GetArg(LPWSTR* szArgList, LPWSTR opt, int argCnt)
{
	for (int i = 0; i < argCnt; i++)
	{
		if (!_tcscmp(szArgList[i], opt))
		{
			if ((szArgList[i + 1] != NULL) && (szArgList[i + 1][0] != _T('/'))){
				return szArgList[i + 1];
			}
		}
	}

	return NULL;
}

BOOL RgDeleteCmd::RegistrySaveOptions(__in int dirErase, __in  int time, __in int eraseMethod)
{
	LONG error = 0;
	HKEY hKey;
	DWORD dwType = REG_DWORD;
	DWORD dwSize = sizeof(int);
	DWORD dwDisp = 0;

	//open and create the key
	error = RegOpenKeyEx(HKEY_CURRENT_USER, _T("Software\\Orient Computer\\RgDelete\\UI_Options"), 0, KEY_ALL_ACCESS, &hKey);
	if (error != ERROR_SUCCESS)
	{
		error = RegCreateKeyEx(HKEY_CURRENT_USER, _T("Software\\Orient Computer\\RgDelete\\UI_Options"), 0
			, _T("REG_BINARY"), REG_OPTION_NON_VOLATILE, KEY_ALL_ACCESS, 0, &hKey, &dwDisp);
		if (error != ERROR_SUCCESS)
			return FALSE;
	}

	//write values
	RegSetValueEx(hKey, _T("FolderErase"), 0, dwType, (BYTE *)&dirErase, dwSize);
	RegSetValueEx(hKey, _T("TimeOption"), 0, dwType, (BYTE *)&time, dwSize);
	RegSetValueEx(hKey, _T("EraseMethod"), 0, dwType, (BYTE *)&eraseMethod, dwSize);

	RegCloseKey(hKey);

	return TRUE;
}

BOOL RgDeleteCmd::RegistryGetOptions(__out int *dirErase, __out int *time, __out int *eraseMethod)
{
	LONG error = 0;
	HKEY hKey;
	DWORD dwType = REG_DWORD;
	DWORD dwSize = sizeof(int);

	//open the key
	error = RegOpenKeyEx(HKEY_CURRENT_USER, _T("Software\\Orient Computer\\RgDelete\\UI_Options"), 0, KEY_ALL_ACCESS, &hKey);
	if (error != ERROR_SUCCESS)
		return FALSE;

	//read
	RegQueryValueEx(hKey, _T("FolderErase"), 0, &dwType, (BYTE *)dirErase, &dwSize);
	RegQueryValueEx(hKey, _T("TimeOption"), 0, &dwType, (BYTE *)time, &dwSize);
	RegQueryValueEx(hKey, _T("EraseMethod"), 0, &dwType, (BYTE *)eraseMethod, &dwSize);

	RegCloseKey(hKey);

	return TRUE;
}
