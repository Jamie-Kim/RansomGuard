#ifndef DISKUTIL_H_INCLUDED
#define DISKUTIL_H_INCLUDED

#include <string>
#include <vector>

using std::vector;
using std::wstring;

namespace DiskUtil
{
	HRESULT HasNoSeekPenalty(const wstring& physical_drive_path);
	vector<int> GetExtentsFromPath(const wstring& path);
	HRESULT HasNominalMediaRotationRate(const wstring& physical_drive_path);
}

#endif //DISKUTIL_H_INCLUDED