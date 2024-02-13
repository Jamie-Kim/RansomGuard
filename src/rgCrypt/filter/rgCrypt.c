/*
*   rgCrypt Filter driver.
*	By Jamie Kim
*/

//
// Macro for getting the path name
//

#include <fltKernel.h>
#include <dontuse.h>
#include <suppress.h>
#include <ntddk.h>

#include "rgCrypt.h"
#include "rgSafeRoutines.h"
#include "rgUtilites.h"

#pragma prefast(disable:__WARNING_ENCODE_MEMBER_FUNCTION_POINTER, "Not valid for kernel mode drivers")

/*************************************************************************
Grobal Values
*************************************************************************/

BOOLEAN needWrite = TRUE;

//
// Filter and service data
//

FILTER_DATA gFilterData;
SERVICE_DATA gServiceData;
MSG_SEND_DATA gMsgData = { 0 };

//
// Filter info to attache the volume
//
PFLT_VOLUME gPFilterVolume;
PFLT_INSTANCE gPFilterVolInstance;

//
// Array for Security Pathes
//

UNICODE_STRING gSecurityPath[SUPPORT_PATH_CNT] = { 0 };
UNICODE_STRING gVolumeInstance = RTL_CONSTANT_STRING(L"rgCrypt");
UNICODE_STRING gAltitude = RTL_CONSTANT_STRING(L"145113");

//
// string to save previous name in preCreate
//

UNICODE_STRING prePurgedFileName = { 0 };
WCHAR prePurgedNameBuffer[MAX_PATH] = { 0 };


//to check allowed Pids
INT gAllowedPids[MAX_ALLOWED_PIDS] = { 0 };
INT gBlackListPids[MAX_BLACK_LIST_PIDS] = { 0 };

INT gAllowedPidCnt = 0;
INT gBlackListPidCnt = 0;

//
//for the copy protection or copy action to save log.
//

PVOID gPreNameInfo = NULL;
BOOLEAN gPreNameInfoSaved = FALSE;

//
//  This is a lookAside list used to allocate our pre-2-post structure.
//

NPAGED_LOOKASIDE_LIST Pre2PostContextList;

//
// Mutex for file IO delay in case of AES256 decryption. 
//

FAST_MUTEX CryptoMutex = { 0 };

typedef enum {

	WRITE_ALLOW = 0x01,
	WRITE_DISALLOW = 0x02,
	WRITE_WATCH = 0x03

} command_type_receive;

//
//  Assign text sections for each routine.
//

#ifdef ALLOC_PRAGMA
#pragma alloc_text(INIT, DriverEntry)
#pragma alloc_text(INIT, ReadDriverParameters)
#pragma alloc_text(PAGE, InstanceSetup)
#pragma alloc_text(PAGE, InstanceQueryTeardown)
#pragma alloc_text(PAGE, FilterUnload)
#pragma alloc_text(PAGE, PortConnect)
#pragma alloc_text(PAGE, PortDisconnect)
#endif

//
//  Operation we currently care about.
//

CONST FLT_OPERATION_REGISTRATION Callbacks[] = {

	{ IRP_MJ_CREATE,
	0,
	PreCreate,
	PostCreate,
	},

	{ IRP_MJ_READ,
	0,
	PreReadBuffers,
	PostReadBuffers
	},

	{ IRP_MJ_WRITE,
	0,
	PreWriteBuffers,
	PostWriteBuffers
	},

	{ IRP_MJ_CLEANUP,
	0,
	PreCleanup,
	NULL 
	},

	{ IRP_MJ_OPERATION_END }
};

//
//  Context definitions we currently care about.  Note that the system will
//  create a lookAside list for the volume context because an explicit size
//  of the context is specified.
//

CONST FLT_CONTEXT_REGISTRATION ContextNotifications[] = {

	{ FLT_VOLUME_CONTEXT,
	0,
	CleanupVolumeContext,
	sizeof(VOLUME_CONTEXT),
	CONTEXT_TAG },

	{ FLT_CONTEXT_END }
};

//
//  This defines what we want to filter with FltMgr
//

CONST FLT_REGISTRATION FilterRegistration = {

	sizeof(FLT_REGISTRATION),         //  Size
	FLT_REGISTRATION_VERSION,           //  Version
	0,                                  //  Flags

	ContextNotifications,               //  Context
	Callbacks,                          //  Operation callbacks

	FilterUnload,                       //  MiniFilterUnload

	InstanceSetup,                      //  InstanceSetup
	InstanceQueryTeardown,              //  InstanceQueryTeardown
	NULL,                               //  InstanceTeardownStart
	NULL,                               //  InstanceTeardownComplete

	NULL,                               //  GenerateFileName
	NULL,                               //  GenerateDestinationFileName
	NULL                                //  NormalizeNameComponent

};

/*************************************************************************
Debug tracing information
*************************************************************************/
//
//  Definitions to display log messages.  The registry DWORD entry:
//  "hklm\system\CurrentControlSet\Services\buffers\DebugFlags" defines
//  the default state of these logging flags
//

ULONG LoggingFlags = {
//LOGFL_CRYPT |
//LOGFL_DEBUG |
//LOGFL_WRITE_PRT |
//LOGFL_PID_PRT |
//LOGFL_FILTER_PRT |
//LOGFL_DIRCTRL	|
//LOGFL_VOLCTX |
//LOGFL_WRITE |
//LOGFL_READ |
//LOGFL_SEND_MSG |
	LOGFL_ERRORS
};

//////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////
//
//                      Routines
//
//////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////

