#include "stdafx.h"
#include <ShlObj.h>
#include <ShellAPI.h>

#include "RgDelete.h"
#include "FileShred.h"
#include "FileShredEx.h"
#include <Wincrypt.h>
#include "Md5Capi.h"
#include "ATLComTime.h"

int FileShredEx::GetTaskName(__in TCHAR *path, __out TCHAR *taskName)
{
	//get hash code from file name
	Cmd5Capi md5;
	CString sPath(path);
	CString hashedStr;
	hashedStr = md5.Digest(sPath);
	wsprintf(taskName, _T("%s"), hashedStr.MakeUpper());

	return 1;
}

int FileShredEx::DoDirectDelete(TCHAR *path, TCHAR *eraseMethod, TCHAR *taskName, bool del_dir)
{
	int result = MB_OK;
	DWORD fileAttr = GetFileAttributes(path);
	int method = _ttoi(eraseMethod);

	//write log
	TCHAR LogText[256] = { 0 };
	wsprintf(LogText, _T("DoDirectDelete delete with %s"), g_szEraseMethod[method]);
	WriteLog(LOG_TYPE_NORMAL, path, LogText);

	//define erase methode
	bool(*OverWriteMethod[5])(HANDLE&) = { FileShred::OverWrite0s, FileShred::OverWriteRand, FileShred::OverWriteDOD3,
		FileShred::OverWriteDOD7, FileShred::OverWriteGut };

	//erase file
	if (fileAttr & FILE_ATTRIBUTE_DIRECTORY)
		result = FileShred::ShredDir(path, OverWriteMethod[method], del_dir);
	else
		result = FileShred::ShredFile(path, OverWriteMethod[method]);

	if (taskName != NULL && (result == MB_OK || fileAttr == INVALID_FILE_ATTRIBUTES))
	{
		//delete task
		TCHAR szSchParam[2048] = { 0 };
		wsprintf(szSchParam, _T("/Delete /TN \"RgDelete\\%s\" /f"), taskName);
		ShellExecute(NULL, _T("open"), _T("SchTasks"), szSchParam, NULL, SW_HIDE);
	}

	SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, NULL, NULL);

	return 1;
}

int FileShredEx::DoDirectDeleteRegular(TCHAR *path, TCHAR *eraseMethod, TCHAR *taskName
	, TCHAR *regular, TCHAR *date, TCHAR *time, TCHAR *eraseDir, bool del_dir)
{
	DoDirectDelete(path, eraseMethod, taskName, del_dir);
	DoScheDelete(path, eraseMethod, date, NULL, time, eraseDir, regular, FALSE);

	return 1;
}

int FileShredEx::DoDirectDeleteTbRegular(TCHAR *eraseMethod, TCHAR *taskName, TCHAR *regular, TCHAR *time, TCHAR *thDay)
{
	DoTrashBinDelete(eraseMethod, thDay);
	DoTbScheDelete(eraseMethod, time, regular, thDay);

	return 1;
}

int  FileShredEx::DoTbScheDelete(TCHAR *eraseMethod, TCHAR *time, TCHAR *regular, TCHAR *thDay = NULL)
{
	TCHAR szSchParam[1024] = { 0 };
	TCHAR szCommand[1024] = { 0 };
	TCHAR szParam[1024] = { 0 };
	TCHAR szXml[MAX_PATH] = { 0 };
	TCHAR szTemp[MAX_PATH] = { 0 };
	TCHAR szEraseDate[21] = { 0 };
	TCHAR szGuid[40] = { 0 };

	GetTempPath(MAX_PATH, szTemp);
	GetModuleFileName(NULL, szCommand, MAX_PATH);

	//create task Name as Guid
	GetTaskName(NULL, szGuid);

	//Set command and parameters
	wsprintf(szXml, _T("%s%s.xml"), szTemp, szGuid);

	//GetNextDeleteDate(regular, szEraseDate);
	GetNextDeleteDateEx(regular, NULL, time, szEraseDate);

	//set param
	wsprintf(szParam, _T("/s RgDelete /th y /td %s /m %s /tn %s /r %s /t %s %s"), thDay, eraseMethod, szGuid, regular, time, g_szLogWrite);

	//create xml file
	CreateXmlForTasks(szXml, szEraseDate, time, szCommand, szParam);

	wsprintf(szSchParam, _T("/Create /F /XML \"%s\" /TN \"RgTbDelete\\%s\""), szXml, szGuid);

	//scheduled delete
	SHELLEXECUTEINFO ShExecInfo = { 0 };
	ShExecInfo.cbSize = sizeof(SHELLEXECUTEINFO);
	ShExecInfo.fMask = SEE_MASK_NOCLOSEPROCESS;
	ShExecInfo.hwnd = NULL;
	ShExecInfo.lpVerb = NULL;
	ShExecInfo.lpFile = _T("SchTasks");
	ShExecInfo.lpParameters = szSchParam;
	ShExecInfo.lpDirectory = NULL;
	ShExecInfo.nShow = SW_HIDE;
	ShExecInfo.hInstApp = NULL;
	ShellExecuteEx(&ShExecInfo);

	// Wait until child process exits.
	WaitForSingleObject(ShExecInfo.hProcess, INFINITE);

	//delete xml file
	DeleteFile(szXml);

	return 1;
}

