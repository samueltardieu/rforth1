needs lib/tty.fs

-25000 constant main-time

300 constant delta

variable pulse0

: set-tmr0-wait ( n -- ) TMR0L ! TMR0IF bit-clr begin TMR0IF bit-set? until ;

: tmr1-wait ( -- ) begin TMR1IF bit-set? until ;

: set-tmr1 ( n -- ) TMR1L ! TMR1IF bit-clr ;

: pwm 
	begin
  	main-time set-tmr1 
        LATB 5 bit-set pulse0 @ set-tmr0-wait LATB 5 bit-clr
	( pulse-max set-tmr1 )
	begin RCIF bit-set? if exit then TMR1IF bit-set? until

        again
;

: main-loop ( -- )
	-25000 pulse0 !
	
	begin
	pwm
        key 
	dup [char] 0 = if
	    read16 pulse0 ! 
		." 0"
	then
	dup [char] p = if
		." PWM 0 : " pulse0 @ . cr
	then
	dup	[char] + = if
		pulse0 @ delta + pulse0 !
		." +" pulse0 @ . cr
	then
	dup	[char] - = if
		pulse0 @ delta - pulse0 !
		." -" pulse0 @ . cr
	then
        dup [char] d = if LATA 2 bit-toggle then
	drop
    again ;

: greetings ( -- )
  ." \nPWM Generator>\n" ;

: init-pwm ( -- )
  7 ADCON1 c!
  TRISB 5 bit-clr
  TRISC 5 bit-clr
  0b10000001 T0CON c! \ pas de prescaler
  0 TMR1H c! 0 TMR1L c!
  0b10110001 T1CON c! \ prescaler = 8
;

: main ( -- ) greetings init-pwm main-loop ." Out of main loop" cr ;
