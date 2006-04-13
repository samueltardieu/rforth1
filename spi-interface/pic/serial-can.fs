\ Serial to CAN interface
\
\ Protocol: AA arbl arbh DLC d0 d1 .. d7 cksum
\ used in both directions.
\ AA is used as a marker, if it is not the first character, it is waiten for.
\ If the cksum is wrong, the message is not considered valid. The checksum
\ is a simple xor between all bytes and the result should be 0.
\

create s2c-buffer 12 allot
cvariable s2c-count
cvariable s2c-cksum

: s2c-reset ( -- ) 0xff s2c-count c! 0 s2c-cksum c! ;

: s2c-send ( -- )
  s2c-buffer @ can-arbitration !
  s2c-buffer 2 + c@ 0xf and can-msg-length c!
  s2c-buffer 3 + c@ can-msg-0 c!
  s2c-buffer 4 + c@ can-msg-1 c!
  s2c-buffer 5 + c@ can-msg-2 c!
  s2c-buffer 6 + c@ can-msg-3 c!
  s2c-buffer 7 + c@ can-msg-4 c!
  s2c-buffer 8 + c@ can-msg-5 c!
  s2c-buffer 9 + c@ can-msg-6 c!
  s2c-buffer 10 + c@ can-msg-7 c!
  can-transmit
;

: serial-key ( -- ) RCREG c@ ;

: handle-serial ( -- )
  \ Check that 0xAA is the first character of the frame
  s2c-count c@ 0xff = if serial-key 0xAA = if 0 s2c-count c! then exit then
  serial-key dup s2c-cksum c@ xor s2c-cksum c!  \ Add to checksum
  s2c-buffer s2c-count c@ + c!           \ Store character
  s2c-count c@ 1+ s2c-count c!           \ Increment counter
  s2c-count c@ 12 = if
    \ Message complete, send if checksum is ok
    s2c-cksum c@ 0= if s2c-send then
    s2c-reset
  then
;

221 constant c2s-buffer-size
create c2s-buffer c2s-buffer-size allot
cvariable c2s-next-to-read
cvariable c2s-next-to-write
cvariable c2s-cksum

: c2s-inc ( n -- n' ) 1+ dup c2s-buffer-size >= if drop 0 then ;

: left ( -- n )
  c2s-next-to-read c@ c2s-next-to-write c@ -
  dup 0<= if 130 + then
;

: c2s-add-to-buffer ( c -- )
  c2s-buffer c2s-next-to-write c@ + c!
  c2s-next-to-write c@ c2s-inc c2s-next-to-write c!
;

: c2s-add-to-buffer-cks ( c -- )
  dup c2s-add-to-buffer c2s-cksum c@ xor c2s-cksum c! ;

: c2s-get-first-char ( -- c )
  c2s-buffer c2s-next-to-read c@ + c@
  c2s-next-to-read c@ c2s-inc c2s-next-to-read c!
;

: c2s-transmit-next-if-needed ( -- )
  c2s-next-to-read c@  c2s-next-to-write c@ = if exit then
  LATA 2 bit-toggle
  c2s-get-first-char TXREG c!
;

: c2s-queue-can-msg ( -- )
  can-receive
  0xAA c2s-add-to-buffer
  0 c2s-cksum c!
  can-arbitration @ 1>2 swap c2s-add-to-buffer-cks c2s-add-to-buffer-cks
  can-msg-length c@ c2s-add-to-buffer-cks
  can-msg-0 c@ c2s-add-to-buffer-cks
  can-msg-1 c@ c2s-add-to-buffer-cks
  can-msg-2 c@ c2s-add-to-buffer-cks
  can-msg-3 c@ c2s-add-to-buffer-cks
  can-msg-4 c@ c2s-add-to-buffer-cks
  can-msg-5 c@ c2s-add-to-buffer-cks
  can-msg-6 c@ c2s-add-to-buffer-cks
  can-msg-7 c@ c2s-add-to-buffer-cks
  c2s-cksum c@ c2s-add-to-buffer
;

: mainloop ( -- )
  begin
    TXIF bit-set? if c2s-transmit-next-if-needed then
    RCIF bit-set? if handle-serial then
    can-msg-present? if left 13 >= if c2s-queue-can-msg then then
  again
;

: main ( -- )
  can-init can-config 0 0 can-set-mask can-normal s2c-reset
  0x222 can-arbitration ! 0 can-msg-length c! can-transmit
  mainloop ;