int  FileShredEx::DoScheDelete(TCHAR *path, TCHAR *eraseMethod, TCHAR *date, 
	TCHAR *sel_day, TCHAR *time, TCHAR *eraseDir, TCHAR *regular, bool isTrashBin = false)
{
	TCHAR szSchParam[1024] = { 0 };
	TCHAR szCommand[1024] = { 0 };
	TCHAR szParam[1024] = { 0 };
	TCHAR szXml[MAX_PATH] = { 0 };
	TCHAR szTemp[MAX_PATH] = { 0 };
	TCHAR szEraseDate[21] = { 0 };
	TCHAR szGuid[40] = { 0 };

	GetTempPath(MAX_PATH, szTemp);
	GetModuleFileName(NULL, szCommand, MAX_PATH);

	//create task Name as Guid
	GetTaskName(path, szGuid);

	//Set command and parameters
	wsprintf(szXml, _T("%s%s.xml"), szTemp, szGuid);

	if (regular == NULL)
	{
		if (isTrashBin)
			wsprintf(szParam, _T("/s RgDelete /th y /m %s /tn \"%s\" %s"), eraseMethod, szGuid, g_szLogWrite);
		else
			wsprintf(szParam, _T("/s RgDelete /p \"%s\" /m %s /tn \"%s\" /de %s %s"), path, eraseMethod, szGuid, eraseDir, g_szLogWrite);
		
		//create xml file
		CreateXmlForTasks(szXml, date, time, szCommand, szParam);
	}
	else
	{
		//GetNextDeleteDate(regular, szEraseDate);
		GetNextDeleteDateEx(regular, sel_day, time, szEraseDate);

		if (isTrashBin)
			wsprintf(szParam, _T("/s RgDelete /th y /m %s /tn %s /r %s /d %s /t %s %s"), 
			eraseMethod, szGuid, regular, szEraseDate, time, g_szLogWrite);
		else
			wsprintf(szParam, _T("/s RgDelete /p \"%s\" /m %s /tn %s /r %s /d %s /t %s /de %s %s"),
			path, eraseMethod, szGuid, regular, szEraseDate, time, eraseDir, g_szLogWrite);

		//create xml file
		CreateXmlForTasks(szXml, szEraseDate, time, szCommand, szParam);
	}
	wsprintf(szSchParam, _T("/Create /F /XML \"%s\" /TN \"RgDelete\\%s\""), szXml, szGuid);

	//scheduled delete
	SHELLEXECUTEINFO ShExecInfo = { 0 };
	ShExecInfo.cbSize = sizeof(SHELLEXECUTEINFO);
	ShExecInfo.fMask = SEE_MASK_NOCLOSEPROCESS;
	ShExecInfo.hwnd = NULL;
	ShExecInfo.lpVerb = NULL;
	ShExecInfo.lpFile = _T("SchTasks");
	ShExecInfo.lpParameters = szSchParam;
	ShExecInfo.lpDirectory = NULL;
	ShExecInfo.nShow = SW_HIDE;
	ShExecInfo.hInstApp = NULL;
	ShellExecuteEx(&ShExecInfo);

	// Wait until child process exits.
	WaitForSingleObject(ShExecInfo.hProcess, INFINITE);

	//delete xml file
	DeleteFile(szXml);

	//write log
	TCHAR LogText[256] = { 0 };
	wsprintf(LogText, _T("DoScheDelete delete with %s at [%s %s] , delete directory:[%s]")
		, g_szEraseMethod[_ttoi(eraseMethod)], date, time, eraseDir);
	WriteLog(LOG_TYPE_NORMAL, path, LogText);

	return 1;
}

