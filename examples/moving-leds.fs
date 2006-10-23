needs lib/tty-rs232.fs                \ Use RS232 for input/output

\ Define three words led0, led1 and led2 designating the leds

LATB 5 bit led0
LATB 6 bit led1
LATB 7 bit led2

\ Use timer 0 to wait for 100ms (with a 40MHz crystal)

: tmr0-init ( -- ) $84 T0CON c! ;    \ Enable timer, 16 bits, prescaler = 32
: 100ms ( -- ) -31250 TMR0L ! TMR0IF bit-clr begin TMR0IF bit-set? until ;

\ Move leds -- when led0 goes to 0, switch led1. When led1 goes to 0, do
\ the same thing with led2

: leds-init ( -- ) 0 LATB c! $1F TRISB c! ;   \ B5, B6 and B7 are outputs
: switch-led2 ( -- ) led2 bit-toggle ;
: switch-led1 ( -- ) led1 bit-toggle led1 bit-clr? if switch-led2 then ;
: switch-led0 ( -- ) led0 bit-toggle led0 bit-clr? if switch-led1 then ;

\ Loop indefinitely until a character arrives on the serial line, with a
\ pause between each led change

: mainloop ( -- ) begin switch-led0 100ms key? until ;

\ Main program: initialize the timer and the leds then run the main loop

: main ( -- ) tmr0-init leds-init mainloop ;
