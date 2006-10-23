needs lib/tty-rs232.fs

300 constant delta

variable pulse0

LATB 5 bit led0
LATB 6 bit led1

: reset-tmr0 ( -- ) pulse0 @ TMR0L ! TMR0IF bit-clr ;
: wait-for-tmr0 ( -- ) begin TMR0IF bit-set? until ;
: tmr0-cycle ( -- ) reset-tmr0 wait-for-tmr0 ;

: reset-tmr1 ( n -- ) -25000 TMR1L ! TMR1IF bit-clr ;

: pwm  ( -- )
  begin
    reset-tmr1
    led0 bit-set tmr0-cycle led0 bit-clr
    begin key? if exit then TMR1IF bit-set? until
  again
;

: .pulse0 ( -- ) pulse0 @ . cr ;

: main-loop ( -- )
  -25000 pulse0 !
  begin
    pwm
    key >w switchw
      [char] 0 casew read16 pulse0 ! [char] 0 emit
      [char] p casew ." PWM 0 : " .pulse0
      [char] + casew pulse0 @ delta + pulse0 ! [char] + emit .pulse0
      [char] - casew pulse0 @ delta - pulse0 ! [char] - emit .pulse0
      [char] d casew led1 bit-toggle
      [char] q casew exit
    endswitchw
  again
;

: greetings ( -- ) cr ." PWM Generator>" cr ;

: init-pwm ( -- )
  7 ADCON1 c!
  TRISB 5 bit-clr
  TRISB 6 bit-clr
  0b10000001 T0CON c! \ no prescaler
  0 TMR1H c! 0 TMR1L c!
  0b10110001 T1CON c! \ prescaler = 8
;

: main ( -- ) greetings init-pwm main-loop ." Out of main loop" cr ;
