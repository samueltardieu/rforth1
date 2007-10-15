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

variable highest                   \ Highest channel used so far
variable index                     \ Address of the channel being sent
variable current                   \ Latest transmitted channel
cvariable state                    \ Current state of the state machine
variable assignment-device         \ Channel to change (state machine)
cvariable assignment-value         \ Value to set to channel (state machine)

\ It is best to create this large area in the last position to allow
\ other variables to be placed in bank 1 which is addressable through
\ the BSR. Access to channels are always indirect so this can only
\ be a net gain.
create channels 512 allot

LATB 0 bit DMXOUT                  \ Shortcut to the port to use

\ Set a channel to a given value
: set-channel ( value channel -- ) channels 1- + c! ;

\ Set all channels to zero -- note that the loop can count 255 times as
\ a maximum, so channels 1 (stored as 0) and 257 (stored as 1) must
\ be set explicitely. Also, mark every channel as unset yet.
: reset-channels ( -- )
  255 cfor 0 cr@ 2dup set-channel 256 + set-channel cnext
  0 0 set-channel
  0 256 set-channel
  0 highest !
;

\ Channel 0 is a virtual channel used to send commands to this program
: set-channel-0 ( data -- )
  0= if reset-channels then ;

\ Reset state machine
: reset-state ( -- ) 0 state c! ;

\ Go to next state
: advance-state ( -- ) 1 state c+! ;

\ State 0: wait for '!'
: handle-state-0 ( char -- ) [CHAR] ! = if advance-state then ;

\ State 1: wait for high byte of channel address and check for address validity
: handle-state-1 ( char -- )
  switchw
    0 casew 0 assignment-device ! advance-state
    1 casew 256 assignment-device ! advance-state
    2 casew 512 assignment-device ! advance-state
    defaultw reset-state
  endswitchw
;

\ State 2: wait for low byte of channel address and check address validity
: handle-state-2 ( char -- )
  assignment-device +!
  assignment-device c@ 512 <= if advance-state else reset-state then
;

\ State 3: read value to assign to previously read channel
: handle-state-3 ( char -- ) assignment-value c! advance-state ;

\ State 4: wait for 'D', make assignment and mark channel as used
: handle-state-4 ( char -- )
  [CHAR] D <> if reset-state exit then  
  assignment-value c@ assignment-device @
  dup 0= if drop set-channel-0 else set-channel then
  assignment-device @ highest @ > if
    assignment-device @ highest !
  then
  reset-state
;

\ State machine: depending on the current state, handle incoming character
: handle-state ( char -- )
  state c@
  switchw
    0 casew handle-state-0
    1 casew handle-state-1
    2 casew handle-state-2
    3 casew handle-state-3
    4 casew handle-state-4
  endswitchw
;

\ If a character is present on the serial port, handle it through the state
\ machine
: handle-serial-port ( -- ) key? if key handle-state then ;

\ Start transmission by transmitting the 32 first channels in any case
: reset-transmission ( -- ) channels index ! 32 current ! ;

\ Set output line according to low bit of W and shift it right in 10 cycles
\ 8 cycles are used before bit is set on the line and 2 after for return
: set-zero ( -- ) nop DMXOUT bit-clr ; no-inline
: set-one ( -- ) DMXOUT bit-set ; no-inline
code set-bit
  WREG ,w ,a rrcf
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

\ Set a low or high state on the DMX line and wait for an approximate
\ time (no need to be precise here)
: transmit-zero ( -- ) 0 >w set-bit nop36 ; no-inline
: transmit-one ( -- ) 255 >w set-bit nop36 ; no-inline

\ Control the line -- no need to be precise
: transmit-break ( -- ) 25 cfor transmit-zero cnext ; \ Required: at least 22
: transmit-mab ( -- ) transmit-one transmit-one ;     \ Mark after break
: transmit-stop ( -- ) transmit-mab ;

\ Transmit byte with a very precise timing (40 cycles at 40MHz, 4 clock
\ cycles per instruction, gives the expected timing for 250kbps)
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
  255 >w      \ 2 + 1 = 3
  nop29
  set-bit     \ 2 + 1 + 29 + 8 = 40, 2 elapsed
  transmit-stop
;

\ Transmit current channel
: transmit-current ( -- ) index @ dup c@ transmit-byte 1+ index ! ;

\ Transmit the next 32 channels
: transmit-32 ( -- ) 32 cfor transmit-current cnext ;

\ Transmit the next 32 channels if they have been used
: transmit-more ( -- f )
  current @ highest @ >= if 0 exit then
  transmit-32
  current @ 32 + current ! 1 exit
;

\ Transmit some of the channels onto the DMX bus -- at least 32 channels
\ must be transmitted anyway (some devices seem to hang if less than 24
\ channels are transmitted in a DMX packet), so group them by 32
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

\ Send a whole DMX packet
: transmit-all ( -- )
  reset-transmission
  transmit-break
  transmit-mab
  0 transmit-byte           \ SC byte: protocol 0 => DMX 512
  transmit-some
;

\ Initial conditions
: init ( -- )
  TRISB 0 bit-clr           \ PORTB0 as output
  transmit-one              \ Idle state, line must be set to high level
  0 state c!                \ Reset state machine
  reset-channels
;

\ Main loop: transmit indefinitely
: mainloop ( -- ) begin transmit-all again ;

\ Main program
: main ( -- ) init mainloop ;
