\ Serial to DMX512 interface. Yet untested.
\ The protocol is:
\   ???!XXYD
\ where "?", "!" and "D" are character literals, XX a two bytes (network
\ order) channel and Y a one byte data value.
\ The initial state is all-zeroes and the software keeps track of the
\ highest channel used and will transmit chunks of 32 values (most
\ device seem to need at least 24) but will stop after a chunk when
\ no higher device has been set to avoid transmitting long frames.
\
\ The PIC has to be clocked at 40MHz and the serial speed must not
\ be higher than 200kbps. 57600 is a safe bet, 115200 should work.
\
\ Writing to special channel 0 has the following meaning:
\   0:     reset highest channel number to zero
\   other: undefined

needs lib/tty-rs232.fs

variable highest
variable index
variable current
cvariable step
variable assignment-device
cvariable assignment-value

\ It is best to create this large area in the last position to allow
\ other variables to be placed in bank 1 which is addressable through
\ the BSR. Access to channels are always indirect so this can only
\ be a net gain.
create channels 512 allot

PORTB 0 bit DMXOUT

: set-channel ( data channel -- ) channels 1 - + c! ;

: reset-channels ( -- )
  255 cfor 0 cr@ 2dup set-channel 256 + set-channel cnext
  0 0 set-channel
  0 256 set-channel
  0 highest !
;

: set-channel-0 ( data -- )
  0= if reset-channels then ;

: reset-step ( -- ) 0 step c! ;

: advance-step ( -- ) 1 step c+! ;

: handle-step-0 ( char -- ) [CHAR] ! = if advance-step then ;

: handle-step-1 ( char -- )
  switchw
    0 casew 0 assignment-device ! advance-step
    1 casew 256 assignment-device ! advance-step
    2 casew 512 assignment-device ! advance-step
    defaultw reset-step
  endswitchw
;

: handle-step-2 ( char -- )
  assignment-device +!
  assignment-device c@ 512 <= if advance-step else reset-step then
;

: handle-step-3 ( char -- ) assignment-value c! advance-step ;

: handle-step-4 ( char -- )
  [CHAR] D <> if reset-step exit then  
  assignment-value c@ assignment-device @
  dup 0= if drop set-channel-0 else set-channel then
  assignment-device @ highest @ > if
    assignment-device @ highest !
  then
  reset-step
;

: handle-step ( char -- )
  step c@
  switchw
    0 casew handle-step-0
    1 casew handle-step-1
    2 casew handle-step-2
    3 casew handle-step-3
    4 casew handle-step-4
  endswitchw
;

: handle-serial-port ( -- )
  key? 0= if exit then
  key handle-step
;

: reset-transmission ( -- ) channels index ! 32 current ! ;

: set-output ( b -- ) if DMXOUT bit-set else DMXOUT bit-clr then ;

\ Set output line according to high bit of W and shift it left in 10 cycles
\ 8 cycles are used before bit is set on the line and 2 after for return

: set-zero ( -- ) nop DMXOUT bit-clr ; no-inline
: set-one ( -- ) DMXOUT bit-set ; no-inline

code set-bit
  WREG ,w ,a rlcf
  C ,a btfss
  set-zero goto
  set-one goto
  return
;code no-inline

\ Wait for 28 cycles (2 to call, 2 to return, 24 nop)
  
: nop28 ( -- )
  nop nop nop nop nop nop nop nop nop nop nop nop
  nop nop nop nop nop nop nop nop nop nop nop nop
; no-inline

\ Wait for 29 cycles (nop28, 1 nop)

: nop29 ( -- ) nop28 nop ; inline

\ Wait for 30 cycles (nop29, 1 nop)

: nop30 ( -- ) nop29 nop ; inline

\ Wait for 36 cycles (2 to call, 2 to return, nop30, 2 nop)

: nop36 ( -- )
  nop30 nop nop
; no-inline

: transmit-zero ( -- ) 0 >w set-bit nop36 ; no-inline
: transmit-one ( -- ) 255 >w set-bit nop36 ; no-inline
  
: transmit-break ( -- ) 25 cfor transmit-zero cnext ; \ Required: 22

: transmit-mab ( -- ) transmit-one transmit-one ; \ Mark after break

: transmit-stop ( -- ) transmit-mab ;

: transmit-byte ( b -- )
  0 >w set-bit   \ 2 cycles elapsed since set
  nop28
  >w          \ 2 + 28 + 2 = 34
  set-bit     \ 2 + 28 + 2 + 8 = 40, 2 elapsed
  nop30
  set-bit     \ 2 + 30 + 8 = 40, 2 elapsed
  nop30
  set-bit     \ 2 + 30 + 8 = 40, 2 elapsed
  nop30
  set-bit     \ 2 + 30 + 8 = 40, 2 elapsed
  nop30
  set-bit     \ 2 + 30 + 8 = 40, 2 elapsed
  nop30
  set-bit     \ 2 + 30 + 8 = 40, 2 elapsed
  nop30
  set-bit     \ 2 + 30 + 8 = 40, 2 elapsed
  nop30
  set-bit     \ 2 + 30 + 8 = 40, 2 elapsed
  1 >w        \ 2 + 1 = 3
  nop29
  set-bit     \ 2 + 1 + 29 + 8 = 40, 2 elapsed
  transmit-stop
;

: transmit-current ( -- ) index @ dup c@ transmit-byte 1+ index ! ;
  
: transmit-32 ( -- ) 32 cfor transmit-current cnext ;

: transmit-more ( -- f )
  current @ highest @ >= if 0 exit then
  transmit-32
  current @ 32 + current ! 1 exit
;

: transmit-some ( -- )
  transmit-32
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more 0= if exit then
  transmit-more drop
;

: transmit-all ( -- )
  reset-transmission
  transmit-break
  transmit-mab
  0 transmit-byte           \ SC byte: protocol 0 => DMX 512
  transmit-some
;

: init ( -- )
  1 set-output              \ idle state
  0 step c!                 \ reset serial port state
  reset-channels
;

: mainloop ( -- )
  begin transmit-all again ;

: main ( -- ) init mainloop ;