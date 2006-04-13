#include <stdio.h>
#include <stdlib.h>
#include "canlib.h"

int
main ()
{
  int fd, r;
  unsigned char buffer[8];
  unsigned int len, arbitration, rtr, i;

  printf ("Opening CAN device\n");
  fd = can_open ();
  printf ("CAN status = %d\n", can_status (fd));
  printf ("Disabling all filters and masks\n");
  can_disable_all_filters_and_masks (fd);
  printf ("Setting mask 0 to 0\n");
  can_set_mask (fd, 0, 0);
  printf ("Setting loopback mode\n");
  can_loopback (fd);
  buffer[0] = 0xAA;
  buffer[1] = 0x55;
  printf ("CAN status = %d\n", can_status (fd));
  printf ("Sending message\n");
  can_send (fd, 2, 0x123, buffer, 0);
  printf ("Trashing data\n");
  buffer[0] = 0x00;
  buffer[1] = 0x00;
  printf ("CAN status = %d\n", can_status (fd));
  printf ("Receiving message\n");
  r = can_receive (fd, &len, &arbitration, buffer, &rtr);
  if (r) {
    perror ("can_receive");
    exit (1);
  }
  printf ("Message length: %d\n", len);
  printf ("Arbitration:    0x%x\n", len);
  printf ("RTR:            %s\n", rtr ? "set" : "not set");
  printf ("Data:\n");
  if (len > 8) {
    printf ("Limiting length to 8 first bytes\n");
    len = 8;
  }
  for (i = 0; i < len; i++)
    printf ("   0x%02x\n", buffer[i]);
  exit (0);
}
