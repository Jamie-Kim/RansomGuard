#include "stdafx.h"
#include <ctime>
#include <CommCtrl.h>
#include <CommDlg.h>
#include <ShlObj.h>
#include <ShellAPI.h>
#include <process.h>

#include "RgDelete.h"
#include "FileShred.h"
#include "FileShredEx.h"
#include "FileShredSSD.h"
#include "RgDeleteCmd.h"

// Make the UI look more "modern"
#pragma comment(linker,"\"/manifestdependency:type='win32' \
name='Microsoft.Windows.Common-Controls' version='6.0.0.0' \
processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
	srand((unsigned) time(0));

	int result = RgDeleteCmd::CmdActions();

	return result;
}

bool WriteLog(INT LogFileType, LPWSTR filename, LPWSTR text)
{
	//do Nothing
	return TRUE;
}

bool DeletAllTasks()
{
	TCHAR szTemp[MAX_PATH] = { 0 };
	TCHAR szBat[MAX_PATH] = { 0 };
	TCHAR szSchParam[MAX_PATH] = { 0 };

	//create xml file for detailed task options
	GetTempPath(MAX_PATH, szTemp);

	//Set command and parameters
	wsprintf(szBat, _T("%s_rgDeleteTmp.bat"), szTemp);

	FILE * eraser = _tfopen(szBat, _T("w+"));
	if (eraser == NULL)
		return false;

	_ftprintf(eraser, _T("for /f %%%%x in ('schtasks /query /tn \\RgDelete\\') do schtasks.exe /Delete /TN \\RgDelete\\%%%%x /f"));
	fclose(eraser);

	//scheduled delete
	SHELLEXECUTEINFO ShExecInfo = { 0 };
	ShExecInfo.cbSize = sizeof(SHELLEXECUTEINFO);
	ShExecInfo.fMask = SEE_MASK_NOCLOSEPROCESS;
	ShExecInfo.hwnd = NULL;
	ShExecInfo.lpVerb = NULL;
	ShExecInfo.lpFile = szBat;
	ShExecInfo.lpParameters = NULL;
	ShExecInfo.lpDirectory = NULL;
	ShExecInfo.nShow = SW_HIDE;
	ShExecInfo.hInstApp = NULL;
	ShellExecuteEx(&ShExecInfo);

	// Wait until child process exits.
	WaitForSingleObject(ShExecInfo.hProcess, INFINITE);
	DeleteFile(szBat);

	return true;
}

bool DeletTbAllTasks()
{
	TCHAR szTemp[MAX_PATH] = { 0 };
	TCHAR szBat[MAX_PATH] = { 0 };
	TCHAR szSchParam[MAX_PATH] = { 0 };

	//create xml file for detailed task options
	GetTempPath(MAX_PATH, szTemp);

	//Set command and parameters
	wsprintf(szBat, _T("%s_rgDeleteTmp.bat"), szTemp);

	FILE * eraser = _tfopen(szBat, _T("w+"));
	if (eraser == NULL)
		return false;

	_ftprintf(eraser, _T("for /f %%%%x in ('schtasks /query /tn \\RgTbDelete\\') do schtasks.exe /Delete /TN \\RgTbDelete\\%%%%x /f"));
	fclose(eraser);

	//scheduled delete
	SHELLEXECUTEINFO ShExecInfo = { 0 };
	ShExecInfo.cbSize = sizeof(SHELLEXECUTEINFO);
	ShExecInfo.fMask = SEE_MASK_NOCLOSEPROCESS;
	ShExecInfo.hwnd = NULL;
	ShExecInfo.lpVerb = NULL;
	ShExecInfo.lpFile = szBat;
	ShExecInfo.lpParameters = NULL;
	ShExecInfo.lpDirectory = NULL;
	ShExecInfo.nShow = SW_HIDE;
	ShExecInfo.hInstApp = NULL;
	ShellExecuteEx(&ShExecInfo);

	// Wait until child process exits.
	WaitForSingleObject(ShExecInfo.hProcess, INFINITE);
	DeleteFile(szBat);

	return true;
}