NTSTATUS
InstanceSetup(
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ FLT_INSTANCE_SETUP_FLAGS Flags,
_In_ DEVICE_TYPE VolumeDeviceType,
_In_ FLT_FILESYSTEM_TYPE VolumeFilesystemType
)
{
	PDEVICE_OBJECT devObj = NULL;
	PVOLUME_CONTEXT ctx = NULL;
	NTSTATUS status = STATUS_SUCCESS;
	ULONG retLen;
	PUNICODE_STRING workingName;
	USHORT size;
	UCHAR volPropBuffer[sizeof(FLT_VOLUME_PROPERTIES) + 512];
	PFLT_VOLUME_PROPERTIES volProp = (PFLT_VOLUME_PROPERTIES)volPropBuffer;

	PAGED_CODE();

	UNREFERENCED_PARAMETER(Flags);
	UNREFERENCED_PARAMETER(VolumeDeviceType);
	UNREFERENCED_PARAMETER(VolumeFilesystemType);

	try {

		//
		//  Allocate a volume context structure.
		//

		status = FltAllocateContext(FltObjects->Filter,
			FLT_VOLUME_CONTEXT,
			sizeof(VOLUME_CONTEXT),
			NonPagedPool,
			&ctx);

		if (!NT_SUCCESS(status)) {

			//
			//  We could not allocate a context, quit now
			//

			leave;
		}

		//
		//  Always get the volume properties, so I can get a sector size
		//

		status = FltGetVolumeProperties(FltObjects->Volume,
			volProp,
			sizeof(volPropBuffer),
			&retLen);

		if (!NT_SUCCESS(status)) {

			leave;
		}

		//
		//  Save the sector size in the context for later use.  Note that
		//  we will pick a minimum sector size if a sector size is not
		//  specified.
		//

		FLT_ASSERT((volProp->SectorSize == 0) || (volProp->SectorSize >= MIN_SECTOR_SIZE));

		ctx->SectorSize = max(volProp->SectorSize, MIN_SECTOR_SIZE);

		//
		//  Init the buffer field (which may be allocated later).
		//

		ctx->Name.Buffer = NULL;

		//
		//  Get the storage device object we want a name for.
		//

		status = FltGetDiskDeviceObject(FltObjects->Volume, &devObj);

		if (NT_SUCCESS(status)) {

			//
			//  Try and get the DOS name.  If it succeeds we will have
			//  an allocated name buffer.  If not, it will be NULL
			//

#pragma prefast(suppress:__WARNING_USE_OTHER_FUNCTION, "Used to maintain compatability with Win 2k")
			status = RtlVolumeDeviceToDosName(devObj, &ctx->Name);
		}

		//
		//  If we could not get a DOS name, get the NT name.
		//

		if (!NT_SUCCESS(status)) {

			FLT_ASSERT(ctx->Name.Buffer == NULL);

			//
			//  Figure out which name to use from the properties
			//

			if (volProp->RealDeviceName.Length > 0) {

				workingName = &volProp->RealDeviceName;

			}
			else if (volProp->FileSystemDeviceName.Length > 0) {

				workingName = &volProp->FileSystemDeviceName;

			}
			else {

				//
				//  No name, don't save the context
				//

				status = STATUS_FLT_DO_NOT_ATTACH;
				leave;
			}

			//
			//  Get size of buffer to allocate.  This is the length of the
			//  string plus room for a trailing colon.
			//

			size = workingName->Length + sizeof(WCHAR);

			//
			//  Now allocate a buffer to hold this name
			//

#pragma prefast(suppress:__WARNING_MEMORY_LEAK, "ctx->Name.Buffer will not be leaked because it is freed in CleanupVolumeContext")
			ctx->Name.Buffer = ExAllocatePoolWithTag(NonPagedPool,
													 size,
													 NAME_TAG);

			if (ctx->Name.Buffer == NULL) {

				status = STATUS_INSUFFICIENT_RESOURCES;
				leave;
			}

			//
			//  Init the rest of the fields
			//

			ctx->Name.Length = 0;
			ctx->Name.MaximumLength = size;

			//
			//  Copy the name in
			//

			RtlCopyUnicodeString(&ctx->Name, workingName);

			//
			//  Put a trailing colon to make the display look good
			//

			RtlAppendUnicodeToString(&ctx->Name, L":");
		}

		//
		//  Set the context
		//

		status = FltSetVolumeContext(FltObjects->Volume,
									 FLT_SET_CONTEXT_KEEP_IF_EXISTS,
									 ctx,
									 NULL);

		//
		//  Log debug info
		//

		LOG_PRINT(LOGFL_VOLCTX,
			("rgCrypt!InstanceSetup: Real SectSize=0x%04x, Used SectSize=0x%04x, Name=\"%wZ\"\n",
			volProp->SectorSize,
			ctx->SectorSize,
			&ctx->Name));

		//
		//  It is OK for the context to already be defined.
		//

		if (status == STATUS_FLT_CONTEXT_ALREADY_DEFINED) {

			status = STATUS_SUCCESS;
		}

	}
	finally {

		//
		//  Always release the context.  If the set failed, it will free the
		//  context.  If not, it will remove the reference added by the set.
		//  Note that the name buffer in the ctx will get freed by the context
		//  cleanup routine.
		//

		if (ctx) {

			FltReleaseContext(ctx);
		}

		//
		//  Remove the reference added to the device object by
		//  FltGetDiskDeviceObject.
		//

		if (devObj) {

			ObDereferenceObject(devObj);
		}
	}

	return status;
}

VOID
CleanupVolumeContext(
_In_ PFLT_CONTEXT Context,
_In_ FLT_CONTEXT_TYPE ContextType
)
{
	PVOLUME_CONTEXT ctx = Context;

	UNREFERENCED_PARAMETER(ContextType);

	FLT_ASSERT(ContextType == FLT_VOLUME_CONTEXT);

	if (ctx->Name.Buffer != NULL) {

		ExFreePool(ctx->Name.Buffer);
		ctx->Name.Buffer = NULL;
	}
}

NTSTATUS
InstanceQueryTeardown(
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ FLT_INSTANCE_QUERY_TEARDOWN_FLAGS Flags
)
{
	PAGED_CODE();

	UNREFERENCED_PARAMETER(FltObjects);
	UNREFERENCED_PARAMETER(Flags);

	return STATUS_SUCCESS;
}


/*************************************************************************
Initialization and unload routines.
*************************************************************************/

NTSTATUS
DriverEntry(
_In_ PDRIVER_OBJECT DriverObject,
_In_ PUNICODE_STRING RegistryPath
)
{
	OBJECT_ATTRIBUTES oa;
	UNICODE_STRING portNameStr;
	PSECURITY_DESCRIPTOR sd = NULL;
	NTSTATUS status = 0;

	LOG_PRINT(LOGFL_DEBUG, ("rgCrypt! FILTER is loading: \n"));

	try
	{
		//
		//  Default to NonPagedPoolNx for non paged pool allocations where supported.
		//

		ExInitializeDriverRuntime(DrvRtPoolNxOptIn);

		//
		//  Get debug trace flags
		//

		ReadDriverParameters(RegistryPath);

		//
		//  Init lookaside list used to allocate our context structure used to
		//  pass information from out preOperation callback to our postOperation
		//  callback.
		//

		ExInitializeNPagedLookasideList(&Pre2PostContextList,
			NULL,
			NULL,
			0,
			sizeof(PRE_2_POST_CONTEXT),
			PRE_2_POST_TAG,
			0);

		//
		//  Register with FltMgr
		//

		status = FltRegisterFilter(DriverObject,
			&FilterRegistration,
			&gFilterData.Filter);

		if (!NT_SUCCESS(status)) {

			leave;
		}

		//
		//  Create a communication port.
		//

		RtlInitUnicodeString(&portNameStr, PORT_NAME);

		//
		//  We secure the port user app can acecss it.
		//

		status = FltBuildDefaultSecurityDescriptor(&sd, FLT_PORT_ALL_ACCESS);

		if (!NT_SUCCESS(status)) {

			leave;
		}

		//
		// Security Set
		//

		status = RtlSetDaclSecurityDescriptor(sd, TRUE, NULL, TRUE);

		if (!NT_SUCCESS(status)) {

			leave;
		}

		InitializeObjectAttributes(&oa,
			&portNameStr,
			OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE,
			NULL,
			sd);

		status = FltCreateCommunicationPort(gFilterData.Filter,
			&gFilterData.ServerPort,
			&oa,
			NULL,
			PortConnect,
			PortDisconnect,
			NULL,
			1);

		if (!NT_SUCCESS(status)) {

			leave;
		}

		//
		//  Free the security descriptor in all cases. It is not needed once
		//  the call to FltCreateCommunicationPort() is made.
		//

		FltFreeSecurityDescriptor(sd);

		//
		//  Memory allocation for saving pre-name Info before filtering I/O.
		//

		gPreNameInfo = ExAllocatePool(NonPagedPool, MAX_FILE_NAME_INFO_SIZE);

		if (gPreNameInfo == NULL) {

			LOG_PRINT(LOGFL_ERRORS,
				("rgCrypt!DriverEntry: Failed to allocate %d bytes of memory\n",
				MAX_FILE_NAME_INFO_SIZE));

			leave;
		}

		//
		//  Start filtering i/o
		//

		status = FltStartFiltering(gFilterData.Filter);

		if (!NT_SUCCESS(status)) {
			
			leave;
		}

		//
		//  Detach all filters attached to the volumes
		//

		//skip for testing.
		//DetachAllVolume(gFilterData.Filter);

		LOG_PRINT(LOGFL_DEBUG, ("rgCrypt! FILTER WAS LOADED Correctly"));
	}
	finally {

		if (!NT_SUCCESS(status)) {

			if (gFilterData.ServerPort != NULL) {

				FltCloseCommunicationPort(gFilterData.ServerPort);
			}

			if (gFilterData.Filter != NULL) {

				FltUnregisterFilter(gFilterData.Filter);
			}

			if (gPreNameInfo != NULL) {

				ExFreePool(gPreNameInfo);
			}

			ExDeleteNPagedLookasideList(&Pre2PostContextList);
		}
	}

	return status;
}

