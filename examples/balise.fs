\ Usage: at prompt, send the 'C' character, then 4 nibbles for
\ high-delay and 4 hex nibbles for low-delay (that is 16 bits each).
\ Each delay corresponds to (4/20MHz)*2 = 0,4µs. You can also use 'P'
\ and 2 nibbles for the patterns (default 1 and 6).
\
\ For example, to get a 30Hz signal, one has to wait (1/30Hz)/2 = 16.666ms
\ in both high and low positions, that is 41667 cycles (A2C3 in hexa).
\ However, we want a not-so-light error (0.0008% is too small) and will
\ cause problems, so we use A000 in hexa.
\
\ The smallest value is 0x1000, which corresponds to 1.638ms. The greatest
\ value is 0xEFFF.

needs lib/tty-rs232.fs

variable nexttimer
variable ondelay
variable offdelay

cvariable pattern-1
cvariable pattern-2

: timer-set ( n -- ) nexttimer +! ;
: diff ( -- n ) TMR0L c@ TMR0H c@ 2>1 nexttimer @ - ;
: neighbour? ( n -- ) abs 0x1000 < ;
: timer-reached? ( -- f ) diff dup neighbour? if 0 >= else drop 0 then ;
: timer-print ( -- ) TMR0L c@ TMR0H c@ 2>1 . cr ;
: timer-wait ( n -- ) timer-set begin timer-reached? until ;
: timer-reset ( -- ) 0 TMR0H c! 0 TMR0L c! 0 nexttimer ! ;

: alt! ( patt -- ) LATC c@ 0xf8 and or LATC ! ;

: alt-1 ( -- ) pattern-1 c@ alt! ;
: alt-2 ( -- ) pattern-2 c@ alt! ;

: leds-off 0 alt! ;
    
: pwm ( -- pressedkey )
    timer-reset
    begin
	\ Turn leds on and wait
	alt-1 ondelay @ timer-wait
	\ Ditto for the other cycle
	alt-2 offdelay @ timer-wait
    key? until ;

: prompt ( -- ) ." \nOK>";

: main-loop ( -- )
    begin
	prompt
	pwm
	key
	dup [char] C = if
	    leds-off
	    read16 ondelay !
	    read16 offdelay !
	then
	dup [char] P = if
	    leds-off
	    read4 pattern-1 c!
	    read4 pattern-2 c!
	then
        dup [char] S = if
            leds-off
            T0CON c@ 0xf0 and read4 or T0CON c!
        then
	drop
    again ;

: greetings ( -- )
  ." \nCode balise\n" ;

: init-ports ( -- )
  \ C0, C1 and C2 are for leds and C6 for TX (output)
  0xb8 TRISC c!
  \ Turn converter on (100000X1) (01XX1110)
  \ 0x81 ADCON0 c! 0x4e ADCON1 c!
  \ Use timer 0, prescale by 2, correctly init delays
  0x80 T0CON c!
  0xA000 ondelay ! 0xA000 offdelay !
;

: main ( -- ) 3 pattern-1 c! 4 pattern-2 c! init-ports greetings main-loop ;