//Get next erasing date time , if e_day is null then just get date without considerging seltected date and time
void FileShredEx::GetNextDeleteDateEx(__in TCHAR *regular, __in TCHAR *e_day, __in TCHAR *e_time, __out TCHAR *date)
{
	//get current date
	CTime currTime = CTime::GetCurrentTime();
	CTime selTime;

	CString nextDate;
	TCHAR tmp_time[16] = { 0 };

	int year = currTime.GetYear();
	int month = currTime.GetMonth();
	int day = currTime.GetDay();
	int hour = currTime.GetHour();
	int min = currTime.GetMinute();

	//selected day and time
	int sel_hour, sel_min = 0;
	swscanf(e_time, _T("%d:%d"), &sel_hour, &sel_min);

	CTime nexEraseDate;

	//selected date is null
	if (e_day == NULL)
	{
		//add a day
		if (!_tcscmp(_T("DAILY"), regular))
			nexEraseDate = currTime + CTimeSpan(1, 0, 0, 0);
		//add a week
		else if (!_tcscmp(_T("WEEKLY"), regular))
			nexEraseDate = currTime + CTimeSpan(7, 0, 0, 0);
		//add a month
		else if (!_tcscmp(_T("MONTHLY"), regular))
			nexEraseDate = CTime(year, month + 1, day, hour, min, 0);
	}
	else
	{
		selTime = CTime(year, month, day, sel_hour, sel_min, 0);

		if (!_tcscmp(_T("DAILY"), regular))
		{
			//get next day
			nexEraseDate = currTime + CTimeSpan((currTime > selTime ? 1 : 0), 0, 0, 0);
		}
		else if (!_tcscmp(_T("WEEKLY"), regular))
		{
			//get next week
			int dayDiff = 0;
			const TCHAR *weekOfDay[] = { _T("ìí"), _T("êÅ"), _T("ûý"), _T("â©"), _T("ÙÊ"), _T("ÑÑ"), _T("÷Ï") };
			int curDay = currTime.GetDayOfWeek() - 1;  // 0~ 6  0: Sun 1:Mon  ... 

			for (int i = 0; i <= 6; i++)
			{
				if (!_tcscmp(weekOfDay[i], e_day))
				{
					dayDiff = abs(curDay - i);
					break;
				}
			}

			//same day now then do it next week.
			if (dayDiff == 0)
			{
				if (currTime > selTime)
					dayDiff = 7;
			}
			nexEraseDate = currTime + CTimeSpan(dayDiff, 0, 0, 0);
		}
		else if (!_tcscmp(_T("MONTHLY"), regular))
		{
			//get next month
			int addMonth = 0;
			int selDate = 0;

			swscanf(e_day, _T("%d"), &selDate);

			if ((day > selDate) || (day == selDate && currTime > selTime))
				addMonth = 1;

			nexEraseDate = CTime(year, month + addMonth, selDate, hour, min, 0);
		}
	}

	nextDate = nexEraseDate.Format(_T("%Y-%m-%d"));

	//wriet string into data
	wsprintf(date, _T("%s"), nextDate);
}

//To get first day of month and day of next Monday to get erasing date
void FileShredEx::GetNextDeleteDate(__in TCHAR *regular, __out TCHAR *date)
{
	//get current date
	CTime currTime = CTime::GetCurrentTime();
	CString nextDate;

	int year = currTime.GetYear();
	int month = currTime.GetMonth();
	int day = currTime.GetDay();

	//regular time set as every Monday
	if (!_tcscmp(_T("DAILY"), regular)){
		//get next day
		CTime nextDay = currTime + CTimeSpan(1, 0, 0, 0);
		nextDate = nextDay.Format(_T("%Y-%m-%d"));
	}
	else if (!_tcscmp(_T("WEEKLY"), regular))
	{
		//get next Monday
		int addDay[7] = { 1, 7, 6, 5, 4, 3, 2 };

		CTime nextMonDay = currTime + CTimeSpan(addDay[currTime.GetDayOfWeek() - 1], 0, 0, 0);
		nextDate = nextMonDay.Format(_T("%Y-%m-%d"));
	}
	else if (!_tcscmp(_T("MONTHLY"), regular))
	{
		//get 1 day of month
		CTime nextMonth(year, month + 1, 1, 12, 0, 0);
		nextDate = nextMonth.Format(_T("%Y-%m-%d"));
	}

	//wriet string into data
	wsprintf(date, _T("%s"), nextDate);
}