NTSTATUS
FilterUnload(
_In_ FLT_FILTER_UNLOAD_FLAGS Flags
)
{
	PAGED_CODE();

	UNREFERENCED_PARAMETER(Flags);

	//
	//	disable to detach when port is once connected with an option of the filter protection
	//

	if (gServiceData.FilterProtectEnable == TRUE && 
		gFilterData.ClientPort != NULL){

		return STATUS_FLT_DO_NOT_DETACH;
	}

	//
	//  Close the server port.
	//

	FltCloseCommunicationPort(gFilterData.ServerPort);

	//
	//  Unregister from FLT mgr
	//

	FltUnregisterFilter(gFilterData.Filter);

	//
	//  Release allocated memory during the filter loading.
	//

	if (gPreNameInfo != NULL)
	{
		ExFreePool(gPreNameInfo);
	}

	//
	//  Delete lookaside list
	//

	ExDeleteNPagedLookasideList(&Pre2PostContextList);
	
	LOG_PRINT(LOGFL_FILTER_PRT, ("rgCrypt!FilterUnload: Filter is unloaded successfully."));

	return STATUS_SUCCESS;
}


/*************************************************************************
MiniFilter callback routines.
*************************************************************************/

FLT_PREOP_CALLBACK_STATUS
PreCreate(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_Flt_CompletionContext_Outptr_ PVOID *CompletionContext
)
{
	FLT_PREOP_CALLBACK_STATUS retValue = FLT_PREOP_SUCCESS_NO_CALLBACK;
	PFLT_FILE_NAME_INFORMATION nameInfo = NULL;
	BOOLEAN isSecurityPath = FALSE;
	NTSTATUS status;
	PFLT_IO_PARAMETER_BLOCK iopb = Data->Iopb;

	ULONG AccessFlag = iopb->Parameters.Create.SecurityContext->DesiredAccess;
	BOOLEAN cacheConWrite = 0;
	BOOLEAN cacheConRead = 0;

	BOOLEAN writeAccess = 0;

	INT curPid = 0;
	PVOLUME_CONTEXT volCtx = NULL;
	INT msgResult = 0;

	try {

		//
		//  If not client port just ignore this write.
		//

		if (gFilterData.ClientPort == NULL) {

			leave;
		}

		//
		// Get current file information
		//

		status = FltGetFileNameInformation(Data,
			FLT_FILE_NAME_NORMALIZED ,
			&nameInfo);

		if (!NT_SUCCESS(status)) {

			leave;
		}

		FltParseFileNameInformation(nameInfo);

		//
		// Get current process ID
		//

		curPid = GetProcessId(Data);

		//
		// We skip PID less than 5 , or RansomGuard app
		//

		if (curPid <= 4 || curPid == gServiceData.AppPid) {

			leave;
		}

		//
		// Check the file is in security PATH
		//

		isSecurityPath = IsSecurityFileEx(nameInfo, gSecurityPath, gServiceData.SecurityPathCount);

		if (!isSecurityPath) {

			leave;
		}

		//
		// Log notification
		//

		if (gServiceData.CopyActionNotifyEnable)
		{
			//
			// Send message related values
			//

			//
			//  Get our volume context so we can display our volume name 
			//

			status = FltGetVolumeContext(FltObjects->Filter,
				FltObjects->Volume,
				&volCtx);

			if (!NT_SUCCESS(status)) {

				LOG_PRINT(LOGFL_ERRORS, 
					("rgCrypt!PreCreate: Error getting volume context, status=%x\n", 
					status));

				leave;
			}

			//
			// Send message
			//

			if (SetMsgData(&gMsgData, curPid, nameInfo, volCtx, AccessFlag))
			{
				msgResult = SendLogMessage(&gFilterData, &gMsgData, DETECT_FILE_ACCESS);
			}
		}

		//
		// Copy protection
		//

		if (gServiceData.CopyProtectEnable)
		{
			if (!IsProcessProtected(curPid)) 
			{

				//
				// result
				//

				switch (msgResult)
				{
					case PROCESS_ACCESS_ALLOW:
						AddProtectedProcess(curPid, 0);
						LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PreCreate: PID - Allowed [%d] ,Cnt : %d", curPid, gAllowedPidCnt));
						break;

						/*
						case PROCESS_ACCESS_DISALLOW:
						Data->IoStatus.Status = STATUS_ACCESS_DENIED;
						Data->IoStatus.Information = 0;
						retValue = FLT_PREOP_COMPLETE;

						LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PreCreate: copyprotection - protected %x ", msgResult));
						leave;
						*/

					default:
						Data->IoStatus.Status = STATUS_ACCESS_DENIED;
						Data->IoStatus.Information = 0;
						retValue = FLT_PREOP_COMPLETE;

						LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PreCreate: copyprotection - protected %x ", msgResult));
						leave;
						break;
				}
			}
		}

		//
		// Routine ReadOnly
		//

		if (gServiceData.ReadOnlyEnable) 
		{
			//
			// Get operation types
			//

			writeAccess = (AccessFlag & (DELETE |
										 WRITE_OWNER |
										 WRITE_DAC |
										 FILE_WRITE_EA |
										 FILE_APPEND_DATA |
										 FILE_WRITE_ATTRIBUTES)) != 0;

			//
			// Allow only read operation
			//

			if (writeAccess) {

				Data->IoStatus.Status = STATUS_ACCESS_DENIED;
				Data->IoStatus.Information = 0;

				retValue = FLT_PREOP_COMPLETE;
				leave;
			}
		}

		//
		//  Routine Write encryption
		//

		if (gServiceData.WriteEncryptionEnable) 
		{
			//
			// Skip if it is system related files.
			//

			if (IsSystemFile(nameInfo, curPid, Data)) {

				leave;
			}

			cacheConWrite = (AccessFlag & (STANDARD_RIGHTS_WRITE | FILE_WRITE_EA | FILE_APPEND_DATA | SYNCHRONIZE)) ==
				(STANDARD_RIGHTS_WRITE | FILE_WRITE_EA | FILE_APPEND_DATA | SYNCHRONIZE);

			if (!cacheConWrite) {

				leave;
			}

			//
			// Get operation types
			//
			/*
			cacheConRead = (AccessFlag & (FILE_READ_ATTRIBUTES | FILE_READ_EA | FILE_READ_DATA)) ==
				(FILE_READ_ATTRIBUTES | FILE_READ_EA | FILE_READ_DATA);

			if (!cacheConRead) {

				leave;
			}
			*/

			//
			// save it to pre purged cahce file
			//

			if (RtlEqualUnicodeString(&nameInfo->Name, &prePurgedFileName, TRUE)) {

				leave;
			}
			else {

				CopyUnicodeString(&prePurgedFileName, &nameInfo->Name);
			}

			//
			// Run callback process to purge the cache of the file
			//

			retValue = FLT_PREOP_SUCCESS_WITH_CALLBACK;
		}
	}
	finally {

		//
		//  Cleanup.
		//

		if (nameInfo != NULL) {

			FltReleaseFileNameInformation(nameInfo);
		}

		if (volCtx != NULL) {

			FltReleaseContext(volCtx);
		}
	}

	return retValue;
}

