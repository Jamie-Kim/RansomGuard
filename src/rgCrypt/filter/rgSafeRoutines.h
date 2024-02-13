#ifndef __rgSafeRoutines_H__
#define __rgSafeRoutines_H__

#include <fltKernel.h>


/*************************************************************************
Prototypes
*************************************************************************/

FLT_POSTOP_CALLBACK_STATUS
PostReadBuffersDecWhenSafe(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
);

FLT_POSTOP_CALLBACK_STATUS
PostWriteBuffersEncWhenSafe(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
);

FLT_POSTOP_CALLBACK_STATUS
PostWriteBuffersWhenSafe(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
);

FLT_POSTOP_CALLBACK_STATUS
PostReadBuffersWhenSafe(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
);

FLT_POSTOP_CALLBACK_STATUS
PostDirCtrlBuffersWhenSafe(
_Inout_ PFLT_CALLBACK_DATA Data,
_In_ PCFLT_RELATED_OBJECTS FltObjects,
_In_ PVOID CompletionContext,
_In_ FLT_POST_OPERATION_FLAGS Flags
);

BOOLEAN
WriteSafeCache(
_In_ PFILE_OBJECT FileObject,
_In_ PLARGE_INTEGER FileOffset,
_In_ ULONG Length,
_In_ BOOLEAN Wait,
_In_reads_bytes_(Length) PVOID Buffer
);

#endif
