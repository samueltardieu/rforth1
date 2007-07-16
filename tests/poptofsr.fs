variable a

: test1 a @ c@ ;
: test2 a @ @ ;
: test3 3 a @ c! ;
: test4 4 a @ ! ;

: main test1 test2 test3 test4 ;
