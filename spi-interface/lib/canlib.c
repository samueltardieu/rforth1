#include <sys/types.h>
#include <sys/ioctl.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <string.h>
#include <unistd.h>
#include "canlib.h"

int
can_open ()
{
  return open ("/dev/misc/spican", O_RDWR);
}

int
can_close (int fd)
{
  return close (fd);
}

int
can_normal (int fd)
{
  return ioctl (fd, SPICAN_IOCTL_SET_MODE, SPICAN_MODE_NORMAL);
}

int
can_loopback (int fd)
{
  return ioctl (fd, SPICAN_IOCTL_SET_MODE, SPICAN_MODE_LOOPBACK);
}

int can_status (int fd)
{
  unsigned char status;
  if (ioctl (fd, SPICAN_IOCTL_STATUS, &status) < 0) return -1;
  return status;
}

int can_reset (int fd)
{
  if (ioctl (fd, SPICAN_IOCTL_ENABLE_INTERRUPTS, 0) < 0) return -1;
  if (ioctl (fd, SPICAN_IOCTL_ENABLE_RESET, 1) < 0) return -1;
  /* PIC will be automatically resetted because RTS is asserted low */
  sleep (1);
  if (ioctl (fd, SPICAN_IOCTL_ENABLE_INTERRUPTS, 1) < 0) return -1;
  if (ioctl (fd, SPICAN_IOCTL_ENABLE_RESET, 0) < 0) return -1;
  return 0;
}

int
can_set_mask (int fd, unsigned char n, unsigned int mask)
{
  struct spican_sid s = {n, mask};
  return ioctl (fd, SPICAN_IOCTL_CHOOSE_SETMASK, &s);
}

int
can_set_filter (int fd, unsigned char n, unsigned int filter)
{
  struct spican_sid s = {n, filter};
  return ioctl (fd, SPICAN_IOCTL_CHOOSE_SETFILTER, &s);
}

int can_disable_all_filters_and_masks (int fd)
{
  int i;
  for (i = 0; i < 2; i++)
    if (can_set_mask (fd, i, 0x7ff) < 0) return -1;
  for (i = 0; i < 6; i++)
    if (can_set_filter (fd, i, 0x7ff) < 0) return -1;
  return 0;
}

int
can_send (int fd, unsigned int len, unsigned int arbitration,
	  unsigned char *data, unsigned int rtr)
{
  unsigned char buffer[12];
  buffer[0] = len;
  buffer[1] = arbitration & 0xff;
  buffer[2] = arbitration >> 8;
  memcpy (&buffer[3], data, len);
  buffer[11] = rtr;
  return write (fd, buffer, 12);
}

int
can_receive (int fd, unsigned int *len, unsigned int *arbitration,
	     unsigned char *data, unsigned int *rtr)
{
  unsigned char buffer[12];
  if (read (fd, buffer, 12) != 12) return -1;
  *len = buffer[0];
  *rtr = buffer[1];
  *arbitration = (buffer[2] << 8 | buffer[3]);
  memcpy (data, &buffer[4], *len);
  return 0;
}