FLT_POSTOP_CALLBACK_STATUS
PostCreate(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
)
{
	PFLT_IO_PARAMETER_BLOCK iopb = Data->Iopb;
	FLT_POSTOP_CALLBACK_STATUS retValue = FLT_POSTOP_FINISHED_PROCESSING;
	BOOLEAN isMutexed = FALSE;

	UNREFERENCED_PARAMETER(CompletionContext);
	UNREFERENCED_PARAMETER(Data);
	UNREFERENCED_PARAMETER(Flags);

	//
	// Remove cache when file open in security drive.  
	// It should be done while write operation or it will have system crush.
	//

	try {
	
		if (!FlagOn(IRP_NOCACHE, iopb->IrpFlags))
		{
			if (CcIsFileCached(FltObjects->FileObject))
			{
				if (FltObjects->FileObject->SectionObjectPointer != NULL &&
					FltObjects->FileObject->SectionObjectPointer->DataSectionObject != NULL) 
				{
					BOOLEAN purgeResult = CcPurgeCacheSection(FltObjects->FileObject->SectionObjectPointer,
															  NULL,
															  0,
															  FALSE);

					LOG_PRINT(LOGFL_WRITE, 
						("rgCrypt!PostCreate -> CcPurgeCacheSection result : %d", 
						purgeResult));
				}
			}
		}
	}
	finally {

		//
		// do nothing..
		//
	}

	return retValue;
}

FLT_PREOP_CALLBACK_STATUS
PreReadBuffers(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_Flt_CompletionContext_Outptr_ PVOID *CompletionContext
)
{
	PFLT_IO_PARAMETER_BLOCK iopb = Data->Iopb;
	FLT_PREOP_CALLBACK_STATUS retValue = FLT_PREOP_SUCCESS_NO_CALLBACK;
	PVOID newBuf = NULL;
	PMDL newMdl = NULL;
	PVOLUME_CONTEXT volCtx = NULL;
	PPRE_2_POST_CONTEXT p2pCtx;
	NTSTATUS status;
	ULONG readLen = iopb->Parameters.Read.Length;

	PFLT_FILE_NAME_INFORMATION nameInfo = NULL;
	BOOLEAN isSecurityPath = FALSE;
	INT curPid = 0;

	try {

		//
		// Return if the port is not connected.
		//

		if (gFilterData.ClientPort == NULL){

			leave;
		}

		//
		// Return if decryption is not eabled.
		//

		if (!gServiceData.ReadDecryptionEnable) {

			leave;
		}

		//
		//  If they are trying to read ZERO bytes, then don't do anything and
		//  we don't need a post-operation callback.
		//

		if (readLen == 0) {

			leave;
		}

		//
		// We only decrypt non cached data.
		//

		if (!FlagOn(IRP_NOCACHE, iopb->IrpFlags)) {

			leave;
		}

		//
		//  Check Security file to decrypt.
		//

		status = FltGetFileNameInformation(Data, FLT_FILE_NAME_NORMALIZED , &nameInfo);
		if (!NT_SUCCESS(status)) {

			leave;
		}

		FltParseFileNameInformation(nameInfo);

		//
		// Skip for other pathes
		//

		isSecurityPath = IsSecurityFileEx(nameInfo, gSecurityPath, gServiceData.SecurityPathCount);
		if (!isSecurityPath){

			leave;
		}

		//
		// Skip if it is system related files.
		//

		INT curPid = GetProcessId(Data);
		if (IsSystemFile(nameInfo, curPid, Data)) {

			leave;
		}

		LOG_PRINT(LOGFL_READ, ("rgCrypt! PreReadBuffers start decrypt"));

		//
		//  Get our volume context so we can display our volume name in the
		//  debug output.
		//

		status = FltGetVolumeContext(FltObjects->Filter,
			FltObjects->Volume,
			&volCtx);

		if (!NT_SUCCESS(status)) {

			LOG_PRINT(LOGFL_ERRORS, 
				("rgCrypt!PreReadBuffers:Error getting volume context, status=%x\n",
				status));

			leave;
		}

		//
		//  If this is a non-cached I/O we need to round the length up to the
		//  sector size for this device.  We must do this because the file
		//  systems do this and we need to make sure our buffer is as big
		//  as they are expecting.
		//

		LOG_PRINT(LOGFL_CRYPT,
			("rgCrypt!PreReadBuffers: readLen  %d\n", 
			readLen));

		if (FlagOn(IRP_NOCACHE, iopb->IrpFlags)) {

			readLen = (ULONG)ROUND_TO_SIZE(readLen, volCtx->SectorSize);

			LOG_PRINT(LOGFL_CRYPT, 
				("rgCrypt!PreReadBuffers: roundedLen %d \n", 
				readLen));
		}

		//
		//  Allocate aligned nonPaged memory for the buffer we are swapping
		//  to. This is really only necessary for noncached IO but we always
		//  do it here for simplification. If we fail to get the memory, just
		//  don't swap buffers on this operation.
		//

		newBuf = FltAllocatePoolAlignedWithTag(FltObjects->Instance,
			NonPagedPool,
			(SIZE_T)readLen,
			BUFFER_SWAP_TAG);

		if (newBuf == NULL) {

			LOG_PRINT(LOGFL_ERRORS,
				("rgCrypt!PreReadBuffers: %wZ Failed to allocate %d bytes of memory\n",
				&volCtx->Name,
				readLen));

			leave;
		}

		//
		//  We only need to build a MDL for IRP operations.  We don't need to
		//  do this for a FASTIO operation since the FASTIO interface has no
		//  parameter for passing the MDL to the file system.
		//

		if (FlagOn(Data->Flags, FLTFL_CALLBACK_DATA_IRP_OPERATION)) {

			//
			//  Allocate a MDL for the new allocated memory.  If we fail
			//  the MDL allocation then we won't swap buffer for this operation
			//

			newMdl = IoAllocateMdl(newBuf,
				readLen,
				FALSE,
				FALSE,
				NULL);

			if (newMdl == NULL) {

				LOG_PRINT(LOGFL_ERRORS,
					("rgCrypt!PreReadBuffers: %wZ Failed to allocate MDL\n",
					&volCtx->Name));

				leave;
			}

			//
			//  setup the MDL for the non-paged pool we just allocated
			//

			MmBuildMdlForNonPagedPool(newMdl);
		}

		//
		//  We are ready to swap buffers, get a pre2Post context structure.
		//  We need it to pass the volume context and the allocate memory
		//  buffer to the post operation callback.
		//

		p2pCtx = ExAllocateFromNPagedLookasideList(&Pre2PostContextList);

		if (p2pCtx == NULL) {

			LOG_PRINT(LOGFL_ERRORS,
				("rgCrypt!PreReadBuffers:%wZ Failed to allocate pre2Post context structure\n",
				&volCtx->Name));

			leave;
		}

		//
		//  Log that we are swapping
		//

		LOG_PRINT(LOGFL_READ,
			("rgCrypt!PreReadBuffers:%wZ newB=%p newMdl=%p oldB=%p oldMdl=%p len=%d\n",
			&volCtx->Name,
			newBuf,
			newMdl,
			iopb->Parameters.Read.ReadBuffer,
			iopb->Parameters.Read.MdlAddress,
			readLen));

		//
		//  Update the buffer pointers and MDL address, mark we have changed
		//  something.
		//

		iopb->Parameters.Read.ReadBuffer = newBuf;
		iopb->Parameters.Read.MdlAddress = newMdl;
		FltSetCallbackDataDirty(Data);

		//
		//  Pass state to our post-operation callback.
		//

		p2pCtx->SwappedBuffer = newBuf;
		p2pCtx->VolCtx = volCtx;

		*CompletionContext = p2pCtx;

		//
		//  Return we want a post-operation callback
		//

		retValue = FLT_PREOP_SUCCESS_WITH_CALLBACK;

	}
	finally {

		//
		//  If we don't want a post-operation callback, then cleanup state.
		//
		if (nameInfo != NULL) {

			FltReleaseFileNameInformation(nameInfo);
		}

		if (retValue != FLT_PREOP_SUCCESS_WITH_CALLBACK) {

			if (newBuf != NULL) {

				FltFreePoolAlignedWithTag(FltObjects->Instance,
					newBuf,
					BUFFER_SWAP_TAG);
			}

			if (newMdl != NULL) {

				IoFreeMdl(newMdl);
			}

			if (volCtx != NULL) {

				FltReleaseContext(volCtx);
			}
		}
	}

	return retValue;
}

