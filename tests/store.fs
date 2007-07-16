variable a
0x200 constant b
variable c

: test1 3 a ! ;
: test2 0xff00 a ! ;
: test3 RCREG c@ dup a c! ;
: test4 3 b ! ;
: test5 0xff00 b ! ;
: test6 RCREG c@ dup b c! ;
: test7 TMR1L @ TMR0L ! ;
: test8 a c@ c ! ;
: test9 a c@ dup c ! ;
: test10 a c@ c c@ 2>1 c ! ;

: main test1 test2 test3 test4 test5 test6 test7 test8 test9 test10 ;
