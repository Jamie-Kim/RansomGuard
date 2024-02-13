#ifndef __rgCrypt_H__
#define __rgCrypt_H__

#include <ntdef.h>


/*************************************************************************
Name of port used to communicate and additional defines
*************************************************************************/

//
// Port name to connect to filter.
//

#define PORT_NAME				     L"\\rgCryptPort"

//
// recommended filter altitude for encyption 
// 140000 - 149999
//

#define FLT_ALTITUDE_STR			 L"145112"

//
// Service data realted defines.
//

#define MAX_PATH				     260
#define ENCRYPTION_KEY_LENGTH	     512
#define SUPPORT_PATH_CNT		     10
#define MAX_PROTECT_PRECCESS    	 10
#define MAX_FILE_NAME_INFO_SIZE      1024
#define WRITE_PROTECTION_PID_MAX     128

//
// Send Message related defines.
//

#define MAX_OPTIONS_SIZE			 256
#define MAX_RESERVED_SIZE			 256

//
// Decryption related defines.
//

#define AES256_BLOCK_SIZE		     16
#define RC4_KEY_BYTE_LENGTH			 32
#define AES256_KEY_BYTE_LENGTH		 32

//
// Max attached volumes
//

#define MAX_ATTACHED_VOLUME			 32

//
// Max allowed pids
//

#define MAX_ALLOWED_PIDS			 256
#define MAX_BLACK_LIST_PIDS			 256

//
// ping buffer related defines.
//

#define MIN_SECTOR_SIZE				 0x64

//
// Max system pid
//

#define SYSTEM_PID	4

//
// System File first letter.
//

#define SYSTEM_FILE_FIRST_LETTER	L'$'


//
// Functions enable or disable
// Do not check filesystem for now.
// if we need it ,then uncomment the define of _CHECK_FIFLESYSTEM.
//
//#define _CHECK_FIFLESYSTEM 

/*************************************************************************
Pool Tags
*************************************************************************/

#define BUFFER_SWAP_TAG     'bdBS'
#define CONTEXT_TAG         'xcBS'
#define NAME_TAG            'mnBS'
#define PRE_2_POST_TAG      'ppBS'

/*************************************************************************
File Access flag to send nofify message to user app.
*************************************************************************/

#define DETECT_FILE_ACCESS	   (0x0001)
#define DETECT_FILE_WRITE	   (0x0002)
#define DETECT_FILE_EXCUTE	   (0x0004)
#define DETECT_FILE_READ	   (0x0008)

/*************************************************************************
Process Access result
*************************************************************************/
#define PROCESS_ACCESS_ALLOW	(0x1000)
#define PROCESS_ACCESS_DISALLOW (0x2000)

/*************************************************************************
Process Security and Access Rights
*************************************************************************/

#define PROCESS_CREATE_THREAD  (0x0002)
#define PROCESS_CREATE_PROCESS (0x0080)
#define PROCESS_TERMINATE      (0x0001)
#define PROCESS_VM_WRITE       (0x0020)
#define PROCESS_VM_READ        (0x0010)
#define PROCESS_VM_OPERATION   (0x0008)
#define PROCESS_SUSPEND_RESUME (0x0800)

/*************************************************************************
Debug tracing information
*************************************************************************/

#define LOGFL_ERRORS			0x00000001  // if set, display error messages
#define LOGFL_DEBUG				0x00000002  // if set, display DEBUG operation info
#define LOGFL_READ				0x00000004  // if set, display READ operation info
#define LOGFL_WRITE				0x00000008  // if set, display WRITE operation info
#define LOGFL_DIRCTRL			0x00000010  // if set, display DIRCTRL operation info
#define LOGFL_VOLCTX			0x00000020  // if set, display VOLCTX operation info
#define LOGFL_CRYPT				0x00000040  // if set, display LOGFL_CRYPT operation info
#define LOGFL_WRITE_PRT			0x00000080  // if set, display LOGFL_WRITE_PRT operation info
#define LOGFL_FILTER_PRT		0x00000100  // if set, display LOGFL_FILTER_PRT operation info
#define LOGFL_PID_PRT			0x00000200  // if set, display LOGFL_PID_PRT operation info
#define LOGFL_SEND_MSG			0x00000400  // if set, display LOGFL_SEND_MSG operation info

