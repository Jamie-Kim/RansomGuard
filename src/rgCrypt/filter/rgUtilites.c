#include "rgUtilites.h"
#include "rgCrypt.h"
#include "rgSafeRoutines.h"

#include <Ntstrsafe.h>
#include <aes256.h>
#include <rc4.h>

//
// Manage PIDs for write protection.
//

const INT MaxProcessCnt = WRITE_PROTECTION_PID_MAX;
INT PidArray[WRITE_PROTECTION_PID_MAX] = { 0 };
INT PidArrayIndex = 0;

UNICODE_STRING sysVolumeInfo = RTL_CONSTANT_STRING(L"\\System Volume Information\\");
UNICODE_STRING desktopIni = RTL_CONSTANT_STRING(L"Desktop.ini");

//
// For special applications we should have to handle.
//

const PCHAR XlsName = "EXCEL";

//
//  Decryption keys.
//

UCHAR Rc4Key[RC4_KEY_BYTE_LENGTH] = { 0 };
aes256_context Aes256Ctx = { 0 };
rc4_state Rc4Ctx = { 0 };

//
// Mutex for file IO delay in case of AES256 decryption. 
//

//FAST_MUTEX CryptoMutex = { 0 };

//
// Supported decryption type
//

typedef enum 
{ 
	RC4 = 0,
	AES256 = 1,
	NONE = 2

} EncryptionMethod;

typedef PCHAR(*GET_PROCESS_IMAGE_NAME) (PEPROCESS Process);
extern UCHAR *PsGetProcessImageFileName(IN PEPROCESS Process);


//
// Check the path of file is in the security path. 
// It can support multiple security paths.
//

VOID
CopyUnicodeString(
_In_ PUNICODE_STRING des,
_In_ PUNICODE_STRING src
)
{
	RtlUnicodeStringCopy(des , src);
}

//
// Detach filter on all volumes
//

VOID
DetachAllVolume(
_In_ PFLT_FILTER Filter
)
{
	//to detach from the all volumes
	ULONG volCnt = 0;
	ULONG index = 0;
	PFLT_VOLUME gPFilterVolumes[MAX_ATTACHED_VOLUME];
	NTSTATUS status = 0;

	status = FltEnumerateVolumes(Filter,
								 gPFilterVolumes,
							 	 MAX_ATTACHED_VOLUME,
								 &volCnt);

	if (NT_SUCCESS(status)) 
	{
		for (index = 0; index < volCnt; index++)
		{
			//detach each volume
			status = FltDetachVolume(Filter,
				gPFilterVolumes[index],
				NULL);

			if (NT_SUCCESS(status)) {

				LOG_PRINT(LOGFL_DEBUG, ("rgCrypt! FltDetachVolume Detached [%d]", index));
			}

			//free allocated volume pointer
			FltObjectDereference(gPFilterVolumes[index]);
		}
	}
}

//
// Check the path of file is in the security path. 
// It can support multiple security paths.
//

BOOLEAN
IsSecurityFileEx(
_In_ PFLT_FILE_NAME_INFORMATION NameInfo,
_In_ PUNICODE_STRING pSePathArray,
_In_ INT SePathCnt
)
{
	INT i = 0;

	for (i = 0; i < SePathCnt; i++)
	{
		if (IsSecurityFile(NameInfo, pSePathArray + i))
		{
			return TRUE;
		}
	}

	return FALSE;
}

//
// Check the system file realted to volume and we don't need to touch the file.
//

