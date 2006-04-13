\ Commande de la tourelle camera
\ AREABOT

needs examples/can-tourelle-camera.fs

variable working
variable wanted-angle
variable current-angle
variable ref-tension

2 constant precision 

: read-ad
	ADCON0 c@ 4 or ADCON0 c!
	begin ADCON0 c@ 4 and while repeat ADRESH c@ 
;

: handle-angle
." Received angle" cr
read-payload
; 


\ ****************************************************************************
\ CONTROL

: neighbour? ( n -- ) abs 10 < ;
: tension-diff read-ad ref-tension @ - ;
: recalibrate  tension-diff neighbour? if 0 current-angle ! then ; 

: do-step working c@ if STEP bit-set 11-nop STEP bit-clr then ;

: angle-diff wanted-angle @ current-angle @ - ;

: go-back DIR bit-clr do-step precision current-angle -! ;
: go-forth DIR bit-set do-step precision current-angle -! ;


: control 
led-toggle
recalibrate
angle-diff precision < if go-back exit then
angle-diff precision > if go-forth exit then 
; 

\ ****************************************************************************
\ TIMER 0

: timer0-reset 0x0b TMR0H c! 0xdb TMR0L c! ; ( 100 ms )
: timer0-reached ( -- ) TMR0IF bit-set? if timer0-reset TMR0IF bit-clr control then ;

: timers-reached timer0-reached ;
\ Non blocant emit code
: emit begin timers-reached TXIF bit-set? until TXREG c! ;
: key? RCIF bit-set? ;
: key begin timers-reached key? until RCREG c@ ;

: init-timers 
\ timer0  (1:16 prescaler)
 0x83 T0CON c!
;

\ ****************************************************************************
: init-variables
    0 current-angle !
    0 wanted-angle !
    read-ad ref-tension !  
    0 working !
;

: greetings 
    led-toggle
    ." Welcome to tourelle control with can!" cr
;


: main ( -- )  init-common can-setup init-variables init-timers greetings begin handle-can timer0-reached again ;