#define LOG_PRINT( _logFlag, _string )                        \
    (FlagOn(LoggingFlags,(_logFlag)) ?                        \
        DbgPrint _string  :                                   \
        ((int)0))

/*************************************************************************
Customized Local structures
*************************************************************************/

typedef struct _FILTER_DATA {

	PDRIVER_OBJECT DriverObject;
	PFLT_FILTER Filter;
	PFLT_PORT ServerPort;
	PEPROCESS UserProcess;
	PFLT_PORT ClientPort;

} FILTER_DATA, *PFILTER_DATA;

typedef struct _SERVICE_DATA {

	//
	//  Auto decryption when file read
	//

	BOOLEAN ReadDecryptionEnable;

	//
	//  Auto encryption when file read
	//

	BOOLEAN WriteEncryptionEnable;

	//
	//  Copy protection by file name in security folder
	//

	BOOLEAN CopyProtectEnable;

	//
	//  Readonly Eanbled.
	//

	BOOLEAN ReadOnlyEnable;

	//
	//  Filter protect from unloading it during the running application(filter connected). 
	//

	BOOLEAN FilterProtectEnable;

	//
	//  Send file copy action info. to the application in case of the secured folder. 
	//

	BOOLEAN CopyActionNotifyEnable;

	//
	//  Predefined encryption method,  0: simple encryption with XOR, 1: AES256
	//

	INT EncryptionType;

	//
	// Set count of the secured folder.
	//

	INT SecurityPathCount;

	//
	// Accessed app's PID.
	//

	INT AppPid;

	//
	// Encryption Key.
	//

	UCHAR EncryptionKey[ENCRYPTION_KEY_LENGTH];

	//
	// Security paths.
	//

	WCHAR SecurityPath[SUPPORT_PATH_CNT][MAX_PATH];

}SERVICE_DATA, *PSERVICE_DATA;


typedef struct _STREAM_HANDLE_CONTEXT {

	BOOLEAN RescanRequired;

} STREAM_HANDLE_CONTEXT, *PSTREAM_HANDLE_CONTEXT;


typedef struct _CREATE_PARAMS {

	WCHAR String;

} CREATE_PARAMS, *PCREATE_PARAMS;

#endif //  __rgCrypt_H__

/*************************************************************************
Message data to send and replay
*************************************************************************/

typedef struct _MSG_SEND_DATA {

	//
	// Command
	//

	INT Command;

	//
	// PID related to the command
	//

	INT Pid;

	//
	// File Path which is accessed by the pid
	//

	WCHAR FilePath[MAX_PATH];

	WCHAR Contents_Reserved[MAX_PATH];

} MSG_SEND_DATA, *PMSG_SEND_DATA;


typedef struct _REPLY_DATA {

	//
	// Command
	//

	INT Command;

	//
	// PID related to the command
	//

	INT Pid;

	//
	// Reserved fields for future
	//

	UCHAR Options[MAX_OPTIONS_SIZE];

	UCHAR Reserved[MAX_RESERVED_SIZE];

} REPLY_DATA, *PREPLY_DATA;


typedef struct _NOTIFICATION {

	ULONG BytesToScan;
	ULONG Reserved;             // for quad-word alignement of the Contents structure

	INT NotifyType;

	MSG_SEND_DATA Contents;

} NOTIFICATION, *PNOTIFICATION;

/*************************************************************************
Supported Message Command
*************************************************************************/
typedef enum
{
	None = 0,
	CheckPidtoWrite = 1,
	CheckPidtoRead = 2,
	CheckPidtoAccess = 3,

	MaxCommand
} MessageCommand;


/*************************************************************************
Local structures
*************************************************************************/

//
//  This is a volume context, one of these are attached to each volume
//  we monitor.  This is used to get a "DOS" name for debug display.
//

typedef struct _VOLUME_CONTEXT {

	//
	//  Holds the name to display
	//

	UNICODE_STRING Name;

	//
	//  Holds the sector size for this volume.
	//

	ULONG SectorSize;

} VOLUME_CONTEXT, *PVOLUME_CONTEXT;


//
//  This is a context structure that is used to pass state from our
//  pre-operation callback to our post-operation callback.
//