BOOLEAN
IsSystemFile(
_In_ PFLT_FILE_NAME_INFORMATION NameInfo,
_In_ INT Pid,
_In_ PFLT_CALLBACK_DATA Data
)
{	
	if (NameInfo->Extension.Length == 0 &&
		NameInfo->Stream.Length == 0)
	{
		//
		// We need to care about some special programs like Excel...
		// Excel touch the system files ,so we need to handle it or Excel can't save the file correctly.
		//

		PEPROCESS objCurProcess = IoThreadToProcess(Data->Thread);
		CHAR*  pStrProcessName = PsGetProcessImageFileName(objCurProcess);

		if (strncmp(XlsName, pStrProcessName, strlen(XlsName)) == 0)
		{
			//
			// Excel Program use 16 length of hidden file and sometime touch the $LogFile.
			// So, don't encrypt $LogFile but encrypt 16 length of hidden file.
			//

			if (NameInfo->FinalComponent.Length == 16 && 
				NameInfo->FinalComponent.Buffer[0] != L'$')
			{
				LOG_PRINT(LOGFL_CRYPT,
					("rgCrypt!IsSystemFile Don't SKIP --------> PID[%d], PDir [%wZ], Name [%wZ], Length %d",
					Pid,
					&NameInfo->ParentDir,
					&NameInfo->FinalComponent,
					NameInfo->FinalComponent.Length));

				//
				// Do Encrypt or Decrypt or Purge the cahce.
				//

				return FALSE;
			}
		}

		//
		// Skip becase it is system file which is don't need to encrypt.
		//

		return TRUE;
	}

	//
	// Normal Files. Do encrypt or decrypt.
	//

	return FALSE;
}

//
// Check the path of file is in the security path.
//

BOOLEAN
IsSecurityFile(
_In_ PFLT_FILE_NAME_INFORMATION NameInfo,
_In_ PUNICODE_STRING SePath
)
{
	//Add Volume and ParentDir
	INT index = 0;
	INT vol_length = 0;
	INT dir_length = 0;

	//set length
	vol_length = NameInfo->Volume.Length / 2;
	dir_length = ((SePath->Length / 2) - vol_length) - 1;

	if (vol_length > NameInfo->Volume.Length){
		return FALSE;
	}

	if (dir_length > NameInfo->ParentDir.Length){
		return FALSE;
	}

	//check volume
	for (index = 0; index < vol_length; index++){
		if (NameInfo->Volume.Buffer[index] != SePath->Buffer[index]){
			return FALSE;
		}
	}

	//check dir
	for (index = 0; index < dir_length; index++){
		if (NameInfo->ParentDir.Buffer[index] != SePath->Buffer[vol_length + index]){
			return FALSE;
		}
	}

	return TRUE;
}

//
// Get current process ID.
//

INT 
GetProcessId(
_In_ PFLT_CALLBACK_DATA Data
	)
{
	PEPROCESS objCurProcess = IoThreadToProcess(Data->Thread);
	HANDLE iCurProcID;
	iCurProcID = PsGetProcessId(objCurProcess);

	return (INT)iCurProcID;
}

//
// Check the process for write protection.
//

BOOLEAN 
IsProcessProtected(
_In_ INT processId
)
{
	INT i = 0;

	for (i = 0; i < WRITE_PROTECTION_PID_MAX; i++)
	{

		if (PidArray[i] == processId) {

			return TRUE;
		}
		else if (PidArray[i] == 0){

			return FALSE;
		}
	}

	return FALSE;
}


//
// Reset write protected PID when disconnect the filter.
//

VOID
ResetWriteProtectedPid()
{
	INT i = 0;

	PidArrayIndex = 0;
	for (i = 0; i < WRITE_PROTECTION_PID_MAX; i++)
	{
		PidArray[i] = 0;
	}
}

//
// print the process ids of write protected with empty it
//

VOID
PrintWriteProtectedPID()
{
	INT i = 0;

	LOG_PRINT(LOGFL_WRITE_PRT,
		("rgCrypt!PrintWriteProtectedPID: Currently write protected PIDs , Count[%d]",
		PidArrayIndex));

	for (i = 0; i < WRITE_PROTECTION_PID_MAX; i++)
	{
		if (PidArray[i] == 0) {

			break;
		}

		LOG_PRINT(LOGFL_WRITE_PRT,
			("rgCrypt!PrintWriteProtectedPID: Protected Pid[%d] ,",
			PidArray[i]));
	}
}

