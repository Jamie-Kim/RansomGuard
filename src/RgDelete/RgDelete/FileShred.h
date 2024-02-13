#ifndef FILESHRED_H_INCLUDED
#define FILESHRED_H_INCLUDED

//#define MAX_BUFFER_SIZE 1048576 // 1 megabyte
#define MAX_BUFFER_SIZE 524288 // 1/2 megabyte

namespace FileShred
{
	// Handles to allow the main program to communicate with the shredding functions
	extern HWND hProgDlg;
	extern HANDLE hStopMutex;
	extern BOOL bTriedTrimEnableFlag;

    // Shredding functions
	int ShredFile(TCHAR filename[], bool(*OverWriteMethod)(HANDLE&));
	int ShredDir(TCHAR dirname[], bool(*OverWriteMethod)(HANDLE&), bool del_dir);
	int DelDir(TCHAR dirname[], bool del_dir);

	int ShredTrashBin(bool(*OverWriteMethod)(HANDLE&));

    // Overwrite functions
	bool OverWrite0s(HANDLE& hFile);
	bool OverWriteRand(HANDLE& hFile);
	bool OverWriteDOD3(HANDLE& hFile);
	bool OverWriteDOD7(HANDLE& hFile);
	bool OverWriteGut(HANDLE& hFile);
	bool OverWriteNone(HANDLE& hFile);
}

#endif // FILESHRED_H_INCLUDED
