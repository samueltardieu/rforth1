0x1fC0 constant configuration-address
0x7fe  constant can-generic
0x7fd  constant can-relay-response
0x2000 constant USER_PROGRAM_ADDR

\ ----------------------------------------------------------------------
\ Configuration parameters
\ ----------------------------------------------------------------------

0xDEAD constant current-magic
0x0666 constant current-version

create ram-configuration

   variable magic
   variable version

   variable led-lat
   cvariable led-conf
   led-conf 7 bit led-active-high
   7 constant led-bit-mask

   variable button-port
   cvariable button-conf
   button-conf 0 constant button-active-high
   7 constant button-bit-mask

   cvariable latest-SPBRG
   cvariable latest-SPBRG-conf
   latest-SPBRG-conf 0 bit latest-BRGH

   cvariable conf-bits
   conf-bits 0 bit use-serial
   conf-bits 1 bit use-can

   cvariable frequency

   variable can-addr

   variable wait-delay

   create card-id 16 allot

create end-configuration

create buffer 64 allot         \ Used for flashing and for serial<->can

cvariable various-bits
various-bits 0 bit echo
various-bits 1 bit console-can
various-bits 2 bit bootloader-active

\ ----------------------------------------------------------------------
\ Serial
\ ----------------------------------------------------------------------

: serial-configure ( -- )
  0b00100000 TXSTA c!
  0b10010000 RCSTA c!
  latest-BRGH bit-set? if BRGH bit-set then  
  latest-SPBRG c@ SPBRG c!
  TRISC 6 bit-clr ;

forward timer-check

: serial-emit ( c -- ) begin clrwdt timer-check TXIF bit-set? until TXREG c! ;
: serial-key? ( -- f ) RCIF bit-set? ;
: serial-key ( -- c ) begin clrwdt timer-check serial-key? until RCREG c@ ;

\ ----------------------------------------------------------------------
\ CAN
\ ----------------------------------------------------------------------

\ Mask 0 is set to match the generic address (filter 0) as well as the
\ relay response if needed (filter 1). Mask 1 designates the generic
\ address unless we are in observer mode.

: can-configure ( -- )
  can-init
  can-config
  0x7fc 0 can-set-mask
  can-generic 0 can-set-filter
  can-addr 2 can-set-filter
  0x7ff 1 can-set-mask
  can-monobuffer
  can-normal
;

: can-configure-relay ( -- )
  can-config can-relay-response 1 can-set-filter can-normal ;

: can-deconfigure-relay ( -- ) can-config 0x7ff 1 can-set-filter can-normal ;

variable can-target

: can-emit ( c -- ) can-target can-emit-1 ;
: can-key? ( -- f ) can-msg-present? ;
: can-key ( -- c )
  begin clrwdt timer-check can-key? until
  can-receive can-msg-0 @ ;

\ ----------------------------------------------------------------------
\ Serial/CAN transparent relay
\ ----------------------------------------------------------------------

cvariable next-to-read
cvariable next-to-write

: buffered? ( -- f ) next-to-read c@ next-to-write c@ <> ;

