\ Source of the color sensors command card 

needs lib/tty-rs232.fs

\ Port naming
ADCON0 2 bit GO/DONE
LATB 0 bit led1
LATB 1 bit led2
PORTB 4 bit button

\ Sensors leds 
LATA 5 bit leds

\ Status flag
0 value calibrated?

\ Color code
0 constant UNKNOWN
1 constant CREAM
2 constant BROWN
3 constant WHITE

\ Current and previous values
create current-values
variable current-value-0
variable current-value-1
variable current-value-2
variable current-value-3
create current-colors
cvariable current-color-0
cvariable current-color-1
cvariable current-color-2
cvariable current-color-3
create previous-colors 4 allot

\ EEPROM pointer for threshold storage
eevariable cream-white-threshold-eeprom
eevariable brown-cream-threshold-eeprom

\ Outgoing commands
0b0001000100 constant COLORS-ARBITRATION
0b0001000101 constant VALUES-ARBITRATION
0b0001000110 constant SET-CALIBRATION-REQUEST
0b0001000111 constant CALIBRATION-ARBITRATION

\ *************
\ CAN constant
\ XXXXX define the address
0b00000000000 constant local-addr
0b01111100000 constant remote-addr
0b01111100000 constant addr-mask
0b00000011111 constant private-mask
\ Action Mask
0b00000011000 constant request-values-mask
0b00000000100 constant request-color-mask
0b00000000011 constant set-thresholds-mask

\ Color thresholds
variable brown-cream-threshold
variable cream-white-threshold

: 8* 4* 4* ; inline

: can-setup ( -- ) 
  can-init 
  can-config 
  addr-mask 0 can-set-mask 
  local-addr 0 can-set-filter 
  can-loopback
;

: leds-on ( -- ) leds bit-set ; inline
: leds-off ( -- ) leds bit-clr ; inline

: timer0-reset ( -- ) 0x0400 negate TMR0L ! TMR0IF bit-clr ; 
: wait-timer0 ( -- ) timer0-reset  begin TMR0IF bit-set? until ;

: timer1-reset ( -- ) 0 TMR0L ! TMR1IF bit-clr ;
: timer1-expired? ( -- ) TMR1IF bit-set? ; inline

: select-channel ( channel -- ) 8* ADCON0 c@ 0b11000111 and or ADCON0 c! ;

: conversion ( -- result )
  GO/DONE bit-set begin GO/DONE bit-clr? until
  ADRESL @ ;
  
: 4-measures ( -- result  ) 0 4 cfor conversion + cnext ;

: channel-measure ( channel -- result )
  select-channel
  leds-on  wait-timer0 4-measures
  leds-off wait-timer0 4-measures
  - ;

: button-pressed? ( -- ) button bit-set? ; inline

: brown-detected ( -- color ) ." brown detected" cr brown ;
: white-detected ( -- color ) ." white detected" cr white ;
: cream-detected ( -- color ) ." cream detected" cr cream ;

: determine-color ( A/D -- color  )
  dup 
  brown-cream-threshold @ < if drop brown-detected exit then
  cream-white-threshold @ > if drop white-detected exit then
  drop cream-detected ;

: channel-determine-color ( channel -- c ) channel-measure determine-color ;

: update-current-values ( -- )
  4 cfor
    cr@ 1- channel-measure dup cr@ 2* current-values + !
    determine-color current-colors cr@ + c!
  cnext ;

: color-changed? ( n -- f )
  dup current-colors + c@ swap previous-colors + c@ <>
;

: update-color ( n -- )
  dup current-colors + c@ swap previous-colors + c@ c! ;

: colors-changed? ( -- f )
  0 4 cfor cr@ 1- color-changed? if cr@ 1- update-color 1+ then cnext ;

: can-send-colors ( -- )
  COLORS-ARBITRATION can-arbitration !
  4 can-msg-length c!
  current-color-0 c@ can-msg-0 c! current-color-1 c@ can-msg-1 c!
  current-color-2 c@ can-msg-2 c! current-color-3 c@ can-msg-3 c!
  can-transmit
  timer1-reset
;

: can-send-values ( -- )
  VALUES-ARBITRATION can-arbitration !
  8 can-msg-length c!
  current-value-0 @ can-msg-0 ! current-value-1 @ can-msg-2 !
  current-value-2 @ can-msg-4 ! current-value-3 @ can-msg-6 !
  can-transmit
;

: can-send-calibration ( -- )
  CALIBRATION-ARBITRATION can-arbitration !
  4 can-msg-length c!
  cream-white-threshold @ can-msg-0 ! brown-cream-threshold @ can-msg-2 !
  can-transmit
;