FLT_POSTOP_CALLBACK_STATUS
PostReadBuffers(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
)
{
	PVOID origBuf;
	PFLT_IO_PARAMETER_BLOCK iopb = Data->Iopb;
	FLT_POSTOP_CALLBACK_STATUS retValue = FLT_POSTOP_FINISHED_PROCESSING;
	PPRE_2_POST_CONTEXT p2pCtx = CompletionContext;
	BOOLEAN cleanupAllocatedBuffer = TRUE;

	//
	//  This system won't draining an operation with swapped buffers, verify
	//  the draining flag is not set.
	//

	FLT_ASSERT(!FlagOn(Flags, FLTFL_POST_OPERATION_DRAINING));

	try {

		//
		// Return if the port is not connected.
		//
		if (gFilterData.ClientPort == NULL){

			leave;
		}

		//
		//  If the operation failed or the count is zero, there is no data to
		//  copy so just return now.
		//

		if (!NT_SUCCESS(Data->IoStatus.Status) ||
			(Data->IoStatus.Information == 0)) {

			LOG_PRINT(LOGFL_READ,
				("rgCrypt!PostReadBuffers: %wZ newB=%p No data read, status=%x, info=%Iu\n",
				&p2pCtx->VolCtx->Name,
				p2pCtx->SwappedBuffer,
				Data->IoStatus.Status,
				Data->IoStatus.Information));

			leave;
		}

		LOG_PRINT(LOGFL_READ, ("rgCrypt!PostReadBuffers: start\n"));

		//
		//  We need to copy the read data back into the users buffer.  Note
		//  that the parameters passed in are for the users original buffers
		//  not our swapped buffers.
		//

		if (iopb->Parameters.Read.MdlAddress != NULL) {

			//
			//  This should be a simple MDL. We don't expect chained MDLs
			//  this high up the stack
			//

			LOG_PRINT(LOGFL_READ, ("rgCrypt!PostReadBuffers: MdlAddress is not NULL\n"));


			FLT_ASSERT(((PMDL)iopb->Parameters.Read.MdlAddress)->Next == NULL);

			//
			//  Since there is a MDL defined for the original buffer, get a
			//  system address for it so we can copy the data back to it.
			//  We must do this because we don't know what thread context
			//  we are in.
			//

			origBuf = MmGetSystemAddressForMdlSafe(iopb->Parameters.Read.MdlAddress,
				NormalPagePriority);

			if (origBuf == NULL) {

				LOG_PRINT(LOGFL_ERRORS,
					("rgCrypt!PostReadBuffers: %wZ Failed to get system address for MDL: %p\n",
					&p2pCtx->VolCtx->Name,
					iopb->Parameters.Read.MdlAddress));

				//
				//  If we failed to get a SYSTEM address, mark that the read
				//  failed and return.
				//

				Data->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
				Data->IoStatus.Information = 0;
				leave;
			}

		}
		else if (FlagOn(Data->Flags, FLTFL_CALLBACK_DATA_SYSTEM_BUFFER) ||
			FlagOn(Data->Flags, FLTFL_CALLBACK_DATA_FAST_IO_OPERATION)) {

			//
			//  If this is a system buffer, just use the given address because
			//      it is valid in all thread contexts.
			//  If this is a FASTIO operation, we can just use the
			//      buffer (inside a try/except) since we know we are in
			//      the correct thread context (you can't pend FASTIO's).
			//

			LOG_PRINT(LOGFL_READ, ("rgCrypt!PostReadBuffers: FLTFL_CALLBACK_DATA_FAST_IO_OPERATION\n"));

			origBuf = iopb->Parameters.Read.ReadBuffer;

		}
		else {

			//
			//  They don't have a MDL and this is not a system buffer
			//  or a fastio so this is probably some arbitrary user
			//  buffer.  We can not do the processing at DPC level so
			//  try and get to a safe IRQL so we can do the processing.
			//

			LOG_PRINT(LOGFL_READ, ("rgCrypt!PostReadBuffers: FltDoCompletionProcessingWhenSafe\n"));

			if (FltDoCompletionProcessingWhenSafe(Data,
				FltObjects,
				CompletionContext,
				Flags,
				PostReadBuffersWhenSafe,
				&retValue)) {

				//
				//  This operation has been moved to a safe IRQL, the called
				//  routine will do (or has done) the freeing so don't do it
				//  in our routine.
				//

				cleanupAllocatedBuffer = FALSE;

			}
			else {

				//
				//  We are in a state where we can not get to a safe IRQL and
				//  we do not have a MDL.  There is nothing we can do to safely
				//  copy the data back to the users buffer, fail the operation
				//  and return.  This shouldn't ever happen because in those
				//  situations where it is not safe to post, we should have
				//  a MDL.
				//

				LOG_PRINT(LOGFL_ERRORS,
					("rgCrypt!PostReadBuffers: %wZ Unable to post to a safe IRQL\n",
					&p2pCtx->VolCtx->Name));

				Data->IoStatus.Status = STATUS_UNSUCCESSFUL;
				Data->IoStatus.Information = 0;
			}

			leave;
		}

		//
		//  We either have a system buffer or this is a fastio operation
		//  so we are in the proper context.  Copy the data handling an
		//  exception.
		//

		try {

			LOG_PRINT(LOGFL_READ,
				("rgCrypt! DecryptionData FasrIO operation decrypt Read : %d , offset : %d\n", 
				iopb->Parameters.Read.Length, 
				iopb->Parameters.Read.ByteOffset));

			if (CryptoData(gServiceData.EncryptionType, Data, FltObjects, 
				(PUCHAR)p2pCtx->SwappedBuffer, (ULONG)Data->IoStatus.Information, DECRYPT))
			{
				RtlCopyMemory(origBuf,
					p2pCtx->SwappedBuffer,
					Data->IoStatus.Information);
			}
			else{

				//
				// Failed decryption at DPC level 

				if (FltDoCompletionProcessingWhenSafe(Data,
					FltObjects,
					CompletionContext,
					Flags,
					PostReadBuffersDecWhenSafe,
					&retValue)) {

					//
					//  This operation has been moved to a safe IRQL, the called
					//  routine will do (or has done) the freeing so don't do it
					//  in our routine.
					//

					cleanupAllocatedBuffer = FALSE;
				
				}
				else {

					Data->IoStatus.Status = STATUS_UNSUCCESSFUL;
					Data->IoStatus.Information = 0;
				}

				leave;

			}

		} except(EXCEPTION_EXECUTE_HANDLER) {

			//
			//  The copy failed, return an error, failing the operation.
			//

			Data->IoStatus.Status = GetExceptionCode();
			Data->IoStatus.Information = 0;
		}
	}
	finally {

		//
		//  If we are supposed to, cleanup the allocated memory and release
		//  the volume context.  The freeing of the MDL (if there is one) is
		//  handled by FltMgr.
		//

		if (cleanupAllocatedBuffer) {

			LOG_PRINT(LOGFL_READ,
				("rgCrypt!PostReadBuffers: %wZ newB=%p info=%Iu Freeing\n",
				&p2pCtx->VolCtx->Name,
				p2pCtx->SwappedBuffer,
				Data->IoStatus.Information));

			FltFreePoolAlignedWithTag(FltObjects->Instance,
				p2pCtx->SwappedBuffer,
				BUFFER_SWAP_TAG);

			FltReleaseContext(p2pCtx->VolCtx);

			ExFreeToNPagedLookasideList(&Pre2PostContextList,
				p2pCtx);
		}
	}

	return retValue;
}

