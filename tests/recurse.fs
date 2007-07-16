: foo recurse recurse ;
: bar foo foo ;

: foo2 recurse recurse ; inline
: bar2 foo2 foo2 ;

: fact dup 1 >= if dup 1- recurse * then ;

: fact2 dup 1 >= if dup 1- recurse * then ; inline

: main bar bar2 fact2 fact ;