: rollover ( n -- n' ) 1+ dup 64 >= if drop 0 then ;

: >buffer ( c -- )
  next-to-write c@ buffer + c!
  next-to-write c@ rollover next-to-write c!
;
: buffer> ( -- c )
  next-to-read c@ buffer + c@
  next-to-write c@ rollover next-to-write c!
;

: relay ( -- )
  begin
    clrwdt
    timer-check
    serial-key? if serial-key dup 27 = if drop exit then can-emit then
    can-key? if can-key >buffer then
    TXIF bit-set? if buffered? if buffer> serial-emit then then
  again
;

\ ----------------------------------------------------------------------
\ CAN bus observer
\ ----------------------------------------------------------------------

: observe-can-message ( -- )
  can-receive
  can-arbitration @ .
  can-msg-rtr bit-set? if ."  (rtr)" then
  can-msg-length c@ if
    can-msg-length c@ cfor space can-msg-0 cr@ + c@ . cnext
  then
  cr
;

: observer-mainloop ( -- )
  begin
    clrwdt
    timer-check
    serial-key? if serial-key dup 27 = if drop exit then then
    can-msg-present? if observe-can-message then
  again
;

: observer ( -- )
  cr ." Observing CAN bus" cr
  can-config 0 1 can-set-mask can-normal
  observer-mainloop
  can-config 0x7ff 1 can-set-mask can-normal
  ." End of CAN bus observation" cr
;


\ ----------------------------------------------------------------------
\ Console I/O
\ ----------------------------------------------------------------------

: emit ( c -- ) console-can bit-set? if can-emit else serial-emit then ;
: key? ( -- f ) console-can bit-set? if can-key? else serial-key? then ;
: key ( -- c )
  console-can bit-set? if can-key else serial-key then
  echo bit-set? if dup emit then ;

\ ----------------------------------------------------------------------
\ Global configuration
\ ----------------------------------------------------------------------

end-configuration ram-configuration - constant configuration-length

: configuration-valid? ( -- f )
  magic @ current-magic = version @ current-version = and ;

: load-configuration ( -- )
  configuration-address ram-configuration
  end-configuration ram-configuration - cfor
    >r dup flashc@ r@ c! 1+ r> 1+
  cnext
  2drop
;

: save-configuration ( -- )
  ram-configuration configuration-address flash-write
;

: reset-configuration ( -- )
  current-magic magic !
  current-version version !
  0 led-lat ! 1 led-conf c! ( XXXXX )
  led-active-high bit-set
  0 button-port !
  ( 115200 at 40MHz )
  21 latest-SPBRG c!
  latest-BRGH bit-set
  ( XXXXX 57600 at 20MHz )
  ( 21 latest-SPBRG c! )
  ( latest-BRGH bit-set )
  use-serial bit-set
  use-can bit-set
  40 frequency c!  ( XXXXX )
  0 can-addr !
  0 card-id c!
  500 wait-delay ! ( 0xffff wait-delay ! ) ( XXXXX )
  save-configuration
;

\ ----------------------------------------------------------------------
\ Port A usage
\ ----------------------------------------------------------------------

: adcon-configure ( tris -- )
  6 ADCON1 c! ;

: adcon-deconfigure ( -- )
  0 ADCON1 c! ;

\ ----------------------------------------------------------------------
\ LED
\ ----------------------------------------------------------------------

cvariable led-blink-value
cvariable led-blink-current

: lat>tris ( lat -- tris ) 9 + ;

: led-tris ( -- tris ) led-lat @ lat>tris ;

: led-mask ( -- mask ) led-conf c@ led-bit-mask and ;

: led-disabled? ( -- f )
  led-lat @ 0= ;

: led-addrmask ( -- addr mask )
  led-lat @ led-mask bit-mask ;

: led-high ( -- ) led-addrmask bit-set ;
: led-low ( -- ) led-addrmask bit-clr ;

: led-on ( -- )
  led-disabled? if exit then
  led-active-high bit-set? if led-high else led-low then ;

: led-off ( -- )
  led-disabled? if exit then
  led-active-high bit-set? if led-low else led-low then ;

: led-toggle ( -- )
  led-disabled? if exit then
  led-addrmask bit-toggle ;

: led-configure ( -- )
  led-disabled? if exit then
  led-tris @ TRISA = if adcon-configure then
  led-on
  led-addrmask >r lat>tris r> bit-clr ;

: led-deconfigure ( -- )
  led-disabled? 0= if 0xff led-tris c@ c! then ;

: led-blinking? ( -- f )
  led-blink-value c@ ;

: led-blink-step ( -- )
  led-blinking? 0= if exit then
  1 led-blink-current c+!
  led-blink-current c@ led-blink-value c@ = if
    led-toggle
    0 led-blink-current c!
  then ;

: led-blink-set ( n -- )
  w> led-blink-value c! 0 led-blink-current c! ; inw

: led-steady ( -- )
  0 led-blink-set ;

: led-blink-fast ( -- )
  10 led-blink-set ;

: led-blink-slow ( -- )
  50 led-blink-set ;

\ ----------------------------------------------------------------------
\ Button
\ ----------------------------------------------------------------------

: button-disabled? ( -- f )
  button-port @ 0= ;

: button-addrmask ( -- addr mask )
  button-port @ button-conf c@ button-bit-mask and bit-mask ;

: button-pressed? ( -- f )
  button-disabled? if 0 exit then
  button-addrmask bit-set? button-active-high bit-set? = ;

: button-configure ( -- )
  button-port @ PORTA = if adcon-configure then ;

\ ----------------------------------------------------------------------
\ Bootloader
\ ----------------------------------------------------------------------

: read24 ( -- a ) read8 drop read16 ;

: bootloader-read ( -- )
  read24 space flashc@ . cr
;

: bootloader-write ( -- )
  buffer read24
  buffer 64 cfor >r read8 r@ c! r> 1+ cnext drop
  flash-write cr
;

: bootloader-execute ( -- )
  0 STKPTR c!
  read24 cr 0 PCLATU c! 1>2 PCLATH c! >w nop w> PCL c! ;

: bootloader-relay ( -- )
  read4 read8 swap 2>1 can-target ! cr
  ." Entering bootloader relay with target " can-target @ . cr
  can-configure-relay
  relay
  can-deconfigure-relay
  ." Out from relay" cr
;

: bootloader-version ( -- )
  cr
  ." rforth1 bootloader version: " version @ . cr
  led-disabled? if
    ." No led configured" cr
  else
    ." Led port address: " led-lat @ . cr
    ." Led bit: " led-conf c@ led-bit-mask and . cr
    ." Led active " led-active-high bit-set? if ." high" else ." low" then cr
  then
  button-disabled? if
    ." No button configured" cr
  else
    ." Button port address: " button-port @ . cr
    ." Button bit: " button-conf c@ button-bit-mask and . cr
    ." Button active "
      button-active-high bit-set? if ." high" else ." low" then cr
  then
  use-serial bit-set? if
    ." Serial port enabled" cr
    ."   SPBRG: " latest-SPBRG c@ . cr
    ."   BRGH: " latest-BRGH bit-set? negate . cr
  else
    ." Serial port disabled"
  then
  use-can bit-set? if
    ." CAN enabled" cr
    ."   CAN arbitration: " can-addr @ . cr
  else
    ." CAN disabled" cr
  then
  ." Frequency in MHz: " frequency c@ . cr
  ." Wait delay (in 10ms increments): " wait-delay @ . cr
;

: bootloader-command ( -- )
  key
  dup [char] X = if drop bootloader-execute exit then
  dup [char] R = if drop bootloader-read exit then
  dup [char] W = if drop bootloader-write exit then
  dup [char] : = if drop bootloader-relay exit then
  dup [char] O = if drop observer exit then
  dup [char] V = if drop bootloader-version exit then
  dup [char] T = if drop ." !!" cr exit then
  drop cr
;

: bootloader-prompt ( -- ) ." ok>" ;

: bootloader ( -- )
  ." Entering bootloader" cr
  led-blink-slow
  begin clrwdt bootloader-prompt bootloader-command again
;

\ ----------------------------------------------------------------------
\ State uncertainty
\ ----------------------------------------------------------------------

variable current-delay

forward user-program

variable t-found

: check-serial ( -- )
  use-serial bit-clr? if exit then
  serial-key? 0= if exit then
  serial-key [char] T = if
    1 t-found +! t-found @ 2 = if bootloader-active bit-set then
    [char] ! emit
  else
    0 t-found !
    [char] ? emit
  then
;

: check-can ( -- )
  use-can bit-clr? if exit then
  can-key? if
    bootloader-active bit-set
    console-can bit-set
    can-relay-response can-target !
  then
;

: uncertain-step ( -- )
  bootloader-active bit-set? if exit then
  check-serial
  check-can
  1 current-delay -!
  current-delay @ 0= if
    user-program
  then
;

: uncertain ( -- )
  led-blink-fast
  wait-delay @ current-delay !
  begin clrwdt timer-check bootloader-active bit-set? until
  bootloader
;

\ ----------------------------------------------------------------------
\ Timer
\ ----------------------------------------------------------------------

variable timer-counter

\ Return the prescaler and (0x10000 - counter) corresponding to 10ms

: timer-parameters ( frequency -- prescaler counter )
  dup 4 = if drop ( XXXXX ) 0 0 exit then
  dup 10 = if drop ( XXXXX ) 0 0 exit then
  dup 16 = if drop ( XXXXX ) 0 0 exit then
  dup 20 = if drop 0b000 0x9e58 exit then
  drop 0b000 0x3cb0                               \ Assume 40MHz by default
;

: timer-reset ( -- )
  timer-counter @ 1>2 TMR0H c! TMR0L c! TMR0IF bit-clr ;

: timer-act ( -- )
  led-blink-step uncertain-step ;

: timer-check ( -- )
  TMR0IF bit-set? if timer-reset timer-act then ;

: timer-configure ( -- )
  frequency c@ timer-parameters timer-counter ! 0x80 or T0CON c!
  timer-reset
;

: timer-deconfigure ( -- )
  0 T0CON c! T0IF bit-clr ;

\ ----------------------------------------------------------------------
\ User program
\ ----------------------------------------------------------------------

: reset-ports ( -- )
  led-deconfigure
  timer-deconfigure
  adcon-deconfigure
;

code user-program
    reset-ports rcall
    STKPTR ,a clrf
    USER_PROGRAM_ADDR goto
;code

\ ----------------------------------------------------------------------
\ Main program
\ ----------------------------------------------------------------------

: all-configure ( -- )
  led-configure 
  can-configure
  serial-configure
  echo bit-set
  timer-configure
;

: main ( -- ) reset-ports
  \ Load configuration from flash
  load-configuration

  \ Check configuration and reset from factory default if invalid/obsolete
  configuration-valid? 0= if reset-configuration then

  \ If button is pressed, execute boot loader which never terminates
  button-configure button-pressed? if all-configure bootloader exit then

  \ If delay is 0, execute main program which should never terminate
  wait-delay @ 0= if user-program reset then

  \ Indicate that program does not know what to do
  all-configure uncertain
;