\
\ SPI to PIC interface
\
\ This module allows the PIC to be entirely controlled by a SPI interface
\ where the PIC acts as a slave.
\
\ Protocol:
\      WRITECMD ADDRH ADDRL DATA DATA DATA ...
\      READCMD ADDRH ADDRL -> DATA DATA DATA ...
\      BTCHCMD ADDRH ADDRL MASK DATA MASK DATA MASK DATA

0x00 constant receiving-command
0x01 constant receiving-address-low
0x02 constant receiving-address-high
0x03 constant writing-data
0x04 constant reading-data
0x05 constant receiving-bit-mask

0x00 constant write-command
0x01 constant read-command
0x02 constant bit-change-command

cvariable current-command
cvariable current-mode

variable current-address

: init-spi ( -- )
  TRISC 5 bit-clr TRISC 3 bit-set TRISA 5 bit-set
  0x40 SSPSTAT c! 0x24 SSPCON1 c! 
;

: reset-logic ( -- ) receiving-command current-mode c! ;

\ Receive byte, blocking mode
: receive-byte ( -- c ) begin SSPIF bit-set? until SSPBUF c@ ;

\ Send bytes until slave select is driven high
: send-bytes ( -- )
  current-address @
  begin
    dup c@
    begin SS bit-set? if 2drop exit then SSPIF bit-clr? until
    SSPBUF c!
    1+
  repeat
;

\ Receive bytes until slave select is driven high
: receive-bytes ( -- )
  current-address @
  begin
    >r
    begin SS bit-set? if rdrop exit then SSPIF bit-set? until
    SSPBUF c@ r@ c!
    r> 1+
  repeat
;

\ Receive mask and bytes until slave select is driven high
: receive-masks-bytes ( -- )
  current-address @
  begin
    >r
    begin SS bit-set? if rdrop exit then SSPIF bit-set? until
    SSPBUF c@ invert r@ c@ and
    begin SS bit-set? if rdrop drop exit then SSPIF bit-set? until
    SSPBUF c@ or r@ c!
    r> 1+
  repeat
;

\ Main logic for one command
: handle-command ( -- )
  receive-byte
  receive-byte current-address c! receive-byte current-address 1 + c!
  dup write-command = if drop send-bytes exit then
  dup read-command = if drop receive-bytes exit then
  dup bit-change-command = if drop receive-masks-bytes then
  drop
;

\ Do work
: mainloop ( -- ) begin handle-command again ;

\ Main program
: main ( -- ) init-spi mainloop ;
