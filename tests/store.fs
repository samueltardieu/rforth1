variable a
0x200 constant b

: test1 3 a ! ;
: test2 0xff00 a ! ;
: test3 RCREG c@ dup a c! ;
: test4 3 b ! ;
: test5 0xff00 b ! ;
: test6 RCREG c@ dup b c! ;

: main test1 test2 test3 test4 test5 test6 ;
