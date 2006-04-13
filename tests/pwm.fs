needs lib/tty.fs

-28000 constant main-time

-6000 constant pulse-max

10 constant delta

variable pulse0
variable pulse1

: set-tmr0-wait ( n -- ) 1>2 TMR0H c! TMR0L c! TMR0IF bit-clr begin TMR0IF bit-set? until ;

: tmr1-wait ( -- ) begin TMR1IF bit-set? until ;

: set-tmr1 ( n -- ) 1>2 TMR1H c! TMR1L c! TMR1IF bit-clr ;

: pwm 
	begin
	
	pulse-max set-tmr1
  	LATA 2 bit-set pulse0 @ set-tmr0-wait LATA 2 bit-clr
	tmr1-wait
	pulse-max set-tmr1
  	LATC 1 bit-set pulse1 @ set-tmr0-wait LATC 1 bit-clr
	tmr1-wait
	main-time set-tmr1 begin RCIF bit-set? if exit then TMR1IF bit-set? until

	again
;

: main-loop ( -- )
	-6000 pulse0 !
	-6000 pulse1 !
	
	begin
	pwm
	key
	dup [char] 0 = if
	    read16 pulse0 ! 
		." 0"
	then
	dup [char] 1 = if
	    read16 pulse1 !
		." 1"
	then
	dup [char] p = if
		." PWM 0 : " pulse0 @ . cr
		." PWM 1 : " pulse1 @ . cr
	then
	dup	[char] + = if
		pulse1 @ delta + pulse1 !
		." +" pulse1 @ . cr
	then
	dup	[char] - = if
		pulse1 @ delta - pulse1 !
		." -" pulse1 @ . cr
	then
	drop
    again ;

: greetings ( -- )
  ." \nPWM Generator>\n" ;

: init-pwm ( -- )
  7 ADCON1 c!
  TRISA 2 bit-clr
  TRISC 1 bit-clr
  0b10001000 T0CON c! \ pas de prescaler
  0 TMR1H c! 0 TMR1L c!
  0b10010001 T1CON c! \ prescaler = 2
;

: main ( -- ) greetings init-pwm main-loop ;
