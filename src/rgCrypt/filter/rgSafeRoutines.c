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

/*************************************************************************
Grobal Values
*************************************************************************/
extern SERVICE_DATA gServiceData;
extern NPAGED_LOOKASIDE_LIST Pre2PostContextList;

/*************************************************************************
MiniFilter callback safe routines.
*************************************************************************/

FLT_POSTOP_CALLBACK_STATUS
PostReadBuffersDecWhenSafe(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
)
/*++

Routine Description:

To read file when safe mode.
This routhine is for handling AES256 to read more block to decrypt it.

--*/
{
	PFLT_IO_PARAMETER_BLOCK iopb = Data->Iopb;
	PPRE_2_POST_CONTEXT p2pCtx = CompletionContext;
	PVOID origBuf;
	NTSTATUS status;

	UNREFERENCED_PARAMETER(Flags);
	FLT_ASSERT(Data->IoStatus.Information != 0);

	//
	//  This is some sort of user buffer without a MDL, lock the user buffer
	//  so we can access it.  This will create a MDL for it.
	//

	status = FltLockUserBuffer(Data);

	if (!NT_SUCCESS(status)) {

		LOG_PRINT(LOGFL_ERRORS,
			("rgCrypt!PostReadBuffersDecWhenSafe:    %wZ Could not lock user buffer, oldB=%p, status=%x\n",
			&p2pCtx->VolCtx->Name,
			iopb->Parameters.Read.ReadBuffer,
			status));

		//
		//  If we can't lock the buffer, fail the operation
		//

		Data->IoStatus.Status = status;
		Data->IoStatus.Information = 0;

	}
	else {

		//
		//We need to handle the case of FAST IO.
		//

		if (FlagOn(Data->Flags, FLTFL_CALLBACK_DATA_SYSTEM_BUFFER) ||
			FlagOn(Data->Flags, FLTFL_CALLBACK_DATA_FAST_IO_OPERATION)) {

			origBuf = iopb->Parameters.Read.ReadBuffer;

		}
		else {

			//
			//  Get a system address for this buffer.
			//

			origBuf = MmGetSystemAddressForMdlSafe(iopb->Parameters.Read.MdlAddress,
				NormalPagePriority);
		}

		if (origBuf == NULL) {

			LOG_PRINT(LOGFL_ERRORS,
				("rgCrypt!PostReadBuffersDecWhenSafe:    %wZ Failed to get system address for MDL: %p\n",
				&p2pCtx->VolCtx->Name,
				iopb->Parameters.Read.MdlAddress));

			//
			//  If we couldn't get a SYSTEM buffer address, fail the operation
			//

			Data->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
			Data->IoStatus.Information = 0;

		}
		else {

			//
			//  Copy the data back to the original buffer.  Note that we
			//  don't need a try/except because we will always have a system
			//  buffer address.

			if (CryptoData(gServiceData.EncryptionType, Data, FltObjects, 
				(PUCHAR)p2pCtx->SwappedBuffer, (ULONG)Data->IoStatus.Information, DECRYPT))
			{
				RtlCopyMemory(origBuf,
					p2pCtx->SwappedBuffer,
					Data->IoStatus.Information);

				LOG_PRINT(LOGFL_READ,
					("PostReadBuffersDecWhenSafe: Decryption is done successfully : read : %d, offset : %ld\n",
					iopb->Parameters.Read.Length,
					iopb->Parameters.Read.ByteOffset));
			}
			else{

				LOG_PRINT(LOGFL_ERRORS,
					("PostReadBuffersDecWhenSafe: Decryption failed : read : %d, offset : %ld\n",
					iopb->Parameters.Read.Length,
					iopb->Parameters.Read.ByteOffset));
			}
		}
	}

	//
	//  Free allocated memory and release the volume context
	//

	FltFreePoolAlignedWithTag(FltObjects->Instance,
		p2pCtx->SwappedBuffer,
		BUFFER_SWAP_TAG);

	FltReleaseContext(p2pCtx->VolCtx);

	ExFreeToNPagedLookasideList(&Pre2PostContextList,
		p2pCtx);

	return FLT_POSTOP_FINISHED_PROCESSING;
}

