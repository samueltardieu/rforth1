needs lib/tty-rs232.fs

\ 20 ms à 16 Mhz => 80 000 ticks
\ prescaler de 2 dans T1 donc :
\ largeur totale du pulse : 40 000

\ on veut 2,5 ms par pulse max
\ prescaler de 2 dans T1 donc :
-5000 constant pulse-max-width

\ ce qu'il reste a la fin : 5ms
-10000 constant leave-time

\ valeur par defaut du pulse : 1,5 ms
\ pas de prescaler sur T0 !
-6000 constant default-pulse-width

5 constant delta-def

0xDEAD constant magic-constant

\ keep them consecutive
variable pulse0
variable pulse1
variable pulse2
variable pulse3
variable pulse4
variable pulse5

LATA 0 bit pwm0
LATA 1 bit pwm1
LATA 2 bit pwm2
LATA 3 bit pwm3
LATC 1 bit pwm4
LATC 2 bit pwm5

variable delta

eevariable magic
eevariable p0
eevariable p1
eevariable p2
eevariable p3
eevariable p4
eevariable p5

: init-ports
	7 ADCON1 c!
 	TRISA 0 bit-clr
 	TRISA 1 bit-clr
 	TRISA 2 bit-clr
 	TRISA 3 bit-clr
  	TRISC 1 bit-clr
  	TRISC 2 bit-clr
;
	
: set-tmr0-wait ( n -- ) 1>2 TMR0H c! TMR0L c! TMR0IF bit-clr begin TMR0IF bit-set? until ;

: tmr1-wait ( -- ) begin TMR1IF bit-set? until ;

: set-tmr1 ( n -- ) 1>2 TMR1H c! TMR1L c! TMR1IF bit-clr ;

: pwm-pulse ( b n b -- ) pulse-max-width set-tmr1 bit-set @ set-tmr0-wait bit-clr tmr1-wait ;

: pwm 
	begin
		pwm0 pulse0 pwm0 pwm-pulse
		pwm1 pulse1 pwm1 pwm-pulse
		pwm2 pulse2 pwm2 pwm-pulse
		pwm3 pulse3 pwm3 pwm-pulse
		pwm4 pulse4 pwm4 pwm-pulse
		pwm5 pulse5 pwm5 pwm-pulse
	
		leave-time set-tmr1 begin RCIF bit-set? if exit then TMR1IF bit-set? until
	
	again
;

: >pulse ( n -- addr ) 2* pulse0 + ;

: add-delta ( addr +/-delta -- )
  swap >pulse >r r@ @ + r@ ! r> @ . cr ;

: emitc ( n -- ) emit-4 ;

: +delta ( n -- ) dup emitc [char] + emit delta @ add-delta ;
: -delta ( n -- ) dup emitc [char] - emit delta @ negate add-delta ;

: update ( n -- ) dup emitc >pulse >r read16 r@ ! r> @ . cr ;

: print-delta ( -- ) ." delta=" delta @ . cr ;

: dump-pwm ( -- )
		." PWM 0 : " pulse0 @ . cr
		." PWM 1 : " pulse1 @ . cr
		." PWM 2 : " pulse2 @ . cr
		." PWM 3 : " pulse3 @ . cr
		." PWM 4 : " pulse4 @ . cr
		." PWM 5 : " pulse5 @ . cr
;

: handle-key ( c -- )
	dup [char] 0 = if drop 0 update exit then
	dup [char] 1 = if drop 1 update exit then
	dup [char] 2 = if drop 2 update exit then
	dup [char] 3 = if drop 3 update exit then
	dup [char] 4 = if drop 4 update exit then
	dup [char] 5 = if drop 5 update exit then
	dup [char] d = if drop dump-pwm exit then
	dup [char] a = if drop 0 +delta exit then
	dup [char] z = if drop 0 -delta exit then
	dup [char] e = if drop 1 +delta exit then
	dup [char] r = if drop 1 -delta exit then
	dup [char] t = if drop 2 +delta exit then
	dup [char] y = if drop 2 -delta exit then
	dup [char] u = if drop 3 +delta exit then
	dup [char] i = if drop 3 -delta exit then
	dup [char] o = if drop 4 +delta exit then
	dup [char] p = if drop 4 -delta exit then
\	dup [char] ^ = if drop
\		pulse5 @ delta @ + pulse5 !
\		." 5+" pulse5 @ . cr
\	exit then
\	dup	[char] $ = if drop
\		pulse5 @ delta @ - pulse5 !
\		." 5-" pulse5 @ . cr
\	exit then
	dup [char] S = if drop
		magic-constant magic !
		pulse0 @ p0 !
		pulse1 @ p1 !
		pulse2 @ p2 !
		pulse3 @ p3 !
		pulse4 @ p4 !
		pulse5 @ p5 !
		." conf saved"
	exit then
	dup [char] q = if drop delta 1 +! print-delta exit then
	dup [char] s = if drop delta 1 -! print-delta exit then
	dup [char] w = if drop print-delta exit then
	drop
;

: init-pulses ( -- )
	magic @ magic-constant = if
		p0 @ pulse0 !
		p1 @ pulse1 !
		p2 @ pulse2 !
		p3 @ pulse3 !
		p4 @ pulse4 !
		p5 @ pulse5 !
	else
		6 cfor default-pulse-width cr@ 1- >pulse ! cnext
	then
	
	delta-def delta !
;

: main-loop ( -- ) begin pwm key handle-key again ;

: greetings ( -- )
  ." \nPWM Generator>\n" ;

: init-pwm ( -- )
  init-ports
  0b10001000 T0CON c! \ pas de prescaler
  0b10010001 T1CON c! \ prescaler = 2
;

: main ( -- ) greetings init-pwm init-pulses main-loop ;
