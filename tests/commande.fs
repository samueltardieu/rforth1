\ Commande de moteurs
\ AREABOT

needs tests/can-commande.fs


\ leds
LATC 5 bit LED1

\ Ports naming

LATC 1 bit STEP
LATC 4 bit HOME    
LATB 1 bit NSLEEP
LATA 1 bit DIR
LATC 0 bit NRESET
LATA 3 bit MS1
LATA 2 bit MS2

\ 0 = OUTPUT
0b11111001 constant TRISB_setup
0b10011100 constant TRISC_setup
0b11110001 constant TRISA_setup


variable working


: handle-free
." Received free" cr
0 working c!
NSLEEP bit-toggle
; inline

: handle-stop
." Received stop" cr
0 working c!
; inline

: handle-forth
." Received forth" cr
read-payload
0 v !
-1 working c! 
DIR bit-clr
; inline

: handle-back
." Received back" cr
read-payload
0 v !
-1 working c!
DIR bit-set
; inline


\ ****************************************************************************
\ CONTROL
: v-to-timer v @ 0< if 0 else 16960 15 v @ /32 negate then ;

: vmax-reached? ( -- f) vmax @ v @ <= ;
: tvmax-not-elapsed? ( -- f) tvmax c@ 0 > ;
: accelerate? ( -- f ) vmax-reached? 0= tvmax-not-elapsed? and ;
: constant-speed? ( -- f) vmax-reached? tvmax-not-elapsed? and ;

: accelerate  acc c@ v +!  ;
: deccelerate dec c@ negate v +! ;
: dec-tvmax 1 negate tvmax +! ;
: constant-speed  dec-tvmax vmax @ v ! ;
: null-speed 0 v ! ; 

: 11-nop nop nop nop nop nop nop nop nop nop nop nop ; inline 
: do-step working c@ if STEP bit-set 11-nop STEP bit-clr then ;

: led-toggle  led1 bit-toggle ; inline


: control 
led-toggle
accelerate? if accelerate  else
constant-speed? if constant-speed  else
v @ 0> if deccelerate   else null-speed 0 working c!  
then then then 
; 

\ ****************************************************************************
\ TIMER 3  


: timer3-reset v-to-timer dup msb TMR3H c! lsb TMR3L c! ; 
: timer3-reached ( -- ) TMR3IF bit-set? if timer3-reset TMR3IF bit-clr do-step then ;
\ TIMER 0

: timer0-reset 0x0b TMR0H c! 0xdb TMR0L c! ; ( 100 ms )
: timer0-reached ( -- ) TMR0IF bit-set? if timer0-reset TMR0IF bit-clr control then ;

: timers-reached timer0-reached timer3-reached ;
\ Non blocant emit code
: emit begin timers-reached TXIF bit-set? until TXREG c! ;
: key? RCIF bit-set? ; inline
: key begin timers-reached key? until RCREG c@ ;

: init-timers 
\ timer0  (1:16 prescaler)
 0x83 T0CON c!
\ timer3  (prescaler 1:1)
0x81 T3CON c! 
;

\ ****************************************************************************
: init-variables
	0 acc c!
	0 dec c!
	0 vmax !
	0 tvmax c!
	0 v ! 
    0 working c!
;

: init 
\ IO setup
TRISB_setup TRISB c! 
TRISC_setup TRISC c!
TRISA_setup TRISA c!

\ Stop AD converter
0x06 ADCON0 c! 

\ set microstep resolution = QuarterStep
MS1 bit-clr
MS2 bit-set

\ reset allegro
NRESET bit-clr
11-nop
NRESET bit-set
11-nop
\ wakeup allegro
NSLEEP bit-set
11-nop

init-variables 
init-timers  
; 

: greetings 
    led-toggle
    ." Welcome to motors control with can!" cr
;


: main ( -- )  init can-setup greetings begin handle-can timer0-reached timer3-reached again ;


\ : print-bit bit-set? if ." 1" else ." 0"  then ; 