//
// Add the process for write protection.
//

INT 
AddProtectedProcess(
_In_ INT processId,
_In_ INT exceptId)
{

	//skip the application for registered.
	if (exceptId == processId) {
	
		return FALSE;
	}

	if (IsProcessProtected(processId)) {

		return FALSE;
	}

	if (PidArrayIndex >= WRITE_PROTECTION_PID_MAX) {

		PidArrayIndex = 0;
	}

	PidArray[PidArrayIndex++] = processId;

	return processId;
}

//
// Check the process which is protected or not for the proecess termination.
//

BOOLEAN
IsExitProtectedPID(
_In_ INT CurPid,
_In_ INT *pExitProtectedPids,
_In_ INT ProtectedPidCnt
)
{
	if (CurPid == 0) {

		return FALSE;
	}

	if (ProtectedPidCnt >= MAX_PROTECT_PRECCESS){

		return FALSE;
	}

	for (int i = 0; i < ProtectedPidCnt; i++)
	{
		if (pExitProtectedPids[i] == 0) {
			
			break;
		}

		if (CurPid == pExitProtectedPids[i]) {

			return TRUE;
		}
	}

	return FALSE;
}

/*************************************************************************
 Related functions for encryption and decryption 
*************************************************************************/

VOID
CryptoInit(
_In_   INT EncryptionType,
IN	   PUCHAR  pEncryptionKey
)
{
	if (EncryptionType == RC4)
	{
		rc4_init(&Rc4Ctx, pEncryptionKey, RC4_KEY_BYTE_LENGTH);

		LOG_PRINT(LOGFL_CRYPT,
			("rgCrypt!CryptoDataInit , XOR_256 = 0 : %x 32 : %x",
			pEncryptionKey[0], pEncryptionKey[31]));

		//
		// Initialize the mutex for AES256, this routine will do only once when port is connected.
		//

		ExInitializeFastMutex(&CryptoMutex);
	}
	else if (EncryptionType == AES256)
	{

		aes256_init(&Aes256Ctx, pEncryptionKey);

		LOG_PRINT(LOGFL_CRYPT,
			("rgCrypt!CryptoDataInit , AES256 = 0: %x , 32 : %x",
			pEncryptionKey[0], pEncryptionKey[31]));

		//
		// Initialize the mutex for AES256, this routine will do only once when port is connected.
		//

		ExInitializeFastMutex(&CryptoMutex);
	}
	else {

		LOG_PRINT(LOGFL_CRYPT, ("rgCrypt!None"));
	}
}

/*************************************************************************
 Cryptography
*************************************************************************/

BOOLEAN
CryptoData(
_In_   INT EncryptionType,
_In_   PFLT_CALLBACK_DATA Data,
_In_   PCFLT_RELATED_OBJECTS FltObjects,
IN OUT PUCHAR  pBuf,
IN     ULONG   ReadBufferLength,
IN     BOOLEAN isEncryption
)
{
	BOOLEAN result = FALSE;

	if (EncryptionType == RC4)
	{
		result = CryptoDataRC4(Data, pBuf, ReadBufferLength, isEncryption);
	}
	else if (EncryptionType == AES256)
	{
		result = CryptoDataAes256(Data, FltObjects, pBuf, ReadBufferLength, isEncryption);
	}
	else {

		LOG_PRINT(LOGFL_CRYPT, ("rgCrypt! CryptoData :: None"));
	}

	return result;
}

//
// Clear the encryption keys 
//

VOID
CryptoDataClose(
_In_   INT EncryptionType
)
{
	int i = 0;

	if (EncryptionType == AES256)
	{
		aes256_done(&Aes256Ctx);
	}
	else {

		for (i = 0; i < sizeof(Rc4Key); i++)	{

			Rc4Key[i] = 0;
		}
	}
}


