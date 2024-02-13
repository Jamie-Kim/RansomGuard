#include "stdafx.h"
#include <cstdio>
#include <cstring>
#include <algorithm>
#include <CommCtrl.h>
#include <Shellapi.h>

#include "RgDelete.h"
#include "FileShred.h"
#include "FileShredSSD.h"
#include "Resource.h"

HWND FileShred::hProgDlg;
HANDLE FileShred::hStopMutex;
BOOL FileShred::bTriedTrimEnableFlag = FALSE;

int FileShred::ShredFile(TCHAR filename[], bool(*OverWriteMethod)(HANDLE&))
{
	WriteLog(LOG_TYPE_NORMAL, filename, _T("File Erased Start"));

	SetFileAttributes(filename, FILE_ATTRIBUTE_NORMAL);

	//Do it only one time after runing the program
	FileShredSSD::DoErasingSSD(filename, &bTriedTrimEnableFlag);

	// Overwrite file
	HANDLE hFile = CreateFile(filename, GENERIC_READ | GENERIC_WRITE, 0, NULL, OPEN_EXISTING, FILE_FLAG_WRITE_THROUGH, NULL);
	if (hFile != INVALID_HANDLE_VALUE)
	{
		if (!OverWriteMethod(hFile))
		{
			WriteLog(LOG_TYPE_ERROR, filename, _T("File Overwriting Error"));
		}

		if (WaitForSingleObject(hStopMutex, 0) == WAIT_OBJECT_0)
		{
			CloseHandle(hFile);
			return -1;
		}

		// Zero size the file
		SetFilePointer(hFile, 0, 0, FILE_BEGIN);
		SetEndOfFile(hFile);

		CloseHandle(hFile);
	}
	else
	{
		WriteLog(LOG_TYPE_ERROR, filename, _T("File Not Found"));
		return -1;
	}

	// Keep a copy of the filename, just in case renaming or deleting goes wrong
	const std::wstring errName(filename);

	TCHAR delName[MAX_PATH] = { 0 };
	wsprintf(delName, _T("%s"), filename);

	// Generate random name
	for (int n = 0; n < 10; ++n)
	{
		std::wstring newfname(delName, _tcsrchr(delName, '\\') + 1);

		for (int i = 0; i < 13; ++i)
			newfname += 'a' + rand() % ('z' - 'a');
		newfname[newfname.size() - 4] = '.';

		if (0 != _trename(delName, newfname.c_str()))
		{
			WriteLog(LOG_TYPE_ERROR, filename, _T("Error Renaming File"));
			Sleep(10);
		}
		else{
			wsprintf(delName, _T("%s"), newfname.c_str());
		}
	}

	// Delete file
	if (0 == DeleteFile(delName))
	{
		WriteLog(LOG_TYPE_ERROR, filename, _T("Error deleting File"));
		return -1;
	}

	WriteLog(LOG_TYPE_NORMAL, filename, _T("File Erased Complete"));

	return 0;
}

int FileShred::ShredTrashBin(bool(*OverWriteMethod)(HANDLE&))
{
	TCHAR szParam[1024] = { 0 };
	DWORD logicalDrives = GetLogicalDrives() << 1;
	TCHAR driveLetter = 'a';
	TCHAR driveStr[4] = { 0 };

	for (int i = 1; i <= 24; i++)
	{
		if (logicalDrives & (int)pow(2, i))
		{
			wsprintf(driveStr, _T("%c:\\"), driveLetter);
			if (GetDriveType(driveStr) == DRIVE_FIXED)
			{
				wsprintf(szParam, _T("%s$Recycle.Bin"), driveStr);
				ShredDir(szParam, OverWriteMethod, FALSE);
			}
		}
		driveLetter++;
	}

	SHEmptyRecycleBin(NULL, NULL, SHERB_NOCONFIRMATION);
	
	return 0;
}