typedef struct _PRE_2_POST_CONTEXT {

	//
	//  Pointer to our volume context structure.  We always get the context
	//  in the preOperation path because you can not safely get it at DPC
	//  level.  We then release it in the postOperation path.  It is safe
	//  to release contexts at DPC level.
	//

	PVOLUME_CONTEXT VolCtx;

	//
	//  Since the post-operation parameters always receive the "original"
	//  parameters passed to the operation, we need to pass our new destination
	//  buffer to our post operation routine so we can free it.
	//

	PVOID SwappedBuffer;

} PRE_2_POST_CONTEXT, *PPRE_2_POST_CONTEXT;


/*************************************************************************
callback for process protection
*************************************************************************/

typedef struct _OB_REG_CONTEXT {

	IN USHORT Version;

	IN UNICODE_STRING Altitude;

	IN USHORT ulIndex;

	OB_OPERATION_REGISTRATION *OperationRegistration;

} REG_CONTEXT, *PREG_CONTEXT;


OB_PREOP_CALLBACK_STATUS ObjectPreCallback(

	IN  PVOID RegistrationContext,

	IN  POB_PRE_OPERATION_INFORMATION OperationInformation

);

typedef PCHAR(*GET_PROCESS_IMAGE_NAME) (PEPROCESS Process);


/*************************************************************************
Prototypes
*************************************************************************/
NTSTATUS
InstanceSetup(
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ FLT_INSTANCE_SETUP_FLAGS Flags,
_In_ DEVICE_TYPE VolumeDeviceType,
_In_ FLT_FILESYSTEM_TYPE VolumeFilesystemType
);

VOID
CleanupVolumeContext(
_In_ PFLT_CONTEXT Context,
_In_ FLT_CONTEXT_TYPE ContextType
);

NTSTATUS
InstanceQueryTeardown(
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ FLT_INSTANCE_QUERY_TEARDOWN_FLAGS Flags
);

DRIVER_INITIALIZE DriverEntry;
NTSTATUS
DriverEntry(
_In_ PDRIVER_OBJECT DriverObject,
_In_ PUNICODE_STRING RegistryPath
);

NTSTATUS
FilterUnload(
_In_ FLT_FILTER_UNLOAD_FLAGS Flags
);

VOID
ReadDriverParameters(
_In_ PUNICODE_STRING RegistryPath
);

NTSTATUS
PortConnect(
_In_ PFLT_PORT ClientPort,
_In_opt_ PVOID ServerPortCookie,
_In_reads_bytes_opt_(SizeOfContext) PVOID ConnectionContext,
_In_ ULONG SizeOfContext,
_Outptr_result_maybenull_ PVOID *ConnectionCookie
);

VOID
PortDisconnect(
_In_opt_ PVOID ConnectionCookie
);

/*************************************************************************
 MJ callback functions
*************************************************************************/
FLT_PREOP_CALLBACK_STATUS
PreCreate(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_Flt_CompletionContext_Outptr_ PVOID *CompletionContext
);

FLT_POSTOP_CALLBACK_STATUS
PostCreate(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
);

FLT_PREOP_CALLBACK_STATUS
PreReadBuffers(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_Flt_CompletionContext_Outptr_ PVOID *CompletionContext
);

FLT_POSTOP_CALLBACK_STATUS
PostReadBuffers(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
);

FLT_PREOP_CALLBACK_STATUS
PreWriteBuffers(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_Flt_CompletionContext_Outptr_ PVOID *CompletionContext
);

FLT_POSTOP_CALLBACK_STATUS
PostWriteBuffers(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
);

FLT_PREOP_CALLBACK_STATUS
PreCleanup(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_Flt_CompletionContext_Outptr_ PVOID *CompletionContext
);

BOOLEAN
SetMsgData(
_In_ PMSG_SEND_DATA pMsgData,
_In_ INT Pid,
_In_ PFLT_FILE_NAME_INFORMATION pNameCtx,
_In_ PVOLUME_CONTEXT pVolCtx,
_In_ ULONG accessFlag
);

INT
SendLogMessage(
_In_ PFILTER_DATA pFilterData,
_In_ PMSG_SEND_DATA MsgData,
_In_ INT NotifyType
);

/*************************************************************************
Global values
*************************************************************************/

extern ULONG LoggingFlags;
extern FAST_MUTEX CryptoMutex;