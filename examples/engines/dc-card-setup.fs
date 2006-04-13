\ leds
LATB 0 bit LED1
LATB 1 bit LED2

\ Ports naming
LATC 1 bit DIR1
LATC 0 bit PWM1    
LATC 3 bit DIR2
LATC 2 bit PWM2
\ 0 = OUTPUT
\ 0b00001011 constant TRISB_setup
\ 0b10111111 constant TRISC_setup
0b11111111 constant TRISA_setup
0b10110000 constant TRISC_setup
0b11111000 constant TRISB_setup

: read-bit bit-set? negate . ;

: led-toggle  led1 bit-toggle ; inline

: init-common 

\ IO setup
TRISB_setup TRISB c! 
TRISC_setup TRISC c!
TRISA_setup TRISA c!

PWM1 bit-clr
PWM2 bit-clr
DIR1  bit-clr
DIR2  bit-clr
LED1 bit-clr
; 


