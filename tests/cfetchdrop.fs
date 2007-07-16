cvariable a
variable b
: test1 3 drop ;
: test2 dup drop ;
: test3 a c@ drop ;
: test4 RCREG c@ drop ;
: test5 b @ drop ;
: main test1 test2 test3 test4 test5 ;