//Do not use, it has some problem to erase becase it works only one time is specific time.. so if the time is missed erasing doesn't work.
int FileShredEx::DoRegularDelete(TCHAR *path, TCHAR *eraseMethod, TCHAR *regular, TCHAR *time, TCHAR *eraseDir)
{
	//regular delete
	TCHAR szSchParam[1024] = { 0 };
	TCHAR szCommand[1024] = { 0 };
	TCHAR szRegular[64] = { 0 };
	TCHAR szNextDate[64] = { 0 };
	TCHAR szGuid[40] = { 0 };

	GetModuleFileName(NULL, szCommand, MAX_PATH);

	//create task Name as Guid
	GetTaskName(path, szGuid);

	//regular time set as every Monday
	if (!_tcscmp(_T("DAILY"), regular))
		wsprintf(szRegular, _T("%s"), regular);
	else if (!_tcscmp(_T("WEEKLY"), regular))
		wsprintf(szRegular, _T("%s /D MON"), regular);
	else if (!_tcscmp(_T("MONTHLY"), regular))
		wsprintf(szRegular, _T("%s /MO first /D MON"), regular);
	else
		return -1;

	wsprintf(szCommand, _T("%s /s RgDelete /p \'%s\' /m %s /tn \"%s\" /r %s /t %s /de %s %s")
		, szCommand, path, eraseMethod, szGuid, szRegular, time, eraseDir, g_szLogWrite);
	wsprintf(szSchParam, _T("/Create /F /TN \"RgDelete\\%s\" /TR \"%s\" /SC %s /ST %s"), szGuid, szCommand, szRegular, time);

	//scheduled delete
	ShellExecute(NULL, _T("open"), _T("SchTasks"), szSchParam, NULL, SW_HIDE);

	//write log
	TCHAR LogText[256] = { 0 };
	wsprintf(LogText, _T("DoRegularDelete delete %s with %s "), time, g_szEraseMethod[_ttoi(eraseMethod)]);
	WriteLog(LOG_TYPE_NORMAL, path, LogText);

	return 1;
}

int FileShredEx::DoTrashBinDelete(TCHAR *eraseMethod, TCHAR *thDay = NULL)
{
	TCHAR szCommand[1024] = { 0 };
	TCHAR folder[1024] = { 0 };

	GetModuleFileName(NULL, szCommand, MAX_PATH);

	DWORD logicalDrives = GetLogicalDrives() << 1;
	TCHAR driveLetter = 'a';
	TCHAR driveStr[4] = { 0 };

	int remainedFile = 0;

	for (int i = 1; i <= 24; i++)
	{
		if (logicalDrives & (int)pow(2, i))
		{
			wsprintf(driveStr, _T("%c:\\"), driveLetter);
			if (GetDriveType(driveStr) == DRIVE_FIXED)
			{
				wsprintf(folder, _T("%s$Recycle.Bin"), driveStr);
				DoTbFolderDelete(folder, eraseMethod, thDay, &remainedFile);
			}
		}

		driveLetter++;
	}

	if (remainedFile == 0)
	{
		SHEmptyRecycleBin(NULL, NULL, SHERB_NOCONFIRMATION);
	}

	return 1;
}

