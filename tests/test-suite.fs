needs lib/tty-rs232.fs

variable x

: greetings ( -- ) ." Starting\n" ;

: test-abs ( x -- )
  ." Testing abs for " dup . ." : " abs . cr ;

: test-plus ( -- )
  ." Testing 0x103 + 0x1234 (0x1337): " 0x103 0x1234 + . cr ;

: test-minus ( -- )
  ." Testing 0x1337 - 0x1234 (0x103): " 0x1337 0x1234 - . cr ;

: test-plus! ( -- )
  ." Adding 0x1234 to 0x103 in memory: " 0x103 x ! 0x1234 x +! x @ . cr ;

: test-minus! ( -- )
  ." Subtrating 0x1234 from 0x1337 in memory: "
   0x1337 x ! 0x1234 x -! x @ . cr ;

: cf ( x -- x ) dup if ."  -- ERROR" then ;
: ct ( x -- x ) dup 0= if ."  -- ERROR" then ;

: test-bool ( -- )
  ." 1 2 < : " 1 2 < ct . cr
  ." 1 2 <= : " 1 2 <= ct . cr
  ." 1 2 > : " 1 2 > cf . cr
  ." 1 2 >= : " 1 2 >= cf . cr
  ." 1 2 = : " 1 2 = cf . cr
  ." 1 2 <> : " 1 2 <> ct . cr
  ." 1 1 < : " 1 1 < cf . cr
  ." 1 1 <= : " 1 1 <= ct . cr
  ." 1 1 > : " 1 1 > cf . cr
  ." 1 1 >= : " 1 1 >= ct . cr
  ." 1 1 = : " 1 1 = ct . cr
  ." 1 1 <> : " 1 1 <> cf . cr
  ." 1 0= : " 1 0= cf . cr
  ." 1 0< : " 1 0< cf . cr
  ." 1 0> : " 1 0> ct . cr
  ." 1 0<= : " 1 0<= cf . cr
  ." 1 0>= : " 1 0>= ct . cr
  ." -1 0= : " -1 0= cf . cr
  ." -1 0< : " -1 0< ct . cr
  ." -1 0> : " -1 0> cf . cr
  ." -1 0<= : " -1 0<= ct . cr
  ." -1 0>= : " -1 0>= cf . cr
  ." 0 0= : " 0 0= ct . cr
  ." 0 0< : " 0 0< cf . cr
  ." 0 0> : " 0 0> cf . cr
  ." 0 0<= : " 0 0<= ct . cr
  ." 0 0>= : " 0 0>= ct . cr
;

: test-swap ( -- )
  ." Should print 1234 5678: " 0x1234 0x5678 swap . space . cr ;

: test-2* ( -- )
  ." 1234 2* : " 0x1234 2* . cr ;

: test-2/ ( -- )
  ." 2468 2/ : " 0x2468 2/ . cr
  ." FFF0 2/ : " 0xFFF0 2/ . cr
;

: test-mult ( -- )
  ." 75 3A * (1A82): " 0x75 0x3A * . cr ;

: test-div ( -- )
  ." 1000 PI * (0C45): " 1000 355 113 */ . cr ;

: main ( -- )
  greetings
  .s cr
  10 test-abs -10 test-abs 0 test-abs
  .s cr
  test-plus
  .s cr
  test-minus
  .s cr
  test-plus!
  .s cr
  test-minus!
  .s cr
  test-bool
  .s cr
  test-swap
  .s cr
  test-2*
  .s cr
  test-2/
  .s cr
  test-mult
  .s cr
  test-div
  .s cr
;
