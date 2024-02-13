// stdafx.cpp : source file that includes just the standard includes
// File Shredder.pch will be the pre-compiled header
// stdafx.obj will contain the pre-compiled type information

#include "stdafx.h"

// TODO: reference any additional headers you need in STDAFX.H
// and not in this file
HWND g_hMain = NULL;
BOOL g_bLogWrite = FALSE;
TCHAR g_szLogWrite[10] = _T("/log n");
TCHAR g_szLogWritePath[MAX_PATH] = _T("");
TCHAR g_szErrorLogWritePath[MAX_PATH] = _T("");
TCHAR g_szEraseMethod[5][MAX_PATH] = { _T("zeroset"), _T("random"), _T("dod 3"), _T("dod 7"), _T("35 overwrite") };
INT g_defaultSettings[DEFAULT_MAX] = {1, 0, 1 };
