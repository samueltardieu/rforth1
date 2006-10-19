\ Serial port handling

: tty-init ( -- ) 0b00100000 TXSTA c! 64 SPBRG c! TRISC 6 bit-clr ;
: emit ( c/w -- ) begin TXIF bit-set? until w> TXREG c! ; inw
: key? ( -- f ) RCIF bit-set? ; inline
: key ( -- c ) begin key? until RCREG c@ >w ; outw
