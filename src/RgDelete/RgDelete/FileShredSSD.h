#ifndef FILESHREDSSD_H_INCLUDED
#define FILESHREDSSD_H_INCLUDED

namespace FileShredSSD
{
	bool IsTrimEnabled();
	bool SetTrimEnable();
	bool SetFileZero(__in TCHAR filename[]);
	bool DoEnableTrim(__inout BOOL *triedTrimFlag);

	bool DoErasingSSD(__in LPWSTR path, __inout BOOL *triedTrimFlag);
	bool IsDrvSSD(__in LPWSTR path);
	bool IsWin7OrLater();
	bool IsWindowsVersionOrGreater(WORD wMajorVersion, WORD wMinorVersion, WORD wServicePackMajor);
}

#endif // FILESHREDSSD_H_INCLUDED