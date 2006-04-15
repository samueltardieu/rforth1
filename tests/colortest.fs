needs lib/tty.fs

: 8* ( n -- 8*n ) 2* 2* 2* ;
: select-channel ( n -- ) 8* adcon0 c@ 0b11000111 and or adcon0 c! ;
: measure ( -- x ) go bit-set begin go bit-clr? until adresl @ ;
: an ( n -- x ) select-channel measure ;
: an. ( n -- ) an . cr ;

: disp0 ( -- ) ." Sensor 0: " 0 an. ;
: disp1 ( -- ) ." Sensor 1: " 1 an. ;
: disp2 ( -- ) ." Sensor 2: " 2 an. ;

: init-adc ( -- ) $81 adcon0 c! $c2 adcon1 c! ;
: main ( -- ) ." Welcome" cr init-adc begin key drop disp0 disp1 disp2 again ;
