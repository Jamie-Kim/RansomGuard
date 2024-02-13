#ifndef RgDeleteCMD_H_INCLUDED
#define RgDeleteCMD_H_INCLUDED
#include <windows.h>

namespace RgDeleteCmd
{
	//int CmdActions();
	LPWSTR GetArg(LPWSTR* szArgList, LPWSTR opt, int argCnt);
	int CmdActions();

	BOOL RegistrySaveOptions(__in int dirErase, __in  int time, __in int eraseMethod);
	BOOL RegistryGetOptions(int *dirErase, __out int *time, __out int *eraseMethod);
}

#endif // RgDeleteCMD_H_INCLUDED