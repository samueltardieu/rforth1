\ Serial port handling

: tty-initialize ( spbrg/w -- ) w> SPBRG c! 0b00100000 TXSTA c! TRISC 6 bit-clr ; inw
: tty-init ( -- ) 64 tty-initialize ;
: emit ( c/w -- ) begin TXIF bit-set? until w> TXREG c! ; inw
: key? ( -- f ) RCIF bit-set? ; inline
: key ( -- c ) begin key? until RCREG c@ >w ; outw
