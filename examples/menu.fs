needs lib/tty-rs232.fs

: star ( -- ) 42 emit ;
: stars ( n -- ) cfor star cnext ;
: spaces ( n -- ) cfor space cnext ;
: missing ( n -- ) 76 swap - ;
: left ( n -- ) missing 2/ ;
: right ( n -- ) dup missing swap left - ;
: center ( addr n -- ) >r star r@ left spaces r@ type r@ right spaces star cr ;
: hr ( -- ) 78 stars cr ;
: empty ( -- ) star 76 spaces star cr ;

: banner ( -- )
  hr empty s" Welcome to the SForth test program" center empty hr ;

: main-menu-choice ( -- )
  key
  dup [char] 1 = if drop ." Forth is cool" cr exit then
  dup [char] 2 = if drop ." We will be ready for the cup" cr exit then
  dup [char] 3 = if reset then
  ." Well, aren't you able to read? What does `" emit ." ' mean?" cr
  ;

: main-menu ( -- )
  hr empty
  s" 1 - Print something intelligent" center
  s" 2 - Print something stupid" center
  s" 3 - Reset" center
  empty hr
  main-menu-choice cr recurse ;

: main ( -- ) cr banner cr main-menu ;