: maybe-send-colors ( -- )
  timer1-expired? 0= if exit then     \ Do not overload the CAN bus
  colors-changed? 0= if exit then     \ No need to send the new colors
  can-send-colors
;

: read-from-eeprom ( -- )
  cream-white-threshold-eeprom @ cream-white-threshold !
  brown-cream-threshold-eeprom @ brown-cream-threshold !
;

: set-thresholds ( -- ) 
  can-msg-0 @ cream-white-threshold-eeprom !
  can-msg-2 @ brown-cream-threshold-eeprom !
  read-from-eeprom 
;

: can-unknown-msg ( -- )
  ." Unknown CAN message with arbitration " . ."  and "
  can-msg-rtr bit-clr? if ." no " then ." RTR" cr
;

: can-handle-message ( -- )
  can-receive
  can-arbitration @
  dup SET-CALIBRATION-REQUEST = if drop set-thresholds exit then
  can-msg-rtr bit-clr? if drop can-unknown-msg exit then
  dup COLORS-ARBITRATION = if drop can-send-colors exit then
  dup VALUES-ARBITRATION = if drop can-send-values exit then
  dup CALIBRATION-ARBITRATION = if drop can-send-calibration exit then
  can-unknown-msg
;

: print-color ( color -- )
  dup UNKNOWN = if drop ." unknown" exit then
  dup CREAM = if drop ." cream" exit then
  dup BROWN = if drop ." brown" exit then
  dup WHITE = if drop ." white" exit then
  drop ." <internal error>"
;

: serial-dump ( -- )
  4 cfor
    ." Channel " cr@ 1- . ." : " cr@ 1- current-colors + c@ print-color
    ."  (" cr@ 1- current-values + @ . ." )" cr
  cnext
;

: print-usage ( -- )
  ." Usage:" cr
  ."    s               execute step" cr
  ."    0...3           print A/D value for channel x" cr
  ."    d               print stack's depth" cr
  ."    L               toggle the sensors' leds" cr
  ."    l               CAN loopback mode on" cr
  ."    k               CAN loopback off" cr
  ."    r               Send a CAN command to request values" cr
  ."    c               Send a CAN command to request color values" cr
  ."    q       Recalibrate" cr
;

: pause key drop ;
: calibration
  ." Sensors on BROWN (press a key when ready)" cr
  pause
  0 channel-measure 
  ." Sensors on CREAM (press a key when ready)" cr
  pause
  0 channel-measure dup 
  ." Sensors on WHITE (press a key when ready)" cr
  pause
  0 channel-measure
        
  ( STK= br be be w )
  + 2/ dup ." cream-white=" . cr cream-white-threshold !
  + 2/ dup ." brown-cream=" . cr brown-cream-threshold !
;

forward step
: handle-key ( n -- )
  dup [CHAR] 0 = if ." Channel0 : "  dup channel-measure . channel-determine-color . cr exit then
  dup [CHAR] 1 = if ." Channel1 : "  dup channel-measure . channel-determine-color . cr exit then
  dup [CHAR] 2 = if ." Channel2 : "  dup channel-measure . channel-determine-color . cr exit then
  dup [CHAR] 3 = if ." Channel3 : "  dup channel-measure . channel-determine-color . cr exit then
  dup [CHAR] q = if drop ." Calibration " calibration exit then
  dup [CHAR] d = if drop ." Stack's depth : " depth . cr exit then
  dup [CHAR] u = if drop print-usage . cr exit then
  dup [CHAR] l = if drop ." Loopback on" cr can-loopback exit then 
  dup [CHAR] k = if drop ." Loopback off" cr can-normal exit then 
  dup [CHAR] s = if drop step exit then
  dup [CHAR] L = if drop  leds bit-toggle ." Sensors' leds toggled" cr  exit then       
  drop print-usage
;

: interactive-mode ( -- ) begin key handle-key again ;

: step ( -- )
  can-msg-present? if can-handle-message then
  maybe-send-colors
  serial-dump
  key? if key [char] m = if interactive-mode then then
;

: mainloop ( -- ) begin step again ;

: init ( -- )
  \ A/D converter
  $81 ADCON0 c! 
  $c2 ADCON1 c!

  TRISB 0 bit-clr \ Led 1
  TRISB 1 bit-clr \ Led 2
  TRISB 4 bit-set \ Button
  TRISA 5 bit-clr \ Sensors' leds 
        
  0b10000100 T0CON c!
  0b10010001 T1CON c!     \ Prescaler = 2, cycle = 13.1ms at 40MHz

  read-from-eeprom
        
  led1 bit-set
  led2 bit-clr
  can-setup
;

: greetings ." Welcome to the sensors program" cr ;


\ Main program
: main ( -- ) init greetings mainloop ;
