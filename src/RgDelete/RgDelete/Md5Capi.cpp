#include "stdafx.h"
#include "Md5Capi.h"

#ifdef _DEBUG
#undef THIS_FILE
static char THIS_FILE[]=__FILE__;
#define new DEBUG_NEW
#endif

//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////

Cmd5Capi::Cmd5Capi()
{
	csDigest.Empty();
}

Cmd5Capi::Cmd5Capi(CString & csBuffer)
{
	Digest(csBuffer);
}

Cmd5Capi::~Cmd5Capi()
{

}

CString &Cmd5Capi::GetDigest(void)
{
	return csDigest;
}

CString &Cmd5Capi::Digest(CString & csBuffer)
{
	HCRYPTPROV hCryptProv;
	HCRYPTHASH hHash;
	BYTE bHash[0x7f];
	DWORD dwHashLen = 16; // The MD5 algorithm always returns 16 bytes. 
	DWORD cbContent = csBuffer.GetLength();

	//make it to support in UNICODE
	BYTE* pbContent;
	pbContent = (BYTE*)malloc(cbContent);
	memcpy((LPSTR)pbContent, CT2A(csBuffer), cbContent);

	if (CryptAcquireContext(&hCryptProv,
		NULL, NULL, PROV_RSA_FULL, CRYPT_VERIFYCONTEXT | CRYPT_MACHINE_KEYSET))
	{
		if (CryptCreateHash(hCryptProv,
			CALG_MD5,	// algorithm identifier definitions see: wincrypt.h
			0, 0, &hHash))
		{
			if (CryptHashData(hHash, pbContent, cbContent, 0))
			{

				if (CryptGetHashParam(hHash, HP_HASHVAL, bHash, &dwHashLen, 0))
				{
					// Make a string version of the numeric digest value
					csDigest.Empty();
					CString tmp;
					for (int i = 0; i < 16; i++)
					{
						tmp.Format(_T("%02x"), bHash[i]);
						csDigest += tmp;
					}
				}
			}
		}
	}

	CryptDestroyHash(hHash);
	CryptReleaseContext(hCryptProv, 0);
	csBuffer.ReleaseBuffer();

	free(pbContent);

	return csDigest;
}