BOOLEAN
CryptoDataAes256(
_In_   PFLT_CALLBACK_DATA Data,
_In_   PCFLT_RELATED_OBJECTS FltObjects,
IN OUT PUCHAR  pBuf,
IN     ULONG   ReadBufferLength,
IN     BOOLEAN isEncryption
)
{
	const ULONG BlockSize = AES256_BLOCK_SIZE;

	BOOLEAN result = TRUE;
	BOOLEAN isMutexed = FALSE;
	NTSTATUS status = 0;

	ULONG blockCnt = 0;
	ULONG i = 0;

	PVOID decrypteBuf = NULL;
	ULONG rearReadLength = 0;
	ULONG frontReadLength = 0;
	ULONG readLen = ReadBufferLength;
	LARGE_INTEGER ReadOffset = Data->Iopb->Parameters.Read.ByteOffset;

	ULONG bytesRead = 0;

	//
	// Calculate offset and readlength to fit as block size. 
	// we need to think the total length like this [ front padding] [ pBuf ] [ rear padding ] 
	//

	frontReadLength = ReadOffset.QuadPart % BlockSize;
	rearReadLength = 16 - ((frontReadLength + ReadBufferLength) % BlockSize);
	readLen = frontReadLength + ReadBufferLength + rearReadLength;
	ReadOffset.QuadPart = ReadOffset.QuadPart - frontReadLength;

	try{

		//
		// readLen is the length we need to decrypt blocks.
		// ReadBufferLength is actual read.
		//

		if (readLen != ReadBufferLength)
		{

			//
			// Too bad ReadBufferLength is not same as blocks size.
			// We need to read file again for decryption and we can't read 
			// file data at DPC level.
			//

			if (KeGetCurrentIrql() > PASSIVE_LEVEL) {

				LOG_PRINT(LOGFL_CRYPT,
					("rgCrypt!DecryptionDataAes256: here is not passive level do it in safe routine: %d\n",
					KeGetCurrentIrql()));

				result = FALSE;

				leave;
			}

			//
			// Thread Lock should be needed to decrypt correctly if it has long delay.
			//

			ExAcquireFastMutex(&CryptoMutex);
			isMutexed = TRUE;

			//
			// buffer allocation for decrypted data.
			//

			decrypteBuf = FltAllocatePoolAlignedWithTag(FltObjects->Instance,
				NonPagedPool,
				(SIZE_T)readLen,
				BUFFER_SWAP_TAG);


			if (decrypteBuf == NULL) {

				LOG_PRINT(LOGFL_ERRORS,
					("rgCrypt!DecryptionDataAes256: Failed to allocate %d bytes of memory\n",
					readLen));

				result = FALSE;
				LOG_PRINT(LOGFL_CRYPT, 
					("rgCrypt! DecryptionDataAes256 decrypteBuf alloc error : %d xxxxxxxxxxxxx>n", 
					readLen));

				leave;
			}

			LOG_PRINT(LOGFL_CRYPT, 
				("rgCrypt! decrypteBuf alloc : %d --------------------*\n", 
				readLen));

			//
			// Read file again for paddings
			//

			status = FltReadFile(
				FltObjects->Instance,
				FltObjects->FileObject,
				&ReadOffset,
				readLen,
				decrypteBuf,
				FLTFL_IO_OPERATION_DO_NOT_UPDATE_BYTE_OFFSET | FLTFL_IO_OPERATION_NON_CACHED | 
				FLTFL_IO_OPERATION_PAGING | FLTFL_IO_OPERATION_SYNCHRONOUS_PAGING,
				&bytesRead,
				NULL,
				NULL
				);

			if (status != 0)
			{
				LOG_PRINT(LOGFL_ERRORS,
					("rgCrypt! DecryptionDataAes256 Read ERROR: offset : %ld ReadLength : %d bytesRead : %d, ST: %d\n",
					ReadOffset.QuadPart, readLen, bytesRead, status));

				result = FALSE;

				leave;
			}

			//
			// Get total block count and decrypt
			//

			blockCnt = readLen / BlockSize;

			for (i = 0; i < blockCnt; i++)
			{
				aes256_decrypt_ecb(&Aes256Ctx, (PUCHAR)decrypteBuf + (i * BlockSize));
			}

			//
			// copy to original buffer with actual read length
			//

			RtlCopyMemory(pBuf,
				(PUCHAR)decrypteBuf + frontReadLength,
				ReadBufferLength);

			LOG_PRINT(LOGFL_CRYPT,
				("rgCrypt! DecryptionDataAes256 FltReadFile deBuf ,bytesRead : %d = %d > %d\n",
				readLen, bytesRead, ReadBufferLength));
		}
		else{

			//
			// Fortunately , ReadBufferLength is same as blocks size.
			// We don't need to read the file again.
			//

			blockCnt = ReadBufferLength / BlockSize;

			for (i = 0; i < blockCnt; i++)
			{
				if (isEncryption)
				{
					aes256_encrypt_ecb(&Aes256Ctx, (PUCHAR)pBuf + (i * BlockSize));
				}
				else
				{
					aes256_decrypt_ecb(&Aes256Ctx, (PUCHAR)pBuf + (i * BlockSize));
				}
			}
		}
	}
	finally{

		//
		// Release mutex after finishing decrypted data copy.
		//
		if (isMutexed == TRUE) {

			ExReleaseFastMutex(&CryptoMutex);
		}

		//
		// Release allocated buffer.
		//

		if (decrypteBuf != NULL) {

			FltFreePoolAlignedWithTag(FltObjects->Instance,
				decrypteBuf,
				BUFFER_SWAP_TAG);

			LOG_PRINT(LOGFL_CRYPT, 
				("rgCrypt! decrypteBuf release *-----------------\n"));

		}
	}

	return result;
}