FLT_PREOP_CALLBACK_STATUS
PreWriteBuffers(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_Flt_CompletionContext_Outptr_ PVOID *CompletionContext
)
{
	PFLT_IO_PARAMETER_BLOCK iopb = Data->Iopb;
	FLT_PREOP_CALLBACK_STATUS retValue = FLT_PREOP_SUCCESS_NO_CALLBACK;
	PVOID newBuf = NULL;
	//PVOID deBuf = NULL;
	PMDL newMdl = NULL;
	PVOLUME_CONTEXT volCtx = NULL;
	PPRE_2_POST_CONTEXT p2pCtx;
	PVOID origBuf;
	NTSTATUS status;
	ULONG writeLen = iopb->Parameters.Write.Length;

	PFLT_FILE_NAME_INFORMATION nameInfo = NULL;
	BOOLEAN isSecurityPath = FALSE;
	INT curPid = 0;
	BOOLEAN writeFileAccess = FALSE;

	try {

		//
		// Do not check if the filter were not connected.
		//

		if (gFilterData.ClientPort == NULL){

			leave;
		}

		//
		// Return if encryption is not eabled.
		//

		if (!gServiceData.WriteEncryptionEnable) {

			leave;
		}	

		//
		//  If they are trying to write ZERO bytes, then don't do anything and
		//  we don't need a post-operation callback.
		//

		if (writeLen == 0) {

			leave;
		}

		//
		//  Check Security file to decrypt.
		//

		status = FltGetFileNameInformation(Data, FLT_FILE_NAME_NORMALIZED, &nameInfo);
		if (!NT_SUCCESS(status)) {

			leave;
		}

		FltParseFileNameInformation(nameInfo);

		//
		// Skip for other pathes
		//

		isSecurityPath = IsSecurityFileEx(nameInfo, gSecurityPath, gServiceData.SecurityPathCount);
		if (!isSecurityPath){

			leave;
		}

		//
		// Skip if it is system related files.
		//

		INT curPid = GetProcessId(Data);
		if (IsSystemFile(nameInfo, curPid, Data)) {

			leave;
		}

		//
		//  Get our volume context so we can display our volume name in the
		//  debug output.
		//

		status = FltGetVolumeContext(FltObjects->Filter,
			FltObjects->Volume,
			&volCtx);

		if (!NT_SUCCESS(status)) {

			LOG_PRINT(LOGFL_ERRORS,
				("rgCrypt!PreWriteBuffers: Error getting volume context, status=%x\n",
				status));

			leave;
		}

		//
		//  If this is a non-cached I/O we need to round the length up to the
		//  sector size for this device.  We must do this because the file
		//  systems do this and we need to make sure our buffer is as big
		//  as they are expecting.
		//

		if (FlagOn(IRP_NOCACHE, iopb->IrpFlags)) {
			
			writeLen = (ULONG)ROUND_TO_SIZE(writeLen, volCtx->SectorSize);
			
			LOG_PRINT(LOGFL_WRITE,
				("rgCrypt! preWrite ROUND_TO_SIZE writeLen %d", 
				writeLen));
		}

		//
		//  Allocate aligned nonPaged memory for the buffer we are swapping
		//  to. This is really only necessary for noncached IO but we always
		//  do it here for simplification. If we fail to get the memory, just
		//  don't swap buffers on this operation.
		//

		newBuf = FltAllocatePoolAlignedWithTag(FltObjects->Instance,
			NonPagedPool,
			(SIZE_T)writeLen,
			BUFFER_SWAP_TAG);

		if (newBuf == NULL) {

			LOG_PRINT(LOGFL_ERRORS,
				("rgCrypt!PreWriteBuffers: %wZ Failed to allocate %d bytes of memory.\n",
				&volCtx->Name,
				writeLen));

			leave;
		}

		//
		//  We only need to build a MDL for IRP operations.  We don't need to
		//  do this for a FASTIO operation because it is a waste of time since
		//  the FASTIO interface has no parameter for passing the MDL to the
		//  file system.
		//

		if (FlagOn(Data->Flags, FLTFL_CALLBACK_DATA_IRP_OPERATION)) {

			//
			//  Allocate a MDL for the new allocated memory.  If we fail
			//  the MDL allocation then we won't swap buffer for this operation
			//

			newMdl = IoAllocateMdl(newBuf,
				writeLen,
				FALSE,
				FALSE,
				NULL);

			if (newMdl == NULL) {

				LOG_PRINT(LOGFL_ERRORS,
					("rgCrypt!PreWriteBuffers: %wZ Failed to allocate MDL.\n",
					&volCtx->Name));

				leave;
			}

			//
			//  setup the MDL for the non-paged pool we just allocated
			//

			MmBuildMdlForNonPagedPool(newMdl);
		}

		//
		//  If the users original buffer had a MDL, get a system address.
		//

		if (iopb->Parameters.Write.MdlAddress != NULL) {

			//
			//  This should be a simple MDL. We don't expect chained MDLs
			//  this high up the stack
			//

			FLT_ASSERT(((PMDL)iopb->Parameters.Write.MdlAddress)->Next == NULL);

			origBuf = MmGetSystemAddressForMdlSafe(iopb->Parameters.Write.MdlAddress,
				NormalPagePriority);

			if (origBuf == NULL) {

				LOG_PRINT(LOGFL_ERRORS,
					("rgCrypt!PreWriteBuffers: %wZ Failed to get system address for MDL: %p\n",
					&volCtx->Name,
					iopb->Parameters.Write.MdlAddress));

				//
				//  If we could not get a system address for the users buffer,
				//  then we are going to fail this operation.
				//

				Data->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
				Data->IoStatus.Information = 0;
				retValue = FLT_PREOP_COMPLETE;
				leave;
			}

		}
		else {

			//
			//  There was no MDL defined, use the given buffer address.
			//

			origBuf = iopb->Parameters.Write.WriteBuffer;
		}

		//
		//  Copy the memory, we must do this inside the try/except because we
		//  may be using a users buffer address
		//

		try {

			if (FlagOn(IRP_NOCACHE, iopb->IrpFlags))
			{
				if (CryptoData(gServiceData.EncryptionType,
					Data,
					FltObjects,
					(PUCHAR)origBuf,
					writeLen,
					ENCRYPT))
				{
					LOG_PRINT(LOGFL_WRITE, 
						("rgCrypt!CryptoData Length : %d\n", 
						writeLen));
				}
			}

			RtlCopyMemory(newBuf,
				origBuf,
				writeLen);

			LOG_PRINT(LOGFL_WRITE, 
				("rgCrypt!Swap data Length : %d\n", 
				writeLen));

		} except(EXCEPTION_EXECUTE_HANDLER) {

			//
			//  The copy failed, return an error, failing the operation.
			//

			Data->IoStatus.Status = GetExceptionCode();
			Data->IoStatus.Information = 0;
			retValue = FLT_PREOP_COMPLETE;

			LOG_PRINT(LOGFL_ERRORS,
				("rgCrypt!PreWriteBuffers: %wZ Invalid user buffer, oldB=%p, status=%x\n",
				&volCtx->Name,
				origBuf,
				Data->IoStatus.Status));

			leave;
		}

		//
		//  We are ready to swap buffers, get a pre2Post context structure.
		//  We need it to pass the volume context and the allocate memory
		//  buffer to the post operation callback.
		//

		p2pCtx = ExAllocateFromNPagedLookasideList(&Pre2PostContextList);

		if (p2pCtx == NULL) {

			LOG_PRINT(LOGFL_ERRORS,
				("rgCrypt!PreWriteBuffers: %wZ Failed to allocate pre2Post context structure\n",
				&volCtx->Name));

			leave;
		}

		//
		//  Set new buffers
		//

		LOG_PRINT(LOGFL_WRITE,
			("rgCrypt!PreWriteBuffers: %wZ newB=%p newMdl=%p oldB=%p oldMdl=%p len=%d\n",
			&volCtx->Name,
			newBuf,
			newMdl,
			iopb->Parameters.Write.WriteBuffer,
			iopb->Parameters.Write.MdlAddress,
			writeLen));

		iopb->Parameters.Write.WriteBuffer = newBuf;
		iopb->Parameters.Write.MdlAddress = newMdl;
		FltSetCallbackDataDirty(Data);

		//
		//  Pass state to our post-operation callback.
		//

		p2pCtx->SwappedBuffer = newBuf;
		p2pCtx->VolCtx = volCtx;

		*CompletionContext = p2pCtx;

		//
		//  Return we want a post-operation callback
		//

		retValue = FLT_PREOP_SUCCESS_WITH_CALLBACK;	
		
	}
	finally {

		//
		//  If we don't want a post-operation callback, then free the buffer
		//  or MDL if it was allocated.
		//

		if (nameInfo != NULL) {

			FltReleaseFileNameInformation(nameInfo);
		}

		if (retValue != FLT_PREOP_SUCCESS_WITH_CALLBACK) {

			if (newBuf != NULL) {

				FltFreePoolAlignedWithTag(FltObjects->Instance,
					newBuf,
					BUFFER_SWAP_TAG);
			}

			if (newMdl != NULL) {

				IoFreeMdl(newMdl);
			}

			if (volCtx != NULL) {

				FltReleaseContext(volCtx);
			}
		}
	}

	return retValue;
}

