#ifndef FILESHREDEX_H_INCLUDED
#define FILESHREDEX_H_INCLUDED

namespace FileShredEx
{
	void CreateXmlForTasks(TCHAR *fileName, TCHAR *date, TCHAR *time, TCHAR *cmd, TCHAR *param);
	void GetNextDeleteDate(__in TCHAR *regular, __out TCHAR *date);
	void GetNextDeleteDateEx(__in TCHAR *regular, __in TCHAR *e_day, __in TCHAR *e_time, __out TCHAR *date);

	int GetTaskName(__in TCHAR *path, __out TCHAR *taskName);

	int DoDirectDelete(TCHAR *path, TCHAR *eraseMethod, TCHAR *taskName, bool del_dir);
	int DoDirectDeleteRegular(TCHAR *path, TCHAR *eraseMethod, TCHAR *taskName,
		TCHAR *regular, TCHAR *date, TCHAR *time, TCHAR *eraseDir, bool del_dir);

	int DoScheDelete(TCHAR *path, TCHAR *eraseMethod, TCHAR *date, TCHAR *sel_day, 
		TCHAR *time, TCHAR *eraseDir, TCHAR *regular, bool isTrashBin);

	int DoRegularDelete(TCHAR *path, TCHAR *eraseMethod, TCHAR *regular, TCHAR *time, TCHAR *eraseDir);
	int DoTrashBinDelete(TCHAR *eraseMethod, TCHAR *thDay);

	int DoTbScheDelete(TCHAR *eraseMethod, TCHAR *time, TCHAR *regular, TCHAR *thDay);
	int DoDirectDeleteTbRegular(TCHAR *eraseMethod, TCHAR *taskName, TCHAR *regular, TCHAR *time, TCHAR *thDay);
	int DoTbFolderDelete(TCHAR *path, TCHAR *eraseMethod, TCHAR *thDay, int *remainedCnt);
}

#endif // FILESHREDEX_H_INCLUDED