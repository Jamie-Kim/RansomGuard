
/*
* rc4.h
*/

#ifndef _RC4_H_
#define _RC4_H_

typedef unsigned char u_char;

typedef struct {
	u_char	perm[256];
	u_char	index1;
	u_char	index2;
}rc4_state;

extern void rc4_init(rc4_state *state, const u_char *key, int keylen);
extern void rc4_crypt(rc4_state * state, u_char *buf, int buflen);

#endif