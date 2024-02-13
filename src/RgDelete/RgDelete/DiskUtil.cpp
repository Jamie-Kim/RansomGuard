#include "stdafx.h"
#include <WinIoCtl.h>
#include <Ntddscsi.h>
#include <Setupapi.h>

#include <iostream>
#include "DiskUtil.h"

// Returns S_OK if |physical_drive_path| has no seek penalty.
// Returns S_FALSE otherwise.
// Returns E_FAIL if fails to retrieve the status.
// |physical_drive_path| should be something like
// "\\\\.\\PhysicalDrive0".
HRESULT DiskUtil::HasNoSeekPenalty(const wstring& physical_drive_path)
{
	// We do not need write permission.
	const HANDLE handle = ::CreateFileW(
		physical_drive_path.c_str(), FILE_READ_ATTRIBUTES,
		FILE_SHARE_READ | FILE_SHARE_WRITE, NULL,
		OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);

	if (handle == INVALID_HANDLE_VALUE) {
		return E_FAIL;
	}

	STORAGE_PROPERTY_QUERY query_seek_penalty = {
		StorageDeviceSeekPenaltyProperty,  // PropertyId
		PropertyStandardQuery,             // QueryType,
	};

	DEVICE_SEEK_PENALTY_DESCRIPTOR query_seek_penalty_desc = {};
	DWORD returned_query_seek_penalty_size = 0;

	const BOOL query_seek_penalty_result = DeviceIoControl(
		handle, IOCTL_STORAGE_QUERY_PROPERTY,
		&query_seek_penalty, sizeof(query_seek_penalty),
		&query_seek_penalty_desc,
		sizeof(query_seek_penalty_desc),
		&returned_query_seek_penalty_size, NULL);

	CloseHandle(handle);

	if (!query_seek_penalty_result) {
		// failed to retrieve data.
		return E_FAIL;
	}

	return !query_seek_penalty_desc.IncursSeekPenalty ? S_OK : S_FALSE;
}

// Returns S_OK if |physical_drive_path| has nominal media
// rotation rate in terms of ATA8-ACS specification.
// http://www.t13.org/Documents/UploadedDocuments/docs2007/D1699r4-ATA8-ACS.pdf#Page=179
// Returns S_FALSE otherwise.
// Returns E_FAIL if fails to retrieve the status.
// |physical_drive_path| should be something like
// "\\\\.\\PhysicalDrive0".
HRESULT DiskUtil::HasNominalMediaRotationRate(const wstring& physical_drive_path)
{
	// In order to use IOCTL_ATA_PASS_THROUGH,
	// We *do* need read/write permission, which means
	// that the caller has admin privilege.
	const HANDLE handle = CreateFileW(
		physical_drive_path.c_str(),
		GENERIC_READ | GENERIC_WRITE,
		FILE_SHARE_READ | FILE_SHARE_WRITE, NULL,
		OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
	if (handle == INVALID_HANDLE_VALUE) {
		return E_FAIL;
	}

	struct ATAIdentifyDeviceQuery {
		ATA_PASS_THROUGH_EX header;
		WORD data[256];
	};

	ATAIdentifyDeviceQuery id_query = {};
	id_query.header.Length = sizeof(id_query.header);
	id_query.header.AtaFlags = ATA_FLAGS_DATA_IN;
	id_query.header.DataTransferLength = sizeof(id_query.data);
	id_query.header.TimeOutValue = 3;  // sec
	id_query.header.DataBufferOffset = sizeof(id_query.header);
	id_query.header.CurrentTaskFile[6] = 0xec;  // ATA IDENTIFY DEVICE command

	DWORD retval_size = 0;
	const BOOL result = DeviceIoControl(
		handle, IOCTL_ATA_PASS_THROUGH,
		&id_query, id_query.header.DataTransferLength,
		&id_query, id_query.header.DataTransferLength,
		&retval_size, NULL);

	if (!result)
	{
		return E_FAIL;
	}

	const int kNominalMediaRotRateWordIndex = 217;

	// RPM == 1 means this is non-rotate device
	return id_query.data[kNominalMediaRotRateWordIndex] == 1 ? S_OK : S_FALSE;
}

vector<int> DiskUtil::GetExtentsFromPath(const wstring& path)
{
	vector<int> extents;

	wchar_t mount_point[1024];
	if (!GetVolumePathNameW(
		path.c_str(), mount_point, ARRAYSIZE(mount_point))) {
		return extents;
	}

	wchar_t volume_name[1024];
	if (!GetVolumeNameForVolumeMountPointW(
		mount_point, volume_name, ARRAYSIZE(volume_name))) {
		return extents;
	}

	wstring volume = volume_name;

	// remove trailing '\\'
	volume.resize(volume.size() - 1);

	// We do not need write permission (nor admin rights).
	const HANDLE volume_handle = CreateFileW(
		volume.c_str(), FILE_READ_ATTRIBUTES,
		FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING,
		FILE_ATTRIBUTE_NORMAL, NULL);

	VOLUME_DISK_EXTENTS initial_buffer = {};
	DWORD returned_size = 0;

	const BOOL get_volume_disk_result = DeviceIoControl(
		volume_handle, IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
		NULL, 0, &initial_buffer, sizeof(initial_buffer),
		&returned_size, NULL);

	const DWORD query_size_error = GetLastError();

	if (get_volume_disk_result != FALSE &&
		initial_buffer.NumberOfDiskExtents == 1) {
		extents.push_back(initial_buffer.Extents[0].DiskNumber);
		return extents;
	}

	if (query_size_error != ERROR_MORE_DATA) {
		return extents;
	}

	const size_t buffer_size = sizeof(initial_buffer.NumberOfDiskExtents)
		+ sizeof(initial_buffer.Extents) * initial_buffer.NumberOfDiskExtents;

	char* underlaying_buffer = new char[buffer_size];

	VOLUME_DISK_EXTENTS* query_buffer =
		reinterpret_cast<VOLUME_DISK_EXTENTS *>(
		&underlaying_buffer[0]);

	const BOOL devide_ioc_result = DeviceIoControl(
		volume_handle, IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
		NULL, 0,
		query_buffer, buffer_size, &returned_size, NULL);

	const DWORD device_detail_result_error = ::GetLastError();
	if (!!devide_ioc_result) {
		for (DWORD i = 0;
			i < query_buffer->NumberOfDiskExtents; ++i) {
			extents.push_back(query_buffer->Extents[i].DiskNumber);
		}
	}

	delete[] underlaying_buffer;
	CloseHandle(volume_handle);

	return extents;
}