int FileShred::ShredDir(TCHAR dirname[], bool(*OverWriteMethod)(HANDLE&), bool del_dir)
{
	DWORD attr = GetFileAttributes(dirname);
	SetFileAttributes(dirname, FILE_ATTRIBUTE_NORMAL);

	WIN32_FIND_DATA ffd;
	HANDLE hFind = INVALID_HANDLE_VALUE;

	_tcscat(dirname, _T("\\*"));

	hFind = FindFirstFile(dirname, &ffd);

	// Erase "\\*" from dirname
	dirname[_tcslen(dirname) - 2] = '\0';

	if (INVALID_HANDLE_VALUE == hFind)
	{
		WriteLog(LOG_TYPE_ERROR, dirname, _T("Directory Not Found"));
		return -1;
	}

	// Get a list of subdirs and files
	do
	{
		if (_tcscmp(ffd.cFileName, _T(".")) != 0 && _tcscmp(ffd.cFileName, _T("..")) != 0)
		{
			TCHAR temp[MAX_PATH];
			_tcscpy(temp, dirname);
			_tcscat(temp, _T("\\"));
			_tcscat(temp, ffd.cFileName);

			if (ffd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
				ShredDir(temp, OverWriteMethod, TRUE);
			else
			{
				SetDlgItemText(hProgDlg, IDC_PROGTEXT, ffd.cFileName);
				ShredFile(temp, OverWriteMethod);
			}
		}
	} while (FindNextFile(hFind, &ffd) != 0);
	FindClose(hFind);

	if (WaitForSingleObject(hStopMutex, 0) == WAIT_OBJECT_0)
		return -1;

	SetDlgItemText(hProgDlg, IDC_PROGTEXT, _tcsrchr(dirname, '\\') + 1);

	// Keep a copy of the dirname, just in case renaming or deleting goes wrong
	//const std::wstring errName(dirname);

	// Delete Directory
	if (del_dir == TRUE)
	{
		// Generate random name
		for (int n = 0; n < 10; ++n)
		{
			std::wstring newdname(dirname, _tcsrchr(dirname, '\\') + 1);

			for (int i = 0; i < 13; ++i)
				newdname += 'a' + rand() % ('z' - 'a');

			if (0 != _trename(dirname, newdname.c_str()))
			{
				WriteLog(LOG_TYPE_ERROR, dirname, _T("Error Renaming Directory"));
			}

			_tcscpy(dirname, newdname.c_str());
		}

		if (0 == RemoveDirectory(dirname))
		{
			WriteLog(LOG_TYPE_ERROR, dirname, _T("Error removing <DIR>"));
		}
	}
	else
	{
		SetFileAttributes(dirname, attr);
	}

	return 0;
}


int FileShred::DelDir(TCHAR dirname[], bool del_dir)
{
	DWORD attr = GetFileAttributes(dirname);
	SetFileAttributes(dirname, FILE_ATTRIBUTE_NORMAL);

	WIN32_FIND_DATA ffd;
	HANDLE hFind = INVALID_HANDLE_VALUE;

	_tcscat(dirname, _T("\\*"));

	hFind = FindFirstFile(dirname, &ffd);

	// Erase "\\*" from dirname
	dirname[_tcslen(dirname) - 2] = '\0';

	if (INVALID_HANDLE_VALUE == hFind)
	{
		return -1;
	}

	// Get a list of subdirs and files
	do
	{
		if (_tcscmp(ffd.cFileName, _T(".")) != 0 && _tcscmp(ffd.cFileName, _T("..")) != 0)
		{
			TCHAR temp[MAX_PATH];
			_tcscpy(temp, dirname);
			_tcscat(temp, _T("\\"));
			_tcscat(temp, ffd.cFileName);

			if (ffd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
				DelDir(temp, TRUE);
			else
			{
				DeleteFile(temp);
			}
		}
	} while (FindNextFile(hFind, &ffd) != 0);
	FindClose(hFind);

	// Delete Directory
	if (del_dir == TRUE)
	{
		RemoveDirectory(dirname);
	}

	return 0;
}


bool FileShred::OverWrite0s(HANDLE& hFile)
{
	LARGE_INTEGER liFileSize;
	GetFileSizeEx(hFile, &liFileSize);

	char buffer[MAX_BUFFER_SIZE];
	memset(buffer, 0, (size_t)(MAX_BUFFER_SIZE < liFileSize.QuadPart ? MAX_BUFFER_SIZE : liFileSize.QuadPart));

	for (long long int s = liFileSize.QuadPart; s > 0 && WaitForSingleObject(hStopMutex, 0) != WAIT_OBJECT_0; s -= MAX_BUFFER_SIZE)
	{
		int bufsize = (int)((s < MAX_BUFFER_SIZE) ? s : MAX_BUFFER_SIZE);

		DWORD dwWritten;
		BOOL bSuccess = WriteFile(hFile, buffer, bufsize, &dwWritten, NULL);

		// Increment the progress bar
		for (int i = 0; i < 105; ++i)
			SendDlgItemMessage(hProgDlg, IDC_PROGRESS, PBM_STEPIT, 0, 0);

		if (!bSuccess)
			return false;
	}

	return true;
}

bool FileShred::OverWriteRand(HANDLE& hFile)
{
	LARGE_INTEGER liFileSize;
	GetFileSizeEx(hFile, &liFileSize);

	char buffer[MAX_BUFFER_SIZE];
	memset(buffer, 0, (size_t)(MAX_BUFFER_SIZE < liFileSize.QuadPart ? MAX_BUFFER_SIZE : liFileSize.QuadPart));

	for (long long int s = liFileSize.QuadPart; s > 0 && WaitForSingleObject(hStopMutex, 0) != WAIT_OBJECT_0; s -= MAX_BUFFER_SIZE)
	{
		DWORD bufsize = (DWORD)((s < MAX_BUFFER_SIZE) ? s : MAX_BUFFER_SIZE);
		for (DWORD i = 0; i < bufsize; ++i)
			buffer[i] = char(rand() % 256);

		DWORD dwWritten;
		BOOL bSuccess = WriteFile(hFile, buffer, bufsize, &dwWritten, NULL);

		// Increment the progress bar
		for (int i = 0; i < 105; ++i)
			SendDlgItemMessage(hProgDlg, IDC_PROGRESS, PBM_STEPIT, 0, 0);

		if (!bSuccess)
			return false;
	}

	return true;
}

bool FileShred::OverWriteDOD3(HANDLE& hFile)
{
	unsigned char pattern[3] = { 0x00, 0xFF, 'r' };

	LARGE_INTEGER liFileSize;
	GetFileSizeEx(hFile, &liFileSize);

	char buffer[MAX_BUFFER_SIZE];

	for (int p = 0; p < 3; ++p)
	{
		SetFilePointer(hFile, 0, 0, FILE_BEGIN);

		if (p != 2)
			memset(buffer, pattern[p], (size_t)(MAX_BUFFER_SIZE < liFileSize.QuadPart ? MAX_BUFFER_SIZE : liFileSize.QuadPart));

		for (long long int s = liFileSize.QuadPart; s > 0 && WaitForSingleObject(hStopMutex, 0) != WAIT_OBJECT_0; s -= MAX_BUFFER_SIZE)
		{
			DWORD bufsize = (DWORD)((s < MAX_BUFFER_SIZE) ? s : MAX_BUFFER_SIZE);
			if (p == 2)
				for (DWORD i = 0; i < bufsize; ++i)
					buffer[i] = char(rand() % 256);

			DWORD dwWritten;
			BOOL bSuccess = WriteFile(hFile, buffer, bufsize, &dwWritten, NULL);

			// Increment the progress bar
			for (int i = 0; i < 35; ++i)
				SendDlgItemMessage(hProgDlg, IDC_PROGRESS, PBM_STEPIT, 0, 0);

			if (!bSuccess)
				return false;
		}
	}

	return true;
}

bool FileShred::OverWriteDOD7(HANDLE& hFile)
{
	unsigned pattern[7] = { 0x00, 0xFF, 'r', 0x96, 0x00, 0xFF, 'r' };

	LARGE_INTEGER liFileSize;
	GetFileSizeEx(hFile, &liFileSize);

	char buffer[MAX_BUFFER_SIZE];

	for (int p = 0; p < 7; ++p)
	{
		SetFilePointer(hFile, 0, 0, FILE_BEGIN);

		if (p != 2 && p != 6)
			memset(buffer, pattern[p], (size_t)(MAX_BUFFER_SIZE < liFileSize.QuadPart ? MAX_BUFFER_SIZE : liFileSize.QuadPart));

		for (long long int s = liFileSize.QuadPart; s > 0 && WaitForSingleObject(hStopMutex, 0) != WAIT_OBJECT_0; s -= MAX_BUFFER_SIZE)
		{
			DWORD bufsize = (DWORD)((s < MAX_BUFFER_SIZE) ? s : MAX_BUFFER_SIZE);
			if (p == 2 || p == 6)
				for (DWORD i = 0; i < bufsize; ++i)
					buffer[i] = char(rand() % 256);

			DWORD dwWritten;
			BOOL bSuccess = WriteFile(hFile, buffer, bufsize, &dwWritten, NULL);

			// Increment the progress bar
			for (int i = 0; i < 15; ++i)
				SendDlgItemMessage(hProgDlg, IDC_PROGRESS, PBM_STEPIT, 0, 0);

			if (!bSuccess)
				return false;
		}
	}

	return true;
}

bool FileShred::OverWriteGut(HANDLE& hFile)
{
	unsigned pattern[27][3] = { { 0x55, 0x55, 0x55 }, { 0xAA, 0xAA, 0xAA }, { 0x92, 0x49, 0x24 }, { 0x49, 0x24, 0x92 }, { 0x24, 0x92, 0x49 }, { 0x00, 0x00, 0x00 },
		{ 0x11, 0x11, 0x11 }, { 0x22, 0x22, 0x22 }, { 0x33, 0x33, 0x33 }, { 0x44, 0x44, 0x44 }, { 0x55, 0x55, 0x55 }, { 0x66, 0x66, 0x66 },
		{ 0x77, 0x77, 0x77 }, { 0x88, 0x88, 0x88 }, { 0x99, 0x99, 0x99 }, { 0xAA, 0xAA, 0xAA }, { 0xBB, 0xBB, 0xBB }, { 0xCC, 0xCC, 0xCC },
		{ 0xDD, 0xDD, 0xDD }, { 0xEE, 0xEE, 0xEE }, { 0xFF, 0xFF, 0xFF }, { 0x92, 0x49, 0x24 }, { 0x49, 0x24, 0x92 }, { 0x24, 0x92, 0x49 },
		{ 0x6D, 0xB6, 0xDB }, { 0xB6, 0xDB, 0x6D }, { 0xDB, 0x6D, 0xB6 } };

	std::random_shuffle(pattern, pattern + 27);

	LARGE_INTEGER liFileSize;
	GetFileSizeEx(hFile, &liFileSize);

	char buffer[MAX_BUFFER_SIZE];

	for (int p = 1; p <= 35; ++p)
	{
		for (int i = 0; i < 3; i++)
		{
			if (p > 4 && p < 32)
				memset(buffer, pattern[p - 5][i], (size_t)(MAX_BUFFER_SIZE < liFileSize.QuadPart ? MAX_BUFFER_SIZE : liFileSize.QuadPart));

			SetFilePointer(hFile, 0, 0, FILE_BEGIN);
			for (long long int s = liFileSize.QuadPart; s > 0 && WaitForSingleObject(hStopMutex, 0) != WAIT_OBJECT_0; s -= MAX_BUFFER_SIZE)
			{
				DWORD bufsize = (DWORD)((s < MAX_BUFFER_SIZE) ? s : MAX_BUFFER_SIZE);
				if (p <= 4 || p >= 32)
					for (DWORD i2 = 0; i2 < bufsize; ++i2)
						buffer[i2] = char(rand() % 256);

				DWORD dwWritten;
				BOOL bSuccess = WriteFile(hFile, buffer, bufsize, &dwWritten, NULL);

				// Increment the progress bar
				SendDlgItemMessage(hProgDlg, IDC_PROGRESS, PBM_STEPIT, 0, 0);

				if (!bSuccess)
					return false;
			}
		}
	}

	return true;
}

bool FileShred::OverWriteNone(HANDLE& hFile)
{
	return true;
}