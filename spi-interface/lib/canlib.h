#ifndef _CANLIB_H
#define _CANLIB_H

#include <linux/spican.h>

int can_open ();
int can_close (int fd);
int can_normal (int fd);
int can_loopback (int fd);
int can_status (int fd);
int can_set_mask (int fd, unsigned char n, unsigned int mask);
int can_set_filter (int fd, unsigned char n, unsigned int filter);
int can_disable_all_filters_and_masks (int fd);
int can_send (int fd, unsigned int len, unsigned int arbitration,
	      unsigned char *data, unsigned int rtr);
int can_receive (int fd, unsigned int *len, unsigned int *arbitration,
		 unsigned char *data, unsigned int *rtr);
int can_reset (int fd);

#endif /* _CANLIB_H */