FLT_POSTOP_CALLBACK_STATUS
PostWriteBuffers(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
)
{
	PFLT_IO_PARAMETER_BLOCK iopb = Data->Iopb;
	FLT_POSTOP_CALLBACK_STATUS retValue = FLT_POSTOP_FINISHED_PROCESSING;
	PPRE_2_POST_CONTEXT p2pCtx = CompletionContext;
	BOOLEAN cleanupAllocatedBuffer = TRUE;
	ULONG writeLen = Data->Iopb->Parameters.Write.Length;

	UNREFERENCED_PARAMETER(Flags);

	try	{

		if (!FlagOn(IRP_NOCACHE, iopb->IrpFlags))
		{
			if (FltObjects->FileObject->SectionObjectPointer != NULL &&
				FltObjects->FileObject->SectionObjectPointer->DataSectionObject != NULL) 
			{
				IO_STATUS_BLOCK	IoStatus;

				CcCoherencyFlushAndPurgeCache(
					FltObjects->FileObject->SectionObjectPointer,
					&Data->Iopb->Parameters.Write.ByteOffset,
					writeLen,
					&IoStatus,
					0
					);

				LOG_PRINT(LOGFL_WRITE, 
					("rgCrypt! CcFlushCache IoStatus : %x, length %d", 
					IoStatus.Status, 
					writeLen));
			}
		}
	}
	finally {

		//
		//  If we are supposed to, cleanup the allocated memory and release
		//  the volume context.  The freeing of the MDL (if there is one) is
		//  handled by FltMgr.
		//

		if (cleanupAllocatedBuffer) {

			FltFreePoolAlignedWithTag(FltObjects->Instance,
				p2pCtx->SwappedBuffer,
				BUFFER_SWAP_TAG);

			FltReleaseContext(p2pCtx->VolCtx);

			ExFreeToNPagedLookasideList(&Pre2PostContextList,
				p2pCtx);
		}
	}

	return retValue;
}

VOID
ReadDriverParameters(
_In_ PUNICODE_STRING RegistryPath
)
{
	OBJECT_ATTRIBUTES attributes;
	HANDLE driverRegKey;
	NTSTATUS status;
	ULONG resultLength;
	UNICODE_STRING valueName;
	UCHAR buffer[sizeof(KEY_VALUE_PARTIAL_INFORMATION) + sizeof(LONG)];

	//
	//  If this value is not zero then somebody has already explicitly set it
	//  so don't override those settings.
	//

	if (LOGFL_ERRORS == LoggingFlags) {

		//
		//  Open the desired registry key
		//

		InitializeObjectAttributes(&attributes,
			RegistryPath,
			OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE,
			NULL,
			NULL);

		status = ZwOpenKey(&driverRegKey,
			KEY_READ,
			&attributes);

		if (!NT_SUCCESS(status)) {

			return;
		}

		//
		// Read the given value from the registry.
		//

		RtlInitUnicodeString(&valueName, L"DebugFlags");

		status = ZwQueryValueKey(driverRegKey,
			&valueName,
			KeyValuePartialInformation,
			buffer,
			sizeof(buffer),
			&resultLength);

		if (NT_SUCCESS(status)) {

			LoggingFlags = *((PULONG)&(((PKEY_VALUE_PARTIAL_INFORMATION)buffer)->Data));
		}

		//
		//  Close the registry entry
		//

		ZwClose(driverRegKey);
	}
}

NTSTATUS
PortConnect(
_In_ PFLT_PORT ClientPort,
_In_opt_ PVOID ServerPortCookie,
_In_reads_bytes_opt_(SizeOfContext) PVOID ConnectionContext,
_In_ ULONG SizeOfContext,
_Outptr_result_maybenull_ PVOID *ConnectionCookie
)
{
	PAGED_CODE();

	NTSTATUS status;
	int index = 0;

	UNREFERENCED_PARAMETER(ServerPortCookie);
	UNREFERENCED_PARAMETER(SizeOfContext);
	UNREFERENCED_PARAMETER(ConnectionCookie = NULL);

	FLT_ASSERT(ClientPort == NULL);

	//
	//  Set the user process and port. In a production filter it may
	//  be necessary to synchronize access to such fields with port
	//  lifetime. For instance, while filter manager will synchronize
	//  FltCloseClientPort with FltSendMessage's reading of the port 
	//  handle, synchronizing access to the UserProcess would be up to
	//  the filter.
	//
	LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PortConnect: Port is connected , port=0x%p", ClientPort));

	gFilterData.ClientPort = ClientPort;

	LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PortConnect: Port is connected New , port=0x%p", ClientPort));

	//
	// Init service data
	//

	RtlCopyMemory(&gServiceData, ConnectionContext, SizeOfContext);

	//
	// Show data of the Sevice
	//

	LOG_PRINT(LOGFL_DEBUG, 
		("rgCrypt!PortConnect: ReadDecryptionEnable=%d", 
		gServiceData.ReadDecryptionEnable));

	LOG_PRINT(LOGFL_DEBUG, 
		("rgCrypt!PortConnect: WriteEncryptionEnable=%d", 
		gServiceData.WriteEncryptionEnable));

	LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PortConnect: CopyProtectEnable=%d", 
		gServiceData.CopyProtectEnable));

	LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PortConnect: ReadOnlyEnable=%d", 
		gServiceData.ReadOnlyEnable));

	LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PortConnect: FilterProtectEnable=%d", 
		gServiceData.FilterProtectEnable));

	LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PortConnect: CopyActionNotifyEnable=%d", 
		gServiceData.CopyActionNotifyEnable));

	LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PortConnect: SecurityPathCount=%d", 
		gServiceData.SecurityPathCount));

	LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PortConnect: App Pid=%d", 
		gServiceData.AppPid));

	//
	// Security folder list
	//

	for (index = 0; index < gServiceData.SecurityPathCount; index++) {

		RtlInitUnicodeString(&gSecurityPath[index], gServiceData.SecurityPath[index]);

		LOG_PRINT(LOGFL_DEBUG, 
			("rgCrypt!PortConnect: , SecurityPath[%d]=%wZ",
			index, 
			&gSecurityPath[index]));
	}
	
	//
	// Encryption type and Key Dump
	//

	LOG_PRINT(LOGFL_DEBUG, 
		("rgCrypt!PortConnect: EncryptionType=[%d] EncryptionKey ---->", 
		gServiceData.EncryptionType));

	for (index = 0; index < AES256_KEY_BYTE_LENGTH; index++) {

		LOG_PRINT(LOGFL_DEBUG, 
			("rgCrypt!PortConnect:  Key %02d : [%02x]", 
			index, 
			gServiceData.EncryptionKey[index]));
	}

	//
	// Dectyption key init by encryption type
	//

	CryptoInit(gServiceData.EncryptionType, gServiceData.EncryptionKey);

	//
	// init unicodestring for pre cache purged file.
	//

	RtlInitEmptyUnicodeString(&prePurgedFileName, prePurgedNameBuffer, MAX_PATH);

	LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PortConnect: Port is connected successfully"));

	return STATUS_SUCCESS;
}

