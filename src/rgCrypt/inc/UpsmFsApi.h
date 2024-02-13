
#ifndef __UPSMFS_API_H__
#define __UPSMFS_API_H__

#include "rgCrypt.h"

//
//This message type should be defined as same in service,client and driver.
//
typedef enum {
	//message from app
	DeviceResEnable = 1,
	SecurityResEnable=2,
	DeviceReadOnlyEnable = 3,

	DeviceSecurityFolderEnable = 4,
	CopyActionNotifyEnable = 5,
	OpenActionNotifyEnable = 6,

	ChangeSecurityFolder = 10,
	ChangeDevices = 11,

	ProgramLoaded = 14,
	ProgramExit = 15,

	//message from flter
	SendFileCopyFromLog = 50,
	SendFileCopyToLog = 51,
	SendFileOpenLog = 52,
	SendFileMoveFromLog = 53,

	MAX_MESSAGE_TYPE
} MESSAGE_TYPE;


NTSTATUS 
UpsmFsInitialize(
	_In_ PUNICODE_STRING RegistryPath
	);

VOID 
UpsmFsFree();

NTSTATUS 
UpsmFsAllocateUnicodeString(
	_Inout_ PUNICODE_STRING String
	);

VOID 
UpsmFsFreeUnicodeString(
	_Inout_ PUNICODE_STRING String
	);

BOOLEAN
IsResVolume(
_In_ PUNICODE_STRING ExDeviceVolumes,
_In_ PUNICODE_STRING Volume,
_In_ PUPSM_SERVICE_DATA ServiceData
);

BOOLEAN
IsResFolder(
_In_ PFLT_FILE_NAME_INFORMATION CurrNameInfo,
_In_ PUNICODE_STRING FolderName,
_In_ PUNICODE_STRING SePath,
_In_ PUPSM_SERVICE_DATA ServiceData
);

BOOLEAN 
UpsmFsCheckCopy(
	_In_ PFLT_FILE_NAME_INFORMATION CurrNameInfo,
	_In_ PFLT_FILE_NAME_INFORMATION PreNameInfo,
	_In_ PUPSM_SERVICE_DATA ServiceData,
	_In_ BOOLEAN IsSecurityFile,
	_In_ BOOLEAN WasSecurityFile
	);

UNICODE_STRING
GetFullPath(
	_In_ PFLT_FILE_NAME_INFORMATION NameInfo
	);

BOOLEAN
IsSecurityFileOpen(
	_In_ PFLT_FILE_NAME_INFORMATION CurNameInfo,
	_In_ PUNICODE_STRING SePath,
	_In_ PUPSM_SERVICE_DATA ServiceData
	);

BOOLEAN
IsSecurityFile(
	_In_ PFLT_FILE_NAME_INFORMATION NameInfo,
	_In_ PUNICODE_STRING SePath,
	_In_ PUPSM_SERVICE_DATA ServiceData
	);

BOOLEAN
IsExternalDevice(
	_In_ PUNICODE_STRING Volumes,
	_In_ PUNICODE_STRING Volume,
	_In_ TCHAR VolListSeperator
	);

NTSTATUS
SendLogMessage(
	_In_ PUNICODE_STRING Contents,
	_In_ PUNICODE_STRING Contents_Reserved,
	_In_ INT NotifyType
	);

#endif /* __UPSMFS_API_H__ */
