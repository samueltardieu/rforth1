\ Serial port handling

: tty-init ( -- ) 0b00100000 TXSTA c! 64 SPBRG c! TRISC 6 bit-clr ;
: emit ( c/w -- ) begin TXIF bit-set? until TXREG c! ;
: key ( -- c ) begin RCIF bit-set? until RCREG c@ ;
: key? ( -- f ) RCIF bit-set? ; inline