VOID
PortDisconnect(
_In_opt_ PVOID ConnectionCookie
)
{
	UNREFERENCED_PARAMETER(ConnectionCookie);

	PAGED_CODE();

	//
	//  Close our handle to the connection: note, since we limited max connections to 1,
	//  another connect will not be allowed until we return from the disconnect routine.
	//

	//
	// Before closing it, show PID list which is protected even after the disconnection.
	// It will showed when LOGFL_WRITE_PRT flagged
	// After that, it will make empty the pid array.
	//

	PrintWriteProtectedPID();

	//
	// Reset write protected PID data
	//

	ResetWriteProtectedPid();

	//
	// Close Encryption
	//

	CryptoDataClose(gServiceData.EncryptionType);

	FltCloseClientPort(gFilterData.Filter, &gFilterData.ClientPort);

	//
	// Reset the user-process field.
	//

	gFilterData.UserProcess = NULL;
	gFilterData.ClientPort = NULL;

	LOG_PRINT(LOGFL_DEBUG, ("rgCrypt!PortDisconnect: Port is Disconnected"));
}

FLT_PREOP_CALLBACK_STATUS
PreCleanup(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_Flt_CompletionContext_Outptr_ PVOID *CompletionContext
)
{
	NTSTATUS status;
	PSTREAM_HANDLE_CONTEXT context;

	UNREFERENCED_PARAMETER(Data);
	UNREFERENCED_PARAMETER(CompletionContext);

	status = FltGetStreamHandleContext(FltObjects->Instance,
		FltObjects->FileObject,
		&context);

	if (NT_SUCCESS(status)) {

		FltReleaseContext(context);
	}

	return FLT_PREOP_SUCCESS_NO_CALLBACK;
}

//
// Check pid is in the list.
//

BOOLEAN
SetMsgData(
_In_ PMSG_SEND_DATA pMsgData,
_In_ INT Pid,
_In_ PFLT_FILE_NAME_INFORMATION pNameCtx,
_In_ PVOLUME_CONTEXT pVolCtx,
_In_ ULONG accessFlag
)
{
	UNICODE_STRING path = { 0 };
	WCHAR pathBuffer[MAX_PATH] = { 0 };
	INT fileActionType = 0;

	//
	//set command : read, write, create, edit, delete
	//

	if ((accessFlag & (FILE_GENERIC_WRITE)) == FILE_GENERIC_WRITE) {

		fileActionType = DETECT_FILE_WRITE;
	}
	else if ((accessFlag & (FILE_GENERIC_EXECUTE)) == FILE_GENERIC_EXECUTE){

		fileActionType = DETECT_FILE_EXCUTE;
	}
	else if ((accessFlag & (FILE_GENERIC_READ)) == FILE_GENERIC_READ){

		fileActionType = DETECT_FILE_READ;
	}
	else {

		//we don't need other actions
		return FALSE;
	}

	// check the message data with precious one.
	// We'd better skip the comparing file path for the performance reason.
	
	if (pMsgData->Command == fileActionType && 
		pMsgData->Pid == Pid)
	{
		//we don't need to send same data
		return FALSE;
	}

	//
	// get targe path string
	//

	path.Buffer = pathBuffer;
	path.Length = 0;
	path.MaximumLength = sizeof(pathBuffer);

	RtlAppendUnicodeStringToString(&path, &pVolCtx->Name);
	RtlAppendUnicodeStringToString(&path, &pNameCtx->ParentDir);
	RtlAppendUnicodeStringToString(&path, &pNameCtx->FinalComponent);

	try{

		RtlZeroMemory(pMsgData->FilePath, MAX_PATH);
		RtlCopyMemory(pMsgData->FilePath, path.Buffer, path.Length);
	}
	except(EXCEPTION_EXECUTE_HANDLER) {

		return FALSE;
	}

	pMsgData->Command = fileActionType;
	pMsgData->Pid = Pid;

	//pass the msg data to send
	return TRUE;
}

//
// Send message functions
//

INT
SendLogMessage(
_In_ PFILTER_DATA pFilterData,
_In_ PMSG_SEND_DATA MsgData,
_In_ INT NotifyType
)
{
	INT result = 0;
	NTSTATUS status = 0;

	ULONG replyLength = 0;
	NOTIFICATION notification = { 0 };
	REPLY_DATA replyData = { 0 };

	try {

		//
		// Set notification info.
		//

		notification.NotifyType = NotifyType;

		replyLength = sizeof(REPLY_DATA);
		notification.BytesToScan = sizeof(MSG_SEND_DATA);

		RtlCopyMemory(&notification.Contents, MsgData, sizeof(MSG_SEND_DATA));

		UNICODE_STRING tmpPath;
		RtlInitUnicodeString(&tmpPath, notification.Contents.FilePath);

		LOG_PRINT(LOGFL_SEND_MSG, ("rgCrypt!SendLogMessage: Msg sending - type : [%d], pid : [%d] File: [%wZ]\n",
			notification.Contents.Command, notification.Contents.Pid, &tmpPath));

		//
		// Send message with msg data and recevied reply data.
		//

		status = FltSendMessage(pFilterData->Filter,
			&pFilterData->ClientPort,
			&notification,
			sizeof(NOTIFICATION),
			&replyData,
			&replyLength,
			NULL);

		if (STATUS_SUCCESS == status) {

			LOG_PRINT(LOGFL_SEND_MSG, ("rgCrypt!SendLogMessage: Msg replied command : [%d], pid : [%d]\n",
				replyData.Command, replyData.Pid));
		}

		//
		// in case of reject command recevied , 1: allow 2: reject
		//

		result = replyData.Command;


	}
	except(EXCEPTION_EXECUTE_HANDLER) {

		LOG_PRINT(LOGFL_ERRORS, ("rgCrypt!SendLogMessage: EXCEPTION_EXECUTE_HANDLER"));
	}

	return result;
}

