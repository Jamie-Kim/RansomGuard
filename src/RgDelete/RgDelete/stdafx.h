// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#pragma once

#define _CRT_SECURE_NO_DEPRECATE        // Get rid of all those annoying "deprecated" warnings

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files:
#include <windows.h>

// C RunTime Header Files
#include <stdlib.h>
#include <malloc.h>
#include <memory.h>
#include <tchar.h>

// TODO: reference additional headers your program requires here
#include <string>
#include "atltime.h"
#include "atlstr.h"
#include "TextHeader.h"

//set types
enum LogFileType
{
	LOG_TYPE_NORMAL = 0,
	LOG_TYPE_ERROR = 1,
	LOG_TYPE_MAX
};

enum DefaultType
{
	DEFAULT_DIR_ERASE = 0,
	DEFAULT_TIME_METHORD = 1,
	DEFAULT_WRITE_METHORD = 2,
	DEFAULT_MAX
};


// Global Variables
extern HWND g_hMain;

// Log Write Flag
extern BOOL g_bLogWrite;
extern TCHAR g_szLogWrite[10];
extern TCHAR g_szLogWritePath[MAX_PATH];
extern TCHAR g_szErrorLogWritePath[MAX_PATH];
extern TCHAR g_szEraseMethod[5][MAX_PATH];
extern INT g_defaultSettings[DEFAULT_MAX];
