needs lib/tty-rs232.fs

\
\ SPI to CAN interface
\
\ This module configures the PIC as a SPI slave which will transmit
\ messages on the CAN bus.
\
\ Protocol:
\    SETMASK SIDL SIDH N
\    SETFILTER SIDL SIDH N
\    READMSG -> LEN RTR SIDH SIDL D0 .. D7
\    WRITEMSG LEN SIDL SIDH D0 .. D7 RTR
\    STATUSMSG -> STATUS
\    SETMODE MODE
\
\ The STATUS byte contains:
\   - bit 0: 1 if a message is available for reception
\   - bit 1: 1 if a transmit buffer is available
\   - bit 7: reserved, set by the Stratix
\
\ The mode is 0 for normal (default at boot time and after configuration)
\ and 1 for loopback.
\
\ If a low pulse is generated on RTS (by setting its output to 0 then in
\ input mode), it indicates that the next byte is ready to be received
\
\ If RTS is down, the PIC won't start. If it goes down in the middle,
\ the PIC will reset.

0x00 constant SETMASK
0x01 constant SETFILTER
0x02 constant READMSG
0x03 constant WRITEMSG
0x04 constant SETMODE
0xAA constant GETSTATUS

LATA  5 bit STATUSCHANGED
LATC  2 bit RTSLATCH
PORTC 2 bit RTSPORT
TRISC 2 bit RTSDIR

variable oldstatus

: spi-init ( -- )
  STATUSCHANGED bit-clr
  RTSLATCH bit-clr RTSDIR bit-set
  TRISC 5 bit-clr TRISC 3 bit-set TRISA 5 bit-clr
  0x40 SSPSTAT c! 0x25 SSPCON1 c!
;

: wait-for-rts ( -- ) begin RTSPORT bit-set? until ;

: check-rts ( -- )
  nop nop nop nop nop nop nop nop nop nop
  RTSPORT bit-clr? if [char] X emit [char] . emit cr reset then ;

: rts-pulse ( -- )
  RTSDIR bit-clr
  nop nop nop nop nop nop nop nop nop nop
  RTSDIR bit-set
  nop nop nop nop nop nop nop nop nop nop
 ;

: get-status ( -- status )
  0 can-msg-present? if 1 or then can-free-txbuffer? if 2 or then ;

: check-for-status-change ( -- )
  get-status dup oldstatus @ = if drop exit then
  oldstatus ! STATUSCHANGED bit-toggle
;

\ Receive byte, blocking mode
: spi> ( -- c )
  SSPIF bit-clr? if rts-pulse then   \ Ask for a byte
  begin check-rts SSPIF bit-set? until
  SSPBUF c@
  SSPIF bit-clr ;

\ Send byte, blocking mode
: >spi ( c -- )
  SSPBUF c! rts-pulse
  begin check-rts SSPIF bit-set? until
  SSPIF bit-clr
  SSPBUF c@ >w         \ Dummy read
;

: handle-getstatus ( -- ) get-status >spi ;

: handle-readmsg ( -- )
  can-receive
  can-msg-length c@ >spi can-msg-rtr bit-set? if 1 >spi else 0 >spi then
  can-arbitration @ 1>2 >spi >spi
  can-msg-0 c@ >spi can-msg-1 c@ >spi can-msg-2 c@ >spi can-msg-3 c@ >spi
  can-msg-4 c@ >spi can-msg-5 c@ >spi can-msg-6 c@ >spi can-msg-7 c@ >spi
;

: handle-writemsg ( -- )
  spi> can-msg-length c! spi> spi> 2>1 can-arbitration !
  spi> can-msg-0 c! spi> can-msg-1 c! spi> can-msg-2 c! spi> can-msg-3 c!
  spi> can-msg-4 c! spi> can-msg-5 c! spi> can-msg-6 c! spi> can-msg-7 c!
  spi> if can-transmit-rtr else can-transmit then
;

: handle-setmask ( -- )
  spi> spi> 2>1 spi> can-config can-set-mask can-normal ;

: handle-setfilter ( -- )
  spi> spi> 2>1 spi> can-config can-set-filter can-normal ;

: handle-setmode ( -- )
  spi> if can-loopback else can-normal then ;

\ Main logic for one command
: handle-command ( -- )
  spi>
  dup GETSTATUS = if drop handle-getstatus exit then
  dup SETMASK = if drop handle-setmask exit then
  dup SETFILTER = if drop handle-setfilter exit then
  dup READMSG = if drop handle-readmsg exit then
  dup WRITEMSG = if drop handle-writemsg exit then
  dup SETMODE = if drop handle-setmode exit then
  drop
;

\ Do work
: mainloop ( -- )
  ." Starting" cr
  rts-pulse       \ Get ready for initial command
  begin
    check-rts
    \ Clear overflow if set at PIC start
    SSPOV bit-set? if SSPOV bit-clr then
    WCOL bit-set? if WCOL bit-clr then
    SSPIF bit-set? if
      handle-command
      rts-pulse   \ Get ready for next command
    then
    check-for-status-change
  again ;

\ Main program
: main ( -- ) spi-init can-init wait-for-rts mainloop ;