int FileShredEx::DoTbFolderDelete(TCHAR *path ,TCHAR *eraseMethod, TCHAR *thDay, int *remainedCnt)
{
	//MessageBox(NULL, path, LD_MSG_BOX_TITLE, MB_OK);
	WIN32_FIND_DATA data;
	HANDLE hSearch;
	TCHAR searchKey[MAX_PATH];
	TCHAR currPath[MAX_PATH];

	int oldDay = _ttoi(thDay);
	int method = _ttoi(eraseMethod);
	int remainedFile = 0;

	CTime currTime = CTime::GetCurrentTime();
	CTime toDelTime = currTime - CTimeSpan(oldDay, 0, 0, 0);

	//define erase methode
	bool(*OverWriteMethod[5])(HANDLE&) = { FileShred::OverWrite0s, FileShred::OverWriteRand, FileShred::OverWriteDOD3,
		FileShred::OverWriteDOD7, FileShred::OverWriteGut};

	wsprintf(searchKey, _T("%s\\*"), path);
	hSearch = FindFirstFile(searchKey, &data);

	if (hSearch != INVALID_HANDLE_VALUE)
	{
		do
		{
			if (data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
			{
				if (_tcscmp(data.cFileName, _T(".")) != 0 &&
					_tcscmp(data.cFileName, _T("..")) != 0)
				{
					wsprintf(currPath, _T("%s\\%s"), path, data.cFileName);
					DoTbFolderDelete(currPath, eraseMethod, thDay, remainedCnt);
				}
			}
			else
			{
				//found only $I* file
				if (data.cFileName[0] == _T('$'))
				{
					if (data.cFileName[1] == _T('I'))
					{ 
						BOOL needToDel = FALSE;
						TCHAR rfileName[MAX_PATH] = { 0 };
						TCHAR timestamp[8] = { 0 };
						DWORD readByte = 0;
						wsprintf(currPath, _T("%s\\%s"), path, data.cFileName);

						//check file date time
						HANDLE hFile = CreateFile(currPath, GENERIC_READ, 0, NULL, OPEN_EXISTING, FILE_FLAG_WRITE_THROUGH, NULL);
						if (hFile != INVALID_HANDLE_VALUE)
						{
							// Zero size the file
							SetFilePointer(hFile, 16, 0, FILE_BEGIN);
							ReadFile(hFile, timestamp, 8, &readByte, NULL);

							CloseHandle(hFile);
						}

						//MessageBox(NULL, currPath, LD_MSG_BOX_TITLE, MB_OK);
						//MessageBox(NULL, fileDelTime.Format(L"D %Y %H %M %S"), LD_MSG_BOX_TITLE, MB_OK);
						//MessageBox(NULL, toDelTime.Format(L"T %Y %H %M %S"), LD_MSG_BOX_TITLE, MB_OK);

						//sometimes $I~ file size is 0, and can't read the info.
						if (readByte)
						{
							FILETIME ft;
							memcpy(&ft, timestamp, 8);
							CTime fileDelTime = CTime(ft);

							if (fileDelTime <= toDelTime)
								needToDel = TRUE;
						}
						else
						{
							needToDel = TRUE;
						}

						if (needToDel)
						{
							//delete info file
							DeleteFile(currPath);

							//get real file path
							data.cFileName[1] = _T('R');
							wsprintf(currPath, _T("%s\\%s"), path, data.cFileName);

							if (method == 5)
							{
								//normal erase
								if (GetFileAttributes(currPath) & FILE_ATTRIBUTE_DIRECTORY)
								{
									FileShred::DelDir(currPath, true);
								}
								else
								{
									DeleteFile(currPath);
								}
							}
							else
							{
								//secure erase
								if (data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
								{
									FileShred::ShredDir(currPath, OverWriteMethod[method], TRUE);
								}
								else
								{
									FileShred::ShredFile(currPath, OverWriteMethod[method]);
								}
							}
						}
						else
						{
							(*remainedCnt)++;
						}
					}
					else
					{
						//we don't need to care $R~ file.
						continue;
					}
				}
			}

		}while(FindNextFile(hSearch, &data));

		FindClose(hSearch);
	}

	return 1;
}

//create xml file for detailed task options
void FileShredEx::CreateXmlForTasks(TCHAR *fileName, TCHAR *date, TCHAR *time, TCHAR *cmd, TCHAR *param)
{
	FILE * hXml = _tfopen(fileName, _T("w+, ccs=UTF-16LE"));
	if (hXml == NULL)
		return;

	_ftprintf(hXml, _T("\
<?xml version=\"1.0\" encoding=\"UTF-16LE\"?>\n\
<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\n\
	<Triggers>\n\
	<TimeTrigger>\n\
		<Repetition>\n\
			<Interval>PT1M</Interval>\n\
			<StopAtDurationEnd>false</StopAtDurationEnd>\n\
		</Repetition>\n\
		<StartBoundary>%sT%s:00</StartBoundary>\n\
		<Enabled>true</Enabled>\n\
		</TimeTrigger>\n\
	</Triggers>\n\
	<Principals>\n\
	    <Principal id=\"Author\">\n\
		<RunLevel>HighestAvailable</RunLevel>\n\
		</Principal>\n\
	</Principals>\n\
	<Settings>\n\
		<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\n\
		<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\n\
		<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\n\
		<AllowHardTerminate>false</AllowHardTerminate>\n\
		<StartWhenAvailable>true</StartWhenAvailable>\n\
		<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>\n\
		<IdleSettings>\n\
		<StopOnIdleEnd>false</StopOnIdleEnd>\n\
		<RestartOnIdle>false</RestartOnIdle>\n\
		</IdleSettings>\n\
		<AllowStartOnDemand>false</AllowStartOnDemand>\n\
		<Enabled>true</Enabled>\n\
		<Hidden>false</Hidden>\n\
		<RunOnlyIfIdle>false</RunOnlyIfIdle>\n\
		<WakeToRun>false</WakeToRun>\n\
		<ExecutionTimeLimit>P30D</ExecutionTimeLimit>\n\
		<Priority>7</Priority>\n\
	</Settings>\n\
	<Actions Context = \"Author\">\n\
		<Exec>\n\
		<Command>%s</Command>\n\
		<Arguments>%s</Arguments>\n\
		</Exec>\n\
	</Actions>\n\
</Task>\n"), date, time, cmd, param);

	fclose(hXml);
}