BOOLEAN
CryptoDataRC4(
_In_   PFLT_CALLBACK_DATA Data,
IN OUT PUCHAR  pBuf,
IN     ULONG   FileSize,
IN     BOOLEAN isEncryption
)
{
	LARGE_INTEGER Offset = { 0 };
	BOOLEAN isMutexed = FALSE;
	BOOLEAN result = TRUE;

	try
	{
		if (KeGetCurrentIrql() == PASSIVE_LEVEL) {

			LOG_PRINT(LOGFL_CRYPT,
				("rgCrypt! set mutex -----------------*\n"));

			//
			// Thread Lock should be needed to decrypt correctly if it has long delay.
			//

			ExAcquireFastMutex(&CryptoMutex);
			isMutexed = TRUE;
		}
	
		if (isEncryption)
		{
			LOG_PRINT(LOGFL_CRYPT,
				("rgCrypt! start rc4_crypt Encryption %x %x ----------*\n", 
				pBuf[0], 
				pBuf[1]));

			Offset = Data->Iopb->Parameters.Write.ByteOffset;
		}
		else
		{
			LOG_PRINT(LOGFL_CRYPT,
				("rgCrypt! start rc4_crypt Decryption %x %x ----------*\n", 
				pBuf[0], 
				pBuf[1]));

			Offset = Data->Iopb->Parameters.Read.ByteOffset;
		}
	
		Rc4Ctx.index1 = (UCHAR)Offset.QuadPart;

		rc4_crypt(&Rc4Ctx, pBuf, FileSize);

		LOG_PRINT(LOGFL_CRYPT,
			("rgCrypt! rc4_crypt done %x %x ----------[%d]\n", 
			pBuf[0], 
			pBuf[1], 
			FileSize));
	}
	finally
	{
		//
		// Release mutex after finishing decrypted data copy.
		//

		if (isMutexed == TRUE) {

			LOG_PRINT(LOGFL_CRYPT,
				("rgCrypt! release mutex *-----------------\n"));

			ExReleaseFastMutex(&CryptoMutex);
		}
	}

	return result;
}
