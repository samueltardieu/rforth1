needs lib/tty-rs232.fs
cvariable a
: foo ci . cr ; inline
: h ." Running test " type cr ;
: test1 s" test1" h 0 cfor foo cnext ;
: test2 s" test2" h 1 0 cfor foo cnext drop ;
: test3 s" test3" h 4 cfor foo cnext ;
: test4 s" test4" h 1 4 cfor foo cnext drop ;
: test5 s" test5" h cfor foo cnext ;
: test6 s" test6" h cfor foo cnext drop ;
: test7 s" test7" h a c@ cfor foo cnext ;
: test8 s" test8" h a c@ cfor foo cnext drop ;
: main
  cr .s cr
  test1 .s cr test2 .s cr test3 .s cr test4 4 test5 .s cr 1 4 test6 .s cr
  0 a c! test7 .s cr 1 test8 .s cr
  4 a c! test7 .s cr 1 test8 .s cr
;
