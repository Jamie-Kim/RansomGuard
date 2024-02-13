#pragma once

#include "resource.h"

// Dialog Procedures
BOOL CALLBACK ProgProc(HWND hwnd, UINT Message, WPARAM wParam, LPARAM lParam);

// Thread Functions
void ShredThread(void*);

// Functions
bool WriteLog(INT LogFileType, LPWSTR filename, LPWSTR text);
bool DeletAllTasks();
bool DeletTbTasks();
bool DeletTbAllTasks();