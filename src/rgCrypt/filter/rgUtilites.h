
#ifndef __ORICOM_API_H__
#define __ORICOM_API_H__

#include <fltKernel.h>

/*************************************************************************
 Enum Types
*************************************************************************/

//
// Crypto action
//

typedef enum
{
	DECRYPT = 0,
	ENCRYPT = 1

} CryptoAction;

/*************************************************************************
Prototypes
*************************************************************************/

VOID
CopyUnicodeString(
_In_ PUNICODE_STRING des,
_In_ PUNICODE_STRING src
);

BOOLEAN
IsSecurityFileEx(
_In_ PFLT_FILE_NAME_INFORMATION NameInfo,
_In_ PUNICODE_STRING pSePathArray,
_In_ INT SePathCnt
);

BOOLEAN
IsSystemFile(
_In_ PFLT_FILE_NAME_INFORMATION NameInfo,
_In_ INT Pid,
_In_ PFLT_CALLBACK_DATA Data
);

BOOLEAN
IsSecurityFile(
_In_ PFLT_FILE_NAME_INFORMATION NameInfo,
_In_ PUNICODE_STRING SePath
);

INT
GetProcessId(
_In_ PFLT_CALLBACK_DATA Data
);

BOOLEAN
IsProcessProtected(
_In_ INT processId
);

VOID
ResetWriteProtectedPid(
);

VOID
PrintWriteProtectedPID(
);

INT
AddProtectedProcess(
_In_ INT processId,
_In_ INT exceptId
);

BOOLEAN
IsExitProtectedPID(
_In_ INT CurPid,
_In_ INT *pExitProtectedPids,
_In_ INT ProtectedPidCnt
);

VOID
DetachAllVolume(
_In_ PFLT_FILTER Filter
);

/*************************************************************************
 Related functions for encryption and decryption
*************************************************************************/
VOID
CryptoInit(
_In_   INT EncryptionType,
IN	   PUCHAR  pEncryptionKey
);

/*************************************************************************
 Decryption related functions
*************************************************************************/

VOID
CryptoDataClose(
_In_   INT EncryptionType
);

BOOLEAN
CryptoData(
_In_   INT EncryptionType,
_In_   PFLT_CALLBACK_DATA Data,
_In_   PCFLT_RELATED_OBJECTS FltObjects,
IN OUT PUCHAR  pBuf,
IN     ULONG   ReadBufferLength,
IN     BOOLEAN isEncryption
);

BOOLEAN
CryptoDataAes256(
_In_   PFLT_CALLBACK_DATA Data,
_In_   PCFLT_RELATED_OBJECTS FltObjects,
IN OUT PUCHAR  pBuf,
IN     ULONG   ReadBufferLength,
IN     BOOLEAN isEncryption
);

BOOLEAN
CryptoDataRC4(
_In_   PFLT_CALLBACK_DATA Data,
IN OUT PUCHAR  pBuf,
IN     ULONG   FileSize,
IN     BOOLEAN isEncryption
);

#endif /* __ORICOM_API_H__ */