FLT_POSTOP_CALLBACK_STATUS
PostReadBuffersWhenSafe(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
)
/*++

Routine Description:

We had an arbitrary users buffer without a MDL so we needed to get
to a safe IRQL so we could lock it and then copy the data.

Arguments:

Data - Pointer to the filter callbackData that is passed to us.

FltObjects - Pointer to the FLT_RELATED_OBJECTS data structure containing
opaque handles to this filter, instance, its associated volume and
file object.

CompletionContext - Contains state from our PreOperation callback

Flags - Denotes whether the completion is successful or is being drained.

Return Value:

FLT_POSTOP_FINISHED_PROCESSING - This is always returned.

--*/
{
	PFLT_IO_PARAMETER_BLOCK iopb = Data->Iopb;
	PPRE_2_POST_CONTEXT p2pCtx = CompletionContext;
	PVOID origBuf;
	NTSTATUS status;

	//UNREFERENCED_PARAMETER(FltObjects);
	UNREFERENCED_PARAMETER(Flags);
	FLT_ASSERT(Data->IoStatus.Information != 0);

	//
	//  This is some sort of user buffer without a MDL, lock the user buffer
	//  so we can access it.  This will create a MDL for it.
	//

	status = FltLockUserBuffer(Data);

	if (!NT_SUCCESS(status)) {

		LOG_PRINT(LOGFL_ERRORS,
			("rgCrypt!PostReadBuffersWhenSafe:    %wZ Could not lock user buffer, oldB=%p, status=%x\n",
			&p2pCtx->VolCtx->Name,
			iopb->Parameters.Read.ReadBuffer,
			status));

		//
		//  If we can't lock the buffer, fail the operation
		//

		Data->IoStatus.Status = status;
		Data->IoStatus.Information = 0;
	}
	else {

		//
		//  Get a system address for this buffer.
		//

		origBuf = MmGetSystemAddressForMdlSafe(iopb->Parameters.Read.MdlAddress,
			NormalPagePriority);

		if (origBuf == NULL) {

			LOG_PRINT(LOGFL_ERRORS,
				("rgCrypt!PostReadBuffersWhenSafe:    %wZ Failed to get system address for MDL: %p\n",
				&p2pCtx->VolCtx->Name,
				iopb->Parameters.Read.MdlAddress));

			//
			//  If we couldn't get a SYSTEM buffer address, fail the operation
			//

			Data->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
			Data->IoStatus.Information = 0;
		}
		else {

			//
			//  Copy the data back to the original buffer.  Note that we
			//  don't need a try/except because we will always have a system
			//  buffer address.

			if (CryptoData(gServiceData.EncryptionType, Data, FltObjects, 
				(PUCHAR)p2pCtx->SwappedBuffer, (ULONG)Data->IoStatus.Information, DECRYPT))
			{
				RtlCopyMemory(origBuf,
					p2pCtx->SwappedBuffer,
					Data->IoStatus.Information);
			}
			else {

				LOG_PRINT(LOGFL_ERRORS,
					("PostReadBuffersDecWhenSafe: Decryption failed : read : %d, offset : %ld\n",
					iopb->Parameters.Read.Length,
					iopb->Parameters.Read.ByteOffset));
			}
		}
	}

	//
	//  Free allocated memory and release the volume context
	//

	FltFreePoolAlignedWithTag(FltObjects->Instance,
		p2pCtx->SwappedBuffer,
		BUFFER_SWAP_TAG);

	FltReleaseContext(p2pCtx->VolCtx);

	ExFreeToNPagedLookasideList(&Pre2PostContextList,
		p2pCtx);

	return FLT_POSTOP_FINISHED_PROCESSING;
}