\ Commande de moteurs
\ AREABOT

cvariable flags
flags 0 bit working
flags 1 bit done-flag
flags 2 bit DIR

needs examples/engines/stepper-card-setup.fs 
needs examples/engines/hbridge.fs
variable vmax
variable vmax-temp
variable tvmax
variable tvmax-temp
cvariable acc
cvariable acc-temp
cvariable dec
cvariable dec-temp
needs examples/engines/can-engines.fs

variable v
: working? working bit-set? ; inline
: working-on working bit-set done-flag bit-clr ; inline
: working-off working bit-clr 0 v ! ;

: set-temp-values 
	acc-temp c@ acc c!
	dec-temp c@ dec c!
	vmax-temp @ vmax !
	tvmax-temp @ tvmax !
;

: handle-start ." Received start" cr read-payload-1 set-temp-values working-on ; inline
: handle-free ." Received free" cr read-payload-1 working-off free-wheel ; inline
: handle-stop ." Received stop" cr read-payload-1 p1 working-off ; inline
: handle-forth ." Received forth" cr read-payload-6 DIR bit-clr ; inline
: handle-back ." Received back" cr read-payload-6 DIR bit-set ; inline

: v-to-timer v @ 0< if 0 else 16960 15 v @ /32 negate then ;
: vmax-reached? ( -- f) vmax @ v @ <= ;
: tvmax-not-elapsed? ( -- f) tvmax @ 0> ;
: accelerate? ( -- f ) vmax-reached? 0= tvmax-not-elapsed? and ;
: constant-speed? ( -- f) vmax-reached? tvmax-not-elapsed? and ;
: accelerate  acc c@ v +! ;
: deccelerate dec c@ v -! ;
: dec-tvmax 1 tvmax -! ;
: constant-speed  dec-tvmax vmax @ v ! ;

: 33-nop 11-nop 11-nop 11-nop ;

: do-step working? if set-next-output then ; 

: control 
working? 0= if exit then 
led-toggle
accelerate? if accelerate exit then
constant-speed? if constant-speed exit then
v @ dec c@ > if deccelerate exit then
working-off
done-flag bit-clr? if send-done done-flag bit-set then
; 

: timer3-reset v-to-timer dup msb TMR3H c! lsb TMR3L c! ; 
: timer3-reached ( -- ) TMR3IF bit-set? if timer3-reset TMR3IF bit-clr do-step then ;

: timer0-reset 0x0b TMR0H c! 0xdb TMR0L c! ; ( 100 ms )
: timer0-reached ( -- ) TMR0IF bit-set? if timer0-reset TMR0IF bit-clr control then ;

: timers-reached timer0-reached timer3-reached ;
\ Non blocant emit code
: emit begin timers-reached TXIF bit-set? until TXREG c! ;
: key? RCIF bit-set? ; \ inline
: key begin timers-reached key? until RCREG c@ ;

: init-timers 
\ timer0  (1:16 prescaler)
0x83 T0CON c!
\ timer3  (prescaler 1:2)
0x91 T3CON c! 
;

: greetings 
    led-toggle
	working bit-set 
    ." Welcome to motors control with can!" cr
;
: init-me p1 done-flag bit-set ;
: main ( -- )  init-common can-setup init-timers init-me greetings  begin handle-can timer0-reached timer3-reached again ;
: handle-key 
        key 
		dup [char] z = if drop print-addresses exit then
		dup [char] x = if drop write-addresses exit then
		dup [char] s = if drop set-next-output ." Step done" cr exit then
        dup [char] a = if drop can-abort ." Can abort" cr exit then
        dup [CHAR] d = if drop ." v=" v @ . cr exit then 
        dup [CHAR] l = if drop ." Loopback on" cr can-loopback exit then 
        dup [CHAR] k = if drop ." Loopback off" cr can-normal exit then 
        dup [CHAR] 1 = if drop send-stop  exit then 
        dup [CHAR] 2 = if drop send-free  exit then
        dup [CHAR] 3 = if drop send-forth exit then
        dup [CHAR] 4 = if drop send-back exit then
        dup [CHAR] 7 = if drop ." P1" p1 exit then
        dup [CHAR] 8 = if drop ." P2" p2 exit then
        dup [CHAR] 9 = if drop ." P3" p3 exit then
       		[CHAR] 5 = if  send-start  exit then   
	.s cr \ if you press any other key you get the stack size 
;
