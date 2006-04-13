LATC 0 bit LED
\ 0 = OUTPUT
0b11111011 constant TRISB_setup
0b10111110 constant TRISC_setup
0b11100001 constant TRISA_setup



: read-bit bit-set? negate . ;

: 11-nop nop nop nop nop nop nop nop nop nop nop nop ; inline 

: led-toggle LED bit-toggle ; inline
: init-common 
\ IO setup
TRISB_setup TRISB c! 
TRISC_setup TRISC c!
TRISA_setup TRISA c!

\ Stop AD converter
0x06 ADCON0 c! 
; 


