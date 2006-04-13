#ifndef _SPICAN_H
#define _SPICAN_H

#include <asm/ioctl.h>

struct spican_sid {
  unsigned char n;
  unsigned int arbitration;
};

#define SPICAN_IOCTL_BASE     		'g'
#define SPICAN_IO(n)          		_IO(SPICAN_IOCTL_BASE,n)
#define SPICAN_IOR(n,t)       		_IOR(SPICAN_IOCTL_BASE,n,t)
#define SPICAN_IOW(n,t)			_IOW(SPICAN_IOCTL_BASE,n,t)

#define SPICAN_IOCTL_CHOOSE_SETMASK	SPICAN_IOW(0x00,struct spican_sid *)
#define SPICAN_IOCTL_CHOOSE_SETFILTER	SPICAN_IOW(0x01,struct spican_sid *)
#define SPICAN_IOCTL_STATUS             SPICAN_IOR(0x02,unsigned char *)
#define SPICAN_IOCTL_SET_MODE           SPICAN_IOW(0x03,unsigned char)
#define SPICAN_IOCTL_ENABLE_INTERRUPTS  SPICAN_IOW(0x04, unsigned char)
#define SPICAN_IOCTL_ENABLE_RESET       SPICAN_IOW(0x05, unsigned char)

/* Status bits */
#define MESSAGE_READY             0x01
#define TRANSMIT_BUFFER_AVAILABLE 0x02

/* Mode */
#define SPICAN_MODE_NORMAL        0x00
#define SPICAN_MODE_LOOPBACK      0x01

#endif /* SPICAN_H */
