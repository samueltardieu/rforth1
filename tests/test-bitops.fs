needs lib/tty-rs232.fs

variable x
x 3 bit t1
x 5 bit t2

: test-set ( a b -- ) bit-set? if ." set" else ." not set" then ;
: test-clr ( a b -- ) bit-clr? if ." clr" else ." not clr" then ;

: set ( a b -- ) bit-set ;
: clr ( a b -- ) bit-clr ;
: toggle ( a b -- ) bit-toggle ;

: main
  cr
  ." Current depth: " depth . cr
  t1 bit-set t2 bit-clr
  t1 test-set space t1 test-clr cr
  t2 test-set space t2 test-clr cr
  t1 clr t2 set
  t1 test-set space t1 test-clr cr
  t2 test-set space t2 test-clr cr
  t1 toggle t2 toggle
  t1 test-set space t1 test-clr cr
  t2 test-set space t2 test-clr cr
  ." Current depth: " depth . cr
;
